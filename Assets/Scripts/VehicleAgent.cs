using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;
using Unity.MLAgents.Actuators;

public class VehicleAgent : Agent 
{
    [Header("Configurações do Raycast")]
    private VehicleController vehicleController;
    [SerializeField] private Transform raycastPoint;
    [SerializeField] private float rayDistance = 5f;
    [SerializeField] private LayerMask hitMask;

    [Header("Env")]
    [SerializeField] private int nextCheckpointIndex;
    private Vector3 oriPos;
    private Vector3 lastPosition;
    private float idlePenaltyTimer = 0f;
    private float timerAlive = 0f;

    [SerializeField] private float timerAliveWeight = 0.005f, idlePenaltyTimerWeight = -0.01f,
        wallCollisiontWeight = -6f, checkpointAchieveWeight = 3f, alignmentRewardWeight = 0.01f;

    private void Start() 
    {
        vehicleController = GetComponent<VehicleController>();
        oriPos = transform.localPosition;
    }

    /// <summary>
    /// Quando uma colisião (não física) ocorre
    /// </summary>
    /// <param name="other">O objeto que colidiu</param>
    private void OnTriggerEnter2D(Collider2D other)
    {
        List<Transform> checkpoints = VehicleManager.Instance.Checkpoints;

        if (other.CompareTag("Checkpoint"))
        {
            int checkpointIndex = checkpoints.IndexOf(other.transform);

            if (checkpointIndex == nextCheckpointIndex)
            {
                AddReward(checkpointAchieveWeight);
                nextCheckpointIndex = (nextCheckpointIndex + 1) % checkpoints.Count;
            }
            else
            {
                // Penalizar checkpoints fora de ordem
                int checkpointCount = checkpoints.Count;

                // Diferença cíclica: calcula o menor caminho entre os índices
                int forwardDifference = (checkpointIndex - nextCheckpointIndex + checkpointCount) % checkpointCount;
                int backwardDifference = (nextCheckpointIndex - checkpointIndex + checkpointCount) % checkpointCount;

                int smallestDifference = Mathf.Min(forwardDifference, backwardDifference);

                // Penaliza proporcional à menor diferença
                AddReward(-smallestDifference * 2f);
                
            }
        }
    }

    /// <summary>
    /// Quando uma colisão ocorre
    /// </summary>
    /// <param name="other">O objeto que colidiu</param>
    private void OnCollisionEnter2D(Collision2D other) 
    {
        if(other.gameObject.CompareTag("Wall"))
        {
            AddReward(wallCollisiontWeight);
            EndEpisode();
        }
    }

    /// <summary>
    /// Chamado toda vez que um novo episódio começa (seja pelo inicio ou quando o carro colidi)
    /// </summary>
    public override void OnEpisodeBegin()
    {
        nextCheckpointIndex = 0;
        idlePenaltyTimer = 0f;
        timerAlive = 0f;
        (float xMin, float xMax) = VehicleManager.Instance.XRange;
        (float yMin, float yMax) = VehicleManager.Instance.YRange;
        transform.localPosition = new(Random.Range(xMin, xMax), Random.Range(yMin, yMax));
        transform.eulerAngles = Vector3.zero;
        transform.localEulerAngles = new Vector3(0, 0, 270f);
        lastPosition = transform.localPosition;
        GetComponent<Rigidbody2D>().velocity = Vector3.zero;
        GetComponent<Rigidbody2D>().angularVelocity = 0;
    }

    /// <summary>
    /// Coleta todas as observações necessárias a todo frame
    /// </summary>
    /// <param name="sensor">Aqui é deve ser armazenado todas as observações que a AI precisa para ser treinada</param>
    public override void CollectObservations(VectorSensor sensor)
    {
        Transform nextCheckpoint = VehicleManager.Instance.Checkpoints[nextCheckpointIndex];
        Vector2 directionToCheckpoint = (nextCheckpoint.position - transform.position).normalized;
        sensor.AddObservation(directionToCheckpoint);

        float distanceToCheckpoint = Vector2.Distance(transform.position, nextCheckpoint.position);
        sensor.AddObservation(distanceToCheckpoint);

        Vector2[] directions = {
            raycastPoint.up, // Frente
            -raycastPoint.up, // Trás
            raycastPoint.right, // Direita
            -raycastPoint.right, // Esquerda
            (raycastPoint.up + raycastPoint.right).normalized, // Frente-Direita
            (raycastPoint.up - raycastPoint.right).normalized, // Frente-Esquerda
            (-raycastPoint.up + raycastPoint.right).normalized, // Trás-Direita
            (-raycastPoint.up - raycastPoint.right).normalized, // Trás-Esquerda
        };

        foreach (var direction in directions)
        {
            RaycastHit2D hit = Physics2D.Raycast(raycastPoint.position, direction, rayDistance, hitMask);   
            float normalizedDistance = hit.collider ? Mathf.Clamp01(hit.distance / rayDistance) : 1f;
            sensor.AddObservation(normalizedDistance); // Observa distância normalizada
            Debug.DrawRay(raycastPoint.position, direction * (hit.collider ? hit.distance : rayDistance), hit.collider ? Color.red : Color.green);
        }

        Vector2 velocity = GetComponent<Rigidbody2D>().velocity;
        sensor.AddObservation(velocity);

        float angleToCheckpoint = Vector2.SignedAngle(transform.up, directionToCheckpoint);
        sensor.AddObservation(angleToCheckpoint / 180f);
        
        float alignmentReward = Vector2.Dot(transform.up, directionToCheckpoint) * alignmentRewardWeight;
        AddReward(alignmentReward);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Penalidade por ficar parado
        float distanceMoved = Vector3.Distance(transform.position, lastPosition);
        if (distanceMoved < 0.01f) // Limite para detectar movimento
        {
            idlePenaltyTimer += Time.deltaTime;
            if (idlePenaltyTimer > 1f) // Penaliza a cada 1 segundo parado
            {
                AddReward(idlePenaltyTimerWeight); // Penalidade pequena mas constante
            }
        }
        else idlePenaltyTimer = 0f; // Reseta o contador se estiver se movendo
        
        lastPosition = transform.position;
        vehicleController.ApplyVerticalInput(actions.ContinuousActions[0]);
        vehicleController.ApplyHorizontalInput(actions.ContinuousActions[1]);
    }

    /// <summary>
    /// É responsável por lidar com os inputs providos pelo jogador
    /// </summary>
    /// <param name="actionsOut">As ações a serem computadas pela AI</param>
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        actionsOut.ContinuousActions.Array[0] = Input.GetAxis("Vertical");
        actionsOut.ContinuousActions.Array[1] = Input.GetAxis("Horizontal");
    }

    /// <summary>
    /// Chamado a cada frame do jogo
    /// </summary>
    private void Update()
    {
        timerAlive += Time.deltaTime;
        if(timerAlive >= 1f)
        {
            timerAlive = 0;
            AddReward(timerAliveWeight);
        }
    }
    
}

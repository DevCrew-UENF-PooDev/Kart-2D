using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;
using Unity.MLAgents.Actuators;

public class VehicleAgent : Agent 
{
    [Header("Configurações do Raycast")]
    private VehicleController vehicleController; // Script Responsavel pelo movimento e comportamento do carro
    [SerializeField] private Transform raycastPoint; // Ponto central localizado no ponto médio do carro e a partir dele que o raycast será desenhado
    [SerializeField] private float rayDistance = 10f; // A distância máxima que o raio do raycast poderá trafegar
    [SerializeField] private LayerMask hitMask; // A camada da parede a ser identificada pelo raycast

    [Header("Env")]
    [SerializeField] private int nextCheckpointIndex; // O index do próximo checkpoint a ser procurado
    private Vector3 oriPos; // Não está sendo usada, mas é a posição inicial do carro
    private Vector3 lastPosition; // A última posição tomada pelo carro em dado tempo, usada para verificar se houve variação de posição e penalizar se o carro estiver parado ou não
    private float idlePenaltyTimer = 0f; // Timer incremental de acordo com o tempo parado do carro
    private float timerAlive = 0f; // Timer incremental  pelo tempo que o agente ficou vivo.
    private float timeSinceLastCheckpoint = 0f; // Timer incremental pelo tempo que o agente/carro ficou sem tocar em um checkpoint

    // Tempo máximo de espera entre um checkpoint a outro e a distância minima que um dos raycast e consequetemente o carro tem que ficar da parede
    [SerializeField] private float checkpointTimeLimit = 2f, safeDistanceFromWall = 1f;

    // Variáveis de peso de cada recompensa e o quanto elas influeciam no carro.
    [SerializeField] private float timerAliveWeight = 0.005f, idlePenaltyTimerWeight = -0.01f,
        wallCollisiontWeight = -6f, checkpointAchieveWeight = 3f, alignmentRewardWeight = 0.01f, checkpointPenaltyWeight = -5f, wallDistancePenalty = 0.1f;

    // Getter do Next Checkpoint Index, chamado para ver o carro mais avançado na pista no VehicleManager e consequetemente CameraFollow
    public int NextCheckpointIndex => nextCheckpointIndex;

    /// <summary>
    /// Chamado assim que o gameobject é carregado, pega o script do controle do veiculo no gameobject e pega a posição inicial do mesmo
    /// </summary>
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
        List<Transform> checkpoints = VehicleManager.Instance.Checkpoints; // Uma lista com todos os checkpoints

        if (other.CompareTag("Checkpoint"))
        {
            int checkpointIndex = checkpoints.IndexOf(other.transform); // Index do checkpoint ao qual colidiu

            if (checkpointIndex == nextCheckpointIndex) // Se for igual recompensar e ir botar o próximo como objetivo
            {
                AddReward(checkpointAchieveWeight);
                nextCheckpointIndex = (nextCheckpointIndex + 1) % checkpoints.Count;
                timeSinceLastCheckpoint = 0f; // Reseta o contador ao atingir o checkpoint.
            }
            else // Se não penaliza
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
        if(other.gameObject.CompareTag("Wall")) // Se bateu na parede penaliza e encerra o episodio
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
        // Resetando posição, tempo, velocidade e rotação tudo de volta ao padrão.
        nextCheckpointIndex = 0;
        idlePenaltyTimer = 0f;
        timerAlive = 0f;
        timeSinceLastCheckpoint = 0f;
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
        // Adiciona a direção normalizada do agente até o próximo checkpoint como entrada do sensor
        Transform nextCheckpoint = VehicleManager.Instance.Checkpoints[nextCheckpointIndex];
        Vector2 directionToCheckpoint = (nextCheckpoint.position - transform.position).normalized;
        sensor.AddObservation(directionToCheckpoint); // 2 valores (x e y)

        // Distância/Magnitude do agente até o próximo checkpoint
        float distanceToCheckpoint = Vector2.Distance(transform.position, nextCheckpoint.position);
        sensor.AddObservation(distanceToCheckpoint); // 1 valor

        Vector2[] directions = { // Todas as direções do raycast que detecta a parede
            raycastPoint.up, // Frente
            -raycastPoint.up, // Trás
            raycastPoint.right, // Direita
            -raycastPoint.right, // Esquerda
            (raycastPoint.up + raycastPoint.right).normalized, // Frente-Direita
            (raycastPoint.up - raycastPoint.right).normalized, // Frente-Esquerda
            (-raycastPoint.up + raycastPoint.right).normalized, // Trás-Direita
            (-raycastPoint.up - raycastPoint.right).normalized, // Trás-Esquerda
            (raycastPoint.up * 0.85f + raycastPoint.right * 0.15f).normalized, // Frente-Leve-Direita
            (raycastPoint.up * 0.85f - raycastPoint.right * 0.15f).normalized, // Frente-Leve-Esquerda
            (-raycastPoint.up * 0.85f + raycastPoint.right * 0.15f).normalized, // Trás-Leve-Direita
            (-raycastPoint.up * 0.85f - raycastPoint.right * 0.15f).normalized,  // Trás-Leve-Esquerda
            (raycastPoint.up * 0.6f + raycastPoint.right * 0.4f).normalized, // Frente-Moderada-Direita
            (raycastPoint.up * 0.6f - raycastPoint.right * 0.4f).normalized, // Frente-Moderada-Esquerda
            (-raycastPoint.up * 0.6f + raycastPoint.right * 0.4f).normalized, // Trás-Moderada-Direita
            (-raycastPoint.up * 0.6f - raycastPoint.right * 0.4f).normalized  // Trás-Moderada-Esquerda
        };

        foreach (var direction in directions)
        {
            RaycastHit2D hit = Physics2D.Raycast(raycastPoint.position, direction, rayDistance, hitMask);
            float normalizedDistance = hit.collider ? Mathf.Clamp01(hit.distance / rayDistance) : 1f;
            sensor.AddObservation(normalizedDistance); // Adiciona distância normalizada como entrada do sensor * 16 (pois são 16 direções)

            // Penalidade por proximidade excessiva
            if (hit.collider && hit.distance < safeDistanceFromWall)
            {
                float proximityPenalty = (safeDistanceFromWall - hit.distance) * 0.1f; // Penalidade proporcional.
                AddReward(-proximityPenalty); //Adiciona como recompensa negativa o fator de proximidade
            }

            Debug.DrawRay(raycastPoint.position, direction * (hit.collider ? hit.distance : rayDistance), hit.collider ? Color.red : Color.green); // Desenha o raycast na tela como debug (apenas para visualização do desenvolvidor)
        }

        Vector2 velocity = GetComponent<Rigidbody2D>().velocity;
        sensor.AddObservation(velocity); // 1 valor a velocidade do carro

        float angleToCheckpoint = Vector2.SignedAngle(transform.up, directionToCheckpoint);
        sensor.AddObservation(angleToCheckpoint / 180f); // 1 valor ângulo entre o carro e o próximo checkpoint
        
        float alignmentReward = Vector2.Dot(transform.up, directionToCheckpoint) * alignmentRewardWeight; // Fator de alinhamento em relação ao carro e o checkpoint
        AddReward(alignmentReward); // 1 valor
    }

    /// <summary>
    /// A cada frame do decision request, calcula a próxima ação a ser tomada pelo agente
    /// </summary>
    /// <param name="actions">As ações retornadas e computadas pela rede neural</param>
    public override void OnActionReceived(ActionBuffers actions)
    {
        // Incrementao os dois tempos
        timeSinceLastCheckpoint += Time.deltaTime;
        timerAlive += Time.deltaTime;
        if(timerAlive >= 1f) // Se ficou 1s vivo adiciona recompensa
        {
            timerAlive = 0;
            AddReward(timerAliveWeight);
        }
        if (timeSinceLastCheckpoint > checkpointTimeLimit)
        {
            AddReward(checkpointPenaltyWeight); // Penalidade por demorar muito.
            timeSinceLastCheckpoint = 0f; // Reseta o contador para evitar penalizar repetidamente.
        }
        // Penalidade por ficar parado
        float distanceMoved = Vector2.Distance(transform.position, lastPosition);
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
        // Como o input vem entre -1 e 1, para garantir que só acelera ou "freie"
        float verticalInputD = (actions.ContinuousActions[0] + 1) / 2f;
        vehicleController.ApplyVerticalInput(verticalInputD);
        vehicleController.ApplyHorizontalInput(actions.ContinuousActions[1]);
    }

    /// <summary>
    /// É responsável por lidar com os inputs providos pelo jogador
    /// </summary>
    /// <param name="actionsOut">As ações a serem computadas pela AI</param>
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        actionsOut.ContinuousActions.Array[0] = Input.GetAxis("Vertical"); // Pega o input do WS e As setas cima e baixo
        actionsOut.ContinuousActions.Array[1] = Input.GetAxis("Horizontal"); // Pega o input do AD e As setas esquerda e direita
    }
}

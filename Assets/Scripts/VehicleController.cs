using UnityEngine;

public class VehicleController : MonoBehaviour
{
    // Configurações Gerais
    [Header("Configurações Gerais")]
    public float maxSpeed = 10f; // Velocidade máxima
    public float acceleration = 5f; // Força de aceleração
    public float brakingForce = 10f; // Força de frenagem
    public float turnSpeed = 200f; // Velocidade de rotação

    // Configurações de Drift
    [Header("Configurações de Drift")]
    public bool driftEnabled = true;
    public float driftFactor = 0.9f; // Controle de derrapagem

    // Tipos de veículo
    [Header("Tipos de Veículo")]
    public bool isRallyCar = false;
    public bool isTruck = false;
    public bool isTank = false;

    private Rigidbody2D rb;

    void Start()
    {
        // Inicializa o Rigidbody2D
        rb = GetComponent<Rigidbody2D>();

        // Configura as propriedades específicas do tipo de veículo
        ConfigureVehicleType();
    }

    void FixedUpdate()
    {
        HandleMovement();
        HandleSteering();
        ApplyDriftLogic();
    }

    void HandleMovement()
    {
        // Entrada do jogador (Vertical para aceleração/frenagem)
        float verticalInput = Input.GetAxis("Vertical");

        // Aceleração
        if (verticalInput > 0)
        {
            rb.AddForce(transform.up * verticalInput * acceleration);
        }
        // Frenagem
        else if (verticalInput < 0)
        {
            rb.AddForce(transform.up * verticalInput * brakingForce);
        }

        // Limita a velocidade máxima
        if (rb.velocity.magnitude > maxSpeed)
        {
            rb.velocity = rb.velocity.normalized * maxSpeed;
        }
    }

    void HandleSteering()
    {
        // Entrada de direção (Horizontal)
        float horizontalInput = Input.GetAxis("Horizontal");

        // Rotaciona o veículo
        float rotationAmount = -horizontalInput * turnSpeed * Time.fixedDeltaTime;
        rb.MoveRotation(rb.rotation + rotationAmount);
    }

    void ApplyDriftLogic()
    {
        if (!driftEnabled) return;

        // Reduz a velocidade lateral para simular aderência
        Vector2 forwardVelocity = transform.up * Vector2.Dot(rb.velocity, transform.up);
        Vector2 lateralVelocity = transform.right * Vector2.Dot(rb.velocity, transform.right);
        rb.velocity = forwardVelocity + lateralVelocity * driftFactor;
    }

    void ConfigureVehicleType()
    {
        // Configurações baseadas no tipo de veículo
        if (isRallyCar)
        {
            maxSpeed = 15f;
            acceleration = 8f;
            brakingForce = 5f;
            driftFactor = 0.85f;
        }
        else if (isTruck)
        {
            maxSpeed = 8f;
            acceleration = 4f;
            brakingForce = 10f;
            driftFactor = 0.95f;
        }
        else if (isTank)
        {
            maxSpeed = 6f;
            acceleration = 3f;
            brakingForce = 15f;
            turnSpeed = 100f;
            driftFactor = 0.98f;
        }
    }
}

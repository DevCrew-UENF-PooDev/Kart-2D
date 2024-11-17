using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VehicleAgent : MonoBehaviour
{
    [Header("Configurações do Raycast")]
    private VehicleController vehicleController;
    [SerializeField] private float rayDistance = 5f;
    [SerializeField] private LayerMask hitMask;

    private Vector2[] directions = {
        Vector2.up,
        Vector2.down,
        Vector2.left,
        Vector2.right,
        new Vector2(1, 1).normalized,
        new Vector2(1, -1).normalized,
        new Vector2(-1, 1).normalized,
        new Vector2(-1, -1).normalized
    };


    private void Awake() 
    {
        vehicleController = GetComponent<VehicleController>();
    }

    private void Update()
    {
        PerformRaycasts();
    }

    void PerformRaycasts()
    {
        Vector2[] directions = {
            transform.up, // Frente
            -transform.up, // Trás
            transform.right, // Direita
            -transform.right, // Esquerda
            (transform.up + transform.right).normalized, // Frente-Direita
            (transform.up - transform.right).normalized, // Frente-Esquerda
            (-transform.up + transform.right).normalized, // Trás-Direita
            (-transform.up - transform.right).normalized, // Trás-Esquerda
        };
        foreach (var direction in directions)
        {
            RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, rayDistance, hitMask);
            
            // Mostra o raio no editor
            

            // Exibe a distância do objeto atingido
            if (hit.collider)
            {
                Debug.DrawRay(transform.position, direction * hit.distance, hit.collider ? Color.red : Color.green);

                // Debug.Log($"Direção: {direction}, Distância: {hit.distance}, Objeto: {hit.collider.name}");
            } else {
                Debug.DrawRay(transform.position, direction * rayDistance, hit.collider ? Color.red : Color.green);
            }
        }
    }
}

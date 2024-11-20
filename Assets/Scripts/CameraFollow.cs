using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Configurações da Câmera")]
    public Transform target; // Objeto a ser seguido (o veículo)
    public float smoothSpeed = 0.125f; // Velocidade de suavização
    public Vector3 offset; // Offset da câmera em relação ao veículo

    void LateUpdate()
    {
        target = VehicleManager.Instance.PickBestAgent().transform;
        if (target == null) return; // Garante que há um alvo definido

        // Posição desejada com o offset
        Vector3 desiredPosition = target.position + offset;

        // Suaviza a transição da posição da câmera
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);

        // Aplica a nova posição à câmera
        transform.position = smoothedPosition;
    }
}

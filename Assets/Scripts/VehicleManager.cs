using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class VehicleManager : MonoBehaviour
{
    public static VehicleManager Instance {get; private set;}
    [Header("Agents ENV")]
    [SerializeField] private int agentToInspectIndex = 0;
    [SerializeField] private List<VehicleAgent> agents;
    [SerializeField] private TextMeshProUGUI mainInfoText;
    [SerializeField] private float xMin, xMax, yMin, yMax;

    [SerializeField] private Transform checkpointsHolder;
    public List<Transform> Checkpoints;

    public (float min, float max) XRange => (xMin, xMax);
    public (float min, float max) YRange => (yMin, yMax);

    private void Awake() 
    {
        if(Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        Checkpoints.AddRange(checkpointsHolder.GetComponentsInChildren<Transform>());
        Checkpoints.Remove(checkpointsHolder);
    }

    private void Update() 
    {
        if (agents[agentToInspectIndex])
        {
            mainInfoText.SetText("Index: " + agentToInspectIndex + "\nRecompensa: " + agents[agentToInspectIndex].GetCumulativeReward());
        }
        else mainInfoText.SetText("Agente não existe");
    }
}

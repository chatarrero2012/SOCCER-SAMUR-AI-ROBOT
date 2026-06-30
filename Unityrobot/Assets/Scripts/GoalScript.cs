using UnityEngine;

public class GoalScript : MonoBehaviour
{
    [Header("Target Agent")]
    [Tooltip("Arrastra aquí el GameObject del Agente (no el script directamente)")]
    public GameObject agentObject; 
    
    [Header("Goal Type")]
    public bool isEnemy;

    // La referencia al protocolo (Polimorfismo puro)
    private IGoalScorer _agentProtocol;

    private void Awake()
    {
        // 🔍 RESOLUCIÓN DE PROTOCOLO
        // Buscamos cualquier componente en el GameObject que implemente IGoalScorer
        if (agentObject != null)
        {
            _agentProtocol = agentObject.GetComponent<IGoalScorer>();
            
            if (_agentProtocol == null)
            {
                Debug.LogError($"[GoalScript] ¡Error! El objeto '{agentObject.name}' no implementa el protocolo IGoalScorer.");
            }
        }
        else
        {
            Debug.LogWarning("[GoalScript] No se ha asignado ningún agentObject en el Inspector.");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Ball") && _agentProtocol != null)
        {
            Debug.Log("goal touched");
            
            // 🪄 POLIMORFISMO: Llamamos al método del protocolo. 
            // No nos importa si es el Agente Conductista o el Competency.
            if (!isEnemy) 
            {
                _agentProtocol.OnGoalScored();
            } 
            else 
            {
                _agentProtocol.OnOwnGoal();
            }
        }
    }
}
/// <summary>
/// PROTOCOLO DE ANOTACIÓN (Equivalente a Swift Protocol)
/// Cualquier agente o entidad que pueda interactuar con las porterías 
/// debe implementar este contrato.
/// </summary>
public interface IGoalScorer
{
    void OnGoalScored();
    void OnOwnGoal();
}
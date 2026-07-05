using UnityEngine;

/// <summary>
/// Mueve el objeto de forma aleatoria sobre el eje X de la cancha.
/// Ideal para añadir ruido geo-espacial a los saques de esquina y laterales en ML-Agents.
/// </summary>
public class CornerPoint : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("El transform de la cancha para usarlo como plano de referencia.")]
    public Transform cancha;

    [Header("Configuración del Rango (Eje X)")]
    [Tooltip("Porcentaje del semieje X de la cancha que se usará. 0.5 = se mueve entre el 50% izquierdo y derecho.")]
    [Range(0f, 1f)]
    public float factorAncho = 0.45f;

    [Tooltip("Activa esto si prefieres definir el límite en metros reales en lugar de usar la escala de la cancha.")]
    public bool usarAnchoManual = false;
    [Tooltip("Distancia máxima desde el centro de la cancha en metros (±X).")]
    public float anchoManualX = 12f;

    [ContextMenu("¡Aleatorizar Eje X Ahora!")]
    public void AleatorizarCorner()
    {
     AleatorizarEjeX();
    }

    /// <summary>
    /// Calcula y aplica una nueva posición aleatoria en el eje X de la cancha,
    /// preservando las alturas (Y) y distancias de banda/fondo (Z) originales.
    /// </summary>
    public void AleatorizarEjeX()
    {
        // Fail-safe: si no se asignó en el inspector, intenta buscar al padre
        if (cancha == null)
        {
            cancha = transform.parent;
            if (cancha == null)
            {
                Debug.LogError($"[CornerPoint] No se ha asignado la referencia de la 'Cancha' en {gameObject.name}.");
                return;
            }
        }

        // 1. Determinar el radio máximo de movimiento en X
        float limiteX = usarAnchoManual ? anchoManualX : (cancha.localScale.x * 0.5f * factorAncho);

        // 2. Generar el offset aleatorio en el carril
        float xAleatorio = Random.Range(-limiteX, limiteX);

        // 3. Aplicar la posición respetando la orientación y rotación de la cancha (Crucial para ML-Agents paralelo)
        if (transform.parent == cancha)
        {
            // Optimización directa si el objeto es hijo de la cancha
            Vector3 posLocal = transform.localPosition;
            posLocal.x = xAleatorio;
            transform.localPosition = posLocal;
        }
        else
        {
            // Si el objeto está fuera de la jerarquía, transformamos el espacio para no romper la física rotada
            Vector3 posRelativaEnCancha = cancha.InverseTransformPoint(transform.position);
            
            // Modificamos únicamente su X local respecto a la cancha
            posRelativaEnCancha.x = xAleatorio;
            
            // Devolvemos el objeto al espacio global
            transform.position = cancha.TransformPoint(posRelativaEnCancha);
        }
    }
}
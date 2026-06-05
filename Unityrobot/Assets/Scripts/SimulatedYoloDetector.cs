using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simulates YOLO-style object detection using
/// projected renderer bounds.
///
/// Designed for:
/// - ML-Agents
/// - Sim-to-real
/// - Smartphone robot vision
/// </summary>
public class SimulatedYoloDetector : MonoBehaviour
{
    // =====================================================
    // DATA STRUCTURE
    // =====================================================

    [System.Serializable]
    public class Detection
    {
        public Transform target;

        public Rect screenRect;

        public Vector2 normalizedCenter;

        public Vector2 normalizedSize;

        public float distance;
    }

    // =====================================================
    // INSPECTOR
    // =====================================================

    [Header("Camera")]

    public Camera detectionCamera;

    [Header("Detection")]

    public List<Transform> targets =
        new List<Transform>();

    public LayerMask occlusionMask;

    public bool requireLineOfSight = false;

    [Header("GUI")]

    public bool drawGUI = true;

    public Color guiColor = Color.green;

    public float lineThickness = 2f;

    // =====================================================
    // OUTPUT
    // =====================================================

    public List<Detection> detections =
        new List<Detection>();

    // =====================================================
    // UNITY
    // =====================================================

    private void LateUpdate()
    {
        UpdateDetections();
    }

    // =====================================================
    // DETECTION
    // =====================================================

    private void UpdateDetections()
    {
        detections.Clear();

        foreach (Transform target in targets)
        {
            if (target == null)
                continue;


                    // ... dentro del foreach (Transform target in targets)

        Bounds combinedBounds;
        
        // --- INICIO DEL CAMBIO ---
        // Priorizamos el Collider si existe (ideal para la bola)
        SphereCollider sphereCol = target.GetComponent<SphereCollider>();
        
        if (sphereCol != null)
        {
            // Usamos el centro y radio del collider, que es matemáticamente perfecto para esferas
            Vector3 centerColl = target.position + sphereCol.center; // Considera el offset local
            float radius = sphereCol.radius * target.lossyScale.x; // Ajusta por escala global
            
            // Creamos un bounds manual basado en la esfera
            combinedBounds = new Bounds(centerColl, Vector3.one * radius * 2f);
        }
        else
        {
            // Fallback original para robots, arcos y objetos sin collider esférico
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            
            if (renderers.Length == 0)
                continue;

            combinedBounds = renderers[0].bounds;
            foreach (Renderer r in renderers)
            {
                combinedBounds.Encapsulate(r.bounds);
            }
        }
        // --- FIN DEL CAMBIO ---

        Vector3 center = combinedBounds.center;
        
        // ... resto del código (Line of Sight, corners, etc.)



            // -------------------------------------------------
            // OPTIONAL LOS
            // -------------------------------------------------

            if (requireLineOfSight)
            {
                Vector3 dir =
                    center -
                    detectionCamera.transform.position;

                if (Physics.Raycast(
                    detectionCamera.transform.position,
                    dir.normalized,
                    out RaycastHit hit,
                    dir.magnitude,
                    occlusionMask))
                {
                    if (!hit.transform.IsChildOf(target))
                        continue;
                }
            }

            // -------------------------------------------------
            // GET 8 CORNERS
            // -------------------------------------------------

            Vector3 extents = combinedBounds.extents;

            Vector3[] corners = new Vector3[8];

            corners[0] = center + new Vector3(-extents.x, -extents.y, -extents.z);
            corners[1] = center + new Vector3(-extents.x, -extents.y,  extents.z);
            corners[2] = center + new Vector3(-extents.x,  extents.y, -extents.z);
            corners[3] = center + new Vector3(-extents.x,  extents.y,  extents.z);

            corners[4] = center + new Vector3( extents.x, -extents.y, -extents.z);
            corners[5] = center + new Vector3( extents.x, -extents.y,  extents.z);
            corners[6] = center + new Vector3( extents.x,  extents.y, -extents.z);
            corners[7] = center + new Vector3( extents.x,  extents.y,  extents.z);

            // -------------------------------------------------
            // PROJECT TO SCREEN
            // -------------------------------------------------

            bool anyVisible = false;

            float minX = float.MaxValue;
            float minY = float.MaxValue;

            float maxX = float.MinValue;
            float maxY = float.MinValue;

            foreach (Vector3 c in corners)
            {
                Vector3 sp =
                    detectionCamera.WorldToScreenPoint(c);

                if (sp.z <= 0)
                    continue;

                anyVisible = true;

                minX = Mathf.Min(minX, sp.x);
                minY = Mathf.Min(minY, sp.y);

                maxX = Mathf.Max(maxX, sp.x);
                maxY = Mathf.Max(maxY, sp.y);
            }

            if (!anyVisible)
                continue;

            // -------------------------------------------------
            // CLAMP TO SCREEN
            // -------------------------------------------------

            minX = Mathf.Clamp(minX, 0, Screen.width);
            maxX = Mathf.Clamp(maxX, 0, Screen.width);

            minY = Mathf.Clamp(minY, 0, Screen.height);
            maxY = Mathf.Clamp(maxY, 0, Screen.height);

            float width = maxX - minX;
            float height = maxY - minY;

            if (width <= 1 || height <= 1)
                continue;

            Rect rect = new Rect(
                minX,
                Screen.height - maxY,
                width,
                height
            );

            // -------------------------------------------------
            // NORMALIZED VALUES
            // -------------------------------------------------

            Vector2 centerNorm = new Vector2(
                (rect.x + rect.width * 0.5f) / Screen.width,
                (rect.y + rect.height * 0.5f) / Screen.height
            );

            Vector2 sizeNorm = new Vector2(
                rect.width / Screen.width,
                rect.height / Screen.height
            );

            Detection d = new Detection();

            d.target = target;
            d.screenRect = rect;
            d.normalizedCenter = centerNorm;
            d.normalizedSize = sizeNorm;

            d.distance = Vector3.Distance(
                detectionCamera.transform.position,
                center
            );

            detections.Add(d);
        }
    }

    // =====================================================
    // GUI
    // =====================================================

    private void OnGUI()
    {
        if (!drawGUI)
            return;

        GUI.color = guiColor;

        foreach (Detection d in detections)
        {
            DrawRect(d.screenRect, lineThickness);

            GUI.Label(
                new Rect(
                    d.screenRect.x,
                    d.screenRect.y - 20,
                    200,
                    20
                ),
                d.target.name
            );
        }
    }

    // =====================================================
    // RECT DRAW
    // =====================================================

    private void DrawRect(Rect rect, float thickness)
    {
        DrawTexture(
            new Rect(rect.x, rect.y, rect.width, thickness));

        DrawTexture(
            new Rect(rect.x, rect.yMax - thickness, rect.width, thickness));

        DrawTexture(
            new Rect(rect.x, rect.y, thickness, rect.height));

        DrawTexture(
            new Rect(rect.xMax - thickness, rect.y, thickness, rect.height));
    }

    private void DrawTexture(Rect rect)
    {
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
    }
}

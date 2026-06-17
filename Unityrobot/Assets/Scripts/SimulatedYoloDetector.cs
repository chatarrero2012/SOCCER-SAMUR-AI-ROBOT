using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simulates YOLO-style object detection with Sim2Real Domain Shifts.
/// Includes Gaussian noise, detection dropout, distance degradation, and camera latency.
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
    // INSPECTOR: BASE VISION
    // =====================================================
    [Header("Camera")]
    public Camera detectionCamera;

    [Header("Detection")]
    public List<Transform> targets = new List<Transform>();
    public LayerMask occlusionMask;
    public bool requireLineOfSight = false;

    [Header("GUI")]
    public bool drawGUI = true;
    public Color guiColor = Color.green;
    public float lineThickness = 2f;

    // =====================================================
    // INSPECTOR: SIM2REAL DOMAIN SHIFTS 🌪️
    // =====================================================
    [Header("Sim2Real Domain Shifts (Reality Injection)")]
    [Tooltip("Master switch for all reality imperfections")]
    public bool enableDomainShifts = false;
    
    [Tooltip("Standard deviation for Gaussian noise on normalized coordinates (0.0 to 0.1)")]
    [Range(0f, 0.1f)] public float gaussianNoiseStdDev = 0.01f;
    
    [Tooltip("Base probability (0 to 1) of losing a valid detection per frame")]
    [Range(0f, 1f)] public float baseDropoutRate = 0.05f;
    
    [Tooltip("How much noise/dropout increases per meter of distance")]
    public float distanceDropoffFactor = 0.08f;
    
    [Tooltip("Fluctuation in the detected bounding box size")]
    [Range(0f, 0.05f)] public float boundingBoxJitter = 0.005f;
    
    [Tooltip("Simulated processing delay in frames (Camera Latency)")]
    [Range(0, 10)] public int latencyFrames = 0;

    // =====================================================
    // OUTPUT & INTERNALS
    // =====================================================
    public List<Detection> detections = new List<Detection>();
    
    // Buffer para simular el retardo de la cámara (Latency)
    private Queue<List<Detection>> frameBuffer = new Queue<List<Detection>>();

    // =====================================================
    // UNITY LIFECYCLE
    // =====================================================
    private void LateUpdate()
    {
        UpdateDetections();
    }

    // =====================================================
    // DETECTION CORE
    // =====================================================
    private void UpdateDetections()
    {
        detections.Clear();

        foreach (Transform target in targets)
        {
            if (target == null) continue;

            Bounds combinedBounds;
            
            // Priorizamos el Collider si existe (ideal para la bola)
            SphereCollider sphereCol = target.GetComponent<SphereCollider>();
            if (sphereCol != null)
            {
                Vector3 centerColl = target.position + sphereCol.center;
                float radius = sphereCol.radius * target.lossyScale.x;
                combinedBounds = new Bounds(centerColl, Vector3.one * radius * 2f);
            }
            else
            {
                Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) continue;

                combinedBounds = renderers[0].bounds;
                foreach (Renderer r in renderers)
                {
                    combinedBounds.Encapsulate(r.bounds);
                }
            }

            Vector3 center = combinedBounds.center;

            // OPTIONAL LINE OF SIGHT
            if (requireLineOfSight)
            {
                Vector3 dir = center - detectionCamera.transform.position;
                if (Physics.Raycast(detectionCamera.transform.position, dir.normalized, out RaycastHit hit, dir.magnitude, occlusionMask))
                {
                    if (!hit.transform.IsChildOf(target)) continue;
                }
            }

            // GET 8 CORNERS
            Vector3 extents = combinedBounds.extents;
            Vector3[] corners = new Vector3[8];
            corners[0] = center + new Vector3(-extents.x, -extents.y, -extents.z);
            corners[1] = center + new Vector3(-extents.x, -extents.y, extents.z);
            corners[2] = center + new Vector3(-extents.x, extents.y, -extents.z);
            corners[3] = center + new Vector3(-extents.x, extents.y, extents.z);
            corners[4] = center + new Vector3(extents.x, -extents.y, -extents.z);
            corners[5] = center + new Vector3(extents.x, -extents.y, extents.z);
            corners[6] = center + new Vector3(extents.x, extents.y, -extents.z);
            corners[7] = center + new Vector3(extents.x, extents.y, extents.z);

            // PROJECT TO SCREEN
            bool anyVisible = false;
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            foreach (Vector3 c in corners)
            {
                Vector3 sp = detectionCamera.WorldToScreenPoint(c);
                if (sp.z <= 0) continue;
                anyVisible = true;
                minX = Mathf.Min(minX, sp.x);
                minY = Mathf.Min(minY, sp.y);
                maxX = Mathf.Max(maxX, sp.x);
                maxY = Mathf.Max(maxY, sp.y);
            }

            if (!anyVisible) continue;

            // CLAMP TO SCREEN
            minX = Mathf.Clamp(minX, 0, Screen.width);
            maxX = Mathf.Clamp(maxX, 0, Screen.width);
            minY = Mathf.Clamp(minY, 0, Screen.height);
            maxY = Mathf.Clamp(maxY, 0, Screen.height);

            float width = maxX - minX;
            float height = maxY - minY;
            if (width <= 1 || height <= 1) continue;

            Rect rect = new Rect(minX, Screen.height - maxY, width, height);

            // NORMALIZED VALUES (Perfect Simulation)
            Vector2 centerNorm = new Vector2(
                (rect.x + rect.width * 0.5f) / Screen.width,
                (rect.y + rect.height * 0.5f) / Screen.height
            );
            Vector2 sizeNorm = new Vector2(
                rect.width / Screen.width,
                rect.height / Screen.height
            );

            float dist = Vector3.Distance(detectionCamera.transform.position, center);

            // =====================================================
            // 🌪️ APPLY SIM2REAL DOMAIN SHIFTS
            // =====================================================
            if (enableDomainShifts)
            {
                float distFactor = dist * distanceDropoffFactor;

                // 1. Gaussian Noise on Center and Size
                centerNorm.x += GaussianRandom() * (gaussianNoiseStdDev + distFactor);
                centerNorm.y += GaussianRandom() * (gaussianNoiseStdDev + distFactor);
                sizeNorm.x += GaussianRandom() * (boundingBoxJitter + distFactor * 0.5f);
                sizeNorm.y += GaussianRandom() * (boundingBoxJitter + distFactor * 0.5f);

                // Clamp to valid normalized ranges [0,1]
                centerNorm.x = Mathf.Clamp01(centerNorm.x);
                centerNorm.y = Mathf.Clamp01(centerNorm.y);
                sizeNorm.x = Mathf.Clamp01(sizeNorm.x);
                sizeNorm.y = Mathf.Clamp01(sizeNorm.y);

                // 2. Distance-based Dropout (The "Blink" effect)
                float currentDropout = baseDropoutRate + distFactor;
                if (Random.value < currentDropout)
                {
                    continue; // Drop this detection entirely
                }
            }

            // BUILD DETECTION OBJECT
            Detection d = new Detection();
            d.target = target;
            d.screenRect = rect;
            d.normalizedCenter = centerNorm;
            d.normalizedSize = sizeNorm;
            d.distance = dist;

            detections.Add(d);
        }

        // =====================================================
        // 🕒 APPLY CAMERA LATENCY (Frame Buffer)
        // =====================================================
        if (enableDomainShifts && latencyFrames > 0)
        {
            // Guardamos el estado actual en el buffer
            frameBuffer.Enqueue(new List<Detection>(detections));
            
            // Si ya tenemos suficientes frames guardados, devolvemos el más viejo
            if (frameBuffer.Count > latencyFrames)
            {
                var delayedDetections = frameBuffer.Dequeue();
                detections.Clear();
                detections.AddRange(delayedDetections);
            }
            else
            {
                // Si aún no llenamos el buffer de retraso, no entregamos nada (latencia inicial)
                detections.Clear(); 
            }
        }
    }

    // =====================================================
    // MATH HELPERS
    // =====================================================
    
    // Genera un número aleatorio con distribución normal (Gaussiana) usando Box-Muller
    private float GaussianRandom()
    {
        float u1 = 1.0f - Random.value;
        float u2 = 1.0f - Random.value;
        return Mathf.Sqrt(-2.0f * Mathf.Log(u1)) * Mathf.Sin(2.0f * Mathf.PI * u2);
    }

    // =====================================================
    // GUI
    // =====================================================
    private void OnGUI()
    {
        if (!drawGUI) return;

        GUI.color = guiColor;
        foreach (Detection d in detections)
        {
            DrawRect(d.screenRect, lineThickness);
            GUI.Label(new Rect(d.screenRect.x, d.screenRect.y - 20, 200, 20), d.target.name);
        }
    }

    private void DrawRect(Rect rect, float thickness)
    {
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), Texture2D.whiteTexture);
    }
}
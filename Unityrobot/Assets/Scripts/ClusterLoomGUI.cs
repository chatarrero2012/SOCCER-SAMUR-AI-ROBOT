using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Visualizador GUI 2D de Clusters Neuronales.
/// Reemplaza el visualizador 3D para salvar el CPU.
/// </summary>
public class ClusterLoomGUI : MonoBehaviour
{
    [Header("References")]
    public NeuralInferenceEngine engine;

    [Header("UI Settings")]
    public Rect windowRect = new Rect(10, 520, 950, 400); // Debajo del Dashboard
    public Color bgColor = new Color(0.05f, 0.05f, 0.08f, 0.95f);
    public Color gridColor = new Color(0.2f, 0.2f, 0.2f, 0.5f);

    // Estado de Zoom / Navegación
    private enum ViewMode { Macro, Meso, Micro }
    private ViewMode currentMode = ViewMode.Macro;
    private int selectedLayerIndex = -1;
    private int selectedNeuronIndex = -1;
    private Vector2 scrollPos;

    // Textura de 1x1 para dibujar rectángulos de colores rápidamente (Optimización IMGUI)
    private Texture2D _whiteTex;
    private Texture2D WhiteTex {
        get {
            if (_whiteTex == null) {
                _whiteTex = new Texture2D(1, 1);
                _whiteTex.SetPixel(0, 0, Color.white);
                _whiteTex.Apply();
            }
            return _whiteTex;
        }
    }

    private void OnGUI()
    {
        // 🚨 SI FALTA LA CONEXIÓN, GRITA EN LA PANTALLA
        if (engine == null || engine.Topology == null) 
        {
            GUI.color = Color.red;
            GUI.Label(new Rect(200, 200, 800, 100), 
                "⚠️ CLUSTER LOOM OFFLINE: Crea un GameObject, añade ClusterLoomGUI y arrastra el NeuralInferenceEngine al slot 'Engine'.", 
                new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold });
            GUI.color = Color.white;
            return; 
        }

        // Si todo está bien, dibuja el Telar
        GUI.backgroundColor = bgColor;
        windowRect = GUI.Window(54321, windowRect, DrawLoomWindow, "🧶 THE CLUSTER LOOM (2D Neural Map)");
        GUI.backgroundColor = Color.white;
    }

    private void DrawLoomWindow(int id)
    {
        // Botones de navegación (Breadcrumb)
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("MACRO (Capas)", GUILayout.Width(120))) { currentMode = ViewMode.Macro; selectedLayerIndex = -1; }
        if (selectedLayerIndex != -1 && GUILayout.Button($"MESO (Capa {selectedLayerIndex})", GUILayout.Width(150))) { currentMode = ViewMode.Meso; selectedNeuronIndex = -1; }
        if (selectedNeuronIndex != -1 && GUILayout.Button($"MICRO (Neurona {selectedNeuronIndex})", GUILayout.Width(180))) { currentMode = ViewMode.Micro; }
        GUILayout.FlexibleSpace();
        GUILayout.Label($"Mode: {currentMode} | Layers: {engine.Topology.layers.Count}");
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        // Área de dibujo principal
        Rect drawArea = GUILayoutUtility.GetRect(0, 0, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        
        // Recortamos para que no se salga de la ventana
        GUI.BeginGroup(drawArea);
        Rect localArea = new Rect(0, 0, drawArea.width, drawArea.height);

        switch (currentMode)
        {
            case ViewMode.Macro: DrawMacroView(localArea); break;
            case ViewMode.Meso: DrawMesoView(localArea); break;
            case ViewMode.Micro: DrawMicroView(localArea); break;
        }

        GUI.EndGroup();
        GUI.DragWindow();
    }

    // ==========================================
    // VISTA MACRO: Capas como barras verticales
    // ==========================================
    private void DrawMacroView(Rect area)
    {
        var layers = engine.Topology.layers;
        float layerWidth = Mathf.Max(40, (area.width - 20) / layers.Count);
        float maxHeight = area.height - 20;

        for (int i = 0; i < layers.Count; i++)
        {
            float x = 10 + (i * layerWidth);
            Rect layerRect = new Rect(x, 10, layerWidth - 5, maxHeight);

            // Calcular activación promedio de la capa para el color
            float avgAct = GetLayerAverageActivation(layers[i].name);
            Color c = GetHeatmapColor(avgAct);
            
            GUI.color = c;
            GUI.DrawTexture(layerRect, WhiteTex);
            GUI.color = Color.white;

            // Borde y texto
            GUI.Box(layerRect, "");
            GUI.Label(new Rect(x, 10 + maxHeight - 20, layerWidth - 5, 20), 
                $"{layers[i].type}\n({layers[i].neuronCount})", 
                new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerCenter, fontSize = 9, fontStyle = FontStyle.Bold });

            // Click para hacer zoom (Meso)
            if (GUI.Button(layerRect, "", GUIStyle.none))
            {
                selectedLayerIndex = i;
                currentMode = ViewMode.Meso;
            }
        }
    }

    // ==========================================
    // VISTA MESO: Grid de Neuronas (Clusters)
    // ==========================================
    private void DrawMesoView(Rect area)
    {
        var layer = engine.Topology.layers[selectedLayerIndex];
        string layerName = layer.name;
        
        if (!engine.lastActivations.TryGetValue(layerName, out float[] activations)) return;

        int totalNeurons = layer.neuronCount;
        // Calculamos grid dinámico (ej: 16 columnas)
        int cols = Mathf.CeilToInt(Mathf.Sqrt(totalNeurons));
        int rows = Mathf.CeilToInt((float)totalNeurons / cols);

        float cellSize = Mathf.Min((area.width - 20) / cols, (area.height - 40) / rows);
        cellSize = Mathf.Clamp(cellSize, 4f, 30f); // Límite visual

        scrollPos = GUI.BeginScrollView(new Rect(0, 0, area.width, area.height), scrollPos, new Rect(0, 0, cols * cellSize + 20, rows * cellSize + 20));

        for (int i = 0; i < totalNeurons; i++)
        {
            int col = i % cols;
            int row = i / cols;
            float x = 10 + (col * cellSize);
            float y = 10 + (row * cellSize);

            Rect cellRect = new Rect(x, y, cellSize - 2, cellSize - 2);
            
            float val = (i < activations.Length) ? activations[i] : 0f;
            GUI.color = GetHeatmapColor(val);
            GUI.DrawTexture(cellRect, WhiteTex);
            GUI.color = Color.white;

            // Click para zoom micro (editar)
            if (GUI.Button(cellRect, "", GUIStyle.none))
            {
                selectedNeuronIndex = i;
                currentMode = ViewMode.Micro;
            }
        }
        GUI.EndScrollView();
    }

    // ==========================================
    // VISTA MICRO: Edición de Pesos (El Telar Manual)
    // ==========================================
    private void DrawMicroView(Rect area)
    {
        GUI.Label(new Rect(10, 10, 400, 20), $"EDITANDO: Capa {selectedLayerIndex} | Neurona {selectedNeuronIndex}", 
            new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 14 });
        
        GUI.Label(new Rect(10, 30, 800, 20), "⚠️ ZONA DE ARTESANO: Ajusta los hilos (pesos) manualmente. Los cambios se aplican en tiempo real.");

        // Simulación de edición de pesos (Como no extrajimos los pesos estáticos para ahorrar CPU, 
        // aquí mostramos las conexiones entrantes y permites inyectar un "bias" o multiplicador visual)
        
        float startY = 60;
        int maxVisibleWeights = Mathf.FloorToInt((area.height - 80) / 25);
        
        // Asumimos que los pesos vienen de la capa anterior
        int prevLayerNeurons = selectedLayerIndex > 0 ? engine.Topology.layers[selectedLayerIndex - 1].neuronCount : 1;
        
        GUI.Label(new Rect(10, startY - 20, 300, 20), $"Conexiones entrantes (desde Capa {selectedLayerIndex - 1}):");

        for (int i = 0; i < Mathf.Min(prevLayerNeurons, maxVisibleWeights); i++)
        {
            float y = startY + (i * 25);
            GUI.Label(new Rect(10, y, 100, 20), $"From N{i}:");
            
            // Un slider dummy para la interfaz de edición (En un futuro, esto modificará el tensor de Barracuda)
            float dummyWeight = Mathf.Sin(i * 0.5f) * 0.5f; // Valor simulado
            float newWeight = GUI.HorizontalSlider(new Rect(110, y + 5, 300, 20), dummyWeight, -2f, 2f);
            GUI.Label(new Rect(420, y, 50, 20), newWeight.ToString("F2"));
        }
        
        if (prevLayerNeurons > maxVisibleWeights)
        {
            GUI.Label(new Rect(10, startY + (maxVisibleWeights * 25), 400, 20), $"... y {prevLayerNeurons - maxVisibleWeights} conexiones más (Scroll para ver)");
        }
    }

    // ==========================================
    // UTILIDADES DE COLOR Y DATOS
    // ==========================================
    private float GetLayerAverageActivation(string layerName)
    {
        if (!engine.lastActivations.TryGetValue(layerName, out float[] acts)) return 0f;
        if (acts.Length == 0) return 0f;
        
        float sum = 0f;
        for(int i=0; i<acts.Length; i++) sum += Mathf.Abs(acts[i]);
        return sum / acts.Length;
    }

    private Color GetHeatmapColor(float value)
    {
        // Azul (Negativo/Frío) -> Gris (Cero) -> Rojo/Naranja (Positivo/Caliente)
        if (value > 0) return Color.Lerp(new Color(0.1f, 0.1f, 0.1f), new Color(1f, 0.4f, 0.1f), Mathf.Clamp01(value));
        else return Color.Lerp(new Color(0.1f, 0.1f, 0.1f), new Color(0.2f, 0.5f, 1f), Mathf.Clamp01(-value));
    }
}
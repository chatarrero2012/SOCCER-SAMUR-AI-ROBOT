using UnityEngine;
using Unity.Barracuda;
using System.Collections.Generic;

/// <summary>
/// Motor de inferencia ligero. Ejecuta la red y expone las activaciones.
/// Parcheado para Barracuda 3.0.0: ignora nodos internos no expuestos.
/// </summary>
public class NeuralInferenceEngine : MonoBehaviour
{
    [Header("Model & Input")]
    public NNModel onnxModel;
    public SoccerAgent liveAgent;

    // Estado interno
    private IWorker worker;
    private NetworkTopology topology;
    
    // Diccionario público para que el GUI lea las activaciones en tiempo real
    public Dictionary<string, float[]> lastActivations { get; private set; } = new Dictionary<string, float[]>();
    public NetworkTopology Topology => topology;

    private void Start()
    {
        if (onnxModel == null) { Debug.LogError("⚠️ Asigna el ONNX al NeuralInferenceEngine"); return; }

        Model runtimeModel = ModelLoader.Load(onnxModel);
        // FORZAMOS CSHARP PARA TU MAC 2012 (Cero dependencia de Metal/GPU)
        worker = WorkerFactory.CreateWorker(WorkerFactory.Type.CSharp, runtimeModel);
        topology = NetworkTopology.ExtractFrom(runtimeModel);
        
        Debug.Log($"🧠 Motor Neuronal Iniciado: {topology.layers.Count} capas detectadas.");
    }

    private void FixedUpdate()
    {
        if (worker == null || topology == null) return;

        float[] inputs = GetInputVector();
        
        using (Tensor inputTensor = new Tensor(1, inputs.Length, inputs))
        {
            worker.Execute(inputTensor);
            
            lastActivations.Clear();
            foreach (var layer in topology.layers)
            {
                // 🛡️ FIX 1: Ignorar nodos internos de ONNX sin nombre real (ej: "17", "", "3")
                if (string.IsNullOrEmpty(layer.name) || char.IsDigit(layer.name[0])) 
                    continue;

                // 🛡️ FIX 2: Barracuda 3.0 lanza KeyNotFoundException si la capa no está en el grafo expuesto
                try 
                {
                    Tensor t = worker.PeekOutput(layer.name);
                    if (t != null)
                    {
                        lastActivations[layer.name] = t.ToReadOnlyArray();
                    }
                }
                catch (System.Collections.Generic.KeyNotFoundException) 
                {
                    // Capa intermedia no expuesta. Se ignora silenciosamente.
                }
            }
        }
    }

    private float[] GetInputVector()
    {
        if (liveAgent != null)
        {
            float[] obs = liveAgent.GetLastObservations();
            if (obs != null && obs.Length > 0) return obs;
        }
        
        // Dummy input si no hay agente
        int size = topology.layers.Count > 0 ? topology.layers[0].neuronCount : 25;
        float[] dummy = new float[size];
        for (int i = 0; i < size; i++) dummy[i] = Random.Range(-1f, 1f);
        return dummy;
    }

    private void OnDestroy()
    {
        if (worker != null) worker.Dispose();
    }
}
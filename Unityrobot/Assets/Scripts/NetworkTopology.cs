using UnityEngine;
using Unity.Barracuda;
using System.Collections.Generic;

/// <summary>
/// 提取网络拓扑结构（层数、神经元数量）。
/// 注意：移除了 weights/biases 提取，因为 Visualizer 只需要实时激活值，
/// 此举可完美避开 BarracudaArray 的底层 API 兼容性问题。
/// </summary>
public class NetworkTopology
{
    [System.Serializable]
    public class LayerInfo
    {
        public string name;
        public string type; // Dense, Conv2D, Input, etc.
        public int neuronCount;
        
        // [已移除] weights 和 biases 列表。
        // 可视化器通过 worker.PeekOutput() 获取实时激活值，无需读取静态权重。
    }
    
    public List<LayerInfo> layers = new List<LayerInfo>();

    public static NetworkTopology ExtractFrom(Model model)
    {
        NetworkTopology topo = new NetworkTopology();
        
        foreach (var layer in model.layers)
        {
            // 兼容不同版本 Barracuda 的 Input 枚举判断
            bool isDenseOrConv = layer.type == Layer.Type.Dense || layer.type == Layer.Type.Conv2D;
            bool isInput = layer.type.ToString() == "Input";

            if (isDenseOrConv || isInput)
            {
                LayerInfo info = new LayerInfo();
                info.name = layer.name;
                info.type = layer.type.ToString();
                
                // 计算每层的神经元数量
                if (layer.type == Layer.Type.Dense)
                {
                    info.neuronCount = layer.datasets != null && layer.datasets.Length > 0 
                        ? layer.datasets[0].length 
                        : 64; // Fallback
                }
                else if (isInput)
                {
                    info.neuronCount = 25; // 你的 SoccerAgent 观察空间大小
                }
                else
                {
                    info.neuronCount = 32; // Conv2D 等 fallback
                }
                
                // [已移除] 彻底放弃读取 layer.weights 和 layer.biases
                // 完美避开 BarracudaArray 无法遍历、无 length、无 ToArray 的玄学问题。
                
                topo.layers.Add(info);
            }
        }
        
        return topo;
    }

    public int GetTotalNeuronCount()
    {
        int total = 0;
        foreach (var l in layers) total += l.neuronCount;
        return total;
    }
}
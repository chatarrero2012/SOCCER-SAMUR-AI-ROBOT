using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Centralized visual debugging for robot soccer.
/// Draws table bounds, robot spawn area, dynamic ball spawn cone, and NPC data.
/// Safe to disable at runtime.
/// </summary>
public class SoccerDebugVisualizer : MonoBehaviour
{
    [Header("Master Switch")]
    public bool drawDebugUI = true;

    // =====================================================
    // REFERENCES
    // =====================================================
    [Header("References")]
    public SoccerAgent soccerAgent;
    public DynamicObstacleBot obstacleBot;

    // =====================================================
    // TABLE
    // =====================================================
    [Header("Table")]
    public Transform tableCenter;
    public float tableWidth = 1.5f;
    public float tableLength = 1.5f;

    // =====================================================
    // UNITY
    // =====================================================
    private void Update()
    {
        if (!drawDebugUI) return;
        
        DrawTable();
        DrawSpawnAreas();
        DrawNPC();
    }

    // =====================================================
    // TABLE
    // =====================================================
    private void DrawTable()
    {
        if (tableCenter == null) return;
        
        DrawRectangle(
            tableCenter.position,
            tableWidth,
            tableLength,
            Color.yellow);
    }

    // =====================================================
    // SPAWNS (ACTUALIZADO: Cono dinámico para el balón)
    // =====================================================
    private void DrawSpawnAreas()
    {
        if (soccerAgent == null) return;

        // 1. Robot Spawn (Círculo verde estático)
        DrawCircle(
            soccerAgent.robotSpawnCenter,
            soccerAgent.robotSpawnRadius,
            Color.green);

        Debug.DrawLine(
            soccerAgent.robotSpawnCenter,
            soccerAgent.robotSpawnCenter + Vector3.up * 0.25f,
            Color.green);

        // 2. Ball Spawn (🪄 NUEVO: Cono dinámico apuntando a la portería)
        if (soccerAgent.enemyGoal != null)
        {
            Vector3 dirToGoal = (soccerAgent.enemyGoal.position - soccerAgent.robotSpawnCenter).normalized;
            float baseAngle = Mathf.Atan2(dirToGoal.x, dirToGoal.z) * Mathf.Rad2Deg;

            Color ballSpawnColor = Color.cyan;
            float minDist = 1.0f;
            float maxDist = 2.5f;
            float angleVariance = 15f; // Coincide con el Random.Range(-15f, 15f) del Agent

            // Dibujar el arco interior (1.0m) y exterior (2.5m)
            DrawArc(soccerAgent.robotSpawnCenter, minDist, baseAngle - angleVariance, baseAngle + angleVariance, 12, ballSpawnColor);
            DrawArc(soccerAgent.robotSpawnCenter, maxDist, baseAngle - angleVariance, baseAngle + angleVariance, 12, ballSpawnColor);

            // Dibujar las líneas laterales que cierran el cono
            Vector3 leftDir = Quaternion.Euler(0, baseAngle - angleVariance, 0) * Vector3.forward;
            Vector3 rightDir = Quaternion.Euler(0, baseAngle + angleVariance, 0) * Vector3.forward;

            Debug.DrawLine(
                soccerAgent.robotSpawnCenter + leftDir * minDist, 
                soccerAgent.robotSpawnCenter + leftDir * maxDist, 
                ballSpawnColor);
                
            Debug.DrawLine(
                soccerAgent.robotSpawnCenter + rightDir * minDist, 
                soccerAgent.robotSpawnCenter + rightDir * maxDist, 
                ballSpawnColor);

            // Línea central de referencia (hacia la portería)
            Debug.DrawLine(
                soccerAgent.robotSpawnCenter, 
                soccerAgent.robotSpawnCenter + dirToGoal * maxDist, 
                new Color(ballSpawnColor.r, ballSpawnColor.g, ballSpawnColor.b, 0.5f));
        }
    }

    // =====================================================
    // NPC
    // =====================================================
    private void DrawNPC()
    {
        if (obstacleBot == null) return;

        // Patrol Area
        if (obstacleBot.patrolCenter != null)
        {
            DrawRectangle(
                obstacleBot.patrolCenter.position,
                obstacleBot.patrolWidth,
                obstacleBot.patrolLength,
                new Color(1f, 0.5f, 0f));
        }

        // Forward
        Debug.DrawLine(
            obstacleBot.transform.position,
            obstacleBot.transform.position + obstacleBot.transform.forward * 0.25f,
            Color.blue);

        // Target
        Debug.DrawLine(
            obstacleBot.transform.position,
            obstacleBot.CurrentTarget,
            Color.magenta);

        DrawCross(
            obstacleBot.CurrentTarget,
            0.05f,
            Color.red);

        // State
        Color stateColor = obstacleBot.IsWaiting ? new Color(1f, 0.5f, 0f) : Color.green;
        
        Debug.DrawLine(
            obstacleBot.transform.position + Vector3.up * 0.05f,
            obstacleBot.transform.position + Vector3.up * 0.25f,
            stateColor);
    }

    // =====================================================
    // HELPERS
    // =====================================================
    
    // 🪄 NUEVO HELPER: Dibuja un arco en el plano XZ
    private void DrawArc(Vector3 center, float radius, float startAngle, float endAngle, int segments, Color color)
    {
        float step = (endAngle - startAngle) / segments;
        Vector3 prevPos = center + (Quaternion.Euler(0, startAngle, 0) * Vector3.forward) * radius;
        
        for (int i = 1; i <= segments; i++)
        {
            float currentAngle = startAngle + (step * i);
            Vector3 nextPos = center + (Quaternion.Euler(0, currentAngle, 0) * Vector3.forward) * radius;
            Debug.DrawLine(prevPos, nextPos, color);
            prevPos = nextPos;
        }
    }

    private void DrawCross(Vector3 pos, float size, Color color)
    {
        Debug.DrawLine(pos + Vector3.left * size, pos + Vector3.right * size, color);
        Debug.DrawLine(pos + Vector3.forward * size, pos + Vector3.back * size, color);
    }

    private void DrawRectangle(Vector3 center, float width, float length, Color color)
    {
        float hw = width * 0.5f;
        float hl = length * 0.5f;
        
        Vector3 a = center + new Vector3(-hw, 0f, -hl);
        Vector3 b = center + new Vector3(hw, 0f, -hl);
        Vector3 c = center + new Vector3(hw, 0f, hl);
        Vector3 d = center + new Vector3(-hw, 0f, hl);

        Debug.DrawLine(a, b, color);
        Debug.DrawLine(b, c, color);
        Debug.DrawLine(c, d, color);
        Debug.DrawLine(d, a, color);
    }

    private void DrawCircle(Vector3 center, float radius, Color color, int segments = 24)
    {
        Vector3 prev = center + new Vector3(radius, 0f, 0f);
        for (int i = 1; i <= segments; i++)
        {
            float angle = (float)i / segments * Mathf.PI * 2f;
            Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            Debug.DrawLine(prev, next, color);
            prev = next;
        }
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Centralized visual debugging for robot soccer.
/// Draws table bounds, agent spawn cone, NPC dynamic cone, and NPC behavioral state.
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
    // SPAWNS (Agent)
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

        // 2. Ball Spawn (Cono dinámico apuntando a la portería)
        if (soccerAgent.enemyGoal != null)
        {
            Vector3 center = soccerAgent.robotSpawnCenter;
            Vector3 forward = (soccerAgent.enemyGoal.position - center).normalized;
            float baseAngle = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

            Color ballSpawnColor = Color.cyan;
            float minDist = 1.0f;
            float maxDist = 2.5f;
            float angleVariance = 15f; 

            DrawArc(center, minDist, baseAngle - angleVariance, baseAngle + angleVariance, 12, ballSpawnColor);
            DrawArc(center, maxDist, baseAngle - angleVariance, baseAngle + angleVariance, 12, ballSpawnColor);

            Vector3 leftDir = Quaternion.Euler(0, baseAngle - angleVariance, 0) * Vector3.forward;
            Vector3 rightDir = Quaternion.Euler(0, baseAngle + angleVariance, 0) * Vector3.forward;

            Debug.DrawLine(center + leftDir * minDist, center + leftDir * maxDist, ballSpawnColor);
            Debug.DrawLine(center + rightDir * minDist, center + rightDir * maxDist, ballSpawnColor);
        }
    }

    // =====================================================
    // NPC (ACTUALIZADO: Cono Naranja + Estados de Color)
    // =====================================================
    private void DrawNPC()
    {
        if (obstacleBot == null) return;

        // 1. NPC Activity Cone (Zona de influencia del NPC en Naranja)
        if (obstacleBot.coneAnchor != null && obstacleBot.coneDirectionTarget != null)
        {
            Vector3 center = obstacleBot.coneAnchor.position;
            Vector3 forward = (obstacleBot.coneDirectionTarget.position - center).normalized;
            float baseAngle = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;

            Color npcConeColor = new Color(1f, 0.5f, 0f, 0.6f); // Naranja transparente
            float minDist = 1.0f;
            float maxDist = 2.5f;
            float angleVariance = 15f;

            DrawArc(center, minDist, baseAngle - angleVariance, baseAngle + angleVariance, 12, npcConeColor);
            DrawArc(center, maxDist, baseAngle - angleVariance, baseAngle + angleVariance, 12, npcConeColor);

            Vector3 leftDir = Quaternion.Euler(0, baseAngle - angleVariance, 0) * Vector3.forward;
            Vector3 rightDir = Quaternion.Euler(0, baseAngle + angleVariance, 0) * Vector3.forward;

            Debug.DrawLine(center + leftDir * minDist, center + leftDir * maxDist, npcConeColor);
            Debug.DrawLine(center + rightDir * minDist, center + rightDir * maxDist, npcConeColor);
        }

        // 2. NPC Forward Vector
        Debug.DrawLine(
            obstacleBot.transform.position,
            obstacleBot.transform.position + obstacleBot.transform.forward * 0.25f,
            Color.blue);

        // 3. NPC Target & Behavioral State (Código de Colores)
        Color targetColor = Color.magenta;
        
        switch (obstacleBot.currentMode)
        {
            case DynamicObstacleBot.BehaviorMode.ConePatrol:
                targetColor = Color.cyan;   // 🟦 Patrullando el cono
                break;
            case DynamicObstacleBot.BehaviorMode.WanderAway:
                targetColor = Color.yellow; // 🟨 Vagando lejos
                break;
            case DynamicObstacleBot.BehaviorMode.BallChase:
                targetColor = Color.red;    // 🟥 ¡Persiguiendo el balón!
                break;
        }

        // Línea hacia el target
        Debug.DrawLine(
            obstacleBot.transform.position,
            obstacleBot.CurrentTarget,
            targetColor);

        // Marcador del target (Cruz + Línea vertical para verla bien en 3D)
        DrawCross(obstacleBot.CurrentTarget, 0.08f, targetColor);
        Debug.DrawRay(obstacleBot.CurrentTarget, Vector3.up * 0.6f, targetColor);

        // 4. State / Waiting indicator (Línea sobre la cabeza del NPC)
        Color stateColor = obstacleBot.IsWaiting ? new Color(1f, 0.5f, 0f) : Color.green;
        Debug.DrawLine(
            obstacleBot.transform.position + Vector3.up * 0.05f,
            obstacleBot.transform.position + Vector3.up * 0.3f,
            stateColor);
    }

    // =====================================================
    // HELPERS
    // =====================================================
    
    // Dibuja un arco en el plano XZ
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
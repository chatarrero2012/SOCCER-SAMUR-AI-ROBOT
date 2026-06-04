using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Centralized visual debugging for robot soccer.
///
/// Draws:
/// - Table bounds
/// - Robot spawn area
/// - Ball spawn area
/// - NPC patrol area
/// - NPC current target
/// - NPC state
/// - NPC forward vector
///
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
    if (!drawDebugUI)
        return;

    DrawTable();

    DrawSpawnAreas();

    DrawNPC();
}

// =====================================================
// TABLE
// =====================================================

private void DrawTable()
{
    if (tableCenter == null)
        return;

    DrawRectangle(
        tableCenter.position,
        tableWidth,
        tableLength,
        Color.yellow);
}

// =====================================================
// SPAWNS
// =====================================================

private void DrawSpawnAreas()
{
    if (soccerAgent == null)
        return;

    // Robot spawn

    DrawCircle(
        soccerAgent.robotSpawnCenter,
        soccerAgent.robotSpawnRadius,
        Color.green);

    Debug.DrawLine(
        soccerAgent.robotSpawnCenter,
        soccerAgent.robotSpawnCenter +
        Vector3.up * 0.25f,
        Color.green);

    // Ball spawn

    DrawCircle(
        soccerAgent.ballSpawnCenter,
        soccerAgent.ballSpawnRadius,
        Color.cyan);

    Debug.DrawLine(
        soccerAgent.ballSpawnCenter,
        soccerAgent.ballSpawnCenter +
        Vector3.up * 0.25f,
        Color.cyan);
}

// =====================================================
// NPC
// =====================================================

private void DrawNPC()
{
    if (obstacleBot == null)
        return;

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
        obstacleBot.transform.position +
        obstacleBot.transform.forward * 0.25f,
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

    Color stateColor =
        obstacleBot.IsWaiting
        ? new Color(1f, 0.5f, 0f)
        : Color.green;

    Debug.DrawLine(
        obstacleBot.transform.position +
        Vector3.up * 0.05f,

        obstacleBot.transform.position +
        Vector3.up * 0.25f,

        stateColor);
}

// =====================================================
// HELPERS
// =====================================================

private void DrawCross(
    Vector3 pos,
    float size,
    Color color)
{
    Debug.DrawLine(
        pos + Vector3.left * size,
        pos + Vector3.right * size,
        color);

    Debug.DrawLine(
        pos + Vector3.forward * size,
        pos + Vector3.back * size,
        color);
}

private void DrawRectangle(
    Vector3 center,
    float width,
    float length,
    Color color)
{
    float hw = width * 0.5f;
    float hl = length * 0.5f;

    Vector3 a =
        center +
        new Vector3(-hw, 0f, -hl);

    Vector3 b =
        center +
        new Vector3(hw, 0f, -hl);

    Vector3 c =
        center +
        new Vector3(hw, 0f, hl);

    Vector3 d =
        center +
        new Vector3(-hw, 0f, hl);

    Debug.DrawLine(a, b, color);
    Debug.DrawLine(b, c, color);
    Debug.DrawLine(c, d, color);
    Debug.DrawLine(d, a, color);
}

private void DrawCircle(
    Vector3 center,
    float radius,
    Color color,
    int segments = 24)
{
    Vector3 prev =
        center +
        new Vector3(radius, 0f, 0f);

    for (int i = 1; i <= segments; i++)
    {
        float angle =
            (float)i /
            segments *
            Mathf.PI * 2f;

        Vector3 next =
            center +
            new Vector3(
                Mathf.Cos(angle) * radius,
                0f,
                Mathf.Sin(angle) * radius);

        Debug.DrawLine(
            prev,
            next,
            color);

        prev = next;
    }
}
}


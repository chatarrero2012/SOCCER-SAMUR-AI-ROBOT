using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// COMPETENCY-BASED DASHBOARD (Fila Kinetic Tracker V4)
/// Interfaz analítica unificada optimizada para la visualización del objetivo de goles.
/// </summary>
public class CompetencyDashboard : MonoBehaviour
{
    [Header("Window")]
    public bool showDashboard = true;
    public Rect windowRect = new Rect(10, 10, 920, 580);

    [Header("Logging")]
    public bool enableCoachLogs = true;
    public int logEveryEpisodes = 50;
    private int lastLoggedEpisode = -1;

    public static int TotalCollisions = 0; 
    public static int TotalFalls = 0;
    public static int TotalTimeouts = 0;
    public static float AverageEpisodeDuration = 0f;
    private static Queue<float> recentDurations = new Queue<float>();
    private const int WINDOW_SIZE = 50;

    private Texture2D _lineTex;

    private void Awake()
    {
        _lineTex = new Texture2D(1, 1);
        _lineTex.SetPixel(0, 0, Color.green);
        _lineTex.Apply();
    }

    private void OnGUI()
    {
        if (!showDashboard) return;
        GUI.skin.window.normal.textColor = Color.cyan;
        windowRect = GUI.Window(12346, windowRect, DrawWindow, "⚔ SAMUR-AI KINETIC SHAPING CENTER");
    }

    private void DrawWindow(int id)
    {
        int episodes = Mathf.Max(1, MatchAnalytics.TotalEpisodes);
        int goals = MatchAnalytics.TotalGoals;
        int ownGoals = MatchAnalytics.TotalOwnGoals;
        
        float goalRate = MatchAnalytics.GetRecentGoalRate();
        float timeoutRate = (float)TotalTimeouts / episodes;
        float fallRate = (float)TotalFalls / episodes;
        
        float totalReward = MatchAnalytics.TotalReward;
        float rewardGoals = MatchAnalytics.RewardFromGoals;
        float penalizacionesFijas = (ownGoals * -600f) + (TotalTimeouts * -20f) + (TotalFalls * -20f);
        float shapingReward = totalReward - rewardGoals - penalizacionesFijas;

        // --- SECCIÓN 1: RESULTADOS ---
        GUI.Label(new Rect(20, 30, 280, 160),
        $@"═══════ RESULTADOS ═══════
Episodes:      {MatchAnalytics.TotalEpisodes}
Goals:         {goals} (Own: {ownGoals})
Current Rate:  {(goalRate * 100f):F2}%
Timeout Rate:  {(timeoutRate * 100f):F2}%
Fall Rate:     {(fallRate * 100f):F2}%");

        // --- SECCIÓN 2: RECOMPENSAS ---
        GUI.Label(new Rect(320, 30, 280, 160),
        $@"══════ RECOMPENSAS ══════
Total Reward:  {totalReward:F1}
From Goals:    {rewardGoals:F1}
Shaping Kinetic:{shapingReward:F1}
Drag Vector:   ✓ Arrastre Hacia Arco Rival Activo
Status:        🔥 ALINEANDO GRADIENTE AGRESIVO");

        // --- SECCIÓN 3: FILA / CURVA INDICADORA DEL OBJETIVO ---
        GUI.Box(new Rect(620, 40, 270, 130), "📊 CURVA DE GOAL RATE (META > 10%)");
        
        float progressWidth = Mathf.Clamp01(goalRate / 0.20f) * 250f; 
        
        GUI.color = goalRate >= 0.10f ? Color.green : Color.yellow;
        GUI.DrawTexture(new Rect(630, 90, progressWidth, 20), _lineTex);
        GUI.color = Color.white;

        GUI.Box(new Rect(630 + 125, 80, 2, 40), ""); 
        GUI.Label(new Rect(630 + 105, 125, 60, 20), "META 10%");
        GUI.Label(new Rect(630, 65, 200, 20), $"Progreso Objetivo: {(goalRate / 0.10f * 100f):F1}%");

        // --- SECCIÓN 4: VEREDICTO DE COMBATE ---
        string rank = GetSamuraiRank(goalRate, MatchAnalytics.TotalEpisodes);
        string verdict = goalRate >= 0.10f ? "✓ OBJETIVO OPERATIVO COMPLETADO" : "🔥 EXPLOITANDO PICOS DE RECOMPENSA SUPERIORES A 500";

        GUI.color = goalRate >= 0.10f ? Color.green : Color.yellow;
        GUI.Label(new Rect(20, 200, 400, 30), $"RANK: {rank}");
        GUI.Label(new Rect(20, 230, 600, 30), $"VERDICT: {verdict}");
        GUI.color = Color.white;

        // --- SECCIÓN 5: ESTADO DE SEQUÍA ---
        bool inDrought = (MatchAnalytics.TotalEpisodes > 25) && (goalRate < 0.02f); 
        string droughtStatus = inDrought ? "🧪 TIRO LIBRE ACTIVO: Forzando Penales Estrictos" : "✓ RITMO DE JUEGO DINÁMICO";
        GUI.color = inDrought ? Color.red : Color.green;
        GUI.Label(new Rect(20, 280, 860, 30), $"LAB STATUS: {droughtStatus}");
        GUI.color = Color.white;

        // --- SECCIÓN 6: REPORTES DE DOJO ---
        GUI.Label(new Rect(20, 330, 860, 150),
        $@"═══════════════════ REPORTE DE SALUD DEL MODELO ═══════════════════
📊 Total de Impactos Cinéticos: {TotalCollisions} | Episodios Totales: {MatchAnalytics.TotalEpisodes}
🚀 El imán de arrastre vectorial está activo: El agente es premiado continuamente mientras sostenga el avance del balón.
🎯 El marcador del 10% se actualizará en tiempo real. Deja correr el entrenamiento.");

        GUI.DragWindow();
    }

    private string GetSamuraiRank(float goalRate, int episodes)
    {
        if (goalRate < 0.02f) return "STRIKER ASUSTADO (PATEANDO SIN SEGUIMIENTO)";
        if (goalRate < 0.10f) return "DELANTERO EN RANGO (COMPORTAMIENTO DE EMPUJE)";
        return "ELITE STRIKER (> 10% GOAL METRIC REALIZADO)";
    }

    private void Update()
    {
        if (!enableCoachLogs) return;
        int episodes = MatchAnalytics.TotalEpisodes;
        if (episodes > 0 && episodes % logEveryEpisodes == 0 && episodes != lastLoggedEpisode)
        {
            lastLoggedEpisode = episodes;
            Debug.Log($"[Dojo Coach] Episodes: {episodes} | Goal Rate Reciente: {MatchAnalytics.GetRecentGoalRate():P2} | Impactos: {TotalCollisions}");
        }
    }

    public static void RecordCollision() => TotalCollisions++;
    public static void RecordFall() => TotalFalls++;
    public static void RecordTimeout() => TotalTimeouts++;

    public static void RecordEpisodeDuration(float duration)
    {
        if (recentDurations.Count >= WINDOW_SIZE) recentDurations.Dequeue();
        recentDurations.Enqueue(duration);
        
        AverageEpisodeDuration = 0f;
        foreach (float d in recentDurations) AverageEpisodeDuration += d;
        if (recentDurations.Count > 0) AverageEpisodeDuration /= recentDurations.Count;
    }

    public static void ResetAll()
    {
        TotalCollisions = 0; TotalFalls = 0; TotalTimeouts = 0; AverageEpisodeDuration = 0f;
        recentDurations.Clear(); MatchAnalytics.Reset();
    }
}
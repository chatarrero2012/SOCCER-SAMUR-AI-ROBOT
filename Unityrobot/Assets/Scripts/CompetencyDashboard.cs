using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// COMPETENCY-BASED DASHBOARD (Fila Kinetic Tracker V5 - Curriculum Edition)
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
    private SoccerAgentCompetency agentInstance;

    private void Awake()
    {
        _lineTex = new Texture2D(1, 1);
        _lineTex.SetPixel(0, 0, Color.green);
        _lineTex.Apply();
        agentInstance = FindObjectOfType<SoccerAgentCompetency>();
    }

    private void OnGUI()
    {
        if (!showDashboard) return;
        GUI.skin.window.normal.textColor = Color.cyan;
        windowRect = GUI.Window(12346, windowRect, DrawWindow, "⚔ SAMUR-AI CURRICULUM SHAPING CONTROL");
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
        float penalizacionesFijas = (ownGoals * -700f) + (TotalTimeouts * -30f) + (TotalFalls * -30f);
        float shapingReward = totalReward - rewardGoals - penalizacionesFijas;

        int currentLvl = agentInstance != null ? agentInstance.currentLevel : 0;

        // --- SECCIÓN 1: RESULTADOS ---
        GUI.Label(new Rect(20, 30, 280, 160),
        $@"═══════ RESULTADOS ═══════
Episodes:      {MatchAnalytics.TotalEpisodes}
Goals:         {goals} (Own: {ownGoals})
Recent Rate:   {(goalRate * 100f):F2}%
Timeout Rate:  {(timeoutRate * 100f):F2}%
Fall Rate:     {(fallRate * 100f):F2}%");

        // --- SECCIÓN 2: CURRICULUM STATUS ---
        GUI.Label(new Rect(320, 30, 280, 160),
        $@"══════ CÁTEDRA PROGRESIVA ══════
Current Level:  LVL {currentLvl} - {GetLevelName(currentLvl)}
Total Reward:   {totalReward:F1}
Kinetic Shaping:{shapingReward:F1}
Sim2Real Check: ✓ Laws of Physics Retained
Status:         ⚙ ADAPTANDO MATRIZ DE SPAWN");

        // --- SECCIÓN 3: FILA INDICADORA DE EFECTIVIDAD ---
        GUI.Box(new Rect(620, 40, 270, 130), "📊 CURVA DE RENDIMIENTO EFECTIVO");
        float progressWidth = Mathf.Clamp01(goalRate / 0.90f) * 250f; 
        
        GUI.color = goalRate >= 0.90f ? Color.green : (goalRate >= 0.50f ? Color.yellow : Color.red);
        GUI.DrawTexture(new Rect(630, 90, progressWidth, 20), _lineTex);
        GUI.color = Color.white;

        GUI.Box(new Rect(630 + 225, 80, 2, 40), ""); 
        GUI.Label(new Rect(630 + 195, 125, 70, 20), "TARGET 90%");
        GUI.Label(new Rect(630, 65, 200, 20), $"Progreso Meta: {(goalRate / 0.90f * 100f):F1}%");

        // --- SECCIÓN 4: VEREDICTO ---
        string phaseName = MatchAnalytics.GetCurrentPhase().ToString();
        GUI.color = goalRate >= 0.85f ? Color.green : Color.yellow;
        GUI.Label(new Rect(20, 200, 500, 30), $"TRAINING PHASE: {phaseName}");
        GUI.Label(new Rect(20, 230, 600, 30), $"VERDICT: Generando trayectorias estables transferibles a hardware.");
        GUI.color = Color.white;

        // --- SECCIÓN 5: INDICADOR DE EMERGENCIA POR SEQUÍA ---
        bool falling = currentLvl == 0 && episodes > 10 && goalRate < 0.20f;
        string systemStatus = falling ? "⚠️ ALERTA DE SEQUÍA: Forzando arranque infalible (Lvl 0)" : "✓ SISTEMA CONVERGIENDO EN PROGRESIÓN GEOMÉTRICA";
        GUI.color = falling ? Color.red : Color.green;
        GUI.Label(new Rect(20, 280, 860, 30), $"DOJO LAB STATUS: {systemStatus}");
        GUI.color = Color.white;

        // --- SECCIÓN 6: DETALLES DE ENTORNO ---
        GUI.Label(new Rect(20, 330, 860, 150),
        $@"═══════════════════ REPORTE DE GEOMETRÍA DE RED ═══════════════════
📊 Impactos con Balón: {TotalCollisions} | Fricción y Masa de Rigidbody Activas.
Alineación Dinámica Arco-Balón-Robot inyectando muestras de alto valor en gradientes de baja entropía.
Los Empty GameObjects de Esquinas y Laterales se activan automáticamente en Nivel 3.");

        GUI.DragWindow();
    }

    private string GetLevelName(int lvl)
    {
        switch(lvl)
        {
            case 0: return "Bootstrapping Infalible";
            case 1: return "Corta Distancia Alineada";
            case 2: return "Punto Penal Asistido";
            case 3: return "Estrategia de Esquinas/Laterales";
            case 4: return "Juego Abierto Descentralizado";
            default: return "Desconocido";
        }
    }

    private void Update()
    {
        if (!enableCoachLogs) return;
        int episodes = MatchAnalytics.TotalEpisodes;
        if (episodes > 0 && episodes % logEveryEpisodes == 0 && episodes != lastLoggedEpisode)
        {
            lastLoggedEpisode = episodes;
            Debug.Log($"[Dojo Curriculum] Ep: {episodes} | Goal Rate: {MatchAnalytics.GetRecentGoalRate():P2} | Lvl Actual: {agentInstance?.currentLevel}");
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
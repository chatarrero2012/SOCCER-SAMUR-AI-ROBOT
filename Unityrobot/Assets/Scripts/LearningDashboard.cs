using UnityEngine;
using System.Collections.Generic;

public class LearningDashboard : MonoBehaviour
{
    [Header("Window")]
    public bool showDashboard = true;
    public Rect windowRect = new Rect(10, 10, 950, 500);

    [Header("Logging")]
    public bool enableCoachLogs = true;
    public int logEveryEpisodes = 100;
    private int lastLoggedEpisode = -1;

    private void OnGUI()
    {
        if (!showDashboard) return;
        // Estilo oscuro para que parezca una terminal de hacker/coach
        GUI.skin.window.normal.textColor = Color.cyan;
        windowRect = GUI.Window(12345, windowRect, DrawWindow, "⚽ SAMUR-AI COMMAND CENTER (Mastery)");
    }

    private void DrawWindow(int id)
    {
        int episodes = MatchAnalytics.TotalEpisodes;
        int goals = MatchAnalytics.TotalGoals;
        int ownGoals = MatchAnalytics.TotalOwnGoals;
        int touches = MatchAnalytics.BallTouches;

        float totalReward = MatchAnalytics.TotalReward;
        float rewardGoals = MatchAnalytics.RewardFromGoals;
        float rewardShaping = MatchAnalytics.RewardFromShaping;

        float goalRate = (float)goals / Mathf.Max(1, episodes);
        float rewardEfficiency = episodes > 0 ? totalReward / episodes : 0f;
        
        float totalMagnitude = Mathf.Abs(rewardShaping) + Mathf.Abs(rewardGoals) + Mathf.Abs(totalReward);
        float shapingDependency = rewardShaping / Mathf.Max(0.0001f, totalMagnitude);

        float avgBallSpeed = MatchAnalytics.AverageBallSpeed;
        float recentTouchRate = MatchAnalytics.GetRecentTouchRate();
        float recentGoalRate = MatchAnalytics.GetRecentGoalRate();

        MatchAnalytics.TrainingPhase phase = MatchAnalytics.GetCurrentPhase();
        
        string phaseName = phase == MatchAnalytics.TrainingPhase.Phase1_Fundamentos ? "FASE 1: FUNDAMENTOS" :
                           phase == MatchAnalytics.TrainingPhase.Phase2_Tecnica ? "FASE 2: TÉCNICA" : 
                           phase == MatchAnalytics.TrainingPhase.Phase3_Maestria ? "FASE 3: MAESTRÍA" : "FASE 4: ESTRATEGIA";

        string meritGoal = "   ";
        if (phase == MatchAnalytics.TrainingPhase.Phase1_Fundamentos)
            meritGoal = $"Meta: >60% Touch Rate (Actual: {recentTouchRate:P0})";
        else if (phase == MatchAnalytics.TrainingPhase.Phase2_Tecnica)
            meritGoal = $"Meta: Speed >0.5m/s & Goal (Speed: {avgBallSpeed:F2}, Recent: {recentGoalRate:P0})";
        else if (phase == MatchAnalytics.TrainingPhase.Phase3_Maestria)
            meritGoal = $"Meta: Dominio total. Peak Speed: {MatchAnalytics.PeakBallSpeed:F2} m/s";
        else
            meritGoal = "Meta: Guerra táctica. Presión defensiva y anticipación.";

        // --- SECCIÓN 1: CAMPAÑA Y RACHAS ---
        GUI.Label(new Rect(20, 30, 280, 180),
        $@"CAMPAIGN & MOMENTUM
Episodes:      {episodes}
Goals:         {goals} (Own: {ownGoals})
Global Rate:   {(goalRate * 100f):F2}%
Reward/Epis:   {rewardEfficiency:F2}");

        // --- SECCIÓN 2: RECOMPENSAS ---
        GUI.Label(new Rect(320, 30, 280, 180),
        $@"REWARDS ANALYSIS
Total:         {totalReward:F1}
Goal Rewards:  {rewardGoals:F1}
Shaping:       {rewardShaping:F1}
Shaping Dep:   {(shapingDependency * 100f):F1}%");

        // --- SECCIÓN 3: MÉTRICAS DEL BALÓN ---
        GUI.Label(new Rect(620, 30, 300, 180),
        $@"BALL METRICS
Touches:       {touches}
Avg Speed:     {avgBallSpeed:F2} m/s
Peak Speed:    {MatchAnalytics.PeakBallSpeed:F2} m/s");

        // --- SECCIÓN 4: CURRICULUM ---
        GUI.Label(new Rect(20, 160, 600, 100),
        $@"CURRICULUM DINÁMICO
Current Phase: {phaseName}
Progress:      {meritGoal}");

        // --- SECCIÓN 5: STATUS Y VEREDICTO (Con colores) ---
        string rank = goals == 0 ? "WANDERING RONIN" : (goalRate < 0.05f ? "BALL SEEKER" : (goalRate < 0.10f ? "BALL HUNTER" : "SAMURAI"));
        string verdict = avgBallSpeed < 0.1f ? "Agent is hugging the ball!" : (goalRate > 0.05f ? "Football discovered!" : "Learning in progress...");

        // Colorear el rango
        Color rankColor = goals == 0 ? Color.gray : (goalRate < 0.05f ? Color.yellow : (goalRate < 0.10f ? Color.green : Color.magenta));
        GUI.color = rankColor;
        GUI.Label(new Rect(20, 270, 300, 30), $"RANK: {rank}");
        
        // Colorear el veredicto
        GUI.color = avgBallSpeed < 0.1f ? Color.red : (goalRate > 0.05f ? Color.green : Color.white);
        GUI.Label(new Rect(20, 300, 600, 30), $"VERDICT: {verdict}");
        
        GUI.color = Color.white; // Reset color

        // --- BARRA DE PROGRESO VISUAL (Fase actual) ---
        float progressValue = phase == MatchAnalytics.TrainingPhase.Phase1_Fundamentos ? recentTouchRate / 0.60f :
                              phase == MatchAnalytics.TrainingPhase.Phase2_Tecnica ? recentGoalRate / 0.05f :
                              phase == MatchAnalytics.TrainingPhase.Phase3_Maestria ? MatchAnalytics.PeakBallSpeed / 3.0f : 1.0f;
        
        progressValue = Mathf.Clamp01(progressValue);
        Rect progressBarBg = new Rect(20, 350, 600, 20);
        Rect progressBarFg = new Rect(20, 350, 600 * progressValue, 20);

        GUI.Box(progressBarBg, "");
        GUI.color = Color.Lerp(Color.red, Color.green, progressValue);
        GUI.DrawTexture(progressBarFg, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(20, 350, 600, 20), $"Phase Progress: {(progressValue * 100):F0}%", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold });

        GUI.DragWindow();
    }

    private void Update()
    {
        if (!enableCoachLogs) return;
        int episodes = MatchAnalytics.TotalEpisodes;
        if (episodes > 0 && episodes % logEveryEpisodes == 0 && episodes != lastLoggedEpisode)
        {
            lastLoggedEpisode = episodes;
            PrintCoachReport();
        }
    }

    private void PrintCoachReport()
    {
        int episodes = MatchAnalytics.TotalEpisodes;
        int goals = MatchAnalytics.TotalGoals;
        float totalReward = MatchAnalytics.TotalReward;
        float rewardShaping = MatchAnalytics.RewardFromShaping;
        float rewardGoals = MatchAnalytics.RewardFromGoals;
        float goalRate = (float)goals / Mathf.Max(1, episodes);
        
        float totalMagnitude = Mathf.Abs(rewardShaping) + Mathf.Abs(rewardGoals) + Mathf.Abs(totalReward);
        float shapingDependency = rewardShaping / Mathf.Max(0.0001f, totalMagnitude);

        Debug.Log(
        $@"
═══════════════════════════════════════
⚔ SAMUR-AI COACH REPORT (Mastery)
═══════════════════════════════════════
Episodes: {episodes} | Goals: {goals} | Rate: {goalRate:P2}
Touches: {MatchAnalytics.BallTouches} | Phase: {MatchAnalytics.GetCurrentPhase()}
Recent Touch: {MatchAnalytics.GetRecentTouchRate():P0} | Recent Goal: {MatchAnalytics.GetRecentGoalRate():P0}
Avg Speed: {MatchAnalytics.AverageBallSpeed:F2} m/s | Peak: {MatchAnalytics.PeakBallSpeed:F2} m/s
Shaping Dependency: {shapingDependency:P1}
═══════════════════════════════════════
");
    }
}
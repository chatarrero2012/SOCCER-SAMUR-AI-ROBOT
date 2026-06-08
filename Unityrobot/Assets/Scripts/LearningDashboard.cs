using UnityEngine;

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
        windowRect = GUI.Window(12345, windowRect, DrawWindow, "⚽ SAMUR-AI COMMAND CENTER (Meta-Training)");
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
        
        // CORRECCIÓN META: Usar Mathf.Abs para evitar porcentajes negativos o infinitos cuando el reward total es negativo
        float shapingDependency = rewardShaping / Mathf.Max(0.0001f, Mathf.Abs(totalReward));
        float touchesPerGoal = (float)touches / Mathf.Max(1, goals);

        string phase = episodes < 100 ? "PHASE 1: Ball Seeking" : (episodes < 400 ? "PHASE 2: Dynamic Progress" : "PHASE 3: Full Training");

        float avgBallSpeed = MatchAnalytics.AverageBallSpeed;
        float peakBallSpeed = MatchAnalytics.PeakBallSpeed;
        float avgGoalTime = MatchAnalytics.AverageGoalTime;

        GUI.Label(new Rect(20, 30, 280, 200),
$@"CAMPAIGN
Episodes:   {episodes}
Goals:      {goals}
OwnGoals:   {ownGoals}
GoalRate:   {(goalRate * 100f):F2}%
Phase:      {phase}
");

        GUI.Label(new Rect(320, 30, 280, 200),
$@"REWARDS
Total:      {totalReward:F1}
Goal:       {rewardGoals:F1}
Shaping:    {rewardShaping:F1}
ShapingDep: {(shapingDependency * 100f):F1}%
");

        GUI.Label(new Rect(620, 30, 300, 200),
$@"META-METRICS
Touches (Ep): {touches}
Avg Speed:    {avgBallSpeed:F2} m/s  <-- CLAVE
Peak Speed:   {peakBallSpeed:F2} m/s
Avg Goal Time:{avgGoalTime:F2}s
");

        string rank = goals == 0 ? "WANDERING RONIN" : (goalRate < 0.05f ? "BALL SEEKER" : (goalRate < 0.15f ? "APPRENTICE STRIKER" : "SAMURAI"));
        string verdict = avgBallSpeed < 0.1f ? "Agent is hugging the ball!" : (goalRate > 0.1f ? "Football discovered!" : "Learning in progress...");

        GUI.Label(new Rect(20, 240, 600, 180),
$@"STATUS
Rank:       {rank}
Verdict:    {verdict}
Touches/Goal: {touchesPerGoal:F1}
");

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
        float goalRate = (float)goals / Mathf.Max(1, episodes);
        
        // Corrección de división
        float shapingDependency = rewardShaping / Mathf.Max(0.0001f, Mathf.Abs(totalReward));

        Debug.Log(
$@"
═══════════════════════════════════════
⚔ SAMUR-AI COACH REPORT (Meta-Training)
═══════════════════════════════════════
Episodes: {episodes}
Goals: {goals}
GoalRate: {goalRate:P2}
BallTouches (Ep): {MatchAnalytics.BallTouches}
TotalReward: {totalReward:F2}
ShapingDependency: {shapingDependency:P1}
Avg Ball Speed: {MatchAnalytics.AverageBallSpeed:F2} m/s  <-- OBSERVAR ESTE VALOR
═══════════════════════════════════════
");
    }
}
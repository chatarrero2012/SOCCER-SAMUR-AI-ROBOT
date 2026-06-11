using UnityEngine;

public class LearningDashboard : MonoBehaviour
{
    [Header("Window")]
    public bool showDashboard = true;
    public Rect windowRect = new Rect(10, 10, 900, 450);
    
    [Header("Logging")]
    public bool enableCoachLogs = true;
    public int logEveryEpisodes = 100;
    private int lastLoggedEpisode = -1;

    private void OnGUI()
    {
        if (!showDashboard) return;
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
        
        float totalMagnitude = Mathf.Abs(rewardShaping) + Mathf.Abs(rewardGoals) + Mathf.Abs(totalReward);
        float shapingDependency = rewardShaping / Mathf.Max(0.0001f, totalMagnitude);

        float avgBallSpeed = MatchAnalytics.AverageBallSpeed;
        float recentTouchRate = MatchAnalytics.GetRecentTouchRate();
        float recentGoalRate = MatchAnalytics.GetRecentGoalRate();

        MatchAnalytics.TrainingPhase phase = MatchAnalytics.GetCurrentPhase();
        
        string phaseName = phase == MatchAnalytics.TrainingPhase.Phase1_Fundamentos ? "FASE 1: FUNDAMENTOS" :
                           phase == MatchAnalytics.TrainingPhase.Phase2_Tecnica ? "FASE 2: TÉCNICA" : 
                           phase == MatchAnalytics.TrainingPhase.Phase3_Maestria ? "FASE 3: MAESTRÍA" : "FASE 4: ESTRATEGIA";

        string meritGoal = " ";
        if (phase == MatchAnalytics.TrainingPhase.Phase1_Fundamentos)
            meritGoal = $"Meta: >60% Touch Rate (Actual: {recentTouchRate:P0})";
        else if (phase == MatchAnalytics.TrainingPhase.Phase2_Tecnica)
            meritGoal = $"Meta: Speed >0.5m/s & Goal (Speed: {avgBallSpeed:F2}, Recent Goals: {recentGoalRate:P0})";
        else if (phase == MatchAnalytics.TrainingPhase.Phase3_Maestria)
            meritGoal = $"Meta: Dominio total. Peak Speed: {MatchAnalytics.PeakBallSpeed:F2} m/s";
        else
            meritGoal = "Meta: Guerra táctica. Presión defensiva y anticipación.";

        GUI.Label(new Rect(20, 30, 260, 180),
$@"CAMPAIGN
Episodes:   {episodes}
Goals:      {goals}
OwnGoals:   {ownGoals}
Global Rate:{(goalRate * 100f):F2}%
");
        GUI.Label(new Rect(300, 30, 260, 180),
$@"REWARDS
Total:      {totalReward:F1}
Goal:       {rewardGoals:F1}
Shaping:    {rewardShaping:F1}
ShapingDep: {(shapingDependency * 100f):F1}%
");
        GUI.Label(new Rect(580, 30, 280, 180),
$@"BALL METRICS
Touches:    {touches}
Avg Speed:  {avgBallSpeed:F2} m/s
Peak Speed: {MatchAnalytics.PeakBallSpeed:F2} m/s
");
        GUI.Label(new Rect(20, 150, 500, 100),
$@"CURRICULUM DINÁMICO
Current Phase: {phaseName}
Progress: {meritGoal}
");
        string rank = goals == 0 ? "WANDERING RONIN" : (goalRate < 0.05f ? "BALL SEEKER" : (goalRate < 0.10f ? "BALL HUNTER" : "SAMURAI"));
        string verdict = avgBallSpeed < 0.1f ? "Agent is hugging the ball!" : (goalRate > 0.05f ? "Football discovered!" : "Learning in progress...");

        GUI.Label(new Rect(20, 260, 600, 180),
$@"STATUS
Rank:       {rank}
Verdict:    {verdict}
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
        float rewardGoals = MatchAnalytics.RewardFromGoals;
        float goalRate = (float)goals / Mathf.Max(1, episodes);
        
        float totalMagnitude = Mathf.Abs(rewardShaping) + Mathf.Abs(rewardGoals) + Mathf.Abs(totalReward);
        float shapingDependency = rewardShaping / Mathf.Max(0.0001f, totalMagnitude);

        Debug.Log(
$@"
═══════════════════════════════════════
⚔ SAMUR-AI COACH REPORT (Mastery)
═══════════════════════════════════════
Episodes: {episodes}
Goals: {goals}
GoalRate: {goalRate:P2}
BallTouches: {MatchAnalytics.BallTouches}
Current Phase: {MatchAnalytics.GetCurrentPhase()}
Recent Touch Rate: {MatchAnalytics.GetRecentTouchRate():P0}
Recent Goal Rate: {MatchAnalytics.GetRecentGoalRate():P0}
Avg Ball Speed: {MatchAnalytics.AverageBallSpeed:F2} m/s
Peak Ball Speed: {MatchAnalytics.PeakBallSpeed:F2} m/s
ShapingDependency: {shapingDependency:P1}
═══════════════════════════════════════
");
    }
}
using UnityEngine;

public class LearningDashboard : MonoBehaviour
{
    [Header("Window")]
    public bool showDashboard = true;

    public Rect windowRect =
        new Rect(10, 10, 900, 420);

    [Header("Logging")]
    public bool enableCoachLogs = true;

    public int logEveryEpisodes = 100;

    private int lastLoggedEpisode = -1;

    // =====================================================
    // GUI
    // =====================================================

    private void OnGUI()
    {
        if (!showDashboard)
            return;

        windowRect = GUI.Window(
            12345,
            windowRect,
            DrawWindow,
            "⚽ SAMUR-AI COMMAND CENTER"
        );
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

        float goalRate =
            (float)goals /
            Mathf.Max(1, episodes);

        float ownGoalRate =
            (float)ownGoals /
            Mathf.Max(1, episodes);

        float touchesPerGoal =
            (float)touches /
            Mathf.Max(1, goals);

        float rewardPerGoal =
            totalReward /
            Mathf.Max(1, goals);

        float rewardPerEpisode =
            totalReward /
            Mathf.Max(1, episodes);

        float shapingDependency =
            rewardShaping /
            Mathf.Max(0.0001f, totalReward);

        float precision =
            (float)goals /
            Mathf.Max(1, touches);

        float aggression =
            goalRate *
            (rewardGoals /
            Mathf.Max(1f, totalReward));

        float obsession =
            touches /
            Mathf.Max(1f, goals * 10f);

        float chaos =
            ownGoalRate * 10f;

        float confidence =
            Mathf.Clamp01(goalRate * 5f);

        float frustration =
            Mathf.Clamp01(touchesPerGoal / 100f);

        float focus =
            Mathf.Clamp01(precision * 10f);

        float curiosity =
            Mathf.Clamp01(shapingDependency);

        float footballIQ =
            Mathf.Clamp(
                (
                    goalRate * 50f +
                    precision * 30f +
                    confidence * 20f
                ) * 100f,
                0f,
                100f
            );

        float rewardHackRisk = 0f;

        if (rewardPerGoal > 100f &&
            goalRate < 0.05f)
        {
            rewardHackRisk += 0.5f;
        }

        if (shapingDependency > 0.75f)
        {
            rewardHackRisk += 0.5f;
        }

        rewardHackRisk =
            Mathf.Clamp01(rewardHackRisk);

        string rank;

        if (goals == 0)
            rank = "WANDERING RONIN";
        else if (goalRate < 0.05f)
            rank = "BALL SEEKER";
        else if (goalRate < 0.10f)
            rank = "BALL HUNTER";
        else if (goalRate < 0.20f)
            rank = "APPRENTICE STRIKER";
        else if (goalRate < 0.40f)
            rank = "SAMURAI";
        else if (goalRate < 0.60f)
            rank = "SHOGUN";
        else
            rank = "FOOTBALL DAIMYO";

        string verdict;

        if (rewardHackRisk > 0.75f)
        {
            verdict =
                "Possible reward farming detected";
        }
        else if (
            touches > 1000 &&
            goals == 0)
        {
            verdict =
                "Obsessed with ball. Goal forgotten.";
        }
        else if (
            goalRate > 0.25f)
        {
            verdict =
                "Football discovered.";
        }
        else
        {
            verdict =
                "Learning in progress.";
        }

        GUI.Label(
            new Rect(20, 30, 260, 180),
$@"CAMPAIGN

Episodes:   {episodes}
Goals:      {goals}
OwnGoals:   {ownGoals}

GoalRate:   {(goalRate * 100f):F2}%
");
        
        GUI.Label(
            new Rect(300, 30, 260, 180),
$@"REWARDS

Total:      {totalReward:F1}
Goal:       {rewardGoals:F1}
Shaping:    {rewardShaping:F1}

PerEpisode: {rewardPerEpisode:F2}
PerGoal:    {rewardPerGoal:F2}
");
        
        GUI.Label(
            new Rect(580, 30, 280, 180),
$@"BALL

Touches:      {touches}
Touch/Goal:   {touchesPerGoal:F1}

Precision:    {(precision*100f):F1}%
");
        
        GUI.Label(
            new Rect(20, 150, 260, 180),
$@"PERSONALITY

Aggression
{Bar(aggression)}

Obsession
{Bar(Mathf.Clamp01(obsession/10f))}

Chaos
{Bar(Mathf.Clamp01(chaos/10f))}
");
        
        GUI.Label(
            new Rect(300, 150, 260, 180),
$@"MOOD

Confidence
{Bar(confidence)}

Frustration
{Bar(frustration)}

Focus
{Bar(focus)}

Curiosity
{Bar(curiosity)}
");
        
        GUI.Label(
            new Rect(580, 150, 280, 220),
$@"STATUS

Football IQ
{footballIQ:F0}/100

Reward Risk
{(rewardHackRisk*100f):F0}%

Rank
{rank}

Verdict
{verdict}
");

        GUI.DragWindow();
    }

    // =====================================================
    // LOGGING
    // =====================================================

    private void Update()
    {
        if (!enableCoachLogs)
            return;

        int episodes =
            MatchAnalytics.TotalEpisodes;

        if (
            episodes > 0 &&
            episodes % logEveryEpisodes == 0 &&
            episodes != lastLoggedEpisode)
        {
            lastLoggedEpisode = episodes;

            PrintCoachReport();
        }
    }

    // =====================================================
    // REPORT
    // =====================================================

    private void PrintCoachReport()
    {
        int episodes = MatchAnalytics.TotalEpisodes;
        int goals = MatchAnalytics.TotalGoals;
        int ownGoals = MatchAnalytics.TotalOwnGoals;
        int touches = MatchAnalytics.BallTouches;

        float totalReward =
            MatchAnalytics.TotalReward;

        float rewardGoals =
            MatchAnalytics.RewardFromGoals;

        float rewardShaping =
            MatchAnalytics.RewardFromShaping;

        float goalRate =
            (float)goals /
            Mathf.Max(1, episodes);

        float shapingDependency =
            rewardShaping /
            Mathf.Max(0.0001f, totalReward);

        float touchesPerGoal =
            (float)touches /
            Mathf.Max(1, goals);

        Debug.Log(
$@"

═══════════════════════════════════════
⚔ SAMUR-AI COACH REPORT
═══════════════════════════════════════

Episodes
{episodes}

Goals
{goals}

OwnGoals
{ownGoals}

GoalRate
{goalRate:P2}

BallTouches
{touches}

TouchesPerGoal
{touchesPerGoal:F1}

TotalReward
{totalReward:F2}

RewardFromGoals
{rewardGoals:F2}

RewardFromShaping
{rewardShaping:F2}

ShapingDependency
{shapingDependency:P1}

═══════════════════════════════════════

");
    }

    // =====================================================
    // BAR
    // =====================================================

    private string Bar(float value)
    {
        value =
            Mathf.Clamp01(value);

        int filled =
            Mathf.RoundToInt(value * 10f);

        string result = "";

        for (int i = 0; i < 10; i++)
        {
            result +=
                i < filled
                ? "█"
                : "░";
        }

        return result;
    }
}
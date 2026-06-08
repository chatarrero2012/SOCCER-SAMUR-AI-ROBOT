using UnityEngine;

public static class MatchAnalytics
{
    public static int TotalEpisodes;
    public static int TotalGoals;
    public static int TotalOwnGoals;
    public static int BallTouches; // Episodios con al menos 1 toque exitoso

    public static float TotalReward;
    public static float RewardFromGoals;
    public static float RewardFromShaping;

    // Métricas de Meta-Análisis
    public static float TotalEpisodeTime;
    public static float AverageGoalTime;
    public static float FastestGoalTime;
    public static float SlowestGoalTime;

    public static float AverageBallSpeed;
    public static float PeakBallSpeed;
    public static float AverageDistanceBallToGoal;
    public static float BestDistanceBallToGoal;
    public static float BallProgressPerEpisode;

    private static int goalCountForTime;
    private static int speedSamples;
    private static int distanceSamples;

    public static void Reset()
    {
        TotalEpisodes = 0; TotalGoals = 0; TotalOwnGoals = 0; BallTouches = 0;
        TotalReward = 0f; RewardFromGoals = 0f; RewardFromShaping = 0f;
        TotalEpisodeTime = 0f; AverageGoalTime = 0f; FastestGoalTime = float.MaxValue; SlowestGoalTime = 0f;
        AverageBallSpeed = 0f; PeakBallSpeed = 0f; AverageDistanceBallToGoal = 0f; BestDistanceBallToGoal = float.MaxValue;
        BallProgressPerEpisode = 0f;
        goalCountForTime = 0; speedSamples = 0; distanceSamples = 0;
    }

    public static void RecordGoalTime(float time)
    {
        goalCountForTime++;
        TotalEpisodeTime += time;
        AverageGoalTime = TotalEpisodeTime / goalCountForTime;
        if (time < FastestGoalTime) FastestGoalTime = time;
        if (time > SlowestGoalTime) SlowestGoalTime = time;
    }

    public static void RecordBallSpeed(float speed)
    {
        speedSamples++;
        AverageBallSpeed = ((AverageBallSpeed * (speedSamples - 1)) + speed) / speedSamples;
        if (speed > PeakBallSpeed) PeakBallSpeed = speed;
    }

    public static void RecordBallDistanceToGoal(float distance)
    {
        distanceSamples++;
        AverageDistanceBallToGoal = ((AverageDistanceBallToGoal * (distanceSamples - 1)) + distance) / distanceSamples;
        if (distance < BestDistanceBallToGoal) BestDistanceBallToGoal = distance;
    }

    public static void RecordBallProgress(float progress)
    {
        BallProgressPerEpisode += progress;
    }

    public static void RecordBallTouch() { BallTouches++; }
    public static void AddReward(float amount) { TotalReward += amount; }
    public static void AddGoalReward(float amount) { RewardFromGoals += amount; }
    public static void AddShapingReward(float amount) { RewardFromShaping += amount; }
}
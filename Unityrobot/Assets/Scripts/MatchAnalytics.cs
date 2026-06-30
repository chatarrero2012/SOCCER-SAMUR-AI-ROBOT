using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// METRIC ENGINE - MATCH ANALYTICS (Edición Definitiva V4)
/// Cálculos estables en ventana móvil de 50 episodios para monitorear el progreso exacto.
/// </summary>
public static class MatchAnalytics
{
    public static int TotalEpisodes;
    public static int TotalGoals;
    public static int TotalOwnGoals;
    public static int BallTouches;
    public static float TotalReward;
    public static float RewardFromGoals;
    public static float RewardFromShaping;
    public static float AverageBallSpeed;
    public static float PeakBallSpeed;

    private static Queue<bool> recentTouches = new Queue<bool>();
    private static Queue<bool> recentGoals = new Queue<bool>();
    private static Queue<float> recentSpeeds = new Queue<float>();
    private const int WINDOW_SIZE = 50;

    public enum TrainingPhase { Phase1_Fundamentos, Phase2_Tecnica, Phase3_Maestria, Phase4_Estrategia }

    public static void Reset()
    {
        TotalEpisodes = 0; TotalGoals = 0; TotalOwnGoals = 0; BallTouches = 0;
        TotalReward = 0f; RewardFromGoals = 0f; RewardFromShaping = 0f;
        AverageBallSpeed = 0f; PeakBallSpeed = 0f;
        recentTouches.Clear(); recentGoals.Clear(); recentSpeeds.Clear();
    }

    public static void RecordEpisodeResult(bool touchedBall, bool scoredGoal, float avgSpeedInEpisode)
    {
        TotalEpisodes++;
        if (touchedBall) BallTouches++;
        if (scoredGoal) TotalGoals++;

        if (recentTouches.Count >= WINDOW_SIZE) recentTouches.Dequeue();
        recentTouches.Enqueue(touchedBall);

        if (recentGoals.Count >= WINDOW_SIZE) recentGoals.Dequeue();
        recentGoals.Enqueue(scoredGoal);

        if (recentSpeeds.Count >= WINDOW_SIZE) recentSpeeds.Dequeue();
        recentSpeeds.Enqueue(avgSpeedInEpisode);
        
        AverageBallSpeed = 0f;
        foreach (float s in recentSpeeds) AverageBallSpeed += s;
        if (recentSpeeds.Count > 0) AverageBallSpeed /= recentSpeeds.Count;
    }

    public static void RecordBallSpeed(float speed)
    {
        if (speed > PeakBallSpeed) PeakBallSpeed = speed;
    }

    public static void AddReward(float amount) { TotalReward += amount; }
    public static void AddGoalReward(float amount) { RewardFromGoals += amount; }
    public static void AddShapingReward(float amount) { RewardFromShaping += amount; }

    public static float GetRecentGoalRate() 
    { 
        float rate = 0f; 
        foreach (bool g in recentGoals) if (g) rate++; 
        return recentGoals.Count > 0 ? rate / recentGoals.Count : 0f; 
    }

    public static float GetRecentTouchRate() 
    { 
        float rate = 0f; 
        foreach (bool t in recentTouches) if (t) rate++; 
        return recentTouches.Count > 0 ? rate / recentTouches.Count : 0f; 
    }

    public static TrainingPhase GetCurrentPhase()
    {
        if (TotalEpisodes < 20) return TrainingPhase.Phase1_Fundamentos;

        float recentTouchRate = GetRecentTouchRate();
        float recentGoalRate = GetRecentGoalRate();

        if (recentTouchRate > 0.40f)
        {
            if (recentGoalRate > 0.05f)
            {
                if (TotalEpisodes > 100 && recentGoalRate > 0.12f)
                {
                    return TrainingPhase.Phase4_Estrategia;
                }
                return TrainingPhase.Phase3_Maestria;
            }
            return TrainingPhase.Phase2_Tecnica;
        }

        return TrainingPhase.Phase1_Fundamentos;
    }
}
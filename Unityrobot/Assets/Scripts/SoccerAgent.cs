using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.IO;
using System.Text;

public class SoccerAgent : Agent
{
    [Header("Vision")]
    public SimulatedYoloDetector yolo;

    [Header("Motor Driver")]
    public SimulatedMotorDriver motorDriver;

    [Header("Targets")]
    public Transform ball;
    public Transform enemyGoal;
    public Transform ownGoal;
    public Transform enemy;

    [Header("Rewards (Minimalist & Kick-Focused)")]
    public float goalReward = 1000f;
    public float ownGoalPenalty = -50f;
    public float stepPenalty = -0.01f;
    public float kickRewardMultiplier = 15.0f; 
    public float minKickSpeed = 2.5f;
    public float minAlignmentForKick = 0.6f;

    [Header("Spawn & Physics")]
    public Rigidbody robotRb;
    public Rigidbody ballRb;
    public Transform robotRoot;
    public Vector3 robotSpawnCenter;
    public Vector3 ballSpawnCenter;
    public float robotSpawnRadius = 0.4f;
    public float ballSpawnRadius = 0.4f;
    public float fallHeight = -0.5f;
    public float fallPenalty = -20f;
    public float maxEpisodeSeconds = 20f;

    [Header("Personality Weights")]
    [Tooltip("X:Ofensiva | Y:Técnica | Z:Velocidad | W:Disciplina")]
    public Vector4 personality = new Vector4(1f, 1f, 1f, 1f);

    // =====================================================
    // 🎓 SHAPING ANNEALING (Quitar las rueditas gradualmente)
    // =====================================================
    [Header("Shaping Annealing (Curriculum de Desvanecimiento)")]
    [Tooltip("0.0 = Shaping completo, 1.0 = Solo gol (sparse rewards)")]
    [Range(0f, 1f)] public float shapingFade = 0.0f;
    
    [Tooltip("Multiplicador base para el shaping de distancia (se reduce con shapingFade)")]
    public float baseDistanceShapingMultiplier = 2.0f;
    
    [Tooltip("Umbral mínimo de mejora de distancia para dar recompensa")]
    public float distanceShapingThreshold = 0.15f;
    
    [Tooltip("Recompensa por primer toque (se reduce con shapingFade)")]
    public float baseFirstTouchReward = 5.0f;
    
    [Tooltip("Radio para considerar que el robot 'toca' el balón")]
    public float touchRadius = 0.8f;

    // --- SISTEMA DE HOOKS / TELEMETRÍA EN C# ---
    [Header("Telemetry & Hooks")]
    public bool enableTelemetryLogging = false;
    private StreamWriter telemetryWriter;
    private string logFilePath;

    private Vector4 _p;
    private float previousBallDistToEnemyGoal;
    private float episodeTimer;
    private bool hasReachedBallThisEpisode;
    private bool scoredGoalThisEpisode;
    private float episodeAvgSpeed;
    private int speedSamples;

    // =====================================================
    // CÁLCULO DE MULTIPLICADORES SEGÚN SHAPING FADE
    // =====================================================
    private float CurrentDistanceShapingMultiplier => 
        baseDistanceShapingMultiplier * (1f - shapingFade);
    
    private float CurrentFirstTouchReward => 
        baseFirstTouchReward * (1f - shapingFade);

    private void Awake()
    {
        _p = new Vector4(
            Mathf.Clamp(personality.x, 0.1f, 3.0f),
            Mathf.Clamp(personality.y, 0.1f, 2.0f),
            Mathf.Clamp(personality.z, 0.1f, 2.5f),
            Mathf.Clamp(personality.w, 0.1f, 3.0f)
        );

        if (enableTelemetryLogging)
        {
            logFilePath = Path.Combine(Application.dataPath, $"AgentTelemetry_{System.DateTime.Now:yyyyMMdd_HHmmss}.csv");
            telemetryWriter = new StreamWriter(logFilePath);
            telemetryWriter.WriteLine("Step,Time,Ball_X,Ball_Y,Ball_Size,Action_Left,Action_Right,Reward");
        }
    }

    public override void OnEpisodeBegin()
    {
        episodeTimer = 0f;
        hasReachedBallThisEpisode = false;
        scoredGoalThisEpisode = false;
        episodeAvgSpeed = 0f;
        speedSamples = 0;
         
        motorDriver.SetMotorInputs(0f, 0f);
        SpawnEpisode();
        
        previousBallDistToEnemyGoal = Vector3.Distance(ball.position, enemyGoal.position);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        SimulatedYoloDetector.Detection ballDetection = default;
        bool seesBall = TryGetDetection(ball, out ballDetection);

        AddDetectionObservation(sensor, ball);
        AddDetectionObservation(sensor, enemyGoal);
        AddDetectionObservation(sensor, ownGoal);  
        AddDetectionObservation(sensor, enemy);
        
        sensor.AddObservation(_p.x);
        sensor.AddObservation(_p.y);
        sensor.AddObservation(_p.z);
        sensor.AddObservation(_p.w);
        sensor.AddObservation(motorDriver.currentBatteryCharge);

        OnObservationCollected(seesBall, ballDetection);
    }

    private void OnObservationCollected(bool seesTarget, SimulatedYoloDetector.Detection detection)
    {
        if (enableTelemetryLogging && seesTarget)
        {
            // Hook para depuración
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float leftMotor = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f); 
        float rightMotor = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
        
        OnActionDecided(leftMotor, rightMotor);
        motorDriver.SetMotorInputs(leftMotor, rightMotor);

        CheckFallConditions();
        ApplyDynamicRewards();
        RecordBallMetrics();

        AddReward(stepPenalty);
        MatchAnalytics.AddReward(stepPenalty);

        episodeTimer += Time.fixedDeltaTime;
        
        if (episodeTimer >= maxEpisodeSeconds)
        {
            FinishEpisode();
        }
    }

    private void OnActionDecided(float left, float right)
    {
        if (enableTelemetryLogging && telemetryWriter != null)
        {
            SimulatedYoloDetector.Detection d;
            float ballX = TryGetDetection(ball, out d) ? d.normalizedCenter.x : -1f;
            string logLine = $"{speedSamples},{episodeTimer:F2},{ballX:F3},{left:F3},{right:F3},{GetCumulativeReward():F3}";
            telemetryWriter.WriteLine(logLine);
        }
    }

    // =====================================================
    // 🎓 RECOMPENSAS CON SHAPING ANNEALING
    // =====================================================
    private void ApplyDynamicRewards()
    {
        float ballSpeed = ballRb.velocity.magnitude;
        Vector3 dirToGoal = (enemyGoal.position - ball.position).normalized;
        float alignment = ballSpeed > 0.1f ? Vector3.Dot(ballRb.velocity.normalized, dirToGoal) : 0f;

        // 1. KICK REWARD (Se mantiene, pero se puede reducir si quieres)
        if (ballSpeed > minKickSpeed && alignment > minAlignmentForKick)
        {
            float kickReward = (ballSpeed * alignment) * kickRewardMultiplier * _p.x;
            AddReward(kickReward);
            MatchAnalytics.AddShapingReward(kickReward);
        }

        // 2. FIRST TOUCH REWARD (Se desvanece con shapingFade)
        float distToBall = Vector3.Distance(robotRoot.position, ball.position);
        if (distToBall < touchRadius && !hasReachedBallThisEpisode)
        {
            hasReachedBallThisEpisode = true;
            float firstTouchReward = CurrentFirstTouchReward;
            if (firstTouchReward > 0.01f) // Solo dar si es significativo
            {
                AddReward(firstTouchReward);
                MatchAnalytics.AddShapingReward(firstTouchReward);
            }
        }

        // 3. DISTANCE SHAPING (Se desvanece con shapingFade)
        float currentBallDistToEnemyGoal = Vector3.Distance(ball.position, enemyGoal.position);
        float distImprovement = previousBallDistToEnemyGoal - currentBallDistToEnemyGoal;
        
        float distanceMultiplier = CurrentDistanceShapingMultiplier;
        if (distImprovement > distanceShapingThreshold && distanceMultiplier > 0.01f)
        {
            float shapingReward = distImprovement * distanceMultiplier;
            AddReward(shapingReward);
            MatchAnalytics.AddShapingReward(shapingReward);
        }
        
        previousBallDistToEnemyGoal = currentBallDistToEnemyGoal;
    }

    private void RecordBallMetrics()
    {  
        float speed = ballRb.velocity.magnitude;
        MatchAnalytics.RecordBallSpeed(speed);
        episodeAvgSpeed = ((episodeAvgSpeed * speedSamples) + speed) / (speedSamples + 1);
        speedSamples++;
    }

    private void AddDetectionObservation(VectorSensor sensor, Transform target)
    {
        if (TryGetDetection(target, out SimulatedYoloDetector.Detection d))
        {
            sensor.AddObservation(1f);
            sensor.AddObservation(d.normalizedCenter.x);
            sensor.AddObservation(d.normalizedCenter.y);
            sensor.AddObservation(d.normalizedSize.x);
            sensor.AddObservation(d.normalizedSize.y);
        }
        else
        {
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
            sensor.AddObservation(0f);
        }
    }

    private bool TryGetDetection(Transform target, out SimulatedYoloDetector.Detection result)
    {
        foreach (var d in yolo.detections)
        {
            if (d.target == target) 
            { 
                result = d; 
                return true; 
            }
        }
        result = default;
        return false;
    }

    public void OnGoalScored()
    {
        scoredGoalThisEpisode = true;
        float reward = goalReward * _p.x;
        AddReward(reward);
        MatchAnalytics.AddGoalReward(reward);
        Debug.Log($"⚽ GOOOLLLLLL - Reward: {reward:F1}");
        FinishEpisode();
    }

    public void OnOwnGoal()
    {
        scoredGoalThisEpisode = false;
        Debug.Log("❌ GOL EN PROPIA");
        AddReward(ownGoalPenalty); 
        MatchAnalytics.TotalOwnGoals++;
        FinishEpisode();
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var actions = actionsOut.ContinuousActions;
        float left = 0f, right = 0f;
        if (Input.GetKey(KeyCode.W)) { left = 1f; right = 1f; }
        if (Input.GetKey(KeyCode.S)) { left = -1f; right = -1f; } 
        if (Input.GetKey(KeyCode.A)) { left = -1f; right = 1f; }
        if (Input.GetKey(KeyCode.D)) { left = 1f; right = -1f; }
        actions[0] = left; 
        actions[1] = right;
    }

    private void CheckFallConditions()
    {
        if (robotRoot.position.y < fallHeight)
        {
            AddReward(fallPenalty);
            FinishEpisode();
            return;
        }
        if (ball.position.y < fallHeight)
        {
            FinishEpisode();
            return;
        }
    }

    private void SpawnEpisode()
    {
        Vector2 robotOffset = Random.insideUnitCircle * robotSpawnRadius;
        robotRoot.position = robotSpawnCenter + new Vector3(robotOffset.x, 0f, robotOffset.y);
        robotRoot.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        robotRb.velocity = Vector3.zero; 
        robotRb.angularVelocity = Vector3.zero;

        Vector2 ballOffset = Random.insideUnitCircle * ballSpawnRadius;
        ball.position = ballSpawnCenter + new Vector3(ballOffset.x, 0f, ballOffset.y);
        ballRb.velocity = Vector3.zero; 
        ballRb.angularVelocity = Vector3.zero;
    }

    private void FinishEpisode()
    {
        MatchAnalytics.RecordEpisodeResult(hasReachedBallThisEpisode, scoredGoalThisEpisode, episodeAvgSpeed);
        
        if (enableTelemetryLogging && telemetryWriter != null)
        {
            telemetryWriter.Flush();
        }
        
        EndEpisode();
    }

    private void OnDestroy()
    {
        if (telemetryWriter != null)
        {
            telemetryWriter.Close();
            telemetryWriter.Dispose();
        }
    }
}
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.IO;
using System.Text;
using System.Collections.Generic;

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
    
    // 🛠️ FIX 1 & 2: BARRA DE ENTRADA BAJADA (Para que sí logre patear)
    public float minKickSpeed = 1.2f;         // Antes era 2.5f
    public float minAlignmentForKick = 0.4f;  // Antes era 0.6f

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

    [Header("Shaping Annealing (Curriculum de Desvanecimiento)")]
    [Tooltip("0.0 = Shaping completo, 1.0 = Solo gol (sparse rewards)")]
    [Range(0f, 1f)] public float shapingFade = 0.0f;
    public float baseDistanceShapingMultiplier = 2.0f;
    public float distanceShapingThreshold = 0.15f;
    
    // 🛠️ FIX 3: RECOMPENSA POR TOQUE REDUCIDA (Para que no se vuelva rico solo tocándolo)
    public float baseFirstTouchReward = 1.0f; // Antes era 5.0f
    public float touchRadius = 0.8f;

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
    // 🧠 FIX: TRACKING DE OBSERVACIONES PARA EL VISUALIZADOR
    // =====================================================
    private List<float> lastObservationsList = new List<float>();

    public float[] GetLastObservations()
    {
        return lastObservationsList.ToArray();
    }

    private void AddObs(VectorSensor sensor, float val)
    {
        sensor.AddObservation(val);
        lastObservationsList.Add(val);
    }

    // FIX: Corregido el espacio en "= >" para que sea una propiedad válida "=>"
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
        lastObservationsList.Clear(); // Limpiamos para el frame actual

        SimulatedYoloDetector.Detection ballDetection = default;
        bool seesBall = TryGetDetection(ball, out ballDetection);

        AddDetectionObservation(sensor, ball);
        AddDetectionObservation(sensor, enemyGoal);
        AddDetectionObservation(sensor, ownGoal);  
        AddDetectionObservation(sensor, enemy);
        
        AddObs(sensor, _p.x);
        AddObs(sensor, _p.y);
        AddObs(sensor, _p.z);
        AddObs(sensor, _p.w);
        AddObs(sensor, motorDriver.currentBatteryCharge);

        OnObservationCollected(seesBall, ballDetection);
    }

    private void OnObservationCollected(bool seesTarget, SimulatedYoloDetector.Detection detection) 
    {
        if (enableTelemetryLogging && seesTarget) { }
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

    private void ApplyDynamicRewards()
    {
        float ballSpeed = ballRb.velocity.magnitude;
        Vector3 dirToGoal = (enemyGoal.position - ball.position).normalized;
        float alignment = ballSpeed > 0.1f ? Vector3.Dot(ballRb.velocity.normalized, dirToGoal) : 0f;
        
        // Calculamos la distancia al balón aquí para reutilizarla
        float distToBall = Vector3.Distance(robotRoot.position, ball.position);

        // 1. RECOMPENSA POR PATADA (Umbral facilitado)
        if (ballSpeed > minKickSpeed && alignment > minAlignmentForKick)
        {
            float kickReward = (ballSpeed * alignment) * kickRewardMultiplier * _p.x;
            AddReward(kickReward);
            MatchAnalytics.AddShapingReward(kickReward);
        }

        // 2. PRIMER TOQUE (Recompensa reducida para evitar el hackeo inicial)
        if (distToBall < touchRadius && !hasReachedBallThisEpisode)
        {
            hasReachedBallThisEpisode = true;
            float firstTouchReward = CurrentFirstTouchReward;
            if (firstTouchReward > 0.01f) 
            {
                AddReward(firstTouchReward);
                MatchAnalytics.AddShapingReward(firstTouchReward);
            }
        }

        // 🛠️ FIX 4: CASTIGO POR ABRAZAR EL BALÓN (Anti-Hugging)
        // Si está cerca del balón PERO el balón no se mueve, ¡penalizar!
        if (distToBall < touchRadius && ballSpeed < 0.5f)
        {
            float hugPenalty = -0.05f;
            AddReward(hugPenalty);
            MatchAnalytics.AddShapingReward(hugPenalty);
        }

        // 🛠️ FIX 5: RECOMPENSA CONTINUA POR AVANZAR EL BALÓN
        // Fomenta que lo mantenga empujando hacia la portería en cada frame
        float forwardBallSpeed = Vector3.Dot(ballRb.velocity, dirToGoal);
        if (forwardBallSpeed > 0.1f)
        {
            float continuousReward = forwardBallSpeed * 0.5f * _p.x;
            AddReward(continuousReward);
            MatchAnalytics.AddShapingReward(continuousReward);
        }

        // 3. SHAPING POR DISTANCIA (Original)
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
            AddObs(sensor, 1f);
            AddObs(sensor, d.normalizedCenter.x);
            AddObs(sensor, d.normalizedCenter.y);
            AddObs(sensor, d.normalizedSize.x);
            AddObs(sensor, d.normalizedSize.y);
        }
        else
        {
            AddObs(sensor, 0f); AddObs(sensor, 0f); AddObs(sensor, 0f);
            AddObs(sensor, 0f); AddObs(sensor, 0f);
        }
    }

    private bool TryGetDetection(Transform target, out SimulatedYoloDetector.Detection result)
    {
        foreach (var d in yolo.detections)
        {
            if (d.target == target) { result = d; return true; }
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
        actions[0] = left; actions[1] = right;
    }

    private void CheckFallConditions()
    {
        if (robotRoot.position.y < fallHeight) { AddReward(fallPenalty); FinishEpisode(); return; }
        if (ball.position.y < fallHeight) { FinishEpisode(); return; }
    }

    private void SpawnEpisode()
    {
        Vector2 robotOffset = Random.insideUnitCircle * robotSpawnRadius;
        robotRoot.position = robotSpawnCenter + new Vector3(robotOffset.x, 0f, robotOffset.y);
        robotRoot.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        robotRb.velocity = Vector3.zero; robotRb.angularVelocity = Vector3.zero;

        Vector2 ballOffset = Random.insideUnitCircle * ballSpawnRadius;
        ball.position = ballSpawnCenter + new Vector3(ballOffset.x, 0f, ballOffset.y); 
        ballRb.velocity = Vector3.zero; ballRb.angularVelocity = Vector3.zero;
    }

    private void FinishEpisode()
    {
        MatchAnalytics.RecordEpisodeResult(hasReachedBallThisEpisode, scoredGoalThisEpisode, episodeAvgSpeed);
        if (enableTelemetryLogging && telemetryWriter != null) telemetryWriter.Flush();
        EndEpisode();
    }

    private void OnDestroy()
    {
        if (telemetryWriter != null) { telemetryWriter.Close(); telemetryWriter.Dispose(); }
    }
}
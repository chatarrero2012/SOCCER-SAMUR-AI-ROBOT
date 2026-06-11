using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

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

    [Header("Rewards")]
    public float goalReward = 1000f;
    public float ownGoalPenalty = -500f;
    public float stepPenalty = -0.0001f;

    [Header("Phase Specific Rewards")]
    public float ballProximityReward = 0.5f;
    public float ballTouchReward = 5f;
    public float ballSpeedReward = 3.0f;
    public float minBallSpeedForReward = 0.5f;

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
    public float maxEpisodeSeconds = 60f;

    [Header("Personality Weights")]
    [Tooltip("X:Ofensiva | Y:Técnica | Z:Velocidad | W:Disciplina")]
    public Vector4 personality = new Vector4(1f, 1f, 1f, 1f);

    private Vector4 _p; // Versión clampada para evitar explosiones numéricas
    private float previousDistanceToBall;
    private float episodeTimer;
    private bool hasReachedBallThisEpisode;
    private bool scoredGoalThisEpisode;
    private float episodeAvgSpeed;
    private int speedSamples;

    private void Awake()
    {
        _p = new Vector4(
            Mathf.Clamp(personality.x, 0.1f, 3.0f),
            Mathf.Clamp(personality.y, 0.1f, 2.0f),
            Mathf.Clamp(personality.z, 0.1f, 2.5f),
            Mathf.Clamp(personality.w, 0.1f, 3.0f)
        );
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
        previousDistanceToBall = Vector3.Distance(transform.position, ball.position);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        AddDetectionObservation(sensor, ball);
        AddDetectionObservation(sensor, enemyGoal);
        AddDetectionObservation(sensor, ownGoal);
        AddDetectionObservation(sensor, enemy);
        
        // Contexto de personalidad: el agente sabe "cómo" está siendo evaluado
        sensor.AddObservation(_p.x);
        sensor.AddObservation(_p.y);
        sensor.AddObservation(_p.z);
        sensor.AddObservation(_p.w);
        
        sensor.AddObservation(motorDriver.currentBatteryCharge);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float leftMotor = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float rightMotor = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
        motorDriver.SetMotorInputs(leftMotor, rightMotor);

        CheckFallConditions();
        ApplyDynamicRewards();
        RecordBallMetrics();

        // La penalización por paso se escala con la Disciplina (W)
        float stepPen = stepPenalty * _p.w;
        AddReward(stepPen);
        MatchAnalytics.AddReward(stepPen);

        episodeTimer += Time.fixedDeltaTime;
        
        if (episodeTimer >= maxEpisodeSeconds)
        {
            FinishEpisode();
        }
    }

    private void ApplyDynamicRewards()
    {
        float currentDistanceToBall = Vector3.Distance(transform.position, ball.position);
        float distToBallImprovement = previousDistanceToBall - currentDistanceToBall;
        MatchAnalytics.TrainingPhase currentPhase = MatchAnalytics.GetCurrentPhase();

        float techMult = _p.y;
        float speedMult = _p.z;

        if (currentPhase == MatchAnalytics.TrainingPhase.Phase1_Fundamentos)
        {
            if (distToBallImprovement > 0f)
            {
                float reward = distToBallImprovement * ballProximityReward * techMult;
                AddReward(reward);
                MatchAnalytics.AddShapingReward(reward);
            }
            if (currentDistanceToBall < 0.4f && !hasReachedBallThisEpisode)
            {
                hasReachedBallThisEpisode = true;
                float reward = ballTouchReward * techMult;
                AddReward(reward);
                MatchAnalytics.AddShapingReward(reward);
            }
        }
        else if (currentPhase == MatchAnalytics.TrainingPhase.Phase2_Tecnica)
        {
            if (currentDistanceToBall < 0.4f && !hasReachedBallThisEpisode)
            {
                hasReachedBallThisEpisode = true;
                float reward = ballTouchReward * techMult * 0.5f;
                AddReward(reward);
                MatchAnalytics.AddShapingReward(reward);
            }
            ApplyVelocityReward(speedMult);
        }
        else if (currentPhase == MatchAnalytics.TrainingPhase.Phase3_Maestria)
        {
            if (currentDistanceToBall < 0.4f && !hasReachedBallThisEpisode)
            {
                hasReachedBallThisEpisode = true;
                float reward = ballTouchReward * techMult * 0.2f;
                AddReward(reward);
                MatchAnalytics.AddShapingReward(reward);
            }
            ApplyVelocityReward(speedMult);
        }
        else // Phase4_Estrategia
        {
            if (currentDistanceToBall < 0.4f && !hasReachedBallThisEpisode)
            {
                hasReachedBallThisEpisode = true;
                float reward = ballTouchReward * techMult * 0.1f;
                AddReward(reward);
                MatchAnalytics.AddShapingReward(reward);
            }
            ApplyVelocityReward(speedMult);
            ApplyDefensivePressure();
        }

        previousDistanceToBall = currentDistanceToBall;
    }

    private void ApplyVelocityReward(float speedMult)
    {
        float ballSpeed = ballRb.velocity.magnitude;
        if (ballSpeed > minBallSpeedForReward)
        {
            // FILTRO DE DIRECCIÓN: Solo recompensamos si va hacia la portería enemiga
            Vector3 dirToGoal = (enemyGoal.position - ball.position).normalized;
            float alignment = Vector3.Dot(ballRb.velocity.normalized, dirToGoal);

            if (alignment > 0.1f) 
            {
                float reward = (ballSpeed - minBallSpeedForReward) * ballSpeedReward * speedMult * alignment;
                AddReward(reward);
                MatchAnalytics.AddShapingReward(reward);
            }
        }
    }

    private void ApplyDefensivePressure()
    {
        float distEnemyToBall = Vector3.Distance(enemy.position, ball.position);
        float distAgentToBall = Vector3.Distance(transform.position, ball.position);

        // Recompensa por llegar primero al balón cuando el enemigo está cerca
        if (distEnemyToBall < 1.5f && distAgentToBall < distEnemyToBall)
        {
            float reward = 0.5f * _p.x;
            AddReward(reward);
            MatchAnalytics.AddShapingReward(reward);
        }
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
        // La recompensa del gol se escala puramente con la Ofensiva (X)
        float reward = goalReward * _p.x;
        AddReward(reward);
        MatchAnalytics.AddGoalReward(reward);
        Debug.Log($"⚽ GOOOLLLLLL - Fase: {MatchAnalytics.GetCurrentPhase()} | Reward: {reward:F1}");
        FinishEpisode();
    }

    public void OnOwnGoal()
    {
        scoredGoalThisEpisode = false;
        Debug.Log("❌ GOL EN PROPIA");
        // La penalización se escala con la Disciplina (W)
        AddReward(ownGoalPenalty * _p.w);
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
            AddReward(fallPenalty * _p.w);
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
        EndEpisode();
    }
}
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

    [Header("Curriculum Learning")]
    public int phase1Episodes = 200;  // Solo buscar el balón
    public int phase2Episodes = 500;  // Buscar + patear fuerte

    public enum TrainingPhase
    {
        Phase1_BallSeeking,
        Phase2_BallKicking,
        Phase3_FullTraining
    }

    public TrainingPhase CurrentPhase
    {
        get
        {
            if (MatchAnalytics.TotalEpisodes < phase1Episodes)
                return TrainingPhase.Phase1_BallSeeking;
            else if (MatchAnalytics.TotalEpisodes < phase2Episodes)
                return TrainingPhase.Phase2_BallKicking;
            else
                return TrainingPhase.Phase3_FullTraining;
        }
    }

    [Header("Rewards - SPARSE")]
    public float goalReward = 1000f;  // Aumentada drásticamente
    public float ownGoalPenalty = -500f;
    public float stepPenalty = -0.001f;

    [Header("Phase 1 - Ball Seeking")]
    public float ballProximityReward = 0.5f;
    public float ballTouchReward = 5f;

    [Header("Phase 2 - Ball Kicking")]
    public float ballSpeedReward = 2f;  // Recompensa por velocidad del balón
    public float minBallSpeedForReward = 1.0f;  // Solo premia si el balón va rápido

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

    public Vector4 personality;

    private float previousDistanceToBall;
    private float episodeTimer;
    private float episodeStartTime;
    private bool hasReachedBallThisEpisode;
    private float previousBallSpeed;

    public override void OnEpisodeBegin()
    {
        MatchAnalytics.TotalEpisodes++;
        episodeTimer = 0f;
        episodeStartTime = Time.time;
        hasReachedBallThisEpisode = false;

        motorDriver.SetMotorInputs(0f, 0f);
        SpawnEpisode();

        previousDistanceToBall = Vector3.Distance(transform.position, ball.position);
        previousBallSpeed = ballRb.velocity.magnitude;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        AddDetectionObservation(sensor, ball);
        AddDetectionObservation(sensor, enemyGoal);
        AddDetectionObservation(sensor, ownGoal);
        AddDetectionObservation(sensor, enemy);

        sensor.AddObservation(motorDriver.currentBatteryCharge);
        sensor.AddObservation(personality.x);
        sensor.AddObservation(personality.y);
        sensor.AddObservation(personality.z);
        sensor.AddObservation(personality.w);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float leftMotor = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
        float rightMotor = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);

        motorDriver.SetMotorInputs(leftMotor, rightMotor);

        CheckFallConditions();
        ApplyCurriculumRewards();
        RecordBallMetrics();

        AddReward(stepPenalty);
        MatchAnalytics.AddReward(stepPenalty);

        episodeTimer += Time.fixedDeltaTime;

        if (episodeTimer >= maxEpisodeSeconds)
        {
            EndEpisode();
        }
    }

    private void ApplyCurriculumRewards()
    {
        float currentDistanceToBall = Vector3.Distance(transform.position, ball.position);
        float distToBallImprovement = previousDistanceToBall - currentDistanceToBall;

        switch (CurrentPhase)
        {
            case TrainingPhase.Phase1_BallSeeking:
                // Solo premia por acercarse al balón
                if (distToBallImprovement > 0f)
                {
                    float reward = distToBallImprovement * ballProximityReward;
                    AddReward(reward);
                    MatchAnalytics.AddShapingReward(reward);
                }

                // Recompensa única por tocar el balón
                if (currentDistanceToBall < 0.4f && !hasReachedBallThisEpisode)
                {
                    hasReachedBallThisEpisode = true;
                    AddReward(ballTouchReward);
                    MatchAnalytics.AddShapingReward(ballTouchReward);
                    MatchAnalytics.RecordBallTouch();
                }
                break;

            case TrainingPhase.Phase2_BallKicking:
                // Shaping reducido para buscar el balón
                if (distToBallImprovement > 0f)
                {
                    float reward = distToBallImprovement * (ballProximityReward * 0.3f);
                    AddReward(reward);
                    MatchAnalytics.AddShapingReward(reward);
                }

                if (currentDistanceToBall < 0.4f && !hasReachedBallThisEpisode)
                {
                    hasReachedBallThisEpisode = true;
                    AddReward(ballTouchReward * 0.5f);
                    MatchAnalytics.AddShapingReward(ballTouchReward * 0.5f);
                    MatchAnalytics.RecordBallTouch();
                }

                // CLAVE: Recompensa por VELOCIDAD del balón
                float ballSpeed = ballRb.velocity.magnitude;
                if (ballSpeed > minBallSpeedForReward)
                {
                    float speedReward = (ballSpeed - minBallSpeedForReward) * ballSpeedReward;
                    AddReward(speedReward);
                    MatchAnalytics.AddShapingReward(speedReward);
                }
                break;

            case TrainingPhase.Phase3_FullTraining:
                // Sin shaping de proximidad, solo velocidad y goles
                if (currentDistanceToBall < 0.4f && !hasReachedBallThisEpisode)
                {
                    hasReachedBallThisEpisode = true;
                    AddReward(ballTouchReward * 0.2f);
                    MatchAnalytics.AddShapingReward(ballTouchReward * 0.2f);
                    MatchAnalytics.RecordBallTouch();
                }

                // Recompensa por velocidad del balón (más exigente)
                float ballSpeedFull = ballRb.velocity.magnitude;
                if (ballSpeedFull > minBallSpeedForReward)
                {
                    float speedReward = (ballSpeedFull - minBallSpeedForReward) * (ballSpeedReward * 0.5f);
                    AddReward(speedReward);
                    MatchAnalytics.AddShapingReward(speedReward);
                }
                break;
        }

        previousDistanceToBall = currentDistanceToBall;
    }

    private void RecordBallMetrics()
    {
        float ballSpeed = ballRb.velocity.magnitude;
        MatchAnalytics.RecordBallSpeed(ballSpeed);
        MatchAnalytics.RecordBallDistanceToGoal(Vector3.Distance(ball.position, enemyGoal.position));
        previousBallSpeed = ballSpeed;
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
        float goalTime = Time.time - episodeStartTime;
        MatchAnalytics.RecordGoalTime(goalTime);
        MatchAnalytics.TotalGoals++;

        float reward = goalReward * personality.x;
        AddReward(reward);
        MatchAnalytics.AddGoalReward(reward);

        Debug.Log($"⚽ GOOOLLLLLL en {goalTime:F1}s - Fase: {CurrentPhase}");
        EndEpisode();
    }

    public void OnOwnGoal()
    {
        Debug.Log("❌ GOL EN PROPIA");
        AddReward(ownGoalPenalty);
        MatchAnalytics.TotalOwnGoals++;
        EndEpisode();
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var actions = actionsOut.ContinuousActions;
        float left = 0f;
        float right = 0f;

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
            EndEpisode();
            return;
        }

        if (ball.position.y < fallHeight)
        {
            EndEpisode();
            return;
        }
    }

    private void SpawnEpisode()
    {
        Vector2 robotOffset = Random.insideUnitCircle * robotSpawnRadius;
        Vector3 robotPos = robotSpawnCenter + new Vector3(robotOffset.x, 0f, robotOffset.y);
        robotRoot.position = robotPos;
        robotRoot.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        robotRb.velocity = Vector3.zero;
        robotRb.angularVelocity = Vector3.zero;

        Vector2 ballOffset = Random.insideUnitCircle * ballSpawnRadius;
        Vector3 ballPos = ballSpawnCenter + new Vector3(ballOffset.x, 0f, ballOffset.y);
        ball.position = ballPos;
        ballRb.velocity = Vector3.zero;
        ballRb.angularVelocity = Vector3.zero;
    }
}
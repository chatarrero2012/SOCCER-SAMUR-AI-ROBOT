using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections.Generic;

/// <summary>
/// AGENTE DE FÚTBOL - KINETIC STRIKE (Edición Definitiva Completa)
/// Embiste el balón con inercia para maximizar la tasa de goles por encima del 10%.
/// </summary>
public class SoccerAgentCompetency : Agent, IGoalScorer
{
    [Header("Vision & Motor")]
    public SimulatedYoloDetector yolo;
    public SimulatedMotorDriver motorDriver;

    [Header("Targets")]
    public Transform ball;
    public Transform enemyGoal;
    public Transform ownGoal;
    public Transform enemy;

    [Header("⚔ PURE REWARDS")]
    public float goalReward = 2000f; 
    public float ownGoalPenalty = -500f; 

    [Header("👟 INCENTIVOS KINÉTICOS (Reward Shaping)")]
    public float ballAccelerationReward = 0.5f; 
    public float forwardGoalBreadcrumb = 0.2f;   
    public float accelerationThreshold = 0.2f;   

    [Header("🧪 ROLLING DROUGHT (Sequía Constante)")]
    public int droughtThreshold = 30; 
    public Transform penaltyPoint;
    public Transform penaltyRobotPoint;
    public float penaltyMicroNoise = 0.3f;

    [Header("Spawn & Physics")]
    public Rigidbody robotRb;
    public Rigidbody ballRb;
    public Transform robotRoot;
    public Vector3 robotSpawnCenter;
    public Vector3 ballSpawnCenter;
    public float robotSpawnRadius = 0.4f;
    public float ballSpawnRadius = 0.4f;
    public float fallHeight = -0.5f;
    public float maxEpisodeSeconds = 20f;

    [Header("Personality Weights")]
    public Vector4 personality = new Vector4(2f, 1.5f, 1f, 1f);

    private Vector4 _p;
    private float episodeTimer;
    private bool scoredGoalThisEpisode;
    private bool scoredOwnGoalThisEpisode;
    private bool diedThisEpisode;
    private bool hasTouchedBallThisEpisode;
    
    private int episodesSinceLastGoal = 0;
    private float lastBallSpeed;
    private float lastDistanceToBall;

    private List<float> lastObservationsList = new List<float>();
    public float[] GetLastObservations() => lastObservationsList.ToArray();

    private void AddObs(VectorSensor sensor, float val)
    {
        sensor.AddObservation(val);
        lastObservationsList.Add(val);
    }

    private void Awake()
    {
        NormalizePersonality();
    }

    private void NormalizePersonality()
    {
        _p = new Vector4(
            Mathf.Clamp(personality.x, 1.0f, 4.0f), 
            Mathf.Clamp(personality.y, 0.5f, 3.0f), 
            Mathf.Clamp(personality.z, 0.1f, 2.5f), 
            Mathf.Clamp(personality.w, 0.1f, 3.0f)
        );
    }

    public override void OnEpisodeBegin()
    {
        episodeTimer = 0f;
        scoredGoalThisEpisode = false;
        scoredOwnGoalThisEpisode = false;
        diedThisEpisode = false;
        hasTouchedBallThisEpisode = false;
        
        episodesSinceLastGoal++;
        
        if (motorDriver != null) motorDriver.SetMotorInputs(0f, 0f);

        personality = new Vector4(
            Random.Range(1.5f, 4.0f), 
            Random.Range(1.0f, 3.0f), 
            Random.Range(0.5f, 2.0f), 
            Random.Range(0.2f, 2.5f)  
        );

        NormalizePersonality();
        SpawnEpisode();

        if (ballRb != null) lastBallSpeed = ballRb.velocity.magnitude;
        if (ball != null && robotRoot != null) lastDistanceToBall = Vector3.Distance(robotRoot.position, ball.position);
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        lastObservationsList.Clear();

        AddDetectionObservation(sensor, ball);
        AddDetectionObservation(sensor, enemyGoal);
        AddDetectionObservation(sensor, ownGoal);
        
        if (robotRb != null)
        {
            AddObs(sensor, robotRb.velocity.x);
            AddObs(sensor, robotRb.velocity.z);
            AddObs(sensor, robotRb.angularVelocity.y);
        }
        else
        {
            AddObs(sensor, 0f); AddObs(sensor, 0f); AddObs(sensor, 0f);
        }

        if (ball != null)
        {
            Vector3 dirToBall = (ball.position - robotRoot.position).normalized;
            AddObs(sensor, dirToBall.x);
            AddObs(sensor, dirToBall.z);
            
            float lookingAtBall = Vector3.Dot(robotRoot.forward, dirToBall);
            AddObs(sensor, lookingAtBall);
        }
        else
        {
            AddObs(sensor, 0f); AddObs(sensor, 0f); AddObs(sensor, 0f);
        }
        
        AddObs(sensor, _p.x);
        AddObs(sensor, _p.y);
        AddObs(sensor, _p.z);
        AddObs(sensor, _p.w);
        AddObs(sensor, motorDriver != null ? motorDriver.currentBatteryCharge : 1.0f);
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float leftMotor = 0f;
        float rightMotor = 0f;

        if (actions.ContinuousActions.Length >= 2)
        {
            leftMotor = Mathf.Clamp(actions.ContinuousActions[0], -1f, 1f);
            rightMotor = Mathf.Clamp(actions.ContinuousActions[1], -1f, 1f);
        }
        
        if (motorDriver != null) motorDriver.SetMotorInputs(leftMotor, rightMotor);

        EvaluateBallKinetics();
        CheckFallConditions();

        episodeTimer += Time.fixedDeltaTime;
        
        if (episodeTimer >= maxEpisodeSeconds)
        {
            FinishEpisode();
        }
    }

    private void EvaluateBallKinetics()
    {
        if (ballRb == null || enemyGoal == null || Time.fixedDeltaTime == 0f || ball == null || robotRb == null) return;

        float currentDistanceToBall = Vector3.Distance(robotRoot.position, ball.position);
        float distanceDelta = lastDistanceToBall - currentDistanceToBall;
        lastDistanceToBall = currentDistanceToBall;

        if (distanceDelta > 0.001f) 
        {
            float approachReward = distanceDelta * 0.2f * _p.z; 
            AddReward(approachReward);
            MatchAnalytics.AddReward(approachReward);
        }

        float currentBallSpeed = ballRb.velocity.magnitude;
        float acceleration = (currentBallSpeed - lastBallSpeed) / Time.fixedDeltaTime;
        lastBallSpeed = currentBallSpeed;

        if (currentDistanceToBall < 0.35f && robotRb.velocity.magnitude > 0.5f && acceleration > 0.1f)
        {
            hasTouchedBallThisEpisode = true;
            float strikeReward = robotRb.velocity.magnitude * 0.5f;
            AddReward(strikeReward);
            MatchAnalytics.AddReward(strikeReward);
        }

        if (acceleration <= accelerationThreshold) return;

        float timeFactor = Mathf.Clamp(1f - (episodeTimer / maxEpisodeSeconds), 0.30f, 1f);

        float rewardValue = ballAccelerationReward * _p.y * timeFactor;
        AddReward(rewardValue);
        MatchAnalytics.AddReward(rewardValue);

        Vector3 directionToGoal = (enemyGoal.position - ball.position).normalized;
        Vector3 ballVelocityDirection = ballRb.velocity.normalized;
        float alignment = Vector3.Dot(ballVelocityDirection, directionToGoal);

        if (alignment > 0.1f) 
        {
            float breadcrumbValue = (forwardGoalBreadcrumb * alignment) * _p.x;
            AddReward(breadcrumbValue);
            MatchAnalytics.AddReward(breadcrumbValue);
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.transform == ball)
        {
            hasTouchedBallThisEpisode = true;
            CompetencyDashboard.RecordCollision();
            AddReward(1.0f * _p.y);
            MatchAnalytics.AddReward(1.0f * _p.y);
        }
    }

    public void OnGoalScored()
    {
        scoredGoalThisEpisode = true;
        float reward = goalReward * _p.x;
        AddReward(reward);
        MatchAnalytics.AddGoalReward(reward);
        MatchAnalytics.AddReward(reward);
        
        episodesSinceLastGoal = 0; 
        FinishEpisode();
    }

    public void OnOwnGoal()
    {
        scoredOwnGoalThisEpisode = true;
        AddReward(ownGoalPenalty);
        MatchAnalytics.AddReward(ownGoalPenalty);
        MatchAnalytics.TotalOwnGoals++;
        FinishEpisode();
    }

    private void FinishEpisode()
    {
        bool reachedGoal = scoredGoalThisEpisode;
        if (!diedThisEpisode && !scoredGoalThisEpisode && !scoredOwnGoalThisEpisode)
        {
            CompetencyDashboard.RecordTimeout();
            AddReward(-30f); 
            MatchAnalytics.AddReward(-30f);
        }

        CompetencyDashboard.RecordEpisodeDuration(episodeTimer);
        MatchAnalytics.RecordEpisodeResult(hasTouchedBallThisEpisode, reachedGoal, ballRb != null ? ballRb.velocity.magnitude : 0f);
        EndEpisode();
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
            AddObs(sensor, 0f); AddObs(sensor, 0f); AddObs(sensor, 0f); AddObs(sensor, 0f); AddObs(sensor, 0f);
        }
    }

    private bool TryGetDetection(Transform target, out SimulatedYoloDetector.Detection result)
    {
        if (yolo == null || yolo.detections == null)
        {
            result = default;
            return false;
        }
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
 
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var actions = actionsOut.ContinuousActions;
        float left = 0f, right = 0f;
        if (Input.GetKey(KeyCode.W)) { left = 1f; right = 1f; }
        if (Input.GetKey(KeyCode.S)) { left = -1f; right = -1f; }
        if (Input.GetKey(KeyCode.A)) { left = -1f; right = 1f; }
        if (Input.GetKey(KeyCode.D)) { left = 1f; right = -1f; }
        
        if (actions.Length >= 2)
        {
            actions[0] = left;
            actions[1] = right;
        }
    }

    private void CheckFallConditions()
    {
        if (robotRoot.position.y < fallHeight)
        {
            diedThisEpisode = true;
            CompetencyDashboard.RecordFall();
            AddReward(-30f); 
            MatchAnalytics.AddReward(-30f);
            FinishEpisode();
            return;
        }
        if (ball != null && ball.position.y < fallHeight)
        {
            diedThisEpisode = true;
            FinishEpisode();
            return;
        }
    }

    private void SpawnEpisode()
    {
        if (episodesSinceLastGoal >= droughtThreshold && penaltyPoint != null)
        {
            Vector3 noise = new Vector3(
                Random.Range(-penaltyMicroNoise, penaltyMicroNoise), 
                0f, 
                Random.Range(-penaltyMicroNoise, penaltyMicroNoise)
            );
            ball.position = penaltyPoint.position + noise;
            robotRoot.rotation = Quaternion.Euler(0f, 90f, 0f);
            robotRoot.position = penaltyRobotPoint.position;
        }
        else
        {
            Vector2 ballOffset = Random.insideUnitCircle * ballSpawnRadius;
            ball.position = ballSpawnCenter + new Vector3(ballOffset.x, 0f, ballOffset.y);
            Vector2 robotOffset = Random.insideUnitCircle * robotSpawnRadius;
            robotRoot.position = robotSpawnCenter + new Vector3(robotOffset.x, 0f, robotOffset.y);
            robotRoot.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        }

        if (ballRb != null) { ballRb.velocity = Vector3.zero; ballRb.angularVelocity = Vector3.zero; }
        if (robotRb != null) { robotRb.velocity = Vector3.zero; robotRb.angularVelocity = Vector3.zero; }
    }
}
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections.Generic;

/// <summary>
/// AGENTE DE FÚTBOL - CURRICULUM GEOMETRIC ALIGNMENT (Edición Cátedra Progresiva V5)
/// Diseñado para evitar el Sim2Real mediante bootstrapping posicional adaptativo.
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
    public Transform enemy; // Excluido de las observaciones para evitar contaminación.

    [Header("🎓 CÁTEDRA PROGRESIVA (Curriculum Points)")]
    public int currentLevel = 0; 
    public Transform cornerLeftPoint;
    public Transform cornerRightPoint;
    public Transform lateralPoint;
    public Transform penaltyPoint; // COPATIBLE CON EL LEVEL 2
    public int droughtThreshold = 10;

    [Header("⚔ PURE REWARDS")]
    public float goalReward = 3500f; 
    public float ownGoalPenalty = -700f; 
    public float stepPenalty = -0.01f; // Penalización existencial suave para buscar eficiencia temporal

    [Header("👟 INCENTIVOS VECTORIALES (Rozamiento Real)")]
    public float ballAccelerationReward = 0.8f; 
    public float forwardGoalBreadcrumb = 1.0f;   
    public float accelerationThreshold = 0.12f;   

    [Header("Spawn & Physics")]
    public Rigidbody robotRb;
    public Rigidbody ballRb;
    public Transform robotRoot;
    public Vector3 robotSpawnCenter;
    public Vector3 ballSpawnCenter;
    public float robotSpawnRadius = 0.4f;
    public float ballSpawnRadius = 0.4f;
    public float fallHeight = -0.5f;
    public float maxEpisodeSeconds = 15f; 

    [Header("Personality Weights")]
    public Vector4 personality = new Vector4(2.5f, 2.0f, 1.5f, 1.0f);

    private Vector4 _p;
    private float episodeTimer;
    private bool scoredGoalThisEpisode;
    private bool scoredOwnGoalThisEpisode;
    private bool diedThisEpisode;
    private bool hasTouchedBallThisEpisode;
    
    private int episodesSinceLastGoal = 0;
    private float lastBallSpeed;
    private float lastDistanceToBall;
    private float lastDistanceBallToGoal;

    [Header("Curriculum Learning")]
    public int currentLesson = 0;
    public int consecutiveGoals = 0;

    // CALIBRACIÓN ESTRICTA (Basada en tu cancha de 2.73m x 1.27m)
    // El límite físico máximo en X es -2.4f (cerca a tu propio arco)
    private float[] maxDepths =   { -0.35f, -0.60f, -1.00f, -1.50f, -2.00f, -2.40f };

    // El límite físico máximo en Z es 0.63f. Dejamos un margen de seguridad de 0.13m para no pegar a la pared.
    private float[] maxLaterals = {  0.00f,  0.15f,  0.25f,  0.35f,  0.45f,  0.50f };

    private List<float> lastObservationsList = new List<float>();
    public float[] GetLastObservations() => lastObservationsList.ToArray();

    private void AddObs(VectorSensor sensor, float val)
    {
        sensor.AddObservation(val);
        lastObservationsList.Add(val);
    }

    public override void Initialize()
    {
        NormalizePersonality();
    }

    private void NormalizePersonality()
    {
        _p = new Vector4(
            Mathf.Clamp(personality.x, 1.0f, 5.0f), 
            Mathf.Clamp(personality.y, 1.0f, 4.0f), 
            Mathf.Clamp(personality.z, 0.5f, 3.0f), 
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

        // Ajustar nivel del currículo basado en analíticas de ventana móvil
        EvaluateCurriculumProgress();

        personality = new Vector4(
            Random.Range(2.0f, 5.0f), 
            Random.Range(1.5f, 4.0f), 
            Random.Range(1.0f, 3.0f), 
            Random.Range(0.2f, 2.5f)  
        );
        
        NormalizePersonality();
        SpawnCurriculumEpisode();

        if (ballRb != null) lastBallSpeed = ballRb.velocity.magnitude;
        if (ball != null && robotRoot != null) lastDistanceToBall = Vector3.Distance(robotRoot.position, ball.position);
        if (ball != null && enemyGoal != null) lastDistanceBallToGoal = Vector3.Distance(ball.position, enemyGoal.position);
    }

    private void EvaluateCurriculumProgress()
    {
        if (episodesSinceLastGoal >= droughtThreshold)
        {
            currentLevel = 0;
            return;
        }

        float currentGoalRate = MatchAnalytics.GetRecentGoalRate();
        int totalEp = MatchAnalytics.TotalEpisodes;

        if (totalEp >= 20)
        {
            if (currentGoalRate > 0.85f && currentLevel < 4)
            {
                currentLevel++;
                episodesSinceLastGoal = 0; 
            }
            else if (currentGoalRate < 0.30f && currentLevel > 0)
            {
                currentLevel--; 
            }
        }
    }

    private void SpawnCurriculumEpisode()
    {
        // 1. DESACTIVACIÓN CINEMÁTICA MOMENTÁNEA
        if (ballRb != null) 
        { 
            ballRb.isKinematic = true;
            ballRb.velocity = Vector3.zero; 
            ballRb.angularVelocity = Vector3.zero; 
        }
        if (robotRb != null) 
        { 
            robotRb.isKinematic = true;
            robotRb.velocity = Vector3.zero; 
            robotRb.angularVelocity = Vector3.zero; 
        }

        Vector3 targetBallPos = ballSpawnCenter;
        float robotOffsetDistance = 0.35f; 

        // MICRO-LEVITACIÓN DE SEGURIDAD
        float safeHeightBall = ballSpawnCenter.y + 0.08f;
        float safeHeightRobot = robotSpawnCenter.y + 0.08f;

        switch (currentLevel)
        {
            case 0:
            {
                currentLesson = Mathf.Clamp(currentLesson, 0, maxDepths.Length - 1);

                float minDepth = -0.35f; 
                float maxDepth = maxDepths[currentLesson];
                float lateralRange = maxLaterals[currentLesson];

                float randomDepth = Random.Range(minDepth, maxDepth);   
                float randomLateral = Random.Range(-lateralRange, lateralRange);  

                Vector3 localBallOffset = (enemyGoal.right * randomDepth) + (Vector3.forward * randomLateral);
                targetBallPos = enemyGoal.position + localBallOffset;
                targetBallPos.y = safeHeightBall; 
                ball.position = targetBallPos;
                
                AlignRobotBehindBall(targetBallPos, robotOffsetDistance);
                
                Vector3 rPos0 = robotRoot.position; 
                rPos0.y = safeHeightRobot; 
                robotRoot.position = rPos0;
            }
            break;

            case 1: 
                targetBallPos = Vector3.Lerp(ballSpawnCenter, enemyGoal.position, 0.4f);
                targetBallPos.y = safeHeightBall;
                ball.position = targetBallPos;
                
                AlignRobotBehindBall(targetBallPos, robotOffsetDistance);
                Vector3 rPos1 = robotRoot.position; rPos1.y = safeHeightRobot; robotRoot.position = rPos1;
                break;

            case 2: 
                if (penaltyPoint != null) targetBallPos = penaltyPoint.position;
                else targetBallPos = Vector3.Lerp(ballSpawnCenter, enemyGoal.position, 0.6f);
                
                targetBallPos += new Vector3(Random.Range(-0.1f, 0.1f), 0f, Random.Range(-0.1f, 0.1f));
                targetBallPos.y = safeHeightBall;
                ball.position = targetBallPos;
                
                AlignRobotBehindBall(ball.position, robotOffsetDistance);
                Vector3 rPos2 = robotRoot.position; rPos2.y = safeHeightRobot; robotRoot.position = rPos2;
                break;

            case 3: 
                Transform selectedTransform = lateralPoint;
                int tacticalIndex = Random.Range(0, 3);
                if (tacticalIndex == 0 && cornerLeftPoint != null) selectedTransform = cornerLeftPoint;
                if (tacticalIndex == 1 && cornerRightPoint != null) selectedTransform = cornerRightPoint;

                if (selectedTransform != null)
                {
                    CornerPoint scriptMover = selectedTransform.GetComponent<CornerPoint>();
                    if (scriptMover != null)
                    {
                        scriptMover.AleatorizarEjeX(); 
                    }
                    targetBallPos = selectedTransform.position;
                }
                
                targetBallPos.y = safeHeightBall;
                ball.position = targetBallPos;
                
                AlignRobotBehindBall(ball.position, robotOffsetDistance);
                Vector3 rPos3 = robotRoot.position; rPos3.y = safeHeightRobot; robotRoot.position = rPos3;
                break;

            case 4: 
                Vector2 ballOffset = Random.insideUnitCircle * ballSpawnRadius;
                Vector3 bPos = ballSpawnCenter + new Vector3(ballOffset.x, 0f, ballOffset.y);
                bPos.y = safeHeightBall;
                ball.position = bPos;

                Vector2 robotOffset = Random.insideUnitCircle * robotSpawnRadius;
                Vector3 rPos4 = robotSpawnCenter + new Vector3(robotOffset.x, 0f, robotOffset.y);
                rPos4.y = safeHeightRobot;
                robotRoot.position = rPos4;
                robotRoot.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                break;
        }

        // RECONEXIÓN DINÁMICA DE FÍSICAS
        if (ballRb != null) ballRb.isKinematic = false;
        if (robotRb != null) robotRb.isKinematic = false;
    }

    private void AlignRobotBehindBall(Vector3 ballPos, float distanceBehind)
    {
        Vector3 forwardToGoal = enemyGoal.right;
        forwardToGoal.y = 0f; 
        forwardToGoal.Normalize();

        robotRoot.position = ballPos - (forwardToGoal * distanceBehind);
        robotRoot.rotation = Quaternion.LookRotation(forwardToGoal);
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

        AddReward(stepPenalty);
        MatchAnalytics.AddReward(stepPenalty);

        EvaluateBallKinetics();
        CheckFallConditions();

        episodeTimer += Time.fixedDeltaTime;
        if (episodeTimer >= maxEpisodeSeconds) FinishEpisode();
    }

    private void EvaluateBallKinetics()
    {
        if (ballRb == null || enemyGoal == null || Time.fixedDeltaTime == 0f || ball == null || robotRb == null) return;

        float currentDistanceToBall = Vector3.Distance(robotRoot.position, ball.position);
        float distanceDelta = lastDistanceToBall - currentDistanceToBall;
        lastDistanceToBall = currentDistanceToBall;

        if (distanceDelta > 0.001f) 
        {
            float approachReward = distanceDelta * 0.4f * _p.z; 
            AddReward(approachReward);
            MatchAnalytics.AddReward(approachReward);
        }

        float currentDistanceBallToGoal = Vector3.Distance(ball.position, enemyGoal.position);
        float ballToGoalDelta = lastDistanceBallToGoal - currentDistanceBallToGoal;
        lastDistanceBallToGoal = currentDistanceBallToGoal;

        if (ballToGoalDelta > 0.001f && hasTouchedBallThisEpisode)
        {
            float forwardPushReward = ballToGoalDelta * 2.5f * _p.x;
            AddReward(forwardPushReward);
            MatchAnalytics.AddReward(forwardPushReward);
        }

        float currentBallSpeed = ballRb.velocity.magnitude;
        float acceleration = (currentBallSpeed - lastBallSpeed) / Time.fixedDeltaTime;
        lastBallSpeed = currentBallSpeed;

        if (currentDistanceToBall < 0.35f && robotRb.velocity.magnitude > 0.4f && acceleration > 0.1f)
        {
            hasTouchedBallThisEpisode = true;
            float strikeReward = robotRb.velocity.magnitude * 1.0f * _p.y;
            AddReward(strikeReward);
            MatchAnalytics.AddReward(strikeReward);
        }

        if (acceleration <= accelerationThreshold) return;

        float timeFactor = Mathf.Clamp(1f - (episodeTimer / maxEpisodeSeconds), 0.40f, 1f);
        float rewardValue = ballAccelerationReward * _p.y * timeFactor;
        AddReward(rewardValue);
        MatchAnalytics.AddReward(rewardValue);

        Vector3 directionToGoal = (enemyGoal.position - ball.position).normalized;
        float alignmentComponent = Vector3.Dot(ballRb.velocity, directionToGoal);

        if (alignmentComponent > 0.05f) 
        {
            float breadcrumbValue = (forwardGoalBreadcrumb * alignmentComponent) * _p.x * Time.fixedDeltaTime;
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
            AddReward(2.0f * _p.y);
            MatchAnalytics.AddReward(2.0f * _p.y);
        }
    }

    public void OnGoalScored()
    {
        scoredGoalThisEpisode = true;
        float reward = goalReward * _p.x;
        AddReward(reward);
        MatchAnalytics.AddGoalReward(reward);
        MatchAnalytics.AddReward(reward);

        consecutiveGoals++;
        if (consecutiveGoals >= 3) 
        {
            currentLesson++;
            consecutiveGoals = 0;
            Debug.Log($"<color=green>¡Graduado! Subiendo a Lección {currentLesson}</color>");
        }
        
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

        // CONTROL DE CURRICULUM CENTRALIZADO AL FINAL DEL INTENTO
        if (!reachedGoal)
        {
            if (consecutiveGoals > 0) 
            {
                consecutiveGoals = 0; // Rompe la racha si el intento culmina sin éxito
                Debug.Log("<color=yellow>Racha de goles reiniciada por fallo o tiempo límite.</color>");
            }
            else
            {
                // Si la racha ya era cero y sigue fallando, reduce un escalón de lección para recuperar confianza
                currentLesson = Mathf.Max(0, currentLesson - 1);
                Debug.Log($"<color=red>Dificultad reducida a Lección {currentLesson}</color>");
            }
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
        
        if (actions.Length >= 2) { actions[0] = left; actions[1] = right; }
    }

    private void CheckFallConditions()
    {
        if (robotRoot.position.y < fallHeight)
        {
            diedThisEpisode = true;
            CompetencyDashboard.RecordFall();
            AddReward(-30f); MatchAnalytics.AddReward(-30f);
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
}
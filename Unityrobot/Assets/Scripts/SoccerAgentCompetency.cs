using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using System.Collections.Generic;

/// <summary>
/// AGENTE DE FÚTBOL - CURRICULUM GEOMETRIC ALIGNMENT (Edición Cátedra Progresiva V5 con Matriz Dinámica)
/// Diseñado para evitar el Sim2Real mediante bootstrapping posicional adaptativo y regulaciónVAR exógena.
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
    public Transform penaltyPoint; // COMPATIBLE CON EL LEVEL 2
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

    [Header("Personality Weights (Static Seed base)")]
    public Vector4 personality = new Vector4(2.5f, 2.0f, 1.5f, 1.0f);

    [Header("⚖️ REGULATORY COMPLIANCE SENSES (Mock Referee Input)")]
    [Range(0f, 1f)] public float juegoActivo = 1.0f;       // 0.0 Pausa / 1.0 Activo
    [Range(0f, 1.0f)] public float tarjetasAmarillas = 0.0f; // 0.0 Limpio / 0.5 Amonestado / 1.0 Expulsado
    [Range(0f, 1f)] public float faltasAcumuladas = 0.0f;   // Ratio normalizado basado en MaxFouls

    [Header("🛡️ ANTI-EXPLOIT CALIBRATION")]
    public float suicidePenalty = -200f; // Bloqueo de loop autodestructivo
    public float timeoutPenalty = -150f; // Bloqueo de bucle de estancamiento defensivo

    private Vector4 _p; // Vector de escalado dinámico interno
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
    private float[] maxDepths =   { -0.35f, -0.60f, -1.00f, -1.50f, -2.00f, -2.40f };
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
        UpdatePersonalityWeights();
    }

    private void UpdatePersonalityWeights()
    {
        float cronometroEpisodio = Mathf.Clamp01(episodeTimer / maxEpisodeSeconds);

        // ESTADO 3: LAW COMPLIANCE / STATUE MODE
        if (juegoActivo < 0.5f)
        {
            _p.x = 0.0f; // Zero Attack Drive
            _p.y = 0.0f; // Zero Kinetic Impulse
            _p.z = 1.0f; // Mantenimiento nominal de alineación posicional estática
            _p.w = 5.0f; // Máxima prudencia de bloqueo reactivo
            return;
        }

        // ESTADO 2: DEFENSIVE SHADOW / ESGRIMISTA
        // Activado si el árbitro reporta amonestaciones, altas faltas, o si se va ganando al cierre del tiempo límite
        if (tarjetasAmarillas > 0.1f || faltasAcumuladas > 0.7f || (cronometroEpisodio > 0.8f && MatchAnalytics.GetRecentGoalRate() > 0.6f))
        {
            _p.x = 0.3f; // Mitigación radical de riesgos ofensivos descontrolados
            _p.y = 1.5f; // Impulso cinético controlado
            _p.z = 3.0f; // Maximizar anclajes estructurales y breadcrumbs de posicionamiento geométrico
            _p.w = 4.5f; // Escalado severo de evasión de faltas (Prudence)
        }
        // ESTADO 1: HUNTER / ATTACKER
        // Historial limpio, juego fluido y alta disponibilidad táctica
        else
        {
            _p.x = 4.5f; // Máxima agresión ofensiva
            _p.y = 4.0f; // Máxima aceleración y golpeo del esférico
            _p.z = 1.2f; // Flexibilidad geométrica holgada para fintas creativas
            _p.w = 0.5f; // Minimización preventiva del peso de penalizaciones por roce limpio
        }

        // Blindaje de estabilidad matemática para evitar explosión/desvanecimiento de gradientes en PPO
        _p.x = Mathf.Clamp(_p.x, 0.0f, 5.0f);
        _p.y = Mathf.Clamp(_p.y, 0.0f, 4.0f);
        _p.z = Mathf.Clamp(_p.z, 0.5f, 3.0f);
        _p.w = Mathf.Clamp(_p.w, 0.1f, 5.0f);
    }

    public override void OnEpisodeBegin()
    {
        episodeTimer = 0f;
        scoredGoalThisEpisode = false;
        scoredOwnGoalThisEpisode = false;
        diedThisEpisode = false;
        hasTouchedBallThisEpisode = false;
        
        episodesSinceLastGoal++;
        
        if (motorDriver != null) motorDriver.Stop();

        EvaluateCurriculumProgress();

        personality = new Vector4(
            Random.Range(2.0f, 5.0f), 
            Random.Range(1.5f, 4.0f), 
            Random.Range(1.0f, 3.0f), 
            Random.Range(0.2f, 2.5f)  
        );
        
        UpdatePersonalityWeights();
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

        // ⚖️ INYECCIÓN DE SENTIDOS REGULATORIOS VECTORIALES (4 Normalized Floats - Critical for PPO Fusion)
        AddObs(sensor, juegoActivo);
        AddObs(sensor, tarjetasAmarillas);
        AddObs(sensor, faltasAcumuladas);
        AddObs(sensor, Mathf.Clamp01(episodeTimer / maxEpisodeSeconds)); // cronometroEpisodio
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // 🛑 ESTADO 3: LAW COMPLIANCE / STATUE MODE OVERRIDE IMMEDIATE
        if (juegoActivo < 0.5f)
        {
            if (motorDriver != null) motorDriver.Stop(); // Forzar frenado de actuadores directos

            // Escalado exponencial si existe inercia residual o deslizamiento no permitido durante pausa
            if (robotRb != null && robotRb.velocity.magnitude > 0.05f)
            {
                float illegalMovementPenalty = -5.0f; // Escalado drástico por frame
                AddReward(illegalMovementPenalty);
                MatchAnalytics.AddReward(illegalMovementPenalty);
            }

            CheckFallConditions();
            episodeTimer += Time.fixedDeltaTime;
            if (episodeTimer >= maxEpisodeSeconds) FinishEpisode();
            return;
        }

        // ⚡ ENVOLUCIÓN OPERATIVA EN TIEMPO REAL (Hunter o Esgrimista)
        UpdatePersonalityWeights();

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
            return;
        }

        // 🛡️ REFUERZO DE HARDWARE (Proteger smartphone de impactos directos)
        if (collision.transform == enemy || collision.transform == enemyGoal || collision.transform == ownGoal)
        {
            float relativeVel = collision.relativeVelocity.magnitude;
            
            // Si es el NPC enemigo, el castigo es severo desde el contacto cero
            float basePenalty = (collision.transform == enemy) ? -8.0f : -1.5f;
            
            // Escalamos con la velocidad, pero manteniendo un piso firme independiente de _p.w
            // Multiplicamos por (1.0f + _p.w) para que la prudencia aumente el castigo, pero nunca baje de la base
            float kineticImpactPenalty = basePenalty * (1.0f + relativeVel) * (1.0f + _p.w);

            // Si el agente está amonestado, se triplica para forzar juego ultra-limpio
            if (tarjetasAmarillas > 0.4f)
            {
                kineticImpactPenalty *= 3.0f;
            }

            AddReward(kineticImpactPenalty);
            MatchAnalytics.AddReward(kineticImpactPenalty);
        }
    }

    // 🔥 BLINDAJE CONTRA EMPUJES Y FRICCIÓN CONTINUA (Anti-Grinding)
    private void OnCollisionStay(Collision collision)
    {
        if (collision.transform == enemy)
        {
            // El contacto continuo sobrecalienta motores, drena batería y mete ruido severo a los sensores
            // Penalización por frame activo de contacto
            float continuousContactPenalty = -0.5f * (1.0f + _p.w);
            
            AddReward(continuousContactPenalty);
            MatchAnalytics.AddReward(continuousContactPenalty);
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

        // Regulación Drástica de Tiempos Límite (Anti-Exploit Stall)
        if (!diedThisEpisode && !scoredGoalThisEpisode && !scoredOwnGoalThisEpisode)
        {
            CompetencyDashboard.RecordTimeout();
            AddReward(timeoutPenalty); 
            MatchAnalytics.AddReward(timeoutPenalty);
        }

        // CONTROL DE CURRICULUM CENTRALIZADO AL FINAL DEL INTENTO
        if (!reachedGoal)
        {
            if (consecutiveGoals > 0) 
            {
                consecutiveGoals = 0; 
                Debug.Log("<color=yellow>Racha de goles reiniciada por fallo o tiempo límite.</color>");
            }
            else
            {
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
            
            // Sobreescritura drástica contra el exploit de suicidio forzado
            AddReward(suicidePenalty); 
            MatchAnalytics.AddReward(suicidePenalty);
            
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
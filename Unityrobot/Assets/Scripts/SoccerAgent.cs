using UnityEngine;

using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class SoccerAgent : Agent
{
    // =====================================================
    // REFERENCES
    // =====================================================

    [Header("Vision")]

    public SimulatedYoloDetector yolo;

    [Header("Motor Driver")]

    public SimulatedMotorDriver motorDriver;

    [Header("Targets")]

    public Transform ball;

    public Transform enemyGoal;

    public Transform ownGoal;

    public Transform enemy;

    // =====================================================
    // REWARDS
    // =====================================================

    [Header("Rewards")]

    public float goalReward = 100f;

    public float ownGoalPenalty = -100f;

    public float ballVisibleReward = 0.0002f;

    public float ballAndGoalReward = 0.0005f;

    public float stepPenalty = -0.00005f;

    [Header("Progress Rewards")]

    public float ballToGoalProgressReward = 1f;

    // =====================================================
    // EPISODE
    // =====================================================

    [Header("Episode")]

    public float maxEpisodeSeconds = 60f;

    private float episodeTimer;

    [Header("Fall Detection")]

    public Transform robotRoot;

    public float fallHeight = -0.5f;

    public float fallPenalty = -20f;

    [Header("Spawn")]

    public Rigidbody robotRb;

    public Rigidbody ballRb;

    public Vector3 robotSpawnCenter;

    public Vector3 ballSpawnCenter;

    public float robotSpawnRadius = 0.4f;

    public float ballSpawnRadius = 0.4f;

    private float previousBallGoalDistance;

    // x = deseo de gol, y = agresividad, z = precisión, w = ahorro de energía


    public Vector4 personality;

    // =====================================================
    // EPISODE START
    // =====================================================

    public override void OnEpisodeBegin()
    {
        MatchAnalytics.TotalEpisodes++;

        episodeTimer = 0f;

        motorDriver.SetMotorInputs(0f, 0f);

        SpawnEpisode();

        previousBallGoalDistance =
        Vector3.Distance(
            ball.position,
            enemyGoal.position
        );
    }

    // =====================================================
    // OBSERVATIONS
    // =====================================================

    public override void CollectObservations(
        VectorSensor sensor)
    {
        
        AddDetectionObservation(
            sensor,
            ball
        );

        AddDetectionObservation(
            sensor,
            enemyGoal
        );

        AddDetectionObservation(
            sensor,
            ownGoal
        );
        AddDetectionObservation(
            sensor,
            enemy
        );

        sensor.AddObservation(
            motorDriver.currentBatteryCharge
        );

        sensor.AddObservation(
            personality.x
            );
        sensor.AddObservation(
            personality.y
            );
        sensor.AddObservation(
            personality.z
            );
        sensor.AddObservation(
            personality.w)
        ;
    }

    // =====================================================
    // ACTIONS
    // =====================================================
    /*
public override void OnActionReceived(
    ActionBuffers actions)
{
    motorDriver.SetMotorInputs(
        1f,
        1f
    );
}

*/
    public override void OnActionReceived(
        ActionBuffers actions)
    {

        // Debug.Log(
    // $"Raw Actions: L={actions.ContinuousActions[0]:F3} " +
    // $"R={actions.ContinuousActions[1]:F3}"
// );

        float leftMotor =
            Mathf.Clamp(
                actions.ContinuousActions[0],
                -1f,
                1f
            );

        float rightMotor =
            Mathf.Clamp(
                actions.ContinuousActions[1],
                -1f,
                1f
            );

        motorDriver.SetMotorInputs(
            leftMotor,
            rightMotor
        );
        CheckFallConditions();
        //RewardVision();
        RewardBallProgress();

        AddReward(stepPenalty);

        episodeTimer += Time.fixedDeltaTime;

        if (episodeTimer >= maxEpisodeSeconds)
        {
            EndEpisode();
        }
    }

    // =====================================================
    // DETECTION OBSERVATION
    // =====================================================

    private void AddDetectionObservation(
        VectorSensor sensor,
        Transform target
    )
    {
        SimulatedYoloDetector.Detection d;

        if (TryGetDetection(
            target,
            out d))
        {
            sensor.AddObservation(1f);

            sensor.AddObservation(
                d.normalizedCenter.x);

            sensor.AddObservation(
                d.normalizedCenter.y);

            sensor.AddObservation(
                d.normalizedSize.x);

            sensor.AddObservation(
                d.normalizedSize.y);
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

    // =====================================================
    // DETECTION SEARCH
    // =====================================================

    private bool TryGetDetection(
        Transform target,
        out SimulatedYoloDetector.Detection result
    )
    {
        foreach (var d in yolo.detections)
        {
            if (d.target == target)
            {
                result = d;
                return true;
            }
        }

        result = null;

        return false;
    }

    // =====================================================
    // REWARDS
    // =====================================================

    private void RewardBallProgress()
    {
        float currentDistance =
            Vector3.Distance(
                ball.position,
                enemyGoal.position
            );

        float distanceImprovement =
            previousBallGoalDistance -
            currentDistance;

        if (distanceImprovement > 0f)
        {
            AddReward(
                (distanceImprovement *
                ballToGoalProgressReward) * personality.y
            );
            MatchAnalytics.RewardFromShaping +=
        (distanceImprovement *
                ballToGoalProgressReward) * personality.y;
        }

        previousBallGoalDistance =
            currentDistance;
    }

    private void RewardVision()
    {
        bool seesBall =
            TryGetDetection(
                ball,
                out _
            );

        bool seesEnemyGoal =
            TryGetDetection(
                enemyGoal,
                out _
            );

        if (seesBall)
        {
            AddReward(
                ballVisibleReward * personality.z
            );
        }

        if (
            seesBall &&
            seesEnemyGoal
        )
        {
            AddReward(
                ballAndGoalReward
            );
        }
    }

    // =====================================================
    // GOALS
    // =====================================================

    public void OnGoalScored()
    {
        AddReward(
            goalReward * personality.x
        );
        MatchAnalytics.RewardFromGoals +=
        goalReward * personality.x;
        MatchAnalytics.TotalGoals ++;
        Debug.Log("GOOOLLLLLL");

        EndEpisode();
    }

    public void OnOwnGoal()
    {
        Debug.Log("noooooo");
        AddReward(
            ownGoalPenalty
        );
        MatchAnalytics.TotalOwnGoals ++;

        EndEpisode();
    }

    // =====================================================
    // HEURISTIC
    // =====================================================

    public override void Heuristic(
        in ActionBuffers actionsOut)
    {
        var actions =
            actionsOut.ContinuousActions;

        float left = 0f;
        float right = 0f;

        if (Input.GetKey(KeyCode.W))
        {
            left = 1f;
            right = 1f;
        }

        if (Input.GetKey(KeyCode.S))
        {
            left = -1f;
            right = -1f;
        }

        if (Input.GetKey(KeyCode.A))
        {
            left = -1f;
            right = 1f;
        }

        if (Input.GetKey(KeyCode.D))
        {
            left = 1f;
            right = -1f;
        }

        actions[0] = left;
        actions[1] = right;
    }

    private void CheckFallConditions()
{
    // Robot fell

    if (robotRoot.position.y < fallHeight)
    {
        AddReward(fallPenalty);

        Debug.Log("Robot fell off table");

        EndEpisode();

        return;
    }

    // Ball fell

    if (ball.position.y < fallHeight)
    {
        Debug.Log("Ball fell off table");

        EndEpisode();

        return;
    }
}
private void SpawnEpisode()
{
        // -------------------------
        // Robot
        // -------------------------

        Vector2 robotOffset =
        Random.insideUnitCircle *
        robotSpawnRadius;

        Vector3 robotPos =
        robotSpawnCenter +
        new Vector3(
            robotOffset.x,
            0f,
            robotOffset.y);

        robotRoot.position = robotPos;

        robotRoot.rotation =
        Quaternion.Euler(
            0f,
            Random.Range(0f, 360f),
            0f);

        robotRb.velocity = Vector3.zero;
        robotRb.angularVelocity = Vector3.zero;

        // -------------------------
        // Ball
        // -------------------------

        Vector2 ballOffset =
        Random.insideUnitCircle *
        ballSpawnRadius;

        Vector3 ballPos =
        ballSpawnCenter +
        new Vector3(
            ballOffset.x,
            0f,
            ballOffset.y);

        ball.position = ballPos;

        ballRb.velocity = Vector3.zero;
        ballRb.angularVelocity = Vector3.zero;
    }
}
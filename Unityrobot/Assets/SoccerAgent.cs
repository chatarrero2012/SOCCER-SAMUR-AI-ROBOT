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

    // =====================================================
    // REWARDS
    // =====================================================

    [Header("Rewards")]

    public float goalReward = 100f;

    public float ownGoalPenalty = -100f;

    public float ballVisibleReward = 0.0002f;

    public float ballAndGoalReward = 0.0005f;

    public float stepPenalty = -0.00005f;

    // =====================================================
    // EPISODE
    // =====================================================

    [Header("Episode")]

    public float maxEpisodeSeconds = 60f;

    private float episodeTimer;

    // =====================================================
    // EPISODE START
    // =====================================================

    public override void OnEpisodeBegin()
    {
        episodeTimer = 0f;

        motorDriver.SetMotorInputs(0f, 0f);
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

        sensor.AddObservation(
            motorDriver.currentBatteryCharge
        );
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

        Debug.Log(
    $"Raw Actions: L={actions.ContinuousActions[0]:F3} " +
    $"R={actions.ContinuousActions[1]:F3}"
);

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
        Debug.Log(
    $"SETTING MOTORS L={leftMotor} R={rightMotor}"
);
        RewardVision();

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
                ballVisibleReward
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
            goalReward
        );

        EndEpisode();
    }

    public void OnOwnGoal()
    {
        AddReward(
            ownGoalPenalty
        );

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
}
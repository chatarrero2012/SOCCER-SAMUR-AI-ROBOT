using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PunkDummyBrain : MonoBehaviour
{
    // =====================================================
    // ENUM
    // =====================================================

    public enum DummyMode
    {
        Aggressive,
        Goalkeeper
    }

    // =====================================================
    // REFERENCES
    // =====================================================

    [Header("References")]

    public Transform ball;

    public Transform enemyGoal;

    public Transform ownGoal;

    public SimulatedMotorDriver driver;

    public Rigidbody rb;

    // =====================================================
    // MODE
    // =====================================================

    [Header("Mode")]

    public DummyMode mode;

    // =====================================================
    // MOVEMENT
    // =====================================================

    [Header("Movement")]

    public float forwardSpeed = 1f;

    public float turnSpeed = 2f;

    public float ballPushDistance = 1.2f;

    public float goalkeeperRange = 3f;

    // =====================================================
    // UNITY
    // =====================================================

    private void FixedUpdate()
    {
        switch (mode)
        {
            case DummyMode.Aggressive:
                UpdateAggressive();
                break;

            case DummyMode.Goalkeeper:
                UpdateGoalkeeper();
                break;
        }
    }

    // =====================================================
    // AGGRESSIVE
    // =====================================================

    private void UpdateAggressive()
    {
        float distToBall =
            Vector3.Distance(
                transform.position,
                ball.position
            );

        Vector3 target;

        // -------------------------------------------------
        // GO TO BALL
        // -------------------------------------------------

        if (distToBall > ballPushDistance)
        {
            target = ball.position;
        }

        // -------------------------------------------------
        // PUSH BALL TO GOAL
        // -------------------------------------------------

        else
        {
            Vector3 pushDir =
                (enemyGoal.position - ball.position).normalized;

            target =
                ball.position - pushDir * 0.5f;
        }

        MoveToward(target);
    }

    // =====================================================
    // GOALKEEPER
    // =====================================================

    private void UpdateGoalkeeper()
    {
        Vector3 goalToBall =
            (ball.position - ownGoal.position);

        goalToBall.y = 0;

        // Clamp goalkeeper distance
        if (goalToBall.magnitude > goalkeeperRange)
        {
            goalToBall =
                goalToBall.normalized *
                goalkeeperRange;
        }

        Vector3 defendPoint =
            ownGoal.position + goalToBall;

        MoveToward(defendPoint);
    }

    // =====================================================
    // MOVEMENT
    // =====================================================

    private void MoveToward(Vector3 target)
    {
        Vector3 dir =
            target - transform.position;

        dir.y = 0;

        if (dir.magnitude < 0.05f)
        {
            driver.SetMotorInputs(0, 0);
            return;
        }

        dir.Normalize();

        float angle =
            Vector3.SignedAngle(
                transform.forward,
                dir,
                Vector3.up
            );

        float turn =
            Mathf.Clamp(
                angle / 45f,
                -1f,
                1f
            );

        float forward =
            1f - Mathf.Abs(turn);

        float left =
            forward * forwardSpeed -
            turn * turnSpeed;

        float right =
            forward * forwardSpeed +
            turn * turnSpeed;

        left = Mathf.Clamp(left, -1f, 1f);
        right = Mathf.Clamp(right, -1f, 1f);

        driver.SetMotorInputs(left, right);
    }
}

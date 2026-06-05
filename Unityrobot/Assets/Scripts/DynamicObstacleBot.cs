using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple moving obstacle for robot soccer.
///
/// Behaviour:
/// - Chooses random patrol points inside a zone.
/// - Drives like a differential drive robot.
/// - Waits briefly when reaching destination.
/// - Adds small randomness to movement.
/// - Does NOT chase the ball.
/// </summary>
public class DynamicObstacleBot : MonoBehaviour
{
[Header("References")]

public SimulatedMotorDriver motorDriver;

public Rigidbody rb;

// --------------------------------------------------

[Header("Patrol Area")]

public Transform patrolCenter;

public float patrolWidth = 1.0f;

public float patrolLength = 1.0f;

// --------------------------------------------------

[Header("Movement")]

public float arrivalDistance = 0.15f;

public float forwardSpeed = 1f;

public float turnThreshold = 15f;

// --------------------------------------------------

[Header("Timing")]

public float minWaitTime = 0.5f;

public float maxWaitTime = 2f;

// --------------------------------------------------

[Header("Randomness")]

[Range(0f, 1f)]
public float steeringNoise = 0.15f;

[Header("Respawn")]

public float fallHeight = -0.2f;

private Vector3 initialPosition;
private Quaternion initialRotation;

// --------------------------------------------------

private Vector3 currentTarget;

private bool waiting;

private float waitTimer;

// ==================================================
// UNITY
// ==================================================

private void Start()
{
    initialPosition = transform.position;
    initialRotation = transform.rotation;
    PickNewTarget();
}

private void FixedUpdate()
{
     CheckFall();
    if (motorDriver == null)
        return;

    if (waiting)
    {
        UpdateWaiting();
        return;
    }

    MoveTowardTarget();
}

private void CheckFall()
{
    if (transform.position.y > fallHeight)
        return;

    Respawn();
}

// ==================================================
// WAITING
// ==================================================

private void UpdateWaiting()
{
    waitTimer -= Time.fixedDeltaTime;

    motorDriver.SetMotorInputs(0f, 0f);

    if (waitTimer <= 0f)
    {
        waiting = false;

        PickNewTarget();
    }
}

private void Respawn()
{
    transform.position =
        initialPosition;

    transform.rotation =
        initialRotation;

    if (rb != null)
    {
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    waiting = false;

    PickNewTarget();

    motorDriver.SetMotorInputs(
        0f,
        0f);
}

// ==================================================
// MOVEMENT
// ==================================================

private void MoveTowardTarget()
{
    Vector3 toTarget =
        currentTarget -
        transform.position;

    toTarget.y = 0f;

    float distance =
        toTarget.magnitude;

    if (distance < arrivalDistance)
    {
        StartWaiting();

        return;
    }

    Vector3 localDirection =
        transform.InverseTransformDirection(
            toTarget.normalized);

    float angle =
        Mathf.Atan2(
            localDirection.x,
            localDirection.z)
        * Mathf.Rad2Deg;

    angle +=
        Random.Range(
            -steeringNoise * 10f,
             steeringNoise * 10f);

        // Debug.Log(
    // $"Forward={transform.forward}  ToTarget={toTarget.normalized}");
    //    Debug.Log(
    // $"Angle={angle:F1}");
    float leftMotor = 0f;
    float rightMotor = 0f;

    // ----------------------------
    // TURN LEFT
    // ----------------------------

    if (angle < -turnThreshold)
    {
        leftMotor = 0.5f;
        rightMotor = -0.5f;
    }

    // ----------------------------
    // TURN RIGHT
    // ----------------------------

    else if (angle > turnThreshold)
    {
        leftMotor = -0.5f;
        rightMotor = 0.5f;
    }

    // ----------------------------
    // FORWARD
    // ----------------------------

    else
    {
        leftMotor = forwardSpeed;
        rightMotor = forwardSpeed;
    }

    motorDriver.SetMotorInputs(
        Mathf.Clamp(leftMotor, -1f, 1f),
        Mathf.Clamp(rightMotor, -1f, 1f));
}

// ==================================================
// TARGET SELECTION
// ==================================================

private void PickNewTarget()
{
    Vector3 center =
        patrolCenter.position;

    float x =
        Random.Range(
            -patrolWidth * 0.5f,
             patrolWidth * 0.5f);

    float z =
        Random.Range(
            -patrolLength * 0.5f,
             patrolLength * 0.5f);

    currentTarget =
        center +
        new Vector3(
            x,
            0f,
            z);
}

private void StartWaiting()
{
    waiting = true;

    waitTimer =
        Random.Range(
            minWaitTime,
            maxWaitTime);

    motorDriver.SetMotorInputs(
        0f,
        0f);
}

// ==================================================
// DEBUG
// ==================================================

private void OnDrawGizmosSelected()
{
    if (patrolCenter == null)
        return;

    Gizmos.color = Color.yellow;

    Gizmos.DrawWireCube(
        patrolCenter.position,
        new Vector3(
            patrolWidth,
            0.05f,
            patrolLength));

    Gizmos.color = Color.red;

    Gizmos.DrawSphere(
        currentTarget,
        0.05f);
}
// =====================================================
// DEBUG ACCESSORS
// =====================================================

public Vector3 CurrentTarget
{
    get { return currentTarget; }
}

public bool IsWaiting
{
    get { return waiting; }
}
}


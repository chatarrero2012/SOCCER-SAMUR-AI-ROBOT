using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;

public class SoccerAgent : Agent
{
    // =====================================================
    // REFERENCES
    // =====================================================

    [Header("References")]
    public Rigidbody rb;

    public Rigidbody ballRb;

    public Transform ball;

    public Transform enemyGoal;

    public Camera robotCamera;

    public BoxCollider fieldCollider;

    public Transform roboTarget;

    // =====================================================
    // CURRICULUM
    // =====================================================

    [Header("Curriculum")]

    public int currentCourse = 0;

    public string[] courseNames =
    {
        "LOOK BALL",
        "APPROACH BALL",
        "GET BEHIND BALL",
        "PUSH TO GOAL",
        "SCORE GOAL"
    };

    // =====================================================
    // MOTORS
    // =====================================================

    [Header("Motors")]

    public float maxSpeed = 6f;

    // =====================================================
    // KICK TRAINING
    // =====================================================

    [Header("Kick Training")]

    public float strongKickThreshold = 2.5f;

    // =====================================================
    // YOLO MOCK
    // =====================================================

    [Header("Ball Vision")]

    public bool ballVisible;

    [Range(0f, 1f)]
    public float ballX;

    [Range(0f, 1f)]
    public float ballY;

    [Range(0f, 1f)]
    public float ballSize;

    [Range(0f, 1f)]
    public float goalSize;

    // =====================================================
    // GOAL VISION
    // =====================================================

    [Header("Goal Vision")]

    public bool goalVisible;

    [Range(0f, 1f)]
    public float goalX;

    [Range(0f, 1f)]
    public float goalY;

    // =====================================================
    // FOOTBALL
    // =====================================================

    [Header("Football")]

    public Vector3 behindBallPoint;

    public float behindScore;

    public float attackAlignment;

    public bool attackAligned;

    // =====================================================
    // DEBUG
    // =====================================================

    [Header("Debug")]

    public bool showGUI = true;

    // =====================================================
    // INTERNAL
    // =====================================================

    private float totalReward;

    private int episodes;

    private float lastBallDistance;

    private float lastGoalDistance;

    private float visibleTimer;

    private Vector3 lastBallVelocity;

    private int outFieldLayer;

    // =====================================================
    // INITIALIZE
    // =====================================================

    public override void Initialize()
    {
        if(ballRb == null)
        {
            ballRb =
                ball.GetComponent<Rigidbody>();
        }

        outFieldLayer =
            LayerMask.NameToLayer("OutZone");

        lastBallVelocity =
            Vector3.zero;
    }

    // =====================================================
    // EPISODE BEGIN
    // =====================================================

    public override void OnEpisodeBegin()
    {
        episodes++;

        transform.localPosition =
            new Vector3(
                Random.Range(-1f, 1f),
                0.05f,
                -2f
            );

        transform.rotation =
            Quaternion.Euler(
                0f,
                Random.Range(0f, 360f),
                0f
            );

        rb.velocity = Vector3.zero;

        rb.angularVelocity = Vector3.zero;

        SpawnBall();

        totalReward = 0f;

        visibleTimer = 0f;

        roboTarget.position =
            GetRandomPointInField(fieldCollider);

        lastBallDistance =
            Vector3.Distance(
                transform.position,
                ball.position
            );

        lastGoalDistance =
            Vector3.Distance(
                ball.position,
                roboTarget.position
            );

        lastBallVelocity =
            ballRb.velocity;
    }

    Vector3 GetRandomPointInField(
        BoxCollider fieldCollider
    )
    {
        Vector3 center =
            fieldCollider.bounds.center;

        Vector3 size =
            fieldCollider.bounds.size;

        float x =
            Random.Range(
                -size.x / 2f,
                size.x / 2f
            );

        float z =
            Random.Range(
                -size.z / 2f,
                size.z / 2f
            );

        return new Vector3(
            center.x + x,
            center.y,
            center.z + z
        );
    }

    // =====================================================
    // OBSERVATIONS
    // =====================================================

    public override void CollectObservations(
        VectorSensor sensor
    )
    {
        UpdateVision();

        // BALL

        sensor.AddObservation(
            ballVisible ? 1f : 0f
        );

        sensor.AddObservation(ballX);

        sensor.AddObservation(ballY);

        sensor.AddObservation(ballSize);

        sensor.AddObservation(goalSize);

        // GOAL

        sensor.AddObservation(
            goalVisible ? 1f : 0f
        );

        sensor.AddObservation(goalX);

        sensor.AddObservation(goalY);

        // FOOTBALL

        sensor.AddObservation(
            behindScore
        );

        sensor.AddObservation(
            attackAlignment
        );

        sensor.AddObservation(
            attackAligned ? 1f : 0f
        );

        // BALL SPEED

        sensor.AddObservation(
            ballRb.velocity.magnitude
        );

        // ROBOT SPEED

        sensor.AddObservation(
            rb.velocity.magnitude
        );
    }

    // =====================================================
    // ACTIONS
    // =====================================================

    public override void OnActionReceived(
        ActionBuffers actions
    )
    {
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

        // =================================================
        // DIFFERENTIAL DRIVE
        // =================================================

        float currentMotorForce = 32f;

        float turnTorque = 11f;

        float forward =
            (leftMotor + rightMotor) * 0.5f;

        float turn =
            (rightMotor - leftMotor);

        // =================================================
        // MOVEMENT
        // =================================================

        rb.AddForce(
            transform.forward *
            forward *
            currentMotorForce,
            ForceMode.Force
        );

        // =================================================
        // ROTATION
        // =================================================

        rb.AddTorque(
            Vector3.up *
            turn *
            turnTorque,
            ForceMode.Force
        );

        // =================================================
        // MAX SPEED
        // =================================================

        Vector3 flatVelocity =
            new Vector3(
                rb.velocity.x,
                0f,
                rb.velocity.z
            );

        if(flatVelocity.magnitude > maxSpeed)
        {
            flatVelocity =
                flatVelocity.normalized *
                maxSpeed;

            rb.velocity =
                new Vector3(
                    flatVelocity.x,
                    rb.velocity.y,
                    flatVelocity.z
                );
        }

        // =================================================
        // ANTI FLIP
        // =================================================

        Vector3 av =
            rb.angularVelocity;

        rb.angularVelocity =
            new Vector3(
                0f,
                av.y,
                0f
            );

        // =================================================
        // FALL CHECK
        // =================================================

        if(transform.position.y < -1.3f)
        {
            AddCustomReward(-2f);

            EndEpisode();

            return;
        }

        if(ball.position.y < -1.3f)
        {
            AddCustomReward(-2f);

            EndEpisode();

            return;
        }

        // =================================================
        // EXISTENCE PAIN
        // =================================================

        AddCustomReward(-0.0045f);

        // =================================================
        // ANTI VIBRATION
        // =================================================

        if(Mathf.Abs(leftMotor - rightMotor) > 1.7f)
        {
            AddCustomReward(-0.0015f);
        }

        // =================================================
        // COURSE SYSTEM
        // =================================================

        switch(currentCourse)
        {
            case 0:
                CourseLookBall();
                break;

            case 1:
                CourseApproachBall();
                break;

            case 2:
                CourseBehindBall();
                break;

            case 3:
                CoursePushBall();
                break;

            case 4:
                CourseScoreGoal();
                break;
        }

        // =================================================
        // MAX STEPS
        // =================================================

        if(StepCount > 1500)
        {
            AddCustomReward(-1f);

            EndEpisode();
        }
    }

    // =====================================================
    // COURSE 0
    // =====================================================

    private void CourseLookBall()
    {
        if(ballVisible)
        {
            AddCustomReward(0.00003f);

            float centered =
                1f - Mathf.Abs(ballX - 0.5f);

            AddCustomReward(
                centered *
                centered *
                0.00025f
            );

            visibleTimer +=
                Time.fixedDeltaTime;

            AddCustomReward(
                visibleTimer *
                0.000004f
            );
        }
        else
        {
            visibleTimer = 0f;

            AddCustomReward(-0.0008f);
        }

        float distance =
            Vector3.Distance(
                transform.position,
                ball.position
            );

        float distanceDelta =
            lastBallDistance - distance;

        if(distanceDelta > 0f)
        {
            AddCustomReward(
                distanceDelta * 0.04f
            );
        }
        else
        {
            AddCustomReward(-0.003f);
        }

        lastBallDistance = distance;
    }

    // =====================================================
    // COURSE 1
    // =====================================================

    private void CourseApproachBall()
    {
        CourseLookBall();

        AddCustomReward(
            ballSize * 0.003f
        );

        float distance =
            Vector3.Distance(
                transform.position,
                ball.position
            );

        if(distance < 0.45f)
        {
            AddCustomReward(0.015f);
        }

        // =================================================
        // SPEED ONLY IF MOVING TO BALL
        // =================================================

        Vector3 dirToBall =
            (
                ball.position -
                transform.position
            ).normalized;

        Vector3 flatVel =
            new Vector3(
                rb.velocity.x,
                0f,
                rb.velocity.z
            );

        if(flatVel.magnitude > 0.1f)
        {
            float towardBall =
                Vector3.Dot(
                    flatVel.normalized,
                    dirToBall
                );

            if(towardBall > 0f)
            {
                AddCustomReward(
                    towardBall *
                    flatVel.magnitude *
                    0.0015f
                );
            }
        }
    }

    // =====================================================
    // COURSE 2
    // =====================================================

    private void CourseBehindBall()
    {
        CourseApproachBall();

        AddCustomReward(
            behindScore * 0.0012f
        );

        float targetDistance =
            Vector3.Distance(
                transform.position,
                behindBallPoint
            );

        float targetReward =
            Mathf.Clamp01(
                1f / (targetDistance + 0.15f)
            );

        AddCustomReward(
            targetReward * 0.0015f
        );

        if(
            attackAligned &&
            ballRb.velocity.magnitude > 1.5f
        )
        {
            AddCustomReward(0.03f);
        }
    }

    // =====================================================
    // COURSE 3
    // =====================================================

    private void CoursePushBall()
    {
        CourseBehindBall();

        Vector3 toGoal =
            (
                roboTarget.position -
                ball.position
            ).normalized;

        // =================================================
        // BALL MOVING TO GOAL
        // =================================================

        float velocityToGoal =
            Vector3.Dot(
                ballRb.velocity,
                toGoal
            );

        if(velocityToGoal > 0f)
        {
            AddCustomReward(
                velocityToGoal * 0.05f
            );
        }

        // =================================================
        // BALL ACCELERATION TO GOAL
        // =================================================

        Vector3 acceleration =
            (
                ballRb.velocity -
                lastBallVelocity
            ) / Time.fixedDeltaTime;

        float accelToGoal =
            Vector3.Dot(
                acceleration,
                toGoal
            );

        if(accelToGoal > 0f)
        {
            AddCustomReward(
                accelToGoal * 0.002f
            );
        }

        // =================================================
        // BALL GETTING CLOSER
        // =================================================

        float goalDistance =
            Vector3.Distance(
                ball.position,
                roboTarget.position
            );

        float delta =
            lastGoalDistance -
            goalDistance;

        if(delta > 0f)
        {
            AddCustomReward(
                delta * 0.08f
            );
        }
        else
        {
            AddCustomReward(-0.01f);
        }

        // =================================================
        // BALL STOPPED PENALTY
        // =================================================

        if(
            goalDistance < 3f &&
            ballRb.velocity.magnitude < 0.25f
        )
        {
            AddCustomReward(-0.015f);
        }

        lastGoalDistance =
            goalDistance;

        lastBallVelocity =
            ballRb.velocity;
    }

    // =====================================================
    // COURSE 4
    // =====================================================

    private void CourseScoreGoal()
    {
        CoursePushBall();

        float goalDistance =
            Vector3.Distance(
                ball.position,
                roboTarget.position
            );

        float scoreReward =
            Mathf.Clamp01(
                1f / (goalDistance + 0.15f)
            );

        AddCustomReward(
            scoreReward * 0.04f
        );
    }

    // =====================================================
    // COLLISIONS
    // =====================================================

    private void OnCollisionEnter(
        Collision collision
    )
    {
        if(
            collision.gameObject.layer ==
            outFieldLayer
        )
        {
            AddCustomReward(-2f);

            EndEpisode();

            return;
        }

        if(collision.transform == ball)
        {
            float impactForce =
                collision.relativeVelocity.magnitude;

            Vector3 toGoal =
                (
                    roboTarget.position -
                    ball.position
                ).normalized;

            float ballTowardGoal =
                Vector3.Dot(
                    ballRb.velocity,
                    toGoal
                );

            // =================================================
            // HIT REWARD ONLY IF USEFUL
            // =================================================

            if(ballTowardGoal > 0f)
            {
                AddCustomReward(
                    impactForce *
                    ballTowardGoal *
                    0.03f
                );
            }

            // =================================================
            // STRONG SHOT BONUS
            // =================================================

            if(
                impactForce > strongKickThreshold &&
                attackAligned
            )
            {
                AddCustomReward(1.2f);
            }

            // =================================================
            // MASSIVE SHOT
            // =================================================

            if(
                impactForce > 5f &&
                ballTowardGoal > 2f
            )
            {
                AddCustomReward(2f);
            }
        }
    }

    // =====================================================
    // GOAL
    // =====================================================

    public void OnGoalScored()
    {
        AddCustomReward(15f);

        Debug.Log("GOOOOOLLLLL");

        EndEpisode();
    }

    public void OnGoalSelfGoal()
    {
        AddCustomReward(-15f);

        Debug.Log("SELF GOAL");

        EndEpisode();
    }

    // =====================================================
    // VISION
    // =====================================================

    private void UpdateVision()
    {
        Vector3 ballViewport =
            robotCamera.WorldToViewportPoint(
                ball.position
            );

        ballVisible =
            ballViewport.z > 0 &&
            ballViewport.x >= 0 &&
            ballViewport.x <= 1 &&
            ballViewport.y >= 0 &&
            ballViewport.y <= 1;

        if(ballVisible)
        {
            ballX = ballViewport.x;

            ballY = ballViewport.y;

            float distance =
                Vector3.Distance(
                    transform.position,
                    ball.position
                );

            float realBallDiameter = 0.04f;

            ballSize =
                Mathf.Clamp01(
                    realBallDiameter /
                    distance
                );
        }
        else
        {
            ballX = 0f;

            ballY = 0f;

            ballSize = 0f;
        }

        // =================================================
        // GOAL
        // =================================================

        Vector3 goalViewport =
            robotCamera.WorldToViewportPoint(
                enemyGoal.position
            );

        goalVisible =
            goalViewport.z > 0 &&
            goalViewport.x >= 0 &&
            goalViewport.x <= 1 &&
            goalViewport.y >= 0 &&
            goalViewport.y <= 1;

        if(goalVisible)
        {
            goalX = goalViewport.x;

            goalY = goalViewport.y;

            float gdistance =
                Vector3.Distance(
                    transform.position,
                    enemyGoal.position
                );

            float realGoalWidth = 0.838f;

            goalSize =
                Mathf.Clamp01(
                    realGoalWidth /
                    gdistance
                );
        }
        else
        {
            goalX = 0f;

            goalY = 0f;

            goalSize = 0f;
        }

        CalculateFootballPositioning();
    }

    // =====================================================
    // FOOTBALL POSITIONING
    // =====================================================

    private void CalculateFootballPositioning()
    {
        Vector3 goalDir =
            (
                roboTarget.position -
                ball.position
            ).normalized;

        behindBallPoint =
            ball.position -
            goalDir * 0.42f;

        Vector3 ballToGoal =
            (
                roboTarget.position -
                ball.position
            ).normalized;

        Vector3 ballToRobot =
            (
                transform.position -
                ball.position
            ).normalized;

        behindScore =
            Mathf.Clamp01(
                Vector3.Dot(
                    ballToRobot,
                    -ballToGoal
                )
            );

        Vector3 toGoal =
            (
                roboTarget.position -
                transform.position
            ).normalized;

        Vector3 toBall =
            (
                ball.position -
                transform.position
            ).normalized;

        attackAlignment =
            Mathf.Clamp01(
                Vector3.Dot(
                    toGoal,
                    toBall
                )
            );

        attackAligned =
            attackAlignment > 0.90f &&
            behindScore > 0.72f;
    }

    // =====================================================
    // CUSTOM REWARD
    // =====================================================

    private void AddCustomReward(
        float reward
    )
    {
        AddReward(reward);

        totalReward += reward;
    }

    // =====================================================
    // HEURISTIC
    // =====================================================

    public override void Heuristic(
        in ActionBuffers actionsOut
    )
    {
        var actions =
            actionsOut.ContinuousActions;

        actions[0] =
            Input.GetAxis("Vertical") -
            Input.GetAxis("Horizontal");

        actions[1] =
            Input.GetAxis("Vertical") +
            Input.GetAxis("Horizontal");
    }

    // =====================================================
    // BALL SPAWN
    // =====================================================

    private void SpawnBall()
    {
        Vector3 randomPos =
            new Vector3(
                Random.Range(-2.1f, 2.1f),
                0.68f,
                Random.Range(-1.95f, 0.15f)
            );

        ball.position =
            randomPos;

        ballRb.velocity =
            Vector3.zero;

        ballRb.angularVelocity =
            Vector3.zero;
    }
}
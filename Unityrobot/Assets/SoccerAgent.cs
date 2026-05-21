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

    public float motorForce = 6f;

    public float turnForce = 400f;

    public float maxSpeed = 6f;

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

        lastBallDistance =
            Vector3.Distance(
                transform.position,
                ball.position
            );

        lastGoalDistance =
            Vector3.Distance(
                ball.position,
                enemyGoal.position
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

        // TARGET POINT

        Vector3 localTarget =
            transform.InverseTransformPoint(
                behindBallPoint
            );


        // SPEED

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

// =====================================
// SETTINGS
// =====================================

float currentMotorForce = 25f;
float turnTorque = 12f;

// =====================================
// FORWARD / TURN
// =====================================

float forward =
    (leftMotor + rightMotor) * 0.5f;

float turn =
    (rightMotor - leftMotor);

// =====================================
// MOVEMENT
// =====================================

rb.AddForce(
    transform.forward *
    forward *
    currentMotorForce,
    ForceMode.Force
);

// =====================================
// ROTATION
// =====================================

rb.AddTorque(
    Vector3.up *
    turn *
    turnTorque,
    ForceMode.Force
);

// =====================================
// MAX SPEED
// =====================================

Vector3 flatVelocity =
    new Vector3(
        rb.velocity.x,
        0f,
        rb.velocity.z
    );

if (flatVelocity.magnitude > maxSpeed)
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

// =====================================
// ANTI FLIP
// =====================================

Vector3 av = rb.angularVelocity;

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
            AddCustomReward(-1f);

            EndEpisode();

            return;
        }

        if(ball.position.y < -1.3f)
        {
            AddCustomReward(-0.5f);

            EndEpisode();

            return;
        }

        // =================================================
        // STEP PENALTY
        // =================================================

        AddCustomReward(-0.0002f);

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

        if(StepCount > 2000)
        {
            EndEpisode();
        }
    }

    // =====================================================
    // COURSE 0
    // LOOK BALL
    // =====================================================

    private void CourseLookBall()
    {
        if(ballVisible)
        {
            // SMALL REWARD

            AddCustomReward(0.0003f);

            // KEEP BALL CENTERED

            float centered =
                1f - Mathf.Abs(ballX - 0.5f);

            AddCustomReward(
                centered * centered * 0.004f
            );


            // STABLE VISION

            visibleTimer +=
                Time.fixedDeltaTime;

            AddCustomReward(
                visibleTimer * 0.0001f
            );
        }
        else
        {
            visibleTimer = 0f;

            AddCustomReward(-0.0005f);
        }

        // =================================================
        // PROXIMITY TO BALL
        // =================================================

        float distance =
            Vector3.Distance(
                transform.position,
                ball.position
            );

        float proximityReward =
            Mathf.Clamp01(
                1f / (distance + 0.5f)
            );

        AddCustomReward(
            proximityReward * 0.002f
        );

        // =================================================
        // REWARD GETTING CLOSER
        // =================================================

        if(distance < lastBallDistance)
        {
            AddCustomReward(0.001f);
        }
        else
        {
            AddCustomReward(-0.003f);
        }

        lastBallDistance = distance;

        // =================================================
        // TOO FAR EXPLOIT PREVENTION
        // =================================================

        if(distance > 3f)
        {
            AddCustomReward(-0.005f);
        }

        // =================================================
        // TOO FAST PENALTY
        // =================================================

        float speed =
            rb.velocity.magnitude;

        if(speed > 1.2f)
        {
            AddCustomReward(-0.003f);
        }
    }

    // =====================================================
    // COURSE 1
    // APPROACH BALL
    // =====================================================

    private void CourseApproachBall()
    {
        CourseLookBall();

        // BIGGER BALL

        AddCustomReward(
            ballSize * 0.004f
        );

        // TOUCH BALL

        float distance =
            Vector3.Distance(
                transform.position,
                ball.position
            );

        if(distance < 0.4f)
        {
            AddCustomReward(0.03f);
        }
    }

    // =====================================================
    // COURSE 2
    // GET BEHIND BALL
    // =====================================================

    private void CourseBehindBall()
    {
        CourseApproachBall();

        // =================================================
        // POSITIONING
        // =================================================

        AddCustomReward(
            behindScore * 0.005f
        );

        float targetDistance =
            Vector3.Distance(
                transform.position,
                behindBallPoint
            );

        float targetReward =
            Mathf.Clamp01(
                1f / (targetDistance + 0.2f)
            );

        AddCustomReward(
            targetReward * 0.006f
        );

        // =================================================
        // PERFECT POSITION
        // =================================================

        if(behindScore > 0.8f)
        {
            AddCustomReward(0.03f);
        }

        if(attackAligned)
        {
            AddCustomReward(0.05f);
        }
    }

    // =====================================================
    // COURSE 3
    // PUSH BALL
    // =====================================================

    private void CoursePushBall()
    {
        CourseBehindBall();

        Vector3 toGoal =
            (
                enemyGoal.position -
                ball.position
            ).normalized;

        float velocityToGoal =
            Vector3.Dot(
                ballRb.velocity,
                toGoal
            );

        AddCustomReward(
            velocityToGoal * 0.01f
        );

        // =================================================
        // BALL GETTING CLOSER TO GOAL
        // =================================================

        float goalDistance =
            Vector3.Distance(
                ball.position,
                enemyGoal.position
            );

        if(goalDistance < lastGoalDistance)
        {
            AddCustomReward(0.02f);
        }
        else
        {
            AddCustomReward(-0.002f);
        }

        lastGoalDistance =
            goalDistance;
    }

    // =====================================================
    // COURSE 4
    // SCORE GOAL
    // =====================================================

    private void CourseScoreGoal()
    {
        CoursePushBall();

        float goalDistance =
            Vector3.Distance(
                ball.position,
                enemyGoal.position
            );

        float scoreReward =
            Mathf.Clamp01(
                1f / (goalDistance + 0.2f)
            );

        AddCustomReward(
            scoreReward * 0.02f
        );
    }

    // =====================================================
    // COLLISIONS
    // =====================================================

    private void OnCollisionEnter(
        Collision collision
    )
    {
        if(collision.transform == ball)
        {
            AddCustomReward(0.1f);

            if(attackAligned)
            {
                AddCustomReward(0.2f);
            }
        }
    }

    // =====================================================
    // GOAL
    // =====================================================

    public void OnGoalScored()
    {
        AddCustomReward(5f);

        EndEpisode();
    }

    // =====================================================
    // VISION
    // =====================================================

    private void UpdateVision()
    {
        // =================================================
        // BALL
        // =================================================

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

            ballSize = Mathf.Clamp01(realBallDiameter / distance);
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

            goalSize = Mathf.Clamp01(realGoalWidth / gdistance);
        }
        else
        {
            goalX = 0f;

            goalY = 0f;

            goalSize = 0f;
        }

        // =================================================
        // FOOTBALL POSITIONING
        // =================================================

        CalculateFootballPositioning();
    }

    // =====================================================
    // FOOTBALL POSITIONING
    // =====================================================

    private void CalculateFootballPositioning()
    {
        Vector3 goalDir =
            (
                enemyGoal.position -
                ball.position
            ).normalized;

        // TARGET POINT

        behindBallPoint =
            ball.position -
            goalDir * 0.45f;

        // =================================================
        // BEHIND SCORE
        // =================================================

        Vector3 ballToGoal =
            (
                enemyGoal.position -
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

        // =================================================
        // ATTACK ALIGNMENT
        // =================================================

        Vector3 toGoal =
            (
                enemyGoal.position -
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
            attackAlignment > 0.92f &&
            behindScore > 0.75f;
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

    // =====================================================
    // DEBUG
    // =====================================================

    private void Update()
    {
        // ROBOT -> TARGET

        Debug.DrawLine(
            transform.position,
            behindBallPoint,
            Color.cyan
        );

        // TARGET -> BALL

        Debug.DrawLine(
            behindBallPoint,
            ball.position,
            Color.green
        );

        // BALL -> GOAL

        Debug.DrawLine(
            ball.position,
            enemyGoal.position,
            Color.yellow
        );

        // ROBOT -> GOAL

        Debug.DrawLine(
            transform.position,
            enemyGoal.position,
            attackAligned
                ? Color.green
                : Color.red
        );
    }

    // =====================================================
    // GIZMOS
    // =====================================================

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;

        Gizmos.DrawSphere(
            behindBallPoint,
            0.08f
        );

        Gizmos.DrawLine(
            behindBallPoint,
            ball.position
        );

        Gizmos.color = Color.yellow;

        Gizmos.DrawLine(
            ball.position,
            enemyGoal.position
        );
    }

    // =====================================================
    // GUI
    // =====================================================

// =====================================================
// ON GUI
// =====================================================

private void OnGUI()
{
    // =================================================
    // YOLO BALL
    // =================================================

    if(ballVisible)
    {
        DrawYOLOBox(
            ballX,
            ballY,

            // WIDTH
            Mathf.Clamp(
                ballSize * 2.5f,
                0.015f,
                0.08f
            ),

            // HEIGHT
            Mathf.Clamp(
                ballSize * 2.5f,
                0.015f,
                0.08f
            ),

            "BALL " +
            Mathf.RoundToInt(
                ballSize * 100f
            ) + "%",

            Color.green
        );
    }

    // =================================================
    // YOLO GOAL
    // =================================================

    if(goalVisible)
    {
        DrawYOLOBox(
            goalX,
            goalY,

            // WIDTH
            Mathf.Clamp(
                goalSize,
                0.3f,
                0.12f
            ),

            // HEIGHT
            Mathf.Clamp(
                goalSize / 3.0f,
                0.1f,
                0.4f
            ),

            "GOAL " +
            Mathf.RoundToInt(
                goalSize * 100f
            ) + "%",

            Color.green
        );
    }

    // =================================================
    // DEBUG PANEL
    // =================================================

    if(!showGUI)
        return;

    GUILayout.BeginArea(
        new Rect(10, 10, 420, 720),
        GUI.skin.box
    );

    GUILayout.Label(
        "⚽ SAMUR-AI FOOTBALL"
    );

    GUILayout.Space(10);

    GUILayout.Label(
        "Episode: " + episodes
    );

    GUILayout.Label(
        "Step: " + StepCount
    );

    GUILayout.Label(
        "Course: " + currentCourse
    );

    GUILayout.Label(
        "Mode: " +
        courseNames[currentCourse]
    );

    GUILayout.Space(10);

    GUILayout.Label(
        "=== BALL ==="
    );

    GUILayout.Label(
        "Visible: " +
        ballVisible
    );

    GUILayout.Label(
        "X: " +
        ballX.ToString("F2")
    );

    GUILayout.Label(
        "Y: " +
        ballY.ToString("F2")
    );

    GUILayout.Label(
        "Size: " +
        ballSize.ToString("F2")
    );

    GUILayout.Space(10);

    GUILayout.Label(
        "=== GOAL ==="
    );

    GUILayout.Label(
        "Visible: " +
        goalVisible
    );

    GUILayout.Label(
        "Goal X: " +
        goalX.ToString("F2")
    );

    GUILayout.Label(
        "Goal Y: " +
        goalY.ToString("F2")
    );

    GUILayout.Label(
        "Goal Size: " +
        goalSize.ToString("F2")
    );

    GUILayout.Space(10);

    GUILayout.Label(
        "=== FOOTBALL ==="
    );

    GUILayout.Label(
        "Behind Score: " +
        behindScore.ToString("F2")
    );

    GUILayout.Label(
        "Attack Alignment: " +
        attackAlignment.ToString("F2")
    );

    GUILayout.Label(
        "Attack Aligned: " +
        attackAligned
    );

    GUILayout.Space(10);

    GUILayout.Label(
        "Reward: " +
        totalReward.ToString("F3")
    );

    GUILayout.Space(20);

    // =================================================
    // BUTTONS
    // =================================================

    if(
        GUILayout.Button(
            "NEXT COURSE",
            GUILayout.Height(40)
        )
    )
    {
        currentCourse++;

        if(
            currentCourse >=
            courseNames.Length
        )
        {
            currentCourse = 0;
        }

        EndEpisode();
    }

    if(
        GUILayout.Button(
            "RESET EPISODE",
            GUILayout.Height(40)
        )
    )
    {
        EndEpisode();
    }

    GUILayout.EndArea();
}

// =====================================================
// YOLO DEBUG GUI
// =====================================================

private void DrawYOLOBox(
    float x,
    float y,
    float widthNorm,
    float heightNorm,
    string label,
    Color color
)
{
    // INVALID

    if(widthNorm <= 0f ||
       heightNorm <= 0f)
    {
        return;
    }

    // =================================================
    // VIEWPORT -> SCREEN
    // =================================================

    float screenX =
        x * Screen.width;

    float screenY =
        (1f - y) * Screen.height;

    // =================================================
    // YOLO IMPERFECTION
    // =================================================

    screenX +=
        Random.Range(-0.5f, 0.5f);

    screenY +=
        Random.Range(-0.5f, 0.5f);

    // =================================================
    // BOX SIZE
    // =================================================

    float boxWidth =
        widthNorm * Screen.width;

    float boxHeight =
        heightNorm * Screen.height;

    Rect rect =
        new Rect(
            screenX - boxWidth * 0.5f,
            screenY - boxHeight * 0.5f,
            boxWidth,
            boxHeight
        );

    // =================================================
    // COLOR
    // =================================================

    Color oldColor =
        GUI.color;

    GUI.color = color;

    // =================================================
    // BOX LINES
    // =================================================

    // TOP

    GUI.DrawTexture(
        new Rect(
            rect.x,
            rect.y,
            rect.width,
            3f
        ),
        Texture2D.whiteTexture
    );

    // BOTTOM

    GUI.DrawTexture(
        new Rect(
            rect.x,
            rect.yMax,
            rect.width,
            3f
        ),
        Texture2D.whiteTexture
    );

    // LEFT

    GUI.DrawTexture(
        new Rect(
            rect.x,
            rect.y,
            3f,
            rect.height
        ),
        Texture2D.whiteTexture
    );

    // RIGHT

    GUI.DrawTexture(
        new Rect(
            rect.xMax,
            rect.y,
            3f,
            rect.height
        ),
        Texture2D.whiteTexture
    );

    // =================================================
    // CORNERS
    // =================================================

    float corner = 18f;

    // TOP LEFT

    GUI.DrawTexture(
        new Rect(
            rect.x,
            rect.y,
            corner,
            5f
        ),
        Texture2D.whiteTexture
    );

    GUI.DrawTexture(
        new Rect(
            rect.x,
            rect.y,
            5f,
            corner
        ),
        Texture2D.whiteTexture
    );

    // TOP RIGHT

    GUI.DrawTexture(
        new Rect(
            rect.xMax - corner,
            rect.y,
            corner,
            5f
        ),
        Texture2D.whiteTexture
    );

    GUI.DrawTexture(
        new Rect(
            rect.xMax - 5f,
            rect.y,
            5f,
            corner
        ),
        Texture2D.whiteTexture
    );

    // BOTTOM LEFT

    GUI.DrawTexture(
        new Rect(
            rect.x,
            rect.yMax - 5f,
            corner,
            5f
        ),
        Texture2D.whiteTexture
    );

    GUI.DrawTexture(
        new Rect(
            rect.x,
            rect.yMax - corner,
            5f,
            corner
        ),
        Texture2D.whiteTexture
    );

    // BOTTOM RIGHT

    GUI.DrawTexture(
        new Rect(
            rect.xMax - corner,
            rect.yMax - 5f,
            corner,
            5f
        ),
        Texture2D.whiteTexture
    );

    GUI.DrawTexture(
        new Rect(
            rect.xMax - 5f,
            rect.yMax - corner,
            5f,
            corner
        ),
        Texture2D.whiteTexture
    );

    // =================================================
    // LABEL BG
    // =================================================

    GUI.DrawTexture(
        new Rect(
            rect.x,
            rect.y - 24f,
            160f,
            24f
        ),
        Texture2D.whiteTexture
    );

    // =================================================
    // LABEL STYLE
    // =================================================

    GUIStyle style =
        new GUIStyle(
            GUI.skin.label
        );

    style.normal.textColor =
        Color.black;

    style.fontStyle =
        FontStyle.Bold;

    style.fontSize = 14;

    // =================================================
    // LABEL
    // =================================================

    GUI.Label(
        new Rect(
            rect.x + 6f,
            rect.y - 24f,
            160f,
            24f
        ),
        label,
        style
    );

    GUI.color = oldColor;
}
}
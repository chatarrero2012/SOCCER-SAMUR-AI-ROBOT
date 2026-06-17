using UnityEngine;

/// <summary>
/// NPC Dinámico con Comportamiento Táctico y Límites de Cancha.
/// </summary>
public class DynamicObstacleBot : MonoBehaviour
{
    [Header("References")]
    public SimulatedMotorDriver motorDriver;
    public Rigidbody rb;
    [Tooltip("Arrastra aquí la Transform del balón")]
    public Transform ball;

    [Header("Cone Configuration (from SoccerAgent)")]
    public Transform coneAnchor;
    public Transform coneDirectionTarget;

    // 🛡️ NUEVO: LÍMITES DE LA CANCHA
    [Header("Table Boundaries (Anti-Void)")]
    [Tooltip("Arrastra el centro de la cancha/mesa")]
    public Transform tableCenter;
    public float tableWidth = 1.5f;
    public float tableLength = 1.5f;
    [Tooltip("Margen de seguridad para que no se pegue al borde exacto")]
    public float safetyMargin = 0.1f; 

    [Header("Movement & Behavior")]
    public float arrivalDistance = 0.15f;
    public float forwardSpeed = 1f;
    public float chaseSpeedMultiplier = 1.5f; 
    public float turnThreshold = 15f;
    public float minWaitTime = 0.5f;
    public float maxWaitTime = 2f;
    public float stateChangeInterval = 4f;    
    
    [Header("Probabilities (0 to 1)")]
    [Range(0f, 1f)] public float chaseBallChance = 0.25f;   
    [Range(0f, 1f)] public float wanderAwayChance = 0.35f;  
    
    [Header("Randomness & Fallback")]
    [Range(0f, 1f)] public float steeringNoise = 0.15f;
    public float fallHeight = -0.2f;

    // Privados
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Vector3 currentTarget;
    private bool waiting;
    private float waitTimer;
    private float stateTimer;

    public enum BehaviorMode { ConePatrol, WanderAway, BallChase }
    public BehaviorMode currentMode = BehaviorMode.ConePatrol;

    // ==================================================
    // UNITY LIFECYCLE
    // ==================================================
    private void Start()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        stateTimer = stateChangeInterval;
        PickNewTarget();
    }

    private void FixedUpdate()
    {
        CheckFall();
        if (motorDriver == null) return;

        if (!waiting)
        {
            stateTimer -= Time.fixedDeltaTime;
            if (stateTimer <= 0f)
            {
                DecideNextBehavior();
                PickNewTarget();
                stateTimer = Random.Range(stateChangeInterval * 0.6f, stateChangeInterval * 1.4f);
            }
        }

        if (waiting)
        {
            UpdateWaiting();
            return;
        }

        MoveTowardTarget();
    }

    // ==================================================
    // STATE MACHINE & TARGET SELECTION
    // ==================================================
    private void DecideNextBehavior()
    {
        float roll = Random.value;
        if (ball != null && roll < chaseBallChance)
        {
            currentMode = BehaviorMode.BallChase;
        }
        else if (roll < chaseBallChance + wanderAwayChance)
        {
            currentMode = BehaviorMode.WanderAway;
        }
        else
        {
            currentMode = BehaviorMode.ConePatrol;
        }
    }

    private void PickNewTarget()
    {
        switch (currentMode)
        {
            case BehaviorMode.ConePatrol:
                PickTargetInsideCone();
                break;
            case BehaviorMode.WanderAway:
                PickTargetOutsideCone();
                break;
            case BehaviorMode.BallChase:
                currentTarget = ball != null ? ball.position : PickFallbackTarget();
                break;
        }

        // 🛡️ BLINDAJE TÁCTICO: Asegurar que el target NUNCA esté fuera de la cancha
        currentTarget = ClampToTable(currentTarget);
    }

    private void PickTargetInsideCone()
    {
        if (coneAnchor == null || coneDirectionTarget == null) { PickFallbackTarget(); return; }
        
        Vector3 center = coneAnchor.position;
        Vector3 forward = (coneDirectionTarget.position - center).normalized;
        float dist = Random.Range(1.0f, 2.5f);
        float angle = Random.Range(-15f, 15f);
        
        Quaternion rot = Quaternion.Euler(0f, angle, 0f);
        currentTarget = center + (rot * forward) * dist;
    }

    private void PickTargetOutsideCone()
    {
        if (coneAnchor == null || coneDirectionTarget == null) { PickFallbackTarget(); return; }
        
        Vector3 center = coneAnchor.position;
        Vector3 forward = (coneDirectionTarget.position - center).normalized;
        float dist = Random.Range(0.8f, 3.0f);
        float angle = Random.Range(25f, 90f) * (Random.value > 0.5f ? 1f : -1f);
        
        Quaternion rot = Quaternion.Euler(0f, angle, 0f);
        currentTarget = center + (rot * forward) * dist;
    }

    private Vector3 PickFallbackTarget()
    {
        return transform.position + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
    }

    // 🛡️ NUEVO MÉTODO: La "Valla Virtual"
    private Vector3 ClampToTable(Vector3 target)
    {
        if (tableCenter == null) return target;
        
        Vector3 center = tableCenter.position;
        float halfW = (tableWidth * 0.5f) - safetyMargin;
        float halfL = (tableLength * 0.5f) - safetyMargin;

        float minX = center.x - halfW;
        float maxX = center.x + halfW;
        float minZ = center.z - halfL;
        float maxZ = center.z + halfL;

        // Recorta las coordenadas X y Z para que no se pasen del rectángulo
        target.x = Mathf.Clamp(target.x, minX, maxX);
        target.z = Mathf.Clamp(target.z, minZ, maxZ);
        
        return target;
    }

    // ==================================================
    // WAITING & RESPAWN
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
        transform.position = initialPosition;
        transform.rotation = initialRotation;

        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        waiting = false;
        stateTimer = stateChangeInterval;
        PickNewTarget();
        motorDriver.SetMotorInputs(0f, 0f);
    }

    // ==================================================
    // MOVEMENT
    // ==================================================
    private void MoveTowardTarget()
    {
        Vector3 toTarget = currentTarget - transform.position;
        toTarget.y = 0f;

        float distance = toTarget.magnitude;

        if (distance < arrivalDistance)
        {
            StartWaiting();
            return;
        }

        Vector3 localDirection = transform.InverseTransformDirection(toTarget.normalized);
        float angle = Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg;
        angle += Random.Range(-steeringNoise * 10f, steeringNoise * 10f);

        float speed = (currentMode == BehaviorMode.BallChase) ? forwardSpeed * chaseSpeedMultiplier : forwardSpeed;
        float leftMotor = 0f, rightMotor = 0f;

        if (angle < -turnThreshold)
        {
            leftMotor = speed * 0.5f;
            rightMotor = -speed * 0.5f;
        }
        else if (angle > turnThreshold)
        {
            leftMotor = -speed * 0.5f;
            rightMotor = speed * 0.5f;
        }
        else
        {
            leftMotor = speed;
            rightMotor = speed;
        }

        motorDriver.SetMotorInputs(Mathf.Clamp(leftMotor, -1f, 1f), Mathf.Clamp(rightMotor, -1f, 1f));
    }

    // ==================================================
    // UTILS
    // ==================================================
    private void CheckFall()
    {
        if (transform.position.y > fallHeight) return;
        Respawn();
    }

    private void StartWaiting()
    {
        waiting = true;
        waitTimer = Random.Range(minWaitTime, maxWaitTime);
        motorDriver.SetMotorInputs(0f, 0f);
    }

    // =====================================================
    // DEBUG ACCESSORS
    // =====================================================
    public Vector3 CurrentTarget { get { return currentTarget; } }
    public bool IsWaiting { get { return waiting; } }

    private void OnDrawGizmosSelected()
    {
        if (coneAnchor == null || coneDirectionTarget == null) return;
        Gizmos.color = currentMode == BehaviorMode.BallChase ? Color.red : Color.magenta;
        Gizmos.DrawSphere(currentTarget, 0.08f);
    }
}
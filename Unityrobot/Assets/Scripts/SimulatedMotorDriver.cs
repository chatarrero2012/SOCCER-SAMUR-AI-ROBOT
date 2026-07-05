using UnityEngine;

public class SimulatedMotorDriver : MonoBehaviour
{
    [Header("References")]
    public Rigidbody rb;
    public Transform leftWheelPoint;
    public Transform rightWheelPoint;
    
    [Tooltip("Punto virtual para ajustar el Centro de Masa. Crucial por el peso del smartphone.")]
    public Transform centerOfMassOverride;

    [Header("Motor Inputs")]
    [Range(-1f, 1f)] public float leftInput;
    [Range(-1f, 1f)] public float rightInput;

    [Header("Battery")]
    [Range(0f, 1f)] public float currentBatteryCharge = 1f;

    [Header("Sim2Real: Imperfecciones Físicas")]
    public float maxWheelForce = 0.25f;
    public float motorResponseSpeed = 4f;
    public float lateralGrip = 0.5f;

    [Tooltip("Umbral mínimo de input para vencer la fricción estática del motor real.")]
    public float motorDeadband = 0.15f; 
    
    [Tooltip("Multiplicador de asimetría. Ejemplo: 0.95 hace que el motor derecho sea un 5% más débil.")]
    [Range(0.8f, 1.2f)] public float rightMotorBias = 0.95f;

    [Header("Debug")]
    public bool drawDebug = true;

    private float currentLeftMotor;
    private float currentRightMotor;

    private void Start()
    {
        // Ajustar el Centro de Masa para simular el peso elevado del smartphone
        if (rb != null && centerOfMassOverride != null)
        {
            rb.centerOfMass = centerOfMassOverride.localPosition;
        }
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        UpdateMotorResponse();
        ApplyDriveForces();
        ApplyLateralGrip();
    }

    private void UpdateMotorResponse()
    {
        currentLeftMotor = Mathf.MoveTowards(
            currentLeftMotor,
            leftInput,
            motorResponseSpeed * Time.fixedDeltaTime);

        currentRightMotor = Mathf.MoveTowards(
            currentRightMotor,
            rightInput,
            motorResponseSpeed * Time.fixedDeltaTime);
    }

    private void ApplyDriveForces()
    {
        float batteryFactor = Mathf.Clamp01(currentBatteryCharge);

        // Simular Zona Muerta (Deadband)
        float effLeftMotor = Mathf.Abs(currentLeftMotor) < motorDeadband ? 0f : currentLeftMotor;
        float effRightMotor = Mathf.Abs(currentRightMotor) < motorDeadband ? 0f : currentRightMotor;

        // Aplicar fuerza y asimetría
        float leftForceMagnitude = effLeftMotor * maxWheelForce * batteryFactor;
        float rightForceMagnitude = effRightMotor * maxWheelForce * batteryFactor * rightMotorBias;

        Vector3 leftForce = leftWheelPoint.forward * leftForceMagnitude;
        Vector3 rightForce = rightWheelPoint.forward * rightForceMagnitude;

        rb.AddForceAtPosition(leftForce, leftWheelPoint.position, ForceMode.Force);
        rb.AddForceAtPosition(rightForce, rightWheelPoint.position, ForceMode.Force);

        if (drawDebug)
        {
            Debug.DrawRay(leftWheelPoint.position, leftWheelPoint.forward * 0.15f, Color.red);
            Debug.DrawRay(rightWheelPoint.position, rightWheelPoint.forward * 0.15f, Color.blue);
        }
    }

    private void ApplyLateralGrip()
    {
        ApplyWheelGrip(leftWheelPoint);
        ApplyWheelGrip(rightWheelPoint);
    }

    private void ApplyWheelGrip(Transform wheel)
    {
        Vector3 velocityAtWheel = rb.GetPointVelocity(wheel.position);
        Vector3 lateralVelocity = Vector3.Project(velocityAtWheel, wheel.right);
        Vector3 correctionForce = -lateralVelocity * lateralGrip;

        rb.AddForceAtPosition(correctionForce, wheel.position, ForceMode.Force);
    }

    public void SetMotorInputs(float left, float right)
    {
        leftInput = Mathf.Clamp(left, -1f, 1f);
        rightInput = Mathf.Clamp(right, -1f, 1f);
    }

    public void Stop()
    {
        leftInput = 0f;
        rightInput = 0f;
    }
}
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class ArrowKeysFlyCam : MonoBehaviour
{
    [Header("Velocità base")]
    public float moveSpeed = 3f;         // m/s
    public float fastMultiplier = 3f;    // Shift
    public float slowMultiplier = 0.3f;  // Ctrl

    [Header("Dinamica")]
    public float acceleration = 12f;     // quanto velocemente raggiunge la velocità target (m/s^2)
    public float decelHalfLife = 0.6f;   // s per dimezzare la velocità quando NON premi tasti
    public float minStopSpeed = 0.01f;   // soglia per azzerare il movimento residuo

    [Header("Vincoli")]
    public bool lockY = false;
    public float lockedY = 1.7f;

    [Header("Opzione: frecce ruotano")]
    public bool rotateWithArrows = false;
    public float rotationSpeed = 90f;    // °/s (target)
    public float angAccel = 720f;        // °/s^2
    public float angDecelHalfLife = 0.3f;

    Vector3 vel;           // velocità lineare effettiva (m/s)
    float yawVel, pitchVel; // velocità angolare (°/s)

    void Start()
    {
        if (lockY) lockedY = transform.position.y;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        // input frecce
        float x = (Input.GetKey(KeyCode.RightArrow) ? 1f : 0f) - (Input.GetKey(KeyCode.LeftArrow) ? 1f : 0f);
        float z = (Input.GetKey(KeyCode.UpArrow)    ? 1f : 0f) - (Input.GetKey(KeyCode.DownArrow) ? 1f : 0f);
        float y = (Input.GetKey(KeyCode.PageUp)     ? 1f : 0f) - (Input.GetKey(KeyCode.PageDown)  ? 1f : 0f);
        y += (Input.GetKey(KeyCode.E) ? 1f : 0f) - (Input.GetKey(KeyCode.Q) ? 1f : 0f);

        float mult = 1f;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))  mult *= fastMultiplier;
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) mult *= slowMultiplier;

        if (rotateWithArrows)
        {
            // --- Rotazione con inerzia ---
            float yawTarget   = x * rotationSpeed;   // ←/→
            float pitchTarget = -z * rotationSpeed;  // ↑/↓

            yawVel   = MoveTowardsWithAccel(yawVel,   yawTarget, angAccel, dt);
            pitchVel = MoveTowardsWithAccel(pitchVel, pitchTarget, angAccel, dt);

            if (Mathf.Approximately(yawTarget, 0f))   yawVel   = DecayHalfLife(yawVel,   angDecelHalfLife, dt);
            if (Mathf.Approximately(pitchTarget, 0f)) pitchVel = DecayHalfLife(pitchVel, angDecelHalfLife, dt);

            transform.Rotate(pitchVel * dt, yawVel * dt, 0f, Space.Self);

            // su/giù mantiene inerzia lineare
            Vector3 inputUpDown = Vector3.up * y;
            ApplyLinearMotion(inputUpDown, mult, dt);
        }
        else
        {
            // --- Traslazione con inerzia ---
            Vector3 inputDir = new Vector3(x, y, z);
            ApplyLinearMotion(inputDir, mult, dt);
        }

        if (lockY)
        {
            var p = transform.position; p.y = lockedY; transform.position = p;
            vel.y = 0f;
        }
    }

    void ApplyLinearMotion(Vector3 inputDir, float mult, float dt)
    {
        inputDir = Vector3.ClampMagnitude(inputDir, 1f);

        if (inputDir.sqrMagnitude > 0f)
        {
            // accelera verso la velocità target
            Vector3 desiredVel =
                (transform.right   * inputDir.x +
                 Vector3.up        * inputDir.y +
                 transform.forward * inputDir.z) * moveSpeed * mult;

            vel = MoveTowardsWithAccel(vel, desiredVel, acceleration, dt);
        }
        else
        {
            // rilascio lento: decadimento esponenziale (half-life)
            float k = Mathf.Exp(-Mathf.Log(2f) * dt / Mathf.Max(0.0001f, decelHalfLife));
            vel *= k;
            if (vel.magnitude < minStopSpeed) vel = Vector3.zero;
        }

        transform.position += vel * dt;
    }

    // scalare e vettoriale
    static float  MoveTowardsWithAccel(float current, float target, float accel, float dt)
    {
        float maxDelta = accel * dt;
        return Mathf.MoveTowards(current, target, maxDelta);
    }
    static Vector3 MoveTowardsWithAccel(Vector3 current, Vector3 target, float accel, float dt)
    {
        float maxDelta = accel * dt;
        Vector3 delta = target - current;
        float m = delta.magnitude;
        if (m <= maxDelta || m == 0f) return target;
        return current + delta / m * maxDelta;
    }
    static float DecayHalfLife(float value, float halfLife, float dt)
    {
        if (Mathf.Abs(value) < 0.0001f) return 0f;
        float k = Mathf.Exp(-Mathf.Log(2f) * dt / Mathf.Max(0.0001f, halfLife));
        return value * k;
    }
}

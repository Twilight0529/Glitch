using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DynamicObstacleController : MonoBehaviour
{
    private enum MotionMode
    {
        Slide,
        Pulse,
        OrbitSpiral
    }

    [SerializeField] private MotionMode mode = MotionMode.Slide;

    [Header("Slide")]
    [SerializeField] private Vector2 slideAxis = Vector2.right;
    [SerializeField] private float slideDistance = 1.2f;
    [SerializeField] private float slideSpeed = 1.0f;

    [Header("Pulse")]
    [SerializeField] private float pulseMinScale = 0.8f;
    [SerializeField] private float pulseMaxScale = 1.2f;
    [SerializeField] private float pulseSpeed = 1.25f;

    [Header("Orbit Spiral")]
    [SerializeField] private Vector2 orbitCenter = Vector2.zero;
    [SerializeField] private float orbitRadius = 4f;
    [SerializeField] private float orbitAngularSpeed = 30f;
    [SerializeField] private float orbitRadialAmplitude = 0.35f;
    [SerializeField] private float orbitRadialSpeed = 0.85f;
    [SerializeField] private bool orbitAlignToTangent = true;
    [SerializeField] private float orbitBaseAngleDeg;

    [Header("Phase")]
    [SerializeField] private float phaseOffset;

    private Rigidbody2D rb;
    private Vector2 basePosition;
    private Vector3 baseScale;

    public void ConfigureSlide(Vector2 axis, float distance, float speed, float phase)
    {
        mode = MotionMode.Slide;
        slideAxis = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector2.right;
        slideDistance = Mathf.Max(0f, distance);
        slideSpeed = Mathf.Max(0.01f, speed);
        phaseOffset = phase;
    }

    public void ConfigurePulse(float minScale, float maxScale, float speed, float phase)
    {
        mode = MotionMode.Pulse;
        pulseMinScale = Mathf.Max(0.1f, Mathf.Min(minScale, maxScale));
        pulseMaxScale = Mathf.Max(pulseMinScale, maxScale);
        pulseSpeed = Mathf.Max(0.01f, speed);
        phaseOffset = phase;
    }

    public void ConfigureOrbitSpiral(
        Vector2 center,
        float radius,
        float angularSpeedDeg,
        float radialAmplitude,
        float radialSpeed,
        float phase,
        bool alignToTangent)
    {
        mode = MotionMode.OrbitSpiral;
        orbitCenter = center;
        orbitRadius = Mathf.Max(0.1f, radius);
        orbitAngularSpeed = angularSpeedDeg;
        orbitRadialAmplitude = Mathf.Max(0f, radialAmplitude);
        orbitRadialSpeed = Mathf.Max(0.01f, radialSpeed);
        orbitAlignToTangent = alignToTangent;
        phaseOffset = phase;

        Vector2 fromCenter = (Vector2)transform.position - orbitCenter;
        if (fromCenter.sqrMagnitude > 0.0001f)
        {
            orbitBaseAngleDeg = Mathf.Atan2(fromCenter.y, fromCenter.x) * Mathf.Rad2Deg;
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        basePosition = transform.position;
        baseScale = transform.localScale;

        if (Mathf.Approximately(phaseOffset, 0f))
        {
            phaseOffset = Random.Range(0f, Mathf.PI * 2f);
        }
    }

    private void OnEnable()
    {
        basePosition = transform.position;
        baseScale = transform.localScale;

        if (mode == MotionMode.OrbitSpiral)
        {
            Vector2 fromCenter = (Vector2)transform.position - orbitCenter;
            if (fromCenter.sqrMagnitude > 0.0001f)
            {
                orbitBaseAngleDeg = Mathf.Atan2(fromCenter.y, fromCenter.x) * Mathf.Rad2Deg;
            }
        }
    }

    private void FixedUpdate()
    {
        if (mode == MotionMode.Slide)
        {
            float slideWave = Mathf.Sin(Time.time * slideSpeed + phaseOffset);
            Vector2 slideTarget = basePosition + slideAxis * (slideWave * slideDistance);

            if (rb != null && rb.bodyType == RigidbodyType2D.Kinematic)
            {
                rb.MovePosition(slideTarget);
            }
            else
            {
                transform.position = slideTarget;
            }

            return;
        }

        if (mode != MotionMode.OrbitSpiral)
        {
            return;
        }

        float t = Time.time;
        float angleDeg = orbitBaseAngleDeg + orbitAngularSpeed * t + phaseOffset * Mathf.Rad2Deg;
        float angleRad = angleDeg * Mathf.Deg2Rad;
        float orbitWave = Mathf.Sin(t * orbitRadialSpeed + phaseOffset);
        float radius = Mathf.Max(0.05f, orbitRadius + orbitWave * orbitRadialAmplitude);
        Vector2 dir = new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
        Vector2 orbitTarget = orbitCenter + dir * radius;

        if (rb != null && rb.bodyType == RigidbodyType2D.Kinematic)
        {
            rb.MovePosition(orbitTarget);
        }
        else
        {
            transform.position = orbitTarget;
        }

        if (!orbitAlignToTangent)
        {
            return;
        }

        Vector2 tangent = orbitAngularSpeed >= 0f
            ? new Vector2(-dir.y, dir.x)
            : new Vector2(dir.y, -dir.x);
        float facing = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, facing);
    }

    private void Update()
    {
        if (mode != MotionMode.Pulse)
        {
            return;
        }

        float wave01 = (Mathf.Sin(Time.time * pulseSpeed + phaseOffset) + 1f) * 0.5f;
        float scale = Mathf.Lerp(pulseMinScale, pulseMaxScale, wave01);
        transform.localScale = baseScale * scale;
    }
}

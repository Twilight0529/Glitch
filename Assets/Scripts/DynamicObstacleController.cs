using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DynamicObstacleController : MonoBehaviour
{
    private enum MotionMode
    {
        Slide,
        Pulse
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
    }

    private void FixedUpdate()
    {
        if (mode != MotionMode.Slide)
        {
            return;
        }

        float wave = Mathf.Sin(Time.time * slideSpeed + phaseOffset);
        Vector2 target = basePosition + slideAxis * (wave * slideDistance);

        if (rb != null && rb.bodyType == RigidbodyType2D.Kinematic)
        {
            rb.MovePosition(target);
        }
        else
        {
            transform.position = target;
        }
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

using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 11f;
    [SerializeField] private float maxBoostMultiplier = 1.9f;
    [SerializeField, Range(0.2f, 1f)] private float minSlowMultiplier = 0.3f;

    [Header("Shield Visual")]
    [SerializeField] private Color shieldColor = new Color(0.95f, 0.64f, 0.88f, 0.85f);
    [SerializeField] private float shieldPulseSpeed = 7.2f;
    [SerializeField] private float shieldRadius = 0.72f;

    [Header("Movement Trail")]
    [SerializeField] private bool enableMovementTrail = true;
    [SerializeField] private Color trailColor = new Color(0.42f, 0.92f, 1f, 0.85f);
    [SerializeField] private float trailParticleLifetime = 0.36f;
    [SerializeField] private float trailStartSize = 0.17f;
    [SerializeField] private float trailEmissionAtMaxSpeed = 42f;
    [SerializeField] private float trailMinVelocityToEmit = 1.1f;

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private float speedBoostTimer;
    private float speedBoostMultiplier = 1f;
    private float movementSlowTimer;
    private float movementSlowMultiplier = 1f;
    private float shieldTimer;
    private SpriteRenderer shieldRenderer;
    private GameObject shieldVisual;
    private ParticleSystem movementTrail;

    public Vector2 CurrentVelocity => rb != null ? rb.linearVelocity : Vector2.zero;
    public bool HasShield => shieldTimer > 0f;
    public bool HasSpeedBoost => speedBoostTimer > 0f;
    public bool HasMovementSlow => movementSlowTimer > 0f;
    public string ActivePowerupLabel
    {
        get
        {
            if (HasMovementSlow && HasShield && HasSpeedBoost)
            {
                return "Shield + Speed + Suppressed";
            }

            if (HasMovementSlow && HasShield)
            {
                return "Shield + Suppressed";
            }

            if (HasMovementSlow && HasSpeedBoost)
            {
                return "Speed + Suppressed";
            }

            if (HasMovementSlow)
            {
                return "Suppressed";
            }

            if (HasShield && HasSpeedBoost)
            {
                return "Shield + Speed";
            }

            if (HasShield)
            {
                return "Shield";
            }

            if (HasSpeedBoost)
            {
                return "Speed";
            }

            return "None";
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        EnsureShieldVisual();
        EnsureMovementTrail();
    }

    private void Update()
    {
        UpdatePowerupTimers();
        UpdateShieldVisual();

        Vector2 rawInput = ReadMoveInput();
        float horizontal = rawInput.x;
        float vertical = rawInput.y;

        // 4-directions priority: keeps movement cardinal and readable under pressure.
        if (Mathf.Abs(horizontal) > 0f)
        {
            vertical = 0f;
        }

        moveInput = new Vector2(horizontal, vertical).normalized;
        float effectiveSpeed = moveSpeed;
        if (speedBoostTimer > 0f)
        {
            effectiveSpeed *= speedBoostMultiplier;
        }
        if (movementSlowTimer > 0f)
        {
            effectiveSpeed *= movementSlowMultiplier;
        }

        rb.linearVelocity = moveInput * effectiveSpeed;
        UpdateMovementTrail(Mathf.Max(0.01f, effectiveSpeed));
    }

    private static Vector2 ReadMoveInput()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            float horizontal = 0f;
            float vertical = 0f;

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) horizontal -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) horizontal += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) vertical -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) vertical += 1f;

            return new Vector2(horizontal, vertical);
        }
#endif
        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
    }

    public Vector2 GetPosition()
    {
        return rb.position;
    }

    public void ApplySpeedBoost(float multiplier, float duration)
    {
        speedBoostMultiplier = Mathf.Clamp(multiplier, 1f, Mathf.Max(1f, maxBoostMultiplier));
        speedBoostTimer = Mathf.Max(speedBoostTimer, Mathf.Max(0.1f, duration));
    }

    public void ApplyShield(float duration)
    {
        shieldTimer = Mathf.Max(shieldTimer, Mathf.Max(0.1f, duration));
    }

    public void ApplyMovementSlow(float multiplier, float duration)
    {
        movementSlowMultiplier = Mathf.Min(
            movementSlowTimer > 0f ? movementSlowMultiplier : 1f,
            Mathf.Clamp(multiplier, minSlowMultiplier, 1f));
        movementSlowTimer = Mathf.Max(movementSlowTimer, Mathf.Max(0.05f, duration));
    }

    public bool TryAbsorbHit()
    {
        if (shieldTimer <= 0f)
        {
            return false;
        }

        shieldTimer = 0f;
        return true;
    }

    private void UpdatePowerupTimers()
    {
        if (speedBoostTimer > 0f)
        {
            speedBoostTimer -= Time.deltaTime;
            if (speedBoostTimer <= 0f)
            {
                speedBoostTimer = 0f;
                speedBoostMultiplier = 1f;
            }
        }

        if (shieldTimer > 0f)
        {
            shieldTimer -= Time.deltaTime;
            if (shieldTimer < 0f)
            {
                shieldTimer = 0f;
            }
        }

        if (movementSlowTimer > 0f)
        {
            movementSlowTimer -= Time.deltaTime;
            if (movementSlowTimer <= 0f)
            {
                movementSlowTimer = 0f;
                movementSlowMultiplier = 1f;
            }
        }
    }

    private void EnsureShieldVisual()
    {
        if (shieldVisual != null)
        {
            return;
        }

        shieldVisual = new GameObject("ShieldVisual");
        shieldVisual.transform.SetParent(transform, false);
        shieldVisual.transform.localPosition = Vector3.zero;
        shieldVisual.transform.localScale = Vector3.one;

        shieldRenderer = shieldVisual.AddComponent<SpriteRenderer>();
        shieldRenderer.sprite = CircleSpriteProvider.Get();
        shieldRenderer.drawMode = SpriteDrawMode.Sliced;
        shieldRenderer.size = Vector2.one * Mathf.Max(0.25f, shieldRadius * 2f);
        shieldRenderer.sortingOrder = 9;
        shieldRenderer.color = new Color(shieldColor.r, shieldColor.g, shieldColor.b, 0f);
        shieldVisual.SetActive(false);
    }

    private void UpdateShieldVisual()
    {
        if (shieldVisual == null || shieldRenderer == null)
        {
            return;
        }

        if (shieldTimer <= 0f)
        {
            if (shieldVisual.activeSelf)
            {
                shieldVisual.SetActive(false);
            }

            return;
        }

        if (!shieldVisual.activeSelf)
        {
            shieldVisual.SetActive(true);
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Max(0.1f, shieldPulseSpeed));
        Color c = shieldColor;
        c.a = Mathf.Lerp(0.24f, 0.64f, pulse);
        shieldRenderer.color = c;
        shieldVisual.transform.localScale = Vector3.one * Mathf.Lerp(0.96f, 1.05f, pulse);
    }

    private void EnsureMovementTrail()
    {
        if (movementTrail != null || !enableMovementTrail)
        {
            return;
        }

        GameObject trailGo = new GameObject("MovementTrail");
        trailGo.transform.SetParent(transform, false);
        trailGo.transform.localPosition = Vector3.zero;
        trailGo.transform.localScale = Vector3.one;

        movementTrail = trailGo.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = movementTrail.main;
        main.duration = 1f;
        main.loop = true;
        main.startLifetime = Mathf.Max(0.08f, trailParticleLifetime);
        main.startSize = Mathf.Max(0.03f, trailStartSize);
        main.startSpeed = 0.02f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = true;
        main.maxParticles = 120;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.startColor = trailColor;

        ParticleSystem.EmissionModule emission = movementTrail.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = movementTrail.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.12f;
        shape.radiusThickness = 1f;

        ParticleSystem.ColorOverLifetimeModule colorLifetime = movementTrail.colorOverLifetime;
        colorLifetime.enabled = true;
        Gradient colorGradient = new Gradient();
        colorGradient.SetKeys(
            new[]
            {
                new GradientColorKey(trailColor, 0f),
                new GradientColorKey(new Color(trailColor.r * 0.85f, trailColor.g * 0.95f, trailColor.b, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.65f, 0f),
                new GradientAlphaKey(0.25f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            });
        colorLifetime.color = new ParticleSystem.MinMaxGradient(colorGradient);

        ParticleSystem.SizeOverLifetimeModule sizeLifetime = movementTrail.sizeOverLifetime;
        sizeLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.55f, 0.7f),
            new Keyframe(1f, 0.2f));
        sizeLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ParticleSystemRenderer trailRenderer = movementTrail.GetComponent<ParticleSystemRenderer>();
        trailRenderer.material = new Material(Shader.Find("Sprites/Default"));
        trailRenderer.sortingOrder = 8;
        trailRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        trailRenderer.alignment = ParticleSystemRenderSpace.View;

        movementTrail.Play();
    }

    private void UpdateMovementTrail(float effectiveSpeed)
    {
        if (!enableMovementTrail || movementTrail == null)
        {
            return;
        }

        float velocityMagnitude = rb != null ? rb.linearVelocity.magnitude : 0f;
        if (velocityMagnitude <= trailMinVelocityToEmit)
        {
            ParticleSystem.EmissionModule idleEmission = movementTrail.emission;
            idleEmission.rateOverTime = 0f;
            return;
        }

        float normalized = Mathf.Clamp01(velocityMagnitude / Mathf.Max(0.01f, effectiveSpeed));
        float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 18f);
        float rate = Mathf.Lerp(8f, Mathf.Max(10f, trailEmissionAtMaxSpeed), normalized) * pulse;

        ParticleSystem.EmissionModule emission = movementTrail.emission;
        emission.rateOverTime = rate;
    }
}

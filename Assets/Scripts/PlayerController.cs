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

    [Header("Shield Visual")]
    [SerializeField] private Color shieldColor = new Color(0.95f, 0.64f, 0.88f, 0.85f);
    [SerializeField] private float shieldPulseSpeed = 7.2f;
    [SerializeField] private float shieldRadius = 0.72f;

    private Rigidbody2D rb;
    private Vector2 moveInput;
    private float speedBoostTimer;
    private float speedBoostMultiplier = 1f;
    private float shieldTimer;
    private SpriteRenderer shieldRenderer;
    private GameObject shieldVisual;

    public Vector2 CurrentVelocity => rb != null ? rb.linearVelocity : Vector2.zero;
    public bool HasShield => shieldTimer > 0f;
    public bool HasSpeedBoost => speedBoostTimer > 0f;
    public string ActivePowerupLabel
    {
        get
        {
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

        rb.linearVelocity = moveInput * effectiveSpeed;
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
}

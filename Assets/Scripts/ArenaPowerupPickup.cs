using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class ArenaPowerupPickup : MonoBehaviour
{
    public enum PickupKind
    {
        SpeedBurst,
        Shield
    }

    [SerializeField] private float bobAmplitude = 0.14f;
    [SerializeField] private float bobSpeed = 3.2f;
    [SerializeField] private float spinSpeed = 75f;
    [SerializeField] private float auraPulseSpeed = 4.8f;
    [SerializeField] private float auraScale = 1.95f;

    private ArenaChaosDirector owner;
    private PickupKind kind;
    private float lifeTimer;
    private float lifetime;
    private float speedMultiplier;
    private float speedDuration;
    private float shieldDuration;
    private Vector3 basePosition;
    private SpriteRenderer spriteRenderer;
    private SpriteRenderer auraRenderer;
    private GameObject auraObject;

    public PickupKind Kind => kind;

    public void Configure(
        ArenaChaosDirector ownerController,
        PickupKind pickupKind,
        float pickupLifetime,
        float boostMultiplier,
        float boostDuration,
        float shieldSeconds)
    {
        owner = ownerController;
        kind = pickupKind;
        lifetime = Mathf.Max(0.5f, pickupLifetime);
        speedMultiplier = Mathf.Max(1f, boostMultiplier);
        speedDuration = Mathf.Max(0.1f, boostDuration);
        shieldDuration = Mathf.Max(0.1f, shieldSeconds);
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        basePosition = transform.position;
        EnsureAuraVisual();
    }

    private void Start()
    {
        ApplyVisualTheme();
    }

    private void Update()
    {
        lifeTimer += Time.deltaTime;
        if (lifeTimer >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
        transform.position = new Vector3(basePosition.x, basePosition.y + bob, basePosition.z);
        transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime, Space.Self);
        UpdateAuraVisual();
    }

    private void ApplyVisualTheme()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        switch (kind)
        {
            case PickupKind.Shield:
                spriteRenderer.color = new Color(1f, 0.66f, 0.86f, 0.95f);
                break;
            default:
                spriteRenderer.color = new Color(0.46f, 0.96f, 1f, 0.95f);
                break;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null)
        {
            return;
        }

        if (kind == PickupKind.Shield)
        {
            player.ApplyShield(shieldDuration);
        }
        else
        {
            player.ApplySpeedBoost(speedMultiplier, speedDuration);
        }

        owner?.NotifyPickupConsumed(this);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        owner?.NotifyPickupDestroyed(this);
    }

    private void EnsureAuraVisual()
    {
        auraObject = new GameObject("PowerupAura");
        auraObject.transform.SetParent(transform, false);
        auraObject.transform.localPosition = Vector3.zero;
        auraObject.transform.localScale = Vector3.one * Mathf.Max(1.1f, auraScale);
        auraRenderer = auraObject.AddComponent<SpriteRenderer>();
        auraRenderer.sprite = CircleSpriteProvider.Get();
        auraRenderer.sortingOrder = 11;
        auraRenderer.color = new Color(1f, 1f, 1f, 0.18f);
    }

    private void UpdateAuraVisual()
    {
        if (auraObject == null || auraRenderer == null)
        {
            return;
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * auraPulseSpeed);
        Color tint = kind == PickupKind.Shield
            ? new Color(1f, 0.68f, 0.88f, 1f)
            : new Color(0.52f, 0.95f, 1f, 1f);

        float lifeN = 1f - Mathf.Clamp01(lifeTimer / Mathf.Max(0.01f, lifetime));
        float warn = lifeN < 0.25f ? 0.45f + 0.55f * Mathf.Sin(Time.time * 18f) : 1f;
        auraRenderer.color = new Color(tint.r, tint.g, tint.b, (0.10f + pulse * 0.24f) * warn);
        auraObject.transform.localScale = Vector3.one * auraScale * Mathf.Lerp(0.9f, 1.12f, pulse);
    }
}

using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class ArenaPowerupPickup : MonoBehaviour
{
    // Objeto recolectable temporal: aplica velocidad o escudo al jugador y notifica al director de arena.
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
    [SerializeField] private int collectBurstRayCount = 10;
    [SerializeField] private float collectBurstRadius = 1.45f;
    [SerializeField] private float collectBurstDuration = 0.34f;

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
                spriteRenderer.sprite = ShieldSpriteProvider.Get();
                spriteRenderer.drawMode = SpriteDrawMode.Simple;
                spriteRenderer.size = Vector2.one * 0.62f;
                spriteRenderer.color = new Color(1f, 0.66f, 0.86f, 0.95f);
                break;
            default:
                spriteRenderer.sprite = LightningSpriteProvider.Get();
                spriteRenderer.drawMode = SpriteDrawMode.Simple;
                spriteRenderer.size = Vector2.one * 0.62f;
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

        player.AddFirewallChargeFromPowerup();
        GlitchAudioManager.PlayPowerupCollected(kind, transform.position);
        SpawnCollectBurstFx();
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

    private void SpawnCollectBurstFx()
    {
        Color burstColor = kind == PickupKind.Shield
            ? new Color(1f, 0.70f, 0.90f, 1f)
            : new Color(0.54f, 0.96f, 1f, 1f);

        GameObject ring = new GameObject("PowerupCollectRingFx");
        ring.transform.position = transform.position;
        SpriteRenderer ringRenderer = ring.AddComponent<SpriteRenderer>();
        ringRenderer.sprite = CircleSpriteProvider.Get();
        ringRenderer.sortingOrder = 14;
        ringRenderer.color = new Color(burstColor.r, burstColor.g, burstColor.b, 0.88f);
        ring.transform.localScale = Vector3.one * 0.28f;
        ring.AddComponent<PowerupCollectBurstFx>().Configure(ringRenderer, Mathf.Max(0.18f, collectBurstDuration), Mathf.Max(0.5f, collectBurstRadius), burstColor);
        Destroy(ring, Mathf.Max(0.2f, collectBurstDuration + 0.08f));

        int rays = Mathf.Max(4, collectBurstRayCount);
        for (int i = 0; i < rays; i++)
        {
            float angle = ((Mathf.PI * 2f) * i) / rays + Random.Range(-0.1f, 0.1f);
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            GameObject ray = new GameObject($"PowerupCollectRay_{i}");
            ray.transform.position = transform.position;
            SpriteRenderer sr = ray.AddComponent<SpriteRenderer>();
            sr.sprite = SquareSpriteProvider.Get();
            sr.sortingOrder = 14;
            sr.color = burstColor;
            ray.transform.localScale = new Vector3(0.22f, 0.08f, 1f);
            ray.AddComponent<PowerupCollectRayFx>().Configure(sr, dir, Mathf.Max(0.45f, collectBurstRadius), Mathf.Max(0.16f, collectBurstDuration));
            Destroy(ray, Mathf.Max(0.24f, collectBurstDuration + 0.12f));
        }
    }
}

public class PowerupCollectBurstFx : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private float duration = 0.3f;
    private float radius = 1f;
    private Color baseColor = Color.white;
    private float age;

    public void Configure(SpriteRenderer rendererRef, float life, float maxRadius, Color tint)
    {
        spriteRenderer = rendererRef;
        duration = Mathf.Max(0.08f, life);
        radius = Mathf.Max(0.2f, maxRadius);
        baseColor = tint;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / duration);
        transform.localScale = Vector3.one * Mathf.Lerp(0.26f, radius, t);
        if (spriteRenderer != null)
        {
            spriteRenderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Lerp(0.85f, 0f, t));
        }
    }
}

public class PowerupCollectRayFx : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Vector2 direction = Vector2.right;
    private float distance = 1f;
    private float duration = 0.25f;
    private float age;
    private Vector3 origin;

    public void Configure(SpriteRenderer rendererRef, Vector2 travelDir, float travelDistance, float life)
    {
        spriteRenderer = rendererRef;
        direction = travelDir.sqrMagnitude > 0.001f ? travelDir.normalized : Vector2.right;
        distance = Mathf.Max(0.15f, travelDistance);
        duration = Mathf.Max(0.08f, life);
        origin = transform.position;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / duration);
        float eased = 1f - Mathf.Pow(1f - t, 2f);
        transform.position = origin + (Vector3)(direction * distance * eased);
        transform.localScale = new Vector3(Mathf.Lerp(0.22f, 0.06f, t), Mathf.Lerp(0.08f, 0.03f, t), 1f);
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = Mathf.Lerp(0.95f, 0f, t);
            spriteRenderer.color = c;
        }
    }
}

using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class ArenaPowerupPickup : MonoBehaviour
{
    // Objeto recolectable temporal: aplica velocidad o escudo al jugador y notifica al director de arena.
    public enum PickupKind
    {
        SpeedBurst,
        Shield,
        Compact,
        PhaseDash
    }

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
    private float compactDuration;
    private float compactScaleMultiplier;
    private float compactMoveMultiplier;
    private float phaseDashDuration;
    private SpriteRenderer spriteRenderer;
    private SpriteRenderer auraRenderer;
    private GameObject auraObject;
    private WorldObjectAnimationFx animationFx;

    public PickupKind Kind => kind;

    public void ApplyExternalDisplacement(Vector2 delta)
    {
        if (delta.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        transform.position += (Vector3)delta;
    }

    public void Configure(
        ArenaChaosDirector ownerController,
        PickupKind pickupKind,
        float pickupLifetime,
        float boostMultiplier,
        float boostDuration,
        float shieldSeconds,
        float compactSeconds,
        float compactScale,
        float compactMove,
        float phaseDashSeconds)
    {
        owner = ownerController;
        kind = pickupKind;
        lifetime = Mathf.Max(0.5f, pickupLifetime);
        speedMultiplier = Mathf.Max(1f, boostMultiplier);
        speedDuration = Mathf.Max(0.1f, boostDuration);
        shieldDuration = Mathf.Max(0.1f, shieldSeconds);
        compactDuration = Mathf.Max(0.1f, compactSeconds);
        compactScaleMultiplier = Mathf.Clamp(compactScale, 0.35f, 0.88f);
        compactMoveMultiplier = Mathf.Clamp(compactMove, 0.25f, 1f);
        phaseDashDuration = Mathf.Max(0.5f, phaseDashSeconds);
        ApplyVisualTheme();
        animationFx?.SetLifetimeState(lifeTimer, lifetime);
    }

    private void Awake()
    {
        SpriteRenderer sourceRenderer = GetComponent<SpriteRenderer>();
        animationFx = GetComponent<WorldObjectAnimationFx>();
        if (animationFx == null)
        {
            animationFx = gameObject.AddComponent<WorldObjectAnimationFx>();
        }

        spriteRenderer = animationFx.Initialize(
            sourceRenderer,
            WorldObjectAnimationFx.VisualKind.PowerupSpeed,
            GlitchUiPalette.Information);
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

        animationFx?.SetLifetimeState(lifeTimer, lifetime);
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
                spriteRenderer.color = GlitchUiPalette.WithAlpha(GlitchUiPalette.Special, 0.95f);
                break;
            case PickupKind.Compact:
                spriteRenderer.sprite = CompactSpriteProvider.Get();
                spriteRenderer.drawMode = SpriteDrawMode.Simple;
                spriteRenderer.size = Vector2.one * 0.68f;
                spriteRenderer.color = GlitchUiPalette.WithAlpha(GlitchUiPalette.Success, 0.96f);
                break;
            case PickupKind.PhaseDash:
                spriteRenderer.sprite = PhaseDashSpriteProvider.Get();
                spriteRenderer.drawMode = SpriteDrawMode.Simple;
                spriteRenderer.size = Vector2.one * 0.68f;
                spriteRenderer.color = GlitchUiPalette.WithAlpha(GlitchUiPalette.Information, 0.96f);
                break;
            default:
                spriteRenderer.sprite = LightningSpriteProvider.Get();
                spriteRenderer.drawMode = SpriteDrawMode.Simple;
                spriteRenderer.size = Vector2.one * 0.62f;
                spriteRenderer.color = GlitchUiPalette.WithAlpha(GlitchUiPalette.Information, 0.95f);
                break;
        }

        animationFx?.SetStyle(GetVisualKind(kind), GetThemeColor());
        animationFx?.SetContentScale(1f);
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
        else if (kind == PickupKind.Compact)
        {
            player.ApplyCompactMode(compactDuration, compactScaleMultiplier, compactMoveMultiplier);
        }
        else if (kind == PickupKind.PhaseDash)
        {
            player.ApplyPhaseDashPowerup(phaseDashDuration);
        }
        else
        {
            player.ApplySpeedBoost(speedMultiplier, speedDuration);
        }

        player.AddFirewallChargeFromPowerup();
        FindAnyObjectByType<GameManager>()?.NotifyPowerupCollected();
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
        if (auraObject != null)
        {
            return;
        }

        auraObject = new GameObject("PowerupAura");
        auraObject.transform.SetParent(animationFx != null ? animationFx.VisualRoot : transform, false);
        auraObject.transform.localPosition = Vector3.zero;
        auraObject.transform.localScale = Vector3.one * Mathf.Max(1.1f, auraScale);
        auraRenderer = auraObject.AddComponent<SpriteRenderer>();
        auraRenderer.sprite = ContainmentRingSpriteProvider.Get();
        auraRenderer.sortingOrder = 11;
        auraRenderer.color = new Color(1f, 1f, 1f, 0.18f);
    }

    private void UpdateAuraVisual()
    {
        if (auraObject == null || auraRenderer == null)
        {
            return;
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(lifeTimer * auraPulseSpeed);
        Color tint = GetThemeColor();

        float lifeN = 1f - Mathf.Clamp01(lifeTimer / Mathf.Max(0.01f, lifetime));
        float warn = lifeN < 0.25f ? 0.45f + 0.55f * Mathf.Sin(lifeTimer * 18f) : 1f;
        auraRenderer.color = new Color(tint.r, tint.g, tint.b, (0.10f + pulse * 0.24f) * warn);
        auraObject.transform.localScale = Vector3.one * auraScale * Mathf.Lerp(0.9f, 1.12f, pulse);
    }

    private void SpawnCollectBurstFx()
    {
        Color burstColor = GetThemeColor();

        GameObject ring = new GameObject("PowerupCollectRingFx");
        ring.transform.position = transform.position;
        SpriteRenderer ringRenderer = ring.AddComponent<SpriteRenderer>();
        ringRenderer.sprite = ContainmentRingSpriteProvider.Get();
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

    private Color GetThemeColor()
    {
        switch (kind)
        {
            case PickupKind.Shield:
                return GlitchUiPalette.Special;
            case PickupKind.Compact:
                return GlitchUiPalette.Success;
            case PickupKind.PhaseDash:
                return GlitchUiPalette.Information;
            default:
                return GlitchUiPalette.Information;
        }
    }

    private static WorldObjectAnimationFx.VisualKind GetVisualKind(PickupKind pickupKind)
    {
        switch (pickupKind)
        {
            case PickupKind.Shield:
                return WorldObjectAnimationFx.VisualKind.PowerupShield;
            case PickupKind.Compact:
                return WorldObjectAnimationFx.VisualKind.PowerupCompact;
            case PickupKind.PhaseDash:
                return WorldObjectAnimationFx.VisualKind.PowerupPhaseDash;
            default:
                return WorldObjectAnimationFx.VisualKind.PowerupSpeed;
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

using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class ArenaScorePickup : MonoBehaviour
{
    // Objeto recolectable de puntaje: aparece por tiempo limitado y suma puntos al tocarlo.
    [SerializeField] private bool enableAura = false;
    [SerializeField] private float bobAmplitude = 0.08f;
    [SerializeField] private float bobSpeed = 4.5f;
    [SerializeField] private float pulseSpeed = 5.4f;
    [SerializeField] private float baseScale = 1f;
    [SerializeField] private float auraScale = 1.25f;
    [SerializeField] private float auraPulseSpeed = 3.4f;
    [SerializeField] private int collectSparkCount = 6;
    [SerializeField] private float collectSparkDistance = 0.95f;
    [SerializeField] private float collectSparkLifetime = 0.24f;

    private ArenaChaosDirector owner;
    private GameManager gameManager;
    private float lifeTimer;
    private float lifetime;
    private int scoreValue;
    private bool chargedDataCore;
    private float extraFirewallCharge;
    private Vector3 basePosition;
    private SpriteRenderer spriteRenderer;
    private SpriteRenderer auraRenderer;
    private GameObject auraObject;

    public int ScoreValue => scoreValue;

    public void Configure(ArenaChaosDirector ownerController, GameManager manager, int points, float lifeSeconds, bool dataCore = false, float firewallChargeBonus = 0f)
    {
        owner = ownerController;
        gameManager = manager;
        scoreValue = Mathf.Max(1, points);
        lifetime = Mathf.Max(0.5f, lifeSeconds);
        chargedDataCore = dataCore;
        extraFirewallCharge = Mathf.Max(0f, firewallChargeBonus);
        if (chargedDataCore)
        {
            enableAura = true;
            baseScale = Mathf.Max(baseScale, 1.24f);
            EnsureAura();
        }

        ApplyVisual();
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        basePosition = transform.position;
        if (enableAura || chargedDataCore)
        {
            EnsureAura();
        }
        ApplyVisual();
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

        float pulse = 0.9f + 0.12f * (0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed));
        transform.localScale = Vector3.one * (baseScale * pulse);
        UpdateAura(pulse);
    }

    private void ApplyVisual()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        if (chargedDataCore)
        {
            spriteRenderer.color = new Color(0.58f, 1f, 0.86f, 0.98f);
            transform.localScale = Vector3.one * 1.2f;
            return;
        }

        spriteRenderer.color = new Color(0.97f, 0.97f, 0.98f, 0.96f);
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

        if (gameManager != null)
        {
            gameManager.AddScore(scoreValue);
            gameManager.NotifyScorePickupCollected(scoreValue);
        }

        player.AddFirewallChargeFromScore(scoreValue);
        player.AddFirewallCharge(extraFirewallCharge);
        GlitchAudioManager.PlayScorePickup(transform.position);
        SpawnCollectFlash();
        owner?.NotifyScorePickupConsumed(this);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        owner?.NotifyScorePickupDestroyed(this);
    }

    private void EnsureAura()
    {
        if (auraObject != null)
        {
            return;
        }

        auraObject = new GameObject(chargedDataCore ? "DataCoreAura" : "ScoreAura");
        auraObject.transform.SetParent(transform, false);
        auraObject.transform.localPosition = Vector3.zero;
        auraObject.transform.localScale = Vector3.one * Mathf.Max(1f, auraScale);
        auraRenderer = auraObject.AddComponent<SpriteRenderer>();
        auraRenderer.sprite = CircleSpriteProvider.Get();
        auraRenderer.sortingOrder = 11;
        auraRenderer.color = chargedDataCore ? new Color(0.58f, 1f, 0.86f, 0.28f) : new Color(0.82f, 0.95f, 1f, 0.22f);
    }

    private void UpdateAura(float pulse)
    {
        if ((!enableAura && !chargedDataCore) || auraRenderer == null || auraObject == null)
        {
            return;
        }

        float auraPulse = 0.65f + 0.35f * (0.5f + 0.5f * Mathf.Sin(Time.time * auraPulseSpeed));
        Color auraColor = chargedDataCore ? new Color(0.58f, 1f, 0.86f, 1f) : new Color(0.82f, 0.95f, 1f, 1f);
        auraRenderer.color = new Color(auraColor.r, auraColor.g, auraColor.b, 0.06f + auraPulse * (chargedDataCore ? 0.22f : 0.12f));
        auraObject.transform.localScale = Vector3.one * auraScale * (chargedDataCore ? 1.22f : 1f) * Mathf.Lerp(0.88f, 1.12f, auraPulse * pulse);
    }

    private void SpawnCollectFlash()
    {
        GameObject fx = new GameObject("ScoreCollectFx");
        fx.transform.position = transform.position;
        SpriteRenderer fxRenderer = fx.AddComponent<SpriteRenderer>();
        fxRenderer.sprite = CircleSpriteProvider.Get();
        fxRenderer.sortingOrder = 13;
        fxRenderer.color = new Color(1f, 1f, 1f, 0.9f);
        fx.transform.localScale = Vector3.one * 0.34f;
        Destroy(fx, 0.28f);
        fx.AddComponent<ScoreCollectFlash>().Configure(fxRenderer);

        int sparks = Mathf.Max(3, collectSparkCount);
        for (int i = 0; i < sparks; i++)
        {
            float angle = ((Mathf.PI * 2f) * i) / sparks + Random.Range(-0.16f, 0.16f);
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            GameObject spark = new GameObject($"ScoreCollectSpark_{i}");
            spark.transform.position = transform.position;
            SpriteRenderer sr = spark.AddComponent<SpriteRenderer>();
            sr.sprite = SquareSpriteProvider.Get();
            sr.color = new Color(0.92f, 0.98f, 1f, 1f);
            sr.sortingOrder = 13;
            spark.transform.localScale = new Vector3(0.12f, 0.05f, 1f);
            spark.AddComponent<ScoreCollectSparkFx>().Configure(sr, dir, Mathf.Max(0.15f, collectSparkDistance), Mathf.Max(0.08f, collectSparkLifetime));
            Destroy(spark, Mathf.Max(0.12f, collectSparkLifetime + 0.08f));
        }
    }
}

public class ScoreCollectFlash : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private float age;

    public void Configure(SpriteRenderer rendererRef)
    {
        spriteRenderer = rendererRef;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / 0.28f);
        transform.localScale = Vector3.one * Mathf.Lerp(0.35f, 1.35f, t);
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, 35f, t));
        if (spriteRenderer != null)
        {
            spriteRenderer.color = new Color(1f, 1f, 1f, 1f - t);
        }
    }
}

public class ScoreCollectSparkFx : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Vector2 direction = Vector2.right;
    private float distance = 1f;
    private float life = 0.22f;
    private float age;
    private Vector3 origin;

    public void Configure(SpriteRenderer rendererRef, Vector2 travelDir, float travelDistance, float lifetime)
    {
        spriteRenderer = rendererRef;
        direction = travelDir.sqrMagnitude > 0.001f ? travelDir.normalized : Vector2.right;
        distance = Mathf.Max(0.1f, travelDistance);
        life = Mathf.Max(0.08f, lifetime);
        origin = transform.position;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / life);
        float eased = 1f - Mathf.Pow(1f - t, 2f);
        transform.position = origin + (Vector3)(direction * distance * eased);
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        transform.localScale = new Vector3(Mathf.Lerp(0.12f, 0.02f, t), Mathf.Lerp(0.05f, 0.02f, t), 1f);
        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color; 
            c.a = Mathf.Lerp(1f, 0f, t);
            spriteRenderer.color = c;
        }
    }
 
}  

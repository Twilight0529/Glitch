using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class ArenaScorePickup : MonoBehaviour
{
    [SerializeField] private bool enableAura = false;
    [SerializeField] private float bobAmplitude = 0.08f;
    [SerializeField] private float bobSpeed = 4.5f;
    [SerializeField] private float pulseSpeed = 5.4f;
    [SerializeField] private float baseScale = 1f;
    [SerializeField] private float auraScale = 1.25f;
    [SerializeField] private float auraPulseSpeed = 3.4f;

    private ArenaChaosDirector owner;
    private GameManager gameManager;
    private float lifeTimer;
    private float lifetime;
    private int scoreValue;
    private Vector3 basePosition;
    private SpriteRenderer spriteRenderer;
    private SpriteRenderer auraRenderer;
    private GameObject auraObject;

    public int ScoreValue => scoreValue;

    public void Configure(ArenaChaosDirector ownerController, GameManager manager, int points, float lifeSeconds)
    {
        owner = ownerController;
        gameManager = manager;
        scoreValue = Mathf.Max(1, points);
        lifetime = Mathf.Max(0.5f, lifeSeconds);
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        basePosition = transform.position;
        if (enableAura)
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
        }

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
        auraObject = new GameObject("ScoreAura");
        auraObject.transform.SetParent(transform, false);
        auraObject.transform.localPosition = Vector3.zero;
        auraObject.transform.localScale = Vector3.one * Mathf.Max(1f, auraScale);
        auraRenderer = auraObject.AddComponent<SpriteRenderer>();
        auraRenderer.sprite = CircleSpriteProvider.Get();
        auraRenderer.sortingOrder = 11;
        auraRenderer.color = new Color(0.82f, 0.95f, 1f, 0.22f);
    }

    private void UpdateAura(float pulse)
    {
        if (!enableAura || auraRenderer == null || auraObject == null)
        {
            return;
        }

        float auraPulse = 0.65f + 0.35f * (0.5f + 0.5f * Mathf.Sin(Time.time * auraPulseSpeed));
        auraRenderer.color = new Color(0.82f, 0.95f, 1f, 0.06f + auraPulse * 0.12f);
        auraObject.transform.localScale = Vector3.one * auraScale * Mathf.Lerp(0.88f, 1.12f, auraPulse * pulse);
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
        if (spriteRenderer != null)
        {
            spriteRenderer.color = new Color(1f, 1f, 1f, 1f - t);
        }
    }
}

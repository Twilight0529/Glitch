using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class ArenaBreachGate : MonoBehaviour
{
    // Portal de transicion: al tocarlo, el director reconfigura la arena hacia otro sector.
    [SerializeField] private float pulseSpeed = 9f;
    [SerializeField] private int stripCount = 5;

    private ArenaChaosDirector owner;
    private SpriteRenderer spriteRenderer;
    private readonly SpriteRenderer[] stripRenderers = new SpriteRenderer[6];
    private Color breachColor = Color.magenta;
    private Vector2 gateSize = new Vector2(2.5f, 1f);
    private float lifetime = 12f;
    private float age;
    private bool consumed;

    public void Configure(ArenaChaosDirector director, float lifeSeconds, Color tint, Vector2 size)
    {
        owner = director;
        lifetime = Mathf.Max(1f, lifeSeconds);
        breachColor = tint;
        gateSize = new Vector2(Mathf.Max(0.5f, size.x), Mathf.Max(0.3f, size.y));
        ApplyRendererSize();
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        EnsureStrips();
    }

    private void Update()
    {
        if (consumed)
        {
            return;
        }

        age += Time.deltaTime;
        float lifeN = 1f - Mathf.Clamp01(age / Mathf.Max(0.01f, lifetime));
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed);
        float warn = lifeN < 0.25f ? 0.45f + 0.55f * Mathf.Sin(Time.time * 18f) : 1f;

        if (spriteRenderer != null)
        {
            spriteRenderer.color = new Color(breachColor.r, breachColor.g, breachColor.b, Mathf.Lerp(0.42f, 0.86f, pulse) * warn);
        }

        transform.localScale = Vector3.one * Mathf.Lerp(0.96f, 1.08f, pulse);
        UpdateStrips(pulse, warn);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed || other == null || other.GetComponent<PlayerController>() == null)
        {
            return;
        }

        consumed = true;
        SpawnEnterFx();
        owner?.NotifyBreachEntered(this);
    }

    private void ApplyRendererSize()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        spriteRenderer.size = gateSize;
    }

    private void EnsureStrips()
    {
        int count = Mathf.Min(stripRenderers.Length, Mathf.Max(2, stripCount));
        for (int i = 0; i < count; i++)
        {
            GameObject strip = new GameObject($"BreachStrip_{i}");
            strip.transform.SetParent(transform, false);
            SpriteRenderer sr = strip.AddComponent<SpriteRenderer>();
            sr.sprite = SquareSpriteProvider.Get();
            sr.sortingOrder = 17;
            stripRenderers[i] = sr;
        }
    }

    private void UpdateStrips(float pulse, float warn)
    {
        for (int i = 0; i < stripRenderers.Length; i++)
        {
            SpriteRenderer sr = stripRenderers[i];
            if (sr == null)
            {
                continue;
            }

            float t = (i + 1f) / (stripRenderers.Length + 1f);
            float drift = Mathf.Sin(Time.time * (4.5f + i) + i * 1.7f) * 0.08f;
            sr.transform.localPosition = new Vector3(Mathf.Lerp(-gateSize.x * 0.42f, gateSize.x * 0.42f, t), drift, -0.01f);
            sr.transform.localScale = new Vector3(0.08f, gateSize.y * Mathf.Lerp(0.55f, 1.05f, pulse), 1f);
            sr.color = new Color(1f, 0.84f, 0.98f, Mathf.Lerp(0.18f, 0.62f, pulse) * warn);
        }
    }

    private void SpawnEnterFx()
    {
        GameObject flash = new GameObject("BreachEnterFx");
        flash.transform.position = transform.position;
        flash.transform.rotation = transform.rotation;
        SpriteRenderer sr = flash.AddComponent<SpriteRenderer>();
        sr.sprite = CircleSpriteProvider.Get();
        sr.color = new Color(breachColor.r, breachColor.g, breachColor.b, 0.88f);
        sr.sortingOrder = 19;
        flash.transform.localScale = Vector3.one * 0.35f;
        flash.AddComponent<BreachEnterBurstFx>().Configure(sr, 2.4f, 0.34f, breachColor);
        Destroy(flash, 0.46f);
    }
}

public class BreachEnterBurstFx : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private float radius = 1f;
    private float lifetime = 0.3f;
    private Color tint = Color.white;
    private float age;

    public void Configure(SpriteRenderer rendererRef, float maxRadius, float lifeSeconds, Color color)
    {
        spriteRenderer = rendererRef;
        radius = Mathf.Max(0.2f, maxRadius);
        lifetime = Mathf.Max(0.08f, lifeSeconds);
        tint = color;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / lifetime);
        transform.localScale = Vector3.one * Mathf.Lerp(0.35f, radius, t);
        if (spriteRenderer != null)
        {
            spriteRenderer.color = new Color(tint.r, tint.g, tint.b, Mathf.Lerp(0.86f, 0f, t));
        }
    }
}

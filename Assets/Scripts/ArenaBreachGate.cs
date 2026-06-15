using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class ArenaBreachGate : MonoBehaviour
{
    // Portal de transicion: al tocarlo, el director reconfigura la arena hacia otro sector.
    [SerializeField] private float pulseSpeed = 9f;
    [SerializeField] private float framePulseSpeed = 6.2f;
    [SerializeField] private int stripCount = 5;

    private ArenaChaosDirector owner;
    private SpriteRenderer spriteRenderer;
    private readonly SpriteRenderer[] stripRenderers = new SpriteRenderer[6];
    private readonly SpriteRenderer[] chevronRenderers = new SpriteRenderer[4];
    private readonly SpriteRenderer[] frameEdgeRenderers = new SpriteRenderer[4];
    private SpriteRenderer backplateRenderer;
    private SpriteRenderer frameRenderer;
    private SpriteRenderer coreRenderer;
    private SpriteRenderer scanRenderer;
    private Color breachColor = Color.magenta;
    private Vector2 gateSize = new Vector2(2.5f, 1f);
    private float lifetime = 12f;
    private float age;
    private bool consumed;
    private bool enemyAbsorbed;

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
        EnsureReadabilityVisuals();
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
            spriteRenderer.color = new Color(breachColor.r, breachColor.g, breachColor.b, Mathf.Lerp(0.30f, 0.62f, pulse) * warn);
        }

        transform.localScale = Vector3.one * Mathf.Lerp(0.96f, 1.08f, pulse);
        UpdateReadabilityVisuals(pulse, warn, lifeN);
        UpdateStrips(pulse, warn);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        SplitAnomalyCloneController clone = other != null ? other.GetComponent<SplitAnomalyCloneController>() : null;
        if (!consumed && clone != null)
        {
            clone.AbsorbIntoBreach(transform.position);
            SpawnEnterFx();
            GlitchAudioManager.PlayBreachEnter(transform.position);
            return;
        }

        if (consumed || other == null || other.GetComponent<PlayerController>() == null)
        {
            EnemyController enemy = other != null ? other.GetComponent<EnemyController>() : null;
            if (!enemyAbsorbed && enemy != null)
            {
                enemyAbsorbed = true;
                enemy.AbsorbIntoBreach(transform.position);
                SpawnEnterFx();
                GlitchAudioManager.PlayBreachEnter(transform.position);
            }

            return;
        }

        consumed = true;
        SpawnEnterFx();
        GlitchAudioManager.PlayBreachEnter(transform.position);
        owner?.NotifyBreachEntered(this);
    }

    private void ApplyRendererSize()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        spriteRenderer.size = gateSize;
        if (backplateRenderer != null)
        {
            backplateRenderer.size = gateSize + new Vector2(0.62f, 0.42f);
        }
        if (frameRenderer != null)
        {
            frameRenderer.size = gateSize + new Vector2(0.34f, 0.24f);
        }
        if (coreRenderer != null)
        {
            coreRenderer.size = gateSize * new Vector2(0.30f, 0.92f);
        }
        if (scanRenderer != null)
        {
            scanRenderer.size = new Vector2(gateSize.x + 0.48f, 0.075f);
        }
    }

    private void EnsureReadabilityVisuals()
    {
        if (backplateRenderer == null)
        {
            backplateRenderer = CreateChildRenderer("BreachBackplate", 15);
        }
        if (frameRenderer == null)
        {
            frameRenderer = CreateChildRenderer("BreachReadableFrame", 18);
        }
        if (coreRenderer == null)
        {
            coreRenderer = CreateChildRenderer("BreachExitCore", 19);
        }
        if (scanRenderer == null)
        {
            scanRenderer = CreateChildRenderer("BreachScanLine", 20);
        }

        for (int i = 0; i < chevronRenderers.Length; i++)
        {
            if (chevronRenderers[i] == null)
            {
                chevronRenderers[i] = CreateChildRenderer($"BreachExitChevron_{i}", 21);
            }
        }
        for (int i = 0; i < frameEdgeRenderers.Length; i++)
        {
            if (frameEdgeRenderers[i] == null)
            {
                frameEdgeRenderers[i] = CreateChildRenderer($"BreachFrameEdge_{i}", 23);
            }
        }

        ApplyRendererSize();
    }

    private SpriteRenderer CreateChildRenderer(string childName, int sortingOrder)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(transform, false);
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = SquareSpriteProvider.Get();
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.sortingOrder = sortingOrder;
        renderer.color = Color.clear;
        return renderer;
    }

    private void UpdateReadabilityVisuals(float pulse, float warn, float lifeN)
    {
        EnsureReadabilityVisuals();

        float framePulse = 0.5f + 0.5f * Mathf.Sin(Time.time * framePulseSpeed);
        Color hot = Color.Lerp(breachColor, Color.white, 0.32f + framePulse * 0.22f);

        backplateRenderer.transform.localPosition = new Vector3(0f, 0f, 0.03f);
        backplateRenderer.color = new Color(0.015f, 0.004f, 0.025f, Mathf.Lerp(0.62f, 0.82f, pulse) * warn);

        frameRenderer.transform.localPosition = Vector3.zero;
        frameRenderer.color = new Color(hot.r, hot.g, hot.b, Mathf.Lerp(0.07f, 0.16f, framePulse) * warn);
        UpdateFrameEdges(hot, framePulse, warn);

        coreRenderer.transform.localPosition = Vector3.zero;
        coreRenderer.color = new Color(1f, 0.94f, 1f, Mathf.Lerp(0.32f, 0.78f, pulse) * warn);
        coreRenderer.transform.localScale = new Vector3(Mathf.Lerp(0.70f, 1.05f, framePulse), 1f, 1f);

        float scanY = Mathf.Lerp(-gateSize.y * 0.42f, gateSize.y * 0.42f, Mathf.PingPong(Time.time * 0.82f, 1f));
        scanRenderer.transform.localPosition = new Vector3(0f, scanY, -0.02f);
        scanRenderer.color = new Color(1f, 0.88f, 0.98f, Mathf.Lerp(0.30f, 0.74f, framePulse) * warn);

        float directionSign = Mathf.Abs(transform.up.x) > Mathf.Abs(transform.up.y) ? Mathf.Sign(transform.up.x) : Mathf.Sign(transform.up.y);
        for (int i = 0; i < chevronRenderers.Length; i++)
        {
            SpriteRenderer chevron = chevronRenderers[i];
            if (chevron == null)
            {
                continue;
            }

            float t = (i + 1f) / (chevronRenderers.Length + 1f);
            float flow = Mathf.Repeat(t + Time.time * 0.74f, 1f);
            float x = Mathf.Lerp(-gateSize.x * 0.36f, gateSize.x * 0.36f, flow);
            float alpha = Mathf.Lerp(0.26f, 0.86f, 1f - Mathf.Abs(flow - 0.5f) * 2f) * warn * Mathf.Lerp(0.75f, 1.05f, lifeN);
            chevron.transform.localPosition = new Vector3(x, 0f, -0.035f);
            chevron.transform.localRotation = Quaternion.Euler(0f, 0f, directionSign >= 0f ? 45f : -45f);
            chevron.transform.localScale = new Vector3(0.18f, gateSize.y * 0.46f, 1f);
            chevron.color = new Color(1f, 0.90f, 1f, alpha);
        }
    }

    private void UpdateFrameEdges(Color hot, float framePulse, float warn)
    {
        Vector2 frameSize = gateSize + new Vector2(0.42f, 0.30f);
        float thickness = 0.055f;
        Color edgeColor = new Color(1f, 0.88f, 1f, Mathf.Lerp(0.52f, 0.95f, framePulse) * warn);
        Color sideColor = new Color(hot.r, hot.g, hot.b, Mathf.Lerp(0.36f, 0.68f, framePulse) * warn);

        SetFrameEdge(0, new Vector3(0f, frameSize.y * 0.5f, -0.04f), new Vector2(frameSize.x, thickness), edgeColor);
        SetFrameEdge(1, new Vector3(0f, -frameSize.y * 0.5f, -0.04f), new Vector2(frameSize.x, thickness), edgeColor);
        SetFrameEdge(2, new Vector3(-frameSize.x * 0.5f, 0f, -0.04f), new Vector2(thickness, frameSize.y), sideColor);
        SetFrameEdge(3, new Vector3(frameSize.x * 0.5f, 0f, -0.04f), new Vector2(thickness, frameSize.y), sideColor);
    }

    private void SetFrameEdge(int index, Vector3 localPosition, Vector2 size, Color color)
    {
        if (index < 0 || index >= frameEdgeRenderers.Length || frameEdgeRenderers[index] == null)
        {
            return;
        }

        SpriteRenderer edge = frameEdgeRenderers[index];
        edge.transform.localPosition = localPosition;
        edge.transform.localRotation = Quaternion.identity;
        edge.size = size;
        edge.color = color;
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
            sr.sortingOrder = 22;
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
            sr.color = new Color(1f, 0.84f, 0.98f, Mathf.Lerp(0.16f, 0.50f, pulse) * warn);
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

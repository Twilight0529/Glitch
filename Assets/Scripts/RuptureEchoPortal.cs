using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public class RuptureEchoPortal : MonoBehaviour
{
    // Portal enlazado de Rupture: cruzarlo reposiciona al jugador y deja una trampa de eco.
    private RuptureSpinEventController owner;
    private CircleCollider2D trigger;
    private SpriteRenderer ringRenderer;
    private SpriteRenderer coreRenderer;
    private readonly SpriteRenderer[] shardRenderers = new SpriteRenderer[10];

    private float radius = 0.7f;
    private float lifetime = 8f;
    private float age;
    private Color color = Color.magenta;
    private int portalIndex;

    public int PortalIndex => portalIndex;
    public Vector2 Position => transform.position;

    public void Configure(RuptureSpinEventController ownerRef, int index, float portalRadius, float lifeSeconds, Color tint)
    {
        owner = ownerRef;
        portalIndex = index;
        radius = Mathf.Max(0.25f, portalRadius);
        lifetime = Mathf.Max(0.5f, lifeSeconds);
        color = tint;
        EnsureVisuals();
        UpdateVisuals();
    }

    private void Awake()
    {
        trigger = GetComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        EnsureVisuals();
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        UpdateVisuals();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController player = other != null ? other.GetComponent<PlayerController>() : null;
        if (player != null)
        {
            owner?.NotifyRupturePortalEntered(this, player);
        }
    }

    private void EnsureVisuals()
    {
        if (trigger != null)
        {
            trigger.radius = radius;
        }

        if (ringRenderer == null)
        {
            ringRenderer = GetComponent<SpriteRenderer>();
            if (ringRenderer == null)
            {
                ringRenderer = gameObject.AddComponent<SpriteRenderer>();
            }
        }

        ringRenderer.sprite = CircleSpriteProvider.Get();
        ringRenderer.drawMode = SpriteDrawMode.Sliced;
        ringRenderer.sortingOrder = 23;

        if (coreRenderer == null)
        {
            coreRenderer = CreateRenderer("PortalCore", CircleSpriteProvider.Get(), 24);
        }

        for (int i = 0; i < shardRenderers.Length; i++)
        {
            if (shardRenderers[i] == null)
            {
                shardRenderers[i] = CreateRenderer($"PortalShard_{i}", SquareSpriteProvider.Get(), 25);
            }
        }
    }

    private SpriteRenderer CreateRenderer(string childName, Sprite sprite, int order)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(transform, false);
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.sortingOrder = order;
        renderer.color = Color.clear;
        return renderer;
    }

    private void UpdateVisuals()
    {
        EnsureVisuals();

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 8.8f + portalIndex);
        if (ringRenderer != null)
        {
            ringRenderer.size = Vector2.one * radius * Mathf.Lerp(1.85f, 2.35f, pulse);
            Color ring = Color.Lerp(color, Color.white, pulse * 0.34f);
            ring.a = Mathf.Lerp(0.30f, 0.72f, pulse);
            ringRenderer.color = ring;
        }

        if (coreRenderer != null)
        {
            coreRenderer.transform.localScale = Vector3.one * Mathf.Lerp(0.72f, 1.08f, pulse);
            coreRenderer.size = Vector2.one * radius * 1.05f;
            coreRenderer.color = new Color(0.03f, 0.01f, 0.06f, Mathf.Lerp(0.58f, 0.82f, pulse));
        }

        for (int i = 0; i < shardRenderers.Length; i++)
        {
            SpriteRenderer shard = shardRenderers[i];
            if (shard == null)
            {
                continue;
            }

            float angle = (Mathf.PI * 2f * i / shardRenderers.Length) - Time.time * 1.1f + portalIndex * 0.6f;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float jitter = Mathf.Sin(Time.time * 19f + i) * 0.12f;
            shard.transform.localPosition = dir * radius * (0.76f + jitter);
            shard.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f);
            shard.size = new Vector2(Mathf.Lerp(0.14f, 0.36f, pulse), 0.055f);
            Color shardColor = Color.Lerp(color, new Color(0.42f, 0.96f, 1f, 1f), (i & 1) == 0 ? pulse : 0.35f);
            shardColor.a = Mathf.Lerp(0.42f, 0.95f, pulse);
            shard.color = shardColor;
        }
    }
}

public class RupturePlayerEchoFx : MonoBehaviour
{
    private RuptureSpinEventController owner;
    private CircleCollider2D trigger;
    private SpriteRenderer bodyRenderer;
    private readonly SpriteRenderer[] echoLines = new SpriteRenderer[7];
    private float radius = 0.9f;
    private float duration = 1.3f;
    private float stunSeconds = 1f;
    private float age;
    private Color color = Color.cyan;
    private bool consumed;

    public void Configure(RuptureSpinEventController ownerRef, float echoRadius, float lifeSeconds, float stunDuration, Color tint)
    {
        owner = ownerRef;
        radius = Mathf.Max(0.25f, echoRadius);
        duration = Mathf.Max(0.15f, lifeSeconds);
        stunSeconds = Mathf.Max(0.05f, stunDuration);
        color = tint;
        EnsureVisuals();
    }

    private void Awake()
    {
        trigger = gameObject.AddComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        EnsureVisuals();
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= duration)
        {
            Destroy(gameObject);
            return;
        }

        UpdateVisuals();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed || other == null)
        {
            return;
        }

        EnemyController enemy = other.GetComponent<EnemyController>();
        if (enemy != null)
        {
            consumed = true;
            enemy.ApplyContainmentLock(transform.position, stunSeconds);
            owner?.NotifyRuptureEchoTrapTriggered();
            Destroy(gameObject, 0.08f);
            return;
        }

        SplitAnomalyCloneController clone = other.GetComponent<SplitAnomalyCloneController>();
        if (clone != null)
        {
            consumed = true;
            clone.ApplyContainmentLock(stunSeconds);
            owner?.NotifyRuptureEchoTrapTriggered();
            Destroy(gameObject, 0.08f);
        }
    }

    private void EnsureVisuals()
    {
        if (trigger != null)
        {
            trigger.radius = radius;
        }

        if (bodyRenderer == null)
        {
            bodyRenderer = gameObject.AddComponent<SpriteRenderer>();
            bodyRenderer.sprite = CircleSpriteProvider.Get();
            bodyRenderer.drawMode = SpriteDrawMode.Sliced;
            bodyRenderer.sortingOrder = 24;
        }

        for (int i = 0; i < echoLines.Length; i++)
        {
            if (echoLines[i] == null)
            {
                GameObject line = new GameObject($"EchoLine_{i}");
                line.transform.SetParent(transform, false);
                SpriteRenderer sr = line.AddComponent<SpriteRenderer>();
                sr.sprite = SquareSpriteProvider.Get();
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.sortingOrder = 26;
                echoLines[i] = sr;
            }
        }
    }

    private void UpdateVisuals()
    {
        EnsureVisuals();

        float t = Mathf.Clamp01(age / Mathf.Max(0.001f, duration));
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 14f);
        if (bodyRenderer != null)
        {
            bodyRenderer.size = Vector2.one * radius * Mathf.Lerp(1.85f, 2.55f, t);
            Color c = Color.Lerp(color, Color.white, pulse * 0.3f);
            c.a = Mathf.Lerp(0.5f, 0f, t) * (0.65f + pulse * 0.35f);
            bodyRenderer.color = c;
        }

        for (int i = 0; i < echoLines.Length; i++)
        {
            SpriteRenderer line = echoLines[i];
            if (line == null)
            {
                continue;
            }

            float y = Mathf.Lerp(-radius, radius, i / Mathf.Max(1f, echoLines.Length - 1f));
            float offset = Mathf.Sin(Time.time * 25f + i * 0.8f) * radius * 0.28f;
            line.transform.localPosition = new Vector3(offset, y, 0f);
            line.size = new Vector2(radius * Mathf.Lerp(0.55f, 1.45f, pulse), 0.045f);
            Color lineColor = i % 2 == 0 ? color : new Color(1f, 0.42f, 0.95f, 1f);
            lineColor.a = Mathf.Lerp(0.62f, 0f, t);
            line.color = lineColor;
        }
    }
}

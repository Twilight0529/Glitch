using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class LabContainmentTerminal : MonoBehaviour
{
    // Terminal de laboratorio: el jugador debe permanecer encima para cargar el protocolo.
    private LabSweepEventController owner;
    private BoxCollider2D trigger;
    private SpriteRenderer bodyRenderer;
    private SpriteRenderer fillRenderer;
    private SpriteRenderer ringRenderer;
    private readonly SpriteRenderer[] tickRenderers = new SpriteRenderer[4];

    private float activationSeconds = 1f;
    private float lifetime = 6f;
    private float age;
    private float activationTimer;
    private Color accentColor = Color.cyan;
    private bool playerInside;
    private bool activated;

    public bool IsActivated => activated;

    public void Configure(LabSweepEventController ownerRef, float secondsToActivate, float lifeSeconds, Color tint)
    {
        owner = ownerRef;
        activationSeconds = Mathf.Max(0.15f, secondsToActivate);
        lifetime = Mathf.Max(activationSeconds + 0.25f, lifeSeconds);
        accentColor = tint;
        EnsureVisuals();
        UpdateVisuals();
    }

    private void Awake()
    {
        trigger = GetComponent<BoxCollider2D>();
        trigger.isTrigger = true;
        trigger.size = new Vector2(1.05f, 1.05f);
        EnsureVisuals();
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (!activated && age >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        if (!activated)
        {
            float speed = playerInside ? 1f : -0.72f;
            activationTimer = Mathf.Clamp(activationTimer + Time.deltaTime * speed, 0f, activationSeconds);
            if (activationTimer >= activationSeconds)
            {
                Activate();
            }
        }

        UpdateVisuals();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other != null && other.GetComponent<PlayerController>() != null)
        {
            playerInside = true;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other != null && other.GetComponent<PlayerController>() != null)
        {
            playerInside = false;
        }
    }

    private void Activate()
    {
        if (activated)
        {
            return;
        }

        activated = true;
        activationTimer = activationSeconds;
        if (trigger != null)
        {
            trigger.enabled = false;
        }

        owner?.NotifyContainmentTerminalActivated(this);
    }

    private void EnsureVisuals()
    {
        if (bodyRenderer == null)
        {
            bodyRenderer = GetComponent<SpriteRenderer>();
            if (bodyRenderer == null)
            {
                bodyRenderer = gameObject.AddComponent<SpriteRenderer>();
            }
        }

        bodyRenderer.sprite = SquareSpriteProvider.Get();
        bodyRenderer.drawMode = SpriteDrawMode.Sliced;
        bodyRenderer.sortingOrder = 16;
        bodyRenderer.size = Vector2.one * 1.05f;

        if (fillRenderer == null)
        {
            fillRenderer = CreateChildRenderer("TerminalCharge", SquareSpriteProvider.Get(), 18);
        }

        if (ringRenderer == null)
        {
            ringRenderer = CreateChildRenderer("TerminalRing", CircleSpriteProvider.Get(), 17);
        }

        for (int i = 0; i < tickRenderers.Length; i++)
        {
            if (tickRenderers[i] == null)
            {
                tickRenderers[i] = CreateChildRenderer($"TerminalTick_{i}", SquareSpriteProvider.Get(), 19);
            }
        }
    }

    private SpriteRenderer CreateChildRenderer(string childName, Sprite sprite, int sortingOrder)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(transform, false);
        child.transform.localPosition = Vector3.zero;
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private void UpdateVisuals()
    {
        EnsureVisuals();

        float progress = Mathf.Clamp01(activationTimer / Mathf.Max(0.001f, activationSeconds));
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (playerInside ? 10f : 5.2f));
        Color body = new Color(0.025f, 0.045f, 0.08f, 0.88f);
        if (activated)
        {
            body = Color.Lerp(body, accentColor, 0.35f);
        }

        bodyRenderer.color = body;

        if (fillRenderer != null)
        {
            fillRenderer.transform.localPosition = new Vector3(0f, Mathf.Lerp(-0.39f, 0f, progress), 0f);
            fillRenderer.size = new Vector2(0.62f, Mathf.Lerp(0.04f, 0.78f, progress));
            Color fill = Color.Lerp(new Color(0.22f, 0.55f, 0.78f, 0.52f), accentColor, progress);
            fill.a = activated ? 0.9f : Mathf.Lerp(0.28f, 0.78f, progress) + pulse * 0.08f;
            fillRenderer.color = fill;
        }

        if (ringRenderer != null)
        {
            ringRenderer.transform.localScale = Vector3.one * Mathf.Lerp(0.92f, 1.18f, pulse);
            ringRenderer.size = Vector2.one * 1.4f;
            Color ring = Color.Lerp(accentColor, Color.white, activated ? 0.38f : progress * 0.24f);
            ring.a = activated ? 0.58f : Mathf.Lerp(0.14f, 0.42f, pulse + progress * 0.35f);
            ringRenderer.color = ring;
        }

        for (int i = 0; i < tickRenderers.Length; i++)
        {
            SpriteRenderer tick = tickRenderers[i];
            if (tick == null)
            {
                continue;
            }

            float side = i < 2 ? -1f : 1f;
            float vertical = (i & 1) == 0 ? -1f : 1f;
            tick.transform.localPosition = new Vector3(side * 0.72f, vertical * 0.72f, 0f);
            tick.transform.localRotation = Quaternion.Euler(0f, 0f, i < 2 ? 0f : 90f);
            tick.size = new Vector2(0.32f, 0.055f);
            Color tickColor = Color.Lerp(accentColor, Color.white, pulse * 0.35f);
            tickColor.a = activated ? 0.9f : Mathf.Lerp(0.28f, 0.7f, pulse);
            tick.color = tickColor;
        }
    }
}

public class LabContainmentCageFx : MonoBehaviour
{
    private SpriteRenderer ringRenderer;
    private readonly SpriteRenderer[] bars = new SpriteRenderer[6];
    private float duration = 1.6f;
    private float radius = 1.8f;
    private float age;
    private Color color = Color.cyan;

    public void Configure(float cageRadius, float lifeSeconds, Color tint)
    {
        radius = Mathf.Max(0.6f, cageRadius);
        duration = Mathf.Max(0.2f, lifeSeconds);
        color = tint;
        EnsureVisuals();
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / Mathf.Max(0.001f, duration));
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 13f);
        float alpha = Mathf.Sin(t * Mathf.PI);

        EnsureVisuals();
        if (ringRenderer != null)
        {
            ringRenderer.size = Vector2.one * radius * Mathf.Lerp(1.75f, 2.35f, t);
            Color ring = Color.Lerp(color, Color.white, 0.34f + pulse * 0.18f);
            ring.a = 0.62f * alpha;
            ringRenderer.color = ring;
        }

        for (int i = 0; i < bars.Length; i++)
        {
            SpriteRenderer bar = bars[i];
            if (bar == null)
            {
                continue;
            }

            float angle = (Mathf.PI * 2f * i / bars.Length) + Time.time * 0.55f;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            bar.transform.localPosition = dir * radius * Mathf.Lerp(0.35f, 0.82f, t);
            bar.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f);
            bar.size = new Vector2(0.09f, radius * Mathf.Lerp(1.35f, 2.1f, t));
            Color barColor = Color.Lerp(color, Color.white, pulse * 0.42f);
            barColor.a = alpha * Mathf.Lerp(0.55f, 0.95f, pulse);
            bar.color = barColor;
        }

        if (age >= duration)
        {
            Destroy(gameObject);
        }
    }

    private void EnsureVisuals()
    {
        if (ringRenderer == null)
        {
            ringRenderer = CreateRenderer("ContainmentCageRing", CircleSpriteProvider.Get(), 26);
        }

        for (int i = 0; i < bars.Length; i++)
        {
            if (bars[i] == null)
            {
                bars[i] = CreateRenderer($"ContainmentCageBar_{i}", SquareSpriteProvider.Get(), 27);
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
}

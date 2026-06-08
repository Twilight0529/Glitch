using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public class RuptureResonanceAnchor : MonoBehaviour
{
    // Nodo interactivo de Rupture: al tocar dos anclas se dibuja una trampa lineal de eco.
    private RuptureSpinEventController controller;
    private CircleCollider2D triggerCollider;
    private SpriteRenderer outerRenderer;
    private SpriteRenderer coreRenderer;
    private SpriteRenderer pointerRenderer;
    private Color telegraphColor;
    private Color activeColor;
    private float radius = 1f;
    private float lifeSeconds = 5f;
    private float telegraphSeconds = 0.6f;
    private float age;
    private bool armed;
    private bool triggered;
    private int index;

    public Vector2 Position => transform.position;

    public void Configure(
        RuptureSpinEventController owner,
        int anchorIndex,
        float anchorRadius,
        float lifetime,
        float telegraphTime,
        Color telegraph,
        Color active)
    {
        controller = owner;
        index = anchorIndex;
        radius = Mathf.Max(0.25f, anchorRadius);
        lifeSeconds = Mathf.Max(0.2f, lifetime);
        telegraphSeconds = Mathf.Clamp(telegraphTime, 0.05f, lifeSeconds);
        telegraphColor = telegraph;
        activeColor = active;

        EnsureVisuals();
        triggerCollider.radius = radius * 0.72f;
        triggerCollider.isTrigger = true;
        UpdateVisuals();
    }

    private void Awake()
    {
        triggerCollider = GetComponent<CircleCollider2D>();
        EnsureVisuals();
    }

    private void Update()
    {
        age += Time.deltaTime;
        armed = age >= telegraphSeconds && age <= lifeSeconds;
        UpdateVisuals();

        if (age >= lifeSeconds)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!armed || triggered)
        {
            return;
        }

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null)
        {
            return;
        }

        triggered = true;
        controller?.NotifyRiftEchoAnchorTriggered(this, player);
    }

    private void EnsureVisuals()
    {
        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<CircleCollider2D>();
        }

        if (outerRenderer == null)
        {
            outerRenderer = CreateRenderer("EchoAnchorOuter", CircleSpriteProvider.Get(), 18);
        }

        if (coreRenderer == null)
        {
            coreRenderer = CreateRenderer("EchoAnchorCore", CircleSpriteProvider.Get(), 19);
        }

        if (pointerRenderer == null)
        {
            pointerRenderer = CreateRenderer("EchoAnchorPointer", SquareSpriteProvider.Get(), 20);
        }
    }

    private SpriteRenderer CreateRenderer(string childName, Sprite sprite, int sortingOrder)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(transform, false);
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private void UpdateVisuals()
    {
        float telegraphT = Mathf.Clamp01(age / Mathf.Max(0.05f, telegraphSeconds));
        float lifeT = Mathf.Clamp01(age / Mathf.Max(0.05f, lifeSeconds));
        float fadeOut = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - lifeT) / 0.18f));
        float pulse = 0.5f + 0.5f * Mathf.Sin((Time.time * 8.5f) + index);
        Color current = Color.Lerp(telegraphColor, activeColor, armed ? 1f : telegraphT);
        if (triggered)
        {
            current = Color.Lerp(activeColor, Color.white, 0.35f + pulse * 0.25f);
        }

        float alpha = fadeOut * (triggered ? 0.95f : 0.58f + pulse * 0.22f);
        outerRenderer.size = Vector2.one * radius * Mathf.Lerp(1.65f, 2.2f, pulse);
        outerRenderer.color = new Color(current.r, current.g, current.b, alpha * 0.38f);

        coreRenderer.size = Vector2.one * radius * (triggered ? 0.62f : Mathf.Lerp(0.38f, 0.56f, pulse));
        coreRenderer.color = new Color(current.r, current.g, current.b, alpha * (armed ? 0.82f : 0.42f));

        float angle = (Time.time * (armed ? 150f : 72f)) + (index * 47f);
        pointerRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        pointerRenderer.size = new Vector2(radius * 1.45f, 0.08f);
        pointerRenderer.color = new Color(1f, 0.86f, 1f, alpha * (armed ? 0.72f : 0.34f));
    }
}

using UnityEngine;

public class RuptureResonanceLink : MonoBehaviour
{
    // Trampa lineal creada al encadenar dos ecos: premia baitear a la anomalia a cruzarla.
    private RuptureSpinEventController controller;
    private SpriteRenderer bodyRenderer;
    private SpriteRenderer coreRenderer;
    private SpriteRenderer pulseRenderer;
    private Vector2 start;
    private Vector2 end;
    private Color activeColor;
    private Color accentColor;
    private float lifeSeconds = 4f;
    private float triggerRadius = 0.35f;
    private float armDelay = 0.22f;
    private float age;
    private bool detonated;

    public void Configure(
        RuptureSpinEventController owner,
        Vector2 startPoint,
        Vector2 endPoint,
        float lifetime,
        float radius,
        Color active,
        Color accent)
    {
        controller = owner;
        start = startPoint;
        end = endPoint;
        lifeSeconds = Mathf.Max(0.2f, lifetime);
        triggerRadius = Mathf.Max(0.05f, radius);
        activeColor = active;
        accentColor = accent;

        EnsureVisuals();
        AlignToSegment();
        UpdateVisuals();
    }

    private void Awake()
    {
        EnsureVisuals();
    }

    private void Update()
    {
        age += Time.deltaTime;

        if (!detonated && age >= armDelay && controller != null && controller.TryDetonateRiftEchoLink(start, end, triggerRadius))
        {
            detonated = true;
            age = Mathf.Max(age, lifeSeconds - 0.32f);
        }

        UpdateVisuals();
        if (age >= lifeSeconds)
        {
            Destroy(gameObject);
        }
    }

    private void EnsureVisuals()
    {
        if (bodyRenderer == null)
        {
            bodyRenderer = CreateRenderer("ResonanceLinkBody", 18);
        }

        if (coreRenderer == null)
        {
            coreRenderer = CreateRenderer("ResonanceLinkCore", 19);
        }

        if (pulseRenderer == null)
        {
            pulseRenderer = CreateRenderer("ResonanceLinkPulse", 20);
        }
    }

    private SpriteRenderer CreateRenderer(string childName, int sortingOrder)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(transform, false);
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = SquareSpriteProvider.Get();
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private void AlignToSegment()
    {
        Vector2 delta = end - start;
        float length = delta.magnitude;
        Vector2 mid = (start + end) * 0.5f;
        transform.position = new Vector3(mid.x, mid.y, 0f);
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

        bodyRenderer.size = new Vector2(length, triggerRadius * 2.15f);
        coreRenderer.size = new Vector2(length, Mathf.Max(0.04f, triggerRadius * 0.34f));
        pulseRenderer.size = new Vector2(Mathf.Max(0.18f, length * 0.18f), triggerRadius * 1.32f);
    }

    private void UpdateVisuals()
    {
        float lifeT = Mathf.Clamp01(age / Mathf.Max(0.05f, lifeSeconds));
        float fadeIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(lifeT / 0.12f));
        float fadeOut = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - lifeT) / 0.12f));
        float envelope = fadeIn * fadeOut;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (detonated ? 26f : 13f));
        float detonationBoost = detonated ? 1.8f : 1f;

        bodyRenderer.color = new Color(activeColor.r, activeColor.g, activeColor.b, 0.22f * envelope * detonationBoost);
        coreRenderer.color = new Color(1f, 0.92f, 1f, (0.55f + pulse * 0.32f) * envelope);

        float travel = Mathf.Repeat(Time.time * 1.8f, 1f);
        float length = Vector2.Distance(start, end);
        pulseRenderer.transform.localPosition = new Vector3(Mathf.Lerp(-length * 0.5f, length * 0.5f, travel), 0f, 0f);
        pulseRenderer.color = new Color(accentColor.r, accentColor.g, accentColor.b, (0.42f + pulse * 0.28f) * envelope);

        if (detonated)
        {
            bodyRenderer.size = new Vector2(length, triggerRadius * Mathf.Lerp(2.4f, 4.8f, pulse));
            coreRenderer.size = new Vector2(length, triggerRadius * Mathf.Lerp(0.55f, 1.35f, pulse));
        }
    }
}

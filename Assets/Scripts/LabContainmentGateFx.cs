using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class LabContainmentGateFx : MonoBehaviour
{
    // Compuerta ambiental de Lab: avisa el cierre, se despliega, bloquea un sector y se retrae.
    private BoxCollider2D gateCollider;
    private Rigidbody2D gateRigidbody;
    private SpriteRenderer bodyRenderer;
    private SpriteRenderer warningRenderer;
    private readonly SpriteRenderer[] edgeRenderers = new SpriteRenderer[4];
    private readonly SpriteRenderer[] stripeRenderers = new SpriteRenderer[6];

    private Vector2 startPosition;
    private Vector2 targetPosition;
    private Vector2 gateSize = Vector2.one;
    private Color warningColor = Color.yellow;
    private Color activeColor = Color.cyan;
    private float duration = 3f;
    private float telegraphFraction = 0.22f;
    private float deployEndFraction = 0.42f;
    private float retractStartFraction = 0.78f;
    private float age;

    public void Configure(
        Vector2 start,
        Vector2 target,
        Vector2 size,
        float lifeSeconds,
        float warningFraction,
        float deployedFraction,
        float retractFraction,
        Color warning,
        Color active)
    {
        startPosition = start;
        targetPosition = target;
        gateSize = new Vector2(Mathf.Max(0.2f, size.x), Mathf.Max(0.2f, size.y));
        duration = Mathf.Max(0.5f, lifeSeconds);
        telegraphFraction = Mathf.Clamp(warningFraction, 0.05f, 0.6f);
        deployEndFraction = Mathf.Clamp(deployedFraction, telegraphFraction + 0.04f, 0.82f);
        retractStartFraction = Mathf.Clamp(retractFraction, deployEndFraction + 0.05f, 0.96f);
        warningColor = warning;
        activeColor = active;
        EnsureVisuals();
        UpdateVisuals(0f);
    }

    private void Awake()
    {
        gateCollider = GetComponent<BoxCollider2D>();
        gateRigidbody = GetComponent<Rigidbody2D>();
        gateRigidbody.bodyType = RigidbodyType2D.Kinematic;
        gateRigidbody.gravityScale = 0f;
        gateCollider.enabled = false;
        gateCollider.isTrigger = false;
        EnsureVisuals();
    }

    private void Update()
    {
        age += Time.deltaTime;
        float progress = Mathf.Clamp01(age / Mathf.Max(0.001f, duration));
        if (progress >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        UpdateVisuals(progress);
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
        bodyRenderer.sortingOrder = 13;

        if (warningRenderer == null)
        {
            warningRenderer = CreateRenderer("GateWarning", 12);
        }

        for (int i = 0; i < edgeRenderers.Length; i++)
        {
            if (edgeRenderers[i] == null)
            {
                edgeRenderers[i] = CreateRenderer($"GateEdge_{i}", 14);
            }
        }

        for (int i = 0; i < stripeRenderers.Length; i++)
        {
            if (stripeRenderers[i] == null)
            {
                stripeRenderers[i] = CreateRenderer($"GateStripe_{i}", 15);
            }
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
        renderer.color = Color.clear;
        return renderer;
    }

    private void UpdateVisuals(float progress)
    {
        EnsureVisuals();

        float deployT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(telegraphFraction, deployEndFraction, progress));
        float retractT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(retractStartFraction, 0.98f, progress));
        float visibleT = Mathf.Clamp01(deployT * (1f - retractT));
        Vector2 position = Vector2.Lerp(startPosition, targetPosition, visibleT);

        if (gateRigidbody != null)
        {
            gateRigidbody.MovePosition(position);
        }
        else
        {
            transform.position = new Vector3(position.x, position.y, transform.position.z);
        }

        if (gateCollider != null)
        {
            gateCollider.size = gateSize;
            gateCollider.enabled = visibleT > 0.95f && progress < retractStartFraction;
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 8.4f);
        Color bodyColor = Color.Lerp(activeColor, new Color(0.09f, 0.13f, 0.18f, 1f), 0.38f);
        bodyColor.a = Mathf.Lerp(0.42f, 0.92f, visibleT);
        bodyRenderer.size = gateSize;
        bodyRenderer.color = bodyColor;

        UpdateWarning(progress, pulse);
        UpdateEdges(visibleT, pulse);
        UpdateStripes(visibleT, pulse);
    }

    private void UpdateWarning(float progress, float pulse)
    {
        if (warningRenderer == null)
        {
            return;
        }

        Vector2 localTarget = targetPosition - (Vector2)transform.position;
        warningRenderer.transform.localPosition = localTarget;
        warningRenderer.transform.localRotation = Quaternion.identity;
        warningRenderer.size = gateSize + Vector2.one * Mathf.Lerp(0.1f, 0.34f, pulse);
        Color color = progress < telegraphFraction ? warningColor : activeColor;
        color.a = progress < telegraphFraction
            ? Mathf.Lerp(0.20f, 0.58f, pulse)
            : Mathf.Lerp(0.08f, 0.22f, pulse) * Mathf.Clamp01(1f - progress);
        warningRenderer.color = color;
    }

    private void UpdateEdges(float visibleT, float pulse)
    {
        Color edge = Color.Lerp(activeColor, Color.white, pulse * 0.22f);
        edge.a = Mathf.Lerp(0.1f, 0.95f, visibleT);
        SetEdge(0, new Vector2(0f, gateSize.y * 0.5f), new Vector2(gateSize.x, 0.075f), edge);
        SetEdge(1, new Vector2(0f, -gateSize.y * 0.5f), new Vector2(gateSize.x, 0.075f), edge);
        SetEdge(2, new Vector2(-gateSize.x * 0.5f, 0f), new Vector2(0.075f, gateSize.y), edge);
        SetEdge(3, new Vector2(gateSize.x * 0.5f, 0f), new Vector2(0.075f, gateSize.y), edge);
    }

    private void SetEdge(int index, Vector2 localPosition, Vector2 size, Color color)
    {
        if (index < 0 || index >= edgeRenderers.Length || edgeRenderers[index] == null)
        {
            return;
        }

        SpriteRenderer edge = edgeRenderers[index];
        edge.transform.localPosition = localPosition;
        edge.transform.localRotation = Quaternion.identity;
        edge.size = size;
        edge.color = color;
    }

    private void UpdateStripes(float visibleT, float pulse)
    {
        bool horizontal = gateSize.x >= gateSize.y;
        Color stripeColor = Color.Lerp(warningColor, Color.white, pulse * 0.16f);
        stripeColor.a = Mathf.Lerp(0.06f, 0.74f, visibleT);

        for (int i = 0; i < stripeRenderers.Length; i++)
        {
            SpriteRenderer stripe = stripeRenderers[i];
            if (stripe == null)
            {
                continue;
            }

            float t = (i + 0.5f) / stripeRenderers.Length;
            if (horizontal)
            {
                stripe.transform.localPosition = new Vector3(Mathf.Lerp(-gateSize.x * 0.42f, gateSize.x * 0.42f, t), 0f, 0f);
                stripe.transform.localRotation = Quaternion.Euler(0f, 0f, 18f);
                stripe.size = new Vector2(0.1f, gateSize.y * 1.15f);
            }
            else
            {
                stripe.transform.localPosition = new Vector3(0f, Mathf.Lerp(-gateSize.y * 0.42f, gateSize.y * 0.42f, t), 0f);
                stripe.transform.localRotation = Quaternion.Euler(0f, 0f, -18f);
                stripe.size = new Vector2(gateSize.x * 1.15f, 0.1f);
            }

            stripe.color = stripeColor;
        }
    }
}

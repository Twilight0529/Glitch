using UnityEngine;

public class StorageCraneHookFx : MonoBehaviour
{
    // Indicador visual de grua: conecta la maquinaria del borde con la carga que esta manipulando.
    private enum AnchorMode
    {
        Top,
        Left,
        Right
    }

    private Transform target;
    private SpriteRenderer cableRenderer;
    private SpriteRenderer carriageRenderer;
    private SpriteRenderer hookRenderer;
    private SpriteRenderer glowRenderer;
    private Color warningColor = Color.yellow;
    private Color activeColor = Color.cyan;
    private float topY;
    private float sideX;
    private float lifeSeconds = 4f;
    private float telegraphFraction = 0.2f;
    private float age;
    private AnchorMode anchorMode = AnchorMode.Top;

    public void Configure(Transform followTarget, float arenaTopY, float duration, float warningFraction, Color warning, Color active)
    {
        target = followTarget;
        topY = arenaTopY;
        anchorMode = AnchorMode.Top;
        lifeSeconds = Mathf.Max(0.25f, duration);
        telegraphFraction = Mathf.Clamp01(warningFraction);
        warningColor = warning;
        activeColor = active;
        EnsureVisuals();
        UpdateVisuals(0f);
    }

    public void ConfigureSide(Transform followTarget, float arenaSideX, bool fromLeft, float duration, float warningFraction, Color warning, Color active)
    {
        target = followTarget;
        sideX = arenaSideX;
        anchorMode = fromLeft ? AnchorMode.Left : AnchorMode.Right;
        lifeSeconds = Mathf.Max(0.25f, duration);
        telegraphFraction = Mathf.Clamp01(warningFraction);
        warningColor = warning;
        activeColor = active;
        EnsureVisuals();
        UpdateVisuals(0f);
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= lifeSeconds || target == null)
        {
            Destroy(gameObject);
            return;
        }

        UpdateVisuals(Mathf.Clamp01(age / lifeSeconds));
    }

    private void EnsureVisuals()
    {
        if (cableRenderer == null)
        {
            cableRenderer = CreateRenderer("CraneCable", 18);
        }
        if (carriageRenderer == null)
        {
            carriageRenderer = CreateRenderer("CraneCarriage", 19);
        }
        if (hookRenderer == null)
        {
            hookRenderer = CreateRenderer("CraneHook", 20);
        }
        if (glowRenderer == null)
        {
            glowRenderer = CreateRenderer("CraneGlow", 17);
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

        if (target == null)
        {
            return;
        }

        Vector2 targetPos = target.position;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 9.5f);
        bool telegraph = progress < telegraphFraction;
        Color color = telegraph ? warningColor : activeColor;
        color = Color.Lerp(color, Color.white, pulse * 0.18f);
        color.a = telegraph ? Mathf.Lerp(0.32f, 0.62f, pulse) : Mathf.Lerp(0.48f, 0.88f, pulse);

        transform.position = new Vector3(targetPos.x, targetPos.y, -0.02f);

        if (anchorMode == AnchorMode.Top)
        {
            UpdateTopCrane(targetPos, color);
        }
        else
        {
            UpdateSideCrane(targetPos, color);
        }

        hookRenderer.transform.localPosition = Vector3.zero;
        hookRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, 45f + Mathf.Sin(Time.time * 6.2f) * 4f);
        hookRenderer.size = new Vector2(0.38f, 0.38f);
        hookRenderer.color = color;

        glowRenderer.transform.localPosition = Vector3.zero;
        glowRenderer.transform.localRotation = Quaternion.identity;
        glowRenderer.size = Vector2.one * Mathf.Lerp(0.8f, 1.3f, pulse);
        Color glow = color;
        glow.a *= telegraph ? 0.18f : 0.28f;
        glowRenderer.color = glow;
    }

    private void UpdateTopCrane(Vector2 targetPos, Color color)
    {
        float anchorY = Mathf.Max(topY + 0.25f, targetPos.y + 0.9f);
        float cableLength = Mathf.Max(0.35f, anchorY - targetPos.y);

        cableRenderer.transform.localPosition = new Vector3(0f, cableLength * 0.5f, 0f);
        cableRenderer.transform.localRotation = Quaternion.identity;
        cableRenderer.size = new Vector2(0.055f, cableLength);
        cableRenderer.color = color;

        carriageRenderer.transform.localPosition = new Vector3(0f, cableLength + 0.08f, 0f);
        carriageRenderer.transform.localRotation = Quaternion.identity;
        carriageRenderer.size = new Vector2(0.82f, 0.16f);
        carriageRenderer.color = color;
    }

    private void UpdateSideCrane(Vector2 targetPos, Color color)
    {
        float sign = anchorMode == AnchorMode.Left ? -1f : 1f;
        float anchorX = anchorMode == AnchorMode.Left
            ? Mathf.Min(sideX - 0.25f, targetPos.x - 0.9f)
            : Mathf.Max(sideX + 0.25f, targetPos.x + 0.9f);
        float cableLength = Mathf.Max(0.35f, Mathf.Abs(targetPos.x - anchorX));

        cableRenderer.transform.localPosition = new Vector3(sign * cableLength * 0.5f, 0f, 0f);
        cableRenderer.transform.localRotation = Quaternion.identity;
        cableRenderer.size = new Vector2(cableLength, 0.055f);
        cableRenderer.color = color;

        carriageRenderer.transform.localPosition = new Vector3(sign * (cableLength + 0.08f), 0f, 0f);
        carriageRenderer.transform.localRotation = Quaternion.identity;
        carriageRenderer.size = new Vector2(0.16f, 0.82f);
        carriageRenderer.color = color;
    }
}

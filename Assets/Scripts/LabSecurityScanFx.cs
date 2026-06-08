using UnityEngine;

public class LabSecurityScanFx : MonoBehaviour
{
    // Barrido ambiental de Lab: muestra que el sistema de seguridad esta calculando un cierre de contencion.
    private SpriteRenderer scanRenderer;
    private SpriteRenderer pulseRenderer;
    private readonly SpriteRenderer[] nodeRenderers = new SpriteRenderer[2];
    private readonly SpriteRenderer[] tickRenderers = new SpriteRenderer[10];

    private bool horizontal = true;
    private float left;
    private float right;
    private float bottom;
    private float top;
    private float laneCenter;
    private float duration = 3f;
    private float telegraphFraction = 0.25f;
    private float age;
    private Color warningColor = Color.yellow;
    private Color activeColor = Color.cyan;

    public void Configure(
        bool horizontalScan,
        float worldLeft,
        float worldRight,
        float worldBottom,
        float worldTop,
        float axisCenter,
        float lifeSeconds,
        float warningFraction,
        Color warning,
        Color active)
    {
        horizontal = horizontalScan;
        left = Mathf.Min(worldLeft, worldRight);
        right = Mathf.Max(worldLeft, worldRight);
        bottom = Mathf.Min(worldBottom, worldTop);
        top = Mathf.Max(worldBottom, worldTop);
        laneCenter = axisCenter;
        duration = Mathf.Max(0.25f, lifeSeconds);
        telegraphFraction = Mathf.Clamp01(warningFraction);
        warningColor = warning;
        activeColor = active;
        EnsureVisuals();
        UpdateVisuals(0f);
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= duration)
        {
            Destroy(gameObject);
            return;
        }

        UpdateVisuals(Mathf.Clamp01(age / duration));
    }

    private void EnsureVisuals()
    {
        if (scanRenderer == null)
        {
            scanRenderer = CreateRenderer("SecurityScanLine", 10);
        }
        if (pulseRenderer == null)
        {
            pulseRenderer = CreateRenderer("SecurityScanPulse", 16);
        }

        for (int i = 0; i < nodeRenderers.Length; i++)
        {
            if (nodeRenderers[i] == null)
            {
                nodeRenderers[i] = CreateRenderer($"SecurityWallNode_{i}", 17);
            }
        }

        for (int i = 0; i < tickRenderers.Length; i++)
        {
            if (tickRenderers[i] == null)
            {
                tickRenderers[i] = CreateRenderer($"SecurityTick_{i}", 15);
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

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 9f);
        bool warning = progress < telegraphFraction;
        Color color = warning ? warningColor : activeColor;
        color = Color.Lerp(color, Color.white, pulse * 0.16f);
        color.a = warning ? Mathf.Lerp(0.22f, 0.52f, pulse) : Mathf.Lerp(0.30f, 0.72f, pulse);

        float width = Mathf.Max(0.5f, right - left);
        float height = Mathf.Max(0.5f, top - bottom);
        float centerX = (left + right) * 0.5f;
        float centerY = (bottom + top) * 0.5f;
        Vector2 scanPosition = horizontal ? new Vector2(centerX, laneCenter) : new Vector2(laneCenter, centerY);
        scanRenderer.transform.position = new Vector3(scanPosition.x, scanPosition.y, 0f);
        scanRenderer.transform.rotation = Quaternion.identity;
        scanRenderer.size = horizontal ? new Vector2(width, 0.08f) : new Vector2(0.08f, height);
        scanRenderer.color = color;

        UpdatePulse(progress, width, height, color);
        UpdateNodes(width, height, color, pulse);
        UpdateTicks(width, height, color, pulse);
    }

    private void UpdatePulse(float progress, float width, float height, Color color)
    {
        float travel = Mathf.Repeat(progress * 2.2f + Time.time * 0.08f, 1f);
        float x = horizontal ? Mathf.Lerp(left, right, travel) : laneCenter;
        float y = horizontal ? laneCenter : Mathf.Lerp(bottom, top, travel);
        pulseRenderer.transform.position = new Vector3(x, y, 0f);
        pulseRenderer.transform.rotation = horizontal ? Quaternion.identity : Quaternion.Euler(0f, 0f, 90f);
        pulseRenderer.size = horizontal ? new Vector2(0.16f, 0.9f) : new Vector2(0.9f, 0.16f);
        Color pulseColor = Color.Lerp(color, Color.white, 0.35f);
        pulseColor.a *= 1.15f;
        pulseRenderer.color = pulseColor;
    }

    private void UpdateNodes(float width, float height, Color color, float pulse)
    {
        for (int i = 0; i < nodeRenderers.Length; i++)
        {
            SpriteRenderer node = nodeRenderers[i];
            if (node == null)
            {
                continue;
            }

            Vector2 position = horizontal
                ? new Vector2(i == 0 ? left : right, laneCenter)
                : new Vector2(laneCenter, i == 0 ? bottom : top);
            node.transform.position = new Vector3(position.x, position.y, 0f);
            node.transform.rotation = horizontal ? Quaternion.identity : Quaternion.Euler(0f, 0f, 90f);
            node.size = horizontal ? new Vector2(0.22f, 1.2f) : new Vector2(1.2f, 0.22f);
            Color nodeColor = Color.Lerp(color, Color.white, pulse * 0.2f);
            nodeColor.a *= 1.1f;
            node.color = nodeColor;
        }
    }

    private void UpdateTicks(float width, float height, Color color, float pulse)
    {
        for (int i = 0; i < tickRenderers.Length; i++)
        {
            SpriteRenderer tick = tickRenderers[i];
            if (tick == null)
            {
                continue;
            }

            float t = (i + 0.5f) / tickRenderers.Length;
            Vector2 position = horizontal
                ? new Vector2(Mathf.Lerp(left, right, t), laneCenter)
                : new Vector2(laneCenter, Mathf.Lerp(bottom, top, t));
            tick.transform.position = new Vector3(position.x, position.y, 0f);
            tick.transform.rotation = horizontal ? Quaternion.identity : Quaternion.Euler(0f, 0f, 90f);
            tick.size = horizontal ? new Vector2(0.045f, Mathf.Lerp(0.28f, 0.56f, pulse)) : new Vector2(Mathf.Lerp(0.28f, 0.56f, pulse), 0.045f);
            Color tickColor = color;
            tickColor.a *= Mathf.Lerp(0.55f, 1f, pulse);
            tick.color = tickColor;
        }
    }
}

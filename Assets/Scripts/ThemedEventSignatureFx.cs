using UnityEngine;

public class ThemedEventSignatureFx : MonoBehaviour
{
    // Firma visual de evento: refuerza la lectura sin depender del texto del HUD.
    public enum SignatureKind
    {
        LabSweep,
        LabGrid,
        LabContainment,
        StorageConveyor,
        StorageMagnet,
        StorageCargo,
        RuptureSpin,
        RuptureEcho,
        RupturePortal
    }

    private SignatureKind kind;
    private float left;
    private float right;
    private float bottom;
    private float top;
    private float duration = 3f;
    private float age;
    private Color primary = Color.cyan;
    private Color secondary = Color.white;
    private readonly SpriteRenderer[] borderRenderers = new SpriteRenderer[4];
    private readonly SpriteRenderer[] accentRenderers = new SpriteRenderer[18];
    private SpriteRenderer coreRenderer;
    private SpriteRenderer sweepRenderer;

    public void Configure(SignatureKind signatureKind, float worldLeft, float worldRight, float worldBottom, float worldTop, float lifeSeconds, Color primaryColor, Color secondaryColor)
    {
        kind = signatureKind;
        left = Mathf.Min(worldLeft, worldRight);
        right = Mathf.Max(worldLeft, worldRight);
        bottom = Mathf.Min(worldBottom, worldTop);
        top = Mathf.Max(worldBottom, worldTop);
        duration = Mathf.Max(0.25f, lifeSeconds);
        primary = primaryColor;
        secondary = secondaryColor;
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
        for (int i = 0; i < borderRenderers.Length; i++)
        {
            if (borderRenderers[i] == null)
            {
                borderRenderers[i] = CreateRenderer($"SignatureBorder_{i}", SquareSpriteProvider.Get(), 6);
            }
        }

        for (int i = 0; i < accentRenderers.Length; i++)
        {
            if (accentRenderers[i] == null)
            {
                accentRenderers[i] = CreateRenderer($"SignatureAccent_{i}", SquareSpriteProvider.Get(), 7);
            }
        }

        if (coreRenderer == null)
        {
            coreRenderer = CreateRenderer("SignatureCore", CircleSpriteProvider.Get(), 6);
        }

        if (sweepRenderer == null)
        {
            sweepRenderer = CreateRenderer("SignatureSweep", SquareSpriteProvider.Get(), 8);
        }
    }

    private SpriteRenderer CreateRenderer(string childName, Sprite sprite, int order)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(transform, false);
        child.transform.localPosition = Vector3.zero;
        child.transform.localScale = Vector3.one;
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.sortingOrder = order;
        renderer.color = Color.clear;
        return renderer;
    }

    private void UpdateVisuals(float progress)
    {
        EnsureVisuals();
        HideAccents();

        float width = Mathf.Max(0.5f, right - left);
        float height = Mathf.Max(0.5f, top - bottom);
        Vector2 center = new Vector2((left + right) * 0.5f, (bottom + top) * 0.5f);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 5.4f);
        float envelope = Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI);
        float alpha = Mathf.Lerp(0.18f, 0.42f, pulse) * Mathf.Max(0.22f, envelope);

        DrawBorder(width, height, center, alpha);

        switch (kind)
        {
            case SignatureKind.LabSweep:
                DrawLabSweep(width, height, center, pulse, envelope);
                break;
            case SignatureKind.LabGrid:
                DrawLabGrid(width, height, center, pulse, envelope);
                break;
            case SignatureKind.LabContainment:
                DrawContainment(width, height, center, pulse, envelope);
                break;
            case SignatureKind.StorageConveyor:
                DrawConveyor(width, height, center, pulse, envelope);
                break;
            case SignatureKind.StorageMagnet:
                DrawMagnet(width, height, center, pulse, envelope);
                break;
            case SignatureKind.StorageCargo:
                DrawCargo(width, height, center, pulse, envelope);
                break;
            case SignatureKind.RuptureSpin:
                DrawRuptureSpin(width, height, center, pulse, envelope);
                break;
            case SignatureKind.RuptureEcho:
                DrawRuptureEcho(width, height, center, pulse, envelope);
                break;
            case SignatureKind.RupturePortal:
                DrawRupturePortal(width, height, center, pulse, envelope);
                break;
        }
    }

    private void DrawBorder(float width, float height, Vector2 center, float alpha)
    {
        Color a = WithAlpha(primary, alpha);
        float thickness = 0.055f;
        SetRect(borderRenderers[0], new Vector2(center.x, top), new Vector2(width, thickness), a, 0f);
        SetRect(borderRenderers[1], new Vector2(center.x, bottom), new Vector2(width, thickness), a, 0f);
        SetRect(borderRenderers[2], new Vector2(left, center.y), new Vector2(thickness, height), a, 0f);
        SetRect(borderRenderers[3], new Vector2(right, center.y), new Vector2(thickness, height), a, 0f);
    }

    private void DrawLabSweep(float width, float height, Vector2 center, float pulse, float envelope)
    {
        float speed = 0.28f;
        for (int i = 0; i < 6; i++)
        {
            float n = Mathf.Repeat((i / 6f) + Time.time * speed, 1f);
            float y = Mathf.Lerp(bottom, top, n);
            Color c = WithAlpha(i % 2 == 0 ? primary : secondary, Mathf.Lerp(0.08f, 0.26f, pulse) * envelope);
            SetRect(accentRenderers[i], new Vector2(center.x, y), new Vector2(width, 0.035f + i % 2 * 0.025f), c, 0f);
        }

        SetRect(sweepRenderer, new Vector2(center.x, Mathf.Lerp(bottom, top, Mathf.Repeat(Time.time * 0.18f, 1f))), new Vector2(width, 0.16f), WithAlpha(primary, 0.22f * envelope), 0f);
    }

    private void DrawLabGrid(float width, float height, Vector2 center, float pulse, float envelope)
    {
        for (int i = 0; i < 4; i++)
        {
            float x = Mathf.Lerp(left, right, (i + 1f) / 5f);
            float y = Mathf.Lerp(bottom, top, (i + 1f) / 5f);
            Color c = WithAlpha(i % 2 == 0 ? primary : secondary, Mathf.Lerp(0.13f, 0.34f, pulse) * envelope);
            SetRect(accentRenderers[i], new Vector2(x, center.y), new Vector2(0.045f, height), c, 0f);
            SetRect(accentRenderers[i + 4], new Vector2(center.x, y), new Vector2(width, 0.045f), c, 0f);
        }

        DrawCornerBrackets(width, height, 0.58f, WithAlpha(primary, 0.55f * envelope));
    }

    private void DrawContainment(float width, float height, Vector2 center, float pulse, float envelope)
    {
        float radius = Mathf.Min(width, height) * Mathf.Lerp(0.18f, 0.24f, pulse);
        SetCircle(coreRenderer, center, radius * 2f, WithAlpha(new Color(0.02f, 0.05f, 0.08f, 1f), 0.42f * envelope));
        for (int i = 0; i < 8; i++)
        {
            float angle = (Mathf.PI * 2f * i / 8f) + Time.time * 0.75f;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            SetRect(accentRenderers[i], center + dir * radius, new Vector2(0.08f, radius * 0.42f), WithAlpha(i % 2 == 0 ? primary : secondary, 0.66f * envelope), Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f);
        }
    }

    private void DrawConveyor(float width, float height, Vector2 center, float pulse, float envelope)
    {
        for (int i = 0; i < 12; i++)
        {
            float n = Mathf.Repeat(i / 12f + Time.time * 0.52f, 1f);
            bool topSide = i % 2 == 0;
            float x = Mathf.Lerp(left + 0.6f, right - 0.6f, n);
            float y = topSide ? top - 0.24f : bottom + 0.24f;
            Color c = WithAlpha(topSide ? primary : secondary, Mathf.Lerp(0.34f, 0.78f, pulse) * envelope);
            SetRect(accentRenderers[i], new Vector2(x, y), new Vector2(0.48f, 0.10f), c, 0f);
            SetRect(accentRenderers[i + 6 < accentRenderers.Length ? i + 6 : i], new Vector2(x + 0.24f, y), new Vector2(0.14f, 0.20f), c, 45f);
        }
    }

    private void DrawMagnet(float width, float height, Vector2 center, float pulse, float envelope)
    {
        float radiusA = Mathf.Min(width, height) * Mathf.Lerp(0.20f, 0.25f, pulse);
        float radiusB = radiusA * 0.62f;
        SetCircle(coreRenderer, center, radiusA * 2f, WithAlpha(primary, 0.13f * envelope));
        SetCircle(sweepRenderer, center, radiusB * 2f, WithAlpha(secondary, 0.18f * envelope));

        for (int i = 0; i < 10; i++)
        {
            float angle = Mathf.PI * 2f * i / 10f;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Color c = WithAlpha(i % 2 == 0 ? primary : secondary, 0.54f * envelope);
            SetRect(accentRenderers[i], center + dir * radiusA, new Vector2(0.34f, 0.08f), c, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        }
    }

    private void DrawCargo(float width, float height, Vector2 center, float pulse, float envelope)
    {
        Vector2[] positions =
        {
            new Vector2(left + width * 0.23f, top - height * 0.22f),
            new Vector2(right - width * 0.23f, top - height * 0.22f),
            new Vector2(left + width * 0.23f, bottom + height * 0.22f),
            new Vector2(right - width * 0.23f, bottom + height * 0.22f)
        };

        for (int i = 0; i < positions.Length; i++)
        {
            float wobble = Mathf.Sin(Time.time * 3.5f + i) * 0.08f;
            Color c = WithAlpha(i % 2 == 0 ? primary : secondary, Mathf.Lerp(0.30f, 0.62f, pulse) * envelope);
            SetRect(accentRenderers[i], positions[i] + Vector2.up * wobble, new Vector2(1.05f, 0.16f), c, 0f);
            SetRect(accentRenderers[i + 4], positions[i] + Vector2.down * (0.34f - wobble), new Vector2(0.82f, 0.16f), c, 0f);
            SetRect(accentRenderers[i + 8], positions[i] + Vector2.left * 0.46f, new Vector2(0.13f, 0.72f), c, 0f);
            SetRect(accentRenderers[i + 12], positions[i] + Vector2.right * 0.46f, new Vector2(0.13f, 0.72f), c, 0f);
        }
    }

    private void DrawRuptureSpin(float width, float height, Vector2 center, float pulse, float envelope)
    {
        float radius = Mathf.Min(width, height) * Mathf.Lerp(0.34f, 0.40f, pulse);
        SetCircle(coreRenderer, center, radius * 2f, WithAlpha(new Color(0.02f, 0.01f, 0.04f, 1f), 0.38f * envelope));
        for (int i = 0; i < 16; i++)
        {
            float angle = (Mathf.PI * 2f * i / 16f) + Time.time * 0.95f;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Color c = WithAlpha(i % 2 == 0 ? primary : secondary, Mathf.Lerp(0.34f, 0.82f, pulse) * envelope);
            SetRect(accentRenderers[i], center + dir * radius, new Vector2(0.46f, 0.07f), c, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f);
        }
    }

    private void DrawRuptureEcho(float width, float height, Vector2 center, float pulse, float envelope)
    {
        for (int i = 0; i < 4; i++)
        {
            float radius = Mathf.Min(width, height) * Mathf.Lerp(0.13f + i * 0.06f, 0.18f + i * 0.07f, pulse);
            SpriteRenderer r = i == 0 ? coreRenderer : accentRenderers[i - 1];
            SetCircle(r, center, radius * 2f, WithAlpha(i % 2 == 0 ? primary : secondary, Mathf.Lerp(0.10f, 0.24f, pulse) * envelope));
        }

        for (int i = 4; i < 14; i++)
        {
            float angle = (Mathf.PI * 2f * i / 10f) - Time.time * 0.72f;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float jitter = Mathf.Sin(Time.time * 18f + i) * 0.18f;
            SetRect(accentRenderers[i], center + dir * Mathf.Min(width, height) * (0.28f + jitter * 0.05f), new Vector2(0.24f, 0.055f), WithAlpha(primary, 0.58f * envelope), Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        }
    }

    private void DrawRupturePortal(float width, float height, Vector2 center, float pulse, float envelope)
    {
        Vector2 a = center + Vector2.left * width * 0.27f;
        Vector2 b = center + Vector2.right * width * 0.27f;
        float radius = Mathf.Min(width, height) * Mathf.Lerp(0.105f, 0.14f, pulse);
        SetCircle(coreRenderer, a, radius * 2f, WithAlpha(primary, 0.34f * envelope));
        SetCircle(sweepRenderer, b, radius * 2f, WithAlpha(secondary, 0.34f * envelope));
        SetRect(accentRenderers[0], center, new Vector2(Vector2.Distance(a, b), 0.045f), WithAlpha(primary, 0.28f * envelope), 0f);

        for (int i = 1; i < 13; i++)
        {
            bool aroundA = i % 2 == 0;
            Vector2 portalCenter = aroundA ? a : b;
            float angle = Mathf.PI * 2f * i / 12f + Time.time * (aroundA ? 1.1f : -1.1f);
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            SetRect(accentRenderers[i], portalCenter + dir * radius, new Vector2(0.28f, 0.055f), WithAlpha(aroundA ? primary : secondary, 0.76f * envelope), Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f);
        }
    }

    private void DrawCornerBrackets(float width, float height, float length, Color color)
    {
        Vector2[] corners =
        {
            new Vector2(left, top),
            new Vector2(right, top),
            new Vector2(left, bottom),
            new Vector2(right, bottom)
        };

        for (int i = 0; i < corners.Length; i++)
        {
            float xSign = corners[i].x < (left + right) * 0.5f ? 1f : -1f;
            float ySign = corners[i].y < (bottom + top) * 0.5f ? 1f : -1f;
            SetRect(accentRenderers[8 + i * 2], corners[i] + Vector2.right * xSign * length * 0.5f, new Vector2(length, 0.08f), color, 0f);
            SetRect(accentRenderers[9 + i * 2], corners[i] + Vector2.up * ySign * length * 0.5f, new Vector2(0.08f, length), color, 0f);
        }
    }

    private void HideAccents()
    {
        for (int i = 0; i < accentRenderers.Length; i++)
        {
            if (accentRenderers[i] != null)
            {
                accentRenderers[i].color = Color.clear;
            }
        }

        if (coreRenderer != null)
        {
            coreRenderer.color = Color.clear;
        }

        if (sweepRenderer != null)
        {
            sweepRenderer.color = Color.clear;
        }
    }

    private static void SetRect(SpriteRenderer renderer, Vector2 position, Vector2 size, Color color, float rotationZ)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.sprite = SquareSpriteProvider.Get();
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.transform.position = new Vector3(position.x, position.y, 0f);
        renderer.transform.rotation = Quaternion.Euler(0f, 0f, rotationZ);
        renderer.size = new Vector2(Mathf.Max(0.02f, size.x), Mathf.Max(0.02f, size.y));
        renderer.color = color;
    }

    private static void SetCircle(SpriteRenderer renderer, Vector2 position, float diameter, Color color)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.sprite = CircleSpriteProvider.Get();
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.transform.position = new Vector3(position.x, position.y, 0f);
        renderer.transform.rotation = Quaternion.identity;
        renderer.size = Vector2.one * Mathf.Max(0.05f, diameter);
        renderer.color = color;
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }
}

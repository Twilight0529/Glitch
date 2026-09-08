using UnityEngine;

// Genera siluetas de props redondeados por sector. Son sprites transparentes y
// reutilizables: sustituyen el circulo genérico sin modificar los colliders.
public static class ThemedPropSpriteProvider
{
    private enum PropStyle
    {
        LabPod,
        StorageDrum,
        RuptureShard,
        CoreCapacitor,
        ArchiveSeal
    }

    private const int TextureSize = 128;
    private static Sprite labPod;
    private static Sprite storageDrum;
    private static Sprite ruptureShard;
    private static Sprite coreCapacitor;
    private static Sprite archiveSeal;

    private static readonly Vector2[] LabPodShape =
    {
        new Vector2(-0.56f, 0.84f), new Vector2(0.56f, 0.84f),
        new Vector2(0.84f, 0.56f), new Vector2(0.84f, -0.56f),
        new Vector2(0.56f, -0.84f), new Vector2(-0.56f, -0.84f),
        new Vector2(-0.84f, -0.56f), new Vector2(-0.84f, 0.56f)
    };

    private static readonly Vector2[] StorageDrumShape =
    {
        new Vector2(-0.48f, 0.86f), new Vector2(0.48f, 0.86f),
        new Vector2(0.76f, 0.70f), new Vector2(0.86f, 0.28f),
        new Vector2(0.86f, -0.28f), new Vector2(0.76f, -0.70f),
        new Vector2(0.48f, -0.86f), new Vector2(-0.48f, -0.86f),
        new Vector2(-0.76f, -0.70f), new Vector2(-0.86f, -0.28f),
        new Vector2(-0.86f, 0.28f), new Vector2(-0.76f, 0.70f)
    };

    private static readonly Vector2[] RuptureShardShape =
    {
        new Vector2(-0.20f, 0.90f), new Vector2(0.38f, 0.78f),
        new Vector2(0.76f, 0.50f), new Vector2(0.88f, -0.10f),
        new Vector2(0.56f, -0.72f), new Vector2(0.02f, -0.88f),
        new Vector2(-0.34f, -0.70f), new Vector2(-0.82f, -0.46f),
        new Vector2(-0.72f, 0.08f), new Vector2(-0.52f, 0.66f)
    };

    private static readonly Vector2[] CoreCapacitorShape =
    {
        new Vector2(-0.34f, 0.88f), new Vector2(0.34f, 0.88f),
        new Vector2(0.44f, 0.68f), new Vector2(0.72f, 0.68f),
        new Vector2(0.88f, 0.34f), new Vector2(0.88f, -0.34f),
        new Vector2(0.72f, -0.68f), new Vector2(0.44f, -0.68f),
        new Vector2(0.34f, -0.88f), new Vector2(-0.34f, -0.88f),
        new Vector2(-0.44f, -0.68f), new Vector2(-0.72f, -0.68f),
        new Vector2(-0.88f, -0.34f), new Vector2(-0.88f, 0.34f),
        new Vector2(-0.72f, 0.68f), new Vector2(-0.44f, 0.68f)
    };

    private static readonly Vector2[] ArchiveSealShape =
    {
        new Vector2(0f, 0.90f), new Vector2(0.58f, 0.62f),
        new Vector2(0.84f, 0.18f), new Vector2(0.84f, -0.18f),
        new Vector2(0.58f, -0.62f), new Vector2(0f, -0.90f),
        new Vector2(-0.58f, -0.62f), new Vector2(-0.84f, -0.18f),
        new Vector2(-0.84f, 0.18f), new Vector2(-0.58f, 0.62f)
    };

    public static Sprite Get(ProceduralArenaGenerator.ArenaTheme theme)
    {
        switch (theme)
        {
            case ProceduralArenaGenerator.ArenaTheme.StorageBay:
                return storageDrum != null ? storageDrum : storageDrum = Create(PropStyle.StorageDrum, StorageDrumShape, "StorageDrumProp");
            case ProceduralArenaGenerator.ArenaTheme.RuptureZone:
                return ruptureShard != null ? ruptureShard : ruptureShard = Create(PropStyle.RuptureShard, RuptureShardShape, "RuptureShardProp");
            case ProceduralArenaGenerator.ArenaTheme.DataCore:
                return coreCapacitor != null ? coreCapacitor : coreCapacitor = Create(PropStyle.CoreCapacitor, CoreCapacitorShape, "CoreCapacitorProp");
            case ProceduralArenaGenerator.ArenaTheme.NullArchive:
                return archiveSeal != null ? archiveSeal : archiveSeal = Create(PropStyle.ArchiveSeal, ArchiveSealShape, "ArchiveSealProp");
            default:
                return labPod != null ? labPod : labPod = Create(PropStyle.LabPod, LabPodShape, "LabContainmentPodProp");
        }
    }

    public static void Warmup()
    {
        _ = Get(ProceduralArenaGenerator.ArenaTheme.ContainmentLab);
        _ = Get(ProceduralArenaGenerator.ArenaTheme.StorageBay);
        _ = Get(ProceduralArenaGenerator.ArenaTheme.RuptureZone);
        _ = Get(ProceduralArenaGenerator.ArenaTheme.DataCore);
        _ = Get(ProceduralArenaGenerator.ArenaTheme.NullArchive);
    }

    private static Sprite Create(PropStyle style, Vector2[] shape, string spriteName)
    {
        Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
        {
            name = spriteName + "Texture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] pixels = new Color[TextureSize * TextureSize];
        float pixelWidth = 2f / TextureSize;
        for (int y = 0; y < TextureSize; y++)
        {
            for (int x = 0; x < TextureSize; x++)
            {
                Vector2 point = new Vector2(
                    ((x + 0.5f) / TextureSize) * 2f - 1f,
                    ((y + 0.5f) / TextureSize) * 2f - 1f);
                float signedDistance = SignedDistanceToPolygon(point, shape);
                float coverage = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-pixelWidth, pixelWidth, signedDistance));
                pixels[y * TextureSize + x] = coverage <= 0f
                    ? Color.clear
                    : ShadePixel(style, point, signedDistance, coverage);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, false);

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, TextureSize, TextureSize),
            Vector2.one * 0.5f,
            TextureSize,
            0,
            SpriteMeshType.FullRect);
        sprite.name = spriteName;
        return sprite;
    }

    private static Color ShadePixel(PropStyle style, Vector2 p, float edgeDistance, float alpha)
    {
        GetPalette(style, out Color shadow, out Color material, out Color highlight, out Color signal);
        float lightDirection = Mathf.Clamp01(0.48f + (-p.x + p.y) * 0.18f);
        Color color = Color.Lerp(shadow, material, 0.58f + lightDirection * 0.28f);

        if (edgeDistance < 0.045f)
        {
            color = Color.Lerp(new Color(0.012f, 0.018f, 0.028f, 1f), highlight, lightDirection * 0.55f);
        }
        else if (edgeDistance < 0.115f)
        {
            color = Color.Lerp(material, highlight, 0.28f + lightDirection * 0.36f);
        }

        ApplyStyleDetails(style, p, signal, ref color);
        color.a = alpha;
        return color;
    }

    private static void ApplyStyleDetails(PropStyle style, Vector2 p, Color signal, ref Color color)
    {
        float ax = Mathf.Abs(p.x);
        float ay = Mathf.Abs(p.y);
        switch (style)
        {
            case PropStyle.LabPod:
                if (ax < 0.13f && ay < 0.53f)
                {
                    color = Color.Lerp(new Color(0.05f, 0.16f, 0.20f, 1f), signal, 0.62f);
                }
                else if (Mathf.Abs(ax - 0.48f) < 0.035f && ay < 0.50f)
                {
                    color = Color.Lerp(color, signal, 0.34f);
                }
                break;

            case PropStyle.StorageDrum:
                if (Mathf.Abs(ay - 0.40f) < 0.075f || Mathf.Abs(ax - 0.58f) < 0.045f)
                {
                    color = Color.Lerp(color, signal, 0.58f);
                }
                else if (ax < 0.30f && ay < 0.16f)
                {
                    color *= 0.72f;
                    color.a = 1f;
                }
                break;

            case PropStyle.RuptureShard:
                float crackA = Mathf.Abs(p.y - p.x * 0.38f - 0.04f);
                float crackB = Mathf.Abs(p.x + p.y * 0.52f + 0.18f);
                if ((crackA < 0.034f && p.x > -0.56f) || (crackB < 0.026f && p.y < 0.32f))
                {
                    color = Color.Lerp(new Color(0.015f, 0.004f, 0.026f, 1f), signal, 0.72f);
                }
                break;

            case PropStyle.CoreCapacitor:
                if ((ax < 0.055f && ay < 0.62f) || (ay < 0.055f && ax < 0.62f))
                {
                    color = Color.Lerp(color, signal, 0.78f);
                }
                else if ((Mathf.Abs(ax - 0.44f) < 0.05f && ay < 0.24f) ||
                         (Mathf.Abs(ay - 0.44f) < 0.05f && ax < 0.24f))
                {
                    color = Color.Lerp(color, signal, 0.48f);
                }
                break;

            case PropStyle.ArchiveSeal:
                float diamond = ax + ay;
                if (Mathf.Abs(diamond - 0.50f) < 0.035f ||
                    (ax < 0.055f && ay > 0.24f && ay < 0.58f))
                {
                    color = Color.Lerp(color, signal, 0.58f);
                }
                else if (diamond < 0.22f)
                {
                    color *= 0.68f;
                    color.a = 1f;
                }
                break;
        }
    }

    private static void GetPalette(PropStyle style, out Color shadow, out Color material, out Color highlight, out Color signal)
    {
        shadow = new Color(0.025f, 0.038f, 0.058f, 1f);
        material = new Color(0.16f, 0.22f, 0.30f, 1f);
        highlight = new Color(0.38f, 0.48f, 0.60f, 1f);
        signal = new Color(0.28f, 0.90f, 1f, 1f);

        switch (style)
        {
            case PropStyle.StorageDrum:
                material = new Color(0.26f, 0.23f, 0.18f, 1f);
                highlight = new Color(0.48f, 0.40f, 0.28f, 1f);
                signal = new Color(1f, 0.62f, 0.16f, 1f);
                break;
            case PropStyle.RuptureShard:
                material = new Color(0.20f, 0.14f, 0.27f, 1f);
                highlight = new Color(0.42f, 0.27f, 0.52f, 1f);
                signal = new Color(1f, 0.18f, 0.76f, 1f);
                break;
            case PropStyle.CoreCapacitor:
                material = new Color(0.08f, 0.25f, 0.22f, 1f);
                highlight = new Color(0.18f, 0.48f, 0.38f, 1f);
                signal = new Color(0.30f, 1f, 0.42f, 1f);
                break;
            case PropStyle.ArchiveSeal:
                material = new Color(0.14f, 0.14f, 0.24f, 1f);
                highlight = new Color(0.30f, 0.31f, 0.46f, 1f);
                signal = new Color(0.70f, 0.56f, 1f, 1f);
                break;
        }
    }

    private static float SignedDistanceToPolygon(Vector2 point, Vector2[] vertices)
    {
        bool inside = false;
        float minDistanceSqr = float.MaxValue;
        int previous = vertices.Length - 1;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector2 a = vertices[previous];
            Vector2 b = vertices[i];
            Vector2 segment = b - a;
            float lengthSqr = Mathf.Max(0.000001f, segment.sqrMagnitude);
            float t = Mathf.Clamp01(Vector2.Dot(point - a, segment) / lengthSqr);
            minDistanceSqr = Mathf.Min(minDistanceSqr, (point - (a + segment * t)).sqrMagnitude);

            if ((b.y > point.y) != (a.y > point.y) &&
                point.x < (a.x - b.x) * (point.y - b.y) / (a.y - b.y) + b.x)
            {
                inside = !inside;
            }

            previous = i;
        }

        float distance = Mathf.Sqrt(minDistanceSqr);
        return inside ? distance : -distance;
    }
}

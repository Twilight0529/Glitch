using UnityEngine;

// Silueta procedural del jugador: un nucleo de contencion angular y tecnologico.
// Sus cuadros solo alteran los biseles unos pocos pixeles; la hitbox permanece intacta.
public static class PlayerAvatarSpriteProvider
{
    private const int FrameCount = 8;
    private static readonly Sprite[] cachedFrames = new Sprite[FrameCount];

    public static Sprite Get()
    {
        return GetFrame(0f);
    }

    public static Sprite GetFrame(float phase)
    {
        int frame = Mathf.FloorToInt(Mathf.Repeat(phase, Mathf.PI * 2f) / (Mathf.PI * 2f) * FrameCount) % FrameCount;
        if (cachedFrames[frame] == null)
        {
            cachedFrames[frame] = BuildFrame(frame);
        }

        return cachedFrames[frame];
    }

    public static void Warmup()
    {
        for (int i = 0; i < FrameCount; i++)
        {
            GetFrame(i / (float)FrameCount * Mathf.PI * 2f);
        }
    }

    private static Sprite BuildFrame(int frame)
    {
        const int size = 96;
        float phase = frame / (float)FrameCount * Mathf.PI * 2f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = $"PlayerContainmentCoreTexture_{frame:00}"
        };

        const int samples = 3;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float coverage = 0f;
                for (int sy = 0; sy < samples; sy++)
                {
                    for (int sx = 0; sx < samples; sx++)
                    {
                        float px = ((x + (sx + 0.5f) / samples) / size) * 2f - 1f;
                        float py = ((y + (sy + 0.5f) / samples) / size) * 2f - 1f;
                        coverage += IsInside(px, py, phase) ? 1f : 0f;
                    }
                }

                coverage /= samples * samples;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, coverage));
            }
        }

        texture.Apply();
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            size,
            0,
            SpriteMeshType.Tight);
        sprite.name = $"PlayerContainmentCore_{frame:00}";
        return sprite;
    }

    private static bool IsInside(float x, float y, float phase)
    {
        float ax = Mathf.Abs(x);
        float ay = Mathf.Abs(y);
        float breathe = Mathf.Sin(phase) * 0.014f;
        float bevelShift = Mathf.Sin(phase * 2f) * 0.018f;

        bool insideCore = ax <= 0.82f + breathe &&
                          ay <= 0.82f - breathe * 0.55f &&
                          ax + ay <= 1.34f + bevelShift;

        // Puertos laterales negativos rompen la lectura de cuadrado sin volver fragil la silueta.
        float portHalfHeight = 0.105f + Mathf.Cos(phase) * 0.012f;
        bool sidePort = ax > 0.69f && ay < portHalfHeight;
        bool cornerKey = ax > 0.63f && ay > 0.63f && ax + ay > 1.39f;
        return insideCore && !sidePort && !cornerKey;
    }
}

// Anillo con cuatro interrupciones. Sirve para que defensa y parry rodeen al
// personaje sin tapar su silueta ni confundirse con una zona de peligro llena.
public static class ContainmentRingSpriteProvider
{
    private static Sprite cached;

    public static Sprite Get()
    {
        if (cached != null)
        {
            return cached;
        }

        const int size = 96;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "PlayerMechanicRingTexture"
        };

        Vector2 center = Vector2.one * ((size - 1) * 0.5f);
        float outerRadius = size * 0.47f;
        float innerRadius = size * 0.385f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 delta = new Vector2(x, y) - center;
                float distance = delta.magnitude;
                float angle = Mathf.Atan2(delta.y, delta.x);
                float cardinal = Mathf.Abs(Mathf.Sin(angle * 2f));
                bool gap = cardinal < 0.13f;
                float outerAlpha = Mathf.Clamp01(outerRadius - distance + 0.8f);
                float innerAlpha = Mathf.Clamp01(distance - innerRadius + 0.8f);
                float alpha = gap ? 0f : Mathf.Min(outerAlpha, innerAlpha);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        cached = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.Tight);
        cached.name = "PlayerMechanicRing";
        return cached;
    }
}

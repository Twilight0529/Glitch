using UnityEngine;

// Silueta unica y compacta para la anomalia. Evita construir el cuerpo con
// circulos superpuestos y conserva un borde organico aun cuando no se anima.
public static class CompactSlimeSpriteProvider
{
    private const int FrameCount = 12;
    private static readonly Sprite[] cachedFrames = new Sprite[FrameCount];

    public static Sprite Get()
    {
        return GetFrame(0f);
    }

    public static void Warmup()
    {
        for (int i = 0; i < FrameCount; i++)
        {
            GetFrame(i / (float)FrameCount * Mathf.PI * 2f);
        }
    }

    public static Sprite GetFrame(float phase)
    {
        float normalized = Mathf.Repeat(phase, Mathf.PI * 2f) / (Mathf.PI * 2f);
        int frame = Mathf.FloorToInt(normalized * FrameCount) % FrameCount;
        if (cachedFrames[frame] != null)
        {
            return cachedFrames[frame];
        }

        cachedFrames[frame] = BuildFrame(frame);
        return cachedFrames[frame];
    }

    private static Sprite BuildFrame(int frame)
    {
        const int size = 96;
        float phase = frame / (float)FrameCount * Mathf.PI * 2f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = $"CompactSlimeSpriteTexture_{frame:00}"
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
            new Vector2(0.5f, 0.46f),
            size,
            0,
            SpriteMeshType.Tight);
        sprite.name = $"CompactSlimeSprite_{frame:00}";
        return sprite;
    }

    private static bool IsInside(float x, float y, float phase)
    {
        // Cada cuadro redistribuye el volumen: la cupula se inclina, los lados
        // respiran y la base avanza como una onda. No cambia la altura total.
        float normalizedY = Mathf.Clamp((y + 0.70f) / 1.58f, 0f, 1f);
        float dome = Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((y + 0.02f) / 0.91f, 2f)));
        float lowerMass = Mathf.SmoothStep(0.12f, 0f, normalizedY) * 0.13f;
        float sideBreath = Mathf.Sin(phase * 2f + y * 4.6f) * 0.052f;
        float width = dome * (0.82f + sideBreath) + lowerMass;
        float topLean = Mathf.Sin(phase) * normalizedY * normalizedY * 0.125f;
        float surfaceRipple = 0.032f * Mathf.Sin(y * 8.2f + phase) + 0.019f * Mathf.Sin(y * 15.7f - phase * 1.4f);
        float center = topLean + surfaceRipple;
        float baseLine = -0.69f
            + 0.075f * Mathf.Sin((x + 0.12f) * 10f - phase * 2f)
            + 0.018f * Mathf.Sin(x * 23f + phase);
        bool inMainMass = y >= baseLine && y <= 0.88f && Mathf.Abs(x - center) <= width;

        // Dos apoyos inferiores apenas marcados dan sensacion viscosa sin
        // convertir la figura en una coleccion de burbujas.
        float footShift = Mathf.Sin(phase) * 0.070f;
        bool leftFoot = y >= -0.75f && y <= -0.61f && Mathf.Abs(x + 0.43f - footShift) <= 0.22f;
        bool rightFoot = y >= -0.73f && y <= -0.61f && Mathf.Abs(x - 0.38f - footShift) <= 0.25f;
        return inMainMass || leftFoot || rightFoot;
    }
}

// Ojo afilado de una sola pieza. El segundo ojo reutiliza el sprite espejado.
public static class SlimeEyeSpriteProvider
{
    private static Sprite cached;

    public static Sprite Get()
    {
        if (cached != null)
        {
            return cached;
        }

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "SlimeEyeSpriteTexture"
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
                        float px = (x + (sx + 0.5f) / samples) / size;
                        float py = (y + (sy + 0.5f) / samples) / size;
                        float lower = Mathf.Lerp(0.24f, 0.46f, px);
                        float upper = Mathf.Lerp(0.78f, 0.54f, px);
                        if (px >= 0.08f && px <= 0.94f && py >= lower && py <= upper)
                        {
                            coverage += 1f;
                        }
                    }
                }

                coverage /= samples * samples;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, coverage));
            }
        }

        texture.Apply();
        cached = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.Tight);
        cached.name = "SlimeEyeSprite";
        return cached;
    }
}

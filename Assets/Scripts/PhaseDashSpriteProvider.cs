using UnityEngine;

public static class PhaseDashSpriteProvider
{
    // Portal fracturado con doble chevron: comunica avance y atravesamiento sin reutilizar otro powerup.
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
            name = "PhaseDashSpriteTexture"
        };

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float coverage = 0f;
                const int samples = 3;
                for (int sy = 0; sy < samples; sy++)
                {
                    for (int sx = 0; sx < samples; sx++)
                    {
                        Vector2 p = new Vector2(x + (sx + 0.5f) / samples, y + (sy + 0.5f) / samples);
                        if (IsFilled(p)) coverage += 1f;
                    }
                }
                coverage /= samples * samples;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, coverage));
            }
        }

        texture.Apply();
        cached = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 96f, 0, SpriteMeshType.Tight);
        cached.name = "PhaseDashSprite";
        return cached;
    }

    private static bool IsFilled(Vector2 p)
    {
        Vector2 centered = p - new Vector2(31.5f, 31.5f);
        float diamond = Mathf.Abs(centered.x) + Mathf.Abs(centered.y);
        bool brokenPortal = diamond <= 27f && diamond >= 18f &&
                            !(centered.x < -7f && Mathf.Abs(centered.y) < 5f);

        bool frontChevron = Chevron(p, 22f, 32f, 15f, 6f);
        bool rearChevron = Chevron(p, 10f, 32f, 11f, 4.5f);
        bool phaseCore = centered.sqrMagnitude <= 19f;
        return brokenPortal || frontChevron || rearChevron || phaseCore;
    }

    private static bool Chevron(Vector2 p, float centerX, float centerY, float height, float thickness)
    {
        float localX = p.x - centerX;
        float localY = Mathf.Abs(p.y - centerY);
        float ridge = localX + height - localY;
        return localX >= -height && localX <= height && ridge >= 0f && ridge <= thickness;
    }
}

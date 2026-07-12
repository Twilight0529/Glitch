using UnityEngine;

public static class PhaseDashSpriteProvider
{
    // Cuerpo, ecos y una barrera atravesada: la silueta explica la mecanica sin depender del nombre.
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
                        coverage += GetCoverage(p);
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

    private static float GetCoverage(Vector2 point)
    {
        // La pared queda visible por debajo de la trayectoria que la cruza.
        if (point.x >= 29f && point.x <= 35f && point.y >= 6f && point.y <= 58f)
        {
            return 0.38f;
        }

        if (point.x >= 9f && point.x <= 48f && Mathf.Abs(point.y - 32f) <= 2.2f)
        {
            return 0.70f;
        }

        float ghostRear = Mathf.Abs(point.x - 14f) + Mathf.Abs(point.y - 32f);
        if (ghostRear <= 5.5f)
        {
            return 0.34f;
        }

        float ghostFront = Mathf.Abs(point.x - 23f) + Mathf.Abs(point.y - 32f);
        if (ghostFront <= 6.5f)
        {
            return 0.58f;
        }

        float player = Mathf.Abs(point.x - 46f) + Mathf.Abs(point.y - 32f);
        if (player <= 9f)
        {
            return 1f;
        }

        // Una punta corta fija la direccion sin competir con la silueta del jugador.
        bool directionTip = point.x >= 50f && point.x <= 59f &&
                            Mathf.Abs(point.y - 32f) <= (59f - point.x) * 0.62f;
        return directionTip ? 0.94f : 0f;
    }
}

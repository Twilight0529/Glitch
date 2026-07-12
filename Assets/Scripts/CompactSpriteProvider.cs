using UnityEngine;

public static class CompactSpriteProvider
{
    // Un marco grande, cuatro flechas y un nucleo chico explican la reduccion incluso a escala de HUD.
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
            name = "CompactSpriteTexture"
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
                        Vector2 point = new Vector2(
                            x + (sx + 0.5f) / samples,
                            y + (sy + 0.5f) / samples);
                        coverage += GetCoverage(point);
                    }
                }

                coverage /= samples * samples;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, coverage));
            }
        }

        texture.Apply();

        cached = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            96f,
            0,
            SpriteMeshType.Tight);

        cached.name = "CompactSprite";
        return cached;
    }

    private static float GetCoverage(Vector2 point)
    {
        Vector2 centered = point - new Vector2(32f, 32f);
        float ax = Mathf.Abs(centered.x);
        float ay = Mathf.Abs(centered.y);

        // El contorno conserva el tamano original como referencia visual.
        float outer = Mathf.Max(ax, ay);
        if (outer >= 24.5f && outer <= 27.5f)
        {
            return 0.42f;
        }

        // El resultado compacto siempre es la masa visual de mayor jerarquia.
        if (ax <= 8.5f && ay <= 8.5f)
        {
            return 1f;
        }

        bool leftArrow = InHorizontalInwardArrow(point, 8f, 22f, true);
        bool rightArrow = InHorizontalInwardArrow(point, 42f, 56f, false);
        bool bottomArrow = InVerticalInwardArrow(point, 8f, 22f, true);
        bool topArrow = InVerticalInwardArrow(point, 42f, 56f, false);
        return leftArrow || rightArrow || bottomArrow || topArrow ? 0.92f : 0f;
    }

    private static bool InHorizontalInwardArrow(Vector2 point, float minX, float maxX, bool pointsRight)
    {
        if (point.x < minX || point.x > maxX)
        {
            return false;
        }

        float progress = Mathf.InverseLerp(minX, maxX, point.x);
        float distanceFromTip = pointsRight ? 1f - progress : progress;
        bool head = Mathf.Abs(point.y - 32f) <= distanceFromTip * 7.5f;
        bool shaft = Mathf.Abs(point.y - 32f) <= 2.1f;
        return head || shaft;
    }

    private static bool InVerticalInwardArrow(Vector2 point, float minY, float maxY, bool pointsUp)
    {
        if (point.y < minY || point.y > maxY)
        {
            return false;
        }

        float progress = Mathf.InverseLerp(minY, maxY, point.y);
        float distanceFromTip = pointsUp ? 1f - progress : progress;
        bool head = Mathf.Abs(point.x - 32f) <= distanceFromTip * 7.5f;
        bool shaft = Mathf.Abs(point.x - 32f) <= 2.1f;
        return head || shaft;
    }
}

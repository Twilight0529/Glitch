using UnityEngine;

public static class LightningSpriteProvider
{
    // Genera y reutiliza una silueta de rayo para distinguir el powerup de velocidad.
    private static Sprite cached;

    public static Sprite Get()
    {
        if (cached != null)
        {
            return cached;
        }

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.name = "LightningSpriteTexture";

        Vector2[] points =
        {
            new Vector2(38f, 61f),
            new Vector2(18f, 31f),
            new Vector2(31f, 31f),
            new Vector2(25f, 3f),
            new Vector2(49f, 38f),
            new Vector2(35f, 38f)
        };

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color fill = Color.white;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 pixel = new Vector2(x + 0.5f, y + 0.5f);
                texture.SetPixel(x, y, IsInsidePolygon(pixel, points) ? fill : clear);
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

        cached.name = "LightningSprite";
        return cached;
    }

    private static bool IsInsidePolygon(Vector2 pixel, Vector2[] points)
    {
        bool inside = false;
        int last = points.Length - 1;

        for (int current = 0; current < points.Length; current++)
        {
            Vector2 a = points[current];
            Vector2 b = points[last];
            bool crossesVerticalRange = (a.y > pixel.y) != (b.y > pixel.y);
            if (crossesVerticalRange)
            {
                float intersectionX = (b.x - a.x) * (pixel.y - a.y) / (b.y - a.y) + a.x;
                if (pixel.x < intersectionX)
                {
                    inside = !inside;
                }
            }

            last = current;
        }

        return inside;
    }
}

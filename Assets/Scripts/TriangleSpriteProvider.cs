using UnityEngine;

public static class TriangleSpriteProvider
{
    private static Sprite cached;

    public static Sprite Get()
    {
        if (cached != null) return cached;

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "TriangleSpriteTexture"
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
                        float px = x + (sx + 0.5f) / samples;
                        float py = y + (sy + 0.5f) / samples;
                        float halfWidth = Mathf.Lerp(25f, 0f, Mathf.InverseLerp(8f, 58f, py));
                        if (py >= 8f && py <= 58f && Mathf.Abs(px - 31.5f) <= halfWidth) coverage += 1f;
                    }
                }
                coverage /= samples * samples;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, coverage));
            }
        }

        texture.Apply();
        cached = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.16f), 96f, 0, SpriteMeshType.Tight);
        cached.name = "TriangleSprite";
        return cached;
    }
}

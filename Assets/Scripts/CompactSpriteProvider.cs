using UnityEngine;

public static class CompactSpriteProvider
{
    // Genera y reutiliza un icono de compresion para el powerup que reduce al jugador.
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
        texture.name = "CompactSpriteTexture";

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color fill = Color.white;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                texture.SetPixel(x, y, IsFilled(x, y) ? fill : clear);
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

    private static bool IsFilled(int x, int y)
    {
        float dx = x - 31.5f;
        float dy = y - 31.5f;
        float distance = Mathf.Sqrt(dx * dx + dy * dy);

        bool core = distance <= 8.5f;
        bool innerRing = distance >= 16f && distance <= 19f && Mathf.Abs(dx) + Mathf.Abs(dy) < 34f;

        bool leftArrow = x >= 8 && x <= 25 && Mathf.Abs(y - 32) <= Mathf.Lerp(9f, 2f, Mathf.InverseLerp(8f, 25f, x));
        bool rightArrow = x >= 38 && x <= 55 && Mathf.Abs(y - 32) <= Mathf.Lerp(2f, 9f, Mathf.InverseLerp(38f, 55f, x));
        bool topArrow = y >= 38 && y <= 55 && Mathf.Abs(x - 32) <= Mathf.Lerp(2f, 9f, Mathf.InverseLerp(38f, 55f, y));
        bool bottomArrow = y >= 8 && y <= 25 && Mathf.Abs(x - 32) <= Mathf.Lerp(9f, 2f, Mathf.InverseLerp(8f, 25f, y));

        return core || innerRing || leftArrow || rightArrow || topArrow || bottomArrow;
    }
}

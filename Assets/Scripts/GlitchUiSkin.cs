using UnityEngine;

// Presentation only: layout, input, icons and semantic colors stay with callers.
public static class GlitchUiSkin
{
    public static readonly Color Surface = new Color(0.025f, 0.040f, 0.065f, 0.98f);
    public static readonly Color Raised = new Color(0.040f, 0.060f, 0.090f, 0.98f);
    public static readonly Color Control = new Color(0.050f, 0.075f, 0.105f, 1f);
    public static readonly Color Border = new Color(0.18f, 0.24f, 0.31f, 0.72f);
    public static readonly Color Text = new Color(0.90f, 0.93f, 0.98f, 1f);
    public static readonly Color SecondaryText = new Color(0.60f, 0.68f, 0.77f, 1f);
    public const int BodySize = 16;
    public const int ControlSize = 18;

    public static void Panel(Rect rect, Color tint, Color accent, bool shadow = true)
    {
        if (shadow)
        {
            Fill(new Rect(rect.x - 3f, rect.y + 5f, rect.width + 6f, rect.height + 3f), new Color(0f, 0f, 0f, 0.20f));
            Fill(new Rect(rect.x - 1f, rect.y + 2f, rect.width + 2f, rect.height + 1f), new Color(0f, 0f, 0f, 0.18f));
        }
        Fill(rect, Color.Lerp(Surface, new Color(tint.r, tint.g, tint.b, 0.98f), 0.12f));
        Frame(rect, Border);
        Fill(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, 1f), new Color(1f, 1f, 1f, 0.045f));
        Fill(new Rect(rect.x + 1f, rect.y, Mathf.Min(48f, rect.width - 2f), 2f), new Color(accent.r, accent.g, accent.b, 0.64f));
    }

    public static void Button(Rect rect, Color accent, float hover, bool primary, bool enabled)
    {
        hover = enabled ? Mathf.Clamp01(hover) : 0f;
        Color fill = Color.Lerp(Control, Raised, primary ? 0f : 0.55f);
        Fill(rect, Color.Lerp(fill, new Color(accent.r, accent.g, accent.b, 1f), (primary ? 0.09f : 0.02f) + hover * 0.07f));
        Frame(rect, Color.Lerp(Border, new Color(accent.r, accent.g, accent.b, 0.72f), hover * 0.80f));
        if (primary || hover > 0.01f)
            Fill(new Rect(rect.x, rect.y, 2f, rect.height), new Color(accent.r, accent.g, accent.b, primary ? 0.76f : hover * 0.70f));
        Fill(new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, 1f), new Color(1f, 1f, 1f, 0.035f));
        if (!enabled) Fill(rect, new Color(0f, 0f, 0f, 0.24f));
    }

    public static void Accent(Rect rect, Color accent)
    {
        Fill(new Rect(rect.x + 16f, rect.y + 16f, 20f, 1f), new Color(accent.r, accent.g, accent.b, 0.22f));
    }

    public static void Frame(Rect rect, Color color)
    {
        Fill(new Rect(rect.x, rect.y, rect.width, 1f), color);
        Fill(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), color);
        Fill(new Rect(rect.x, rect.y, 1f, rect.height), color);
        Fill(new Rect(rect.xMax - 1f, rect.y, 1f, rect.height), color);
    }

    private static void Fill(Rect rect, Color color)
    {
        if (rect.width <= 0f || rect.height <= 0f) return;
        Color old = GUI.color;
        GUI.color = old * color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = old;
    }
}

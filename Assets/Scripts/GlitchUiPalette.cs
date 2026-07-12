using UnityEngine;

// Paleta semantica compartida: el color explica la funcion antes de decorar la interfaz.
public static class GlitchUiPalette
{
    public static readonly Color Information = new Color(0.34f, 0.86f, 1f, 1f);
    public static readonly Color Alert = new Color(1f, 0.78f, 0.24f, 1f);
    public static readonly Color Danger = new Color(1f, 0.28f, 0.40f, 1f);
    public static readonly Color Success = new Color(0.38f, 1f, 0.66f, 1f);
    public static readonly Color Special = new Color(0.72f, 0.54f, 1f, 1f);
    public static readonly Color Neutral = new Color(0.84f, 0.90f, 1f, 1f);
    public static readonly Color Surface = new Color(0.018f, 0.030f, 0.060f, 1f);

    public static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }
}

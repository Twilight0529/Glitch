using System;
using UnityEditor;
using UnityEngine;

public static class UiSkinValidation
{
    [MenuItem("GLITCH/Validation/UI surface contrast")]
    public static void Run() => Debug.Log($"UI surface contrast: {Validate()} checks passed.");

    public static int Validate()
    {
        Check(GlitchUiSkin.Surface.a >= 0.95f, "reading surface blocks background noise");
        Check(Contrast(GlitchUiSkin.Text, GlitchUiSkin.Surface) >= 7f, "primary text on panel");
        Check(Contrast(GlitchUiSkin.SecondaryText, GlitchUiSkin.Surface) >= 4.5f, "secondary text on panel");
        Check(Contrast(GlitchUiSkin.Text, GlitchUiSkin.Raised) >= 7f, "primary text on card");
        Check(Contrast(GlitchUiSkin.SecondaryText, GlitchUiSkin.Raised) >= 4.5f, "secondary text on card");
        Check(Contrast(GlitchUiSkin.Text, GlitchUiSkin.Control) >= 7f, "control text");
        Check(Contrast(GlitchUiPalette.Information, GlitchUiSkin.Control) >= 4.5f, "information accent");
        Check(Contrast(GlitchUiPalette.Danger, GlitchUiSkin.Surface) >= 4.5f, "danger text remains legible");
        Check(Contrast(GlitchUiPalette.Alert, GlitchUiSkin.Surface) >= 7f, "warning text remains legible");
        Check(Contrast(GlitchUiPalette.Special, GlitchUiSkin.Surface) >= 4.5f, "special text remains legible");
        Check(GlitchUiSkin.BodySize >= 16 && GlitchUiSkin.ControlSize >= 18, "reading and action type hierarchy");
        return 11;
    }

    private static double Linear(float value) => value <= 0.04045f ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    private static double Luminance(Color color) => 0.2126 * Linear(color.r) + 0.7152 * Linear(color.g) + 0.0722 * Linear(color.b);
    private static double Contrast(Color a, Color b)
    {
        double x = Luminance(a), y = Luminance(b);
        return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05);
    }
    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("UI skin: " + label);
    }
}

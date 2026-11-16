using System;
using UnityEditor;
using UnityEngine;

public static class VisualAccessibilityValidation
{
    [MenuItem("GLITCH/Validation/Visual accessibility")]
    public static void Run() => Debug.Log($"Visual accessibility: {Validate()} checks passed.");
    public static int Validate()
    {
        Check(VisualAccessibility.ScaleIntensity(0.5f, 0f) == 0f, "zero disables selected impact");
        Check(VisualAccessibility.ScaleIntensity(0.5f, 1f) == 0.5f, "default appearance preserved");
        Check(VisualAccessibility.ScaleIntensity(0.5f, 0.5f) == 0.25f, "partial reduction");
        Check(VisualAccessibility.ScaleIntensity(-1f, 1f) == 0f, "negative intensity bounded");
        Check(VisualAccessibility.ScaleIntensity(float.NaN, 1f) == 0f, "invalid intensity rejected");
        Check(VisualAccessibility.ScaleIntensity(float.PositiveInfinity, 1f) == 0f, "infinite intensity rejected");
        Check(VisualAccessibility.ScaleIntensity(0.5f, -1f) == 0f, "negative preference bounded");
        Check(VisualAccessibility.ScaleIntensity(0.5f, 2f) == 0.5f, "preference cannot amplify beyond default");
        Check(VisualAccessibility.ScaleIntensity(0.5f, float.NaN) == 0.5f, "invalid preference uses default");
        Check(VisualAccessibility.ScaleIntensity(0.5f, float.PositiveInfinity) == 0.5f, "infinite preference uses default");
        return 10;
    }
    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("Visual accessibility: " + label);
    }
}

using System;
using UnityEditor;
using UnityEngine;

public static class FiniteSettingValidation
{
    [MenuItem("GLITCH/Validation/Finite user settings")]
    public static void Run() => Debug.Log($"Finite settings: {Validate()} checks passed.");
    public static int Validate()
    {
        Check(FiniteSetting.Clamp(0.4f, 0.8f, 0f, 1f) == 0.4f, "valid setting preserved");
        Check(FiniteSetting.Clamp(-1f, 0.8f, 0f, 1f) == 0f, "below range");
        Check(FiniteSetting.Clamp(2f, 0.8f, 0f, 1f) == 1f, "above range");
        Check(FiniteSetting.Clamp(float.NaN, 0.8f, 0f, 1f) == 0.8f, "NaN uses authored default");
        Check(FiniteSetting.Clamp(float.PositiveInfinity, 0.8f, 0f, 1f) == 0.8f, "positive infinity");
        Check(FiniteSetting.Clamp(float.NegativeInfinity, 0.8f, 0f, 1f) == 0.8f, "negative infinity");
        Check(FiniteSetting.Clamp(float.NaN, 1.1f, 0.85f, 1.55f) == 1.1f, "HUD default");
        Check(FiniteSetting.Clamp(0f, 1f, 0.4f, 1.6f) == 0.4f, "motion lower bound");
        Check(FiniteSetting.Clamp(0f, 0.8f, 0f, 1f) == 0f, "intentional mute preserved");
        Check(FiniteSetting.Clamp(1f, 0.8f, 0f, 1f) == 1f, "upper endpoint preserved");
        return 10;
    }
    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("Finite settings: " + label);
    }
}

using System;

public static class VisualAccessibility
{
    public static float ScaleIntensity(float amount, float preference)
    {
        if (float.IsNaN(amount) || float.IsInfinity(amount)) return 0f;
        return Math.Max(0f, amount) * FiniteSetting.Clamp(preference, 1f, 0f, 1f);
    }
}

using System;

public static class FiniteSetting
{
    public static float Clamp(float value, float fallback, float min, float max)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) value = fallback;
        return Math.Min(max, Math.Max(min, value));
    }
}

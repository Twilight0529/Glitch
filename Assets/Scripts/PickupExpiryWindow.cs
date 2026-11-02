using System;

public static class PickupExpiryWindow
{
    public const float WarningSeconds = 3f;

    public static bool TryGetFraction(float age, float lifetime, out float fraction)
    {
        fraction = 0f;
        if (float.IsNaN(age) || float.IsInfinity(age) || float.IsNaN(lifetime) ||
            float.IsInfinity(lifetime) || age < 0f || lifetime <= 0f) return false;
        float remaining = lifetime - age;
        float window = Math.Min(WarningSeconds, lifetime);
        if (remaining <= 0f || remaining > window) return false;
        fraction = Math.Min(1f, Math.Max(0f, remaining / window));
        return true;
    }
}

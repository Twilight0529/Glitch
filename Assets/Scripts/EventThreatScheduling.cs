using System;

// Shared admission rules keep event -> boss and boss -> event scheduling symmetric.
public static class EventThreatScheduling
{
    public static bool RequiresBossRecovery(float pressure, float suppressionThreshold, float existingPressure = 0f)
    {
        return Math.Max(0f, pressure) + Math.Max(0f, existingPressure) >= Math.Max(0.1f, suppressionThreshold);
    }

    public static float EstimatePulseDuration(int maxBaseCount, int extraHazards,
        float warning, float telegraph, float active, float stagger)
    {
        int count = Math.Max(1, maxBaseCount) + Math.Max(0, extraHazards);
        return Math.Max(0f, warning) + Math.Max(0.1f, telegraph) + Math.Max(0.1f, active)
            + Math.Max(0f, stagger) * Math.Max(0, count - 1) + 0.35f;
    }
}

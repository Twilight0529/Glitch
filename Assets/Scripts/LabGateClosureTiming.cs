using System;

// Contextual timing without scene or physics dependencies, so its limits can be tested.
public static class LabGateClosureTiming
{
    public static bool TryResolve(float requestedSeconds, int reachableBefore, int reachableAfter,
        float minimumReachableFraction, float minimumHoldSeconds, out float holdSeconds)
    {
        holdSeconds = 0f;
        if (requestedSeconds <= 0f || reachableBefore <= 0 || reachableAfter <= 0) return false;
        float minimumFraction = Math.Max(0.2f, Math.Min(1f, minimumReachableFraction));
        float available = Math.Min(1f, (float)reachableAfter / reachableBefore);
        if (available < minimumFraction) return false;

        // Generous layouts retain the rolled duration; constrained layouts reopen sooner.
        float span = Math.Max(0.01f, 0.92f - minimumFraction);
        float openness = Math.Max(0f, Math.Min(1f, (available - minimumFraction) / span));
        float multiplier = 0.45f + openness * 0.55f;
        float floor = Math.Min(requestedSeconds, Math.Max(0.1f, minimumHoldSeconds));
        holdSeconds = Math.Max(floor, requestedSeconds * multiplier);
        return true;
    }
}

using System;

public static class EventThreatSchedulingTests
{
    private static int passed;

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        passed++;
    }

    public static int Main()
    {
        Check(EventThreatScheduling.RequiresBossRecovery(0.72f, 0.6f),
            "Core spikes cannot start over a boss major state.");
        Check(EventThreatScheduling.RequiresBossRecovery(0.62f, 0.6f),
            "Containment pulse follows the same threshold as boss suppression.");
        Check(EventThreatScheduling.RequiresBossRecovery(0.6f, 0.6f), "Threshold is inclusive.");
        Check(!EventThreatScheduling.RequiresBossRecovery(0.59f, 0.6f), "Light events remain eligible.");
        Check(!EventThreatScheduling.RequiresBossRecovery(-1f, -1f), "Zero load never suppresses the boss.");
        Check(!EventThreatScheduling.RequiresBossRecovery(0.72f, 0.8f), "Serialized threshold is respected.");
        Check(EventThreatScheduling.RequiresBossRecovery(0.3f, 0.6f, 0.4f),
            "Several light events cannot exceed the threshold over an active major state.");
        Check(!EventThreatScheduling.RequiresBossRecovery(0.2f, 0.6f, 0.3f),
            "Light combinations below the threshold remain eligible.");

        float early = EventThreatScheduling.EstimatePulseDuration(4, 0, 0.9f, 0.9f, 2.3f, 0.2f);
        float late = EventThreatScheduling.EstimatePulseDuration(4, 3, 0.9f, 0.9f, 2.3f, 0.2f);
        Check(Math.Abs(late - early - 0.6f) < 0.00001f, "Sector hazards extend pressure through the last spawn.");
        Check(late > 0.9f + 6 * 0.2f + 0.9f + 2.3f, "Reservation covers the final hazard lifetime.");
        float single = EventThreatScheduling.EstimatePulseDuration(1, 0, 0f, 0f, 0f, 10f);
        Check(Math.Abs(single - 0.55f) < 0.00001f, "One hazard has no stagger and honors zone minimum lifetime.");
        Check(EventThreatScheduling.EstimatePulseDuration(-4, -2, -1f, -1f, -1f, -1f) >= 0.55f,
            "Invalid serialized values are clamped consistently with hazard configuration.");
        Console.WriteLine("EventThreatScheduling: " + passed + " tests passed.");
        return 0;
    }
}

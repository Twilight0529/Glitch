using System;

internal static class CoreSpikeGroupTimingTests
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }

    private static int Main()
    {
        Check(CoreSpikeGroupTiming.GetPhase(0f, 0, 1, 1.5f, 7f) == CoreSpikePhase.Warning,
            "Single obstacle starts with warning.");
        Check(CoreSpikeGroupTiming.GetPhase(1.5f, 0, 1, 1.5f, 7f) == CoreSpikePhase.Active,
            "Single obstacle keeps existing warning duration.");
        Check(CoreSpikeGroupTiming.GetPhase(8.5f, 0, 1, 1.5f, 7f) == CoreSpikePhase.Finished,
            "Single obstacle keeps existing total duration.");
        Check(CoreSpikeGroupTiming.GetPhase(0f, 1, 2, 1.5f, 7f) == CoreSpikePhase.Dormant,
            "Later group is invisible during first warning.");
        Check(CoreSpikeGroupTiming.GetPhase(4.25f, 0, 2, 1.5f, 7f) == CoreSpikePhase.Finished &&
            CoreSpikeGroupTiming.GetPhase(4.25f, 1, 2, 1.5f, 7f) == CoreSpikePhase.Warning,
            "First group stops before second group warns.");
        Check(CoreSpikeGroupTiming.GetPhase(5.75f, 1, 2, 1.5f, 7f) == CoreSpikePhase.Active,
            "Second group receives a full warning.");
        Check(CoreSpikeGroupTiming.GetPhase(8.5f, 1, 2, 1.5f, 7f) == CoreSpikePhase.Finished,
            "Alternation does not lengthen event or pressure reservation.");

        foreach (int obstacleCount in new[] { 2, 3, 6, 12 })
        {
            bool safe = true;
            for (int step = 0; step <= 900; step++)
            {
                int active = 0;
                float elapsed = step * 0.01f;
                for (int obstacle = 0; obstacle < obstacleCount; obstacle++)
                    if (CoreSpikeGroupTiming.GetPhase(elapsed, obstacle % 2, 2, 1.5f, 7f) == CoreSpikePhase.Active)
                        active++;
                if (active > (obstacleCount + 1) / 2) safe = false;
            }
            Check(safe, "Alternating groups never activate all props: " + obstacleCount);
        }

        bool shortBudgetSafe = true;
        for (int step = 0; step <= 250; step++)
        {
            float elapsed = step * 0.01f;
            CoreSpikePhase first = CoreSpikeGroupTiming.GetPhase(elapsed, 0, 2, 1.5f, 1f);
            CoreSpikePhase second = CoreSpikeGroupTiming.GetPhase(elapsed, 1, 2, 1.5f, 1f);
            if (first == CoreSpikePhase.Active && second == CoreSpikePhase.Active) shortBudgetSafe = false;
        }
        Check(shortBudgetSafe, "Short inspector durations still prevent group overlap.");
        Check(CoreSpikeGroupTiming.GetPhase(2.5f, 1, 2, 1.5f, 1f) == CoreSpikePhase.Finished,
            "Short duration ends inside original budget.");
        Console.WriteLine("Core spike groups: " + checks + " checks passed.");
        return 0;
    }
}

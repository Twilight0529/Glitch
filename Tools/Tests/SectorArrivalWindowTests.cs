using System;

internal static class SectorArrivalWindowTests
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }

    private static int Main()
    {
        var window = new SectorArrivalWindow();
        Check(!window.IsActive(0f), "The first map receives no unintended extra delay.");
        window.Extend(100f, 6f);
        Check(window.IsActive(100f) && window.IsActive(105.99f) && !window.IsActive(106f),
            "Arrival holds new events until the configured boundary.");
        window.Extend(101f, 1f);
        Check(window.IsActive(105f), "A shorter request does not cancel existing protection.");
        window.Extend(103f, 6f);
        Check(window.IsActive(108f) && !window.IsActive(109f), "Another arrival extends the deadline.");
        Check(window.IsActive(108f), "Reading the same paused gameplay time consumes no protection.");
        window.Clear();
        Check(!window.IsActive(0f), "Restarting clears the previous run's deadline.");
        window.Extend(10f, -1f);
        Check(!window.IsActive(10f), "A disabled or negative duration adds no arrival delay.");
        Console.WriteLine("Sector arrival: " + checks + " checks passed.");
        return 0;
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

internal static class LabGateClosureTimingTests
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }

    private static int Main()
    {
        float hold;
        Check(LabGateClosureTiming.TryResolve(10f, 100, 95, 0.55f, 3f, out hold) && hold == 10f,
            "Open layouts keep the configured hold duration.");
        Check(LabGateClosureTiming.TryResolve(10f, 100, 55, 0.55f, 3f, out hold) && Math.Abs(hold - 4.5f) < 0.001f,
            "Constrained layouts reopen sooner.");
        Check(!LabGateClosureTiming.TryResolve(10f, 100, 54, 0.55f, 3f, out hold),
            "Closures that isolate too much space are rejected.");
        Check(!LabGateClosureTiming.TryResolve(10f, 100, 0, 0.55f, 3f, out hold),
            "A fully blocked player origin cannot start a closure.");
        Check(LabGateClosureTiming.TryResolve(2f, 100, 55, 0.55f, 3f, out hold) && hold <= 2f,
            "The contextual floor never extends a shorter configured duration.");
        Check(!LabGateClosureTiming.TryResolve(10f, 0, 100, 0.55f, 3f, out hold),
            "Invalid baseline is rejected.");

        var original = new ArenaReachabilityGrid(new Rect(0f, 0f, 12f, 10f), 0.5f, 0.25f);
        original.Build(new List<Rect>());
        Vector2 origin = new Vector2(2f, 5f);
        original.FloodFrom(origin);
        int baseline = original.ReachableCellCount;
        var closed = original.WithAdditionalObstacles(new[] { new Rect(5f, 0f, 1f, 10f) });
        closed.FloodFrom(origin);
        Check(closed.ReachableCellCount < baseline && !closed.CanReach(new Vector2(9f, 5f)),
            "A prospective gate changes the player's actual reachable component.");
        Check(original.ReachableCellCount == baseline && original.CanReach(new Vector2(9f, 5f)),
            "Rejected closures do not corrupt the original navigation snapshot.");
        Check(!closed.FloodFrom(new Vector2(5.5f, 5f)) && closed.ReachableCellCount == 0,
            "An obstructed origin does not retain the old area count.");
        Console.WriteLine("Lab gate timing: " + checks + " checks passed.");
        return 0;
    }
}

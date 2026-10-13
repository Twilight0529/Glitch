using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ArenaReachabilityValidation
{
    [MenuItem("GLITCH/Validation/Arena reachability")]
    public static void Run()
    {
        Debug.Log($"Arena reachability: {Validate()} checks passed.");
    }

    public static int Validate()
    {
        int checks = 0;
        Rect area = new Rect(0f, 0f, 12f, 10f);
        var empty = new List<Rect>();
        var grid = new ArenaReachabilityGrid(area, 0.5f, 0.25f);
        Check(grid.WouldRemainConnected(empty, new Rect(5f, 4f, 1f, 1f)), "isolated obstacle preserves routes", ref checks);
        Check(!grid.WouldRemainConnected(empty, new Rect(5f, 0f, 1f, 10f)), "wall splitting the arena is rejected", ref checks);

        var narrowGap = new List<Rect> { new Rect(5f, 0f, 1f, 4.7f), new Rect(5f, 5.3f, 1f, 4.7f) };
        grid.Build(narrowGap);
        Check(grid.FloodFrom(new Vector2(2f, 5f)) && !grid.CanReach(new Vector2(9f, 5f)),
            "gap smaller than the player is impassable", ref checks);
        var wideGap = new List<Rect> { new Rect(5f, 0f, 1f, 3f), new Rect(5f, 7f, 1f, 3f) };
        grid.Build(wideGap);
        Check(grid.FloodFrom(new Vector2(2f, 5f)) && grid.CanReach(new Vector2(9f, 5f)),
            "wide corridor connects both sides", ref checks);

        Rect lower = new Rect(5f, 0f, 1f, 4.4f);
        Rect upper = new Rect(5f, 5.6f, 1f, 4.4f);
        Check(grid.WouldRemainConnected(empty, lower) && grid.WouldRemainConnected(empty, upper),
            "individual compound parts leave a route", ref checks);
        Check(!grid.WouldRemainConnected(empty, lower, new Rect(5f, 4.4f, 1f, 5.6f)),
            "compound obstacle is validated as a whole", ref checks);

        grid.Build(new List<Rect> { new Rect(5f, 0f, 1f, 10f) });
        Check(grid.FloodFrom(new Vector2(2f, 5f)) && !grid.CanReach(new Vector2(9f, 5f)),
            "objective across a closed gate is unreachable", ref checks);
        grid.Build(empty);
        Check(grid.FloodFrom(new Vector2(2f, 5f)) && grid.CanReach(new Vector2(9f, 5f)),
            "rebuilding after gate opens restores reachability", ref checks);
        Check(!grid.CanReach(new Vector2(-1f, 5f)), "outside arena is unreachable", ref checks);
        Check(!grid.FloodFrom(new Vector2(-1f, 5f)) && !grid.CanReach(new Vector2(2f, 5f)),
            "invalid origin clears previous reachability", ref checks);

        var diagonal = new ArenaReachabilityGrid(new Rect(0f, 0f, 2f, 2f), 0f, 1f);
        diagonal.Build(new List<Rect> { new Rect(1.1f, 0.1f, 0.8f, 0.8f), new Rect(0.1f, 1.1f, 0.8f, 0.8f) });
        Check(diagonal.FloodFrom(new Vector2(0.5f, 0.5f)) && !diagonal.CanReach(new Vector2(1.5f, 1.5f)),
            "diagonal corner cutting is forbidden", ref checks);
        return checks;
    }

    private static void Check(bool condition, string label, ref int checks)
    {
        if (!condition) throw new InvalidOperationException("Arena reachability validation failed: " + label);
        checks++;
    }
}

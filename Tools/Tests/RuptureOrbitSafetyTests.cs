using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

internal static class RuptureOrbitSafetyTests
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }

    private static int Main()
    {
        var safety = new RuptureOrbitSafety(new Rect(-16f, -9f, 32f, 18f), 0.55f);
        Check(safety.IsConnected(new Rect[0]), "An empty arena stays connected.");
        Check(!safety.IsConnected(new[] { new Rect(-0.5f, -9f, 1f, 18f) }),
            "A swept wall sealing the arena is rejected.");
        Check(safety.IsConnected(new[] { new Rect(-0.5f, -6f, 1f, 12f) }),
            "A wall leaving wide passages around both ends stays traversable.");
        Check(!safety.IsConnected(new[] { new Rect(-0.5f, -8.3f, 1f, 16.6f) }),
            "Gaps narrower than player clearance are rejected.");
        Check(safety.IsConnected(new[] { new Rect(-2f, -2f, 4f, 4f) }),
            "Global connectivity does not depend on the player's conservative grid cell.");
        Vector2[] square = { new Vector2(-1f, -1f), new Vector2(1f, -1f),
            new Vector2(-1f, 1f), new Vector2(1f, 1f) };
        Check(!RuptureOrbitSafety.IntersectsPlayerFootprint(square, new Rect(2f, 0f, 0.8f, 0.8f), 0.05f),
            "Player outside the actual sweep remains free even next to static geometry.");
        Check(!RuptureOrbitSafety.IntersectsPlayerFootprint(square, new Rect(-15.9f, 0f, 0.8f, 0.8f), 0.05f),
            "Physical wall contact outside the navigation grid domain cannot block unrelated moving props.");
        Check(RuptureOrbitSafety.IntersectsPlayerFootprint(square, new Rect(-0.4f, -0.4f, 0.8f, 0.8f), 0.05f),
            "A moving solid cannot sweep through the actual player collider.");
        Check(RuptureOrbitSafety.IntersectsPlayerFootprint(square, new Rect(1.04f, -0.4f, 0.8f, 0.8f), 0.05f),
            "Padding protects collisions between the sampled footprints.");
        Vector2[] diamond = { new Vector2(0f, -1.414f), new Vector2(1.414f, 0f),
            new Vector2(-1.414f, 0f), new Vector2(0f, 1.414f) };
        Check(!RuptureOrbitSafety.IntersectsPlayerFootprint(diamond, new Rect(1.05f, 1.05f, 0.3f, 0.3f), 0.01f),
            "Empty corners of rotated obstacle AABBs do not stop the formation.");
        Check(safety.IsConnected(new Rect[0]),
            "Rejected formations do not poison subsequent checks.");
        var objects = new List<Rect>();
        for (int y = -5; y <= 5; y += 5)
            for (int x = -12; x <= 12; x += 4)
                objects.Add(new Rect(x, y, 0.8f, 0.8f));
        Check(safety.IsConnected(objects), "A populated representative formation remains traversable.");
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < 1000; i++) safety.IsConnected(objects);
        watch.Stop();
        Console.WriteLine("Rupture orbit safety: " + checks + " checks passed. 1000 reused-grid checks: " + watch.ElapsedMilliseconds + " ms.");
        return 0;
    }
}

using System;
using UnityEditor;
using UnityEngine;

public static class ReadinessTransitionValidation
{
    [MenuItem("GLITCH/Validation/Dash readiness")]
    public static void Run() => Debug.Log($"Dash readiness: {Validate()} checks passed.");

    public static int Validate()
    {
        var edge = new ReadinessTransition();
        Check(!edge.Observe(true), "no cue at spawn");
        Check(!edge.Observe(true), "ready state does not repeat");
        Check(!edge.Observe(false), "using dash does not cue readiness");
        Check(!edge.Observe(false), "cooldown does not cue readiness");
        Check(edge.Observe(true), "cooldown completion cues once");
        Check(!edge.Observe(true), "completion does not repeat");
        edge.Observe(false);
        Check(edge.Observe(true), "next recharge can cue");
        edge.Reset();
        Check(!edge.Observe(true), "reset does not replay ready arrival");
        edge.Reset();
        Check(!edge.Observe(false) && edge.Observe(true), "initial unavailable sample permits next arrival");
        return 9;
    }

    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("Dash readiness: " + label);
    }
}

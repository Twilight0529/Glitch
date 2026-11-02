using System;
using UnityEditor;
using UnityEngine;

public static class RunEndContextValidation
{
    [MenuItem("GLITCH/Validation/Run end context")]
    public static void Run() => Debug.Log($"Run end context: {Validate()} checks passed.");

    public static int Validate()
    {
        var context = new RunEndContext();
        Check(context.source == "unknown", "unobserved cause is explicit");
        Check(context.TryCapture(" core_spikes ", "Core", "OrbitBarrage", 2f, -3f, 140f), "first cause accepted");
        Check(context.source == "core_spikes" && context.arena == "Core", "cause and arena captured");
        Check(context.enemyState == "OrbitBarrage" && context.playerX == 2f && context.playerY == -3f && context.gameplaySeconds == 140f, "state and position captured");
        Check(!context.TryCapture("scene_exit", "Lab", "BasePursuit", 0f, 0f, 0f), "later cleanup cannot replace cause");
        Check(context.source == "core_spikes" && context.arena == "Core", "first snapshot preserved");
        var invalid = new RunEndContext();
        invalid.TryCapture(null, " ", new string('x', 100), float.NaN, float.PositiveInfinity, -1f);
        Check(invalid.source == "unknown" && invalid.arena == "unknown", "missing labels explicit");
        Check(invalid.enemyState.Length == 64, "labels bounded");
        Check(invalid.playerX == 0f && invalid.playerY == 0f && invalid.gameplaySeconds == 0f, "invalid numeric data normalized");
        Check(new RunEndContext().TryCapture("application_quit", "Lab", "BasePursuit", 0f, 0f, 1f), "new run is independent");
        return 10;
    }

    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("Run end context: " + label);
    }
}

using System;
using UnityEditor;
using UnityEngine;

public static class GameplayTickPolicyValidation
{
    [MenuItem("GLITCH/Validation/Paused anomaly updates")]
    public static void Run() => Debug.Log($"Gameplay tick: {Validate()} checks passed.");
    public static int Validate()
    {
        Check(GameplayTickPolicy.CanAdvance(1f, false), "normal gameplay");
        Check(!GameplayTickPolicy.CanAdvance(0f, false), "paused before deltaTime refresh");
        Check(!GameplayTickPolicy.CanAdvance(1f, true), "scene transition");
        Check(GameplayTickPolicy.CanAdvance(0.2f, false), "slow motion");
        Check(GameplayTickPolicy.CanAdvance(2f, false), "positive fast clock");
        Check(!GameplayTickPolicy.CanAdvance(float.NaN, false), "invalid clock");
        Check(!GameplayTickPolicy.CanAdvance(float.PositiveInfinity, false), "infinite clock");
        Check(!GameplayTickPolicy.CanAdvance(-1f, false), "negative clock");
        return 8;
    }
    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("Gameplay tick: " + label);
    }
}

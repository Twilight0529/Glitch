using System;
using UnityEditor;
using UnityEngine;

public static class GameplayInputGateValidation
{
    [MenuItem("GLITCH/Validation/Paused gameplay input")]
    public static void Run() => Debug.Log($"Gameplay input gate: {Validate()} checks passed.");

    public static int Validate()
    {
        var gate = new GameplayInputGate();
        Check(gate.Allows(1f, false, 10), "normal gameplay allowed");
        Check(!gate.Allows(0f, false, 10), "paused actions blocked");
        Check(!gate.Allows(1f, true, 10), "transition actions blocked");
        Check(gate.Allows(0.25f, false, 10), "positive slow motion allowed");
        gate.BlockFrame(10);
        Check(!gate.Allows(1f, false, 10), "resume click frame blocked");
        Check(gate.Allows(1f, false, 11), "next frame restored");
        gate.BlockFrame(11);
        Check(!gate.Allows(1f, false, 11), "another resume can block another frame");
        Check(!gate.Allows(float.NaN, false, 12), "invalid clock rejected");
        Check(!gate.Allows(float.PositiveInfinity, false, 12), "infinite clock rejected");
        Check(!gate.Allows(-1f, false, 12), "negative clock rejected");
        return 10;
    }

    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("Gameplay input gate: " + label);
    }
}

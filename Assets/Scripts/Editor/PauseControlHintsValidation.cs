using System;
using UnityEditor;
using UnityEngine;

public static class PauseControlHintsValidation
{
    [MenuItem("GLITCH/Validation/Pause control hints")]
    public static void Run() => Debug.Log($"Pause control hints: {Validate()} checks passed.");

    public static int Validate()
    {
        var solo = PauseControlHints.KeyboardAndMouse(false);
        var versus = PauseControlHints.KeyboardAndMouse(true);
        Check(solo.Count == 6, "solo lists six essential actions");
        Check(solo[0].input.Contains("WASD") && solo[0].input.Contains("Flechas"), "solo movement alternatives");
        Check(solo[2].input.Contains("Clic izq.") && solo[3].input.Contains("Clic der."), "both mouse actions shown");
        Check(solo[4].input == "F", "hijack binding shown");
        Check(versus.Count == 7 && versus[0].input == "WASD" && versus[4].input == "Flechas", "versus players separated");
        Check(versus[5].input.Contains("Ctrl der.") && versus[5].input.Contains("Enter"), "anomaly state controls shown");
        foreach (var hint in versus)
            Check(!hint.input.Contains("Clic") && !hint.action.Contains("Hijack"), "unsupported versus actions excluded");
        Check(solo[5].input == "Esc" && versus[6].input == "Esc", "pause binding in both modes");
        return 14;
    }

    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("Pause control hints: " + label);
    }
}

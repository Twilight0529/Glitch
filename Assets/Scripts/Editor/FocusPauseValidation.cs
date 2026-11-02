using System;
using UnityEditor;
using UnityEngine;

public static class FocusPauseValidation
{
    [MenuItem("GLITCH/Validation/Focus pause")]
    public static void Run() => Debug.Log($"Focus pause: {Validate()} checks passed.");

    public static int Validate()
    {
        Check(GameplayFocusPausePolicy.ShouldPause(false, true, true, false, false, true), "background run pauses");
        Check(!GameplayFocusPausePolicy.ShouldPause(true, true, true, false, false, true), "foreground stays playing");
        Check(!GameplayFocusPausePolicy.ShouldPause(false, false, true, false, false, true), "does not re-pause overlay");
        Check(!GameplayFocusPausePolicy.ShouldPause(false, true, false, false, false, true), "does not interrupt startup");
        Check(!GameplayFocusPausePolicy.ShouldPause(false, true, true, true, false, true), "does not replace defeat");
        Check(!GameplayFocusPausePolicy.ShouldPause(false, true, true, false, true, true), "does not interrupt transition");
        Check(!GameplayFocusPausePolicy.ShouldPause(false, true, true, false, false, false), "does not cover frozen tutorial");
        Check(!GameplayFocusPausePolicy.ShouldPause(true, false, true, false, false, false), "returning does not request resume");
        return 8;
    }

    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("Focus pause: " + label);
    }
}

using System;
using UnityEditor;
using UnityEngine;

public static class RunExitConfirmationValidation
{
    [MenuItem("GLITCH/Validation/Run exit confirmation")]
    public static void Run() => Debug.Log($"Run exit confirmation: {Validate()} checks passed.");

    public static int Validate()
    {
        var prompt = new RunExitConfirmationState();
        Check(!prompt.TryConfirm(out _), "no action without a request");
        Check(!prompt.Request(RunExitConfirmationState.Action.None), "empty request rejected");
        Check(prompt.Request(RunExitConfirmationState.Action.Restart), "restart asks first");
        Check(!prompt.Request(RunExitConfirmationState.Action.ReturnToMenu), "another click cannot replace pending action");
        prompt.Cancel();
        Check(!prompt.TryConfirm(out _), "cancel and Escape do not execute");
        Check(prompt.Request(RunExitConfirmationState.Action.ReturnToMenu), "new choice after cancel");
        Check(prompt.TryConfirm(out var action) && action == RunExitConfirmationState.Action.ReturnToMenu, "confirm returns chosen action");
        Check(!prompt.TryConfirm(out _), "double confirmation executes only once");
        Check(prompt.Request(RunExitConfirmationState.Action.Restart) && prompt.TryConfirm(out action) && action == RunExitConfirmationState.Action.Restart, "restart remains a distinct action");
        return 9;
    }

    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("Run exit confirmation: " + label);
    }
}

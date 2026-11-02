using System;
using UnityEditor;
using UnityEngine;

public static class PickupClaimValidation
{
    [MenuItem("GLITCH/Validation/Pickup collection")]
    public static void Run() => Debug.Log($"Pickup collection: {Validate()} checks passed.");

    public static int Validate()
    {
        var gate = new PickupClaimGate();
        Check(!gate.TryClaim(false), "ineligible contact does not collect");
        Check(gate.TryClaim(true), "later eligible contact can collect");
        Check(!gate.TryClaim(true), "second collider cannot collect again");
        Check(!gate.TryClaim(false), "ineligible contact cannot reopen gate");
        gate.Expire();
        Check(!gate.TryClaim(true), "expiry after collection cannot reopen gate");
        var expired = new PickupClaimGate();
        expired.Expire();
        Check(!expired.TryClaim(true), "expired pickup cannot collect before deferred destruction");
        var independent = new PickupClaimGate();
        Check(independent.TryClaim(true), "another pickup remains independent");
        return 7;
    }

    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("Pickup collection: " + label);
    }
}

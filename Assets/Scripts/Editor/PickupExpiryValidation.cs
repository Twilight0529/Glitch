using System;
using UnityEditor;
using UnityEngine;

public static class PickupExpiryValidation
{
    [MenuItem("GLITCH/Validation/Powerup expiry")]
    public static void Run() => Debug.Log($"Powerup expiry: {Validate()} checks passed.");

    public static int Validate()
    {
        Check(!PickupExpiryWindow.TryGetFraction(6.9f, 10f, out _), "hidden before warning");
        Check(PickupExpiryWindow.TryGetFraction(7f, 10f, out float full) && full == 1f, "full at warning boundary");
        Check(PickupExpiryWindow.TryGetFraction(8.5f, 10f, out float half) && half == 0.5f, "half window");
        Check(!PickupExpiryWindow.TryGetFraction(10f, 10f, out _), "hidden at expiry");
        Check(!PickupExpiryWindow.TryGetFraction(11f, 10f, out _), "hidden after expiry");
        Check(PickupExpiryWindow.TryGetFraction(0f, 1f, out full) && full == 1f, "short lifetime starts full");
        Check(PickupExpiryWindow.TryGetFraction(0.5f, 1f, out half) && half == 0.5f, "short lifetime drains proportionally");
        Check(!PickupExpiryWindow.TryGetFraction(0f, 0f, out _), "invalid lifetime");
        Check(!PickupExpiryWindow.TryGetFraction(-1f, 10f, out _), "invalid age");
        Check(!PickupExpiryWindow.TryGetFraction(float.NaN, 10f, out _), "invalid clock");
        Check(!PickupExpiryWindow.TryGetFraction(0f, float.PositiveInfinity, out _), "infinite lifetime");
        return 11;
    }

    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("Powerup expiry: " + label);
    }
}

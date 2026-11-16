using System;
using UnityEditor;
using UnityEngine;

public static class LeaderboardRequestPolicyValidation
{
    [MenuItem("GLITCH/Validation/Leaderboard request timeout")]
    public static void Run() => Debug.Log($"Leaderboard timeout: {Validate()} checks passed.");
    public static int Validate()
    {
        Check(LeaderboardRequestPolicy.TimeoutSeconds(0) == 15, "old config uses safe default");
        Check(LeaderboardRequestPolicy.TimeoutSeconds(-1) == 15, "negative value does not disable timeout");
        Check(LeaderboardRequestPolicy.TimeoutSeconds(1) == 5, "minimum timeout");
        Check(LeaderboardRequestPolicy.TimeoutSeconds(5) == 5, "lower boundary");
        Check(LeaderboardRequestPolicy.TimeoutSeconds(15) == 15, "configured default");
        Check(LeaderboardRequestPolicy.TimeoutSeconds(30) == 30, "valid custom value");
        Check(LeaderboardRequestPolicy.TimeoutSeconds(60) == 60, "upper boundary");
        Check(LeaderboardRequestPolicy.TimeoutSeconds(int.MaxValue) == 60, "excessive value bounded");
        return 8;
    }
    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("Leaderboard timeout: " + label);
    }
}

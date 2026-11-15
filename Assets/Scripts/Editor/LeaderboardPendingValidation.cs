using System;
using UnityEditor;
using UnityEngine;

public static class LeaderboardPendingValidation
{
    [MenuItem("GLITCH/Validation/Leaderboard pending acknowledgement")]
    public static void Run() => Debug.Log($"Leaderboard pending: {Validate()} checks passed.");

    public static int Validate()
    {
        Check(!LeaderboardPendingPolicy.CanClear(2001, 2000), "old response preserves newer record");
        Check(LeaderboardPendingPolicy.CanClear(2000, 2000), "matching response clears pending");
        Check(LeaderboardPendingPolicy.CanClear(2000, 3000), "higher remote record covers pending");
        Check(!LeaderboardPendingPolicy.CanClear(1, -1), "missing remote record cannot acknowledge");
        Check(!LeaderboardPendingPolicy.CanClear(-1, 2000), "missing pending cannot be cleared");
        Check(LeaderboardPendingPolicy.CanClear(0, 0), "zero record handled explicitly");
        Check(!LeaderboardPendingPolicy.CanClear(1, 0), "zero response cannot erase one point");
        Check(LeaderboardPendingPolicy.CanClear(int.MaxValue, int.MaxValue), "max score boundary");
        Check(!LeaderboardPendingPolicy.CanClear(int.MaxValue, int.MaxValue - 1), "no arithmetic overflow");
        int pending = 2001;
        if (LeaderboardPendingPolicy.CanClear(pending, 2000)) pending = -1;
        Check(pending == 2001, "in-flight old upload keeps latest pending");
        if (LeaderboardPendingPolicy.CanClear(pending, 2001)) pending = -1;
        Check(pending == -1, "latest acknowledgement drains pending");
        return 11;
    }

    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidOperationException("Leaderboard pending: " + label);
    }
}

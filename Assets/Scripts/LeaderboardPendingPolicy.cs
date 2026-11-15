public static class LeaderboardPendingPolicy
{
    // An acknowledgement for 2000 must never erase a newer pending 2001.
    public static bool CanClear(int pendingScore, int acknowledgedScore)
    {
        return pendingScore >= 0 && acknowledgedScore >= pendingScore;
    }
}

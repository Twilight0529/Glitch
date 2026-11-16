using System;

public static class LeaderboardRequestPolicy
{
    public static int TimeoutSeconds(int configured)
    {
        return configured <= 0 ? 15 : Math.Min(60, Math.Max(5, configured));
    }
}

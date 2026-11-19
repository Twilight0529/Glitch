using System;
using System.Collections.Generic;

[Serializable]
public sealed class SheetsTelemetryRun
{
    public int schemaVersion;
    public string version, sessionId, startedAtUtc, endedAtUtc, level, playerClass;
    public int score, scorePickups, powerups, parries, firewallBursts, breaches, contractsCompleted, upgradesChosen;
    public float survivalSeconds;
    public string endReason;
    public RunEndContext endContext;
    public string installationId, applicationSessionId, platform;
}

// Plain data and deterministic queue rules, independent of Unity and networking.
[Serializable]
public sealed class SheetsTelemetryOutbox
{
    public string consentEpoch;
    public List<SheetsTelemetryRun> runs = new List<SheetsTelemetryRun>();
    public const int MaximumRuns = 100;
    public const int RetentionDays = 30;

    public bool Enqueue(SheetsTelemetryRun run, DateTime now)
    {
        Prune(now);
        if (run == null || !ValidId(run.sessionId) || runs.Exists(r => r.sessionId == run.sessionId)) return false;
        runs.Add(run);
        while (runs.Count > MaximumRuns) runs.RemoveAt(0);
        return true;
    }

    public void Prune(DateTime now)
    {
        if (runs == null) runs = new List<SheetsTelemetryRun>();
        runs.RemoveAll(r => r == null || !ValidId(r.sessionId) || !DateTime.TryParse(r.endedAtUtc,
            null, System.Globalization.DateTimeStyles.RoundtripKind, out var ended) ||
            ended.ToUniversalTime() < now.ToUniversalTime().AddDays(-RetentionDays));
        while (runs.Count > MaximumRuns) runs.RemoveAt(0);
    }

    public int Acknowledge(IList<SheetsTelemetryRun> sent, string[] accepted, string[] rejected)
    {
        var allowed = new HashSet<string>();
        if (sent != null) foreach (var run in sent) if (run != null) allowed.Add(run.sessionId);
        var remove = new HashSet<string>();
        foreach (var ids in new[] { accepted, rejected })
            if (ids != null) foreach (string id in ids) if (allowed.Contains(id)) remove.Add(id);
        return runs.RemoveAll(r => remove.Contains(r.sessionId));
    }

    public static bool ValidId(string id) => id != null && id.Length == 32 && id == id.ToLowerInvariant() &&
        Guid.TryParseExact(id, "N", out _);
}

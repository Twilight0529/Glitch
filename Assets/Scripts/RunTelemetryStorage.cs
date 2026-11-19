using System;
using System.IO;
using UnityEngine;

// Registro local independiente, con envío opcional de resúmenes a Sheets si hay consentimiento.
public static class RunTelemetryStorage
{
    [Serializable]
    private class RunRecord
    {
        public int schemaVersion = 2;
        public RunEndContext endContext = new RunEndContext();
        public string version;
        public string sessionId;
        public string startedAtUtc;
        public string endedAtUtc;
        public string level;
        public string playerClass;
        public int score;
        public float survivalSeconds;
        public int scorePickups;
        public int powerups;
        public int parries;
        public int firewallBursts;
        public int breaches;
        public int contractsCompleted;
        public int upgradesChosen;
        public string endReason;
    }

    [Serializable]
    private class Summary
    {
        public int runs;
        public int totalScore;
        public float totalSurvivalSeconds;
        public int totalPickups;
        public int totalPowerups;
        public int totalParries;
        public int totalFirewallBursts;
        public int totalBreaches;
        public int totalContractsCompleted;
        public int totalUpgradesChosen;
        public int bestScore;
        public float bestSurvivalSeconds;
        public string updatedAtUtc;
    }

    private static RunRecord current;
    private static bool recorded;
    private static bool suppressRemoteForCurrentRun;
    public static void MarkCurrentRunAsDebug() { suppressRemoteForCurrentRun = true; }
    public static string DirectoryPath => Path.Combine(Application.persistentDataPath, "telemetry");
    public static string RunsPath => Path.Combine(DirectoryPath, "runs.jsonl");
    public static string SummaryPath => Path.Combine(DirectoryPath, "summary.json");

    public static void BeginRun(string level, string playerClass)
    {
        recorded = false;
        suppressRemoteForCurrentRun = false;
        current = new RunRecord
        {
            version = Application.version,
            sessionId = Guid.NewGuid().ToString("N"),
            startedAtUtc = DateTime.UtcNow.ToString("o"),
            level = Safe(level),
            playerClass = Safe(playerClass)
        };
    }

    private static bool IsRecording => current != null && !recorded;
    public static void ScorePickup() { if (IsRecording) current.scorePickups++; }
    public static void Powerup() { if (IsRecording) current.powerups++; }
    public static void Parry() { if (IsRecording) current.parries++; }
    public static void FirewallBurst() { if (IsRecording) current.firewallBursts++; }
    public static void Breach() { if (IsRecording) current.breaches++; }
    public static void ContractCompleted() { if (IsRecording) current.contractsCompleted++; }
    public static void UpgradeChosen() { if (IsRecording) current.upgradesChosen++; }

    public static RunPerformanceSummary PerformanceSummary => current == null ? default :
        new RunPerformanceSummary(current.parries, current.breaches, current.contractsCompleted, current.upgradesChosen, current.scorePickups);

    public static void CaptureEndContext(string source, string arena, string enemyState, float x, float y, float seconds)
    {
        if (current == null || recorded) return;
        current.endContext.TryCapture(source, arena, enemyState, x, y, seconds);
    }

    public static void EndRun(int score, float survivalSeconds, string reason)
    {
        if (current == null || recorded) return;
        recorded = true;
        current.score = Mathf.Max(0, score);
        current.survivalSeconds = Mathf.Max(0f, survivalSeconds);
        current.endReason = Safe(reason);
        current.endedAtUtc = DateTime.UtcNow.ToString("o");

        try
        {
            Directory.CreateDirectory(DirectoryPath);
            File.AppendAllText(RunsPath, JsonUtility.ToJson(current) + Environment.NewLine);
            Summary summary = LoadSummary();
            summary.runs++;
            summary.totalScore += current.score;
            summary.totalSurvivalSeconds += current.survivalSeconds;
            summary.totalPickups += current.scorePickups;
            summary.totalPowerups += current.powerups;
            summary.totalParries += current.parries;
            summary.totalFirewallBursts += current.firewallBursts;
            summary.totalBreaches += current.breaches;
            summary.totalContractsCompleted += current.contractsCompleted;
            summary.totalUpgradesChosen += current.upgradesChosen;
            summary.bestScore = Mathf.Max(summary.bestScore, current.score);
            summary.bestSurvivalSeconds = Mathf.Max(summary.bestSurvivalSeconds, current.survivalSeconds);
            summary.updatedAtUtc = DateTime.UtcNow.ToString("o");
            File.WriteAllText(SummaryPath, JsonUtility.ToJson(summary, true));
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Telemetry local no pudo guardarse: {exception.Message}");
        }
        // Independent of local I/O: a failed local summary must not prevent queueing, or vice versa.
        if (!suppressRemoteForCurrentRun)
            GoogleSheetsTelemetryService.QueueFinishedRun(JsonUtility.ToJson(current));
    }

    public static void ResetLocalData()
    {
        GoogleSheetsTelemetryService.ClearPending();
        current = null;
        recorded = false;

        try
        {
            if (File.Exists(RunsPath)) File.Delete(RunsPath);
            if (File.Exists(SummaryPath)) File.Delete(SummaryPath);
            if (Directory.Exists(DirectoryPath) && Directory.GetFileSystemEntries(DirectoryPath).Length == 0)
            {
                Directory.Delete(DirectoryPath);
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"No se pudo borrar la telemetría local: {exception.Message}");
        }
    }

    private static Summary LoadSummary()
    {
        if (!File.Exists(SummaryPath)) return new Summary();
        try { return JsonUtility.FromJson<Summary>(File.ReadAllText(SummaryPath)) ?? new Summary(); }
        catch { return new Summary(); }
    }

    private static string Safe(string value) => string.IsNullOrWhiteSpace(value) ? "Unknown" : value.Trim();
}

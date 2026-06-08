using System;
using UnityEngine;

public static class DailyChallengeStorage
{
    // Operacion diaria: una meta rotativa que da una razon concreta para volver a jugar.
    public enum ChallengeKind
    {
        Survival,
        Score,
        Pickups,
        Parry,
        FirewallBurst,
        Contract
    }

    public struct DailyChallenge
    {
        public string id;
        public string dateKey;
        public string title;
        public string description;
        public string progressLabel;
        public ChallengeKind kind;
        public int target;
        public int dataReward;
    }

    private const string ProgressPrefix = "Glitch_Daily_Progress_";
    private const string CompletedPrefix = "Glitch_Daily_Completed_";

    public static DailyChallenge CurrentChallenge => BuildChallenge(DateTime.Now);

    public static int CurrentProgress
    {
        get
        {
            DailyChallenge challenge = CurrentChallenge;
            return Mathf.Clamp(PlayerPrefs.GetInt(GetProgressKey(challenge), 0), 0, Mathf.Max(1, challenge.target));
        }
    }

    public static bool IsCompleted
    {
        get
        {
            DailyChallenge challenge = CurrentChallenge;
            return PlayerPrefs.GetInt(GetCompletedKey(challenge), 0) == 1;
        }
    }

    public static string CurrentSummary
    {
        get
        {
            DailyChallenge challenge = CurrentChallenge;
            int progress = CurrentProgress;
            string state = IsCompleted ? "COMPLETADA" : $"{progress}/{challenge.target}";
            return $"{challenge.title}: {state}";
        }
    }

    public static bool AddProgress(ChallengeKind kind, int amount, out DailyChallenge completedChallenge)
    {
        DailyChallenge challenge = CurrentChallenge;
        if (challenge.kind != kind || amount <= 0 || IsCompleted)
        {
            completedChallenge = default;
            return false;
        }

        int progress = Mathf.Clamp(CurrentProgress + amount, 0, Mathf.Max(1, challenge.target));
        return SaveProgressAndTryComplete(challenge, progress, out completedChallenge);
    }

    public static bool SetProgress(ChallengeKind kind, int progress, out DailyChallenge completedChallenge)
    {
        DailyChallenge challenge = CurrentChallenge;
        if (challenge.kind != kind || IsCompleted)
        {
            completedChallenge = default;
            return false;
        }

        int clamped = Mathf.Clamp(progress, CurrentProgress, Mathf.Max(1, challenge.target));
        return SaveProgressAndTryComplete(challenge, clamped, out completedChallenge);
    }

    public static void ResetCurrentChallenge()
    {
        DailyChallenge challenge = CurrentChallenge;
        PlayerPrefs.DeleteKey(GetProgressKey(challenge));
        PlayerPrefs.DeleteKey(GetCompletedKey(challenge));
        PlayerPrefs.Save();
    }

    public static bool CompleteCurrentChallenge(out DailyChallenge completedChallenge)
    {
        DailyChallenge challenge = CurrentChallenge;
        return SaveProgressAndTryComplete(challenge, Mathf.Max(1, challenge.target), out completedChallenge);
    }

    private static bool SaveProgressAndTryComplete(DailyChallenge challenge, int progress, out DailyChallenge completedChallenge)
    {
        PlayerPrefs.SetInt(GetProgressKey(challenge), progress);
        if (progress < Mathf.Max(1, challenge.target))
        {
            PlayerPrefs.Save();
            completedChallenge = default;
            return false;
        }

        if (PlayerPrefs.GetInt(GetCompletedKey(challenge), 0) == 1)
        {
            PlayerPrefs.Save();
            completedChallenge = default;
            return false;
        }

        PlayerPrefs.SetInt(GetCompletedKey(challenge), 1);
        MetaProgressionStorage.AddData(Mathf.Max(0, challenge.dataReward));
        PlayerPrefs.Save();
        completedChallenge = challenge;
        return true;
    }

    private static DailyChallenge BuildChallenge(DateTime date)
    {
        string dateKey = date.ToString("yyyyMMdd");
        int variant = Mathf.Abs((date.Year * 397) ^ date.DayOfYear) % 6;
        switch (variant)
        {
            case 0:
                return Create(dateKey, "operacion_supervivencia", "Pulso Prolongado", "Sobrevive 90 segundos entre tus runs de hoy.", "Segundos", ChallengeKind.Survival, 90, 14);
            case 1:
                return Create(dateKey, "operacion_score", "Extraccion Limpia", "Suma 260 puntos durante el dia.", "Puntos", ChallengeKind.Score, 260, 14);
            case 2:
                return Create(dateKey, "operacion_pickups", "Barrido de Datos", "Recolecta 18 pickups de score.", "Pickups", ChallengeKind.Pickups, 18, 12);
            case 3:
                return Create(dateKey, "operacion_parry", "Mano Estable", "Conecta 4 parries exitosos.", "Parries", ChallengeKind.Parry, 4, 12);
            case 4:
                return Create(dateKey, "operacion_firewall", "Descarga Controlada", "Activa 2 Firewall Burst.", "Bursts", ChallengeKind.FirewallBurst, 2, 12);
            default:
                return Create(dateKey, "operacion_contratos", "Protocolo Firmado", "Completa 2 contratos de contencion.", "Contratos", ChallengeKind.Contract, 2, 16);
        }
    }

    private static DailyChallenge Create(
        string dateKey,
        string id,
        string title,
        string description,
        string progressLabel,
        ChallengeKind kind,
        int target,
        int dataReward)
    {
        return new DailyChallenge
        {
            id = id,
            dateKey = dateKey,
            title = title,
            description = description,
            progressLabel = progressLabel,
            kind = kind,
            target = Mathf.Max(1, target),
            dataReward = Mathf.Max(0, dataReward)
        };
    }

    private static string GetProgressKey(DailyChallenge challenge)
    {
        return $"{ProgressPrefix}{challenge.dateKey}_{challenge.id}";
    }

    private static string GetCompletedKey(DailyChallenge challenge)
    {
        return $"{CompletedPrefix}{challenge.dateKey}_{challenge.id}";
    }
}

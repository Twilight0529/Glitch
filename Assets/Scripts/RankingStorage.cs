using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct RankingEntry
{
    public string playerName;
    public int score;
    public string level;
    public float survivalTime;
    public string recordedAtUtc;
}

// Ranking local simple: normaliza nombres, ordena puntajes y limita cuántas entradas quedan guardadas.
public static class RankingStorage
{
    // Guarda el ranking local como datos JSON dentro de PlayerPrefs.
    [Serializable]
    private class RankingTable
    {
        public List<RankingEntry> entries = new List<RankingEntry>();
    }

    private const string RankingKey = "glitch_ranking_table_v1";
    private const string RankingCacheUtcKey = "glitch_ranking_cache_utc";
    private const string PersonalBestKey = "glitch_ranking_personal_best_v2";
    private const string PendingBestKey = "glitch_ranking_pending_best";
    private const string LastPlayerNameKey = "glitch_ranking_last_name";
    private const string PlayerNameRegistrationKey = "glitch_player_name_registration_v1";
    private const int MaxEntries = 15;

    public static IReadOnlyList<RankingEntry> GetTopEntries()
    {
        FirebaseLeaderboardService.RefreshTop();
        RankingTable table = LoadTable();
        SortAndTrim(table.entries);
        return table.entries;
    }

    public static void AddEntry(string playerName, int score, string level, float survivalTime)
    {
        RankingTable table = LoadTable();
        RankingEntry entry = new RankingEntry
        {
            playerName = SanitizeName(playerName),
            score = Mathf.Max(0, score),
            level = string.IsNullOrWhiteSpace(level) ? "Unknown" : level.Trim(),
            survivalTime = Mathf.Max(0f, survivalTime),
            recordedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
        };

        int previousBest = Mathf.Max(-1, PlayerPrefs.GetInt(PersonalBestKey, -1));
        for (int i = 0; i < table.entries.Count; i++)
        {
            if (string.Equals(table.entries[i].playerName, entry.playerName, StringComparison.OrdinalIgnoreCase))
                previousBest = Mathf.Max(previousBest, table.entries[i].score);
        }
        if (entry.score <= previousBest)
        {
            SaveLastPlayerName(entry.playerName);
            return;
        }

        table.entries.RemoveAll(existing => string.Equals(existing.playerName, entry.playerName, StringComparison.OrdinalIgnoreCase));
        table.entries.Add(entry);
        SortAndTrim(table.entries);
        SaveTable(table);
        SaveLastPlayerName(entry.playerName);
        PlayerPrefs.SetString(PendingBestKey, JsonUtility.ToJson(entry));
        PlayerPrefs.Save();
        FirebaseLeaderboardService.SubmitPersonalBest(entry);
    }

    public static string GetLastPlayerName()
    {
        string saved = PlayerPrefs.GetString(LastPlayerNameKey, string.Empty);
        if (string.IsNullOrWhiteSpace(saved))
        {
            return "Player";
        }

        return SanitizeName(saved);
    }

    public static bool HasSavedPlayerName()
    {
        if (PlayerPrefs.GetInt(PlayerNameRegistrationKey, 0) != 1) return false;
        string saved = PlayerPrefs.GetString(LastPlayerNameKey, string.Empty);
        return !string.IsNullOrWhiteSpace(saved) &&
               !IsGenericPlayerName(saved);
    }

    public static void RegisterPlayerName(string value)
    {
        string name = SanitizeName(value);
        if (IsGenericPlayerName(name)) return;
        PlayerPrefs.SetString(LastPlayerNameKey, name);
        PlayerPrefs.SetInt(PlayerNameRegistrationKey, 1);
        PlayerPrefs.Save();
    }

    public static void SaveLastPlayerName(string value)
    {
        string name = SanitizeName(value);
        PlayerPrefs.SetString(LastPlayerNameKey, name);
        PlayerPrefs.Save();
    }

    public static void ClearEntries()
    {
        PlayerPrefs.DeleteKey(RankingKey);
        PlayerPrefs.Save();
    }

    public static void ResetLocalProgress()
    {
        PlayerPrefs.DeleteKey(RankingKey);
        PlayerPrefs.DeleteKey(RankingCacheUtcKey);
        PlayerPrefs.DeleteKey(PersonalBestKey);
        PlayerPrefs.DeleteKey(PendingBestKey);
        PlayerPrefs.DeleteKey(LastPlayerNameKey);
        PlayerPrefs.DeleteKey(PlayerNameRegistrationKey);
        PlayerPrefs.Save();
    }

    public static bool HasFreshGlobalCache(int seconds)
    {
        long saved;
        if (!long.TryParse(PlayerPrefs.GetString(RankingCacheUtcKey, "0"), out saved)) return false;
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds() - saved < Mathf.Max(1, seconds);
    }

    public static void ReplaceGlobalCache(List<RankingEntry> entries)
    {
        RankingTable table = new RankingTable { entries = entries ?? new List<RankingEntry>() };
        SortAndTrim(table.entries);
        SaveTable(table);
        PlayerPrefs.SetString(RankingCacheUtcKey, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());
        PlayerPrefs.Save();
    }

    public static void UpdatePersonalBestFloor(int score)
    {
        int current = PlayerPrefs.GetInt(PersonalBestKey, -1);
        if (score <= current) return;
        PlayerPrefs.SetInt(PersonalBestKey, score);
        PlayerPrefs.Save();
    }

    public static bool TryGetPendingBest(out RankingEntry entry)
    {
        string json = PlayerPrefs.GetString(PendingBestKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(json))
        {
            entry = JsonUtility.FromJson<RankingEntry>(json);
            return entry.score >= 0;
        }
        entry = default;
        return false;
    }

    public static void ClearPendingBest()
    {
        PlayerPrefs.DeleteKey(PendingBestKey);
        PlayerPrefs.Save();
    }

    private static RankingTable LoadTable()
    {
        string json = PlayerPrefs.GetString(RankingKey, string.Empty);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new RankingTable();
        }

        RankingTable table = JsonUtility.FromJson<RankingTable>(json);
        if (table == null || table.entries == null)
        {
            return new RankingTable();
        }

        return table;
    }

    private static void SaveTable(RankingTable table)
    {
        string json = JsonUtility.ToJson(table);
        PlayerPrefs.SetString(RankingKey, json);
        PlayerPrefs.Save();
    }

    private static void SortAndTrim(List<RankingEntry> entries)
    {
        entries.Sort((a, b) =>
        {
            int scoreOrder = b.score.CompareTo(a.score);
            if (scoreOrder != 0)
            {
                return scoreOrder;
            }

            return b.survivalTime.CompareTo(a.survivalTime);
        });

        if (entries.Count > MaxEntries)
        {
            entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);
        }
    }

    public static string SanitizePlayerName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Player";
        }

        string trimmed = value.Trim();
        if (trimmed.Length > 16)
        {
            trimmed = trimmed.Substring(0, 16);
        }

        return trimmed;
    }

    public static bool IsGenericPlayerName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;
        string normalized = value.Trim().Replace(" ", string.Empty);
        return string.Equals(normalized, "Player", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "PlayerOne", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "Jugador", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(normalized, "JugadorUno", StringComparison.OrdinalIgnoreCase);
    }

    private static string SanitizeName(string value) => SanitizePlayerName(value);
}

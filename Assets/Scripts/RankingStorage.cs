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

public static class RankingStorage
{
    [Serializable]
    private class RankingTable
    {
        public List<RankingEntry> entries = new List<RankingEntry>();
    }

    private const string RankingKey = "glitch_ranking_table_v1";
    private const string LastPlayerNameKey = "glitch_ranking_last_name";
    private const int MaxEntries = 15;

    public static IReadOnlyList<RankingEntry> GetTopEntries()
    {
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

        table.entries.Add(entry);
        SortAndTrim(table.entries);
        SaveTable(table);
        SaveLastPlayerName(entry.playerName);
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

    private static string SanitizeName(string value)
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
}

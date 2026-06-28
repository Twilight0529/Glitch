using System.Collections.Generic;
using UnityEngine;

// Lleva la cuenta de arenas descubiertas y decide qué temas pueden aparecer al comenzar una nueva run.
public static class ArenaDiscoveryStorage
{
    // Persiste las zonas conocidas y el orden en que fueron descubiertas.
    private const string DiscoveryOrderKey = "Glitch_ArenaDiscovery_Order";
    private const char Separator = '|';

    public static IReadOnlyList<ProceduralArenaGenerator.ArenaTheme> DiscoveredThemes => LoadOrder();

    public static bool IsDiscovered(ProceduralArenaGenerator.ArenaTheme theme)
    {
        List<ProceduralArenaGenerator.ArenaTheme> order = LoadOrder();
        return order.Contains(theme);
    }

    public static bool Discover(ProceduralArenaGenerator.ArenaTheme theme)
    {
        List<ProceduralArenaGenerator.ArenaTheme> order = LoadOrder();
        if (order.Contains(theme))
        {
            return false;
        }

        order.Add(theme);
        SaveOrder(order);
        return true;
    }

    public static List<ProceduralArenaGenerator.ArenaTheme> GetInitialThemePool(bool includeAdvancedThemes)
    {
        List<ProceduralArenaGenerator.ArenaTheme> result = LoadOrder();
        result.RemoveAll(theme =>
            !includeAdvancedThemes &&
            (theme == ProceduralArenaGenerator.ArenaTheme.DataCore ||
             theme == ProceduralArenaGenerator.ArenaTheme.NullArchive));

        if (result.Count == 0)
        {
            result.Add(ProceduralArenaGenerator.ArenaTheme.ContainmentLab);
        }

        return result;
    }

    public static void UnlockAll()
    {
        SaveOrder(new List<ProceduralArenaGenerator.ArenaTheme>
        {
            ProceduralArenaGenerator.ArenaTheme.ContainmentLab,
            ProceduralArenaGenerator.ArenaTheme.StorageBay,
            ProceduralArenaGenerator.ArenaTheme.RuptureZone,
            ProceduralArenaGenerator.ArenaTheme.DataCore,
            ProceduralArenaGenerator.ArenaTheme.NullArchive
        });
    }

    public static void Reset()
    {
        SaveOrder(new List<ProceduralArenaGenerator.ArenaTheme>
        {
            ProceduralArenaGenerator.ArenaTheme.ContainmentLab
        });
    }

    private static List<ProceduralArenaGenerator.ArenaTheme> LoadOrder()
    {
        string raw = PlayerPrefs.GetString(DiscoveryOrderKey, string.Empty);
        List<ProceduralArenaGenerator.ArenaTheme> result = new List<ProceduralArenaGenerator.ArenaTheme>();
        if (!string.IsNullOrWhiteSpace(raw))
        {
            string[] values = raw.Split(Separator);
            for (int i = 0; i < values.Length; i++)
            {
                if (System.Enum.TryParse(values[i], out ProceduralArenaGenerator.ArenaTheme theme) &&
                    !result.Contains(theme))
                {
                    result.Add(theme);
                }
            }
        }

        if (!result.Contains(ProceduralArenaGenerator.ArenaTheme.ContainmentLab))
        {
            result.Insert(0, ProceduralArenaGenerator.ArenaTheme.ContainmentLab);
            SaveOrder(result);
        }

        return result;
    }

    private static void SaveOrder(List<ProceduralArenaGenerator.ArenaTheme> order)
    {
        List<string> values = new List<string>(order.Count);
        for (int i = 0; i < order.Count; i++)
        {
            values.Add(order[i].ToString());
        }

        PlayerPrefs.SetString(DiscoveryOrderKey, string.Join(Separator.ToString(), values));
        PlayerPrefs.Save();
    }
}

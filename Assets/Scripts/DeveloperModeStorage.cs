using UnityEngine;

public static class DeveloperModeStorage
{
    // Preferencias ocultas de testing: se usan solo desde el panel dev del menu principal.
    private const string ArenaOverrideEnabledKey = "Glitch_Dev_ArenaOverrideEnabled";
    private const string ArenaOverrideThemeKey = "Glitch_Dev_ArenaOverrideTheme";

    public static bool TryGetArenaOverride(out ProceduralArenaGenerator.ArenaTheme theme)
    {
        if (PlayerPrefs.GetInt(ArenaOverrideEnabledKey, 0) != 1)
        {
            theme = default;
            return false;
        }

        int raw = PlayerPrefs.GetInt(ArenaOverrideThemeKey, 0);
        theme = (ProceduralArenaGenerator.ArenaTheme)Mathf.Clamp(raw, 0, 2);
        return true;
    }

    public static void SetArenaOverride(ProceduralArenaGenerator.ArenaTheme theme)
    {
        PlayerPrefs.SetInt(ArenaOverrideEnabledKey, 1);
        PlayerPrefs.SetInt(ArenaOverrideThemeKey, (int)theme);
        PlayerPrefs.Save();
    }

    public static void ClearArenaOverride()
    {
        PlayerPrefs.DeleteKey(ArenaOverrideEnabledKey);
        PlayerPrefs.DeleteKey(ArenaOverrideThemeKey);
        PlayerPrefs.Save();
    }

    public static string GetArenaOverrideLabel()
    {
        if (!TryGetArenaOverride(out ProceduralArenaGenerator.ArenaTheme theme))
        {
            return "Random";
        }

        switch (theme)
        {
            case ProceduralArenaGenerator.ArenaTheme.ContainmentLab:
                return "Lab";
            case ProceduralArenaGenerator.ArenaTheme.StorageBay:
                return "Storage";
            default:
                return "Rupture";
        }
    }
}

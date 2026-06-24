using UnityEngine;

public static class DeveloperModeStorage
{
    // Preferencias ocultas de testing: se usan solo desde el panel dev del menu principal.
    private const string ArenaOverrideEnabledKey = "Glitch_Dev_ArenaOverrideEnabled";
    private const string ArenaOverrideThemeKey = "Glitch_Dev_ArenaOverrideTheme";
    private const string ForceBossLevelTwoKey = "Glitch_Dev_ForceBossLevelTwo";
    private const string ForceBossLevelThreeKey = "Glitch_Dev_ForceBossLevelThree";
    private const string StartTimeSecondsKey = "Glitch_Dev_StartTimeSeconds";
    private const string FastRunLoopsKey = "Glitch_Dev_FastRunLoops";
    private const string SkipCountdownKey = "Glitch_Dev_SkipCountdown";

    public static bool TryGetArenaOverride(out ProceduralArenaGenerator.ArenaTheme theme)
    {
        if (PlayerPrefs.GetInt(ArenaOverrideEnabledKey, 0) != 1)
        {
            theme = default;
            return false;
        }

        int raw = PlayerPrefs.GetInt(ArenaOverrideThemeKey, 0);
        int max = System.Enum.GetValues(typeof(ProceduralArenaGenerator.ArenaTheme)).Length - 1;
        theme = (ProceduralArenaGenerator.ArenaTheme)Mathf.Clamp(raw, 0, Mathf.Max(0, max));
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
            case ProceduralArenaGenerator.ArenaTheme.RuptureZone:
                return "Rupture";
            case ProceduralArenaGenerator.ArenaTheme.DataCore:
                return "Core";
            default:
                return "Archive";
        }
    }

    public static bool ForceBossLevelTwo
    {
        get => PlayerPrefs.GetInt(ForceBossLevelTwoKey, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(ForceBossLevelTwoKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static bool ForceBossLevelThree
    {
        get => PlayerPrefs.GetInt(ForceBossLevelThreeKey, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(ForceBossLevelThreeKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static bool FastRunLoops
    {
        get => PlayerPrefs.GetInt(FastRunLoopsKey, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(FastRunLoopsKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static bool SkipCountdown
    {
        get => PlayerPrefs.GetInt(SkipCountdownKey, 0) == 1;
        set
        {
            PlayerPrefs.SetInt(SkipCountdownKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static float StartTimeSeconds
    {
        get => Mathf.Max(0f, PlayerPrefs.GetFloat(StartTimeSecondsKey, 0f));
        set
        {
            PlayerPrefs.SetFloat(StartTimeSecondsKey, Mathf.Max(0f, value));
            PlayerPrefs.Save();
        }
    }

    public static string GetStartTimeLabel()
    {
        float seconds = StartTimeSeconds;
        if (seconds <= 0.01f)
        {
            return "0s";
        }

        int minutes = Mathf.FloorToInt(seconds / 60f);
        int remaining = Mathf.FloorToInt(seconds % 60f);
        return minutes > 0 ? $"{minutes}m {remaining:00}s" : $"{remaining}s";
    }

    public static string GetDebugSummary()
    {
        string levelTwo = ForceBossLevelTwo ? "N2 ON" : "N2 OFF";
        string levelThree = ForceBossLevelThree ? "N3 ON" : "N3 OFF";
        string fast = FastRunLoops ? "Loops rapidos" : "Loops normales";
        string countdown = SkipCountdown ? "Sin cuenta" : "Cuenta normal";
        return $"{levelTwo} | {levelThree} | Inicio {GetStartTimeLabel()} | {fast} | {countdown}";
    }

    public static void ClearRunDebugOptions()
    {
        PlayerPrefs.DeleteKey(ForceBossLevelTwoKey);
        PlayerPrefs.DeleteKey(ForceBossLevelThreeKey);
        PlayerPrefs.DeleteKey(StartTimeSecondsKey);
        PlayerPrefs.DeleteKey(FastRunLoopsKey);
        PlayerPrefs.DeleteKey(SkipCountdownKey);
        PlayerPrefs.Save();
    }
}

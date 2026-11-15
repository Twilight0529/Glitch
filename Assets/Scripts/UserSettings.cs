using UnityEngine;

// Punto único para opciones del usuario. Sanitiza valores antes de guardarlos para que un PlayerPrefs roto no dañe el juego.
public static class UserSettings
{
    // Guarda y lee preferencias del jugador usando PlayerPrefs.
    public const string MasterVolumeKey = "glitch_master_volume";
    public const string MusicVolumeKey = "glitch_music_volume";
    public const string SfxVolumeKey = "glitch_sfx_volume";
    public const string MenuUiScaleKey = "glitch_menu_ui_scale";
    public const string HudScaleKey = "glitch_hud_scale";
    public const string MenuMotionKey = "glitch_menu_motion";
    public const string FullscreenKey = "glitch_fullscreen";
    public const string VSyncKey = "glitch_vsync";
    public const string IntroTutorialKey = "glitch_intro_tutorial";
    public const string ContextTutorialKey = "glitch_context_tutorial";
    private const string ContextTutorialSeenPrefix = "glitch_context_tutorial_seen_";
    private const string ContextTutorialProgressVersionKey = "glitch_context_tutorial_progress_version";
    private const string ContextTutorialDynamicRegistryKey = "glitch_context_tutorial_dynamic_keys";
    private const int CurrentContextTutorialProgressVersion = 3;
    private static readonly string[] ContextTutorialKeys =
    {
        "movement",
        "parry",
        "ghost_dash",
        "firewall",
        "score_pickup",
        "powerup",
        "upgrade",
        "interface_v2",
        "arena_event_v2",
        "breach",
        "state_hijack_unlock",
        "boss_state",
        "state_hijack"
    };

    public const float DefaultMasterVolume = 0.8f;
    public const float DefaultMusicVolume = 0.85f;
    public const float DefaultSfxVolume = 0.9f;
    public const float DefaultMenuUiScale = 1f;
    public const float DefaultHudScale = 1.1f;
    public const float DefaultMenuMotion = 1f;
    public const bool DefaultFullscreen = false;
    public const bool DefaultVSync = true;
    public const bool DefaultShowIntroTutorial = true;
    public const bool DefaultShowContextTutorial = true;

    public const float MinMenuUiScale = 0.8f;
    public const float MaxMenuUiScale = 1.25f;
    public const float MinHudScale = 0.85f;
    public const float MaxHudScale = 1.55f;
    public const float MinMenuMotion = 0.4f;
    public const float MaxMenuMotion = 1.6f;

    public static float GetMasterVolume()
    {
        return FiniteSetting.Clamp(PlayerPrefs.GetFloat(MasterVolumeKey, DefaultMasterVolume), DefaultMasterVolume, 0f, 1f);
    }

    public static void SetMasterVolume(float value)
    {
        PlayerPrefs.SetFloat(MasterVolumeKey, FiniteSetting.Clamp(value, DefaultMasterVolume, 0f, 1f));
        PlayerPrefs.Save();
    }

    public static float GetMusicVolume()
    {
        return FiniteSetting.Clamp(PlayerPrefs.GetFloat(MusicVolumeKey, DefaultMusicVolume), DefaultMusicVolume, 0f, 1f);
    }

    public static void SetMusicVolume(float value)
    {
        PlayerPrefs.SetFloat(MusicVolumeKey, FiniteSetting.Clamp(value, DefaultMusicVolume, 0f, 1f));
        PlayerPrefs.Save();
    }

    public static float GetSfxVolume()
    {
        return FiniteSetting.Clamp(PlayerPrefs.GetFloat(SfxVolumeKey, DefaultSfxVolume), DefaultSfxVolume, 0f, 1f);
    }

    public static void SetSfxVolume(float value)
    {
        PlayerPrefs.SetFloat(SfxVolumeKey, FiniteSetting.Clamp(value, DefaultSfxVolume, 0f, 1f));
        PlayerPrefs.Save();
    }

    public static float GetMenuUiScale()
    {
        return FiniteSetting.Clamp(
            PlayerPrefs.GetFloat(MenuUiScaleKey, DefaultMenuUiScale),
            DefaultMenuUiScale,
            MinMenuUiScale,
            MaxMenuUiScale);
    }

    public static void SetMenuUiScale(float value)
    {
        PlayerPrefs.SetFloat(MenuUiScaleKey, FiniteSetting.Clamp(value, DefaultMenuUiScale, MinMenuUiScale, MaxMenuUiScale));
        PlayerPrefs.Save();
    }

    public static float GetHudScale()
    {
        return FiniteSetting.Clamp(
            PlayerPrefs.GetFloat(HudScaleKey, DefaultHudScale),
            DefaultHudScale,
            MinHudScale,
            MaxHudScale);
    }

    public static void SetHudScale(float value)
    {
        PlayerPrefs.SetFloat(HudScaleKey, FiniteSetting.Clamp(value, DefaultHudScale, MinHudScale, MaxHudScale));
        PlayerPrefs.Save();
    }

    public static float GetMenuMotion()
    {
        return FiniteSetting.Clamp(
            PlayerPrefs.GetFloat(MenuMotionKey, DefaultMenuMotion),
            DefaultMenuMotion,
            MinMenuMotion,
            MaxMenuMotion);
    }

    public static void SetMenuMotion(float value)
    {
        PlayerPrefs.SetFloat(MenuMotionKey, FiniteSetting.Clamp(value, DefaultMenuMotion, MinMenuMotion, MaxMenuMotion));
        PlayerPrefs.Save();
    }

    public static bool GetFullscreen()
    {
        return PlayerPrefs.GetInt(FullscreenKey, DefaultFullscreen ? 1 : 0) == 1;
    }

    public static void SetFullscreen(bool value)
    {
        PlayerPrefs.SetInt(FullscreenKey, value ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static bool GetVSync()
    {
        return PlayerPrefs.GetInt(VSyncKey, DefaultVSync ? 1 : 0) == 1;
    }

    public static void SetVSync(bool value)
    {
        PlayerPrefs.SetInt(VSyncKey, value ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static bool GetShowIntroTutorial()
    {
        return PlayerPrefs.GetInt(IntroTutorialKey, DefaultShowIntroTutorial ? 1 : 0) == 1;
    }

    public static void SetShowIntroTutorial(bool value)
    {
        PlayerPrefs.SetInt(IntroTutorialKey, value ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static bool GetShowContextTutorial()
    {
        return PlayerPrefs.GetInt(ContextTutorialKey, DefaultShowContextTutorial ? 1 : 0) == 1;
    }

    public static void SetShowContextTutorial(bool value)
    {
        PlayerPrefs.SetInt(ContextTutorialKey, value ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static bool HasSeenContextTutorial(string tutorialKey)
    {
        if (string.IsNullOrWhiteSpace(tutorialKey))
        {
            return true;
        }

        return PlayerPrefs.GetInt(ContextTutorialSeenPrefix + tutorialKey, 0) == 1;
    }

    public static void MarkContextTutorialSeen(string tutorialKey)
    {
        if (string.IsNullOrWhiteSpace(tutorialKey))
        {
            return;
        }

        PlayerPrefs.SetInt(ContextTutorialSeenPrefix + tutorialKey, 1);
        RegisterDynamicTutorialKey(tutorialKey);
        PlayerPrefs.Save();
    }

    private static void RegisterDynamicTutorialKey(string tutorialKey)
    {
        if (!tutorialKey.Contains("_"))
        {
            return;
        }

        string registry = PlayerPrefs.GetString(ContextTutorialDynamicRegistryKey, string.Empty);
        string marker = $"|{tutorialKey}|";
        string wrapped = $"|{registry}|";
        if (wrapped.Contains(marker))
        {
            return;
        }

        PlayerPrefs.SetString(
            ContextTutorialDynamicRegistryKey,
            string.IsNullOrWhiteSpace(registry) ? tutorialKey : $"{registry}|{tutorialKey}");
    }

    public static void EnsureContextTutorialProgressVersion()
    {
        if (PlayerPrefs.GetInt(ContextTutorialProgressVersionKey, 0) >= CurrentContextTutorialProgressVersion)
        {
            return;
        }

        // La version anterior guardaba el tutorial al abrirlo, incluso si la run terminaba antes de completarlo.
        for (int i = 0; i < ContextTutorialKeys.Length; i++)
        {
            PlayerPrefs.DeleteKey(ContextTutorialSeenPrefix + ContextTutorialKeys[i]);
        }
        DeleteDynamicTutorialProgress();

        PlayerPrefs.SetInt(ContextTutorialProgressVersionKey, CurrentContextTutorialProgressVersion);
        PlayerPrefs.Save();
    }

    public static void ResetTutorialProgress()
    {
        SetShowIntroTutorial(DefaultShowIntroTutorial);
        SetShowContextTutorial(DefaultShowContextTutorial);

        for (int i = 0; i < ContextTutorialKeys.Length; i++)
        {
            PlayerPrefs.DeleteKey(ContextTutorialSeenPrefix + ContextTutorialKeys[i]);
        }
        DeleteDynamicTutorialProgress();

        PlayerPrefs.Save();
    }

    private static void DeleteDynamicTutorialProgress()
    {
        string registry = PlayerPrefs.GetString(ContextTutorialDynamicRegistryKey, string.Empty);
        string[] keys = registry.Split('|');
        for (int i = 0; i < keys.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(keys[i]))
            {
                PlayerPrefs.DeleteKey(ContextTutorialSeenPrefix + keys[i]);
            }
        }

        PlayerPrefs.DeleteKey(ContextTutorialDynamicRegistryKey);
    }

    public static void ResetOptions()
    {
        SetMasterVolume(DefaultMasterVolume);
        SetMusicVolume(DefaultMusicVolume);
        SetSfxVolume(DefaultSfxVolume);
        SetMenuUiScale(DefaultMenuUiScale);
        SetHudScale(DefaultHudScale);
        SetMenuMotion(DefaultMenuMotion);
        SetFullscreen(DefaultFullscreen);
        SetVSync(DefaultVSync);
        SetShowIntroTutorial(DefaultShowIntroTutorial);
        SetShowContextTutorial(DefaultShowContextTutorial);
    }
}

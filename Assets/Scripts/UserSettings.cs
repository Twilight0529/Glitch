using UnityEngine;

public static class UserSettings
{
    // Guarda y lee preferencias del jugador usando PlayerPrefs.
    public const string MasterVolumeKey = "glitch_master_volume";
    public const string MenuUiScaleKey = "glitch_menu_ui_scale";
    public const string HudScaleKey = "glitch_hud_scale";
    public const string MenuMotionKey = "glitch_menu_motion";
    public const string FullscreenKey = "glitch_fullscreen";
    public const string VSyncKey = "glitch_vsync";

    public const float DefaultMasterVolume = 0.8f;
    public const float DefaultMenuUiScale = 1f;
    public const float DefaultHudScale = 1.1f;
    public const float DefaultMenuMotion = 1f;
    public const bool DefaultFullscreen = false;
    public const bool DefaultVSync = true;

    public const float MinMenuUiScale = 0.8f;
    public const float MaxMenuUiScale = 1.25f;
    public const float MinHudScale = 0.85f;
    public const float MaxHudScale = 1.55f;
    public const float MinMenuMotion = 0.4f;
    public const float MaxMenuMotion = 1.6f;

    public static float GetMasterVolume()
    {
        return Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, DefaultMasterVolume));
    }

    public static void SetMasterVolume(float value)
    {
        PlayerPrefs.SetFloat(MasterVolumeKey, Mathf.Clamp01(value));
        PlayerPrefs.Save();
    }

    public static float GetMenuUiScale()
    {
        return Mathf.Clamp(
            PlayerPrefs.GetFloat(MenuUiScaleKey, DefaultMenuUiScale),
            MinMenuUiScale,
            MaxMenuUiScale);
    }

    public static void SetMenuUiScale(float value)
    {
        PlayerPrefs.SetFloat(MenuUiScaleKey, Mathf.Clamp(value, MinMenuUiScale, MaxMenuUiScale));
        PlayerPrefs.Save();
    }

    public static float GetHudScale()
    {
        return Mathf.Clamp(
            PlayerPrefs.GetFloat(HudScaleKey, DefaultHudScale),
            MinHudScale,
            MaxHudScale);
    }

    public static void SetHudScale(float value)
    {
        PlayerPrefs.SetFloat(HudScaleKey, Mathf.Clamp(value, MinHudScale, MaxHudScale));
        PlayerPrefs.Save();
    }

    public static float GetMenuMotion()
    {
        return Mathf.Clamp(
            PlayerPrefs.GetFloat(MenuMotionKey, DefaultMenuMotion),
            MinMenuMotion,
            MaxMenuMotion);
    }

    public static void SetMenuMotion(float value)
    {
        PlayerPrefs.SetFloat(MenuMotionKey, Mathf.Clamp(value, MinMenuMotion, MaxMenuMotion));
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

    public static void ResetOptions()
    {
        SetMasterVolume(DefaultMasterVolume);
        SetMenuUiScale(DefaultMenuUiScale);
        SetHudScale(DefaultHudScale);
        SetMenuMotion(DefaultMenuMotion);
        SetFullscreen(DefaultFullscreen);
        SetVSync(DefaultVSync);
    }
}

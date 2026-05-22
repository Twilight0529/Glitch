using UnityEngine;

public static class UserSettings
{
    // Guarda y lee preferencias del jugador usando PlayerPrefs.
    public const string MasterVolumeKey = "glitch_master_volume";
    public const string MenuUiScaleKey = "glitch_menu_ui_scale";
    public const string HudScaleKey = "glitch_hud_scale";

    public const float DefaultMasterVolume = 0.8f;
    public const float DefaultMenuUiScale = 1f;
    public const float DefaultHudScale = 1.1f;

    public const float MinMenuUiScale = 0.8f;
    public const float MaxMenuUiScale = 1.25f;
    public const float MinHudScale = 0.85f;
    public const float MaxHudScale = 1.55f;

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
}

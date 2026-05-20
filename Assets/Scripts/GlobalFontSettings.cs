using UnityEngine;

/// <summary>
/// Global two-font registry for UI: one primary (important) and one secondary.
/// Add this component to a scene object and assign fonts from Inspector.
/// </summary>
[DefaultExecutionOrder(-1000)]
public class GlobalFontSettings : MonoBehaviour
{
    [Header("Global Fonts")]
    [SerializeField] private Font importantFont;
    [SerializeField] private Font secondaryFont;
    [SerializeField] private bool persistAcrossScenes = true;

    private static GlobalFontSettings instance;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (persistAcrossScenes)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    public static Font GetImportantFont()
    {
        EnsureInstance();
        if (instance != null && instance.importantFont != null)
        {
            return instance.importantFont;
        }

        return GetSafeFallbackFont();
    }

    public static Font GetSecondaryFont()
    {
        EnsureInstance();
        if (instance != null)
        {
            if (instance.secondaryFont != null)
            {
                return instance.secondaryFont;
            }

            if (instance.importantFont != null)
            {
                return instance.importantFont;
            }
        }

        return GetSafeFallbackFont();
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        instance = FindAnyObjectByType<GlobalFontSettings>();
    }

    private static Font GetSafeFallbackFont()
    {
        Font fallback = TryGetBuiltinFont("LegacyRuntime.ttf");
        if (fallback != null)
        {
            return fallback;
        }

        if (GUI.skin != null && GUI.skin.label != null)
        {
            return GUI.skin.label.font;
        }

        return null;
    }

    private static Font TryGetBuiltinFont(string path)
    {
        try
        {
            return Resources.GetBuiltinResource<Font>(path);
        }
        catch
        {
            return null;
        }
    }
}


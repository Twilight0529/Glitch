using UnityEngine;

public class MainMenuController : MonoBehaviour
{
    private const float BandOverlayAlpha = 0.26f;

    [Header("Main Menu")]
    [SerializeField] private string gameTitle = "GLITCH";
    [SerializeField] private string gameSubtitle = "Containment Breach";
    [SerializeField] private string gameplaySceneName = "Game";
    [SerializeField] private Font titleFontOverride;
    [SerializeField] private Font uiFontOverride;

    [Header("Options (Generic)")]
    [SerializeField] private float masterVolume = 0.8f;
    [SerializeField] private float uiScale = 1f;

    [Header("Menu Reveal")]
    [SerializeField] private float menuElementsFadeDelay = 0.5f;
    [SerializeField] private float menuElementsFadeDuration = 0.65f;

    private bool showOptions;
    private Font titleFont;
    private Font uiFont;
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle bandLabelStyle;
    private GUIStyle buttonStyle;
    private GUIStyle panelTitleStyle;
    private GUIStyle paragraphStyle;
    private int cachedScreenWidth;
    private int cachedScreenHeight;
    private float cachedUiScale = -1f;
    private bool revealTimerStarted;
    private float revealStartTimestamp;
    private float menuElementsAlpha;

    private void Awake()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
        ResolveFonts();
        ResetMenuReveal();
    }

    private void OnGUI()
    {
        EnsureStyles();
        DrawThematicBackground();

        UpdateMenuReveal();

        if (SceneTransitionController.IsFading)
        {
            return;
        }

        if (showOptions)
        {
            DrawOptionsMenu();
            return;
        }

        DrawMainMenu();
    }

    private static Rect CenterRect(float width, float height)
    {
        return new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
    }

    private void DrawMainMenu()
    {
        if (menuElementsAlpha <= 0.001f)
        {
            return;
        }

        DrawScreenFade(0.38f);
        float bob = Mathf.Sin(Time.unscaledTime * 1.35f) * 6f;
        Rect panel = CenterRect(390f, 360f);
        panel.y += bob;
        Color old = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, menuElementsAlpha);
        DrawPanel(panel, new Color(0.03f, 0.05f, 0.09f, 0.88f), new Color(0.47f, 0.56f, 0.72f, 0.55f));

        Rect area = new Rect(panel.x + 18f, panel.y + 14f, panel.width - 36f, panel.height - 24f);
        GUILayout.BeginArea(area);
        GUILayout.Label(gameTitle, titleStyle);
        GUILayout.Space(2f);
        GUILayout.Label(gameSubtitle, subtitleStyle);
        GUILayout.Space(20f);

        if (DrawMenuButton("Jugar", 44f))
        {
            StartGameplay();
        }

        GUILayout.Space(10f);
        if (DrawMenuButton("Opciones", 38f))
        {
            showOptions = true;
        }

        GUILayout.Space(10f);
        if (DrawMenuButton("Salir", 38f))
        {
            ExitGame();
        }

        GUILayout.EndArea();
        GUI.color = old;
    }

    private void DrawOptionsMenu()
    {
        if (menuElementsAlpha <= 0.001f)
        {
            return;
        }

        DrawScreenFade(0.46f);
        float bob = Mathf.Sin(Time.unscaledTime * 1.2f + 1.2f) * 5f;
        Rect panel = CenterRect(420f, 380f);
        panel.y += bob;
        Color old = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, menuElementsAlpha);
        DrawPanel(panel, new Color(0.03f, 0.05f, 0.09f, 0.90f), new Color(0.47f, 0.56f, 0.72f, 0.55f));

        Rect area = new Rect(panel.x + 18f, panel.y + 16f, panel.width - 36f, panel.height - 28f);
        GUILayout.BeginArea(area);
        GUILayout.Label("Opciones", panelTitleStyle);
        GUILayout.Space(6f);
        GUILayout.Label("Ajustes genericos (placeholder)", paragraphStyle);
        GUILayout.Space(12f);

        GUILayout.Label($"Volumen Maestro: {masterVolume:F2}", paragraphStyle);
        masterVolume = GUILayout.HorizontalSlider(masterVolume, 0f, 1f);

        GUILayout.Space(10f);
        GUILayout.Label($"Escala UI: {uiScale:F2}", paragraphStyle);
        uiScale = GUILayout.HorizontalSlider(uiScale, 0.8f, 1.2f);

        GUILayout.FlexibleSpace();
        if (DrawMenuButton("Volver", 38f))
        {
            showOptions = false;
        }

        GUILayout.EndArea();
        GUI.color = old;
    }

    private void StartGameplay()
    {
        if (!string.IsNullOrWhiteSpace(gameplaySceneName) && Application.CanStreamedLevelBeLoaded(gameplaySceneName))
        {
            SceneTransitionController.LoadScene(gameplaySceneName);
            return;
        }

        Debug.LogError($"Gameplay scene '{gameplaySceneName}' is not in Build Settings.");
    }

    private static void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static void DrawScreenFade(float alpha)
    {
        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = old;
    }

    private void DrawThematicBackground()
    {
        float bandWidth = Screen.width / 3f;
        float t = Time.unscaledTime;

        Rect lab = new Rect(0f, 0f, bandWidth, Screen.height);
        Rect storage = new Rect(bandWidth, 0f, bandWidth, Screen.height);
        Rect rupture = new Rect(bandWidth * 2f, 0f, Screen.width - bandWidth * 2f, Screen.height);

        DrawBand(lab, new Color(0.14f, 0.18f, 0.26f), "LAB", 0, t);
        DrawBand(storage, new Color(0.28f, 0.20f, 0.14f), "STORAGE", 1, t);
        DrawBand(rupture, new Color(0.25f, 0.16f, 0.31f), "RUPTURE", 2, t);

        DrawSolidRect(new Rect(bandWidth - 1f, 0f, 2f, Screen.height), new Color(1f, 1f, 1f, 0.14f));
        DrawSolidRect(new Rect(bandWidth * 2f - 1f, 0f, 2f, Screen.height), new Color(1f, 1f, 1f, 0.14f));
    }

    private void DrawBand(Rect band, Color baseColor, string label, int motifType, float time)
    {
        DrawSolidRect(band, baseColor);
        DrawSolidRect(band, new Color(0f, 0f, 0f, BandOverlayAlpha));

        switch (motifType)
        {
            case 0:
                DrawLabMotif(band, time);
                break;
            case 1:
                DrawStorageMotif(band, time);
                break;
            default:
                DrawRuptureMotif(band, time);
                break;
        }

        Rect labelRect = new Rect(band.x + 12f, band.y + 10f, band.width - 24f, 30f);
        GUI.Label(labelRect, label, bandLabelStyle);
    }

    private static void DrawLabMotif(Rect band, float time)
    {
        Color lane = new Color(0.50f, 0.63f, 0.83f, 0.24f);
        Color obstacle = new Color(0.31f, 0.38f, 0.49f, 0.58f);
        float pulse = 0.5f + 0.5f * Mathf.Sin(time * 1.7f);
        float yMid = band.y + band.height * (0.44f + Mathf.Sin(time * 0.35f) * 0.02f);
        float xMid = band.x + band.width * (0.52f + Mathf.Sin(time * 0.4f + 1.1f) * 0.02f);

        DrawSolidRect(new Rect(band.x + 16f, yMid - 16f, band.width - 32f, 32f), new Color(0f, 0f, 0f, 0.22f));
        DrawSolidRect(new Rect(xMid - 16f, band.y + 18f, 32f, band.height - 36f), new Color(0f, 0f, 0f, 0.22f));
        DrawSolidRect(new Rect(band.x + 16f, yMid - 2f, band.width - 32f, 4f), lane);
        DrawSolidRect(new Rect(xMid - 2f, band.y + 18f, 4f, band.height - 36f), lane);

        for (float y = band.y + 36f; y < band.yMax - 40f; y += 84f)
        {
            DrawSolidRect(new Rect(band.x + 24f, y, 58f, 30f), obstacle);
            DrawSolidRect(new Rect(band.xMax - 82f, y + 10f, 56f, 28f), obstacle);
        }

        DrawSolidRect(new Rect(band.x + 22f, yMid - 22f, 10f, 44f), new Color(0.65f, 0.78f, 0.98f, 0.12f + pulse * 0.18f));
    }

    private static void DrawStorageMotif(Rect band, float time)
    {
        Color crate = new Color(0.88f, 0.69f, 0.43f, 0.42f);
        Color accent = new Color(0.95f, 0.78f, 0.52f, 0.22f);
        Vector2[] clusterCenters =
        {
            new Vector2(band.x + band.width * 0.30f, band.y + band.height * 0.28f),
            new Vector2(band.x + band.width * 0.70f, band.y + band.height * 0.30f),
            new Vector2(band.x + band.width * 0.36f, band.y + band.height * 0.72f),
            new Vector2(band.x + band.width * 0.72f, band.y + band.height * 0.70f)
        };

        for (int c = 0; c < clusterCenters.Length; c++)
        {
            Vector2 center = clusterCenters[c];
            float driftX = Mathf.Sin(time * 0.65f + c * 1.8f) * 4f;
            float driftY = Mathf.Cos(time * 0.58f + c * 1.3f) * 3f;
            center += new Vector2(driftX, driftY);

            for (int i = 0; i < 5; i++)
            {
                float ox = (i % 3 - 1) * 26f + Mathf.Sin((c + i) * 1.7f) * 4f;
                float oy = (i / 3 - 0.5f) * 32f + Mathf.Cos((c + i) * 1.2f) * 4f;
                float w = 26f + (i % 3) * 10f;
                float h = 18f + (i % 2) * 8f;
                Rect r = new Rect(center.x + ox - w * 0.5f, center.y + oy - h * 0.5f, w, h);
                DrawSolidRect(r, crate);
                DrawSolidRect(new Rect(r.x + 2f, r.y + 2f, r.width - 4f, 2f), accent);
            }
        }
    }

    private static void DrawRuptureMotif(Rect band, float time)
    {
        Vector2 center = new Vector2(band.x + band.width * 0.5f, band.y + band.height * 0.5f);
        float pulse = 0.5f + 0.5f * Mathf.Sin(time * 1.05f);
        Color ring = new Color(0.96f, 0.55f, 0.84f, 0.22f);
        DrawEllipseRing(center, band.width * (0.13f + pulse * 0.01f), band.height * (0.18f + pulse * 0.01f), ring);
        DrawEllipseRing(center, band.width * (0.22f + pulse * 0.012f), band.height * (0.31f + pulse * 0.012f), ring);
        DrawEllipseRing(center, band.width * (0.31f + pulse * 0.014f), band.height * (0.44f + pulse * 0.014f), ring);
        DrawFilledCircle(center, 8f + pulse * 2f, new Color(0.98f, 0.62f, 0.86f, 0.36f));

        Color obstacle = new Color(0.56f, 0.39f, 0.62f, 0.46f);
        float radiusX = band.width * 0.30f;
        float radiusY = band.height * 0.43f;
        for (int i = 0; i < 12; i++)
        {
            float ang = (i / 12f) * Mathf.PI * 2f + time * 0.09f;
            float x = center.x + Mathf.Cos(ang) * radiusX;
            float y = center.y + Mathf.Sin(ang) * radiusY;
            float w = i % 3 == 0 ? 28f : 20f;
            float h = i % 3 == 0 ? 10f : 18f;
            DrawSolidRect(new Rect(x - w * 0.5f, y - h * 0.5f, w, h), obstacle);
        }
    }

    private static void DrawEllipseRing(Vector2 center, float radiusX, float radiusY, Color color)
    {
        const int segments = 90;
        for (int i = 0; i < segments; i += 2)
        {
            float t = i / (float)segments * Mathf.PI * 2f;
            float x = center.x + Mathf.Cos(t) * radiusX;
            float y = center.y + Mathf.Sin(t) * radiusY;
            DrawSolidRect(new Rect(x, y, 3f, 3f), color);
        }
    }

    private static void DrawFilledCircle(Vector2 center, float radius, Color color)
    {
        for (float y = -radius; y <= radius; y += 1f)
        {
            float x = Mathf.Sqrt(Mathf.Max(0f, radius * radius - y * y));
            DrawSolidRect(new Rect(center.x - x, center.y + y, x * 2f, 1f), color);
        }
    }

    private bool DrawMenuButton(string label, float height)
    {
        bool canInteract = menuElementsAlpha >= 0.99f && !SceneTransitionController.IsFading;
        Color old = GUI.color;
        float pulse = 0.86f + 0.14f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2f));
        GUI.color = new Color(pulse, pulse, pulse, 1f);
        bool oldEnabled = GUI.enabled;
        GUI.enabled = canInteract;
        bool clicked = GUILayout.Button(label, buttonStyle, GUILayout.Height(height));
        GUI.enabled = oldEnabled;
        GUI.color = old;
        return clicked;
    }

    private void DrawPanel(Rect rect, Color fill, Color border)
    {
        DrawSolidRect(rect, fill);
        DrawSolidRect(new Rect(rect.x, rect.y, rect.width, 2f), border);
        DrawSolidRect(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), border);
        DrawSolidRect(new Rect(rect.x, rect.y, 2f, rect.height), border);
        DrawSolidRect(new Rect(rect.xMax - 2f, rect.y, 2f, rect.height), border);
    }

    private void EnsureStyles()
    {
        if (titleStyle != null &&
            cachedScreenWidth == Screen.width &&
            cachedScreenHeight == Screen.height &&
            Mathf.Abs(cachedUiScale - uiScale) < 0.001f)
        {
            return;
        }

        cachedScreenWidth = Screen.width;
        cachedScreenHeight = Screen.height;
        cachedUiScale = uiScale;

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            font = titleFont,
            fontSize = Mathf.RoundToInt(48f * uiScale),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        titleStyle.normal.textColor = new Color(0.94f, 0.97f, 1f, 1f);

        subtitleStyle = new GUIStyle(GUI.skin.label)
        {
            font = uiFont,
            fontSize = Mathf.RoundToInt(16f * uiScale),
            fontStyle = FontStyle.Italic,
            alignment = TextAnchor.MiddleCenter
        };
        subtitleStyle.normal.textColor = new Color(0.80f, 0.86f, 0.93f, 0.95f);

        panelTitleStyle = new GUIStyle(GUI.skin.label)
        {
            font = titleFont,
            fontSize = Mathf.RoundToInt(30f * uiScale),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        panelTitleStyle.normal.textColor = new Color(0.93f, 0.96f, 1f, 1f);

        paragraphStyle = new GUIStyle(GUI.skin.label)
        {
            font = uiFont,
            fontSize = Mathf.RoundToInt(15f * uiScale),
            alignment = TextAnchor.MiddleLeft
        };
        paragraphStyle.normal.textColor = new Color(0.86f, 0.90f, 0.96f, 0.95f);

        bandLabelStyle = new GUIStyle(GUI.skin.label)
        {
            font = titleFont,
            fontSize = Mathf.RoundToInt(22f * uiScale),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperRight
        };
        bandLabelStyle.normal.textColor = new Color(1f, 1f, 1f, 0.92f);

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            font = uiFont,
            fontSize = Mathf.RoundToInt(18f * uiScale),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
    }

    private void ResolveFonts()
    {
        // Safe fallback for IMGUI across Unity versions (Unity 6+).
        Font fallback = TryGetBuiltinFont("LegacyRuntime.ttf");
        if (fallback == null)
        {
            fallback = TryGetBuiltinFont("Arial.ttf");
        }

        if (fallback == null && GUI.skin != null && GUI.skin.label != null)
        {
            fallback = GUI.skin.label.font;
        }

        titleFont = titleFontOverride != null ? titleFontOverride : fallback;
        uiFont = uiFontOverride != null ? uiFontOverride : fallback;
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

    private static void DrawSolidRect(Rect rect, Color color)
    {
        Color old = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = old;
    }

    private void ResetMenuReveal()
    {
        revealTimerStarted = false;
        revealStartTimestamp = 0f;
        menuElementsAlpha = 0f;
    }

    private void UpdateMenuReveal()
    {
        if (SceneTransitionController.IsFading)
        {
            ResetMenuReveal();
            return;
        }

        if (!revealTimerStarted)
        {
            revealTimerStarted = true;
            revealStartTimestamp = Time.unscaledTime;
        }

        float elapsed = Time.unscaledTime - revealStartTimestamp;
        float t = (elapsed - menuElementsFadeDelay) / Mathf.Max(0.01f, menuElementsFadeDuration);
        menuElementsAlpha = Mathf.Clamp01(t);
    }
}

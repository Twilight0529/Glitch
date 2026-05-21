using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class GameMenuController : MonoBehaviour
{
    private enum OverlayState
    {
        Playing,
        Paused,
        Defeat
    }

    [Header("References")]
    [SerializeField] private GameManager gameManager;

    [Header("Scene Flow")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Defeat Theme")]
    [SerializeField] private string defeatTitle = "CONTAINMENT FAILURE";
    [SerializeField] private string defeatSubtitle = "La anomalia te alcanzo.";
    [SerializeField] private float defeatPulseSpeed = 1.8f;
    [SerializeField] private float glitchJitterStrength = 6.5f;
    [SerializeField] private float glitchSplitStrength = 2.2f;
    [SerializeField] private float glitchBarOpacity = 0.14f;
    [SerializeField, Range(0f, 1f)] private float hudGlitchWeight = 0.85f;
    [SerializeField, Range(0f, 1f)] private float textGlitchWeight = 0.35f;
    [SerializeField] private float panelJitterStrength = 1.1f;

    public static bool ShouldHideGameplayHud { get; private set; }

    private OverlayState state;
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle bodyStyle;
    private GUIStyle buttonStyle;
    private Font importantFont;
    private Font secondaryFont;

    private void Awake()
    {
        if (gameManager == null)
        {
            gameManager = FindAnyObjectByType<GameManager>();
        }

        importantFont = GlobalFontSettings.GetImportantFont();
        secondaryFont = GlobalFontSettings.GetSecondaryFont();
        SetState(OverlayState.Playing);
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
        ShouldHideGameplayHud = false;
        Cursor.visible = true;
    }

    private void Update()
    {
        if (SceneTransitionController.IsFading)
        {
            return;
        }

        if (gameManager == null)
        {
            gameManager = FindAnyObjectByType<GameManager>();
        }

        if (gameManager != null && gameManager.IsGameOver && state != OverlayState.Defeat)
        {
            SetState(OverlayState.Defeat);
            return;
        }

        // During startup delay/countdown/GO flash (but not game over), GameManager owns time scale.
        // We ignore pause input and force gameplay overlay state to avoid desync.
        if (gameManager != null && !gameManager.IsRunActive && !gameManager.IsGameOver)
        {
            if (state != OverlayState.Playing)
            {
                SetState(OverlayState.Playing);
            }

            return;
        }

        if (state == OverlayState.Defeat)
        {
            return;
        }

        if (GetEscapePressedThisFrame())
        {
            if (state == OverlayState.Paused)
            {
                SetState(OverlayState.Playing);
            }
            else if (state == OverlayState.Playing)
            {
                SetState(OverlayState.Paused);
            }
        }
    }

    private static bool GetEscapePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Escape);
#else
        return false;
#endif
    }

    private void SetState(OverlayState nextState)
    {
        state = nextState;
        bool gameplayActive = state == OverlayState.Playing;

        bool runCanAdvance = gameManager == null || gameManager.IsRunActive;
        Time.timeScale = gameplayActive && runCanAdvance ? 1f : 0f;
        ShouldHideGameplayHud = !gameplayActive;
        Cursor.visible = !gameplayActive;
    }

    private void RestartLevel()
    {
        Time.timeScale = 1f;
        SceneTransitionController.ReloadActiveScene();
    }

    private void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        if (!string.IsNullOrWhiteSpace(mainMenuSceneName) && Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            SceneTransitionController.LoadScene(mainMenuSceneName);
            return;
        }

        Debug.LogWarning($"Main menu scene '{mainMenuSceneName}' is not in Build Settings. Loading scene index 0.");
        SceneTransitionController.LoadScene(0);
    }

    private void OnGUI()
    {
        if (SceneTransitionController.IsFading)
        {
            return;
        }

        EnsureStyles();

        if (state == OverlayState.Paused)
        {
            DrawPauseMenu();
        }
        else if (state == OverlayState.Defeat)
        {
            DrawDefeatMenu();
        }
    }

    private void EnsureStyles()
    {
        if (titleStyle != null)
        {
            return;
        }

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            font = importantFont,
            fontSize = 34,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = false,
            clipping = TextClipping.Clip
        };
        titleStyle.normal.textColor = new Color(1f, 0.58f, 0.64f, 1f);

        subtitleStyle = new GUIStyle(GUI.skin.label)
        {
            font = secondaryFont,
            fontSize = 16,
            fontStyle = FontStyle.Italic,
            alignment = TextAnchor.MiddleCenter
        };
        subtitleStyle.normal.textColor = new Color(0.95f, 0.90f, 0.92f, 0.95f);

        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            font = secondaryFont,
            fontSize = 15,
            alignment = TextAnchor.MiddleLeft
        };
        bodyStyle.normal.textColor = new Color(0.90f, 0.93f, 1f, 0.95f);

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            font = importantFont,
            fontSize = 17,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
    }

    private static Rect CenterRect(float width, float height)
    {
        return new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
    }

    private void DrawPauseMenu()
    {
        DrawScreenFade(0.62f);
        Rect panel = CenterRect(340f, 270f);
        DrawPanel(panel, new Color(0.05f, 0.07f, 0.12f, 0.90f), new Color(0.58f, 0.66f, 0.86f, 0.52f));

        Rect area = new Rect(panel.x + 16f, panel.y + 14f, panel.width - 32f, panel.height - 24f);
        GUILayout.BeginArea(area);
        GUILayout.Label("Pausa", titleStyle);
        GUILayout.Space(16f);

        if (GUILayout.Button("Continuar", buttonStyle, GUILayout.Height(38f)))
        {
            SetState(OverlayState.Playing);
        }

        GUILayout.Space(8f);
        if (GUILayout.Button("Reiniciar", buttonStyle, GUILayout.Height(38f)))
        {
            RestartLevel();
        }

        GUILayout.Space(8f);
        if (GUILayout.Button("Menu Principal", buttonStyle, GUILayout.Height(38f)))
        {
            ReturnToMainMenu();
        }

        GUILayout.EndArea();
    }

    private void DrawDefeatMenu()
    {
        float t = Time.unscaledTime;
        float pulse = 0.5f + 0.5f * Mathf.Sin(t * defeatPulseSpeed);
        float glitch = Mathf.Clamp01(0.20f + pulse * 0.50f);
        float hudGlitch = glitch * Mathf.Clamp01(hudGlitchWeight);
        float textGlitch = glitch * Mathf.Clamp01(textGlitchWeight);
        DrawScreenFade(0.70f);
        DrawDefeatBackdrop(pulse, hudGlitch, t);

        Rect panel = CenterRect(470f, 360f);
        float cappedJitter = Mathf.Min(glitchJitterStrength, 2.0f) * panelJitterStrength;
        float jitterX = (Mathf.PerlinNoise(t * 13.5f, 0.37f) - 0.5f) * 2f * cappedJitter * textGlitch;
        float jitterY = (Mathf.PerlinNoise(0.77f, t * 9.2f) - 0.5f) * 2f * (cappedJitter * 0.28f) * textGlitch;
        panel.x += jitterX;
        panel.y += jitterY;
        DrawPanel(panel, new Color(0.07f, 0.03f, 0.06f, 0.92f), new Color(0.94f, 0.42f, 0.55f, 0.65f));

        Rect area = new Rect(panel.x + 20f, panel.y + 14f, panel.width - 40f, panel.height - 24f);
        GUILayout.BeginArea(area);
        GUIStyle fittedTitleStyle = GetFittedSingleLineStyle(titleStyle, defeatTitle, area.width, 34, 24);
        Rect titleRect = new Rect(0f, 0f, area.width, 48f);
        DrawGlitchLabel(titleRect, defeatTitle, fittedTitleStyle, textGlitch * 0.30f);
        GUILayout.Space(48f);
        GUILayout.Space(2f);
        Rect subtitleRect = new Rect(0f, 46f, area.width, 24f);
        DrawGlitchLabel(subtitleRect, defeatSubtitle, subtitleStyle, textGlitch * 0.45f);
        GUILayout.Space(24f);
        GUILayout.Space(16f);

        string level = gameManager != null ? gameManager.CurrentLevelTypeLabel : "Unknown";
        float time = gameManager != null ? gameManager.SurvivalTime : 0f;
        float threat = gameManager != null ? gameManager.DifficultyMultiplier : 1f;

        GUILayout.Label($"Tiempo sobrevivido: {time:F1}s", bodyStyle);
        GUILayout.Label($"Nivel de amenaza final: x{threat:F2}", bodyStyle);
        GUILayout.Label($"Zona de contencion: {level}", bodyStyle);

        GUILayout.Space(24f);

        if (GUILayout.Button("Reiniciar", buttonStyle, GUILayout.Height(40f)))
        {
            RestartLevel();
        }

        GUILayout.Space(10f);
        if (GUILayout.Button("Menu Principal", buttonStyle, GUILayout.Height(40f)))
        {
            ReturnToMainMenu();
        }

        GUILayout.EndArea();
    }

    private void DrawDefeatBackdrop(float pulse, float glitch, float time)
    {
        Color ring = new Color(0.98f, 0.45f, 0.60f, 0.18f + pulse * 0.10f);
        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        DrawEllipseRing(center, Screen.width * 0.18f, Screen.height * 0.20f, ring);
        DrawEllipseRing(center, Screen.width * 0.30f, Screen.height * 0.33f, ring);

        Color stripe = new Color(1f, 0.28f, 0.42f, 0.11f + pulse * 0.06f);
        float spacing = 90f;
        for (float y = 0f; y < Screen.height + spacing; y += spacing)
        {
            DrawSolidRect(new Rect(-40f, y, Screen.width + 80f, 8f), stripe);
        }

        float barStep = Mathf.Max(24f, Screen.height / 15f);
        for (float y = 0f; y < Screen.height + barStep; y += barStep)
        {
            float flow = Mathf.Repeat(y * 0.012f + time * 1.3f, 1f);
            float width = Screen.width * Mathf.Lerp(0.2f, 0.82f, flow);
            float offset = (Mathf.PerlinNoise(y * 0.03f, time * 5.2f) - 0.5f) * 60f * glitch;
            float x = (Screen.width - width) * 0.5f + offset;
            Color bar = new Color(1f, 0.34f, 0.48f, glitchBarOpacity * glitch);
            DrawSolidRect(new Rect(x, y, width, 2f), bar);
        }

        DrawHudGlitchOverlay(glitch, time);
    }

    private void DrawGlitchLabel(Rect rect, string text, GUIStyle style, float glitch)
    {
        float cappedSplit = Mathf.Min(glitchSplitStrength, 1.15f);
        float split = cappedSplit * glitch;
        Color old = GUI.color;

        GUI.color = new Color(1f, 0.28f, 0.38f, 0.55f * glitch);
        GUI.Label(new Rect(rect.x - split, rect.y, rect.width, rect.height), text, style);

        GUI.color = new Color(0.24f, 0.98f, 1f, 0.50f * glitch);
        GUI.Label(new Rect(rect.x + split, rect.y, rect.width, rect.height), text, style);

        GUI.color = old;
        GUI.Label(rect, text, style);
    }

    private static GUIStyle GetFittedSingleLineStyle(GUIStyle source, string text, float maxWidth, int maxFont, int minFont)
    {
        GUIStyle style = new GUIStyle(source)
        {
            wordWrap = false,
            clipping = TextClipping.Clip
        };

        GUIContent content = new GUIContent(text);
        float targetWidth = Mathf.Max(12f, maxWidth - 10f);
        int top = Mathf.Max(maxFont, minFont);
        int bottom = Mathf.Min(maxFont, minFont);

        int chosen = bottom;
        for (int size = top; size >= bottom; size--)
        {
            style.fontSize = size;
            if (style.CalcSize(content).x <= targetWidth)
            {
                chosen = size;
                break;
            }
        }

        style.fontSize = chosen;
        return style;
    }

    private static void DrawHudGlitchOverlay(float glitch, float time)
    {
        if (glitch <= 0.001f)
        {
            return;
        }

        // Soft moving interference blocks: affects the scene/HUD feel without breaking menu readability.
        int blocks = Mathf.RoundToInt(Mathf.Lerp(6f, 16f, glitch));
        for (int i = 0; i < blocks; i++)
        {
            float seed = i * 0.173f;
            float px = Mathf.PerlinNoise(seed, time * 0.45f + seed * 1.7f) * Screen.width;
            float py = Mathf.PerlinNoise(seed * 2.1f, time * 0.38f + 3.1f) * Screen.height;
            float w = Mathf.Lerp(36f, 110f, Mathf.PerlinNoise(seed * 3.7f, time * 0.21f));
            float h = Mathf.Lerp(12f, 42f, Mathf.PerlinNoise(seed * 4.3f, time * 0.18f));
            Color c = i % 2 == 0
                ? new Color(1f, 0.30f, 0.45f, 0.03f + 0.06f * glitch)
                : new Color(0.35f, 0.90f, 1f, 0.02f + 0.04f * glitch);
            DrawSolidRect(new Rect(px - w * 0.5f, py - h * 0.5f, w, h), c);
        }

        // Thin scanline glitches, mostly horizontal.
        int lines = Mathf.RoundToInt(Mathf.Lerp(4f, 12f, glitch));
        for (int i = 0; i < lines; i++)
        {
            float y = Mathf.PerlinNoise(i * 0.66f, time * 0.9f + i) * Screen.height;
            float drift = (Mathf.PerlinNoise(i * 0.44f, time * 3.1f) - 0.5f) * 22f;
            float width = Screen.width * Mathf.Lerp(0.25f, 0.8f, Mathf.PerlinNoise(i * 0.22f, time * 0.35f));
            float x = (Screen.width - width) * 0.5f + drift;
            DrawSolidRect(new Rect(x, y, width, 1.5f), new Color(1f, 0.36f, 0.50f, 0.05f + 0.07f * glitch));
        }
    }

    private static void DrawEllipseRing(Vector2 center, float radiusX, float radiusY, Color color)
    {
        const int segments = 110;
        for (int i = 0; i < segments; i += 2)
        {
            float t = i / (float)segments * Mathf.PI * 2f;
            float x = center.x + Mathf.Cos(t) * radiusX;
            float y = center.y + Mathf.Sin(t) * radiusY;
            DrawSolidRect(new Rect(x, y, 3f, 3f), color);
        }
    }

    private static void DrawScreenFade(float alpha)
    {
        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = old;
    }

    private static void DrawPanel(Rect rect, Color fill, Color border)
    {
        DrawSolidRect(rect, fill);
        DrawSolidRect(new Rect(rect.x, rect.y, rect.width, 2f), border);
        DrawSolidRect(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), border);
        DrawSolidRect(new Rect(rect.x, rect.y, 2f, rect.height), border);
        DrawSolidRect(new Rect(rect.xMax - 2f, rect.y, 2f, rect.height), border);
    }

    private static void DrawSolidRect(Rect rect, Color color)
    {
        Color old = GUI.color;
        GUI.color = color; 
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = old;  
    } 
}

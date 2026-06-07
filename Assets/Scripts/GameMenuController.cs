using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class GameMenuController : MonoBehaviour
{
    // Capa de menu en partida: pausa, pantalla de derrota, registro de ranking y navegacion de escena.
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
    [SerializeField] private float defeatCinematicDuration = 1.05f;
    [SerializeField] private float defeatInputUnlockDelay = 1.2f;
    [SerializeField] private float defeatFlashDuration = 0.16f;
    [SerializeField] private float defeatShockwaveDuration = 0.95f;
    [SerializeField] private Color defeatFlashColor = new Color(1f, 0.72f, 0.78f, 1f);
    [SerializeField] private Color defeatShockwaveColor = new Color(1f, 0.44f, 0.58f, 1f);

    [Header("Ranking")]
    [SerializeField] private int rankingNameMaxLength = 16;

    public static bool ShouldHideGameplayHud { get; private set; }

    private OverlayState state;
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle bodyStyle;
    private GUIStyle buttonStyle;
    private GUIStyle textFieldStyle;
    private GUIStyle rankingStatusStyle;
    private Font importantFont;
    private Font secondaryFont;
    private string rankingNameInput = "Player";
    private bool rankingSubmitted;
    private int rankingSubmittedScore;
    private float rankingSubmittedTime;
    private float defeatStartedAtUnscaled;

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

        RefreshCursorVisibility();

        if (gameManager != null && gameManager.IsGameOver && state != OverlayState.Defeat)
        {
            SetState(OverlayState.Defeat);
            return;
        }

        // Durante demora/cuenta regresiva/inicio, GameManager controla la escala de tiempo.
        // Ignora pausa y fuerza estado de juego para evitar desincronizacion.
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
            if (GetSubmitPressedThisFrame() && IsDefeatInputUnlocked())
            {
                TryRegisterCurrentScore();
            }

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

    private static bool GetSubmitPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#else
        return false;
#endif
    }

    private void SetState(OverlayState nextState)
    {
        OverlayState previousState = state;
        state = nextState;
        if (state == OverlayState.Defeat)
        {
            PrepareDefeatRegistration();
            defeatStartedAtUnscaled = Time.unscaledTime;
        }

        bool gameplayActive = state == OverlayState.Playing;

        bool runCanAdvance = gameManager == null || gameManager.IsRunActive;
        Time.timeScale = gameplayActive && runCanAdvance ? 1f : 0f;
        ShouldHideGameplayHud = !gameplayActive;
        RefreshCursorVisibility();

        if (previousState == OverlayState.Playing && state == OverlayState.Paused)
        {
            GlitchAudioManager.PlayPauseOpen();
        }
        else if (previousState == OverlayState.Paused && state == OverlayState.Playing)
        {
            GlitchAudioManager.PlayPauseClose();
        }
    }

    private void RefreshCursorVisibility()
    {
        bool gameplayActive = state == OverlayState.Playing;
        bool upgradeSelectionOpen = gameManager != null && gameManager.IsUpgradeSelectionOpen;
        Cursor.visible = !gameplayActive || upgradeSelectionOpen;
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

        textFieldStyle = new GUIStyle(GUI.skin.textField)
        {
            font = secondaryFont,
            fontSize = 16,
            alignment = TextAnchor.MiddleLeft
        };

        rankingStatusStyle = new GUIStyle(GUI.skin.label)
        {
            font = secondaryFont,
            fontSize = 13,
            fontStyle = FontStyle.Italic,
            alignment = TextAnchor.MiddleLeft
        };
        rankingStatusStyle.normal.textColor = new Color(0.94f, 0.86f, 0.98f, 0.95f);

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
            GlitchAudioManager.PlayMenuConfirm();
            RestartLevel();
        }

        GUILayout.Space(8f);
        if (GUILayout.Button("Menu Principal", buttonStyle, GUILayout.Height(38f)))
        {
            GlitchAudioManager.PlayMenuBack();
            ReturnToMainMenu();
        }

        GUILayout.EndArea();
    }

    private void DrawDefeatMenu()
    {
        float t = Time.unscaledTime;
        float defeatElapsed = Mathf.Max(0f, t - defeatStartedAtUnscaled);
        float introDuration = Mathf.Max(0.01f, defeatCinematicDuration);
        float introT = Mathf.Clamp01(defeatElapsed / introDuration);
        float introEase = EaseOutCubic(introT);

        float pulse = 0.5f + 0.5f * Mathf.Sin(t * defeatPulseSpeed);
        float glitch = Mathf.Clamp01(0.20f + pulse * 0.50f);
        float hudGlitch = glitch * Mathf.Clamp01(hudGlitchWeight);
        float textGlitch = glitch * Mathf.Clamp01(textGlitchWeight);
        DrawScreenFade(Mathf.Lerp(0.88f, 0.70f, introEase));
        DrawDefeatCinematicIntro(defeatElapsed, introT);
        DrawDefeatBackdrop(pulse, hudGlitch, t);

        Rect panel = CenterRect(500f, 470f);
        float panelScale = Mathf.Lerp(0.90f, 1f, introEase);
        if (introT < 1f)
        {
            float overshoot = Mathf.Sin(introT * Mathf.PI) * 0.03f;
            panelScale += overshoot;
        }
        panel = ScaleRectFromCenter(panel, panelScale);

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
        int score = gameManager != null ? gameManager.CurrentScore : 0;

        GUILayout.Label($"Tiempo sobrevivido: {time:F1}s", bodyStyle);
        GUILayout.Label($"Puntuacion final: {score}", bodyStyle);
        GUILayout.Label($"Nivel de amenaza final: x{threat:F2}", bodyStyle);
        GUILayout.Label($"Zona de contencion: {level}", bodyStyle);

        GUILayout.Space(14f);
        GUILayout.Label("Ingresa tu nombre para el ranking:", bodyStyle);
        bool canInteract = defeatElapsed >= Mathf.Max(0f, defeatInputUnlockDelay);
        bool prevEnabled = GUI.enabled;
        GUI.enabled = canInteract;
        rankingNameInput = GUILayout.TextField(
            rankingNameInput ?? string.Empty,
            Mathf.Max(1, rankingNameMaxLength),
            textFieldStyle,
            GUILayout.Height(30f));
        GUI.enabled = prevEnabled;

        GUILayout.Space(8f);
        bool canRegister = canInteract && !rankingSubmitted && !string.IsNullOrWhiteSpace(rankingNameInput);
        GUI.enabled = canRegister;
        if (GUILayout.Button("Registrar Puntaje", buttonStyle, GUILayout.Height(36f)))
        {
            TryRegisterCurrentScore();
        }
        GUI.enabled = prevEnabled;

        GUILayout.Space(4f);
        string rankingStatusText = rankingSubmitted
            ? $"Registrado: {rankingNameInput} | {rankingSubmittedScore} pts ({rankingSubmittedTime:F1}s)"
            : " ";
        GUILayout.Label(rankingStatusText, rankingStatusStyle, GUILayout.Height(22f));

        GUILayout.Space(10f);

        GUI.enabled = canInteract;
        if (GUILayout.Button("Reiniciar", buttonStyle, GUILayout.Height(40f)))
        {
            GlitchAudioManager.PlayMenuConfirm();
            RestartLevel();
        }

        GUILayout.Space(10f);
        if (GUILayout.Button("Menu Principal", buttonStyle, GUILayout.Height(40f)))
        {
            GlitchAudioManager.PlayMenuBack();
            ReturnToMainMenu();
        }
        GUI.enabled = prevEnabled;

        GUILayout.EndArea();

        if (!canInteract)
        {
            float remaining = Mathf.Max(0f, defeatInputUnlockDelay - defeatElapsed);
            DrawDefeatHoldPrompt(remaining);
        }
    }

    private void PrepareDefeatRegistration()
    {
        rankingSubmitted = false;
        rankingSubmittedScore = 0;
        rankingSubmittedTime = 0f;

        string lastName = RankingStorage.GetLastPlayerName();
        rankingNameInput = string.IsNullOrWhiteSpace(lastName) ? "Player" : lastName;
    }

    private void TryRegisterCurrentScore()
    {
        if (gameManager == null || rankingSubmitted)
        {
            return;
        }

        string candidate = string.IsNullOrWhiteSpace(rankingNameInput) ? "Player" : rankingNameInput.Trim();
        RankingStorage.AddEntry(candidate, gameManager.CurrentScore, gameManager.CurrentLevelTypeLabel, gameManager.SurvivalTime);
        rankingNameInput = RankingStorage.GetLastPlayerName();
        rankingSubmitted = true;
        rankingSubmittedScore = gameManager.CurrentScore;
        rankingSubmittedTime = gameManager.SurvivalTime;
        GlitchAudioManager.PlayRankingSubmit();
    }

    private bool IsDefeatInputUnlocked()
    {
        if (state != OverlayState.Defeat)
        {
            return true;
        }

        return (Time.unscaledTime - defeatStartedAtUnscaled) >= Mathf.Max(0f, defeatInputUnlockDelay);
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

    private void DrawDefeatCinematicIntro(float defeatElapsed, float introT)
    {
        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        float flashDuration = Mathf.Max(0.01f, defeatFlashDuration);
        if (defeatElapsed <= flashDuration)
        {
            float f = 1f - Mathf.Clamp01(defeatElapsed / flashDuration);
            float alpha = f * f;
            DrawSolidRect(
                new Rect(0f, 0f, Screen.width, Screen.height),
                new Color(defeatFlashColor.r, defeatFlashColor.g, defeatFlashColor.b, alpha * 0.75f));
        }

        float shockDuration = Mathf.Max(0.01f, defeatShockwaveDuration);
        float shockT = Mathf.Clamp01(defeatElapsed / shockDuration);
        float shockEase = EaseOutCubic(shockT);
        float ringCount = 4f;
        for (int i = 0; i < ringCount; i++)
        {
            float offset = i * 0.12f;
            float p = Mathf.Clamp01(shockEase - offset);
            if (p <= 0f)
            {
                continue;
            }

            float radiusX = Mathf.Lerp(24f, Screen.width * 0.62f, p);
            float radiusY = Mathf.Lerp(14f, Screen.height * 0.54f, p);
            float a = (1f - p) * (0.34f - i * 0.05f);
            Color c = new Color(defeatShockwaveColor.r, defeatShockwaveColor.g, defeatShockwaveColor.b, Mathf.Max(0f, a));
            DrawEllipseRing(center, radiusX, radiusY, c);
        }

        float barAlpha = (1f - introT) * 0.30f;
        if (barAlpha > 0.001f)
        {
            float step = Mathf.Max(18f, Screen.height / 22f);
            for (float y = 0f; y < Screen.height + step; y += step)
            {
                float drift = Mathf.Sin((y * 0.04f) + Time.unscaledTime * 13f) * 26f;
                float width = Screen.width * Mathf.Lerp(0.38f, 0.95f, Mathf.PerlinNoise(y * 0.013f, Time.unscaledTime * 1.9f));
                float x = (Screen.width - width) * 0.5f + drift;
                DrawSolidRect(new Rect(x, y, width, 2f), new Color(1f, 0.44f, 0.56f, barAlpha));
            }
        }

        float vignetteA = Mathf.Lerp(0.34f, 0.12f, introT);
        float edge = Mathf.Lerp(120f, 34f, introT);
        Color vignette = new Color(0f, 0f, 0f, vignetteA);
        DrawSolidRect(new Rect(0f, 0f, Screen.width, edge), vignette);
        DrawSolidRect(new Rect(0f, Screen.height - edge, Screen.width, edge), vignette);
        DrawSolidRect(new Rect(0f, edge, edge, Screen.height - edge * 2f), vignette);
        DrawSolidRect(new Rect(Screen.width - edge, edge, edge, Screen.height - edge * 2f), vignette);
    }

    private void DrawDefeatHoldPrompt(float remaining)
    {
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f);
        float alpha = Mathf.Lerp(0.18f, 0.42f, pulse);
        string msg = remaining > 0.06f ? "Restableciendo protocolo..." : " ";
        Rect r = new Rect((Screen.width - 520f) * 0.5f, Screen.height * 0.82f, 520f, 34f);
        Color old = GUI.color;
        GUI.color = new Color(1f, 0.70f, 0.78f, alpha);
        GUI.Label(r, msg, subtitleStyle);
        GUI.color = old;
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

        // Bloques suaves de interferencia: dan clima sin dificultar la lectura del menu.
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

        // Lineas finas tipo barrido, principalmente horizontales.
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

    private static float EaseOutCubic(float t)
    {
        t = Mathf.Clamp01(t);
        float inv = 1f - t;
        return 1f - (inv * inv * inv);
    }

    private static Rect ScaleRectFromCenter(Rect source, float scale)
    {
        float clamped = Mathf.Max(0.01f, scale);
        float w = source.width * clamped;
        float h = source.height * clamped;
        return new Rect(
            source.center.x - w * 0.5f,
            source.center.y - h * 0.5f,
            w,
            h);
    }
}

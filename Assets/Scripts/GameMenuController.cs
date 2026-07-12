using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Menú que aparece sobre la partida: pausa, derrota, ranking y retorno de escena sin duplicar la lógica de la run.
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

    public static bool ShouldHideGameplayHud { get; private set; }

    private OverlayState state;
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle bodyStyle;
    private GUIStyle buttonStyle;
    private GUIStyle rankingStatusStyle;
    private Font importantFont;
    private Font secondaryFont;
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
        bool introTutorialOpen = gameManager != null && gameManager.IsIntroTutorialOpen;
        bool contextTutorialNeedsClick = gameManager != null && gameManager.ShouldShowCursorForContextTutorial;
        Cursor.visible = !gameplayActive || upgradeSelectionOpen || introTutorialOpen || contextTutorialNeedsClick;
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
        float t = Time.unscaledTime;
        float pulse = 0.5f + 0.5f * Mathf.Sin(t * 2.6f);
        DrawScreenFade(0.66f);
        DrawPauseBackdrop(t, pulse);

        float mainW = 340f;
        float mainH = 292f;
        float audioW = 250f;
        float gap = 16f;
        float totalW = mainW + gap + audioW;
        float startX = (Screen.width - totalW) * 0.5f;
        float y = (Screen.height - mainH) * 0.5f;
        Rect panel = new Rect(startX, y, mainW, mainH);
        Rect audioPanel = new Rect(panel.xMax + gap, y + 18f, audioW, mainH - 36f);

        DrawPanel(panel, new Color(0.04f, 0.06f, 0.11f, 0.92f), new Color(0.58f, 0.72f, 0.96f, 0.62f));
        DrawPausePanelFx(panel, new Color(0.66f, 0.82f, 1f, 1f), pulse);

        DrawPanel(audioPanel, new Color(0.035f, 0.05f, 0.09f, 0.88f), new Color(0.86f, 0.58f, 1f, 0.55f));
        DrawPausePanelFx(audioPanel, new Color(0.96f, 0.58f, 1f, 1f), 1f - pulse);

        Rect titleRect = new Rect(panel.x + 18f, panel.y + 18f, panel.width - 36f, 48f);
        DrawGlitchLabel(titleRect, "Pausa", titleStyle, 0.16f + pulse * 0.10f);
        DrawSolidRect(new Rect(panel.x + 34f, panel.y + 70f, panel.width - 68f, 1f), new Color(0.82f, 0.92f, 1f, 0.20f));

        if (DrawPauseButton(new Rect(panel.x + 18f, panel.y + 92f, panel.width - 36f, 42f), "Continuar", true))
        {
            SetState(OverlayState.Playing);
        }

        if (DrawPauseButton(new Rect(panel.x + 18f, panel.y + 148f, panel.width - 36f, 42f), "Reiniciar", false))
        {
            GlitchAudioManager.PlayMenuConfirm();
            RestartLevel();
        }

        if (DrawPauseButton(new Rect(panel.x + 18f, panel.y + 204f, panel.width - 36f, 42f), "Menu Principal", false))
        {
            GlitchAudioManager.PlayMenuBack();
            ReturnToMainMenu();
        }

        DrawPauseAudioPanel(audioPanel);
    }

    private void DrawPauseAudioPanel(Rect panel)
    {
        Rect title = new Rect(panel.x + 16f, panel.y + 14f, panel.width - 32f, 28f);
        GUI.Label(title, "Audio", GetFittedSingleLineStyle(titleStyle, "Audio", title.width, 26, 18));
        DrawSolidRect(new Rect(panel.x + 20f, panel.y + 50f, panel.width - 40f, 1f), new Color(0.96f, 0.68f, 1f, 0.22f));

        float music = DrawPauseVolumeSlider(panel, 72f, "Musica", UserSettings.GetMusicVolume(), GlitchUiPalette.Information);
        if (!Mathf.Approximately(music, UserSettings.GetMusicVolume()))
        {
            UserSettings.SetMusicVolume(music);
        }

        float sfx = DrawPauseVolumeSlider(panel, 146f, "SFX", UserSettings.GetSfxVolume(), GlitchUiPalette.Information);
        if (!Mathf.Approximately(sfx, UserSettings.GetSfxVolume()))
        {
            UserSettings.SetSfxVolume(sfx);
        }

        Rect hint = new Rect(panel.x + 18f, panel.yMax - 42f, panel.width - 36f, 30f);
        GUI.Label(hint, "Ajustes aplicados en tiempo real.", rankingStatusStyle);
    }

    private float DrawPauseVolumeSlider(Rect panel, float yOffset, string label, float value, Color accent)
    {
        Rect labelRect = new Rect(panel.x + 18f, panel.y + yOffset, panel.width - 36f, 22f);
        Rect barRect = new Rect(labelRect.x, labelRect.yMax + 8f, labelRect.width, 10f);
        Rect sliderRect = new Rect(barRect.x, barRect.y - 6f, barRect.width, 24f);
        string valueText = $"{Mathf.RoundToInt(value * 100f)}%";
        GUIStyle valueStyle = new GUIStyle(bodyStyle)
        {
            alignment = TextAnchor.MiddleRight,
            fontStyle = FontStyle.Bold
        };
        valueStyle.normal.textColor = new Color(1f, 0.84f, 0.56f, 0.98f);

        GUI.Label(labelRect, label, bodyStyle);
        GUI.Label(new Rect(labelRect.xMax - 62f, labelRect.y, 62f, labelRect.height), valueText, valueStyle);
        DrawSolidRect(barRect, new Color(0.04f, 0.06f, 0.10f, 0.92f));
        DrawSolidRect(new Rect(barRect.x, barRect.y, barRect.width * Mathf.Clamp01(value), barRect.height), new Color(accent.r, accent.g, accent.b, 0.72f));
        DrawSolidRect(new Rect(barRect.x, barRect.y, barRect.width, 1f), new Color(1f, 1f, 1f, 0.18f));
        return GUI.HorizontalSlider(sliderRect, value, 0f, 1f);
    }

    private bool DrawPauseButton(Rect rect, string label, bool primary)
    {
        bool hovered = rect.Contains(Event.current.mousePosition);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (primary ? 4.0f : 3.0f));
        Color fill = primary
            ? new Color(0.10f, 0.18f, 0.28f, 0.94f)
            : new Color(0.07f, 0.10f, 0.16f, 0.92f);
        if (hovered)
        {
            fill = Color.Lerp(fill, new Color(0.18f, 0.28f, 0.42f, 0.96f), 0.72f);
        }

        Color border = hovered
            ? new Color(0.90f, 0.96f, 1f, 0.78f)
            : new Color(0.54f, 0.70f, 0.96f, 0.42f + pulse * 0.16f);
        DrawSolidRect(rect, fill);
        DrawSolidRect(new Rect(rect.x, rect.y, rect.width, 2f), border);
        DrawSolidRect(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), new Color(border.r, border.g, border.b, border.a * 0.58f));
        DrawSolidRect(new Rect(rect.x + 7f, rect.y + 7f, 3f, rect.height - 14f), new Color(0.86f, 0.96f, 1f, hovered ? 0.46f : 0.22f));

        if (hovered)
        {
            float scanX = rect.x + 16f + Mathf.Repeat(Time.unscaledTime * 82f, Mathf.Max(1f, rect.width - 48f));
            DrawSolidRect(new Rect(scanX, rect.y + rect.height * 0.5f - 1f, 24f, 2f), new Color(1f, 1f, 1f, 0.24f));
        }

        GUI.Label(rect, label, buttonStyle);
        bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
        if (clicked && primary)
        {
            GlitchAudioManager.PlayMenuConfirm();
        }

        return clicked;
    }

    private void DrawPauseBackdrop(float time, float pulse)
    {
        DrawHudGlitchOverlay(0.20f + pulse * 0.10f, time);
        float stripeAlpha = 0.06f + pulse * 0.035f;
        for (float y = 0f; y < Screen.height + 80f; y += 80f)
        {
            float drift = Mathf.Sin(time * 1.4f + y * 0.02f) * 34f;
            DrawSolidRect(new Rect(drift - 40f, y, Screen.width + 80f, 2f), new Color(0.62f, 0.82f, 1f, stripeAlpha));
        }
    }

    private void DrawPausePanelFx(Rect rect, Color accent, float pulse)
    {
        DrawSolidRect(new Rect(rect.x + 8f, rect.y + 8f, 24f, 2f), new Color(accent.r, accent.g, accent.b, 0.42f));
        DrawSolidRect(new Rect(rect.x + 8f, rect.y + 8f, 2f, 24f), new Color(accent.r, accent.g, accent.b, 0.42f));
        DrawSolidRect(new Rect(rect.xMax - 32f, rect.y + 8f, 24f, 2f), new Color(accent.r, accent.g, accent.b, 0.42f));
        DrawSolidRect(new Rect(rect.xMax - 10f, rect.y + 8f, 2f, 24f), new Color(accent.r, accent.g, accent.b, 0.42f));
        DrawSolidRect(new Rect(rect.x + 2f, rect.y + Mathf.Repeat(Time.unscaledTime * 36f, Mathf.Max(1f, rect.height)), rect.width - 4f, 1f), new Color(accent.r, accent.g, accent.b, 0.06f + pulse * 0.06f));
    }

    private void DrawDefeatMenu()
    {
        if (gameManager != null && gameManager.IsLocalVersus)
        {
            DrawVersusResultMenu();
            return;
        }

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

        Rect panel = CenterRect(Mathf.Min(700f, Screen.width - 48f), Mathf.Min(510f, Screen.height - 48f));
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

        Rect area = new Rect(panel.x + 28f, panel.y + 18f, panel.width - 56f, panel.height - 36f);
        GUIStyle fittedTitleStyle = GetFittedSingleLineStyle(titleStyle, defeatTitle, area.width, 32, 22);
        Rect titleRect = new Rect(area.x, area.y, area.width, 46f);
        DrawGlitchLabel(titleRect, defeatTitle, fittedTitleStyle, textGlitch * 0.30f);
        DrawSolidRect(new Rect(area.x + 24f, area.y + 52f, area.width - 48f, 1f), new Color(1f, 0.52f, 0.62f, 0.26f));

        float time = gameManager != null ? gameManager.SurvivalTime : 0f;
        int score = gameManager != null ? gameManager.CurrentScore : 0;
        MetaProgressionStorage.RunReward reward = gameManager != null && gameManager.HasAwardedMetaReward
            ? gameManager.LastMetaReward
            : MetaProgressionStorage.LastRunReward;
        string grade = string.IsNullOrWhiteSpace(reward.performanceGrade) ? "D" : reward.performanceGrade;
        DrawPerformanceGrade(new Rect(area.x, area.y + 66f, area.width, 190f), grade, defeatElapsed, pulse);

        float statY = area.y + 270f;
        float statGap = 10f;
        float statWidth = (area.width - statGap * 2f) / 3f;
        DrawResultMetric(new Rect(area.x, statY, statWidth, 64f), "PUNTOS", score.ToString(), GlitchUiPalette.Information);
        DrawResultMetric(new Rect(area.x + statWidth + statGap, statY, statWidth, 64f), "TIEMPO", $"{time:F1}s", GlitchUiPalette.Neutral);
        DrawResultMetric(new Rect(area.x + (statWidth + statGap) * 2f, statY, statWidth, 64f), "DATOS", $"+{reward.dataEarned}", GlitchUiPalette.Success);

        bool canInteract = defeatElapsed >= Mathf.Max(0f, defeatInputUnlockDelay);
        bool prevEnabled = GUI.enabled;
        GUI.enabled = canInteract;
        float actionsY = area.yMax - 48f;
        float actionWidth = (area.width - 12f) * 0.5f;
        if (GUI.Button(new Rect(area.x, actionsY, actionWidth, 42f), "REINTENTAR", buttonStyle))
        {
            GlitchAudioManager.PlayMenuConfirm();
            RestartLevel();
        }
        if (GUI.Button(new Rect(area.x + actionWidth + 12f, actionsY, actionWidth, 42f), "MENÚ", buttonStyle))
        {
            GlitchAudioManager.PlayMenuBack();
            ReturnToMainMenu();
        }
        GUI.enabled = prevEnabled;

        if (!canInteract)
        {
            float remaining = Mathf.Max(0f, defeatInputUnlockDelay - defeatElapsed);
            DrawDefeatHoldPrompt(remaining);
        }
    }

    private void DrawPerformanceGrade(Rect rect, string grade, float elapsed, float pulse)
    {
        float reveal = EaseOutCubic(Mathf.Clamp01((elapsed - 0.28f) / 0.62f));
        Color color = GetGradeColor(grade);
        DrawGradeCelebrationFx(rect, color, reveal, pulse, elapsed);
        float size = Mathf.Lerp(0.35f, 1f, reveal) + Mathf.Sin(reveal * Mathf.PI) * 0.08f;
        Rect gradeRect = ScaleRectFromCenter(rect, size);
        GUIStyle gradeStyle = new GUIStyle(titleStyle)
        {
            fontSize = Mathf.RoundToInt(Mathf.Lerp(52f, 132f, reveal)),
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Overflow
        };
        gradeStyle.normal.textColor = Color.Lerp(Color.white, color, 0.72f + pulse * 0.16f);

        float split = (1f - reveal) * 13f + pulse * 0.8f;
        Color old = GUI.color;
        GUI.color = new Color(color.r, color.g, color.b, 0.16f * reveal);
        GUI.Label(new Rect(gradeRect.x - split, gradeRect.y, gradeRect.width, gradeRect.height), grade, gradeStyle);
        GUI.Label(new Rect(gradeRect.x + split, gradeRect.y, gradeRect.width, gradeRect.height), grade, gradeStyle);
        GUI.color = old;
        DrawGlitchLabel(gradeRect, grade, gradeStyle, (1f - reveal) * 0.85f + pulse * 0.05f);
    }

    private void DrawGradeCelebrationFx(Rect rect, Color color, float reveal, float pulse, float elapsed)
    {
        if (reveal <= 0.001f)
        {
            return;
        }

        Vector2 center = rect.center;
        float impact = 1f - Mathf.Clamp01((elapsed - 0.28f) / 0.52f);
        float glow = (0.06f + pulse * 0.035f) * reveal;
        for (int i = 3; i >= 1; i--)
        {
            float scale = i / 3f;
            Rect halo = new Rect(
                center.x - rect.width * 0.30f * scale,
                center.y - rect.height * 0.42f * scale,
                rect.width * 0.60f * scale,
                rect.height * 0.84f * scale);
            DrawSolidRect(halo, new Color(color.r, color.g, color.b, glow * (0.32f / i)));
        }

        float ringExpansion = Mathf.Lerp(0.20f, 1f, reveal);
        DrawEllipseRing(center, rect.width * 0.19f * ringExpansion, rect.height * 0.42f * ringExpansion,
            new Color(color.r, color.g, color.b, (0.22f + impact * 0.42f) * reveal));
        DrawEllipseRing(center, rect.width * 0.31f * ringExpansion, rect.height * 0.57f * ringExpansion,
            new Color(color.r, color.g, color.b, (0.10f + impact * 0.24f) * reveal));

        float railWidth = Mathf.Lerp(0f, rect.width * 0.30f, reveal);
        DrawSolidRect(new Rect(center.x - railWidth - 42f, center.y - 2f, railWidth, 3f),
            new Color(color.r, color.g, color.b, 0.48f * reveal));
        DrawSolidRect(new Rect(center.x + 42f, center.y - 2f, railWidth, 3f),
            new Color(color.r, color.g, color.b, 0.48f * reveal));

        float scanY = rect.y + Mathf.Repeat(elapsed * 96f, Mathf.Max(1f, rect.height));
        DrawSolidRect(new Rect(rect.x + rect.width * 0.20f, scanY, rect.width * 0.60f, 2f),
            new Color(1f, 1f, 1f, 0.12f * reveal));

        for (int i = 0; i < 18; i++)
        {
            float angle = i / 18f * Mathf.PI * 2f + elapsed * (i % 2 == 0 ? 0.30f : -0.22f);
            float radiusX = Mathf.Lerp(28f, rect.width * 0.34f, reveal) * (0.78f + (i % 3) * 0.10f);
            float radiusY = Mathf.Lerp(18f, rect.height * 0.52f, reveal) * (0.78f + (i % 4) * 0.06f);
            float x = center.x + Mathf.Cos(angle) * radiusX;
            float y = center.y + Mathf.Sin(angle) * radiusY;
            float spark = i % 3 == 0 ? 5f : 3f;
            DrawSolidRect(new Rect(x - spark * 0.5f, y - spark * 0.5f, spark, spark),
                new Color(color.r, color.g, color.b, (0.24f + pulse * 0.30f) * reveal));
        }
    }

    private void DrawResultMetric(Rect rect, string label, string value, Color accent)
    {
        DrawSolidRect(rect, new Color(0.025f, 0.04f, 0.075f, 0.82f));
        DrawSolidRect(new Rect(rect.x, rect.yMax - 3f, rect.width, 3f), new Color(accent.r, accent.g, accent.b, 0.76f));
        GUIStyle labelStyle = new GUIStyle(rankingStatusStyle) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Normal };
        GUIStyle valueStyle = new GUIStyle(titleStyle) { alignment = TextAnchor.MiddleCenter, fontSize = 24 };
        valueStyle.normal.textColor = accent;
        GUI.Label(new Rect(rect.x + 6f, rect.y + 3f, rect.width - 12f, 18f), label, labelStyle);
        GUI.Label(new Rect(rect.x + 6f, rect.y + 20f, rect.width - 12f, 37f), value, valueStyle);
    }

    private static Color GetGradeColor(string grade)
    {
        switch (grade)
        {
            case "S": return Color.Lerp(GlitchUiPalette.Success, Color.white, 0.34f);
            case "A": return GlitchUiPalette.Success;
            case "B": return GlitchUiPalette.Information;
            case "C": return GlitchUiPalette.Special;
            default: return GlitchUiPalette.Danger;
        }
    }

    private void DrawVersusResultMenu()
    {
        float elapsed = Mathf.Max(0f, Time.unscaledTime - defeatStartedAtUnscaled);
        float intro = Mathf.Clamp01(elapsed / 1.05f);
        float ease = EaseOutCubic(intro);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4.6f);
        bool anomalyWon = gameManager != null &&
                          string.Equals(gameManager.VersusWinnerLabel, "ANOMALIA", System.StringComparison.OrdinalIgnoreCase);
        Color runnerBlue = new Color(0.25f, 0.90f, 1f, 1f);
        Color anomalyRed = new Color(1f, 0.28f, 0.48f, 1f);
        Color winnerColor = anomalyWon ? anomalyRed : runnerBlue;

        DrawVersusResultBackdrop(runnerBlue, anomalyRed, winnerColor, pulse, elapsed, intro);
        DrawVersusResultImpact(winnerColor, elapsed);

        float panelWidth = Mathf.Min(690f, Screen.width - 44f);
        float panelHeight = Mathf.Min(470f, Screen.height - 44f);
        Rect panel = CenterRect(panelWidth, panelHeight);
        panel.y += Mathf.Lerp(46f, 0f, ease);
        panel = ScaleRectFromCenter(panel, Mathf.Lerp(0.90f, 1f, ease) + Mathf.Sin(intro * Mathf.PI) * 0.018f);

        float jitter = (1f - intro) * 7f;
        panel.x += (Mathf.PerlinNoise(Time.unscaledTime * 18f, 0.31f) - 0.5f) * jitter;
        DrawPanel(panel, new Color(0.012f, 0.022f, 0.046f, 0.96f),
            new Color(winnerColor.r, winnerColor.g, winnerColor.b, 0.82f));
        DrawVersusResultPanelFx(panel, runnerBlue, anomalyRed, winnerColor, pulse);

        Rect content = new Rect(panel.x + 28f, panel.y + 18f, panel.width - 56f, panel.height - 36f);
        string winner = string.IsNullOrWhiteSpace(gameManager.VersusWinnerLabel)
            ? "DUELO FINALIZADO"
            : $"GANA {gameManager.VersusWinnerLabel}";
        GUIStyle winnerStyle = GetFittedSingleLineStyle(titleStyle, winner, content.width, 38, 23);
        Color previousTitle = winnerStyle.normal.textColor;
        winnerStyle.normal.textColor = Color.Lerp(Color.white, winnerColor, 0.38f);
        DrawGlitchLabel(new Rect(content.x, content.y + 13f, content.width, 50f), winner, winnerStyle, (1f - intro) * 0.7f + pulse * 0.08f);
        winnerStyle.normal.textColor = previousTitle;

        GUIStyle resultLabelStyle = new GUIStyle(rankingStatusStyle)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 12,
            wordWrap = true
        };
        resultLabelStyle.normal.textColor = new Color(0.86f, 0.91f, 1f, 0.90f);
        GUI.Label(new Rect(content.x + 40f, content.y + 68f, content.width - 80f, 38f),
            gameManager.VersusResultSubtitle, resultLabelStyle);

        float statsAlpha = Mathf.Clamp01((elapsed - 0.42f) / 0.35f);
        Color oldGui = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, statsAlpha);
        Rect arenaStat = new Rect(content.x + 18f, content.y + 123f, (content.width - 48f) * 0.5f, 78f);
        Rect timeStat = new Rect(arenaStat.xMax + 12f, arenaStat.y, arenaStat.width, arenaStat.height);
        DrawVersusResultStat(arenaStat, "J1 | ZONA DE CONTENCION", gameManager.CurrentLevelTypeLabel, runnerBlue);
        DrawVersusResultStat(timeStat, "J2 | DURACION DEL DUELO", $"{gameManager.SurvivalTime:F1}s", anomalyRed);
        GUI.color = oldGui;

        float dividerY = content.y + 220f;
        DrawSolidRect(new Rect(content.x + 18f, dividerY, content.width - 36f, 1f),
            new Color(winnerColor.r, winnerColor.g, winnerColor.b, 0.28f));
        GUI.Label(new Rect(content.x, dividerY + 10f, content.width, 24f),
            anomalyWon ? "CAPTURA COMPLETADA" : "PROTOCOLO RESISTIDO", subtitleStyle);

        bool canInteract = elapsed >= 1.15f;
        float actionsAlpha = Mathf.Clamp01((elapsed - 0.90f) / 0.35f);
        GUI.color = new Color(1f, 1f, 1f, actionsAlpha);
        bool previousEnabled = GUI.enabled;
        GUI.enabled = canInteract;
        Rect rematchRect = new Rect(content.x + 18f, content.yMax - 104f, content.width - 36f, 42f);
        Rect menuRect = new Rect(content.x + 18f, content.yMax - 52f, content.width - 36f, 42f);
        if (GUI.Button(rematchRect, "Revancha", buttonStyle))
        {
            GlitchAudioManager.PlayMenuConfirm();
            RestartLevel();
        }
        if (GUI.Button(menuRect, "Menu Principal", buttonStyle))
        {
            GlitchAudioManager.PlayMenuBack();
            ReturnToMainMenu();
        }
        GUI.enabled = previousEnabled;
        GUI.color = oldGui;
    }

    private void DrawVersusResultBackdrop(
        Color runnerBlue,
        Color anomalyRed,
        Color winner,
        float pulse,
        float elapsed,
        float intro)
    {
        DrawScreenFade(Mathf.Lerp(0.94f, 0.78f, intro));
        float split = Mathf.Lerp(0f, Screen.width * 0.54f, EaseOutCubic(Mathf.Clamp01(elapsed / 0.62f)));
        DrawSolidRect(new Rect(0f, 0f, split, Screen.height),
            new Color(runnerBlue.r, runnerBlue.g, runnerBlue.b, 0.055f + pulse * 0.025f));
        DrawSolidRect(new Rect(Screen.width - split, 0f, split, Screen.height),
            new Color(anomalyRed.r, anomalyRed.g, anomalyRed.b, 0.055f + pulse * 0.025f));
        float winnerWidth = Screen.width * 0.5f;
        float winnerX = winner.r > winner.b ? Screen.width * 0.5f : 0f;
        DrawSolidRect(new Rect(winnerX, 0f, winnerWidth, Screen.height),
            new Color(winner.r, winner.g, winner.b, 0.025f + pulse * 0.018f));

        for (int i = 0; i < 14; i++)
        {
            float direction = i % 2 == 0 ? 1f : -1f;
            float width = Mathf.Lerp(80f, 360f, Mathf.PerlinNoise(i * 0.31f, Time.unscaledTime * 0.75f));
            float x = direction > 0f
                ? Mathf.Repeat(Time.unscaledTime * (115f + i * 5f) + i * 127f, Screen.width + width) - width
                : Screen.width - Mathf.Repeat(Time.unscaledTime * (105f + i * 6f) + i * 91f, Screen.width + width);
            float y = (i + 0.5f) * Screen.height / 14f;
            Color color = direction > 0f ? runnerBlue : anomalyRed;
            DrawSolidRect(new Rect(x, y, width, i % 4 == 0 ? 3f : 1.5f),
                new Color(color.r, color.g, color.b, 0.08f + pulse * 0.035f));
        }
    }

    private void DrawVersusResultImpact(Color color, float elapsed)
    {
        if (elapsed < 0.16f)
        {
            float alpha = 1f - elapsed / 0.16f;
            DrawSolidRect(new Rect(0f, 0f, Screen.width, Screen.height),
                new Color(color.r, color.g, color.b, alpha * 0.72f));
        }

        float progress = Mathf.Clamp01(elapsed / 0.95f);
        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        for (int i = 0; i < 4; i++)
        {
            float p = Mathf.Clamp01(progress - i * 0.10f);
            if (p <= 0f)
            {
                continue;
            }
            DrawEllipseRing(center, Mathf.Lerp(24f, Screen.width * 0.56f, EaseOutCubic(p)),
                Mathf.Lerp(12f, Screen.height * 0.48f, EaseOutCubic(p)),
                new Color(color.r, color.g, color.b, (1f - p) * (0.30f - i * 0.045f)));
        }
    }

    private void DrawVersusResultPanelFx(Rect panel, Color runnerBlue, Color anomalyRed, Color winner, float pulse)
    {
        DrawSolidRect(new Rect(panel.x, panel.y, panel.width * 0.5f, 4f),
            new Color(runnerBlue.r, runnerBlue.g, runnerBlue.b, 0.78f));
        DrawSolidRect(new Rect(panel.center.x, panel.y, panel.width * 0.5f, 4f),
            new Color(anomalyRed.r, anomalyRed.g, anomalyRed.b, 0.78f));
        float scanY = panel.y + Mathf.Repeat(Time.unscaledTime * 62f, panel.height);
        DrawSolidRect(new Rect(panel.x + 3f, scanY, panel.width - 6f, 2f),
            new Color(winner.r, winner.g, winner.b, 0.055f + pulse * 0.025f));
        DrawPausePanelFx(panel, winner, pulse);
    }

    private void DrawVersusResultStat(Rect rect, string label, string value, Color accent)
    {
        DrawSolidRect(rect, new Color(accent.r, accent.g, accent.b, 0.10f));
        DrawSolidRect(new Rect(rect.x, rect.y, 4f, rect.height),
            new Color(accent.r, accent.g, accent.b, 0.78f));
        GUIStyle labelStyle = GetFittedSingleLineStyle(rankingStatusStyle, label, rect.width - 24f, 11, 8);
        labelStyle.alignment = TextAnchor.MiddleCenter;
        GUIStyle valueStyle = GetFittedSingleLineStyle(titleStyle, value, rect.width - 24f, 22, 13);
        valueStyle.alignment = TextAnchor.MiddleCenter;
        GUI.Label(new Rect(rect.x + 12f, rect.y + 6f, rect.width - 24f, 20f), label, labelStyle);
        GUI.Label(new Rect(rect.x + 12f, rect.y + 28f, rect.width - 24f, 40f), value, valueStyle);
    }

    private void PrepareDefeatRegistration()
    {
        if (gameManager == null || gameManager.IsLocalVersus)
        {
            return;
        }

        string candidate = RankingStorage.GetLastPlayerName();
        RankingStorage.AddEntry(candidate, gameManager.CurrentScore, gameManager.CurrentLevelTypeLabel, gameManager.SurvivalTime);
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

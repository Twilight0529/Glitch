using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class MainMenuController : MonoBehaviour
{
    // Menu principal: muestra opciones, ranking, fondo animado y transicion hacia el juego.
    private enum MenuTransitionState
    {
        IntroHold,
        IntroFadeOut,
        Idle,
        ExitFadeIn
    }

    private const float BandOverlayAlpha = 0.26f;

    [Header("Main Menu")]
    [SerializeField] private string gameTitle = "GLITCH";
    [SerializeField] private string gameSubtitle = "Containment Breach";
    [SerializeField] private string gameplaySceneName = "Game";

    [Header("Options (Generic)")]
    [SerializeField] private float masterVolume = 0.8f;
    [SerializeField] private float uiScale = 1f;
    [SerializeField] private float hudScale = 1.1f;
    [SerializeField] private bool fullscreen;
    [SerializeField] private bool vSyncEnabled = true;

    [Header("Scene Fade")]
    [SerializeField] private float introBlackHoldDuration = 0.20f;
    [SerializeField] private float introFadeOutDuration = 1.30f;
    [SerializeField] private float exitFadeInDuration = 1.05f;
    [SerializeField] private Color transitionBlack = Color.black;
    [Header("Menu Visuals")]
    [SerializeField] private float menuMotionIntensity = 1f;

    private bool showOptions;
    private bool showUnlocks;
    private bool showStats;
    private bool showDeveloperMenu;
    private bool queuedGameplayLoad;
    private int selectedUnlockIndex;
    private int selectedAchievementIndex;
    private string selectedUnlockSection = MetaProgressionStorage.SectionRunUpgrades;
    private Font titleFont;
    private Font uiFont;
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle bandLabelStyle;
    private GUIStyle buttonStyle;
    private GUIStyle panelTitleStyle;
    private GUIStyle paragraphStyle;
    private GUIStyle rankingTitleStyle;
    private GUIStyle rankingRowStyle;
    private GUIStyle rankingScoreStyle;
    private GUIStyle menuButtonLabelStyle;
    private int cachedScreenWidth;
    private int cachedScreenHeight;
    private float cachedUiScale = -1f;
    private float blackOverlayAlpha = 1f;
    private float transitionTimer;
    private MenuTransitionState transitionState;
    private bool introReady;
    private readonly Dictionary<string, float> buttonHoverStates = new Dictionary<string, float>();
    private bool clearRankingConfirmArmed;
    private float clearRankingConfirmExpireAt;
    private string rankingActionMessage = string.Empty;
    private float rankingActionMessageExpireAt;
    private string unlockActionMessage = string.Empty;
    private float unlockActionMessageExpireAt;
    private string optionsActionMessage = string.Empty;
    private float optionsActionMessageExpireAt;
    private readonly Dictionary<string, bool> buttonHoverFlags = new Dictionary<string, bool>();

    private void Awake()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
        LoadSettings();
        GlitchAudioManager.Ensure();
        GlitchAudioManager.EnterMainMenu();
        ResolveFonts();
        NormalizeTransitionDurations();
        BeginIntroImmediateBlack();
    }

    private void Start()
    {
        // Espera un frame para que la escena termine de inicializarse antes de revelarla.
        introReady = false;
    }

    private void Update()
    {
        if (!introReady)
        {
            introReady = true;
            transitionTimer = 0f;
            transitionState = MenuTransitionState.IntroHold;
        }

        UpdateTransition();
        if (WasDeveloperTogglePressed())
        {
            showDeveloperMenu = !showDeveloperMenu;
            showOptions = false;
            showUnlocks = false;
            showStats = false;
            GlitchAudioManager.PlayMenuToggle();
        }
    }

    private void OnGUI()
    {
        EnsureStyles();
        DrawThematicBackground();

        if (showOptions)
        {
            DrawOptionsMenu();
        }
        else if (showStats)
        {
            DrawStatsMenu();
        }
        else if (showDeveloperMenu)
        {
            DrawDeveloperMenu();
        }
        else if (showUnlocks)
        {
            DrawUnlocksMenu();
        }
        else
        {
            DrawMainMenu();
        }

        DrawTransitionBlackOverlay();
    }

    private static Rect CenterRect(float width, float height)
    {
        return new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
    }

    private static bool WasDeveloperTogglePressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            bool ctrl = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
            bool shift = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            return ctrl && shift && keyboard.dKey.wasPressedThisFrame;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) &&
               (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) &&
               Input.GetKeyDown(KeyCode.D);
#else
        return false;
#endif
    }

    private void DrawMainMenu()
    {
        DrawScreenFade(0.38f);
        float bob = Mathf.Sin(Time.unscaledTime * 1.35f) * 6f;
        GetMainMenuLayout(out Rect panel, out Rect rankingRect, out bool sideBySide);
        panel.y += bob;
        rankingRect.y += bob * 0.55f;
        DrawSignalBeams(Time.unscaledTime);
        DrawMenuAtmosphere(panel, rankingRect, sideBySide);
        DrawPanelOrbiters(panel, rankingRect);

        DrawPanel(panel, new Color(0.03f, 0.05f, 0.09f, 0.90f), new Color(0.47f, 0.56f, 0.72f, 0.58f));
        DrawPanelFx(panel, new Color(0.62f, 0.76f, 1f, 1f), 0.14f);

        Rect titleRect = new Rect(panel.x + 18f, panel.y + 14f, panel.width - 36f, 62f);
        Rect subtitleRect = new Rect(panel.x + 18f, panel.y + 76f, panel.width - 36f, 26f);
        DrawGlitchTitle(titleRect, gameTitle, titleStyle, 1f);
        DrawGlitchTitle(subtitleRect, gameSubtitle, subtitleStyle, 0.55f);
        DrawSolidRect(new Rect(panel.x + 36f, panel.y + 110f, panel.width - 72f, 1f), new Color(0.80f, 0.90f, 1f, 0.18f));
        float markerX = panel.x + 42f + Mathf.Repeat(Time.unscaledTime * 90f, Mathf.Max(1f, panel.width - 88f));
        DrawSolidRect(new Rect(markerX, panel.y + 109f, 26f, 3f), new Color(0.90f, 0.97f, 1f, 0.35f));

        GUI.Label(new Rect(panel.x + 18f, panel.y + 114f, panel.width - 36f, 22f), $"Datos Recuperados: {MetaProgressionStorage.CurrentData}", paragraphStyle);
        float buttonY = panel.y + 144f;
        Rect playRect = new Rect(panel.x + 18f, buttonY, panel.width - 36f, 44f);
        Rect unlocksRect = new Rect(panel.x + 18f, buttonY + 54f, panel.width - 36f, 38f);
        Rect statsRect = new Rect(panel.x + 18f, buttonY + 102f, panel.width - 36f, 38f);
        Rect optionsRect = new Rect(panel.x + 18f, buttonY + 150f, panel.width - 36f, 38f);
        Rect exitRect = new Rect(panel.x + 18f, buttonY + 198f, panel.width - 36f, 38f);

        if (DrawAnimatedMenuButton(playRect, "Jugar", true))
        {
            StartGameplay();
        }
        if (DrawAnimatedMenuButton(unlocksRect, "Desbloqueos"))
        {
            showOptions = false;
            showStats = false;
            showUnlocks = true;
        }
        if (DrawAnimatedMenuButton(statsRect, "Estadisticas"))
        {
            showOptions = false;
            showUnlocks = false;
            showStats = true;
        }
        if (DrawAnimatedMenuButton(optionsRect, "Opciones"))
        {
            showUnlocks = false;
            showStats = false;
            showOptions = true;
        }
        if (DrawAnimatedMenuButton(exitRect, "Salir"))
        {
            ExitGame();
        }

        DrawRankingPanel(rankingRect, sideBySide);
    }

    private void DrawRankingPanel(Rect panel, bool sideBySide)
    {
        float now = Time.unscaledTime;
        if (clearRankingConfirmArmed && now >= clearRankingConfirmExpireAt)
        {
            clearRankingConfirmArmed = false;
        }
        if (!string.IsNullOrEmpty(rankingActionMessage) && now >= rankingActionMessageExpireAt)
        {
            rankingActionMessage = string.Empty;
        }

        DrawPanel(panel, new Color(0.03f, 0.05f, 0.09f, 0.90f), new Color(0.47f, 0.56f, 0.72f, 0.50f));
        DrawPanelFx(panel, new Color(0.64f, 0.74f, 1f, 1f), 0.09f);
        Rect area = new Rect(panel.x + 14f, panel.y + 10f, panel.width - 28f, panel.height - 16f);
        GUILayout.BeginArea(area);
        GUILayout.Label("Ranking Global", rankingTitleStyle);
        GUILayout.Space(8f);

        IReadOnlyList<RankingEntry> entries = RankingStorage.GetTopEntries();
        bool hasEntries = entries != null && entries.Count > 0;
        if (!hasEntries)
        {
            GUILayout.Label("Sin registros aun. Juega una ronda y deja tu marca.", paragraphStyle);
        }
        else
        {
            int rows = Mathf.Min(sideBySide ? 8 : 6, entries.Count);
            for (int i = 0; i < rows; i++)
            {
                RankingEntry entry = entries[i];
                Rect row = GUILayoutUtility.GetRect(area.width, 24f);
                if (i % 2 == 0)
                {
                    DrawSolidRect(new Rect(row.x, row.y + 2f, row.width, row.height - 4f), new Color(1f, 1f, 1f, 0.03f));
                }

                if (i == 0)
                {
                    DrawSolidRect(new Rect(row.x, row.y + 3f, row.width, row.height - 6f), new Color(1f, 0.84f, 0.45f, 0.08f));
                }

                GUI.Label(
                    new Rect(row.x + 6f, row.y, row.width * 0.66f, row.height),
                    $"{i + 1}. {entry.playerName}",
                    rankingRowStyle);
                GUI.Label(
                    new Rect(row.x + row.width * 0.68f, row.y, row.width * 0.30f, row.height),
                    entry.score.ToString(),
                    rankingScoreStyle);

                if (i == 0)
                {
                    float sparkle = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3.2f));
                    DrawSolidRect(new Rect(row.x + row.width - 14f, row.y + 9f, 4f, 4f), new Color(1f, 0.88f, 0.55f, 0.32f * sparkle));
                }
            }
        }

        GUILayout.FlexibleSpace();
        if (!string.IsNullOrEmpty(rankingActionMessage))
        {
            GUILayout.Label(rankingActionMessage, paragraphStyle);
            GUILayout.Space(4f);
        }

        string clearLabel = clearRankingConfirmArmed ? "Confirmar limpieza" : "Limpiar ranking";
        bool oldEnabled = GUI.enabled;
        GUI.enabled = transitionState == MenuTransitionState.Idle;
        if (GUILayout.Button(clearLabel, buttonStyle, GUILayout.Height(30f)))
        {
            if (clearRankingConfirmArmed)
            {
                GlitchAudioManager.PlayRankingSubmit();
                RankingStorage.ClearEntries();
                clearRankingConfirmArmed = false;
                rankingActionMessage = "Ranking limpiado.";
                rankingActionMessageExpireAt = Time.unscaledTime + 2.2f;
            }
            else
            {
                GlitchAudioManager.PlayMenuToggle();
                clearRankingConfirmArmed = true;
                clearRankingConfirmExpireAt = Time.unscaledTime + 2.8f;
                rankingActionMessage = "Pulsa nuevamente para confirmar.";
                rankingActionMessageExpireAt = clearRankingConfirmExpireAt;
            }
        }
        GUI.enabled = oldEnabled;

        GUILayout.EndArea();
    }

    private void GetMainMenuLayout(out Rect mainPanel, out Rect rankingPanel, out bool sideBySide)
    {
        float mainW = 390f;
        float mainH = 400f;
        float rankW = 400f;
        float rankH = 320f;
        float gap = 22f;
        float margin = 26f;

        sideBySide = Screen.width >= (mainW + rankW + gap + margin * 2f) && Screen.height >= Mathf.Max(mainH, rankH) + 70f;

        if (sideBySide)
        {
            float totalW = mainW + gap + rankW;
            float left = (Screen.width - totalW) * 0.5f;
            float yMain = (Screen.height - mainH) * 0.5f;
            float yRank = (Screen.height - rankH) * 0.5f;
            mainPanel = new Rect(left, yMain, mainW, mainH);
            rankingPanel = new Rect(left + mainW + gap, yRank, rankW, rankH);
            return;
        }

        mainPanel = CenterRect(mainW, mainH);
        float rankWidth = mainW + 36f;
        float rankY = mainPanel.yMax + 14f;
        rankingPanel = new Rect(mainPanel.x - 18f, rankY, rankWidth, 248f);
        if (rankingPanel.yMax > Screen.height - 10f)
        {
            rankingPanel.y = Mathf.Max(8f, Screen.height - rankingPanel.height - 10f);
        }
    }

    private void DrawOptionsMenu()
    {
        DrawScreenFade(0.46f);
        float bob = Mathf.Sin(Time.unscaledTime * 1.2f + 1.2f) * 5f;
        Rect panel = CenterRect(Mathf.Min(640f, Screen.width - 42f), Mathf.Min(500f, Screen.height - 42f));
        panel.y += bob;
        DrawPanel(panel, new Color(0.03f, 0.05f, 0.09f, 0.90f), new Color(0.47f, 0.56f, 0.72f, 0.55f));
        DrawPanelFx(panel, new Color(0.58f, 0.82f, 1f, 1f), 0.09f);

        if (!string.IsNullOrEmpty(optionsActionMessage) && Time.unscaledTime >= optionsActionMessageExpireAt)
        {
            optionsActionMessage = string.Empty;
        }

        Rect area = new Rect(panel.x + 18f, panel.y + 16f, panel.width - 36f, panel.height - 28f);
        GUI.Label(new Rect(area.x, area.y, area.width, 42f), "Opciones", panelTitleStyle);
        DrawSolidRect(new Rect(area.x, area.y + 50f, area.width, 1f), new Color(0.70f, 0.90f, 1f, 0.24f));

        Rect audio = new Rect(area.x, area.y + 66f, area.width * 0.48f, 124f);
        Rect interfaceBox = new Rect(audio.xMax + 18f, audio.y, area.width - audio.width - 18f, 184f);
        Rect display = new Rect(area.x, audio.yMax + 18f, audio.width, 156f);
        Rect actions = new Rect(interfaceBox.x, interfaceBox.yMax + 18f, interfaceBox.width, 136f);

        DrawStatsSection(audio, "Audio", new Color(0.55f, 0.95f, 1f, 1f));
        float newMaster = DrawOptionSlider(audio, 42f, "Volumen maestro", masterVolume, 0f, 1f);
        if (!Mathf.Approximately(newMaster, masterVolume))
        {
            masterVolume = newMaster;
            AudioListener.volume = masterVolume;
            UserSettings.SetMasterVolume(masterVolume);
        }

        DrawStatsSection(interfaceBox, "Interfaz", new Color(0.74f, 0.88f, 1f, 1f));
        float newUiScale = DrawOptionSlider(interfaceBox, 42f, "Escala del menu", uiScale, UserSettings.MinMenuUiScale, UserSettings.MaxMenuUiScale);
        if (!Mathf.Approximately(newUiScale, uiScale))
        {
            uiScale = newUiScale;
            UserSettings.SetMenuUiScale(uiScale);
        }

        float newHudScale = DrawOptionSlider(interfaceBox, 90f, "Escala del HUD", hudScale, UserSettings.MinHudScale, UserSettings.MaxHudScale);
        if (!Mathf.Approximately(newHudScale, hudScale))
        {
            hudScale = newHudScale;
            UserSettings.SetHudScale(hudScale);
        }

        float newMotion = DrawOptionSlider(interfaceBox, 138f, "Movimiento visual", menuMotionIntensity, UserSettings.MinMenuMotion, UserSettings.MaxMenuMotion);
        if (!Mathf.Approximately(newMotion, menuMotionIntensity))
        {
            menuMotionIntensity = newMotion;
            UserSettings.SetMenuMotion(menuMotionIntensity);
        }

        DrawStatsSection(display, "Pantalla", new Color(1f, 0.76f, 0.50f, 1f));
        bool newFullscreen = DrawOptionToggle(display, 42f, "Pantalla completa", fullscreen);
        if (newFullscreen != fullscreen)
        {
            fullscreen = newFullscreen;
            UserSettings.SetFullscreen(fullscreen);
            ApplyDisplaySettings();
        }

        bool newVSync = DrawOptionToggle(display, 86f, "VSync", vSyncEnabled);
        if (newVSync != vSyncEnabled)
        {
            vSyncEnabled = newVSync;
            UserSettings.SetVSync(vSyncEnabled);
            ApplyDisplaySettings();
        }

        DrawStatsSection(actions, "Acciones", new Color(0.95f, 0.58f, 1f, 1f));
        Rect resetRect = new Rect(actions.x + 14f, actions.y + 42f, actions.width - 28f, 34f);
        if (DrawAnimatedMenuButton(resetRect, "Restaurar opciones"))
        {
            ResetOptionsToDefault();
        }

        Rect tutorialRect = new Rect(actions.x + 14f, actions.y + 84f, actions.width - 28f, 34f);
        if (DrawAnimatedMenuButton(tutorialRect, "Mostrar tutorial"))
        {
            UserSettings.SetShowIntroTutorial(true);
            optionsActionMessage = "Tutorial de inicio activado.";
            optionsActionMessageExpireAt = Time.unscaledTime + 2.2f;
            GlitchAudioManager.PlayMenuConfirm();
        }

        if (!string.IsNullOrEmpty(optionsActionMessage))
        {
            GUI.Label(new Rect(area.x, area.yMax - 38f, area.width * 0.62f, 28f), optionsActionMessage, paragraphStyle);
        }

        Rect backRect = new Rect(area.xMax - 132f, area.yMax - 42f, 132f, 34f);
        if (DrawAnimatedMenuButton(backRect, "Volver", true))
        {
            showOptions = false;
        }
    }

    private void DrawStatsMenu()
    {
        DrawScreenFade(0.46f);
        float bob = Mathf.Sin(Time.unscaledTime * 1.2f + 0.35f) * 5f;
        Rect panel = CenterRect(Mathf.Min(720f, Screen.width - 42f), Mathf.Min(520f, Screen.height - 42f));
        panel.y += bob;
        DrawPanel(panel, new Color(0.03f, 0.05f, 0.09f, 0.92f), new Color(0.54f, 0.76f, 1f, 0.55f));
        DrawPanelFx(panel, new Color(0.50f, 0.92f, 1f, 1f), 0.10f);

        Rect area = new Rect(panel.x + 18f, panel.y + 16f, panel.width - 36f, panel.height - 28f);
        GUI.Label(new Rect(area.x, area.y, area.width, 42f), "Estadisticas", panelTitleStyle);
        DrawSolidRect(new Rect(area.x, area.y + 50f, area.width, 1f), new Color(0.70f, 0.90f, 1f, 0.24f));

        MetaProgressionStorage.CareerStats stats = MetaProgressionStorage.Stats;
        MetaProgressionStorage.RunReward last = MetaProgressionStorage.LastRunReward;
        DailyChallengeStorage.DailyChallenge daily = DailyChallengeStorage.CurrentChallenge;
        int dailyProgress = DailyChallengeStorage.CurrentProgress;
        bool dailyComplete = DailyChallengeStorage.IsCompleted;

        float footerH = 52f;
        Rect contentArea = new Rect(area.x, area.y + 66f, area.width, area.height - 122f - footerH);
        Rect left = new Rect(contentArea.x, contentArea.y, contentArea.width * 0.48f, contentArea.height);
        Rect right = new Rect(left.xMax + 18f, left.y, area.width - left.width - 18f, left.height);

        DrawStatsSection(left, "Perfil", new Color(0.45f, 0.90f, 1f, 1f));
        DrawStatLine(left, 34f, "Datos recuperados", MetaProgressionStorage.CurrentData.ToString());
        DrawStatLine(left, 64f, "Runs totales", stats.totalRuns.ToString());
        DrawStatLine(left, 94f, "Mejor puntuacion", $"{stats.bestScore} pts");
        DrawStatLine(left, 124f, "Mejor supervivencia", $"{stats.bestSurvivalTime:F1}s");
        DrawStatLine(left, 154f, "Puntos acumulados", stats.totalScore.ToString());
        DrawStatLine(left, 184f, "Tiempo acumulado", FormatDuration(stats.totalSurvivalTime));

        Rect records = new Rect(left.x, left.y + 230f, left.width, left.height - 230f);
        DrawStatsSection(records, "Records por zona", new Color(0.74f, 0.88f, 1f, 1f));
        DrawStatLine(records, 34f, "Lab", MetaProgressionStorage.GetArenaRecordLabel("Lab"));
        DrawStatLine(records, 64f, "Storage", MetaProgressionStorage.GetArenaRecordLabel("Storage"));
        DrawStatLine(records, 94f, "Rupture", MetaProgressionStorage.GetArenaRecordLabel("Rupture"));

        DrawStatsSection(right, "Operacion diaria", dailyComplete ? new Color(1f, 0.82f, 0.46f, 1f) : new Color(0.95f, 0.58f, 1f, 1f));
        GUI.Label(new Rect(right.x + 14f, right.y + 34f, right.width - 28f, 28f), daily.title, rankingTitleStyle);
        GUIStyle wrapped = new GUIStyle(paragraphStyle)
        {
            wordWrap = true,
            alignment = TextAnchor.UpperLeft,
            clipping = TextClipping.Clip
        };
        GUI.Label(new Rect(right.x + 14f, right.y + 70f, right.width - 28f, 54f), daily.description, wrapped);
        DrawStatLine(right, 130f, daily.progressLabel, dailyComplete ? "COMPLETADA" : $"{dailyProgress}/{daily.target}");

        Rect bar = new Rect(right.x + 14f, right.y + 164f, right.width - 28f, 10f);
        float normalized = dailyComplete ? 1f : Mathf.Clamp01(dailyProgress / Mathf.Max(1f, daily.target));
        DrawSolidRect(bar, new Color(0.04f, 0.06f, 0.10f, 0.92f));
        Color dailyAccent = dailyComplete ? new Color(1f, 0.82f, 0.46f, 0.86f) : new Color(0.95f, 0.58f, 1f, 0.78f);
        DrawSolidRect(new Rect(bar.x, bar.y, bar.width * normalized, bar.height), dailyAccent);
        DrawStatLine(right, 184f, "Recompensa", dailyComplete ? "Cobrada" : $"+{daily.dataReward} Datos");

        Rect lastRun = new Rect(right.x, right.y + 218f, right.width, Mathf.Max(146f, right.height - 218f));
        DrawStatsSection(lastRun, "Ultima run", new Color(1f, 0.76f, 0.50f, 1f));
        string lastSummary = last.score > 0
            ? $"{last.score} pts | {last.survivalTime:F1}s | {last.performanceGrade}"
            : "Sin runs registradas";
        DrawStatLine(lastRun, 30f, "Resultado", lastSummary);
        DrawStatLine(lastRun, 58f, "Zona", last.levelLabel);
        DrawStatLine(lastRun, 86f, "Datos ganados", $"+{last.dataEarned}");
        DrawStatLine(lastRun, 114f, "Bonus contratos", $"+{last.contractBonusData}");

        Rect backRect = new Rect(area.xMax - 132f, area.yMax - 36f, 132f, 34f);
        if (DrawAnimatedMenuButton(backRect, "Volver", true))
        {
            showStats = false;
        }
    }

    private void DrawStatsSection(Rect rect, string title, Color accent)
    {
        DrawSolidRect(rect, new Color(0.025f, 0.045f, 0.085f, 0.72f));
        DrawSolidRect(new Rect(rect.x, rect.y, rect.width, 2f), new Color(accent.r, accent.g, accent.b, 0.55f));
        GUI.Label(new Rect(rect.x + 12f, rect.y + 6f, rect.width - 24f, 24f), title, rankingRowStyle);
    }

    private void DrawStatLine(Rect container, float yOffset, string label, string value)
    {
        Rect row = new Rect(container.x + 14f, container.y + yOffset, container.width - 28f, 26f);
        DrawSolidRect(row, new Color(1f, 1f, 1f, 0.025f));
        Rect labelRect = new Rect(row.x + 6f, row.y, row.width * 0.58f, row.height);
        Rect valueRect = new Rect(row.x + row.width * 0.60f, row.y, row.width * 0.38f, row.height);
        GUI.Label(labelRect, label, BuildFittedSingleLineStyle(paragraphStyle, label, labelRect.width, labelRect.height, 10));
        GUI.Label(valueRect, value, BuildFittedSingleLineStyle(rankingScoreStyle, value, valueRect.width, valueRect.height, 10));
    }

    private float DrawOptionSlider(Rect container, float yOffset, string label, float value, float min, float max)
    {
        Rect labelRect = new Rect(container.x + 14f, container.y + yOffset, container.width - 28f, 22f);
        Rect sliderRect = new Rect(container.x + 14f, labelRect.yMax + 4f, container.width - 28f, 18f);
        Rect valueRect = new Rect(labelRect.x + labelRect.width - 58f, labelRect.y, 58f, labelRect.height);
        GUI.Label(new Rect(labelRect.x, labelRect.y, labelRect.width - 66f, labelRect.height), label, paragraphStyle);
        GUI.Label(valueRect, value.ToString("0.00"), rankingScoreStyle);
        return GUI.HorizontalSlider(sliderRect, value, min, max);
    }

    private bool DrawOptionToggle(Rect container, float yOffset, string label, bool value)
    {
        Rect row = new Rect(container.x + 14f, container.y + yOffset, container.width - 28f, 34f);
        bool hovered = row.Contains(Event.current.mousePosition);
        Color fill = value
            ? new Color(0.10f, 0.20f, 0.26f, 0.88f)
            : new Color(0.05f, 0.07f, 0.12f, 0.78f);
        if (hovered)
        {
            fill = Color.Lerp(fill, new Color(0.16f, 0.24f, 0.34f, 0.92f), 0.55f);
        }

        DrawSolidRect(row, fill);
        DrawSolidRect(new Rect(row.x, row.y, row.width, 2f), value ? new Color(0.50f, 1f, 0.74f, 0.56f) : new Color(0.48f, 0.72f, 1f, 0.28f));
        GUI.Label(new Rect(row.x + 10f, row.y, row.width - 76f, row.height), label, paragraphStyle);
        GUI.Label(new Rect(row.xMax - 68f, row.y, 58f, row.height), value ? "ON" : "OFF", rankingScoreStyle);
        if (GUI.Button(row, GUIContent.none, GUIStyle.none))
        {
            GlitchAudioManager.PlayMenuToggle();
            return !value;
        }

        return value;
    }

    private void DrawUnlocksMenu()
    {
        DrawScreenFade(0.46f);
        float bob = Mathf.Sin(Time.unscaledTime * 1.2f + 0.7f) * 5f;
        Rect panel = CenterRect(Mathf.Min(700f, Screen.width - 42f), Mathf.Min(520f, Screen.height - 42f));
        panel.y += bob;
        DrawPanel(panel, new Color(0.03f, 0.05f, 0.09f, 0.92f), new Color(0.52f, 0.78f, 1f, 0.55f));
        DrawPanelFx(panel, new Color(0.48f, 0.94f, 1f, 1f), 0.10f);

        if (!string.IsNullOrEmpty(unlockActionMessage) && Time.unscaledTime >= unlockActionMessageExpireAt)
        {
            unlockActionMessage = string.Empty;
        }

        Rect area = new Rect(panel.x + 18f, panel.y + 16f, panel.width - 36f, panel.height - 28f);
        MetaProgressionStorage.RunReward last = MetaProgressionStorage.LastRunReward;
        bool showingAchievements = selectedUnlockSection == AchievementStorage.SectionAchievements;
        List<MetaProgressionStorage.UnlockDefinition> unlocks = showingAchievements
            ? null
            : GetUnlocksForSection(selectedUnlockSection);
        IReadOnlyList<AchievementStorage.AchievementDefinition> achievements = AchievementStorage.Definitions;
        if (!showingAchievements && (unlocks == null || unlocks.Count == 0))
        {
            return;
        }

        if (showingAchievements)
        {
            selectedAchievementIndex = Mathf.Clamp(selectedAchievementIndex, 0, Mathf.Max(0, achievements.Count - 1));
        }
        else
        {
            selectedUnlockIndex = Mathf.Clamp(selectedUnlockIndex, 0, unlocks.Count - 1);
        }

        GUI.Label(new Rect(area.x, area.y, area.width, 42f), "Desbloqueos", panelTitleStyle);
        DrawSolidRect(new Rect(area.x, area.y + 50f, area.width, 1f), new Color(0.70f, 0.90f, 1f, 0.24f));

        Rect dataRect = new Rect(area.x, area.y + 62f, area.width * 0.44f, 40f);
        DrawSolidRect(dataRect, new Color(0.05f, 0.09f, 0.14f, 0.82f));
        GUI.Label(new Rect(dataRect.x + 10f, dataRect.y + 2f, dataRect.width - 20f, 18f), "DATOS RECUPERADOS", rankingRowStyle);
        GUI.Label(new Rect(dataRect.x + 10f, dataRect.y + 18f, dataRect.width - 20f, 22f), MetaProgressionStorage.CurrentData.ToString(), rankingScoreStyle);

        Rect lastRunRect = new Rect(area.x + dataRect.width + 12f, area.y + 62f, area.width - dataRect.width - 12f, 40f);
        string lastRun = last.dataEarned > 0
            ? $"+{last.dataEarned} datos | {last.score} pts | {last.survivalTime:F1}s"
            : "Completa una run para recuperar datos.";
        DrawSolidRect(lastRunRect, new Color(0.05f, 0.07f, 0.12f, 0.72f));
        GUI.Label(new Rect(lastRunRect.x + 10f, lastRunRect.y + 2f, lastRunRect.width - 20f, 18f), "ULTIMA RUN", rankingRowStyle);
        GUI.Label(new Rect(lastRunRect.x + 10f, lastRunRect.y + 20f, lastRunRect.width - 20f, 18f), lastRun, paragraphStyle);

        Rect tabsRect = new Rect(area.x, area.y + 114f, area.width, 34f);
        DrawUnlockSectionTabs(tabsRect);

        Rect listRect = new Rect(area.x, area.y + 158f, area.width * 0.43f, area.height - 214f);
        Rect detailRect = new Rect(listRect.xMax + 14f, listRect.y, area.width - listRect.width - 14f, listRect.height);
        if (showingAchievements)
        {
            DrawAchievementList(listRect, achievements);
            DrawAchievementDetails(detailRect, achievements[selectedAchievementIndex]);
        }
        else
        {
            DrawUnlockList(listRect, unlocks);
            DrawUnlockDetails(detailRect, unlocks[selectedUnlockIndex]);
        }

        if (!string.IsNullOrEmpty(unlockActionMessage))
        {
            GUI.Label(new Rect(area.x, area.yMax - 42f, area.width * 0.62f, 32f), unlockActionMessage, paragraphStyle);
        }

        Rect backRect = new Rect(area.xMax - 132f, area.yMax - 42f, 132f, 34f);
        if (DrawAnimatedMenuButton(backRect, "Volver", true))
        {
            showUnlocks = false;
        }
    }

    private void DrawDeveloperMenu()
    {
        DrawScreenFade(0.52f);
        Rect panel = CenterRect(Mathf.Min(620f, Screen.width - 42f), Mathf.Min(480f, Screen.height - 42f));
        DrawPanel(panel, new Color(0.04f, 0.035f, 0.06f, 0.94f), new Color(1f, 0.58f, 0.78f, 0.62f));
        DrawPanelFx(panel, new Color(1f, 0.48f, 0.86f, 1f), 0.13f);

        Rect area = new Rect(panel.x + 18f, panel.y + 16f, panel.width - 36f, panel.height - 28f);
        GUILayout.BeginArea(area);
        GUILayout.Label("Modo Desarrollador", panelTitleStyle);
        GUILayout.Space(4f);
        GUILayout.Label("Panel oculto de testing | Ctrl + Shift + D", paragraphStyle);
        GUILayout.Space(8f);

        GUILayout.Label($"Datos Recuperados: {MetaProgressionStorage.CurrentData}", rankingScoreStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("+25 Datos", buttonStyle, GUILayout.Height(34f)))
        {
            MetaProgressionStorage.AddData(25);
            GlitchAudioManager.PlayUpgradeSelect();
        }
        if (GUILayout.Button("+100 Datos", buttonStyle, GUILayout.Height(34f)))
        {
            MetaProgressionStorage.AddData(100);
            GlitchAudioManager.PlayUpgradeSelect();
        }
        if (GUILayout.Button("-25 Datos", buttonStyle, GUILayout.Height(34f)))
        {
            MetaProgressionStorage.AddData(-25);
            GlitchAudioManager.PlayMenuToggle();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(8f);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Desbloquear todo", buttonStyle, GUILayout.Height(34f)))
        {
            MetaProgressionStorage.UnlockAll();
            GlitchAudioManager.PlayUpgradeSelect();
        }
        if (GUILayout.Button("Reset progresion", buttonStyle, GUILayout.Height(34f)))
        {
            MetaProgressionStorage.ResetProgress();
            AchievementStorage.ResetAchievements();
            GlitchAudioManager.PlayMenuBack();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(6f);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Completar logros", buttonStyle, GUILayout.Height(34f)))
        {
            AchievementStorage.UnlockAll();
            GlitchAudioManager.PlayUpgradeSelect();
        }
        if (GUILayout.Button("Reset logros", buttonStyle, GUILayout.Height(34f)))
        {
            AchievementStorage.ResetAchievements();
            GlitchAudioManager.PlayMenuBack();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(6f);
        GUILayout.Label($"Operacion diaria: {DailyChallengeStorage.CurrentSummary}", paragraphStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Completar diaria", buttonStyle, GUILayout.Height(34f)))
        {
            DailyChallengeStorage.DailyChallenge challenge;
            DailyChallengeStorage.CompleteCurrentChallenge(out challenge);
            GlitchAudioManager.PlayUpgradeSelect();
        }
        if (GUILayout.Button("Reset diaria", buttonStyle, GUILayout.Height(34f)))
        {
            DailyChallengeStorage.ResetCurrentChallenge();
            GlitchAudioManager.PlayMenuBack();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(14f);
        GUILayout.Label($"Arena override: {DeveloperModeStorage.GetArenaOverrideLabel()}", rankingScoreStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Lab", buttonStyle, GUILayout.Height(34f)))
        {
            DeveloperModeStorage.SetArenaOverride(ProceduralArenaGenerator.ArenaTheme.ContainmentLab);
            GlitchAudioManager.PlayMenuToggle();
        }
        if (GUILayout.Button("Storage", buttonStyle, GUILayout.Height(34f)))
        {
            DeveloperModeStorage.SetArenaOverride(ProceduralArenaGenerator.ArenaTheme.StorageBay);
            GlitchAudioManager.PlayMenuToggle();
        }
        if (GUILayout.Button("Rupture", buttonStyle, GUILayout.Height(34f)))
        {
            DeveloperModeStorage.SetArenaOverride(ProceduralArenaGenerator.ArenaTheme.RuptureZone);
            GlitchAudioManager.PlayMenuToggle();
        }
        if (GUILayout.Button("Random", buttonStyle, GUILayout.Height(34f)))
        {
            DeveloperModeStorage.ClearArenaOverride();
            GlitchAudioManager.PlayMenuToggle();
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(8f);
        if (GUILayout.Button($"Ejecutar mapa: {DeveloperModeStorage.GetArenaOverrideLabel()}", buttonStyle, GUILayout.Height(42f)))
        {
            StartGameplay();
        }

        GUILayout.FlexibleSpace();
        if (DrawMenuButton("Cerrar", 38f, true))
        {
            showDeveloperMenu = false;
        }

        GUILayout.EndArea();
    }

    private void DrawUnlockList(Rect listRect, IReadOnlyList<MetaProgressionStorage.UnlockDefinition> unlocks)
    {
        DrawSolidRect(listRect, new Color(0.02f, 0.035f, 0.07f, 0.62f));
        DrawSolidRect(new Rect(listRect.x, listRect.y, listRect.width, 2f), new Color(0.48f, 0.86f, 1f, 0.35f));

        float rowGap = 7f;
        float rowHeight = Mathf.Min(54f, (listRect.height - rowGap * (unlocks.Count + 1)) / Mathf.Max(1, unlocks.Count));
        float y = listRect.y + rowGap;
        for (int i = 0; i < unlocks.Count; i++)
        {
            Rect row = new Rect(listRect.x + 8f, y, listRect.width - 16f, rowHeight);
            DrawUnlockSelectableRow(row, unlocks[i], i);
            y += rowHeight + rowGap;
        }
    }

    private List<MetaProgressionStorage.UnlockDefinition> GetUnlocksForSection(string section)
    {
        List<MetaProgressionStorage.UnlockDefinition> result = new List<MetaProgressionStorage.UnlockDefinition>();
        IReadOnlyList<MetaProgressionStorage.UnlockDefinition> all = MetaProgressionStorage.UnlockDefinitions;
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i].section == section)
            {
                result.Add(all[i]);
            }
        }

        return result;
    }

    private void DrawUnlockSectionTabs(Rect tabsRect)
    {
        float gap = 10f;
        float tabW = (tabsRect.width - gap * 2f) / 3f;
        DrawUnlockSectionTab(new Rect(tabsRect.x, tabsRect.y, tabW, tabsRect.height), MetaProgressionStorage.SectionRunUpgrades, "Mejoras");
        DrawUnlockSectionTab(new Rect(tabsRect.x + tabW + gap, tabsRect.y, tabW, tabsRect.height), MetaProgressionStorage.SectionSkins, "Colores");
        DrawUnlockSectionTab(new Rect(tabsRect.x + (tabW + gap) * 2f, tabsRect.y, tabW, tabsRect.height), AchievementStorage.SectionAchievements, "Logros");
    }

    private void DrawUnlockSectionTab(Rect rect, string section, string label)
    {
        bool selected = selectedUnlockSection == section;
        bool hovered = rect.Contains(Event.current.mousePosition);
        Color fill = selected
            ? new Color(0.12f, 0.24f, 0.34f, 0.92f)
            : new Color(0.05f, 0.08f, 0.13f, 0.72f);
        if (hovered && !selected)
        {
            fill = Color.Lerp(fill, new Color(0.10f, 0.18f, 0.26f, 0.88f), 0.6f);
        }

        DrawSolidRect(rect, fill);
        DrawSolidRect(new Rect(rect.x, rect.y, rect.width, 2f), new Color(0.48f, 0.86f, 1f, selected ? 0.82f : 0.32f));
        GUI.Label(rect, label, buttonStyle);

        if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
        {
            GlitchAudioManager.PlayMenuToggle();
            selectedUnlockSection = section;
            selectedUnlockIndex = 0;
            selectedAchievementIndex = 0;
        }
    }

    private void DrawAchievementList(Rect listRect, IReadOnlyList<AchievementStorage.AchievementDefinition> achievements)
    {
        DrawSolidRect(listRect, new Color(0.02f, 0.035f, 0.07f, 0.62f));
        DrawSolidRect(new Rect(listRect.x, listRect.y, listRect.width, 2f), new Color(0.95f, 0.64f, 1f, 0.38f));

        float rowGap = 7f;
        float rowHeight = Mathf.Min(50f, (listRect.height - rowGap * (achievements.Count + 1)) / Mathf.Max(1, achievements.Count));
        float y = listRect.y + rowGap;
        for (int i = 0; i < achievements.Count; i++)
        {
            Rect row = new Rect(listRect.x + 8f, y, listRect.width - 16f, rowHeight);
            DrawAchievementSelectableRow(row, achievements[i], i);
            y += rowHeight + rowGap;
        }
    }

    private void DrawAchievementSelectableRow(Rect row, AchievementStorage.AchievementDefinition achievement, int index)
    {
        bool unlocked = AchievementStorage.IsUnlocked(achievement.id);
        bool selected = index == selectedAchievementIndex;
        bool hovered = row.Contains(Event.current.mousePosition);
        TrackButtonHover($"achievement_row_{index}", hovered);

        Color fill = unlocked
            ? new Color(0.13f, 0.18f, 0.12f, 0.82f)
            : new Color(0.05f, 0.07f, 0.12f, 0.78f);
        if (selected || hovered)
        {
            fill = Color.Lerp(fill, new Color(0.18f, 0.16f, 0.28f, 0.92f), selected ? 0.85f : 0.45f);
        }

        DrawSolidRect(row, fill);
        DrawSolidRect(new Rect(row.x, row.y, row.width, 2f), unlocked ? new Color(1f, 0.84f, 0.48f, 0.60f) : new Color(0.84f, 0.58f, 1f, 0.38f));
        if (selected)
        {
            DrawSolidRect(new Rect(row.x, row.y, 4f, row.height), new Color(1f, 0.72f, 0.92f, 0.82f));
        }

        int progress = AchievementStorage.GetProgress(achievement, 0f);
        string state = unlocked ? "COMPLETADO" : $"{progress}/{achievement.target}";
        GUI.Label(new Rect(row.x + 10f, row.y + 4f, row.width - 20f, 22f), achievement.title, rankingRowStyle);
        GUI.Label(new Rect(row.x + 10f, row.y + row.height - 23f, row.width - 20f, 19f), state, unlocked ? rankingScoreStyle : paragraphStyle);

        if (GUI.Button(row, GUIContent.none, GUIStyle.none))
        {
            GlitchAudioManager.PlayMenuToggle();
            selectedAchievementIndex = index;
        }
    }

    private void DrawAchievementDetails(Rect detailRect, AchievementStorage.AchievementDefinition achievement)
    {
        bool unlocked = AchievementStorage.IsUnlocked(achievement.id);
        int progress = AchievementStorage.GetProgress(achievement, 0f);
        float normalized = unlocked ? 1f : Mathf.Clamp01(progress / Mathf.Max(1f, achievement.target));
        Color accent = unlocked ? new Color(1f, 0.82f, 0.46f, 1f) : new Color(0.94f, 0.58f, 1f, 1f);

        DrawSolidRect(detailRect, new Color(0.025f, 0.045f, 0.085f, 0.76f));
        DrawSolidRect(new Rect(detailRect.x, detailRect.y, detailRect.width, 2f), new Color(accent.r, accent.g, accent.b, 0.55f));
        DrawSolidRect(new Rect(detailRect.x, detailRect.yMax - 2f, detailRect.width, 2f), new Color(accent.r, accent.g, accent.b, 0.30f));

        Rect icon = new Rect(detailRect.x + 18f, detailRect.y + 18f, 62f, 62f);
        DrawSolidRect(icon, new Color(accent.r, accent.g, accent.b, unlocked ? 0.25f : 0.14f));
        DrawSolidRect(new Rect(icon.x, icon.y, icon.width, 2f), new Color(accent.r, accent.g, accent.b, 0.68f));
        GUI.Label(icon, unlocked ? "OK" : "?", rankingScoreStyle);

        Rect titleRect = new Rect(icon.xMax + 14f, detailRect.y + 12f, detailRect.width - 112f, 42f);
        GUI.Label(titleRect, achievement.title, BuildFittedSingleLineStyle(rankingTitleStyle, achievement.title, titleRect.width, titleRect.height, 13));
        GUI.Label(new Rect(icon.xMax + 14f, detailRect.y + 54f, detailRect.width - 112f, 24f), unlocked ? "COMPLETADO" : $"+{achievement.dataReward} DATOS", rankingScoreStyle);

        GUIStyle detailParagraph = new GUIStyle(paragraphStyle)
        {
            wordWrap = true,
            alignment = TextAnchor.UpperLeft,
            clipping = TextClipping.Clip
        };

        Rect desc = new Rect(detailRect.x + 18f, detailRect.y + 104f, detailRect.width - 36f, 68f);
        GUI.Label(desc, achievement.description, detailParagraph);

        Rect progressLabel = new Rect(detailRect.x + 18f, desc.yMax + 8f, detailRect.width - 36f, 22f);
        GUI.Label(progressLabel, $"{achievement.progressLabel}: {progress}/{achievement.target}", paragraphStyle);

        Rect bar = new Rect(detailRect.x + 18f, progressLabel.yMax + 6f, detailRect.width - 36f, 10f);
        DrawSolidRect(bar, new Color(0.04f, 0.06f, 0.10f, 0.92f));
        DrawSolidRect(new Rect(bar.x, bar.y, bar.width * normalized, bar.height), new Color(accent.r, accent.g, accent.b, 0.78f));

        Rect noteRect = new Rect(detailRect.x + 18f, detailRect.yMax - 84f, detailRect.width - 36f, 48f);
        Color noteFill = unlocked
            ? new Color(0.20f, 0.15f, 0.08f, 0.78f)
            : new Color(accent.r, accent.g, accent.b, 0.10f);
        DrawSolidRect(noteRect, noteFill);
        DrawSolidRect(new Rect(noteRect.x, noteRect.y, noteRect.width, 2f), new Color(accent.r, accent.g, accent.b, unlocked ? 0.46f : 0.22f));
        string note = unlocked
            ? $"Recompensa cobrada: +{achievement.dataReward} Datos."
            : "Completa este objetivo para cobrar Datos y avanzar tu progreso.";
        GUIStyle noteStyle = new GUIStyle(detailParagraph);
        noteStyle.normal.textColor = unlocked
            ? new Color(1f, 0.82f, 0.48f, 0.98f)
            : new Color(0.82f, 0.90f, 1f, 0.94f);
        GUI.Label(new Rect(noteRect.x + 10f, noteRect.y + 6f, noteRect.width - 20f, noteRect.height - 12f), note, noteStyle);
    }

    private void DrawUnlockSelectableRow(Rect row, MetaProgressionStorage.UnlockDefinition unlock, int index)
    {
        bool unlocked = MetaProgressionStorage.IsUnlocked(unlock.id);
        bool selected = index == selectedUnlockIndex;
        bool hovered = row.Contains(Event.current.mousePosition);
        TrackButtonHover($"unlock_row_{index}", hovered);

        Color fill = unlocked
            ? new Color(0.08f, 0.18f, 0.15f, 0.82f)
            : new Color(0.05f, 0.07f, 0.12f, 0.78f);
        if (selected || hovered)
        {
            fill = Color.Lerp(fill, new Color(0.12f, 0.20f, 0.30f, 0.92f), selected ? 0.85f : 0.45f);
        }

        DrawSolidRect(row, fill);
        DrawSolidRect(new Rect(row.x, row.y, row.width, 2f), unlocked ? new Color(0.50f, 1f, 0.74f, 0.52f) : new Color(0.48f, 0.72f, 1f, 0.38f));
        if (selected)
        {
            DrawSolidRect(new Rect(row.x, row.y, 4f, row.height), new Color(0.48f, 0.94f, 1f, 0.82f));
        }

        float textX = row.x + (unlock.skin ? 44f : 10f);
        if (unlock.skin)
        {
            Rect swatch = new Rect(row.x + 10f, row.y + 10f, 24f, row.height - 20f);
            DrawSolidRect(swatch, unlock.bodyColor);
            DrawSolidRect(new Rect(swatch.x, swatch.y, swatch.width, 2f), unlock.trailColor);
        }

        GUI.Label(new Rect(textX, row.y + 4f, row.xMax - textX - 10f, 22f), unlock.title, rankingRowStyle);

        string state = GetUnlockStateLabel(unlock, unlocked);
        GUI.Label(new Rect(textX, row.y + row.height - 24f, row.xMax - textX - 10f, 20f), state, unlocked ? rankingScoreStyle : paragraphStyle);

        if (GUI.Button(row, GUIContent.none, GUIStyle.none))
        {
            GlitchAudioManager.PlayMenuToggle();
            selectedUnlockIndex = index;
        }
    }

    private void DrawUnlockDetails(Rect detailRect, MetaProgressionStorage.UnlockDefinition unlock)
    {
        bool unlocked = MetaProgressionStorage.IsUnlocked(unlock.id);
        bool selectedSkin = unlock.skin && MetaProgressionStorage.IsSelectedSkin(unlock.id);
        bool canBuy = !unlocked && MetaProgressionStorage.CurrentData >= unlock.cost;
        bool canSelect = unlock.skin && unlocked && !selectedSkin;
        Color accent = unlocked ? new Color(0.50f, 1f, 0.74f, 1f) : new Color(0.48f, 0.86f, 1f, 1f);

        DrawSolidRect(detailRect, new Color(0.025f, 0.045f, 0.085f, 0.76f));
        DrawSolidRect(new Rect(detailRect.x, detailRect.y, detailRect.width, 2f), new Color(accent.r, accent.g, accent.b, 0.55f));
        DrawSolidRect(new Rect(detailRect.x, detailRect.yMax - 2f, detailRect.width, 2f), new Color(accent.r, accent.g, accent.b, 0.30f));

        Rect icon = new Rect(detailRect.x + 18f, detailRect.y + 18f, 62f, 62f);
        DrawSolidRect(icon, unlock.skin ? unlock.bodyColor : new Color(accent.r, accent.g, accent.b, 0.18f));
        DrawSolidRect(new Rect(icon.x, icon.y, icon.width, 2f), new Color(accent.r, accent.g, accent.b, 0.68f));
        if (unlock.skin)
        {
            DrawSolidRect(new Rect(icon.x + 8f, icon.yMax - 16f, icon.width - 16f, 6f), unlock.trailColor);
        }
        else
        {
            GUI.Label(icon, unlocked ? "OK" : "++", rankingScoreStyle);
        }

        Rect titleRect = new Rect(icon.xMax + 14f, detailRect.y + 12f, detailRect.width - 112f, 42f);
        GUI.Label(titleRect, unlock.title, BuildFittedSingleLineStyle(rankingTitleStyle, unlock.title, titleRect.width, titleRect.height, 13));
        GUI.Label(new Rect(icon.xMax + 14f, detailRect.y + 54f, detailRect.width - 112f, 24f), GetUnlockStateLabel(unlock, unlocked), rankingScoreStyle);

        Rect desc = new Rect(detailRect.x + 18f, detailRect.y + 104f, detailRect.width - 36f, 90f);
        GUIStyle detailParagraph = new GUIStyle(paragraphStyle)
        {
            wordWrap = true,
            alignment = TextAnchor.UpperLeft,
            clipping = TextClipping.Clip
        };
        GUI.Label(desc, unlock.description, detailParagraph);

        Rect button = new Rect(detailRect.x + 18f, detailRect.yMax - 42f, detailRect.width - 36f, 34f);
        Rect ruleRect = new Rect(detailRect.x + 18f, button.y - 68f, detailRect.width - 36f, 50f);
        DrawSolidRect(ruleRect, new Color(accent.r, accent.g, accent.b, 0.10f));
        GUI.Label(
            new Rect(ruleRect.x + 10f, ruleRect.y + 5f, ruleRect.width - 20f, ruleRect.height - 10f),
            GetUnlockRuleText(unlock, unlocked, selectedSkin),
            detailParagraph);

        bool oldEnabled = GUI.enabled;
        GUI.enabled = (canBuy || canSelect) && transitionState == MenuTransitionState.Idle;
        if (GUI.Button(button, GetUnlockButtonLabel(unlock, unlocked, selectedSkin), buttonStyle))
        {
            bool success = false;
            if (canSelect)
            {
                success = MetaProgressionStorage.TrySelectSkin(unlock.id);
                unlockActionMessage = success ? $"Color activo: {unlock.title}" : "No se pudo seleccionar.";
            }
            else if (MetaProgressionStorage.TryUnlock(unlock.id))
            {
                success = true;
                if (unlock.skin)
                {
                    MetaProgressionStorage.TrySelectSkin(unlock.id);
                    unlockActionMessage = $"Color activo: {unlock.title}";
                }
                else
                {
                    unlockActionMessage = $"Desbloqueado: {unlock.title}";
                }
            }
            else
            {
                unlockActionMessage = "Datos insuficientes.";
            }

            if (success)
            {
                GlitchAudioManager.PlayUpgradeSelect();
            }
            else
            {
                GlitchAudioManager.PlayMenuToggle();
            }

            unlockActionMessageExpireAt = Time.unscaledTime + 2.4f;
        }
        GUI.enabled = oldEnabled;
    }

    private static string GetUnlockStateLabel(MetaProgressionStorage.UnlockDefinition unlock, bool unlocked)
    {
        if (unlock.skin && MetaProgressionStorage.IsSelectedSkin(unlock.id))
        {
            return "SELECCIONADO";
        }

        return unlocked ? "DESBLOQUEADO" : $"{unlock.cost} DATOS";
    }

    private static string GetUnlockButtonLabel(MetaProgressionStorage.UnlockDefinition unlock, bool unlocked, bool selectedSkin)
    {
        if (unlock.skin)
        {
            if (selectedSkin)
            {
                return "Color activo";
            }

            return unlocked ? "Usar color" : "Comprar color";
        }

        return unlocked ? "Desbloqueado" : "Comprar desbloqueo";
    }

    private static string GetUnlockRuleText(MetaProgressionStorage.UnlockDefinition unlock, bool unlocked, bool selectedSkin)
    {
        if (unlock.skin)
        {
            if (selectedSkin)
            {
                return "Color activo para la proxima run.";
            }

            return unlocked ? "Selecciona este color para usarlo." : "Compra esta paleta del jugador.";
        }

        return unlocked ? "Modulo disponible en futuras runs." : "Suma este modulo al pool de mejoras.";
    }

    private void StartGameplay()
    {
        if (transitionState != MenuTransitionState.Idle)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(gameplaySceneName) && Application.CanStreamedLevelBeLoaded(gameplaySceneName))
        {
            transitionState = MenuTransitionState.ExitFadeIn;
            transitionTimer = 0f;
            queuedGameplayLoad = false;
            return;
        }

        Debug.LogError($"Gameplay scene '{gameplaySceneName}' is not in Build Settings.");
    }

    private void UpdateTransition()
    {
        float dt = Time.unscaledDeltaTime;

        switch (transitionState)
        {
            case MenuTransitionState.IntroHold:
                transitionTimer += dt;
                blackOverlayAlpha = 1f;
                if (transitionTimer >= Mathf.Max(0f, introBlackHoldDuration))
                {
                    transitionState = MenuTransitionState.IntroFadeOut;
                    transitionTimer = 0f;
                }
                break;

            case MenuTransitionState.IntroFadeOut:
                transitionTimer += dt;
                blackOverlayAlpha = 1f - Mathf.Clamp01(transitionTimer / Mathf.Max(0.01f, introFadeOutDuration));
                if (transitionTimer >= introFadeOutDuration)
                {
                    blackOverlayAlpha = 0f;
                    transitionState = MenuTransitionState.Idle;
                    transitionTimer = 0f;
                }
                break;

            case MenuTransitionState.ExitFadeIn:
                transitionTimer += dt;
                blackOverlayAlpha = Mathf.Clamp01(transitionTimer / Mathf.Max(0.01f, exitFadeInDuration));
                if (transitionTimer >= exitFadeInDuration && !queuedGameplayLoad)
                {
                    queuedGameplayLoad = true;
                    Time.timeScale = 1f;
                    if (Application.CanStreamedLevelBeLoaded(gameplaySceneName))
                    {
                        SceneTransitionController.LoadScene(gameplaySceneName);
                    }
                    else
                    {
                        SceneManager.LoadScene(gameplaySceneName);
                    }
                }
                break;

            case MenuTransitionState.Idle:
            default:
                blackOverlayAlpha = 0f;
                break;
        }
    }

    private void BeginIntroImmediateBlack()
    {
        transitionState = MenuTransitionState.IntroHold;
        transitionTimer = 0f;
        blackOverlayAlpha = 1f;
        queuedGameplayLoad = false;
        introReady = false;
    }

    private void NormalizeTransitionDurations()
    {
        if (introBlackHoldDuration < 0.05f)
        {
            introBlackHoldDuration = 0.20f;
        }

        if (introFadeOutDuration < 0.25f)
        {
            introFadeOutDuration = 1.30f;
        }

        if (exitFadeInDuration < 0.25f)
        {
            exitFadeInDuration = 1.05f;
        }
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

    private void DrawTransitionBlackOverlay()
    {
        if (blackOverlayAlpha <= 0.001f)
        {
            return;
        }

        Color old = GUI.color;
        GUI.color = new Color(transitionBlack.r, transitionBlack.g, transitionBlack.b, Mathf.Clamp01(blackOverlayAlpha));
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
        DrawGlobalAmbientOverlay(t);
    }

    private static void DrawGlobalAmbientOverlay(float time)
    {
        DrawSolidRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.02f, 0.03f, 0.06f, 0.14f));

        float stripeStep = Mathf.Max(26f, Screen.height / 24f);
        for (float y = 0f; y < Screen.height + stripeStep; y += stripeStep)
        {
            float alpha = 0.01f + 0.015f * (0.5f + 0.5f * Mathf.Sin((y * 0.045f) + (time * 1.8f)));
            DrawSolidRect(new Rect(0f, y, Screen.width, 1f), new Color(1f, 1f, 1f, alpha));
        }

        for (int i = 0; i < 18; i++)
        {
            float seed = i * 0.173f;
            float x = Mathf.PerlinNoise(seed, time * 0.22f + 2.1f) * Screen.width;
            float y = Mathf.PerlinNoise(seed * 2.3f, time * 0.18f + 7.7f) * Screen.height;
            float w = Mathf.Lerp(2f, 5f, Mathf.PerlinNoise(seed * 4.1f, time * 0.3f));
            float h = Mathf.Lerp(2f, 5f, Mathf.PerlinNoise(seed * 5.2f, time * 0.25f));
            DrawSolidRect(new Rect(x, y, w, h), new Color(0.84f, 0.91f, 1f, 0.08f));
        }

        float band = Mathf.Repeat(time * 85f, Screen.height + 220f) - 110f;
        DrawSolidRect(new Rect(0f, band, Screen.width, 42f), new Color(0.66f, 0.78f, 0.97f, 0.025f));
    }

    private static void DrawMenuAtmosphere(Rect mainPanel, Rect rankingPanel, bool sideBySide)
    {
        float t = Time.unscaledTime;
        Vector2 mainCenter = new Vector2(mainPanel.x + mainPanel.width * 0.5f, mainPanel.y + mainPanel.height * 0.5f);
        Vector2 rankCenter = new Vector2(rankingPanel.x + rankingPanel.width * 0.5f, rankingPanel.y + rankingPanel.height * 0.5f);

        float pulseA = 0.5f + 0.5f * Mathf.Sin(t * 1.3f);
        float pulseB = 0.5f + 0.5f * Mathf.Sin(t * 1.1f + 1.6f);

        DrawFilledCircle(mainCenter, 118f + pulseA * 12f, new Color(0.20f, 0.36f, 0.62f, 0.10f));
        DrawFilledCircle(rankCenter, 102f + pulseB * 10f, new Color(0.38f, 0.24f, 0.68f, 0.09f));

        Color ring = new Color(0.75f, 0.85f, 1f, 0.12f + pulseA * 0.06f);
        DrawEllipseRing(mainCenter, mainPanel.width * 0.56f, mainPanel.height * 0.48f, ring);
        DrawEllipseRing(rankCenter, rankingPanel.width * 0.52f, rankingPanel.height * 0.45f, ring);

        if (sideBySide)
        {
            float x0 = mainPanel.xMax + 8f;
            float x1 = rankingPanel.x - 8f;
            float y = (mainCenter.y + rankCenter.y) * 0.5f;
            DrawSolidRect(new Rect(x0, y - 1f, Mathf.Max(4f, x1 - x0), 2f), new Color(0.88f, 0.95f, 1f, 0.14f));
        }
    }

    private void DrawSignalBeams(float time)
    {
        float pulse = 0.5f + 0.5f * Mathf.Sin(time * 2f);
        float alpha = (0.05f + pulse * 0.04f) * Mathf.Clamp(menuMotionIntensity, 0.4f, 2f);
        float yA = Screen.height * 0.24f + Mathf.Sin(time * 0.9f) * 22f;
        float yB = Screen.height * 0.74f + Mathf.Sin(time * 1.15f + 1.4f) * 18f;
        DrawSolidRect(new Rect(0f, yA, Screen.width, 2f), new Color(0.72f, 0.86f, 1f, alpha));
        DrawSolidRect(new Rect(0f, yB, Screen.width, 1f), new Color(0.98f, 0.52f, 0.72f, alpha * 0.65f));

        float xA = Screen.width * 0.33f + Mathf.Sin(time * 0.75f + 2f) * 16f;
        float xB = Screen.width * 0.66f + Mathf.Sin(time * 0.95f + 0.8f) * 14f;
        DrawSolidRect(new Rect(xA, 0f, 1f, Screen.height), new Color(0.62f, 0.74f, 0.96f, alpha * 0.42f));
        DrawSolidRect(new Rect(xB, 0f, 1f, Screen.height), new Color(0.95f, 0.57f, 0.82f, alpha * 0.32f));
    }

    private static void DrawPanelOrbiters(Rect mainPanel, Rect rankingPanel)
    {
        float t = Time.unscaledTime;
        DrawRectOrbit(mainPanel, t * 0.55f, new Color(0.84f, 0.94f, 1f, 0.22f));
        DrawRectOrbit(rankingPanel, -t * 0.48f, new Color(0.98f, 0.74f, 0.92f, 0.16f));
    }

    private static void DrawRectOrbit(Rect rect, float phase, Color color)
    {
        float perimeter = Mathf.Max(1f, rect.width * 2f + rect.height * 2f);
        float p = Mathf.Repeat(phase * 72f, perimeter);
        Vector2 pos = GetPointOnRectPerimeter(rect, p);
        DrawSolidRect(new Rect(pos.x - 2f, pos.y - 2f, 4f, 4f), color);
        Vector2 pos2 = GetPointOnRectPerimeter(rect, Mathf.Repeat(p + perimeter * 0.5f, perimeter));
        DrawSolidRect(new Rect(pos2.x - 1f, pos2.y - 1f, 2f, 2f), new Color(color.r, color.g, color.b, color.a * 0.7f));
    }

    private static Vector2 GetPointOnRectPerimeter(Rect rect, float dist)
    {
        float top = rect.width;
        float right = rect.height;
        float bottom = rect.width;
        float left = rect.height;

        if (dist < top)
        {
            return new Vector2(rect.x + dist, rect.y);
        }

        dist -= top;
        if (dist < right)
        {
            return new Vector2(rect.xMax, rect.y + dist);
        }

        dist -= right;
        if (dist < bottom)
        {
            return new Vector2(rect.xMax - dist, rect.yMax);
        }

        dist -= bottom;
        return new Vector2(rect.x, rect.yMax - Mathf.Min(left, dist));
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

    private bool DrawMenuButton(string label, float height, bool backSound = false)
    {
        bool canInteract = transitionState == MenuTransitionState.Idle;
        Color old = GUI.color;
        float pulse = 0.86f + 0.14f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2f));
        GUI.color = new Color(pulse, pulse, pulse, 1f);
        bool oldEnabled = GUI.enabled;
        GUI.enabled = canInteract;
        bool clicked = GUILayout.Button(label, buttonStyle, GUILayout.Height(height));
        GUI.enabled = oldEnabled;
        GUI.color = old;
        if (clicked)
        {
            if (backSound)
            {
                GlitchAudioManager.PlayMenuBack();
            }
            else
            {
                GlitchAudioManager.PlayMenuConfirm();
            }
        }

        return clicked;
    }

    private bool DrawAnimatedMenuButton(Rect rect, string label, bool primary = false)
    {
        bool canInteract = transitionState == MenuTransitionState.Idle;
        bool hovered = rect.Contains(Event.current.mousePosition);
        TrackButtonHover(label, canInteract && hovered);
        float hoverLerp = GetButtonHoverState(label, hovered);
        float t = Time.unscaledTime;
        float pulse = 0.6f + 0.4f * Mathf.Sin(t * (primary ? 3.6f : 2.8f));
        float wobble = hovered ? Mathf.Sin(t * 7.2f) * 0.7f : 0f;
        rect.y += wobble;

        Color baseFill = primary
            ? new Color(0.10f, 0.18f, 0.28f, 0.94f)
            : new Color(0.08f, 0.11f, 0.18f, 0.90f);
        Color fill = Color.Lerp(baseFill, new Color(0.18f, 0.30f, 0.48f, 0.96f), hoverLerp * 0.65f);
        Color border = Color.Lerp(
            new Color(0.62f, 0.76f, 1f, 0.36f),
            new Color(0.88f, 0.96f, 1f, 0.78f),
            hoverLerp);

        DrawSolidRect(rect, fill);
        DrawSolidRect(new Rect(rect.x, rect.y, rect.width, 1.5f), border);
        DrawSolidRect(new Rect(rect.x, rect.yMax - 1.5f, rect.width, 1.5f), new Color(border.r, border.g, border.b, border.a * 0.65f));
        DrawSolidRect(new Rect(rect.x, rect.y, 1.5f, rect.height), new Color(border.r, border.g, border.b, border.a * 0.8f));
        DrawSolidRect(new Rect(rect.xMax - 1.5f, rect.y, 1.5f, rect.height), new Color(border.r, border.g, border.b, border.a * 0.8f));
        DrawSolidRect(new Rect(rect.x + 6f, rect.y + 6f, 3f, rect.height - 12f), new Color(0.84f, 0.95f, 1f, 0.24f + hoverLerp * 0.28f));

        float shineW = Mathf.Lerp(rect.width * 0.16f, rect.width * 0.28f, hoverLerp);
        float shineX = Mathf.Repeat(t * 120f + rect.width * (primary ? 0.12f : 0.38f), rect.width + shineW) - shineW;
        Rect shineRect = new Rect(rect.x + shineX, rect.y + 1f, shineW, rect.height - 2f);
        Rect innerBounds = new Rect(rect.x + 1f, rect.y + 1f, rect.width - 2f, rect.height - 2f);
        Rect clippedShine = IntersectRect(shineRect, innerBounds);
        if (clippedShine.width > 0f && clippedShine.height > 0f)
        {
            DrawSolidRect(
                clippedShine,
                new Color(0.84f, 0.94f, 1f, (0.05f + 0.06f * pulse) * (0.45f + hoverLerp * 0.8f)));
        }

        if (hovered)
        {
            DrawSolidRect(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, 2f), new Color(0.85f, 0.96f, 1f, 0.32f));
            DrawSolidRect(new Rect(rect.x - 2f, rect.yMax, rect.width + 4f, 2f), new Color(0.85f, 0.96f, 1f, 0.24f));
            float waveX = rect.x + 14f + Mathf.Repeat(t * 46f, Mathf.Max(1f, rect.width - 40f));
            DrawSolidRect(new Rect(waveX, rect.y + rect.height * 0.5f - 1f, 18f, 2f), new Color(1f, 1f, 1f, 0.26f));
        }

        Color old = GUI.color;
        GUI.color = canInteract ? Color.white : new Color(1f, 1f, 1f, 0.45f);
        GUI.Label(rect, label, menuButtonLabelStyle);
        GUI.color = old;

        if (!canInteract)
        {
            return false;
        }

        bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
        if (clicked)
        {
            GlitchAudioManager.PlayMenuConfirm();
        }

        return clicked;
    }

    private void TrackButtonHover(string key, bool hovered)
    {
        if (Event.current == null || Event.current.type != EventType.Repaint)
        {
            return;
        }

        bool wasHovered = buttonHoverFlags.TryGetValue(key, out bool previous) && previous;
        if (hovered && !wasHovered)
        {
            GlitchAudioManager.PlayMenuHover();
        }

        buttonHoverFlags[key] = hovered;
    }

    private float GetButtonHoverState(string key, bool hovered)
    {
        float current = buttonHoverStates.TryGetValue(key, out float value) ? value : 0f;
        float target = hovered ? 1f : 0f;
        float speed = 8f * Mathf.Clamp(menuMotionIntensity, 0.4f, 2.2f);
        current = Mathf.MoveTowards(current, target, Time.unscaledDeltaTime * speed);
        buttonHoverStates[key] = current;
        return current;
    }

    private static void DrawPanelFx(Rect rect, Color accent, float baseAlpha)
    {
        float t = Time.unscaledTime;
        float pulse = 0.5f + 0.5f * Mathf.Sin(t * 2.4f);
        float alpha = Mathf.Clamp01(baseAlpha + pulse * 0.08f);
        Color glow = new Color(accent.r, accent.g, accent.b, alpha);

        DrawSolidRect(new Rect(rect.x - 1f, rect.y - 1f, rect.width + 2f, 1f), glow);
        DrawSolidRect(new Rect(rect.x - 1f, rect.yMax, rect.width + 2f, 1f), new Color(glow.r, glow.g, glow.b, glow.a * 0.85f));

        float corner = 20f;
        DrawSolidRect(new Rect(rect.x + 6f, rect.y + 6f, corner, 2f), new Color(glow.r, glow.g, glow.b, glow.a * 0.8f));
        DrawSolidRect(new Rect(rect.x + 6f, rect.y + 6f, 2f, corner), new Color(glow.r, glow.g, glow.b, glow.a * 0.8f));
        DrawSolidRect(new Rect(rect.xMax - 6f - corner, rect.y + 6f, corner, 2f), new Color(glow.r, glow.g, glow.b, glow.a * 0.8f));
        DrawSolidRect(new Rect(rect.xMax - 8f, rect.y + 6f, 2f, corner), new Color(glow.r, glow.g, glow.b, glow.a * 0.8f));
        DrawSolidRect(new Rect(rect.x + 6f, rect.yMax - 8f, corner, 2f), new Color(glow.r, glow.g, glow.b, glow.a * 0.7f));
        DrawSolidRect(new Rect(rect.x + 6f, rect.yMax - 6f - corner, 2f, corner), new Color(glow.r, glow.g, glow.b, glow.a * 0.7f));
        DrawSolidRect(new Rect(rect.xMax - 6f - corner, rect.yMax - 8f, corner, 2f), new Color(glow.r, glow.g, glow.b, glow.a * 0.7f));
        DrawSolidRect(new Rect(rect.xMax - 8f, rect.yMax - 6f - corner, 2f, corner), new Color(glow.r, glow.g, glow.b, glow.a * 0.7f));

        float sweepY = rect.y + Mathf.Repeat(t * 42f, Mathf.Max(1f, rect.height));
        DrawSolidRect(new Rect(rect.x + 2f, sweepY, rect.width - 4f, 1f), new Color(accent.r, accent.g, accent.b, 0.10f));
    }

    private static void DrawGlitchTitle(Rect rect, string text, GUIStyle style, float intensity)
    {
        float t = Time.unscaledTime;
        float split = (0.7f + 0.6f * Mathf.Sin(t * 5.4f + rect.y * 0.012f)) * Mathf.Clamp01(intensity);
        Color old = GUI.color;

        GUI.color = new Color(1f, 0.46f, 0.54f, 0.22f * intensity);
        GUI.Label(new Rect(rect.x - split, rect.y, rect.width, rect.height), text, style);
        GUI.color = new Color(0.35f, 0.95f, 1f, 0.22f * intensity);
        GUI.Label(new Rect(rect.x + split, rect.y, rect.width, rect.height), text, style);

        GUI.color = old;
        GUI.Label(rect, text, style);
    }

    private static GUIStyle BuildFittedSingleLineStyle(GUIStyle baseStyle, string text, float width, float height, int minSize)
    {
        if (baseStyle == null || string.IsNullOrEmpty(text))
        {
            return baseStyle;
        }

        GUIContent content = new GUIContent(text);
        GUIStyle style = new GUIStyle(baseStyle)
        {
            wordWrap = false,
            clipping = TextClipping.Clip
        };

        int preferred = Mathf.Max(minSize, baseStyle.fontSize);
        for (int size = preferred; size >= minSize; size--)
        {
            style.fontSize = size;
            Vector2 measured = style.CalcSize(content);
            if (measured.x <= width && measured.y <= height)
            {
                return style;
            }
        }

        style.fontSize = minSize;
        return style;
    }

    private static string FormatDuration(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        int minutes = total / 60;
        int secs = total % 60;
        if (minutes >= 60)
        {
            int hours = minutes / 60;
            minutes %= 60;
            return $"{hours}h {minutes}m";
        }

        return $"{minutes}m {secs}s";
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
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Overflow,
            wordWrap = false
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

        rankingTitleStyle = new GUIStyle(GUI.skin.label)
        {
            font = titleFont,
            fontSize = Mathf.RoundToInt(24f * uiScale),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        rankingTitleStyle.normal.textColor = new Color(0.94f, 0.97f, 1f, 0.98f);

        rankingRowStyle = new GUIStyle(GUI.skin.label)
        {
            font = uiFont,
            fontSize = Mathf.RoundToInt(15f * uiScale),
            alignment = TextAnchor.MiddleLeft
        };
        rankingRowStyle.normal.textColor = new Color(0.90f, 0.94f, 1f, 0.95f);

        rankingScoreStyle = new GUIStyle(GUI.skin.label)
        {
            font = titleFont,
            fontSize = Mathf.RoundToInt(17f * uiScale),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleRight
        };
        rankingScoreStyle.normal.textColor = new Color(1f, 0.84f, 0.56f, 0.98f);

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
            alignment = TextAnchor.MiddleCenter,
            padding = new RectOffset(12, 12, 8, 8)
        };
        buttonStyle.normal.textColor = new Color(0.90f, 0.95f, 1f, 0.96f);
        buttonStyle.hover.textColor = new Color(1f, 1f, 1f, 1f);
        buttonStyle.active.textColor = new Color(0.98f, 0.88f, 0.72f, 1f);

        menuButtonLabelStyle = new GUIStyle(GUI.skin.label)
        {
            font = titleFont,
            fontSize = Mathf.RoundToInt(16f * uiScale),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        menuButtonLabelStyle.normal.textColor = new Color(0.92f, 0.96f, 1f, 0.98f);
        menuButtonLabelStyle.hover.textColor = new Color(1f, 1f, 1f, 1f);
    }

    private void ResolveFonts()
    {
        titleFont = GlobalFontSettings.GetImportantFont();
        uiFont = GlobalFontSettings.GetSecondaryFont();
    }

    private void LoadSettings()
    {
        masterVolume = UserSettings.GetMasterVolume();
        uiScale = UserSettings.GetMenuUiScale();
        hudScale = UserSettings.GetHudScale();
        menuMotionIntensity = UserSettings.GetMenuMotion();
        fullscreen = UserSettings.GetFullscreen();
        vSyncEnabled = UserSettings.GetVSync();
        AudioListener.volume = masterVolume;
        ApplyDisplaySettings();
    }

    private void ApplyDisplaySettings()
    {
        Screen.fullScreen = fullscreen;
        QualitySettings.vSyncCount = vSyncEnabled ? 1 : 0;
        Application.targetFrameRate = vSyncEnabled ? -1 : 120;
    }

    private void ResetOptionsToDefault()
    {
        UserSettings.ResetOptions();
        LoadSettings();
        cachedUiScale = -1f;
        optionsActionMessage = "Opciones restauradas.";
        optionsActionMessageExpireAt = Time.unscaledTime + 2.2f;
        GlitchAudioManager.PlayMenuBack();
    }

    private static Rect IntersectRect(Rect a, Rect b)
    {
        float xMin = Mathf.Max(a.xMin, b.xMin);
        float yMin = Mathf.Max(a.yMin, b.yMin);
        float xMax = Mathf.Min(a.xMax, b.xMax);
        float yMax = Mathf.Min(a.yMax, b.yMax);
        if (xMax <= xMin || yMax <= yMin)
        {
            return new Rect(0f, 0f, 0f, 0f);
        }

        return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
    }

    private static void DrawSolidRect(Rect rect, Color color)
    {
        Color old = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = old;
    }
}

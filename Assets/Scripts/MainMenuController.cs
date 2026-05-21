using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
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

    [Header("Scene Fade")]
    [SerializeField] private float introBlackHoldDuration = 0.20f;
    [SerializeField] private float introFadeOutDuration = 1.30f;
    [SerializeField] private float exitFadeInDuration = 1.05f;
    [SerializeField] private Color transitionBlack = Color.black;
    [Header("Menu Visuals")]
    [SerializeField] private float menuMotionIntensity = 1f;

    private bool showOptions;
    private bool queuedGameplayLoad;
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

    private void Awake()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
        LoadSettings();
        ResolveFonts();
        NormalizeTransitionDurations();
        BeginIntroImmediateBlack();
    }

    private void Start()
    {
        // Wait one frame so the scene has fully initialized before revealing.
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
    }

    private void OnGUI()
    {
        EnsureStyles();
        DrawThematicBackground();

        if (showOptions)
        {
            DrawOptionsMenu();
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

        float buttonY = panel.y + 132f;
        Rect playRect = new Rect(panel.x + 18f, buttonY, panel.width - 36f, 44f);
        Rect optionsRect = new Rect(panel.x + 18f, buttonY + 54f, panel.width - 36f, 38f);
        Rect exitRect = new Rect(panel.x + 18f, buttonY + 102f, panel.width - 36f, 38f);

        if (DrawAnimatedMenuButton(playRect, "Jugar", true))
        {
            StartGameplay();
        }
        if (DrawAnimatedMenuButton(optionsRect, "Opciones"))
        {
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
        DrawPanel(panel, new Color(0.03f, 0.05f, 0.09f, 0.90f), new Color(0.47f, 0.56f, 0.72f, 0.50f));
        DrawPanelFx(panel, new Color(0.64f, 0.74f, 1f, 1f), 0.09f);
        Rect area = new Rect(panel.x + 14f, panel.y + 10f, panel.width - 28f, panel.height - 16f);
        GUILayout.BeginArea(area);
        GUILayout.Label("Ranking Global", rankingTitleStyle);
        GUILayout.Space(8f);

        IReadOnlyList<RankingEntry> entries = RankingStorage.GetTopEntries();
        if (entries == null || entries.Count == 0)
        {
            GUILayout.Label("Sin registros aun. Juega una ronda y deja tu marca.", paragraphStyle);
            GUILayout.EndArea();
            return;
        }

        int rows = Mathf.Min(sideBySide ? 10 : 8, entries.Count);
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

        GUILayout.EndArea();
    }

    private void GetMainMenuLayout(out Rect mainPanel, out Rect rankingPanel, out bool sideBySide)
    {
        float mainW = 390f;
        float mainH = 360f;
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
        Rect panel = CenterRect(440f, 420f);
        panel.y += bob;
        DrawPanel(panel, new Color(0.03f, 0.05f, 0.09f, 0.90f), new Color(0.47f, 0.56f, 0.72f, 0.55f));

        Rect area = new Rect(panel.x + 18f, panel.y + 16f, panel.width - 36f, panel.height - 28f);
        GUILayout.BeginArea(area);
        GUILayout.Label("Opciones", panelTitleStyle);
        GUILayout.Space(6f);
        GUILayout.Label("Ajustes genericos (placeholder)", paragraphStyle);
        GUILayout.Space(12f);

        GUILayout.Label($"Volumen Maestro: {masterVolume:F2}", paragraphStyle);
        float newMaster = GUILayout.HorizontalSlider(masterVolume, 0f, 1f);
        if (!Mathf.Approximately(newMaster, masterVolume))
        {
            masterVolume = newMaster;
            AudioListener.volume = masterVolume;
            UserSettings.SetMasterVolume(masterVolume);
        }

        GUILayout.Space(10f);
        GUILayout.Label($"Escala UI: {uiScale:F2}", paragraphStyle);
        float newUiScale = GUILayout.HorizontalSlider(uiScale, UserSettings.MinMenuUiScale, UserSettings.MaxMenuUiScale);
        if (!Mathf.Approximately(newUiScale, uiScale))
        {
            uiScale = newUiScale;
            UserSettings.SetMenuUiScale(uiScale);
        }

        GUILayout.Space(10f);
        GUILayout.Label($"Escala HUD (Nivel): {hudScale:F2}", paragraphStyle);
        float newHudScale = GUILayout.HorizontalSlider(hudScale, UserSettings.MinHudScale, UserSettings.MaxHudScale);
        if (!Mathf.Approximately(newHudScale, hudScale))
        {
            hudScale = newHudScale;
            UserSettings.SetHudScale(hudScale);
        }

        GUILayout.FlexibleSpace();
        if (DrawMenuButton("Volver", 38f))
        {
            showOptions = false;
        }

        GUILayout.EndArea();
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

    private bool DrawMenuButton(string label, float height)
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
        return clicked;
    }

    private bool DrawAnimatedMenuButton(Rect rect, string label, bool primary = false)
    {
        bool canInteract = transitionState == MenuTransitionState.Idle;
        bool hovered = rect.Contains(Event.current.mousePosition);
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
        DrawSolidRect(
            new Rect(rect.x + shineX, rect.y + 1f, shineW, rect.height - 2f),
            new Color(0.84f, 0.94f, 1f, (0.05f + 0.06f * pulse) * (0.45f + hoverLerp * 0.8f)));

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

        return GUI.Button(rect, GUIContent.none, GUIStyle.none);
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
        AudioListener.volume = masterVolume;
    }

    private static void DrawSolidRect(Rect rect, Color color)
    {
        Color old = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = old;
    }
}

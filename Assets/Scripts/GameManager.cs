using UnityEngine;

public class GameManager : MonoBehaviour
{
    private enum RunPhase
    {
        StartupDelay,
        Countdown,
        GoFlash,
        Active
    }

    [Header("Run State")]
    [SerializeField] private bool autoReloadSceneOnGameOver = false;
    [SerializeField] private float reloadDelay = 1.2f;
    [SerializeField] private float startupDelaySeconds = 1f;
    [SerializeField] private int countdownSeconds = 3;
    [SerializeField] private float goFlashSeconds = 0.4f;

    [Header("Difficulty")]
    [SerializeField] private float behaviorChangeInterval = 2.5f;
    [SerializeField] private float difficultyRampPerSecond = 0.03f;
    [SerializeField] private bool chaosTempoEnabled = true;
    [SerializeField, Range(0.45f, 1f)] private float chaosBehaviorIntervalMultiplier = 0.72f;

    [Header("Scoring")]
    [SerializeField] private float pointsPerSecond = 4f;

    [Header("Progression Gates")]
    [SerializeField] private float bossSpecialStatesUnlockTime = 30f;
    [SerializeField] private float mapEventsUnlockTime = 60f;
    [SerializeField] private float containmentPulseUnlockTime = 90f;

    [Header("Countdown Theme")]
    [SerializeField] private float countdownPulseSpeed = 2.1f;
    [SerializeField, Range(0f, 1f)] private float countdownBackdropOpacity = 0.33f;
    [SerializeField, Range(0f, 1f)] private float countdownGlitchOpacity = 0.18f;
    [SerializeField] private Color countdownPrimary = new Color(0.98f, 0.63f, 0.73f, 1f);
    [SerializeField] private Color countdownAccent = new Color(0.33f, 0.93f, 1f, 1f);

    [Header("Tempo Feedback")]
    [SerializeField] private float statePulseOverlayDuration = 0.24f;
    [SerializeField, Range(0f, 1f)] private float statePulseOverlayOpacity = 0.22f;
    [SerializeField] private Color statePulseOverlayColorA = new Color(1f, 0.35f, 0.52f, 1f);
    [SerializeField] private Color statePulseOverlayColorB = new Color(0.30f, 0.95f, 1f, 1f);

    [Header("HUD Atmosphere")]
    [SerializeField] private bool enableAmbientHudFrame = true;
    [SerializeField, Range(0.04f, 0.35f)] private float sideHudOpacity = 0.12f;
    [SerializeField, Range(0.02f, 0.22f)] private float sideHudAccentOpacity = 0.09f;
    [SerializeField, Range(0.85f, 1.55f)] private float fallbackHudScale = 1.1f;

    public bool IsGameOver { get; private set; }
    public bool IsRunActive => runPhase == RunPhase.Active && !IsGameOver;
    public float SurvivalTime { get; private set; }
    public float DifficultyMultiplier => 1f + (SurvivalTime * difficultyRampPerSecond);
    public int CurrentScore => Mathf.Max(0, Mathf.FloorToInt(SurvivalTime * Mathf.Max(0f, pointsPerSecond)) + bonusScore);
    public string CurrentLevelTypeLabel => levelType;
    public bool AreBossSpecialStatesUnlocked => IsRunActive && SurvivalTime >= Mathf.Max(0f, bossSpecialStatesUnlockTime);
    public bool AreMapEventsUnlocked => IsRunActive && SurvivalTime >= Mathf.Max(0f, mapEventsUnlockTime);
    public bool IsContainmentPulseUnlocked => IsRunActive && SurvivalTime >= Mathf.Max(0f, containmentPulseUnlockTime);

    public float CurrentBehaviorChangeInterval
    {
        get
        {
            float interval = Mathf.Max(0.5f, behaviorChangeInterval);
            if (!chaosTempoEnabled)
            {
                return interval;
            }

            return interval * Mathf.Max(0.1f, chaosBehaviorIntervalMultiplier);
        }
    }

    private float reloadTimer;
    private RunPhase runPhase;
    private float startupTimer;
    private float countdownElapsed;
    private float goFlashTimer;
    private int countdownStartValue;
    private ProceduralArenaGenerator arenaGenerator;
    private EnemyController enemyController;
    private PlayerController playerController;
    private ArenaChaosDirector chaosDirector;
    private string levelType = "Unknown";
    private GUIStyle countdownStyle;
    private GUIStyle countdownSubStyle;
    private GUIStyle hudLabelStyle;
    private GUIStyle hudValueStyle;
    private GUIStyle hudChipStyle;
    private GUIStyle bossStateStyle;
    private GUIStyle bossStateValueStyle;
    private GUIStyle eventWarningStyle;
    private Font importantFont;
    private Font secondaryFont;
    private string lastBossStateRaw;
    private float statePulseOverlayTimer;
    private int bonusScore;
    private float hudScale = 1f;
    private float cachedHudScaleForStyles = -1f;

    private void Awake()
    {
        EnsureMenuController();
    }

    private void Start()
    {
        ResolveFonts();
        RefreshHudScaleSetting();
        RefreshLevelType();
        BeginStartupSequence();
    }

    private void Update()
    {
        if (arenaGenerator == null)
        {
            RefreshLevelType();
        }
        else if (levelType != arenaGenerator.ActiveThemeLabel)
        {
            levelType = arenaGenerator.ActiveThemeLabel;
        }

        if (enemyController == null)
        {
            enemyController = FindAnyObjectByType<EnemyController>();
        }
        if (playerController == null)
        {
            playerController = FindAnyObjectByType<PlayerController>();
        }
        if (chaosDirector == null)
        {
            chaosDirector = FindAnyObjectByType<ArenaChaosDirector>();
        }

        if (enemyController != null)
        {
            TrackBossTempoPulse();
        }

        if (statePulseOverlayTimer > 0f)
        {
            statePulseOverlayTimer -= Time.deltaTime;
        }

        if (IsGameOver)
        {
            HandleOptionalReload();
            return;
        }

        if (runPhase != RunPhase.Active)
        {
            UpdateStartupSequence();
            return;
        }

        SurvivalTime += Time.deltaTime;
    }

    public void TriggerGameOver()
    {
        if (IsGameOver)
        {
            return;
        }

        IsGameOver = true;
        runPhase = RunPhase.Active;
        Debug.Log($"GAME OVER | Time Survived: {SurvivalTime:F2}s");
    }

    public void AddScore(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        bonusScore += amount;
    }

    private void HandleOptionalReload()
    {
        if (!autoReloadSceneOnGameOver)
        {
            return;
        }

        reloadTimer += Time.deltaTime;

        if (reloadTimer >= reloadDelay)
        {
            SceneTransitionController.ReloadActiveScene();
        }
    }

    private void OnGUI()
    {
        if (GameMenuController.ShouldHideGameplayHud || SceneTransitionController.IsFading)
        {
            return;
        }

        if (runPhase == RunPhase.Active)
        {
            DrawRuntimeHud();
            DrawBossStateHud();
            DrawStatePulseOverlay();
            DrawChaosWarningOverlay();
        }

        if (runPhase == RunPhase.StartupDelay || runPhase == RunPhase.Countdown || runPhase == RunPhase.GoFlash)
        {
            DrawCountdownOverlay();
        }
    }

    private void BeginStartupSequence()
    {
        runPhase = RunPhase.StartupDelay;
        startupTimer = 0f;
        countdownStartValue = Mathf.Max(1, countdownSeconds);
        countdownElapsed = 0f;
        goFlashTimer = 0f;
        SurvivalTime = 0f;
        bonusScore = 0;
        reloadTimer = 0f;
        statePulseOverlayTimer = 0f;
        lastBossStateRaw = null;
        Time.timeScale = 0f;
    }

    private void UpdateStartupSequence()
    {
        // Do not consume startup/countdown time while scene fade is covering the screen.
        // This guarantees the visible "3" lasts a full second on screen.
        if (SceneTransitionController.IsFading)
        {
            return;
        }

        float dt = Time.unscaledDeltaTime;

        if (runPhase == RunPhase.StartupDelay)
        {
            startupTimer += dt;
            if (startupTimer >= Mathf.Max(0f, startupDelaySeconds))
            {
                runPhase = RunPhase.Countdown;
                countdownElapsed = 0f;
            }
            return;
        }

        if (runPhase == RunPhase.Countdown)
        {
            countdownElapsed += dt;
            if (countdownElapsed >= countdownStartValue)
            {
                runPhase = RunPhase.GoFlash;
                goFlashTimer = 0f;
            }
            return;
        }

        if (runPhase == RunPhase.GoFlash)
        {
            goFlashTimer += dt;
            if (goFlashTimer >= Mathf.Max(0.05f, goFlashSeconds))
            {
                runPhase = RunPhase.Active;
                Time.timeScale = 1f;
            }
        }
    }

    private void DrawCountdownOverlay()
    {
        EnsureCountdownStyles();

        float t = Time.unscaledTime;
        float pulse = 0.5f + 0.5f * Mathf.Sin(t * countdownPulseSpeed);
        DrawCountdownBackdrop(t, pulse);

        string main;
        string sub;

        if (runPhase == RunPhase.StartupDelay)
        {
            main = string.Empty;
            sub = "Inicializando contencion";
        }
        else if (runPhase == RunPhase.GoFlash)
        {
            main = "GO";
            sub = "Corre";
        }
        else
        {
            int remaining = countdownStartValue - Mathf.FloorToInt(countdownElapsed);
            remaining = Mathf.Clamp(remaining, 1, countdownStartValue);
            main = remaining.ToString();
            sub = "Preparate";
        }

        float centerX = Screen.width * 0.5f;
        float centerY = Screen.height * 0.5f;
        Rect mainRect = new Rect(centerX - 140f, centerY - 90f, 280f, 96f);
        Rect subRect = new Rect(centerX - 220f, centerY + 12f, 440f, 36f);

        GUIStyle dynamicMain = new GUIStyle(countdownStyle);
        dynamicMain.fontSize = Mathf.RoundToInt(Mathf.Lerp(66f, 78f, pulse));
        dynamicMain.normal.textColor = Color.Lerp(
            new Color(countdownPrimary.r, countdownPrimary.g, countdownPrimary.b, 0.92f),
            new Color(countdownAccent.r, countdownAccent.g, countdownAccent.b, 0.98f),
            pulse * 0.26f);

        DrawSplitLabel(mainRect, main, dynamicMain, 0.85f + pulse * 0.55f);
        GUI.Label(subRect, sub, countdownSubStyle);
    }

    private void EnsureCountdownStyles()
    {
        if (countdownStyle != null)
        {
            return;
        }

        countdownStyle = new GUIStyle(GUI.skin.label)
        {
            font = importantFont,
            fontSize = 72,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        countdownStyle.normal.textColor = new Color(0.96f, 0.90f, 0.95f, 0.96f);

        countdownSubStyle = new GUIStyle(GUI.skin.label)
        {
            font = secondaryFont,
            fontSize = 24,
            fontStyle = FontStyle.Italic,
            alignment = TextAnchor.MiddleCenter
        };
        countdownSubStyle.normal.textColor = new Color(0.86f, 0.78f, 0.90f, 0.92f);
    }

    private void DrawCountdownBackdrop(float time, float pulse)
    {
        float alpha = Mathf.Clamp01(countdownBackdropOpacity);
        DrawSolidRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.04f, 0.03f, 0.06f, alpha));

        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        Color ring = new Color(countdownPrimary.r, countdownPrimary.g, countdownPrimary.b, 0.16f + pulse * 0.08f);
        DrawEllipseRing(center, Screen.width * 0.12f, Screen.height * 0.14f, ring);
        DrawEllipseRing(center, Screen.width * 0.21f, Screen.height * 0.24f, ring);

        float barStep = Mathf.Max(30f, Screen.height / 16f);
        float glitchA = Mathf.Clamp01(countdownGlitchOpacity) * (0.55f + pulse * 0.45f);
        for (float y = 0f; y <= Screen.height + barStep; y += barStep)
        {
            float width = Screen.width * Mathf.Lerp(0.28f, 0.76f, Mathf.PerlinNoise(y * 0.021f, time * 0.55f));
            float xOffset = (Mathf.PerlinNoise(y * 0.017f, time * 1.8f) - 0.5f) * 44f;
            float x = (Screen.width - width) * 0.5f + xOffset;
            DrawSolidRect(new Rect(x, y, width, 2f), new Color(countdownPrimary.r, 0.32f, 0.45f, glitchA));
        }
    }

    private static void DrawSplitLabel(Rect rect, string text, GUIStyle style, float split)
    {
        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        Color old = GUI.color;
        GUI.color = new Color(1f, 0.35f, 0.45f, 0.28f);
        GUI.Label(new Rect(rect.x - split, rect.y, rect.width, rect.height), text, style);

        GUI.color = new Color(0.28f, 0.95f, 1f, 0.24f);
        GUI.Label(new Rect(rect.x + split, rect.y, rect.width, rect.height), text, style);

        GUI.color = old;
        GUI.Label(rect, text, style);
    }

    private static void DrawEllipseRing(Vector2 center, float radiusX, float radiusY, Color color)
    {
        const int segments = 96;
        for (int i = 0; i < segments; i += 2)
        {
            float t = i / (float)segments * Mathf.PI * 2f;
            float x = center.x + Mathf.Cos(t) * radiusX;
            float y = center.y + Mathf.Sin(t) * radiusY;
            DrawSolidRect(new Rect(x, y, 3f, 3f), color);
        }
    }

    private static void DrawSolidRect(Rect rect, Color color)
    {
        Color old = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = old;
    }

    private void RefreshLevelType()
    {
        arenaGenerator = FindAnyObjectByType<ProceduralArenaGenerator>();
        enemyController = FindAnyObjectByType<EnemyController>();
        playerController = FindAnyObjectByType<PlayerController>();
        chaosDirector = FindAnyObjectByType<ArenaChaosDirector>();
        if (arenaGenerator != null)
        {
            levelType = arenaGenerator.ActiveThemeLabel;
        }
    }

    private string GetMapEventLabel()
    {
        if (chaosDirector == null)
        {
            return "Nominal";
        }

        string warning = chaosDirector.ActiveWarningLabel;
        if (!string.IsNullOrWhiteSpace(warning))
        {
            return warning;
        }

        string label = chaosDirector.ActiveEventLabel;
        if (string.IsNullOrWhiteSpace(label))
        {
            return "Nominal";
        }

        return label;
    }

    private void DrawChaosWarningOverlay()
    {
        if (chaosDirector == null)
        {
            return;
        }

        string warning = chaosDirector.ActiveWarningLabel;
        if (string.IsNullOrWhiteSpace(warning))
        {
            return;
        }

        EnsureWarningStyle();
        float t = chaosDirector.ActiveWarningNormalized;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7.5f);
        float alpha = Mathf.Lerp(0.9f, 0.45f, t) + pulse * 0.08f;

        Color old = GUI.color;
        GUI.color = new Color(1f, 0.56f, 0.66f, Mathf.Clamp01(alpha));

        float w = 720f;
        float h = 54f;
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height * 0.2f;
        GUI.Label(new Rect(x, y, w, h), warning.ToUpperInvariant(), eventWarningStyle);

        GUI.color = old;
    }

    private void TrackBossTempoPulse()
    {
        if (enemyController == null)
        {
            return;
        }

        string stateRaw = enemyController.CurrentStateLabel;
        if (!string.Equals(lastBossStateRaw, stateRaw))
        {
            statePulseOverlayTimer = Mathf.Max(statePulseOverlayTimer, Mathf.Max(0.05f, statePulseOverlayDuration));
            lastBossStateRaw = stateRaw;
        }
    }

    private void DrawStatePulseOverlay()
    {
        if (statePulseOverlayTimer <= 0f)
        {
            return;
        }

        float normalized = Mathf.Clamp01(statePulseOverlayTimer / Mathf.Max(0.05f, statePulseOverlayDuration));
        float pulse = Mathf.Sin((1f - normalized) * Mathf.PI);
        float alpha = Mathf.Clamp01(statePulseOverlayOpacity) * pulse;

        Color tintA = statePulseOverlayColorA;
        tintA.a = alpha;
        Color tintB = statePulseOverlayColorB;
        tintB.a = alpha * 0.85f;

        float stripeHeight = Mathf.Lerp(4f, 10f, pulse);
        DrawSolidRect(new Rect(0f, 0f, Screen.width, stripeHeight), tintA);
        DrawSolidRect(new Rect(0f, Screen.height - stripeHeight, Screen.width, stripeHeight), tintB);

        float midY = Screen.height * 0.5f;
        float midWidth = Screen.width * Mathf.Lerp(0.2f, 0.65f, pulse);
        float midX = (Screen.width - midWidth) * 0.5f;
        DrawSolidRect(new Rect(midX, midY - 1f, midWidth, 2f), new Color(tintA.r, tintA.g, tintA.b, alpha * 0.8f));
    }

    private void DrawBossStateHud()
    {
        EnsureBossStateStyles();

        string stateValue = "Unknown";
        if (enemyController != null)
        {
            stateValue = ToBossStateLabel(enemyController.CurrentStateLabel);
        }

        float s = hudScale;
        float width = 460f * s;
        float lineHeight = 22f * s;
        float x = (Screen.width - width) * 0.5f;
        float y = 10f * s;
        Rect panel = new Rect(x - (12f * s), y + (2f * s), width + (24f * s), 52f * s);
        DrawSolidRect(panel, new Color(0.05f, 0.04f, 0.08f, 0.62f));
        DrawSolidRect(new Rect(panel.x, panel.y, panel.width, 1f), new Color(0.93f, 0.76f, 0.88f, 0.44f));
        DrawSolidRect(new Rect(panel.x, panel.yMax - 1f, panel.width, 1f), new Color(0.45f, 0.62f, 0.92f, 0.30f));

        GUI.Label(new Rect(x, y, width, lineHeight), "Anomaly State", bossStateStyle);
        GUI.Label(new Rect(x, y + (22f * s), width, lineHeight), stateValue, bossStateValueStyle);
    }

    private static string ToBossStateLabel(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "Unknown";
        }

        switch (raw)
        {
            case "DirectChase":
                return "Direct Chase";
            case "PredictiveIntercept":
                return "Predictive Intercept";
            case "CutoffFlank":
                return "Cutoff Flank";
            case "ErraticBurst":
                return "Erratic Burst";
            case "Split":
                return "Split";
            case "ExpansionShoot":
                return "Expansion Shoot";
            case "SpeedSurge":
                return "Speed Surge";
            case "WeaveHunter":
                return "Weave Hunter";
            case "Destroyer":
                return "Destroyer";
            default:
                return raw;
        }
    }

    private void EnsureBossStateStyles()
    {
        if (bossStateStyle != null &&
            bossStateValueStyle != null &&
            hudLabelStyle != null &&
            hudValueStyle != null &&
            hudChipStyle != null &&
            Mathf.Abs(cachedHudScaleForStyles - hudScale) < 0.001f)
        {
            return;
        }

        cachedHudScaleForStyles = hudScale;
        int labelSize = Mathf.RoundToInt(14f * hudScale);
        int valueSize = Mathf.RoundToInt(28f * hudScale);
        int chipSize = Mathf.RoundToInt(14f * hudScale);
        int bossLabelSize = Mathf.RoundToInt(15f * hudScale);
        int bossValueSize = Mathf.RoundToInt(18f * hudScale);

        hudLabelStyle = new GUIStyle(GUI.skin.label)
        {
            font = secondaryFont,
            fontSize = labelSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft,
            clipping = TextClipping.Overflow,
            wordWrap = false
        };
        hudLabelStyle.normal.textColor = new Color(0.82f, 0.90f, 1f, 0.86f);

        hudValueStyle = new GUIStyle(GUI.skin.label)
        {
            font = importantFont,
            fontSize = valueSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft,
            clipping = TextClipping.Overflow,
            wordWrap = false
        };
        hudValueStyle.normal.textColor = new Color(0.96f, 0.98f, 1f, 0.98f);

        hudChipStyle = new GUIStyle(GUI.skin.label)
        {
            font = secondaryFont,
            fontSize = chipSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Clip,
            wordWrap = false
        };
        hudChipStyle.normal.textColor = new Color(0.90f, 0.95f, 1f, 0.96f);

        bossStateStyle = new GUIStyle(GUI.skin.label)
        {
            font = importantFont,
            fontSize = bossLabelSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Overflow,
            wordWrap = false
        };
        bossStateStyle.normal.textColor = new Color(0.92f, 0.87f, 0.95f, 0.90f);

        bossStateValueStyle = new GUIStyle(GUI.skin.label)
        {
            font = importantFont,
            fontSize = bossValueSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Overflow,
            wordWrap = false
        };
        bossStateValueStyle.normal.textColor = new Color(1f, 0.76f, 0.82f, 0.98f);
    }

    private void DrawRuntimeHud()
    {
        EnsureBossStateStyles();
        DrawHudAmbientFrame();

        float s = hudScale;
        Rect leftPanel = new Rect(12f * s, 10f * s, 320f * s, 114f * s);
        DrawSolidRect(leftPanel, new Color(0.03f, 0.05f, 0.10f, 0.58f));
        DrawSolidRect(new Rect(leftPanel.x, leftPanel.y, leftPanel.width, 2f), new Color(0.38f, 0.58f, 0.84f, 0.6f));
        DrawSolidRect(new Rect(leftPanel.x, leftPanel.yMax - 2f, leftPanel.width, 2f), new Color(0.18f, 0.32f, 0.50f, 0.42f));

        float pad = 14f * s;
        float headerY = leftPanel.y + (10f * s);
        float valueY = leftPanel.y + (36f * s);
        float colGap = 18f * s;
        float colW = (leftPanel.width - (pad * 2f) - colGap) * 0.5f;
        float valueH = 40f * s;
        float labelH = 22f * s;

        GUI.Label(new Rect(leftPanel.x + pad, headerY, colW, labelH), "TIME", hudLabelStyle);
        GUI.Label(new Rect(leftPanel.x + pad, valueY, colW, valueH), $"{SurvivalTime:F1}s", hudValueStyle);

        float rightColX = leftPanel.x + pad + colW + colGap;
        GUI.Label(new Rect(rightColX, headerY, colW, labelH), "POINTS", hudLabelStyle);
        GUI.Label(new Rect(rightColX, valueY, colW, valueH), CurrentScore.ToString(), hudValueStyle);

        float chipH = 34f * s;
        float rightMargin = 12f * s;
        string levelText = $"Level: {levelType}";
        float levelW = CalcChipWidth(levelText, 190f * s, 420f * s, 22f * s);
        float rightX = Screen.width - levelW - rightMargin;
        DrawHudChip(new Rect(rightX, 10f * s, levelW, chipH), levelText, new Color(0.14f, 0.21f, 0.33f, 0.72f));

        string eventLabel = GetMapEventLabel();
        if (!string.Equals(eventLabel, "Nominal"))
        {
            float eventW = CalcChipWidth(eventLabel, 220f * s, 520f * s, 22f * s);
            float eventX = Screen.width - eventW - rightMargin;
            DrawHudChip(new Rect(eventX, (10f * s) + chipH + (6f * s), eventW, chipH), eventLabel, new Color(0.31f, 0.17f, 0.22f, 0.74f));
        }
    }

    private float CalcChipWidth(string text, float minWidth, float maxWidth, float horizontalPadding)
    {
        if (string.IsNullOrWhiteSpace(text) || hudChipStyle == null)
        {
            return minWidth;
        }

        float content = hudChipStyle.CalcSize(new GUIContent(text)).x + Mathf.Max(8f, horizontalPadding * 2f);
        return Mathf.Clamp(content, minWidth, maxWidth);
    }

    private void DrawHudAmbientFrame()
    {
        if (!enableAmbientHudFrame)
        {
            return;
        }

        Color baseTint;
        Color accentTint;
        GetHudThemeColors(out baseTint, out accentTint);

        float t = Time.unscaledTime;
        float sideWidth = Mathf.Clamp(Screen.width * 0.052f, 36f, 86f);
        Rect left = new Rect(0f, 0f, sideWidth, Screen.height);
        Rect right = new Rect(Screen.width - sideWidth, 0f, sideWidth, Screen.height);
        DrawSolidRect(left, new Color(baseTint.r, baseTint.g, baseTint.b, sideHudOpacity));
        DrawSolidRect(right, new Color(baseTint.r, baseTint.g, baseTint.b, sideHudOpacity));

        float innerGlowWidth = Mathf.Max(2f, sideWidth * 0.12f);
        DrawSolidRect(
            new Rect(sideWidth - innerGlowWidth, 0f, innerGlowWidth, Screen.height),
            new Color(accentTint.r, accentTint.g, accentTint.b, sideHudAccentOpacity));
        DrawSolidRect(
            new Rect(Screen.width - sideWidth, 0f, innerGlowWidth, Screen.height),
            new Color(accentTint.r, accentTint.g, accentTint.b, sideHudAccentOpacity));

        float step = Mathf.Max(34f, Screen.height / 17f);
        for (float y = -step; y <= Screen.height + step; y += step)
        {
            float wobble = Mathf.Sin((y * 0.03f) + (t * 2.4f)) * 4f;
            float yAnim = y + Mathf.Sin((t * 1.8f) + y * 0.02f) * 6f;
            float lineWidth = Mathf.Lerp(8f, sideWidth * 0.55f, Mathf.PerlinNoise(y * 0.013f, t * 0.45f));
            float lineAlpha = 0.08f + 0.09f * (0.5f + 0.5f * Mathf.Sin((t * 4.2f) + y * 0.06f));

            DrawSolidRect(
                new Rect(7f + wobble, yAnim, lineWidth, 2f),
                new Color(accentTint.r, accentTint.g, accentTint.b, lineAlpha));
            DrawSolidRect(
                new Rect(Screen.width - 7f - lineWidth - wobble, yAnim, lineWidth, 2f),
                new Color(accentTint.r, accentTint.g, accentTint.b, lineAlpha));
        }

        float sweepHeight = Mathf.Lerp(Screen.height * 0.12f, Screen.height * 0.18f, 0.5f + 0.5f * Mathf.Sin(t * 0.7f));
        float sweepY = Mathf.Repeat(t * (Screen.height * 0.24f), Screen.height + sweepHeight) - sweepHeight;
        DrawSolidRect(
            new Rect(0f, sweepY, sideWidth, sweepHeight),
            new Color(accentTint.r, accentTint.g, accentTint.b, sideHudAccentOpacity * 0.56f));
        DrawSolidRect(
            new Rect(Screen.width - sideWidth, Screen.height - sweepY - sweepHeight, sideWidth, sweepHeight),
            new Color(accentTint.r, accentTint.g, accentTint.b, sideHudAccentOpacity * 0.56f));

        float bracketW = 28f;
        float bracketH = 3f;
        float inset = sideWidth + 6f;
        float topY = 8f;
        float bottomY = Screen.height - 12f;
        Color bracket = new Color(accentTint.r, accentTint.g, accentTint.b, 0.26f);
        DrawSolidRect(new Rect(inset, topY, bracketW, bracketH), bracket);
        DrawSolidRect(new Rect(Screen.width - inset - bracketW, topY, bracketW, bracketH), bracket);
        DrawSolidRect(new Rect(inset, bottomY, bracketW, bracketH), bracket);
        DrawSolidRect(new Rect(Screen.width - inset - bracketW, bottomY, bracketW, bracketH), bracket);
    }

    private void GetHudThemeColors(out Color baseTint, out Color accentTint)
    {
        switch (levelType)
        {
            case "Lab":
                baseTint = new Color(0.08f, 0.13f, 0.22f, 1f);
                accentTint = new Color(0.42f, 0.73f, 1f, 1f);
                break;
            case "Storage":
                baseTint = new Color(0.17f, 0.12f, 0.07f, 1f);
                accentTint = new Color(0.98f, 0.71f, 0.34f, 1f);
                break;
            case "Rupture":
                baseTint = new Color(0.16f, 0.08f, 0.18f, 1f);
                accentTint = new Color(1f, 0.43f, 0.75f, 1f);
                break;
            default:
                baseTint = new Color(0.10f, 0.12f, 0.18f, 1f);
                accentTint = new Color(0.68f, 0.78f, 0.96f, 1f);
                break;
        }
    }

    private void DrawHudChip(Rect rect, string text, Color fill)
    {
        DrawSolidRect(rect, fill);
        DrawSolidRect(new Rect(rect.x, rect.y, rect.width, 1f), new Color(0.85f, 0.92f, 1f, 0.35f));
        DrawSolidRect(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), new Color(0.85f, 0.92f, 1f, 0.25f));
        GUI.Label(rect, text, hudChipStyle);
    }

    private void EnsureWarningStyle()
    {
        if (eventWarningStyle != null)
        {
            return;
        }

        eventWarningStyle = new GUIStyle(GUI.skin.label)
        {
            font = importantFont,
            fontSize = 30,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        eventWarningStyle.normal.textColor = new Color(1f, 0.6f, 0.72f, 0.95f);
    }

    private void EnsureMenuController()
    {
        if (FindAnyObjectByType<GameMenuController>() != null)
        {
            return;
        }

        GameObject go = new GameObject("GameMenuController");
        go.AddComponent<GameMenuController>();
    }

    private void ResolveFonts()
    {
        importantFont = GlobalFontSettings.GetImportantFont();
        secondaryFont = GlobalFontSettings.GetSecondaryFont();
    }

    private void RefreshHudScaleSetting()
    {
        float loaded = UserSettings.GetHudScale();
        if (loaded <= 0f)
        {
            loaded = fallbackHudScale;
        }

        hudScale = Mathf.Clamp(loaded, 0.85f, 1.55f);
    }
}

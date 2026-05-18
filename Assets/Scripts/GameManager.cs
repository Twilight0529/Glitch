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

    public bool IsGameOver { get; private set; }
    public bool IsRunActive => runPhase == RunPhase.Active && !IsGameOver;
    public float SurvivalTime { get; private set; }
    public float DifficultyMultiplier => 1f + (SurvivalTime * difficultyRampPerSecond);
    public string CurrentLevelTypeLabel => levelType;

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
    private GUIStyle bossStateStyle;
    private GUIStyle bossStateValueStyle;
    private GUIStyle bossPacingValueStyle;
    private GUIStyle eventWarningStyle;
    private string lastBossStateRaw;
    private string lastPacingPhaseRaw;
    private float statePulseOverlayTimer;

    private void Awake()
    {
        EnsureMenuController();
    }

    private void Start()
    {
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

        const int margin = 12;

        GUI.Label(new Rect(margin, margin, 350f, 25f), $"Survival Time: {SurvivalTime:F1}s");
        GUI.Label(new Rect(margin, margin + 22f, 350f, 25f), $"Threat Level: x{DifficultyMultiplier:F2}");
        GUI.Label(new Rect(margin, margin + 44f, 350f, 25f), $"Pattern Shift: {CurrentBehaviorChangeInterval:F1}s");
        GUI.Label(new Rect(margin, margin + 66f, 350f, 25f), $"Level Type: {levelType}");
        GUI.Label(new Rect(margin, margin + 88f, 380f, 25f), $"Player Effect: {GetPlayerEffectLabel()}");
        GUI.Label(new Rect(margin, margin + 110f, 480f, 25f), $"Map Event: {GetMapEventLabel()}");
        DrawBossStateHud();
        DrawStatePulseOverlay();
        DrawChaosWarningOverlay();

        if (IsGameOver)
        {
            GUI.Label(new Rect(margin, margin + 132f, 500f, 25f), "GAME OVER - The anomaly reached you.");
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
        reloadTimer = 0f;
        statePulseOverlayTimer = 0f;
        lastBossStateRaw = null;
        lastPacingPhaseRaw = null;
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
            fontSize = 72,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        countdownStyle.normal.textColor = new Color(0.96f, 0.90f, 0.95f, 0.96f);

        countdownSubStyle = new GUIStyle(GUI.skin.label)
        {
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

    private string GetPlayerEffectLabel()
    {
        if (playerController == null)
        {
            return "None";
        }

        return playerController.ActivePowerupLabel;
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
        string phaseRaw = enemyController.CurrentPacingPhaseLabel;

        if (!string.Equals(lastBossStateRaw, stateRaw) || !string.Equals(lastPacingPhaseRaw, phaseRaw))
        {
            statePulseOverlayTimer = Mathf.Max(statePulseOverlayTimer, Mathf.Max(0.05f, statePulseOverlayDuration));
            lastBossStateRaw = stateRaw;
            lastPacingPhaseRaw = phaseRaw;
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
        string pacingValue = "Unknown";
        if (enemyController != null)
        {
            stateValue = ToBossStateLabel(enemyController.CurrentStateLabel);
            pacingValue = ToPacingPhaseLabel(enemyController.CurrentPacingPhaseLabel);
        }

        const float width = 440f;
        const float lineHeight = 22f;
        float x = (Screen.width - width) * 0.5f;
        float y = 10f;

        GUI.Label(new Rect(x, y, width, lineHeight), "Anomaly State", bossStateStyle);
        GUI.Label(new Rect(x, y + 18f, width, lineHeight), stateValue, bossStateValueStyle);
        GUI.Label(new Rect(x, y + 38f, width, lineHeight), $"Pacing: {pacingValue}", bossPacingValueStyle);
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

    private static string ToPacingPhaseLabel(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "Unknown";
        }

        switch (raw)
        {
            case "BuildUp":
                return "Build Up";
            case "SustainPeak":
                return "Sustain Peak";
            case "PeakFade":
                return "Peak Fade";
            case "Relax":
                return "Relax";
            default:
                return raw;
        }
    }

    private void EnsureBossStateStyles()
    {
        if (bossStateStyle != null && bossStateValueStyle != null && bossPacingValueStyle != null)
        {
            return;
        }

        bossStateStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        bossStateStyle.normal.textColor = new Color(0.92f, 0.87f, 0.95f, 0.90f);

        bossStateValueStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        bossStateValueStyle.normal.textColor = new Color(1f, 0.76f, 0.82f, 0.98f);

        bossPacingValueStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        bossPacingValueStyle.normal.textColor = new Color(0.78f, 0.90f, 1f, 0.95f);
    }

    private void EnsureWarningStyle()
    {
        if (eventWarningStyle != null)
        {
            return;
        }

        eventWarningStyle = new GUIStyle(GUI.skin.label)
        {
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
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    // Estado central de la partida: cuenta regresiva, tiempo, puntaje, interfaz y derrota.
    private struct ScorePopup
    {
        public int amount;
        public float age;
        public float lifetime;
        public float xJitter;
        public Color color;
    }

    private enum RunPhase
    {
        StartupDelay,
        Countdown,
        GoFlash,
        Active
    }

    private enum PlayerUpgradeKind
    {
        MoveSpeed,
        ParryWindow,
        ParryCooldown,
        ParryRadius,
        ShieldDuration,
        FirewallChargeGain,
        FirewallBurstRadius,
        FirewallBurstStun
    }

    private struct UpgradeChoice
    {
        public PlayerUpgradeKind kind;
        public string title;
        public string description;
        public string category;
        public string rarity;
        public Color accent;
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

    [Header("Run Upgrades")]
    [SerializeField] private bool enableRunUpgrades = true;
    [SerializeField] private float firstUpgradeTime = 35f;
    [SerializeField] private float upgradeInterval = 42f;
    [SerializeField] private int upgradeOptionsShown = 3;
    [SerializeField] private int upgradeScoreBonus = 8;
    [SerializeField] private float upgradeChoiceLimitSeconds = 10f;
    [SerializeField] private float upgradeEnterDuration = 0.32f;
    [SerializeField] private float upgradeExitDuration = 0.46f;

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
    [SerializeField] private int countdownOrbitTickCount = 18;
    [SerializeField] private float countdownOrbitSpinSpeed = 0.95f;
    [SerializeField, Range(0f, 1f)] private float countdownImpactFlashOpacity = 0.28f;
    [SerializeField] private float countdownImpactSweepSpeed = 640f;

    [Header("Tempo Feedback")]
    [SerializeField] private float statePulseOverlayDuration = 0.24f;
    [SerializeField, Range(0f, 1f)] private float statePulseOverlayOpacity = 0.22f;
    [SerializeField] private Color statePulseOverlayColorA = new Color(1f, 0.35f, 0.52f, 1f);
    [SerializeField] private Color statePulseOverlayColorB = new Color(0.30f, 0.95f, 1f, 1f);
    [SerializeField] private float bossStateBannerDuration = 1.25f;

    [Header("HUD Atmosphere")]
    [SerializeField] private bool enableAmbientHudFrame = true;
    [SerializeField, Range(0.04f, 0.35f)] private float sideHudOpacity = 0.12f;
    [SerializeField, Range(0.02f, 0.22f)] private float sideHudAccentOpacity = 0.09f;
    [SerializeField, Range(0.85f, 1.55f)] private float fallbackHudScale = 1.1f;

    [Header("HUD Reactive FX")]
    [SerializeField] private bool enableReactiveHudFx = true;
    [SerializeField] private float threatDangerDistance = 2.2f;
    [SerializeField] private float threatSafeDistance = 10.5f;
    [SerializeField] private float threatSmoothing = 5f;
    [SerializeField] private float scoreSmoothing = 10f;
    [SerializeField] private float scorePopupLifetime = 0.95f;

    public bool IsGameOver { get; private set; }
    public bool IsRunActive => runPhase == RunPhase.Active && !IsGameOver && !playerDefeatSequenceRunning;
    public bool IsUpgradeSelectionOpen => upgradeSelectionOpen;
    public float SurvivalTime { get; private set; }
    public float DifficultyMultiplier => 1f + (SurvivalTime * difficultyRampPerSecond);
    public int CurrentScore => Mathf.Max(0, Mathf.FloorToInt(SurvivalTime * Mathf.Max(0f, pointsPerSecond)) + bonusScore);
    public string CurrentLevelTypeLabel => levelType;
    public bool IsBreachSensitiveSuppressionActive => breachSensitiveSuppressionTimer > 0f;
    public bool AreBossSpecialStatesUnlocked => IsRunActive && !IsBreachSensitiveSuppressionActive && SurvivalTime >= Mathf.Max(0f, bossSpecialStatesUnlockTime);
    public bool AreMapEventsUnlocked => IsRunActive && SurvivalTime >= Mathf.Max(0f, mapEventsUnlockTime);
    public bool IsContainmentPulseUnlocked => IsRunActive && !IsBreachSensitiveSuppressionActive && SurvivalTime >= Mathf.Max(0f, containmentPulseUnlockTime);

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
    private int lastCountdownCueValue;
    private bool countdownGoCuePlayed;
    private ProceduralArenaGenerator arenaGenerator;
    private EnemyController enemyController;
    private PlayerController playerController;
    private ArenaChaosDirector chaosDirector;
    private IThemedEventStatusProvider themedEventStatusProvider;
    private string levelType = "Unknown";
    private GUIStyle countdownStyle;
    private GUIStyle countdownSubStyle;
    private GUIStyle hudLabelStyle;
    private GUIStyle hudValueStyle;
    private GUIStyle hudChipStyle;
    private GUIStyle bossStateStyle;
    private GUIStyle bossStateValueStyle;
    private GUIStyle bossStateBannerLabelStyle;
    private GUIStyle bossStateBannerValueStyle;
    private GUIStyle eventWarningStyle;
    private GUIStyle upgradeKickerStyle;
    private GUIStyle upgradeTitleStyle;
    private GUIStyle upgradeDescriptionStyle;
    private GUIStyle upgradeButtonStyle;
    private GUIStyle upgradeTimerStyle;
    private Font importantFont;
    private Font secondaryFont;
    private string lastBossStateRaw;
    private float statePulseOverlayTimer;
    private float bossStateBannerTimer;
    private string bossStateBannerRaw;
    private int bonusScore;
    private float hudScale = 1f;
    private float cachedHudScaleForStyles = -1f;
    private float cachedUpgradeHudScaleForStyles = -1f;
    private float displayedScore;
    private float scorePulseTimer;
    private float smoothedThreat;
    private readonly List<ScorePopup> scorePopups = new List<ScorePopup>();
    private readonly List<UpgradeChoice> currentUpgradeChoices = new List<UpgradeChoice>();
    private float nextUpgradeTime;
    private bool upgradeSelectionOpen;
    private bool upgradeSelectionClosing;
    private float upgradeSelectionAge;
    private float upgradeTimeRemaining;
    private float upgradeExitTimer;
    private int upgradeSelectedIndex = -1;
    private Color upgradeSelectedAccent = Color.white;
    private int upgradePickCount;
    private bool playerDefeatSequenceRunning;
    private Coroutine playerDefeatSequenceRoutine;
    private float breachSensitiveSuppressionTimer;

    private void Awake()
    {
        EnsureMenuController();
    }

    private void Start()
    {
        GlitchAudioManager.Ensure();
        GlitchAudioManager.EnterGameplay();
        ResolveFonts();
        RefreshHudScaleSetting();
        RefreshLevelType();
        BeginStartupSequence();
    }

    private void Update()
    {
        UpdateHudReactiveState();

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
        if (!IsThemedEventStatusProviderValid())
        {
            themedEventStatusProvider = FindThemedEventStatusProvider();
        }

        if (enemyController != null)
        {
            TrackBossTempoPulse();
        }

        if (statePulseOverlayTimer > 0f)
        {
            statePulseOverlayTimer -= Time.deltaTime;
        }
        if (bossStateBannerTimer > 0f)
        {
            bossStateBannerTimer -= Time.deltaTime;
        }
        if (breachSensitiveSuppressionTimer > 0f)
        {
            breachSensitiveSuppressionTimer -= Time.deltaTime;
        }

        if (upgradeSelectionOpen)
        {
            UpdateUpgradeSelectionState();
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

        if (playerDefeatSequenceRunning)
        {
            return;
        }

        if (upgradeSelectionOpen)
        {
            return;
        }

        SurvivalTime += Time.deltaTime;
        if (ShouldOpenUpgradeSelection())
        {
            OpenUpgradeSelection();
        }
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

    public void RequestPlayerDefeat(PlayerController defeatedPlayer)
    {
        if (IsGameOver || playerDefeatSequenceRunning)
        {
            return;
        }

        if (playerDefeatSequenceRoutine != null)
        {
            StopCoroutine(playerDefeatSequenceRoutine);
            playerDefeatSequenceRoutine = null;
        }

        playerDefeatSequenceRoutine = StartCoroutine(PlayerDefeatSequenceRoutine(defeatedPlayer));
    }

    public void AddScore(int amount)
    {
        if (amount <= 0)
        {
            return;
        }

        bonusScore += amount;
        scorePulseTimer = Mathf.Max(scorePulseTimer, 0.34f);
        if (scorePopups.Count < 10)
        {
            ScorePopup popup = new ScorePopup
            {
                amount = amount,
                age = 0f,
                lifetime = Mathf.Max(0.4f, scorePopupLifetime),
                xJitter = Random.Range(-42f, 42f),
                color = Random.value < 0.22f
                    ? new Color(1f, 0.83f, 0.45f, 1f)
                    : new Color(0.70f, 0.95f, 1f, 1f)
            };
            scorePopups.Add(popup);
        }
    }

    public void SuppressBreachSensitiveSystems(float seconds)
    {
        breachSensitiveSuppressionTimer = Mathf.Max(breachSensitiveSuppressionTimer, Mathf.Max(0f, seconds));
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
            DrawBossStateBanner();
            DrawChaosWarningOverlay();
            DrawUpgradeSelectionOverlay();
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
        lastCountdownCueValue = -1;
        countdownGoCuePlayed = false;
        goFlashTimer = 0f;
        SurvivalTime = 0f;
        bonusScore = 0;
        reloadTimer = 0f;
        statePulseOverlayTimer = 0f;
        bossStateBannerTimer = 0f;
        bossStateBannerRaw = null;
        lastBossStateRaw = null;
        scorePopups.Clear();
        currentUpgradeChoices.Clear();
        displayedScore = 0f;
        smoothedThreat = 0f;
        scorePulseTimer = 0f;
        nextUpgradeTime = Mathf.Max(1f, firstUpgradeTime);
        upgradeSelectionOpen = false;
        upgradeSelectionClosing = false;
        upgradeSelectionAge = 0f;
        upgradeTimeRemaining = 0f;
        upgradeExitTimer = 0f;
        upgradeSelectedIndex = -1;
        upgradePickCount = 0;
        playerDefeatSequenceRunning = false;
        playerDefeatSequenceRoutine = null;
        breachSensitiveSuppressionTimer = 0f;
        Time.timeScale = 0f;
    }

    private IEnumerator PlayerDefeatSequenceRoutine(PlayerController defeatedPlayer)
    {
        playerDefeatSequenceRunning = true;

        float waitSeconds = 0.55f;
        if (defeatedPlayer != null)
        {
            bool started = defeatedPlayer.StartDeathExplosion();
            if (started)
            {
                waitSeconds = Mathf.Max(0.08f, defeatedPlayer.DeathExplosionDuration);
            }
        }

        float timer = 0f;
        while (timer < waitSeconds && !IsGameOver)
        {
            timer += Time.unscaledDeltaTime;
            yield return null;
        }

        playerDefeatSequenceRunning = false;
        playerDefeatSequenceRoutine = null;

        if (!IsGameOver)
        {
            TriggerGameOver();
        }
    }

    private void UpdateHudReactiveState()
    {
        float dt = Time.unscaledDeltaTime;

        if (scorePulseTimer > 0f)
        {
            scorePulseTimer -= dt;
        }

        float targetScore = CurrentScore;
        float scoreLerp = 1f - Mathf.Exp(-Mathf.Max(1f, scoreSmoothing) * dt);
        displayedScore = Mathf.Lerp(displayedScore, targetScore, scoreLerp);
        if (Mathf.Abs(displayedScore - targetScore) < 0.02f)
        {
            displayedScore = targetScore;
        }

        for (int i = scorePopups.Count - 1; i >= 0; i--)
        {
            ScorePopup popup = scorePopups[i];
            popup.age += dt;
            if (popup.age >= popup.lifetime)
            {
                scorePopups.RemoveAt(i);
                continue;
            }

            scorePopups[i] = popup;
        }

        float targetThreat = 0f;
        if (enableReactiveHudFx && playerController != null && enemyController != null)
        {
            float distance = Vector2.Distance(playerController.GetPosition(), enemyController.GetCurrentPosition());
            float safe = Mathf.Max(threatDangerDistance + 0.1f, threatSafeDistance);
            float danger = Mathf.Max(0.05f, threatDangerDistance);
            targetThreat = Mathf.InverseLerp(safe, danger, distance);
            targetThreat = Mathf.Clamp01(targetThreat);
        }

        float threatLerp = 1f - Mathf.Exp(-Mathf.Max(0.1f, threatSmoothing) * dt);
        smoothedThreat = Mathf.Lerp(smoothedThreat, targetThreat, threatLerp);
    }

    private void UpdateStartupSequence()
    {
        // No consume tiempo de inicio/cuenta regresiva mientras el fundido tapa la pantalla.
        // Esto garantiza que el "3" visible dure un segundo completo.
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
                lastCountdownCueValue = -1;
                countdownGoCuePlayed = false;
            }
            return;
        }

        if (runPhase == RunPhase.Countdown)
        {
            countdownElapsed += dt;
            PlayCountdownTickIfNeeded();
            if (countdownElapsed >= countdownStartValue)
            {
                runPhase = RunPhase.GoFlash;
                goFlashTimer = 0f;
                PlayCountdownGoIfNeeded();
            }
            return;
        }

        if (runPhase == RunPhase.GoFlash)
        {
            PlayCountdownGoIfNeeded();
            goFlashTimer += dt;
            if (goFlashTimer >= Mathf.Max(0.05f, goFlashSeconds))
            {
                runPhase = RunPhase.Active;
                Time.timeScale = 1f;
            }
        }
    }

    private void PlayCountdownTickIfNeeded()
    {
        if (runPhase != RunPhase.Countdown)
        {
            return;
        }

        int remaining = countdownStartValue - Mathf.FloorToInt(countdownElapsed);
        remaining = Mathf.Clamp(remaining, 1, countdownStartValue);
        if (remaining == lastCountdownCueValue)
        {
            return;
        }

        lastCountdownCueValue = remaining;
        GlitchAudioManager.PlayCountdownTick(remaining);
    }

    private void PlayCountdownGoIfNeeded()
    {
        if (countdownGoCuePlayed)
        {
            return;
        }

        countdownGoCuePlayed = true;
        GlitchAudioManager.PlayCountdownGo();
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
        float phaseN = GetCountdownPhaseNormalized();

        GUIStyle dynamicMain = new GUIStyle(countdownStyle);
        float countdownBeat = runPhase == RunPhase.Countdown
            ? Mathf.SmoothStep(1f, 0f, phaseN)
            : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(goFlashTimer / Mathf.Max(0.05f, goFlashSeconds)));
        dynamicMain.fontSize = Mathf.RoundToInt(Mathf.Lerp(66f, 82f, pulse * 0.55f + countdownBeat * 0.45f));
        dynamicMain.normal.textColor = Color.Lerp(
            new Color(countdownPrimary.r, countdownPrimary.g, countdownPrimary.b, 0.92f),
            new Color(countdownAccent.r, countdownAccent.g, countdownAccent.b, 0.98f),
            pulse * 0.26f);

        DrawCountdownOrbitTicks(centerX, centerY, pulse, phaseN, runPhase == RunPhase.GoFlash);
        DrawCountdownImpactSweep(pulse, phaseN);
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

        float beat = runPhase == RunPhase.Countdown ? Mathf.SmoothStep(1f, 0f, GetCountdownPhaseNormalized()) : 1f;
        float flashAlpha = Mathf.Clamp01(countdownImpactFlashOpacity) * beat;
        DrawSolidRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(countdownAccent.r, countdownAccent.g, countdownAccent.b, flashAlpha * 0.15f));
    }

    private float GetCountdownPhaseNormalized()
    {
        if (runPhase != RunPhase.Countdown)
        {
            return 0f;
        }

        float fractional = countdownElapsed - Mathf.Floor(countdownElapsed);
        return Mathf.Clamp01(fractional);
    }

    private void DrawCountdownOrbitTicks(float centerX, float centerY, float pulse, float phaseN, bool goPhase)
    {
        int tickCount = Mathf.Max(6, countdownOrbitTickCount);
        float time = Time.unscaledTime;
        float spin = time * countdownOrbitSpinSpeed * (goPhase ? 2.8f : 1f);
        float radiusX = Mathf.Lerp(Screen.width * 0.08f, Screen.width * 0.18f, goPhase ? 1f : 0.3f + pulse * 0.25f);
        float radiusY = Mathf.Lerp(Screen.height * 0.08f, Screen.height * 0.15f, goPhase ? 1f : 0.32f + pulse * 0.22f);
        float alphaBoost = goPhase ? 1f : Mathf.SmoothStep(1f, 0.45f, phaseN);

        for (int i = 0; i < tickCount; i++)
        {
            float a = ((Mathf.PI * 2f) * i / tickCount) + spin;
            float x = centerX + Mathf.Cos(a) * radiusX;
            float y = centerY + Mathf.Sin(a) * radiusY;
            float localPulse = 0.35f + 0.65f * (0.5f + 0.5f * Mathf.Sin(time * 7.5f + i * 0.8f));
            Color c = Color.Lerp(countdownPrimary, countdownAccent, i / (float)tickCount);
            c.a = (0.12f + localPulse * 0.16f) * alphaBoost;
            DrawSolidRect(new Rect(x - 2f, y - 2f, 4f, 4f), c);
        }
    }

    private void DrawCountdownImpactSweep(float pulse, float phaseN)
    {
        float time = Time.unscaledTime;
        float speed = Mathf.Max(120f, countdownImpactSweepSpeed);
        float x = Mathf.Repeat(time * speed, Screen.width + 240f) - 120f;
        float y = Screen.height * (0.40f + Mathf.Sin(time * 1.7f) * 0.04f);
        float h = Mathf.Lerp(2f, 6f, pulse);
        float beat = runPhase == RunPhase.GoFlash ? 1f : Mathf.SmoothStep(1f, 0.30f, phaseN);
        DrawSolidRect(new Rect(x - 160f, y, 320f, h), new Color(0.9f, 0.97f, 1f, 0.08f * beat));
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
        themedEventStatusProvider = FindThemedEventStatusProvider();
        if (arenaGenerator != null)
        {
            levelType = arenaGenerator.ActiveThemeLabel;
        }
    }

    private string GetMapEventLabel()
    {
        if (chaosDirector != null)
        {
            string warning = chaosDirector.ActiveWarningLabel;
            if (!string.IsNullOrWhiteSpace(warning))
            {
                return warning;
            }

            string label = chaosDirector.ActiveEventLabel;
            if (!string.IsNullOrWhiteSpace(label))
            {
                return label;
            }
        }

        string themedLabel = GetThemedEventLabel();
        if (!string.IsNullOrWhiteSpace(themedLabel))
        {
            return themedLabel;
        }

        return "Nominal";
    }

    private void DrawChaosWarningOverlay()
    {
        string warning = chaosDirector != null ? chaosDirector.ActiveWarningLabel : string.Empty;
        if (string.IsNullOrWhiteSpace(warning))
        {
            DrawThemedEventHintOverlay();
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

    private void DrawThemedEventHintOverlay()
    {
        string hint = GetThemedEventHint();
        if (string.IsNullOrWhiteSpace(hint))
        {
            return;
        }

        EnsureWarningStyle();
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6.8f);
        float s = hudScale;
        float w = Mathf.Min(Screen.width * 0.72f, 760f * s);
        float h = 46f * s;
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height * 0.18f;

        DrawSolidRect(new Rect(x, y, w, h), new Color(0.02f, 0.04f, 0.08f, 0.44f + pulse * 0.08f));
        DrawSolidRect(new Rect(x, y, w, 2f * s), new Color(0.42f, 0.96f, 1f, 0.58f + pulse * 0.18f));
        DrawSolidRect(new Rect(x, y + h - (2f * s), w, 2f * s), new Color(1f, 0.46f, 0.78f, 0.38f + pulse * 0.12f));

        Color old = GUI.color;
        GUI.color = new Color(0.88f, 0.97f, 1f, Mathf.Lerp(0.72f, 0.96f, pulse));
        GUI.Label(new Rect(x, y + (5f * s), w, h), hint.ToUpperInvariant(), eventWarningStyle);
        GUI.color = old;
    }

    private string GetThemedEventLabel()
    {
        IThemedEventStatusProvider provider = GetThemedEventStatusProvider();
        return provider != null ? provider.ActiveThemedEventLabel : string.Empty;
    }

    private string GetThemedEventHint()
    {
        IThemedEventStatusProvider provider = GetThemedEventStatusProvider();
        return provider != null ? provider.ActiveThemedEventHint : string.Empty;
    }

    private IThemedEventStatusProvider GetThemedEventStatusProvider()
    {
        if (!IsThemedEventStatusProviderValid())
        {
            themedEventStatusProvider = FindThemedEventStatusProvider();
        }

        return themedEventStatusProvider;
    }

    private bool IsThemedEventStatusProviderValid()
    {
        return themedEventStatusProvider is MonoBehaviour behaviour && behaviour != null && behaviour.isActiveAndEnabled;
    }

    private static IThemedEventStatusProvider FindThemedEventStatusProvider()
    {
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null && behaviours[i].isActiveAndEnabled && behaviours[i] is IThemedEventStatusProvider provider)
            {
                return provider;
            }
        }

        return null;
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
            if (IsBossSpecialState(stateRaw))
            {
                statePulseOverlayTimer = Mathf.Max(statePulseOverlayTimer, Mathf.Max(0.05f, statePulseOverlayDuration));
                bossStateBannerTimer = Mathf.Max(bossStateBannerTimer, Mathf.Max(0.1f, bossStateBannerDuration));
                bossStateBannerRaw = stateRaw;
            }
            else
            {
                bossStateBannerTimer = 0f;
                bossStateBannerRaw = null;
            }

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

        string stateRaw = enemyController != null ? enemyController.CurrentStateLabel : string.Empty;
        string stateValue = "Unknown";
        if (!string.IsNullOrWhiteSpace(stateRaw))
        {
            stateValue = ToBossStateLabel(stateRaw);
        }
        if (!IsBossSpecialState(stateRaw))
        {
            return;
        }

        float s = hudScale;
        float width = Mathf.Min(Screen.width * 0.44f, 560f * s);
        float lineHeight = 25f * s;
        float x = (Screen.width - width) * 0.5f;
        float y = 8f * s;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7.5f);
        Color stateColor = GetBossStateColor(stateRaw);
        Color panelFill = Color.Lerp(new Color(0.03f, 0.04f, 0.08f, 0.84f), new Color(stateColor.r, stateColor.g, stateColor.b, 0.76f), 0.18f + pulse * 0.08f);
        Rect panel = new Rect(x - (16f * s), y, width + (32f * s), 66f * s);

        DrawSolidRect(panel, panelFill);
        DrawSolidRect(new Rect(panel.x, panel.y, panel.width, 2f * s), new Color(stateColor.r, stateColor.g, stateColor.b, 0.78f));
        DrawSolidRect(new Rect(panel.x, panel.yMax - (2f * s), panel.width, 2f * s), new Color(0.45f, 0.72f, 1f, 0.36f));

        float markerW = Mathf.Lerp(42f * s, 90f * s, pulse);
        DrawSolidRect(new Rect(panel.x + (8f * s), panel.y + (7f * s), markerW, 3f * s), new Color(stateColor.r, stateColor.g, stateColor.b, 0.64f));
        DrawSolidRect(new Rect(panel.xMax - markerW - (8f * s), panel.yMax - (10f * s), markerW, 3f * s), new Color(stateColor.r, stateColor.g, stateColor.b, 0.48f));

        Color oldValue = bossStateValueStyle.normal.textColor;
        bossStateValueStyle.normal.textColor = Color.Lerp(Color.white, stateColor, 0.42f);
        GUI.Label(new Rect(x, y + (6f * s), width, lineHeight), "ANOMALIA", bossStateStyle);
        GUI.Label(new Rect(x, y + (30f * s), width, lineHeight + (6f * s)), stateValue.ToUpperInvariant(), bossStateValueStyle);
        bossStateValueStyle.normal.textColor = oldValue;
    }

    private void DrawBossStateBanner()
    {
        if (bossStateBannerTimer <= 0f || string.IsNullOrWhiteSpace(bossStateBannerRaw))
        {
            return;
        }

        EnsureBossStateStyles();

        float duration = Mathf.Max(0.1f, bossStateBannerDuration);
        float normalized = Mathf.Clamp01(bossStateBannerTimer / duration);
        float appear = Mathf.Sin((1f - normalized) * Mathf.PI);
        float hold = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(normalized * 4f));
        float alpha = Mathf.Clamp01(Mathf.Max(appear, hold * normalized));
        if (alpha <= 0.01f)
        {
            return;
        }

        float s = hudScale;
        string label = ToBossStateLabel(bossStateBannerRaw).ToUpperInvariant();
        Color stateColor = GetBossStateColor(bossStateBannerRaw);
        float width = Mathf.Min(Screen.width * 0.72f, 760f * s);
        float height = 112f * s;
        float x = (Screen.width - width) * 0.5f;
        float y = Mathf.Max(86f * s, Screen.height * 0.16f);
        float jitter = Mathf.Sin(Time.unscaledTime * 34f) * 2.4f * s * alpha;
        Rect panel = new Rect(x + jitter, y, width, height);

        DrawSolidRect(panel, new Color(0.02f, 0.03f, 0.07f, 0.82f * alpha));
        DrawSolidRect(new Rect(panel.x, panel.y, panel.width, 3f * s), new Color(stateColor.r, stateColor.g, stateColor.b, 0.92f * alpha));
        DrawSolidRect(new Rect(panel.x, panel.yMax - (3f * s), panel.width, 3f * s), new Color(0.80f, 0.94f, 1f, 0.42f * alpha));

        float sweepW = Mathf.Lerp(80f * s, width * 0.7f, 1f - normalized);
        DrawSolidRect(new Rect(panel.x + (16f * s), panel.y + (16f * s), sweepW, 4f * s), new Color(stateColor.r, stateColor.g, stateColor.b, 0.58f * alpha));
        DrawSolidRect(new Rect(panel.xMax - sweepW - (16f * s), panel.yMax - (20f * s), sweepW, 4f * s), new Color(stateColor.r, stateColor.g, stateColor.b, 0.42f * alpha));

        Color oldGui = GUI.color;
        Color oldLabel = bossStateBannerLabelStyle.normal.textColor;
        Color oldValue = bossStateBannerValueStyle.normal.textColor;
        GUI.color = new Color(1f, 1f, 1f, alpha);
        bossStateBannerLabelStyle.normal.textColor = new Color(0.88f, 0.94f, 1f, 0.86f * alpha);
        bossStateBannerValueStyle.normal.textColor = Color.Lerp(Color.white, stateColor, 0.36f);

        GUI.Label(new Rect(panel.x, panel.y + (16f * s), panel.width, 26f * s), "CAMBIO DE ESTADO", bossStateBannerLabelStyle);
        GUI.Label(new Rect(panel.x, panel.y + (42f * s), panel.width, 58f * s), label, bossStateBannerValueStyle);

        bossStateBannerLabelStyle.normal.textColor = oldLabel;
        bossStateBannerValueStyle.normal.textColor = oldValue;
        GUI.color = oldGui;
    }

    private bool ShouldOpenUpgradeSelection()
    {
        return enableRunUpgrades &&
               !upgradeSelectionOpen &&
               IsRunActive &&
               SurvivalTime >= nextUpgradeTime &&
               playerController != null;
    }

    private void OpenUpgradeSelection()
    {
        currentUpgradeChoices.Clear();

        List<PlayerUpgradeKind> pool = new List<PlayerUpgradeKind>
        {
            PlayerUpgradeKind.MoveSpeed,
            PlayerUpgradeKind.ParryWindow,
            PlayerUpgradeKind.ParryCooldown,
            PlayerUpgradeKind.ParryRadius,
            PlayerUpgradeKind.ShieldDuration,
            PlayerUpgradeKind.FirewallChargeGain,
            PlayerUpgradeKind.FirewallBurstRadius,
            PlayerUpgradeKind.FirewallBurstStun
        };

        int desired = Mathf.Clamp(upgradeOptionsShown, 1, pool.Count);
        while (currentUpgradeChoices.Count < desired && pool.Count > 0)
        {
            int index = Random.Range(0, pool.Count);
            PlayerUpgradeKind kind = pool[index];
            pool.RemoveAt(index);
            currentUpgradeChoices.Add(BuildUpgradeChoice(kind));
        }

        upgradeSelectionOpen = currentUpgradeChoices.Count > 0;
        if (upgradeSelectionOpen)
        {
            upgradeSelectionClosing = false;
            upgradeSelectionAge = 0f;
            upgradeTimeRemaining = Mathf.Max(1f, upgradeChoiceLimitSeconds);
            upgradeExitTimer = 0f;
            upgradeSelectedIndex = -1;
            upgradeSelectedAccent = Color.white;
            Time.timeScale = 0f;
            GlitchAudioManager.PlayUpgradeOpen();
        }
    }

    private void UpdateUpgradeSelectionState()
    {
        float dt = Time.unscaledDeltaTime;
        upgradeSelectionAge += dt;

        if (upgradeSelectionClosing)
        {
            upgradeExitTimer -= dt;
            if (upgradeExitTimer <= 0f)
            {
                FinishUpgradeSelection();
            }

            return;
        }

        upgradeTimeRemaining -= dt;
        if (upgradeTimeRemaining <= 0f && currentUpgradeChoices.Count > 0)
        {
            ChooseUpgrade(Random.Range(0, currentUpgradeChoices.Count));
        }
    }

    private UpgradeChoice BuildUpgradeChoice(PlayerUpgradeKind kind)
    {
        switch (kind)
        {
            case PlayerUpgradeKind.MoveSpeed:
                return new UpgradeChoice
                {
                    kind = kind,
                    title = "Impulso Vectorial",
                    description = "Aumenta la velocidad base del jugador.",
                    category = "MOVIMIENTO",
                    rarity = "COMUN",
                    accent = new Color(0.46f, 0.96f, 1f, 1f)
                };
            case PlayerUpgradeKind.ParryWindow:
                return new UpgradeChoice
                {
                    kind = kind,
                    title = "Ventana Extendida",
                    description = "El Firewall Parry permanece activo un poco mas.",
                    category = "FIREWALL",
                    rarity = "COMUN",
                    accent = new Color(1f, 0.90f, 0.54f, 1f)
                };
            case PlayerUpgradeKind.ParryCooldown:
                return new UpgradeChoice
                {
                    kind = kind,
                    title = "Recarga Fria",
                    description = "Reduce el tiempo de espera del Firewall Parry.",
                    category = "FIREWALL",
                    rarity = "COMUN",
                    accent = new Color(0.60f, 0.84f, 1f, 1f)
                };
            case PlayerUpgradeKind.ParryRadius:
                return new UpgradeChoice
                {
                    kind = kind,
                    title = "Pulso Expandido",
                    description = "Aumenta el radio efectivo del Firewall Parry.",
                    category = "FIREWALL",
                    rarity = "COMUN",
                    accent = new Color(0.76f, 0.64f, 1f, 1f)
                };
            case PlayerUpgradeKind.FirewallChargeGain:
                return new UpgradeChoice
                {
                    kind = kind,
                    title = "Recolector de Datos",
                    description = "Pickups, nodos y parries cargan mas rapido el Firewall Burst.",
                    category = "BUILD",
                    rarity = "INESTABLE",
                    accent = new Color(0.52f, 1f, 0.78f, 1f)
                };
            case PlayerUpgradeKind.FirewallBurstRadius:
                return new UpgradeChoice
                {
                    kind = kind,
                    title = "Firewall Amplificado",
                    description = "Aumenta el area del Burst para empujar enemigos y limpiar proyectiles.",
                    category = "CONTROL",
                    rarity = "INESTABLE",
                    accent = new Color(0.42f, 0.94f, 1f, 1f)
                };
            case PlayerUpgradeKind.FirewallBurstStun:
                return new UpgradeChoice
                {
                    kind = kind,
                    title = "Corte de Senal",
                    description = "El Burst deja a la anomalia vulnerable durante mas tiempo.",
                    category = "CONTROL",
                    rarity = "CRITICO",
                    accent = new Color(1f, 0.54f, 0.72f, 1f)
                };
            default:
                return new UpgradeChoice
                {
                    kind = kind,
                    title = "Escudo Resonante",
                    description = "Los escudos duran mas cuando los recolectas.",
                    category = "DEFENSA",
                    rarity = "COMUN",
                    accent = new Color(1f, 0.66f, 0.86f, 1f)
                };
        }
    }

    private void ChooseUpgrade(int index)
    {
        if (!upgradeSelectionOpen ||
            upgradeSelectionClosing ||
            playerController == null ||
            index < 0 ||
            index >= currentUpgradeChoices.Count)
        {
            return;
        }

        UpgradeChoice choice = currentUpgradeChoices[index];
        ApplyUpgrade(choice.kind);
        AddScore(Mathf.Max(0, upgradeScoreBonus));
        GlitchAudioManager.PlayUpgradeSelect();

        upgradeSelectedIndex = index;
        upgradeSelectedAccent = choice.accent;
        upgradeSelectionClosing = true;
        upgradeExitTimer = Mathf.Max(0.08f, upgradeExitDuration);
        upgradePickCount++;
    }

    private void FinishUpgradeSelection()
    {
        currentUpgradeChoices.Clear();
        upgradeSelectionOpen = false;
        upgradeSelectionClosing = false;
        upgradeSelectionAge = 0f;
        upgradeTimeRemaining = 0f;
        upgradeExitTimer = 0f;
        upgradeSelectedIndex = -1;
        nextUpgradeTime = SurvivalTime + Mathf.Max(8f, upgradeInterval);
        Time.timeScale = 1f;
    }

    private void ApplyUpgrade(PlayerUpgradeKind kind)
    {
        if (playerController == null)
        {
            return;
        }

        switch (kind)
        {
            case PlayerUpgradeKind.MoveSpeed:
                playerController.AddPermanentMoveSpeed(0.65f);
                break;
            case PlayerUpgradeKind.ParryWindow:
                playerController.ExtendParryWindow(0.035f);
                break;
            case PlayerUpgradeKind.ParryCooldown:
                playerController.ReduceParryCooldown(0.86f);
                break;
            case PlayerUpgradeKind.ParryRadius:
                playerController.ExpandParryRadius(0.16f);
                break;
            case PlayerUpgradeKind.ShieldDuration:
                playerController.ImproveShieldDuration(1.18f);
                break;
            case PlayerUpgradeKind.FirewallChargeGain:
                playerController.ImproveFirewallChargeGain(1.22f);
                break;
            case PlayerUpgradeKind.FirewallBurstRadius:
                playerController.ExpandFirewallBurstRadius(0.42f);
                break;
            case PlayerUpgradeKind.FirewallBurstStun:
                playerController.ImproveFirewallBurstStun(0.22f);
                break;
        }
    }

    private void DrawUpgradeSelectionOverlay()
    {
        if (!upgradeSelectionOpen || currentUpgradeChoices.Count == 0)
        {
            return;
        }

        EnsureUpgradeStyles();

        float s = hudScale;
        Color old = GUI.color;
        float enter = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(upgradeSelectionAge / Mathf.Max(0.05f, upgradeEnterDuration)));
        float exit = upgradeSelectionClosing
            ? Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - (upgradeExitTimer / Mathf.Max(0.05f, upgradeExitDuration))))
            : 0f;
        float alpha = Mathf.Clamp01(enter) * Mathf.Lerp(1f, 0.18f, exit);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 11f);
        DrawSolidRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.01f, 0.015f, 0.03f, 0.76f * alpha));

        float panelW = Mathf.Min(Screen.width * 0.82f, 900f * s);
        float panelH = Mathf.Min(Screen.height * 0.72f, 430f * s);
        float panelX = (Screen.width - panelW) * 0.5f;
        float panelY = (Screen.height - panelH) * 0.5f;
        panelY += Mathf.Lerp(36f * s, 0f, enter) - (exit * 28f * s);
        Rect panel = new Rect(panelX, panelY, panelW, panelH);

        DrawUpgradeScanlines(alpha, enter, exit, s);
        DrawSolidRect(panel, new Color(0.025f, 0.04f, 0.08f, 0.92f * alpha));
        DrawSolidRect(new Rect(panel.x, panel.y, panel.width, 3f * s), new Color(0.46f, 0.96f, 1f, 0.74f * alpha));
        DrawSolidRect(new Rect(panel.x, panel.yMax - (3f * s), panel.width, 3f * s), new Color(1f, 0.58f, 0.74f, 0.46f * alpha));

        float sweepW = Mathf.Lerp(40f * s, panel.width * 0.52f, enter);
        float sweepX = panel.x + Mathf.Repeat(Time.unscaledTime * 180f * s, Mathf.Max(1f, panel.width + sweepW)) - sweepW;
        DrawSolidRect(new Rect(sweepX, panel.y + (10f * s), sweepW, 2f * s), new Color(0.46f, 0.96f, 1f, 0.22f * alpha));

        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.Label(new Rect(panel.x, panel.y + (26f * s), panel.width, 24f * s), $"UPGRADE {upgradePickCount + 1}", upgradeKickerStyle);
        GUI.Label(new Rect(panel.x, panel.y + (54f * s), panel.width, 48f * s), "ELIGE UNA ALTERACION", upgradeTitleStyle);
        DrawUpgradeTimer(panel, s, alpha, pulse);

        float cardGap = 14f * s;
        float cardsTop = panel.y + (132f * s);
        float cardH = panel.height - (166f * s);
        float cardW = (panel.width - (48f * s) - (cardGap * (currentUpgradeChoices.Count - 1))) / currentUpgradeChoices.Count;
        float cardX = panel.x + (24f * s);

        for (int i = 0; i < currentUpgradeChoices.Count; i++)
        {
            UpgradeChoice choice = currentUpgradeChoices[i];
            Rect card = new Rect(cardX + ((cardW + cardGap) * i), cardsTop, cardW, cardH);
            float cardEnter = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((upgradeSelectionAge - (0.06f * i)) / Mathf.Max(0.05f, upgradeEnterDuration)));
            card.y += Mathf.Lerp(28f * s, 0f, cardEnter);
            DrawUpgradeCard(card, choice, i, s, alpha, cardEnter, exit);
        }

        if (upgradeSelectionClosing && upgradeSelectedIndex >= 0)
        {
            DrawUpgradePickFx(panel, s, alpha);
        }

        GUI.color = old;
    }

    private void DrawUpgradeCard(Rect card, UpgradeChoice choice, int index, float s, float alpha, float enter, float exit)
    {
        bool selected = upgradeSelectionClosing && index == upgradeSelectedIndex;
        bool suppressed = upgradeSelectionClosing && index != upgradeSelectedIndex;
        bool hovered = !upgradeSelectionClosing && card.Contains(Event.current.mousePosition);
        float selectedPulse = selected ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 22f) : 0f;
        Color fill = hovered
            ? new Color(choice.accent.r * 0.18f, choice.accent.g * 0.18f, choice.accent.b * 0.22f, 0.92f)
            : new Color(0.04f, 0.06f, 0.11f, 0.90f);
        if (selected)
        {
            fill = Color.Lerp(fill, new Color(choice.accent.r * 0.32f, choice.accent.g * 0.28f, choice.accent.b * 0.22f, 0.96f), 0.75f);
        }

        float cardAlpha = alpha * enter * (suppressed ? Mathf.Lerp(1f, 0.18f, exit) : 1f);
        Rect animatedCard = selected
            ? new Rect(card.x - (5f * s * selectedPulse), card.y - (5f * s * selectedPulse), card.width + (10f * s * selectedPulse), card.height + (10f * s * selectedPulse))
            : card;

        DrawSolidRect(animatedCard, new Color(fill.r, fill.g, fill.b, fill.a * cardAlpha));
        DrawSolidRect(new Rect(animatedCard.x, animatedCard.y, animatedCard.width, 2f * s), new Color(choice.accent.r, choice.accent.g, choice.accent.b, (0.78f + selectedPulse * 0.18f) * cardAlpha));
        DrawSolidRect(new Rect(animatedCard.x + (14f * s), animatedCard.y + (18f * s), Mathf.Lerp(30f * s, 86f * s, selected ? selectedPulse : enter), 5f * s), new Color(choice.accent.r, choice.accent.g, choice.accent.b, 0.62f * cardAlpha));

        Color old = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, cardAlpha);
        string meta = $"{choice.category} / {choice.rarity}";
        GUI.Label(new Rect(animatedCard.x + (16f * s), animatedCard.y + (26f * s), animatedCard.width - (32f * s), 22f * s), meta, hudLabelStyle);
        GUI.Label(new Rect(animatedCard.x + (16f * s), animatedCard.y + (52f * s), animatedCard.width - (32f * s), 54f * s), choice.title.ToUpperInvariant(), upgradeButtonStyle);
        GUI.Label(new Rect(animatedCard.x + (16f * s), animatedCard.y + (112f * s), animatedCard.width - (32f * s), 78f * s), choice.description, upgradeDescriptionStyle);

        Rect buttonRect = new Rect(animatedCard.x + (16f * s), animatedCard.yMax - (52f * s), animatedCard.width - (32f * s), 34f * s);
        DrawSolidRect(buttonRect, new Color(choice.accent.r, choice.accent.g, choice.accent.b, (hovered ? 0.46f : 0.28f) * cardAlpha));
        GUI.enabled = !upgradeSelectionClosing;
        if (GUI.Button(buttonRect, selected ? "INSTALANDO" : "INSTALAR", upgradeButtonStyle))
        {
            ChooseUpgrade(index);
        }
        GUI.enabled = true;
        GUI.color = old;
    }

    private void DrawUpgradeTimer(Rect panel, float s, float alpha, float pulse)
    {
        float limit = Mathf.Max(1f, upgradeChoiceLimitSeconds);
        float normalized = Mathf.Clamp01(upgradeTimeRemaining / limit);
        Color timerColor = Color.Lerp(new Color(1f, 0.35f, 0.46f, 1f), new Color(0.46f, 0.96f, 1f, 1f), normalized);

        Rect timerRect = new Rect(panel.x + (32f * s), panel.y + (108f * s), panel.width - (64f * s), 10f * s);
        DrawSolidRect(timerRect, new Color(0.03f, 0.05f, 0.09f, 0.85f * alpha));
        DrawSolidRect(new Rect(timerRect.x, timerRect.y, timerRect.width * normalized, timerRect.height), new Color(timerColor.r, timerColor.g, timerColor.b, (0.82f + pulse * 0.12f) * alpha));

        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.Label(new Rect(panel.x, panel.y + (106f * s), panel.width, 24f * s), $"{Mathf.CeilToInt(Mathf.Max(0f, upgradeTimeRemaining))}s", upgradeTimerStyle);
    }

    private void DrawUpgradeScanlines(float alpha, float enter, float exit, float s)
    {
        float lineAlpha = 0.035f * alpha * Mathf.Lerp(0.4f, 1f, enter);
        for (float y = Mathf.Repeat(Time.unscaledTime * 46f * s, 18f * s) - (18f * s); y < Screen.height; y += 18f * s)
        {
            DrawSolidRect(new Rect(0f, y, Screen.width, 1f * s), new Color(0.48f, 0.93f, 1f, lineAlpha));
        }

        if (exit <= 0f)
        {
            return;
        }

        float sweepY = Mathf.Lerp(0f, Screen.height, exit);
        DrawSolidRect(new Rect(0f, sweepY - (8f * s), Screen.width, 16f * s), new Color(upgradeSelectedAccent.r, upgradeSelectedAccent.g, upgradeSelectedAccent.b, 0.22f * alpha));
    }

    private void DrawUpgradePickFx(Rect panel, float s, float alpha)
    {
        float progress = Mathf.Clamp01(1f - (upgradeExitTimer / Mathf.Max(0.05f, upgradeExitDuration)));
        float flash = Mathf.Sin(progress * Mathf.PI);
        Color c = upgradeSelectedAccent;
        float ringW = Mathf.Lerp(80f * s, panel.width * 0.92f, progress);
        float ringH = Mathf.Lerp(18f * s, panel.height * 0.92f, progress);
        Rect ring = new Rect(
            panel.center.x - (ringW * 0.5f),
            panel.center.y - (ringH * 0.5f),
            ringW,
            ringH);

        DrawSolidRect(new Rect(ring.x, ring.y, ring.width, 3f * s), new Color(c.r, c.g, c.b, 0.62f * flash * alpha));
        DrawSolidRect(new Rect(ring.x, ring.yMax - (3f * s), ring.width, 3f * s), new Color(c.r, c.g, c.b, 0.48f * flash * alpha));
        DrawSolidRect(new Rect(ring.x, ring.y, 3f * s, ring.height), new Color(c.r, c.g, c.b, 0.46f * flash * alpha));
        DrawSolidRect(new Rect(ring.xMax - (3f * s), ring.y, 3f * s, ring.height), new Color(c.r, c.g, c.b, 0.46f * flash * alpha));
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

    private static bool IsBossSpecialState(string raw)
    {
        switch (raw)
        {
            case "Split":
            case "ExpansionShoot":
            case "SpeedSurge":
            case "WeaveHunter":
            case "Destroyer":
                return true;
            default:
                return false;
        }
    }

    private static Color GetBossStateColor(string raw)
    {
        switch (raw)
        {
            case "DirectChase":
                return new Color(1f, 0.48f, 0.66f, 1f);
            case "PredictiveIntercept":
                return new Color(0.48f, 0.94f, 1f, 1f);
            case "CutoffFlank":
                return new Color(1f, 0.76f, 0.46f, 1f);
            case "ErraticBurst":
                return new Color(1f, 0.38f, 0.52f, 1f);
            case "Split":
                return new Color(0.74f, 0.76f, 1f, 1f);
            case "ExpansionShoot":
                return new Color(1f, 0.50f, 0.72f, 1f);
            case "SpeedSurge":
                return new Color(0.98f, 0.94f, 0.58f, 1f);
            case "WeaveHunter":
                return new Color(0.85f, 0.68f, 1f, 1f);
            case "Destroyer":
                return new Color(1f, 0.86f, 0.88f, 1f);
            default:
                return new Color(1f, 0.76f, 0.82f, 1f);
        }
    }

    private void EnsureBossStateStyles()
    {
        if (bossStateStyle != null &&
            bossStateValueStyle != null &&
            bossStateBannerLabelStyle != null &&
            bossStateBannerValueStyle != null &&
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
        int bossLabelSize = Mathf.RoundToInt(16f * hudScale);
        int bossValueSize = Mathf.RoundToInt(24f * hudScale);
        int bannerLabelSize = Mathf.RoundToInt(18f * hudScale);
        int bannerValueSize = Mathf.RoundToInt(42f * hudScale);

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

        bossStateBannerLabelStyle = new GUIStyle(GUI.skin.label)
        {
            font = secondaryFont,
            fontSize = bannerLabelSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Overflow,
            wordWrap = false
        };
        bossStateBannerLabelStyle.normal.textColor = new Color(0.88f, 0.94f, 1f, 0.86f);

        bossStateBannerValueStyle = new GUIStyle(GUI.skin.label)
        {
            font = importantFont,
            fontSize = bannerValueSize,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Overflow,
            wordWrap = false
        };
        bossStateBannerValueStyle.normal.textColor = Color.white;
    }

    private void DrawRuntimeHud()
    {
        EnsureBossStateStyles();
        DrawHudAmbientFrame();

        float s = hudScale;
        Rect leftPanel = new Rect(12f * s, 10f * s, 320f * s, 146f * s);
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
        int shownScore = Mathf.Max(0, Mathf.RoundToInt(displayedScore));
        GUI.Label(new Rect(rightColX, valueY, colW, valueH), shownScore.ToString(), hudValueStyle);

        DrawFirewallChargeMeter(leftPanel, s);
        DrawThreatMeter(leftPanel, s);
        DrawScorePopups(rightColX + (colW * 0.45f), valueY - (6f * s), s);

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

        if (enableReactiveHudFx)
        {
            DrawThreatVignette();
        }
    }

    private void DrawThreatMeter(Rect panel, float s)
    {
        float barH = 10f * s;
        float barW = panel.width - (28f * s);
        float barX = panel.x + (14f * s);
        float barY = panel.yMax - (16f * s);
        Rect background = new Rect(barX, barY, barW, barH);
        DrawSolidRect(background, new Color(0.05f, 0.07f, 0.11f, 0.82f));

        float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 8f);
        Color barColor = Color.Lerp(new Color(0.34f, 0.83f, 1f, 0.86f), new Color(1f, 0.35f, 0.45f, 0.96f), smoothedThreat);
        float threatFill = barW * Mathf.Clamp01(smoothedThreat);
        DrawSolidRect(new Rect(barX, barY, threatFill, barH), new Color(barColor.r, barColor.g, barColor.b, 0.85f + pulse * 0.12f * smoothedThreat));
        DrawSolidRect(new Rect(barX, barY, barW, 1f), new Color(0.85f, 0.92f, 1f, 0.18f));
        DrawSolidRect(new Rect(barX, barY + barH - 1f, barW, 1f), new Color(0.85f, 0.92f, 1f, 0.14f));
    }

    private void DrawFirewallChargeMeter(Rect panel, float s)
    {
        if (playerController == null)
        {
            return;
        }

        float normalized = playerController.FirewallChargeNormalized;
        bool ready = playerController.IsFirewallBurstReady;
        float barH = 9f * s;
        float barW = panel.width - (28f * s);
        float barX = panel.x + (14f * s);
        float barY = panel.yMax - (42f * s);
        Rect background = new Rect(barX, barY, barW, barH);
        DrawSolidRect(background, new Color(0.04f, 0.06f, 0.10f, 0.86f));

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (ready ? 13f : 6f));
        Color fill = ready
            ? Color.Lerp(new Color(0.45f, 0.95f, 1f, 0.95f), new Color(1f, 0.88f, 0.48f, 1f), pulse)
            : Color.Lerp(new Color(0.22f, 0.48f, 0.74f, 0.82f), new Color(0.45f, 0.95f, 1f, 0.95f), normalized);

        DrawSolidRect(new Rect(barX, barY, barW * normalized, barH), fill);
        DrawSolidRect(new Rect(barX, barY, barW, 1f), new Color(0.85f, 0.92f, 1f, 0.18f));
        DrawSolidRect(new Rect(barX, barY + barH - 1f, barW, 1f), new Color(0.85f, 0.92f, 1f, 0.14f));

        string hint = ready ? "FIREWALL READY" : $"FIREWALL {Mathf.RoundToInt(normalized * 100f)}%";
        GUI.Label(new Rect(barX, barY - (21f * s), barW, 18f * s), hint, hudLabelStyle);
    }

    private void DrawScorePopups(float anchorX, float anchorY, float s)
    {
        if (scorePopups.Count == 0)
        {
            return;
        }

        for (int i = 0; i < scorePopups.Count; i++)
        {
            ScorePopup popup = scorePopups[i];
            float t = popup.age / Mathf.Max(0.001f, popup.lifetime);
            float rise = Mathf.Lerp(0f, 28f * s, t);
            float alpha = 1f - t;
            float jitter = Mathf.Sin((popup.age * 12f) + i) * 1.8f * s;

            Color old = GUI.color;
            GUI.color = new Color(popup.color.r, popup.color.g, popup.color.b, alpha);
            Rect r = new Rect(anchorX + popup.xJitter * 0.28f * s + jitter, anchorY - rise, 92f * s, 22f * s);
            GUI.Label(r, $"+{popup.amount}", hudLabelStyle);
            GUI.color = old;
        }
    }

    private void DrawThreatVignette()
    {
        float intensity = Mathf.Clamp01(smoothedThreat);
        if (intensity <= 0.02f)
        {
            return;
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (5f + intensity * 4f));
        Color tint = new Color(1f, 0.28f, 0.38f, (0.04f + intensity * 0.14f) * (0.76f + pulse * 0.24f));
        float edge = Mathf.Lerp(18f, 70f, intensity);

        DrawSolidRect(new Rect(0f, 0f, Screen.width, edge), tint);
        DrawSolidRect(new Rect(0f, Screen.height - edge, Screen.width, edge), tint);
        DrawSolidRect(new Rect(0f, edge, edge, Screen.height - edge * 2f), tint);
        DrawSolidRect(new Rect(Screen.width - edge, edge, edge, Screen.height - edge * 2f), tint);
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
        float threat = enableReactiveHudFx ? Mathf.Clamp01(smoothedThreat) : 0f;
        Color dangerTint = new Color(1f, 0.28f, 0.38f, 1f);
        Color reactiveBase = Color.Lerp(baseTint, dangerTint, threat * 0.72f);
        Color reactiveAccent = Color.Lerp(accentTint, dangerTint, threat * 0.86f);
        float reactiveSideOpacity = sideHudOpacity * Mathf.Lerp(0.72f, 1.45f, threat);
        float reactiveAccentOpacity = sideHudAccentOpacity * Mathf.Lerp(0.7f, 1.8f, threat);

        float t = Time.unscaledTime;
        float sideWidth = Mathf.Clamp(Screen.width * 0.052f, 36f, 86f);
        Rect left = new Rect(0f, 0f, sideWidth, Screen.height);
        Rect right = new Rect(Screen.width - sideWidth, 0f, sideWidth, Screen.height);
        DrawSolidRect(left, new Color(reactiveBase.r, reactiveBase.g, reactiveBase.b, reactiveSideOpacity));
        DrawSolidRect(right, new Color(reactiveBase.r, reactiveBase.g, reactiveBase.b, reactiveSideOpacity));

        float innerGlowWidth = Mathf.Max(2f, sideWidth * 0.12f);
        DrawSolidRect(
            new Rect(sideWidth - innerGlowWidth, 0f, innerGlowWidth, Screen.height),
            new Color(reactiveAccent.r, reactiveAccent.g, reactiveAccent.b, reactiveAccentOpacity));
        DrawSolidRect(
            new Rect(Screen.width - sideWidth, 0f, innerGlowWidth, Screen.height),
            new Color(reactiveAccent.r, reactiveAccent.g, reactiveAccent.b, reactiveAccentOpacity));

        float step = Mathf.Max(34f, Screen.height / 17f);
        for (float y = -step; y <= Screen.height + step; y += step)
        {
            float wobble = Mathf.Sin((y * 0.03f) + (t * Mathf.Lerp(1.1f, 2.0f, threat))) * Mathf.Lerp(2.5f, 4.8f, threat);
            float yAnim = y + Mathf.Sin((t * Mathf.Lerp(0.8f, 1.45f, threat)) + y * 0.02f) * Mathf.Lerp(4f, 6.5f, threat);
            float lineWidth = Mathf.Lerp(8f, sideWidth * 0.55f, Mathf.PerlinNoise(y * 0.013f, t * 0.45f));
            float lineAlpha = (0.08f + 0.09f * (0.5f + 0.5f * Mathf.Sin((t * 2.2f) + y * 0.06f))) * Mathf.Lerp(0.72f, 1.45f, threat);

            DrawSolidRect(
                new Rect(7f + wobble, yAnim, lineWidth, 2f),
                new Color(reactiveAccent.r, reactiveAccent.g, reactiveAccent.b, lineAlpha));
            DrawSolidRect(
                new Rect(Screen.width - 7f - lineWidth - wobble, yAnim, lineWidth, 2f),
                new Color(reactiveAccent.r, reactiveAccent.g, reactiveAccent.b, lineAlpha));
        }

        float sweepHeight = Mathf.Lerp(Screen.height * 0.11f, Screen.height * Mathf.Lerp(0.16f, 0.20f, threat), 0.5f + 0.5f * Mathf.Sin(t * Mathf.Lerp(0.35f, 0.68f, threat)));
        float sweepY = Mathf.Repeat(t * (Screen.height * Mathf.Lerp(0.10f, 0.18f, threat)), Screen.height + sweepHeight) - sweepHeight;
        DrawSolidRect(
            new Rect(0f, sweepY, sideWidth, sweepHeight),
            new Color(reactiveAccent.r, reactiveAccent.g, reactiveAccent.b, reactiveAccentOpacity * 0.56f));
        DrawSolidRect(
            new Rect(Screen.width - sideWidth, Screen.height - sweepY - sweepHeight, sideWidth, sweepHeight),
            new Color(reactiveAccent.r, reactiveAccent.g, reactiveAccent.b, reactiveAccentOpacity * 0.56f));

        float bracketW = 28f;
        float bracketH = 3f;
        float inset = sideWidth + 6f;
        float topY = 8f;
        float bottomY = Screen.height - 12f;
        Color bracket = new Color(reactiveAccent.r, reactiveAccent.g, reactiveAccent.b, Mathf.Lerp(0.20f, 0.42f, threat));
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

    private void EnsureUpgradeStyles()
    {
        if (upgradeKickerStyle != null &&
            upgradeTitleStyle != null &&
            upgradeDescriptionStyle != null &&
            upgradeButtonStyle != null &&
            upgradeTimerStyle != null &&
            Mathf.Abs(cachedUpgradeHudScaleForStyles - hudScale) < 0.001f)
        {
            return;
        }

        EnsureBossStateStyles();
        cachedUpgradeHudScaleForStyles = hudScale;

        upgradeKickerStyle = new GUIStyle(GUI.skin.label)
        {
            font = secondaryFont,
            fontSize = Mathf.RoundToInt(15f * hudScale),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Overflow
        };
        upgradeKickerStyle.normal.textColor = new Color(0.70f, 0.92f, 1f, 0.86f);

        upgradeTitleStyle = new GUIStyle(GUI.skin.label)
        {
            font = importantFont,
            fontSize = Mathf.RoundToInt(34f * hudScale),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Overflow
        };
        upgradeTitleStyle.normal.textColor = new Color(0.96f, 0.98f, 1f, 0.98f);

        upgradeDescriptionStyle = new GUIStyle(GUI.skin.label)
        {
            font = secondaryFont,
            fontSize = Mathf.RoundToInt(15f * hudScale),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft,
            clipping = TextClipping.Clip,
            wordWrap = true
        };
        upgradeDescriptionStyle.normal.textColor = new Color(0.82f, 0.90f, 1f, 0.92f);

        upgradeButtonStyle = new GUIStyle(GUI.skin.button)
        {
            font = importantFont,
            fontSize = Mathf.RoundToInt(17f * hudScale),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Clip,
            wordWrap = true
        };
        upgradeButtonStyle.normal.textColor = new Color(0.95f, 0.98f, 1f, 0.96f);
        upgradeButtonStyle.hover.textColor = Color.white;
        upgradeButtonStyle.active.textColor = new Color(1f, 0.90f, 0.58f, 1f);

        upgradeTimerStyle = new GUIStyle(GUI.skin.label)
        {
            font = importantFont,
            fontSize = Mathf.RoundToInt(18f * hudScale),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Overflow
        };
        upgradeTimerStyle.normal.textColor = new Color(0.94f, 0.98f, 1f, 0.96f);
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

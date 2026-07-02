using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Es el director general de una run. Coordina tiempos, dificultad, tutoriales, contratos, HUD y cierre de partida.
// No mueve actores por su cuenta: les marca el ritmo y recibe sus avisos para mantener todo sincronizado.
public class GameManager : MonoBehaviour
{
    // Estos tipos chicos agrupan datos que solo tienen sentido durante una run y evitan listas de variables sueltas.
    private struct ScorePopup
    {
        public int amount;
        public float age;
        public float lifetime;
        public float xJitter;
        public Color color;
    }

    private struct HudTopLayout
    {
        public Rect bar;
        public Rect metrics;
        public Rect sector;
        public Rect focus;
        public Rect dash;
        public Rect firewall;
        public Rect hijack;
    }

    private enum RunPhase
    {
        StartupDelay,
        Countdown,
        GoFlash,
        Active
    }

    private enum IntroTutorialStep
    {
        Movement,
        Parry,
        Firewall,
        Resources,
        ArenaEvents,
        Ready
    }

    private enum ContextTutorialKind
    {
        None,
        Movement,
        Parry,
        GhostDash,
        Firewall,
        ScorePickup,
        Powerup,
        Upgrade,
        Interface,
        ArenaEvent,
        Breach,
        BossState,
        StateHijackUnlock,
        StateHijack
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
        FirewallBurstStun,
        HazardResistance,
        DisplacementStabilizer,
        HazardFirewallCharge,
        VectorCore,
        EmergencyShield,
        ParryCapacitor
    }

    private enum RunContractKind
    {
        Survive,
        Score,
        Pickups,
        Parry,
        FirewallBurst
    }

    private enum ScriptedDirectorBeat
    {
        FirstArenaSignature,
        FirstObjective,
        FirstBreach,
        FreePlay
    }

    private struct UpgradeChoice
    {
        public PlayerUpgradeKind kind;
        public string title;
        public string description;
        public string category;
        public string rarity;
        public string icon;
        public string impact;
        public Color accent;
    }

    private struct RunContract
    {
        public RunContractKind kind;
        public string title;
        public string hint;
        public int target;
        public int progress;
        public int startScore;
        public float startedAt;
        public float duration;
        public int scoreReward;
        public int dataReward;
    }

    private struct EventPressureReservation
    {
        public string key;
        public float pressure;
        public float remainingSeconds;
        public float recoveryCooldown;
    }

    private readonly HashSet<string> contextArenaEventTutorialsShown = new HashSet<string>();

    [Header("Run State")]
    [SerializeField] private bool autoReloadSceneOnGameOver = false;
    [SerializeField] private float reloadDelay = 1.2f;
    [SerializeField] private float startupDelaySeconds = 1f;
    [SerializeField] private int countdownSeconds = 3;
    [SerializeField] private float goFlashSeconds = 0.4f;

    [Header("Difficulty")]
    [SerializeField] private float behaviorChangeInterval = 2.5f;
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
    [SerializeField] private float upgradeChoiceLimitSeconds = 21f;
    [SerializeField] private float upgradeLevelTwoChoiceSeconds = 17f;
    [SerializeField] private float upgradeLevelThreeChoiceSeconds = 14f;
    [SerializeField] private float upgradeMinimumChoiceSeconds = 6f;
    [SerializeField] private float upgradeEnterDuration = 0.32f;
    [SerializeField] private float upgradeExitDuration = 0.46f;

    [Header("Run Contracts")]
    [SerializeField] private bool enableRunContracts = true;
    [SerializeField] private float firstContractTime = 24f;
    [SerializeField] private float contractInterval = 42f;
    [SerializeField] private float contractDuration = 55f;
    [SerializeField] private int contractScoreReward = 24;
    [SerializeField] private int contractDataReward = 5;

    [Header("Progression Gates")]
    [SerializeField] private float bossSpecialStatesUnlockTime = 30f;
    [SerializeField] private float bossLevelTwoUnlockTime = 150f;
    [SerializeField] private float bossLevelThreeUnlockTime = 300f;
    [SerializeField] private float stateHijackUnlockTime = 150f;
    [SerializeField] private float mapEventsUnlockTime = 60f;
    [SerializeField] private float containmentPulseUnlockTime = 90f;

    [Header("Event Pressure Curve")]
    [SerializeField] private bool enableEventPressureBudget = true;
    [SerializeField] private float eventPressureInitialCap = 1.05f;
    [SerializeField] private float eventPressureMaxCap = 1.65f;
    [SerializeField] private float eventPressureSoloHeavyAllowance = 1.8f;
    [SerializeField] private float eventPressureRampStartTime = 60f;
    [SerializeField] private float eventPressureRampFullTime = 240f;
    [SerializeField] private float eventPressureRetryDelay = 1.35f;

    [Header("Hybrid Difficulty Director")]
    [SerializeField] private bool enableHybridDifficultyDirector = true;
    [SerializeField] private float firstArenaSignatureTime = 64f;
    [SerializeField] private float firstObjectiveBeatTime = 88f;
    [SerializeField] private float firstBreachBeatTime = 126f;
    [SerializeField] private float scriptedBeatReserveLead = 6f;
    [SerializeField] private float scriptedBeatTimeout = 20f;
    [SerializeField] private float sectorArrivalRecoverySeconds = 6f;
    [SerializeField] private float postUpgradeRecoverySeconds = 3f;
    [SerializeField] private float bossMilestoneProtectionLead = 5f;
    [SerializeField] private float bossMilestoneRecoverySeconds = 4f;
    [SerializeField] private float bossMajorSuppressionPressure = 0.6f;
    [SerializeField] private float boundedDifficultyMultiplierMax = 2.25f;

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

    [Header("Boss Level 2 Intro")]
    [SerializeField] private float bossLevelTwoIntroDuration = 2.45f;
    [SerializeField, Range(0f, 1f)] private float bossLevelTwoIntroBackdropOpacity = 0.54f;
    [SerializeField] private Color bossLevelTwoIntroPrimary = new Color(0.58f, 1f, 0.92f, 1f);
    [SerializeField] private Color bossLevelTwoIntroSecondary = new Color(1f, 0.42f, 0.92f, 1f);

    [Header("Boss Level 3 Intro")]
    [SerializeField] private float bossLevelThreeIntroDuration = 3.15f;
    [SerializeField, Range(0f, 1f)] private float bossLevelThreeIntroBackdropOpacity = 0.62f;
    [SerializeField] private Color bossLevelThreeIntroPrimary = new Color(1f, 0.34f, 0.74f, 1f);
    [SerializeField] private Color bossLevelThreeIntroSecondary = new Color(0.34f, 0.94f, 1f, 1f);

    [Header("HUD Atmosphere")]
    [SerializeField] private bool enableAmbientHudFrame = true;
    [SerializeField, Range(0.04f, 0.35f)] private float sideHudOpacity = 0.085f;
    [SerializeField, Range(0.02f, 0.22f)] private float sideHudAccentOpacity = 0.058f;
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
    public bool IsLocalVersus => LocalVersusModeStorage.IsLocalVersus;
    public string VersusWinnerLabel { get; private set; }
    public string VersusResultSubtitle { get; private set; }
    public bool IsUpgradeSelectionOpen => upgradeSelectionOpen;
    public bool IsIntroTutorialOpen => introTutorialOpen;
    public bool ShouldShowCursorForContextTutorial => contextTutorialOpen && IsClickValidatedContextTutorial(contextTutorialKind);
    public MetaProgressionStorage.RunReward LastMetaReward { get; private set; }
    public bool HasAwardedMetaReward { get; private set; }
    public int ContractDataBonusEarned => contractDataBonusEarned;
    public string CurrentOperationTitle => activeOperation.title;
    public float SurvivalTime { get; private set; }
    public float DifficultyMultiplier => Mathf.Lerp(1f, Mathf.Max(1f, boundedDifficultyMultiplierMax), GetDirectorDifficulty01());
    public int CurrentScore
    {
        get
        {
            int baseScore = Mathf.Max(0, Mathf.FloorToInt(SurvivalTime * Mathf.Max(0f, pointsPerSecond)) + bonusScore);
            return Mathf.Max(0, Mathf.RoundToInt(baseScore * Mathf.Max(1f, activeOperation.scoreMultiplier)));
        }
    }
    public string CurrentLevelTypeLabel => levelType;
    public string CurrentMapEventLabel => GetMapEventLabel();
    public string CurrentMapEventHint => GetThemedEventHint();
    public bool IsBreachSensitiveSuppressionActive => breachSensitiveSuppressionTimer > 0f;
    public bool AreBossSpecialStatesUnlocked => IsRunActive && !IsBreachSensitiveSuppressionActive && (devForceBossLevelTwo || devForceBossLevelThree || SurvivalTime >= Mathf.Max(0f, bossSpecialStatesUnlockTime));
    public bool AreBossLevelTwoStatesUnlocked => IsRunActive && !IsBreachSensitiveSuppressionActive && (devForceBossLevelTwo || devForceBossLevelThree || SurvivalTime >= Mathf.Max(bossSpecialStatesUnlockTime, bossLevelTwoUnlockTime));
    public bool AreBossLevelThreeStatesUnlocked => IsRunActive && !IsBreachSensitiveSuppressionActive && (devForceBossLevelThree || SurvivalTime >= Mathf.Max(bossLevelTwoUnlockTime, bossLevelThreeUnlockTime));
    public bool IsStateHijackUnlocked => IsRunActive && !IsBreachSensitiveSuppressionActive &&
        (devForceBossLevelTwo || devForceBossLevelThree || SurvivalTime >= Mathf.Max(bossLevelTwoUnlockTime, stateHijackUnlockTime));
    public bool AreMapEventsUnlocked => IsRunActive && SurvivalTime >= Mathf.Max(0f, mapEventsUnlockTime);
    public bool IsContainmentPulseUnlocked => IsRunActive && !IsBreachSensitiveSuppressionActive && SurvivalTime >= Mathf.Max(0f, containmentPulseUnlockTime);
    public bool IsContainmentPulsePressureActive => chaosDirector != null && chaosDirector.IsContainmentPulsePressureActive;
    public float EventPressureRetryDelay => Mathf.Max(0.25f, eventPressureRetryDelay);
    public float CurrentEventPressureLoad => GetCurrentEventPressureLoad();
    public float CurrentEventPressureCap => GetCurrentEventPressureCap();
    public bool ShouldSuppressBossMajorStates => enableHybridDifficultyDirector &&
        (GetCurrentEventPressureLoad() >= Mathf.Max(0.1f, bossMajorSuppressionPressure) || SurvivalTime < directorRecoveryUntil);

    public float CurrentBehaviorChangeInterval
    {
        get
        {
            float interval = Mathf.Max(0.5f, behaviorChangeInterval);
            float curveMultiplier = Mathf.Lerp(1.08f, 0.82f, GetDirectorDifficulty01());
            float sectorMultiplier = arenaGenerator != null
                ? Mathf.Clamp(1f / Mathf.Max(1f, arenaGenerator.SectorPressureMultiplier), 0.76f, 1f)
                : 1f;
            if (!chaosTempoEnabled)
            {
                return interval * curveMultiplier * sectorMultiplier;
            }

            return interval * curveMultiplier * Mathf.Max(0.1f, chaosBehaviorIntervalMultiplier) * sectorMultiplier;
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
    private GUIStyle upgradeMetaStyle;
    private GUIStyle upgradeIconStyle;
    private GUIStyle upgradeImpactStyle;
    private GUIStyle upgradeCardTitleStyle;
    private GUIStyle tutorialBodyStyle;
    private GUIStyle tutorialHeaderStyle;
    private GUIStyle tutorialTinyStyle;
    private Font importantFont;
    private Font secondaryFont;
    private string lastBossStateRaw;
    private float statePulseOverlayTimer;
    private float bossStateBannerTimer;
    private string bossStateBannerRaw;
    private bool bossLevelTwoIntroPlayed;
    private float bossLevelTwoIntroTimer;
    private bool bossLevelThreeIntroPlayed;
    private float bossLevelThreeIntroTimer;
    private int bonusScore;
    private float hudScale = 1f;
    private float cachedHudScaleForStyles = -1f;
    private float cachedUpgradeHudScaleForStyles = -1f;
    private float displayedScore;
    private float scorePulseTimer;
    private float smoothedThreat;
    private readonly List<ScorePopup> scorePopups = new List<ScorePopup>();
    private readonly List<UpgradeChoice> currentUpgradeChoices = new List<UpgradeChoice>();
    private readonly List<EventPressureReservation> eventPressureReservations = new List<EventPressureReservation>();
    private RunContract activeContract;
    private bool hasActiveContract;
    private float nextUpgradeTime;
    private float nextContractTime;
    private float contractCompletePulseTimer;
    private string lastContractCompletionLabel;
    private int contractDataBonusEarned;
    private ContainmentOperationStorage.OperationDefinition activeOperation;
    private bool operationRunRecorded;
    private string achievementToastTitle;
    private string achievementToastDescription;
    private string achievementToastHeader;
    private int achievementToastReward;
    private float achievementToastTimer;
    private float stateHijackNoticeTimer;
    private string stateHijackNoticeLabel;
    private string stateHijackNoticeVerb;
    private string stateHijackNoticeHint;
    private Color stateHijackNoticeColor = Color.white;
    private bool upgradeSelectionOpen;
    private bool upgradeSelectionClosing;
    private float upgradeSelectionAge;
    private float upgradeTimeRemaining;
    private float upgradeCurrentLimitSeconds;
    private float upgradeExitTimer;
    private int upgradeSelectedIndex = -1;
    private Color upgradeSelectedAccent = Color.white;
    private int upgradePickCount;
    private bool playerDefeatSequenceRunning;
    private Coroutine playerDefeatSequenceRoutine;
    private float breachSensitiveSuppressionTimer;
    private float eventPressureCooldownTimer;
    private ScriptedDirectorBeat scriptedDirectorBeat;
    private float directorRecoveryUntil;
    private bool sectorSignatureBeatPending;
    private float sectorSignatureBeatDeadline;
    private bool introTutorialOpen;
    private IntroTutorialStep introTutorialStep;
    private float introTutorialStepProgress;
    private float introTutorialStepTimer;
    private float introTutorialActionFlash;
    private Vector2 introTutorialDemoPlayer = new Vector2(0.34f, 0.58f);
    private bool introMoveWPressed;
    private bool introMoveAPressed;
    private bool introMoveSPressed;
    private bool introMoveDPressed;
    private bool contextTutorialOpen;
    private ContextTutorialKind contextTutorialKind;
    private float contextTutorialProgress;
    private float contextTutorialTimer;
    private float contextTutorialActionFlash;
    private int contextInterfacePhase;
    private float previousTimeScaleBeforeContext = 1f;
    private string contextTutorialEventKey;
    private string contextTutorialEventLabel;
    private string contextTutorialEventHint;
    private string pendingMapEventTutorialKey;
    private string pendingMapEventTutorialLabel;
    private string pendingMapEventTutorialHint;
    private bool contextMoveWPressed;
    private bool contextMoveAPressed;
    private bool contextMoveSPressed;
    private bool contextMoveDPressed;
    private bool contextMovementShown;
    private bool contextParryShown;
    private bool contextGhostDashShown;
    private bool contextFirewallShown;
    private bool contextScorePickupShown;
    private bool contextPowerupShown;
    private bool contextUpgradeShown;
    private bool contextInterfaceShown;
    private bool contextBreachShown;
    private bool contextStateHijackUnlockShown;
    private bool operationPlayerModifiersApplied;
    private bool operationEnemyModifiersApplied;
    private bool devForceBossLevelTwo;
    private bool devForceBossLevelThree;
    private bool devFastRunLoops;
    private bool devSkipCountdown;
    private float labRunTime;
    private float storageRunTime;
    private float ruptureRunTime;
    private LocalVersusManager localVersusManager;

    private void Awake()
    {
        UserSettings.EnsureContextTutorialProgressVersion();
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
        EnsureLocalVersusSetup();
    }

    private void OnDestroy()
    {
        if (introTutorialOpen || contextTutorialOpen)
        {
            Time.timeScale = 1f;
        }

        PlayerController.SetTutorialInputLocked(false);
    }

    private void Update()
    {
        // Este Update funciona como una mesa de control: avanza la fase actual y delega cada sistema a su método.
        // La lógica pesada queda afuera para que sea fácil ver qué cosas se actualizan y en qué orden.
        UpdateHudReactiveState();

        if (arenaGenerator == null)
        {
            RefreshLevelType();
        }
        else if (levelType != arenaGenerator.ActiveThemeLabel)
        {
            levelType = arenaGenerator.ActiveThemeLabel;
            themedEventStatusProvider = FindThemedEventStatusProvider();
        }

        if (enemyController == null)
        {
            enemyController = FindAnyObjectByType<EnemyController>();
        }
        if (playerController == null)
        {
            playerController = FindAnyObjectByType<PlayerController>();
        }
        EnsureLocalVersusSetup();
        if (IsLocalVersus)
        {
            if (IsGameOver)
            {
                HandleOptionalReload();
                return;
            }

            if (runPhase != RunPhase.Active &&
                localVersusManager != null &&
                localVersusManager.IntroductionComplete)
            {
                UpdateStartupSequence();
            }

            return;
        }
        ApplyOperationPlayerModifiersOnce();
        ApplyOperationEnemyModifiersOnce();
        if (chaosDirector == null)
        {
            chaosDirector = FindAnyObjectByType<ArenaChaosDirector>();
        }
        if (!IsThemedEventStatusProviderValid())
        {
            themedEventStatusProvider = FindThemedEventStatusProvider();
        }

        if (introTutorialOpen)
        {
            UpdateIntroTutorialState();
            return;
        }

        if (contextTutorialOpen)
        {
            UpdateContextTutorialState();
            return;
        }

        if (enemyController != null)
        {
            TrackBossTempoPulse();
            TrackBossLevelTwoIntro();
            TrackBossLevelThreeIntro();
            UpdateDeveloperBossShortcuts();
        }

        if (statePulseOverlayTimer > 0f)
        {
            statePulseOverlayTimer -= Time.deltaTime;
        }
        if (bossStateBannerTimer > 0f)
        {
            bossStateBannerTimer -= Time.deltaTime;
        }
        if (bossLevelTwoIntroTimer > 0f)
        {
            bossLevelTwoIntroTimer -= Time.deltaTime;
        }
        if (bossLevelThreeIntroTimer > 0f)
        {
            bossLevelThreeIntroTimer -= Time.deltaTime;
        }
        if (achievementToastTimer > 0f)
        {
            achievementToastTimer -= Time.unscaledDeltaTime;
        }
        if (stateHijackNoticeTimer > 0f)
        {
            stateHijackNoticeTimer -= Time.unscaledDeltaTime;
        }
        if (breachSensitiveSuppressionTimer > 0f)
        {
            breachSensitiveSuppressionTimer -= Time.deltaTime;
        }
        TickEventPressureBudget();
        UpdateDifficultyDirectorState();

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

        UpdateContextTutorialTriggers();

        SurvivalTime += Time.deltaTime;
        TrackArenaRunTime(Time.deltaTime);
        TrackSurvivalAchievement();
        TrySetDailyChallengeProgress(DailyChallengeStorage.ChallengeKind.Survival, Mathf.FloorToInt(SurvivalTime));
        TrySetDailyChallengeProgress(DailyChallengeStorage.ChallengeKind.Score, CurrentScore);
        UpdateRunContracts();
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

        if (IsLocalVersus)
        {
            TriggerVersusGameOver("ANOMALIA", "La anomalia alcanzo al corredor.");
            return;
        }

        IsGameOver = true;
        runPhase = RunPhase.Active;
        RecordOperationRun();
        if (!HasAwardedMetaReward)
        {
            LastMetaReward = MetaProgressionStorage.AwardRun(CurrentScore, SurvivalTime, levelType, contractDataBonusEarned);
            HasAwardedMetaReward = true;
            TrackPerformanceAchievements(LastMetaReward.performanceGrade);
        }

        Debug.Log($"GAME OVER | Time Survived: {SurvivalTime:F2}s");
    }

    public void SetVersusElapsedTime(float elapsed)
    {
        if (IsLocalVersus && !IsGameOver)
        {
            SurvivalTime = Mathf.Max(0f, elapsed);
        }
    }

    public void TriggerVersusGameOver(string winner, string subtitle)
    {
        if (IsGameOver)
        {
            return;
        }

        VersusWinnerLabel = winner;
        VersusResultSubtitle = subtitle;
        IsGameOver = true;
        runPhase = RunPhase.Active;
        Debug.Log($"LOCAL VERSUS OVER | Winner: {winner}");
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

        NotifyContractProgress(RunContractKind.Score, amount);
        TrySetDailyChallengeProgress(DailyChallengeStorage.ChallengeKind.Score, CurrentScore);
    }

    public void NotifyScorePickupCollected(int scoreValue)
    {
        TryOpenContextTutorial(ContextTutorialKind.ScorePickup);

        NotifyContractProgress(RunContractKind.Pickups, 1);
        AdvanceCounterAchievements(
            AchievementStorage.CounterPickups,
            1,
            AchievementStorage.PickupsTwentyFiveId,
            AchievementStorage.PickupsOneHundredId,
            AchievementStorage.PickupsTwoHundredFiftyId,
            AchievementStorage.PickupsFiveHundredId);
        TryAddDailyChallengeProgress(DailyChallengeStorage.ChallengeKind.Pickups, 1);
    }

    public void NotifyPowerupCollected()
    {
        TryOpenContextTutorial(ContextTutorialKind.Powerup);
    }

    public void NotifyParrySuccess()
    {
        NotifyContractProgress(RunContractKind.Parry, 1);
        AdvanceCounterAchievements(
            AchievementStorage.CounterParries,
            1,
            AchievementStorage.ParryFiveId,
            AchievementStorage.ParryTwentyFiveId,
            AchievementStorage.ParrySeventyFiveId,
            AchievementStorage.ParryOneHundredFiftyId);
        TryAddDailyChallengeProgress(DailyChallengeStorage.ChallengeKind.Parry, 1);
    }

    public void NotifyStateHijackCaptured(string abilityLabel, string abilityHint, Color color)
    {
        stateHijackNoticeLabel = abilityLabel;
        stateHijackNoticeVerb = "ESTADO CAPTURADO";
        stateHijackNoticeHint = abilityHint;
        stateHijackNoticeColor = color;
        stateHijackNoticeTimer = 2.8f;
    }

    public void NotifyStateHijackActivated(string abilityLabel, Color color)
    {
        stateHijackNoticeLabel = abilityLabel;
        stateHijackNoticeVerb = "HIJACK EJECUTADO";
        stateHijackNoticeHint = string.Empty;
        stateHijackNoticeColor = color;
        stateHijackNoticeTimer = 1.8f;
    }

    public void NotifyFirewallBurstActivated()
    {
        AdvanceCounterAchievements(
            AchievementStorage.CounterFirewallBursts,
            1,
            AchievementStorage.FirstFirewallBurstId,
            AchievementStorage.FirewallBurstTenId,
            AchievementStorage.FirewallBurstTwentyFiveId,
            AchievementStorage.FirewallBurstFiftyId);
        NotifyContractProgress(RunContractKind.FirewallBurst, 1);
        TryAddDailyChallengeProgress(DailyChallengeStorage.ChallengeKind.FirewallBurst, 1);
    }

    public void NotifyBreachEscaped()
    {
        AdvanceCounterAchievements(
            AchievementStorage.CounterBreaches,
            1,
            AchievementStorage.BreachFirstId,
            AchievementStorage.BreachThreeId,
            AchievementStorage.BreachSevenId);
    }

    public void NotifyBreachTutorialOpportunity()
    {
        TryOpenContextTutorial(ContextTutorialKind.Breach);
    }

    public void NotifyThemedMapEventStarted(string eventLabel, string eventHint)
    {
        if (string.IsNullOrWhiteSpace(eventLabel))
        {
            return;
        }

        pendingMapEventTutorialLabel = eventLabel;
        pendingMapEventTutorialHint = eventHint ?? string.Empty;
        pendingMapEventTutorialKey = $"{levelType}:{NormalizeTutorialIdentity(eventLabel)}";
    }

    public void NotifyRuptureEchoTrapSuccess()
    {
        TryUnlockAchievement(AchievementStorage.RuptureEchoTrapId);
    }

    private void RecordOperationRun()
    {
        if (operationRunRecorded || activeOperation.id == ContainmentOperationStorage.NoneId)
        {
            return;
        }

        operationRunRecorded = true;
        TrackOperationAchievement(activeOperation.id);
        AdvanceCounterAchievements(
            AchievementStorage.CounterOperations,
            1,
            AchievementStorage.OperationsThreeId,
            AchievementStorage.OperationsTenId);
    }

    // --- Contratos de la run -------------------------------------------------
    // Acá se crea el desafío activo, se sigue su progreso y se decide si terminó bien o venció.
    private void UpdateRunContracts()
    {
        if (!enableRunContracts || IsGameOver || !IsRunActive)
        {
            return;
        }

        if (contractCompletePulseTimer > 0f)
        {
            contractCompletePulseTimer -= Time.deltaTime;
        }

        if (!hasActiveContract)
        {
            if (SurvivalTime >= nextContractTime)
            {
                StartRunContract();
            }

            return;
        }

        if (activeContract.kind == RunContractKind.Survive)
        {
            int progress = Mathf.FloorToInt(Mathf.Max(0f, SurvivalTime - activeContract.startedAt));
            activeContract.progress = Mathf.Clamp(progress, 0, activeContract.target);
            if (activeContract.progress >= activeContract.target)
            {
                CompleteRunContract();
                return;
            }
        }
        else if (activeContract.kind == RunContractKind.Score)
        {
            activeContract.progress = Mathf.Clamp(CurrentScore - activeContract.startScore, 0, activeContract.target);
            if (activeContract.progress >= activeContract.target)
            {
                CompleteRunContract();
                return;
            }
        }

        float elapsed = SurvivalTime - activeContract.startedAt;
        if (elapsed >= activeContract.duration)
        {
            FailRunContract();
        }
    }

    private void StartRunContract()
    {
        activeContract = BuildRunContract(PickRunContractKind());
        activeContract.startedAt = SurvivalTime;
        activeContract.startScore = CurrentScore;
        activeContract.duration = Mathf.Max(10f, contractDuration);
        activeContract.scoreReward = Mathf.Max(0, contractScoreReward);
        activeContract.dataReward = Mathf.Max(0, contractDataReward);
        hasActiveContract = true;
    }

    private RunContractKind PickRunContractKind()
    {
        RunContractKind[] options =
        {
            RunContractKind.Survive,
            RunContractKind.Score,
            RunContractKind.Pickups,
            RunContractKind.Parry,
            RunContractKind.FirewallBurst
        };

        return options[Random.Range(0, options.Length)];
    }

    private RunContract BuildRunContract(RunContractKind kind)
    {
        switch (kind)
        {
            case RunContractKind.Score:
                return new RunContract
                {
                    kind = kind,
                    title = "Recolecta datos",
                    hint = "Suma puntos antes de que expire",
                    target = 120
                };
            case RunContractKind.Pickups:
                return new RunContract
                {
                    kind = kind,
                    title = "Barrido de datos",
                    hint = "Recolecta pickups de score",
                    target = 7
                };
            case RunContractKind.Parry:
                return new RunContract
                {
                    kind = kind,
                    title = "Rechazo limpio",
                    hint = "Conecta parries exitosos",
                    target = 3
                };
            case RunContractKind.FirewallBurst:
                return new RunContract
                {
                    kind = kind,
                    title = "Descarga Firewall",
                    hint = "Activa un Burst durante el contrato",
                    target = 1
                };
            default:
                return new RunContract
                {
                    kind = RunContractKind.Survive,
                    title = "Mantener contencion",
                    hint = "Sobrevive hasta estabilizar la zona",
                    target = 35
                };
        }
    }

    private void NotifyContractProgress(RunContractKind kind, int amount)
    {
        if (!hasActiveContract || amount <= 0 || activeContract.kind != kind)
        {
            return;
        }

        activeContract.progress = Mathf.Clamp(activeContract.progress + amount, 0, activeContract.target);
        if (activeContract.progress >= activeContract.target)
        {
            CompleteRunContract();
        }
    }

    private void CompleteRunContract()
    {
        if (!hasActiveContract)
        {
            return;
        }

        lastContractCompletionLabel = activeContract.title;
        contractCompletePulseTimer = 1.8f;
        contractDataBonusEarned += Mathf.Max(0, activeContract.dataReward);
        int reward = Mathf.Max(0, activeContract.scoreReward);
        hasActiveContract = false;
        nextContractTime = SurvivalTime + Mathf.Max(10f, contractInterval);

        if (reward > 0)
        {
            AddScore(reward);
        }

        AdvanceCounterAchievements(
            AchievementStorage.CounterContracts,
            1,
            AchievementStorage.FirstContractId,
            AchievementStorage.ContractsFiveId,
            AchievementStorage.ContractsFifteenId,
            AchievementStorage.ContractsThirtyId);
        TryAddDailyChallengeProgress(DailyChallengeStorage.ChallengeKind.Contract, 1);

        GlitchAudioManager.PlayUpgradeSelect();
    }

    private void FailRunContract()
    {
        hasActiveContract = false;
        nextContractTime = SurvivalTime + Mathf.Max(8f, contractInterval * 0.65f);
    }

    private void TrackSurvivalAchievement()
    {
        if (SurvivalTime >= 180f)
        {
            TryUnlockAchievement(AchievementStorage.SurviveThreeMinutesId);
        }
        if (SurvivalTime >= 300f)
        {
            TryUnlockAchievement(AchievementStorage.SurviveFiveMinutesId);
        }
        if (SurvivalTime >= 480f)
        {
            TryUnlockAchievement(AchievementStorage.SurviveEightMinutesId);
        }

        if (labRunTime >= 90f)
        {
            TryUnlockAchievement(AchievementStorage.LabSurviveNinetyId);
        }
        if (labRunTime >= 180f)
        {
            TryUnlockAchievement(AchievementStorage.LabSurviveThreeMinutesId);
        }
        if (storageRunTime >= 90f)
        {
            TryUnlockAchievement(AchievementStorage.StorageSurviveNinetyId);
        }
        if (storageRunTime >= 180f)
        {
            TryUnlockAchievement(AchievementStorage.StorageSurviveThreeMinutesId);
        }
        if (ruptureRunTime >= 90f)
        {
            TryUnlockAchievement(AchievementStorage.RuptureSurviveNinetyId);
        }
        if (ruptureRunTime >= 180f)
        {
            TryUnlockAchievement(AchievementStorage.RuptureSurviveThreeMinutesId);
        }
    }

    private void TrackArenaRunTime(float deltaTime)
    {
        float dt = Mathf.Max(0f, deltaTime);
        if (levelType == "Lab")
        {
            labRunTime += dt;
        }
        else if (levelType == "Storage")
        {
            storageRunTime += dt;
        }
        else if (levelType == "Rupture")
        {
            ruptureRunTime += dt;
        }
    }

    private void TryAdvanceAchievementCounter(string counterId, int amount, int target, string achievementId)
    {
        AchievementStorage.AchievementDefinition achievement;
        if (AchievementStorage.AddCounterAndTryUnlock(counterId, amount, target, achievementId, out achievement))
        {
            ShowAchievementToast(achievement);
        }
    }

    private void AdvanceCounterAchievements(string counterId, int amount, params string[] achievementIds)
    {
        if (amount > 0)
        {
            AchievementStorage.AddCounter(counterId, amount);
        }

        if (achievementIds == null)
        {
            return;
        }

        for (int i = 0; i < achievementIds.Length; i++)
        {
            AchievementStorage.AchievementDefinition achievement;
            if (AchievementStorage.TryUnlockCounterAchievement(counterId, achievementIds[i], out achievement))
            {
                ShowAchievementToast(achievement);
            }
        }
    }

    private void TrackOperationAchievement(string operationId)
    {
        switch (operationId)
        {
            case ContainmentOperationStorage.FirewallId:
                TryUnlockAchievement(AchievementStorage.OperationFirewallId);
                break;
            case ContainmentOperationStorage.ExtractionId:
                TryUnlockAchievement(AchievementStorage.OperationExtractionId);
                break;
            case ContainmentOperationStorage.ContractId:
                TryUnlockAchievement(AchievementStorage.OperationContractId);
                break;
            case ContainmentOperationStorage.BreachId:
                TryUnlockAchievement(AchievementStorage.OperationBreachId);
                break;
            case ContainmentOperationStorage.AmbientOverdriveId:
                TryUnlockAchievement(AchievementStorage.OperationAmbientId);
                break;
        }
    }

    private void TrackPerformanceAchievements(string grade)
    {
        if (string.IsNullOrWhiteSpace(grade))
        {
            return;
        }

        if (grade == "S")
        {
            TryUnlockAchievement(AchievementStorage.GradeSId);
            TryUnlockAchievement(AchievementStorage.GradeAId);
        }
        else if (grade == "A")
        {
            TryUnlockAchievement(AchievementStorage.GradeAId);
        }
    }

    private void TryUnlockAchievement(string achievementId)
    {
        AchievementStorage.AchievementDefinition achievement;
        if (AchievementStorage.TryUnlock(achievementId, out achievement))
        {
            ShowAchievementToast(achievement);
        }
    }

    private void TryAddDailyChallengeProgress(DailyChallengeStorage.ChallengeKind kind, int amount)
    {
        DailyChallengeStorage.DailyChallenge challenge;
        if (DailyChallengeStorage.AddProgress(kind, amount, out challenge))
        {
            ShowDailyChallengeToast(challenge);
        }
    }

    private void TrySetDailyChallengeProgress(DailyChallengeStorage.ChallengeKind kind, int progress)
    {
        DailyChallengeStorage.DailyChallenge challenge;
        if (DailyChallengeStorage.SetProgress(kind, progress, out challenge))
        {
            ShowDailyChallengeToast(challenge);
        }
    }

    private void ShowAchievementToast(AchievementStorage.AchievementDefinition achievement)
    {
        achievementToastHeader = "LOGRO DESBLOQUEADO";
        achievementToastTitle = achievement.title;
        achievementToastDescription = achievement.description;
        achievementToastReward = Mathf.Max(0, achievement.dataReward);
        achievementToastTimer = 4.2f;
        GlitchAudioManager.PlayUpgradeSelect();
    }

    private void ShowDailyChallengeToast(DailyChallengeStorage.DailyChallenge challenge)
    {
        achievementToastHeader = "OPERACION COMPLETADA";
        achievementToastTitle = challenge.title;
        achievementToastDescription = challenge.description;
        achievementToastReward = Mathf.Max(0, challenge.dataReward);
        achievementToastTimer = 4.2f;
        GlitchAudioManager.PlayUpgradeSelect();
    }

    // --- Director de dificultad y presupuesto de eventos --------------------
    // Los eventos piden "presupuesto" antes de arrancar. Así evitamos que tres sistemas intensos exploten juntos.
    public void SuppressBreachSensitiveSystems(float seconds)
    {
        breachSensitiveSuppressionTimer = Mathf.Max(breachSensitiveSuppressionTimer, Mathf.Max(0f, seconds));
    }

    public bool TryReserveEventPressure(string eventKey, float pressureCost, float expectedDuration, float recoveryCooldown)
    {
        if (!IsRunActive)
        {
            return false;
        }

        string key = string.IsNullOrWhiteSpace(eventKey) ? "event" : eventKey;
        for (int i = 0; i < eventPressureReservations.Count; i++)
        {
            if (eventPressureReservations[i].key == key)
            {
                return true;
            }
        }

        if (!CanDifficultyDirectorReserve(key, expectedDuration, pressureCost))
        {
            return false;
        }

        if (!enableEventPressureBudget)
        {
            RegisterDifficultyDirectorReservation(key);
            return true;
        }

        float pressure = Mathf.Max(0f, pressureCost);
        float currentLoad = GetCurrentEventPressureLoad();
        float cap = GetCurrentEventPressureCap();
        bool fitsCurrentCap = currentLoad + pressure <= cap;
        bool fitsAsSoloHeavy = currentLoad <= 0.001f && pressure <= Mathf.Max(cap, eventPressureSoloHeavyAllowance);
        if (eventPressureCooldownTimer > 0f || (!fitsCurrentCap && !fitsAsSoloHeavy))
        {
            return false;
        }

        eventPressureReservations.Add(new EventPressureReservation
        {
            key = key,
            pressure = pressure,
            remainingSeconds = Mathf.Max(0.05f, expectedDuration),
            recoveryCooldown = Mathf.Max(0f, recoveryCooldown)
        });

        RegisterDifficultyDirectorReservation(key);

        return true;
    }

    public void ReleaseEventPressure(string eventKey, float recoveryCooldown)
    {
        if (!enableEventPressureBudget)
        {
            return;
        }

        string key = string.IsNullOrWhiteSpace(eventKey) ? "event" : eventKey;
        bool removed = false;
        for (int i = eventPressureReservations.Count - 1; i >= 0; i--)
        {
            if (eventPressureReservations[i].key != key)
            {
                continue;
            }

            eventPressureReservations.RemoveAt(i);
            removed = true;
        }

        if (removed)
        {
            eventPressureCooldownTimer = Mathf.Max(eventPressureCooldownTimer, Mathf.Max(0f, recoveryCooldown));
            directorRecoveryUntil = Mathf.Max(directorRecoveryUntil, SurvivalTime + Mathf.Max(0f, recoveryCooldown));
        }
    }

    public void NotifySectorArrivalForDifficultyDirector()
    {
        if (!enableHybridDifficultyDirector)
        {
            return;
        }

        sectorSignatureBeatPending = true;
        directorRecoveryUntil = Mathf.Max(directorRecoveryUntil, SurvivalTime + Mathf.Max(0f, sectorArrivalRecoverySeconds));
        sectorSignatureBeatDeadline = directorRecoveryUntil + Mathf.Max(5f, scriptedBeatTimeout);
    }

    private void UpdateDifficultyDirectorState()
    {
        if (!enableHybridDifficultyDirector)
        {
            return;
        }

        if (sectorSignatureBeatPending && SurvivalTime >= sectorSignatureBeatDeadline)
        {
            sectorSignatureBeatPending = false;
        }

        float timeout = Mathf.Max(5f, scriptedBeatTimeout);
        if (scriptedDirectorBeat == ScriptedDirectorBeat.FirstArenaSignature &&
            SurvivalTime >= Mathf.Max(mapEventsUnlockTime, firstArenaSignatureTime) + timeout)
        {
            scriptedDirectorBeat = ScriptedDirectorBeat.FirstObjective;
        }
        if (scriptedDirectorBeat == ScriptedDirectorBeat.FirstObjective &&
            SurvivalTime >= Mathf.Max(firstArenaSignatureTime, firstObjectiveBeatTime) + timeout)
        {
            scriptedDirectorBeat = ScriptedDirectorBeat.FirstBreach;
        }
        if (scriptedDirectorBeat == ScriptedDirectorBeat.FirstBreach &&
            SurvivalTime >= Mathf.Max(firstObjectiveBeatTime, firstBreachBeatTime) + timeout)
        {
            scriptedDirectorBeat = ScriptedDirectorBeat.FreePlay;
        }
    }

    private bool CanDifficultyDirectorReserve(string eventKey, float expectedDuration, float pressureCost)
    {
        if (!enableHybridDifficultyDirector)
        {
            return true;
        }

        if (SurvivalTime < directorRecoveryUntil)
        {
            return false;
        }

        bool themeEvent = IsThemeEventKey(eventKey);
        bool objectiveEvent = eventKey == "ChaosObjectiveNodes";
        bool breachEvent = eventKey == "ChaosBreach";
        float duration = Mathf.Max(0f, expectedDuration);

        if (!breachEvent && Mathf.Max(0f, pressureCost) >= 0.8f &&
            enemyController != null && enemyController.IsCurrentStateMajor)
        {
            return false;
        }

        if (!breachEvent && WouldOverlapBossMilestone(duration))
        {
            return false;
        }

        if (sectorSignatureBeatPending)
        {
            return themeEvent;
        }

        switch (scriptedDirectorBeat)
        {
            case ScriptedDirectorBeat.FirstArenaSignature:
                return SurvivalTime >= Mathf.Max(mapEventsUnlockTime, firstArenaSignatureTime) && themeEvent;

            case ScriptedDirectorBeat.FirstObjective:
                return CanReserveBeforeOrAtScriptedBeat(
                    objectiveEvent,
                    Mathf.Max(firstArenaSignatureTime, firstObjectiveBeatTime),
                    duration);

            case ScriptedDirectorBeat.FirstBreach:
                return CanReserveBeforeOrAtScriptedBeat(
                    breachEvent,
                    Mathf.Max(firstObjectiveBeatTime, firstBreachBeatTime),
                    duration);

            default:
                return true;
        }
    }

    private bool CanReserveBeforeOrAtScriptedBeat(bool requestedBeat, float beatTime, float expectedDuration)
    {
        if (SurvivalTime >= beatTime)
        {
            return requestedBeat;
        }

        if (requestedBeat)
        {
            return false;
        }

        float reserveFrom = beatTime - Mathf.Max(0f, scriptedBeatReserveLead);
        return SurvivalTime < reserveFrom && SurvivalTime + expectedDuration < reserveFrom;
    }

    private void RegisterDifficultyDirectorReservation(string eventKey)
    {
        bool themeEvent = IsThemeEventKey(eventKey);
        if (sectorSignatureBeatPending && themeEvent)
        {
            sectorSignatureBeatPending = false;
            return;
        }

        if (scriptedDirectorBeat == ScriptedDirectorBeat.FirstArenaSignature && themeEvent)
        {
            scriptedDirectorBeat = ScriptedDirectorBeat.FirstObjective;
        }
        else if (scriptedDirectorBeat == ScriptedDirectorBeat.FirstObjective && eventKey == "ChaosObjectiveNodes")
        {
            scriptedDirectorBeat = ScriptedDirectorBeat.FirstBreach;
        }
        else if (scriptedDirectorBeat == ScriptedDirectorBeat.FirstBreach && eventKey == "ChaosBreach")
        {
            scriptedDirectorBeat = ScriptedDirectorBeat.FreePlay;
        }
    }

    private bool WouldOverlapBossMilestone(float expectedDuration)
    {
        return WouldOverlapMilestone(bossLevelTwoUnlockTime, expectedDuration) ||
               WouldOverlapMilestone(bossLevelThreeUnlockTime, expectedDuration);
    }

    private bool WouldOverlapMilestone(float milestone, float expectedDuration)
    {
        float lead = Mathf.Max(0f, bossMilestoneProtectionLead);
        float recovery = Mathf.Max(0f, bossMilestoneRecoverySeconds);
        return SurvivalTime <= milestone + recovery && SurvivalTime + expectedDuration >= milestone - lead;
    }

    private static bool IsThemeEventKey(string eventKey)
    {
        return !string.IsNullOrWhiteSpace(eventKey) && eventKey.StartsWith("Theme");
    }

    private void TickEventPressureBudget()
    {
        if (!enableEventPressureBudget)
        {
            eventPressureReservations.Clear();
            eventPressureCooldownTimer = 0f;
            return;
        }

        float dt = Time.deltaTime;
        if (eventPressureCooldownTimer > 0f)
        {
            eventPressureCooldownTimer = Mathf.Max(0f, eventPressureCooldownTimer - dt);
        }

        for (int i = eventPressureReservations.Count - 1; i >= 0; i--)
        {
            EventPressureReservation reservation = eventPressureReservations[i];
            reservation.remainingSeconds -= dt;
            if (reservation.remainingSeconds <= 0f)
            {
                eventPressureCooldownTimer = Mathf.Max(eventPressureCooldownTimer, reservation.recoveryCooldown);
                directorRecoveryUntil = Mathf.Max(directorRecoveryUntil, SurvivalTime + reservation.recoveryCooldown);
                eventPressureReservations.RemoveAt(i);
                continue;
            }

            eventPressureReservations[i] = reservation;
        }
    }

    private float GetCurrentEventPressureLoad()
    {
        float load = 0f;
        for (int i = 0; i < eventPressureReservations.Count; i++)
        {
            load += Mathf.Max(0f, eventPressureReservations[i].pressure);
        }

        return load;
    }

    private float GetCurrentEventPressureCap()
    {
        if (!enableEventPressureBudget)
        {
            return float.PositiveInfinity;
        }

        float initial = Mathf.Max(0.1f, eventPressureInitialCap);
        float max = Mathf.Max(initial, eventPressureMaxCap);
        float start = Mathf.Max(0f, eventPressureRampStartTime);
        float full = Mathf.Max(start + 1f, eventPressureRampFullTime);
        float timeRamp = Mathf.InverseLerp(start, full, SurvivalTime);
        float t = Mathf.Min(timeRamp, GetDirectorDifficulty01());
        float cap = Mathf.Lerp(initial, max, Mathf.SmoothStep(0f, 1f, t));
        if (activeOperation.id == ContainmentOperationStorage.AmbientOverdriveId)
        {
            cap *= 0.82f;
        }
        return Mathf.Max(initial * 0.85f, cap);
    }

    private float GetDirectorDifficulty01()
    {
        float time = Mathf.Max(0f, SurvivalTime);
        float special = Mathf.Max(1f, bossSpecialStatesUnlockTime);
        float levelTwo = Mathf.Max(special + 1f, bossLevelTwoUnlockTime);
        float levelThree = Mathf.Max(levelTwo + 1f, bossLevelThreeUnlockTime);
        float normalized;

        if (time < special)
        {
            normalized = Mathf.Lerp(0f, 0.18f, Mathf.SmoothStep(0f, 1f, time / special));
        }
        else if (time < levelTwo)
        {
            normalized = Mathf.Lerp(0.18f, 0.55f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(special, levelTwo, time)));
        }
        else if (time < levelThree)
        {
            normalized = Mathf.Lerp(0.55f, 0.82f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(levelTwo, levelThree, time)));
        }
        else
        {
            normalized = Mathf.Lerp(0.82f, 1f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(levelThree, levelThree + 180f, time)));
        }

        int extraSectors = arenaGenerator != null ? Mathf.Max(0, arenaGenerator.SectorLevel - 1) : 0;
        return Mathf.Clamp01(normalized + Mathf.Min(0.14f, extraSectors * 0.028f));
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

    // --- Dibujo general de interfaz -----------------------------------------
    // OnGUI solo decide qué capa corresponde. Cada pantalla se dibuja en un método separado más abajo.
    private void OnGUI()
    {
        if (GameMenuController.ShouldHideGameplayHud || SceneTransitionController.IsFading)
        {
            return;
        }

        if (IsLocalVersus)
        {
            if (localVersusManager != null &&
                localVersusManager.IntroductionComplete &&
                (runPhase == RunPhase.StartupDelay || runPhase == RunPhase.Countdown || runPhase == RunPhase.GoFlash))
            {
                DrawCountdownOverlay();
            }

            return;
        }

        if (introTutorialOpen)
        {
            DrawIntroTutorialOverlay();
            return;
        }

        if (contextTutorialOpen)
        {
            if (runPhase == RunPhase.Active)
            {
                DrawRuntimeHud();
                DrawBossStateHud();
                DrawStatePulseOverlay();
                DrawBossLevelTwoIntroOverlay();
                DrawBossLevelThreeIntroOverlay();
                DrawBossStateBanner();
                DrawChaosWarningOverlay();
            }

            DrawContextTutorialOverlay();
            return;
        }

        if (runPhase == RunPhase.Active)
        {
            DrawRuntimeHud();
            DrawBossStateHud();
            DrawStatePulseOverlay();
            DrawBossLevelTwoIntroOverlay();
            DrawBossLevelThreeIntroOverlay();
            DrawBossStateBanner();
            DrawChaosWarningOverlay();
            DrawUpgradeSelectionOverlay();
        }

        if (runPhase == RunPhase.StartupDelay || runPhase == RunPhase.Countdown || runPhase == RunPhase.GoFlash)
        {
            DrawCountdownOverlay();
        }
    }

    private void EnsureLocalVersusSetup()
    {
        if (!IsLocalVersus || localVersusManager != null)
        {
            return;
        }

        if (playerController == null)
        {
            playerController = FindAnyObjectByType<PlayerController>();
        }
        if (enemyController == null)
        {
            enemyController = FindAnyObjectByType<EnemyController>();
        }
        if (playerController == null || enemyController == null)
        {
            return;
        }

        localVersusManager = GetComponent<LocalVersusManager>();
        if (localVersusManager == null)
        {
            localVersusManager = gameObject.AddComponent<LocalVersusManager>();
        }

        localVersusManager.Configure(this, playerController, enemyController);
    }

    // --- Arranque de la run --------------------------------------------------
    // Prepara referencias y recién después habilita tutorial, cuenta regresiva y juego activo.
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
        bossLevelTwoIntroPlayed = false;
        bossLevelTwoIntroTimer = 0f;
        bossLevelThreeIntroPlayed = false;
        bossLevelThreeIntroTimer = 0f;
        scorePopups.Clear();
        currentUpgradeChoices.Clear();
        displayedScore = 0f;
        smoothedThreat = 0f;
        scorePulseTimer = 0f;
        nextUpgradeTime = Mathf.Max(1f, firstUpgradeTime);
        nextContractTime = Mathf.Max(1f, firstContractTime);
        hasActiveContract = false;
        activeContract = default;
        contractCompletePulseTimer = 0f;
        lastContractCompletionLabel = string.Empty;
        contractDataBonusEarned = 0;
        activeOperation = ContainmentOperationStorage.SelectedOperation;
        operationRunRecorded = false;
        operationPlayerModifiersApplied = false;
        operationEnemyModifiersApplied = false;
        labRunTime = 0f;
        storageRunTime = 0f;
        ruptureRunTime = 0f;

        achievementToastTitle = string.Empty;
        achievementToastDescription = string.Empty;
        achievementToastHeader = string.Empty;
        achievementToastReward = 0;
        achievementToastTimer = 0f;
        upgradeSelectionOpen = false;
        upgradeSelectionClosing = false;
        upgradeSelectionAge = 0f;
        upgradeTimeRemaining = 0f;
        upgradeCurrentLimitSeconds = 0f;
        upgradeExitTimer = 0f;
        upgradeSelectedIndex = -1;
        upgradePickCount = 0;
        playerDefeatSequenceRunning = false;
        playerDefeatSequenceRoutine = null;
        HasAwardedMetaReward = false;
        LastMetaReward = default;
        breachSensitiveSuppressionTimer = 0f;
        eventPressureCooldownTimer = 0f;
        eventPressureReservations.Clear();
        introTutorialOpen = false;
        introTutorialStep = IntroTutorialStep.Movement;
        introTutorialStepProgress = 0f;
        introTutorialStepTimer = 0f;
        introTutorialActionFlash = 0f;
        introTutorialDemoPlayer = new Vector2(0.34f, 0.58f);
        introMoveWPressed = false;
        introMoveAPressed = false;
        introMoveSPressed = false;
        introMoveDPressed = false;
        contextTutorialOpen = false;
        contextTutorialKind = ContextTutorialKind.None;
        contextTutorialProgress = 0f;
        contextTutorialTimer = 0f;
        contextTutorialActionFlash = 0f;
        contextInterfacePhase = 0;
        contextTutorialEventKey = string.Empty;
        contextTutorialEventLabel = string.Empty;
        contextTutorialEventHint = string.Empty;
        pendingMapEventTutorialKey = string.Empty;
        pendingMapEventTutorialLabel = string.Empty;
        pendingMapEventTutorialHint = string.Empty;
        contextMoveWPressed = false;
        contextMoveAPressed = false;
        contextMoveSPressed = false;
        contextMoveDPressed = false;
        previousTimeScaleBeforeContext = 1f;
        contextArenaEventTutorialsShown.Clear();
        contextMovementShown = false;
        contextParryShown = false;
        contextGhostDashShown = false;
        contextFirewallShown = false;
        contextScorePickupShown = false;
        contextPowerupShown = false;
        contextUpgradeShown = false;
        contextInterfaceShown = false;
        contextBreachShown = false;
        contextStateHijackUnlockShown = false;
        PlayerController.SetTutorialInputLocked(false);
        Time.timeScale = 0f;
        ApplyDeveloperRunSettings();
        InitializeDifficultyDirector();
    }

    private void InitializeDifficultyDirector()
    {
        directorRecoveryUntil = 0f;
        sectorSignatureBeatPending = false;
        sectorSignatureBeatDeadline = 0f;
        if (!enableHybridDifficultyDirector || SurvivalTime >= firstBreachBeatTime + Mathf.Max(8f, scriptedBeatReserveLead))
        {
            scriptedDirectorBeat = ScriptedDirectorBeat.FreePlay;
        }
        else if (SurvivalTime >= firstObjectiveBeatTime)
        {
            scriptedDirectorBeat = ScriptedDirectorBeat.FirstBreach;
        }
        else if (SurvivalTime >= firstArenaSignatureTime)
        {
            scriptedDirectorBeat = ScriptedDirectorBeat.FirstObjective;
        }
        else
        {
            scriptedDirectorBeat = ScriptedDirectorBeat.FirstArenaSignature;
        }
    }

    private void ApplyDeveloperRunSettings()
    {
        if (IsLocalVersus)
        {
            devForceBossLevelTwo = false;
            devForceBossLevelThree = false;
            devFastRunLoops = false;
            devSkipCountdown = false;
            SurvivalTime = 0f;
            runPhase = RunPhase.StartupDelay;
            Time.timeScale = 0f;
            return;
        }

        devForceBossLevelTwo = DeveloperModeStorage.ForceBossLevelTwo;
        devForceBossLevelThree = DeveloperModeStorage.ForceBossLevelThree;
        devFastRunLoops = DeveloperModeStorage.FastRunLoops;
        devSkipCountdown = DeveloperModeStorage.SkipCountdown;

        float startTime = DeveloperModeStorage.StartTimeSeconds;
        if (devForceBossLevelTwo)
        {
            startTime = Mathf.Max(startTime, Mathf.Max(bossSpecialStatesUnlockTime, bossLevelTwoUnlockTime) + 0.5f);
        }
        if (devForceBossLevelThree)
        {
            devForceBossLevelTwo = true;
            startTime = Mathf.Max(startTime, Mathf.Max(bossLevelTwoUnlockTime, bossLevelThreeUnlockTime) + 0.5f);
        }

        SurvivalTime = Mathf.Max(0f, startTime);

        if (devFastRunLoops)
        {
            nextUpgradeTime = Mathf.Min(nextUpgradeTime, SurvivalTime + 8f);
            nextContractTime = Mathf.Min(nextContractTime, SurvivalTime + 9f);
            upgradeInterval = Mathf.Max(12f, upgradeInterval * 0.35f);
            contractInterval = Mathf.Max(14f, contractInterval * 0.35f);
            contractDuration = Mathf.Max(24f, contractDuration * 0.65f);
            mapEventsUnlockTime = Mathf.Min(mapEventsUnlockTime, SurvivalTime + 4f);
            containmentPulseUnlockTime = Mathf.Min(containmentPulseUnlockTime, SurvivalTime + 10f);
        }

        if (devSkipCountdown)
        {
            runPhase = RunPhase.Active;
            Time.timeScale = 1f;
        }
    }

    private void ApplyOperationPlayerModifiersOnce()
    {
        if (operationPlayerModifiersApplied || playerController == null)
        {
            return;
        }

        operationPlayerModifiersApplied = true;
        if (activeOperation.id == ContainmentOperationStorage.FirewallId)
        {
            playerController.ApplyDefensiveSystemDegradation(1.75f, 0.60f);
        }
    }

    private void ApplyOperationEnemyModifiersOnce()
    {
        if (operationEnemyModifiersApplied || enemyController == null)
        {
            return;
        }

        operationEnemyModifiersApplied = true;
        if (activeOperation.id == ContainmentOperationStorage.BreachId)
        {
            enemyController.ApplyContainmentOperationPressure(1.18f, 0.82f);
        }
    }

    private void UpdateIntroTutorialState()
    {
        float dt = Time.unscaledDeltaTime;
        bool wasCompleteAtStart = IsIntroStepComplete();
        introTutorialStepTimer += dt;
        if (introTutorialActionFlash > 0f)
        {
            introTutorialActionFlash -= dt;
        }

        Vector2 moveInput = ReadTutorialMoveInput();
        if (introTutorialStep == IntroTutorialStep.Movement)
        {
            if (moveInput.sqrMagnitude > 0.01f)
            {
                introTutorialDemoPlayer += moveInput.normalized * dt * 0.42f;
                introTutorialDemoPlayer.x = Mathf.Clamp(introTutorialDemoPlayer.x, 0.14f, 0.86f);
                introTutorialDemoPlayer.y = Mathf.Clamp(introTutorialDemoPlayer.y, 0.16f, 0.84f);
            }

            UpdateIntroMovementKeyChecklist();
            introTutorialStepProgress = GetIntroMovementKeyCount() / 4f;
            if (WasAnyTutorialMovementKeyPressed())
            {
                introTutorialActionFlash = 0.16f;
            }
        }
        else if (introTutorialStep == IntroTutorialStep.Parry)
        {
            if (WasTutorialParryPressed())
            {
                introTutorialStepProgress = 1f;
                introTutorialActionFlash = 0.28f;
                GlitchAudioManager.PlayParryStart(Vector3.zero);
            }
        }
        else if (introTutorialStep == IntroTutorialStep.Firewall)
        {
            if (WasTutorialFirewallPressed())
            {
                introTutorialStepProgress = 1f;
                introTutorialActionFlash = 0.34f;
                GlitchAudioManager.PlayFirewallBurst(Vector3.zero);
            }
        }
        else if (introTutorialStep == IntroTutorialStep.Resources || introTutorialStep == IntroTutorialStep.ArenaEvents)
        {
            introTutorialStepProgress = Mathf.Clamp01(introTutorialStepProgress + dt * 0.45f);
            if (WasTutorialConfirmPressed())
            {
                introTutorialStepProgress = 1f;
                introTutorialActionFlash = 0.22f;
            }
        }
        else
        {
            introTutorialStepProgress = 1f;
        }

        if (wasCompleteAtStart && IsIntroStepComplete() && WasTutorialConfirmPressed())
        {
            AdvanceIntroTutorialStep();
        }
    }

    private void DrawIntroTutorialOverlay()
    {
        EnsureUpgradeStyles();
        EnsureTutorialStyles();

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 2.4f);
        DrawSolidRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.005f, 0.01f, 0.02f, 0.82f));

        float panelW = Mathf.Min(1040f, Screen.width - 28f);
        float panelH = Mathf.Min(610f, Screen.height - 26f);
        Rect panel = new Rect((Screen.width - panelW) * 0.5f, (Screen.height - panelH) * 0.5f, panelW, panelH);
        DrawTutorialPanel(panel, new Color(0.025f, 0.04f, 0.08f, 0.95f), new Color(0.45f, 0.95f, 1f, 0.62f + pulse * 0.12f));

        Rect area = new Rect(panel.x + 22f, panel.y + 18f, panel.width - 44f, panel.height - 34f);
        GUI.Label(new Rect(area.x, area.y, area.width, 36f), "PROTOCOLO DE CONTENCION", upgradeTitleStyle);
        GUI.Label(
            new Rect(area.x, area.y + 38f, area.width, 24f),
            "Practica los controles y senales principales antes de entrar a la arena.",
            BuildFittedSingleLineStyle(tutorialHeaderStyle, "Practica los controles y senales principales antes de entrar a la arena.", area.width, 24f, 12));
        DrawSolidRect(new Rect(area.x, area.y + 70f, area.width, 2f), new Color(0.68f, 0.92f, 1f, 0.26f));

        DrawIntroStepDots(new Rect(area.x, area.y + 82f, area.width, 26f));

        Rect content = new Rect(area.x, area.y + 118f, area.width, area.height - 180f);
        float visualW = Mathf.Clamp(content.width * 0.46f, 330f, 455f);
        Rect visualRect = new Rect(content.x, content.y, visualW, content.height);
        Rect textRect = new Rect(visualRect.xMax + 20f, content.y, content.xMax - visualRect.xMax - 20f, content.height);
        DrawIntroTutorialVisual(visualRect);
        DrawIntroTutorialText(textRect);

        Rect progressBar = new Rect(textRect.x, textRect.yMax - 72f, textRect.width, 10f);
        DrawSolidRect(progressBar, new Color(0.03f, 0.05f, 0.09f, 0.92f));
        Color stepAccent = GetIntroStepAccent();
        DrawSolidRect(new Rect(progressBar.x, progressBar.y, progressBar.width * Mathf.Clamp01(introTutorialStepProgress), progressBar.height), new Color(stepAccent.r, stepAccent.g, stepAccent.b, 0.92f));

        Rect footer = new Rect(area.x, area.yMax - 48f, area.width, 42f);
        string saveHint = "Se guarda al completar. Reinicialo desde Opciones.";
        GUI.Label(
            new Rect(footer.x, footer.y + 6f, footer.width - 250f, 30f),
            saveHint,
            BuildFittedSingleLineStyle(tutorialBodyStyle, saveHint, footer.width - 250f, 30f, 10));

        string buttonLabel = introTutorialStep == IntroTutorialStep.Ready ? "INICIAR PROTOCOLO" : "CONTINUAR";
        Rect continueButton = new Rect(footer.xMax - 232f, footer.y + 2f, 232f, 36f);
        bool canContinue = IsIntroStepComplete();
        Color oldColor = GUI.color;
        bool oldEnabled = GUI.enabled;
        GUI.color = canContinue ? Color.white : new Color(1f, 1f, 1f, 0.42f);
        DrawSolidRect(continueButton, new Color(0.09f, 0.18f, 0.28f, 0.92f));
        DrawSolidRect(new Rect(continueButton.x, continueButton.y, continueButton.width, 2f), new Color(stepAccent.r, stepAccent.g, stepAccent.b, 0.82f));
        DrawSolidRect(new Rect(continueButton.x, continueButton.yMax - 2f, continueButton.width, 2f), new Color(1f, 0.58f, 0.78f, 0.48f));
        GUI.enabled = canContinue;
        if (GUI.Button(continueButton, buttonLabel, upgradeButtonStyle))
        {
            AdvanceIntroTutorialStep();
        }
        GUI.enabled = oldEnabled;
        GUI.color = oldColor;
    }

    private void CloseIntroTutorial()
    {
        UserSettings.SetShowIntroTutorial(false);

        introTutorialOpen = false;
        PlayerController.SetTutorialInputLocked(false);
        GlitchAudioManager.PlayMenuConfirm();
    }

    // --- Tutorial contextual -------------------------------------------------
    // Observa lo que está pasando y frena justo cuando aparece una situación que vale la pena enseñar.
    private void UpdateContextTutorialTriggers()
    {
        if (contextTutorialOpen || !IsRunActive || playerController == null)
        {
            return;
        }

        if (!contextMovementShown && SurvivalTime >= 0.35f)
        {
            TryOpenContextTutorial(ContextTutorialKind.Movement);
            return;
        }

        if (!contextInterfaceShown && SurvivalTime >= 1f)
        {
            if (TryOpenContextTutorial(ContextTutorialKind.Interface))
            {
                return;
            }
        }

        if (!contextParryShown && enemyController != null &&
            enemyController.IsAvailableForCombatTutorial() && SurvivalTime >= 1.2f)
        {
            float distance = Vector2.Distance(playerController.GetPosition(), enemyController.GetCurrentPosition());
            float parryTeachDistance = Mathf.Max(0.65f, playerController.ParryRadius + 0.10f);
            if (distance <= parryTeachDistance)
            {
                TryOpenContextTutorial(ContextTutorialKind.Parry);
                return;
            }
        }

        if (!contextGhostDashShown && enemyController != null &&
            enemyController.IsAvailableForCombatTutorial() && SurvivalTime >= 3.2f && playerController.IsGhostDashReady)
        {
            float distance = Vector2.Distance(playerController.GetPosition(), enemyController.GetCurrentPosition());
            if (distance <= 3.2f)
            {
                TryOpenContextTutorial(ContextTutorialKind.GhostDash);
                return;
            }
        }

        if (!contextFirewallShown && playerController.IsFirewallBurstReady)
        {
            TryOpenContextTutorial(ContextTutorialKind.Firewall);
            return;
        }

        if (IsStateHijackUnlocked && !contextStateHijackUnlockShown && bossLevelTwoIntroTimer <= 0f)
        {
            TryOpenContextTutorial(ContextTutorialKind.StateHijackUnlock);
            return;
        }

        if (!string.IsNullOrWhiteSpace(pendingMapEventTutorialKey))
        {
            string pendingKey = pendingMapEventTutorialKey;
            if (HasContextTutorialBeenShown(ContextTutorialKind.ArenaEvent, pendingKey))
            {
                ClearPendingMapEventTutorial();
            }
            else if (TryOpenContextTutorial(
                         ContextTutorialKind.ArenaEvent,
                         pendingKey,
                         pendingMapEventTutorialLabel,
                         pendingMapEventTutorialHint))
            {
                ClearPendingMapEventTutorial();
                return;
            }
        }

        if (IsStateHijackUnlocked && playerController.HasStoredHijack)
        {
            string hijackKey = $"Hijack:{playerController.StoredHijackLabel}";
            if (TryOpenContextTutorial(
                    ContextTutorialKind.StateHijack,
                    hijackKey,
                    playerController.StoredHijackLabel,
                    playerController.StoredHijackHint))
            {
                return;
            }
        }

        if (enemyController != null && IsBossSpecialState(enemyController.CurrentStateLabel))
        {
            string stateRaw = enemyController.CurrentStateLabel;
            if (TryOpenContextTutorial(
                    ContextTutorialKind.BossState,
                    $"Boss:{stateRaw}",
                    ToBossStateLabel(stateRaw),
                    GetBossStateTutorialHint(stateRaw)))
            {
                return;
            }
        }

        if (SurvivalTime >= mapEventsUnlockTime && TryGetContextEventTutorialInfo(out string eventLabel, out string eventHint))
        {
            // La identidad depende del sector y del evento, no de un hint que puede variar
            // durante sus distintas fases. Asi cada evento se enseña una sola vez.
            string eventKey = $"{levelType}:{NormalizeTutorialIdentity(eventLabel)}";
            if (!contextArenaEventTutorialsShown.Contains(eventKey))
            {
                TryOpenContextTutorial(ContextTutorialKind.ArenaEvent, eventKey, eventLabel, eventHint);
            }
        }
    }

    private void ClearPendingMapEventTutorial()
    {
        pendingMapEventTutorialKey = string.Empty;
        pendingMapEventTutorialLabel = string.Empty;
        pendingMapEventTutorialHint = string.Empty;
    }

    private static string NormalizeTutorialIdentity(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        System.Text.StringBuilder builder = new System.Text.StringBuilder(value.Length);
        bool previousWasNumber = false;
        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];
            if (char.IsDigit(character))
            {
                if (!previousWasNumber)
                {
                    builder.Append('#');
                }
                previousWasNumber = true;
                continue;
            }

            previousWasNumber = false;
            builder.Append(char.ToUpperInvariant(character));
        }

        return builder.ToString();
    }

    private bool TryGetContextEventTutorialInfo(out string label, out string hint)
    {
        label = GetThemedEventLabel();
        hint = GetThemedEventHint();
        if (!string.IsNullOrWhiteSpace(label))
        {
            return true;
        }

        string warning = chaosDirector != null ? chaosDirector.ActiveWarningLabel : string.Empty;
        if (!string.IsNullOrWhiteSpace(warning) && ShouldTeachChaosEventLabel(warning))
        {
            label = warning;
            hint = "Aviso critico del mapa: prepara movimiento y lee la zona antes de que se active.";
            return true;
        }

        string chaosLabel = chaosDirector != null ? chaosDirector.ActiveEventLabel : string.Empty;
        if (!string.IsNullOrWhiteSpace(chaosLabel) && chaosLabel.Contains("Sigue las flechas") && contextBreachShown)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(chaosLabel) && ShouldTeachChaosEventLabel(chaosLabel))
        {
            label = chaosLabel;
            hint = GetChaosEventTutorialHint(chaosLabel);
            return true;
        }

        return false;
    }

    private bool ShouldTeachChaosEventLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return false;
        }

        return label.Contains("Mantener nodos") ||
               label.Contains("Containment Lock") ||
               label.Contains("Containment Pulse") ||
               label.Contains("Sigue las flechas");
    }

    private string GetChaosEventTutorialHint(string label)
    {
        if (label.Contains("Mantener nodos"))
        {
            return "Objetivo de mapa: mantenete dentro de los nodos hasta sincronizarlos. Te cargan Firewall y dan recompensa si completas la secuencia.";
        }

        if (label.Contains("Containment Lock") || label.Contains("Containment Pulse"))
        {
            return "Pulso de contencion: el mapa va a imponer presion. Mira la zona activa, evita quedar encerrado y usa movimiento para reposicionarte.";
        }

        if (label.Contains("Sigue las flechas"))
        {
            return "Breach detectada: segui los indicadores del mapa hacia la salida antes de que el barrido avance.";
        }

        return "Evento de mapa activo: lee la alerta visual y responde con movimiento antes de que el mapa te encierre.";
    }

    private static string GetBossStateTutorialHint(string state)
    {
        switch (state)
        {
            case "Split": return "Aparece un clon que busca encerrarte desde otro angulo. Mantene ambos a la vista y recorda que el parry afecta a los dos.";
            case "ExpansionShoot": return "La anomalia dispara alrededor suyo. Busca huecos entre proyectiles y evita quedarte pegado a un obstaculo.";
            case "SpeedSurge": return "La persecucion acelera mucho durante unos segundos. Abri distancia temprano y guarda Dash para corregir una mala ruta.";
            case "WeaveHunter": return "El jefe alterna laterales para cortar tu trayectoria. Cambia de direccion despues de que comprometa su curva.";
            case "Destroyer": return "La anomalia puede romper obstáculos de su camino. Las paredes dejan de ser refugios confiables durante este estado.";
            case "PhaseBlink": return "Primero marca el destino y despues se teletransporta. Sali del area anunciada antes del impacto.";
            case "PincerBarrage": return "Dos lineas de disparo intentan cerrarse sobre vos. Escapa por el eje abierto antes de que ambas pinzas disparen.";
            case "SignalJam": return "La señal prepara una alteracion de control. Lee el telegraph y evita maniobras finas durante la descarga.";
            case "OrbitBarrage": return "Las zonas orbitales explotan despues del aviso. No cruces su radio al final del conteo y conserva una salida libre.";
            case "ReplayPredator": return "Un fantasma repite tu recorrido anterior. No vuelvas sobre tus propios pasos mientras el eco siga activo.";
            case "ChecksumLattice": return "Segui la secuencia de nodos resaltados. Resolverla encierra al jefe; tocar un nodo incorrecto castiga el intento.";
            case "InputDesync": return "Tus entradas pueden desplazarte de forma inesperada. Usa pulsaciones cortas y alejate de los bordes hasta recuperar sincronía.";
            case "MapRecompile": return "El jefe mueve obstáculos para cortar tu ruta futura. Observa las posiciones marcadas y cambia de corredor antes del cierre.";
            case "SignalPossession": return "Una señal intenta atraerte hacia una trampa. Mantene distancia del foco y no sigas su señuelo visual.";
            case "PhaseContract": return "Cumpli la condición visual dentro del tiempo de gracia. El radio mostrado indica la distancia exacta que debe respetarse.";
            case "AdaptiveCountermeasure": return "La anomalia estudia tu forma de moverte y castiga la repetición. Alterna ritmo, dirección y pausas breves.";
            case "SignalTether": return "Un vínculo altera tu vector de movimiento. Rompe la distancia del enlace antes de que pueda reposicionarte.";
            case "BlindspotProtocol": return "El ataque aprovecha el lado que no estas observando. Mantenete en movimiento y rota alrededor del jefe para negar el punto ciego.";
            default: return "Lee el telegraph de color antes de reaccionar: cada estado anuncia su zona y momento de peligro.";
        }
    }

    private bool TryOpenContextTutorial(ContextTutorialKind kind, string eventKey = null, string eventLabel = null, string eventHint = null)
    {
        if (kind == ContextTutorialKind.None || contextTutorialOpen || introTutorialOpen || upgradeSelectionOpen || IsGameOver)
        {
            return false;
        }

        if (!UserSettings.GetShowContextTutorial())
        {
            MarkContextTutorialOpenedThisRun(kind, eventKey);
            return false;
        }

        if (HasContextTutorialBeenShown(kind, eventKey))
        {
            return false;
        }

        MarkContextTutorialOpenedThisRun(kind, eventKey);
        contextTutorialOpen = true;
        contextTutorialKind = kind;
        contextTutorialProgress = 0f;
        contextTutorialTimer = 0f;
        contextTutorialActionFlash = 0.2f;
        contextInterfacePhase = 0;
        contextTutorialEventKey = eventKey ?? string.Empty;
        bool usesDynamicContext = kind == ContextTutorialKind.ArenaEvent ||
                                  kind == ContextTutorialKind.BossState ||
                                  kind == ContextTutorialKind.StateHijack;
        contextTutorialEventLabel = usesDynamicContext ? eventLabel ?? GetThemedEventLabel() : string.Empty;
        contextTutorialEventHint = usesDynamicContext ? eventHint ?? GetThemedEventHint() : string.Empty;
        contextMoveWPressed = false;
        contextMoveAPressed = false;
        contextMoveSPressed = false;
        contextMoveDPressed = false;
        previousTimeScaleBeforeContext = Time.timeScale;
        Time.timeScale = 0f;
        PlayerController.SetTutorialInputLocked(true);
        GlitchAudioManager.PlayMenuHover();
        return true;
    }

    private bool HasContextTutorialBeenShown(ContextTutorialKind kind, string eventKey)
    {
        string settingsKey = GetContextTutorialSettingsKey(kind, eventKey);
        if (UserSettings.HasSeenContextTutorial(settingsKey))
        {
            return true;
        }

        switch (kind)
        {
            case ContextTutorialKind.Movement:
                return contextMovementShown;
            case ContextTutorialKind.Parry:
                return contextParryShown;
            case ContextTutorialKind.GhostDash:
                return contextGhostDashShown;
            case ContextTutorialKind.Firewall:
                return contextFirewallShown;
            case ContextTutorialKind.ScorePickup:
                return contextScorePickupShown;
            case ContextTutorialKind.Powerup:
                return contextPowerupShown;
            case ContextTutorialKind.Upgrade:
                return contextUpgradeShown;
            case ContextTutorialKind.Interface:
                return contextInterfaceShown;
            case ContextTutorialKind.ArenaEvent:
            case ContextTutorialKind.BossState:
            case ContextTutorialKind.StateHijack:
                return !string.IsNullOrWhiteSpace(eventKey) && contextArenaEventTutorialsShown.Contains(eventKey);
            case ContextTutorialKind.Breach:
                return contextBreachShown;
            case ContextTutorialKind.StateHijackUnlock:
                return contextStateHijackUnlockShown;
            default:
                return true;
        }
    }

    private void MarkContextTutorialCompleted(ContextTutorialKind kind, string eventKey)
    {
        string settingsKey = GetContextTutorialSettingsKey(kind, eventKey);
        UserSettings.MarkContextTutorialSeen(settingsKey);
        MarkContextTutorialOpenedThisRun(kind, eventKey);
    }

    private void MarkContextTutorialOpenedThisRun(ContextTutorialKind kind, string eventKey)
    {
        switch (kind)
        {
            case ContextTutorialKind.Movement:
                contextMovementShown = true;
                break;
            case ContextTutorialKind.Parry:
                contextParryShown = true;
                break;
            case ContextTutorialKind.GhostDash:
                contextGhostDashShown = true;
                break;
            case ContextTutorialKind.Firewall:
                contextFirewallShown = true;
                break;
            case ContextTutorialKind.ScorePickup:
                contextScorePickupShown = true;
                break;
            case ContextTutorialKind.Powerup:
                contextPowerupShown = true;
                break;
            case ContextTutorialKind.Upgrade:
                contextUpgradeShown = true;
                break;
            case ContextTutorialKind.Interface:
                contextInterfaceShown = true;
                break;
            case ContextTutorialKind.ArenaEvent:
            case ContextTutorialKind.BossState:
            case ContextTutorialKind.StateHijack:
                if (!string.IsNullOrWhiteSpace(eventKey))
                {
                    contextArenaEventTutorialsShown.Add(eventKey);
                }
                break;
            case ContextTutorialKind.Breach:
                contextBreachShown = true;
                break;
            case ContextTutorialKind.StateHijackUnlock:
                contextStateHijackUnlockShown = true;
                break;
        }
    }

    private static string GetContextTutorialSettingsKey(ContextTutorialKind kind, string eventKey = null)
    {
        switch (kind)
        {
            case ContextTutorialKind.Movement:
                return "movement";
            case ContextTutorialKind.Parry:
                return "parry";
            case ContextTutorialKind.GhostDash:
                return "ghost_dash";
            case ContextTutorialKind.Firewall:
                return "firewall";
            case ContextTutorialKind.ScorePickup:
                return "score_pickup";
            case ContextTutorialKind.Powerup:
                return "powerup";
            case ContextTutorialKind.Upgrade:
                return "upgrade";
            case ContextTutorialKind.Interface:
                return "interface_v2";
            case ContextTutorialKind.ArenaEvent:
                return BuildDynamicContextTutorialKey("arena_event_v2", eventKey);
            case ContextTutorialKind.Breach:
                return "breach";
            case ContextTutorialKind.BossState:
                return BuildDynamicContextTutorialKey("boss_state", eventKey);
            case ContextTutorialKind.StateHijackUnlock:
                return "state_hijack_unlock";
            case ContextTutorialKind.StateHijack:
                return BuildDynamicContextTutorialKey("state_hijack", eventKey);
            default:
                return string.Empty;
        }
    }

    private static string BuildDynamicContextTutorialKey(string prefix, string value)
    {
        unchecked
        {
            uint hash = 2166136261;
            string source = value ?? string.Empty;
            for (int i = 0; i < source.Length; i++)
            {
                hash ^= source[i];
                hash *= 16777619;
            }

            return $"{prefix}_{hash:x8}";
        }
    }

    private void UpdateContextTutorialState()
    {
        float dt = Time.unscaledDeltaTime;
        contextTutorialTimer += dt;
        if (contextTutorialActionFlash > 0f)
        {
            contextTutorialActionFlash -= dt;
        }

        if (contextTutorialKind == ContextTutorialKind.Movement)
        {
            UpdateContextMovementKeyChecklist();
            int pressedCount = GetContextMovementKeyCount();
            contextTutorialProgress = pressedCount / 4f;
            if (WasAnyTutorialMovementKeyPressed())
            {
                contextTutorialActionFlash = 0.14f;
            }

            if (pressedCount >= 4)
            {
                CloseContextTutorial();
                return;
            }
        }
        else if (contextTutorialKind == ContextTutorialKind.Parry)
        {
            if (WasTutorialParryPressed())
            {
                contextTutorialProgress = 1f;
                contextTutorialActionFlash = 0.28f;
                CloseContextTutorial(playConfirm: false);
                playerController?.TryStartParryFromTutorial();
                return;
            }
        }
        else if (contextTutorialKind == ContextTutorialKind.GhostDash)
        {
            if (WasTutorialGhostDashPressed())
            {
                contextTutorialProgress = 1f;
                contextTutorialActionFlash = 0.28f;
                CloseContextTutorial(playConfirm: false);
                playerController?.TryStartGhostDashFromTutorial();
                return;
            }
        }
        else if (contextTutorialKind == ContextTutorialKind.Firewall)
        {
            if (WasTutorialFirewallPressed())
            {
                contextTutorialProgress = 1f;
                contextTutorialActionFlash = 0.34f;
                CloseContextTutorial(playConfirm: false);
                playerController?.TryActivateFirewallBurst();
                return;
            }
        }
        else if (contextTutorialKind == ContextTutorialKind.StateHijack)
        {
            if (WasTutorialStateHijackPressed())
            {
                contextTutorialProgress = 1f;
                contextTutorialActionFlash = 0.34f;
                CloseContextTutorial(playConfirm: false);
                playerController?.TryActivateStateHijackFromTutorial();
                return;
            }
        }
        else if (contextTutorialKind == ContextTutorialKind.ArenaEvent || contextTutorialKind == ContextTutorialKind.Breach)
        {
            Vector2 move = ReadTutorialMoveInput();
            contextTutorialProgress = Mathf.Clamp01(contextTutorialProgress + dt * 0.38f);
            if (move.sqrMagnitude > 0.01f)
            {
                contextTutorialProgress = 1f;
                CloseContextTutorial();
                return;
            }
        }
        else if (contextTutorialKind == ContextTutorialKind.Interface)
        {
            contextTutorialProgress = (contextInterfacePhase + 1f) / GetInterfaceTutorialPhaseCount();
            if (WasTutorialClickPressed())
            {
                contextTutorialActionFlash = 0.22f;
                contextInterfacePhase++;
                if (contextInterfacePhase >= GetInterfaceTutorialPhaseCount())
                {
                    contextTutorialProgress = 1f;
                    CloseContextTutorial();
                }
                else
                {
                    GlitchAudioManager.PlayMenuHover();
                }
                return;
            }
        }
        else if (IsClickValidatedContextTutorial(contextTutorialKind))
        {
            contextTutorialProgress = Mathf.Clamp01(contextTutorialProgress + dt * 0.65f);
            if (WasTutorialClickPressed())
            {
                contextTutorialProgress = 1f;
                CloseContextTutorial();
                return;
            }
        }
    }

    private bool IsContextTutorialComplete()
    {
        return contextTutorialProgress >= 0.98f;
    }

    private static bool IsClickValidatedContextTutorial(ContextTutorialKind kind)
    {
        return kind == ContextTutorialKind.ScorePickup ||
               kind == ContextTutorialKind.Powerup ||
               kind == ContextTutorialKind.Upgrade ||
               kind == ContextTutorialKind.Interface ||
               kind == ContextTutorialKind.BossState ||
               kind == ContextTutorialKind.StateHijackUnlock;
    }

    private void CloseContextTutorial(bool playConfirm = true)
    {
        ContextTutorialKind completedKind = contextTutorialKind;
        string completedEventKey = contextTutorialEventKey;
        bool completed = IsContextTutorialComplete();

        contextTutorialOpen = false;
        contextTutorialKind = ContextTutorialKind.None;
        contextTutorialProgress = 0f;
        contextTutorialTimer = 0f;
        contextTutorialActionFlash = 0f;
        PlayerController.SetTutorialInputLocked(false);

        if (completed)
        {
            MarkContextTutorialCompleted(completedKind, completedEventKey);
        }

        if (!IsGameOver && !upgradeSelectionOpen)
        {
            Time.timeScale = Mathf.Approximately(previousTimeScaleBeforeContext, 0f) ? 1f : previousTimeScaleBeforeContext;
        }

        if (playConfirm)
        {
            GlitchAudioManager.PlayMenuConfirm();
        }
    }

    private void DrawContextTutorialOverlay()
    {
        EnsureUpgradeStyles();
        EnsureTutorialStyles();

        if (contextTutorialKind == ContextTutorialKind.Interface)
        {
            DrawInterfaceTutorialOverlay();
            return;
        }

        Color accent = GetContextTutorialAccent();
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.2f);
        DrawContextFreezeFrameVignette(accent, pulse);
        DrawContextWorldMarkers(accent, pulse);

        float panelW = Mathf.Min(540f, Screen.width - 32f);
        float panelH = Mathf.Min(246f, Screen.height - 32f);
        Rect panel = GetContextTutorialPanelRect(panelW, panelH);
        DrawTutorialPanel(panel, new Color(0.018f, 0.028f, 0.054f, 0.94f), new Color(accent.r, accent.g, accent.b, 0.62f + pulse * 0.16f));

        Rect area = new Rect(panel.x + 18f, panel.y + 14f, panel.width - 36f, panel.height - 26f);
        GUI.Label(new Rect(area.x, area.y, area.width, 20f), "PAUSA CONTEXTUAL", BuildFittedSingleLineStyle(tutorialTinyStyle, "PAUSA CONTEXTUAL", area.width, 20f, 9));
        GUI.Label(new Rect(area.x, area.y + 22f, area.width, 34f), GetContextTutorialTitle(), BuildFittedSingleLineStyle(upgradeTitleStyle, GetContextTutorialTitle(), area.width, 34f, 14));

        GUIStyle compactBody = new GUIStyle(tutorialBodyStyle)
        {
            fontSize = Mathf.Max(11, Mathf.RoundToInt(12f * hudScale)),
            clipping = TextClipping.Clip,
            wordWrap = true
        };

        Rect body = new Rect(area.x, area.y + 64f, area.width, 70f);
        GUI.Label(body, GetContextTutorialBody(), compactBody);

        Rect hint = new Rect(area.x, area.y + 140f, area.width, 30f);
        DrawSolidRect(hint, new Color(accent.r, accent.g, accent.b, 0.12f));
        GUI.Label(new Rect(hint.x + 10f, hint.y + 4f, hint.width - 20f, hint.height - 8f), GetContextTutorialHint(), BuildFittedSingleLineStyle(tutorialHeaderStyle, GetContextTutorialHint(), hint.width - 20f, hint.height - 8f, 10));

        Rect progress = new Rect(area.x, area.y + 176f, area.width, 8f);
        DrawSolidRect(progress, new Color(0.03f, 0.05f, 0.09f, 0.92f));
        DrawSolidRect(new Rect(progress.x, progress.y, progress.width * Mathf.Clamp01(contextTutorialProgress), progress.height), new Color(accent.r, accent.g, accent.b, 0.92f));

        Rect footer = new Rect(area.x, area.yMax - 32f, area.width, 30f);
        DrawSolidRect(footer, new Color(0.03f, 0.05f, 0.09f, 0.74f));
        GUI.Label(new Rect(footer.x + 10f, footer.y + 4f, footer.width - 20f, footer.height - 8f), GetContextTutorialFooter(), BuildFittedSingleLineStyle(tutorialHeaderStyle, GetContextTutorialFooter(), footer.width - 20f, footer.height - 8f, 10));
    }

    private const int InterfaceTutorialPhaseCount = 6;

    private static int GetInterfaceTutorialPhaseCount()
    {
        return InterfaceTutorialPhaseCount;
    }

    private void DrawInterfaceTutorialOverlay()
    {
        float s = hudScale;
        HudTopLayout layout = GetHudTopLayout(s);
        int phase = Mathf.Clamp(contextInterfacePhase, 0, GetInterfaceTutorialPhaseCount() - 1);
        Rect target = GetInterfaceTutorialTarget(layout, phase);
        Color accent = GetInterfaceTutorialPhaseColor(phase);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4.2f);
        float margin = Mathf.Max(5f, 7f * s);
        Rect focus = new Rect(target.x - margin, target.y - margin, target.width + margin * 2f, target.height + margin * 2f);

        // Atenua el mundo por fuera del foco, pero deja el HUD real totalmente visible.
        Color dim = new Color(0.002f, 0.006f, 0.014f, 0.48f);
        DrawSolidRect(new Rect(0f, 0f, Screen.width, Mathf.Max(0f, focus.y)), dim);
        DrawSolidRect(new Rect(0f, focus.yMax, Screen.width, Mathf.Max(0f, Screen.height - focus.yMax)), dim);
        DrawSolidRect(new Rect(0f, focus.y, Mathf.Max(0f, focus.x), focus.height), dim);
        DrawSolidRect(new Rect(focus.xMax, focus.y, Mathf.Max(0f, Screen.width - focus.xMax), focus.height), dim);

        DrawTutorialFrame(focus, new Color(accent.r, accent.g, accent.b, 0.82f + pulse * 0.16f), Mathf.Max(2f, 3f * s));
        DrawSolidRect(new Rect(focus.x, focus.yMax + 4f, focus.width, 2f), new Color(accent.r, accent.g, accent.b, 0.62f));

        float panelW = Mathf.Min(680f, Screen.width - 32f);
        float panelH = Mathf.Clamp(Screen.height * 0.22f, 138f, 184f);
        Rect panel = new Rect((Screen.width - panelW) * 0.5f, Screen.height - panelH - 18f, panelW, panelH);
        DrawTutorialPanel(panel, new Color(0.012f, 0.024f, 0.048f, 0.96f), new Color(accent.r, accent.g, accent.b, 0.68f));

        Rect area = new Rect(panel.x + 20f, panel.y + 14f, panel.width - 40f, panel.height - 26f);
        string step = $"INTERFAZ  {phase + 1}/{GetInterfaceTutorialPhaseCount()}";
        string title = GetInterfaceTutorialPhaseTitle(phase);
        string bodyText = GetInterfaceTutorialPhaseBody(phase);
        GUI.Label(new Rect(area.x, area.y, area.width, 18f), step,
            BuildFittedSingleLineStyle(tutorialTinyStyle, step, area.width, 18f, 9));
        GUI.Label(new Rect(area.x, area.y + 20f, area.width, 34f), title,
            BuildFittedSingleLineStyle(upgradeTitleStyle, title, area.width, 34f, 16));

        GUIStyle bodyStyle = new GUIStyle(tutorialBodyStyle)
        {
            fontSize = Mathf.Max(12, Mathf.RoundToInt(14f * hudScale)),
            alignment = TextAnchor.UpperLeft,
            clipping = TextClipping.Clip,
            wordWrap = true
        };
        Rect body = new Rect(area.x, area.y + 58f, area.width, Mathf.Max(38f, area.height - 92f));
        GUI.Label(body, bodyText, BuildFittedWrappedStyle(bodyStyle, bodyText, body.width, body.height, 10));

        string footer = phase == GetInterfaceTutorialPhaseCount() - 1 ? "CLICK PARA TERMINAR" : "CLICK PARA CONTINUAR";
        Rect footerRect = new Rect(area.x, area.yMax - 26f, area.width, 24f);
        DrawSolidRect(footerRect, new Color(accent.r, accent.g, accent.b, 0.14f));
        GUI.Label(footerRect, footer, BuildFittedSingleLineStyle(tutorialHeaderStyle, footer, footerRect.width, footerRect.height, 10));
    }

    private static Rect GetInterfaceTutorialTarget(HudTopLayout layout, int phase)
    {
        switch (phase)
        {
            case 0: return new Rect(layout.metrics.x, layout.metrics.y, layout.metrics.width * 0.5f, layout.metrics.height);
            case 1: return new Rect(layout.metrics.center.x, layout.metrics.y, layout.metrics.width * 0.5f, layout.metrics.height);
            case 2: return layout.hijack;
            case 3: return layout.dash;
            case 4: return layout.firewall;
            default: return new Rect(layout.sector.x, layout.sector.y, layout.focus.xMax - layout.sector.x, layout.sector.height);
        }
    }

    private static string GetInterfaceTutorialPhaseTitle(int phase)
    {
        switch (phase)
        {
            case 0: return "TIEMPO DE SUPERVIVENCIA";
            case 1: return "PUNTAJE";
            case 2: return "FIREWALL PARRY";
            case 3: return "DASH FANTASMA";
            case 4: return "CARGA FIREWALL";
            default: return "SECTOR Y ESTADO";
        }
    }

    private static string GetInterfaceTutorialPhaseBody(int phase)
    {
        switch (phase)
        {
            case 0: return "El reloj mide cuanto tiempo llevas sobreviviendo. La dificultad y las amenazas aumentan a medida que avanza.";
            case 1: return "El puntaje aumenta con el tiempo, los datos recogidos y los objetivos completados. Al finalizar la run se registra en el ranking.";
            case 2: return "Esta barra muestra la recarga del Parry. Cuando indica ESPACIO / E, podes rechazar el contacto y reflejar proyectiles.";
            case 3: return "La barra de Dash muestra su recarga. Cuando aparece SHIFT, el Dash Fantasma esta listo para atravesar una amenaza.";
            case 4: return "Las acciones defensivas y los pickups cargan Firewall. Al llegar al maximo, usa Q / R para liberar el Burst.";
            default: return "El sector actual y el estado de la anomalia aparecen aqui. Los contratos y eventos temporales reutilizan esta zona para mostrar su objetivo.";
        }
    }

    private static Color GetInterfaceTutorialPhaseColor(int phase)
    {
        switch (phase)
        {
            case 1: return new Color(0.62f, 0.82f, 1f, 1f);
            case 2: return new Color(1f, 0.62f, 0.78f, 1f);
            case 3: return new Color(0.55f, 1f, 0.94f, 1f);
            case 4: return new Color(1f, 0.84f, 0.42f, 1f);
            case 5: return new Color(0.76f, 0.66f, 1f, 1f);
            default: return new Color(0.46f, 0.88f, 1f, 1f);
        }
    }

    private void DrawContextFreezeFrameVignette(Color accent, float pulse)
    {
        float edge = Mathf.Clamp(Screen.height * 0.09f, 34f, 82f);
        DrawSolidRect(new Rect(0f, 0f, Screen.width, edge), new Color(0.002f, 0.006f, 0.014f, 0.34f));
        DrawSolidRect(new Rect(0f, Screen.height - edge, Screen.width, edge), new Color(0.002f, 0.006f, 0.014f, 0.30f));
        DrawSolidRect(new Rect(0f, 0f, edge * 0.75f, Screen.height), new Color(0.002f, 0.006f, 0.014f, 0.18f));
        DrawSolidRect(new Rect(Screen.width - edge * 0.75f, 0f, edge * 0.75f, Screen.height), new Color(0.002f, 0.006f, 0.014f, 0.18f));
        DrawSolidRect(new Rect(0f, 0f, Screen.width, 2f), new Color(accent.r, accent.g, accent.b, 0.32f + pulse * 0.12f));
        DrawSolidRect(new Rect(0f, Screen.height - 2f, Screen.width, 2f), new Color(accent.r, accent.g, accent.b, 0.26f));
    }

    private Rect GetContextTutorialPanelRect(float width, float height)
    {
        Vector2 focus = GetContextFocusGuiPoint();
        float margin = 18f;
        bool placeTop = focus.y > Screen.height * 0.54f;
        float y = placeTop ? margin : Screen.height - height - margin;
        float x = focus.x < Screen.width * 0.5f ? Screen.width - width - margin : margin;

        if (contextTutorialKind == ContextTutorialKind.Upgrade)
        {
            x = (Screen.width - width) * 0.5f;
            y = Screen.height - height - margin;
        }

        return new Rect(
            Mathf.Clamp(x, margin, Screen.width - width - margin),
            Mathf.Clamp(y, margin, Screen.height - height - margin),
            width,
            height);
    }

    private Vector2 GetContextFocusGuiPoint()
    {
        if (contextTutorialKind == ContextTutorialKind.Parry && enemyController != null)
        {
            return WorldToGuiPoint(enemyController.GetCurrentPosition());
        }

        if (playerController != null)
        {
            return WorldToGuiPoint(playerController.GetPosition());
        }

        return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
    }

    private Vector2 WorldToGuiPoint(Vector2 worldPosition)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        Vector3 screen = cam.WorldToScreenPoint(worldPosition);
        return new Vector2(screen.x, Screen.height - screen.y);
    }

    private void DrawContextWorldMarkers(Color accent, float pulse)
    {
        if (playerController != null)
        {
            DrawContextTargetMarker(WorldToGuiPoint(playerController.GetPosition()), "VOS", new Color(0.42f, 0.95f, 1f, 1f), pulse);
        }

        if (enemyController != null && (contextTutorialKind == ContextTutorialKind.Parry || contextTutorialKind == ContextTutorialKind.Movement))
        {
            Vector2 enemyPoint = WorldToGuiPoint(enemyController.GetCurrentPosition());
            DrawContextTargetMarker(enemyPoint, "ANOMALIA", new Color(1f, 0.28f, 0.38f, 1f), pulse);

            if (playerController != null)
            {
                Vector2 playerPoint = WorldToGuiPoint(playerController.GetPosition());
                DrawContextLine(playerPoint, enemyPoint, accent);
                if (contextTutorialKind == ContextTutorialKind.Parry)
                {
                    DrawContextCallout(
                        Vector2.Lerp(playerPoint, enemyPoint, 0.52f) + new Vector2(0f, -46f),
                        "Este es el rango peligroso: Parry corta el contacto.",
                        accent);
                }
            }
        }
        else if (contextTutorialKind == ContextTutorialKind.Firewall && playerController != null)
        {
            DrawContextCallout(WorldToGuiPoint(playerController.GetPosition()) + new Vector2(0f, -58f), "Barra lista: usalo antes de quedar encerrado.", accent);
        }
        else if (contextTutorialKind == ContextTutorialKind.Breach)
        {
            DrawContextCallout(new Vector2(Screen.width * 0.5f, Screen.height * 0.22f), "Segui la flecha real del mapa hacia la brecha.", accent);
        }
    }

    private void DrawContextTargetMarker(Vector2 center, string label, Color color, float pulse)
    {
        float size = 54f + pulse * 10f;
        Rect marker = new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size);
        DrawTutorialFrame(marker, new Color(color.r, color.g, color.b, 0.72f), 3f);
        DrawSolidRect(new Rect(center.x - 3f, center.y - size * 0.5f - 9f, 6f, 18f), new Color(color.r, color.g, color.b, 0.82f));
        DrawSolidRect(new Rect(center.x - 3f, center.y + size * 0.5f - 9f, 6f, 18f), new Color(color.r, color.g, color.b, 0.82f));
        DrawSolidRect(new Rect(center.x - size * 0.5f - 9f, center.y - 3f, 18f, 6f), new Color(color.r, color.g, color.b, 0.82f));
        DrawSolidRect(new Rect(center.x + size * 0.5f - 9f, center.y - 3f, 18f, 6f), new Color(color.r, color.g, color.b, 0.82f));

        Rect labelRect = new Rect(center.x - 74f, center.y - size * 0.5f - 30f, 148f, 22f);
        DrawSolidRect(labelRect, new Color(0.01f, 0.02f, 0.04f, 0.78f));
        GUI.Label(labelRect, label, BuildFittedSingleLineStyle(tutorialTinyStyle, label, labelRect.width, labelRect.height, 8));
    }

    private void DrawContextLine(Vector2 from, Vector2 to, Color color)
    {
        Vector2 delta = to - from;
        int steps = Mathf.Clamp(Mathf.CeilToInt(delta.magnitude / 18f), 4, 32);
        for (int i = 0; i <= steps; i++)
        {
            if (i % 2 != 0)
            {
                continue;
            }

            Vector2 p = Vector2.Lerp(from, to, i / (float)steps);
            DrawSolidRect(new Rect(p.x - 5f, p.y - 2f, 10f, 4f), new Color(color.r, color.g, color.b, 0.68f));
        }
    }

    private void DrawContextCallout(Vector2 anchor, string text, Color accent)
    {
        float width = Mathf.Min(360f, Screen.width - 48f);
        Rect rect = new Rect(anchor.x - width * 0.5f, anchor.y - 17f, width, 34f);
        rect.x = Mathf.Clamp(rect.x, 18f, Screen.width - rect.width - 18f);
        rect.y = Mathf.Clamp(rect.y, 18f, Screen.height - rect.height - 18f);
        DrawSolidRect(rect, new Color(0.012f, 0.020f, 0.040f, 0.86f));
        DrawSolidRect(new Rect(rect.x, rect.y, rect.width, 2f), new Color(accent.r, accent.g, accent.b, 0.74f));
        GUI.Label(new Rect(rect.x + 10f, rect.y + 5f, rect.width - 20f, rect.height - 8f), text, BuildFittedSingleLineStyle(tutorialHeaderStyle, text, rect.width - 20f, rect.height - 8f, 9));
    }

    private string GetContextTutorialTitle()
    {
        switch (contextTutorialKind)
        {
            case ContextTutorialKind.Parry:
                return "La anomalia esta encima: usa Parry";
            case ContextTutorialKind.GhostDash:
                return "Demasiado cerca: activa Dash Fantasma";
            case ContextTutorialKind.Firewall:
                return "Firewall cargado: libera el Burst";
            case ContextTutorialKind.ScorePickup:
                return "Datos recuperados";
            case ContextTutorialKind.Powerup:
                return "Powerup instalado";
            case ContextTutorialKind.Upgrade:
                return "Alteraciones de run";
            case ContextTutorialKind.Interface:
                return "Como leer la interfaz";
            case ContextTutorialKind.ArenaEvent:
                return string.IsNullOrWhiteSpace(contextTutorialEventLabel) ? "Regla temporal de arena" : contextTutorialEventLabel;
            case ContextTutorialKind.Breach:
                return "Breach activo: busca la salida";
            case ContextTutorialKind.BossState:
                return string.IsNullOrWhiteSpace(contextTutorialEventLabel) ? "Nuevo protocolo de la anomalia" : contextTutorialEventLabel;
            case ContextTutorialKind.StateHijackUnlock:
                return "State Hijack desbloqueado";
            case ContextTutorialKind.StateHijack:
                return string.IsNullOrWhiteSpace(contextTutorialEventLabel) ? "Estado capturado" : contextTutorialEventLabel;
            default:
                return "Movimiento bajo presion";
        }
    }

    private string GetContextTutorialBody()
    {
        switch (contextTutorialKind)
        {
            case ContextTutorialKind.Parry:
                return "El jefe ya esta en rango de contacto. Presiona Parry justo antes del golpe para cortar la persecucion, empujarlo y cargar Firewall.";
            case ContextTutorialKind.GhostDash:
                return "El Dash Fantasma te vuelve intangible por un instante y te reposiciona. Usalo para cruzar persecuciones, proyectiles o salidas estrechas.";
            case ContextTutorialKind.Firewall:
                return "La barra esta completa. El Burst empuja enemigos, afecta clones y limpia proyectiles. Guardarlo demasiado tiempo te quita una salida defensiva.";
            case ContextTutorialKind.ScorePickup:
                return "Los datos suman puntaje, cargan Firewall y alimentan la progresion meta. En runs largas, recolectar bien abre mas opciones.";
            case ContextTutorialKind.Powerup:
                return "Los powerups cambian tu estado temporal. El rayo acelera, el escudo absorbe un golpe y el nucleo compacto te achica, aunque reduce tu velocidad.";
            case ContextTutorialKind.Upgrade:
                return "Las alteraciones cambian tu build durante la run. Mira categoria, impacto y rareza: no siempre gana la mejora mas ofensiva.";
            case ContextTutorialKind.Interface:
                return "La franja superior resume tiempo, puntaje y sector. Las barras muestran Dash, Firewall y habilidades; los objetivos temporales y eventos aparecen como avisos separados. No necesitas memorizar cada contrato: lee su meta y progreso en el HUD.";
            case ContextTutorialKind.ArenaEvent:
                return string.IsNullOrWhiteSpace(contextTutorialEventHint)
                    ? "Las arenas anuncian reglas temporales con color, flechas y zonas activas. Reacciona apenas aparece la alerta."
                    : contextTutorialEventHint;
            case ContextTutorialKind.Breach:
                return "La brecha abre una salida entre arenas. Segui el indicador hacia el portal antes de que el barrido glitch consuma el mapa.";
            case ContextTutorialKind.BossState:
            case ContextTutorialKind.StateHijack:
                return string.IsNullOrWhiteSpace(contextTutorialEventHint)
                    ? "Lee la señal visual y prepara tu siguiente decisión antes de reanudar."
                    : contextTutorialEventHint;
            case ContextTutorialKind.StateHijackUnlock:
                return "La anomalia evoluciono y ahora podes copiar su estado. Hace un parry directo al jefe para guardar una version util de su mecanica.";
            default:
                return "Podes mantener dos direcciones a la vez. Las diagonales te ayudan a rodear obstaculos y romper persecuciones simples.";
        }
    }

    private string GetContextTutorialHint()
    {
        switch (contextTutorialKind)
        {
            case ContextTutorialKind.Parry:
                return "Presiona ESPACIO o E para ejecutar el Parry ahora.";
            case ContextTutorialKind.GhostDash:
                return "Presiona SHIFT para ejecutar el Dash Fantasma ahora.";
            case ContextTutorialKind.Firewall:
                return "Presiona Q o R para liberar el Burst ahora.";
            case ContextTutorialKind.Movement:
                return $"Presiona cada direccion: {GetContextMovementChecklistText()}";
            case ContextTutorialKind.ArenaEvent:
            case ContextTutorialKind.Breach:
                return "Usa WASD o flechas para reaccionar y recuperar el control.";
            case ContextTutorialKind.ScorePickup:
            case ContextTutorialKind.Powerup:
            case ContextTutorialKind.Upgrade:
            case ContextTutorialKind.Interface:
            case ContextTutorialKind.BossState:
            case ContextTutorialKind.StateHijackUnlock:
                return "Lee la senal y haz click para continuar la run.";
            case ContextTutorialKind.StateHijack:
                return "Presiona F para ejecutar ahora la habilidad capturada.";
            default:
                return "Usa el input indicado por la mecanica.";
        }
    }

    private string GetContextTutorialFooter()
    {
        switch (contextTutorialKind)
        {
            case ContextTutorialKind.Parry:
                return "Input real: ESPACIO / E";
            case ContextTutorialKind.GhostDash:
                return "Input real: SHIFT";
            case ContextTutorialKind.Firewall:
                return "Input real: Q / R";
            case ContextTutorialKind.Movement:
                return $"Progreso: {GetContextMovementKeyCount()}/4";
            case ContextTutorialKind.ArenaEvent:
            case ContextTutorialKind.Breach:
                return "Input real: WASD / Flechas";
            case ContextTutorialKind.ScorePickup:
                return "Click: confirmar lectura";
            case ContextTutorialKind.Powerup:
                return "Click: confirmar powerup";
            case ContextTutorialKind.Upgrade:
                return "Click: abrir alteraciones";
            case ContextTutorialKind.Interface:
                return "Click: confirmar lectura del HUD";
            case ContextTutorialKind.BossState:
                return "Click: confirmar lectura del ataque";
            case ContextTutorialKind.StateHijackUnlock:
                return "Parry directo: capturar | F: ejecutar";
            case ContextTutorialKind.StateHijack:
                return "Input real: F";
            default:
                return "Entrenamiento contextual";
        }
    }

    private Color GetContextTutorialAccent()
    {
        switch (contextTutorialKind)
        {
            case ContextTutorialKind.Parry:
                return new Color(1f, 0.62f, 0.78f, 1f);
            case ContextTutorialKind.GhostDash:
                return new Color(0.55f, 1f, 0.94f, 1f);
            case ContextTutorialKind.Firewall:
                return new Color(1f, 0.84f, 0.42f, 1f);
            case ContextTutorialKind.ScorePickup:
                return new Color(0.88f, 0.98f, 1f, 1f);
            case ContextTutorialKind.Powerup:
                return new Color(0.55f, 1f, 0.78f, 1f);
            case ContextTutorialKind.Upgrade:
                return new Color(0.72f, 0.58f, 1f, 1f);
            case ContextTutorialKind.Interface:
                return new Color(0.46f, 0.88f, 1f, 1f);
            case ContextTutorialKind.ArenaEvent:
                return new Color(1f, 0.50f, 0.86f, 1f);
            case ContextTutorialKind.Breach:
                return new Color(0.46f, 0.96f, 1f, 1f);
            case ContextTutorialKind.BossState:
                return enemyController != null ? GetBossStateColor(enemyController.CurrentStateLabel) : new Color(1f, 0.46f, 0.68f, 1f);
            case ContextTutorialKind.StateHijackUnlock:
            case ContextTutorialKind.StateHijack:
                return playerController != null && playerController.HasStoredHijack
                    ? playerController.StoredHijackColor
                    : new Color(0.54f, 0.96f, 1f, 1f);
            default:
                return new Color(0.43f, 0.94f, 1f, 1f);
        }
    }

    private void DrawContextTutorialVisual(Rect rect, Color accent)
    {
        switch (contextTutorialKind)
        {
            case ContextTutorialKind.Parry:
                DrawIntroParryDemo(rect, accent);
                break;
            case ContextTutorialKind.GhostDash:
                DrawContextGhostDashDemo(rect, accent);
                break;
            case ContextTutorialKind.Firewall:
                DrawIntroFirewallDemo(rect, accent);
                break;
            case ContextTutorialKind.ScorePickup:
            case ContextTutorialKind.Powerup:
                DrawIntroResourcesDemo(rect, accent);
                break;
            case ContextTutorialKind.Upgrade:
                DrawContextUpgradeDemo(rect, accent);
                break;
            case ContextTutorialKind.Interface:
                DrawContextInterfaceDemo(rect, accent);
                break;
            case ContextTutorialKind.ArenaEvent:
            case ContextTutorialKind.Breach:
                DrawIntroEventsDemo(rect, accent);
                break;
            case ContextTutorialKind.BossState:
                DrawContextBossStateDemo(rect, accent);
                break;
            case ContextTutorialKind.StateHijackUnlock:
            case ContextTutorialKind.StateHijack:
                DrawContextStateHijackDemo(rect, accent);
                break;
            default:
                DrawIntroMovementDemo(rect, accent);
                break;
        }
    }

    private void DrawContextGhostDashDemo(Rect rect, Color accent)
    {
        DrawTutorialGrid(rect, accent);
        Vector2 center = rect.center;
        float time = Time.unscaledTime;
        Vector2 dashDir = Vector2.right;
        for (int i = 0; i < 4; i++)
        {
            float t = i / 3f;
            Vector2 pos = center - dashDir * Mathf.Lerp(68f, 12f, t);
            Color ghost = new Color(accent.r, accent.g, accent.b, Mathf.Lerp(0.10f, 0.42f, t));
            DrawSolidRect(new Rect(pos.x - 12f, pos.y - 12f, 24f, 24f), ghost);
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(time * 9f);
        DrawSolidRect(new Rect(center.x + 16f, center.y - 14f, 28f, 28f), new Color(accent.r, accent.g, accent.b, 0.82f));
        DrawSolidRect(new Rect(center.x - 58f, center.y - 3f, 88f, 6f), new Color(accent.r, accent.g, accent.b, 0.30f + pulse * 0.26f));
        DrawSolidRect(new Rect(center.x + 60f, center.y - 18f, 30f, 36f), new Color(1f, 0.38f, 0.52f, 0.76f));
        DrawTutorialLabel(new Rect(rect.x + 20f, rect.yMax - 34f, rect.width - 40f, 24f), "SHIFT = FANTASMA BREVE");
    }

    private void DrawContextStateHijackDemo(Rect rect, Color accent)
    {
        DrawTutorialGrid(rect, accent);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f);
        Rect enemy = new Rect(rect.x + (38f * hudScale), rect.center.y - (24f * hudScale), 48f * hudScale, 48f * hudScale);
        Rect player = new Rect(rect.xMax - (86f * hudScale), rect.center.y - (18f * hudScale), 36f * hudScale, 36f * hudScale);
        DrawSolidRect(enemy, new Color(1f, 0.30f, 0.46f, 0.92f));
        DrawTutorialRing(enemy.center, 34f * hudScale + pulse * 5f * hudScale, accent, 0.48f);
        DrawSolidRect(player, new Color(0.30f, 0.88f, 1f, 0.98f));

        Vector2 midpoint = Vector2.Lerp(enemy.center, player.center, 0.52f);
        DrawContextLine(enemy.center, player.center, accent);
        Rect captured = new Rect(midpoint.x - (25f * hudScale), midpoint.y - (25f * hudScale), 50f * hudScale, 50f * hudScale);
        DrawTutorialFrame(captured, new Color(accent.r, accent.g, accent.b, 0.82f), 3f * hudScale);
        GUI.Label(captured, "F", BuildFittedSingleLineStyle(upgradeTitleStyle, "F", captured.width, captured.height, 14));
        DrawTutorialLabel(new Rect(rect.x + 20f, rect.yMax - 38f, rect.width - 40f, 24f), "PARRY: COPIAR   |   F: EJECUTAR");
    }

    private void DrawContextBossStateDemo(Rect rect, Color accent)
    {
        DrawTutorialGrid(rect, accent);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6.5f);
        Vector2 center = rect.center;
        DrawTutorialRing(center, 76f + pulse * 9f, accent, 0.48f);
        DrawTutorialRing(center, 48f, new Color(accent.r, accent.g, accent.b, 0.72f), 0.38f);
        DrawSolidRect(new Rect(center.x - 22f, center.y - 22f, 44f, 44f), new Color(1f, 0.30f, 0.46f, 0.94f));
        for (int i = 0; i < 8; i++)
        {
            float angle = (Time.unscaledTime * 34f + i * 45f) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 position = center + direction * (88f + pulse * 6f);
            DrawSolidRect(new Rect(position.x - 7f, position.y - 3f, 14f, 6f), new Color(accent.r, accent.g, accent.b, 0.72f));
        }

        DrawTutorialLabel(new Rect(rect.x + 20f, rect.yMax - 38f, rect.width - 40f, 24f), "LEE EL TELEGRAPH ANTES DE MOVERTE");
    }

    private void DrawContextUpgradeDemo(Rect rect, Color accent)
    {
        DrawTutorialGrid(rect, accent);
        float cardW = (rect.width - 46f) / 3f;
        for (int i = 0; i < 3; i++)
        {
            Rect card = new Rect(rect.x + 16f + i * (cardW + 7f), rect.y + 42f, cardW, rect.height - 84f);
            Color c = i == 1 ? accent : new Color(0.56f, 0.72f, 1f, 1f);
            DrawSolidRect(card, new Color(0.025f, 0.035f, 0.065f, 0.92f));
            DrawSolidRect(new Rect(card.x, card.y, card.width, 3f), new Color(c.r, c.g, c.b, i == 1 ? 0.92f : 0.38f));
            DrawSolidRect(new Rect(card.x + 10f, card.y + 22f, card.width - 20f, 22f), new Color(c.r, c.g, c.b, 0.18f));
            DrawSolidRect(new Rect(card.x + 12f, card.y + 62f, card.width - 24f, 8f), new Color(1f, 0.82f, 0.42f, i == 1 ? 0.92f : 0.42f));
            DrawSolidRect(new Rect(card.x + 12f, card.yMax - 38f, card.width - 24f, 20f), new Color(0.08f, 0.16f, 0.25f, 0.88f));
        }
        DrawTutorialLabel(new Rect(rect.x + 20f, rect.yMax - 34f, rect.width - 40f, 24f), "ELIGE BUILD, NO SOLO BONUS");
    }

    private void DrawContextInterfaceDemo(Rect rect, Color accent)
    {
        DrawTutorialGrid(rect, accent);

        Rect topBar = new Rect(rect.x + 24f, rect.y + 22f, rect.width - 48f, 38f);
        DrawSolidRect(topBar, new Color(0.025f, 0.045f, 0.082f, 0.94f));
        DrawTutorialFrame(topBar, new Color(accent.r, accent.g, accent.b, 0.62f), 2f);

        float chipWidth = (topBar.width - 28f) / 3f;
        for (int i = 0; i < 3; i++)
        {
            Rect chip = new Rect(topBar.x + 8f + i * (chipWidth + 6f), topBar.y + 8f, chipWidth, 22f);
            DrawSolidRect(chip, new Color(accent.r, accent.g, accent.b, i == 1 ? 0.26f : 0.14f));
        }

        Rect abilityPanel = new Rect(rect.x + 34f, rect.y + 78f, rect.width * 0.42f, 66f);
        DrawSolidRect(abilityPanel, new Color(0.025f, 0.040f, 0.075f, 0.92f));
        DrawTutorialFrame(abilityPanel, new Color(0.46f, 0.96f, 1f, 0.52f), 2f);
        DrawSolidRect(new Rect(abilityPanel.x + 12f, abilityPanel.y + 15f, abilityPanel.width - 24f, 8f), new Color(0.46f, 0.96f, 1f, 0.74f));
        DrawSolidRect(new Rect(abilityPanel.x + 12f, abilityPanel.y + 39f, (abilityPanel.width - 24f) * 0.68f, 8f), new Color(1f, 0.82f, 0.42f, 0.82f));

        Rect objectivePanel = new Rect(rect.xMax - rect.width * 0.43f - 34f, rect.y + 78f, rect.width * 0.43f, 66f);
        DrawSolidRect(objectivePanel, new Color(0.025f, 0.040f, 0.075f, 0.92f));
        DrawTutorialFrame(objectivePanel, new Color(1f, 0.54f, 0.82f, 0.52f), 2f);
        DrawSolidRect(new Rect(objectivePanel.x + 12f, objectivePanel.y + 14f, objectivePanel.width - 24f, 10f), new Color(1f, 0.54f, 0.82f, 0.28f));
        DrawSolidRect(new Rect(objectivePanel.x + 12f, objectivePanel.y + 38f, (objectivePanel.width - 24f) * 0.54f, 9f), new Color(1f, 0.76f, 0.38f, 0.78f));

        DrawTutorialLabel(new Rect(rect.x + 20f, rect.yMax - 34f, rect.width - 40f, 24f), "ARRIBA: RUN   |   BARRAS: RECURSOS   |   AVISOS: OBJETIVOS");
    }

    private void AdvanceIntroTutorialStep()
    {
        if (introTutorialStep == IntroTutorialStep.Ready)
        {
            CloseIntroTutorial();
            return;
        }

        introTutorialStep = (IntroTutorialStep)((int)introTutorialStep + 1);
        introTutorialStepProgress = introTutorialStep == IntroTutorialStep.Ready ? 1f : 0f;
        introTutorialStepTimer = 0f;
        introTutorialActionFlash = 0.24f;
        GlitchAudioManager.PlayMenuConfirm();
    }

    private bool IsIntroStepComplete()
    {
        return introTutorialStep == IntroTutorialStep.Ready || introTutorialStepProgress >= 0.98f;
    }

    private Color GetIntroStepAccent()
    {
        switch (introTutorialStep)
        {
            case IntroTutorialStep.Parry:
                return new Color(1f, 0.62f, 0.78f, 1f);
            case IntroTutorialStep.Firewall:
                return new Color(1f, 0.84f, 0.42f, 1f);
            case IntroTutorialStep.Resources:
                return new Color(0.58f, 1f, 0.74f, 1f);
            case IntroTutorialStep.ArenaEvents:
                return new Color(0.86f, 0.48f, 1f, 1f);
            case IntroTutorialStep.Ready:
                return new Color(0.82f, 0.96f, 1f, 1f);
            default:
                return new Color(0.43f, 0.94f, 1f, 1f);
        }
    }

    private string GetIntroStepTitle()
    {
        switch (introTutorialStep)
        {
            case IntroTutorialStep.Parry:
                return "2. Firewall Parry";
            case IntroTutorialStep.Firewall:
                return "3. Firewall Burst";
            case IntroTutorialStep.Resources:
                return "4. Datos y mejoras";
            case IntroTutorialStep.ArenaEvents:
                return "5. Eventos de arena";
            case IntroTutorialStep.Ready:
                return "Protocolo listo";
            default:
                return "1. Movimiento base";
        }
    }

    private string GetIntroStepInstruction()
    {
        switch (introTutorialStep)
        {
            case IntroTutorialStep.Parry:
                return "Presiona ESPACIO, E o click izquierdo para abrir la ventana de parry. Si lo haces al contacto, empujas a la anomalia y cargas Firewall.";
            case IntroTutorialStep.Firewall:
                return "Presiona Q, R o click derecho para practicar el Burst. En partida solo se activa cuando la barra de Firewall esta llena.";
            case IntroTutorialStep.Resources:
                return "Los datos dan puntaje y progresion meta. Los powerups cambian tu ritmo: rayo para velocidad, escudo para absorber un golpe y nucleo compacto para esquivar mejor a cambio de velocidad. Presiona ESPACIO para confirmar.";
            case IntroTutorialStep.ArenaEvents:
                return "Cuando aparezcan flechas, barridos o brechas, leelos como ordenes urgentes del mapa. Sigue la direccion segura y evita quedarte dentro del glitch. Presiona ESPACIO para confirmar.";
            case IntroTutorialStep.Ready:
                return "Ya conoces lo basico. Ahora empieza la cuenta regresiva real y la anomalia entra en juego.";
            default:
                return "Presiona W, A, S y D una vez para registrar las direcciones basicas. Durante la run puedes combinar dos direcciones para moverte en diagonal.";
        }
    }

    private string GetIntroStepHint()
    {
        switch (introTutorialStep)
        {
            case IntroTutorialStep.Parry:
                return IsIntroStepComplete() ? "Parry registrado." : "Input esperado: ESPACIO / E / click izquierdo.";
            case IntroTutorialStep.Firewall:
                return IsIntroStepComplete() ? "Burst registrado." : "Input esperado: Q / R / click derecho.";
            case IntroTutorialStep.Resources:
                return IsIntroStepComplete() ? "Lectura confirmada." : "Observa los iconos y confirma con ESPACIO o click.";
            case IntroTutorialStep.ArenaEvents:
                return IsIntroStepComplete() ? "Ruta de escape confirmada." : "Observa el barrido y confirma con ESPACIO o click.";
            case IntroTutorialStep.Ready:
                return "Pulsa iniciar para entrar a la run.";
            default:
                return IsIntroStepComplete() ? "Movimiento registrado." : $"Pendiente: {GetIntroMovementChecklistText()}";
        }
    }

    private void DrawIntroStepDots(Rect rect)
    {
        int total = 6;
        float gap = 8f;
        float dotW = Mathf.Min(112f, (rect.width - gap * (total - 1)) / total);
        float startX = rect.x + (rect.width - (dotW * total + gap * (total - 1))) * 0.5f;
        Color accent = GetIntroStepAccent();

        for (int i = 0; i < total; i++)
        {
            Rect dot = new Rect(startX + i * (dotW + gap), rect.y + 4f, dotW, rect.height - 8f);
            bool active = i == (int)introTutorialStep;
            bool completed = i < (int)introTutorialStep;
            Color fill = active
                ? new Color(accent.r, accent.g, accent.b, 0.34f)
                : completed ? new Color(0.58f, 1f, 0.82f, 0.20f) : new Color(0.04f, 0.07f, 0.12f, 0.76f);
            DrawSolidRect(dot, fill);
            DrawTutorialFrame(dot, new Color(accent.r, accent.g, accent.b, active ? 0.64f : 0.20f), 1f);
        }
    }

    private void DrawIntroTutorialText(Rect rect)
    {
        Color accent = GetIntroStepAccent();
        DrawSolidRect(rect, new Color(0.012f, 0.020f, 0.040f, 0.76f));
        DrawSolidRect(new Rect(rect.x, rect.y, rect.width, 2f), new Color(accent.r, accent.g, accent.b, 0.72f));

        Rect kicker = new Rect(rect.x + 18f, rect.y + 18f, rect.width - 36f, 24f);
        GUI.Label(kicker, "ENTRENAMIENTO INTERACTIVO", BuildFittedSingleLineStyle(tutorialTinyStyle, "ENTRENAMIENTO INTERACTIVO", kicker.width, kicker.height, 9));

        Rect title = new Rect(rect.x + 18f, rect.y + 48f, rect.width - 36f, 42f);
        GUI.Label(title, GetIntroStepTitle(), BuildFittedSingleLineStyle(upgradeTitleStyle, GetIntroStepTitle(), title.width, title.height, 18));

        Rect body = new Rect(rect.x + 18f, rect.y + 106f, rect.width - 36f, 118f);
        GUI.Label(body, GetIntroStepInstruction(), tutorialBodyStyle);

        Rect hint = new Rect(rect.x + 18f, rect.yMax - 118f, rect.width - 36f, 34f);
        DrawSolidRect(hint, new Color(accent.r, accent.g, accent.b, 0.12f));
        GUI.Label(new Rect(hint.x + 10f, hint.y + 4f, hint.width - 20f, hint.height - 8f), GetIntroStepHint(), BuildFittedSingleLineStyle(tutorialHeaderStyle, GetIntroStepHint(), hint.width - 20f, hint.height - 8f, 10));
    }

    private void DrawIntroTutorialVisual(Rect rect)
    {
        Color accent = GetIntroStepAccent();
        DrawSolidRect(rect, new Color(0.009f, 0.014f, 0.028f, 0.90f));
        DrawTutorialFrame(rect, new Color(accent.r, accent.g, accent.b, 0.42f), 2f);
        DrawSolidRect(new Rect(rect.x + 14f, rect.y + 14f, rect.width - 28f, 2f), new Color(accent.r, accent.g, accent.b, 0.42f));
        DrawSolidRect(new Rect(rect.x + 14f, rect.yMax - 16f, rect.width - 28f, 2f), new Color(accent.r, accent.g, accent.b, 0.22f));

        Rect demo = new Rect(rect.x + 22f, rect.y + 30f, rect.width - 44f, rect.height - 62f);
        switch (introTutorialStep)
        {
            case IntroTutorialStep.Parry:
                DrawIntroParryDemo(demo, accent);
                break;
            case IntroTutorialStep.Firewall:
                DrawIntroFirewallDemo(demo, accent);
                break;
            case IntroTutorialStep.Resources:
                DrawIntroResourcesDemo(demo, accent);
                break;
            case IntroTutorialStep.ArenaEvents:
                DrawIntroEventsDemo(demo, accent);
                break;
            case IntroTutorialStep.Ready:
                DrawIntroReadyDemo(demo, accent);
                break;
            default:
                DrawIntroMovementDemo(demo, accent);
                break;
        }
    }

    private void DrawIntroMovementDemo(Rect rect, Color accent)
    {
        DrawTutorialGrid(rect, accent);
        Vector2 player = new Vector2(
            Mathf.Lerp(rect.x + 38f, rect.xMax - 38f, introTutorialDemoPlayer.x),
            Mathf.Lerp(rect.yMax - 38f, rect.y + 38f, introTutorialDemoPlayer.y));
        DrawArrowLine(new Vector2(rect.x + 70f, rect.yMax - 70f), player, accent);
        DrawSolidRect(new Rect(player.x - 13f, player.y - 13f, 26f, 26f), new Color(0.30f, 0.88f, 1f, 1f));
        DrawSolidRect(new Rect(player.x - 26f, player.y + 17f, 18f, 4f), new Color(accent.r, accent.g, accent.b, 0.58f));
        DrawSolidRect(new Rect(player.x + 15f, player.y - 22f, 24f, 4f), new Color(accent.r, accent.g, accent.b, 0.58f));

        float key = 34f;
        float x = rect.x + 20f;
        float y = rect.y + 22f;
        DrawKey(new Rect(x + key + 6f, y, key, key), "W", accent);
        DrawKey(new Rect(x, y + key + 6f, key, key), "A", accent);
        DrawKey(new Rect(x + key + 6f, y + key + 6f, key, key), "S", accent);
        DrawKey(new Rect(x + (key + 6f) * 2f, y + key + 6f, key, key), "D", accent);
    }

    private void DrawIntroParryDemo(Rect rect, Color accent)
    {
        DrawTutorialGrid(rect, accent);
        Vector2 center = rect.center;
        float radius = 50f + Mathf.Sin(Time.unscaledTime * 5f) * 6f + introTutorialActionFlash * 44f;
        DrawTutorialRing(center, radius, accent, 0.58f);
        DrawTutorialRing(center, radius * 0.68f, new Color(0.48f, 0.96f, 1f, 1f), 0.34f);
        DrawSolidRect(new Rect(center.x - 13f, center.y - 13f, 26f, 26f), new Color(0.30f, 0.88f, 1f, 1f));
        DrawSolidRect(new Rect(rect.x + 38f, center.y - 12f, 24f, 24f), new Color(1f, 0.22f, 0.32f, 1f));
        DrawArrowLine(new Vector2(rect.x + 70f, center.y), new Vector2(center.x - 28f, center.y), new Color(1f, 0.62f, 0.74f, 1f));
        DrawKey(new Rect(rect.xMax - 132f, rect.yMax - 48f, 58f, 28f), "ESP", accent);
        DrawKey(new Rect(rect.xMax - 66f, rect.yMax - 48f, 46f, 28f), "E", accent);
    }

    private void DrawIntroFirewallDemo(Rect rect, Color accent)
    {
        DrawTutorialGrid(rect, accent);
        Vector2 center = rect.center;
        float burst = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(introTutorialActionFlash / 0.34f));
        DrawTutorialRing(center, Mathf.Lerp(52f, 126f, 1f - burst), accent, Mathf.Lerp(0.30f, 0.68f, burst));
        for (int i = 0; i < 12; i++)
        {
            float angle = (360f / 12f) * i + Time.unscaledTime * 35f;
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            Vector2 p = center + dir * (46f + Mathf.Sin(Time.unscaledTime * 3f + i) * 4f);
            DrawSolidRect(new Rect(p.x - 2f, p.y - 12f, 4f, 24f), new Color(accent.r, accent.g, accent.b, 0.54f));
        }

        DrawSolidRect(new Rect(center.x - 13f, center.y - 13f, 26f, 26f), new Color(0.30f, 0.88f, 1f, 1f));
        Rect bar = new Rect(rect.x + 34f, rect.yMax - 44f, rect.width - 68f, 12f);
        DrawSolidRect(bar, new Color(0.03f, 0.04f, 0.08f, 0.92f));
        DrawSolidRect(new Rect(bar.x, bar.y, bar.width, bar.height), new Color(1f, 0.84f, 0.42f, 0.88f));
        DrawKey(new Rect(rect.xMax - 120f, rect.y + 22f, 44f, 30f), "Q", accent);
        DrawKey(new Rect(rect.xMax - 68f, rect.y + 22f, 44f, 30f), "R", accent);
    }

    private void DrawIntroResourcesDemo(Rect rect, Color accent)
    {
        DrawTutorialGrid(rect, accent);
        for (int i = 0; i < 8; i++)
        {
            float x = rect.x + 38f + i * 26f;
            float y = rect.y + 42f + Mathf.Sin(Time.unscaledTime * 2.2f + i) * 8f;
            DrawSolidRect(new Rect(x, y, 13f, 13f), new Color(0.92f, 0.96f, 1f, 0.88f));
        }

        Rect speed = new Rect(rect.center.x - 96f, rect.center.y - 34f, 38f, 54f);
        DrawSolidRect(new Rect(speed.x + 15f, speed.y, 14f, 26f), new Color(0.36f, 0.95f, 1f, 1f));
        DrawSolidRect(new Rect(speed.x + 3f, speed.y + 22f, 34f, 12f), new Color(0.36f, 0.95f, 1f, 1f));
        DrawSolidRect(new Rect(speed.x + 9f, speed.y + 34f, 14f, 24f), new Color(0.36f, 0.95f, 1f, 1f));

        Rect shield = new Rect(rect.center.x - 18f, rect.center.y - 34f, 42f, 54f);
        DrawSolidRect(new Rect(shield.x + 7f, shield.y, shield.width - 14f, 14f), new Color(1f, 0.70f, 0.90f, 0.92f));
        DrawSolidRect(new Rect(shield.x, shield.y + 13f, shield.width, 22f), new Color(1f, 0.70f, 0.90f, 0.92f));
        DrawSolidRect(new Rect(shield.x + 11f, shield.y + 35f, shield.width - 22f, 14f), new Color(1f, 0.70f, 0.90f, 0.92f));

        Rect compact = new Rect(rect.center.x + 62f, rect.center.y - 31f, 46f, 46f);
        DrawSolidRect(new Rect(compact.center.x - 8f, compact.center.y - 8f, 16f, 16f), new Color(0.74f, 1f, 0.70f, 0.96f));
        DrawSolidRect(new Rect(compact.x, compact.center.y - 4f, 14f, 8f), new Color(0.74f, 1f, 0.70f, 0.82f));
        DrawSolidRect(new Rect(compact.xMax - 14f, compact.center.y - 4f, 14f, 8f), new Color(0.74f, 1f, 0.70f, 0.82f));
        DrawSolidRect(new Rect(compact.center.x - 4f, compact.y, 8f, 14f), new Color(0.74f, 1f, 0.70f, 0.82f));
        DrawSolidRect(new Rect(compact.center.x - 4f, compact.yMax - 14f, 8f, 14f), new Color(0.74f, 1f, 0.70f, 0.82f));

        DrawTutorialLabel(new Rect(rect.x + 24f, rect.yMax - 42f, rect.width - 48f, 26f), "DATOS + POWERUPS CON COSTO");
    }

    private void DrawIntroEventsDemo(Rect rect, Color accent)
    {
        DrawTutorialGrid(rect, accent);
        DrawTutorialFrame(new Rect(rect.x + 26f, rect.y + 24f, rect.width - 52f, rect.height - 48f), new Color(0.62f, 0.78f, 1f, 0.36f), 2f);
        float sweepX = Mathf.Lerp(rect.x + 42f, rect.xMax - 84f, Mathf.PingPong(Time.unscaledTime * 0.22f, 1f));
        DrawSolidRect(new Rect(sweepX, rect.y + 24f, 10f, rect.height - 48f), new Color(1f, 0.35f, 0.78f, 0.78f));
        DrawSolidRect(new Rect(sweepX + 12f, rect.y + 24f, 34f, rect.height - 48f), new Color(1f, 0.35f, 0.78f, 0.18f));
        DrawSolidRect(new Rect(rect.xMax - 76f, rect.center.y - 34f, 24f, 68f), new Color(0.48f, 0.95f, 1f, 0.78f));
        DrawSolidRect(new Rect(rect.x + 54f, rect.center.y - 12f, 24f, 24f), new Color(0.30f, 0.88f, 1f, 1f));
        DrawArrowLine(new Vector2(rect.x + 88f, rect.center.y), new Vector2(rect.xMax - 86f, rect.center.y), accent);
        DrawTutorialLabel(new Rect(rect.x + 24f, rect.yMax - 42f, rect.width - 48f, 26f), "LEE LA ALERTA -> MUEVETE A LA SALIDA");
    }

    private void DrawIntroReadyDemo(Rect rect, Color accent)
    {
        DrawTutorialGrid(rect, accent);
        Vector2 center = rect.center;
        DrawTutorialRing(center, 96f, accent, 0.34f);
        DrawTutorialRing(center, 58f, new Color(0.48f, 0.96f, 1f, 1f), 0.42f);
        DrawSolidRect(new Rect(center.x - 18f, center.y - 18f, 36f, 36f), new Color(0.30f, 0.88f, 1f, 1f));
        GUI.Label(new Rect(rect.x + 24f, rect.yMax - 56f, rect.width - 48f, 34f), "CONTENCION LISTA", BuildFittedSingleLineStyle(upgradeTitleStyle, "CONTENCION LISTA", rect.width - 48f, 34f, 14));
    }

    private void DrawTutorialGrid(Rect rect, Color accent)
    {
        DrawSolidRect(rect, new Color(0.014f, 0.024f, 0.044f, 0.84f));
        for (float x = rect.x + 28f; x < rect.xMax; x += 44f)
        {
            DrawSolidRect(new Rect(x, rect.y + 8f, 1f, rect.height - 16f), new Color(accent.r, accent.g, accent.b, 0.055f));
        }
        for (float y = rect.y + 28f; y < rect.yMax; y += 44f)
        {
            DrawSolidRect(new Rect(rect.x + 8f, y, rect.width - 16f, 1f), new Color(accent.r, accent.g, accent.b, 0.055f));
        }
    }

    private void UpdateContextMovementKeyChecklist()
    {
        contextMoveWPressed |= WasTutorialMoveKeyPressed(KeyCode.W, TutorialMoveKey.W);
        contextMoveAPressed |= WasTutorialMoveKeyPressed(KeyCode.A, TutorialMoveKey.A);
        contextMoveSPressed |= WasTutorialMoveKeyPressed(KeyCode.S, TutorialMoveKey.S);
        contextMoveDPressed |= WasTutorialMoveKeyPressed(KeyCode.D, TutorialMoveKey.D);
    }

    private void UpdateIntroMovementKeyChecklist()
    {
        introMoveWPressed |= WasTutorialMoveKeyPressed(KeyCode.W, TutorialMoveKey.W);
        introMoveAPressed |= WasTutorialMoveKeyPressed(KeyCode.A, TutorialMoveKey.A);
        introMoveSPressed |= WasTutorialMoveKeyPressed(KeyCode.S, TutorialMoveKey.S);
        introMoveDPressed |= WasTutorialMoveKeyPressed(KeyCode.D, TutorialMoveKey.D);
    }

    private bool WasAnyTutorialMovementKeyPressed()
    {
        return WasTutorialMoveKeyPressed(KeyCode.W, TutorialMoveKey.W) ||
               WasTutorialMoveKeyPressed(KeyCode.A, TutorialMoveKey.A) ||
               WasTutorialMoveKeyPressed(KeyCode.S, TutorialMoveKey.S) ||
               WasTutorialMoveKeyPressed(KeyCode.D, TutorialMoveKey.D);
    }

    private int GetContextMovementKeyCount()
    {
        int count = 0;
        if (contextMoveWPressed) count++;
        if (contextMoveAPressed) count++;
        if (contextMoveSPressed) count++;
        if (contextMoveDPressed) count++;
        return count;
    }

    private int GetIntroMovementKeyCount()
    {
        int count = 0;
        if (introMoveWPressed) count++;
        if (introMoveAPressed) count++;
        if (introMoveSPressed) count++;
        if (introMoveDPressed) count++;
        return count;
    }

    private string GetContextMovementChecklistText()
    {
        return $"{FormatContextMoveKey("W", contextMoveWPressed)}  {FormatContextMoveKey("A", contextMoveAPressed)}  {FormatContextMoveKey("S", contextMoveSPressed)}  {FormatContextMoveKey("D", contextMoveDPressed)}";
    }

    private string GetIntroMovementChecklistText()
    {
        return $"{FormatContextMoveKey("W", introMoveWPressed)}  {FormatContextMoveKey("A", introMoveAPressed)}  {FormatContextMoveKey("S", introMoveSPressed)}  {FormatContextMoveKey("D", introMoveDPressed)}";
    }

    private static string FormatContextMoveKey(string key, bool done)
    {
        return done ? $"{key}:OK" : $"{key}:--";
    }

    private enum TutorialMoveKey
    {
        W,
        A,
        S,
        D
    }

    private static bool WasTutorialMoveKeyPressed(KeyCode legacyKey, TutorialMoveKey key)
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            switch (key)
            {
                case TutorialMoveKey.W:
                    return keyboard.wKey.wasPressedThisFrame;
                case TutorialMoveKey.A:
                    return keyboard.aKey.wasPressedThisFrame;
                case TutorialMoveKey.S:
                    return keyboard.sKey.wasPressedThisFrame;
                case TutorialMoveKey.D:
                    return keyboard.dKey.wasPressedThisFrame;
            }
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(legacyKey);
#else
        return false;
#endif
    }

    private static Vector2 ReadTutorialMoveInput()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            float horizontal = 0f;
            float vertical = 0f;

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) horizontal -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) horizontal += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) vertical -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) vertical += 1f;

            return new Vector2(horizontal, vertical);
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#else
        return Vector2.zero;
#endif
    }

    private static bool WasTutorialParryPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame))
        {
            return true;
        }

        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0);
#else
        return false;
#endif
    }

    private static bool WasTutorialGhostDashPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.leftShiftKey.wasPressedThisFrame || keyboard.rightShiftKey.wasPressedThisFrame))
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift);
#else
        return false;
#endif
    }

    private static bool WasTutorialFirewallPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (keyboard != null && (keyboard.qKey.wasPressedThisFrame || keyboard.rKey.wasPressedThisFrame))
        {
            return true;
        }

        if (mouse != null && mouse.rightButton.wasPressedThisFrame)
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.R) || Input.GetMouseButtonDown(1);
#else
        return false;
#endif
    }

    private static bool WasTutorialStateHijackPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.fKey.wasPressedThisFrame)
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.F);
#else
        return false;
#endif
    }

    private static bool WasTutorialConfirmPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
        {
            return true;
        }

        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetMouseButtonDown(0);
#else
        return false;
#endif
    }

    private static bool WasTutorialClickPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonDown(0);
#else
        return false;
#endif
    }

    private delegate void TutorialVisualDrawer(Rect rect, Color accent);

    private void DrawTutorialCard(Rect rect, string title, string body, Color accent, TutorialVisualDrawer visualDrawer)
    {
        DrawSolidRect(rect, new Color(0.015f, 0.025f, 0.05f, 0.78f));
        DrawSolidRect(new Rect(rect.x, rect.y, rect.width, 2f), new Color(accent.r, accent.g, accent.b, 0.72f));
        DrawSolidRect(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), new Color(accent.r, accent.g, accent.b, 0.24f));
        DrawSolidRect(new Rect(rect.x + 12f, rect.y + 12f, 58f, 4f), new Color(accent.r, accent.g, accent.b, 0.62f));

        Rect visual = new Rect(rect.x + 14f, rect.y + 24f, Mathf.Min(150f, rect.width * 0.34f), rect.height - 42f);
        visualDrawer(visual, accent);

        float textX = visual.xMax + 16f;
        Rect titleRect = new Rect(textX, rect.y + 24f, rect.xMax - textX - 14f, 28f);
        GUI.Label(titleRect, title, BuildFittedSingleLineStyle(tutorialHeaderStyle, title, titleRect.width, titleRect.height, 11));

        Rect bodyRect = new Rect(textX, titleRect.yMax + 8f, rect.xMax - textX - 14f, rect.yMax - titleRect.yMax - 22f);
        GUI.Label(bodyRect, body, tutorialBodyStyle);
    }

    private void DrawTutorialMovementVisual(Rect rect, Color accent)
    {
        DrawSolidRect(rect, new Color(0.03f, 0.06f, 0.10f, 0.82f));
        float key = Mathf.Min(32f, rect.width * 0.23f);
        float startX = rect.x + 12f;
        float startY = rect.y + 12f;
        DrawKey(new Rect(startX + key + 6f, startY, key, key), "W", accent);
        DrawKey(new Rect(startX, startY + key + 6f, key, key), "A", accent);
        DrawKey(new Rect(startX + key + 6f, startY + key + 6f, key, key), "S", accent);
        DrawKey(new Rect(startX + (key + 6f) * 2f, startY + key + 6f, key, key), "D", accent);

        Vector2 player = new Vector2(rect.xMax - 42f, rect.yMax - 38f);
        DrawSolidRect(new Rect(player.x - 11f, player.y - 11f, 22f, 22f), new Color(0.30f, 0.88f, 1f, 1f));
        DrawArrowLine(new Vector2(rect.x + 64f, rect.yMax - 30f), new Vector2(player.x - 18f, player.y), accent);
        DrawSolidRect(new Rect(player.x + 17f, player.y - 2f, 18f, 4f), new Color(accent.r, accent.g, accent.b, 0.58f));
    }

    private void DrawTutorialDefenseVisual(Rect rect, Color accent)
    {
        DrawSolidRect(rect, new Color(0.055f, 0.025f, 0.050f, 0.82f));
        float ringRadius = Mathf.Min(34f, Mathf.Min(rect.width * 0.24f, rect.height * 0.24f));
        Vector2 center = new Vector2(rect.center.x, rect.y + Mathf.Clamp(rect.height * 0.39f, 48f, 62f));
        DrawTutorialRing(center, ringRadius, accent, 0.52f);
        DrawTutorialRing(center, ringRadius * 0.72f, new Color(0.48f, 0.96f, 1f, 1f), 0.40f);
        DrawSolidRect(new Rect(center.x - 11f, center.y - 11f, 22f, 22f), new Color(0.30f, 0.88f, 1f, 1f));
        DrawSolidRect(new Rect(rect.x + 12f, center.y - 36f, 22f, 22f), new Color(1f, 0.20f, 0.28f, 1f));
        DrawArrowLine(new Vector2(rect.x + 40f, center.y - 25f), new Vector2(center.x - 18f, center.y - 8f), new Color(1f, 0.56f, 0.65f, 1f));

        float keyGap = 6f;
        float keyW = Mathf.Floor((rect.width - 28f - (keyGap * 2f)) / 3f);
        float keyY = rect.yMax - 48f;
        Rect parryLabel = new Rect(rect.x + 10f, keyY - 18f, (keyW * 2f) + keyGap, 16f);
        Rect burstLabel = new Rect(rect.x + 10f + ((keyW + keyGap) * 2f), keyY - 18f, keyW, 16f);
        DrawTutorialLabel(parryLabel, "PARRY");
        DrawTutorialLabel(burstLabel, "BURST");
        DrawKey(new Rect(rect.x + 10f, keyY, keyW, 24f), "ESP", accent);
        DrawKey(new Rect(rect.x + 10f + keyW + keyGap, keyY, keyW, 24f), "E", accent);
        DrawKey(new Rect(rect.x + 10f + ((keyW + keyGap) * 2f), keyY, keyW, 24f), "Q/R", new Color(1f, 0.86f, 0.46f, 1f));

        Rect charge = new Rect(rect.x + 14f, rect.yMax - 18f, rect.width - 28f, 7f);
        DrawSolidRect(charge, new Color(0.04f, 0.06f, 0.10f, 0.92f));
        DrawSolidRect(new Rect(charge.x, charge.y, charge.width * 0.78f, charge.height), new Color(1f, 0.86f, 0.46f, 0.86f));
    }

    private void DrawTutorialPowerupVisual(Rect rect, Color accent)
    {
        DrawSolidRect(rect, new Color(0.06f, 0.045f, 0.02f, 0.82f));
        Rect data = new Rect(rect.x + 14f, rect.y + 18f, 16f, 16f);
        for (int i = 0; i < 4; i++)
        {
            DrawSolidRect(new Rect(data.x + i * 22f, data.y + (i % 2) * 8f, data.width, data.height), new Color(0.95f, 0.96f, 1f, 0.88f));
        }

        Rect speed = new Rect(rect.center.x - 48f, rect.center.y - 24f, 28f, 44f);
        DrawSolidRect(new Rect(speed.x + 13f, speed.y, 12f, 22f), new Color(0.36f, 0.95f, 1f, 1f));
        DrawSolidRect(new Rect(speed.x + 3f, speed.y + 18f, 30f, 10f), new Color(0.36f, 0.95f, 1f, 1f));
        DrawSolidRect(new Rect(speed.x + 8f, speed.y + 28f, 12f, 20f), new Color(0.36f, 0.95f, 1f, 1f));

        Rect shield = new Rect(rect.center.x + 2f, rect.center.y - 23f, 30f, 38f);
        DrawSolidRect(new Rect(shield.x + 5f, shield.y, shield.width - 10f, 11f), new Color(1f, 0.70f, 0.90f, 0.92f));
        DrawSolidRect(new Rect(shield.x, shield.y + 10f, shield.width, 17f), new Color(1f, 0.70f, 0.90f, 0.92f));
        DrawSolidRect(new Rect(shield.x + 8f, shield.y + 27f, shield.width - 16f, 11f), new Color(1f, 0.70f, 0.90f, 0.92f));

        Rect compact = new Rect(rect.xMax - 42f, rect.yMax - 55f, 30f, 30f);
        DrawSolidRect(new Rect(compact.center.x - 5f, compact.center.y - 5f, 10f, 10f), new Color(0.74f, 1f, 0.70f, 0.96f));
        DrawSolidRect(new Rect(compact.x, compact.center.y - 3f, 10f, 6f), new Color(0.74f, 1f, 0.70f, 0.82f));
        DrawSolidRect(new Rect(compact.xMax - 10f, compact.center.y - 3f, 10f, 6f), new Color(0.74f, 1f, 0.70f, 0.82f));
        DrawSolidRect(new Rect(compact.center.x - 3f, compact.y, 6f, 10f), new Color(0.74f, 1f, 0.70f, 0.82f));
        DrawSolidRect(new Rect(compact.center.x - 3f, compact.yMax - 10f, 6f, 10f), new Color(0.74f, 1f, 0.70f, 0.82f));
        DrawTutorialLabel(new Rect(rect.x + 10f, rect.yMax - 27f, rect.width - 20f, 20f), "+ DATOS");
    }

    private void DrawTutorialEventsVisual(Rect rect, Color accent)
    {
        DrawSolidRect(rect, new Color(0.055f, 0.025f, 0.070f, 0.82f));
        Rect arena = new Rect(rect.x + 13f, rect.y + 16f, rect.width - 26f, rect.height - 32f);
        DrawTutorialFrame(arena, new Color(0.62f, 0.78f, 1f, 0.36f), 2f);
        float sweepX = arena.x + Mathf.Repeat(Time.unscaledTime * 38f, Mathf.Max(1f, arena.width - 20f));
        DrawSolidRect(new Rect(sweepX, arena.y, 8f, arena.height), new Color(1f, 0.35f, 0.78f, 0.70f));
        DrawSolidRect(new Rect(sweepX + 10f, arena.y, 24f, arena.height), new Color(1f, 0.35f, 0.78f, 0.18f));
        DrawSolidRect(new Rect(arena.xMax - 26f, arena.center.y - 22f, 18f, 44f), new Color(0.48f, 0.95f, 1f, 0.72f));
        DrawSolidRect(new Rect(arena.x + 28f, arena.center.y - 9f, 18f, 18f), new Color(0.30f, 0.88f, 1f, 1f));
        DrawArrowLine(new Vector2(arena.x + 52f, arena.center.y), new Vector2(arena.xMax - 34f, arena.center.y), accent);
        DrawTutorialLabel(new Rect(rect.x + 10f, rect.yMax - 27f, rect.width - 20f, 20f), "ESCAPA");
    }

    private void DrawKey(Rect rect, string text, Color accent)
    {
        DrawSolidRect(rect, new Color(0.06f, 0.10f, 0.16f, 0.95f));
        DrawTutorialFrame(rect, new Color(accent.r, accent.g, accent.b, 0.44f), 2f);
        GUI.Label(rect, text, BuildFittedSingleLineStyle(tutorialTinyStyle, text, rect.width - 4f, rect.height - 2f, 8));
    }

    private void DrawTutorialLabel(Rect rect, string text)
    {
        GUI.Label(rect, text, BuildFittedSingleLineStyle(tutorialTinyStyle, text, rect.width - 4f, rect.height - 2f, 8));
    }

    private void DrawArrowLine(Vector2 from, Vector2 to, Color color)
    {
        DrawSolidRect(new Rect(Mathf.Min(from.x, to.x), from.y - 2f, Mathf.Abs(to.x - from.x), 4f), new Color(color.r, color.g, color.b, 0.68f));
        DrawSolidRect(new Rect(to.x - 12f, to.y - 8f, 12f, 4f), new Color(color.r, color.g, color.b, 0.68f));
        DrawSolidRect(new Rect(to.x - 12f, to.y + 4f, 12f, 4f), new Color(color.r, color.g, color.b, 0.68f));
    }

    private void DrawTutorialRing(Vector2 center, float radius, Color color, float alpha)
    {
        float size = radius * 2f;
        Rect ring = new Rect(center.x - radius, center.y - radius, size, size);
        DrawTutorialFrame(ring, new Color(color.r, color.g, color.b, alpha), 3f);
    }

    private void DrawTutorialPanel(Rect rect, Color fill, Color border)
    {
        DrawSolidRect(rect, fill);
        DrawTutorialFrame(rect, border, 2f);
        DrawSolidRect(new Rect(rect.x + 8f, rect.y + 8f, 42f, 2f), new Color(border.r, border.g, border.b, 0.75f));
        DrawSolidRect(new Rect(rect.x + 8f, rect.y + 8f, 2f, 42f), new Color(border.r, border.g, border.b, 0.75f));
        DrawSolidRect(new Rect(rect.xMax - 50f, rect.y + 8f, 42f, 2f), new Color(border.r, border.g, border.b, 0.60f));
        DrawSolidRect(new Rect(rect.xMax - 10f, rect.y + 8f, 2f, 42f), new Color(border.r, border.g, border.b, 0.60f));
        DrawSolidRect(new Rect(rect.x + 8f, rect.yMax - 10f, 42f, 2f), new Color(border.r, border.g, border.b, 0.50f));
        DrawSolidRect(new Rect(rect.x + 8f, rect.yMax - 50f, 2f, 42f), new Color(border.r, border.g, border.b, 0.50f));
        DrawSolidRect(new Rect(rect.xMax - 50f, rect.yMax - 10f, 42f, 2f), new Color(border.r, border.g, border.b, 0.50f));
        DrawSolidRect(new Rect(rect.xMax - 10f, rect.yMax - 50f, 2f, 42f), new Color(border.r, border.g, border.b, 0.50f));
    }

    private void DrawTutorialFrame(Rect rect, Color color, float thickness)
    {
        DrawSolidRect(new Rect(rect.x, rect.y, rect.width, thickness), color);
        DrawSolidRect(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), color);
        DrawSolidRect(new Rect(rect.x, rect.y, thickness, rect.height), color);
        DrawSolidRect(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), color);
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

    // --- Cuenta regresiva y avisos urgentes ---------------------------------
    // Estas capas son temporales y pueden ocupar el centro porque comunican algo que requiere atención inmediata.
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
            return;
        }

        EnsureWarningStyle();
        float t = chaosDirector.ActiveWarningNormalized;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7.5f);
        float alpha = Mathf.Lerp(0.9f, 0.45f, t) + pulse * 0.08f;

        DrawEventAlertPanel(
            warning.ToUpperInvariant(),
            Screen.height * 0.2f,
            new Color(0.10f, 0.03f, 0.06f, 0.52f),
            new Color(1f, 0.56f, 0.66f, Mathf.Clamp01(alpha)),
            new Color(0.90f, 0.96f, 1f, 0.28f + pulse * 0.12f),
            new Color(1f, 0.56f, 0.66f, Mathf.Clamp01(alpha)));
    }

    private void DrawEventAlertPanel(string text, float y, Color fill, Color topLine, Color bottomLine, Color textColor)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        EnsureWarningStyle();
        float s = hudScale;
        float screenPadding = Mathf.Max(12f * s, 12f);
        float availableW = Mathf.Max(120f, Screen.width - screenPadding * 2f);
        float minPanelW = Mathf.Min(260f * s, availableW);
        float panelW = Mathf.Clamp(Mathf.Min(availableW, 820f * s), minPanelW, availableW);
        float textPaddingX = 18f * s;
        float textW = Mathf.Max(80f, panelW - textPaddingX * 2f);
        float maxTextH = Mathf.Max(42f * s, 64f * s);
        GUIContent content = new GUIContent(text);
        GUIStyle fittedStyle = BuildFittedWarningStyle(content, textW, maxTextH);
        float textH = Mathf.Clamp(fittedStyle.CalcHeight(content, textW), 24f * s, maxTextH);
        float panelH = Mathf.Clamp(textH + 18f * s, 48f * s, 92f * s);
        float x = (Screen.width - panelW) * 0.5f;
        y = Mathf.Clamp(y, 8f * s, Screen.height - panelH - (8f * s));

        DrawSolidRect(new Rect(x, y, panelW, panelH), fill);
        DrawSolidRect(new Rect(x, y, panelW, 2f * s), topLine);
        DrawSolidRect(new Rect(x, y + panelH - (2f * s), panelW, 2f * s), bottomLine);

        Rect textRect = new Rect(
            x + textPaddingX,
            y + (panelH - textH) * 0.5f,
            textW,
            textH);

        Color old = GUI.color;
        GUI.color = textColor;
        GUI.Label(textRect, text, fittedStyle);
        GUI.color = old;
    }

    private GUIStyle BuildFittedWarningStyle(GUIContent content, float width, float maxHeight)
    {
        int preferredSize = Mathf.RoundToInt(30f * hudScale);
        int minSize = Mathf.Max(13, Mathf.RoundToInt(16f * hudScale));
        GUIStyle style = new GUIStyle(eventWarningStyle)
        {
            wordWrap = true,
            clipping = TextClipping.Clip
        };

        for (int size = preferredSize; size >= minSize; size--)
        {
            style.fontSize = size;
            if (style.CalcHeight(content, width) <= maxHeight)
            {
                return style;
            }
        }

        style.fontSize = minSize;
        return style;
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

    private void TrackBossLevelTwoIntro()
    {
        if (bossLevelTwoIntroPlayed || !AreBossLevelTwoStatesUnlocked || enemyController == null)
        {
            return;
        }

        bossLevelTwoIntroPlayed = true;
        if (devForceBossLevelThree)
        {
            return;
        }

        bossLevelTwoIntroTimer = Mathf.Max(0.25f, bossLevelTwoIntroDuration);
        statePulseOverlayTimer = Mathf.Max(statePulseOverlayTimer, Mathf.Max(0.08f, statePulseOverlayDuration) * 1.8f);
        enemyController.TriggerLevelTwoAwakeningFx();
        if (devForceBossLevelTwo)
        {
            enemyController.ForceLevelTwoStateForDebug();
        }
        GlitchAudioManager.PlayBossLevelTwoAwaken(enemyController.transform.position);
    }

    private void UpdateDeveloperBossShortcuts()
    {
        if (!devForceBossLevelThree || enemyController == null || !IsRunActive)
        {
            return;
        }

        bool cyclePressed = false;
        bool topologyPressed = false;
#if ENABLE_INPUT_SYSTEM
        cyclePressed |= Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame;
        topologyPressed |= Keyboard.current != null && Keyboard.current.f10Key.wasPressedThisFrame;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        cyclePressed |= Input.GetKeyDown(KeyCode.F9);
        topologyPressed |= Input.GetKeyDown(KeyCode.F10);
#endif
        if (cyclePressed)
        {
            enemyController.ForceLevelThreeStateForDebug();
        }
        if (topologyPressed)
        {
            enemyController.ForceTopologyProjectileStateForDebug();
        }
    }

    private void TrackBossLevelThreeIntro()
    {
        if (bossLevelThreeIntroPlayed || !AreBossLevelThreeStatesUnlocked || enemyController == null)
        {
            return;
        }

        bossLevelThreeIntroPlayed = true;
        bossLevelThreeIntroTimer = Mathf.Max(0.25f, bossLevelThreeIntroDuration);
        statePulseOverlayTimer = Mathf.Max(statePulseOverlayTimer, Mathf.Max(0.08f, statePulseOverlayDuration) * 2.4f);
        enemyController.TriggerLevelThreeAwakeningFx();
        if (devForceBossLevelThree)
        {
            enemyController.ForceLevelThreeStateForDebug();
        }
        GlitchAudioManager.PlayBossLevelTwoAwaken(enemyController.transform.position);
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

    private void DrawBossLevelTwoIntroOverlay()
    {
        if (bossLevelTwoIntroTimer <= 0f)
        {
            return;
        }

        EnsureBossStateStyles();

        float duration = Mathf.Max(0.25f, bossLevelTwoIntroDuration);
        float remaining = Mathf.Clamp01(bossLevelTwoIntroTimer / duration);
        float age = 1f - remaining;
        float intro = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age * 4f));
        float outro = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(remaining * 3f));
        float alpha = Mathf.Clamp01(Mathf.Min(intro, outro));
        if (alpha <= 0.01f)
        {
            return;
        }

        float s = hudScale;
        Color primary = bossLevelTwoIntroPrimary;
        Color secondary = bossLevelTwoIntroSecondary;
        float scan = Mathf.Repeat(Time.unscaledTime * 420f, Screen.width + 180f) - 90f;
        float jitter = Mathf.Sin(Time.unscaledTime * 42f) * 4f * s * alpha;

        DrawSolidRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.004f, 0.010f, 0.018f, bossLevelTwoIntroBackdropOpacity * 0.34f * alpha));
        DrawSolidRect(new Rect(0f, Screen.height * 0.20f, Screen.width, 5f * s), new Color(primary.r, primary.g, primary.b, 0.48f * alpha));
        DrawSolidRect(new Rect(0f, Screen.height * 0.80f, Screen.width, 5f * s), new Color(secondary.r, secondary.g, secondary.b, 0.44f * alpha));
        DrawSolidRect(new Rect(scan, 0f, 20f * s, Screen.height), new Color(primary.r, primary.g, primary.b, 0.18f * alpha));
        DrawSolidRect(new Rect(scan + 28f * s, 0f, 6f * s, Screen.height), new Color(secondary.r, secondary.g, secondary.b, 0.24f * alpha));

        for (int i = 0; i < 8; i++)
        {
            float y = Mathf.Lerp(Screen.height * 0.25f, Screen.height * 0.76f, i / 7f);
            float w = Mathf.Lerp(80f, 220f, Mathf.PingPong(Time.unscaledTime * 1.7f + i * 0.19f, 1f)) * s;
            float x = Mathf.Repeat(Time.unscaledTime * (80f + i * 12f) + i * 157f, Screen.width + w) - w;
            Color c = i % 2 == 0 ? primary : secondary;
            DrawSolidRect(new Rect(x, y, w, 2f * s), new Color(c.r, c.g, c.b, 0.18f * alpha));
        }

        float width = Mathf.Min(Screen.width * 0.78f, 820f * s);
        float height = 138f * s;
        Rect panel = new Rect((Screen.width - width) * 0.5f + jitter, (Screen.height - height) * 0.5f, width, height);
        DrawSolidRect(panel, new Color(0.012f, 0.020f, 0.040f, 0.82f * alpha));
        DrawSolidRect(new Rect(panel.x, panel.y, panel.width, 4f * s), new Color(primary.r, primary.g, primary.b, 0.88f * alpha));
        DrawSolidRect(new Rect(panel.x, panel.yMax - (4f * s), panel.width, 4f * s), new Color(secondary.r, secondary.g, secondary.b, 0.78f * alpha));
        DrawSolidRect(new Rect(panel.x + (18f * s), panel.y + (18f * s), panel.width - (36f * s), 2f * s), new Color(0.88f, 0.96f, 1f, 0.22f * alpha));
        DrawSolidRect(new Rect(panel.x + (18f * s), panel.yMax - (20f * s), panel.width - (36f * s), 2f * s), new Color(0.88f, 0.96f, 1f, 0.18f * alpha));

        Color oldGui = GUI.color;
        Color oldLabel = bossStateBannerLabelStyle.normal.textColor;
        Color oldValue = bossStateBannerValueStyle.normal.textColor;
        GUI.color = new Color(1f, 1f, 1f, alpha);
        bossStateBannerLabelStyle.normal.textColor = new Color(primary.r, primary.g, primary.b, 0.92f);
        bossStateBannerValueStyle.normal.textColor = Color.Lerp(Color.white, secondary, 0.24f);
        GUI.Label(new Rect(panel.x, panel.y + (22f * s), panel.width, 28f * s), "PROTOCOLO DE CONTENCION", bossStateBannerLabelStyle);
        GUI.Label(new Rect(panel.x, panel.y + (50f * s), panel.width, 62f * s), "ANOMALIA NIVEL 2", bossStateBannerValueStyle);
        bossStateBannerLabelStyle.normal.textColor = new Color(0.82f, 0.94f, 1f, 0.86f);
        GUI.Label(new Rect(panel.x, panel.y + (104f * s), panel.width, 28f * s), "NUEVOS PATRONES ACTIVOS", bossStateBannerLabelStyle);
        bossStateBannerLabelStyle.normal.textColor = oldLabel;
        bossStateBannerValueStyle.normal.textColor = oldValue;
        GUI.color = oldGui;
    }

    private void DrawBossLevelThreeIntroOverlay()
    {
        if (bossLevelThreeIntroTimer <= 0f)
        {
            return;
        }

        EnsureBossStateStyles();
        float duration = Mathf.Max(0.25f, bossLevelThreeIntroDuration);
        float remaining = Mathf.Clamp01(bossLevelThreeIntroTimer / duration);
        float age = 1f - remaining;
        float alpha = Mathf.Min(
            Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(age * 3.6f)),
            Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(remaining * 3f)));
        if (alpha <= 0.01f)
        {
            return;
        }

        float s = hudScale;
        Color primary = bossLevelThreeIntroPrimary;
        Color secondary = bossLevelThreeIntroSecondary;
        DrawSolidRect(new Rect(0f, 0f, Screen.width, Screen.height),
            new Color(0.008f, 0.004f, 0.016f, bossLevelThreeIntroBackdropOpacity * 0.42f * alpha));

        for (int i = 0; i < 12; i++)
        {
            float direction = i % 2 == 0 ? 1f : -1f;
            float width = Mathf.Lerp(90f, 320f, Mathf.PingPong(Time.unscaledTime * 1.2f + i * 0.17f, 1f)) * s;
            float x = direction > 0f
                ? Mathf.Repeat(Time.unscaledTime * (130f + i * 9f) + i * 113f, Screen.width + width) - width
                : Screen.width - Mathf.Repeat(Time.unscaledTime * (130f + i * 9f) + i * 113f, Screen.width + width);
            float y = Mathf.Lerp(Screen.height * 0.12f, Screen.height * 0.88f, i / 11f);
            Color color = i % 2 == 0 ? primary : secondary;
            DrawSolidRect(new Rect(x, y, width, (i % 3 == 0 ? 4f : 2f) * s),
                new Color(color.r, color.g, color.b, 0.22f * alpha));
        }

        float panelWidth = Mathf.Min(Screen.width * 0.82f, 900f * s);
        float panelHeight = 158f * s;
        float jitter = Mathf.Sin(Time.unscaledTime * 51f) * 5f * s * alpha;
        Rect panel = new Rect((Screen.width - panelWidth) * 0.5f + jitter, (Screen.height - panelHeight) * 0.5f, panelWidth, panelHeight);
        DrawSolidRect(panel, new Color(0.012f, 0.014f, 0.032f, 0.9f * alpha));
        DrawSolidRect(new Rect(panel.x, panel.y, panel.width, 5f * s), new Color(primary.r, primary.g, primary.b, 0.94f * alpha));
        DrawSolidRect(new Rect(panel.x, panel.yMax - 5f * s, panel.width, 5f * s), new Color(secondary.r, secondary.g, secondary.b, 0.9f * alpha));
        float scanX = panel.x + Mathf.Repeat(Time.unscaledTime * 520f, panel.width + 60f * s) - 30f * s;
        DrawSolidRect(new Rect(scanX, panel.y, 14f * s, panel.height), new Color(secondary.r, secondary.g, secondary.b, 0.2f * alpha));

        Color oldGui = GUI.color;
        Color oldLabel = bossStateBannerLabelStyle.normal.textColor;
        Color oldValue = bossStateBannerValueStyle.normal.textColor;
        GUI.color = new Color(1f, 1f, 1f, alpha);
        bossStateBannerLabelStyle.normal.textColor = new Color(secondary.r, secondary.g, secondary.b, 0.94f);
        bossStateBannerValueStyle.normal.textColor = Color.Lerp(Color.white, primary, 0.32f);
        GUI.Label(new Rect(panel.x, panel.y + 22f * s, panel.width, 30f * s), "PROTOCOLO ADAPTATIVO INESTABLE", bossStateBannerLabelStyle);
        GUI.Label(new Rect(panel.x, panel.y + 52f * s, panel.width, 66f * s), "ANOMALIA NIVEL 3", bossStateBannerValueStyle);
        bossStateBannerLabelStyle.normal.textColor = new Color(0.88f, 0.95f, 1f, 0.9f);
        GUI.Label(new Rect(panel.x, panel.y + 120f * s, panel.width, 28f * s), "LA ARENA YA NO ES UNA REGLA FIJA", bossStateBannerLabelStyle);
        bossStateBannerLabelStyle.normal.textColor = oldLabel;
        bossStateBannerValueStyle.normal.textColor = oldValue;
        GUI.color = oldGui;
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
        HudTopLayout layout = GetHudTopLayout(s);
        Rect panel = new Rect(
            layout.focus.x + (10f * s),
            layout.focus.y + (6f * s),
            layout.focus.width - (20f * s),
            Mathf.Max(24f * s, layout.focus.height * 0.43f));
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7.5f);
        Color stateColor = GetBossStateColor(stateRaw);

        DrawSolidRect(new Rect(panel.x, panel.y, 3f * s, panel.height), new Color(stateColor.r, stateColor.g, stateColor.b, 0.64f + pulse * 0.20f));
        DrawSolidRect(new Rect(panel.x + (8f * s), panel.yMax - (1f * s), panel.width - (8f * s), 1f * s), new Color(stateColor.r, stateColor.g, stateColor.b, 0.25f));

        float markerW = Mathf.Lerp(panel.width * 0.16f, panel.width * 0.42f, pulse);
        DrawSolidRect(new Rect(panel.x + (8f * s), panel.y + (2f * s), markerW, 2f * s), new Color(stateColor.r, stateColor.g, stateColor.b, 0.58f));

        Color oldValue = bossStateValueStyle.normal.textColor;
        bossStateValueStyle.normal.textColor = Color.Lerp(Color.white, stateColor, 0.42f);
        Rect stateIcon = new Rect(panel.x + (10f * s), panel.y + (5f * s), 18f * s, 18f * s);
        DrawHudMetricIcon(stateIcon, "anomaly", stateColor);
        Rect stateLabel = new Rect(stateIcon.xMax + (8f * s), panel.y, panel.width - (44f * s), panel.height);
        GUI.Label(
            stateLabel,
            stateValue.ToUpperInvariant(),
            BuildFittedSingleLineStyle(bossStateValueStyle, stateValue.ToUpperInvariant(), stateLabel.width, stateLabel.height, Mathf.RoundToInt(9f * s)));
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

        string header = IsBossLevelThreeState(bossStateBannerRaw)
            ? "ANOMALIA NIVEL 3"
            : IsBossLevelTwoState(bossStateBannerRaw) ? "ANOMALIA NIVEL 2" : "CAMBIO DE ESTADO";
        GUI.Label(new Rect(panel.x, panel.y + (16f * s), panel.width, 26f * s), header, bossStateBannerLabelStyle);
        GUI.Label(new Rect(panel.x, panel.y + (42f * s), panel.width, 58f * s), label, bossStateBannerValueStyle);

        bossStateBannerLabelStyle.normal.textColor = oldLabel;
        bossStateBannerValueStyle.normal.textColor = oldValue;
        GUI.color = oldGui;
    }

    private bool ShouldOpenUpgradeSelection()
    {
        if (activeOperation.id == ContainmentOperationStorage.ContractId)
        {
            return false;
        }

        if (enableHybridDifficultyDirector && SurvivalTime >= nextUpgradeTime && !IsUpgradeSelectionWindowSafe())
        {
            return false;
        }

        if (enableRunUpgrades &&
            !upgradeSelectionOpen &&
            IsRunActive &&
            SurvivalTime >= nextUpgradeTime &&
            playerController != null &&
            TryOpenContextTutorial(ContextTutorialKind.Upgrade))
        {
            return false;
        }

        return enableRunUpgrades &&
               !upgradeSelectionOpen &&
               IsRunActive &&
               SurvivalTime >= nextUpgradeTime &&
               playerController != null;
    }

    private bool IsUpgradeSelectionWindowSafe()
    {
        return GetCurrentEventPressureLoad() <= 0.001f &&
               eventPressureCooldownTimer <= 0f &&
               SurvivalTime >= directorRecoveryUntil &&
               !IsBreachSensitiveSuppressionActive &&
               (enemyController == null || !enemyController.IsCurrentStateMajor);
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
            PlayerUpgradeKind.FirewallBurstRadius
        };

        AddUnlockedUpgrade(pool, PlayerUpgradeKind.FirewallBurstStun, MetaProgressionStorage.UnlockFirewallBurstStun);
        AddUnlockedUpgrade(pool, PlayerUpgradeKind.HazardResistance, MetaProgressionStorage.UnlockHazardResistance);
        AddUnlockedUpgrade(pool, PlayerUpgradeKind.DisplacementStabilizer, MetaProgressionStorage.UnlockDisplacementStabilizer);
        AddUnlockedUpgrade(pool, PlayerUpgradeKind.HazardFirewallCharge, MetaProgressionStorage.UnlockHazardFirewallCharge);
        AddUnlockedUpgrade(pool, PlayerUpgradeKind.VectorCore, MetaProgressionStorage.UnlockVectorCore);
        AddUnlockedUpgrade(pool, PlayerUpgradeKind.EmergencyShield, MetaProgressionStorage.UnlockEmergencyShield);
        AddUnlockedUpgrade(pool, PlayerUpgradeKind.ParryCapacitor, MetaProgressionStorage.UnlockParryCapacitor);

        int optionBonus = MetaProgressionStorage.IsUnlocked(MetaProgressionStorage.UnlockExtraUpgradeChoice) ? 1 : 0;
        int desired = Mathf.Clamp(upgradeOptionsShown + optionBonus, 1, pool.Count);
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
            upgradeCurrentLimitSeconds = GetUpgradeChoiceLimitForCurrentBossLevel();
            upgradeTimeRemaining = upgradeCurrentLimitSeconds;
            upgradeExitTimer = 0f;
            upgradeSelectedIndex = -1;
            upgradeSelectedAccent = Color.white;
            Time.timeScale = 0f;
            GlitchAudioManager.PlayUpgradeOpen();
        }
    }

    private static void AddUnlockedUpgrade(List<PlayerUpgradeKind> pool, PlayerUpgradeKind kind, string unlockId)
    {
        if (MetaProgressionStorage.IsUnlocked(unlockId))
        {
            pool.Add(kind);
        }
    }

    private float GetUpgradeChoiceLimitForCurrentBossLevel()
    {
        float minimum = Mathf.Max(1f, upgradeMinimumChoiceSeconds);
        bool levelThree = devForceBossLevelThree ||
            SurvivalTime >= Mathf.Max(bossLevelTwoUnlockTime, bossLevelThreeUnlockTime);
        if (levelThree)
        {
            return Mathf.Max(minimum, upgradeLevelThreeChoiceSeconds);
        }

        bool levelTwo = devForceBossLevelTwo ||
            SurvivalTime >= Mathf.Max(bossSpecialStatesUnlockTime, bossLevelTwoUnlockTime);
        if (levelTwo)
        {
            return Mathf.Max(minimum, upgradeLevelTwoChoiceSeconds);
        }

        return Mathf.Max(minimum, upgradeChoiceLimitSeconds);
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
                    icon = ">>",
                    impact = "VELOCIDAD +",
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
                    icon = "[]",
                    impact = "PARRY DURA MAS",
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
                    icon = "<>",
                    impact = "PARRY RECARGA ANTES",
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
                    icon = "()",
                    impact = "PARRY MAS GRANDE",
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
                    icon = "+%",
                    impact = "FIREWALL CARGA +",
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
                    icon = "O+",
                    impact = "BURST MAS GRANDE",
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
                    icon = "!!",
                    impact = "STUN MAS LARGO",
                    accent = new Color(1f, 0.54f, 0.72f, 1f)
                };
            case PlayerUpgradeKind.HazardResistance:
                return new UpgradeChoice
                {
                    kind = kind,
                    title = "Filtro Ambiental",
                    description = "Reduce la duracion e intensidad de slows provocados por la arena.",
                    category = "SUPERVIVENCIA",
                    rarity = "INESTABLE",
                    icon = "##",
                    impact = "SLOWS MAS DEBILES",
                    accent = new Color(0.52f, 1f, 0.86f, 1f)
                };
            case PlayerUpgradeKind.DisplacementStabilizer:
                return new UpgradeChoice
                {
                    kind = kind,
                    title = "Anclaje Inercial",
                    description = "Reduce empujes, corrientes y desplazamientos externos del mapa.",
                    category = "MOVIMIENTO",
                    rarity = "INESTABLE",
                    icon = "_|",
                    impact = "MENOS EMPUJE",
                    accent = new Color(0.50f, 0.72f, 1f, 1f)
                };
            case PlayerUpgradeKind.HazardFirewallCharge:
                return new UpgradeChoice
                {
                    kind = kind,
                    title = "Reciclaje de Riesgo",
                    description = "Los slows y empujes de la arena cargan un poco el Firewall.",
                    category = "BUILD",
                    rarity = "CRITICO",
                    icon = "x+",
                    impact = "ARENA CARGA FIREWALL",
                    accent = new Color(1f, 0.72f, 0.40f, 1f)
                };
            case PlayerUpgradeKind.VectorCore:
                return new UpgradeChoice
                {
                    kind = kind,
                    title = "Nucleo Vectorial",
                    description = "Aumenta velocidad base y reduce empujes externos del mapa.",
                    category = "MOVIMIENTO",
                    rarity = "CRITICO",
                    icon = ">>",
                    impact = "RAPIDO Y ESTABLE",
                    accent = new Color(0.46f, 1f, 0.92f, 1f)
                };
            case PlayerUpgradeKind.EmergencyShield:
                return new UpgradeChoice
                {
                    kind = kind,
                    title = "Escudo de Emergencia",
                    description = "Activa un escudo inmediato y mejora la duracion de futuros escudos.",
                    category = "DEFENSA",
                    rarity = "CRITICO",
                    icon = "[]",
                    impact = "ESCUDO AHORA",
                    accent = new Color(0.78f, 1f, 0.62f, 1f)
                };
            case PlayerUpgradeKind.ParryCapacitor:
                return new UpgradeChoice
                {
                    kind = kind,
                    title = "Capacitor Parry",
                    description = "Reduce recarga del parry y acelera la carga del Firewall Burst.",
                    category = "FIREWALL",
                    rarity = "CRITICO",
                    icon = "+>",
                    impact = "PARRY CARGA MAS",
                    accent = new Color(1f, 0.84f, 0.48f, 1f)
                };
            default:
                return new UpgradeChoice
                {
                    kind = kind,
                    title = "Escudo Resonante",
                    description = "Los escudos duran mas cuando los recolectas.",
                    category = "DEFENSA",
                    rarity = "COMUN",
                    icon = "[]",
                    impact = "ESCUDOS DURAN MAS",
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
        upgradeCurrentLimitSeconds = 0f;
        upgradeExitTimer = 0f;
        upgradeSelectedIndex = -1;
        nextUpgradeTime = SurvivalTime + Mathf.Max(8f, upgradeInterval);
        directorRecoveryUntil = Mathf.Max(directorRecoveryUntil, SurvivalTime + Mathf.Max(0f, postUpgradeRecoverySeconds));
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
                playerController.ImproveFirewallChargeGain(1.16f);
                break;
            case PlayerUpgradeKind.FirewallBurstRadius:
                playerController.ExpandFirewallBurstRadius(0.42f);
                break;
            case PlayerUpgradeKind.FirewallBurstStun:
                playerController.ImproveFirewallBurstStun(0.22f);
                break;
            case PlayerUpgradeKind.HazardResistance:
                playerController.ImproveHazardResistance(0.82f);
                break;
            case PlayerUpgradeKind.DisplacementStabilizer:
                playerController.ImproveExternalDisplacementResistance(0.78f);
                break;
            case PlayerUpgradeKind.HazardFirewallCharge:
                playerController.ImproveHazardFirewallCharge(2.4f);
                break;
            case PlayerUpgradeKind.VectorCore:
                playerController.AddPermanentMoveSpeed(0.95f);
                playerController.ImproveExternalDisplacementResistance(0.84f);
                break;
            case PlayerUpgradeKind.EmergencyShield:
                playerController.ImproveShieldDuration(1.28f);
                playerController.ApplyShield(4.2f);
                break;
            case PlayerUpgradeKind.ParryCapacitor:
                playerController.ReduceParryCooldown(0.82f);
                playerController.ImproveFirewallChargeGain(1.20f);
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
        float hoverPulse = hovered ? 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 9f) : 0f;
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
        DrawSolidRect(new Rect(animatedCard.x, animatedCard.yMax - (2f * s), animatedCard.width, 2f * s), new Color(choice.accent.r, choice.accent.g, choice.accent.b, (hovered ? 0.58f : 0.24f) * cardAlpha));

        Rect metaRect = new Rect(animatedCard.x + (16f * s), animatedCard.y + (30f * s), animatedCard.width - (32f * s), 22f * s);
        Color rarityColor = GetUpgradeRarityColor(choice.rarity, choice.accent);
        DrawSolidRect(metaRect, new Color(rarityColor.r, rarityColor.g, rarityColor.b, 0.18f * cardAlpha));

        Rect iconRect = new Rect(animatedCard.center.x - (30f * s), animatedCard.y + (66f * s), 60f * s, 58f * s);
        DrawSolidRect(iconRect, new Color(choice.accent.r, choice.accent.g, choice.accent.b, (0.16f + hoverPulse * 0.08f) * cardAlpha));
        DrawSolidRect(new Rect(iconRect.x, iconRect.y, iconRect.width, 2f * s), new Color(choice.accent.r, choice.accent.g, choice.accent.b, 0.72f * cardAlpha));
        DrawSolidRect(new Rect(iconRect.x, iconRect.yMax - (2f * s), iconRect.width, 2f * s), new Color(0.82f, 0.94f, 1f, 0.28f * cardAlpha));

        Color old = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, cardAlpha);
        string meta = $"{choice.category} / {choice.rarity}";
        GUI.Label(metaRect, meta, BuildFittedSingleLineStyle(upgradeMetaStyle, meta, metaRect.width - (10f * s), metaRect.height, Mathf.RoundToInt(8f * s)));
        GUI.Label(iconRect, choice.icon, upgradeIconStyle);

        Rect buttonRect = new Rect(animatedCard.x + (16f * s), animatedCard.yMax - (52f * s), animatedCard.width - (32f * s), 34f * s);
        Rect impactRect = new Rect(animatedCard.x + (18f * s), buttonRect.y - (38f * s), animatedCard.width - (36f * s), 28f * s);
        float titleY = animatedCard.y + (140f * s);
        Rect titleRect = new Rect(
            animatedCard.x + (16f * s),
            titleY,
            animatedCard.width - (32f * s),
            Mathf.Max(36f * s, impactRect.y - titleY - (8f * s)));
        GUI.Label(titleRect, choice.title.ToUpperInvariant(), upgradeCardTitleStyle);
        DrawSolidRect(impactRect, new Color(choice.accent.r, choice.accent.g, choice.accent.b, 0.12f * cardAlpha));
        GUI.Label(impactRect, choice.impact, BuildFittedSingleLineStyle(upgradeImpactStyle, choice.impact, impactRect.width - (10f * s), impactRect.height, Mathf.RoundToInt(9f * s)));

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
        float limit = Mathf.Max(1f, upgradeCurrentLimitSeconds > 0f
            ? upgradeCurrentLimitSeconds
            : GetUpgradeChoiceLimitForCurrentBossLevel());
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

    private static Color GetUpgradeRarityColor(string rarity, Color fallback)
    {
        switch (rarity)
        {
            case "CRITICO":
                return new Color(1f, 0.54f, 0.72f, 1f);
            case "INESTABLE":
                return new Color(1f, 0.78f, 0.42f, 1f);
            case "COMUN":
                return new Color(0.62f, 0.92f, 1f, 1f);
            default:
                return fallback;
        }
    }

    private static string ToBossStateLabel(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "Unknown";
        }

        switch (raw)
        {
            case "BasePursuit":
                return "Base Pursuit";
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
            case "PhaseBlink":
                return "Phase Blink";
            case "PincerBarrage":
                return "Pincer Barrage";
            case "SignalJam":
                return "Signal Jam";
            case "OrbitBarrage":
                return "Orbit Barrage";
            case "ReplayPredator":
                return "Replay Predator";
            case "ChecksumLattice":
                return "Checksum Lattice";
            case "InputDesync":
                return "Input Desync";
            case "MapRecompile":
                return "Map Recompile";
            case "SignalPossession":
                return "Signal Possession";
            case "PhaseContract":
                return "Phase Contract";
            case "AdaptiveCountermeasure":
                return "Adaptive Countermeasure";
            case "SignalTether":
                return "Signal Tether";
            case "BlindspotProtocol":
                return "Blindspot Protocol";
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
            case "PhaseBlink":
            case "PincerBarrage":
            case "SignalJam":
            case "OrbitBarrage":
            case "ReplayPredator":
            case "ChecksumLattice":
            case "InputDesync":
            case "MapRecompile":
            case "SignalPossession":
            case "PhaseContract":
            case "AdaptiveCountermeasure":
            case "SignalTether":
            case "BlindspotProtocol":
                return true;
            default:
                return false;
        }
    }

    private static bool IsBossLevelTwoState(string raw)
    {
        return raw == "PhaseBlink" ||
               raw == "PincerBarrage" ||
               raw == "SignalJam" ||
               raw == "OrbitBarrage" ||
               raw == "ReplayPredator" ||
               raw == "ChecksumLattice" ||
               raw == "InputDesync" ||
               raw == "MapRecompile" ||
               raw == "SignalPossession" ||
               raw == "PhaseContract";
    }

    private static bool IsBossLevelThreeState(string raw)
    {
        return raw == "AdaptiveCountermeasure" ||
               raw == "SignalTether" ||
               raw == "BlindspotProtocol";
    }

    private static Color GetBossStateColor(string raw)
    {
        switch (raw)
        {
            case "BasePursuit":
                return new Color(0.48f, 0.88f, 1f, 1f);
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
            case "PhaseBlink":
                return new Color(0.58f, 1f, 0.92f, 1f);
            case "PincerBarrage":
                return new Color(0.92f, 0.62f, 1f, 1f);
            case "SignalJam":
                return new Color(1f, 0.78f, 0.42f, 1f);
            case "OrbitBarrage":
                return new Color(0.58f, 0.82f, 1f, 1f);
            case "ReplayPredator":
                return new Color(1f, 0.42f, 0.76f, 1f);
            case "ChecksumLattice":
                return new Color(1f, 0.82f, 0.34f, 1f);
            case "InputDesync":
                return new Color(0.66f, 0.74f, 1f, 1f);
            case "MapRecompile":
                return new Color(0.92f, 0.62f, 1f, 1f);
            case "SignalPossession":
                return new Color(0.76f, 1f, 0.54f, 1f);
            case "PhaseContract":
                return new Color(1f, 0.84f, 0.46f, 1f);
            case "AdaptiveCountermeasure":
                return new Color(1f, 0.35f, 0.72f, 1f);
            case "SignalTether":
                return new Color(0.32f, 1f, 0.78f, 1f);
            case "BlindspotProtocol":
                return new Color(1f, 0.76f, 0.28f, 1f);
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

    // --- HUD persistente -----------------------------------------------------
    // Toda la información de consulta rápida vive en la cabina superior y comparte un mismo layout responsivo.
    private void DrawRuntimeHud()
    {
        EnsureBossStateStyles();
        DrawHudAmbientFrame();

        float s = hudScale;
        DrawUnifiedHudRail(s);
        HudTopLayout layout = GetHudTopLayout(s);
        Rect leftPanel = layout.metrics;

        float pad = 10f * s;
        float iconSize = 21f * s;
        float valueY = leftPanel.y + (8f * s);
        float colGap = 8f * s;
        float colW = (leftPanel.width - (pad * 2f) - colGap) * 0.5f;
        float valueH = 34f * s;

        Rect timeIcon = new Rect(leftPanel.x + pad, valueY + (6f * s), iconSize, iconSize);
        DrawHudMetricIcon(timeIcon, "clock", new Color(0.64f, 0.86f, 0.98f, 0.92f));
        string timeText = $"{SurvivalTime:F1}s";
        Rect timeValue = new Rect(timeIcon.xMax + (6f * s), valueY, colW - iconSize - (6f * s), valueH);
        GUI.Label(timeValue, timeText, BuildFittedSingleLineStyle(hudValueStyle, timeText, timeValue.width, timeValue.height, Mathf.RoundToInt(11f * s)));

        float rightColX = leftPanel.x + pad + colW + colGap;
        Rect scoreIcon = new Rect(rightColX, valueY + (6f * s), iconSize, iconSize);
        DrawHudMetricIcon(scoreIcon, "data", new Color(0.64f, 0.86f, 0.98f, 0.92f));
        int shownScore = Mathf.Max(0, Mathf.RoundToInt(displayedScore));
        string scoreText = shownScore.ToString();
        Rect scoreValue = new Rect(scoreIcon.xMax + (6f * s), valueY, colW - iconSize - (6f * s), valueH);
        GUI.Label(scoreValue, scoreText, BuildFittedSingleLineStyle(hudValueStyle, scoreText, scoreValue.width, scoreValue.height, Mathf.RoundToInt(11f * s)));

        DrawScorePopups(scoreIcon.xMax + (colW * 0.35f), valueY - (6f * s), s);

        int sectorLevel = arenaGenerator != null ? arenaGenerator.SectorLevel : 1;
        string levelText = $"L{sectorLevel} {levelType.ToUpperInvariant()}";
        Rect sectorIcon = new Rect(layout.sector.x + (10f * s), layout.sector.y + (9f * s), 18f * s, 18f * s);
        Color themeAccent;
        GetHudThemeColors(out _, out themeAccent);
        DrawHudMetricIcon(sectorIcon, "sector", themeAccent);
        Rect sectorLabel = new Rect(sectorIcon.xMax + (7f * s), layout.sector.y + (4f * s), layout.sector.width - (45f * s), 30f * s);
        GUI.Label(sectorLabel, levelText,
            BuildFittedSingleLineStyle(hudChipStyle, levelText, sectorLabel.width, sectorLabel.height, Mathf.RoundToInt(8f * s)));

        DrawRunContractHud(s);
        DrawOperationHud(s);
        DrawGhostDashDock(s);
        DrawFirewallChargeDock(s);
        if (IsStateHijackUnlocked)
        {
            DrawStateHijackDock(s);
        }
        else
        {
            DrawParryDock(s);
        }
        DrawAchievementToast(s);
        DrawStateHijackNotice(s);

        if (enableReactiveHudFx)
        {
            DrawThreatVignette();
        }
    }

    private void DrawUnifiedHudRail(float s)
    {
        HudTopLayout layout = GetHudTopLayout(s);
        Rect topBand = layout.bar;
        float threat = enableReactiveHudFx ? Mathf.Clamp01(smoothedThreat) : 0f;
        Color themeBase;
        Color themeAccent;
        GetHudThemeColors(out themeBase, out themeAccent);
        Color accent = Color.Lerp(themeAccent, new Color(1f, 0.30f, 0.40f, 1f), threat);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.Lerp(2.2f, 6f, threat));

        DrawSolidRect(topBand, new Color(0.008f, 0.016f, 0.034f, 0.88f));
        DrawSolidRect(new Rect(topBand.x, topBand.y, topBand.width, 2f * s), new Color(themeAccent.r, themeAccent.g, themeAccent.b, 0.58f));
        DrawSolidRect(new Rect(topBand.x, topBand.yMax - (3f * s), topBand.width, 3f * s), new Color(accent.r, accent.g, accent.b, 0.42f + pulse * 0.18f));
        DrawSolidRect(new Rect(topBand.x, topBand.y, 3f * s, topBand.height), new Color(accent.r, accent.g, accent.b, 0.48f));
        DrawSolidRect(new Rect(topBand.xMax - (3f * s), topBand.y, 3f * s, topBand.height), new Color(accent.r, accent.g, accent.b, 0.34f));

        Rect[] zones = { layout.metrics, layout.sector, layout.focus, layout.dash, layout.firewall, layout.hijack };
        for (int i = 0; i < zones.Length; i++)
        {
            float zoneAlpha = i % 2 == 0 ? 0.10f : 0.055f;
            DrawSolidRect(zones[i], new Color(themeBase.r, themeBase.g, themeBase.b, zoneAlpha));
            if (i > 0)
            {
                DrawSolidRect(new Rect(zones[i].x, topBand.y + (8f * s), 1f * s, topBand.height - (18f * s)), new Color(themeAccent.r, themeAccent.g, themeAccent.b, 0.20f));
            }
        }

        float topScanX = topBand.x + Mathf.Repeat(Time.unscaledTime * 64f * s, Mathf.Max(1f, topBand.width - (30f * s)));
        DrawSolidRect(new Rect(topScanX, topBand.y, 30f * s, 2f * s), new Color(accent.r, accent.g, accent.b, 0.34f));

        Rect threatRail = new Rect(topBand.x + (10f * s), topBand.yMax - (8f * s), topBand.width - (20f * s), 4f * s);
        DrawSolidRect(threatRail, new Color(0.03f, 0.045f, 0.075f, 0.95f));
        DrawSolidRect(new Rect(threatRail.x, threatRail.y, threatRail.width * threat, threatRail.height), new Color(accent.r, accent.g, accent.b, 0.86f));
        float threatHead = Mathf.Clamp(threatRail.x + threatRail.width * threat - (2f * s), threatRail.x, threatRail.xMax - (4f * s));
        DrawSolidRect(new Rect(threatHead, threatRail.y - (2f * s), 4f * s, threatRail.height + (4f * s)), new Color(accent.r, accent.g, accent.b, 0.92f));

        for (int i = 1; i < 8; i++)
        {
            float tickX = Mathf.Lerp(threatRail.x, threatRail.xMax, i / 8f);
            DrawSolidRect(new Rect(tickX, threatRail.y, 1f, threatRail.height), new Color(0.82f, 0.92f, 1f, 0.20f));
        }
    }

    private Rect GetHudArenaScreenRect()
    {
        Camera camera = Camera.main;
        if (camera == null || arenaGenerator == null)
        {
            return new Rect(Screen.width * 0.15f, Screen.height * 0.14f, Screen.width * 0.70f, Screen.height * 0.72f);
        }

        Vector2 center = arenaGenerator.transform.position;
        Vector3 worldMin = new Vector3(center.x - arenaGenerator.ArenaWidth * 0.5f, center.y - arenaGenerator.ArenaHeight * 0.5f, 0f);
        Vector3 worldMax = new Vector3(center.x + arenaGenerator.ArenaWidth * 0.5f, center.y + arenaGenerator.ArenaHeight * 0.5f, 0f);
        Vector3 screenMin = camera.WorldToScreenPoint(worldMin);
        Vector3 screenMax = camera.WorldToScreenPoint(worldMax);
        float x = Mathf.Min(screenMin.x, screenMax.x);
        float width = Mathf.Abs(screenMax.x - screenMin.x);
        float y = Screen.height - Mathf.Max(screenMin.y, screenMax.y);
        float height = Mathf.Abs(screenMax.y - screenMin.y);
        return new Rect(x, y, width, height);
    }

    private Rect GetHudTopBand(float s)
    {
        Rect arenaRect = GetHudArenaScreenRect();
        float available = Mathf.Max(56f * s, arenaRect.y - (8f * s));
        float height = Mathf.Min(82f * s, available);
        return new Rect(arenaRect.x, Mathf.Max(4f * s, arenaRect.y - height - (4f * s)), arenaRect.width, height);
    }

    private HudTopLayout GetHudTopLayout(float s)
    {
        Rect bar = GetHudTopBand(s);
        float usableX = bar.x + (4f * s);
        float usableWidth = bar.width - (8f * s);
        float metricsWidth = usableWidth * 0.20f;
        float sectorWidth = usableWidth * 0.12f;
        float focusWidth = usableWidth * 0.25f;
        float dashWidth = usableWidth * 0.14f;
        float firewallWidth = usableWidth * 0.14f;
        float hijackWidth = usableWidth - metricsWidth - sectorWidth - focusWidth - dashWidth - firewallWidth;

        HudTopLayout layout = new HudTopLayout();
        layout.bar = bar;
        layout.metrics = new Rect(usableX, bar.y + (2f * s), metricsWidth, bar.height - (4f * s));
        layout.sector = new Rect(layout.metrics.xMax, layout.metrics.y, sectorWidth, layout.metrics.height);
        layout.focus = new Rect(layout.sector.xMax, layout.metrics.y, focusWidth, layout.metrics.height);
        layout.dash = new Rect(layout.focus.xMax, layout.metrics.y, dashWidth, layout.metrics.height);
        layout.firewall = new Rect(layout.dash.xMax, layout.metrics.y, firewallWidth, layout.metrics.height);
        layout.hijack = new Rect(layout.firewall.xMax, layout.metrics.y, hijackWidth, layout.metrics.height);
        return layout;
    }

    private void DrawStateHijackNotice(float s)
    {
        if (stateHijackNoticeTimer <= 0f || string.IsNullOrWhiteSpace(stateHijackNoticeLabel))
        {
            return;
        }

        float enter = Mathf.Clamp01((2.8f - stateHijackNoticeTimer) / 0.18f);
        float exit = Mathf.Clamp01(stateHijackNoticeTimer / 0.28f);
        float alpha = Mathf.Min(enter, exit);
        Rect topBand = GetHudTopBand(s);
        float width = Mathf.Min(320f * s, topBand.width * 0.34f);
        float panelHeight = string.IsNullOrWhiteSpace(stateHijackNoticeHint) ? 48f * s : 62f * s;
        Rect panel = new Rect(topBand.xMax - width, topBand.yMax + (8f * s), width, panelHeight);
        Color color = stateHijackNoticeColor;

        DrawSolidRect(panel, new Color(0.02f, 0.03f, 0.065f, 0.86f * alpha));
        DrawSolidRect(new Rect(panel.x, panel.y, 3f * s, panel.height), new Color(color.r, color.g, color.b, 0.86f * alpha));
        DrawSolidRect(new Rect(panel.x, panel.y, panel.width, 2f * s), new Color(color.r, color.g, color.b, 0.44f * alpha));
        Color old = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.Label(new Rect(panel.x + (12f * s), panel.y + (3f * s), panel.width - (24f * s), 17f * s), stateHijackNoticeVerb, hudLabelStyle);
        string value = $"{stateHijackNoticeLabel}  |  F";
        Rect valueRect = new Rect(panel.x + (12f * s), panel.y + (19f * s), panel.width - (24f * s), 25f * s);
        GUI.Label(valueRect, value, BuildFittedSingleLineStyle(hudValueStyle, value, valueRect.width, valueRect.height, Mathf.RoundToInt(9f * s)));
        if (!string.IsNullOrWhiteSpace(stateHijackNoticeHint))
        {
            Rect hintRect = new Rect(panel.x + (12f * s), panel.y + (42f * s), panel.width - (24f * s), 16f * s);
            GUI.Label(hintRect, stateHijackNoticeHint, BuildFittedSingleLineStyle(hudLabelStyle, stateHijackNoticeHint, hintRect.width, hintRect.height, Mathf.RoundToInt(7f * s)));
        }
        GUI.color = old;
    }

    private void DrawAchievementToast(float s)
    {
        if (achievementToastTimer <= 0f || string.IsNullOrWhiteSpace(achievementToastTitle))
        {
            return;
        }

        float life = Mathf.Clamp01(achievementToastTimer / 4.2f);
        float enter = Mathf.Clamp01((4.2f - achievementToastTimer) / 0.32f);
        float exit = Mathf.Clamp01(achievementToastTimer / 0.36f);
        float alpha = Mathf.Min(enter, exit);
        float bob = Mathf.Sin(Time.unscaledTime * 18f) * 1.5f * s;
        Rect topBand = GetHudTopBand(s);
        float y = topBand.yMax + Mathf.Lerp(8f * s, 18f * s, enter) + bob;
        Rect panel = new Rect((Screen.width - (390f * s)) * 0.5f, y, 390f * s, 70f * s);
        Color accent = Color.Lerp(new Color(0.44f, 0.95f, 1f, 1f), new Color(1f, 0.78f, 0.44f, 1f), 1f - life);

        DrawSolidRect(panel, new Color(0.03f, 0.045f, 0.085f, 0.82f * alpha));
        DrawSolidRect(new Rect(panel.x, panel.y, panel.width, 2f * s), new Color(accent.r, accent.g, accent.b, 0.72f * alpha));
        DrawSolidRect(new Rect(panel.x, panel.yMax - (2f * s), panel.width, 2f * s), new Color(accent.r, accent.g, accent.b, 0.36f * alpha));
        DrawSolidRect(new Rect(panel.x + (10f * s), panel.y + (10f * s), 42f * s, 42f * s), new Color(accent.r, accent.g, accent.b, 0.18f * alpha));

        Color old = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);
        string header = string.IsNullOrWhiteSpace(achievementToastHeader) ? "LOGRO DESBLOQUEADO" : achievementToastHeader;
        GUI.Label(new Rect(panel.x + (62f * s), panel.y + (7f * s), panel.width - (122f * s), 18f * s), header, hudLabelStyle);
        GUI.Label(
            new Rect(panel.x + (62f * s), panel.y + (25f * s), panel.width - (122f * s), 25f * s),
            achievementToastTitle,
            BuildFittedSingleLineStyle(hudValueStyle, achievementToastTitle, panel.width - (122f * s), 25f * s, Mathf.RoundToInt(14f * s)));
        GUI.Label(
            new Rect(panel.x + (62f * s), panel.y + (49f * s), panel.width - (122f * s), 18f * s),
            achievementToastDescription,
            BuildFittedSingleLineStyle(hudLabelStyle, achievementToastDescription, panel.width - (122f * s), 18f * s, Mathf.RoundToInt(9f * s)));
        GUI.Label(new Rect(panel.xMax - (78f * s), panel.y + (20f * s), 66f * s, 32f * s), $"+{achievementToastReward}", hudValueStyle);
        GUI.color = old;
    }

    private void DrawRunContractHud(float s)
    {
        if (!hasActiveContract && contractCompletePulseTimer <= 0f)
        {
            return;
        }

        HudTopLayout layout = GetHudTopLayout(s);
        Rect panel = new Rect(
            layout.focus.x + (10f * s),
            layout.focus.y + layout.focus.height * 0.49f,
            layout.focus.width - (20f * s),
            Mathf.Max(24f * s, layout.focus.height * 0.38f));
        Color accent = contractCompletePulseTimer > 0f
            ? new Color(0.56f, 1f, 0.76f, 1f)
            : new Color(0.48f, 0.90f, 1f, 1f);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 9f);
        DrawSolidRect(new Rect(panel.x, panel.y, 2f * s, panel.height), new Color(accent.r, accent.g, accent.b, 0.42f + pulse * 0.20f));

        if (!hasActiveContract)
        {
            Rect completeRect = new Rect(panel.x + (9f * s), panel.y, panel.width - (18f * s), panel.height);
            GUI.Label(completeRect, "CONTRATO COMPLETADO",
                BuildFittedSingleLineStyle(hudLabelStyle, "CONTRATO COMPLETADO", completeRect.width, completeRect.height, Mathf.RoundToInt(8f * s)));
            return;
        }

        float remaining = Mathf.Max(0f, activeContract.duration - (SurvivalTime - activeContract.startedAt));
        float normalized = Mathf.Clamp01(activeContract.progress / Mathf.Max(1f, activeContract.target));
        string title = activeContract.title.ToUpperInvariant();
        string progress = $"{activeContract.progress}/{activeContract.target} {Mathf.CeilToInt(remaining)}s";
        Rect titleRect = new Rect(panel.x + (9f * s), panel.y, panel.width * 0.62f, 16f * s);
        Rect progressRect = new Rect(titleRect.xMax, panel.y, panel.xMax - titleRect.xMax - (5f * s), 16f * s);
        GUI.Label(titleRect, title, BuildFittedSingleLineStyle(hudLabelStyle, title, titleRect.width, titleRect.height, Mathf.RoundToInt(7f * s)));
        GUI.Label(progressRect, progress, BuildFittedSingleLineStyle(hudLabelStyle, progress, progressRect.width, progressRect.height, Mathf.RoundToInt(7f * s)));

        Rect bar = new Rect(panel.x + (9f * s), panel.yMax - (6f * s), panel.width - (14f * s), 4f * s);
        DrawSolidRect(bar, new Color(0.04f, 0.06f, 0.10f, 0.92f));
        DrawSolidRect(new Rect(bar.x, bar.y, bar.width * normalized, bar.height), new Color(accent.r, accent.g, accent.b, 0.82f));
    }

    private void DrawOperationHud(float s)
    {
        if (activeOperation.id == ContainmentOperationStorage.NoneId)
        {
            return;
        }

        HudTopLayout layout = GetHudTopLayout(s);
        Rect panel = new Rect(
            layout.sector.x + (9f * s),
            layout.sector.y + layout.sector.height * 0.50f,
            layout.sector.width - (18f * s),
            Mathf.Max(22f * s, layout.sector.height * 0.34f));
        Color accent;
        GetHudThemeColors(out _, out accent);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.2f);

        DrawSolidRect(new Rect(panel.x, panel.yMax - (1f * s), panel.width, 1f * s), new Color(accent.r, accent.g, accent.b, 0.25f + pulse * 0.10f));

        Rect icon = new Rect(panel.x + (2f * s), panel.y + (5f * s), 15f * s, 15f * s);
        DrawHudMetricIcon(icon, "rule", accent);
        string progress = $"REGLA  x{activeOperation.scoreMultiplier:0.00}";
        Rect progressRect = new Rect(icon.xMax + (5f * s), panel.y, panel.width - (24f * s), panel.height);
        GUI.Label(progressRect, progress,
            BuildFittedSingleLineStyle(hudLabelStyle, progress, progressRect.width, progressRect.height, Mathf.RoundToInt(7f * s)));
    }

    private void DrawGhostDashDock(float s)
    {
        if (playerController == null)
        {
            return;
        }

        bool ready = playerController.IsGhostDashReady;
        float normalized = playerController.GhostDashCooldownNormalized;
        HudTopLayout layout = GetHudTopLayout(s);
        Rect dock = new Rect(
            layout.dash.x + (8f * s),
            layout.dash.y + (7f * s),
            layout.dash.width - (16f * s),
            layout.dash.height - (17f * s));
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (ready ? 9f : 3.5f));
        Color accent = ready
            ? Color.Lerp(new Color(0.55f, 1f, 0.94f, 1f), new Color(0.92f, 1f, 1f, 1f), pulse)
            : new Color(0.26f, 0.52f, 0.72f, 1f);

        DrawSolidRect(new Rect(dock.x, dock.y, dock.width, 2f * s), new Color(accent.r, accent.g, accent.b, ready ? 0.72f : 0.28f));

        Rect icon = new Rect(dock.x + (4f * s), dock.y + (10f * s), 22f * s, 22f * s);
        DrawHudMetricIcon(icon, "dash", accent);
        float valueWidth = Mathf.Min(46f * s, dock.width * 0.28f);
        Rect bar = new Rect(icon.xMax + (7f * s), dock.y + (15f * s), Mathf.Max(24f * s, dock.width - icon.width - valueWidth - (22f * s)), 10f * s);
        DrawSolidRect(bar, new Color(0.04f, 0.06f, 0.10f, 0.74f));
        DrawSolidRect(new Rect(bar.x, bar.y, bar.width * normalized, bar.height), new Color(accent.r, accent.g, accent.b, ready ? 0.92f : 0.62f));
        string value = ready ? "SHIFT" : $"{Mathf.RoundToInt(normalized * 100f)}%";
        Rect valueRect = new Rect(bar.xMax + (5f * s), dock.y + (8f * s), valueWidth, 26f * s);
        GUI.Label(valueRect, value, BuildFittedSingleLineStyle(hudChipStyle, value, valueRect.width, valueRect.height, Mathf.RoundToInt(8f * s)));
    }

    private void DrawParryDock(float s)
    {
        if (playerController == null)
        {
            return;
        }

        bool ready = playerController.IsParryReady;
        float normalized = playerController.ParryCooldownNormalized;
        HudTopLayout layout = GetHudTopLayout(s);
        Rect panel = new Rect(
            layout.hijack.x + (8f * s),
            layout.hijack.y + (7f * s),
            layout.hijack.width - (16f * s),
            layout.hijack.height - (17f * s));
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (ready ? 8.5f : 3.2f));
        Color accent = ready
            ? Color.Lerp(new Color(1f, 0.58f, 0.76f, 1f), new Color(1f, 0.88f, 0.94f, 1f), pulse)
            : new Color(0.58f, 0.34f, 0.52f, 1f);

        DrawSolidRect(new Rect(panel.x, panel.y, panel.width, 2f * s), new Color(accent.r, accent.g, accent.b, ready ? 0.72f : 0.28f));
        Rect icon = new Rect(panel.x + (4f * s), panel.y + (10f * s), 22f * s, 22f * s);
        DrawHudMetricIcon(icon, "parry", accent);
        float valueWidth = Mathf.Min(58f * s, panel.width * 0.38f);
        Rect bar = new Rect(icon.xMax + (7f * s), panel.y + (15f * s), Mathf.Max(20f * s, panel.width - icon.width - valueWidth - (22f * s)), 10f * s);
        DrawSolidRect(bar, new Color(0.04f, 0.06f, 0.10f, 0.78f));
        DrawSolidRect(new Rect(bar.x, bar.y, bar.width * normalized, bar.height), new Color(accent.r, accent.g, accent.b, ready ? 0.92f : 0.64f));
        string value = ready ? "ESP / E" : $"{Mathf.RoundToInt(normalized * 100f)}%";
        Rect valueRect = new Rect(bar.xMax + (5f * s), panel.y + (8f * s), valueWidth, 26f * s);
        GUI.Label(valueRect, value, BuildFittedSingleLineStyle(hudChipStyle, value, valueRect.width, valueRect.height, Mathf.RoundToInt(7f * s)));
    }

    private void DrawFirewallChargeDock(float s)
    {
        if (playerController == null)
        {
            return;
        }

        float normalized = playerController.FirewallChargeNormalized;
        bool ready = playerController.IsFirewallBurstReady;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (ready ? 11f : 4.2f));
        Color readyAccent = Color.Lerp(new Color(0.42f, 0.96f, 1f, 1f), new Color(1f, 0.84f, 0.42f, 1f), pulse);
        Color accent = ready ? readyAccent : Color.Lerp(new Color(0.28f, 0.56f, 0.86f, 1f), new Color(0.48f, 0.94f, 1f, 1f), normalized);

        HudTopLayout layout = GetHudTopLayout(s);
        Rect panel = new Rect(
            layout.firewall.x + (8f * s),
            layout.firewall.y + (7f * s),
            layout.firewall.width - (16f * s),
            layout.firewall.height - (17f * s));

        DrawSolidRect(new Rect(panel.x, panel.y, panel.width, 1f * s), new Color(accent.r, accent.g, accent.b, ready ? 0.88f : 0.42f));
        if (ready)
        {
            DrawSolidRect(new Rect(panel.x - (3f * s), panel.y + (6f * s), 2f * s, panel.height - (12f * s)), new Color(accent.r, accent.g, accent.b, 0.46f + pulse * 0.26f));
            DrawSolidRect(new Rect(panel.xMax + (1f * s), panel.y + (6f * s), 2f * s, panel.height - (12f * s)), new Color(accent.r, accent.g, accent.b, 0.46f + pulse * 0.26f));
        }

        Rect icon = new Rect(panel.x + (4f * s), panel.y + (10f * s), 22f * s, 22f * s);
        DrawHudMetricIcon(icon, "firewall", accent);
        float inputWidth = Mathf.Min(46f * s, panel.width * 0.28f);
        Rect background = new Rect(icon.xMax + (7f * s), panel.y + (15f * s), Mathf.Max(24f * s, panel.width - icon.width - inputWidth - (22f * s)), 11f * s);
        DrawSolidRect(background, new Color(0.04f, 0.06f, 0.10f, 0.88f));
        Color fill = ready
            ? Color.Lerp(new Color(0.45f, 0.95f, 1f, 0.95f), new Color(1f, 0.88f, 0.48f, 1f), pulse)
            : Color.Lerp(new Color(0.22f, 0.48f, 0.74f, 0.82f), new Color(0.45f, 0.95f, 1f, 0.95f), normalized);
        float fillW = background.width * normalized;
        DrawSolidRect(new Rect(background.x, background.y, fillW, background.height), fill);
        DrawSolidRect(new Rect(background.x, background.y, background.width, 1f), new Color(0.85f, 0.92f, 1f, 0.18f));
        DrawSolidRect(new Rect(background.x, background.y + background.height - 1f, background.width, 1f), new Color(0.85f, 0.92f, 1f, 0.14f));

        for (int i = 1; i < 4; i++)
        {
            float tickX = Mathf.Lerp(background.x, background.xMax, i / 4f);
            DrawSolidRect(new Rect(tickX, background.y - (3f * s), 1f, background.height + (6f * s)), new Color(0.72f, 0.84f, 1f, 0.22f));
        }

        string input = ready ? "Q / R" : $"{Mathf.RoundToInt(normalized * 100f)}%";
        Rect inputRect = new Rect(background.xMax + (5f * s), panel.y + (8f * s), inputWidth, 26f * s);
        DrawSolidRect(inputRect, new Color(accent.r, accent.g, accent.b, ready ? 0.24f : 0.12f));
        GUI.Label(inputRect, input, BuildFittedSingleLineStyle(hudChipStyle, input, inputRect.width - (6f * s), inputRect.height, Mathf.RoundToInt(8f * s)));
    }

    private void DrawStateHijackDock(float s)
    {
        if (playerController == null || LocalVersusModeStorage.IsLocalVersus || !IsStateHijackUnlocked)
        {
            return;
        }

        HudTopLayout layout = GetHudTopLayout(s);
        Rect panel = new Rect(
            layout.hijack.x + (8f * s),
            layout.hijack.y + (7f * s),
            layout.hijack.width - (16f * s),
            layout.hijack.height - (17f * s));
        bool stored = playerController.HasStoredHijack;
        Color accent = stored ? playerController.StoredHijackColor : new Color(0.30f, 0.44f, 0.62f, 1f);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (stored ? 8.5f : 2.2f));

        DrawSolidRect(new Rect(panel.x, panel.y, panel.width, 2f * s), new Color(accent.r, accent.g, accent.b, stored ? 0.58f + pulse * 0.22f : 0.24f));
        Rect icon = new Rect(panel.x + (4f * s), panel.y + (10f * s), 22f * s, 22f * s);
        DrawHudMetricIcon(icon, "hijack", accent);

        float keyWidth = Mathf.Min(34f * s, panel.width * 0.24f);
        Rect keyRect = new Rect(panel.xMax - keyWidth, panel.y + (8f * s), keyWidth, 26f * s);
        DrawSolidRect(keyRect, new Color(accent.r, accent.g, accent.b, stored ? 0.24f + pulse * 0.10f : 0.08f));
        GUI.Label(keyRect, "F", BuildFittedSingleLineStyle(hudChipStyle, "F", keyRect.width, keyRect.height, Mathf.RoundToInt(9f * s)));

        string label = stored ? playerController.StoredHijackLabel : "PARRY AL JEFE";
        Rect labelRect = new Rect(icon.xMax + (6f * s), panel.y + (7f * s), keyRect.x - icon.xMax - (11f * s), 29f * s);
        GUI.Label(labelRect, label, BuildFittedSingleLineStyle(hudChipStyle, label, labelRect.width, labelRect.height, Mathf.RoundToInt(7f * s)));
    }

    private float GetFirewallRailY(float s, float panelH)
    {
        float afterTopHud = 132f * s;
        if (hasActiveContract || contractCompletePulseTimer > 0f)
        {
            afterTopHud = 238f * s;
        }
        if (activeOperation.id != ContainmentOperationStorage.NoneId)
        {
            afterTopHud = (hasActiveContract || contractCompletePulseTimer > 0f) ? 304f * s : 230f * s;
        }

        float preferredY = Mathf.Max(afterTopHud, Screen.height * 0.50f - panelH * 0.50f);
        float minY = 132f * s;
        float maxY = Screen.height - panelH - (14f * s);
        if (maxY < minY)
        {
            return Mathf.Max(10f * s, maxY);
        }

        return Mathf.Clamp(preferredY, minY, maxY);
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
        Color tint = new Color(1f, 0.25f, 0.34f, (0.025f + intensity * 0.095f) * (0.82f + pulse * 0.18f));
        float edge = Mathf.Lerp(14f, 52f, intensity);

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
        Color reactiveBase = Color.Lerp(baseTint, dangerTint, threat * 0.58f);
        Color reactiveAccent = Color.Lerp(accentTint, dangerTint, threat * 0.68f);
        float reactiveSideOpacity = sideHudOpacity * Mathf.Lerp(0.62f, 1.16f, threat);
        float reactiveAccentOpacity = sideHudAccentOpacity * Mathf.Lerp(0.62f, 1.32f, threat);

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
            float wobble = Mathf.Sin((y * 0.03f) + (t * Mathf.Lerp(0.7f, 1.25f, threat))) * Mathf.Lerp(1.1f, 2.8f, threat);
            float yAnim = y + Mathf.Sin((t * Mathf.Lerp(0.55f, 0.95f, threat)) + y * 0.02f) * Mathf.Lerp(2f, 4.2f, threat);
            float lineWidth = Mathf.Lerp(8f, sideWidth * 0.55f, Mathf.PerlinNoise(y * 0.013f, t * 0.45f));
            float lineAlpha = (0.045f + 0.055f * (0.5f + 0.5f * Mathf.Sin((t * 1.4f) + y * 0.06f))) * Mathf.Lerp(0.62f, 1.10f, threat);

            DrawSolidRect(
                new Rect(7f + wobble, yAnim, lineWidth, 2f),
                new Color(reactiveAccent.r, reactiveAccent.g, reactiveAccent.b, lineAlpha));
            DrawSolidRect(
                new Rect(Screen.width - 7f - lineWidth - wobble, yAnim, lineWidth, 2f),
                new Color(reactiveAccent.r, reactiveAccent.g, reactiveAccent.b, lineAlpha));
        }

        float sweepHeight = Mathf.Lerp(Screen.height * 0.09f, Screen.height * Mathf.Lerp(0.12f, 0.16f, threat), 0.5f + 0.5f * Mathf.Sin(t * Mathf.Lerp(0.22f, 0.42f, threat)));
        float sweepY = Mathf.Repeat(t * (Screen.height * Mathf.Lerp(0.06f, 0.10f, threat)), Screen.height + sweepHeight) - sweepHeight;
        DrawSolidRect(
            new Rect(0f, sweepY, sideWidth, sweepHeight),
            new Color(reactiveAccent.r, reactiveAccent.g, reactiveAccent.b, reactiveAccentOpacity * 0.34f));
        DrawSolidRect(
            new Rect(Screen.width - sideWidth, Screen.height - sweepY - sweepHeight, sideWidth, sweepHeight),
            new Color(reactiveAccent.r, reactiveAccent.g, reactiveAccent.b, reactiveAccentOpacity * 0.34f));

        float bracketW = 28f;
        float bracketH = 3f;
        float inset = sideWidth + 6f;
        float topY = 8f;
        float bottomY = Screen.height - 12f;
        Color bracket = new Color(reactiveAccent.r, reactiveAccent.g, reactiveAccent.b, Mathf.Lerp(0.14f, 0.30f, threat));
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
            case "Core":
                baseTint = new Color(0.04f, 0.15f, 0.12f, 1f);
                accentTint = new Color(0.44f, 1f, 0.58f, 1f);
                break;
            case "Archive":
                baseTint = new Color(0.08f, 0.08f, 0.16f, 1f);
                accentTint = new Color(0.55f, 0.92f, 1f, 1f);
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
        GUI.Label(rect, text, BuildFittedSingleLineStyle(hudChipStyle, text, rect.width - (10f * hudScale), rect.height, Mathf.RoundToInt(10f * hudScale)));
    }

    private static void DrawHudMetricIcon(Rect rect, string icon, Color color)
    {
        float unit = Mathf.Max(1f, rect.width / 12f);
        if (icon == "data")
        {
            for (int i = 0; i < 5; i++)
            {
                float inset = Mathf.Abs(2 - i) * unit * 1.5f;
                DrawSolidRect(new Rect(rect.x + inset, rect.y + unit * (1f + i * 2f), rect.width - inset * 2f, unit * 1.6f), color);
            }
            return;
        }

        if (icon == "firewall")
        {
            DrawSolidRect(new Rect(rect.x + unit * 2f, rect.y + unit, rect.width - unit * 4f, unit * 2f), color);
            DrawSolidRect(new Rect(rect.x + unit * 2f, rect.y + unit * 3f, unit * 2f, rect.height - unit * 6f), color);
            DrawSolidRect(new Rect(rect.xMax - unit * 4f, rect.y + unit * 3f, unit * 2f, rect.height - unit * 6f), color);
            DrawSolidRect(new Rect(rect.x + unit * 3f, rect.yMax - unit * 4f, rect.width - unit * 6f, unit * 2f), color);
            DrawSolidRect(new Rect(rect.center.x - unit, rect.yMax - unit * 3f, unit * 2f, unit * 2f), color);
            return;
        }

        if (icon == "hijack")
        {
            DrawSolidRect(new Rect(rect.x, rect.y + unit, rect.width * 0.42f, unit * 2f), color);
            DrawSolidRect(new Rect(rect.xMax - rect.width * 0.42f, rect.yMax - unit * 3f, rect.width * 0.42f, unit * 2f), color);
            DrawSolidRect(new Rect(rect.center.x - unit, rect.y + unit * 2f, unit * 2f, rect.height - unit * 4f), color);
            DrawSolidRect(new Rect(rect.x + unit * 2f, rect.center.y - unit, rect.width - unit * 4f, unit * 2f), color);
            return;
        }

        if (icon == "rule")
        {
            DrawSolidRect(new Rect(rect.x + unit * 2f, rect.y + unit * 2f, rect.width - unit * 4f, unit * 1.5f), color);
            DrawSolidRect(new Rect(rect.x + unit * 2f, rect.center.y - unit * 0.75f, rect.width - unit * 4f, unit * 1.5f), color);
            DrawSolidRect(new Rect(rect.x + unit * 2f, rect.yMax - unit * 3.5f, rect.width - unit * 4f, unit * 1.5f), color);
            DrawSolidRect(new Rect(rect.x + unit * 3f, rect.y + unit, unit * 2f, unit * 3.5f), color);
            DrawSolidRect(new Rect(rect.xMax - unit * 5f, rect.center.y - unit * 1.75f, unit * 2f, unit * 3.5f), color);
            DrawSolidRect(new Rect(rect.center.x - unit, rect.yMax - unit * 4.5f, unit * 2f, unit * 3.5f), color);
            return;
        }

        if (icon == "anomaly")
        {
            DrawSolidRect(new Rect(rect.center.x - unit, rect.y, unit * 2f, rect.height), color);
            DrawSolidRect(new Rect(rect.x, rect.center.y - unit, rect.width, unit * 2f), color);
            DrawSolidRect(new Rect(rect.x + unit * 2f, rect.y + unit * 2f, unit * 2f, unit * 2f), color);
            DrawSolidRect(new Rect(rect.xMax - unit * 4f, rect.y + unit * 2f, unit * 2f, unit * 2f), color);
            DrawSolidRect(new Rect(rect.x + unit * 2f, rect.yMax - unit * 4f, unit * 2f, unit * 2f), color);
            DrawSolidRect(new Rect(rect.xMax - unit * 4f, rect.yMax - unit * 4f, unit * 2f, unit * 2f), color);
            return;
        }

        if (icon == "sector")
        {
            DrawSolidRect(new Rect(rect.x, rect.y + unit * 2f, rect.width, unit * 2f), color);
            DrawSolidRect(new Rect(rect.x + unit * 2f, rect.center.y - unit, rect.width - unit * 4f, unit * 2f), color);
            DrawSolidRect(new Rect(rect.x + unit * 4f, rect.yMax - unit * 4f, rect.width - unit * 8f, unit * 2f), color);
            return;
        }

        if (icon == "dash")
        {
            DrawSolidRect(new Rect(rect.x, rect.center.y - unit, rect.width - unit * 3f, unit * 2f), color);
            DrawSolidRect(new Rect(rect.x + unit * 2f, rect.center.y - unit * 4f, rect.width - unit * 5f, unit * 2f), color);
            DrawSolidRect(new Rect(rect.x + unit * 2f, rect.center.y + unit * 2f, rect.width - unit * 5f, unit * 2f), color);
            DrawSolidRect(new Rect(rect.xMax - unit * 5f, rect.center.y - unit * 5f, unit * 5f, unit * 10f), new Color(color.r, color.g, color.b, color.a * 0.36f));
            return;
        }

        if (icon == "parry")
        {
            DrawSolidRect(new Rect(rect.center.x - unit, rect.y + unit, unit * 2f, rect.height - unit * 2f), color);
            DrawSolidRect(new Rect(rect.x + unit, rect.center.y - unit, rect.width - unit * 2f, unit * 2f), color);
            DrawSolidRect(new Rect(rect.x + unit * 2f, rect.y + unit * 2f, unit * 2f, unit * 2f), color);
            DrawSolidRect(new Rect(rect.xMax - unit * 4f, rect.y + unit * 2f, unit * 2f, unit * 2f), color);
            DrawSolidRect(new Rect(rect.x + unit * 2f, rect.yMax - unit * 4f, unit * 2f, unit * 2f), color);
            DrawSolidRect(new Rect(rect.xMax - unit * 4f, rect.yMax - unit * 4f, unit * 2f, unit * 2f), color);
            return;
        }

        float frameInset = unit * 1.5f;
        DrawSolidRect(new Rect(rect.x + frameInset, rect.y, rect.width - frameInset * 2f, unit), color);
        DrawSolidRect(new Rect(rect.x + frameInset, rect.yMax - unit, rect.width - frameInset * 2f, unit), color);
        DrawSolidRect(new Rect(rect.x, rect.y + frameInset, unit, rect.height - frameInset * 2f), color);
        DrawSolidRect(new Rect(rect.xMax - unit, rect.y + frameInset, unit, rect.height - frameInset * 2f), color);
        DrawSolidRect(new Rect(rect.x + unit * 0.7f, rect.y + unit * 0.7f, unit * 1.5f, unit * 1.5f), color);
        DrawSolidRect(new Rect(rect.xMax - unit * 2.2f, rect.y + unit * 0.7f, unit * 1.5f, unit * 1.5f), color);
        DrawSolidRect(new Rect(rect.x + unit * 0.7f, rect.yMax - unit * 2.2f, unit * 1.5f, unit * 1.5f), color);
        DrawSolidRect(new Rect(rect.xMax - unit * 2.2f, rect.yMax - unit * 2.2f, unit * 1.5f, unit * 1.5f), color);
        DrawSolidRect(new Rect(rect.center.x - unit * 0.5f, rect.y + unit * 2.5f, unit, rect.height * 0.34f), color);
        DrawSolidRect(new Rect(rect.center.x, rect.center.y - unit * 0.5f, rect.width * 0.25f, unit), color);
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

    private static GUIStyle BuildFittedWrappedStyle(GUIStyle baseStyle, string text, float width, float height, int minSize)
    {
        if (baseStyle == null || string.IsNullOrEmpty(text))
        {
            return baseStyle;
        }

        GUIContent content = new GUIContent(text);
        GUIStyle style = new GUIStyle(baseStyle)
        {
            wordWrap = true,
            clipping = TextClipping.Clip
        };

        int preferred = Mathf.Max(minSize, baseStyle.fontSize);
        for (int size = preferred; size >= minSize; size--)
        {
            style.fontSize = size;
            if (style.CalcHeight(content, Mathf.Max(1f, width)) <= height)
            {
                return style;
            }
        }

        style.fontSize = minSize;
        return style;
    }

    private void EnsureTutorialStyles()
    {
        if (tutorialBodyStyle != null && tutorialHeaderStyle != null && tutorialTinyStyle != null)
        {
            return;
        }

        tutorialHeaderStyle = new GUIStyle(GUI.skin.label)
        {
            font = importantFont,
            fontSize = Mathf.RoundToInt(17f * hudScale),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            clipping = TextClipping.Clip,
            wordWrap = false
        };
        tutorialHeaderStyle.normal.textColor = new Color(0.94f, 0.98f, 1f, 0.98f);

        tutorialBodyStyle = new GUIStyle(GUI.skin.label)
        {
            font = secondaryFont,
            fontSize = Mathf.RoundToInt(14f * hudScale),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperLeft,
            clipping = TextClipping.Clip,
            wordWrap = true
        };
        tutorialBodyStyle.normal.textColor = new Color(0.82f, 0.90f, 1f, 0.92f);

        tutorialTinyStyle = new GUIStyle(GUI.skin.label)
        {
            font = importantFont,
            fontSize = Mathf.RoundToInt(14f * hudScale),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Clip,
            wordWrap = false
        };
        tutorialTinyStyle.normal.textColor = new Color(0.96f, 0.99f, 1f, 0.98f);
    }

    private void EnsureUpgradeStyles()
    {
        if (upgradeKickerStyle != null &&
            upgradeTitleStyle != null &&
            upgradeDescriptionStyle != null &&
            upgradeButtonStyle != null &&
            upgradeTimerStyle != null &&
            upgradeMetaStyle != null &&
            upgradeIconStyle != null &&
            upgradeImpactStyle != null &&
            upgradeCardTitleStyle != null &&
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

        upgradeMetaStyle = new GUIStyle(GUI.skin.label)
        {
            font = secondaryFont,
            fontSize = Mathf.RoundToInt(11f * hudScale),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Clip,
            wordWrap = false
        };
        upgradeMetaStyle.normal.textColor = new Color(0.88f, 0.96f, 1f, 0.92f);

        upgradeIconStyle = new GUIStyle(GUI.skin.label)
        {
            font = importantFont,
            fontSize = Mathf.RoundToInt(28f * hudScale),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Clip,
            wordWrap = false
        };
        upgradeIconStyle.normal.textColor = new Color(0.96f, 0.99f, 1f, 0.98f);

        upgradeImpactStyle = new GUIStyle(GUI.skin.label)
        {
            font = secondaryFont,
            fontSize = Mathf.RoundToInt(13f * hudScale),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Clip,
            wordWrap = false
        };
        upgradeImpactStyle.normal.textColor = new Color(1f, 0.88f, 0.62f, 0.96f);

        upgradeCardTitleStyle = new GUIStyle(GUI.skin.label)
        {
            font = importantFont,
            fontSize = Mathf.RoundToInt(17f * hudScale),
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            clipping = TextClipping.Clip,
            wordWrap = true
        };
        upgradeCardTitleStyle.normal.textColor = new Color(0.96f, 0.99f, 1f, 0.98f);

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

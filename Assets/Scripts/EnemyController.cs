using System.Collections.Generic;
using UnityEngine;
using System.Collections;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EnemyController : MonoBehaviour
{
    // IA principal de la anomalia: selecciona estados, persigue al jugador y maneja habilidades.
    public enum AnomalyState
    {
        DirectChase,
        PredictiveIntercept,
        CutoffFlank,
        ErraticBurst,
        Split,
        ExpansionShoot,
        SpeedSurge,
        WeaveHunter,
        Destroyer,
        PhaseBlink,
        PincerBarrage,
        SignalJam,
        OrbitBarrage,
        ReplayPredator,
        ChecksumLattice,
        InputDesync,
        MapRecompile,
        SignalPossession,
        PhaseContract,
        AdaptiveCountermeasure,
        SignalTether,
        BlindspotProtocol
    }

    private enum BehaviorPattern
    {
        DirectChase,
        PredictiveIntercept,
        CutoffFlank,
        ErraticBurst
    }

    public enum PacingPhase
    {
        BuildUp,
        SustainPeak,
        PeakFade,
        Relax
    }

    [Header("References")]
    [SerializeField] private PlayerController player;
    [SerializeField] private GameManager gameManager;

    [Header("Base Movement")]
    [SerializeField] private float baseMoveSpeed = 6.5f;
    [SerializeField] private float velocityResponsiveness = 48f;

    [Header("Pattern Timing")]
    [SerializeField] private float flankRetargetInterval = 0.7f;

    [Header("Predictive Pattern")]
    [SerializeField] private float minLeadTime = 0.1f;
    [SerializeField] private float maxLeadTime = 1.4f;

    [Header("Cutoff Pattern")]
    [SerializeField] private float flankRadius = 2.8f;
    [SerializeField] private float flankLeadFactor = 1.1f;

    [Header("Erratic Pattern")]
    [SerializeField] private float erraticDirectionRefresh = 0.18f;
    [SerializeField] private float erraticOffsetRadius = 2.7f;
    [SerializeField] private float erraticBurstMultiplier = 1.2f;

    [Header("Anomaly State Machine")]
    [SerializeField] private bool enableAdvancedStates = true;
    [SerializeField] private Vector2 stateDurationMultiplierRange = new Vector2(0.85f, 1.25f);
    [SerializeField] private Vector2 majorStateDurationRange = new Vector2(9f, 15f);
    [SerializeField] private Vector2 levelTwoStateDurationRange = new Vector2(14f, 19f);
    [SerializeField] private Vector2 levelThreeStateDurationRange = new Vector2(17f, 23f);
    [SerializeField] private float speedStateMultiplier = 1.35f;
    [SerializeField] private bool logStateChanges = false;

    [Header("Pacing Director")]
    [SerializeField] private int buildUpMinorStatesMin = 2;
    [SerializeField] private int buildUpMinorStatesMax = 4;
    [SerializeField] private int sustainPeakMajorStatesMin = 1;
    [SerializeField] private int sustainPeakMajorStatesMax = 2;
    [SerializeField] private int peakFadeMinorStates = 1;
    [SerializeField] private int relaxMinorStatesMin = 1;
    [SerializeField] private int relaxMinorStatesMax = 2;
    [SerializeField] private Vector2 peakFadeDurationMultiplierRange = new Vector2(0.7f, 0.95f);
    [SerializeField] private Vector2 relaxDurationMultiplierRange = new Vector2(0.85f, 1.05f);

    [Header("Chaos Feel")]
    [SerializeField] private bool chaosDriveEnabled = true;
    [SerializeField, Range(1f, 2.5f)] private float chaosTempoMultiplier = 1.35f;
    [SerializeField, Range(0.4f, 1f)] private float chaosMinorDurationMultiplier = 0.72f;
    [SerializeField, Range(0.6f, 1.2f)] private float chaosExpansionIntervalMultiplier = 0.8f;
    [SerializeField, Range(1f, 2f)] private float chaosExpansionProjectileMultiplier = 1.3f;
    [SerializeField, Range(0.5f, 1.2f)] private float chaosRepathIntervalMultiplier = 0.8f;

    [Header("State Pulse FX")]
    [SerializeField] private bool statePulseFxEnabled = true;
    [SerializeField] private float statePulseDuration = 0.24f;
    [SerializeField, Range(0f, 1f)] private float statePulseLighten = 0.42f;
    [SerializeField] private float statePulseScaleBoost = 1.08f;
    [SerializeField] private bool stateTransitionBurstEnabled = true;
    [SerializeField] private float stateTransitionBurstDuration = 0.34f;
    [SerializeField] private float stateTransitionBurstRadius = 1.35f;
    [SerializeField] private int stateTransitionBurstRayCount = 10;
    [SerializeField] private float stateTransitionBurstCooldown = 0.12f;

    [Header("Expansion Shoot")]
    [SerializeField] private int expansionShootProjectileCount = 10;
    [SerializeField] private float expansionShootInterval = 2f;
    [SerializeField] private float expansionShootProjectileSpeed = 9.5f;
    [SerializeField] private float expansionShootProjectileLifetime = 3.2f;
    [SerializeField] private float expansionShootSpawnRadius = 0.7f;
    [SerializeField] private Color expansionShootProjectileColor = new Color(1f, 0.48f, 0.63f, 1f);
    [SerializeField] private Vector2 expansionShootProjectileSize = new Vector2(0.24f, 0.24f);
    [SerializeField] private float expansionShootTelegraphLeadTime = 0.6f;
    [SerializeField] private Color expansionShootTelegraphColor = new Color(1f, 0.50f, 0.68f, 0.8f);
    [SerializeField] private float expansionShootTelegraphPulseSpeed = 8.5f;
    [SerializeField] private float expansionShootTelegraphRingRadius = 0.9f;
    [SerializeField] private Vector2 expansionShootTelegraphTickSize = new Vector2(0.22f, 0.06f);

    [Header("Split")]
    [SerializeField] private float splitCloneSpeedMultiplier = 0.95f;
    [SerializeField] private float splitCloneVelocityResponsiveness = 20f;
    [SerializeField] private float splitCloneSideOffset = 1.1f;
    [SerializeField] private float splitCloneSpawnOffset = 0.8f;
    [SerializeField] private float splitMergeLeadTime = 0.7f;
    [SerializeField] private float splitMergeSpeed = 7.5f;
    [SerializeField] private float splitMergeOwnerSpeedMultiplier = 1.05f;
    [SerializeField] private float splitMergeTimeout = 2.4f;
    [SerializeField] private Color splitCloneColor = new Color(1f, 0.30f, 0.42f, 0.92f);

    [Header("Split Merge Telegraph")]
    [SerializeField] private Color splitMergeTelegraphColor = new Color(1f, 0.73f, 0.82f, 0.92f);
    [SerializeField] private float splitMergeTelegraphPulseSpeed = 7.8f;
    [SerializeField] private float splitMergeTelegraphRingRadius = 0.34f;
    [SerializeField] private int splitMergeTelegraphSegments = 8;
    [SerializeField] private Vector2 splitMergeTelegraphSegmentSize = new Vector2(0.18f, 0.04f);

    [Header("Destroyer")]
    [SerializeField] private int destroyerMinBreaks = 3;
    [SerializeField] private int destroyerMaxBreaks = 5;
    [SerializeField] private float destroyerTouchCooldown = 0.06f;
    [SerializeField, Range(0f, 1f)] private float destroyerRepulsionFactor = 0.12f;
    [SerializeField] private float destroyerFractureDuration = 0.12f;
    [SerializeField] private Color destroyerFractureFlashColor = new Color(1f, 0.82f, 0.86f, 1f);
    [SerializeField, Range(0.1f, 1f)] private float destroyerFractureEndScale = 0.2f;
    [SerializeField] private float destroyerFractureSpinDegrees = 25f;
    [SerializeField] private float destroyerRespawnDelay = 8f;
    [SerializeField] private float destroyerRespawnWarningLeadTime = 2f;
    [SerializeField] private float destroyerRespawnWarningPulseSpeed = 7.5f;
    [SerializeField] private Color destroyerRespawnWarningColor = new Color(1f, 0.72f, 0.84f, 0.85f);

    [Header("Weave Hunter")]
    [SerializeField] private float weaveHunterSpeedMultiplier = 1.2f;
    [SerializeField] private float weaveHunterSideOffset = 2f;
    [SerializeField] private float weaveHunterSwitchInterval = 0.23f;
    [SerializeField] private float weaveHunterPlayerVelocityBias = 0.75f;

    [Header("Level 2 - Phase Blink")]
    [SerializeField] private float phaseBlinkInterval = 2.35f;
    [SerializeField] private float phaseBlinkTelegraphSeconds = 0.62f;
    [SerializeField] private float phaseBlinkDistanceFromPlayer = 3.0f;
    [SerializeField] private float phaseBlinkProbeRadius = 0.52f;
    [SerializeField] private int phaseBlinkPlacementAttempts = 18;
    [SerializeField] private Color phaseBlinkColor = new Color(0.66f, 1f, 0.92f, 0.92f);

    [Header("Level 2 - Pincer Barrage")]
    [SerializeField] private float pincerBarrageInterval = 2.2f;
    [SerializeField] private float pincerBarrageTelegraphSeconds = 0.58f;
    [SerializeField] private int pincerProjectilePairs = 3;
    [SerializeField] private float pincerSpawnDistance = 6.2f;
    [SerializeField] private float pincerVerticalSpread = 2.4f;
    [SerializeField] private float pincerProjectileSpeedMultiplier = 1.08f;
    [SerializeField] private Color pincerProjectileColor = new Color(0.92f, 0.62f, 1f, 1f);

    [Header("Level 2 - Signal Jam")]
    [SerializeField] private float signalJamInterval = 2.75f;
    [SerializeField] private float signalJamTelegraphSeconds = 0.72f;
    [SerializeField] private float signalJamRadius = 2.55f;
    [SerializeField] private float signalJamSlowMultiplier = 0.48f;
    [SerializeField] private float signalJamSlowDuration = 1.05f;
    [SerializeField] private Color signalJamColor = new Color(1f, 0.78f, 0.42f, 0.94f);

    [Header("Level 2 - Orbit Barrage")]
    [SerializeField] private float orbitBarrageInterval = 2.55f;
    [SerializeField] private float orbitBarrageTelegraphSeconds = 0.95f;
    [SerializeField] private int orbitBarrageProjectileCount = 8;
    [SerializeField] private float orbitBarrageSpawnRadius = 2.55f;
    [SerializeField] private float orbitBarrageProjectileSpeedMultiplier = 0.92f;
    [SerializeField] private Color orbitBarrageProjectileColor = new Color(0.58f, 0.82f, 1f, 1f);

    [Header("Level 2 - Replay Predator")]
    [SerializeField] private float replaySampleInterval = 0.12f;
    [SerializeField] private float replayMemorySeconds = 4.2f;
    [SerializeField] private float replayPredatorInterval = 3.1f;
    [SerializeField] private float replayPredatorTelegraphSeconds = 0.85f;
    [SerializeField] private int replayPredatorEchoCount = 8;
    [SerializeField] private float replayPredatorEchoRadius = 0.48f;
    [SerializeField] private float replayPredatorGhostTravelSeconds = 1.35f;
    [SerializeField] private Color replayPredatorColor = new Color(1f, 0.42f, 0.76f, 0.92f);

    [Header("Level 2 - Checksum Lattice")]
    [SerializeField] private float checksumLatticeInterval = 6.2f;
    [SerializeField] private float checksumLatticePrepSeconds = 1.35f;
    [SerializeField] private float checksumLatticeDuration = 7.2f;
    [SerializeField] private int checksumLatticeNodeCount = 4;
    [SerializeField] private int checksumLatticeProbeCount = 18;
    [SerializeField] private float checksumLatticeSearchRadius = 4.5f;
    [SerializeField] private float checksumLatticeNodeRadius = 0.5f;
    [SerializeField] private float checksumLatticeRewardStun = 1.8f;
    [SerializeField] private float checksumLatticePenaltySlowMultiplier = 0.46f;
    [SerializeField] private float checksumLatticePenaltyDuration = 1.3f;
    [SerializeField] private float checksumLatticePenaltyPull = 1.2f;
    [SerializeField] private Color checksumLatticeNodeColor = new Color(0.36f, 0.86f, 1f, 0.88f);
    [SerializeField] private Color checksumLatticeActiveColor = new Color(1f, 0.82f, 0.34f, 1f);
    [SerializeField] private Color checksumLatticeFailColor = new Color(1f, 0.34f, 0.72f, 0.95f);

    [Header("Level 2 - Input Desync")]
    [SerializeField] private float inputDesyncInterval = 1.25f;
    [SerializeField] private float inputDesyncDelay = 0.48f;
    [SerializeField] private float inputDesyncDisplacement = 1.15f;
    [SerializeField] private float inputDesyncSlowMultiplier = 0.82f;
    [SerializeField] private float inputDesyncSlowDuration = 0.36f;
    [SerializeField] private Color inputDesyncColor = new Color(0.66f, 0.74f, 1f, 0.88f);

    [Header("Level 2 - Map Recompile")]
    [SerializeField] private float mapRecompileInterval = 5.4f;
    [SerializeField] private float mapRecompileTelegraphSeconds = 1.2f;
    [SerializeField] private int mapRecompileMinObstacleCount = 2;
    [SerializeField] private int mapRecompileMaxObstacleCount = 5;
    [SerializeField] private float mapRecompileMoveDistance = 2.35f;
    [SerializeField] private float mapRecompileMoveSeconds = 0.82f;
    [SerializeField] private float mapRecompileBlockDistanceFromPlayer = 2.65f;
    [SerializeField] private float mapRecompileBlockSpacing = 1.1f;
    [SerializeField] private float mapRecompileMaxObstaclePullDistance = 7.5f;
    [SerializeField] private float mapRecompileHighSpeedThreshold = 7.5f;
    [SerializeField] private float mapRecompileFarDistanceThreshold = 6.4f;
    [SerializeField] private Color mapRecompileColor = new Color(0.92f, 0.62f, 1f, 0.92f);

    [Header("Level 2 - Signal Possession")]
    [SerializeField] private float signalPossessionInterval = 4.8f;
    [SerializeField] private float signalPossessionLifetime = 3.4f;
    [SerializeField] private float signalPossessionArmSeconds = 0.7f;
    [SerializeField] private float signalPossessionRadius = 1.55f;
    [SerializeField] private int signalPossessionProjectileCount = 7;
    [SerializeField] private Color signalPossessionColor = new Color(0.76f, 1f, 0.54f, 0.95f);

    [Header("Level 2 - Phase Contract")]
    [SerializeField] private float phaseContractInterval = 5.6f;
    [SerializeField] private float phaseContractDuration = 4.6f;
    [SerializeField] private float phaseContractGraceSeconds = 1.35f;
    [SerializeField] private float phaseContractMinMoveSpeed = 2.5f;
    [SerializeField] private float phaseContractMinEnemyDistance = 3.1f;
    [SerializeField] private float phaseContractRewardStun = 1.1f;
    [SerializeField] private float phaseContractPenaltySlow = 0.48f;
    [SerializeField] private float phaseContractPenaltyDuration = 1.3f;
    [SerializeField] private Color phaseContractColor = new Color(1f, 0.84f, 0.46f, 0.92f);

    [Header("Level 2 Awakening FX")]
    [SerializeField] private float levelTwoAwakeningDuration = 2.35f;
    [SerializeField] private Color levelTwoAwakenedColor = new Color(0.64f, 1f, 0.94f, 1f);
    [SerializeField] private Color levelTwoAwakeningBurstColor = new Color(1f, 0.54f, 0.96f, 1f);
    [SerializeField, Range(0f, 1f)] private float levelTwoPassiveTint = 0.24f;
    [SerializeField] private float levelTwoAwakeningScaleBoost = 1.28f;

    [Header("Level 3 Awakening FX")]
    [SerializeField] private float levelThreeAwakeningDuration = 2.8f;
    [SerializeField] private Color levelThreeAwakenedColor = new Color(1f, 0.36f, 0.78f, 1f);
    [SerializeField] private Color levelThreeAwakeningBurstColor = new Color(0.38f, 0.94f, 1f, 1f);
    [SerializeField, Range(0f, 1f)] private float levelThreePassiveTint = 0.38f;
    [SerializeField] private float levelThreeAwakeningScaleBoost = 1.42f;

    [Header("State Weights")]
    [SerializeField, Min(0f)] private float directChaseWeight = 1f;
    [SerializeField, Min(0f)] private float predictiveInterceptWeight = 1f;
    [SerializeField, Min(0f)] private float cutoffFlankWeight = 1f;
    [SerializeField, Min(0f)] private float erraticBurstWeight = 1f;
    [SerializeField, Min(0f)] private float splitWeight = 0.7f;
    [SerializeField, Min(0f)] private float expansionShootWeight = 0.8f;
    [SerializeField, Min(0f)] private float speedSurgeWeight = 0.9f;
    [SerializeField, Min(0f)] private float weaveHunterWeight = 0.65f;
    [SerializeField, Min(0f)] private float destroyerWeight = 0.75f;
    [SerializeField, Min(0f)] private float phaseBlinkWeight = 0.72f;
    [SerializeField, Min(0f)] private float pincerBarrageWeight = 0.68f;
    [SerializeField, Min(0f)] private float signalJamWeight = 0.66f;
    [SerializeField, Min(0f)] private float orbitBarrageWeight = 0.64f;
    [SerializeField, Min(0f)] private float replayPredatorWeight = 0.62f;
    [SerializeField, Min(0f)] private float checksumLatticeWeight = 0.58f;
    [SerializeField, Min(0f)] private float inputDesyncWeight = 0.52f;
    [SerializeField, Min(0f)] private float mapRecompileWeight = 0.56f;
    [SerializeField, Min(0f)] private float signalPossessionWeight = 0.54f;
    [SerializeField, Min(0f)] private float phaseContractWeight = 0.50f;
    [SerializeField, Min(0f)] private float adaptiveCountermeasureWeight = 0.62f;
    [SerializeField, Min(0f)] private float signalTetherWeight = 0.62f;
    [SerializeField, Min(0f)] private float blindspotProtocolWeight = 0.64f;

    [Header("Level 2 State Priority")]
    [SerializeField, Range(1f, 5f)] private float levelTwoStatePriorityMultiplier = 2.75f;
    [SerializeField, Range(0.5f, 2f)] private float levelTwoBaseSpecialPriorityMultiplier = 1.12f;
    [SerializeField, Range(0.05f, 1f)] private float levelTwoMinorStatePriorityMultiplier = 0.36f;

    [Header("Level 3 State Priority")]
    [SerializeField, Range(1f, 6f)] private float levelThreeStatePriorityMultiplier = 3.35f;
    [SerializeField, Range(0.2f, 2f)] private float levelThreeLevelTwoPriorityMultiplier = 0.88f;
    [SerializeField, Range(0.1f, 2f)] private float levelThreeBaseSpecialPriorityMultiplier = 0.62f;
    [SerializeField, Range(0.02f, 1f)] private float levelThreeMinorStatePriorityMultiplier = 0.2f;

    public AnomalyState CurrentState => currentState;
    public string CurrentStateLabel => currentState.ToString();
    public PacingPhase CurrentPacingPhase => pacingPhase;
    public string CurrentPacingPhaseLabel => pacingPhase.ToString();
    public bool IsCurrentStateLevelTwo => IsLevelTwoState(currentState);
    public bool IsCurrentStateLevelThree => IsLevelThreeState(currentState);
    public Vector2 CurrentVelocity => rb != null ? rb.linearVelocity : Vector2.zero;
    public bool IsLocalVersusControlled => localVersusControl;
    public float LocalVersusStateTimeRemaining => localVersusControl ? Mathf.Max(0f, localVersusStateDuration - stateTimer) : 0f;
    public float LocalVersusManualChangeTimeRemaining => localVersusControl ? Mathf.Max(0f, localVersusManualChangeUnlock - stateTimer) : 0f;
    public bool CanLocalVersusChangeState => localVersusControl && stateTimer >= localVersusManualChangeUnlock;

    [Header("Navigation Grid")]
    [SerializeField] private LayerMask obstacleMask = ~0;
    [SerializeField] private Vector2 fallbackArenaSize = new Vector2(32f, 18f);
    [SerializeField] private float nodeSize = 0.6f;
    [SerializeField] private float nodeProbePadding = 0.18f;
    [SerializeField] private float repathInterval = 0.16f;
    [SerializeField] private float waypointReachDistance = 0.25f;
    [SerializeField] private float targetRepathThreshold = 0.45f;
    [SerializeField] private int pathLookahead = 4;
    [SerializeField] private float gridRefreshInterval = 0.8f;

    [Header("Obstacle Preference")]
    [SerializeField] private float obstaclePenaltyDistance = 3f;
    [SerializeField] private float obstaclePenaltyWeight = 8f;

    [Header("Local Avoidance")]
    [SerializeField] private float repulsionProbeDistance = 1.75f;
    [SerializeField] private float repulsionWeight = 1.15f;
    [SerializeField] private int repulsionRayCount = 11;
    [SerializeField] private float repulsionSpreadAngle = 150f;

    [Header("Anti-Stuck")]
    [SerializeField] private float stuckCheckInterval = 0.30f;
    [SerializeField] private float stuckDistanceThreshold = 0.09f;
    [SerializeField] private int stuckChecksBeforeRecovery = 2;
    [SerializeField] private int stuckChecksBeforeEmergencyDestroyer = 5;
    [SerializeField] private float stuckTargetMinDistance = 1.4f;
    [SerializeField] private float stuckObstacleProbeRadius = 1.45f;
    [SerializeField] private float stuckEscapeDistance = 2.9f;
    [SerializeField] private float stuckEscapeVelocityBoost = 1.35f;
    [SerializeField] private float emergencyDestroyerDuration = 2f;

    [Header("Path Hysteresis")]
    [SerializeField] private float blockedRepathHoldSeconds = 0.28f;
    [SerializeField] private float blockedRepathDistanceMultiplier = 1.8f;
    [SerializeField] private float blockedPathCommitSeconds = 1.15f;
    [SerializeField] private float blockedPathCommitDistanceMultiplier = 3f;
    [SerializeField] private int blockedOscillationThreshold = 3;
    [SerializeField] private float blockedOscillationResetSeconds = 1.2f;

    [Header("Player Parry Reaction")]
    [SerializeField] private float parryStunDuration = 0.34f;
    [SerializeField] private float parryKnockbackDuration = 0.16f;
    [SerializeField] private float parryKnockbackSpeed = 12f;
    [SerializeField] private float parryBurstRadius = 1.45f;
    [SerializeField] private float parryBurstDuration = 0.22f;

    [Header("Local Versus")]
    [SerializeField] private float localVersusManualChangeUnlock = 10f;
    [SerializeField] private float localVersusStateDuration = 20f;
    [SerializeField, Range(1f, 1.6f)] private float localVersusMoveSpeedMultiplier = 1.22f;

    private Rigidbody2D rb;
    private Collider2D ownCollider;
    private SpriteRenderer ownRenderer;
    private bool localVersusControl;

    private AnomalyState currentState;
    private BehaviorPattern currentPattern;
    private float stateTimer;
    private float currentStateDuration;
    private float erraticRefreshTimer;
    private float flankRetargetTimer;
    private float expansionShootTimer;
    private float flankSide = 1f;
    private int projectileSerial;
    private GameObject expansionTelegraphRoot;
    private SpriteRenderer expansionTelegraphRing;
    private readonly List<SpriteRenderer> expansionTelegraphTicks = new List<SpriteRenderer>();
    private SplitAnomalyCloneController splitClone;
    private bool splitStateActive;
    private bool splitMergeInProgress;
    private int splitSideSign = 1;
    private float splitCloneRadius = 0.45f;
    private float splitMergeTimer;
    private GameObject splitMergeTelegraphRoot;
    private SpriteRenderer splitMergeOwnerRing;
    private SpriteRenderer splitMergeCloneRing;
    private readonly List<SpriteRenderer> splitMergeBridgeSegments = new List<SpriteRenderer>();
    private int destroyerBreakLimit;
    private int destroyerBreakCount;
    private float destroyerTouchCooldownTimer;
    private readonly HashSet<int> destroyerDestroyedIds = new HashSet<int>();
    private readonly HashSet<int> destroyerPendingRespawnIds = new HashSet<int>();
    private float weaveHunterTimer;
    private float weaveHunterSideSign = 1f;
    private float phaseBlinkTimer;
    private bool phaseBlinkCharging;
    private Vector2 phaseBlinkTarget;
    private GameObject phaseBlinkTelegraphRoot;
    private SpriteRenderer phaseBlinkRingRenderer;
    private SpriteRenderer phaseBlinkCoreRenderer;
    private SpriteRenderer phaseBlinkOriginRenderer;
    private SpriteRenderer phaseBlinkPathRenderer;
    private SpriteRenderer phaseBlinkCrossHorizontalRenderer;
    private SpriteRenderer phaseBlinkCrossVerticalRenderer;
    private float pincerBarrageTimer;
    private bool pincerBarrageCharging;
    private Vector2 pincerLeftSpawn;
    private Vector2 pincerRightSpawn;
    private GameObject pincerTelegraphRoot;
    private SpriteRenderer pincerFocusRingRenderer;
    private readonly List<SpriteRenderer> pincerTelegraphRenderers = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> pincerLaneRenderers = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> pincerArrowRenderers = new List<SpriteRenderer>();
    private float signalJamTimer;
    private bool signalJamCharging;
    private Vector2 signalJamCenter;
    private GameObject signalJamTelegraphRoot;
    private SpriteRenderer signalJamRingRenderer;
    private SpriteRenderer signalJamCoreRenderer;
    private SpriteRenderer signalJamInnerRenderer;
    private SpriteRenderer signalJamCrossHorizontalRenderer;
    private SpriteRenderer signalJamCrossVerticalRenderer;
    private SpriteRenderer signalJamWarningRenderer;
    private readonly List<SpriteRenderer> signalJamTickRenderers = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> signalJamNoiseRenderers = new List<SpriteRenderer>();
    private float orbitBarrageTimer;
    private bool orbitBarrageCharging;
    private Vector2 orbitBarrageCenter;
    private int orbitBarrageDirectionSign = 1;
    private GameObject orbitBarrageTelegraphRoot;
    private SpriteRenderer orbitBarrageRingRenderer;
    private SpriteRenderer orbitBarrageInnerRingRenderer;
    private readonly List<SpriteRenderer> orbitBarrageTickRenderers = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> orbitBarrageGuideRenderers = new List<SpriteRenderer>();
    private readonly List<ReplaySample> replaySamples = new List<ReplaySample>();
    private float replaySampleTimer;
    private float replayPredatorTimer;
    private float checksumLatticeTimer;
    private ChecksumLatticeFx checksumLatticeFx;
    private float inputDesyncTimer;
    private float mapRecompileTimer;
    private bool mapRecompileCharging;
    private GameObject mapRecompileTelegraphRoot;
    private readonly List<SpriteRenderer> mapRecompileTelegraphRenderers = new List<SpriteRenderer>();
    private readonly List<RecompileTarget> mapRecompileTargets = new List<RecompileTarget>();
    private float signalPossessionTimer;
    private SignalPossessionLure activeSignalPossessionLure;
    private float phaseContractTimer;
    private bool phaseContractActive;
    private float phaseContractActiveTimer;
    private int phaseContractRuleIndex;
    private GameObject phaseContractRoot;
    private TextMesh phaseContractText;
    private SpriteRenderer phaseContractRingRenderer;
    private SpriteRenderer phaseContractLineRenderer;
    private PacingPhase pacingPhase = PacingPhase.BuildUp;
    private int pacingMinorStatesRemaining;
    private int pacingMajorStatesRemaining;
    private float statePulseTimer;
    private float stateTransitionBurstCooldownTimer;
    private float externalSpeedTimer;
    private float externalSpeedMultiplier = 1f;
    private float sectorSpeedMultiplier = 1f;
    private float sectorResponseMultiplier = 1f;
    private float parryStunTimer;
    private float parryKnockbackTimer;
    private float breachLureTimer;
    private Vector2 breachLureTarget;
    private bool breachAbsorbed;
    private bool levelTwoAwakened;
    private float levelTwoAwakeningTimer;
    private bool levelThreeAwakened;
    private float levelThreeAwakeningTimer;
    private EnemyLevelThreeStateController levelThreeStateController;
    private Vector3 baseScale = Vector3.one;
    private Color baseColor = Color.white;

    private Vector2 erraticTarget;
    private Vector2 lastMoveDirection = Vector2.right;

    private Vector2 navOrigin;
    private Vector2 navSize;
    private int gridWidth;
    private int gridHeight;
    private bool[,] walkable;
    private int[,] obstacleDistanceSteps;

    private float repathTimer;
    private float gridRefreshTimer;
    private Vector2 lastPathGoal;
    private readonly List<Vector2> pathWorld = new List<Vector2>();
    private int pathIndex;

    private float stuckTimer;
    private Vector2 stuckCheckPosition;
    private int stuckConsecutiveChecks;
    private bool emergencyDestroyerActive;
    private Vector2 pendingBlockedRepathGoal;
    private float pendingBlockedRepathTimer;
    private bool hasPendingBlockedRepathGoal;
    private float blockedPathCommitTimer;
    private int blockedOscillationCounter;
    private float blockedOscillationTimer;
    private float agentRadius;
    private static PhysicsMaterial2D noFrictionMaterial;

    private static readonly Vector2Int[] NeighborOffsets =
    {
        new Vector2Int(-1, 0),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(0, 1),
        new Vector2Int(-1, -1),
        new Vector2Int(-1, 1),
        new Vector2Int(1, -1),
        new Vector2Int(1, 1)
    };

    private static readonly Vector2Int[] DistanceFieldOffsets =
    {
        new Vector2Int(-1, 0),
        new Vector2Int(1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(0, 1)
    };

    private struct StateWeight
    {
        public AnomalyState state;
        public float weight;

        public StateWeight(AnomalyState state, float weight)
        {
            this.state = state;
            this.weight = weight;
        }
    }

    private struct ReplaySample
    {
        public Vector2 position;
        public float time;
    }

    private struct RecompileTarget
    {
        public Transform transform;
        public Rigidbody2D rigidbody;
        public Vector2 start;
        public Vector2 target;
        public SpriteRenderer[] renderers;
        public Color[] colors;
    }

    private struct ChecksumNodePlan
    {
        public Vector2 position;
        public float score;
        public int checksum;
    }

    private sealed class DestroyerRespawnSnapshot
    {
        public GameObject target;
        public Transform targetTransform;
        public Vector3 startScale;
        public Quaternion startRotation;
        public Collider2D[] colliders;
        public bool[] colliderEnabled;
        public Rigidbody2D rigidbody;
        public bool rigidbodySimulated;
        public SpriteRenderer[] renderers;
        public bool[] rendererEnabled;
        public Color[] rendererColors;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        ownCollider = GetComponent<Collider2D>();
        ownRenderer = GetComponent<SpriteRenderer>();

        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        Bounds bounds = ownCollider.bounds;
        agentRadius = Mathf.Max(bounds.extents.x, bounds.extents.y);
        if (agentRadius < 0.05f)
        {
            agentRadius = 0.45f;
        }

        EnsureNoFrictionMaterial();
        ownCollider.sharedMaterial = noFrictionMaterial;

        baseScale = transform.localScale;
        if (ownRenderer != null)
        {
            baseColor = ownRenderer.color;
        }

        levelThreeStateController = GetComponent<EnemyLevelThreeStateController>();
        if (levelThreeStateController == null)
        {
            levelThreeStateController = gameObject.AddComponent<EnemyLevelThreeStateController>();
        }
    }

    private static void EnsureNoFrictionMaterial()
    {
        if (noFrictionMaterial != null)
        {
            return;
        }

        noFrictionMaterial = new PhysicsMaterial2D("EnemyNoFriction")
        {
            friction = 0f,
            bounciness = 0f
        };
    }

    private void Start()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<PlayerController>();
        }

        if (gameManager == null)
        {
            gameManager = FindAnyObjectByType<GameManager>();
        }

        levelThreeStateController?.Configure(this, player, gameManager);
        ResolveArenaBounds();
        BuildNavigationGrid();
        InitializePacingDirector();

        stuckCheckPosition = rb.position;
        if (localVersusControl)
        {
            SelectNextLocalVersusState();
        }
        else
        {
            SelectNextState(forceDifferent: false);
        }
    }

    private void OnDisable()
    {
        DestroySplitCloneImmediate();
        DestroySplitMergeTelegraphImmediate();
        DestroyLevelTwoTelegraphsImmediate();
        levelThreeStateController?.ExitState();
        if (ownRenderer != null)
        {
            ownRenderer.color = baseColor;
        }

        levelTwoAwakened = false;
        levelTwoAwakeningTimer = 0f;
        levelThreeAwakened = false;
        levelThreeAwakeningTimer = 0f;
        transform.localScale = baseScale;
    }

    private void Update()
    {
        if (breachAbsorbed)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (gameManager == null || player == null || gameManager.IsGameOver || !gameManager.IsRunActive)
        {
            rb.linearVelocity = Vector2.zero;
            UpdateStatePulseVisual();
            return;
        }

        RecordReplaySample();
        HandleStateSwitch();
        UpdatePatternInternals();
        UpdateStateAbilities();
        UpdateStatePulseVisual();
        UpdateExternalSpeedEffects();
        if (stateTransitionBurstCooldownTimer > 0f)
        {
            stateTransitionBurstCooldownTimer -= Time.deltaTime;
        }

        if (TickParryStun())
        {
            return;
        }

        if (localVersusControl)
        {
            UpdateLocalVersusMovement();
            return;
        }

        if (levelThreeStateController != null &&
            levelThreeStateController.TryGetMovementOverride(out Vector2 levelThreeVelocity))
        {
            Vector2 overrideTarget = levelThreeStateController.GetMovementOverrideTarget(player.GetPosition());
            UpdateStuckDetection(overrideTarget);
            if (emergencyDestroyerActive || currentState == AnomalyState.Destroyer)
            {
                return;
            }

            rb.linearVelocity = Vector2.MoveTowards(
                rb.linearVelocity,
                levelThreeVelocity,
                velocityResponsiveness * sectorResponseMultiplier * Time.deltaTime);
            if (rb.linearVelocity.sqrMagnitude > 0.001f)
            {
                lastMoveDirection = rb.linearVelocity.normalized;
            }
            return;
        }

        if (splitMergeInProgress && splitClone != null)
        {
            TickOwnerSplitMergeMovement();
            UpdateSplitMergeTelegraphVisual();
            return;
        }
        HideSplitMergeTelegraphVisual();

        // Reconstruye la grilla periodicamente porque eventos y el estado destructor pueden mover o quitar bloqueos.
        gridRefreshTimer += Time.deltaTime;
        if (gridRefreshTimer >= gridRefreshInterval)
        {
            gridRefreshTimer = 0f;
            BuildNavigationGrid();
        }

        Vector2 strategicTarget = ClampToArena(GetStrategicTarget());
        bool destroyerState = currentState == AnomalyState.Destroyer;
        bool destroyerOutOfBreaks = destroyerState && destroyerBreakLimit > 0 && destroyerBreakCount >= destroyerBreakLimit;
        bool useDestroyerSteering = destroyerState && !destroyerOutOfBreaks;

        if (!useDestroyerSteering)
        {
            repathTimer += Time.deltaTime;
            if (blockedPathCommitTimer > 0f)
            {
                blockedPathCommitTimer -= Time.deltaTime;
            }
            if (blockedOscillationTimer > 0f)
            {
                blockedOscillationTimer -= Time.deltaTime;
                if (blockedOscillationTimer <= 0f)
                {
                    blockedOscillationCounter = 0;
                }
            }

            bool targetMoved = Vector2.Distance(strategicTarget, lastPathGoal) >= targetRepathThreshold;
            bool hasDirectToStrategic = HasDirectPath(rb.position, strategicTarget);
            bool obstacleBlocked = !hasDirectToStrategic && pathWorld.Count > 0;
            bool pathAheadBlocked = IsCurrentPathSegmentBlocked();
            float effectiveRepathInterval = Mathf.Max(
                0.05f,
                repathInterval * (chaosDriveEnabled ? chaosRepathIntervalMultiplier : 1f));

            bool intervalRepathDue = repathTimer >= effectiveRepathInterval;
            bool shouldRepath = pathWorld.Count == 0 || pathAheadBlocked;
            if (!shouldRepath && intervalRepathDue)
            {
                // Si ya eligio un rodeo con linea bloqueada, no recalcula por intervalo hasta cumplir el compromiso.
                // Esto evita el loop izquierda/derecha cuando el jugador usa un obstaculo como cobertura.
                shouldRepath = !obstacleBlocked || blockedPathCommitTimer <= 0f;
            }

            if (!shouldRepath && targetMoved)
            {
                if (!obstacleBlocked)
                {
                    shouldRepath = true;
                    ResetBlockedRepathHysteresis();
                }
                else
                {
                    // Si el jugador se mueve detras de cobertura, espera antes de aceptar una nueva ruta bloqueada.
                    // Asi se evitan cambios bruscos cuando el objetivo tiembla alrededor de esquinas.
                    float forceDistance = Mathf.Max(targetRepathThreshold, targetRepathThreshold * blockedRepathDistanceMultiplier);
                    float targetShift = Vector2.Distance(strategicTarget, lastPathGoal);
                    float resetThreshold = Mathf.Max(0.06f, targetRepathThreshold * 0.35f);
                    bool inCommitWindow = blockedPathCommitTimer > 0f;
                    float commitBreakDistance = Mathf.Max(forceDistance, targetRepathThreshold * blockedPathCommitDistanceMultiplier);

                    if (!hasPendingBlockedRepathGoal || Vector2.Distance(strategicTarget, pendingBlockedRepathGoal) > resetThreshold)
                    {
                        pendingBlockedRepathGoal = strategicTarget;
                        pendingBlockedRepathTimer = 0f;
                        hasPendingBlockedRepathGoal = true;
                    }
                    else
                    {
                        pendingBlockedRepathTimer += Time.deltaTime;
                    }

                    bool stable = pendingBlockedRepathTimer >= Mathf.Max(0.05f, blockedRepathHoldSeconds);
                    bool farEnough = targetShift >= forceDistance;
                    bool breakCommit = inCommitWindow && targetShift >= commitBreakDistance;
                    if ((!inCommitWindow && (stable || farEnough)) || breakCommit)
                    {
                        shouldRepath = true;
                    }
                    else
                    {
                        blockedOscillationCounter++;
                        blockedOscillationTimer = Mathf.Max(0.2f, blockedOscillationResetSeconds);
                    }
                }
            }
            else if (!obstacleBlocked)
            {
                ResetBlockedRepathHysteresis();
            }

            if (!shouldRepath &&
                obstacleBlocked &&
                currentState != AnomalyState.Destroyer &&
                blockedOscillationCounter >= Mathf.Max(2, blockedOscillationThreshold))
            {
                EnterEmergencyDestroyerFromStuck();
                blockedOscillationCounter = 0;
                blockedOscillationTimer = 0f;
                return;
            }

            if (shouldRepath)
            {
                RebuildPathTo(strategicTarget);
                bool shouldCommit = obstacleBlocked && !pathAheadBlocked;
                ResetBlockedRepathHysteresis();
                if (shouldCommit)
                {
                    blockedPathCommitTimer = Mathf.Max(0.05f, blockedPathCommitSeconds);
                }
            }
        }

        Vector2 steeringTarget = useDestroyerSteering ? strategicTarget : SelectSteeringTarget(strategicTarget);
        Vector2 desiredDirection = steeringTarget - rb.position;

        if (desiredDirection.sqrMagnitude < 0.0001f)
        {
            desiredDirection = lastMoveDirection;
        }
        else
        {
            desiredDirection.Normalize();
        }

        if (useDestroyerSteering)
        {
            Vector2 repulsed = ApplyObstacleRepulsion(desiredDirection);
            desiredDirection = Vector2.Lerp(desiredDirection, repulsed, Mathf.Clamp01(destroyerRepulsionFactor));
            if (desiredDirection.sqrMagnitude > 0.0001f)
            {
                desiredDirection.Normalize();
            }
        }
        else
        {
            desiredDirection = ApplyObstacleRepulsion(desiredDirection);
        }
        if (desiredDirection.sqrMagnitude > 0.0001f)
        {
            lastMoveDirection = desiredDirection;
        }

        UpdateStuckDetection(strategicTarget);

        float speed = GetCurrentStateMoveSpeed();

        Vector2 desiredVelocity = desiredDirection * speed;
        rb.linearVelocity = Vector2.MoveTowards(
            rb.linearVelocity,
            desiredVelocity,
            velocityResponsiveness * sectorResponseMultiplier * Time.deltaTime);
    }

    private void ResolveArenaBounds()
    {
        navSize = fallbackArenaSize;
        navOrigin = new Vector2(-navSize.x * 0.5f, -navSize.y * 0.5f);

        ProceduralArenaGenerator generator = FindAnyObjectByType<ProceduralArenaGenerator>();
        if (generator == null)
        {
            return;
        }

        navSize = new Vector2(generator.ArenaWidth, generator.ArenaHeight);
        Vector2 center = generator.transform.position;
        navOrigin = center - navSize * 0.5f;
    }

    private void BuildNavigationGrid()
    {
        gridWidth = Mathf.Max(8, Mathf.RoundToInt(navSize.x / nodeSize) + 1);
        gridHeight = Mathf.Max(8, Mathf.RoundToInt(navSize.y / nodeSize) + 1);

        walkable = new bool[gridWidth, gridHeight];
        obstacleDistanceSteps = new int[gridWidth, gridHeight];

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                Vector2 world = CellToWorld(new Vector2Int(x, y));
                walkable[x, y] = IsWalkableWorld(world);
                obstacleDistanceSteps[x, y] = int.MaxValue;
            }
        }

        ComputeObstacleDistanceField();
    }

    private void ComputeObstacleDistanceField()
    {
        Queue<Vector2Int> queue = new Queue<Vector2Int>();

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                if (!walkable[x, y])
                {
                    obstacleDistanceSteps[x, y] = 0;
                    queue.Enqueue(new Vector2Int(x, y));
                }
            }
        }

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            int currentStep = obstacleDistanceSteps[current.x, current.y];

            for (int i = 0; i < DistanceFieldOffsets.Length; i++)
            {
                Vector2Int next = current + DistanceFieldOffsets[i];
                if (!IsCellInside(next))
                {
                    continue;
                }

                int nextStep = currentStep + 1;
                if (nextStep >= obstacleDistanceSteps[next.x, next.y])
                {
                    continue;
                }

                obstacleDistanceSteps[next.x, next.y] = nextStep;
                queue.Enqueue(next);
            }
        }
    }

    private bool IsWalkableWorld(Vector2 world)
    {
        float clearance = agentRadius + nodeProbePadding;
        Collider2D[] hits = Physics2D.OverlapCircleAll(world, clearance, obstacleMask);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (!IsBlockingCollider(hit))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private void HandleStateSwitch()
    {
        if (localVersusControl)
        {
            stateTimer += Time.deltaTime;
            if (stateTimer >= Mathf.Max(localVersusManualChangeUnlock, localVersusStateDuration) ||
                (stateTimer >= localVersusManualChangeUnlock && WasLocalVersusStateChangePressed()))
            {
                SelectNextLocalVersusState();
            }
            return;
        }

        if (AreSpecialStatesSuppressedForBreach() && IsPre60SpecialState(currentState))
        {
            SelectNextState(forceDifferent: true);
            return;
        }
        if (gameManager != null &&
            gameManager.IsContainmentPulsePressureActive &&
            (IsLevelTwoState(currentState) || IsLevelThreeState(currentState)))
        {
            SelectNextState(forceDifferent: true);
            return;
        }

        stateTimer += Time.deltaTime;

        if (emergencyDestroyerActive)
        {
            if (stateTimer >= currentStateDuration)
            {
                emergencyDestroyerActive = false;
                SelectNextState(forceDifferent: true);
            }

            return;
        }

        if (stateTimer >= currentStateDuration)
        {
            SelectNextState(forceDifferent: true);
        }
    }

    private void SelectNextState(bool forceDifferent)
    {
        emergencyDestroyerActive = false;
        stuckConsecutiveChecks = 0;
        ResetBlockedRepathHysteresis();
        AnomalyState previousState = currentState;
        AnomalyState nextState = PickWeightedState(forceDifferent);
        currentState = nextState;
        currentPattern = ResolvePatternForState(currentState);
        stateTimer = 0f;

        currentStateDuration = GetRandomDurationForState(currentState);

        if (logStateChanges)
        {
            Debug.Log($"[Anomaly] State -> {currentState} ({currentStateDuration:F2}s) | Phase: {pacingPhase}");
        }

        HandleStateTransition(previousState, currentState);
        TriggerStatePulse();
        SpawnStateTransitionBurst(currentState, previousState != currentState);
        if (CanDamagePlayer())
        {
            GlitchAudioManager.PlayEnemyState(currentState, transform.position);
        }
        OnStateEntered();
        RegisterStateForPacing(currentState);
    }

    public void EnableLocalVersusControl(PlayerController controlledTarget)
    {
        localVersusControl = true;
        if (controlledTarget != null)
        {
            player = controlledTarget;
        }

        emergencyDestroyerActive = false;
        DestroySplitCloneImmediate();
        levelThreeStateController?.ExitState();
        SelectNextLocalVersusState();
    }

    private void SelectNextLocalVersusState()
    {
        AnomalyState[] options =
        {
            AnomalyState.DirectChase,
            AnomalyState.SpeedSurge,
            AnomalyState.Destroyer,
            AnomalyState.ExpansionShoot,
            AnomalyState.PhaseBlink,
            AnomalyState.PincerBarrage,
            AnomalyState.SignalJam,
            AnomalyState.OrbitBarrage
        };

        AnomalyState previous = currentState;
        int previousIndex = System.Array.IndexOf(options, previous);
        int offset = Random.Range(1, options.Length);
        int nextIndex = previousIndex >= 0 ? (previousIndex + offset) % options.Length : Random.Range(0, options.Length);
        ApplyLocalVersusState(options[nextIndex], previous);
    }

    private void ApplyLocalVersusState(AnomalyState next, AnomalyState previous)
    {
        emergencyDestroyerActive = false;
        stuckConsecutiveChecks = 0;
        ResetBlockedRepathHysteresis();
        currentState = next;
        currentPattern = ResolvePatternForState(currentState);
        stateTimer = 0f;
        currentStateDuration = Mathf.Max(localVersusManualChangeUnlock, localVersusStateDuration);
        HandleStateTransition(previous, currentState);
        TriggerStatePulse();
        SpawnStateTransitionBurst(currentState, previous != currentState);
        GlitchAudioManager.PlayEnemyState(currentState, transform.position);
        OnStateEntered();
    }

    private void UpdateLocalVersusMovement()
    {
        Vector2 input = ReadLocalVersusMoveInput();
        if (input.sqrMagnitude > 1f)
        {
            input.Normalize();
        }
        if (input.sqrMagnitude > 0.001f)
        {
            lastMoveDirection = input.normalized;
        }

        float speed = GetCurrentStateMoveSpeed();
        rb.linearVelocity = Vector2.MoveTowards(
            rb.linearVelocity,
            input * speed,
            velocityResponsiveness * sectorResponseMultiplier * Time.deltaTime);
    }

    private float GetCurrentStateMoveSpeed()
    {
        float speed = baseMoveSpeed * sectorSpeedMultiplier;
        if (localVersusControl)
        {
            speed *= Mathf.Max(1f, localVersusMoveSpeedMultiplier);
        }
        if (currentPattern == BehaviorPattern.ErraticBurst)
        {
            speed *= erraticBurstMultiplier;
        }
        if (currentState == AnomalyState.SpeedSurge)
        {
            speed *= speedStateMultiplier;
        }
        else if (currentState == AnomalyState.WeaveHunter)
        {
            speed *= Mathf.Max(0.1f, weaveHunterSpeedMultiplier);
        }
        if (chaosDriveEnabled)
        {
            speed *= Mathf.Lerp(1f, chaosTempoMultiplier, 0.42f);
        }
        if (externalSpeedTimer > 0f)
        {
            speed *= externalSpeedMultiplier;
        }
        return speed;
    }

    private static Vector2 ReadLocalVersusMoveInput()
    {
        Vector2 input = Vector2.zero;
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.leftArrowKey.isPressed) input.x -= 1f;
            if (keyboard.rightArrowKey.isPressed) input.x += 1f;
            if (keyboard.downArrowKey.isPressed) input.y -= 1f;
            if (keyboard.upArrowKey.isPressed) input.y += 1f;
        }
        if (Gamepad.all.Count > 1)
        {
            input += Gamepad.all[1].leftStick.ReadValue();
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(KeyCode.LeftArrow)) input.x -= 1f;
        if (Input.GetKey(KeyCode.RightArrow)) input.x += 1f;
        if (Input.GetKey(KeyCode.DownArrow)) input.y -= 1f;
        if (Input.GetKey(KeyCode.UpArrow)) input.y += 1f;
#endif
        return Vector2.ClampMagnitude(input, 1f);
    }

    private static bool WasLocalVersusStateChangePressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null &&
            (keyboard.rightCtrlKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame ||
             keyboard.numpadEnterKey.wasPressedThisFrame))
        {
            return true;
        }
        if (Gamepad.all.Count > 1 && Gamepad.all[1].rightShoulder.wasPressedThisFrame)
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.RightControl) || Input.GetKeyDown(KeyCode.Return) ||
               Input.GetKeyDown(KeyCode.KeypadEnter);
#else
        return false;
#endif
    }

    private float GetRandomDurationForState(AnomalyState state)
    {
        if (IsLevelThreeState(state))
        {
            float minLevelThree = Mathf.Max(1f, Mathf.Min(levelThreeStateDurationRange.x, levelThreeStateDurationRange.y));
            float maxLevelThree = Mathf.Max(minLevelThree, Mathf.Max(levelThreeStateDurationRange.x, levelThreeStateDurationRange.y));
            return Random.Range(minLevelThree, maxLevelThree);
        }

        if (IsLevelTwoState(state))
        {
            float minLevelTwo = Mathf.Max(1f, Mathf.Min(levelTwoStateDurationRange.x, levelTwoStateDurationRange.y));
            float maxLevelTwo = Mathf.Max(minLevelTwo, Mathf.Max(levelTwoStateDurationRange.x, levelTwoStateDurationRange.y));
            return Random.Range(minLevelTwo, maxLevelTwo);
        }

        if (state == AnomalyState.Split ||
            state == AnomalyState.ExpansionShoot ||
            state == AnomalyState.Destroyer ||
            state == AnomalyState.SpeedSurge ||
            state == AnomalyState.PhaseBlink ||
            state == AnomalyState.PincerBarrage ||
            state == AnomalyState.SignalJam ||
            state == AnomalyState.OrbitBarrage ||
            state == AnomalyState.ReplayPredator ||
            state == AnomalyState.ChecksumLattice ||
            state == AnomalyState.InputDesync ||
            state == AnomalyState.MapRecompile ||
            state == AnomalyState.SignalPossession ||
            state == AnomalyState.PhaseContract)
        {
            float minMajor = Mathf.Max(0.6f, Mathf.Min(majorStateDurationRange.x, majorStateDurationRange.y));
            float maxMajor = Mathf.Max(minMajor, Mathf.Max(majorStateDurationRange.x, majorStateDurationRange.y));
            return Random.Range(minMajor, maxMajor);
        }

        float baseInterval = gameManager != null ? gameManager.CurrentBehaviorChangeInterval : 5f;
        float minMul = Mathf.Min(stateDurationMultiplierRange.x, stateDurationMultiplierRange.y);
        float maxMul = Mathf.Max(stateDurationMultiplierRange.x, stateDurationMultiplierRange.y);
        float durationMultiplier = Random.Range(minMul, maxMul);

        if (pacingPhase == PacingPhase.PeakFade)
        {
            float minFade = Mathf.Min(peakFadeDurationMultiplierRange.x, peakFadeDurationMultiplierRange.y);
            float maxFade = Mathf.Max(peakFadeDurationMultiplierRange.x, peakFadeDurationMultiplierRange.y);
            durationMultiplier *= Random.Range(minFade, maxFade);
        }
        else if (pacingPhase == PacingPhase.Relax)
        {
            float minRelax = Mathf.Min(relaxDurationMultiplierRange.x, relaxDurationMultiplierRange.y);
            float maxRelax = Mathf.Max(relaxDurationMultiplierRange.x, relaxDurationMultiplierRange.y);
            durationMultiplier *= Random.Range(minRelax, maxRelax);
        }

        if (chaosDriveEnabled)
        {
            durationMultiplier *= Mathf.Clamp(chaosMinorDurationMultiplier, 0.4f, 1f);
        }

        return Mathf.Max(0.6f, baseInterval * durationMultiplier);
    }

    private void InitializePacingDirector()
    {
        pacingPhase = PacingPhase.BuildUp;
        pacingMinorStatesRemaining = RollBuildUpMinorCount();
        pacingMajorStatesRemaining = 0;
    }

    private void RegisterStateForPacing(AnomalyState state)
    {
        bool major = IsMajorState(state);
        switch (pacingPhase)
        {
            case PacingPhase.BuildUp:
                if (!major)
                {
                    pacingMinorStatesRemaining--;
                    if (pacingMinorStatesRemaining <= 0)
                    {
                        TransitionToPacingPhase(PacingPhase.SustainPeak);
                    }
                }
                break;
            case PacingPhase.SustainPeak:
                if (major)
                {
                    pacingMajorStatesRemaining--;
                    if (pacingMajorStatesRemaining <= 0)
                    {
                        TransitionToPacingPhase(PacingPhase.PeakFade);
                    }
                }
                break;
            case PacingPhase.PeakFade:
                if (!major)
                {
                    pacingMinorStatesRemaining--;
                    if (pacingMinorStatesRemaining <= 0)
                    {
                        TransitionToPacingPhase(PacingPhase.Relax);
                    }
                }
                break;
            case PacingPhase.Relax:
                if (!major)
                {
                    pacingMinorStatesRemaining--;
                    if (pacingMinorStatesRemaining <= 0)
                    {
                        TransitionToPacingPhase(PacingPhase.BuildUp);
                    }
                }
                break;
        }
    }

    private void TransitionToPacingPhase(PacingPhase nextPhase)
    {
        pacingPhase = nextPhase;
        switch (pacingPhase)
        {
            case PacingPhase.BuildUp:
                pacingMinorStatesRemaining = RollBuildUpMinorCount();
                pacingMajorStatesRemaining = 0;
                break;
            case PacingPhase.SustainPeak:
                pacingMinorStatesRemaining = 0;
                pacingMajorStatesRemaining = RollSustainPeakMajorCount();
                break;
            case PacingPhase.PeakFade:
                pacingMinorStatesRemaining = Mathf.Max(1, peakFadeMinorStates);
                pacingMajorStatesRemaining = 0;
                break;
            case PacingPhase.Relax:
                pacingMinorStatesRemaining = RollRelaxMinorCount();
                pacingMajorStatesRemaining = 0;
                break;
        }
    }

    private int RollBuildUpMinorCount()
    {
        int min = Mathf.Max(1, Mathf.Min(buildUpMinorStatesMin, buildUpMinorStatesMax));
        int max = Mathf.Max(min, Mathf.Max(buildUpMinorStatesMin, buildUpMinorStatesMax));
        return Random.Range(min, max + 1);
    }

    private int RollRelaxMinorCount()
    {
        int min = Mathf.Max(1, Mathf.Min(relaxMinorStatesMin, relaxMinorStatesMax));
        int max = Mathf.Max(min, Mathf.Max(relaxMinorStatesMin, relaxMinorStatesMax));
        return Random.Range(min, max + 1);
    }

    private int RollSustainPeakMajorCount()
    {
        int min = Mathf.Max(1, Mathf.Min(sustainPeakMajorStatesMin, sustainPeakMajorStatesMax));
        int max = Mathf.Max(min, Mathf.Max(sustainPeakMajorStatesMin, sustainPeakMajorStatesMax));
        int rolled = Random.Range(min, max + 1);
        if (chaosDriveEnabled)
        {
            rolled += 1;
        }

        return rolled;
    }

    private void OnStateEntered()
    {
        expansionShootTimer = 0f;
        HideExpansionShootTelegraphVisual();
        destroyerTouchCooldownTimer = 0f;
        destroyerDestroyedIds.Clear();

        if (currentState == AnomalyState.Destroyer)
        {
            int minBreaks = Mathf.Max(0, Mathf.Min(destroyerMinBreaks, destroyerMaxBreaks));
            int maxBreaks = Mathf.Max(minBreaks, Mathf.Max(destroyerMinBreaks, destroyerMaxBreaks));
            destroyerBreakLimit = Random.Range(minBreaks, maxBreaks + 1);
            if (emergencyDestroyerActive)
            {
                destroyerBreakLimit = Mathf.Clamp(destroyerBreakLimit, 1, 2);
            }
            destroyerBreakCount = 0;
        }
        else
        {
            destroyerBreakLimit = 0;
            destroyerBreakCount = 0;
        }

        if (currentState == AnomalyState.WeaveHunter)
        {
            weaveHunterTimer = 0f;
            weaveHunterSideSign = Random.value < 0.5f ? -1f : 1f;
        }
        else if (currentState == AnomalyState.PhaseBlink)
        {
            phaseBlinkTimer = Mathf.Max(0.25f, phaseBlinkInterval * 0.45f);
            phaseBlinkCharging = false;
            HidePhaseBlinkTelegraph();
        }
        else if (currentState == AnomalyState.PincerBarrage)
        {
            pincerBarrageTimer = Mathf.Max(0.25f, pincerBarrageInterval * 0.55f);
            pincerBarrageCharging = false;
            HidePincerTelegraph();
        }
        else if (currentState == AnomalyState.SignalJam)
        {
            signalJamTimer = Mathf.Max(0.25f, signalJamInterval * 0.52f);
            signalJamCharging = false;
            HideSignalJamTelegraph();
        }
        else if (currentState == AnomalyState.OrbitBarrage)
        {
            orbitBarrageTimer = Mathf.Max(0.25f, orbitBarrageInterval * 0.5f);
            orbitBarrageCharging = false;
            orbitBarrageDirectionSign = Random.value < 0.5f ? -1 : 1;
            HideOrbitBarrageTelegraph();
        }
        else if (currentState == AnomalyState.ReplayPredator)
        {
            replayPredatorTimer = Mathf.Max(0.25f, replayPredatorInterval * 0.45f);
        }
        else if (currentState == AnomalyState.ChecksumLattice)
        {
            checksumLatticeTimer = Mathf.Max(0.25f, checksumLatticeInterval * 0.35f);
        }
        else if (currentState == AnomalyState.InputDesync)
        {
            inputDesyncTimer = Mathf.Max(0.15f, inputDesyncInterval * 0.45f);
        }
        else if (currentState == AnomalyState.MapRecompile)
        {
            mapRecompileTimer = Mathf.Max(0.35f, mapRecompileInterval * 0.35f);
            mapRecompileCharging = false;
            HideMapRecompileTelegraph();
        }
        else if (currentState == AnomalyState.SignalPossession)
        {
            signalPossessionTimer = Mathf.Max(0.35f, signalPossessionInterval * 0.35f);
        }
        else if (currentState == AnomalyState.PhaseContract)
        {
            phaseContractTimer = Mathf.Max(0.4f, phaseContractInterval * 0.35f);
            phaseContractActive = false;
            HidePhaseContractVisual();
        }

        levelThreeStateController?.Configure(this, player, gameManager);
        levelThreeStateController?.EnterState(currentState);

        if (currentPattern == BehaviorPattern.ErraticBurst)
        {
            erraticRefreshTimer = 0f;
            RefreshErraticTarget();
        }

        if (currentPattern == BehaviorPattern.CutoffFlank)
        {
            flankRetargetTimer = 0f;
            flankSide = Random.value < 0.5f ? -1f : 1f;
        }
    }

    private void HandleStateTransition(AnomalyState previous, AnomalyState next)
    {
        if (levelThreeStateController != null && levelThreeStateController.IsActive && previous != next)
        {
            levelThreeStateController?.ExitState();
        }

        if (previous == AnomalyState.PhaseBlink && next != AnomalyState.PhaseBlink)
        {
            phaseBlinkCharging = false;
            HidePhaseBlinkTelegraph();
        }
        if (previous == AnomalyState.PincerBarrage && next != AnomalyState.PincerBarrage)
        {
            pincerBarrageCharging = false;
            HidePincerTelegraph();
        }
        if (previous == AnomalyState.SignalJam && next != AnomalyState.SignalJam)
        {
            signalJamCharging = false;
            HideSignalJamTelegraph();
        }
        if (previous == AnomalyState.OrbitBarrage && next != AnomalyState.OrbitBarrage)
        {
            orbitBarrageCharging = false;
            HideOrbitBarrageTelegraph();
        }
        if (previous == AnomalyState.MapRecompile && next != AnomalyState.MapRecompile)
        {
            mapRecompileCharging = false;
            HideMapRecompileTelegraph();
        }
        if (previous == AnomalyState.PhaseContract && next != AnomalyState.PhaseContract)
        {
            phaseContractActive = false;
            HidePhaseContractVisual();
        }
        if (previous == AnomalyState.SignalPossession && next != AnomalyState.SignalPossession)
        {
            if (activeSignalPossessionLure != null)
            {
                Destroy(activeSignalPossessionLure.gameObject);
                activeSignalPossessionLure = null;
            }
        }

        if (previous != AnomalyState.Split && next == AnomalyState.Split)
        {
            BeginSplitState();
            return;
        }

        if (previous == AnomalyState.Split && next != AnomalyState.Split)
        {
            BeginSplitMerge();
        }
    }

    private AnomalyState PickWeightedState(bool forceDifferent)
    {
        List<StateWeight> fullOptions = new List<StateWeight>
        {
            new StateWeight(AnomalyState.DirectChase, directChaseWeight),
            new StateWeight(AnomalyState.PredictiveIntercept, predictiveInterceptWeight),
            new StateWeight(AnomalyState.CutoffFlank, cutoffFlankWeight),
            new StateWeight(AnomalyState.ErraticBurst, erraticBurstWeight)
        };

        if (enableAdvancedStates)
        {
            fullOptions.Add(new StateWeight(AnomalyState.Split, splitWeight));
            fullOptions.Add(new StateWeight(AnomalyState.ExpansionShoot, expansionShootWeight));
            fullOptions.Add(new StateWeight(AnomalyState.SpeedSurge, speedSurgeWeight));
            fullOptions.Add(new StateWeight(AnomalyState.WeaveHunter, weaveHunterWeight));
            fullOptions.Add(new StateWeight(AnomalyState.Destroyer, destroyerWeight));
            fullOptions.Add(new StateWeight(AnomalyState.PhaseBlink, phaseBlinkWeight));
            fullOptions.Add(new StateWeight(AnomalyState.PincerBarrage, pincerBarrageWeight));
            fullOptions.Add(new StateWeight(AnomalyState.SignalJam, signalJamWeight));
            fullOptions.Add(new StateWeight(AnomalyState.OrbitBarrage, orbitBarrageWeight));
            fullOptions.Add(new StateWeight(AnomalyState.ReplayPredator, replayPredatorWeight));
            fullOptions.Add(new StateWeight(AnomalyState.ChecksumLattice, checksumLatticeWeight));
            fullOptions.Add(new StateWeight(AnomalyState.InputDesync, inputDesyncWeight));
            fullOptions.Add(new StateWeight(AnomalyState.MapRecompile, mapRecompileWeight));
            fullOptions.Add(new StateWeight(AnomalyState.SignalPossession, signalPossessionWeight));
            fullOptions.Add(new StateWeight(AnomalyState.PhaseContract, phaseContractWeight));
            fullOptions.Add(new StateWeight(AnomalyState.AdaptiveCountermeasure, adaptiveCountermeasureWeight));
            fullOptions.Add(new StateWeight(AnomalyState.SignalTether, signalTetherWeight));
            fullOptions.Add(new StateWeight(AnomalyState.BlindspotProtocol, blindspotProtocolWeight));
        }

        ApplyProgressionFilter(fullOptions);
        ApplyLevelTwoPriority(fullOptions);
        ApplyLevelThreePriority(fullOptions);

        List<StateWeight> filtered = new List<StateWeight>(fullOptions);
        ApplyPacingFilter(filtered);

        List<StateWeight> options = filtered.Count > 0 ? filtered : new List<StateWeight>(fullOptions);

        if (forceDifferent)
        {
            List<StateWeight> forceDifferentOptions = new List<StateWeight>(options);
            forceDifferentOptions.RemoveAll(o => o.state == currentState);
            if (forceDifferentOptions.Count > 0)
            {
                options = forceDifferentOptions;
            }
        }

        if (options.Count == 0)
        {
            options.Add(new StateWeight(GetPhaseFallbackState(), 1f));
        }

        float totalWeight = 0f;
        for (int i = 0; i < options.Count; i++)
        {
            totalWeight += Mathf.Max(0f, options[i].weight);
        }

        if (totalWeight <= 0.0001f)
        {
            return GetPhaseFallbackState();
        }

        float roll = Random.Range(0f, totalWeight);
        float cursor = 0f;
        for (int i = 0; i < options.Count; i++)
        {
            float w = Mathf.Max(0f, options[i].weight);
            cursor += w;
            if (roll <= cursor)
            {
                return options[i].state;
            }
        }

        return options[options.Count - 1].state;
    }

    private void ApplyPacingFilter(List<StateWeight> options)
    {
        if (CanUseLevelThreeStates())
        {
            ApplyLevelThreePacingFilter(options);
            return;
        }

        if (CanUseLevelTwoStates())
        {
            ApplyLevelTwoPacingFilter(options);
            return;
        }

        switch (pacingPhase)
        {
            case PacingPhase.SustainPeak:
                options.RemoveAll(o => !IsMajorState(o.state));
                break;
            case PacingPhase.BuildUp:
            case PacingPhase.PeakFade:
                options.RemoveAll(o => IsMajorState(o.state));
                break;
            case PacingPhase.Relax:
                options.RemoveAll(o => !IsCalmMinorState(o.state));
                break;
        }
    }

    private static bool IsMajorState(AnomalyState state)
    {
        return state == AnomalyState.Split ||
               state == AnomalyState.ExpansionShoot ||
               state == AnomalyState.Destroyer ||
               state == AnomalyState.SpeedSurge ||
               state == AnomalyState.PhaseBlink ||
               state == AnomalyState.PincerBarrage ||
               state == AnomalyState.SignalJam ||
               state == AnomalyState.OrbitBarrage ||
               state == AnomalyState.ReplayPredator ||
               state == AnomalyState.ChecksumLattice ||
               state == AnomalyState.InputDesync ||
               state == AnomalyState.MapRecompile ||
               state == AnomalyState.SignalPossession ||
               state == AnomalyState.PhaseContract ||
               IsLevelThreeState(state);
    }

    private static bool IsCalmMinorState(AnomalyState state)
    {
        return state == AnomalyState.DirectChase ||
               state == AnomalyState.PredictiveIntercept ||
               state == AnomalyState.CutoffFlank;
    }

    private void ApplyProgressionFilter(List<StateWeight> options)
    {
        if (options == null || options.Count == 0)
        {
            return;
        }

        if (!CanUseSpecialStates())
        {
            options.RemoveAll(o => IsPre60SpecialState(o.state));
            return;
        }

        if (!CanUseLevelTwoStates())
        {
            options.RemoveAll(o => IsLevelTwoState(o.state));
        }

        if (!CanUseLevelThreeStates())
        {
            options.RemoveAll(o => IsLevelThreeState(o.state));
        }
    }

    private void ApplyLevelThreePacingFilter(List<StateWeight> options)
    {
        switch (pacingPhase)
        {
            case PacingPhase.SustainPeak:
                options.RemoveAll(o => !IsLevelThreeState(o.state) && !IsLevelTwoState(o.state));
                break;
            case PacingPhase.BuildUp:
            case PacingPhase.PeakFade:
                options.RemoveAll(o => !IsLevelThreeState(o.state) && !IsLevelTwoState(o.state) && !IsBaseSpecialState(o.state));
                break;
            case PacingPhase.Relax:
                options.RemoveAll(o => IsLevelThreeState(o.state));
                if (options.Count == 0)
                {
                    options.Add(new StateWeight(AnomalyState.DirectChase, directChaseWeight));
                    options.Add(new StateWeight(AnomalyState.PredictiveIntercept, predictiveInterceptWeight));
                }
                break;
        }
    }

    private void ApplyLevelTwoPacingFilter(List<StateWeight> options)
    {
        switch (pacingPhase)
        {
            case PacingPhase.SustainPeak:
                options.RemoveAll(o => !IsLevelTwoState(o.state) && !IsBaseSpecialState(o.state));
                break;
            case PacingPhase.BuildUp:
            case PacingPhase.PeakFade:
                options.RemoveAll(o => !IsLevelTwoState(o.state) && !IsBaseSpecialState(o.state) && !IsCalmMinorState(o.state));
                break;
            case PacingPhase.Relax:
                options.RemoveAll(o => !IsCalmMinorState(o.state) && !IsBaseSpecialState(o.state) && !IsLevelTwoState(o.state));
                if (options.Count == 0)
                {
                    options.Add(new StateWeight(AnomalyState.DirectChase, directChaseWeight));
                    options.Add(new StateWeight(AnomalyState.PredictiveIntercept, predictiveInterceptWeight));
                }
                break;
        }
    }

    private bool CanUseSpecialStates()
    {
        return gameManager != null && gameManager.AreBossSpecialStatesUnlocked;
    }

    private bool CanUseLevelTwoStates()
    {
        return gameManager != null && gameManager.AreBossLevelTwoStatesUnlocked && !gameManager.IsContainmentPulsePressureActive;
    }

    private bool CanUseLevelThreeStates()
    {
        return gameManager != null && gameManager.AreBossLevelThreeStatesUnlocked && !gameManager.IsContainmentPulsePressureActive;
    }

    private bool AreSpecialStatesSuppressedForBreach()
    {
        return gameManager != null && gameManager.IsBreachSensitiveSuppressionActive;
    }

    private static bool IsPre60SpecialState(AnomalyState state)
    {
        return state == AnomalyState.Split ||
               state == AnomalyState.ExpansionShoot ||
               state == AnomalyState.SpeedSurge ||
               state == AnomalyState.WeaveHunter ||
               state == AnomalyState.Destroyer ||
               state == AnomalyState.PhaseBlink ||
               state == AnomalyState.PincerBarrage ||
               state == AnomalyState.SignalJam ||
               state == AnomalyState.OrbitBarrage ||
               state == AnomalyState.ReplayPredator ||
               state == AnomalyState.ChecksumLattice ||
               state == AnomalyState.InputDesync ||
               state == AnomalyState.MapRecompile ||
               state == AnomalyState.SignalPossession ||
               state == AnomalyState.PhaseContract ||
               IsLevelThreeState(state);
    }

    private static bool IsLevelTwoState(AnomalyState state)
    {
        return state == AnomalyState.PhaseBlink ||
               state == AnomalyState.PincerBarrage ||
               state == AnomalyState.SignalJam ||
               state == AnomalyState.OrbitBarrage ||
               state == AnomalyState.ReplayPredator ||
               state == AnomalyState.ChecksumLattice ||
               state == AnomalyState.InputDesync ||
               state == AnomalyState.MapRecompile ||
               state == AnomalyState.SignalPossession ||
               state == AnomalyState.PhaseContract;
    }

    private static bool IsLevelThreeState(AnomalyState state)
    {
        return state == AnomalyState.AdaptiveCountermeasure ||
               state == AnomalyState.SignalTether ||
               state == AnomalyState.BlindspotProtocol;
    }

    private static bool IsBaseSpecialState(AnomalyState state)
    {
        return state == AnomalyState.Split ||
               state == AnomalyState.ExpansionShoot ||
               state == AnomalyState.SpeedSurge ||
               state == AnomalyState.WeaveHunter ||
               state == AnomalyState.Destroyer;
    }

    private void ApplyLevelTwoPriority(List<StateWeight> options)
    {
        if (!CanUseLevelTwoStates() || CanUseLevelThreeStates() || options == null)
        {
            return;
        }

        for (int i = 0; i < options.Count; i++)
        {
            StateWeight option = options[i];
            if (IsLevelTwoState(option.state))
            {
                option.weight *= Mathf.Max(1f, levelTwoStatePriorityMultiplier);
            }
            else if (IsBaseSpecialState(option.state))
            {
                option.weight *= Mathf.Max(0.1f, levelTwoBaseSpecialPriorityMultiplier);
            }
            else
            {
                option.weight *= Mathf.Max(0.01f, levelTwoMinorStatePriorityMultiplier);
            }

            options[i] = option;
        }
    }

    private void ApplyLevelThreePriority(List<StateWeight> options)
    {
        if (!CanUseLevelThreeStates() || options == null)
        {
            return;
        }

        for (int i = 0; i < options.Count; i++)
        {
            StateWeight option = options[i];
            if (IsLevelThreeState(option.state))
            {
                option.weight *= Mathf.Max(1f, levelThreeStatePriorityMultiplier);
            }
            else if (IsLevelTwoState(option.state))
            {
                option.weight *= Mathf.Max(0.1f, levelThreeLevelTwoPriorityMultiplier);
            }
            else if (IsBaseSpecialState(option.state))
            {
                option.weight *= Mathf.Max(0.05f, levelThreeBaseSpecialPriorityMultiplier);
            }
            else
            {
                option.weight *= Mathf.Max(0.01f, levelThreeMinorStatePriorityMultiplier);
            }

            options[i] = option;
        }
    }

    public bool IsMapEventSuppressed()
    {
        return currentState == AnomalyState.Destroyer || destroyerPendingRespawnIds.Count > 0;
    }

    public void ApplyExternalSpeedModifier(float multiplier, float duration)
    {
        if (multiplier <= 1f || duration <= 0f)
        {
            return;
        }

        externalSpeedMultiplier = Mathf.Max(externalSpeedMultiplier, multiplier);
        externalSpeedTimer = Mathf.Max(externalSpeedTimer, duration);
        TriggerStatePulse();
    }

    public void GuideTowardBreach(Vector2 breachTarget, float duration)
    {
        breachLureTarget = breachTarget;
        breachLureTimer = Mathf.Max(breachLureTimer, Mathf.Max(0.1f, duration));
        CancelSplitMergeForBreach();
    }

    public void AbsorbIntoBreach(Vector2 breachPosition)
    {
        breachAbsorbed = true;
        breachLureTimer = 0f;
        rb.linearVelocity = Vector2.zero;
        AbsorbSplitCloneIntoBreach(breachPosition);

        if (ownCollider != null)
        {
            ownCollider.enabled = false;
        }

        if (ownRenderer != null)
        {
            ownRenderer.enabled = false;
        }

        transform.position = breachPosition;
        HideExpansionShootTelegraphVisual();
        HideSplitMergeTelegraphVisual();
    }

    public void ReappearFromBreach(Vector2 position)
    {
        transform.position = position;
        rb.position = position;
        rb.linearVelocity = Vector2.zero;
        breachAbsorbed = false;
        breachLureTimer = 0f;
        breachLureTarget = position;
        DestroySplitCloneImmediate();

        if (ownCollider != null)
        {
            ownCollider.enabled = true;
        }

        if (ownRenderer != null)
        {
            ownRenderer.enabled = true;
            ownRenderer.color = baseColor;
        }

        transform.localScale = baseScale;
        TriggerStatePulse();
        BuildNavigationGrid();
        pathWorld.Clear();
        pathIndex = 0;
    }

    private void TriggerStatePulse()
    {
        if (!statePulseFxEnabled)
        {
            return;
        }

        statePulseTimer = Mathf.Max(0.04f, statePulseDuration);
    }

    private void SpawnStateTransitionBurst(AnomalyState newState, bool stateChanged)
    {
        if (!stateTransitionBurstEnabled || !stateChanged)
        {
            return;
        }

        if (stateTransitionBurstCooldownTimer > 0f)
        {
            return;
        }

        stateTransitionBurstCooldownTimer = Mathf.Max(0.02f, stateTransitionBurstCooldown);
        Color burstColor = GetStateColor(newState);
        int rays = Mathf.Max(4, stateTransitionBurstRayCount);
        float duration = Mathf.Max(0.08f, stateTransitionBurstDuration);
        float radius = Mathf.Max(0.25f, stateTransitionBurstRadius);
        Vector3 pos = transform.position;

        GameObject ring = new GameObject("AnomalyStateBurstRing");
        ring.transform.position = pos;
        SpriteRenderer ringRenderer = ring.AddComponent<SpriteRenderer>();
        ringRenderer.sprite = CircleSpriteProvider.Get();
        ringRenderer.sortingOrder = 15;
        ringRenderer.color = new Color(burstColor.r, burstColor.g, burstColor.b, 0.82f);
        ring.transform.localScale = Vector3.one * 0.25f;
        ring.AddComponent<AnomalyStateBurstFx>().Configure(ringRenderer, radius, duration, burstColor);
        Destroy(ring, duration + 0.12f);

        for (int i = 0; i < rays; i++)
        {
            float angle = ((Mathf.PI * 2f) * i) / rays + Random.Range(-0.08f, 0.08f);
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            GameObject ray = new GameObject($"AnomalyStateRay_{i}");
            ray.transform.position = pos;
            SpriteRenderer sr = ray.AddComponent<SpriteRenderer>();
            sr.sprite = SquareSpriteProvider.Get();
            sr.sortingOrder = 15;
            sr.color = burstColor;
            ray.transform.localScale = new Vector3(0.24f, 0.07f, 1f);
            ray.AddComponent<AnomalyStateRayFx>().Configure(sr, dir, radius, duration);
            Destroy(ray, duration + 0.12f);
        }
    }

    private void SpawnParryImpactBurst()
    {
        Color burstColor = new Color(1f, 0.94f, 0.58f, 1f);
        int rays = Mathf.Max(6, stateTransitionBurstRayCount);
        float duration = Mathf.Max(0.08f, parryBurstDuration);
        float radius = Mathf.Max(0.25f, parryBurstRadius);
        Vector3 pos = transform.position;

        GameObject ring = new GameObject("AnomalyParryImpactRing");
        ring.transform.position = pos;
        SpriteRenderer ringRenderer = ring.AddComponent<SpriteRenderer>();
        ringRenderer.sprite = CircleSpriteProvider.Get();
        ringRenderer.sortingOrder = 16;
        ringRenderer.color = new Color(burstColor.r, burstColor.g, burstColor.b, 0.9f);
        ring.transform.localScale = Vector3.one * 0.22f;
        ring.AddComponent<AnomalyStateBurstFx>().Configure(ringRenderer, radius, duration, burstColor);
        Destroy(ring, duration + 0.12f);

        for (int i = 0; i < rays; i++)
        {
            float angle = ((Mathf.PI * 2f) * i) / rays + Random.Range(-0.12f, 0.12f);
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            GameObject ray = new GameObject($"AnomalyParryImpactRay_{i}");
            ray.transform.position = pos;
            SpriteRenderer sr = ray.AddComponent<SpriteRenderer>();
            sr.sprite = SquareSpriteProvider.Get();
            sr.sortingOrder = 16;
            sr.color = burstColor;
            ray.transform.localScale = new Vector3(0.25f, 0.08f, 1f);
            ray.AddComponent<AnomalyStateRayFx>().Configure(sr, dir, radius, duration);
            Destroy(ray, duration + 0.12f);
        }
    }

    private static Color GetStateColor(AnomalyState state)
    {
        switch (state)
        {
            case AnomalyState.Split:
                return new Color(1f, 0.48f, 0.66f, 1f);
            case AnomalyState.ExpansionShoot:
                return new Color(1f, 0.38f, 0.52f, 1f);
            case AnomalyState.SpeedSurge:
                return new Color(1f, 0.76f, 0.46f, 1f);
            case AnomalyState.Destroyer:
                return new Color(1f, 0.50f, 0.42f, 1f);
            case AnomalyState.WeaveHunter:
                return new Color(0.48f, 0.94f, 1f, 1f);
            case AnomalyState.PhaseBlink:
                return new Color(0.58f, 1f, 0.92f, 1f);
            case AnomalyState.PincerBarrage:
                return new Color(0.92f, 0.62f, 1f, 1f);
            case AnomalyState.SignalJam:
                return new Color(1f, 0.78f, 0.42f, 1f);
            case AnomalyState.OrbitBarrage:
                return new Color(0.58f, 0.82f, 1f, 1f);
            case AnomalyState.ReplayPredator:
                return new Color(1f, 0.42f, 0.76f, 1f);
            case AnomalyState.ChecksumLattice:
                return new Color(0.44f, 1f, 0.88f, 1f);
            case AnomalyState.InputDesync:
                return new Color(0.66f, 0.74f, 1f, 1f);
            case AnomalyState.MapRecompile:
                return new Color(0.92f, 0.62f, 1f, 1f);
            case AnomalyState.SignalPossession:
                return new Color(0.76f, 1f, 0.54f, 1f);
            case AnomalyState.PhaseContract:
                return new Color(1f, 0.84f, 0.46f, 1f);
            case AnomalyState.AdaptiveCountermeasure:
                return new Color(1f, 0.35f, 0.72f, 1f);
            case AnomalyState.SignalTether:
                return new Color(0.32f, 1f, 0.78f, 1f);
            case AnomalyState.BlindspotProtocol:
                return new Color(1f, 0.76f, 0.28f, 1f);
            case AnomalyState.ErraticBurst:
                return new Color(0.74f, 0.76f, 1f, 1f);
            case AnomalyState.CutoffFlank:
                return new Color(0.65f, 0.88f, 1f, 1f);
            case AnomalyState.PredictiveIntercept:
                return new Color(0.85f, 0.68f, 1f, 1f);
            default:
                return new Color(0.86f, 0.92f, 1f, 1f);
        }
    }

    private void UpdateStatePulseVisual()
    {
        if (ownRenderer == null)
        {
            return;
        }

        if (levelTwoAwakeningTimer > 0f)
        {
            levelTwoAwakeningTimer -= Time.deltaTime;
        }
        if (levelThreeAwakeningTimer > 0f)
        {
            levelThreeAwakeningTimer -= Time.deltaTime;
        }

        Color visualBaseColor = GetCurrentVisualBaseColor();
        Vector3 visualBaseScale = GetCurrentVisualBaseScale();

        if (!statePulseFxEnabled || statePulseTimer <= 0f)
        {
            ownRenderer.color = visualBaseColor;
            transform.localScale = visualBaseScale;
            return;
        }

        statePulseTimer -= Time.deltaTime;
        float normalized = 1f - Mathf.Clamp01(statePulseTimer / Mathf.Max(0.04f, statePulseDuration));
        float pulse = Mathf.Sin(normalized * Mathf.PI);
        float lighten = Mathf.Clamp01(statePulseLighten) * pulse;
        float scale = Mathf.Lerp(1f, Mathf.Max(1f, statePulseScaleBoost), pulse);

        Color boosted = Color.Lerp(visualBaseColor, Color.white, lighten);
        boosted.a = visualBaseColor.a;
        ownRenderer.color = boosted;
        transform.localScale = visualBaseScale * scale;
    }

    private Color GetCurrentVisualBaseColor()
    {
        if (levelThreeAwakened || levelThreeAwakeningTimer > 0f)
        {
            float levelThreeWake = Mathf.Clamp01(levelThreeAwakeningTimer / Mathf.Max(0.05f, levelThreeAwakeningDuration));
            float levelThreePulse = levelThreeAwakeningTimer > 0f ? Mathf.Sin((1f - levelThreeWake) * Mathf.PI * 8f) * 0.5f + 0.5f : 0f;
            float levelThreeTint = Mathf.Clamp01(levelThreePassiveTint + levelThreeWake * 0.3f + levelThreePulse * 0.2f);
            Color levelThreeColor = Color.Lerp(baseColor, levelThreeAwakenedColor, levelThreeTint);
            levelThreeColor.a = baseColor.a;
            return levelThreeColor;
        }

        if (!levelTwoAwakened && levelTwoAwakeningTimer <= 0f)
        {
            return baseColor;
        }

        float wake = Mathf.Clamp01(levelTwoAwakeningTimer / Mathf.Max(0.05f, levelTwoAwakeningDuration));
        float pulse = levelTwoAwakeningTimer > 0f ? Mathf.Sin((1f - wake) * Mathf.PI * 6f) * 0.5f + 0.5f : 0f;
        float tint = Mathf.Clamp01(levelTwoPassiveTint + wake * 0.26f + pulse * 0.18f);
        Color awakened = Color.Lerp(baseColor, levelTwoAwakenedColor, tint);
        awakened.a = baseColor.a;
        return awakened;
    }

    private Vector3 GetCurrentVisualBaseScale()
    {
        if (levelThreeAwakeningTimer > 0f)
        {
            float levelThreeNormalized = 1f - Mathf.Clamp01(levelThreeAwakeningTimer / Mathf.Max(0.05f, levelThreeAwakeningDuration));
            float levelThreePulse = Mathf.Sin(levelThreeNormalized * Mathf.PI) * Mathf.Max(0f, levelThreeAwakeningScaleBoost - 1f);
            return baseScale * (1f + levelThreePulse);
        }

        if (levelTwoAwakeningTimer <= 0f)
        {
            return baseScale;
        }

        float normalized = 1f - Mathf.Clamp01(levelTwoAwakeningTimer / Mathf.Max(0.05f, levelTwoAwakeningDuration));
        float pulse = Mathf.Sin(normalized * Mathf.PI) * Mathf.Max(0f, levelTwoAwakeningScaleBoost - 1f);
        return baseScale * (1f + pulse);
    }

    public void TriggerLevelTwoAwakeningFx()
    {
        levelTwoAwakened = true;
        levelTwoAwakeningTimer = Mathf.Max(levelTwoAwakeningTimer, Mathf.Max(0.1f, levelTwoAwakeningDuration));
        TriggerStatePulse();
        SpawnLevelTwoAwakeningBurst();
    }

    public void TriggerLevelThreeAwakeningFx()
    {
        levelTwoAwakened = true;
        levelThreeAwakened = true;
        levelThreeAwakeningTimer = Mathf.Max(levelThreeAwakeningTimer, Mathf.Max(0.1f, levelThreeAwakeningDuration));
        TriggerStatePulse();
        SpawnLevelThreeAwakeningBurst();
    }

    public void ForceLevelTwoStateForDebug()
    {
        if (breachAbsorbed)
        {
            return;
        }

        AnomalyState previous = currentState;
        AnomalyState[] options =
        {
            AnomalyState.PhaseBlink,
            AnomalyState.PincerBarrage,
            AnomalyState.SignalJam,
            AnomalyState.OrbitBarrage,
            AnomalyState.ReplayPredator,
            AnomalyState.ChecksumLattice,
            AnomalyState.InputDesync,
            AnomalyState.MapRecompile,
            AnomalyState.SignalPossession,
            AnomalyState.PhaseContract
        };

        currentState = options[Random.Range(0, options.Length)];
        if (currentState == previous)
        {
            currentState = options[(System.Array.IndexOf(options, currentState) + 1) % options.Length];
        }

        currentPattern = ResolvePatternForState(currentState);
        stateTimer = 0f;
        currentStateDuration = GetRandomDurationForState(currentState);
        HandleStateTransition(previous, currentState);
        TriggerStatePulse();
        SpawnStateTransitionBurst(currentState, previous != currentState);
        OnStateEntered();
        RegisterStateForPacing(currentState);
        GlitchAudioManager.PlayEnemyState(currentState, transform.position);
    }

    public void ForceLevelThreeStateForDebug()
    {
        if (breachAbsorbed)
        {
            return;
        }

        AnomalyState previous = currentState;
        AnomalyState[] options =
        {
            AnomalyState.AdaptiveCountermeasure,
            AnomalyState.SignalTether,
            AnomalyState.BlindspotProtocol
        };

        int currentIndex = System.Array.IndexOf(options, currentState);
        currentState = currentIndex >= 0
            ? options[(currentIndex + 1) % options.Length]
            : options[0];
        currentPattern = ResolvePatternForState(currentState);
        stateTimer = 0f;
        currentStateDuration = GetRandomDurationForState(currentState);
        HandleStateTransition(previous, currentState);
        TriggerStatePulse();
        SpawnStateTransitionBurst(currentState, previous != currentState);
        OnStateEntered();
        RegisterStateForPacing(currentState);
        GlitchAudioManager.PlayEnemyState(currentState, transform.position);
    }

    public void ForceTopologyProjectileStateForDebug()
    {
        if (breachAbsorbed)
        {
            return;
        }

        AnomalyState previous = currentState;
        AnomalyState[] options =
        {
            AnomalyState.ExpansionShoot,
            AnomalyState.PincerBarrage,
            AnomalyState.OrbitBarrage,
            AnomalyState.SignalPossession
        };
        int currentIndex = System.Array.IndexOf(options, currentState);
        currentState = currentIndex >= 0
            ? options[(currentIndex + 1) % options.Length]
            : options[0];
        currentPattern = ResolvePatternForState(currentState);
        stateTimer = 0f;
        currentStateDuration = Mathf.Max(GetRandomDurationForState(currentState), 14f);
        HandleStateTransition(previous, currentState);
        TriggerStatePulse();
        SpawnStateTransitionBurst(currentState, previous != currentState);
        OnStateEntered();
        RegisterStateForPacing(currentState);
        GlitchAudioManager.PlayEnemyState(currentState, transform.position);
    }

    private void SpawnLevelTwoAwakeningBurst()
    {
        Vector3 pos = transform.position;
        Color burstColor = levelTwoAwakeningBurstColor;

        GameObject ring = new GameObject("LevelTwoAwakeningRing");
        ring.transform.position = pos;
        SpriteRenderer ringRenderer = ring.AddComponent<SpriteRenderer>();
        ringRenderer.sprite = CircleSpriteProvider.Get();
        ringRenderer.sortingOrder = 18;
        ringRenderer.color = burstColor;
        ring.transform.localScale = Vector3.one * 0.22f;
        ring.AddComponent<AnomalyStateBurstFx>().Configure(ringRenderer, 2.35f, 0.45f, burstColor);
        Destroy(ring, 0.62f);

        for (int i = 0; i < 16; i++)
        {
            float angle = ((Mathf.PI * 2f) * i) / 16f + Random.Range(-0.08f, 0.08f);
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            GameObject ray = new GameObject($"LevelTwoAwakeningRay_{i}");
            ray.transform.position = pos;
            SpriteRenderer sr = ray.AddComponent<SpriteRenderer>();
            sr.sprite = SquareSpriteProvider.Get();
            sr.sortingOrder = 18;
            sr.color = i % 2 == 0 ? burstColor : levelTwoAwakenedColor;
            ray.transform.localScale = new Vector3(0.28f, 0.07f, 1f);
            ray.AddComponent<AnomalyStateRayFx>().Configure(sr, dir, 2.8f, 0.38f);
            Destroy(ray, 0.55f);
        }
    }

    private void SpawnLevelThreeAwakeningBurst()
    {
        Vector3 position = transform.position;
        for (int layer = 0; layer < 3; layer++)
        {
            GameObject ring = new GameObject($"LevelThreeAwakeningRing_{layer}");
            ring.transform.position = position;
            SpriteRenderer ringRenderer = ring.AddComponent<SpriteRenderer>();
            ringRenderer.sprite = CircleSpriteProvider.Get();
            ringRenderer.sortingOrder = 20 + layer;
            ringRenderer.color = layer % 2 == 0 ? levelThreeAwakeningBurstColor : levelThreeAwakenedColor;
            ring.transform.localScale = Vector3.one * (0.18f + layer * 0.15f);
            ring.AddComponent<AnomalyStateBurstFx>().Configure(
                ringRenderer,
                2.8f + layer * 0.7f,
                0.48f + layer * 0.08f,
                ringRenderer.color);
            Destroy(ring, 0.82f);
        }

        for (int i = 0; i < 24; i++)
        {
            float angle = ((Mathf.PI * 2f) * i) / 24f + Random.Range(-0.06f, 0.06f);
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            GameObject ray = new GameObject($"LevelThreeAwakeningRay_{i}");
            ray.transform.position = position;
            SpriteRenderer renderer = ray.AddComponent<SpriteRenderer>();
            renderer.sprite = SquareSpriteProvider.Get();
            renderer.sortingOrder = 22;
            renderer.color = i % 3 == 0 ? levelThreeAwakenedColor : levelThreeAwakeningBurstColor;
            ray.transform.localScale = new Vector3(i % 2 == 0 ? 0.42f : 0.24f, 0.065f, 1f);
            ray.AddComponent<AnomalyStateRayFx>().Configure(renderer, direction, 3.4f + (i % 3) * 0.45f, 0.5f);
            Destroy(ray, 0.72f);
        }
    }

    private void UpdateExternalSpeedEffects()
    {
        if (breachLureTimer > 0f)
        {
            breachLureTimer -= Time.deltaTime;
            if (breachLureTimer < 0f)
            {
                breachLureTimer = 0f;
            }
        }

        if (externalSpeedTimer <= 0f)
        {
            externalSpeedTimer = 0f;
            externalSpeedMultiplier = 1f;
            return;
        }

        externalSpeedTimer -= Time.deltaTime;
        if (externalSpeedTimer <= 0f)
        {
            externalSpeedTimer = 0f;
            externalSpeedMultiplier = 1f;
        }
    }

    private AnomalyState GetPhaseFallbackState()
    {
        switch (pacingPhase)
        {
            case PacingPhase.SustainPeak:
                return enableAdvancedStates && CanUseSpecialStates() ? AnomalyState.SpeedSurge : AnomalyState.DirectChase;
            case PacingPhase.Relax:
                return AnomalyState.DirectChase;
            case PacingPhase.PeakFade:
            case PacingPhase.BuildUp:
            default:
                return AnomalyState.PredictiveIntercept;
        }
    }

    private static BehaviorPattern ResolvePatternForState(AnomalyState state)
    {
        switch (state)
        {
            case AnomalyState.DirectChase:
                return BehaviorPattern.DirectChase;
            case AnomalyState.PredictiveIntercept:
                return BehaviorPattern.PredictiveIntercept;
            case AnomalyState.CutoffFlank:
                return BehaviorPattern.CutoffFlank;
            case AnomalyState.ErraticBurst:
                return BehaviorPattern.ErraticBurst;
            case AnomalyState.Split:
                return BehaviorPattern.CutoffFlank;
            case AnomalyState.ExpansionShoot:
                return BehaviorPattern.PredictiveIntercept;
            case AnomalyState.SpeedSurge:
                return BehaviorPattern.DirectChase;
            case AnomalyState.WeaveHunter:
                return BehaviorPattern.DirectChase;
            case AnomalyState.Destroyer:
                return BehaviorPattern.DirectChase;
            case AnomalyState.PhaseBlink:
                return BehaviorPattern.PredictiveIntercept;
            case AnomalyState.PincerBarrage:
                return BehaviorPattern.CutoffFlank;
            case AnomalyState.SignalJam:
                return BehaviorPattern.PredictiveIntercept;
            case AnomalyState.OrbitBarrage:
                return BehaviorPattern.ErraticBurst;
            case AnomalyState.ReplayPredator:
                return BehaviorPattern.CutoffFlank;
            case AnomalyState.ChecksumLattice:
                return BehaviorPattern.PredictiveIntercept;
            case AnomalyState.InputDesync:
                return BehaviorPattern.ErraticBurst;
            case AnomalyState.MapRecompile:
                return BehaviorPattern.CutoffFlank;
            case AnomalyState.SignalPossession:
                return BehaviorPattern.PredictiveIntercept;
            case AnomalyState.PhaseContract:
                return BehaviorPattern.DirectChase;
            case AnomalyState.AdaptiveCountermeasure:
                return BehaviorPattern.PredictiveIntercept;
            case AnomalyState.SignalTether:
                return BehaviorPattern.DirectChase;
            case AnomalyState.BlindspotProtocol:
                return BehaviorPattern.PredictiveIntercept;
            default:
                return BehaviorPattern.DirectChase;
        }
    }

    private void UpdatePatternInternals()
    {
        if (currentPattern == BehaviorPattern.ErraticBurst)
        {
            erraticRefreshTimer += Time.deltaTime;
            if (erraticRefreshTimer >= erraticDirectionRefresh)
            {
                erraticRefreshTimer = 0f;
                RefreshErraticTarget();
            }
        }

        if (currentPattern == BehaviorPattern.CutoffFlank)
        {
            flankRetargetTimer += Time.deltaTime;
            if (flankRetargetTimer >= flankRetargetInterval)
            {
                flankRetargetTimer = 0f;
                flankSide *= -1f;
            }
        }
    }

    private void UpdateStateAbilities()
    {
        UpdateSplitAbility();
        UpdateDestroyerAbility();
        UpdateWeaveHunterAbility();
        UpdatePhaseBlinkAbility();
        UpdatePincerBarrageAbility();
        UpdateSignalJamAbility();
        UpdateOrbitBarrageAbility();
        UpdateReplayPredatorAbility();
        UpdateChecksumLatticeAbility();
        UpdateInputDesyncAbility();
        UpdateMapRecompileAbility();
        UpdateSignalPossessionAbility();
        UpdatePhaseContractAbility();

        if (currentState != AnomalyState.ExpansionShoot)
        {
            HideExpansionShootTelegraphVisual();
            return;
        }

        expansionShootTimer += Time.deltaTime;
        float intervalMultiplier = chaosDriveEnabled ? chaosExpansionIntervalMultiplier : 1f;
        float interval = Mathf.Max(0.12f, expansionShootInterval * Mathf.Max(0.1f, intervalMultiplier));
        float lead = Mathf.Clamp(expansionShootTelegraphLeadTime, 0.05f, interval);
        float remaining = interval - expansionShootTimer;
        if (remaining <= lead && remaining > 0f)
        {
            float progress = 1f - Mathf.Clamp01(remaining / lead);
            UpdateExpansionShootTelegraphVisual(progress);
        }
        else
        {
            HideExpansionShootTelegraphVisual();
        }

        if (expansionShootTimer < interval)
        {
            return;
        }

        expansionShootTimer = 0f;
        HideExpansionShootTelegraphVisual();
        FireExpansionShoot();
    }

    private void UpdateWeaveHunterAbility()
    {
        if (currentState != AnomalyState.WeaveHunter)
        {
            return;
        }

        weaveHunterTimer += Time.deltaTime;
        if (weaveHunterTimer >= Mathf.Max(0.12f, weaveHunterSwitchInterval))
        {
            weaveHunterTimer = 0f;
            weaveHunterSideSign *= -1f;
        }
    }

    private void UpdatePhaseBlinkAbility()
    {
        if (currentState != AnomalyState.PhaseBlink)
        {
            phaseBlinkCharging = false;
            HidePhaseBlinkTelegraph();
            return;
        }

        phaseBlinkTimer += Time.deltaTime;
        float interval = Mathf.Max(0.35f, phaseBlinkInterval);
        float lead = Mathf.Clamp(phaseBlinkTelegraphSeconds, 0.08f, interval * 0.85f);
        float remaining = interval - phaseBlinkTimer;

        if (!phaseBlinkCharging && remaining <= lead)
        {
            phaseBlinkCharging = true;
            phaseBlinkTarget = PickPhaseBlinkTarget();
            GlitchAudioManager.PlayEnemyPhaseBlinkCharge(transform.position);
        }

        if (phaseBlinkCharging && remaining > 0f)
        {
            float progress = 1f - Mathf.Clamp01(remaining / lead);
            UpdatePhaseBlinkTelegraph(progress);
        }

        if (phaseBlinkTimer < interval)
        {
            return;
        }

        phaseBlinkTimer = 0f;
        phaseBlinkCharging = false;
        HidePhaseBlinkTelegraph();
        ExecutePhaseBlink();
    }

    private void UpdatePincerBarrageAbility()
    {
        if (currentState != AnomalyState.PincerBarrage)
        {
            pincerBarrageCharging = false;
            HidePincerTelegraph();
            return;
        }

        pincerBarrageTimer += Time.deltaTime;
        float interval = Mathf.Max(0.45f, pincerBarrageInterval);
        float lead = Mathf.Clamp(pincerBarrageTelegraphSeconds, 0.08f, interval * 0.85f);
        float remaining = interval - pincerBarrageTimer;

        if (!pincerBarrageCharging && remaining <= lead)
        {
            pincerBarrageCharging = true;
            ComputePincerSpawnPoints(out pincerLeftSpawn, out pincerRightSpawn);
            GlitchAudioManager.PlayEnemyPincerCharge(transform.position);
        }

        if (pincerBarrageCharging && remaining > 0f)
        {
            float progress = 1f - Mathf.Clamp01(remaining / lead);
            UpdatePincerTelegraph(progress);
        }

        if (pincerBarrageTimer < interval)
        {
            return;
        }

        pincerBarrageTimer = 0f;
        pincerBarrageCharging = false;
        HidePincerTelegraph();
        FirePincerBarrage();
    }

    private void UpdateSignalJamAbility()
    {
        if (currentState != AnomalyState.SignalJam)
        {
            signalJamCharging = false;
            HideSignalJamTelegraph();
            return;
        }

        signalJamTimer += Time.deltaTime;
        float interval = Mathf.Max(0.45f, signalJamInterval);
        float lead = Mathf.Clamp(signalJamTelegraphSeconds, 0.1f, interval * 0.85f);
        float remaining = interval - signalJamTimer;

        if (!signalJamCharging && remaining <= lead)
        {
            signalJamCharging = true;
            signalJamCenter = PickSignalJamCenter();
            GlitchAudioManager.PlayEnemySignalJamCharge(transform.position);
        }

        if (signalJamCharging && remaining > 0f)
        {
            float progress = 1f - Mathf.Clamp01(remaining / lead);
            UpdateSignalJamTelegraph(progress);
        }

        if (signalJamTimer < interval)
        {
            return;
        }

        signalJamTimer = 0f;
        signalJamCharging = false;
        HideSignalJamTelegraph();
        FireSignalJam();
    }

    private void UpdateOrbitBarrageAbility()
    {
        if (currentState != AnomalyState.OrbitBarrage)
        {
            orbitBarrageCharging = false;
            HideOrbitBarrageTelegraph();
            return;
        }

        orbitBarrageTimer += Time.deltaTime;
        float interval = Mathf.Max(0.5f, orbitBarrageInterval);
        float lead = Mathf.Clamp(orbitBarrageTelegraphSeconds, 0.1f, interval * 0.85f);
        float remaining = interval - orbitBarrageTimer;

        if (!orbitBarrageCharging && remaining <= lead)
        {
            orbitBarrageCharging = true;
            orbitBarrageCenter = PickOrbitBarrageCenter();
            orbitBarrageDirectionSign = Random.value < 0.5f ? -1 : 1;
            GlitchAudioManager.PlayEnemyOrbitBarrageCharge(transform.position);
        }

        if (orbitBarrageCharging && remaining > 0f)
        {
            float progress = 1f - Mathf.Clamp01(remaining / lead);
            UpdateOrbitBarrageTelegraph(progress);
        }

        if (orbitBarrageTimer < interval)
        {
            return;
        }

        orbitBarrageTimer = 0f;
        orbitBarrageCharging = false;
        HideOrbitBarrageTelegraph();
        FireOrbitBarrage();
    }

    private void UpdateReplayPredatorAbility()
    {
        if (currentState != AnomalyState.ReplayPredator)
        {
            return;
        }

        replayPredatorTimer += Time.deltaTime;
        if (replayPredatorTimer < Mathf.Max(0.8f, replayPredatorInterval))
        {
            return;
        }

        replayPredatorTimer = 0f;
        SpawnReplayPredatorEchoes();
    }

    private void UpdateChecksumLatticeAbility()
    {
        if (currentState != AnomalyState.ChecksumLattice)
        {
            return;
        }

        if (checksumLatticeFx != null)
        {
            return;
        }

        checksumLatticeTimer += Time.deltaTime;
        if (checksumLatticeTimer < Mathf.Max(2.4f, checksumLatticeInterval))
        {
            return;
        }

        checksumLatticeTimer = 0f;
        SpawnChecksumLattice();
    }

    private void UpdateInputDesyncAbility()
    {
        if (currentState != AnomalyState.InputDesync)
        {
            return;
        }

        inputDesyncTimer += Time.deltaTime;
        if (inputDesyncTimer < Mathf.Max(0.35f, inputDesyncInterval))
        {
            return;
        }

        inputDesyncTimer = 0f;
        QueueInputDesyncEcho();
    }

    private void UpdateMapRecompileAbility()
    {
        if (currentState != AnomalyState.MapRecompile)
        {
            mapRecompileCharging = false;
            HideMapRecompileTelegraph();
            return;
        }

        mapRecompileTimer += Time.deltaTime;
        float interval = Mathf.Max(1.4f, mapRecompileInterval);
        float lead = Mathf.Clamp(mapRecompileTelegraphSeconds, 0.25f, interval * 0.75f);
        float remaining = interval - mapRecompileTimer;

        if (!mapRecompileCharging && remaining <= lead)
        {
            mapRecompileCharging = true;
            BuildMapRecompileTargets();
        }

        if (mapRecompileCharging && remaining > 0f)
        {
            float progress = 1f - Mathf.Clamp01(remaining / lead);
            UpdateMapRecompileTelegraph(progress);
        }

        if (mapRecompileTimer < interval)
        {
            return;
        }

        mapRecompileTimer = 0f;
        mapRecompileCharging = false;
        HideMapRecompileTelegraph();
        StartCoroutine(ExecuteMapRecompileRoutine());
    }

    private void UpdateSignalPossessionAbility()
    {
        if (currentState != AnomalyState.SignalPossession)
        {
            return;
        }

        signalPossessionTimer += Time.deltaTime;
        if (signalPossessionTimer < Mathf.Max(1.2f, signalPossessionInterval))
        {
            return;
        }

        signalPossessionTimer = 0f;
        SpawnSignalPossessionLure();
    }

    private void UpdatePhaseContractAbility()
    {
        if (currentState != AnomalyState.PhaseContract)
        {
            phaseContractActive = false;
            HidePhaseContractVisual();
            return;
        }

        if (phaseContractActive)
        {
            TickPhaseContract();
            return;
        }

        phaseContractTimer += Time.deltaTime;
        if (phaseContractTimer < Mathf.Max(1.6f, phaseContractInterval))
        {
            return;
        }

        phaseContractTimer = 0f;
        BeginPhaseContract();
    }

    private void UpdateDestroyerAbility()
    {
        if (destroyerTouchCooldownTimer > 0f)
        {
            destroyerTouchCooldownTimer -= Time.deltaTime;
        }
    }

    private void UpdateSplitAbility()
    {
        if (breachLureTimer > 0f || breachAbsorbed)
        {
            CancelSplitMergeForBreach();
            return;
        }

        if (currentState == AnomalyState.Split && splitStateActive && !splitMergeInProgress)
        {
            float remaining = Mathf.Max(0f, currentStateDuration - stateTimer);
            if (remaining <= Mathf.Max(0.05f, splitMergeLeadTime))
            {
                BeginSplitMerge();
            }
        }

        if (splitClone == null)
        {
            splitMergeInProgress = false;
            HideSplitMergeTelegraphVisual();
        }
    }

    private void BeginSplitState()
    {
        splitStateActive = true;
        splitMergeInProgress = false;
        splitMergeTimer = 0f;
        splitSideSign = Random.value < 0.5f ? -1 : 1;
        HideSplitMergeTelegraphVisual();

        if (splitClone != null)
        {
            splitClone.ConfigureRuntime(player, gameManager, this, obstacleMask);
            splitClone.SetSplitState(splitStateActive, splitMergeInProgress);
            return;
        }

        Vector2 spawnPos = (Vector2)transform.position + ComputeSplitSpawnOffset();
        GameObject cloneGo = new GameObject("AnomalySplitClone");
        cloneGo.transform.position = new Vector3(spawnPos.x, spawnPos.y, 0f);
        cloneGo.transform.localScale = transform.localScale;

        SpriteRenderer sr = cloneGo.AddComponent<SpriteRenderer>();
        sr.sprite = SquareSpriteProvider.Get();
        sr.color = splitCloneColor;
        sr.sortingOrder = 10;

        CircleCollider2D col = cloneGo.AddComponent<CircleCollider2D>();
        col.radius = Mathf.Max(0.2f, agentRadius * 0.95f);
        splitCloneRadius = col.radius;

        Rigidbody2D rbClone = cloneGo.AddComponent<Rigidbody2D>();
        rbClone.gravityScale = 0f;
        rbClone.freezeRotation = true;
        rbClone.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rbClone.interpolation = RigidbodyInterpolation2D.Interpolate;

        EnsureNoFrictionMaterial();
        col.sharedMaterial = noFrictionMaterial;
        if (ownCollider != null)
        {
            Physics2D.IgnoreCollision(ownCollider, col, true);
        }

        splitClone = cloneGo.AddComponent<SplitAnomalyCloneController>();
        splitClone.ConfigureMovement(
            Mathf.Max(0.5f, baseMoveSpeed * sectorSpeedMultiplier * splitCloneSpeedMultiplier),
            splitCloneVelocityResponsiveness,
            splitCloneSideOffset,
            splitMergeSpeed,
            splitSideSign);
        splitClone.ConfigureRuntime(player, gameManager, this, obstacleMask);
        splitClone.SetSplitState(splitStateActive, splitMergeInProgress);
    }

    private void BeginSplitMerge()
    {
        splitStateActive = false;
        splitMergeTimer = 0f;
        if (splitClone == null)
        {
            splitMergeInProgress = false;
            HideSplitMergeTelegraphVisual();
            return;
        }

        splitMergeInProgress = true;
        splitClone.SetSplitState(splitStateActive, splitMergeInProgress);
    }

    private Vector2 ComputeSplitSpawnOffset()
    {
        Vector2 toPlayer = player != null ? (player.GetPosition() - (Vector2)transform.position) : Vector2.right;
        if (toPlayer.sqrMagnitude < 0.0001f)
        {
            toPlayer = Vector2.right;
        }

        Vector2 side = new Vector2(-toPlayer.y, toPlayer.x).normalized * splitSideSign;
        float offset = Mathf.Max(0.2f, splitCloneSpawnOffset);
        return side * offset;
    }

    public void NotifySplitCloneMerged(SplitAnomalyCloneController clone)
    {
        if (clone != null && clone == splitClone)
        {
            splitClone = null;
        }

        splitMergeInProgress = false;
        splitMergeTimer = 0f;
        HideSplitMergeTelegraphVisual();
    }

    public bool IsSplitStateActive()
    {
        return splitStateActive;
    }

    public bool IsSplitMergeInProgress()
    {
        return splitMergeInProgress;
    }

    public bool IsBreachLureActive()
    {
        return breachLureTimer > 0f || breachAbsorbed;
    }

    public Vector2 GetCurrentPosition()
    {
        return rb != null ? rb.position : (Vector2)transform.position;
    }

    public Vector2 GetArenaCenter()
    {
        return navOrigin + navSize * 0.5f;
    }

    public void GetAdvancedStateArena(out Vector2 origin, out Vector2 size)
    {
        origin = navOrigin;
        size = navSize;
    }

    public Vector2 ClampAdvancedStatePoint(Vector2 point, float margin)
    {
        return ClampPointToArenaWithMargin(point, margin);
    }

    public bool TryBuildAdvancedStatePath(Vector2 startWorld, Vector2 goalWorld, List<Vector2> result)
    {
        if (result == null)
        {
            return false;
        }

        result.Clear();
        BuildNavigationGrid();
        Vector2Int start = WorldToCell(ClampPointToArenaWithMargin(startWorld, agentRadius + 0.12f));
        Vector2Int goal = WorldToCell(ClampPointToArenaWithMargin(goalWorld, agentRadius + 0.12f));
        if (!TryNearestWalkable(start, out start) || !TryNearestWalkable(goal, out goal) ||
            !TryFindPath(start, goal, out List<Vector2Int> cells))
        {
            return false;
        }

        result.Add(startWorld);
        for (int i = 1; i < cells.Count; i++)
        {
            result.Add(CellToWorld(cells[i]));
        }
        return result.Count > 1;
    }

    public bool HasAdvancedStateLineOfSight(Vector2 from, Vector2 to)
    {
        return HasDirectPath(from, to);
    }

    public void FireAdvancedAmbushVolley(Vector2 predictedTarget, int projectileCount)
    {
        if (player == null || !CanDamagePlayer())
        {
            return;
        }

        Vector2 origin = GetCurrentPosition();
        Vector2 baseDirection = predictedTarget - origin;
        if (baseDirection.sqrMagnitude < 0.001f)
        {
            baseDirection = player.GetPosition() - origin;
        }
        baseDirection = baseDirection.sqrMagnitude > 0.001f ? baseDirection.normalized : Vector2.right;
        int count = Mathf.Clamp(projectileCount, 1, 5);
        float totalSpread = count <= 1 ? 0f : 24f;
        for (int i = 0; i < count; i++)
        {
            float t = count <= 1 ? 0.5f : i / (float)(count - 1);
            float angle = Mathf.Lerp(-totalSpread * 0.5f, totalSpread * 0.5f, t);
            Vector2 direction = Rotate(baseDirection, angle);
            CreateProjectile(
                origin + direction * 0.48f,
                direction,
                new Color(1f, 0.72f, 0.30f, 1f),
                expansionShootProjectileSize * 0.88f,
                1.18f);
        }

        SpawnLevelTwoRadialBurst(origin, 1.15f, new Color(1f, 0.72f, 0.30f, 1f), "BlindspotAmbush");
        GlitchAudioManager.PlayEnemyOrbitBarrageFire(origin);
    }

    public void TeleportForAdvancedState(Vector2 position, bool preserveVelocity)
    {
        if (breachAbsorbed || rb == null)
        {
            return;
        }

        Vector2 velocity = rb.linearVelocity;
        Vector2 target = ClampPointToArenaWithMargin(position, agentRadius + 0.18f);
        rb.position = target;
        transform.position = new Vector3(target.x, target.y, transform.position.z);
        rb.linearVelocity = preserveVelocity ? velocity : Vector2.zero;
        pathWorld.Clear();
        pathIndex = 0;
        repathTimer = repathInterval;
        ResetBlockedRepathHysteresis();
        stuckCheckPosition = target;
        stuckConsecutiveChecks = 0;
    }

    public Vector2 GetCurrentTargetForSplitClone()
    {
        if (breachLureTimer > 0f)
        {
            return breachLureTarget;
        }

        if (player == null)
        {
            return GetCurrentPosition();
        }

        Vector2 target = player.GetPosition();
        Vector2 toPlayer = target - GetCurrentPosition();
        if (toPlayer.sqrMagnitude < 0.0001f)
        {
            return target;
        }

        Vector2 side = new Vector2(-toPlayer.y, toPlayer.x).normalized * splitSideSign;
        return target + side * splitCloneSideOffset;
    }

    public bool CanDamagePlayer()
    {
        return gameManager != null && gameManager.IsRunActive && !gameManager.IsGameOver;
    }

    public void ApplyExternalDisplacement(Vector2 delta)
    {
        if (breachAbsorbed || rb == null || delta.sqrMagnitude <= 0f)
        {
            return;
        }

        Vector2 target = rb.position + delta;
        rb.position = target;
        transform.position = new Vector3(target.x, target.y, transform.position.z);
    }

    public void ApplySectorProgression(int sectorLevel)
    {
        int extraSectors = Mathf.Max(0, sectorLevel - 1);
        sectorSpeedMultiplier = 1f + Mathf.Min(0.28f, extraSectors * 0.04f);
        sectorResponseMultiplier = 1f + Mathf.Min(0.36f, extraSectors * 0.055f);
    }

    public void ApplyParryImpact(Vector2 impactPosition, Vector2 pushDirection)
    {
        Vector2 direction = pushDirection.sqrMagnitude > 0.0001f
            ? pushDirection.normalized
            : ((Vector2)transform.position - impactPosition).normalized;

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = -lastMoveDirection;
        }

        parryStunTimer = Mathf.Max(parryStunTimer, Mathf.Max(0.04f, parryStunDuration));
        parryKnockbackTimer = Mathf.Max(0.02f, parryKnockbackDuration);
        rb.linearVelocity = direction * Mathf.Max(0.1f, parryKnockbackSpeed);
        TriggerStatePulse();
        SpawnParryImpactBurst();
        GlitchAudioManager.PlayEnemyParried(transform.position);
    }

    public void ApplyFirewallBurst(Vector2 burstOrigin, float burstRadius, float stunSeconds, float knockbackMultiplier)
    {
        Vector2 direction = ((Vector2)transform.position - burstOrigin);
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = -lastMoveDirection;
        }

        float distance = Vector2.Distance(transform.position, burstOrigin);
        float radius = Mathf.Max(0.5f, burstRadius);
        float force = Mathf.Lerp(1.25f, 0.72f, Mathf.Clamp01(distance / radius));
        parryStunTimer = Mathf.Max(parryStunTimer, Mathf.Max(0.08f, stunSeconds));
        parryKnockbackTimer = Mathf.Max(parryKnockbackTimer, Mathf.Max(0.08f, parryKnockbackDuration * 1.75f));
        rb.linearVelocity = direction.normalized * Mathf.Max(0.1f, parryKnockbackSpeed * Mathf.Max(0.1f, knockbackMultiplier) * force);
        TriggerStatePulse();
        SpawnParryImpactBurst();
        GlitchAudioManager.PlayEnemyParried(transform.position);
    }

    public void ApplyContainmentLock(Vector2 lockCenter, float seconds)
    {
        if (breachAbsorbed || rb == null)
        {
            return;
        }

        parryStunTimer = Mathf.Max(parryStunTimer, Mathf.Max(0.08f, seconds));
        parryKnockbackTimer = 0f;
        rb.linearVelocity = Vector2.zero;

        if (splitClone != null)
        {
            splitClone.ApplyContainmentLock(seconds * 0.85f);
        }

        TriggerStatePulse();
        SpawnParryImpactBurst();
        GlitchAudioManager.PlayEnemyParried(transform.position);
    }

    private bool TickParryStun()
    {
        if (parryStunTimer <= 0f)
        {
            return false;
        }

        parryStunTimer -= Time.deltaTime;
        parryKnockbackTimer -= Time.deltaTime;

        if (parryKnockbackTimer <= 0f)
        {
            rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, Vector2.zero, velocityResponsiveness * 1.8f * Time.deltaTime);
        }

        return true;
    }

    private void DestroySplitCloneImmediate()
    {
        if (splitClone == null)
        {
            splitMergeInProgress = false;
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(splitClone.gameObject);
        }
        else
        {
            DestroyImmediate(splitClone.gameObject);
        }

        splitClone = null;
        splitMergeInProgress = false;
        splitMergeTimer = 0f;
        HideSplitMergeTelegraphVisual();
    }

    private void AbsorbSplitCloneIntoBreach(Vector2 breachPosition)
    {
        if (splitClone == null)
        {
            splitMergeInProgress = false;
            splitMergeTimer = 0f;
            return;
        }

        splitClone.AbsorbIntoBreach(breachPosition);
        splitClone = null;
        splitMergeInProgress = false;
        splitMergeTimer = 0f;
        HideSplitMergeTelegraphVisual();
    }

    private void CancelSplitMergeForBreach()
    {
        if (splitClone != null)
        {
            splitStateActive = true;
            splitClone.SetSplitState(active: true, merging: false);
        }

        if (!splitMergeInProgress)
        {
            return;
        }

        splitMergeInProgress = false;
        splitMergeTimer = 0f;
        HideSplitMergeTelegraphVisual();
    }

    private void TickOwnerSplitMergeMovement()
    {
        if (splitClone == null)
        {
            splitMergeInProgress = false;
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 clonePos = splitClone.transform.position;
        Vector2 toClone = clonePos - rb.position;
        float distance = toClone.magnitude;
        splitMergeTimer += Time.deltaTime;
        float mergeThreshold = GetSplitMergeDistanceThreshold();
        if (distance <= mergeThreshold || splitMergeTimer >= Mathf.Max(0.25f, splitMergeTimeout))
        {
            ForceCompleteSplitMerge();
            return;
        }

        Vector2 desiredDirection = toClone / distance;
        desiredDirection = ApplyObstacleRepulsion(desiredDirection);
        if (desiredDirection.sqrMagnitude > 0.0001f)
        {
            lastMoveDirection = desiredDirection;
        }

        float speed = baseMoveSpeed * sectorSpeedMultiplier * Mathf.Max(0.2f, splitMergeOwnerSpeedMultiplier);
        Vector2 desiredVelocity = desiredDirection * speed;
        rb.linearVelocity = Vector2.MoveTowards(
            rb.linearVelocity,
            desiredVelocity,
            velocityResponsiveness * sectorResponseMultiplier * 1.15f * Time.deltaTime);
    }

    private void ForceCompleteSplitMerge()
    {
        rb.linearVelocity = Vector2.zero;

        if (splitClone != null)
        {
            if (Application.isPlaying)
            {
                Destroy(splitClone.gameObject);
            }
            else
            {
                DestroyImmediate(splitClone.gameObject);
            }

            splitClone = null;
        }

        splitMergeInProgress = false;
        splitMergeTimer = 0f;
        HideSplitMergeTelegraphVisual();
    }

    public float GetSplitMergeDistanceThreshold()
    {
        float cloneR = Mathf.Max(0.1f, splitCloneRadius);
        float ownR = Mathf.Max(0.1f, agentRadius);
        return ownR + cloneR + 0.06f;
    }

    private void UpdateSplitMergeTelegraphVisual()
    {
        if (splitClone == null)
        {
            HideSplitMergeTelegraphVisual();
            return;
        }

        EnsureSplitMergeTelegraphVisuals();
        if (splitMergeTelegraphRoot == null)
        {
            return;
        }

        if (!splitMergeTelegraphRoot.activeSelf)
        {
            splitMergeTelegraphRoot.SetActive(true);
        }

        Vector2 ownerPos = rb != null ? rb.position : (Vector2)transform.position;
        Vector2 clonePos = splitClone.transform.position;
        Vector2 delta = clonePos - ownerPos;
        float distance = delta.magnitude;
        Vector2 dir = distance > 0.001f ? delta / distance : Vector2.right;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Max(0.1f, splitMergeTelegraphPulseSpeed));

        Color c = splitMergeTelegraphColor;
        c.a *= Mathf.Lerp(0.45f, 1f, pulse);

        if (splitMergeOwnerRing != null)
        {
            splitMergeOwnerRing.color = c;
            splitMergeOwnerRing.transform.localPosition = Vector3.zero;
            float s = 1f + pulse * 0.12f;
            splitMergeOwnerRing.transform.localScale = new Vector3(s, s, 1f);
        }

        if (splitMergeCloneRing != null)
        {
            splitMergeCloneRing.color = c;
            splitMergeCloneRing.transform.localPosition = (Vector3)(clonePos - ownerPos);
            float s = 1f + (1f - pulse) * 0.12f;
            splitMergeCloneRing.transform.localScale = new Vector3(s, s, 1f);
        }

        int count = splitMergeBridgeSegments.Count;
        for (int i = 0; i < count; i++)
        {
            SpriteRenderer seg = splitMergeBridgeSegments[i];
            if (seg == null)
            {
                continue;
            }

            float t = (i + 1f) / (count + 1f);
            Vector2 pos = ownerPos + delta * t;
            seg.transform.localPosition = pos - ownerPos;
            seg.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            seg.color = new Color(c.r, c.g, c.b, c.a * Mathf.Lerp(0.45f, 0.95f, Mathf.Sin((Time.time * 5.5f + i) * 0.5f) * 0.5f + 0.5f));
            seg.size = splitMergeTelegraphSegmentSize;
        }
    }

    private void HideSplitMergeTelegraphVisual()
    {
        if (splitMergeTelegraphRoot != null && splitMergeTelegraphRoot.activeSelf)
        {
            splitMergeTelegraphRoot.SetActive(false);
        }
    }

    private void EnsureSplitMergeTelegraphVisuals()
    {
        int desiredSegments = Mathf.Max(3, splitMergeTelegraphSegments);
        float ringDiameter = Mathf.Max(0.1f, splitMergeTelegraphRingRadius) * 2f;

        if (splitMergeTelegraphRoot == null)
        {
            splitMergeTelegraphRoot = new GameObject("SplitMergeTelegraph");
            splitMergeTelegraphRoot.transform.SetParent(transform, false);
            splitMergeTelegraphRoot.transform.localPosition = Vector3.zero;
            splitMergeTelegraphRoot.transform.localRotation = Quaternion.identity;
            splitMergeTelegraphRoot.transform.localScale = Vector3.one;
            splitMergeTelegraphRoot.SetActive(false);

            GameObject ownerRing = new GameObject("OwnerRing");
            ownerRing.transform.SetParent(splitMergeTelegraphRoot.transform, false);
            splitMergeOwnerRing = ownerRing.AddComponent<SpriteRenderer>();
            splitMergeOwnerRing.sprite = CircleSpriteProvider.Get();
            splitMergeOwnerRing.drawMode = SpriteDrawMode.Sliced;
            splitMergeOwnerRing.size = Vector2.one * ringDiameter;
            splitMergeOwnerRing.color = splitMergeTelegraphColor;
            splitMergeOwnerRing.sortingOrder = 13;

            GameObject cloneRing = new GameObject("CloneRing");
            cloneRing.transform.SetParent(splitMergeTelegraphRoot.transform, false);
            splitMergeCloneRing = cloneRing.AddComponent<SpriteRenderer>();
            splitMergeCloneRing.sprite = CircleSpriteProvider.Get();
            splitMergeCloneRing.drawMode = SpriteDrawMode.Sliced;
            splitMergeCloneRing.size = Vector2.one * ringDiameter;
            splitMergeCloneRing.color = splitMergeTelegraphColor;
            splitMergeCloneRing.sortingOrder = 13;
        }

        if (splitMergeOwnerRing != null)
        {
            splitMergeOwnerRing.size = Vector2.one * ringDiameter;
        }

        if (splitMergeCloneRing != null)
        {
            splitMergeCloneRing.size = Vector2.one * ringDiameter;
        }

        while (splitMergeBridgeSegments.Count > desiredSegments)
        {
            int last = splitMergeBridgeSegments.Count - 1;
            SpriteRenderer seg = splitMergeBridgeSegments[last];
            splitMergeBridgeSegments.RemoveAt(last);
            if (seg != null)
            {
                Destroy(seg.gameObject);
            }
        }

        while (splitMergeBridgeSegments.Count < desiredSegments)
        {
            GameObject segGo = new GameObject($"Bridge_{splitMergeBridgeSegments.Count}");
            segGo.transform.SetParent(splitMergeTelegraphRoot.transform, false);
            SpriteRenderer sr = segGo.AddComponent<SpriteRenderer>();
            sr.sprite = SquareSpriteProvider.Get();
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = splitMergeTelegraphSegmentSize;
            sr.color = splitMergeTelegraphColor;
            sr.sortingOrder = 13;
            splitMergeBridgeSegments.Add(sr);
        }
    }

    private void DestroySplitMergeTelegraphImmediate()
    {
        if (splitMergeTelegraphRoot == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(splitMergeTelegraphRoot);
        }
        else
        {
            DestroyImmediate(splitMergeTelegraphRoot);
        }

        splitMergeTelegraphRoot = null;
        splitMergeOwnerRing = null;
        splitMergeCloneRing = null;
        splitMergeBridgeSegments.Clear();
    }

    private void FireExpansionShoot()
    {
        float projectileMultiplier = chaosDriveEnabled ? chaosExpansionProjectileMultiplier : 1f;
        int count = Mathf.Max(4, Mathf.RoundToInt(expansionShootProjectileCount * Mathf.Max(1f, projectileMultiplier)));
        float spawnRadius = Mathf.Max(0.05f, expansionShootSpawnRadius);
        Vector2 origin = rb != null ? rb.position : (Vector2)transform.position;

        for (int i = 0; i < count; i++)
        {
            float angleDeg = (360f / count) * i;
            Vector2 dir = new Vector2(Mathf.Cos(angleDeg * Mathf.Deg2Rad), Mathf.Sin(angleDeg * Mathf.Deg2Rad));
            Vector2 spawnPos = origin + dir * spawnRadius;
            CreateProjectile(spawnPos, dir);
        }
    }

    private void CreateProjectile(Vector2 position, Vector2 direction)
    {
        CreateProjectile(position, direction, expansionShootProjectileColor, expansionShootProjectileSize, 1f);
    }

    private void CreateProjectile(Vector2 position, Vector2 direction, Color projectileColor, Vector2 projectileSize, float speedMultiplier)
    {
        GameObject go = new GameObject($"AnomalyProjectile_{projectileSerial++}");
        go.transform.position = new Vector3(position.x, position.y, 0f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SquareSpriteProvider.Get();
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = projectileSize;
        sr.color = projectileColor;
        sr.sortingOrder = 11;

        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = Mathf.Max(0.04f, Mathf.Min(projectileSize.x, projectileSize.y) * 0.48f);

        AnomalyProjectile projectile = go.AddComponent<AnomalyProjectile>();
        projectile.Configure(
            direction,
            expansionShootProjectileSpeed * Mathf.Max(0.1f, speedMultiplier),
            expansionShootProjectileLifetime,
            obstacleMask,
            gameManager);
    }

    private Vector2 PickPhaseBlinkTarget()
    {
        Vector2 playerPos = player != null ? player.GetPosition() : (Vector2)transform.position;
        Vector2 fromEnemy = rb != null ? playerPos - rb.position : Vector2.right;
        if (fromEnemy.sqrMagnitude <= 0.01f)
        {
            fromEnemy = Random.insideUnitCircle;
        }

        Vector2 baseDir = fromEnemy.sqrMagnitude > 0.001f ? -fromEnemy.normalized : Vector2.right;
        float radius = Mathf.Max(0.8f, phaseBlinkDistanceFromPlayer);
        int attempts = Mathf.Max(3, phaseBlinkPlacementAttempts);

        for (int i = 0; i < attempts; i++)
        {
            float angle = Random.Range(-105f, 105f);
            Vector2 dir = Rotate(baseDir, angle);
            Vector2 candidate = ClampToArena(playerPos + dir * radius);
            if (IsBlinkCandidateValid(candidate))
            {
                return candidate;
            }
        }

        Vector2 fallback = ClampToArena(playerPos + baseDir * radius);
        return IsBlinkCandidateValid(fallback) ? fallback : ClampToArena(playerPos + Random.insideUnitCircle.normalized * radius);
    }

    private bool IsBlinkCandidateValid(Vector2 candidate)
    {
        if (!IsWalkableWorld(candidate))
        {
            return false;
        }

        float radius = Mathf.Max(0.15f, phaseBlinkProbeRadius);
        Collider2D[] hits = Physics2D.OverlapCircleAll(candidate, radius, obstacleMask);
        for (int i = 0; i < hits.Length; i++)
        {
            if (IsBlockingCollider(hits[i]))
            {
                return false;
            }
        }

        return true;
    }

    private void ExecutePhaseBlink()
    {
        Vector2 target = phaseBlinkTarget;
        SpawnPhaseBlinkBurst(rb != null ? rb.position : (Vector2)transform.position, 0.78f);
        if (rb != null)
        {
            rb.position = target;
            rb.linearVelocity = Vector2.zero;
        }
        transform.position = new Vector3(target.x, target.y, transform.position.z);
        SpawnPhaseBlinkBurst(target, 1f);
        TriggerStatePulse();
        GlitchAudioManager.PlayEnemyPhaseBlinkArrive(transform.position);
    }

    private static void ConfigureSpriteLine(SpriteRenderer rendererRef, Vector2 start, Vector2 end, float thickness, Color color)
    {
        if (rendererRef == null)
        {
            return;
        }

        Vector2 delta = end - start;
        float length = delta.magnitude;
        if (length <= 0.01f)
        {
            rendererRef.color = Color.clear;
            return;
        }

        rendererRef.transform.position = (start + end) * 0.5f;
        rendererRef.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        rendererRef.size = new Vector2(length, Mathf.Max(0.01f, thickness));
        rendererRef.color = color;
    }

    private void UpdatePhaseBlinkTelegraph(float progress)
    {
        EnsurePhaseBlinkTelegraph();
        if (phaseBlinkTelegraphRoot == null)
        {
            return;
        }

        if (!phaseBlinkTelegraphRoot.activeSelf)
        {
            phaseBlinkTelegraphRoot.SetActive(true);
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 16f);
        float warningPulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 8.5f);
        Vector2 origin = rb != null ? rb.position : (Vector2)transform.position;
        phaseBlinkTelegraphRoot.transform.position = new Vector3(phaseBlinkTarget.x, phaseBlinkTarget.y, 0f);

        if (phaseBlinkPathRenderer != null)
        {
            Color pathColor = new Color(phaseBlinkColor.r, phaseBlinkColor.g, phaseBlinkColor.b, Mathf.Lerp(0.10f, 0.54f, progress) * (0.70f + warningPulse * 0.30f));
            ConfigureSpriteLine(phaseBlinkPathRenderer, origin, phaseBlinkTarget, Mathf.Lerp(0.035f, 0.095f, progress), pathColor);
        }

        if (phaseBlinkOriginRenderer != null)
        {
            phaseBlinkOriginRenderer.transform.position = new Vector3(origin.x, origin.y, 0f);
            phaseBlinkOriginRenderer.size = Vector2.one * Mathf.Lerp(0.62f, 1.18f, progress);
            phaseBlinkOriginRenderer.color = new Color(phaseBlinkColor.r, phaseBlinkColor.g, phaseBlinkColor.b, Mathf.Lerp(0.08f, 0.38f, progress));
        }

        if (phaseBlinkRingRenderer != null)
        {
            phaseBlinkRingRenderer.size = Vector2.one * Mathf.Lerp(2.45f, 0.82f, progress);
            phaseBlinkRingRenderer.color = new Color(phaseBlinkColor.r, phaseBlinkColor.g, phaseBlinkColor.b, Mathf.Lerp(0.20f, 0.92f, progress) * (0.68f + pulse * 0.32f));
        }
        if (phaseBlinkCoreRenderer != null)
        {
            phaseBlinkCoreRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, Time.time * 220f);
            phaseBlinkCoreRenderer.size = Vector2.one * Mathf.Lerp(0.30f, 0.54f, pulse);
            phaseBlinkCoreRenderer.color = new Color(1f, 0.92f, 1f, Mathf.Lerp(0.32f, 1f, progress));
        }
        if (phaseBlinkCrossHorizontalRenderer != null)
        {
            phaseBlinkCrossHorizontalRenderer.size = new Vector2(Mathf.Lerp(0.42f, 1.45f, progress), 0.07f);
            phaseBlinkCrossHorizontalRenderer.color = new Color(1f, 0.46f, 0.92f, Mathf.Lerp(0.14f, 0.78f, progress) * (0.70f + warningPulse * 0.30f));
        }
        if (phaseBlinkCrossVerticalRenderer != null)
        {
            phaseBlinkCrossVerticalRenderer.size = new Vector2(0.07f, Mathf.Lerp(0.42f, 1.45f, progress));
            phaseBlinkCrossVerticalRenderer.color = new Color(0.62f, 1f, 0.94f, Mathf.Lerp(0.14f, 0.72f, progress) * (0.70f + warningPulse * 0.30f));
        }
    }

    private void EnsurePhaseBlinkTelegraph()
    {
        if (phaseBlinkTelegraphRoot != null)
        {
            return;
        }

        phaseBlinkTelegraphRoot = new GameObject("PhaseBlinkTelegraph");
        phaseBlinkTelegraphRoot.SetActive(false);

        GameObject path = new GameObject("PhaseBlinkPath");
        path.transform.SetParent(phaseBlinkTelegraphRoot.transform, false);
        phaseBlinkPathRenderer = path.AddComponent<SpriteRenderer>();
        phaseBlinkPathRenderer.sprite = SquareSpriteProvider.Get();
        phaseBlinkPathRenderer.drawMode = SpriteDrawMode.Sliced;
        phaseBlinkPathRenderer.sortingOrder = 14;

        GameObject origin = new GameObject("PhaseBlinkOrigin");
        origin.transform.SetParent(phaseBlinkTelegraphRoot.transform, false);
        phaseBlinkOriginRenderer = origin.AddComponent<SpriteRenderer>();
        phaseBlinkOriginRenderer.sprite = CircleSpriteProvider.Get();
        phaseBlinkOriginRenderer.drawMode = SpriteDrawMode.Sliced;
        phaseBlinkOriginRenderer.sortingOrder = 14;

        GameObject ring = new GameObject("PhaseBlinkRing");
        ring.transform.SetParent(phaseBlinkTelegraphRoot.transform, false);
        phaseBlinkRingRenderer = ring.AddComponent<SpriteRenderer>();
        phaseBlinkRingRenderer.sprite = CircleSpriteProvider.Get();
        phaseBlinkRingRenderer.drawMode = SpriteDrawMode.Sliced;
        phaseBlinkRingRenderer.sortingOrder = 15;

        GameObject crossH = new GameObject("PhaseBlinkTargetCrossH");
        crossH.transform.SetParent(phaseBlinkTelegraphRoot.transform, false);
        phaseBlinkCrossHorizontalRenderer = crossH.AddComponent<SpriteRenderer>();
        phaseBlinkCrossHorizontalRenderer.sprite = SquareSpriteProvider.Get();
        phaseBlinkCrossHorizontalRenderer.drawMode = SpriteDrawMode.Sliced;
        phaseBlinkCrossHorizontalRenderer.sortingOrder = 16;

        GameObject crossV = new GameObject("PhaseBlinkTargetCrossV");
        crossV.transform.SetParent(phaseBlinkTelegraphRoot.transform, false);
        phaseBlinkCrossVerticalRenderer = crossV.AddComponent<SpriteRenderer>();
        phaseBlinkCrossVerticalRenderer.sprite = SquareSpriteProvider.Get();
        phaseBlinkCrossVerticalRenderer.drawMode = SpriteDrawMode.Sliced;
        phaseBlinkCrossVerticalRenderer.sortingOrder = 16;

        GameObject core = new GameObject("PhaseBlinkCore");
        core.transform.SetParent(phaseBlinkTelegraphRoot.transform, false);
        phaseBlinkCoreRenderer = core.AddComponent<SpriteRenderer>();
        phaseBlinkCoreRenderer.sprite = SquareSpriteProvider.Get();
        phaseBlinkCoreRenderer.drawMode = SpriteDrawMode.Sliced;
        phaseBlinkCoreRenderer.sortingOrder = 16;
    }

    private void HidePhaseBlinkTelegraph()
    {
        if (phaseBlinkTelegraphRoot != null && phaseBlinkTelegraphRoot.activeSelf)
        {
            phaseBlinkTelegraphRoot.SetActive(false);
        }
    }

    private void SpawnPhaseBlinkBurst(Vector2 position, float alpha)
    {
        GameObject ring = new GameObject("PhaseBlinkBurst");
        ring.transform.position = new Vector3(position.x, position.y, 0f);
        SpriteRenderer sr = ring.AddComponent<SpriteRenderer>();
        sr.sprite = CircleSpriteProvider.Get();
        sr.sortingOrder = 16;
        sr.color = new Color(phaseBlinkColor.r, phaseBlinkColor.g, phaseBlinkColor.b, Mathf.Clamp01(alpha));
        ring.transform.localScale = Vector3.one * 0.25f;
        ring.AddComponent<AnomalyStateBurstFx>().Configure(sr, 1.55f, 0.22f, phaseBlinkColor);
        Destroy(ring, 0.32f);
    }

    private void ComputePincerSpawnPoints(out Vector2 left, out Vector2 right)
    {
        Vector2 playerPos = player != null ? player.GetPosition() : (Vector2)transform.position;
        Vector2 towardPlayer = rb != null ? playerPos - rb.position : Vector2.right;
        if (towardPlayer.sqrMagnitude <= 0.01f)
        {
            towardPlayer = Vector2.right;
        }

        Vector2 side = new Vector2(-towardPlayer.y, towardPlayer.x).normalized;
        float distance = Mathf.Max(2.5f, pincerSpawnDistance);
        left = ClampToArena(playerPos + side * distance);
        right = ClampToArena(playerPos - side * distance);
    }

    private void UpdatePincerTelegraph(float progress)
    {
        EnsurePincerTelegraph();
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 14f);
        Vector2 playerPos = player != null ? player.GetPosition() : (Vector2)transform.position;

        if (pincerFocusRingRenderer != null)
        {
            pincerFocusRingRenderer.transform.position = new Vector3(playerPos.x, playerPos.y, 0f);
            pincerFocusRingRenderer.size = Vector2.one * Mathf.Lerp(1.85f, 0.78f, progress);
            pincerFocusRingRenderer.color = new Color(1f, 0.46f, 0.78f, Mathf.Lerp(0.10f, 0.62f, progress) * (0.72f + pulse * 0.28f));
        }

        for (int i = 0; i < pincerTelegraphRenderers.Count; i++)
        {
            SpriteRenderer sr = pincerTelegraphRenderers[i];
            if (sr == null)
            {
                continue;
            }

            Vector2 pos = (i & 1) == 0 ? pincerLeftSpawn : pincerRightSpawn;
            int pairCount = Mathf.Max(1, pincerProjectilePairs);
            int pairIndex = i / 2;
            float lane = pairCount == 1 ? 0f : Mathf.Lerp(-1f, 1f, pairIndex / (float)(pairCount - 1));
            Vector2 dir = (playerPos - pos).sqrMagnitude > 0.001f ? (playerPos - pos).normalized : Vector2.right;
            Vector2 perp = new Vector2(-dir.y, dir.x);
            Vector2 laneStart = pos + perp * lane * Mathf.Max(0.2f, pincerVerticalSpread * 0.34f);
            sr.transform.position = laneStart;
            sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            sr.size = new Vector2(Mathf.Lerp(0.78f, 1.55f, progress), 0.09f);
            sr.color = new Color(pincerProjectileColor.r, pincerProjectileColor.g, pincerProjectileColor.b, Mathf.Lerp(0.20f, 0.88f, progress) * (0.75f + pulse * 0.25f));

            if (i < pincerLaneRenderers.Count && pincerLaneRenderers[i] != null)
            {
                Color laneColor = new Color(pincerProjectileColor.r, pincerProjectileColor.g, pincerProjectileColor.b, Mathf.Lerp(0.06f, 0.34f, progress));
                ConfigureSpriteLine(pincerLaneRenderers[i], laneStart, playerPos, Mathf.Lerp(0.035f, 0.075f, progress), laneColor);
            }

            if (i < pincerArrowRenderers.Count && pincerArrowRenderers[i] != null)
            {
                SpriteRenderer arrow = pincerArrowRenderers[i];
                float arrowTravel = Mathf.Lerp(0.28f, 0.72f, Mathf.PingPong(Time.time * 1.6f + i * 0.17f, 1f));
                Vector2 arrowPos = Vector2.Lerp(laneStart, playerPos, arrowTravel);
                arrow.transform.position = arrowPos;
                arrow.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                arrow.size = new Vector2(Mathf.Lerp(0.30f, 0.62f, progress), 0.075f);
                arrow.color = new Color(1f, 0.70f, 0.98f, Mathf.Lerp(0.10f, 0.62f, progress) * (0.55f + pulse * 0.45f));
            }
        }
    }

    private void EnsurePincerTelegraph()
    {
        int desired = Mathf.Max(2, pincerProjectilePairs * 2);
        if (pincerTelegraphRoot == null)
        {
            pincerTelegraphRoot = new GameObject("PincerBarrageTelegraph");

            GameObject focus = new GameObject("PincerFocusRing");
            focus.transform.SetParent(pincerTelegraphRoot.transform, false);
            pincerFocusRingRenderer = focus.AddComponent<SpriteRenderer>();
            pincerFocusRingRenderer.sprite = CircleSpriteProvider.Get();
            pincerFocusRingRenderer.drawMode = SpriteDrawMode.Sliced;
            pincerFocusRingRenderer.sortingOrder = 14;
        }

        while (pincerTelegraphRenderers.Count < desired)
        {
            GameObject go = new GameObject($"PincerWarn_{pincerTelegraphRenderers.Count}");
            go.transform.SetParent(pincerTelegraphRoot.transform, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SquareSpriteProvider.Get();
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.sortingOrder = 15;
            pincerTelegraphRenderers.Add(sr);
        }

        while (pincerTelegraphRenderers.Count > desired)
        {
            int last = pincerTelegraphRenderers.Count - 1;
            SpriteRenderer sr = pincerTelegraphRenderers[last];
            pincerTelegraphRenderers.RemoveAt(last);
            if (sr != null)
            {
                Destroy(sr.gameObject);
            }
        }

        while (pincerLaneRenderers.Count < desired)
        {
            GameObject go = new GameObject($"PincerLane_{pincerLaneRenderers.Count}");
            go.transform.SetParent(pincerTelegraphRoot.transform, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SquareSpriteProvider.Get();
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.sortingOrder = 13;
            pincerLaneRenderers.Add(sr);
        }

        while (pincerLaneRenderers.Count > desired)
        {
            int last = pincerLaneRenderers.Count - 1;
            SpriteRenderer sr = pincerLaneRenderers[last];
            pincerLaneRenderers.RemoveAt(last);
            if (sr != null)
            {
                Destroy(sr.gameObject);
            }
        }

        while (pincerArrowRenderers.Count < desired)
        {
            GameObject go = new GameObject($"PincerArrow_{pincerArrowRenderers.Count}");
            go.transform.SetParent(pincerTelegraphRoot.transform, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SquareSpriteProvider.Get();
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.sortingOrder = 16;
            pincerArrowRenderers.Add(sr);
        }

        while (pincerArrowRenderers.Count > desired)
        {
            int last = pincerArrowRenderers.Count - 1;
            SpriteRenderer sr = pincerArrowRenderers[last];
            pincerArrowRenderers.RemoveAt(last);
            if (sr != null)
            {
                Destroy(sr.gameObject);
            }
        }
    }

    private void HidePincerTelegraph()
    {
        for (int i = 0; i < pincerTelegraphRenderers.Count; i++)
        {
            if (pincerTelegraphRenderers[i] != null)
            {
                pincerTelegraphRenderers[i].color = Color.clear;
            }
        }
        for (int i = 0; i < pincerLaneRenderers.Count; i++)
        {
            if (pincerLaneRenderers[i] != null)
            {
                pincerLaneRenderers[i].color = Color.clear;
            }
        }
        for (int i = 0; i < pincerArrowRenderers.Count; i++)
        {
            if (pincerArrowRenderers[i] != null)
            {
                pincerArrowRenderers[i].color = Color.clear;
            }
        }
        if (pincerFocusRingRenderer != null)
        {
            pincerFocusRingRenderer.color = Color.clear;
        }
    }

    private void DestroyLevelTwoTelegraphsImmediate()
    {
        DestroyTelegraphRootImmediate(phaseBlinkTelegraphRoot);
        phaseBlinkTelegraphRoot = null;
        phaseBlinkRingRenderer = null;
        phaseBlinkCoreRenderer = null;
        phaseBlinkOriginRenderer = null;
        phaseBlinkPathRenderer = null;
        phaseBlinkCrossHorizontalRenderer = null;
        phaseBlinkCrossVerticalRenderer = null;

        DestroyTelegraphRootImmediate(pincerTelegraphRoot);
        pincerTelegraphRoot = null;
        pincerFocusRingRenderer = null;
        pincerTelegraphRenderers.Clear();
        pincerLaneRenderers.Clear();
        pincerArrowRenderers.Clear();

        DestroyTelegraphRootImmediate(signalJamTelegraphRoot);
        signalJamTelegraphRoot = null;
        signalJamRingRenderer = null;
        signalJamCoreRenderer = null;
        signalJamInnerRenderer = null;
        signalJamCrossHorizontalRenderer = null;
        signalJamCrossVerticalRenderer = null;
        signalJamWarningRenderer = null;
        signalJamTickRenderers.Clear();
        signalJamNoiseRenderers.Clear();

        DestroyTelegraphRootImmediate(orbitBarrageTelegraphRoot);
        orbitBarrageTelegraphRoot = null;
        orbitBarrageRingRenderer = null;
        orbitBarrageInnerRingRenderer = null;
        orbitBarrageTickRenderers.Clear();
        orbitBarrageGuideRenderers.Clear();

        DestroyTelegraphRootImmediate(mapRecompileTelegraphRoot);
        mapRecompileTelegraphRoot = null;
        mapRecompileTelegraphRenderers.Clear();

        DestroyTelegraphRootImmediate(phaseContractRoot);
        phaseContractRoot = null;
        phaseContractText = null;
        phaseContractRingRenderer = null;
        phaseContractLineRenderer = null;

        if (activeSignalPossessionLure != null)
        {
            DestroyTelegraphRootImmediate(activeSignalPossessionLure.gameObject);
            activeSignalPossessionLure = null;
        }
    }

    private static void DestroyTelegraphRootImmediate(GameObject root)
    {
        if (root == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(root);
        }
        else
        {
            DestroyImmediate(root);
        }
    }

    private void FirePincerBarrage()
    {
        int pairs = Mathf.Max(1, pincerProjectilePairs);
        Vector2 playerPos = player != null ? player.GetPosition() : (Vector2)transform.position;
        for (int i = 0; i < pairs; i++)
        {
            float lane = pairs == 1 ? 0f : Mathf.Lerp(-1f, 1f, i / (float)(pairs - 1));
            Vector2 leftDir = (playerPos - pincerLeftSpawn).sqrMagnitude > 0.001f ? (playerPos - pincerLeftSpawn).normalized : Vector2.right;
            Vector2 rightDir = (playerPos - pincerRightSpawn).sqrMagnitude > 0.001f ? (playerPos - pincerRightSpawn).normalized : Vector2.left;
            Vector2 leftPerp = new Vector2(-leftDir.y, leftDir.x);
            Vector2 rightPerp = new Vector2(-rightDir.y, rightDir.x);
            Vector2 leftPos = pincerLeftSpawn + leftPerp * lane * Mathf.Max(0.2f, pincerVerticalSpread);
            Vector2 rightPos = pincerRightSpawn + rightPerp * lane * Mathf.Max(0.2f, pincerVerticalSpread);
            CreateProjectile(leftPos, leftDir, pincerProjectileColor, expansionShootProjectileSize * 0.92f, pincerProjectileSpeedMultiplier);
            CreateProjectile(rightPos, rightDir, pincerProjectileColor, expansionShootProjectileSize * 0.92f, pincerProjectileSpeedMultiplier);
        }

        GlitchAudioManager.PlayEnemyPincerFire(transform.position);
    }

    private Vector2 PickSignalJamCenter()
    {
        Vector2 playerPos = player != null ? player.GetPosition() : (Vector2)transform.position;
        Vector2 playerVelocity = player != null ? player.CurrentVelocity : Vector2.zero;
        Vector2 predicted = playerPos + playerVelocity * 0.18f;
        return ClampToArena(predicted);
    }

    private void UpdateSignalJamTelegraph(float progress)
    {
        EnsureSignalJamTelegraph();
        if (signalJamTelegraphRoot == null)
        {
            return;
        }

        if (!signalJamTelegraphRoot.activeSelf)
        {
            signalJamTelegraphRoot.SetActive(true);
        }

        float radius = Mathf.Max(0.35f, signalJamRadius);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 18f);
        float warningPulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 7.5f);
        signalJamTelegraphRoot.transform.position = new Vector3(signalJamCenter.x, signalJamCenter.y, 0f);

        if (signalJamInnerRenderer != null)
        {
            signalJamInnerRenderer.size = Vector2.one * radius * 2f;
            signalJamInnerRenderer.color = new Color(0.04f, 0.025f, 0.012f, Mathf.Lerp(0.08f, 0.24f, progress));
        }

        if (signalJamRingRenderer != null)
        {
            float diameter = Mathf.Lerp(radius * 2.55f, radius * 2f, progress);
            signalJamRingRenderer.size = Vector2.one * diameter;
            signalJamRingRenderer.color = new Color(signalJamColor.r, signalJamColor.g, signalJamColor.b, Mathf.Lerp(0.20f, 0.82f, progress) * (0.76f + warningPulse * 0.24f));
        }

        if (signalJamCoreRenderer != null)
        {
            signalJamCoreRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, -Time.time * 180f);
            signalJamCoreRenderer.size = Vector2.one * Mathf.Lerp(0.28f, 0.58f, pulse);
            signalJamCoreRenderer.color = new Color(1f, 0.94f, 0.70f, Mathf.Lerp(0.26f, 0.92f, progress));
        }

        if (signalJamCrossHorizontalRenderer != null)
        {
            signalJamCrossHorizontalRenderer.size = new Vector2(radius * Mathf.Lerp(0.85f, 1.78f, progress), 0.055f);
            signalJamCrossHorizontalRenderer.color = new Color(signalJamColor.r, signalJamColor.g, signalJamColor.b, Mathf.Lerp(0.12f, 0.58f, progress) * (0.65f + pulse * 0.35f));
        }

        if (signalJamCrossVerticalRenderer != null)
        {
            signalJamCrossVerticalRenderer.size = new Vector2(0.055f, radius * Mathf.Lerp(0.85f, 1.78f, progress));
            signalJamCrossVerticalRenderer.color = new Color(0.96f, 0.92f, 0.78f, Mathf.Lerp(0.10f, 0.48f, progress) * (0.65f + pulse * 0.35f));
        }

        if (signalJamWarningRenderer != null)
        {
            signalJamWarningRenderer.size = new Vector2(radius * 1.52f, 0.12f);
            signalJamWarningRenderer.transform.localPosition = Vector3.up * radius * 0.60f;
            signalJamWarningRenderer.color = new Color(1f, 0.34f, 0.22f, Mathf.Lerp(0.12f, 0.68f, progress) * warningPulse);
        }

        for (int i = 0; i < signalJamTickRenderers.Count; i++)
        {
            SpriteRenderer tick = signalJamTickRenderers[i];
            if (tick == null)
            {
                continue;
            }

            float angle = ((Mathf.PI * 2f) * i) / signalJamTickRenderers.Count + Time.time * 0.85f;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            tick.transform.localPosition = dir * radius * Mathf.Lerp(0.75f, 1f, progress);
            tick.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f);
            tick.size = new Vector2(0.09f, Mathf.Lerp(0.28f, 0.62f, progress));
            tick.color = new Color(signalJamColor.r, signalJamColor.g, signalJamColor.b, Mathf.Lerp(0.16f, 0.76f, progress));
        }

        for (int i = 0; i < signalJamNoiseRenderers.Count; i++)
        {
            SpriteRenderer noise = signalJamNoiseRenderers[i];
            if (noise == null)
            {
                continue;
            }

            float seed = i * 1.73f;
            float angle = seed + Time.time * (1.4f + (i % 3) * 0.22f);
            float distance = radius * Mathf.Lerp(0.18f, 0.78f, Mathf.PingPong(Time.time * 0.9f + i * 0.19f, 1f));
            Vector2 pos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
            noise.transform.localPosition = pos;
            noise.transform.localRotation = Quaternion.Euler(0f, 0f, Random.value * 180f);
            noise.size = new Vector2(Mathf.Lerp(0.16f, 0.42f, Mathf.PingPong(Time.time * 4f + i, 1f)), 0.035f);
            noise.color = new Color(1f, 0.92f, 0.64f, Mathf.Lerp(0.05f, 0.32f, progress) * (0.45f + pulse * 0.55f));
        }
    }

    private void EnsureSignalJamTelegraph()
    {
        if (signalJamTelegraphRoot == null)
        {
            signalJamTelegraphRoot = new GameObject("SignalJamTelegraph");
            signalJamTelegraphRoot.SetActive(false);

            GameObject inner = new GameObject("SignalJamInnerField");
            inner.transform.SetParent(signalJamTelegraphRoot.transform, false);
            signalJamInnerRenderer = inner.AddComponent<SpriteRenderer>();
            signalJamInnerRenderer.sprite = CircleSpriteProvider.Get();
            signalJamInnerRenderer.drawMode = SpriteDrawMode.Sliced;
            signalJamInnerRenderer.sortingOrder = 13;

            GameObject ring = new GameObject("SignalJamRing");
            ring.transform.SetParent(signalJamTelegraphRoot.transform, false);
            signalJamRingRenderer = ring.AddComponent<SpriteRenderer>();
            signalJamRingRenderer.sprite = CircleSpriteProvider.Get();
            signalJamRingRenderer.drawMode = SpriteDrawMode.Sliced;
            signalJamRingRenderer.sortingOrder = 15;

            GameObject crossH = new GameObject("SignalJamCrossHorizontal");
            crossH.transform.SetParent(signalJamTelegraphRoot.transform, false);
            signalJamCrossHorizontalRenderer = crossH.AddComponent<SpriteRenderer>();
            signalJamCrossHorizontalRenderer.sprite = SquareSpriteProvider.Get();
            signalJamCrossHorizontalRenderer.drawMode = SpriteDrawMode.Sliced;
            signalJamCrossHorizontalRenderer.sortingOrder = 16;

            GameObject crossV = new GameObject("SignalJamCrossVertical");
            crossV.transform.SetParent(signalJamTelegraphRoot.transform, false);
            signalJamCrossVerticalRenderer = crossV.AddComponent<SpriteRenderer>();
            signalJamCrossVerticalRenderer.sprite = SquareSpriteProvider.Get();
            signalJamCrossVerticalRenderer.drawMode = SpriteDrawMode.Sliced;
            signalJamCrossVerticalRenderer.sortingOrder = 16;

            GameObject warning = new GameObject("SignalJamWarningBar");
            warning.transform.SetParent(signalJamTelegraphRoot.transform, false);
            signalJamWarningRenderer = warning.AddComponent<SpriteRenderer>();
            signalJamWarningRenderer.sprite = SquareSpriteProvider.Get();
            signalJamWarningRenderer.drawMode = SpriteDrawMode.Sliced;
            signalJamWarningRenderer.sortingOrder = 17;

            GameObject core = new GameObject("SignalJamCore");
            core.transform.SetParent(signalJamTelegraphRoot.transform, false);
            signalJamCoreRenderer = core.AddComponent<SpriteRenderer>();
            signalJamCoreRenderer.sprite = SquareSpriteProvider.Get();
            signalJamCoreRenderer.drawMode = SpriteDrawMode.Sliced;
            signalJamCoreRenderer.sortingOrder = 16;
        }

        while (signalJamTickRenderers.Count < 14)
        {
            GameObject tickGo = new GameObject($"SignalJamTick_{signalJamTickRenderers.Count}");
            tickGo.transform.SetParent(signalJamTelegraphRoot.transform, false);
            SpriteRenderer tick = tickGo.AddComponent<SpriteRenderer>();
            tick.sprite = SquareSpriteProvider.Get();
            tick.drawMode = SpriteDrawMode.Sliced;
            tick.sortingOrder = 16;
            signalJamTickRenderers.Add(tick);
        }

        while (signalJamNoiseRenderers.Count < 18)
        {
            GameObject noiseGo = new GameObject($"SignalJamNoise_{signalJamNoiseRenderers.Count}");
            noiseGo.transform.SetParent(signalJamTelegraphRoot.transform, false);
            SpriteRenderer noise = noiseGo.AddComponent<SpriteRenderer>();
            noise.sprite = SquareSpriteProvider.Get();
            noise.drawMode = SpriteDrawMode.Sliced;
            noise.sortingOrder = 15;
            signalJamNoiseRenderers.Add(noise);
        }
    }

    private void HideSignalJamTelegraph()
    {
        if (signalJamTelegraphRoot != null && signalJamTelegraphRoot.activeSelf)
        {
            signalJamTelegraphRoot.SetActive(false);
        }
    }

    private void FireSignalJam()
    {
        float radius = Mathf.Max(0.35f, signalJamRadius);
        Vector2 playerPos = player != null ? player.GetPosition() : signalJamCenter;
        if (player != null && Vector2.Distance(playerPos, signalJamCenter) <= radius)
        {
            player.ApplyMovementSlow(signalJamSlowMultiplier, signalJamSlowDuration);
            SpawnSignalJamHitMarker(playerPos);
        }

        SpawnSignalJamBurst(signalJamCenter, radius);
        GlitchAudioManager.PlayEnemySignalJamFire(new Vector3(signalJamCenter.x, signalJamCenter.y, transform.position.z));
    }

    private void SpawnSignalJamHitMarker(Vector2 position)
    {
        Color hitColor = new Color(1f, 0.32f, 0.20f, 1f);
        GameObject ring = new GameObject("SignalJamPlayerHitRing");
        ring.transform.position = new Vector3(position.x, position.y, 0f);
        SpriteRenderer ringRenderer = ring.AddComponent<SpriteRenderer>();
        ringRenderer.sprite = CircleSpriteProvider.Get();
        ringRenderer.sortingOrder = 21;
        ringRenderer.color = hitColor;
        ring.transform.localScale = Vector3.one * 0.2f;
        ring.AddComponent<AnomalyStateBurstFx>().Configure(ringRenderer, 0.82f, 0.22f, hitColor);
        Destroy(ring, 0.32f);

        for (int i = 0; i < 4; i++)
        {
            float angle = (Mathf.PI * 0.5f) * i + Mathf.PI * 0.25f;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            GameObject ray = new GameObject($"SignalJamPlayerHitRay_{i}");
            ray.transform.position = new Vector3(position.x, position.y, 0f);
            SpriteRenderer sr = ray.AddComponent<SpriteRenderer>();
            sr.sprite = SquareSpriteProvider.Get();
            sr.sortingOrder = 22;
            sr.color = hitColor;
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = new Vector2(0.34f, 0.08f);
            ray.AddComponent<AnomalyStateRayFx>().Configure(sr, dir, 0.55f, 0.18f);
            Destroy(ray, 0.28f);
        }
    }

    private void SpawnSignalJamBurst(Vector2 center, float radius)
    {
        SpawnLevelTwoRadialBurst(center, radius, signalJamColor, "SignalJam");
    }

    private void SpawnLevelTwoRadialBurst(Vector2 center, float radius, Color color, string prefix)
    {
        GameObject ring = new GameObject($"{prefix}Burst");
        ring.transform.position = new Vector3(center.x, center.y, 0f);
        SpriteRenderer sr = ring.AddComponent<SpriteRenderer>();
        sr.sprite = CircleSpriteProvider.Get();
        sr.sortingOrder = 16;
        sr.color = color;
        ring.transform.localScale = Vector3.one * 0.2f;
        ring.AddComponent<AnomalyStateBurstFx>().Configure(sr, radius * 1.35f, 0.28f, color);
        Destroy(ring, 0.42f);

        for (int i = 0; i < 12; i++)
        {
            float angle = ((Mathf.PI * 2f) * i) / 12f + Random.Range(-0.12f, 0.12f);
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            GameObject ray = new GameObject($"{prefix}Shard_{i}");
            ray.transform.position = new Vector3(center.x, center.y, 0f);
            SpriteRenderer rayRenderer = ray.AddComponent<SpriteRenderer>();
            rayRenderer.sprite = SquareSpriteProvider.Get();
            rayRenderer.sortingOrder = 16;
            rayRenderer.color = color;
            ray.transform.localScale = new Vector3(0.18f, 0.06f, 1f);
            ray.AddComponent<AnomalyStateRayFx>().Configure(rayRenderer, dir, radius, 0.24f);
            Destroy(ray, 0.36f);
        }
    }

    private Vector2 PickOrbitBarrageCenter()
    {
        Vector2 playerPos = player != null ? player.GetPosition() : (Vector2)transform.position;
        Vector2 playerVelocity = player != null ? player.CurrentVelocity : Vector2.zero;
        return ClampToArena(playerPos + playerVelocity * 0.12f);
    }

    private void UpdateOrbitBarrageTelegraph(float progress)
    {
        EnsureOrbitBarrageTelegraph();
        if (orbitBarrageTelegraphRoot == null)
        {
            return;
        }

        if (!orbitBarrageTelegraphRoot.activeSelf)
        {
            orbitBarrageTelegraphRoot.SetActive(true);
        }

        float radius = Mathf.Max(0.65f, orbitBarrageSpawnRadius);
        float spin = Time.time * 1.15f * orbitBarrageDirectionSign;
        int count = Mathf.Max(4, orbitBarrageProjectileCount);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 8.5f);
        orbitBarrageTelegraphRoot.transform.position = new Vector3(orbitBarrageCenter.x, orbitBarrageCenter.y, 0f);

        if (orbitBarrageRingRenderer != null)
        {
            orbitBarrageRingRenderer.size = Vector2.one * radius * Mathf.Lerp(2.32f, 2.02f, progress);
            orbitBarrageRingRenderer.color = new Color(orbitBarrageProjectileColor.r, orbitBarrageProjectileColor.g, orbitBarrageProjectileColor.b, Mathf.Lerp(0.14f, 0.66f, progress) * (0.72f + pulse * 0.28f));
        }

        if (orbitBarrageInnerRingRenderer != null)
        {
            orbitBarrageInnerRingRenderer.size = Vector2.one * radius * Mathf.Lerp(0.72f, 0.96f, progress);
            orbitBarrageInnerRingRenderer.color = new Color(1f, 0.62f, 0.96f, Mathf.Lerp(0.06f, 0.36f, progress) * (0.68f + pulse * 0.32f));
        }

        for (int i = 0; i < orbitBarrageTickRenderers.Count; i++)
        {
            SpriteRenderer tick = orbitBarrageTickRenderers[i];
            if (tick == null)
            {
                continue;
            }

            float angle = ((Mathf.PI * 2f) * i) / count + spin;
            Vector2 radial = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 tangential = new Vector2(-radial.y, radial.x) * orbitBarrageDirectionSign;
            float lanePulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 15f + i * 0.8f);
            tick.transform.localPosition = radial * radius * Mathf.Lerp(0.82f, 1f, progress);
            tick.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(tangential.y, tangential.x) * Mathf.Rad2Deg);
            tick.size = new Vector2(Mathf.Lerp(0.32f, 0.58f, progress), 0.09f + lanePulse * 0.06f);
            tick.color = new Color(orbitBarrageProjectileColor.r, orbitBarrageProjectileColor.g, orbitBarrageProjectileColor.b, Mathf.Lerp(0.18f, 0.88f, progress));
        }

        for (int i = 0; i < orbitBarrageGuideRenderers.Count; i++)
        {
            SpriteRenderer guide = orbitBarrageGuideRenderers[i];
            if (guide == null)
            {
                continue;
            }

            float angle = ((Mathf.PI * 2f) * i) / Mathf.Max(1, orbitBarrageGuideRenderers.Count) + spin;
            Vector2 radial = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 tangential = new Vector2(-radial.y, radial.x) * orbitBarrageDirectionSign;
            bool isArrow = (i & 1) == 0;
            if (isArrow)
            {
                guide.transform.localPosition = radial * radius * 0.62f + tangential * 0.22f;
                guide.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(tangential.y, tangential.x) * Mathf.Rad2Deg);
                guide.size = new Vector2(Mathf.Lerp(0.28f, 0.72f, progress), 0.055f);
                guide.color = new Color(1f, 0.62f, 0.96f, Mathf.Lerp(0.08f, 0.48f, progress));
            }
            else
            {
                guide.transform.localPosition = radial * radius * 0.5f;
                guide.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(radial.y, radial.x) * Mathf.Rad2Deg);
                guide.size = new Vector2(radius * Mathf.Lerp(0.22f, 0.42f, progress), 0.035f);
                guide.color = new Color(orbitBarrageProjectileColor.r, orbitBarrageProjectileColor.g, orbitBarrageProjectileColor.b, Mathf.Lerp(0.04f, 0.26f, progress));
            }
        }
    }

    private void EnsureOrbitBarrageTelegraph()
    {
        int desired = Mathf.Max(4, orbitBarrageProjectileCount);
        if (orbitBarrageTelegraphRoot == null)
        {
            orbitBarrageTelegraphRoot = new GameObject("OrbitBarrageTelegraph");
            orbitBarrageTelegraphRoot.SetActive(false);

            GameObject outerRing = new GameObject("OrbitBarrageOuterRing");
            outerRing.transform.SetParent(orbitBarrageTelegraphRoot.transform, false);
            orbitBarrageRingRenderer = outerRing.AddComponent<SpriteRenderer>();
            orbitBarrageRingRenderer.sprite = CircleSpriteProvider.Get();
            orbitBarrageRingRenderer.drawMode = SpriteDrawMode.Sliced;
            orbitBarrageRingRenderer.sortingOrder = 14;

            GameObject innerRing = new GameObject("OrbitBarrageInnerRing");
            innerRing.transform.SetParent(orbitBarrageTelegraphRoot.transform, false);
            orbitBarrageInnerRingRenderer = innerRing.AddComponent<SpriteRenderer>();
            orbitBarrageInnerRingRenderer.sprite = CircleSpriteProvider.Get();
            orbitBarrageInnerRingRenderer.drawMode = SpriteDrawMode.Sliced;
            orbitBarrageInnerRingRenderer.sortingOrder = 14;
        }

        while (orbitBarrageTickRenderers.Count < desired)
        {
            GameObject go = new GameObject($"OrbitBarrageWarn_{orbitBarrageTickRenderers.Count}");
            go.transform.SetParent(orbitBarrageTelegraphRoot.transform, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SquareSpriteProvider.Get();
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.sortingOrder = 15;
            orbitBarrageTickRenderers.Add(sr);
        }

        while (orbitBarrageTickRenderers.Count > desired)
        {
            int last = orbitBarrageTickRenderers.Count - 1;
            SpriteRenderer sr = orbitBarrageTickRenderers[last];
            orbitBarrageTickRenderers.RemoveAt(last);
            if (sr != null)
            {
                Destroy(sr.gameObject);
            }
        }

        int desiredGuides = Mathf.Max(8, desired * 2);
        while (orbitBarrageGuideRenderers.Count < desiredGuides)
        {
            GameObject go = new GameObject($"OrbitBarrageGuide_{orbitBarrageGuideRenderers.Count}");
            go.transform.SetParent(orbitBarrageTelegraphRoot.transform, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SquareSpriteProvider.Get();
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.sortingOrder = 13;
            orbitBarrageGuideRenderers.Add(sr);
        }

        while (orbitBarrageGuideRenderers.Count > desiredGuides)
        {
            int last = orbitBarrageGuideRenderers.Count - 1;
            SpriteRenderer sr = orbitBarrageGuideRenderers[last];
            orbitBarrageGuideRenderers.RemoveAt(last);
            if (sr != null)
            {
                Destroy(sr.gameObject);
            }
        }
    }

    private void HideOrbitBarrageTelegraph()
    {
        if (orbitBarrageTelegraphRoot != null && orbitBarrageTelegraphRoot.activeSelf)
        {
            orbitBarrageTelegraphRoot.SetActive(false);
        }
    }

    private void FireOrbitBarrage()
    {
        int count = Mathf.Max(4, orbitBarrageProjectileCount);
        float radius = Mathf.Max(0.65f, orbitBarrageSpawnRadius);
        float offset = Random.Range(0f, 360f);
        for (int i = 0; i < count; i++)
        {
            float angleDeg = offset + (360f / count) * i;
            Vector2 radial = new Vector2(Mathf.Cos(angleDeg * Mathf.Deg2Rad), Mathf.Sin(angleDeg * Mathf.Deg2Rad));
            Vector2 spawn = ClampToArena(orbitBarrageCenter + radial * radius);
            Vector2 inward = (orbitBarrageCenter - spawn).sqrMagnitude > 0.001f ? (orbitBarrageCenter - spawn).normalized : -radial;
            Vector2 direction = Rotate(inward, 58f * orbitBarrageDirectionSign);
            SpawnOrbitBarrageMuzzleCue(spawn, direction, i);
            CreateProjectile(spawn, direction, orbitBarrageProjectileColor, expansionShootProjectileSize * 0.86f, orbitBarrageProjectileSpeedMultiplier);
        }

        SpawnLevelTwoRadialBurst(orbitBarrageCenter, radius * 0.55f, orbitBarrageProjectileColor, "OrbitBarrage");
        GlitchAudioManager.PlayEnemyOrbitBarrageFire(new Vector3(orbitBarrageCenter.x, orbitBarrageCenter.y, transform.position.z));
    }

    private void SpawnOrbitBarrageMuzzleCue(Vector2 position, Vector2 direction, int index)
    {
        Color cueColor = index % 2 == 0 ? orbitBarrageProjectileColor : new Color(1f, 0.56f, 0.95f, 1f);
        GameObject ray = new GameObject($"OrbitBarrageMuzzleCue_{index}");
        ray.transform.position = new Vector3(position.x, position.y, 0f);
        SpriteRenderer sr = ray.AddComponent<SpriteRenderer>();
        sr.sprite = SquareSpriteProvider.Get();
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.sortingOrder = 18;
        sr.color = cueColor;
        sr.size = new Vector2(0.52f, 0.09f);
        ray.AddComponent<AnomalyStateRayFx>().Configure(sr, direction, 0.74f, 0.22f);
        Destroy(ray, 0.32f);
    }

    private void RecordReplaySample()
    {
        if (player == null)
        {
            return;
        }

        replaySampleTimer += Time.deltaTime;
        if (replaySampleTimer < Mathf.Max(0.03f, replaySampleInterval))
        {
            return;
        }

        replaySampleTimer = 0f;
        replaySamples.Add(new ReplaySample
        {
            position = player.GetPosition(),
            time = Time.time
        });

        float cutoff = Time.time - Mathf.Max(0.5f, replayMemorySeconds);
        while (replaySamples.Count > 0 && replaySamples[0].time < cutoff)
        {
            replaySamples.RemoveAt(0);
        }
    }

    private void SpawnReplayPredatorEchoes()
    {
        if (replaySamples.Count <= 1 || player == null)
        {
            return;
        }

        int count = Mathf.Clamp(replayPredatorEchoCount, 3, replaySamples.Count);
        List<Vector2> ghostPath = new List<Vector2>(count);
        for (int i = 0; i < count; i++)
        {
            int index = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(0, replaySamples.Count - 1, i / Mathf.Max(1f, count - 1f))), 0, replaySamples.Count - 1);
            ghostPath.Add(ClampToArena(replaySamples[index].position));
        }

        GameObject ghost = new GameObject("ReplayPredatorGhost");
        ReplayPredatorGhostFx fx = ghost.AddComponent<ReplayPredatorGhostFx>();
        fx.Configure(
            player,
            gameManager,
            ghostPath,
            Mathf.Max(0.15f, replayPredatorEchoRadius),
            Mathf.Max(0.2f, replayPredatorTelegraphSeconds),
            Mathf.Max(0.35f, replayPredatorGhostTravelSeconds),
            replayPredatorColor);

        SpawnLevelTwoRadialBurst(ghostPath[0], 1.1f, replayPredatorColor, "ReplayPredator");
    }

    private void SpawnChecksumLattice()
    {
        if (player == null || gameManager == null || gameManager.IsGameOver || checksumLatticeFx != null)
        {
            return;
        }

        List<ChecksumNodePlan> plans = BuildChecksumLatticePlans();
        int desiredCount = Mathf.Clamp(checksumLatticeNodeCount, 3, 6);
        if (plans.Count < desiredCount)
        {
            plans = CreateFallbackChecksumNodes(desiredCount);
        }

        int count = Mathf.Min(desiredCount, plans.Count);
        if (count < 3)
        {
            return;
        }

        Vector2[] nodes = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            nodes[i] = plans[i].position;
        }

        int[] sequence = BuildChecksumSequence(plans, count);
        GameObject root = new GameObject("ChecksumLattice");
        checksumLatticeFx = root.AddComponent<ChecksumLatticeFx>();
        checksumLatticeFx.Configure(
            this,
            player,
            gameManager,
            nodes,
            sequence,
            checksumLatticePrepSeconds,
            checksumLatticeDuration,
            checksumLatticeNodeRadius,
            checksumLatticeNodeColor,
            checksumLatticeActiveColor,
            checksumLatticeFailColor,
            navOrigin,
            navSize);

        SpawnLevelTwoRadialBurst(player.GetPosition(), 1.1f, checksumLatticeActiveColor, "ChecksumLattice");
    }

    private List<ChecksumNodePlan> BuildChecksumLatticePlans()
    {
        List<ChecksumNodePlan> plans = new List<ChecksumNodePlan>();
        if (player == null)
        {
            return plans;
        }

        Vector2 playerPos = player.GetPosition();
        Vector2 enemyPos = GetCurrentPosition();
        Vector2 velocity = player.CurrentVelocity;
        Vector2 awayFromEnemy = (playerPos - enemyPos).sqrMagnitude > 0.001f ? (playerPos - enemyPos).normalized : lastMoveDirection;
        Vector2 movementDir = velocity.sqrMagnitude > 0.05f ? velocity.normalized : awayFromEnemy;
        int probes = Mathf.Max(8, checksumLatticeProbeCount);
        float radius = Mathf.Max(2f, checksumLatticeSearchRadius);

        for (int i = 0; i < probes; i++)
        {
            float angle = (360f / probes) * i + Random.Range(-7f, 7f);
            Vector2 direction = Rotate(Vector2.right, angle).normalized;
            float ring = i % 3 == 0 ? 0.72f : i % 3 == 1 ? 0.92f : 1.12f;
            Vector2 candidate = ClampPointToArenaWithMargin(playerPos + direction * radius * ring, agentRadius + 0.5f);
            if (!IsWalkableWorld(candidate) || Vector2.Distance(candidate, playerPos) < 1.4f)
            {
                continue;
            }

            float directFromPlayer = HasDirectPath(playerPos, candidate) ? 1f : 0.25f;
            float directFromEnemy = HasDirectPath(enemyPos, candidate) ? 0.3f : 1f;
            float movementAlignment = Mathf.Max(0f, Vector2.Dot(direction, movementDir));
            float escapeAlignment = Mathf.Max(0f, Vector2.Dot(direction, awayFromEnemy));
            float enemyDistance = Mathf.Clamp01(Vector2.Distance(candidate, enemyPos) / Mathf.Max(1f, radius + 2f));
            float playerDistance = Mathf.Abs(Vector2.Distance(candidate, playerPos) - radius) * 0.12f;
            float score = directFromPlayer * 2.1f + directFromEnemy * 0.9f + movementAlignment * 1.15f + escapeAlignment * 0.85f + enemyDistance - playerDistance;

            plans.Add(new ChecksumNodePlan
            {
                position = candidate,
                score = score,
                checksum = Mathf.Abs(Mathf.RoundToInt(candidate.x * 31f) ^ Mathf.RoundToInt(candidate.y * 47f) ^ (i * 193))
            });
        }

        plans.Sort((a, b) => b.score.CompareTo(a.score));
        return FilterChecksumNodePlans(plans);
    }

    private List<ChecksumNodePlan> FilterChecksumNodePlans(List<ChecksumNodePlan> sortedPlans)
    {
        List<ChecksumNodePlan> filtered = new List<ChecksumNodePlan>();
        float minDistance = Mathf.Max(1.2f, checksumLatticeNodeRadius * 3.2f);
        for (int i = 0; i < sortedPlans.Count; i++)
        {
            ChecksumNodePlan plan = sortedPlans[i];
            bool tooClose = false;
            for (int j = 0; j < filtered.Count; j++)
            {
                if (Vector2.Distance(plan.position, filtered[j].position) < minDistance)
                {
                    tooClose = true;
                    break;
                }
            }

            if (!tooClose)
            {
                filtered.Add(plan);
            }
        }

        return filtered;
    }

    private List<ChecksumNodePlan> CreateFallbackChecksumNodes(int count)
    {
        List<ChecksumNodePlan> fallback = new List<ChecksumNodePlan>();
        if (player == null)
        {
            return fallback;
        }

        Vector2 playerPos = player.GetPosition();
        Vector2 enemyPos = GetCurrentPosition();
        Vector2 baseDir = (playerPos - enemyPos).sqrMagnitude > 0.001f ? (playerPos - enemyPos).normalized : lastMoveDirection;
        float radius = Mathf.Max(2.2f, checksumLatticeSearchRadius * 0.82f);
        for (int i = 0; i < count; i++)
        {
            float angle = (360f / count) * i + 25f;
            Vector2 pos = ClampPointToArenaWithMargin(playerPos + Rotate(baseDir, angle) * radius, agentRadius + 0.5f);
            fallback.Add(new ChecksumNodePlan
            {
                position = pos,
                score = count - i,
                checksum = Mathf.Abs(Mathf.RoundToInt(pos.x * 37f) ^ Mathf.RoundToInt(pos.y * 53f) ^ (i * 211))
            });
        }

        return fallback;
    }

    private int[] BuildChecksumSequence(List<ChecksumNodePlan> plans, int count)
    {
        int[] sequence = new int[count];
        bool[] used = new bool[count];
        int current = 0;
        int bestChecksum = -1;
        for (int i = 0; i < count; i++)
        {
            int checksum = plans[i].checksum % 997;
            if (checksum > bestChecksum)
            {
                bestChecksum = checksum;
                current = i;
            }
        }

        for (int step = 0; step < count; step++)
        {
            sequence[step] = current;
            used[current] = true;

            int next = -1;
            float bestScore = float.MinValue;
            for (int i = 0; i < count; i++)
            {
                if (used[i])
                {
                    continue;
                }

                float distance = Vector2.Distance(plans[current].position, plans[i].position);
                float checksumDelta = Mathf.Abs((plans[current].checksum % 17) - (plans[i].checksum % 17));
                float score = distance * 0.7f + checksumDelta * 0.18f + plans[i].score * 0.35f;
                if (score > bestScore)
                {
                    bestScore = score;
                    next = i;
                }
            }

            if (next >= 0)
            {
                current = next;
            }
        }

        return sequence;
    }

    public void ResolveChecksumLattice(bool success, int solvedNodes, Vector2 resolvePosition)
    {
        checksumLatticeFx = null;
        if (player == null)
        {
            return;
        }

        if (success)
        {
            ApplyContainmentLock(GetCurrentPosition(), Mathf.Max(0.2f, checksumLatticeRewardStun));
            player.AddFirewallCharge(10f);
            SpawnLevelTwoRadialBurst(resolvePosition, 1.45f, checksumLatticeActiveColor, "ChecksumLatticeSolved");
            return;
        }

        Vector2 pull = (GetCurrentPosition() - player.GetPosition()).sqrMagnitude > 0.001f
            ? (GetCurrentPosition() - player.GetPosition()).normalized
            : Vector2.zero;
        player.ApplyMovementSlow(checksumLatticePenaltySlowMultiplier, checksumLatticePenaltyDuration);
        player.ApplyExternalDisplacement(pull * checksumLatticePenaltyPull);
        SpawnLevelTwoRadialBurst(player.GetPosition(), 1.65f, checksumLatticeFailColor, "ChecksumLatticeFail");
    }

    private void QueueInputDesyncEcho()
    {
        if (player == null)
        {
            return;
        }

        Vector2 velocity = player.CurrentVelocity;
        Vector2 direction = velocity.sqrMagnitude > 0.05f ? velocity.normalized : lastMoveDirection;
        Vector2 origin = player.GetPosition();
        SpawnInputDesyncMarker(origin, direction, Mathf.Max(0.08f, inputDesyncDelay));
        StartCoroutine(InputDesyncEchoRoutine(direction));
    }

    private IEnumerator InputDesyncEchoRoutine(Vector2 direction)
    {
        float delay = Mathf.Max(0.05f, inputDesyncDelay);
        yield return new WaitForSeconds(delay);

        if (currentState != AnomalyState.InputDesync || player == null || gameManager == null || gameManager.IsGameOver)
        {
            yield break;
        }

        Vector2 push = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
        player.ApplyExternalDisplacement(push * Mathf.Max(0.1f, inputDesyncDisplacement));
        player.ApplyMovementSlow(inputDesyncSlowMultiplier, inputDesyncSlowDuration);
        SpawnLevelTwoRadialBurst(player.GetPosition(), 0.9f, inputDesyncColor, "InputDesync");
    }

    private void SpawnInputDesyncMarker(Vector2 origin, Vector2 direction, float duration)
    {
        GameObject marker = new GameObject("InputDesyncGhostInput");
        marker.transform.position = new Vector3(origin.x, origin.y, 0f);
        SpriteRenderer sr = marker.AddComponent<SpriteRenderer>();
        sr.sprite = SquareSpriteProvider.Get();
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.sortingOrder = 18;
        sr.size = new Vector2(0.72f, 0.08f);
        sr.color = inputDesyncColor;
        marker.AddComponent<AnomalyStateRayFx>().Configure(sr, direction, 1.1f, duration);
        Destroy(marker, duration + 0.08f);
    }

    private void BuildMapRecompileTargets()
    {
        mapRecompileTargets.Clear();
        List<Collider2D> candidates = CollectRecompilableObstacles();
        int desired = DecideMapRecompileObstacleCount(candidates.Count);
        List<Vector2> tacticalSlots = BuildMapRecompileBlockSlots(desired);
        if (tacticalSlots.Count <= 0)
        {
            return;
        }

        for (int i = 0; i < tacticalSlots.Count && candidates.Count > 0; i++)
        {
            Vector2 slot = tacticalSlots[i];
            int bestIndex = FindBestRecompileObstacleIndex(candidates, slot);
            if (bestIndex < 0)
            {
                continue;
            }

            Collider2D col = candidates[bestIndex];
            candidates.RemoveAt(bestIndex);
            Transform targetTransform = col.transform;
            Vector2 start = targetTransform.position;
            if (!TryFindValidRecompileSlot(slot, col, mapRecompileTargets, out Vector2 target))
            {
                continue;
            }

            SpriteRenderer[] renderers = targetTransform.GetComponentsInChildren<SpriteRenderer>(true);
            Color[] colors = new Color[renderers.Length];
            for (int r = 0; r < renderers.Length; r++)
            {
                colors[r] = renderers[r] != null ? renderers[r].color : Color.white;
            }

            mapRecompileTargets.Add(new RecompileTarget
            {
                transform = targetTransform,
                rigidbody = targetTransform.GetComponent<Rigidbody2D>(),
                start = start,
                target = target,
                renderers = renderers,
                colors = colors
            });
        }
    }

    private int DecideMapRecompileObstacleCount(int availableObstacles)
    {
        int min = Mathf.Max(1, Mathf.Min(mapRecompileMinObstacleCount, mapRecompileMaxObstacleCount));
        int max = Mathf.Max(min, Mathf.Max(mapRecompileMinObstacleCount, mapRecompileMaxObstacleCount));
        if (player == null || availableObstacles <= 0)
        {
            return Mathf.Min(min, Mathf.Max(0, availableObstacles));
        }

        Vector2 playerPos = player.GetPosition();
        Vector2 enemyPos = GetCurrentPosition();
        float distance = Vector2.Distance(playerPos, enemyPos);
        float playerSpeed = player.CurrentVelocity.magnitude;
        bool enemyHasDirectLine = HasDirectPath(enemyPos, playerPos);
        bool playerIsEscapingFast = playerSpeed >= Mathf.Max(1f, mapRecompileHighSpeedThreshold);
        bool playerFar = distance >= Mathf.Max(2f, mapRecompileFarDistanceThreshold);

        int desired = min;
        if (enemyHasDirectLine)
        {
            desired += 1;
        }
        if (playerIsEscapingFast)
        {
            desired += 1;
        }
        if (playerFar)
        {
            desired += 1;
        }
        if (!enemyHasDirectLine && playerFar)
        {
            desired += 1;
        }

        float arenaScale = Mathf.Min(navSize.x, navSize.y);
        if (arenaScale > 16f && availableObstacles >= max)
        {
            desired += 1;
        }

        return Mathf.Clamp(desired, 1, Mathf.Min(max, availableObstacles));
    }

    private List<Vector2> BuildMapRecompileBlockSlots(int desired)
    {
        List<Vector2> slots = new List<Vector2>();
        if (player == null || rb == null)
        {
            return slots;
        }

        Vector2 playerPos = player.GetPosition();
        Vector2 enemyPos = GetCurrentPosition();
        Vector2 enemyToPlayer = playerPos - enemyPos;
        if (enemyToPlayer.sqrMagnitude < 0.01f)
        {
            enemyToPlayer = player.CurrentVelocity.sqrMagnitude > 0.01f ? -player.CurrentVelocity : lastMoveDirection;
        }

        Vector2 approach = enemyToPlayer.sqrMagnitude > 0.001f ? enemyToPlayer.normalized : Vector2.right;
        Vector2 playerVelocity = player.CurrentVelocity;
        Vector2 escapeDirection = playerVelocity.sqrMagnitude > 0.1f ? playerVelocity.normalized : approach;
        if (Vector2.Dot(escapeDirection, approach) < -0.25f)
        {
            escapeDirection = Vector2.Lerp(escapeDirection, approach, 0.58f).normalized;
        }
        Vector2 barrierCenter = playerPos + escapeDirection * Mathf.Max(1.4f, mapRecompileBlockDistanceFromPlayer);
        Vector2 side = new Vector2(-escapeDirection.y, escapeDirection.x);
        int count = Mathf.Max(1, desired);
        float spacing = Mathf.Max(0.45f, mapRecompileBlockSpacing) * Mathf.Lerp(0.9f, 1.18f, Mathf.Clamp01((count - 2f) / 3f));

        for (int i = 0; i < count; i++)
        {
            float gapHalfWidth = Mathf.Max(agentRadius * 1.7f, 0.9f);
            float laneMagnitude = Mathf.Floor(i * 0.5f) + 1f;
            float laneSign = i % 2 == 0 ? -1f : 1f;
            float lateralOffset = laneSign * laneMagnitude * Mathf.Max(spacing, gapHalfWidth);
            bool secondRow = count >= 4 && (i == 1 || i == count - 2);
            float rowOffset = secondRow ? 0.82f : 0f;
            Vector2 candidate = ClampPointToArenaWithMargin(
                barrierCenter + side * lateralOffset + escapeDirection * rowOffset,
                agentRadius + 0.35f);

            if (Vector2.Distance(candidate, playerPos) < 1.15f)
            {
                candidate = ClampPointToArenaWithMargin(candidate + escapeDirection * 1.15f, agentRadius + 0.35f);
            }

            if (Vector2.Distance(candidate, enemyPos) < 1.0f)
            {
                candidate = ClampPointToArenaWithMargin(candidate + side * laneSign * spacing, agentRadius + 0.35f);
            }

            slots.Add(candidate);
        }

        return slots;
    }

    private List<Collider2D> CollectRecompilableObstacles()
    {
        Collider2D[] colliders = FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
        List<Collider2D> result = new List<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (IsRecompilableObstacle(colliders[i]))
            {
                result.Add(colliders[i]);
            }
        }

        return result;
    }

    private int FindBestRecompileObstacleIndex(List<Collider2D> candidates, Vector2 target)
    {
        int bestIndex = -1;
        float bestScore = float.NegativeInfinity;
        Vector2 playerPos = player != null ? player.GetPosition() : target;
        Vector2 enemyPos = GetCurrentPosition();
        float maxPull = Mathf.Max(1.5f, mapRecompileMaxObstaclePullDistance);

        for (int i = 0; i < candidates.Count; i++)
        {
            Collider2D col = candidates[i];
            if (col == null)
            {
                continue;
            }

            Vector2 pos = col.transform.position;
            float pullDistance = Vector2.Distance(pos, target);
            if (pullDistance > maxPull)
            {
                continue;
            }

            float playerSafety = Vector2.Distance(pos, playerPos);
            float enemySafety = Vector2.Distance(pos, enemyPos);
            float corridorAlignment = DistancePointToSegment(pos, enemyPos, playerPos);
            float size = Mathf.Max(col.bounds.size.x, col.bounds.size.y);
            float score = 0f;
            score -= pullDistance * 1.35f;
            score -= corridorAlignment * 0.28f;
            score += Mathf.Clamp(playerSafety, 0f, 7f) * 0.25f;
            score += Mathf.Clamp(enemySafety, 0f, 7f) * 0.12f;
            score += size * 0.55f;
            if (playerSafety < 2.2f)
            {
                score -= 2.4f;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private static float DistancePointToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 segment = b - a;
        float lenSq = segment.sqrMagnitude;
        if (lenSq <= 0.0001f)
        {
            return Vector2.Distance(point, a);
        }

        float t = Mathf.Clamp01(Vector2.Dot(point - a, segment) / lenSq);
        Vector2 projection = a + segment * t;
        return Vector2.Distance(point, projection);
    }

    private bool TryFindValidRecompileSlot(Vector2 desiredSlot, Collider2D movingCollider, List<RecompileTarget> alreadyPlanned, out Vector2 slot)
    {
        slot = ClampObstacleCenterToArena(desiredSlot, movingCollider.bounds.extents);
        Vector2 playerPos = player != null ? player.GetPosition() : desiredSlot;
        Vector2 enemyPos = GetCurrentPosition();
        Vector2 fromEnemy = playerPos - enemyPos;
        Vector2 approach = fromEnemy.sqrMagnitude > 0.001f ? fromEnemy.normalized : Vector2.right;
        Vector2 side = new Vector2(-approach.y, approach.x);
        Vector2[] offsets =
        {
            Vector2.zero,
            side * 0.55f,
            -side * 0.55f,
            side * 1.1f,
            -side * 1.1f,
            -approach * 0.65f,
            approach * 0.65f
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            Vector2 candidate = ClampObstacleCenterToArena(desiredSlot + offsets[i], movingCollider.bounds.extents);
            if (IsValidRecompileSlot(candidate, movingCollider, alreadyPlanned))
            {
                slot = candidate;
                return true;
            }
        }

        return false;
    }

    private bool IsValidRecompileSlot(Vector2 candidate, Collider2D movingCollider, List<RecompileTarget> alreadyPlanned)
    {
        if (player != null && Vector2.Distance(candidate, player.GetPosition()) < 1.2f)
        {
            return false;
        }

        if (Vector2.Distance(candidate, GetCurrentPosition()) < 1.0f)
        {
            return false;
        }

        for (int i = 0; i < alreadyPlanned.Count; i++)
        {
            if (Vector2.Distance(candidate, alreadyPlanned[i].target) < Mathf.Max(0.45f, mapRecompileBlockSpacing * 0.72f))
            {
                return false;
            }
        }

        Bounds bounds = movingCollider.bounds;
        if (player != null)
        {
            float corridorClearance = Mathf.Max(bounds.extents.x, bounds.extents.y) + agentRadius + 0.35f;
            if (DistancePointToSegment(candidate, GetCurrentPosition(), player.GetPosition()) < corridorClearance)
            {
                return false;
            }
        }

        Vector2 size = new Vector2(
            Mathf.Max(0.35f, bounds.size.x * 0.82f),
            Mathf.Max(0.35f, bounds.size.y * 0.82f));
        Collider2D[] hits = Physics2D.OverlapBoxAll(candidate, size, movingCollider.transform.eulerAngles.z, obstacleMask);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.isTrigger || hit == movingCollider || hit.transform.IsChildOf(movingCollider.transform))
            {
                continue;
            }

            if (hit.GetComponent<PlayerController>() != null ||
                hit.GetComponent<EnemyController>() != null ||
                hit.GetComponent<SplitAnomalyCloneController>() != null)
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private bool IsRecompilableObstacle(Collider2D col)
    {
        if (!IsBlockingCollider(col) || !CanDestroyThisCollider(col))
        {
            return false;
        }

        if (((1 << col.gameObject.layer) & obstacleMask.value) == 0)
        {
            return false;
        }

        Bounds b = col.bounds;
        return Mathf.Max(b.size.x, b.size.y) <= Mathf.Min(navSize.x, navSize.y) * 0.45f;
    }

    private Vector2 ClampObstacleCenterToArena(Vector2 point, Vector3 extents)
    {
        float marginX = Mathf.Max(agentRadius + 0.15f, extents.x + 0.18f);
        float marginY = Mathf.Max(agentRadius + 0.15f, extents.y + 0.18f);
        float minX = navOrigin.x + marginX;
        float maxX = navOrigin.x + navSize.x - marginX;
        float minY = navOrigin.y + marginY;
        float maxY = navOrigin.y + navSize.y - marginY;

        return new Vector2(
            Mathf.Clamp(point.x, minX, maxX),
            Mathf.Clamp(point.y, minY, maxY));
    }

    private Vector2 ClampPointToArenaWithMargin(Vector2 point, float margin)
    {
        float safeMargin = Mathf.Max(0.1f, margin);
        return new Vector2(
            Mathf.Clamp(point.x, navOrigin.x + safeMargin, navOrigin.x + navSize.x - safeMargin),
            Mathf.Clamp(point.y, navOrigin.y + safeMargin, navOrigin.y + navSize.y - safeMargin));
    }

    private void UpdateMapRecompileTelegraph(float progress)
    {
        EnsureMapRecompileTelegraph();
        for (int i = 0; i < mapRecompileTelegraphRenderers.Count; i++)
        {
            SpriteRenderer sr = mapRecompileTelegraphRenderers[i];
            if (sr == null)
            {
                continue;
            }

            if (i >= mapRecompileTargets.Count)
            {
                sr.color = Color.clear;
                continue;
            }

            RecompileTarget target = mapRecompileTargets[i];
            Vector2 start = target.start;
            Vector2 end = target.target;
            Vector2 mid = (start + end) * 0.5f;
            Vector2 delta = end - start;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 12f + i);
            sr.transform.position = new Vector3(mid.x, mid.y, 0f);
            sr.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            sr.size = new Vector2(Mathf.Max(0.2f, delta.magnitude), Mathf.Lerp(0.06f, 0.16f, progress));
            sr.color = new Color(mapRecompileColor.r, mapRecompileColor.g, mapRecompileColor.b, Mathf.Lerp(0.12f, 0.82f, progress) * (0.7f + pulse * 0.3f));
        }
    }

    private void EnsureMapRecompileTelegraph()
    {
        if (mapRecompileTelegraphRoot == null)
        {
            mapRecompileTelegraphRoot = new GameObject("MapRecompileTelegraph");
        }

        while (mapRecompileTelegraphRenderers.Count < mapRecompileTargets.Count)
        {
            GameObject go = new GameObject($"MapRecompileLine_{mapRecompileTelegraphRenderers.Count}");
            go.transform.SetParent(mapRecompileTelegraphRoot.transform, false);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SquareSpriteProvider.Get();
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.sortingOrder = 18;
            mapRecompileTelegraphRenderers.Add(sr);
        }
    }

    private void HideMapRecompileTelegraph()
    {
        for (int i = 0; i < mapRecompileTelegraphRenderers.Count; i++)
        {
            if (mapRecompileTelegraphRenderers[i] != null)
            {
                mapRecompileTelegraphRenderers[i].color = Color.clear;
            }
        }
    }

    private IEnumerator ExecuteMapRecompileRoutine()
    {
        if (mapRecompileTargets.Count <= 0)
        {
            yield break;
        }

        float duration = Mathf.Max(0.08f, mapRecompileMoveSeconds);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            for (int i = 0; i < mapRecompileTargets.Count; i++)
            {
                RecompileTarget target = mapRecompileTargets[i];
                if (target.transform == null)
                {
                    continue;
                }

                Vector2 pos = Vector2.Lerp(target.start, target.target, t);
                if (target.rigidbody != null)
                {
                    target.rigidbody.linearVelocity = Vector2.zero;
                    target.rigidbody.MovePosition(pos);
                }
                else
                {
                    target.transform.position = new Vector3(pos.x, pos.y, target.transform.position.z);
                }

                for (int r = 0; r < target.renderers.Length; r++)
                {
                    if (target.renderers[r] != null)
                    {
                        target.renderers[r].color = Color.Lerp(target.colors[r], mapRecompileColor, Mathf.Sin(t * Mathf.PI) * 0.78f);
                    }
                }
            }

            yield return null;
        }

        for (int i = 0; i < mapRecompileTargets.Count; i++)
        {
            RecompileTarget target = mapRecompileTargets[i];
            for (int r = 0; r < target.renderers.Length; r++)
            {
                if (target.renderers[r] != null)
                {
                    target.renderers[r].color = target.colors[r];
                }
            }
        }

        BuildNavigationGrid();
        pathWorld.Clear();
        pathIndex = 0;
        mapRecompileTargets.Clear();
    }

    private void SpawnSignalPossessionLure()
    {
        if (player == null || activeSignalPossessionLure != null)
        {
            return;
        }

        Vector2 playerPos = player.GetPosition();
        Vector2 dir = Random.insideUnitCircle;
        if (dir.sqrMagnitude < 0.01f)
        {
            dir = Vector2.right;
        }

        Vector2 position = ClampToArena(playerPos + dir.normalized * Random.Range(2.0f, 3.3f));
        GameObject lure = new GameObject("SignalPossessionLure");
        lure.transform.position = new Vector3(position.x, position.y, 0f);
        SpriteRenderer sr = lure.AddComponent<SpriteRenderer>();
        sr.sprite = CircleSpriteProvider.Get();
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.sortingOrder = 18;
        activeSignalPossessionLure = lure.AddComponent<SignalPossessionLure>();
        activeSignalPossessionLure.Configure(
            this,
            player,
            sr,
            Mathf.Max(0.25f, signalPossessionArmSeconds),
            Mathf.Max(0.5f, signalPossessionLifetime),
            Mathf.Max(0.4f, signalPossessionRadius),
            signalPossessionColor);
    }

    public void DetonateSignalPossession(Vector2 position)
    {
        activeSignalPossessionLure = null;
        if (currentState != AnomalyState.SignalPossession)
        {
            return;
        }

        SpawnLevelTwoRadialBurst(position, signalPossessionRadius, signalPossessionColor, "SignalPossession");

        if (player != null && Vector2.Distance(player.GetPosition(), position) <= signalPossessionRadius)
        {
            player.ApplyMovementSlow(0.45f, 1.1f);
        }

        int count = Mathf.Max(4, signalPossessionProjectileCount);
        for (int i = 0; i < count; i++)
        {
            float angle = (Mathf.PI * 2f * i / count) + Random.Range(-0.08f, 0.08f);
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            CreateProjectile(position + dir * 0.35f, dir, signalPossessionColor, expansionShootProjectileSize * 0.86f, 0.86f);
        }

        Vector2 blinkDir = player != null ? ((Vector2)position - player.GetPosition()) : Random.insideUnitCircle;
        if (blinkDir.sqrMagnitude < 0.01f)
        {
            blinkDir = Random.insideUnitCircle.normalized;
        }

        Vector2 enemyTarget = ClampToArena(position + blinkDir.normalized * 1.6f);
        rb.position = enemyTarget;
        transform.position = new Vector3(enemyTarget.x, enemyTarget.y, transform.position.z);
        TriggerStatePulse();
    }

    private void BeginPhaseContract()
    {
        phaseContractActive = true;
        phaseContractActiveTimer = Mathf.Max(0.8f, phaseContractDuration);
        phaseContractRuleIndex = Random.Range(0, 3);
        ApplyContainmentLock(GetCurrentPosition(), Mathf.Max(0.2f, phaseContractGraceSeconds + 0.45f));
        EnsurePhaseContractVisual();
        UpdatePhaseContractVisual();
    }

    private void TickPhaseContract()
    {
        phaseContractActiveTimer -= Time.deltaTime;
        UpdatePhaseContractVisual();

        if (HasFailedPhaseContract())
        {
            ResolvePhaseContract(false);
            return;
        }

        if (phaseContractActiveTimer <= 0f)
        {
            ResolvePhaseContract(true);
        }
    }

    private bool HasFailedPhaseContract()
    {
        if (player == null)
        {
            return false;
        }

        float elapsed = Mathf.Max(0f, phaseContractDuration - phaseContractActiveTimer);
        if (elapsed < Mathf.Max(0.05f, phaseContractGraceSeconds))
        {
            return false;
        }

        switch (phaseContractRuleIndex)
        {
            case 0:
                return player.CurrentVelocity.magnitude < Mathf.Max(0.1f, phaseContractMinMoveSpeed);
            case 1:
                return Vector2.Distance(player.GetPosition(), GetCurrentPosition()) < Mathf.Max(0.5f, phaseContractMinEnemyDistance);
            case 2:
                Vector2 center = navOrigin + navSize * 0.5f;
                float forbiddenRadius = Mathf.Min(navSize.x, navSize.y) * 0.18f;
                return Vector2.Distance(player.GetPosition(), center) < Mathf.Max(1.2f, forbiddenRadius);
            default:
                return false;
        }
    }

    private void ResolvePhaseContract(bool success)
    {
        phaseContractActive = false;
        HidePhaseContractVisual();
        Vector2 position = player != null ? player.GetPosition() : GetCurrentPosition();

        if (success)
        {
            ApplyContainmentLock(GetCurrentPosition(), phaseContractRewardStun);
            if (player != null)
            {
                player.AddFirewallCharge(8f);
            }

            SpawnLevelTwoRadialBurst(position, 1.4f, checksumLatticeActiveColor, "PhaseContractSuccess");
            return;
        }

        if (player != null)
        {
            player.ApplyMovementSlow(phaseContractPenaltySlow, phaseContractPenaltyDuration);
        }

        SpawnLevelTwoRadialBurst(position, 1.65f, phaseContractColor, "PhaseContractFail");
    }

    private void EnsurePhaseContractVisual()
    {
        if (phaseContractRoot == null)
        {
            phaseContractRoot = new GameObject("PhaseContractVisual");
            GameObject textGo = new GameObject("PhaseContractText");
            textGo.transform.SetParent(phaseContractRoot.transform, false);
            phaseContractText = textGo.AddComponent<TextMesh>();
            phaseContractText.anchor = TextAnchor.MiddleCenter;
            phaseContractText.alignment = TextAlignment.Center;
            phaseContractText.characterSize = 0.22f;
            phaseContractText.fontSize = 36;

            GameObject ringGo = new GameObject("PhaseContractRing");
            ringGo.transform.SetParent(phaseContractRoot.transform, false);
            phaseContractRingRenderer = ringGo.AddComponent<SpriteRenderer>();
            phaseContractRingRenderer.sprite = CircleSpriteProvider.Get();
            phaseContractRingRenderer.drawMode = SpriteDrawMode.Sliced;
            phaseContractRingRenderer.sortingOrder = 18;

            GameObject lineGo = new GameObject("PhaseContractRuleLine");
            lineGo.transform.SetParent(phaseContractRoot.transform, false);
            phaseContractLineRenderer = lineGo.AddComponent<SpriteRenderer>();
            phaseContractLineRenderer.sprite = SquareSpriteProvider.Get();
            phaseContractLineRenderer.drawMode = SpriteDrawMode.Sliced;
            phaseContractLineRenderer.sortingOrder = 17;
        }

        if (!phaseContractRoot.activeSelf)
        {
            phaseContractRoot.SetActive(true);
        }
    }

    private void UpdatePhaseContractVisual()
    {
        EnsurePhaseContractVisual();
        if (phaseContractRoot == null || player == null)
        {
            return;
        }

        Vector2 playerPos = player.GetPosition();
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 9f);
        phaseContractRoot.transform.position = new Vector3(playerPos.x, playerPos.y + 1.75f, 0f);

        if (phaseContractText != null)
        {
            phaseContractText.text = GetPhaseContractLabel();
            phaseContractText.color = Color.Lerp(phaseContractColor, Color.white, pulse * 0.32f);
        }

        if (phaseContractRingRenderer != null)
        {
            if (phaseContractRuleIndex == 1)
            {
                float radius = Mathf.Max(0.5f, phaseContractMinEnemyDistance);
                Vector2 enemyPosition = GetCurrentPosition();
                bool safe = Vector2.Distance(playerPos, enemyPosition) >= radius;
                Color distanceColor = safe
                    ? new Color(0.42f, 1f, 0.72f, 1f)
                    : new Color(1f, 0.34f, 0.48f, 1f);
                phaseContractRingRenderer.transform.position = enemyPosition;
                phaseContractRingRenderer.size = Vector2.one * radius * 2f;
                phaseContractRingRenderer.color = new Color(
                    distanceColor.r,
                    distanceColor.g,
                    distanceColor.b,
                    0.2f + pulse * 0.18f);
            }
            else
            {
                phaseContractRingRenderer.transform.localPosition = Vector3.down * 1.75f;
                phaseContractRingRenderer.size = Vector2.one * Mathf.Lerp(1.1f, 1.45f, pulse);
                phaseContractRingRenderer.color = new Color(phaseContractColor.r, phaseContractColor.g, phaseContractColor.b, 0.24f + pulse * 0.28f);
            }
        }

        if (phaseContractLineRenderer != null)
        {
            if (phaseContractRuleIndex == 1)
            {
                Vector2 enemyPosition = GetCurrentPosition();
                Vector2 delta = playerPos - enemyPosition;
                bool safe = delta.magnitude >= Mathf.Max(0.5f, phaseContractMinEnemyDistance);
                Color distanceColor = safe
                    ? new Color(0.42f, 1f, 0.72f, 1f)
                    : new Color(1f, 0.34f, 0.48f, 1f);
                phaseContractLineRenderer.transform.position = (playerPos + enemyPosition) * 0.5f;
                phaseContractLineRenderer.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                phaseContractLineRenderer.size = new Vector2(delta.magnitude, 0.055f + pulse * 0.025f);
                phaseContractLineRenderer.color = new Color(distanceColor.r, distanceColor.g, distanceColor.b, 0.38f + pulse * 0.3f);
            }
            else
            {
                phaseContractLineRenderer.transform.localPosition = Vector3.down * 1.75f;
                phaseContractLineRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, Time.time * 70f);
                phaseContractLineRenderer.size = new Vector2(1.8f, 0.06f);
                phaseContractLineRenderer.color = new Color(phaseContractColor.r, phaseContractColor.g, phaseContractColor.b, 0.22f + pulse * 0.36f);
            }
        }
    }

    private string GetPhaseContractLabel()
    {
        int seconds = Mathf.CeilToInt(Mathf.Max(0f, phaseContractActiveTimer));
        float elapsed = Mathf.Max(0f, phaseContractDuration - phaseContractActiveTimer);
        bool preparing = elapsed < Mathf.Max(0.05f, phaseContractGraceSeconds);
        string prefix = preparing ? "PREPARATE" : "CONTRATO";
        switch (phaseContractRuleIndex)
        {
            case 0:
                return $"{prefix}: NO TE DETENGAS\n{seconds}s";
            case 1:
                return $"{prefix}: MANTEN DISTANCIA\n{seconds}s";
            case 2:
                return $"{prefix}: EVITA EL CENTRO\n{seconds}s";
            default:
                return $"{prefix}\n{seconds}s";
        }
    }

    private void HidePhaseContractVisual()
    {
        if (phaseContractRoot != null)
        {
            phaseContractRoot.SetActive(false);
        }
    }

    private void UpdateExpansionShootTelegraphVisual(float chargeProgress)
    {
        EnsureExpansionTelegraphVisuals();
        if (expansionTelegraphRoot == null)
        {
            return;
        }

        if (!expansionTelegraphRoot.activeSelf)
        {
            expansionTelegraphRoot.SetActive(true);
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Max(0.1f, expansionShootTelegraphPulseSpeed));
        float intensity = Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(chargeProgress));
        float alpha = Mathf.Clamp01((0.25f + pulse * 0.55f) * intensity);

        Color ringColor = expansionShootTelegraphColor;
        ringColor.a *= alpha * 0.95f;
        if (expansionTelegraphRing != null)
        {
            expansionTelegraphRing.color = ringColor;
            float scalePulse = 1f + pulse * 0.07f;
            expansionTelegraphRing.transform.localScale = new Vector3(scalePulse, scalePulse, 1f);
        }

        Color tickColor = expansionShootTelegraphColor;
        tickColor.a *= alpha;
        for (int i = 0; i < expansionTelegraphTicks.Count; i++)
        {
            SpriteRenderer tick = expansionTelegraphTicks[i];
            if (tick == null)
            {
                continue;
            }

            tick.color = tickColor;
        }
    }

    private void HideExpansionShootTelegraphVisual()
    {
        if (expansionTelegraphRoot != null && expansionTelegraphRoot.activeSelf)
        {
            expansionTelegraphRoot.SetActive(false);
        }
    }

    private void EnsureExpansionTelegraphVisuals()
    {
        int desiredTicks = Mathf.Max(4, expansionShootProjectileCount);

        if (expansionTelegraphRoot == null)
        {
            expansionTelegraphRoot = new GameObject("ExpansionShootTelegraph");
            expansionTelegraphRoot.transform.SetParent(transform, false);
            expansionTelegraphRoot.transform.localPosition = Vector3.zero;
            expansionTelegraphRoot.transform.localRotation = Quaternion.identity;
            expansionTelegraphRoot.transform.localScale = Vector3.one;
            expansionTelegraphRoot.SetActive(false);

            GameObject ring = new GameObject("Ring");
            ring.transform.SetParent(expansionTelegraphRoot.transform, false);
            SpriteRenderer ringRenderer = ring.AddComponent<SpriteRenderer>();
            ringRenderer.sprite = CircleSpriteProvider.Get();
            ringRenderer.drawMode = SpriteDrawMode.Sliced;
            ringRenderer.size = Vector2.one * (Mathf.Max(0.2f, expansionShootTelegraphRingRadius) * 2f);
            ringRenderer.color = expansionShootTelegraphColor;
            ringRenderer.sortingOrder = 12;
            expansionTelegraphRing = ringRenderer;
        }

        if (expansionTelegraphRing != null)
        {
            expansionTelegraphRing.size = Vector2.one * (Mathf.Max(0.2f, expansionShootTelegraphRingRadius) * 2f);
        }

        while (expansionTelegraphTicks.Count > desiredTicks)
        {
            int last = expansionTelegraphTicks.Count - 1;
            SpriteRenderer tick = expansionTelegraphTicks[last];
            expansionTelegraphTicks.RemoveAt(last);
            if (tick != null)
            {
                Destroy(tick.gameObject);
            }
        }

        while (expansionTelegraphTicks.Count < desiredTicks)
        {
            GameObject tickGo = new GameObject($"Tick_{expansionTelegraphTicks.Count}");
            tickGo.transform.SetParent(expansionTelegraphRoot.transform, false);
            SpriteRenderer tickRenderer = tickGo.AddComponent<SpriteRenderer>();
            tickRenderer.sprite = SquareSpriteProvider.Get();
            tickRenderer.drawMode = SpriteDrawMode.Sliced;
            tickRenderer.size = expansionShootTelegraphTickSize;
            tickRenderer.color = expansionShootTelegraphColor;
            tickRenderer.sortingOrder = 12;
            expansionTelegraphTicks.Add(tickRenderer);
        }

        float radius = Mathf.Max(0.2f, expansionShootTelegraphRingRadius);
        for (int i = 0; i < expansionTelegraphTicks.Count; i++)
        {
            SpriteRenderer tick = expansionTelegraphTicks[i];
            if (tick == null)
            {
                continue;
            }

            float angleDeg = (360f / expansionTelegraphTicks.Count) * i;
            Vector2 dir = new Vector2(Mathf.Cos(angleDeg * Mathf.Deg2Rad), Mathf.Sin(angleDeg * Mathf.Deg2Rad));
            tick.transform.localPosition = dir * radius;
            tick.transform.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
            tick.size = expansionShootTelegraphTickSize;
        }
    }

    private Vector2 GetStrategicTarget()
    {
        Vector2 enemyPosition = rb.position;
        Vector2 playerPosition = player.GetPosition();
        if (breachLureTimer > 0f)
        {
            return breachLureTarget;
        }

        switch (currentPattern)
        {
            case BehaviorPattern.DirectChase:
                if (currentState == AnomalyState.WeaveHunter)
                {
                    return GetWeaveHunterTarget(enemyPosition, playerPosition);
                }
                return playerPosition;
            case BehaviorPattern.PredictiveIntercept:
                return GetPredictiveTarget(enemyPosition, playerPosition);
            case BehaviorPattern.CutoffFlank:
                return GetCutoffTarget(enemyPosition, playerPosition);
            case BehaviorPattern.ErraticBurst:
                return erraticTarget;
            default:
                return playerPosition;
        }
    }

    private Vector2 GetPredictiveTarget(Vector2 enemyPosition, Vector2 playerPosition)
    {
        Vector2 playerVelocity = player.CurrentVelocity;
        float distance = Vector2.Distance(enemyPosition, playerPosition);
        float enemySpeed = Mathf.Max(0.1f, baseMoveSpeed);

        float leadTime = distance / enemySpeed;
        leadTime = Mathf.Clamp(leadTime, minLeadTime, maxLeadTime);
        return playerPosition + playerVelocity * leadTime;
    }

    private Vector2 GetCutoffTarget(Vector2 enemyPosition, Vector2 playerPosition)
    {
        Vector2 toPlayer = playerPosition - enemyPosition;
        Vector2 playerVelocity = player.CurrentVelocity;

        Vector2 velocityDir = playerVelocity.sqrMagnitude > 0.01f ? playerVelocity.normalized : toPlayer.normalized;
        if (velocityDir.sqrMagnitude < 0.0001f)
        {
            velocityDir = lastMoveDirection;
        }

        Vector2 side = new Vector2(-velocityDir.y, velocityDir.x) * flankSide;
        return playerPosition + velocityDir * flankLeadFactor + side * flankRadius;
    }

    private Vector2 GetWeaveHunterTarget(Vector2 enemyPosition, Vector2 playerPosition)
    {
        Vector2 toPlayer = playerPosition - enemyPosition;
        Vector2 chaseDir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : lastMoveDirection;
        if (chaseDir.sqrMagnitude < 0.0001f)
        {
            chaseDir = Vector2.right;
        }

        Vector2 side = new Vector2(-chaseDir.y, chaseDir.x) * weaveHunterSideSign;
        Vector2 velocityBias = player.CurrentVelocity * Mathf.Max(0f, weaveHunterPlayerVelocityBias);
        return playerPosition + side * Mathf.Max(0f, weaveHunterSideOffset) + velocityBias;
    }

    private void RefreshErraticTarget()
    {
        Vector2 randomOffset = Random.insideUnitCircle * erraticOffsetRadius;
        Vector2 forwardBias = player.CurrentVelocity.normalized * 1.2f;
        erraticTarget = player.GetPosition() + randomOffset + forwardBias;
    }

    private Vector2 ClampToArena(Vector2 point)
    {
        float margin = agentRadius + 0.1f;
        float minX = navOrigin.x + margin;
        float maxX = navOrigin.x + navSize.x - margin;
        float minY = navOrigin.y + margin;
        float maxY = navOrigin.y + navSize.y - margin;

        return new Vector2(
            Mathf.Clamp(point.x, minX, maxX),
            Mathf.Clamp(point.y, minY, maxY));
    }

    private void RebuildPathTo(Vector2 goalWorld)
    {
        repathTimer = 0f;
        lastPathGoal = goalWorld;

        Vector2Int start = WorldToCell(rb.position);
        Vector2Int goal = WorldToCell(goalWorld);

        if (!TryNearestWalkable(start, out start) || !TryNearestWalkable(goal, out goal))
        {
            pathWorld.Clear();
            pathIndex = 0;
            return;
        }

        if (!TryFindPath(start, goal, out List<Vector2Int> pathCells))
        {
            pathWorld.Clear();
            pathIndex = 0;
            return;
        }

        pathWorld.Clear();
        for (int i = 0; i < pathCells.Count; i++)
        {
            pathWorld.Add(CellToWorld(pathCells[i]));
        }

        pathIndex = 0;
        AdvancePathWaypoint();
    }

    private void ResetBlockedRepathHysteresis()
    {
        hasPendingBlockedRepathGoal = false;
        pendingBlockedRepathTimer = 0f;
        pendingBlockedRepathGoal = Vector2.zero;
        blockedPathCommitTimer = 0f;
        blockedOscillationCounter = 0;
        blockedOscillationTimer = 0f;
    }

    private Vector2 SelectSteeringTarget(Vector2 strategicTarget)
    {
        AdvancePathWaypoint();

        if (pathWorld.Count == 0 || pathIndex >= pathWorld.Count)
        {
            return strategicTarget;
        }

        int maxIndex = Mathf.Min(pathWorld.Count - 1, pathIndex + Mathf.Max(1, pathLookahead));
        int chosenIndex = pathIndex;

        for (int i = pathIndex; i <= maxIndex; i++)
        {
            if (!HasDirectPath(rb.position, pathWorld[i]))
            {
                break;
            }

            chosenIndex = i;
        }

        return pathWorld[chosenIndex];
    }

    private bool IsCurrentPathSegmentBlocked()
    {
        if (pathWorld.Count == 0 || pathIndex >= pathWorld.Count)
        {
            return false;
        }

        return !HasDirectPath(rb.position, pathWorld[pathIndex]);
    }

    private void AdvancePathWaypoint()
    {
        while (pathIndex < pathWorld.Count)
        {
            float dist = Vector2.Distance(rb.position, pathWorld[pathIndex]);
            if (dist > waypointReachDistance)
            {
                break;
            }

            pathIndex++;
        }
    }

    private bool HasDirectPath(Vector2 from, Vector2 to)
    {
        Vector2 delta = to - from;
        float distance = delta.magnitude;
        if (distance <= 0.01f)
        {
            return true;
        }

        Vector2 dir = delta / distance;
        RaycastHit2D hit = Physics2D.CircleCast(from, agentRadius, dir, distance, obstacleMask);
        return !IsBlockingCollider(hit.collider);
    }

    private Vector2 ApplyObstacleRepulsion(Vector2 desiredDirection)
    {
        Vector2 baseDir = desiredDirection.sqrMagnitude > 0.0001f ? desiredDirection.normalized : lastMoveDirection;
        Vector2 repulsion = Vector2.zero;

        int rays = Mathf.Max(3, repulsionRayCount);
        for (int i = 0; i < rays; i++)
        {
            float t = rays == 1 ? 0.5f : i / (float)(rays - 1);
            float angle = Mathf.Lerp(-repulsionSpreadAngle * 0.5f, repulsionSpreadAngle * 0.5f, t);
            Vector2 dir = Rotate(baseDir, angle);

            RaycastHit2D hit = Physics2D.CircleCast(rb.position, agentRadius, dir, repulsionProbeDistance, obstacleMask);
            if (!IsBlockingCollider(hit.collider))
            {
                continue;
            }

            float proximity = 1f - (hit.distance / repulsionProbeDistance);
            Vector2 away = (rb.position - hit.point).normalized;
            repulsion += away * proximity;
        }

        if (repulsion.sqrMagnitude < 0.0001f)
        {
            return baseDir;
        }

        Vector2 blended = (baseDir + repulsion.normalized * repulsionWeight).normalized;
        return blended;
    }

    private void UpdateStuckDetection(Vector2 strategicTarget)
    {
        stuckTimer += Time.deltaTime;
        if (stuckTimer < stuckCheckInterval)
        {
            return;
        }

        stuckTimer = 0f;

        float moved = Vector2.Distance(rb.position, stuckCheckPosition);
        stuckCheckPosition = rb.position;

        float targetDistance = Vector2.Distance(rb.position, strategicTarget);
        float progressThreshold = Mathf.Max(0.14f, stuckDistanceThreshold);
        bool lowProgress = moved < progressThreshold;
        bool needsChase = targetDistance >= Mathf.Max(0.45f, stuckTargetMinDistance * 0.55f);
        bool nearObstacle = IsNearBlockingObstacle(Mathf.Max(0.3f, stuckObstacleProbeRadius));

        if (!nearObstacle || !needsChase)
        {
            stuckConsecutiveChecks = 0;
            return;
        }

        if (!lowProgress)
        {
            stuckConsecutiveChecks = Mathf.Max(0, stuckConsecutiveChecks - 1);
            return;
        }

        stuckConsecutiveChecks++;

        if (currentState != AnomalyState.Destroyer && stuckConsecutiveChecks >= Mathf.Max(2, stuckChecksBeforeEmergencyDestroyer))
        {
            EnterEmergencyDestroyerFromStuck();
            return;
        }

        if (stuckConsecutiveChecks >= Mathf.Max(1, stuckChecksBeforeRecovery))
        {
            TryStuckRecovery(strategicTarget);
        }
    }

    private bool IsNearBlockingObstacle(float probeRadius)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(rb.position, Mathf.Max(0.05f, probeRadius), obstacleMask);
        for (int i = 0; i < hits.Length; i++)
        {
            if (IsBlockingCollider(hits[i]))
            {
                return true;
            }
        }

        return false;
    }

    private void TryStuckRecovery(Vector2 strategicTarget)
    {
        flankSide *= -1f;
        if (currentState == AnomalyState.WeaveHunter)
        {
            weaveHunterSideSign *= -1f;
        }

        Vector2 desired = strategicTarget - rb.position;
        if (desired.sqrMagnitude < 0.0001f)
        {
            desired = lastMoveDirection.sqrMagnitude > 0.0001f ? lastMoveDirection : Vector2.right;
        }

        Vector2 repulsed = ApplyObstacleRepulsion(desired.normalized);
        Vector2 jitter = Random.insideUnitCircle * 0.45f;
        Vector2 escapeDir = repulsed + jitter;
        if (escapeDir.sqrMagnitude < 0.0001f)
        {
            escapeDir = desired.normalized;
        }
        escapeDir.Normalize();

        Vector2 escapeTarget = ClampToArena(rb.position + escapeDir * Mathf.Max(0.8f, stuckEscapeDistance));
        BuildNavigationGrid();
        RebuildPathTo(escapeTarget);

        float burstSpeed = Mathf.Max(baseMoveSpeed * Mathf.Max(1f, stuckEscapeVelocityBoost), baseMoveSpeed + 0.4f);
        rb.linearVelocity = escapeDir * burstSpeed;
        lastMoveDirection = escapeDir;
    }

    private void EnterEmergencyDestroyerFromStuck()
    {
        if (currentState == AnomalyState.Destroyer || AreSpecialStatesSuppressedForBreach())
        {
            return;
        }

        AnomalyState previousState = currentState;
        currentState = AnomalyState.Destroyer;
        currentPattern = ResolvePatternForState(currentState);
        stateTimer = 0f;
        currentStateDuration = Mathf.Max(0.4f, emergencyDestroyerDuration);
        emergencyDestroyerActive = true;

        HandleStateTransition(previousState, currentState);
        TriggerStatePulse();
        OnStateEntered();

        BuildNavigationGrid();
        pathWorld.Clear();
        pathIndex = 0;
        ResetBlockedRepathHysteresis();
        stuckConsecutiveChecks = 0;
    }

    private Vector2Int WorldToCell(Vector2 world)
    {
        int x = Mathf.RoundToInt((world.x - navOrigin.x) / nodeSize);
        int y = Mathf.RoundToInt((world.y - navOrigin.y) / nodeSize);
        return new Vector2Int(Mathf.Clamp(x, 0, gridWidth - 1), Mathf.Clamp(y, 0, gridHeight - 1));
    }

    private Vector2 CellToWorld(Vector2Int cell)
    {
        return new Vector2(
            navOrigin.x + cell.x * nodeSize,
            navOrigin.y + cell.y * nodeSize);
    }

    private bool TryNearestWalkable(Vector2Int originCell, out Vector2Int result)
    {
        if (IsCellWalkable(originCell))
        {
            result = originCell;
            return true;
        }

        int maxRadius = Mathf.Max(gridWidth, gridHeight);
        for (int r = 1; r <= maxRadius; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r)
                    {
                        continue;
                    }

                    Vector2Int c = new Vector2Int(originCell.x + dx, originCell.y + dy);
                    if (IsCellWalkable(c))
                    {
                        result = c;
                        return true;
                    }
                }
            }
        }

        result = originCell;
        return false;
    }

    private bool IsCellInside(Vector2Int cell)
    {
        return cell.x >= 0 && cell.x < gridWidth && cell.y >= 0 && cell.y < gridHeight;
    }

    private bool IsCellWalkable(Vector2Int cell)
    {
        return IsCellInside(cell) && walkable[cell.x, cell.y];
    }

    private bool TryFindPath(Vector2Int start, Vector2Int goal, out List<Vector2Int> result)
    {
        result = new List<Vector2Int>();

        float[,] gCost = new float[gridWidth, gridHeight];
        float[,] fCost = new float[gridWidth, gridHeight];
        bool[,] closed = new bool[gridWidth, gridHeight];
        Vector2Int[,] cameFrom = new Vector2Int[gridWidth, gridHeight];
        bool[,] hasParent = new bool[gridWidth, gridHeight];

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                gCost[x, y] = float.PositiveInfinity;
                fCost[x, y] = float.PositiveInfinity;
            }
        }

        List<Vector2Int> open = new List<Vector2Int> { start };
        gCost[start.x, start.y] = 0f;
        fCost[start.x, start.y] = Heuristic(start, goal);

        while (open.Count > 0)
        {
            int currentIndex = 0;
            Vector2Int current = open[0];

            for (int i = 1; i < open.Count; i++)
            {
                Vector2Int candidate = open[i];
                if (fCost[candidate.x, candidate.y] < fCost[current.x, current.y])
                {
                    current = candidate;
                    currentIndex = i;
                }
            }

            if (current == goal)
            {
                ReconstructPath(start, goal, hasParent, cameFrom, result);
                return true;
            }

            open.RemoveAt(currentIndex);
            closed[current.x, current.y] = true;

            for (int i = 0; i < NeighborOffsets.Length; i++)
            {
                Vector2Int offset = NeighborOffsets[i];
                Vector2Int neighbor = current + offset;

                if (!IsCellWalkable(neighbor) || closed[neighbor.x, neighbor.y])
                {
                    continue;
                }

                if (offset.x != 0 && offset.y != 0)
                {
                    Vector2Int sideA = new Vector2Int(current.x + offset.x, current.y);
                    Vector2Int sideB = new Vector2Int(current.x, current.y + offset.y);
                    if (!IsCellWalkable(sideA) || !IsCellWalkable(sideB))
                    {
                        continue;
                    }
                }

                float stepCost = offset.x == 0 || offset.y == 0 ? 1f : 1.4142135f;
                float penalty = GetObstaclePenalty(neighbor);
                float tentativeG = gCost[current.x, current.y] + stepCost + penalty;

                if (tentativeG >= gCost[neighbor.x, neighbor.y])
                {
                    continue;
                }

                cameFrom[neighbor.x, neighbor.y] = current;
                hasParent[neighbor.x, neighbor.y] = true;
                gCost[neighbor.x, neighbor.y] = tentativeG;
                fCost[neighbor.x, neighbor.y] = tentativeG + Heuristic(neighbor, goal);

                if (!open.Contains(neighbor))
                {
                    open.Add(neighbor);
                }
            }
        }

        return false;
    }

    private float GetObstaclePenalty(Vector2Int cell)
    {
        int steps = obstacleDistanceSteps[cell.x, cell.y];
        if (steps == int.MaxValue)
        {
            return 0f;
        }

        float distance = steps * nodeSize;
        if (distance >= obstaclePenaltyDistance)
        {
            return 0f;
        }

        float t = 1f - (distance / Mathf.Max(0.01f, obstaclePenaltyDistance));
        return t * obstaclePenaltyWeight;
    }

    private static float Heuristic(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private static void ReconstructPath(
        Vector2Int start,
        Vector2Int goal,
        bool[,] hasParent,
        Vector2Int[,] cameFrom,
        List<Vector2Int> output)
    {
        output.Clear();

        Vector2Int current = goal;
        output.Add(current);

        while (current != start)
        {
            if (!hasParent[current.x, current.y])
            {
                break;
            }

            current = cameFrom[current.x, current.y];
            output.Add(current);
        }

        output.Reverse();
    }

    private bool IsBlockingCollider(Collider2D col)
    {
        if (col == null)
        {
            return false;
        }

        if (col == ownCollider || col.isTrigger)
        {
            return false;
        }

        if (col.GetComponent<PlayerController>() != null || col.GetComponent<EnemyController>() != null)
        {
            return false;
        }

        if (col.GetComponent<SplitAnomalyCloneController>() != null)
        {
            return false;
        }

        return true;
    }

    private void TryDestroyObstacle(Collider2D col)
    {
        if (currentState != AnomalyState.Destroyer)
        {
            return;
        }

        if (destroyerBreakCount >= destroyerBreakLimit)
        {
            return;
        }

        if (destroyerTouchCooldownTimer > 0f)
        {
            return;
        }

        if (!CanDestroyThisCollider(col))
        {
            return;
        }

        GameObject target = col.gameObject;
        int id = target.GetInstanceID();
        if (!destroyerDestroyedIds.Add(id))
        {
            return;
        }

        destroyerBreakCount++;
        destroyerTouchCooldownTimer = Mathf.Max(0f, destroyerTouchCooldown);
        BeginDestroyFracture(target);

        BuildNavigationGrid();
        pathWorld.Clear();
        pathIndex = 0;
    }

    private void BeginDestroyFracture(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        int id = target.GetInstanceID();
        if (!destroyerPendingRespawnIds.Add(id))
        {
            return;
        }

        if (!TryCreateDestroyerRespawnSnapshot(target, out DestroyerRespawnSnapshot snapshot))
        {
            destroyerPendingRespawnIds.Remove(id);
            return;
        }

        for (int i = 0; i < snapshot.colliders.Length; i++)
        {
            if (snapshot.colliders[i] != null)
            {
                snapshot.colliders[i].enabled = false;
            }
        }

        if (snapshot.rigidbody != null)
        {
            snapshot.rigidbody.linearVelocity = Vector2.zero;
            snapshot.rigidbody.angularVelocity = 0f;
            snapshot.rigidbody.simulated = false;
        }

        if (!Application.isPlaying)
        {
            DestroyImmediate(target);
            destroyerPendingRespawnIds.Remove(id);
            return;
        }

        StartCoroutine(DestroyFractureRoutine(snapshot, id));
    }

    private bool TryCreateDestroyerRespawnSnapshot(GameObject target, out DestroyerRespawnSnapshot snapshot)
    {
        snapshot = null;
        if (target == null)
        {
            return false;
        }

        DestroyerRespawnSnapshot data = new DestroyerRespawnSnapshot();
        data.target = target;
        data.targetTransform = target.transform;
        data.startScale = target.transform.localScale;
        data.startRotation = target.transform.localRotation;
        data.colliders = target.GetComponentsInChildren<Collider2D>(true);
        data.colliderEnabled = new bool[data.colliders.Length];
        for (int i = 0; i < data.colliders.Length; i++)
        {
            data.colliderEnabled[i] = data.colliders[i] != null && data.colliders[i].enabled;
        }

        data.rigidbody = target.GetComponent<Rigidbody2D>();
        data.rigidbodySimulated = data.rigidbody != null && data.rigidbody.simulated;
        data.renderers = target.GetComponentsInChildren<SpriteRenderer>(true);
        data.rendererEnabled = new bool[data.renderers.Length];
        data.rendererColors = new Color[data.renderers.Length];

        for (int i = 0; i < data.renderers.Length; i++)
        {
            if (data.renderers[i] == null)
            {
                data.rendererEnabled[i] = false;
                data.rendererColors[i] = Color.white;
                continue;
            }

            data.rendererEnabled[i] = data.renderers[i].enabled;
            data.rendererColors[i] = data.renderers[i].color;
        }

        snapshot = data;
        return true;
    }

    private IEnumerator DestroyFractureRoutine(DestroyerRespawnSnapshot snapshot, int targetId)
    {
        if (snapshot == null || snapshot.target == null)
        {
            destroyerDestroyedIds.Remove(targetId);
            destroyerPendingRespawnIds.Remove(targetId);
            yield break;
        }

        Transform tr = snapshot.targetTransform;
        SpriteRenderer[] renderers = snapshot.renderers;
        Color[] baseColors = snapshot.rendererColors;
        Vector3 startScale = snapshot.startScale;
        Quaternion startRot = snapshot.startRotation;
        float duration = Mathf.Max(0.02f, destroyerFractureDuration);
        float elapsed = 0f;
        float spinSign = Random.value < 0.5f ? -1f : 1f;

        while (elapsed < duration && snapshot.target != null)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 2f);

            if (tr != null)
            {
                float endScale = Mathf.Clamp01(destroyerFractureEndScale);
                tr.localScale = Vector3.Lerp(startScale, startScale * endScale, eased);
                float z = Mathf.Lerp(0f, destroyerFractureSpinDegrees * spinSign, eased);
                tr.localRotation = startRot * Quaternion.Euler(0f, 0f, z);
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer sr = renderers[i];
                if (sr == null)
                {
                    continue;
                }

                Color flash = Color.Lerp(baseColors[i], destroyerFractureFlashColor, eased);
                flash.a = Mathf.Lerp(baseColors[i].a, 0f, eased);
                sr.enabled = true;
                sr.color = flash;
            }

            yield return null;
        }

        if (snapshot.target == null)
        {
            destroyerDestroyedIds.Remove(targetId);
            destroyerPendingRespawnIds.Remove(targetId);
            yield break;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = false;
            }
        }

        float totalDelay = Mathf.Max(0f, destroyerRespawnDelay);
        float warningDuration = Mathf.Clamp(destroyerRespawnWarningLeadTime, 0f, totalDelay);
        float hiddenDuration = Mathf.Max(0f, totalDelay - warningDuration);

        float hiddenTimer = 0f;
        while (hiddenTimer < hiddenDuration && snapshot.target != null)
        {
            hiddenTimer += Time.deltaTime;
            yield return null;
        }

        if (snapshot.target == null)
        {
            destroyerDestroyedIds.Remove(targetId);
            destroyerPendingRespawnIds.Remove(targetId);
            yield break;
        }

        if (warningDuration > 0f)
        {
            float warningTimer = 0f;
            while (warningTimer < warningDuration && snapshot.target != null)
            {
                warningTimer += Time.deltaTime;
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Max(0.1f, destroyerRespawnWarningPulseSpeed));

                if (tr != null)
                {
                    float scalePulse = Mathf.Lerp(0.93f, 1.03f, pulse);
                    tr.localScale = startScale * scalePulse;
                    tr.localRotation = startRot;
                }

                for (int i = 0; i < renderers.Length; i++)
                {
                    SpriteRenderer sr = renderers[i];
                    if (sr == null || !snapshot.rendererEnabled[i])
                    {
                        continue;
                    }

                    sr.enabled = true;
                    Color c = Color.Lerp(baseColors[i], destroyerRespawnWarningColor, 0.65f);
                    c.a = Mathf.Lerp(0.18f, 0.65f, pulse);
                    sr.color = c;
                }

                yield return null;
            }
        }

        if (snapshot.target == null)
        {
            destroyerDestroyedIds.Remove(targetId);
            destroyerPendingRespawnIds.Remove(targetId);
            yield break;
        }

        if (tr != null)
        {
            tr.localScale = startScale;
            tr.localRotation = startRot;
        }

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null)
            {
                continue;
            }

            renderers[i].enabled = snapshot.rendererEnabled[i];
            renderers[i].color = baseColors[i];
        }

        for (int i = 0; i < snapshot.colliders.Length; i++)
        {
            Collider2D col = snapshot.colliders[i];
            if (col != null)
            {
                col.enabled = snapshot.colliderEnabled[i];
            }
        }

        if (snapshot.rigidbody != null)
        {
            snapshot.rigidbody.simulated = snapshot.rigidbodySimulated;
            snapshot.rigidbody.linearVelocity = Vector2.zero;
            snapshot.rigidbody.angularVelocity = 0f;
        }

        destroyerDestroyedIds.Remove(targetId);
        destroyerPendingRespawnIds.Remove(targetId);

        BuildNavigationGrid();
        pathWorld.Clear();
        pathIndex = 0;
    }

    private bool CanDestroyThisCollider(Collider2D col)
    {
        if (col == null || col.isTrigger || col == ownCollider)
        {
            return false;
        }

        if (col.GetComponent<PlayerController>() != null || col.GetComponent<EnemyController>() != null || col.GetComponent<SplitAnomalyCloneController>() != null)
        {
            return false;
        }

        Transform t = col.transform;
        while (t != null)
        {
            if (t.name == "Bounds")
            {
                return false;
            }

            t = t.parent;
        }

        return true;
    }

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        float s = Mathf.Sin(r);
        float c = Mathf.Cos(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (gameManager == null || !gameManager.IsRunActive)
        {
            return;
        }

        TryDestroyObstacle(collision.collider);

        PlayerController hitPlayer = collision.collider.GetComponent<PlayerController>();
        if (hitPlayer != null)
        {
            if (hitPlayer.TryParryHit(rb.position, out Vector2 parryDirection))
            {
                ApplyParryImpact(hitPlayer.GetPosition(), parryDirection);
                return;
            }

            if (hitPlayer.TryAbsorbHit())
            {
                return;
            }

            gameManager?.RequestPlayerDefeat(hitPlayer);
        }
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (gameManager == null || !gameManager.IsRunActive)
        {
            return;
        }

        TryDestroyObstacle(collision.collider);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (gameManager == null || !gameManager.IsRunActive)
        {
            return;
        }

        TryDestroyObstacle(other);

        PlayerController hitPlayer = other.GetComponent<PlayerController>();
        if (hitPlayer != null)
        {
            if (hitPlayer.TryParryHit(rb.position, out Vector2 parryDirection))
            {
                ApplyParryImpact(hitPlayer.GetPosition(), parryDirection);
                return;
            }

            if (hitPlayer.TryAbsorbHit())
            {
                return;
            }

            gameManager?.RequestPlayerDefeat(hitPlayer);
        }
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (gameManager == null || !gameManager.IsRunActive)
        {
            return;
        }

        TryDestroyObstacle(other);
    }
}

public class AnomalyStateBurstFx : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private float maxRadius = 1.2f;
    private float life = 0.3f;
    private Color baseColor = Color.white;
    private float age;

    public void Configure(SpriteRenderer rendererRef, float radius, float duration, Color tint)
    {
        spriteRenderer = rendererRef;
        maxRadius = Mathf.Max(0.15f, radius);
        life = Mathf.Max(0.08f, duration);
        baseColor = tint;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / life);
        float eased = 1f - Mathf.Pow(1f - t, 2f);
        transform.localScale = Vector3.one * Mathf.Lerp(0.24f, maxRadius, eased);
        if (spriteRenderer != null)
        {
            spriteRenderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Lerp(0.85f, 0f, t));
        }
    }
}

public class AnomalyStateRayFx : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Vector2 direction = Vector2.right;
    private float radius = 1.2f;
    private float life = 0.3f;
    private float age;
    private Vector3 origin;

    public void Configure(SpriteRenderer rendererRef, Vector2 dir, float distance, float duration)
    {
        spriteRenderer = rendererRef;
        direction = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector2.right;
        radius = Mathf.Max(0.15f, distance);
        life = Mathf.Max(0.08f, duration);
        origin = transform.position;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / life);
        float eased = 1f - Mathf.Pow(1f - t, 2f);
        transform.position = origin + (Vector3)(direction * radius * eased);
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        transform.localScale = new Vector3(Mathf.Lerp(0.24f, 0.04f, t), Mathf.Lerp(0.07f, 0.02f, t), 1f);
        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = Mathf.Lerp(0.95f, 0f, t);
            spriteRenderer.color = c;
        }
    }
}

public class ReplayPredatorGhostFx : MonoBehaviour
{
    private PlayerController player;
    private GameManager gameManager;
    private readonly List<Vector2> path = new List<Vector2>();
    private readonly List<SpriteRenderer> afterimages = new List<SpriteRenderer>();
    private SpriteRenderer bodyRenderer;
    private SpriteRenderer coreRenderer;
    private float radius = 0.5f;
    private float telegraphSeconds = 0.8f;
    private float travelSeconds = 1.35f;
    private float age;
    private float hitCooldown;
    private bool active;
    private Color color = Color.magenta;

    public void Configure(PlayerController playerRef, GameManager managerRef, List<Vector2> ghostPath, float hazardRadius, float warningSeconds, float travelDuration, Color tint)
    {
        player = playerRef;
        gameManager = managerRef;
        path.Clear();
        if (ghostPath != null)
        {
            path.AddRange(ghostPath);
        }

        radius = Mathf.Max(0.12f, hazardRadius);
        telegraphSeconds = Mathf.Max(0.08f, warningSeconds);
        travelSeconds = Mathf.Max(0.1f, travelDuration);
        color = tint;
        EnsureVisuals();

        if (path.Count > 0)
        {
            transform.position = new Vector3(path[0].x, path[0].y, 0f);
        }
    }

    private void Update()
    {
        if (path.Count <= 0 || gameManager == null || !gameManager.IsRunActive || gameManager.IsGameOver)
        {
            Destroy(gameObject);
            return;
        }

        age += Time.deltaTime;
        if (hitCooldown > 0f)
        {
            hitCooldown -= Time.deltaTime;
        }

        if (!active && age >= telegraphSeconds)
        {
            active = true;
            age = 0f;
        }

        float progress = active
            ? Mathf.Clamp01(age / travelSeconds)
            : Mathf.Clamp01(age / telegraphSeconds);
        Vector2 position = active ? EvaluatePath(progress) : path[0];
        transform.position = new Vector3(position.x, position.y, 0f);
        UpdateVisuals(progress);

        if (active)
        {
            TryHitPlayer(position);
        }

        if (active && age >= travelSeconds)
        {
            Destroy(gameObject);
        }
    }

    private void EnsureVisuals()
    {
        if (bodyRenderer == null)
        {
            bodyRenderer = gameObject.AddComponent<SpriteRenderer>();
            bodyRenderer.sprite = SquareSpriteProvider.Get();
            bodyRenderer.drawMode = SpriteDrawMode.Sliced;
            bodyRenderer.sortingOrder = 19;
            bodyRenderer.size = Vector2.one * radius * 1.35f;
        }

        if (coreRenderer == null)
        {
            GameObject core = new GameObject("ReplayPredatorGhostCore");
            core.transform.SetParent(transform, false);
            coreRenderer = core.AddComponent<SpriteRenderer>();
            coreRenderer.sprite = SquareSpriteProvider.Get();
            coreRenderer.drawMode = SpriteDrawMode.Sliced;
            coreRenderer.sortingOrder = 20;
            coreRenderer.size = Vector2.one * radius * 0.62f;
        }

        while (afterimages.Count < 7)
        {
            GameObject afterimage = new GameObject($"ReplayPredatorAfterimage_{afterimages.Count}");
            afterimage.transform.SetParent(transform.parent, false);
            SpriteRenderer sr = afterimage.AddComponent<SpriteRenderer>();
            sr.sprite = SquareSpriteProvider.Get();
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.sortingOrder = 18;
            sr.size = Vector2.one * radius * 1.05f;
            afterimages.Add(sr);
        }
    }

    private Vector2 EvaluatePath(float t)
    {
        if (path.Count == 1)
        {
            return path[0];
        }

        float scaled = Mathf.Clamp01(t) * (path.Count - 1);
        int index = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, path.Count - 2);
        float local = scaled - index;
        return Vector2.Lerp(path[index], path[index + 1], local);
    }

    private void UpdateVisuals(float progress)
    {
        EnsureVisuals();
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (active ? 18f : 7f));
        float alpha = active ? Mathf.Lerp(0.95f, 0.48f, progress) : Mathf.Lerp(0.16f, 0.72f, progress);

        if (bodyRenderer != null)
        {
            bodyRenderer.size = Vector2.one * radius * Mathf.Lerp(1.15f, 1.55f, pulse);
            bodyRenderer.color = new Color(color.r, color.g, color.b, alpha);
        }

        if (coreRenderer != null)
        {
            coreRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, Time.time * 180f);
            coreRenderer.color = new Color(0.42f, 0.96f, 1f, Mathf.Lerp(0.28f, 0.82f, pulse) * alpha);
        }

        for (int i = 0; i < afterimages.Count; i++)
        {
            SpriteRenderer sr = afterimages[i];
            if (sr == null)
            {
                continue;
            }

            float offset = (i + 1) / (float)(afterimages.Count + 1);
            float pathT = active ? Mathf.Clamp01(progress - offset * 0.12f) : Mathf.Clamp01(progress * offset);
            Vector2 pos = EvaluatePath(pathT);
            sr.transform.position = new Vector3(pos.x, pos.y, 0f);
            sr.transform.rotation = Quaternion.Euler(0f, 0f, Time.time * 90f + i * 11f);
            sr.color = new Color(color.r, color.g, color.b, alpha * Mathf.Lerp(0.34f, 0.06f, offset));
        }
    }

    private void TryHitPlayer(Vector2 currentPosition)
    {
        if (player == null || hitCooldown > 0f || Vector2.Distance(player.GetPosition(), currentPosition) > radius)
        {
            return;
        }

        hitCooldown = 0.18f;
        if (player.TryAbsorbHit())
        {
            Destroy(gameObject);
            return;
        }

        gameManager.RequestPlayerDefeat(player);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < afterimages.Count; i++)
        {
            if (afterimages[i] != null)
            {
                Destroy(afterimages[i].gameObject);
            }
        }
    }
}

public class ChecksumLatticeFx : MonoBehaviour
{
    private EnemyController owner;
    private PlayerController player;
    private GameManager gameManager;
    private Vector2[] nodes;
    private int[] sequence;
    private SpriteRenderer[] nodeRenderers;
    private SpriteRenderer[] lineRenderers;
    private SpriteRenderer[] dataMoteRenderers;
    private SpriteRenderer activeGuideRenderer;
    private SpriteRenderer activeRingRenderer;
    private SpriteRenderer activeArrowHeadRenderer;
    private TextMesh[] labels;
    private TextMesh orderHintLabel;
    private readonly List<RendererColorSnapshot> grayscaleSnapshots = new List<RendererColorSnapshot>();
    private Vector2 arenaOrigin;
    private Vector2 arenaSize;
    private float prepSeconds = 1.2f;
    private float activeSeconds = 7f;
    private float nodeRadius = 0.5f;
    private float age;
    private float grayscaleRefreshTimer;
    private int sequenceStep;
    private bool resolved;
    private bool sceneColorsRestored;
    private Color nodeColor = Color.cyan;
    private Color activeColor = Color.yellow;
    private Color failColor = Color.magenta;

    private struct RendererColorSnapshot
    {
        public SpriteRenderer renderer;
        public Color color;
    }

    public void Configure(EnemyController ownerRef, PlayerController playerRef, GameManager managerRef, Vector2[] nodePositions, int[] nodeSequence, float prep, float duration, float radius, Color baseTint, Color activeTint, Color failTint, Vector2 arenaStart, Vector2 arenaDimensions)
    {
        owner = ownerRef;
        player = playerRef;
        gameManager = managerRef;
        nodes = nodePositions;
        sequence = nodeSequence;
        prepSeconds = Mathf.Max(0.2f, prep);
        activeSeconds = Mathf.Max(1.5f, duration);
        nodeRadius = Mathf.Max(0.18f, radius);
        nodeColor = baseTint;
        activeColor = activeTint;
        failColor = failTint;
        arenaOrigin = arenaStart;
        arenaSize = arenaDimensions;

        BuildVisuals();
        ApplySceneGrayscale();
    }

    private void BuildVisuals()
    {
        int count = nodes != null ? nodes.Length : 0;
        nodeRenderers = new SpriteRenderer[count];
        labels = new TextMesh[count];
        lineRenderers = new SpriteRenderer[Mathf.Max(0, count - 1)];
        dataMoteRenderers = new SpriteRenderer[12];

        for (int i = 0; i < count; i++)
        {
            GameObject node = new GameObject($"ChecksumNode_{i}");
            node.transform.SetParent(transform, false);
            node.transform.position = new Vector3(nodes[i].x, nodes[i].y, 0f);

            SpriteRenderer sr = node.AddComponent<SpriteRenderer>();
            sr.sprite = CircleSpriteProvider.Get();
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.sortingOrder = 19;
            sr.size = Vector2.one * nodeRadius * 2f;
            nodeRenderers[i] = sr;

            GameObject labelGo = new GameObject($"ChecksumNodeLabel_{i}");
            labelGo.transform.SetParent(node.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, -nodeRadius * 0.08f, 0f);
            TextMesh label = labelGo.AddComponent<TextMesh>();
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = nodeRadius * 0.42f;
            label.fontSize = 32;
            label.text = "?";
            MeshRenderer labelRenderer = labelGo.GetComponent<MeshRenderer>();
            if (labelRenderer != null)
            {
                labelRenderer.sortingOrder = 21;
            }

            labels[i] = label;
        }

        if (sequence == null)
        {
            return;
        }

        for (int i = 0; i < lineRenderers.Length; i++)
        {
            int a = Mathf.Clamp(sequence[i], 0, count - 1);
            int b = Mathf.Clamp(sequence[i + 1], 0, count - 1);
            GameObject line = new GameObject($"ChecksumLink_{i}");
            line.transform.SetParent(transform, false);
            SpriteRenderer sr = line.AddComponent<SpriteRenderer>();
            sr.sprite = SquareSpriteProvider.Get();
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.sortingOrder = 18;
            lineRenderers[i] = sr;
            PositionLine(line.transform, sr, nodes[a], nodes[b], 0.055f);
        }

        BuildGuidanceVisuals();
    }

    private void BuildGuidanceVisuals()
    {
        GameObject guide = new GameObject("ChecksumActiveGuide");
        guide.transform.SetParent(transform, false);
        activeGuideRenderer = guide.AddComponent<SpriteRenderer>();
        activeGuideRenderer.sprite = SquareSpriteProvider.Get();
        activeGuideRenderer.drawMode = SpriteDrawMode.Sliced;
        activeGuideRenderer.sortingOrder = 20;
        activeGuideRenderer.color = Color.clear;

        GameObject arrowHead = new GameObject("ChecksumActiveArrowHead");
        arrowHead.transform.SetParent(transform, false);
        activeArrowHeadRenderer = arrowHead.AddComponent<SpriteRenderer>();
        activeArrowHeadRenderer.sprite = SquareSpriteProvider.Get();
        activeArrowHeadRenderer.drawMode = SpriteDrawMode.Sliced;
        activeArrowHeadRenderer.sortingOrder = 21;
        activeArrowHeadRenderer.size = new Vector2(nodeRadius * 0.62f, nodeRadius * 0.22f);
        activeArrowHeadRenderer.color = Color.clear;

        GameObject ring = new GameObject("ChecksumActiveRing");
        ring.transform.SetParent(transform, false);
        activeRingRenderer = ring.AddComponent<SpriteRenderer>();
        activeRingRenderer.sprite = CircleSpriteProvider.Get();
        activeRingRenderer.drawMode = SpriteDrawMode.Sliced;
        activeRingRenderer.sortingOrder = 20;
        activeRingRenderer.color = Color.clear;

        GameObject hint = new GameObject("ChecksumOrderHint");
        hint.transform.SetParent(transform, false);
        Vector2 hintPos = arenaOrigin + new Vector2(arenaSize.x * 0.5f, arenaSize.y - 0.72f);
        hint.transform.position = new Vector3(hintPos.x, hintPos.y, 0f);
        orderHintLabel = hint.AddComponent<TextMesh>();
        orderHintLabel.anchor = TextAnchor.MiddleCenter;
        orderHintLabel.alignment = TextAlignment.Center;
        orderHintLabel.characterSize = 0.22f;
        orderHintLabel.fontSize = 38;
        orderHintLabel.text = BuildOrderHintText();
        MeshRenderer hintRenderer = hint.GetComponent<MeshRenderer>();
        if (hintRenderer != null)
        {
            hintRenderer.sortingOrder = 22;
        }

        for (int i = 0; i < dataMoteRenderers.Length; i++)
        {
            GameObject mote = new GameObject($"ChecksumDataMote_{i}");
            mote.transform.SetParent(transform, false);
            SpriteRenderer sr = mote.AddComponent<SpriteRenderer>();
            sr.sprite = SquareSpriteProvider.Get();
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.sortingOrder = 21;
            sr.size = Vector2.one * Mathf.Lerp(0.045f, 0.085f, (i % 4) / 3f);
            sr.color = Color.clear;
            dataMoteRenderers[i] = sr;
        }
    }

    private void Update()
    {
        if (resolved)
        {
            return;
        }

        if (gameManager != null && (!gameManager.IsRunActive || gameManager.IsGameOver))
        {
            Resolve(false);
            return;
        }

        age += Time.deltaTime;
        grayscaleRefreshTimer -= Time.deltaTime;
        if (grayscaleRefreshTimer <= 0f)
        {
            grayscaleRefreshTimer = 0.16f;
            ApplySceneGrayscale();
        }

        bool armed = age >= prepSeconds;
        UpdateVisuals(armed);

        if (armed && player != null && sequence != null && sequenceStep < sequence.Length)
        {
            int activeIndex = Mathf.Clamp(sequence[sequenceStep], 0, nodes.Length - 1);
            if (Vector2.Distance(player.GetPosition(), nodes[activeIndex]) <= nodeRadius)
            {
                sequenceStep++;
                if (sequenceStep >= sequence.Length)
                {
                    Resolve(true);
                    return;
                }
            }
        }

        if (age >= prepSeconds + activeSeconds)
        {
            Resolve(false);
        }
    }

    private void UpdateVisuals(bool armed)
    {
        if (nodes == null || sequence == null)
        {
            return;
        }

        float prepT = Mathf.Clamp01(age / prepSeconds);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (armed ? 10f : 6f));

        for (int i = 0; i < nodeRenderers.Length; i++)
        {
            int order = GetSequenceOrder(i);
            bool solved = order >= 0 && order < sequenceStep;
            bool active = order == sequenceStep;
            Color color = solved ? nodeColor : active ? activeColor : Color.Lerp(failColor, nodeColor, 0.45f);
            color.a = solved ? 0.48f : active ? Mathf.Lerp(0.72f, 1f, pulse) : Mathf.Lerp(0.22f, 0.48f, prepT);

            if (nodeRenderers[i] != null)
            {
                nodeRenderers[i].color = color;
                float scale = active ? Mathf.Lerp(1.04f, 1.34f, pulse) : Mathf.Lerp(0.72f, 1f, prepT);
                nodeRenderers[i].size = Vector2.one * nodeRadius * 2f * scale;
            }

            if (labels[i] != null)
            {
                labels[i].text = armed && order >= 0 ? (order + 1).ToString() : "?";
                labels[i].color = active ? Color.white : solved ? nodeColor : new Color(0.82f, 0.9f, 1f, 0.74f);
            }
        }

        for (int i = 0; i < lineRenderers.Length; i++)
        {
            SpriteRenderer sr = lineRenderers[i];
            if (sr == null)
            {
                continue;
            }

            Color color = i < sequenceStep ? nodeColor : i == sequenceStep ? activeColor : failColor;
            color.a = Mathf.Lerp(0.08f, i <= sequenceStep ? 0.5f : 0.22f, armed ? 1f : prepT) * Mathf.Lerp(0.82f, 1.18f, pulse);
            sr.color = color;
        }

        UpdateActiveGuide(armed, prepT, pulse);
        UpdateDataMotes(armed, prepT, pulse);
        UpdateOrderHint(armed, prepT, pulse);
    }

    private void UpdateActiveGuide(bool armed, float prepT, float pulse)
    {
        if (sequence == null || nodes == null || sequenceStep >= sequence.Length || player == null)
        {
            SetGuideVisible(false);
            return;
        }

        int activeIndex = Mathf.Clamp(sequence[sequenceStep], 0, nodes.Length - 1);
        Vector2 target = nodes[activeIndex];
        Vector2 playerPos = player.GetPosition();
        float alpha = armed ? Mathf.Lerp(0.5f, 0.86f, pulse) : Mathf.Lerp(0.08f, 0.34f, prepT);
        Color guideColor = Color.Lerp(nodeColor, activeColor, armed ? 0.85f : 0.35f);
        guideColor.a = alpha;

        if (activeGuideRenderer != null)
        {
            PositionLine(activeGuideRenderer.transform, activeGuideRenderer, playerPos, target, Mathf.Lerp(0.045f, 0.09f, pulse));
            activeGuideRenderer.color = guideColor;
        }

        if (activeArrowHeadRenderer != null)
        {
            Vector2 delta = target - playerPos;
            Vector2 dir = delta.sqrMagnitude > 0.001f ? delta.normalized : Vector2.right;
            Vector2 arrowPos = target - dir * Mathf.Max(0.18f, nodeRadius * 0.72f);
            activeArrowHeadRenderer.transform.position = new Vector3(arrowPos.x, arrowPos.y, 0f);
            activeArrowHeadRenderer.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            activeArrowHeadRenderer.size = new Vector2(nodeRadius * Mathf.Lerp(0.5f, 0.8f, pulse), nodeRadius * 0.22f);
            activeArrowHeadRenderer.color = guideColor;
        }

        if (activeRingRenderer != null)
        {
            activeRingRenderer.transform.position = new Vector3(target.x, target.y, 0f);
            activeRingRenderer.size = Vector2.one * nodeRadius * Mathf.Lerp(2.45f, 3.3f, pulse);
            Color ringColor = activeColor;
            ringColor.a = Mathf.Lerp(0.38f, 0.78f, pulse);
            activeRingRenderer.color = ringColor;
        }
    }

    private void SetGuideVisible(bool visible)
    {
        Color clear = Color.clear;
        if (!visible && activeGuideRenderer != null)
        {
            activeGuideRenderer.color = clear;
        }
        if (!visible && activeArrowHeadRenderer != null)
        {
            activeArrowHeadRenderer.color = clear;
        }
        if (!visible && activeRingRenderer != null)
        {
            activeRingRenderer.color = clear;
        }
    }

    private void UpdateDataMotes(bool armed, float prepT, float pulse)
    {
        if (dataMoteRenderers == null || sequence == null || nodes == null || sequenceStep >= sequence.Length)
        {
            return;
        }

        int activeIndex = Mathf.Clamp(sequence[sequenceStep], 0, nodes.Length - 1);
        Vector2 center = nodes[activeIndex];
        float alpha = armed ? 0.72f : Mathf.Lerp(0.08f, 0.38f, prepT);
        for (int i = 0; i < dataMoteRenderers.Length; i++)
        {
            SpriteRenderer sr = dataMoteRenderers[i];
            if (sr == null)
            {
                continue;
            }

            float offset = i / Mathf.Max(1f, dataMoteRenderers.Length);
            float angle = Time.time * Mathf.Lerp(95f, 145f, offset) + offset * Mathf.PI * 2f;
            float radius = nodeRadius * Mathf.Lerp(1.45f, 2.25f, (i % 5) / 4f) * Mathf.Lerp(0.86f, 1.16f, pulse);
            Vector2 pos = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            sr.transform.position = new Vector3(pos.x, pos.y, 0f);
            sr.transform.rotation = Quaternion.Euler(0f, 0f, -angle * Mathf.Rad2Deg);
            Color c = i % 3 == 0 ? activeColor : nodeColor;
            c.a = alpha * Mathf.Lerp(0.42f, 0.94f, (i % 4) / 3f);
            sr.color = c;
        }
    }

    private void UpdateOrderHint(bool armed, float prepT, float pulse)
    {
        if (orderHintLabel == null)
        {
            return;
        }

        orderHintLabel.text = BuildOrderHintText();
        Color color = armed ? activeColor : nodeColor;
        color.a = Mathf.Lerp(0.18f, armed ? 0.92f : 0.54f, armed ? pulse : prepT);
        orderHintLabel.color = color;
    }

    private void ApplySceneGrayscale()
    {
        SpriteRenderer[] renderers = Object.FindObjectsOfType<SpriteRenderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer sr = renderers[i];
            if (sr == null || sr.transform == null || sr.transform.IsChildOf(transform))
            {
                continue;
            }

            if (!IsRendererTracked(sr))
            {
                grayscaleSnapshots.Add(new RendererColorSnapshot
                {
                    renderer = sr,
                    color = sr.color
                });
            }

            sr.color = ToGrayscale(sr.color);
        }
    }

    private bool IsRendererTracked(SpriteRenderer renderer)
    {
        for (int i = 0; i < grayscaleSnapshots.Count; i++)
        {
            if (grayscaleSnapshots[i].renderer == renderer)
            {
                return true;
            }
        }

        return false;
    }

    private void RestoreSceneColors()
    {
        if (sceneColorsRestored)
        {
            return;
        }

        sceneColorsRestored = true;
        for (int i = 0; i < grayscaleSnapshots.Count; i++)
        {
            RendererColorSnapshot snapshot = grayscaleSnapshots[i];
            if (snapshot.renderer != null)
            {
                snapshot.renderer.color = snapshot.color;
            }
        }

        grayscaleSnapshots.Clear();
    }

    private static Color ToGrayscale(Color color)
    {
        float gray = color.r * 0.299f + color.g * 0.587f + color.b * 0.114f;
        return new Color(gray, gray, gray, color.a);
    }

    private string BuildOrderHintText()
    {
        if (sequence == null || sequence.Length == 0)
        {
            return string.Empty;
        }

        string text = string.Empty;
        for (int i = 0; i < sequence.Length; i++)
        {
            if (i > 0)
            {
                text += " > ";
            }

            text += (i + 1).ToString();
        }

        return text;
    }

    private int GetSequenceOrder(int nodeIndex)
    {
        for (int i = 0; i < sequence.Length; i++)
        {
            if (sequence[i] == nodeIndex)
            {
                return i;
            }
        }

        return -1;
    }

    private void Resolve(bool success)
    {
        if (resolved)
        {
            return;
        }

        resolved = true;
        Vector2 pos = player != null ? player.GetPosition() : (Vector2)transform.position;
        RestoreSceneColors();
        owner?.ResolveChecksumLattice(success, sequenceStep, pos);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        RestoreSceneColors();
    }

    private static void PositionLine(Transform lineTransform, SpriteRenderer renderer, Vector2 from, Vector2 to, float thickness)
    {
        Vector2 delta = to - from;
        float distance = delta.magnitude;
        Vector2 center = (from + to) * 0.5f;
        lineTransform.position = new Vector3(center.x, center.y, 0f);
        lineTransform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        renderer.size = new Vector2(Mathf.Max(0.05f, distance), Mathf.Max(0.02f, thickness));
    }
}

public class SignalPossessionLure : MonoBehaviour
{
    private EnemyController owner;
    private PlayerController player;
    private SpriteRenderer spriteRenderer;
    private float armSeconds = 0.6f;
    private float lifetime = 3f;
    private float radius = 1.4f;
    private float age;
    private bool detonated;
    private Color color = Color.green;

    public void Configure(EnemyController ownerRef, PlayerController playerRef, SpriteRenderer rendererRef, float armDelay, float lifeSeconds, float triggerRadius, Color tint)
    {
        owner = ownerRef;
        player = playerRef;
        spriteRenderer = rendererRef;
        armSeconds = Mathf.Max(0.05f, armDelay);
        lifetime = Mathf.Max(armSeconds + 0.1f, lifeSeconds);
        radius = Mathf.Max(0.25f, triggerRadius);
        color = tint;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float armed = age >= armSeconds ? 1f : Mathf.Clamp01(age / armSeconds);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Lerp(6f, 16f, armed));

        if (spriteRenderer != null)
        {
            spriteRenderer.size = Vector2.one * Mathf.Lerp(0.5f, 0.88f, pulse);
            Color c = Color.Lerp(color, new Color(1f, 0.25f, 0.68f, 1f), armed * pulse);
            c.a = Mathf.Lerp(0.5f, 0.96f, armed);
            spriteRenderer.color = c;
        }

        if (!detonated && age >= armSeconds && player != null && Vector2.Distance(player.GetPosition(), transform.position) <= radius)
        {
            detonated = true;
            owner?.DetonateSignalPossession(transform.position);
            Destroy(gameObject);
            return;
        }

        if (age >= lifetime)
        {
            Destroy(gameObject);
        }
    }
}

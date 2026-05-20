using System.Collections.Generic;
using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EnemyController : MonoBehaviour
{
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
        Destroyer
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
    [SerializeField] private float speedStateMultiplier = 1.55f;
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

    public AnomalyState CurrentState => currentState;
    public string CurrentStateLabel => currentState.ToString();
    public PacingPhase CurrentPacingPhase => pacingPhase;
    public string CurrentPacingPhaseLabel => pacingPhase.ToString();

    [Header("Navigation Grid")]
    [SerializeField] private LayerMask obstacleMask = ~0;
    [SerializeField] private Vector2 fallbackArenaSize = new Vector2(32f, 18f);
    [SerializeField] private float nodeSize = 0.6f;
    [SerializeField] private float nodeProbePadding = 0.10f;
    [SerializeField] private float repathInterval = 0.16f;
    [SerializeField] private float waypointReachDistance = 0.25f;
    [SerializeField] private float targetRepathThreshold = 0.45f;
    [SerializeField] private int pathLookahead = 6;
    [SerializeField] private float gridRefreshInterval = 0.8f;

    [Header("Obstacle Preference")]
    [SerializeField] private float obstaclePenaltyDistance = 2.4f;
    [SerializeField] private float obstaclePenaltyWeight = 5f;

    [Header("Local Avoidance")]
    [SerializeField] private float repulsionProbeDistance = 1.25f;
    [SerializeField] private float repulsionWeight = 1.8f;
    [SerializeField] private int repulsionRayCount = 9;
    [SerializeField] private float repulsionSpreadAngle = 120f;

    [Header("Anti-Stuck")]
    [SerializeField] private float stuckCheckInterval = 0.30f;
    [SerializeField] private float stuckDistanceThreshold = 0.09f;
    [SerializeField] private int stuckChecksBeforeRecovery = 2;
    [SerializeField] private int stuckChecksBeforeEmergencyDestroyer = 5;
    [SerializeField] private float stuckTargetMinDistance = 1.4f;
    [SerializeField] private float stuckObstacleProbeRadius = 1.1f;
    [SerializeField] private float stuckEscapeDistance = 2.9f;
    [SerializeField] private float stuckEscapeVelocityBoost = 1.35f;
    [SerializeField] private float emergencyDestroyerDuration = 2f;

    [Header("Path Hysteresis")]
    [SerializeField] private float blockedRepathHoldSeconds = 0.28f;
    [SerializeField] private float blockedRepathDistanceMultiplier = 1.8f;
    [SerializeField] private float blockedPathCommitSeconds = 0.85f;
    [SerializeField] private float blockedPathCommitDistanceMultiplier = 3f;
    [SerializeField] private int blockedOscillationThreshold = 3;
    [SerializeField] private float blockedOscillationResetSeconds = 1.2f;

    private Rigidbody2D rb;
    private Collider2D ownCollider;
    private SpriteRenderer ownRenderer;

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
    private PacingPhase pacingPhase = PacingPhase.BuildUp;
    private int pacingMinorStatesRemaining;
    private int pacingMajorStatesRemaining;
    private float statePulseTimer;
    private float externalSpeedTimer;
    private float externalSpeedMultiplier = 1f;
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

        ResolveArenaBounds();
        BuildNavigationGrid();
        InitializePacingDirector();

        stuckCheckPosition = rb.position;
        SelectNextState(forceDifferent: false);
    }

    private void OnDisable()
    {
        DestroySplitCloneImmediate();
        DestroySplitMergeTelegraphImmediate();
        if (ownRenderer != null)
        {
            ownRenderer.color = baseColor;
        }

        transform.localScale = baseScale;
    }

    private void Update()
    {
        if (gameManager == null || player == null || gameManager.IsGameOver || !gameManager.IsRunActive)
        {
            rb.linearVelocity = Vector2.zero;
            UpdateStatePulseVisual();
            return;
        }

        HandleStateSwitch();
        UpdatePatternInternals();
        UpdateStateAbilities();
        UpdateStatePulseVisual();
        UpdateExternalSpeedEffects();

        if (splitMergeInProgress && splitClone != null)
        {
            TickOwnerSplitMergeMovement();
            UpdateSplitMergeTelegraphVisual();
            return;
        }
        HideSplitMergeTelegraphVisual();

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
            float effectiveRepathInterval = Mathf.Max(
                0.05f,
                repathInterval * (chaosDriveEnabled ? chaosRepathIntervalMultiplier : 1f));

            bool shouldRepath = pathWorld.Count == 0 || repathTimer >= effectiveRepathInterval;
            if (!shouldRepath && targetMoved)
            {
                if (!obstacleBlocked)
                {
                    shouldRepath = true;
                    ResetBlockedRepathHysteresis();
                }
                else
                {
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
                bool shouldCommit = obstacleBlocked;
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

        float speed = baseMoveSpeed;
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

        Vector2 desiredVelocity = desiredDirection * speed;
        rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, desiredVelocity, velocityResponsiveness * Time.deltaTime);
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
        OnStateEntered();
        RegisterStateForPacing(currentState);
    }

    private float GetRandomDurationForState(AnomalyState state)
    {
        if (state == AnomalyState.Split ||
            state == AnomalyState.ExpansionShoot ||
            state == AnomalyState.Destroyer ||
            state == AnomalyState.SpeedSurge)
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
        }

        ApplyProgressionFilter(fullOptions);

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
               state == AnomalyState.SpeedSurge;
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

        bool specialUnlocked = gameManager != null && gameManager.AreBossSpecialStatesUnlocked;
        if (!specialUnlocked)
        {
            options.RemoveAll(o => IsPre60SpecialState(o.state));
        }
    }

    private static bool IsPre60SpecialState(AnomalyState state)
    {
        return state == AnomalyState.Split ||
               state == AnomalyState.ExpansionShoot ||
               state == AnomalyState.SpeedSurge ||
               state == AnomalyState.WeaveHunter ||
               state == AnomalyState.Destroyer;
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

    private void TriggerStatePulse()
    {
        if (!statePulseFxEnabled)
        {
            return; 
        }

        statePulseTimer = Mathf.Max(0.04f, statePulseDuration);
    }

    private void UpdateStatePulseVisual()
    {
        if (ownRenderer == null)
        {
            return;
        }

        if (!statePulseFxEnabled || statePulseTimer <= 0f)
        {
            ownRenderer.color = baseColor;
            transform.localScale = baseScale;
            return;
        }

        statePulseTimer -= Time.deltaTime;
        float normalized = 1f - Mathf.Clamp01(statePulseTimer / Mathf.Max(0.04f, statePulseDuration));
        float pulse = Mathf.Sin(normalized * Mathf.PI);
        float lighten = Mathf.Clamp01(statePulseLighten) * pulse;
        float scale = Mathf.Lerp(1f, Mathf.Max(1f, statePulseScaleBoost), pulse);

        Color boosted = Color.Lerp(baseColor, Color.white, lighten);
        boosted.a = baseColor.a;
        ownRenderer.color = boosted;
        transform.localScale = baseScale * scale;
    }

    private void UpdateExternalSpeedEffects()
    {
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
                return enableAdvancedStates ? AnomalyState.SpeedSurge : AnomalyState.DirectChase;
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

    private void UpdateDestroyerAbility()
    {
        if (destroyerTouchCooldownTimer > 0f)
        {
            destroyerTouchCooldownTimer -= Time.deltaTime;
        }
    }

    private void UpdateSplitAbility()
    {
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
            Mathf.Max(0.5f, baseMoveSpeed * splitCloneSpeedMultiplier),
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

    public Vector2 GetCurrentPosition()
    {
        return rb != null ? rb.position : (Vector2)transform.position;
    }

    public Vector2 GetCurrentTargetForSplitClone()
    {
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

        float speed = baseMoveSpeed * Mathf.Max(0.2f, splitMergeOwnerSpeedMultiplier);
        Vector2 desiredVelocity = desiredDirection * speed;
        rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, desiredVelocity, velocityResponsiveness * 1.15f * Time.deltaTime);
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
        GameObject go = new GameObject($"AnomalyProjectile_{projectileSerial++}");
        go.transform.position = new Vector3(position.x, position.y, 0f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SquareSpriteProvider.Get();
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = expansionShootProjectileSize;
        sr.color = expansionShootProjectileColor;
        sr.sortingOrder = 11;

        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = Mathf.Max(0.04f, Mathf.Min(expansionShootProjectileSize.x, expansionShootProjectileSize.y) * 0.48f);

        AnomalyProjectile projectile = go.AddComponent<AnomalyProjectile>();
        projectile.Configure(
            direction,
            expansionShootProjectileSpeed,
            expansionShootProjectileLifetime,
            obstacleMask,
            gameManager);
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
        Vector2 escapeDir = (repulsed + jitter);
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
        if (currentState == AnomalyState.Destroyer)
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
            if (hitPlayer.TryAbsorbHit())
            {
                return;
            }

            gameManager?.TriggerGameOver();
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
            if (hitPlayer.TryAbsorbHit())
            {
                return;
            }

            gameManager?.TriggerGameOver();
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

using System.Collections.Generic;
using UnityEngine;

public class EnemyLevelThreeStateController : MonoBehaviour
{
    // Ejecuta los estados de nivel 3 sin mezclar su logica temporal y visual con la navegacion base.
    private enum AdaptiveProfile
    {
        Velocity,
        Parry,
        Distance,
        Stillness
    }

    private struct MotionSample
    {
        public float time;
        public Vector2 position;
        public Vector2 velocity;
    }

    [Header("Adaptive Countermeasure")]
    [SerializeField] private float adaptiveReadSeconds = 1.55f;
    [SerializeField] private float adaptiveRevealSeconds = 0.65f;
    [SerializeField] private float adaptiveCycleSeconds = 4.5f;
    [SerializeField] private float adaptivePredictionSeconds = 0.72f;
    [SerializeField] private float adaptiveArrivalDistance = 1.45f;
    [SerializeField] private float adaptiveExecutionSpeed = 11.5f;
    [SerializeField] private float adaptiveLearningStrength = 0.48f;

    [Header("Signal Tether")]
    [SerializeField] private float tetherBreakHoldSeconds = 0.62f;
    [SerializeField] private float tetherPulseInterval = 0.9f;
    [SerializeField] private float tetherPlayerPull = 1.05f;
    [SerializeField] private float tetherEnemyAdvance = 0.72f;
    [SerializeField] private float tetherBreakStun = 0.9f;
    [SerializeField] private float tetherExposureRampSeconds = 4f;
    [SerializeField] private float tetherPressureSlow = 0.78f;

    [Header("Topology Fold Projectile Modifier")]
    [SerializeField, Range(0f, 1f)] private float topologyProjectileStateChance = 1f;
    [SerializeField] private float topologyWarmupSeconds = 1.65f;
    [SerializeField] private float topologyEdgeDepth = 1.45f;
    [SerializeField] private float topologyExitInset = 1.65f;
    [SerializeField] private float topologyObjectCooldown = 0.42f;
    [SerializeField] private float topologyScanInterval = 0.08f;

    [Header("Blindspot Protocol")]
    [SerializeField] private float blindspotSearchInterval = 0.48f;
    [SerializeField] private float blindspotHiddenChargeSeconds = 0.82f;
    [SerializeField] private float blindspotRouteSpeed = 13f;
    [SerializeField] private float blindspotAmbushSpeed = 18f;
    [SerializeField] private float blindspotPredictionSeconds = 0.62f;
    [SerializeField] private int blindspotCandidateCount = 28;
    [SerializeField] private int blindspotVolleyProjectiles = 3;
    [SerializeField] private Vector2 blindspotRadiusRange = new Vector2(3.5f, 7.2f);

    [Header("Visual Language")]
    [SerializeField] private Color adaptiveColor = new Color(1f, 0.35f, 0.72f, 1f);
    [SerializeField] private Color vectorColor = new Color(0.32f, 1f, 0.78f, 1f);
    [SerializeField] private Color topologyColorA = new Color(0.38f, 0.82f, 1f, 1f);
    [SerializeField] private Color topologyColorB = new Color(1f, 0.48f, 0.84f, 1f);
    [SerializeField] private Color blindspotChargeColor = new Color(1f, 0.76f, 0.28f, 1f);
    [SerializeField] private Color blindspotRouteColor = new Color(0.58f, 0.48f, 1f, 1f);

    private EnemyController owner;
    private PlayerController player;
    private GameManager gameManager;
    private EnemyController.AnomalyState activeState;
    private bool stateActive;
    private float stateAge;
    private GameObject visualRoot;
    private GameObject cycleRoot;
    private TextMesh stateLabel;

    private Vector2 lastPlayerPosition;
    private bool lastDashActive;
    private bool lastParryActive;
    private float sampledSpeed;
    private float sampledDistance;
    private readonly List<float> recentDashTimes = new List<float>();
    private readonly List<float> recentParryTimes = new List<float>();
    private readonly List<MotionSample> motionHistory = new List<MotionSample>();
    private float motionSampleTimer;

    private AdaptiveProfile adaptiveProfile;
    private float adaptiveCycleAge;
    private Vector2 adaptiveTarget;
    private bool adaptiveResolved;
    private SpriteRenderer adaptiveTargetRing;
    private SpriteRenderer adaptiveLine;
    private readonly List<SpriteRenderer> adaptiveTicks = new List<SpriteRenderer>();
    private readonly List<Vector2> adaptivePath = new List<Vector2>();
    private int adaptivePathIndex;
    private bool adaptiveExecuting;
    private float adaptiveRepathTimer;
    private bool adaptivePredictionPending;
    private float adaptivePredictionEvaluationTime;
    private Vector2 adaptivePreviousTarget;
    private Vector2 adaptiveLearnedCorrection;

    private float tetherBlockedTimer;
    private float tetherPulseTimer;
    private float tetherResetTimer;
    private float tetherExposure;
    private SpriteRenderer tetherLine;
    private SpriteRenderer tetherEnemyRing;
    private SpriteRenderer tetherPlayerRing;
    private bool movementOverrideActive;
    private Vector2 movementOverrideVelocity;

    private bool topologyHorizontal;
    private bool topologyLive;
    private bool topologyModifierActive;
    private float topologyScanTimer;
    private readonly Dictionary<int, float> topologyCooldowns = new Dictionary<int, float>();
    private readonly List<SpriteRenderer> topologyBands = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> topologyArrows = new List<SpriteRenderer>();

    private readonly List<Vector2> blindspotPath = new List<Vector2>();
    private int blindspotPathIndex;
    private Vector2 blindspotTarget;
    private float blindspotSearchTimer;
    private float blindspotHiddenCharge;
    private bool blindspotAtCover;
    private bool blindspotAmbushing;
    private SpriteRenderer blindspotTargetRing;
    private SpriteRenderer blindspotSightLine;
    private readonly List<SpriteRenderer> blindspotPathLines = new List<SpriteRenderer>();

    public bool IsActive => stateActive;

    public bool TryGetMovementOverride(out Vector2 velocity)
    {
        velocity = movementOverrideVelocity;
        return stateActive && movementOverrideActive;
    }

    public Vector2 GetMovementOverrideTarget(Vector2 fallback)
    {
        if (!stateActive || !movementOverrideActive)
        {
            return fallback;
        }
        if (activeState == EnemyController.AnomalyState.AdaptiveCountermeasure &&
            adaptivePathIndex >= 0 && adaptivePathIndex < adaptivePath.Count)
        {
            return adaptivePath[adaptivePathIndex];
        }
        if (activeState == EnemyController.AnomalyState.BlindspotProtocol &&
            blindspotPathIndex >= 0 && blindspotPathIndex < blindspotPath.Count)
        {
            return blindspotPath[blindspotPathIndex];
        }
        return fallback;
    }

    public void Configure(EnemyController ownerReference, PlayerController playerReference, GameManager managerReference)
    {
        owner = ownerReference;
        player = playerReference;
        gameManager = managerReference;
        lastPlayerPosition = player != null ? player.GetPosition() : Vector2.zero;
    }

    private void Awake()
    {
        if (owner == null)
        {
            owner = GetComponent<EnemyController>();
        }
    }

    private void Update()
    {
        ResolveReferences();
        SamplePlayerBehavior();

        if (!stateActive || owner == null || player == null || gameManager == null ||
            !gameManager.IsRunActive || gameManager.IsGameOver)
        {
            return;
        }

        stateAge += Time.deltaTime;
        if (stateLabel != null)
        {
            stateLabel.transform.position = owner.GetCurrentPosition() + Vector2.up * 1.15f;
        }
        if (topologyModifierActive)
        {
            TickTopologyFold();
        }

        switch (activeState)
        {
            case EnemyController.AnomalyState.AdaptiveCountermeasure:
                TickAdaptiveCountermeasure();
                break;
            case EnemyController.AnomalyState.SignalTether:
                TickSignalTether();
                break;
            case EnemyController.AnomalyState.BlindspotProtocol:
                TickBlindspotProtocol();
                break;
        }
    }

    private void OnDisable()
    {
        ExitState();
    }

    public void EnterState(EnemyController.AnomalyState state)
    {
        ExitState();
        bool projectileModifier = IsTopologyProjectileState(state) &&
                                  gameManager != null &&
                                  gameManager.AreBossLevelThreeStatesUnlocked &&
                                  Random.value <= Mathf.Clamp01(topologyProjectileStateChance);
        if (!IsLevelThreeState(state) && !projectileModifier)
        {
            return;
        }

        ResolveReferences();
        activeState = state;
        stateActive = true;
        stateAge = 0f;
        visualRoot = new GameObject($"LevelThree_{state}");
        CreateStateLabel(GetStateTitle(state));

        if (projectileModifier)
        {
            topologyModifierActive = true;
            BeginTopologyFold();
            return;
        }

        switch (state)
        {
            case EnemyController.AnomalyState.AdaptiveCountermeasure:
                BeginAdaptiveCycle();
                break;
            case EnemyController.AnomalyState.SignalTether:
                BeginSignalTether();
                break;
            case EnemyController.AnomalyState.BlindspotProtocol:
                BeginBlindspotProtocol();
                break;
        }
    }

    public void ExitState()
    {
        stateActive = false;
        movementOverrideActive = false;
        movementOverrideVelocity = Vector2.zero;
        topologyCooldowns.Clear();
        adaptiveTicks.Clear();
        topologyBands.Clear();
        topologyArrows.Clear();
        blindspotPathLines.Clear();
        topologyLive = false;
        topologyModifierActive = false;

        if (visualRoot != null)
        {
            Destroy(visualRoot);
        }

        visualRoot = null;
        cycleRoot = null;
        stateLabel = null;
        adaptiveTargetRing = null;
        adaptiveLine = null;
        tetherLine = null;
        tetherEnemyRing = null;
        tetherPlayerRing = null;
        blindspotTargetRing = null;
        blindspotSightLine = null;
    }

    private void ResolveReferences()
    {
        if (owner == null)
        {
            owner = GetComponent<EnemyController>();
        }
        if (player == null)
        {
            player = FindAnyObjectByType<PlayerController>();
        }
        if (gameManager == null)
        {
            gameManager = FindAnyObjectByType<GameManager>();
        }
    }

    private void SamplePlayerBehavior()
    {
        // Conserva una memoria corta de velocidad, distancia, dashes y parries para elegir una respuesta real.
        if (player == null)
        {
            return;
        }

        Vector2 current = player.GetPosition();
        float delta = Mathf.Max(0.0001f, Time.deltaTime);
        float instantSpeed = Vector2.Distance(current, lastPlayerPosition) / delta;
        sampledSpeed = Mathf.Lerp(sampledSpeed, Mathf.Min(instantSpeed, 18f), 1f - Mathf.Exp(-delta * 2.8f));
        sampledDistance = owner != null
            ? Mathf.Lerp(sampledDistance, Vector2.Distance(current, owner.GetCurrentPosition()), 1f - Mathf.Exp(-delta * 2f))
            : sampledDistance;

        bool dash = player.IsGhostDashing;
        bool parry = player.IsParryActive;
        if (dash && !lastDashActive)
        {
            recentDashTimes.Add(Time.time);
        }
        if (parry && !lastParryActive)
        {
            recentParryTimes.Add(Time.time);
        }

        lastDashActive = dash;
        lastParryActive = parry;
        lastPlayerPosition = current;
        TrimBehaviorSamples(recentDashTimes, 6f);
        TrimBehaviorSamples(recentParryTimes, 6f);

        motionSampleTimer += delta;
        if (motionSampleTimer >= 0.08f)
        {
            motionSampleTimer = 0f;
            motionHistory.Add(new MotionSample
            {
                time = Time.time,
                position = current,
                velocity = player.CurrentVelocity
            });
        }
        while (motionHistory.Count > 0 && motionHistory[0].time < Time.time - 3.5f)
        {
            motionHistory.RemoveAt(0);
        }

        if (adaptivePredictionPending && Time.time >= adaptivePredictionEvaluationTime)
        {
            Vector2 error = current - adaptivePreviousTarget;
            adaptiveLearnedCorrection = Vector2.ClampMagnitude(
                Vector2.Lerp(adaptiveLearnedCorrection, error, Mathf.Clamp01(adaptiveLearningStrength)),
                2.8f);
            adaptivePredictionPending = false;
        }
    }

    private static void TrimBehaviorSamples(List<float> samples, float memorySeconds)
    {
        float oldest = Time.time - Mathf.Max(0.5f, memorySeconds);
        while (samples.Count > 0 && samples[0] < oldest)
        {
            samples.RemoveAt(0);
        }
    }

    private void BeginAdaptiveCycle()
    {
        // La lectura se anuncia antes de fijar el punto de intercepcion para que la respuesta sea evitable.
        ResetCycleRoot("AdaptiveCycle");
        adaptiveCycleAge = 0f;
        adaptiveResolved = false;
        adaptiveExecuting = false;
        adaptiveRepathTimer = 0f;
        movementOverrideActive = false;
        movementOverrideVelocity = Vector2.zero;
        adaptiveProfile = SelectAdaptiveProfile();
        adaptiveTarget = CalculateAdaptiveTarget(adaptiveProfile);

        adaptiveLine = CreateSprite(cycleRoot.transform, "AnalysisLine", SquareSpriteProvider.Get(), adaptiveColor, 20);
        adaptiveTargetRing = CreateSprite(cycleRoot.transform, "CountermeasureTarget", CircleSpriteProvider.Get(), adaptiveColor, 21);
        adaptiveTargetRing.transform.localScale = Vector3.one * 1.25f;

        adaptiveTicks.Clear();
        for (int i = 0; i < 10; i++)
        {
            SpriteRenderer tick = CreateSprite(cycleRoot.transform, $"TargetTick_{i}", SquareSpriteProvider.Get(), adaptiveColor, 22);
            adaptiveTicks.Add(tick);
        }

        UpdateStateLabel($"LEARNING: {GetAdaptiveProfileLabel(adaptiveProfile)}", adaptiveColor);
    }

    private void TickAdaptiveCountermeasure()
    {
        if (adaptiveExecuting)
        {
            movementOverrideActive = true;
            adaptiveRepathTimer -= Time.deltaTime;
            if (adaptiveRepathTimer <= 0f)
            {
                adaptiveRepathTimer = 0.28f;
                Vector2 liveTarget = CalculateAdaptiveTarget(adaptiveProfile);
                adaptiveTarget = Vector2.Lerp(adaptiveTarget, liveTarget, 0.72f);
                RebuildAdaptiveExecutionPath(adaptiveTarget);
            }
            if (TickPathMovement(adaptivePath, ref adaptivePathIndex, adaptiveExecutionSpeed))
            {
                adaptiveExecuting = false;
                movementOverrideActive = false;
                movementOverrideVelocity = Vector2.zero;
                SpawnImpactBurst(owner.GetCurrentPosition(), adaptiveColor, 14, 1.35f);
                BeginAdaptiveCycle();
            }
            return;
        }

        adaptiveCycleAge += Time.deltaTime;
        float read = Mathf.Max(0.4f, adaptiveReadSeconds);
        float reveal = Mathf.Max(0.2f, adaptiveRevealSeconds);
        adaptiveTarget = Vector2.Lerp(adaptiveTarget, CalculateAdaptiveTarget(adaptiveProfile), Time.deltaTime * 2.6f);

        float telegraphProgress = Mathf.Clamp01(adaptiveCycleAge / read);
        float pulse = 0.55f + Mathf.Sin(Time.time * 10f) * 0.22f;
        Color color = adaptiveColor;
        color.a = Mathf.Lerp(0.18f, 0.9f, telegraphProgress) * pulse;
        SetLine(adaptiveLine, owner.GetCurrentPosition(), adaptiveTarget, 0.075f + telegraphProgress * 0.045f, color);
        UpdateRingTicks(adaptiveTicks, adaptiveTarget, 0.82f + telegraphProgress * 0.3f, color, Time.time * 1.8f);
        if (adaptiveTargetRing != null)
        {
            adaptiveTargetRing.transform.position = adaptiveTarget;
            adaptiveTargetRing.transform.localScale = Vector3.one * Mathf.Lerp(0.28f, 1.1f, telegraphProgress);
            adaptiveTargetRing.color = new Color(color.r, color.g, color.b, color.a * 0.22f);
        }

        if (!adaptiveResolved && adaptiveCycleAge >= read + reveal)
        {
            adaptiveResolved = true;
            ResolveAdaptiveCountermeasure();
        }

        if (adaptiveCycleAge >= Mathf.Max(read + reveal + 0.8f, adaptiveCycleSeconds))
        {
            BeginAdaptiveCycle();
        }
    }

    private AdaptiveProfile SelectAdaptiveProfile()
    {
        if (recentParryTimes.Count >= 2)
        {
            return AdaptiveProfile.Parry;
        }
        if (recentDashTimes.Count >= 2 || sampledSpeed >= 5.4f)
        {
            return AdaptiveProfile.Velocity;
        }
        if (sampledDistance >= 7.2f)
        {
            return AdaptiveProfile.Distance;
        }
        return AdaptiveProfile.Stillness;
    }

    private Vector2 CalculateAdaptiveTarget(AdaptiveProfile profile)
    {
        Vector2 playerPosition = player.GetPosition();
        Vector2 velocity = GetAveragePlayerVelocity(1.1f);
        Vector2 enemyPosition = owner.GetCurrentPosition();
        float distance = Vector2.Distance(enemyPosition, playerPosition);
        float horizon = Mathf.Clamp(distance / Mathf.Max(5f, adaptiveExecutionSpeed), 0.42f, 1.28f);
        Vector2 acceleration = GetPlayerVelocityTrend(1.4f);
        Vector2 target = playerPosition +
                         velocity * horizon +
                         acceleration * (0.5f * horizon * horizon) +
                         adaptiveLearnedCorrection;

        switch (profile)
        {
            case AdaptiveProfile.Velocity:
                target += velocity.normalized * Mathf.Max(0.2f, adaptivePredictionSeconds);
                break;
            case AdaptiveProfile.Parry:
                Vector2 away = (playerPosition - enemyPosition).normalized;
                Vector2 side = new Vector2(-away.y, away.x) * (Vector2.Dot(velocity, new Vector2(-away.y, away.x)) >= 0f ? -1f : 1f);
                target += side * (player.ParryRadius + 0.85f);
                break;
            case AdaptiveProfile.Distance:
                target += (playerPosition - owner.GetArenaCenter()).normalized * 0.8f;
                break;
            default:
                Vector2 center = owner.GetArenaCenter();
                Vector2 centerDirection = (center - playerPosition).normalized;
                target += centerDirection * 1.15f;
                break;
        }

        return owner.ClampAdvancedStatePoint(target, 0.9f);
    }

    private void ResolveAdaptiveCountermeasure()
    {
        Vector2 playerPosition = player.GetPosition();
        Vector2 approach = (adaptiveTarget - playerPosition).normalized;
        if (approach.sqrMagnitude < 0.01f)
        {
            approach = (playerPosition - owner.GetCurrentPosition()).normalized;
        }

        Vector2 arrival = owner.ClampAdvancedStatePoint(
            adaptiveTarget - approach * Mathf.Max(0.45f, adaptiveArrivalDistance * 0.55f),
            0.8f);
        adaptivePath.Clear();
        RebuildAdaptiveExecutionPath(arrival);
        adaptiveExecuting = true;
        adaptiveRepathTimer = 0.28f;
        movementOverrideActive = true;
        adaptivePreviousTarget = adaptiveTarget;
        adaptivePredictionEvaluationTime = Time.time + 0.8f;
        adaptivePredictionPending = true;
        UpdateStateLabel("ROUTE LEARNED", adaptiveColor);
    }

    private void RebuildAdaptiveExecutionPath(Vector2 destination)
    {
        adaptivePath.Clear();
        Vector2 start = owner.GetCurrentPosition();
        Vector2 target = owner.ClampAdvancedStatePoint(destination, 0.8f);
        if (!owner.TryBuildAdvancedStatePath(start, target, adaptivePath))
        {
            adaptivePath.Add(start);
            adaptivePath.Add(target);
        }
        adaptivePathIndex = 1;
    }

    private Vector2 GetAveragePlayerVelocity(float memorySeconds)
    {
        Vector2 sum = Vector2.zero;
        int count = 0;
        float oldest = Time.time - Mathf.Max(0.2f, memorySeconds);
        for (int i = motionHistory.Count - 1; i >= 0; i--)
        {
            if (motionHistory[i].time < oldest)
            {
                break;
            }
            sum += motionHistory[i].velocity;
            count++;
        }
        return count > 0 ? sum / count : player.CurrentVelocity;
    }

    private Vector2 GetPlayerVelocityTrend(float memorySeconds)
    {
        if (motionHistory.Count < 2)
        {
            return Vector2.zero;
        }

        float oldestTime = Time.time - Mathf.Max(0.3f, memorySeconds);
        MotionSample newest = motionHistory[motionHistory.Count - 1];
        MotionSample oldest = newest;
        for (int i = motionHistory.Count - 2; i >= 0; i--)
        {
            oldest = motionHistory[i];
            if (oldest.time <= oldestTime)
            {
                break;
            }
        }

        float elapsed = Mathf.Max(0.08f, newest.time - oldest.time);
        return Vector2.ClampMagnitude((newest.velocity - oldest.velocity) / elapsed, 8f);
    }

    private void BeginSignalTether()
    {
        ResetCycleRoot("SignalTether");
        movementOverrideActive = false;
        movementOverrideVelocity = Vector2.zero;
        tetherBlockedTimer = 0f;
        tetherPulseTimer = Mathf.Max(0.25f, tetherPulseInterval);
        tetherResetTimer = 0f;
        tetherExposure = 0f;
        tetherLine = CreateSprite(cycleRoot.transform, "SignalTetherLine", SquareSpriteProvider.Get(), vectorColor, 20);
        tetherEnemyRing = CreateSprite(cycleRoot.transform, "SignalTetherEnemyRing", CircleSpriteProvider.Get(), vectorColor, 21);
        tetherPlayerRing = CreateSprite(cycleRoot.transform, "SignalTetherPlayerRing", CircleSpriteProvider.Get(), vectorColor, 21);
        UpdateStateLabel("BREAK LINE OF SIGHT", vectorColor);
    }

    private void TickSignalTether()
    {
        Vector2 enemyPosition = owner.GetCurrentPosition();
        Vector2 playerPosition = player.GetPosition();
        Vector2 delta = playerPosition - enemyPosition;
        bool blocked = !owner.HasAdvancedStateLineOfSight(enemyPosition, playerPosition);
        float pulse = 0.5f + Mathf.Sin(Time.time * 10f) * 0.22f;
        Color color = blocked ? topologyColorA : vectorColor;
        color.a = blocked ? 0.42f + pulse * 0.22f : 0.72f + pulse * 0.22f;
        SetLine(tetherLine, enemyPosition, playerPosition, blocked ? 0.045f : 0.095f, color);
        UpdateTetherRing(tetherEnemyRing, enemyPosition, color, pulse);
        UpdateTetherRing(tetherPlayerRing, playerPosition, color, pulse);

        if (tetherResetTimer > 0f)
        {
            tetherResetTimer -= Time.deltaTime;
            return;
        }

        if (blocked)
        {
            tetherBlockedTimer += Time.deltaTime;
            tetherExposure = Mathf.Max(0f, tetherExposure - Time.deltaTime * 2.4f);
            UpdateStateLabel($"SIGNAL BREAK {Mathf.CeilToInt(Mathf.Max(0f, tetherBreakHoldSeconds - tetherBlockedTimer) * 10f) / 10f:F1}s", topologyColorA);
            if (tetherBlockedTimer >= Mathf.Max(0.2f, tetherBreakHoldSeconds))
            {
                owner.ApplyContainmentLock(enemyPosition, Mathf.Max(0.25f, tetherBreakStun));
                player.AddFirewallCharge(4f);
                SpawnImpactBurst(enemyPosition, topologyColorA, 12, 1.25f);
                tetherBlockedTimer = 0f;
                tetherResetTimer = 1.15f;
                tetherExposure = 0f;
                UpdateStateLabel("TETHER SEVERED", topologyColorA);
            }
            return;
        }

        tetherBlockedTimer = Mathf.Max(0f, tetherBlockedTimer - Time.deltaTime * 1.8f);
        tetherExposure += Time.deltaTime;
        float pressure = Mathf.Clamp01(tetherExposure / Mathf.Max(0.5f, tetherExposureRampSeconds));
        owner.ApplyExternalSpeedModifier(Mathf.Lerp(1.16f, 1.42f, pressure), 0.2f);
        UpdateStateLabel(pressure >= 0.66f ? "TETHER CRITICAL - FIND COVER" : "USE COVER TO BREAK TETHER", vectorColor);
        tetherPulseTimer -= Time.deltaTime;
        if (tetherPulseTimer > 0f)
        {
            return;
        }

        tetherPulseTimer = Mathf.Lerp(
            Mathf.Max(0.28f, tetherPulseInterval),
            Mathf.Max(0.2f, tetherPulseInterval * 0.52f),
            pressure);
        Vector2 direction = delta.sqrMagnitude > 0.001f ? delta.normalized : Vector2.right;
        float distanceFactor = Mathf.InverseLerp(2f, 10f, delta.magnitude);
        float pull = Mathf.Lerp(tetherPlayerPull * 0.6f, tetherPlayerPull, distanceFactor) * Mathf.Lerp(1f, 1.5f, pressure);
        float advance = Mathf.Lerp(tetherEnemyAdvance * 0.6f, tetherEnemyAdvance, distanceFactor) * Mathf.Lerp(1f, 1.35f, pressure);
        player.ApplyExternalDisplacement(direction * pull);
        player.ApplyMovementSlow(Mathf.Lerp(0.9f, tetherPressureSlow, pressure), 0.42f);
        owner.ApplyExternalDisplacement(direction * advance);
        SpawnImpactBurst(Vector2.Lerp(enemyPosition, playerPosition, 0.5f), vectorColor, 10, Mathf.Lerp(0.8f, 1.25f, pressure));
    }

    private static void UpdateTetherRing(SpriteRenderer ring, Vector2 position, Color color, float pulse)
    {
        if (ring == null)
        {
            return;
        }
        ring.transform.position = position;
        ring.transform.localScale = Vector3.one * Mathf.Lerp(0.65f, 0.92f, pulse);
        ring.color = new Color(color.r, color.g, color.b, color.a * 0.42f);
    }

    private void BeginTopologyFold()
    {
        // Solo enlaza un eje por activacion para mantener legibles los portales durante la persecucion.
        ResetCycleRoot("TopologyFold");
        topologyLive = false;
        topologyScanTimer = 0f;
        topologyCooldowns.Clear();

        Vector2 separation = player.GetPosition() - owner.GetCurrentPosition();
        topologyHorizontal = Mathf.Abs(separation.x) >= Mathf.Abs(separation.y);
        CreateTopologyBands();
        UpdateStateLabel(topologyHorizontal ? "X EDGES LINKED" : "Y EDGES LINKED", topologyColorA);
    }

    private void TickTopologyFold()
    {
        topologyLive = stateAge >= Mathf.Max(0.3f, topologyWarmupSeconds);
        float warmup = Mathf.Clamp01(stateAge / Mathf.Max(0.3f, topologyWarmupSeconds));
        float pulse = 0.5f + Mathf.Sin(Time.time * (topologyLive ? 9f : 5f)) * 0.28f;

        for (int i = 0; i < topologyBands.Count; i++)
        {
            Color color = i % 2 == 0 ? topologyColorA : topologyColorB;
            color.a = Mathf.Lerp(0.16f, 0.72f, warmup) * pulse;
            topologyBands[i].color = color;
        }
        for (int i = 0; i < topologyArrows.Count; i++)
        {
            SpriteRenderer arrow = topologyArrows[i];
            Color color = i % 2 == 0 ? topologyColorA : topologyColorB;
            color.a = Mathf.Lerp(0.24f, 0.9f, warmup);
            arrow.color = color;
            float wave = Mathf.Repeat(Time.time * 1.7f + i * 0.13f, 1f);
            arrow.transform.localScale = new Vector3(0.22f + wave * 0.12f, 0.08f, 1f);
        }

        if (!topologyLive)
        {
            return;
        }

        TickTopologyCooldowns();
        TryWrapPlayer();
        TryWrapEnemy();

        topologyScanTimer += Time.deltaTime;
        if (topologyScanTimer >= Mathf.Max(0.03f, topologyScanInterval))
        {
            topologyScanTimer = 0f;
            TryWrapProjectilesAndClone();
        }
    }

    private void CreateTopologyBands()
    {
        owner.GetAdvancedStateArena(out Vector2 origin, out Vector2 size);
        float depth = Mathf.Max(0.25f, topologyEdgeDepth);
        for (int side = -1; side <= 1; side += 2)
        {
            SpriteRenderer band = CreateSprite(cycleRoot.transform, $"PortalBand_{side}", SquareSpriteProvider.Get(), topologyColorA, 17);
            if (topologyHorizontal)
            {
                band.transform.position = new Vector2(side < 0 ? origin.x + depth * 0.5f : origin.x + size.x - depth * 0.5f, origin.y + size.y * 0.5f);
                band.transform.localScale = new Vector3(depth, size.y, 1f);
            }
            else
            {
                band.transform.position = new Vector2(origin.x + size.x * 0.5f, side < 0 ? origin.y + depth * 0.5f : origin.y + size.y - depth * 0.5f);
                band.transform.localScale = new Vector3(size.x, depth, 1f);
            }
            topologyBands.Add(band);

            for (int i = 0; i < 9; i++)
            {
                SpriteRenderer arrow = CreateSprite(cycleRoot.transform, $"PortalArrow_{side}_{i}", SquareSpriteProvider.Get(), topologyColorB, 19);
                float t = (i + 1f) / 10f;
                if (topologyHorizontal)
                {
                    arrow.transform.position = new Vector2(
                        side < 0 ? origin.x + depth * 0.55f : origin.x + size.x - depth * 0.55f,
                        origin.y + size.y * t);
                    arrow.transform.rotation = Quaternion.Euler(0f, 0f, side < 0 ? 0f : 180f);
                }
                else
                {
                    arrow.transform.position = new Vector2(
                        origin.x + size.x * t,
                        side < 0 ? origin.y + depth * 0.55f : origin.y + size.y - depth * 0.55f);
                    arrow.transform.rotation = Quaternion.Euler(0f, 0f, side < 0 ? 90f : -90f);
                }
                topologyArrows.Add(arrow);
            }
        }
    }

    private void TryWrapPlayer()
    {
        Vector2 position = player.GetPosition();
        Vector2 outwardIntent = player.CurrentMoveInput.sqrMagnitude > 0.01f
            ? player.CurrentMoveInput
            : player.CurrentVelocity;
        if (!TryGetWrappedPosition(position, outwardIntent, true, out Vector2 wrapped))
        {
            return;
        }

        int id = player.GetInstanceID();
        if (IsTopologyCoolingDown(id))
        {
            return;
        }

        player.TeleportTo(wrapped, true);
        MarkTopologyCooldown(id);
        SpawnTopologyTransfer(position, wrapped);
    }

    private void TryWrapEnemy()
    {
        Vector2 position = owner.GetCurrentPosition();
        if (!TryGetWrappedPosition(position, owner.CurrentVelocity, false, out Vector2 wrapped))
        {
            return;
        }

        int id = owner.GetInstanceID();
        if (IsTopologyCoolingDown(id))
        {
            return;
        }

        owner.TeleportForAdvancedState(wrapped, true);
        MarkTopologyCooldown(id);
        SpawnTopologyTransfer(position, wrapped);
    }

    private void TryWrapProjectilesAndClone()
    {
        // El pliegue afecta a todos los actores moviles y conserva su direccion al cruzar el borde.
        AnomalyProjectile[] projectiles = FindObjectsByType<AnomalyProjectile>();
        for (int i = 0; i < projectiles.Length; i++)
        {
            AnomalyProjectile projectile = projectiles[i];
            if (projectile == null || IsTopologyCoolingDown(projectile.GetInstanceID()))
            {
                continue;
            }

            Vector2 position = projectile.transform.position;
            if (!TryGetWrappedPosition(position, projectile.CurrentVelocity, false, out Vector2 wrapped))
            {
                continue;
            }

            projectile.ApplyExternalDisplacement(wrapped - position);
            MarkTopologyCooldown(projectile.GetInstanceID());
            SpawnTopologyTransfer(position, wrapped);
        }

        SplitAnomalyCloneController clone = FindAnyObjectByType<SplitAnomalyCloneController>();
        if (clone == null || IsTopologyCoolingDown(clone.GetInstanceID()))
        {
            return;
        }

        Vector2 clonePosition = clone.GetCurrentPosition();
        if (TryGetWrappedPosition(clonePosition, clone.CurrentVelocity, false, out Vector2 cloneWrapped))
        {
            clone.ApplyExternalDisplacement(cloneWrapped - clonePosition);
            MarkTopologyCooldown(clone.GetInstanceID());
            SpawnTopologyTransfer(clonePosition, cloneWrapped);
        }
    }

    private bool TryGetWrappedPosition(Vector2 position, Vector2 movementIntent, bool acceptWallContact, out Vector2 wrapped)
    {
        owner.GetAdvancedStateArena(out Vector2 origin, out Vector2 size);
        float depth = Mathf.Max(0.25f, topologyEdgeDepth);
        float inset = Mathf.Max(depth + 0.1f, topologyExitInset);
        wrapped = position;

        if (topologyHorizontal)
        {
            if (position.x <= origin.x + depth && (movementIntent.x < -0.05f || acceptWallContact))
            {
                wrapped.x = origin.x + size.x - inset;
                return true;
            }
            if (position.x >= origin.x + size.x - depth && (movementIntent.x > 0.05f || acceptWallContact))
            {
                wrapped.x = origin.x + inset;
                return true;
            }
        }
        else
        {
            if (position.y <= origin.y + depth && (movementIntent.y < -0.05f || acceptWallContact))
            {
                wrapped.y = origin.y + size.y - inset;
                return true;
            }
            if (position.y >= origin.y + size.y - depth && (movementIntent.y > 0.05f || acceptWallContact))
            {
                wrapped.y = origin.y + inset;
                return true;
            }
        }

        return false;
    }

    private void TickTopologyCooldowns()
    {
        if (topologyCooldowns.Count == 0)
        {
            return;
        }

        List<int> expired = null;
        foreach (KeyValuePair<int, float> pair in topologyCooldowns)
        {
            if (pair.Value <= Time.time)
            {
                expired ??= new List<int>();
                expired.Add(pair.Key);
            }
        }

        if (expired == null)
        {
            return;
        }
        for (int i = 0; i < expired.Count; i++)
        {
            topologyCooldowns.Remove(expired[i]);
        }
    }

    private bool IsTopologyCoolingDown(int id)
    {
        return topologyCooldowns.TryGetValue(id, out float expiresAt) && expiresAt > Time.time;
    }

    private void MarkTopologyCooldown(int id)
    {
        topologyCooldowns[id] = Time.time + Mathf.Max(0.12f, topologyObjectCooldown);
    }

    private void SpawnTopologyTransfer(Vector2 origin, Vector2 destination)
    {
        SpawnImpactBurst(origin, topologyColorA, 8, 0.8f);
        SpawnImpactBurst(destination, topologyColorB, 8, 0.8f);
        GlitchAudioManager.PlayEnemyPhaseBlinkArrive(destination);
    }

    private void BeginBlindspotProtocol()
    {
        ResetCycleRoot("BlindspotProtocol");
        movementOverrideActive = true;
        movementOverrideVelocity = Vector2.zero;
        blindspotSearchTimer = 0f;
        blindspotHiddenCharge = 0f;
        blindspotAtCover = false;
        blindspotAmbushing = false;
        blindspotTargetRing = CreateSprite(cycleRoot.transform, "BlindspotTarget", CircleSpriteProvider.Get(), blindspotChargeColor, 21);
        blindspotSightLine = CreateSprite(cycleRoot.transform, "BlindspotSightLine", SquareSpriteProvider.Get(), blindspotRouteColor, 19);
        SelectBlindspotTarget();
    }

    private void TickBlindspotProtocol()
    {
        Vector2 enemyPosition = owner.GetCurrentPosition();
        Vector2 playerPosition = player.GetPosition();
        UpdateBlindspotPathVisual();
        bool visible = owner.HasAdvancedStateLineOfSight(playerPosition, enemyPosition);
        Color sightColor = visible ? new Color(1f, 0.38f, 0.48f, 0.9f) : new Color(0.48f, 1f, 0.78f, 0.62f);
        SetLine(blindspotSightLine, playerPosition, enemyPosition, visible ? 0.085f : 0.045f, sightColor);
        if (blindspotTargetRing != null)
        {
            float charge = Mathf.Clamp01(blindspotHiddenCharge / Mathf.Max(0.1f, blindspotHiddenChargeSeconds));
            float scale = Mathf.Lerp(0.85f, 1.5f, charge) + Mathf.Sin(Time.time * 8f) * 0.06f;
            blindspotTargetRing.transform.localScale = Vector3.one * scale;
            blindspotTargetRing.color = new Color(
                blindspotChargeColor.r,
                blindspotChargeColor.g,
                blindspotChargeColor.b,
                Mathf.Lerp(0.2f, 0.72f, charge));
        }

        if (blindspotAmbushing)
        {
            if (TickPathMovement(blindspotPath, ref blindspotPathIndex, blindspotAmbushSpeed))
            {
                blindspotAmbushing = false;
                blindspotAtCover = false;
                blindspotHiddenCharge = 0f;
                movementOverrideVelocity = Vector2.zero;
                SpawnImpactBurst(owner.GetCurrentPosition(), blindspotChargeColor, 16, 1.55f);
                SelectBlindspotTarget();
            }
            return;
        }

        if (!blindspotAtCover)
        {
            if (TickPathMovement(blindspotPath, ref blindspotPathIndex, blindspotRouteSpeed))
            {
                blindspotAtCover = true;
                movementOverrideVelocity = Vector2.zero;
            }
            UpdateStateLabel("SEEKING BLINDSPOT", blindspotRouteColor);
            return;
        }

        if (visible)
        {
            blindspotHiddenCharge = Mathf.Max(0f, blindspotHiddenCharge - Time.deltaTime * 2.2f);
            blindspotSearchTimer += Time.deltaTime;
            UpdateStateLabel("KEEP IT IN SIGHT", new Color(1f, 0.48f, 0.56f, 1f));
            if (blindspotSearchTimer >= Mathf.Max(0.2f, blindspotSearchInterval))
            {
                SelectBlindspotTarget();
            }
            return;
        }

        blindspotSearchTimer = 0f;
        blindspotHiddenCharge += Time.deltaTime;
        float remaining = Mathf.Max(0f, blindspotHiddenChargeSeconds - blindspotHiddenCharge);
        UpdateStateLabel($"AMBUSH CHARGING {remaining:F1}s", blindspotChargeColor);
        if (blindspotHiddenCharge < Mathf.Max(0.35f, blindspotHiddenChargeSeconds))
        {
            return;
        }

        Vector2 ambushTarget = owner.ClampAdvancedStatePoint(
            playerPosition + player.CurrentVelocity * Mathf.Max(0.1f, blindspotPredictionSeconds),
            0.75f);
        owner.FireAdvancedAmbushVolley(ambushTarget, Mathf.Max(1, blindspotVolleyProjectiles));
        blindspotPath.Clear();
        if (!owner.TryBuildAdvancedStatePath(enemyPosition, ambushTarget, blindspotPath))
        {
            blindspotPath.Add(enemyPosition);
            blindspotPath.Add(ambushTarget);
        }
        blindspotPathIndex = 1;
        blindspotAmbushing = true;
        UpdateBlindspotPathVisual();
        UpdateStateLabel("AMBUSH COMMITTED", blindspotChargeColor);
    }

    private void SelectBlindspotTarget()
    {
        blindspotSearchTimer = 0f;
        blindspotHiddenCharge = 0f;
        blindspotAtCover = false;
        blindspotAmbushing = false;
        Vector2 playerPosition = player.GetPosition();
        Vector2 enemyPosition = owner.GetCurrentPosition();
        float minRadius = Mathf.Min(blindspotRadiusRange.x, blindspotRadiusRange.y);
        float maxRadius = Mathf.Max(blindspotRadiusRange.x, blindspotRadiusRange.y);
        float bestScore = float.NegativeInfinity;
        List<Vector2> bestPath = null;

        for (int i = 0; i < Mathf.Max(8, blindspotCandidateCount); i++)
        {
            float angle = (Mathf.PI * 2f * i / Mathf.Max(8, blindspotCandidateCount)) + Random.Range(-0.16f, 0.16f);
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 candidate = owner.ClampAdvancedStatePoint(
                playerPosition + direction * Random.Range(minRadius, maxRadius),
                0.85f);
            if (owner.HasAdvancedStateLineOfSight(playerPosition, candidate))
            {
                continue;
            }

            List<Vector2> candidatePath = new List<Vector2>();
            if (!owner.TryBuildAdvancedStatePath(enemyPosition, candidate, candidatePath))
            {
                continue;
            }

            float distanceToPlayer = Vector2.Distance(candidate, playerPosition);
            float pathCost = Mathf.Max(1, candidatePath.Count);
            float behindPlayer = Mathf.Max(0f, Vector2.Dot(direction, -player.LastMoveDirection));
            float score = 5f - Mathf.Abs(distanceToPlayer - 4.5f) - pathCost * 0.08f + behindPlayer * 1.4f;
            if (score > bestScore)
            {
                bestScore = score;
                blindspotTarget = candidate;
                bestPath = candidatePath;
            }
        }

        blindspotPath.Clear();
        if (bestPath != null)
        {
            blindspotPath.AddRange(bestPath);
        }
        else
        {
            blindspotTarget = owner.ClampAdvancedStatePoint(
                playerPosition - player.LastMoveDirection.normalized * 4f,
                0.85f);
            blindspotPath.Add(enemyPosition);
            blindspotPath.Add(blindspotTarget);
        }

        blindspotPathIndex = 1;
        if (blindspotTargetRing != null)
        {
            blindspotTargetRing.transform.position = blindspotTarget;
            blindspotTargetRing.transform.localScale = Vector3.one * 1.15f;
            blindspotTargetRing.color = new Color(blindspotChargeColor.r, blindspotChargeColor.g, blindspotChargeColor.b, 0.32f);
        }
        UpdateBlindspotPathVisual();
    }

    private void UpdateBlindspotPathVisual()
    {
        while (blindspotPathLines.Count < Mathf.Max(0, blindspotPath.Count - 1))
        {
            blindspotPathLines.Add(CreateSprite(cycleRoot.transform, $"BlindspotRoute_{blindspotPathLines.Count}", SquareSpriteProvider.Get(), blindspotRouteColor, 18));
        }

        for (int i = 0; i < blindspotPathLines.Count; i++)
        {
            if (i >= blindspotPath.Count - 1)
            {
                blindspotPathLines[i].color = Color.clear;
                continue;
            }
            Color color = blindspotRouteColor;
            color.a = 0.2f + Mathf.Sin(Time.time * 8f + i) * 0.08f;
            SetLine(blindspotPathLines[i], blindspotPath[i], blindspotPath[i + 1], 0.055f, color);
        }
    }

    private void CreateStateLabel(string text)
    {
        GameObject labelObject = new GameObject("LevelThreeStateLabel");
        labelObject.transform.SetParent(visualRoot.transform, false);
        labelObject.transform.position = owner != null ? owner.GetCurrentPosition() + Vector2.up * 1.15f : transform.position;
        stateLabel = labelObject.AddComponent<TextMesh>();
        stateLabel.text = text;
        stateLabel.anchor = TextAnchor.MiddleCenter;
        stateLabel.alignment = TextAlignment.Center;
        stateLabel.characterSize = 0.11f;
        stateLabel.fontSize = 44;
        stateLabel.fontStyle = FontStyle.Bold;
        stateLabel.color = Color.white;
        MeshRenderer renderer = labelObject.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = 24;
        }
    }

    private void UpdateStateLabel(string text, Color color)
    {
        if (stateLabel == null)
        {
            return;
        }

        stateLabel.text = text;
        color.a = 0.96f;
        stateLabel.color = color;
    }

    private void ResetCycleRoot(string name)
    {
        if (cycleRoot != null)
        {
            Destroy(cycleRoot);
        }

        cycleRoot = new GameObject(name);
        cycleRoot.transform.SetParent(visualRoot.transform, false);
    }

    private static SpriteRenderer CreateSprite(Transform parent, string objectName, Sprite sprite, Color color, int sortingOrder)
    {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(parent, false);
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private Sprite GetOwnerSprite()
    {
        SpriteRenderer renderer = owner != null ? owner.GetComponent<SpriteRenderer>() : null;
        return renderer != null && renderer.sprite != null ? renderer.sprite : SquareSpriteProvider.Get();
    }

    private static void SetLine(SpriteRenderer line, Vector2 start, Vector2 end, float width, Color color)
    {
        if (line == null)
        {
            return;
        }

        Vector2 delta = end - start;
        float distance = delta.magnitude;
        line.transform.position = (start + end) * 0.5f;
        line.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        line.transform.localScale = new Vector3(distance, Mathf.Max(0.01f, width), 1f);
        line.color = color;
    }

    private static void UpdateRingTicks(List<SpriteRenderer> ticks, Vector2 center, float radius, Color color, float rotation)
    {
        for (int i = 0; i < ticks.Count; i++)
        {
            float angle = rotation + (Mathf.PI * 2f * i / ticks.Count);
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            SpriteRenderer tick = ticks[i];
            tick.transform.position = center + direction * radius;
            tick.transform.rotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
            tick.transform.localScale = new Vector3(0.24f, 0.055f, 1f);
            tick.color = color;
        }
    }

    private void SpawnImpactBurst(Vector2 position, Color color, int rayCount, float radius)
    {
        GameObject burst = new GameObject("LevelThreeImpactBurst");
        burst.transform.position = position;
        LevelThreeBurstFx fx = burst.AddComponent<LevelThreeBurstFx>();
        fx.Configure(color, Mathf.Clamp(rayCount, 6, 20), Mathf.Max(0.4f, radius));
    }

    private bool TickPathMovement(List<Vector2> path, ref int pathIndex, float speed)
    {
        if (path == null || path.Count < 2 || pathIndex >= path.Count)
        {
            movementOverrideVelocity = Vector2.zero;
            return true;
        }

        Vector2 position = owner.GetCurrentPosition();
        while (pathIndex < path.Count && Vector2.Distance(position, path[pathIndex]) <= 0.3f)
        {
            pathIndex++;
        }
        if (pathIndex >= path.Count)
        {
            movementOverrideVelocity = Vector2.zero;
            return true;
        }

        Vector2 direction = path[pathIndex] - position;
        movementOverrideVelocity = direction.sqrMagnitude > 0.001f
            ? direction.normalized * Mathf.Max(1f, speed)
            : Vector2.zero;
        return false;
    }

    private static bool IsLevelThreeState(EnemyController.AnomalyState state)
    {
        return state == EnemyController.AnomalyState.AdaptiveCountermeasure ||
               state == EnemyController.AnomalyState.SignalTether ||
               state == EnemyController.AnomalyState.BlindspotProtocol;
    }

    private static bool IsTopologyProjectileState(EnemyController.AnomalyState state)
    {
        return state == EnemyController.AnomalyState.ExpansionShoot ||
               state == EnemyController.AnomalyState.PincerBarrage ||
               state == EnemyController.AnomalyState.OrbitBarrage ||
               state == EnemyController.AnomalyState.SignalPossession;
    }

    private static string GetStateTitle(EnemyController.AnomalyState state)
    {
        switch (state)
        {
            case EnemyController.AnomalyState.AdaptiveCountermeasure:
                return "ADAPTIVE COUNTERMEASURE";
            case EnemyController.AnomalyState.SignalTether:
                return "SIGNAL TETHER";
            case EnemyController.AnomalyState.BlindspotProtocol:
                return "BLINDSPOT PROTOCOL";
            default:
                return "TOPOLOGY FOLD";
        }
    }

    private static string GetAdaptiveProfileLabel(AdaptiveProfile profile)
    {
        switch (profile)
        {
            case AdaptiveProfile.Velocity:
                return "TRAJECTORY";
            case AdaptiveProfile.Parry:
                return "DEFENSE";
            case AdaptiveProfile.Distance:
                return "DISTANCE";
            default:
                return "STILLNESS";
        }
    }
}

public class LevelThreeBurstFx : MonoBehaviour
{
    private readonly List<SpriteRenderer> rays = new List<SpriteRenderer>();
    private SpriteRenderer core;
    private Color color;
    private float radius;
    private float age;
    private const float Lifetime = 0.42f;

    public void Configure(Color burstColor, int rayCount, float burstRadius)
    {
        color = burstColor;
        radius = burstRadius;
        core = CreateRenderer("Core", CircleSpriteProvider.Get(), 24);

        for (int i = 0; i < rayCount; i++)
        {
            SpriteRenderer ray = CreateRenderer($"Ray_{i}", SquareSpriteProvider.Get(), 23);
            float angle = Mathf.PI * 2f * i / rayCount;
            ray.transform.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
            rays.Add(ray);
        }
    }

    private void Update()
    {
        age += Time.deltaTime;
        float progress = Mathf.Clamp01(age / Lifetime);
        float alpha = 1f - progress;
        if (core != null)
        {
            core.transform.localScale = Vector3.one * Mathf.Lerp(0.2f, radius * 0.7f, progress);
            core.color = new Color(color.r, color.g, color.b, alpha * 0.24f);
        }

        for (int i = 0; i < rays.Count; i++)
        {
            SpriteRenderer ray = rays[i];
            float angle = Mathf.PI * 2f * i / rays.Count;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            ray.transform.localPosition = direction * Mathf.Lerp(0.15f, radius, progress);
            ray.transform.localScale = new Vector3(Mathf.Lerp(0.35f, 0.08f, progress), 0.055f, 1f);
            ray.color = new Color(color.r, color.g, color.b, alpha);
        }

        if (age >= Lifetime)
        {
            Destroy(gameObject);
        }
    }

    private SpriteRenderer CreateRenderer(string objectName, Sprite sprite, int sortingOrder)
    {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(transform, false);
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }
}

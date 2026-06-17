using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArenaChaosDirector : MonoBehaviour
{
    // Coordina sistemas secundarios de la arena: mejoras, objetos de puntaje y eventos de peligro.
    [Header("Powerups")]
    [SerializeField] private bool enablePowerups = true;
    [SerializeField] private Vector2 powerupIntervalRange = new Vector2(8f, 13f);
    [SerializeField] private float powerupLifetime = 10f;
    [SerializeField] private float speedBoostMultiplier = 1.38f;
    [SerializeField] private float speedBoostDuration = 3.2f;
    [SerializeField] private float shieldDuration = 5.5f;
    [SerializeField, Range(0f, 1f)] private float shieldPickupChance = 0.35f;
    [SerializeField, Range(0f, 1f)] private float compactPickupChance = 0.24f;
    [SerializeField] private float compactDuration = 6.5f;
    [SerializeField, Range(0.35f, 0.88f)] private float compactScaleMultiplier = 0.58f;
    [SerializeField, Range(0.25f, 1f)] private float compactMoveMultiplier = 0.76f;
    [SerializeField] private int powerupScorePoints = 3;

    [Header("Score Pickups")]
    [SerializeField] private bool enableScorePickups = true;
    [SerializeField] private Vector2 scorePickupIntervalRange = new Vector2(0.9f, 1.8f);
    [SerializeField] private float scorePickupLifetime = 9f;
    [SerializeField] private int scorePickupPoints = 2;
    [SerializeField] private int scorePickupMaxAlive = 8;
    [SerializeField] private int scorePickupBurstMin = 1;
    [SerializeField] private int scorePickupBurstMax = 2;
    [SerializeField] private float scorePickupProbeRadius = 0.34f;
    [SerializeField, Range(0f, 1f)] private float dataCoreChance = 0.18f;
    [SerializeField] private int dataCoreScoreMultiplier = 4;
    [SerializeField] private float dataCoreFirewallChargeBonus = 10f;
    [SerializeField] private float dataCoreMinPlayerDistance = 5.2f;

    [Header("Mini Events")]
    [SerializeField] private bool enablePulseEvents = true;
    [SerializeField] private Vector2 pulseEventIntervalRange = new Vector2(10f, 16f);
    [SerializeField] private Vector2 pulseRadiusRange = new Vector2(1.2f, 1.85f);
    [SerializeField] private int pulseHazardsMin = 2;
    [SerializeField] private int pulseHazardsMax = 4;
    [SerializeField] private float pulsePreWarningSeconds = 0.9f;
    [SerializeField] private float pulseTelegraphSeconds = 0.9f;
    [SerializeField] private float pulseActiveSeconds = 2.3f;
    [SerializeField] private float pulseSpawnStagger = 0.15f;
    [SerializeField] private float pulseTargetLeadSeconds = 0.5f;
    [SerializeField] private float pulseClusterRadius = 2.8f;
    [SerializeField] private float pulseSlowMultiplier = 0.58f;
    [SerializeField] private float pulseSlowDuration = 0.24f;
    [SerializeField] private float pulseEnemyBoostMultiplier = 1.23f;
    [SerializeField] private float pulseEnemyBoostDuration = 1.1f;
    [SerializeField] private float pulseEnemyBoostCooldown = 0.33f;
    [SerializeField] private Color pulseColor = new Color(1f, 0.38f, 0.53f, 0.92f);

    [Header("Objective Events")]
    [SerializeField] private bool enableObjectiveEvents = true;
    [SerializeField] private Vector2 objectiveEventIntervalRange = new Vector2(24f, 34f);
    [SerializeField] private int objectiveNodeCount = 3;
    [SerializeField] private float objectiveNodeLifetime = 18f;
    [SerializeField] private float objectiveNodeActivationSeconds = 1.15f;
    [SerializeField] private float objectiveNodeProbeRadius = 0.7f;
    [SerializeField] private int objectiveCompleteScore = 18;
    [SerializeField] private Color objectiveNodeColor = new Color(0.50f, 0.96f, 1f, 1f);

    [Header("Breach Events")]
    [SerializeField] private bool enableBreachEvents = true;
    [SerializeField] private Vector2 breachEventIntervalRange = new Vector2(42f, 58f);
    [SerializeField] private float breachLifetime = 16f;
    [SerializeField] private Vector2 breachGateSize = new Vector2(2.6f, 1.1f);
    [SerializeField] private int breachScoreBonus = 24;
    [SerializeField] private float breachTelegraphSeconds = 5.2f;
    [SerializeField] private float breachSweepDuration = 13.5f;
    [SerializeField] private float breachEnemyGuideDuration = 6f;
    [SerializeField] private float breachTransitionDuration = 1.25f;
    [SerializeField] private float breachArrivalHoldSeconds = 2f;
    [SerializeField] private float breachArrivalFxDuration = 2.25f;
    [SerializeField] private float minimumBreachMapAgeSeconds = 50f;
    [SerializeField] private float breachSystemSuppressionWindowSeconds = 5f;
    [SerializeField] private float breachConsumedPlayerDeathDelay = 0.7f;
    [SerializeField] private float breachSweepPlayerConsumeMargin = 0.18f;
    [SerializeField] private Color breachColor = new Color(1f, 0.42f, 0.78f, 1f);

    [Header("Spawn Rules")]
    [SerializeField] private float edgePadding = 1.1f;
    [SerializeField] private float actorClearance = 2.2f;
    [SerializeField] private float pickupProbeRadius = 0.55f;
    [SerializeField] private float hazardProbeRadius = 0.9f;
    [SerializeField] private int spawnAttempts = 24;

    [Header("Event Banner")]
    [SerializeField] private float bannerDuration = 2f;

    [Header("Event Pressure")]
    [SerializeField] private float pulseEventPressureCost = 0.62f;
    [SerializeField] private float pulseEventPressureCooldown = 2.4f;
    [SerializeField] private float objectiveEventPressureCost = 0.82f;
    [SerializeField] private float objectiveEventPressureCooldown = 3.4f;
    [SerializeField] private float breachEventPressureCost = 1.7f;
    [SerializeField] private float breachEventPressureCooldown = 5.5f;

    private ProceduralArenaGenerator arena;
    private PlayerController player;
    private EnemyController enemy;
    private GameManager gameManager;

    private float powerupTimer;
    private float scorePickupTimer;
    private float pulseEventTimer;
    private float objectiveEventTimer;
    private float objectiveTimer;
    private float breachEventTimer;
    private float breachTimer;
    private ArenaPowerupPickup activePickup;
    private ArenaBreachGate activeBreachGate;
    private ArenaBreachDirectionIndicatorFx activeBreachIndicatorFx;
    private ArenaBreachSweepFx activeBreachSweepFx;
    private ArenaBreachTransitionFx activeBreachTransitionFx;
    private readonly List<ArenaScorePickup> activeScorePickups = new List<ArenaScorePickup>();
    private readonly List<ArenaObjectiveNode> activeObjectiveNodes = new List<ArenaObjectiveNode>();

    private string activeEventLabel = string.Empty;
    private float eventLabelTimer;
    private string activeWarningLabel = string.Empty;
    private float warningTimer;
    private float warningDuration;
    private bool pulseWasUnlocked;
    private bool pulseEventActive;
    private bool objectiveWasUnlocked;
    private bool objectiveEventActive;
    private int objectiveNodesActivated;
    private int objectiveNodesTotal;
    private bool breachWasUnlocked;
    private bool breachEventActive;
    private bool breachSweepStarted;
    private bool breachTransitionActive;
    private bool pendingBreachEnemyReentry;
    private bool breachPlayerConsumed;
    private bool operationModifiersApplied;
    private float breachTelegraphTimer;
    private float currentArenaPlayableSeconds;
    private Vector2 activeBreachPosition;
    private Vector2 activeBreachSweepDirection;
    private Vector2 pendingBreachEnemyPosition;
    private Vector2 pendingBreachEnemyEntryDirection;

    private Transform runtimeRoot;
    private const string PulseEventPressureKey = "ChaosContainmentPulse";
    private const string ObjectiveEventPressureKey = "ChaosObjectiveNodes";
    private const string BreachEventPressureKey = "ChaosBreach";

    public string ActiveEventLabel => eventLabelTimer > 0f ? activeEventLabel : string.Empty;
    public string ActiveWarningLabel => warningTimer > 0f ? activeWarningLabel : string.Empty;
    public float ActiveWarningNormalized => warningTimer > 0f ? Mathf.Clamp01(warningTimer / Mathf.Max(0.001f, warningDuration)) : 0f;
    public bool IsContainmentPulsePressureActive => pulseEventActive;

    public void Configure(ProceduralArenaGenerator arenaGenerator)
    {
        arena = arenaGenerator;
        RefreshReferences();
        EnsureRuntimeRoot();
        ApplyOperationModifiersOnce();
        ClearRuntimeObjects();
        pulseWasUnlocked = false;
        pulseEventActive = false;
        objectiveWasUnlocked = false;
        breachWasUnlocked = false;
        currentArenaPlayableSeconds = 0f;
        SchedulePowerup();
        ScheduleScorePickup();
        SchedulePulseEvent();
        ScheduleObjectiveEvent();
        ScheduleBreachEvent();
    }

    private void Start()
    {
        if (arena == null)
        {
            arena = GetComponent<ProceduralArenaGenerator>();
        }

        RefreshReferences();
        EnsureRuntimeRoot();
        ApplyOperationModifiersOnce();
        pulseWasUnlocked = false;
        pulseEventActive = false;
        objectiveWasUnlocked = false;
        breachWasUnlocked = false;
        currentArenaPlayableSeconds = 0f;
        SchedulePowerup();
        ScheduleScorePickup();
        SchedulePulseEvent();
        ScheduleObjectiveEvent();
        ScheduleBreachEvent();
    }

    private void ApplyOperationModifiersOnce()
    {
        if (operationModifiersApplied)
        {
            return;
        }

        operationModifiersApplied = true;
        ContainmentOperationStorage.OperationDefinition operation = ContainmentOperationStorage.SelectedOperation;
        if (operation.id == ContainmentOperationStorage.ExtractionId)
        {
            scorePickupIntervalRange = new Vector2(
                Mathf.Max(0.18f, scorePickupIntervalRange.x * 0.38f),
                Mathf.Max(0.34f, scorePickupIntervalRange.y * 0.48f));
            scorePickupMaxAlive = Mathf.Max(scorePickupMaxAlive + 8, 16);
            scorePickupBurstMin = Mathf.Max(scorePickupBurstMin, 3);
            scorePickupBurstMax = Mathf.Max(scorePickupBurstMax, 5);
            scorePickupLifetime = Mathf.Max(4.0f, scorePickupLifetime * 0.66f);
            dataCoreChance = Mathf.Clamp01(dataCoreChance + 0.34f);
            dataCoreScoreMultiplier = Mathf.Max(dataCoreScoreMultiplier + 1, 5);
            dataCoreFirewallChargeBonus += 5f;
        }
        else if (operation.id == ContainmentOperationStorage.BreachId)
        {
            breachEventIntervalRange = new Vector2(16f, 25f);
            minimumBreachMapAgeSeconds = Mathf.Max(20f, minimumBreachMapAgeSeconds - 30f);
            breachTelegraphSeconds += 2.0f;
            breachLifetime += 5.0f;
            breachScoreBonus += 24;
            breachGateSize += new Vector2(0.35f, 0.16f);
            breachEnemyGuideDuration += 1.4f;
        }
    }

    private void Update()
    {
        if (arena == null)
        {
            arena = GetComponent<ProceduralArenaGenerator>();
        }

        if (eventLabelTimer > 0f)
        {
            eventLabelTimer -= Time.deltaTime;
        }
        if (warningTimer > 0f)
        {
            warningTimer -= Time.deltaTime;
        }

        RefreshReferences();
        if (gameManager == null || !gameManager.IsRunActive || gameManager.IsGameOver)
        {
            return;
        }

        TrackCurrentArenaPlayTime();

        if (enablePowerups && activePickup == null)
        {
            powerupTimer -= Time.deltaTime;
            if (powerupTimer <= 0f)
            {
                TrySpawnPowerup();
                SchedulePowerup();
            }
        }

        if (enableScorePickups)
        {
            scorePickupTimer -= Time.deltaTime;
            if (scorePickupTimer <= 0f)
            {
                TrySpawnScorePickups();
                ScheduleScorePickup();
            }
        }

        bool breachUnlocked = ShouldRunBreachEvents();
        if (breachUnlocked && !breachWasUnlocked)
        {
            breachEventTimer = Mathf.Min(breachEventTimer, 3f);
        }
        breachWasUnlocked = breachUnlocked;
        RefreshBreachSensitiveSuppression(breachUnlocked);

        bool pulseUnlocked = ShouldRunPulseEvents();
        if (pulseUnlocked && !pulseWasUnlocked)
        {
            pulseEventTimer = Mathf.Min(pulseEventTimer, 0.75f);
        }
        pulseWasUnlocked = pulseUnlocked;

        bool objectiveUnlocked = ShouldRunObjectiveEvents();
        if (objectiveUnlocked && !objectiveWasUnlocked)
        {
            objectiveEventTimer = Mathf.Min(objectiveEventTimer, 1.5f);
        }
        objectiveWasUnlocked = objectiveUnlocked;

        bool breachBlockingMapEvents = breachEventActive || breachTransitionActive || IsBreachSuppressionWindowActive(breachUnlocked);
        bool bossLevelTwoBlockingPulse = IsBossLevelTwoStateActive();
        if (pulseUnlocked && bossLevelTwoBlockingPulse)
        {
            pulseEventTimer = Mathf.Max(pulseEventTimer, GetPressureRetryDelay());
        }

        if (pulseUnlocked &&
            !bossLevelTwoBlockingPulse &&
            !breachBlockingMapEvents &&
            !IsBreachSuppressionWindowActive(breachUnlocked) &&
            (enemy == null || !enemy.IsMapEventSuppressed()))
        {
            pulseEventTimer -= Time.deltaTime;
            if (pulseEventTimer <= 0f)
            {
                if (TryReserveDirectorEventPressure(PulseEventPressureKey, pulseEventPressureCost, EstimatePulseEventDuration(), pulseEventPressureCooldown))
                {
                    StartCoroutine(SpawnPulseEventRoutine());
                    SchedulePulseEvent();
                }
                else
                {
                    pulseEventTimer = GetPressureRetryDelay();
                }
            }
        }

        if (objectiveEventActive)
        {
            UpdateObjectiveEvent();
        }
        else if (objectiveUnlocked && !breachBlockingMapEvents && (enemy == null || !enemy.IsMapEventSuppressed()))
        {
            objectiveEventTimer -= Time.deltaTime;
            if (objectiveEventTimer <= 0f)
            {
                if (TryStartObjectiveEvent())
                {
                    ScheduleObjectiveEvent();
                }
                else
                {
                    objectiveEventTimer = GetPressureRetryDelay();
                }
            }
        }

        if (breachEventActive)
        {
            UpdateBreachEvent();
        }
        else if (breachUnlocked && !objectiveEventActive && !breachTransitionActive && (enemy == null || !enemy.IsMapEventSuppressed()))
        {
            breachEventTimer -= Time.deltaTime;
            if (breachEventTimer <= 0f && IsBreachMapOldEnough())
            {
                if (TryStartBreachEvent())
                {
                    ScheduleBreachEvent();
                }
                else
                {
                    breachEventTimer = GetPressureRetryDelay();
                }
            }
        }
    }

    public void NotifyPickupConsumed(ArenaPowerupPickup pickup)
    {
        if (pickup != null && pickup == activePickup)
        {
            activePickup = null;
            if (gameManager != null)
            {
                gameManager.AddScore(Mathf.Max(0, powerupScorePoints));
            }
        }
    }

    public void NotifyPickupDestroyed(ArenaPowerupPickup pickup)
    {
        if (pickup != null && pickup == activePickup)
        {
            activePickup = null;
        }
    }

    public void NotifyScorePickupConsumed(ArenaScorePickup pickup)
    {
        if (pickup == null)
        {
            return;
        }

        activeScorePickups.Remove(pickup);
    }

    public void NotifyScorePickupDestroyed(ArenaScorePickup pickup)
    {
        if (pickup == null)
        {
            return;
        }

        activeScorePickups.Remove(pickup);
    }

    public void NotifyObjectiveNodeActivated(ArenaObjectiveNode node)
    {
        if (!objectiveEventActive || node == null)
        {
            return;
        }

        activeObjectiveNodes.Remove(node);
        objectiveNodesActivated++;
        if (player != null)
        {
            player.AddFirewallChargeFromObjectiveNode();
        }

        RaiseEvent($"Node {objectiveNodesActivated}/{Mathf.Max(1, objectiveNodesTotal)} Synced");

        if (activeObjectiveNodes.Count <= 0)
        {
            CompleteObjectiveEvent();
        }
    }

    public void NotifyBreachEntered(ArenaBreachGate gate)
    {
        if (!breachEventActive || gate == null || gate != activeBreachGate)
        {
            return;
        }

        StartCoroutine(CompleteBreachEventRoutine(activeBreachGate.transform.position));
    }

    private void RefreshReferences()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<PlayerController>();
        }

        if (enemy == null)
        {
            enemy = FindAnyObjectByType<EnemyController>();
        }

        if (gameManager == null)
        {
            gameManager = FindAnyObjectByType<GameManager>();
        }
    }

    private void EnsureRuntimeRoot()
    {
        if (runtimeRoot != null)
        {
            return;
        }

        GameObject root = new GameObject("ChaosRuntime");
        root.transform.SetParent(transform, false);
        runtimeRoot = root.transform;
    }

    private void ClearRuntimeObjects()
    {
        if (runtimeRoot == null)
        {
            return;
        }

        for (int i = runtimeRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = runtimeRoot.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }

        activePickup = null;
        activeBreachGate = null;
        activeBreachIndicatorFx = null;
        activeBreachSweepFx = null;
        activeBreachTransitionFx = null;
        activeScorePickups.Clear();
        activeObjectiveNodes.Clear();
        activeWarningLabel = string.Empty;
        warningTimer = 0f;
        warningDuration = 0f;
        objectiveEventActive = false;
        objectiveTimer = 0f;
        objectiveNodesActivated = 0;
        objectiveNodesTotal = 0;
        breachEventActive = false;
        breachSweepStarted = false;
        breachTransitionActive = false;
        pendingBreachEnemyReentry = false;
        breachPlayerConsumed = false;
        breachTimer = 0f;
        breachTelegraphTimer = 0f;
    }

    private void SchedulePowerup()
    {
        float min = Mathf.Min(powerupIntervalRange.x, powerupIntervalRange.y);
        float max = Mathf.Max(powerupIntervalRange.x, powerupIntervalRange.y);
        powerupTimer = Random.Range(min, max);
    }

    private void SchedulePulseEvent()
    {
        float min = Mathf.Min(pulseEventIntervalRange.x, pulseEventIntervalRange.y);
        float max = Mathf.Max(pulseEventIntervalRange.x, pulseEventIntervalRange.y);
        pulseEventTimer = Random.Range(min, max);
    }

    private void ScheduleObjectiveEvent()
    {
        float min = Mathf.Min(objectiveEventIntervalRange.x, objectiveEventIntervalRange.y);
        float max = Mathf.Max(objectiveEventIntervalRange.x, objectiveEventIntervalRange.y);
        objectiveEventTimer = Random.Range(min, max);
    }

    private void ScheduleBreachEvent()
    {
        float min = Mathf.Min(breachEventIntervalRange.x, breachEventIntervalRange.y);
        float max = Mathf.Max(breachEventIntervalRange.x, breachEventIntervalRange.y);
        breachEventTimer = Random.Range(min, max);
    }

    private void ScheduleScorePickup()
    {
        float min = Mathf.Min(scorePickupIntervalRange.x, scorePickupIntervalRange.y);
        float max = Mathf.Max(scorePickupIntervalRange.x, scorePickupIntervalRange.y);
        scorePickupTimer = Random.Range(min, max);
    }

    private bool TryReserveDirectorEventPressure(string pressureKey, float cost, float duration, float cooldown)
    {
        return gameManager == null || gameManager.TryReserveEventPressure(pressureKey, cost, duration, cooldown);
    }

    private void ReleaseDirectorEventPressure(string pressureKey, float cooldown)
    {
        if (gameManager != null)
        {
            gameManager.ReleaseEventPressure(pressureKey, cooldown);
        }
    }

    private float GetPressureRetryDelay()
    {
        return gameManager != null ? gameManager.EventPressureRetryDelay : 1.25f;
    }

    private float EstimatePulseEventDuration()
    {
        int maxCount = Mathf.Max(1, Mathf.Max(pulseHazardsMin, pulseHazardsMax));
        return Mathf.Max(0f, pulsePreWarningSeconds)
            + Mathf.Max(0f, pulseTelegraphSeconds)
            + Mathf.Max(0f, pulseActiveSeconds)
            + Mathf.Max(0f, pulseSpawnStagger) * maxCount
            + 0.35f;
    }

    private void TrySpawnPowerup()
    {
        if (!TryFindSpawnPoint(pickupProbeRadius, out Vector2 position))
        {
            return;
        }

        ArenaPowerupPickup.PickupKind kind = RollPowerupKind();

        GameObject go = new GameObject($"Powerup_{kind}");
        go.transform.SetParent(runtimeRoot, false);
        go.transform.position = new Vector3(position.x, position.y, 0f);
        go.transform.localScale = Vector3.one;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetPowerupSprite(kind);
        sr.drawMode = SpriteDrawMode.Simple;
        sr.size = Vector2.one * 0.62f;
        sr.sortingOrder = 12;

        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.34f;

        ArenaPowerupPickup pickup = go.AddComponent<ArenaPowerupPickup>();
        pickup.Configure(this, kind, powerupLifetime, speedBoostMultiplier, speedBoostDuration, shieldDuration, compactDuration, compactScaleMultiplier, compactMoveMultiplier);
        activePickup = pickup;

        GlitchAudioManager.PlayPowerupSpawn(position);
        RaiseEvent(GetPowerupEventLabel(kind));
    }

    private ArenaPowerupPickup.PickupKind RollPowerupKind()
    {
        float shieldChance = Mathf.Clamp01(shieldPickupChance);
        float compactChance = Mathf.Clamp01(compactPickupChance);
        float totalSpecialChance = shieldChance + compactChance;
        if (totalSpecialChance > 0.92f)
        {
            float scale = 0.92f / totalSpecialChance;
            shieldChance *= scale;
            compactChance *= scale;
        }

        float roll = Random.value;
        if (roll < shieldChance)
        {
            return ArenaPowerupPickup.PickupKind.Shield;
        }

        if (roll < shieldChance + compactChance)
        {
            return ArenaPowerupPickup.PickupKind.Compact;
        }

        return ArenaPowerupPickup.PickupKind.SpeedBurst;
    }

    private static Sprite GetPowerupSprite(ArenaPowerupPickup.PickupKind kind)
    {
        switch (kind)
        {
            case ArenaPowerupPickup.PickupKind.Shield:
                return ShieldSpriteProvider.Get();
            case ArenaPowerupPickup.PickupKind.Compact:
                return CompactSpriteProvider.Get();
            default:
                return LightningSpriteProvider.Get();
        }
    }

    private static string GetPowerupEventLabel(ArenaPowerupPickup.PickupKind kind)
    {
        switch (kind)
        {
            case ArenaPowerupPickup.PickupKind.Shield:
                return "Shield Materialized";
            case ArenaPowerupPickup.PickupKind.Compact:
                return "Compact Core Materialized";
            default:
                return "Speed Core Materialized";
        }
    }

    private void TrySpawnScorePickups()
    {
        if (gameManager == null || !gameManager.IsRunActive || gameManager.IsGameOver)
        {
            return;
        }

        activeScorePickups.RemoveAll(p => p == null);
        int capacity = Mathf.Max(1, scorePickupMaxAlive) - activeScorePickups.Count;
        if (capacity <= 0)
        {
            return;
        }

        int burstMin = Mathf.Max(1, Mathf.Min(scorePickupBurstMin, scorePickupBurstMax));
        int burstMax = Mathf.Max(burstMin, Mathf.Max(scorePickupBurstMin, scorePickupBurstMax));
        int burst = Mathf.Min(capacity, Random.Range(burstMin, burstMax + 1));

        for (int i = 0; i < burst; i++)
        {
            bool dataCore = Random.value < Mathf.Clamp01(dataCoreChance);
            Vector2 position;
            bool foundPoint = dataCore
                ? TryFindDataCoreSpawnPoint(scorePickupProbeRadius, out position)
                : TryFindSpawnPoint(scorePickupProbeRadius, out position);
            if (!foundPoint)
            {
                continue;
            }

            GameObject go = new GameObject(dataCore ? "DataCore" : "ScoreDot");
            go.transform.SetParent(runtimeRoot, false);
            go.transform.position = new Vector3(position.x, position.y, 0f);
            go.transform.localScale = Vector3.one;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CircleSpriteProvider.Get();
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = Vector2.one * (dataCore ? 0.52f : 0.34f);
            sr.sortingOrder = 12;

            CircleCollider2D col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = dataCore ? 0.28f : 0.2f;

            ArenaScorePickup pickup = go.AddComponent<ArenaScorePickup>();
            int points = Mathf.Max(1, scorePickupPoints) * (dataCore ? Mathf.Max(1, dataCoreScoreMultiplier) : 1);
            pickup.Configure(this, gameManager, points, scorePickupLifetime, dataCore, dataCore ? dataCoreFirewallChargeBonus : 0f);
            activeScorePickups.Add(pickup);
        }
    }

    private bool TryStartObjectiveEvent()
    {
        if (objectiveEventActive || runtimeRoot == null)
        {
            return false;
        }

        if (!TryReserveDirectorEventPressure(
                ObjectiveEventPressureKey,
                objectiveEventPressureCost,
                Mathf.Max(2f, objectiveNodeLifetime),
                objectiveEventPressureCooldown))
        {
            return false;
        }

        int desiredCount = Mathf.Max(1, objectiveNodeCount);
        activeObjectiveNodes.Clear();
        objectiveNodesActivated = 0;
        objectiveNodesTotal = 0;

        for (int i = 0; i < desiredCount; i++)
        {
            if (!TryFindSpawnPoint(Mathf.Max(0.35f, objectiveNodeProbeRadius), out Vector2 position))
            {
                continue;
            }

            GameObject go = new GameObject($"ObjectiveNode_{i}");
            go.transform.SetParent(runtimeRoot, false);
            go.transform.position = new Vector3(position.x, position.y, 0f);
            go.transform.localScale = Vector3.one;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CircleSpriteProvider.Get();
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = Vector2.one * 0.82f;
            sr.sortingOrder = 12;

            CircleCollider2D col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.55f;

            ArenaObjectiveNode node = go.AddComponent<ArenaObjectiveNode>();
            node.Configure(
                this,
                Mathf.Max(0.2f, objectiveNodeActivationSeconds),
                Mathf.Max(2f, objectiveNodeLifetime),
                objectiveNodeColor);
            activeObjectiveNodes.Add(node);
        }

        if (activeObjectiveNodes.Count <= 0)
        {
            objectiveEventActive = false;
            ReleaseDirectorEventPressure(ObjectiveEventPressureKey, objectiveEventPressureCooldown);
            return false;
        }

        objectiveEventActive = true;
        objectiveTimer = Mathf.Max(2f, objectiveNodeLifetime);
        objectiveNodesTotal = activeObjectiveNodes.Count;
        RaiseWarning("Pisa los nodos celestes", 2.6f);
        RaiseEvent($"Mantener nodos 0/{activeObjectiveNodes.Count}");
        return true;
    }

    private void UpdateObjectiveEvent()
    {
        objectiveTimer -= Time.deltaTime;
        activeObjectiveNodes.RemoveAll(n => n == null);
        activeEventLabel = $"Pisa nodos celestes {objectiveNodesActivated}/{Mathf.Max(1, objectiveNodesTotal)} | {Mathf.CeilToInt(Mathf.Max(0f, objectiveTimer))}s";
        eventLabelTimer = 0.25f;

        if (activeObjectiveNodes.Count <= 0)
        {
            CompleteObjectiveEvent();
            return;
        }

        if (objectiveTimer <= 0f)
        {
            FailObjectiveEvent();
        }
    }

    private void CompleteObjectiveEvent()
    {
        if (!objectiveEventActive)
        {
            return;
        }

        objectiveEventActive = false;
        objectiveTimer = 0f;
        objectiveNodesTotal = 0;
        ClearObjectiveNodes();
        ReleaseDirectorEventPressure(ObjectiveEventPressureKey, objectiveEventPressureCooldown);
        if (gameManager != null)
        {
            gameManager.AddScore(Mathf.Max(0, objectiveCompleteScore));
        }

        RaiseEvent("Containment Synced");
    }

    private void FailObjectiveEvent()
    {
        if (!objectiveEventActive)
        {
            return;
        }

        objectiveEventActive = false;
        objectiveTimer = 0f;
        objectiveNodesTotal = 0;
        ClearObjectiveNodes();
        ReleaseDirectorEventPressure(ObjectiveEventPressureKey, objectiveEventPressureCooldown);
        RaiseWarning("Sync Failed", 1.2f);
    }

    private void ClearObjectiveNodes()
    {
        for (int i = activeObjectiveNodes.Count - 1; i >= 0; i--)
        {
            ArenaObjectiveNode node = activeObjectiveNodes[i];
            if (node == null)
            {
                activeObjectiveNodes.RemoveAt(i);
                continue;
            }

            Destroy(node.gameObject);
        }

        activeObjectiveNodes.Clear();
    }

    private bool TryStartBreachEvent()
    {
        if (breachEventActive || breachTransitionActive || objectiveEventActive || activeBreachGate != null || arena == null || runtimeRoot == null)
        {
            return false;
        }

        float pressureDuration = Mathf.Max(2f, breachTelegraphSeconds)
            + Mathf.Max(2f, breachLifetime)
            + Mathf.Max(0.25f, breachTransitionDuration)
            + Mathf.Max(0f, breachArrivalHoldSeconds)
            + 1.5f;
        if (!TryReserveDirectorEventPressure(BreachEventPressureKey, breachEventPressureCost, pressureDuration, breachEventPressureCooldown))
        {
            return false;
        }

        Vector2 position = GetBreachGatePosition(out float rotationZ, out Vector2 sweepDirection);
        GameObject go = new GameObject("SectorBreachGate");
        go.transform.SetParent(runtimeRoot, false);
        go.transform.position = new Vector3(position.x, position.y, 0f);
        go.transform.rotation = Quaternion.Euler(0f, 0f, rotationZ);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SquareSpriteProvider.Get();
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = breachGateSize;
        sr.sortingOrder = 16;

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = breachGateSize;

        activeBreachGate = go.AddComponent<ArenaBreachGate>();
        activeBreachGate.Configure(this, Mathf.Max(2f, breachLifetime), breachColor, breachGateSize);

        breachEventActive = true;
        breachTimer = Mathf.Max(2f, breachLifetime);
        breachSweepStarted = false;
        breachPlayerConsumed = false;
        breachTelegraphTimer = Mathf.Max(0.4f, breachTelegraphSeconds);
        activeBreachPosition = position;
        activeBreachSweepDirection = sweepDirection;
        SpawnBreachDirectionIndicator(position);
        gameManager?.NotifyBreachTutorialOpportunity();
        if (enemy != null)
        {
            enemy.GuideTowardBreach(position, Mathf.Max(0.5f, breachEnemyGuideDuration));
        }

        RaiseWarning("Brecha detectada", 2.2f);
        GlitchAudioManager.PlayBreachWarning(position);
        RaiseEvent($"Sigue las flechas | {Mathf.CeilToInt(breachTelegraphTimer)}s");
        return true;
    }

    private void UpdateBreachEvent()
    {
        if (activeBreachGate == null)
        {
            FailBreachEvent();
            return;
        }

        if (enemy != null)
        {
            enemy.GuideTowardBreach(activeBreachGate.transform.position, 0.35f);
        }

        if (!breachSweepStarted)
        {
            RefreshBreachSensitiveSuppression(true);
            breachTelegraphTimer -= Time.deltaTime;
            activeEventLabel = $"Corre hacia la brecha | {Mathf.CeilToInt(Mathf.Max(0f, breachTelegraphTimer))}s";
            eventLabelTimer = 0.25f;
            if (breachTelegraphTimer <= 0f)
            {
                StartBreachCollapse();
            }

            return;
        }

        RefreshBreachSensitiveSuppression(true);
        breachTimer -= Time.deltaTime;
        activeEventLabel = $"Escapa del glitch | {Mathf.CeilToInt(Mathf.Max(0f, breachTimer))}s";
        eventLabelTimer = 0.25f;

        if (TryConsumePlayerByBreachSweep())
        {
            return;
        }

        if (breachTimer <= 0f)
        {
            FailBreachEvent();
        }
    }

    private void StartBreachCollapse()
    {
        if (breachSweepStarted)
        {
            return;
        }

        breachSweepStarted = true;
        RefreshBreachSensitiveSuppression(true);
        breachTimer = Mathf.Max(2f, breachLifetime);
        if (activeBreachIndicatorFx != null)
        {
            activeBreachIndicatorFx.FadeOutAndDestroy();
            activeBreachIndicatorFx = null;
        }

        SpawnBreachSweep(activeBreachPosition, activeBreachSweepDirection);
        GlitchAudioManager.PlayBreachSweep(activeBreachPosition);
        RaiseWarning("El sector se desintegra", 2f);
    }

    private bool TryConsumePlayerByBreachSweep()
    {
        if (breachPlayerConsumed || player == null || activeBreachSweepFx == null)
        {
            return false;
        }

        if (!activeBreachSweepFx.HasConsumedPoint(player.GetPosition(), breachSweepPlayerConsumeMargin))
        {
            return false;
        }

        breachPlayerConsumed = true;
        StartCoroutine(ConsumePlayerByBreachSweepRoutine());
        return true;
    }

    private IEnumerator ConsumePlayerByBreachSweepRoutine()
    {
        breachEventActive = false;
        breachTimer = 0f;
        pendingBreachEnemyReentry = false;
        RefreshBreachSensitiveSuppression(true);

        if (activeBreachGate != null)
        {
            Destroy(activeBreachGate.gameObject);
            activeBreachGate = null;
        }
        if (activeBreachIndicatorFx != null)
        {
            activeBreachIndicatorFx.FadeOutAndDestroy();
            activeBreachIndicatorFx = null;
        }

        if (enemy != null)
        {
            enemy.ReappearFromBreach(enemy.GetCurrentPosition());
        }

        RaiseWarning("Consumido por el glitch", 1.2f);
        activeEventLabel = "El barrido te consumio";
        eventLabelTimer = 1.2f;
        Vector3 playerPosition = player != null ? player.transform.position : transform.position;
        GlitchAudioManager.PlayBreachFail(playerPosition);
        if (player != null)
        {
            player.BeginBreachConsumption(breachColor);
        }

        float delay = Mathf.Max(0.05f, breachConsumedPlayerDeathDelay);
        float elapsed = 0f;
        while (elapsed < delay)
        {
            RefreshBreachSensitiveSuppression(true);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (gameManager != null && player != null)
        {
            gameManager.RequestPlayerDefeat(player);
        }

        ReleaseDirectorEventPressure(BreachEventPressureKey, breachEventPressureCooldown);
    }

    private IEnumerator CompleteBreachEventRoutine(Vector2 breachPosition)
    {
        if (!breachEventActive || breachTransitionActive)
        {
            yield break;
        }

        breachTransitionActive = true;
        breachEventActive = false;
        breachTimer = 0f;
        if (activeBreachGate != null)
        {
            Destroy(activeBreachGate.gameObject);
            activeBreachGate = null;
        }
        if (activeBreachIndicatorFx != null)
        {
            activeBreachIndicatorFx.FadeOutAndDestroy();
            activeBreachIndicatorFx = null;
        }
        if (activeBreachSweepFx != null)
        {
            activeBreachSweepFx.DisableRestoreOnDestroy();
            Destroy(activeBreachSweepFx.gameObject);
            activeBreachSweepFx = null;
        }

        ClearObjectiveNodes();
        if (gameManager != null)
        {
            gameManager.AddScore(Mathf.Max(0, breachScoreBonus));
            gameManager.NotifyBreachEscaped();
        }

        RaiseWarning("Sector reconfigurado", 1.8f);
        RaiseEvent("Breach Complete");
        SpawnBreachTransitionFx();
        GlitchAudioManager.PlayBreachTransition(breachPosition);
        HideActorsForBreachTransition(breachPosition);

        float transitionSeconds = Mathf.Max(0.25f, breachTransitionDuration);
        float regenerateDelay = transitionSeconds * 0.52f;
        float arrivalHoldSeconds = Mathf.Max(0f, breachArrivalHoldSeconds);
        yield return new WaitForSeconds(regenerateDelay);

        arena.GenerateBreachShift();
        RepositionActorsAfterBreach(breachPosition, arrivalHoldSeconds > 0.05f, out Vector2 arrivalPosition, out Vector2 arrivalDirection);
        SpawnBreachArrivalFx(arrivalPosition, arrivalDirection);
        yield return new WaitForSeconds(Mathf.Max(0.02f, transitionSeconds - regenerateDelay));

        if (arrivalHoldSeconds > 0f)
        {
            yield return StartCoroutine(BreachArrivalHoldRoutine(arrivalHoldSeconds));
        }

        ReleasePendingBreachEnemy();
        RefreshBreachSensitiveSuppression(true);
        ResetCurrentArenaPlayTime();
        activeBreachTransitionFx = null;
        breachTransitionActive = false;
        ReleaseDirectorEventPressure(BreachEventPressureKey, breachEventPressureCooldown);
    }

    private void FailBreachEvent()
    {
        if (!breachEventActive)
        {
            return;
        }

        breachEventActive = false;
        pendingBreachEnemyReentry = false;
        breachTimer = 0f;
        if (activeBreachGate != null)
        {
            Destroy(activeBreachGate.gameObject);
            activeBreachGate = null;
        }
        if (activeBreachIndicatorFx != null)
        {
            activeBreachIndicatorFx.FadeOutAndDestroy();
            activeBreachIndicatorFx = null;
        }
        if (activeBreachSweepFx != null)
        {
            activeBreachSweepFx.DisableRestoreOnDestroy();
            Destroy(activeBreachSweepFx.gameObject);
            activeBreachSweepFx = null;
        }

        if (enemy != null)
        {
            enemy.ReappearFromBreach(enemy.GetCurrentPosition());
        }

        RaiseWarning("Consumido por el glitch", 1.2f);
        GlitchAudioManager.PlayBreachFail(player != null ? player.transform.position : transform.position);
        RefreshBreachSensitiveSuppression(true);
        if (gameManager != null && player != null)
        {
            gameManager.RequestPlayerDefeat(player);
        }

        ReleaseDirectorEventPressure(BreachEventPressureKey, breachEventPressureCooldown);
    }

    private void HideActorsForBreachTransition(Vector2 breachPosition)
    {
        if (enemy != null)
        {
            enemy.AbsorbIntoBreach(breachPosition);
        }
    }

    private void RepositionActorsAfterBreach(Vector2 oldBreachPosition, bool delayEnemyReentry, out Vector2 playerPos, out Vector2 entryDir)
    {
        playerPos = transform.position;
        entryDir = Vector2.right;
        if (arena == null)
        {
            return;
        }

        Vector2 center = transform.position;
        Vector2 fromCenter = oldBreachPosition - center;
        entryDir = fromCenter.sqrMagnitude > 0.001f ? -fromCenter.normalized : Vector2.right;

        Vector2 preferredPlayerPos = ClampPointToArena(center + entryDir * 2.6f, 2.3f);
        playerPos = preferredPlayerPos;
        if (TryFindFreePointNear(preferredPlayerPos, 0.55f, 4.2f, out Vector2 safePlayerPos))
        {
            playerPos = safePlayerPos;
        }

        if (player != null)
        {
            player.transform.position = playerPos;
            Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();
            if (playerRb != null)
            {
                playerRb.position = playerPos;
                playerRb.linearVelocity = Vector2.zero;
            }
        }

        if (enemy != null)
        {
            Vector2 enemyPos = ClampPointToArena(center - entryDir * Mathf.Max(6.8f, actorClearance * 2.8f), 2.4f);
            if (TryFindFarEnemyReentryPoint(playerPos, -entryDir, out Vector2 safeEnemyPos))
            {
                enemyPos = safeEnemyPos;
            }

            if (delayEnemyReentry)
            {
                pendingBreachEnemyPosition = enemyPos;
                pendingBreachEnemyEntryDirection = -entryDir;
                pendingBreachEnemyReentry = true;
            }
            else
            {
                enemy.ReappearFromBreach(enemyPos);
                pendingBreachEnemyReentry = false;
            }
        }
    }

    private IEnumerator BreachArrivalHoldRoutine(float seconds)
    {
        RaiseWarning("Sector estabilizando", Mathf.Min(1.4f, seconds));
        float remaining = Mathf.Max(0f, seconds);
        while (remaining > 0f)
        {
            activeEventLabel = $"Sector estabilizando | {Mathf.CeilToInt(remaining)}s";
            eventLabelTimer = 0.25f;
            remaining -= Time.deltaTime;
            yield return null;
        }
    }

    private void ReleasePendingBreachEnemy()
    {
        if (!pendingBreachEnemyReentry)
        {
            return;
        }

        pendingBreachEnemyReentry = false;
        if (enemy == null)
        {
            return;
        }

        enemy.ReappearFromBreach(pendingBreachEnemyPosition);
        SpawnBreachArrivalFx(pendingBreachEnemyPosition, pendingBreachEnemyEntryDirection, true);
        RaiseWarning("Anomalia reinsertada", 1f);
    }

    private Vector2 ClampPointToArena(Vector2 point, float inset)
    {
        if (arena == null)
        {
            return point;
        }

        float halfW = arena.ArenaWidth * 0.5f;
        float halfH = arena.ArenaHeight * 0.5f;
        Vector2 center = transform.position;
        float safeInset = Mathf.Max(0.2f, inset);
        point.x = Mathf.Clamp(point.x, center.x - halfW + safeInset, center.x + halfW - safeInset);
        point.y = Mathf.Clamp(point.y, center.y - halfH + safeInset, center.y + halfH - safeInset);
        return point;
    }

    private bool TryFindFreePointNear(Vector2 preferred, float probeRadius, float maxSearchRadius, out Vector2 result)
    {
        result = ClampPointToArena(preferred, edgePadding + probeRadius + 0.2f);
        if (IsFree(result, probeRadius))
        {
            return true;
        }

        int rings = 5;
        int samples = 16;
        for (int r = 1; r <= rings; r++)
        {
            float radius = Mathf.Lerp(0.6f, Mathf.Max(0.7f, maxSearchRadius), r / (float)rings);
            for (int i = 0; i < samples; i++)
            {
                float angle = (Mathf.PI * 2f) * (i / (float)samples);
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                Vector2 candidate = ClampPointToArena(preferred + offset, edgePadding + probeRadius + 0.2f);
                if (!IsFree(candidate, probeRadius))
                {
                    continue;
                }

                result = candidate;
                return true;
            }
        }

        return false;
    }

    private bool TryFindFarEnemyReentryPoint(Vector2 playerPos, Vector2 preferredAwayDirection, out Vector2 result)
    {
        result = Vector2.zero;
        if (arena == null)
        {
            return false;
        }

        Vector2 away = preferredAwayDirection.sqrMagnitude > 0.001f
            ? preferredAwayDirection.normalized
            : ((Vector2)transform.position - playerPos).normalized;
        if (away.sqrMagnitude < 0.001f)
        {
            away = Vector2.right;
        }

        Vector2 side = new Vector2(-away.y, away.x);
        float minAxis = Mathf.Min(arena.ArenaWidth, arena.ArenaHeight);
        float maxAxis = Mathf.Max(arena.ArenaWidth, arena.ArenaHeight);
        float minDistance = Mathf.Min(Mathf.Max(6.8f, actorClearance * 2.8f), Mathf.Max(4.5f, minAxis * 0.42f));
        float maxDistance = Mathf.Max(minDistance + 0.75f, maxAxis * 0.38f);
        float maxSideOffset = Mathf.Max(1.5f, minAxis * 0.28f);

        for (int band = 0; band < 6; band++)
        {
            float distance = Mathf.Lerp(minDistance, maxDistance, band / 5f);
            for (int i = -3; i <= 3; i++)
            {
                float sideOffset = i == 0 ? 0f : (i / 3f) * maxSideOffset;
                Vector2 candidate = ClampPointToArena(playerPos + away * distance + side * sideOffset, edgePadding + 0.8f);
                if (Vector2.Distance(candidate, playerPos) < minDistance || !IsFree(candidate, 0.7f))
                {
                    continue;
                }

                result = candidate;
                return true;
            }
        }

        float halfW = arena.ArenaWidth * 0.5f;
        float halfH = arena.ArenaHeight * 0.5f;
        Vector2 center = transform.position;
        for (int i = 0; i < spawnAttempts * 3; i++)
        {
            Vector2 candidate = new Vector2(
                Random.Range(center.x - halfW + edgePadding + 0.8f, center.x + halfW - edgePadding - 0.8f),
                Random.Range(center.y - halfH + edgePadding + 0.8f, center.y + halfH - edgePadding - 0.8f));

            if (Vector2.Distance(candidate, playerPos) < minDistance || !IsFree(candidate, 0.7f))
            {
                continue;
            }

            result = candidate;
            return true;
        }

        return false;
    }

    private void SpawnBreachTransitionFx()
    {
        GameObject fx = new GameObject("BreachSectorTransitionFx");
        fx.transform.SetParent(runtimeRoot, false);
        activeBreachTransitionFx = fx.AddComponent<ArenaBreachTransitionFx>();
        activeBreachTransitionFx.Configure(arena, Mathf.Max(0.1f, breachTransitionDuration), breachColor);
    }

    private void SpawnBreachArrivalFx(Vector2 position, Vector2 entryDirection, bool enemyReentry = false)
    {
        if (enemyReentry)
        {
            GlitchAudioManager.PlayBreachEnemyReentry(position);
        }
        else
        {
            GlitchAudioManager.PlayBreachArrival(position);
        }

        if (runtimeRoot == null)
        {
            return;
        }

        GameObject fx = new GameObject("BreachArrivalFx");
        fx.transform.SetParent(runtimeRoot, false);
        ArenaBreachArrivalFx arrivalFx = fx.AddComponent<ArenaBreachArrivalFx>();
        arrivalFx.Configure(position, entryDirection, breachColor, Mathf.Max(0.75f, breachArrivalFxDuration));
    }

    private Vector2 GetBreachGatePosition(out float rotationZ, out Vector2 sweepDirection)
    {
        rotationZ = 0f;
        sweepDirection = Vector2.right;
        if (arena == null)
        {
            return transform.position;
        }

        float halfW = arena.ArenaWidth * 0.5f;
        float halfH = arena.ArenaHeight * 0.5f;
        float inset = Mathf.Max(1.5f, edgePadding + 1.2f);
        Vector2 center = transform.position;
        Vector2 playerPosition = player != null ? player.GetPosition() : center;

        float leftX = center.x - halfW + inset;
        float rightX = center.x + halfW - inset;
        float topY = center.y + halfH - inset;
        float bottomY = center.y - halfH + inset;

        float leftDistance = Mathf.Abs(playerPosition.x - leftX);
        float rightDistance = Mathf.Abs(playerPosition.x - rightX);
        float topDistance = Mathf.Abs(playerPosition.y - topY);
        float bottomDistance = Mathf.Abs(playerPosition.y - bottomY);

        int side = 0;
        float best = leftDistance;
        if (rightDistance > best)
        {
            best = rightDistance;
            side = 1;
        }
        if (topDistance > best)
        {
            best = topDistance;
            side = 2;
        }
        if (bottomDistance > best)
        {
            side = 3;
        }

        float clampedY = Mathf.Clamp(playerPosition.y, center.y - halfH * 0.55f, center.y + halfH * 0.55f);
        float clampedX = Mathf.Clamp(playerPosition.x, center.x - halfW * 0.55f, center.x + halfW * 0.55f);

        if (side == 0)
        {
            rotationZ = 90f;
            sweepDirection = Vector2.left;
            return new Vector2(leftX, clampedY);
        }

        if (side == 1)
        {
            rotationZ = 90f;
            sweepDirection = Vector2.right;
            return new Vector2(rightX, clampedY);
        }

        if (side == 2)
        {
            rotationZ = 0f;
            sweepDirection = Vector2.up;
            return new Vector2(clampedX, topY);
        }

        rotationZ = 0f;
        sweepDirection = Vector2.down;
        return new Vector2(clampedX, bottomY);
    }

    private void SpawnBreachSweep(Vector2 breachPosition, Vector2 sweepDirection)
    {
        if (arena == null || runtimeRoot == null)
        {
            return;
        }

        GameObject sweep = new GameObject("BreachGlitchSweep");
        sweep.transform.SetParent(runtimeRoot, false);
        activeBreachSweepFx = sweep.AddComponent<ArenaBreachSweepFx>();
        activeBreachSweepFx.Configure(
            arena,
            breachPosition,
            sweepDirection,
            Mathf.Max(0.25f, breachSweepDuration),
            breachColor);
    }

    private void SpawnBreachDirectionIndicator(Vector2 breachPosition)
    {
        if (player == null || runtimeRoot == null)
        {
            return;
        }

        GameObject indicator = new GameObject("BreachDirectionIndicator");
        indicator.transform.SetParent(runtimeRoot, false);
        activeBreachIndicatorFx = indicator.AddComponent<ArenaBreachDirectionIndicatorFx>();
        activeBreachIndicatorFx.Configure(player.transform, breachPosition, Mathf.Max(0.4f, breachTelegraphSeconds), breachColor);
    }

    private IEnumerator SpawnPulseEventRoutine()
    {
        pulseEventActive = true;
        int minCount = Mathf.Max(1, Mathf.Min(pulseHazardsMin, pulseHazardsMax));
        int maxCount = Mathf.Max(minCount, Mathf.Max(pulseHazardsMin, pulseHazardsMax));
        int count = Random.Range(minCount, maxCount + 1);

        // Primero avisa y despues crea peligros cerca del foco de pelea para que el evento sea esquivable.
        RaiseWarning("Containment Pulse Incoming", pulsePreWarningSeconds);
        if (pulsePreWarningSeconds > 0f)
        {
            yield return new WaitForSeconds(Mathf.Max(0f, pulsePreWarningSeconds));
        }

        if (gameManager == null || !gameManager.IsRunActive || gameManager.IsGameOver)
        {
            pulseEventActive = false;
            yield break;
        }
        if (gameManager.IsBreachSensitiveSuppressionActive || breachEventActive || breachTransitionActive || IsBossLevelTwoStateActive())
        {
            SchedulePulseEvent();
            ReleaseDirectorEventPressure(PulseEventPressureKey, pulseEventPressureCooldown);
            pulseEventActive = false;
            yield break;
        }

        RaiseEvent("Containment Lock");

        Vector2 pulseCenter = GetPulseFocusCenter();
        float clusterRadius = Mathf.Max(0.75f, pulseClusterRadius);
        float basePhase = Random.Range(0f, 360f);
        float lastHazardSpawnTime = Time.time;
        int spawnedHazards = 0;

        // Distribuye los peligros como un anillo irregular para que el jugador pueda leer un patron.
        for (int i = 0; i < count; i++)
        {
            if (gameManager.IsBreachSensitiveSuppressionActive || breachEventActive || breachTransitionActive || IsBossLevelTwoStateActive())
            {
                SchedulePulseEvent();
                ReleaseDirectorEventPressure(PulseEventPressureKey, pulseEventPressureCooldown);
                pulseEventActive = false;
                yield break;
            }

            float ringAngle = basePhase + ((360f / Mathf.Max(1, count)) * i);
            Vector2 ringDirection = new Vector2(Mathf.Cos(ringAngle * Mathf.Deg2Rad), Mathf.Sin(ringAngle * Mathf.Deg2Rad));
            Vector2 preferred = pulseCenter + ringDirection * Random.Range(clusterRadius * 0.3f, clusterRadius);

            if (!TryFindPulseSpawnPoint(preferred, pulseCenter, hazardProbeRadius, clusterRadius, out Vector2 position))
            {
                continue;
            }

            float radius = Random.Range(
                Mathf.Min(pulseRadiusRange.x, pulseRadiusRange.y),
                Mathf.Max(pulseRadiusRange.x, pulseRadiusRange.y));

            GameObject zone = new GameObject($"PulseHazard_{i}");
            zone.transform.SetParent(runtimeRoot, false);
            zone.transform.position = new Vector3(position.x, position.y, 0f);
            zone.transform.localScale = Vector3.one;

            SpriteRenderer sr = zone.AddComponent<SpriteRenderer>();
            sr.sprite = CircleSpriteProvider.Get();
            sr.drawMode = SpriteDrawMode.Sliced;

            CircleCollider2D col = zone.AddComponent<CircleCollider2D>();
            col.isTrigger = true;

            ContainmentHazardZone hazard = zone.AddComponent<ContainmentHazardZone>();
            hazard.Configure(
                gameManager,
                player,
                enemy,
                radius,
                pulseTelegraphSeconds,
                pulseActiveSeconds,
                pulseColor,
                pulseSlowMultiplier,
                pulseSlowDuration,
                pulseEnemyBoostMultiplier,
                pulseEnemyBoostDuration,
                pulseEnemyBoostCooldown);

            spawnedHazards++;
            lastHazardSpawnTime = Time.time;

            float stagger = Mathf.Max(0f, pulseSpawnStagger);
            if (stagger > 0f && i < count - 1)
            {
                yield return new WaitForSeconds(stagger);
            }
        }

        if (spawnedHazards > 0)
        {
            float hazardLifetime = Mathf.Max(0f, pulseTelegraphSeconds) + Mathf.Max(0f, pulseActiveSeconds);
            float elapsedSinceLastSpawn = Time.time - lastHazardSpawnTime;
            float remainingLifetime = Mathf.Max(0f, hazardLifetime - elapsedSinceLastSpawn);
            if (remainingLifetime > 0f)
            {
                yield return new WaitForSeconds(remainingLifetime);
            }
        }

        pulseEventActive = false;
    }

    private bool TryFindSpawnPoint(float probeRadius, out Vector2 result)
    {
        result = Vector2.zero;
        if (arena == null)
        {
            return false;
        }

        float halfW = arena.ArenaWidth * 0.5f;
        float halfH = arena.ArenaHeight * 0.5f;
        Vector2 arenaCenter = transform.position;
        float minX = arenaCenter.x - halfW + edgePadding;
        float maxX = arenaCenter.x + halfW - edgePadding;
        float minY = arenaCenter.y - halfH + edgePadding;
        float maxY = arenaCenter.y + halfH - edgePadding;

        for (int i = 0; i < spawnAttempts; i++)
        {
            float x = Random.Range(minX, maxX);
            float y = Random.Range(minY, maxY);
            Vector2 point = new Vector2(x, y) + (Vector2)transform.position;

            if (!IsFarFromActors(point))
            {
                continue;
            }

            if (!IsFree(point, probeRadius))
            {
                continue;
            }

            result = point;
            return true;
        }

        return false;
    }

    private bool TryFindDataCoreSpawnPoint(float probeRadius, out Vector2 result)
    {
        result = Vector2.zero;
        if (arena == null)
        {
            return false;
        }

        float minDistance = Mathf.Max(actorClearance, dataCoreMinPlayerDistance);
        for (int i = 0; i < Mathf.Max(4, spawnAttempts); i++)
        {
            if (!TryFindSpawnPoint(probeRadius, out Vector2 candidate))
            {
                break;
            }

            if (player != null && Vector2.Distance(candidate, player.GetPosition()) < minDistance)
            {
                continue;
            }

            result = candidate;
            return true;
        }

        return TryFindSpawnPoint(probeRadius, out result);
    }

    private bool TryFindPulseSpawnPoint(
        Vector2 preferred,
        Vector2 center,
        float probeRadius,
        float clusterRadius,
        out Vector2 result)
    {
        result = Vector2.zero;
        if (arena == null)
        {
            return false;
        }

        float halfW = arena.ArenaWidth * 0.5f;
        float halfH = arena.ArenaHeight * 0.5f;
        Vector2 arenaCenter = transform.position;
        float minX = arenaCenter.x - halfW + edgePadding;
        float maxX = arenaCenter.x + halfW - edgePadding;
        float minY = arenaCenter.y - halfH + edgePadding;
        float maxY = arenaCenter.y + halfH - edgePadding;

        Vector2 clampedPreferred = new Vector2(
            Mathf.Clamp(preferred.x, minX, maxX),
            Mathf.Clamp(preferred.y, minY, maxY));

        if (Vector2.Distance(clampedPreferred, center) <= clusterRadius + 0.2f &&
            IsFree(clampedPreferred, probeRadius))
        {
            result = clampedPreferred;
            return true;
        }

        for (int i = 0; i < spawnAttempts; i++)
        {
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            if (randomDir.sqrMagnitude < 0.0001f)
            {
                randomDir = Vector2.right;
            }

            float randomRadius = Random.Range(clusterRadius * 0.2f, clusterRadius);
            Vector2 point = center + randomDir * randomRadius;
            point.x = Mathf.Clamp(point.x, minX, maxX);
            point.y = Mathf.Clamp(point.y, minY, maxY);

            if (!IsFree(point, probeRadius))
            {
                continue;
            }

            result = point;
            return true;
        }

        return TryFindSpawnPoint(probeRadius, out result);
    }

    private bool IsFarFromActors(Vector2 point)
    {
        float minDist = Mathf.Max(0.5f, actorClearance);
        if (player != null && Vector2.Distance(point, player.GetPosition()) < minDist)
        {
            return false;
        }

        if (enemy != null && Vector2.Distance(point, enemy.GetCurrentPosition()) < minDist)
        {
            return false;
        }

        return true;
    }

    private static bool IsFree(Vector2 point, float radius)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(point, Mathf.Max(0.05f, radius));
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.isTrigger)
            {
                continue;
            }

            if (hit.GetComponent<PlayerController>() != null || hit.GetComponent<EnemyController>() != null)
            {
                continue;
            }

            if (hit.GetComponent<SplitAnomalyCloneController>() != null)
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private Vector2 GetPulseFocusCenter()
    {
        Vector2 center = transform.position;
        if (player == null)
        {
            return center;
        }

        Vector2 predicted = player.GetPosition() + player.CurrentVelocity * Mathf.Max(0f, pulseTargetLeadSeconds);
        if (enemy != null)
        {
            Vector2 toPlayer = predicted - enemy.GetCurrentPosition();
            if (toPlayer.sqrMagnitude > 0.0001f)
            {
                predicted += toPlayer.normalized * 0.6f;
            }
        }

        if (arena != null)
        {
            float halfW = arena.ArenaWidth * 0.5f;
            float halfH = arena.ArenaHeight * 0.5f;
            Vector2 arenaCenter = transform.position;
            float minX = arenaCenter.x - halfW + edgePadding;
            float maxX = arenaCenter.x + halfW - edgePadding;
            float minY = arenaCenter.y - halfH + edgePadding;
            float maxY = arenaCenter.y + halfH - edgePadding;
            predicted.x = Mathf.Clamp(predicted.x, minX, maxX);
            predicted.y = Mathf.Clamp(predicted.y, minY, maxY);
        }

        return predicted;
    }

    private void RaiseEvent(string label)
    {
        activeEventLabel = label;
        eventLabelTimer = Mathf.Max(0.2f, bannerDuration);
    }

    private void RaiseWarning(string label, float duration)
    {
        activeWarningLabel = label;
        warningDuration = Mathf.Max(0.05f, duration);
        warningTimer = warningDuration;
    }

    private bool ShouldRunPulseEvents()
    {
        return enablePulseEvents && gameManager != null && gameManager.IsContainmentPulseUnlocked;
    }

    private bool IsBossLevelTwoStateActive()
    {
        if (enemy == null)
        {
            enemy = FindAnyObjectByType<EnemyController>();
        }

        return enemy != null && enemy.IsCurrentStateLevelTwo;
    }

    private bool ShouldRunObjectiveEvents()
    {
        return enableObjectiveEvents && gameManager != null && gameManager.AreMapEventsUnlocked;
    }

    private bool ShouldRunBreachEvents()
    {
        return enableBreachEvents && gameManager != null && gameManager.AreMapEventsUnlocked;
    }

    private void TrackCurrentArenaPlayTime()
    {
        if (breachEventActive || breachTransitionActive || pendingBreachEnemyReentry || breachPlayerConsumed)
        {
            return;
        }

        currentArenaPlayableSeconds += Time.deltaTime;
    }

    private void ResetCurrentArenaPlayTime()
    {
        currentArenaPlayableSeconds = 0f;
    }

    private bool IsBreachMapOldEnough()
    {
        return currentArenaPlayableSeconds >= Mathf.Max(0f, minimumBreachMapAgeSeconds);
    }

    private float GetBreachMapAgeRemaining()
    {
        return Mathf.Max(0f, Mathf.Max(0f, minimumBreachMapAgeSeconds) - currentArenaPlayableSeconds);
    }

    private float GetBreachSecondsUntilEligibleStart(bool breachUnlocked)
    {
        if (!breachUnlocked)
        {
            return float.PositiveInfinity;
        }

        return Mathf.Max(Mathf.Max(0f, breachEventTimer), GetBreachMapAgeRemaining());
    }

    private void RefreshBreachSensitiveSuppression(bool breachUnlocked)
    {
        if (gameManager == null || !IsBreachSuppressionWindowActive(breachUnlocked))
        {
            return;
        }

        gameManager.SuppressBreachSensitiveSystems(Mathf.Max(0.1f, breachSystemSuppressionWindowSeconds));
    }

    private bool IsBreachSuppressionWindowActive(bool breachUnlocked)
    {
        if (breachEventActive || breachTransitionActive || pendingBreachEnemyReentry || breachPlayerConsumed)
        {
            return true;
        }

        return breachSystemSuppressionWindowSeconds > 0f &&
               GetBreachSecondsUntilEligibleStart(breachUnlocked) <= Mathf.Max(0f, breachSystemSuppressionWindowSeconds);
    }
}

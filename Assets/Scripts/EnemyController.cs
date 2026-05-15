using System.Collections.Generic;
using UnityEngine;

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
        RicochetHunter,
        Destroyer
    }

    private enum BehaviorPattern
    {
        DirectChase,
        PredictiveIntercept,
        CutoffFlank,
        ErraticBurst
    }

    [Header("References")]
    [SerializeField] private PlayerController player;
    [SerializeField] private GameManager gameManager;

    [Header("Base Movement")]
    [SerializeField] private float baseMoveSpeed = 3.25f;
    [SerializeField] private float velocityResponsiveness = 24f;

    [Header("Pattern Timing")]
    [SerializeField] private float flankRetargetInterval = 1.4f;

    [Header("Predictive Pattern")]
    [SerializeField] private float minLeadTime = 0.1f;
    [SerializeField] private float maxLeadTime = 1.4f;

    [Header("Cutoff Pattern")]
    [SerializeField] private float flankRadius = 2.8f;
    [SerializeField] private float flankLeadFactor = 1.1f;

    [Header("Erratic Pattern")]
    [SerializeField] private float erraticDirectionRefresh = 0.35f;
    [SerializeField] private float erraticOffsetRadius = 2.7f;
    [SerializeField] private float erraticBurstMultiplier = 1.2f;

    [Header("Anomaly State Machine")]
    [SerializeField] private bool enableAdvancedStates = true;
    [SerializeField] private Vector2 stateDurationMultiplierRange = new Vector2(0.85f, 1.25f);
    [SerializeField] private float speedStateMultiplier = 1.55f;
    [SerializeField] private bool logStateChanges = false;

    [Header("Expansion Shoot")]
    [SerializeField] private int expansionShootProjectileCount = 10;
    [SerializeField] private float expansionShootInterval = 3f;
    [SerializeField] private float expansionShootProjectileSpeed = 8.5f;
    [SerializeField] private float expansionShootProjectileLifetime = 3.2f;
    [SerializeField] private float expansionShootSpawnRadius = 0.7f;
    [SerializeField] private Color expansionShootProjectileColor = new Color(1f, 0.48f, 0.63f, 1f);
    [SerializeField] private Vector2 expansionShootProjectileSize = new Vector2(0.24f, 0.24f);
    [SerializeField] private float expansionShootTelegraphLeadTime = 0.6f;
    [SerializeField] private Color expansionShootTelegraphColor = new Color(1f, 0.50f, 0.68f, 0.8f);
    [SerializeField] private float expansionShootTelegraphPulseSpeed = 8.5f;
    [SerializeField] private float expansionShootTelegraphRingRadius = 0.9f;
    [SerializeField] private Vector2 expansionShootTelegraphTickSize = new Vector2(0.22f, 0.06f);

    [Header("State Weights")]
    [SerializeField, Min(0f)] private float directChaseWeight = 1f;
    [SerializeField, Min(0f)] private float predictiveInterceptWeight = 1f;
    [SerializeField, Min(0f)] private float cutoffFlankWeight = 1f;
    [SerializeField, Min(0f)] private float erraticBurstWeight = 1f;
    [SerializeField, Min(0f)] private float splitWeight = 0.7f;
    [SerializeField, Min(0f)] private float expansionShootWeight = 0.8f;
    [SerializeField, Min(0f)] private float speedSurgeWeight = 0.9f;
    [SerializeField, Min(0f)] private float ricochetHunterWeight = 0.65f;
    [SerializeField, Min(0f)] private float destroyerWeight = 0.75f;

    public AnomalyState CurrentState => currentState;
    public string CurrentStateLabel => currentState.ToString();

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

    private Rigidbody2D rb;
    private Collider2D ownCollider;

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

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        ownCollider = GetComponent<Collider2D>();

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

        stuckCheckPosition = rb.position;
        SelectNextState(forceDifferent: false);
    }

    private void Update()
    {
        if (gameManager == null || player == null || gameManager.IsGameOver || !gameManager.IsRunActive)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        HandleStateSwitch();
        UpdatePatternInternals();
        UpdateStateAbilities();

        gridRefreshTimer += Time.deltaTime;
        if (gridRefreshTimer >= gridRefreshInterval)
        {
            gridRefreshTimer = 0f;
            BuildNavigationGrid();
        }

        Vector2 strategicTarget = ClampToArena(GetStrategicTarget());

        repathTimer += Time.deltaTime;
        bool targetMoved = Vector2.Distance(strategicTarget, lastPathGoal) >= targetRepathThreshold;
        if (repathTimer >= repathInterval || targetMoved || pathWorld.Count == 0)
        {
            RebuildPathTo(strategicTarget);
        }

        Vector2 steeringTarget = SelectSteeringTarget(strategicTarget);
        Vector2 desiredDirection = steeringTarget - rb.position;

        if (desiredDirection.sqrMagnitude < 0.0001f)
        {
            desiredDirection = lastMoveDirection;
        }
        else
        {
            desiredDirection.Normalize();
        }

        desiredDirection = ApplyObstacleRepulsion(desiredDirection);
        if (desiredDirection.sqrMagnitude > 0.0001f)
        {
            lastMoveDirection = desiredDirection;
        }

        UpdateStuckDetection(strategicTarget);

        float speed = baseMoveSpeed * gameManager.DifficultyMultiplier;
        if (currentPattern == BehaviorPattern.ErraticBurst)
        {
            speed *= erraticBurstMultiplier;
        }

        if (currentState == AnomalyState.SpeedSurge)
        {
            speed *= speedStateMultiplier;
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
        if (stateTimer >= currentStateDuration)
        {
            SelectNextState(forceDifferent: true);
        }
    }

    private void SelectNextState(bool forceDifferent)
    {
        AnomalyState nextState = PickWeightedState(forceDifferent);
        currentState = nextState;
        currentPattern = ResolvePatternForState(currentState);
        stateTimer = 0f;

        float baseInterval = gameManager != null ? gameManager.CurrentBehaviorChangeInterval : 5f;
        float minMul = Mathf.Min(stateDurationMultiplierRange.x, stateDurationMultiplierRange.y);
        float maxMul = Mathf.Max(stateDurationMultiplierRange.x, stateDurationMultiplierRange.y);
        float durationMultiplier = Random.Range(minMul, maxMul);
        currentStateDuration = Mathf.Max(0.6f, baseInterval * durationMultiplier);

        if (logStateChanges)
        {
            Debug.Log($"[Anomaly] State -> {currentState} ({currentStateDuration:F2}s)");
        }

        OnStateEntered();
    }

    private void OnStateEntered()
    {
        expansionShootTimer = 0f;
        HideExpansionShootTelegraphVisual();

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

    private AnomalyState PickWeightedState(bool forceDifferent)
    {
        List<StateWeight> options = new List<StateWeight>
        {
            new StateWeight(AnomalyState.DirectChase, directChaseWeight),
            new StateWeight(AnomalyState.PredictiveIntercept, predictiveInterceptWeight),
            new StateWeight(AnomalyState.CutoffFlank, cutoffFlankWeight),
            new StateWeight(AnomalyState.ErraticBurst, erraticBurstWeight)
        };

        if (enableAdvancedStates)
        {
            options.Add(new StateWeight(AnomalyState.Split, splitWeight));
            options.Add(new StateWeight(AnomalyState.ExpansionShoot, expansionShootWeight));
            options.Add(new StateWeight(AnomalyState.SpeedSurge, speedSurgeWeight));
            options.Add(new StateWeight(AnomalyState.RicochetHunter, ricochetHunterWeight));
            options.Add(new StateWeight(AnomalyState.Destroyer, destroyerWeight));
        }

        if (forceDifferent && options.Count > 1)
        {
            options.RemoveAll(o => o.state == currentState);
        }

        float totalWeight = 0f;
        for (int i = 0; i < options.Count; i++)
        {
            totalWeight += Mathf.Max(0f, options[i].weight);
        }

        if (totalWeight <= 0.0001f)
        {
            return AnomalyState.DirectChase;
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
            case AnomalyState.RicochetHunter:
                return BehaviorPattern.ErraticBurst;
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
        if (currentState != AnomalyState.ExpansionShoot)
        {
            HideExpansionShootTelegraphVisual();
            return;
        }

        expansionShootTimer += Time.deltaTime;
        float interval = Mathf.Max(0.15f, expansionShootInterval);
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

    private void FireExpansionShoot()
    {
        int count = Mathf.Max(4, expansionShootProjectileCount);
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
        float enemySpeed = Mathf.Max(0.1f, baseMoveSpeed * gameManager.DifficultyMultiplier);

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

        if (rb.linearVelocity.sqrMagnitude < 0.2f || moved >= stuckDistanceThreshold)
        {
            return;
        }

        flankSide *= -1f;
        BuildNavigationGrid();
        RebuildPathTo(strategicTarget);
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

        if (collision.collider.GetComponent<PlayerController>() != null)
        {
            gameManager?.TriggerGameOver();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (gameManager == null || !gameManager.IsRunActive)
        {
            return;
        }

        if (other.GetComponent<PlayerController>() != null)
        {
            gameManager?.TriggerGameOver();
        }
    }
}

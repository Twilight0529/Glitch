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

    private struct DelayedVector
    {
        public float releaseTime;
        public Vector2 delta;
    }

    [Header("Adaptive Countermeasure")]
    [SerializeField] private float adaptiveReadSeconds = 1.55f;
    [SerializeField] private float adaptiveRevealSeconds = 0.65f;
    [SerializeField] private float adaptiveCycleSeconds = 4.5f;
    [SerializeField] private float adaptivePredictionSeconds = 0.72f;
    [SerializeField] private float adaptiveArrivalDistance = 1.45f;

    [Header("Vector Hijack")]
    [SerializeField] private float vectorDelay = 0.38f;
    [SerializeField] private float vectorGain = 0.82f;
    [SerializeField] private float vectorMaxStep = 0.2f;
    [SerializeField] private int vectorEchoCount = 5;

    [Header("Topology Fold")]
    [SerializeField] private float topologyWarmupSeconds = 1.65f;
    [SerializeField] private float topologyEdgeDepth = 0.72f;
    [SerializeField] private float topologyExitInset = 1.05f;
    [SerializeField] private float topologyObjectCooldown = 0.42f;
    [SerializeField] private float topologyScanInterval = 0.08f;

    [Header("Causal Fork")]
    [SerializeField] private float causalObserveSeconds = 1.25f;
    [SerializeField] private float causalRevealSeconds = 0.65f;
    [SerializeField] private float causalCollapseSeconds = 0.85f;
    [SerializeField] private float causalFutureSeconds = 1.35f;
    [SerializeField] private float causalHazardRadius = 0.55f;
    [SerializeField] private float causalSlowMultiplier = 0.56f;
    [SerializeField] private float causalSlowDuration = 1.1f;

    [Header("Visual Language")]
    [SerializeField] private Color adaptiveColor = new Color(1f, 0.35f, 0.72f, 1f);
    [SerializeField] private Color vectorColor = new Color(0.32f, 1f, 0.78f, 1f);
    [SerializeField] private Color topologyColorA = new Color(0.38f, 0.82f, 1f, 1f);
    [SerializeField] private Color topologyColorB = new Color(1f, 0.48f, 0.84f, 1f);
    [SerializeField] private Color causalColorA = new Color(1f, 0.76f, 0.28f, 1f);
    [SerializeField] private Color causalColorB = new Color(0.58f, 0.48f, 1f, 1f);

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

    private AdaptiveProfile adaptiveProfile;
    private float adaptiveCycleAge;
    private Vector2 adaptiveTarget;
    private bool adaptiveResolved;
    private SpriteRenderer adaptiveTargetRing;
    private SpriteRenderer adaptiveLine;
    private readonly List<SpriteRenderer> adaptiveTicks = new List<SpriteRenderer>();

    private readonly Queue<DelayedVector> delayedVectors = new Queue<DelayedVector>();
    private readonly List<SpriteRenderer> vectorEchoes = new List<SpriteRenderer>();
    private SpriteRenderer vectorTether;
    private SpriteRenderer vectorArrow;

    private bool topologyHorizontal;
    private bool topologyLive;
    private float topologyScanTimer;
    private readonly Dictionary<int, float> topologyCooldowns = new Dictionary<int, float>();
    private readonly List<SpriteRenderer> topologyBands = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> topologyArrows = new List<SpriteRenderer>();

    private float causalCycleAge;
    private bool causalBranchChosen;
    private bool causalResolved;
    private bool causalCollapseHit;
    private int causalChosenBranch;
    private readonly Vector2[] causalBranchA = new Vector2[4];
    private readonly Vector2[] causalBranchB = new Vector2[4];
    private readonly List<SpriteRenderer> causalLinesA = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> causalLinesB = new List<SpriteRenderer>();
    private SpriteRenderer causalGhostA;
    private SpriteRenderer causalGhostB;

    public bool IsActive => stateActive;

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
        switch (activeState)
        {
            case EnemyController.AnomalyState.AdaptiveCountermeasure:
                TickAdaptiveCountermeasure();
                break;
            case EnemyController.AnomalyState.VectorHijack:
                TickVectorHijack();
                break;
            case EnemyController.AnomalyState.TopologyFold:
                TickTopologyFold();
                break;
            case EnemyController.AnomalyState.CausalFork:
                TickCausalFork();
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
        if (!IsLevelThreeState(state))
        {
            return;
        }

        ResolveReferences();
        activeState = state;
        stateActive = true;
        stateAge = 0f;
        visualRoot = new GameObject($"LevelThree_{state}");
        CreateStateLabel(GetStateTitle(state));

        switch (state)
        {
            case EnemyController.AnomalyState.AdaptiveCountermeasure:
                BeginAdaptiveCycle();
                break;
            case EnemyController.AnomalyState.VectorHijack:
                BeginVectorHijack();
                break;
            case EnemyController.AnomalyState.TopologyFold:
                BeginTopologyFold();
                break;
            case EnemyController.AnomalyState.CausalFork:
                BeginCausalCycle();
                break;
        }
    }

    public void ExitState()
    {
        stateActive = false;
        delayedVectors.Clear();
        topologyCooldowns.Clear();
        adaptiveTicks.Clear();
        vectorEchoes.Clear();
        topologyBands.Clear();
        topologyArrows.Clear();
        causalLinesA.Clear();
        causalLinesB.Clear();
        topologyLive = false;

        if (visualRoot != null)
        {
            Destroy(visualRoot);
        }

        visualRoot = null;
        cycleRoot = null;
        stateLabel = null;
        adaptiveTargetRing = null;
        adaptiveLine = null;
        vectorTether = null;
        vectorArrow = null;
        causalGhostA = null;
        causalGhostB = null;
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

        UpdateStateLabel($"LECTURA: {GetAdaptiveProfileLabel(adaptiveProfile)}", adaptiveColor);
    }

    private void TickAdaptiveCountermeasure()
    {
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
        Vector2 velocity = player.CurrentVelocity;
        Vector2 enemyPosition = owner.GetCurrentPosition();
        Vector2 target;

        switch (profile)
        {
            case AdaptiveProfile.Velocity:
                target = playerPosition + velocity * adaptivePredictionSeconds;
                break;
            case AdaptiveProfile.Parry:
                Vector2 away = (playerPosition - enemyPosition).normalized;
                Vector2 side = new Vector2(-away.y, away.x) * (Vector2.Dot(velocity, new Vector2(-away.y, away.x)) >= 0f ? -1f : 1f);
                target = playerPosition + side * 2.2f;
                break;
            case AdaptiveProfile.Distance:
                Vector2 approach = (playerPosition - enemyPosition).normalized;
                target = playerPosition + approach * 1.8f;
                break;
            default:
                Vector2 center = owner.GetArenaCenter();
                Vector2 centerDirection = (center - playerPosition).normalized;
                target = playerPosition + centerDirection * 1.6f;
                break;
        }

        return owner.ClampAdvancedStatePoint(target, 0.9f);
    }

    private void ResolveAdaptiveCountermeasure()
    {
        Vector2 playerPosition = player.GetPosition();
        Vector2 fromPlayer = adaptiveTarget - playerPosition;
        if (fromPlayer.sqrMagnitude < 0.01f)
        {
            fromPlayer = (owner.GetCurrentPosition() - playerPosition).normalized;
        }

        Vector2 arrival = adaptiveTarget + fromPlayer.normalized * Mathf.Max(0.7f, adaptiveArrivalDistance);
        arrival = owner.ClampAdvancedStatePoint(arrival, 0.8f);
        owner.TeleportForAdvancedState(arrival, true);
        owner.ApplyContainmentLock(arrival, 0.16f);
        SpawnImpactBurst(arrival, adaptiveColor, 14, 1.7f);
        GlitchAudioManager.PlayEnemyPhaseBlinkArrive(arrival);
    }

    private void BeginVectorHijack()
    {
        ResetCycleRoot("VectorHijack");
        delayedVectors.Clear();
        lastPlayerPosition = player.GetPosition();
        vectorTether = CreateSprite(cycleRoot.transform, "VectorTether", SquareSpriteProvider.Get(), vectorColor, 18);
        vectorArrow = CreateSprite(cycleRoot.transform, "VectorArrow", SquareSpriteProvider.Get(), vectorColor, 21);

        vectorEchoes.Clear();
        int count = Mathf.Clamp(vectorEchoCount, 3, 8);
        for (int i = 0; i < count; i++)
        {
            Color echoColor = vectorColor;
            echoColor.a = Mathf.Lerp(0.12f, 0.5f, (i + 1f) / count);
            SpriteRenderer echo = CreateSprite(cycleRoot.transform, $"DelayedVector_{i}", GetPlayerSprite(), echoColor, 19);
            echo.transform.localScale = player.transform.localScale * 0.62f;
            vectorEchoes.Add(echo);
        }

        UpdateStateLabel("VECTOR ROBADO", vectorColor);
    }

    private void TickVectorHijack()
    {
        // Reproduce los vectores del jugador con retraso: el enlace es predecible, pero puede ser explotado.
        Vector2 playerPosition = player.GetPosition();
        Vector2 playerDelta = playerPosition - lastPlayerPosition;
        lastPlayerPosition = playerPosition;
        if (playerDelta.sqrMagnitude <= 1.6f)
        {
            delayedVectors.Enqueue(new DelayedVector
            {
                releaseTime = Time.time + Mathf.Max(0.08f, vectorDelay),
                delta = playerDelta
            });
        }

        while (delayedVectors.Count > 0 && delayedVectors.Peek().releaseTime <= Time.time)
        {
            DelayedVector delayed = delayedVectors.Dequeue();
            Vector2 step = Vector2.ClampMagnitude(delayed.delta * Mathf.Max(0f, vectorGain), Mathf.Max(0.03f, vectorMaxStep));
            Vector2 current = owner.GetCurrentPosition();
            Vector2 target = owner.ClampAdvancedStatePoint(current + step, 0.65f);
            owner.ApplyExternalDisplacement(target - current);
        }

        Vector2 enemyPosition = owner.GetCurrentPosition();
        float pulse = 0.55f + Mathf.Sin(Time.time * 12f) * 0.18f;
        Color tetherColor = vectorColor;
        tetherColor.a = pulse;
        SetLine(vectorTether, playerPosition, enemyPosition, 0.055f, tetherColor);

        Vector2 movement = player.CurrentVelocity;
        if (movement.sqrMagnitude < 0.05f)
        {
            movement = playerPosition - enemyPosition;
        }
        movement = movement.normalized;
        if (vectorArrow != null)
        {
            vectorArrow.transform.position = Vector2.Lerp(playerPosition, enemyPosition, 0.5f);
            vectorArrow.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(movement.y, movement.x) * Mathf.Rad2Deg);
            vectorArrow.transform.localScale = new Vector3(0.62f, 0.12f + pulse * 0.05f, 1f);
            vectorArrow.color = tetherColor;
        }

        for (int i = 0; i < vectorEchoes.Count; i++)
        {
            float t = (i + 1f) / (vectorEchoes.Count + 1f);
            SpriteRenderer echo = vectorEchoes[i];
            echo.transform.position = Vector2.Lerp(playerPosition, enemyPosition, t) - movement * Mathf.Sin(Time.time * 5f + i) * 0.12f;
            echo.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(movement.y, movement.x) * Mathf.Rad2Deg);
        }
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
        UpdateStateLabel(topologyHorizontal ? "BORDES X ENLAZADOS" : "BORDES Y ENLAZADOS", topologyColorA);
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
        Vector2 velocity = player.CurrentVelocity;
        if (!TryGetWrappedPosition(position, velocity, out Vector2 wrapped))
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
        if (!TryGetWrappedPosition(position, owner.CurrentVelocity, out Vector2 wrapped))
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
            if (!TryGetWrappedPosition(position, projectile.CurrentVelocity, out Vector2 wrapped))
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
        if (TryGetWrappedPosition(clonePosition, clone.CurrentVelocity, out Vector2 cloneWrapped))
        {
            clone.ApplyExternalDisplacement(cloneWrapped - clonePosition);
            MarkTopologyCooldown(clone.GetInstanceID());
            SpawnTopologyTransfer(clonePosition, cloneWrapped);
        }
    }

    private bool TryGetWrappedPosition(Vector2 position, Vector2 velocity, out Vector2 wrapped)
    {
        owner.GetAdvancedStateArena(out Vector2 origin, out Vector2 size);
        float depth = Mathf.Max(0.25f, topologyEdgeDepth);
        float inset = Mathf.Max(depth + 0.1f, topologyExitInset);
        wrapped = position;

        if (topologyHorizontal)
        {
            if (position.x <= origin.x + depth && velocity.x < -0.05f)
            {
                wrapped.x = origin.x + size.x - inset;
                return true;
            }
            if (position.x >= origin.x + size.x - depth && velocity.x > 0.05f)
            {
                wrapped.x = origin.x + inset;
                return true;
            }
        }
        else
        {
            if (position.y <= origin.y + depth && velocity.y < -0.05f)
            {
                wrapped.y = origin.y + size.y - inset;
                return true;
            }
            if (position.y >= origin.y + size.y - depth && velocity.y > 0.05f)
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

    private void BeginCausalCycle()
    {
        // Dibuja dos futuros completos; uno se confirma antes del salto y el otro colapsa como riesgo temporal.
        ResetCycleRoot("CausalFork");
        causalCycleAge = 0f;
        causalBranchChosen = false;
        causalResolved = false;
        causalCollapseHit = false;
        BuildCausalBranches();

        causalLinesA.Clear();
        causalLinesB.Clear();
        for (int i = 0; i < causalBranchA.Length - 1; i++)
        {
            causalLinesA.Add(CreateSprite(cycleRoot.transform, $"FutureA_{i}", SquareSpriteProvider.Get(), causalColorA, 19));
            causalLinesB.Add(CreateSprite(cycleRoot.transform, $"FutureB_{i}", SquareSpriteProvider.Get(), causalColorB, 19));
        }

        causalGhostA = CreateSprite(cycleRoot.transform, "FutureGhostA", GetOwnerSprite(), causalColorA, 21);
        causalGhostB = CreateSprite(cycleRoot.transform, "FutureGhostB", GetOwnerSprite(), causalColorB, 21);
        causalGhostA.transform.localScale = transform.localScale * 0.9f;
        causalGhostB.transform.localScale = transform.localScale * 0.9f;
        UpdateStateLabel("DOS FUTUROS POSIBLES", causalColorA);
    }

    private void TickCausalFork()
    {
        causalCycleAge += Time.deltaTime;
        float observe = Mathf.Max(0.55f, causalObserveSeconds);
        float reveal = Mathf.Max(0.25f, causalRevealSeconds);
        float collapse = Mathf.Max(0.35f, causalCollapseSeconds);

        if (!causalBranchChosen && causalCycleAge >= observe)
        {
            causalBranchChosen = true;
            causalChosenBranch = ChooseCausalBranch();
            UpdateStateLabel(causalChosenBranch == 0 ? "FUTURO A FIJADO" : "FUTURO B FIJADO",
                causalChosenBranch == 0 ? causalColorA : causalColorB);
        }

        float revealProgress = causalBranchChosen ? Mathf.Clamp01((causalCycleAge - observe) / reveal) : 0f;
        UpdateCausalVisuals(revealProgress);

        if (!causalResolved && causalCycleAge >= observe + reveal)
        {
            causalResolved = true;
            Vector2 destination = causalChosenBranch == 0 ? causalBranchA[causalBranchA.Length - 1] : causalBranchB[causalBranchB.Length - 1];
            owner.TeleportForAdvancedState(destination, true);
            owner.ApplyContainmentLock(destination, 0.12f);
            SpawnImpactBurst(destination, causalChosenBranch == 0 ? causalColorA : causalColorB, 16, 1.8f);
            GlitchAudioManager.PlayEnemyPhaseBlinkArrive(destination);
        }

        if (causalResolved)
        {
            float collapseProgress = Mathf.Clamp01((causalCycleAge - observe - reveal) / collapse);
            TickCausalCollapsedBranch(collapseProgress);
        }

        if (causalCycleAge >= observe + reveal + collapse + 1.35f)
        {
            BeginCausalCycle();
        }
    }

    private void BuildCausalBranches()
    {
        Vector2 start = owner.GetCurrentPosition();
        Vector2 playerPosition = player.GetPosition();
        Vector2 predicted = playerPosition + Vector2.ClampMagnitude(player.CurrentVelocity, 7f) * causalFutureSeconds;
        predicted = owner.ClampAdvancedStatePoint(predicted, 0.85f);
        Vector2 toward = (predicted - start).normalized;
        if (toward.sqrMagnitude < 0.01f)
        {
            toward = Vector2.right;
        }
        Vector2 side = new Vector2(-toward.y, toward.x);
        float travel = Mathf.Clamp(Vector2.Distance(start, predicted), 3.8f, 7.5f);

        causalBranchA[0] = start;
        causalBranchA[1] = owner.ClampAdvancedStatePoint(start + toward * travel * 0.36f, 0.8f);
        causalBranchA[2] = owner.ClampAdvancedStatePoint(Vector2.Lerp(start, predicted, 0.72f), 0.8f);
        causalBranchA[3] = owner.ClampAdvancedStatePoint(predicted - toward * 1.15f, 0.8f);

        float sideSign = Vector2.Dot(player.CurrentVelocity, side) >= 0f ? -1f : 1f;
        causalBranchB[0] = start;
        causalBranchB[1] = owner.ClampAdvancedStatePoint(start + side * sideSign * 2.8f + toward * travel * 0.2f, 0.8f);
        causalBranchB[2] = owner.ClampAdvancedStatePoint(predicted + side * sideSign * 2.4f, 0.8f);
        causalBranchB[3] = owner.ClampAdvancedStatePoint(predicted + side * sideSign * 1.35f - toward * 0.7f, 0.8f);
    }

    private int ChooseCausalBranch()
    {
        Vector2 projectedPlayer = player.GetPosition() + Vector2.ClampMagnitude(player.CurrentVelocity, 6f) * 0.45f;
        float distanceA = Vector2.Distance(projectedPlayer, causalBranchA[causalBranchA.Length - 1]);
        float distanceB = Vector2.Distance(projectedPlayer, causalBranchB[causalBranchB.Length - 1]);
        return distanceA <= distanceB ? 0 : 1;
    }

    private void UpdateCausalVisuals(float revealProgress)
    {
        float pulse = 0.55f + Mathf.Sin(Time.time * 9f) * 0.2f;
        for (int i = 0; i < causalLinesA.Count; i++)
        {
            Color colorA = causalColorA;
            Color colorB = causalColorB;
            float alphaA = pulse;
            float alphaB = pulse;
            if (causalBranchChosen)
            {
                alphaA *= causalChosenBranch == 0 ? Mathf.Lerp(0.65f, 1f, revealProgress) : Mathf.Lerp(0.65f, 0.16f, revealProgress);
                alphaB *= causalChosenBranch == 1 ? Mathf.Lerp(0.65f, 1f, revealProgress) : Mathf.Lerp(0.65f, 0.16f, revealProgress);
            }
            colorA.a = alphaA;
            colorB.a = alphaB;
            SetLine(causalLinesA[i], causalBranchA[i], causalBranchA[i + 1], 0.1f, colorA);
            SetLine(causalLinesB[i], causalBranchB[i], causalBranchB[i + 1], 0.1f, colorB);
        }

        if (causalGhostA != null)
        {
            causalGhostA.transform.position = causalBranchA[causalBranchA.Length - 1];
            causalGhostA.color = new Color(causalColorA.r, causalColorA.g, causalColorA.b,
                causalBranchChosen && causalChosenBranch != 0 ? Mathf.Lerp(0.42f, 0.08f, revealProgress) : 0.48f);
        }
        if (causalGhostB != null)
        {
            causalGhostB.transform.position = causalBranchB[causalBranchB.Length - 1];
            causalGhostB.color = new Color(causalColorB.r, causalColorB.g, causalColorB.b,
                causalBranchChosen && causalChosenBranch != 1 ? Mathf.Lerp(0.42f, 0.08f, revealProgress) : 0.48f);
        }
    }

    private void TickCausalCollapsedBranch(float progress)
    {
        Vector2[] collapsed = causalChosenBranch == 0 ? causalBranchB : causalBranchA;
        List<SpriteRenderer> lines = causalChosenBranch == 0 ? causalLinesB : causalLinesA;
        Color collapseColor = causalChosenBranch == 0 ? causalColorB : causalColorA;
        float head = Mathf.Clamp01(progress);

        for (int i = 0; i < lines.Count; i++)
        {
            float segmentStart = i / (float)lines.Count;
            float segmentEnd = (i + 1f) / lines.Count;
            float active = Mathf.InverseLerp(segmentStart, segmentEnd, head);
            Color color = collapseColor;
            color.a = active > 0f && active < 1f ? 0.95f : Mathf.Lerp(0.3f, 0f, head);
            SetLine(lines[i], collapsed[i], collapsed[i + 1], Mathf.Lerp(0.11f, 0.34f, active), color);
        }

        if (causalCollapseHit || progress <= 0f || progress >= 1f)
        {
            return;
        }

        Vector2 point = EvaluatePolyline(collapsed, progress);
        if (Vector2.Distance(player.GetPosition(), point) <= Mathf.Max(0.25f, causalHazardRadius))
        {
            causalCollapseHit = true;
            player.ApplyMovementSlow(Mathf.Clamp(causalSlowMultiplier, 0.2f, 0.95f), Mathf.Max(0.25f, causalSlowDuration));
            Vector2 push = (player.GetPosition() - point).normalized;
            player.ApplyExternalDisplacement(push * 0.55f);
            SpawnImpactBurst(point, collapseColor, 10, 1.05f);
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

    private Sprite GetPlayerSprite()
    {
        SpriteRenderer renderer = player != null ? player.GetComponent<SpriteRenderer>() : null;
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

    private static Vector2 EvaluatePolyline(Vector2[] points, float progress)
    {
        if (points == null || points.Length == 0)
        {
            return Vector2.zero;
        }
        if (points.Length == 1)
        {
            return points[0];
        }

        float scaled = Mathf.Clamp01(progress) * (points.Length - 1);
        int index = Mathf.Min(points.Length - 2, Mathf.FloorToInt(scaled));
        return Vector2.Lerp(points[index], points[index + 1], scaled - index);
    }

    private static bool IsLevelThreeState(EnemyController.AnomalyState state)
    {
        return state == EnemyController.AnomalyState.AdaptiveCountermeasure ||
               state == EnemyController.AnomalyState.VectorHijack ||
               state == EnemyController.AnomalyState.TopologyFold ||
               state == EnemyController.AnomalyState.CausalFork;
    }

    private static string GetStateTitle(EnemyController.AnomalyState state)
    {
        switch (state)
        {
            case EnemyController.AnomalyState.AdaptiveCountermeasure:
                return "CONTRAMEDIDA ADAPTATIVA";
            case EnemyController.AnomalyState.VectorHijack:
                return "SECUESTRO VECTORIAL";
            case EnemyController.AnomalyState.TopologyFold:
                return "PLIEGUE TOPOLOGICO";
            default:
                return "BIFURCACION CAUSAL";
        }
    }

    private static string GetAdaptiveProfileLabel(AdaptiveProfile profile)
    {
        switch (profile)
        {
            case AdaptiveProfile.Velocity:
                return "TRAYECTORIA";
            case AdaptiveProfile.Parry:
                return "DEFENSA";
            case AdaptiveProfile.Distance:
                return "DISTANCIA";
            default:
                return "QUIETUD";
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

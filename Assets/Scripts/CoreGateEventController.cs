using System.Collections.Generic;
using UnityEngine;

// Evento propio de Core: arma una secuencia de compuertas y relés, comunica su objetivo y restaura el mapa al terminar.
public class CoreGateEventController : MonoBehaviour, IThemedEventStatusProvider
{
    // Data Core convierte sus compuertas en una red interactiva que el jugador puede reprogramar.
    private class GateSegment
    {
        public GameObject root;
        public SpriteRenderer renderer;
        public BoxCollider2D collider;
        public Vector2 size;
        public float activationTime;
        public float pulseSeed;
    }

    private class RelayNode
    {
        public GameObject root;
        public SpriteRenderer core;
        public SpriteRenderer aura;
        public CircleCollider2D trigger;
        public Vector2 position;
        public float pulseSeed;
        public bool activated;
    }

    [Header("Timing")]
    [SerializeField] private float initialDelayMin = 16f;
    [SerializeField] private float initialDelayMax = 22f;
    [SerializeField] private float intervalMin = 17f;
    [SerializeField] private float intervalMax = 24f;
    [SerializeField] private float telegraphSeconds = 2f;
    [SerializeField] private float activeSeconds = 12f;
    [SerializeField] private float completionHoldSeconds = 2.4f;

    [Header("Relay Network")]
    [SerializeField] private int relayCount = 5;
    [SerializeField] private int requiredRelays = 3;
    [SerializeField] private float relayRadius = 0.56f;
    [SerializeField] private float relayTriggerRadius = 0.82f;
    [SerializeField] private float relayEdgeMargin = 2.2f;
    [SerializeField] private float firewallReward = 18f;

    [Header("Reprogrammable Gates")]
    [SerializeField] private float gateThickness = 0.28f;
    [SerializeField] private float gateGapSize = 3.3f;
    [SerializeField] private float gateMargin = 1.2f;
    [SerializeField] private float gateRecompileTelegraph = 0.7f;
    [SerializeField] private float containmentSize = 3.8f;
    [SerializeField] private float containmentLockSeconds = 1.85f;

    [Header("Visuals")]
    [SerializeField] private Color dormantColor = new Color(0.16f, 0.48f, 0.36f, 0.58f);
    [SerializeField] private Color targetColor = new Color(0.46f, 1f, 0.68f, 1f);
    [SerializeField] private Color completedColor = new Color(1f, 0.88f, 0.34f, 1f);
    [SerializeField] private Color gateTelegraphColor = new Color(0.42f, 1f, 0.62f, 0.66f);
    [SerializeField] private Color gateActiveColor = new Color(0.84f, 1f, 0.34f, 0.94f);

    private readonly List<GateSegment> gateSegments = new List<GateSegment>();
    private readonly List<RelayNode> relays = new List<RelayNode>();
    private readonly List<SpriteRenderer> networkLinks = new List<SpriteRenderer>();
    private Transform centerTransform;
    private ProceduralArenaGenerator arena;
    private GameManager gameManager;
    private EnemyController enemy;
    private PlayerController player;
    private SpriteRenderer guidanceLine;
    private float timer;
    private float eventTimer;
    private float completionTimer;
    private int activeRelayIndex = -1;
    private int completedRelayCount;
    private int previousRelayIndex = -1;
    private bool eventActive;
    private bool networkOnline;
    private bool networkCompleted;
    private bool initialGateBuilt;
    private bool mapEventsWereUnlocked;
    private bool operationModifiersApplied;
    private const string EventPressureKey = "ThemeCoreRelayNetwork";

    public string ActiveThemedEventLabel => eventActive ? "RED REPROGRAMABLE" : string.Empty;
    public string ActiveThemedEventHint
    {
        get
        {
            if (!eventActive)
            {
                return string.Empty;
            }
            if (!networkOnline)
            {
                return "La red esta iniciando";
            }
            if (networkCompleted)
            {
                return "ANOMALIA AISLADA";
            }

            return $"Pisa el rele brillante {completedRelayCount + 1}/{Mathf.Max(1, requiredRelays)}";
        }
    }

    public void Configure(Transform center, Transform staticObstaclesRoot, Transform dynamicObstaclesRoot)
    {
        centerTransform = center != null ? center : transform;
        arena = centerTransform.GetComponent<ProceduralArenaGenerator>();
        ResolveReferences();
        ApplyOperationModifiersOnce();
        ClearEventObjects();
        eventActive = false;
        ScheduleFirstEvent();
    }

    public void ActivateRelay(int relayIndex, PlayerController activatingPlayer)
    {
        if (!eventActive || !networkOnline || networkCompleted || relayIndex != activeRelayIndex)
        {
            return;
        }
        if (relayIndex < 0 || relayIndex >= relays.Count || relays[relayIndex].activated)
        {
            return;
        }

        RelayNode relay = relays[relayIndex];
        relay.activated = true;
        if (relay.trigger != null)
        {
            relay.trigger.enabled = false;
        }

        previousRelayIndex = activeRelayIndex;
        completedRelayCount++;
        SpawnRelayBurst(relay.position, completedColor, 1.25f);
        GlitchAudioManager.PlayLabGateLock(relay.position);

        if (completedRelayCount >= Mathf.Max(1, requiredRelays))
        {
            CompleteNetwork(activatingPlayer);
            return;
        }

        SelectNextRelay();
        RebuildTacticalGate();
    }

    private void ApplyOperationModifiersOnce()
    {
        if (operationModifiersApplied)
        {
            return;
        }

        operationModifiersApplied = true;
        if (ContainmentOperationStorage.SelectedOperation.id != ContainmentOperationStorage.AmbientOverdriveId)
        {
            return;
        }

        intervalMin = Mathf.Max(8f, intervalMin * 0.66f);
        intervalMax = Mathf.Max(intervalMin + 2f, intervalMax * 0.72f);
        activeSeconds += 2f;
        requiredRelays = Mathf.Max(requiredRelays + 1, 4);
        gateGapSize = Mathf.Max(2.7f, gateGapSize - 0.25f);
        containmentLockSeconds += 0.35f;
        firewallReward += 4f;
    }

    private void OnDisable()
    {
        ClearEventObjects();
        eventActive = false;
    }

    private void Update()
    {
        ResolveReferences();
        if (centerTransform == null || gameManager == null || !gameManager.IsRunActive || gameManager.IsGameOver)
        {
            return;
        }

        if (!gameManager.AreMapEventsUnlocked)
        {
            mapEventsWereUnlocked = false;
            if (eventActive)
            {
                EndEvent();
            }

            return;
        }

        if (!mapEventsWereUnlocked)
        {
            mapEventsWereUnlocked = true;
            ScheduleFirstEvent();
        }

        if (enemy != null && enemy.IsMapEventSuppressed())
        {
            return;
        }

        if (!eventActive)
        {
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                StartEvent();
            }

            return;
        }

        eventTimer += Time.deltaTime;
        networkOnline = eventTimer >= telegraphSeconds;
        if (networkOnline && !initialGateBuilt)
        {
            initialGateBuilt = true;
            RebuildTacticalGate();
            EnableActiveRelay();
            GlitchAudioManager.PlayLabSecurityScan(centerTransform.position);
        }

        UpdateRelayVisuals();
        UpdateNetworkLinks();
        UpdateGuidanceLine();
        UpdateGateSegments();

        if (networkCompleted)
        {
            completionTimer -= Time.deltaTime;
            if (completionTimer <= 0f)
            {
                EndEvent();
            }

            return;
        }

        if (eventTimer >= telegraphSeconds + activeSeconds)
        {
            EndEvent();
        }
    }

    private void StartEvent()
    {
        if (arena == null || gameManager == null)
        {
            ScheduleNextEvent();
            return;
        }

        float duration = telegraphSeconds + activeSeconds + completionHoldSeconds + 0.5f;
        if (!gameManager.TryReserveEventPressure(EventPressureKey, 0.95f, duration, 3.8f))
        {
            timer = 2f;
            return;
        }

        ClearEventObjects();
        eventActive = true;
        FindAnyObjectByType<GameManager>()?.NotifyThemedMapEventStarted(ActiveThemedEventLabel, ActiveThemedEventHint);
        networkOnline = false;
        networkCompleted = false;
        initialGateBuilt = false;
        eventTimer = 0f;
        completionTimer = 0f;
        completedRelayCount = 0;
        previousRelayIndex = -1;
        CreateRelayNetwork();
        SelectNextRelay();
    }

    private void CreateRelayNetwork()
    {
        int count = Mathf.Max(4, relayCount);
        float radiusX = Mathf.Max(3f, arena.ArenaWidth * 0.5f - relayEdgeMargin);
        float radiusY = Mathf.Max(2.5f, arena.ArenaHeight * 0.5f - relayEdgeMargin);
        float rotation = Random.Range(0f, Mathf.PI * 2f);

        for (int i = 0; i < count; i++)
        {
            float angle = rotation + Mathf.PI * 2f * i / count;
            Vector2 position = new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
            position += Random.insideUnitCircle * 0.35f;
            position = FindSafeRelayPosition(position, angle, radiusX, radiusY);
            CreateRelay(position, i);
        }

        for (int i = 0; i < relays.Count; i++)
        {
            int next = (i + 1) % relays.Count;
            networkLinks.Add(CreateLine(
                $"CoreNetworkLink_{i}",
                relays[i].position,
                relays[next].position,
                0.055f,
                new Color(dormantColor.r, dormantColor.g, dormantColor.b, 0.22f),
                14));
        }

        GameObject guide = new GameObject("CoreRelayGuidance");
        guide.transform.SetParent(centerTransform, false);
        guidanceLine = guide.AddComponent<SpriteRenderer>();
        guidanceLine.sprite = SquareSpriteProvider.Get();
        guidanceLine.drawMode = SpriteDrawMode.Sliced;
        guidanceLine.sortingOrder = 17;
        guidanceLine.color = new Color(targetColor.r, targetColor.g, targetColor.b, 0f);
    }

    private Vector2 FindSafeRelayPosition(Vector2 preferred, float baseAngle, float radiusX, float radiusY)
    {
        const int probes = 10;
        for (int i = 0; i < probes; i++)
        {
            float angularOffset = i == 0 ? 0f : ((i % 2 == 0 ? 1f : -1f) * Mathf.Ceil(i * 0.5f) * 0.16f);
            float inward = 1f - Mathf.Min(0.28f, i * 0.035f);
            float angle = baseAngle + angularOffset;
            Vector2 candidate = i == 0
                ? preferred
                : new Vector2(Mathf.Cos(angle) * radiusX * inward, Mathf.Sin(angle) * radiusY * inward);
            if (IsRelayPositionClear(candidate))
            {
                return candidate;
            }
        }

        return preferred * 0.72f;
    }

    private bool IsRelayPositionClear(Vector2 position)
    {
        Collider2D[] overlaps = Physics2D.OverlapCircleAll(position, Mathf.Max(0.7f, relayTriggerRadius + 0.25f));
        for (int i = 0; i < overlaps.Length; i++)
        {
            Collider2D overlap = overlaps[i];
            if (overlap == null || overlap.isTrigger)
            {
                continue;
            }
            if (overlap.GetComponent<PlayerController>() != null || overlap.GetComponent<EnemyController>() != null)
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private void CreateRelay(Vector2 position, int index)
    {
        GameObject root = new GameObject($"CoreRelay_{index}");
        root.transform.SetParent(centerTransform, false);
        root.transform.position = new Vector3(position.x, position.y, 0f);

        SpriteRenderer aura = root.AddComponent<SpriteRenderer>();
        aura.sprite = CircleSpriteProvider.Get();
        aura.drawMode = SpriteDrawMode.Sliced;
        aura.size = Vector2.one * relayRadius * 3.1f;
        aura.sortingOrder = 16;
        aura.color = new Color(dormantColor.r, dormantColor.g, dormantColor.b, 0.12f);

        GameObject coreObject = new GameObject("RelayCore");
        coreObject.transform.SetParent(root.transform, false);
        SpriteRenderer core = coreObject.AddComponent<SpriteRenderer>();
        core.sprite = SquareSpriteProvider.Get();
        core.drawMode = SpriteDrawMode.Sliced;
        core.size = Vector2.one * relayRadius * 1.35f;
        core.sortingOrder = 18;
        core.color = dormantColor;
        coreObject.transform.rotation = Quaternion.Euler(0f, 0f, 45f);

        CircleCollider2D trigger = root.AddComponent<CircleCollider2D>();
        trigger.radius = Mathf.Max(relayRadius, relayTriggerRadius);
        trigger.isTrigger = true;
        trigger.enabled = false;

        CoreRelayNodeTrigger relayTrigger = root.AddComponent<CoreRelayNodeTrigger>();
        relayTrigger.Configure(this, index);

        relays.Add(new RelayNode
        {
            root = root,
            core = core,
            aura = aura,
            trigger = trigger,
            position = position,
            pulseSeed = Random.Range(0f, 10f)
        });
    }

    private void SelectNextRelay()
    {
        Vector2 enemyPosition = enemy != null ? enemy.GetCurrentPosition() : Vector2.zero;
        Vector2 playerPosition = player != null ? player.GetPosition() : Vector2.zero;
        int bestIndex = -1;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < relays.Count; i++)
        {
            RelayNode relay = relays[i];
            if (relay.activated || i == previousRelayIndex)
            {
                continue;
            }

            float enemyDistance = Vector2.Distance(relay.position, enemyPosition);
            float playerDistance = Vector2.Distance(relay.position, playerPosition);
            float previousDistance = previousRelayIndex >= 0
                ? Vector2.Distance(relay.position, relays[previousRelayIndex].position)
                : 0f;
            float score = enemyDistance * 1.2f - playerDistance * 0.32f + previousDistance * 0.18f + Random.Range(-0.25f, 0.25f);
            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        activeRelayIndex = bestIndex;
        EnableActiveRelay();
    }

    private void EnableActiveRelay()
    {
        for (int i = 0; i < relays.Count; i++)
        {
            if (relays[i].trigger != null)
            {
                relays[i].trigger.enabled = networkOnline && !networkCompleted && i == activeRelayIndex;
            }
        }
    }

    private void RebuildTacticalGate()
    {
        ClearGateSegments();
        if (player == null || enemy == null)
        {
            return;
        }

        Vector2 playerPosition = player.GetPosition();
        Vector2 enemyPosition = enemy.GetCurrentPosition();
        Vector2 delta = playerPosition - enemyPosition;
        bool vertical = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y);
        float axisPosition = vertical
            ? Mathf.Lerp(enemyPosition.x, playerPosition.x, 0.48f)
            : Mathf.Lerp(enemyPosition.y, playerPosition.y, 0.48f);
        float gapCenter = vertical ? playerPosition.y : playerPosition.x;
        BuildGate(axisPosition, gapCenter, vertical, "Primary");

        // Una segunda compuerta corta la ruta lateral mas probable sin cerrar la salida del jugador.
        if (completedRelayCount > 0)
        {
            bool secondaryVertical = !vertical;
            float secondaryAxis = secondaryVertical
                ? enemyPosition.x + Mathf.Sign(delta.x == 0f ? 1f : delta.x) * 1.5f
                : enemyPosition.y + Mathf.Sign(delta.y == 0f ? 1f : delta.y) * 1.5f;
            float secondaryGap = secondaryVertical ? playerPosition.y : playerPosition.x;
            BuildGate(secondaryAxis, secondaryGap, secondaryVertical, "Secondary");
        }
    }

    private void BuildGate(float axisPosition, float gapCenter, bool vertical, string label)
    {
        float halfLong = (vertical ? arena.ArenaHeight : arena.ArenaWidth) * 0.5f - gateMargin;
        float gapHalf = Mathf.Max(0.8f, gateGapSize * 0.5f);
        gapCenter = Mathf.Clamp(gapCenter, -halfLong + gapHalf, halfLong - gapHalf);
        float lowerMax = gapCenter - gapHalf;
        float upperMin = gapCenter + gapHalf;

        CreateGateSegment(axisPosition, (-halfLong + lowerMax) * 0.5f, lowerMax + halfLong, vertical, $"{label}_A");
        CreateGateSegment(axisPosition, (upperMin + halfLong) * 0.5f, halfLong - upperMin, vertical, $"{label}_B");
    }

    private void CreateGateSegment(float axisPosition, float longCenter, float longLength, bool vertical, string label)
    {
        if (longLength <= 0.25f)
        {
            return;
        }

        GameObject root = new GameObject($"CoreProgrammedGate_{label}");
        root.transform.SetParent(centerTransform, false);
        root.transform.position = vertical
            ? new Vector3(axisPosition, longCenter, 0f)
            : new Vector3(longCenter, axisPosition, 0f);

        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = SquareSpriteProvider.Get();
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.sortingOrder = 18;
        Vector2 size = vertical
            ? new Vector2(gateThickness, longLength)
            : new Vector2(longLength, gateThickness);
        renderer.size = size;
        renderer.color = gateTelegraphColor;

        BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
        collider.size = size;
        collider.enabled = false;

        gateSegments.Add(new GateSegment
        {
            root = root,
            renderer = renderer,
            collider = collider,
            size = size,
            activationTime = eventTimer + gateRecompileTelegraph,
            pulseSeed = Random.Range(0f, 10f)
        });
    }

    private void CompleteNetwork(PlayerController activatingPlayer)
    {
        networkCompleted = true;
        activeRelayIndex = -1;
        completionTimer = Mathf.Max(0.8f, completionHoldSeconds);
        EnableActiveRelay();
        ClearGateSegments();

        Vector2 enemyPosition = enemy != null ? enemy.GetCurrentPosition() : Vector2.zero;
        BuildContainmentBox(enemyPosition);
        if (enemy != null)
        {
            enemy.ApplyContainmentLock(enemyPosition, Mathf.Max(0.25f, containmentLockSeconds));
        }

        PlayerController rewardedPlayer = activatingPlayer != null ? activatingPlayer : player;
        rewardedPlayer?.AddFirewallCharge(Mathf.Max(1f, firewallReward));
        SpawnRelayBurst(enemyPosition, completedColor, containmentSize * 0.75f);
        GlitchAudioManager.PlayParrySuccess(enemyPosition);
    }

    private void BuildContainmentBox(Vector2 center)
    {
        float size = Mathf.Max(2.2f, containmentSize);
        float half = size * 0.5f;
        CreateContainmentSegment(center + Vector2.up * half, new Vector2(size, gateThickness), "Top");
        CreateContainmentSegment(center + Vector2.down * half, new Vector2(size, gateThickness), "Bottom");
        CreateContainmentSegment(center + Vector2.left * half, new Vector2(gateThickness, size), "Left");
        CreateContainmentSegment(center + Vector2.right * half, new Vector2(gateThickness, size), "Right");
    }

    private void CreateContainmentSegment(Vector2 position, Vector2 size, string label)
    {
        GameObject root = new GameObject($"CoreContainment_{label}");
        root.transform.SetParent(centerTransform, false);
        root.transform.position = position;

        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = SquareSpriteProvider.Get();
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.sortingOrder = 19;
        renderer.size = size;
        renderer.color = completedColor;

        BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
        collider.size = size;
        collider.isTrigger = true;
        collider.enabled = true;

        gateSegments.Add(new GateSegment
        {
            root = root,
            renderer = renderer,
            collider = collider,
            size = size,
            activationTime = eventTimer,
            pulseSeed = Random.Range(0f, 10f)
        });
    }

    private void UpdateRelayVisuals()
    {
        float intro = Mathf.Clamp01(eventTimer / Mathf.Max(0.1f, telegraphSeconds));
        for (int i = 0; i < relays.Count; i++)
        {
            RelayNode relay = relays[i];
            if (relay.root == null)
            {
                continue;
            }

            bool target = networkOnline && !networkCompleted && i == activeRelayIndex;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (target ? 8.5f : 3.5f) + relay.pulseSeed);
            Color color = relay.activated ? completedColor : target ? targetColor : dormantColor;
            float coreAlpha = relay.activated ? 0.92f : target ? Mathf.Lerp(0.72f, 1f, pulse) : 0.42f;
            relay.core.color = new Color(color.r, color.g, color.b, coreAlpha * intro);
            relay.core.size = Vector2.one * relayRadius * (target ? Mathf.Lerp(1.35f, 1.65f, pulse) : 1.25f);

            float auraAlpha = relay.activated ? 0.34f : target ? Mathf.Lerp(0.22f, 0.52f, pulse) : 0.08f;
            relay.aura.color = new Color(color.r, color.g, color.b, auraAlpha * intro);
            relay.aura.size = Vector2.one * relayRadius * (target ? Mathf.Lerp(2.8f, 3.7f, pulse) : 2.5f);
            relay.root.transform.rotation = Quaternion.Euler(0f, 0f, Time.time * (target ? 85f : 24f) + i * 22f);
        }
    }

    private void UpdateNetworkLinks()
    {
        for (int i = 0; i < networkLinks.Count; i++)
        {
            SpriteRenderer link = networkLinks[i];
            if (link == null)
            {
                continue;
            }

            bool energized = networkOnline && (i == activeRelayIndex || (i + 1) % relays.Count == activeRelayIndex);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 7f + i);
            Color color = energized ? targetColor : dormantColor;
            link.color = new Color(color.r, color.g, color.b, energized ? Mathf.Lerp(0.22f, 0.58f, pulse) : 0.12f);
        }
    }

    private void UpdateGuidanceLine()
    {
        if (guidanceLine == null || player == null || activeRelayIndex < 0 || activeRelayIndex >= relays.Count)
        {
            if (guidanceLine != null)
            {
                Color hidden = guidanceLine.color;
                hidden.a = 0f;
                guidanceLine.color = hidden;
            }

            return;
        }

        Vector2 start = player.GetPosition();
        Vector2 end = relays[activeRelayIndex].position;
        Vector2 delta = end - start;
        float distance = delta.magnitude;
        guidanceLine.transform.position = (start + end) * 0.5f;
        guidanceLine.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        guidanceLine.size = new Vector2(Mathf.Max(0.05f, distance), 0.07f);
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 9f);
        guidanceLine.color = new Color(targetColor.r, targetColor.g, targetColor.b, Mathf.Lerp(0.14f, 0.34f, pulse));
    }

    private void UpdateGateSegments()
    {
        for (int i = 0; i < gateSegments.Count; i++)
        {
            GateSegment segment = gateSegments[i];
            if (segment == null || segment.renderer == null)
            {
                continue;
            }

            bool active = eventTimer >= segment.activationTime;
            float telegraphProgress = Mathf.Clamp01(1f - (segment.activationTime - eventTimer) / Mathf.Max(0.1f, gateRecompileTelegraph));
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (active ? 11f : 6f) + segment.pulseSeed);
            Color color = networkCompleted ? completedColor : active ? gateActiveColor : gateTelegraphColor;
            color = Color.Lerp(color, Color.white, pulse * (active ? 0.22f : 0.10f));
            color.a = active ? Mathf.Lerp(0.72f, 0.98f, pulse) : Mathf.Lerp(0.14f, 0.48f, pulse) * telegraphProgress;
            segment.renderer.color = color;
            segment.renderer.size = segment.size * (active ? 1f : Mathf.Lerp(0.25f, 1f, telegraphProgress));
            if (segment.collider != null)
            {
                segment.collider.enabled = active;
            }
        }
    }

    private SpriteRenderer CreateLine(string name, Vector2 start, Vector2 end, float thickness, Color color, int sortingOrder)
    {
        Vector2 delta = end - start;
        GameObject root = new GameObject(name);
        root.transform.SetParent(centerTransform, false);
        root.transform.position = (start + end) * 0.5f;
        root.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);

        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = SquareSpriteProvider.Get();
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.size = new Vector2(delta.magnitude, thickness);
        renderer.sortingOrder = sortingOrder;
        renderer.color = color;
        return renderer;
    }

    private void SpawnRelayBurst(Vector2 position, Color color, float radius)
    {
        GameObject burst = new GameObject("CoreRelayBurst");
        burst.transform.position = position;
        SpriteRenderer renderer = burst.AddComponent<SpriteRenderer>();
        renderer.sprite = CircleSpriteProvider.Get();
        renderer.sortingOrder = 20;
        renderer.color = color;
        burst.transform.localScale = Vector3.one * 0.2f;
        burst.AddComponent<PlayerParryBurstFx>().Configure(renderer, Mathf.Max(0.5f, radius), 0.35f, color);
        Destroy(burst, 0.48f);
    }

    private void EndEvent()
    {
        ClearEventObjects();
        eventActive = false;
        networkOnline = false;
        networkCompleted = false;
        ScheduleNextEvent();
        GlitchAudioManager.PlayLabGateRelease(centerTransform != null ? centerTransform.position : Vector3.zero);
    }

    private void ClearEventObjects()
    {
        ClearGateSegments();
        for (int i = 0; i < relays.Count; i++)
        {
            if (relays[i]?.root != null)
            {
                Destroy(relays[i].root);
            }
        }
        for (int i = 0; i < networkLinks.Count; i++)
        {
            if (networkLinks[i] != null)
            {
                Destroy(networkLinks[i].gameObject);
            }
        }
        if (guidanceLine != null)
        {
            Destroy(guidanceLine.gameObject);
        }

        relays.Clear();
        networkLinks.Clear();
        guidanceLine = null;
        activeRelayIndex = -1;
        previousRelayIndex = -1;
    }

    private void ClearGateSegments()
    {
        for (int i = 0; i < gateSegments.Count; i++)
        {
            if (gateSegments[i]?.root != null)
            {
                Destroy(gateSegments[i].root);
            }
        }

        gateSegments.Clear();
    }

    private void ScheduleFirstEvent()
    {
        timer = Random.Range(
            Mathf.Min(initialDelayMin, initialDelayMax),
            Mathf.Max(initialDelayMin, initialDelayMax));
    }

    private void ScheduleNextEvent()
    {
        timer = Random.Range(Mathf.Min(intervalMin, intervalMax), Mathf.Max(intervalMin, intervalMax));
    }

    private void ResolveReferences()
    {
        if (arena == null && centerTransform != null)
        {
            arena = centerTransform.GetComponent<ProceduralArenaGenerator>();
        }
        if (gameManager == null)
        {
            gameManager = FindAnyObjectByType<GameManager>();
        }
        if (enemy == null)
        {
            enemy = FindAnyObjectByType<EnemyController>();
        }
        if (player == null)
        {
            player = FindAnyObjectByType<PlayerController>();
        }
    }
}

public class CoreRelayNodeTrigger : MonoBehaviour
{
    private CoreGateEventController owner;
    private int relayIndex;

    public void Configure(CoreGateEventController controller, int index)
    {
        owner = controller;
        relayIndex = index;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (owner == null || other == null)
        {
            return;
        }

        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null)
        {
            owner.ActivateRelay(relayIndex, player);
        }
    }
}

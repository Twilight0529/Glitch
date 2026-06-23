using System.Collections.Generic;
using UnityEngine;

public class CoreGateEventController : MonoBehaviour, IThemedEventStatusProvider
{
    // Evento de Core: compuertas binarias cierran carriles con huecos calculados para forzar lectura espacial.
    private class GateSegment
    {
        public GameObject root;
        public SpriteRenderer renderer;
        public BoxCollider2D collider;
        public Vector2 size;
        public Color baseColor;
        public float pulseSeed;
    }

    [Header("Timing")]
    [SerializeField] private float intervalMin = 10f;
    [SerializeField] private float intervalMax = 16f;
    [SerializeField] private float telegraphSeconds = 1.55f;
    [SerializeField] private float activeSeconds = 4.8f;

    [Header("Binary Gates")]
    [SerializeField] private int gateCountMin = 2;
    [SerializeField] private int gateCountMax = 3;
    [SerializeField] private float gateThickness = 0.24f;
    [SerializeField] private float gapSize = 3.1f;
    [SerializeField] private float gateMargin = 1.25f;
    [SerializeField] private Color telegraphColor = new Color(0.42f, 1f, 0.62f, 0.78f);
    [SerializeField] private Color activeColor = new Color(0.84f, 1f, 0.34f, 0.92f);

    private readonly List<GateSegment> segments = new List<GateSegment>();
    private Transform centerTransform;
    private ProceduralArenaGenerator arena;
    private GameManager gameManager;
    private EnemyController enemy;
    private float timer;
    private float eventTimer;
    private bool eventActive;
    private bool mapEventsWereUnlocked;
    private bool operationModifiersApplied;
    private const string EventPressureKey = "ThemeCoreBinaryGates";

    public string ActiveThemedEventLabel => eventActive ? "COMPUERTAS BINARIAS" : string.Empty;
    public string ActiveThemedEventHint => eventActive ? "Busca el hueco antes del cierre" : string.Empty;

    public void Configure(Transform center, Transform staticObstaclesRoot, Transform dynamicObstaclesRoot)
    {
        centerTransform = center != null ? center : transform;
        arena = centerTransform.GetComponent<ProceduralArenaGenerator>();
        ResolveReferences();
        ApplyOperationModifiersOnce();
        ClearSegments();
        eventActive = false;
        ScheduleNextEvent();
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

        intervalMin = Mathf.Max(4.5f, intervalMin * 0.58f);
        intervalMax = Mathf.Max(intervalMin + 1f, intervalMax * 0.66f);
        activeSeconds += 1.2f;
        gateCountMax = Mathf.Max(gateCountMax + 1, 4);
        gapSize = Mathf.Max(2.4f, gapSize - 0.28f);
    }

    private void OnDisable()
    {
        ClearSegments();
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
                ClearSegments();
                eventActive = false;
            }

            return;
        }

        if (!mapEventsWereUnlocked)
        {
            mapEventsWereUnlocked = true;
            timer = Mathf.Min(timer, 0.85f);
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
        UpdateSegments();
        if (eventTimer >= telegraphSeconds + activeSeconds)
        {
            ClearSegments();
            eventActive = false;
            ScheduleNextEvent();
        }
    }

    private void StartEvent()
    {
        if (arena == null || gameManager == null)
        {
            ScheduleNextEvent();
            return;
        }

        float duration = telegraphSeconds + activeSeconds + 0.35f;
        if (!gameManager.TryReserveEventPressure(EventPressureKey, 0.92f, duration, 3.6f))
        {
            timer = 1.6f;
            return;
        }

        ClearSegments();
        eventActive = true;
        eventTimer = 0f;
        int gateCount = Random.Range(Mathf.Min(gateCountMin, gateCountMax), Mathf.Max(gateCountMin, gateCountMax) + 1);
        bool vertical = Random.value < 0.5f;
        for (int i = 0; i < gateCount; i++)
        {
            float laneT = (i + 1f) / (gateCount + 1f);
            float axisPos = vertical
                ? Mathf.Lerp(-arena.ArenaWidth * 0.36f, arena.ArenaWidth * 0.36f, laneT)
                : Mathf.Lerp(-arena.ArenaHeight * 0.34f, arena.ArenaHeight * 0.34f, laneT);
            float gapCenter = vertical
                ? Random.Range(-arena.ArenaHeight * 0.28f, arena.ArenaHeight * 0.28f)
                : Random.Range(-arena.ArenaWidth * 0.30f, arena.ArenaWidth * 0.30f);

            BuildGate(axisPos, gapCenter, vertical, i);
        }
    }

    private void BuildGate(float axisPosition, float gapCenter, bool vertical, int index)
    {
        float halfLong = (vertical ? arena.ArenaHeight : arena.ArenaWidth) * 0.5f - gateMargin;
        float gapHalf = Mathf.Max(0.7f, gapSize * 0.5f);
        float lowerMin = -halfLong;
        float lowerMax = Mathf.Clamp(gapCenter - gapHalf, -halfLong, halfLong);
        float upperMin = Mathf.Clamp(gapCenter + gapHalf, -halfLong, halfLong);
        float upperMax = halfLong;

        CreateSegment(axisPosition, (lowerMin + lowerMax) * 0.5f, Mathf.Max(0.15f, lowerMax - lowerMin), vertical, index * 2);
        CreateSegment(axisPosition, (upperMin + upperMax) * 0.5f, Mathf.Max(0.15f, upperMax - upperMin), vertical, index * 2 + 1);
    }

    private void CreateSegment(float axisPosition, float longCenter, float longLength, bool vertical, int index)
    {
        if (longLength <= 0.2f)
        {
            return;
        }

        GameObject go = new GameObject($"CoreBinaryGate_{index}");
        go.transform.SetParent(centerTransform, false);
        go.transform.position = vertical
            ? new Vector3(axisPosition, longCenter, 0f)
            : new Vector3(longCenter, axisPosition, 0f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SquareSpriteProvider.Get();
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.sortingOrder = 18;
        Vector2 size = vertical ? new Vector2(gateThickness, longLength) : new Vector2(longLength, gateThickness);
        sr.size = size;
        sr.color = telegraphColor;

        BoxCollider2D col = go.AddComponent<BoxCollider2D>();
        col.size = size;
        col.enabled = false;

        segments.Add(new GateSegment
        {
            root = go,
            renderer = sr,
            collider = col,
            size = size,
            baseColor = telegraphColor,
            pulseSeed = Random.Range(0f, 8f)
        });
    }

    private void UpdateSegments()
    {
        bool active = eventTimer >= telegraphSeconds;
        float phase = active ? Mathf.Clamp01((eventTimer - telegraphSeconds) / activeSeconds) : Mathf.Clamp01(eventTimer / telegraphSeconds);
        for (int i = 0; i < segments.Count; i++)
        {
            GateSegment segment = segments[i];
            if (segment == null || segment.renderer == null)
            {
                continue;
            }

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (active ? 10f : 6f) + segment.pulseSeed);
            Color c = active ? activeColor : telegraphColor;
            c = Color.Lerp(c, Color.white, active ? pulse * 0.26f : pulse * 0.14f);
            c.a = active ? Mathf.Lerp(0.62f, 0.94f, pulse) : Mathf.Lerp(0.18f, 0.44f, pulse) * Mathf.SmoothStep(0.18f, 1f, phase);
            segment.renderer.color = c;
            segment.renderer.size = segment.size * (active ? 1f : Mathf.Lerp(0.38f, 1f, phase));
            if (segment.collider != null)
            {
                segment.collider.enabled = active;
            }
        }
    }

    private void ClearSegments()
    {
        for (int i = 0; i < segments.Count; i++)
        {
            if (segments[i]?.root != null)
            {
                Destroy(segments[i].root);
            }
        }

        segments.Clear();
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
    }
}

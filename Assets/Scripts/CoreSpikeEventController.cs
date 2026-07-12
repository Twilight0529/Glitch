using System.Collections.Generic;
using UnityEngine;

// Evento exclusivo de Data Core: vuelve peligrosos algunos obstaculos existentes sin cerrar rutas nuevas.
public class CoreSpikeEventController : MonoBehaviour, IThemedEventStatusProvider
{
    [Header("Timing")]
    [SerializeField] private Vector2 initialDelayRange = new Vector2(13f, 19f);
    [SerializeField] private Vector2 intervalRange = new Vector2(20f, 29f);
    [SerializeField] private float telegraphSeconds = 1.5f;
    [SerializeField] private float activeSeconds = 7f;

    [Header("Selection")]
    [SerializeField, Range(1, 5)] private int minSpikedObstacles = 2;
    [SerializeField, Range(1, 6)] private int maxSpikedObstacles = 3;
    [SerializeField] private float actorSafetyDistance = 2.2f;

    [Header("Visuals")]
    [SerializeField] private Color warningColor = new Color(1f, 0.78f, 0.24f, 0.82f);
    [SerializeField] private Color activeColor = new Color(1f, 0.28f, 0.40f, 1f);
    [SerializeField] private float hazardPadding = 0.22f;

    private const string EventPressureKey = "ThemeCoreSpikes";
    private readonly List<CoreSpikeHazard> hazards = new List<CoreSpikeHazard>();
    private Transform centerTransform;
    private Transform obstaclesRoot;
    private GameManager gameManager;
    private EnemyController enemy;
    private PlayerController player;
    private float timer;
    private float eventTimer;
    private bool eventActive;
    private bool hazardsArmed;

    public string ActiveThemedEventLabel => eventActive ? "PROTOCOLO DE ESPINAS" : string.Empty;
    public string ActiveThemedEventHint => !eventActive
        ? string.Empty
        : hazardsArmed
            ? "Evita los objetos rojos o atraviesalos con Phase Dash"
            : "Los objetos amarillos estan por volverse peligrosos";

    public void Configure(Transform center, Transform staticObstaclesRoot, Transform dynamicObstaclesRoot)
    {
        warningColor = GlitchUiPalette.WithAlpha(GlitchUiPalette.Alert, 0.82f);
        activeColor = GlitchUiPalette.Danger;
        centerTransform = center != null ? center : transform;
        obstaclesRoot = staticObstaclesRoot;
        ResolveReferences();
        EndEvent(false);
        Schedule(initialDelayRange);
    }

    private void OnDisable()
    {
        EndEvent(false);
    }

    private void Update()
    {
        ResolveReferences();
        if (gameManager == null || !gameManager.IsRunActive || gameManager.IsGameOver)
        {
            return;
        }

        if (!gameManager.AreMapEventsUnlocked)
        {
            if (eventActive)
            {
                EndEvent(false);
            }
            return;
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
        if (!hazardsArmed && eventTimer >= Mathf.Max(0.2f, telegraphSeconds))
        {
            hazardsArmed = true;
            for (int i = 0; i < hazards.Count; i++)
            {
                hazards[i]?.SetArmed(true);
            }
        }

        if (eventTimer >= Mathf.Max(0.2f, telegraphSeconds) + Mathf.Max(1f, activeSeconds))
        {
            EndEvent(true);
        }
    }

    private void StartEvent()
    {
        if (obstaclesRoot == null || gameManager == null)
        {
            Schedule(intervalRange);
            return;
        }

        float duration = Mathf.Max(0.2f, telegraphSeconds) + Mathf.Max(1f, activeSeconds) + 0.4f;
        if (!gameManager.TryReserveEventPressure(EventPressureKey, 0.72f, duration, 3f))
        {
            timer = 2f;
            return;
        }

        List<Collider2D> candidates = CollectCandidates();
        if (candidates.Count == 0)
        {
            gameManager.ReleaseEventPressure(EventPressureKey, 1f);
            Schedule(intervalRange);
            return;
        }

        Shuffle(candidates);
        int desired = Random.Range(
            Mathf.Max(1, Mathf.Min(minSpikedObstacles, maxSpikedObstacles)),
            Mathf.Max(1, Mathf.Max(minSpikedObstacles, maxSpikedObstacles)) + 1);
        int count = Mathf.Min(desired, candidates.Count);
        for (int i = 0; i < count; i++)
        {
            CoreSpikeHazard hazard = CreateHazard(candidates[i]);
            if (hazard != null)
            {
                hazards.Add(hazard);
            }
        }

        if (hazards.Count == 0)
        {
            gameManager.ReleaseEventPressure(EventPressureKey, 1f);
            Schedule(intervalRange);
            return;
        }

        eventActive = true;
        hazardsArmed = false;
        eventTimer = 0f;
        gameManager.NotifyMapEventStarted(
            "core_spike_protocol",
            "PROTOCOLO DE ESPINAS",
            "Los obstáculos amarillos están por cubrirse de espinas; al cambiar a rojo causarán daño. Rodéalos o atraviésalos mientras Phase Dash esté activo.");
    }

    private List<Collider2D> CollectCandidates()
    {
        List<Collider2D> result = new List<Collider2D>();
        Collider2D[] colliders = obstaclesRoot.GetComponentsInChildren<Collider2D>(true);
        Vector2 playerPosition = player != null ? player.GetPosition() : new Vector2(999f, 999f);
        Vector2 enemyPosition = enemy != null ? enemy.GetCurrentPosition() : new Vector2(999f, 999f);
        float safety = Mathf.Max(0.5f, actorSafetyDistance);

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D candidate = colliders[i];
            if (candidate == null || candidate.isTrigger || !candidate.enabled ||
                !candidate.gameObject.name.StartsWith("Obstacle_Core"))
            {
                continue;
            }

            Vector2 position = candidate.bounds.center;
            if (Vector2.Distance(position, playerPosition) < safety || Vector2.Distance(position, enemyPosition) < safety)
            {
                continue;
            }
            result.Add(candidate);
        }

        return result;
    }

    private CoreSpikeHazard CreateHazard(Collider2D source)
    {
        if (source == null)
        {
            return null;
        }

        GameObject root = new GameObject($"CoreSpikes_{source.gameObject.name}");
        root.transform.SetParent(source.transform, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;

        BoxCollider2D trigger = root.AddComponent<BoxCollider2D>();
        Vector2 localSize;
        Vector2 localOffset = Vector2.zero;
        if (source is BoxCollider2D box)
        {
            localSize = box.size + Vector2.one * Mathf.Max(0.05f, hazardPadding * 2f);
            localOffset = box.offset;
        }
        else
        {
            Vector3 scale = source.transform.lossyScale;
            localSize = new Vector2(
                source.bounds.size.x / Mathf.Max(0.01f, Mathf.Abs(scale.x)),
                source.bounds.size.y / Mathf.Max(0.01f, Mathf.Abs(scale.y))) + Vector2.one * hazardPadding * 2f;
        }
        trigger.size = localSize;
        trigger.offset = localOffset;
        trigger.isTrigger = true;
        trigger.enabled = false;

        CoreSpikeHazard hazard = root.AddComponent<CoreSpikeHazard>();
        hazard.Configure(gameManager, trigger, localSize, warningColor, activeColor);
        return hazard;
    }

    private void EndEvent(bool scheduleNext)
    {
        for (int i = 0; i < hazards.Count; i++)
        {
            if (hazards[i] != null)
            {
                Destroy(hazards[i].gameObject);
            }
        }
        hazards.Clear();

        if (eventActive && gameManager != null)
        {
            gameManager.ReleaseEventPressure(EventPressureKey, 2.5f);
        }
        eventActive = false;
        hazardsArmed = false;
        eventTimer = 0f;
        if (scheduleNext)
        {
            Schedule(intervalRange);
        }
    }

    private void ResolveReferences()
    {
        if (gameManager == null) gameManager = FindAnyObjectByType<GameManager>();
        if (enemy == null) enemy = FindAnyObjectByType<EnemyController>();
        if (player == null) player = FindAnyObjectByType<PlayerController>();
    }

    private void Schedule(Vector2 range)
    {
        timer = Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int swap = Random.Range(0, i + 1);
            (list[i], list[swap]) = (list[swap], list[i]);
        }
    }
}

public class CoreSpikeHazard : MonoBehaviour
{
    private readonly List<SpriteRenderer> spikes = new List<SpriteRenderer>();
    private GameManager gameManager;
    private BoxCollider2D trigger;
    private Color warningColor;
    private Color activeColor;
    private bool armed;
    private bool defeatRequested;

    public void Configure(GameManager manager, BoxCollider2D hazardTrigger, Vector2 size, Color warning, Color active)
    {
        gameManager = manager;
        trigger = hazardTrigger;
        warningColor = warning;
        activeColor = active;
        CreateSpikeVisuals(size);
    }

    public void SetArmed(bool value)
    {
        armed = value;
        if (trigger != null)
        {
            trigger.enabled = value;
        }
    }

    private void Update()
    {
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (armed ? 12f : 7f));
        Color color = Color.Lerp(armed ? activeColor : warningColor, Color.white, pulse * (armed ? 0.18f : 0.35f));
        color.a = armed ? Mathf.Lerp(0.82f, 1f, pulse) : Mathf.Lerp(0.35f, 0.76f, pulse);
        for (int i = 0; i < spikes.Count; i++)
        {
            if (spikes[i] != null)
            {
                spikes[i].color = color;
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!armed || defeatRequested || other == null)
        {
            return;
        }

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null || player.IsIntangibleDashActive || player.TryAbsorbHit())
        {
            return;
        }

        defeatRequested = true;
        if (gameManager == null)
        {
            gameManager = FindAnyObjectByType<GameManager>();
        }
        gameManager?.RequestPlayerDefeat(player);
    }

    private void CreateSpikeVisuals(Vector2 size)
    {
        int horizontalCount = Mathf.Clamp(Mathf.CeilToInt(size.x / 0.52f), 2, 8);
        int verticalCount = Mathf.Clamp(Mathf.CeilToInt(size.y / 0.52f), 2, 7);
        CreateEdge(horizontalCount, size, Vector2.up, 0f);
        CreateEdge(horizontalCount, size, Vector2.down, 180f);
        CreateEdge(verticalCount, size, Vector2.left, 90f);
        CreateEdge(verticalCount, size, Vector2.right, -90f);
    }

    private void CreateEdge(int count, Vector2 size, Vector2 normal, float rotation)
    {
        for (int i = 0; i < count; i++)
        {
            float t = (i + 0.5f) / count - 0.5f;
            Vector2 position = Mathf.Abs(normal.y) > 0.5f
                ? new Vector2(t * size.x, normal.y * size.y * 0.5f)
                : new Vector2(normal.x * size.x * 0.5f, t * size.y);
            GameObject spike = new GameObject("Spike");
            spike.transform.SetParent(transform, false);
            spike.transform.localPosition = position;
            spike.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            float alternatingHeight = i % 2 == 0 ? 0.38f : 0.31f;
            spike.transform.localScale = new Vector3(0.27f, alternatingHeight, 1f);
            SpriteRenderer renderer = spike.AddComponent<SpriteRenderer>();
            renderer.sprite = TriangleSpriteProvider.Get();
            renderer.sortingOrder = 15;
            renderer.color = warningColor;
            spikes.Add(renderer);
        }
    }
}

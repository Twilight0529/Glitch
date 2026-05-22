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

    [Header("Spawn Rules")]
    [SerializeField] private float edgePadding = 1.1f;
    [SerializeField] private float actorClearance = 2.2f;
    [SerializeField] private float pickupProbeRadius = 0.55f;
    [SerializeField] private float hazardProbeRadius = 0.9f;
    [SerializeField] private int spawnAttempts = 24;

    [Header("Event Banner")]
    [SerializeField] private float bannerDuration = 2f;

    private ProceduralArenaGenerator arena;
    private PlayerController player;
    private EnemyController enemy;
    private GameManager gameManager;

    private float powerupTimer;
    private float scorePickupTimer;
    private float pulseEventTimer;
    private ArenaPowerupPickup activePickup;
    private readonly List<ArenaScorePickup> activeScorePickups = new List<ArenaScorePickup>();

    private string activeEventLabel = string.Empty;
    private float eventLabelTimer;
    private string activeWarningLabel = string.Empty;
    private float warningTimer;
    private float warningDuration;
    private bool pulseWasUnlocked;

    private Transform runtimeRoot;

    public string ActiveEventLabel => eventLabelTimer > 0f ? activeEventLabel : string.Empty;
    public string ActiveWarningLabel => warningTimer > 0f ? activeWarningLabel : string.Empty;
    public float ActiveWarningNormalized => warningTimer > 0f ? Mathf.Clamp01(warningTimer / Mathf.Max(0.001f, warningDuration)) : 0f;

    public void Configure(ProceduralArenaGenerator arenaGenerator)
    {
        arena = arenaGenerator;
        RefreshReferences();
        EnsureRuntimeRoot();
        ClearRuntimeObjects();
        pulseWasUnlocked = false;
        SchedulePowerup();
        ScheduleScorePickup();
        SchedulePulseEvent();
    }

    private void Start()
    {
        if (arena == null)
        {
            arena = GetComponent<ProceduralArenaGenerator>();
        }

        RefreshReferences();
        EnsureRuntimeRoot();
        pulseWasUnlocked = false;
        SchedulePowerup();
        ScheduleScorePickup();
        SchedulePulseEvent();
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

        bool pulseUnlocked = ShouldRunPulseEvents();
        if (pulseUnlocked && !pulseWasUnlocked)
        {
            pulseEventTimer = Mathf.Min(pulseEventTimer, 0.75f);
        }
        pulseWasUnlocked = pulseUnlocked;

        if (pulseUnlocked && (enemy == null || !enemy.IsMapEventSuppressed()))
        {
            pulseEventTimer -= Time.deltaTime;
            if (pulseEventTimer <= 0f)
            {
                StartCoroutine(SpawnPulseEventRoutine());
                SchedulePulseEvent();
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
        activeScorePickups.Clear();
        activeWarningLabel = string.Empty;
        warningTimer = 0f;
        warningDuration = 0f;
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

    private void ScheduleScorePickup()
    {
        float min = Mathf.Min(scorePickupIntervalRange.x, scorePickupIntervalRange.y);
        float max = Mathf.Max(scorePickupIntervalRange.x, scorePickupIntervalRange.y);
        scorePickupTimer = Random.Range(min, max);
    }

    private void TrySpawnPowerup()
    {
        if (!TryFindSpawnPoint(pickupProbeRadius, out Vector2 position))
        {
            return;
        }

        ArenaPowerupPickup.PickupKind kind =
            Random.value < Mathf.Clamp01(shieldPickupChance)
                ? ArenaPowerupPickup.PickupKind.Shield
                : ArenaPowerupPickup.PickupKind.SpeedBurst;

        GameObject go = new GameObject($"Powerup_{kind}");
        go.transform.SetParent(runtimeRoot, false);
        go.transform.position = new Vector3(position.x, position.y, 0f);
        go.transform.localScale = Vector3.one;

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CircleSpriteProvider.Get();
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.size = Vector2.one * 0.62f;
        sr.sortingOrder = 12;

        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.34f;

        ArenaPowerupPickup pickup = go.AddComponent<ArenaPowerupPickup>();
        pickup.Configure(this, kind, powerupLifetime, speedBoostMultiplier, speedBoostDuration, shieldDuration);
        activePickup = pickup;

        RaiseEvent(kind == ArenaPowerupPickup.PickupKind.Shield ? "Shield Materialized" : "Speed Core Materialized");
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
            if (!TryFindSpawnPoint(scorePickupProbeRadius, out Vector2 position))
            {
                continue;
            }

            GameObject go = new GameObject("ScoreDot");
            go.transform.SetParent(runtimeRoot, false);
            go.transform.position = new Vector3(position.x, position.y, 0f);
            go.transform.localScale = Vector3.one;

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = CircleSpriteProvider.Get();
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.size = Vector2.one * 0.34f;
            sr.sortingOrder = 12;

            CircleCollider2D col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.2f;

            ArenaScorePickup pickup = go.AddComponent<ArenaScorePickup>();
            pickup.Configure(this, gameManager, Mathf.Max(1, scorePickupPoints), scorePickupLifetime);
            activeScorePickups.Add(pickup);
        }
    }

    private IEnumerator SpawnPulseEventRoutine()
    {
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
            yield break;
        }

        RaiseEvent("Containment Lock");

        Vector2 pulseCenter = GetPulseFocusCenter();
        float clusterRadius = Mathf.Max(0.75f, pulseClusterRadius);
        float basePhase = Random.Range(0f, 360f);

        // Distribuye los peligros como un anillo irregular para que el jugador pueda leer un patron.
        for (int i = 0; i < count; i++)
        {
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

            float stagger = Mathf.Max(0f, pulseSpawnStagger);
            if (stagger > 0f && i < count - 1)
            {
                yield return new WaitForSeconds(stagger);
            }
        }
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
}

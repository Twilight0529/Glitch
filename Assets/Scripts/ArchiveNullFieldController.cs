using System.Collections.Generic;
using UnityEngine;

// Mecánica central de Archive: crea anomalías móviles que atraen actores, proyectiles y objetos de forma constante.
public class ArchiveNullFieldController : MonoBehaviour, IThemedEventStatusProvider
{
    // Evento de Archive: anomalías móviles capturan entidades y curvan su movimiento en órbitas visibles.
    private class Anchor
    {
        public GameObject root;
        public SpriteRenderer core;
        public SpriteRenderer ring;
        public Vector2 position;
        public Vector2 driftVelocity;
        public float seed;
        public float orbitDirection;
    }

    private class CapturedObstacle
    {
        public Transform root;
        public Rigidbody2D body;
        public Collider2D collider;
        public Anchor anchor;
        public Vector2 orbitalVelocity;
    }

    [Header("Timing")]
    [SerializeField] private float initialDelayMin = 18f;
    [SerializeField] private float initialDelayMax = 24f;
    [SerializeField] private float intervalMin = 15f;
    [SerializeField] private float intervalMax = 22f;
    [SerializeField] private float telegraphSeconds = 2f;
    [SerializeField] private float activeSeconds = 10.5f;

    [Header("Null Field")]
    [SerializeField] private int anchorCount = 3;
    [SerializeField] private float anchorRadius = 0.52f;
    [SerializeField] private float visualRadius = 2.8f;
    [SerializeField] private float influenceRadius = 3.6f;
    [SerializeField] private float playerPullSpeed = 4.6f;
    [SerializeField] private float enemyPullSpeed = 3.8f;
    [SerializeField] private float projectilePullSpeed = 9.5f;
    [SerializeField] private float pickupPullSpeed = 6.2f;
    [SerializeField] private float obstaclePullSpeed = 5.4f;
    [SerializeField] private float orbitAcceleration = 19f;
    [SerializeField] private float orbitSpeed = 6.4f;
    [SerializeField] private float orbitDrag = 1.45f;
    [SerializeField] private float minimumOrbitRadius = 0.9f;
    [SerializeField] private float maximumOrbitalSpeed = 8.6f;
    [SerializeField] private float projectileTurnRate = 300f;
    [SerializeField] private float playerSlowMultiplier = 0.94f;

    [Header("Moving Anomalies")]
    [SerializeField] private float anchorDriftSpeedMin = 0.65f;
    [SerializeField] private float anchorDriftSpeedMax = 1.15f;
    [SerializeField] private float anchorWallMargin = 1.35f;

    [Header("Captured Obstacles")]
    [SerializeField] private int obstaclesPerAnchor = 3;
    [SerializeField] private float obstacleCaptureRadiusMultiplier = 1.5f;
    [SerializeField] private float maximumObstacleHalfExtent = 2.2f;
    [SerializeField] private Color telegraphColor = new Color(0.68f, 0.62f, 1f, 0.82f);
    [SerializeField] private Color activeColor = new Color(0.44f, 0.95f, 1f, 0.92f);

    private readonly List<Anchor> anchors = new List<Anchor>();
    private readonly List<CapturedObstacle> capturedObstacles = new List<CapturedObstacle>();
    private Transform centerTransform;
    private Transform staticObstaclesRoot;
    private Transform dynamicObstaclesRoot;
    private ProceduralArenaGenerator arena;
    private GameManager gameManager;
    private EnemyController enemy;
    private PlayerController player;
    private float timer;
    private float eventTimer;
    private bool eventActive;
    private bool mapEventsWereUnlocked;
    private bool operationModifiersApplied;
    private Vector2 playerOrbitalVelocity;
    private Vector2 enemyOrbitalVelocity;
    private readonly Dictionary<int, Vector2> pickupOrbitalVelocities = new Dictionary<int, Vector2>();
    private readonly Dictionary<int, Vector2> cloneOrbitalVelocities = new Dictionary<int, Vector2>();
    private readonly Dictionary<int, Anchor> projectileAnchors = new Dictionary<int, Anchor>();
    private readonly Dictionary<int, Anchor> pickupAnchors = new Dictionary<int, Anchor>();
    private readonly Dictionary<int, Anchor> cloneAnchors = new Dictionary<int, Anchor>();
    private const string EventPressureKey = "ThemeArchiveNullField";

    public string ActiveThemedEventLabel => eventActive ? "POZO GRAVITACIONAL" : string.Empty;
    public string ActiveThemedEventHint => eventActive ? "Las anomalías arrastran la arena con ellas" : string.Empty;

    public void Configure(Transform center, Transform staticRoot, Transform dynamicRoot)
    {
        centerTransform = center != null ? center : transform;
        staticObstaclesRoot = staticRoot;
        dynamicObstaclesRoot = dynamicRoot;
        arena = centerTransform.GetComponent<ProceduralArenaGenerator>();
        ResolveReferences();
        ApplyOperationModifiersOnce();
        ClearAnchors();
        eventActive = false;
        ScheduleFirstEvent();
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

        intervalMin = Mathf.Max(5.0f, intervalMin * 0.60f);
        intervalMax = Mathf.Max(intervalMin + 1f, intervalMax * 0.68f);
        initialDelayMin = Mathf.Max(13f, initialDelayMin * 0.82f);
        initialDelayMax = Mathf.Max(initialDelayMin + 2f, initialDelayMax * 0.86f);
        activeSeconds += 1.35f;
        anchorCount = Mathf.Max(anchorCount + 1, 4);
        influenceRadius += 0.35f;
        playerPullSpeed += 0.22f;
        enemyPullSpeed += 0.18f;
        projectilePullSpeed += 0.35f;
        pickupPullSpeed += 0.25f;
        obstaclePullSpeed += 0.6f;
        orbitAcceleration += 1.1f;
        orbitSpeed += 0.45f;
        anchorDriftSpeedMax += 0.25f;
        obstaclesPerAnchor += 1;
    }

    private void OnDisable()
    {
        ClearAnchors();
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
                ClearAnchors();
                eventActive = false;
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
        bool active = eventTimer >= telegraphSeconds;
        UpdateAnchorMotion(active);
        UpdateVisuals(active);
        if (active)
        {
            ApplyField();
        }

        if (eventTimer >= telegraphSeconds + activeSeconds)
        {
            ClearAnchors();
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
        if (!gameManager.TryReserveEventPressure(EventPressureKey, 0.86f, duration, 3.8f))
        {
            timer = 1.8f;
            return;
        }

        ClearAnchors();
        eventActive = true;
        eventTimer = 0f;
        int count = Mathf.Max(2, anchorCount);
        float radiusX = arena.ArenaWidth * 0.30f;
        float radiusY = arena.ArenaHeight * 0.28f;
        float spin = Random.Range(0f, Mathf.PI * 2f);

        for (int i = 0; i < count; i++)
        {
            float angle = spin + (Mathf.PI * 2f * i / count);
            Vector2 pos = new Vector2(Mathf.Cos(angle) * radiusX, Mathf.Sin(angle) * radiusY);
            pos += Random.insideUnitCircle * 0.65f;
            CreateAnchor(pos, i);
        }

        CaptureNearbyObstacles();
    }

    private void CreateAnchor(Vector2 position, int index)
    {
        Vector2 driftDirection = Random.insideUnitCircle;
        if (driftDirection.sqrMagnitude <= 0.001f)
        {
            driftDirection = Vector2.right;
        }

        GameObject root = new GameObject($"ArchiveNullAnchor_{index}");
        root.transform.SetParent(centerTransform, false);
        root.transform.position = new Vector3(position.x, position.y, 0f);

        SpriteRenderer ring = root.AddComponent<SpriteRenderer>();
        ring.sprite = CircleSpriteProvider.Get();
        ring.drawMode = SpriteDrawMode.Sliced;
        ring.sortingOrder = 16;
        ring.size = Vector2.one * visualRadius * 2f;
        ring.color = telegraphColor;

        GameObject coreGo = new GameObject("NullAnchorCore");
        coreGo.transform.SetParent(root.transform, false);
        SpriteRenderer core = coreGo.AddComponent<SpriteRenderer>();
        core.sprite = CircleSpriteProvider.Get();
        core.drawMode = SpriteDrawMode.Sliced;
        core.sortingOrder = 18;
        core.size = Vector2.one * anchorRadius * 2f;
        core.color = activeColor;

        anchors.Add(new Anchor
        {
            root = root,
            ring = ring,
            core = core,
            position = position,
            driftVelocity = driftDirection.normalized * Random.Range(anchorDriftSpeedMin, anchorDriftSpeedMax),
            seed = Random.Range(0f, 12f),
            orbitDirection = Random.value < 0.5f ? -1f : 1f
        });
    }

    private void UpdateAnchorMotion(bool active)
    {
        if (arena == null)
        {
            return;
        }

        Vector2 center = centerTransform != null ? centerTransform.position : Vector2.zero;
        float margin = Mathf.Max(anchorWallMargin, influenceRadius * 0.42f);
        float minX = center.x - arena.ArenaWidth * 0.5f + margin;
        float maxX = center.x + arena.ArenaWidth * 0.5f - margin;
        float minY = center.y - arena.ArenaHeight * 0.5f + margin;
        float maxY = center.y + arena.ArenaHeight * 0.5f - margin;
        float motionMultiplier = active ? 1f : 0.32f;

        for (int i = 0; i < anchors.Count; i++)
        {
            Anchor anchor = anchors[i];
            if (anchor == null || anchor.root == null)
            {
                continue;
            }

            Vector2 next = anchor.position + anchor.driftVelocity * motionMultiplier * Time.deltaTime;
            if (next.x <= minX || next.x >= maxX)
            {
                anchor.driftVelocity.x *= -1f;
                next.x = Mathf.Clamp(next.x, minX, maxX);
            }
            if (next.y <= minY || next.y >= maxY)
            {
                anchor.driftVelocity.y *= -1f;
                next.y = Mathf.Clamp(next.y, minY, maxY);
            }

            anchor.position = next;
            anchor.root.transform.position = new Vector3(next.x, next.y, anchor.root.transform.position.z);
        }
    }

    private void UpdateVisuals(bool active)
    {
        float phase = active ? Mathf.Clamp01((eventTimer - telegraphSeconds) / activeSeconds) : Mathf.Clamp01(eventTimer / telegraphSeconds);
        for (int i = 0; i < anchors.Count; i++)
        {
            Anchor anchor = anchors[i];
            if (anchor == null)
            {
                continue;
            }

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (active ? 3.2f : 2.4f) + anchor.seed);
            if (anchor.ring != null)
            {
                Color c = active ? activeColor : telegraphColor;
                c.a = active ? Mathf.Lerp(0.15f, 0.32f, pulse) : Mathf.Lerp(0.08f, 0.18f, pulse) * phase;
                anchor.ring.color = c;
                anchor.ring.size = Vector2.one * visualRadius * 2f * Mathf.Lerp(1.00f, 0.86f, pulse);
            }

            if (anchor.core != null)
            {
                Color c = Color.Lerp(active ? activeColor : telegraphColor, Color.white, pulse * 0.30f);
                c.a = active ? Mathf.Lerp(0.46f, 0.78f, pulse) : Mathf.Lerp(0.22f, 0.54f, pulse) * phase;
                anchor.core.color = c;
                anchor.core.size = Vector2.one * anchorRadius * 2f * Mathf.Lerp(0.92f, 1.08f, pulse);
            }
        }
    }

    private void ApplyField()
    {
        if (player != null)
        {
            ApplyFieldToPlayer(player.GetPosition());
        }

        if (enemy != null)
        {
            ApplyFieldToEnemy(enemy.GetCurrentPosition());
        }

        ApplyFieldToProjectiles();
        ApplyFieldToPickups();
        ApplyFieldToSplitClones();
        ApplyFieldToCapturedObstacles();
    }

    private void ApplyFieldToPlayer(Vector2 position)
    {
        if (!TryResolveOrbitalAcceleration(position, playerPullSpeed, playerOrbitalVelocity, out Vector2 acceleration))
        {
            playerOrbitalVelocity = DampOrbitalVelocity(playerOrbitalVelocity);
            return;
        }

        playerOrbitalVelocity = IntegrateOrbitalVelocity(playerOrbitalVelocity, acceleration);
        player.ApplyExternalDisplacement(playerOrbitalVelocity * Time.deltaTime);
        player.ApplyMovementSlow(playerSlowMultiplier, 0.06f);
    }

    private void ApplyFieldToEnemy(Vector2 position)
    {
        if (!TryResolveOrbitalAcceleration(position, enemyPullSpeed, enemyOrbitalVelocity, out Vector2 acceleration))
        {
            enemyOrbitalVelocity = DampOrbitalVelocity(enemyOrbitalVelocity);
            return;
        }

        enemyOrbitalVelocity = IntegrateOrbitalVelocity(enemyOrbitalVelocity, acceleration);
        enemy.ApplyExternalDisplacement(enemyOrbitalVelocity * Time.deltaTime);
    }

    private void ApplyFieldToProjectiles()
    {
        AnomalyProjectile[] projectiles = FindObjectsByType<AnomalyProjectile>(FindObjectsSortMode.None);
        for (int i = 0; i < projectiles.Length; i++)
        {
            AnomalyProjectile projectile = projectiles[i];
            if (projectile == null)
            {
                continue;
            }

            int id = projectile.GetInstanceID();
            projectileAnchors.TryGetValue(id, out Anchor capturedAnchor);
            if (!TryResolveOrbitalAcceleration(
                    projectile.transform.position,
                    projectilePullSpeed,
                    projectile.CurrentVelocity,
                    out Vector2 acceleration,
                    preferredAnchor: capturedAnchor,
                    persistentCapture: capturedAnchor != null))
            {
                continue;
            }

            if (capturedAnchor == null && TryFindNearestAnchor(projectile.transform.position, influenceRadius, out capturedAnchor))
            {
                projectileAnchors[id] = capturedAnchor;
            }

            projectile.ApplyOrbitalAcceleration(acceleration, Time.deltaTime, projectileTurnRate);
        }
    }

    private void ApplyFieldToPickups()
    {
        ArenaScorePickup[] scorePickups = FindObjectsByType<ArenaScorePickup>(FindObjectsSortMode.None);
        for (int i = 0; i < scorePickups.Length; i++)
        {
            ApplyFieldToPickup(scorePickups[i]);
        }

        ArenaPowerupPickup[] powerups = FindObjectsByType<ArenaPowerupPickup>(FindObjectsSortMode.None);
        for (int i = 0; i < powerups.Length; i++)
        {
            ApplyFieldToPickup(powerups[i]);
        }
    }

    private void ApplyFieldToSplitClones()
    {
        SplitAnomalyCloneController[] clones = FindObjectsByType<SplitAnomalyCloneController>(FindObjectsSortMode.None);
        for (int i = 0; i < clones.Length; i++)
        {
            SplitAnomalyCloneController clone = clones[i];
            if (clone == null)
            {
                continue;
            }

            int id = clone.GetInstanceID();
            cloneOrbitalVelocities.TryGetValue(id, out Vector2 velocity);
            cloneAnchors.TryGetValue(id, out Anchor capturedAnchor);
            if (!TryResolveOrbitalAcceleration(
                    clone.GetCurrentPosition(),
                    enemyPullSpeed,
                    velocity,
                    out Vector2 acceleration,
                    preferredAnchor: capturedAnchor,
                    persistentCapture: capturedAnchor != null))
            {
                cloneOrbitalVelocities[id] = DampOrbitalVelocity(velocity);
                continue;
            }

            if (capturedAnchor == null && TryFindNearestAnchor(clone.GetCurrentPosition(), influenceRadius, out capturedAnchor))
            {
                cloneAnchors[id] = capturedAnchor;
            }

            velocity = IntegrateOrbitalVelocity(velocity, acceleration);
            cloneOrbitalVelocities[id] = velocity;
            clone.ApplyExternalDisplacement(velocity * Time.deltaTime);
        }
    }

    private void ApplyFieldToPickup(Component pickup)
    {
        if (pickup == null)
        {
            return;
        }

        int id = pickup.GetInstanceID();
        pickupOrbitalVelocities.TryGetValue(id, out Vector2 velocity);
        pickupAnchors.TryGetValue(id, out Anchor capturedAnchor);
        if (!TryResolveOrbitalAcceleration(
                pickup.transform.position,
                pickupPullSpeed,
                velocity,
                out Vector2 acceleration,
                preferredAnchor: capturedAnchor,
                persistentCapture: capturedAnchor != null))
        {
            pickupOrbitalVelocities[id] = DampOrbitalVelocity(velocity);
            return;
        }

        if (capturedAnchor == null && TryFindNearestAnchor(pickup.transform.position, influenceRadius, out capturedAnchor))
        {
            pickupAnchors[id] = capturedAnchor;
        }

        velocity = IntegrateOrbitalVelocity(velocity, acceleration);
        pickupOrbitalVelocities[id] = velocity;
        Vector2 delta = velocity * Time.deltaTime;
        if (pickup is ArenaScorePickup scorePickup)
        {
            scorePickup.ApplyExternalDisplacement(delta);
        }
        else if (pickup is ArenaPowerupPickup powerup)
        {
            powerup.ApplyExternalDisplacement(delta);
        }
    }

    private void CaptureNearbyObstacles()
    {
        capturedObstacles.Clear();
        List<Collider2D> candidates = new List<Collider2D>();
        CollectObstacleCandidates(staticObstaclesRoot, candidates);
        CollectObstacleCandidates(dynamicObstaclesRoot, candidates);
        HashSet<int> capturedIds = new HashSet<int>();
        float captureRadius = influenceRadius * Mathf.Max(1f, obstacleCaptureRadiusMultiplier);

        for (int anchorIndex = 0; anchorIndex < anchors.Count; anchorIndex++)
        {
            Anchor anchor = anchors[anchorIndex];
            int count = Mathf.Max(1, obstaclesPerAnchor);
            for (int slot = 0; slot < count; slot++)
            {
                Collider2D best = null;
                float bestDistance = float.MaxValue;
                for (int i = 0; i < candidates.Count; i++)
                {
                    Collider2D candidate = candidates[i];
                    if (candidate == null || capturedIds.Contains(candidate.transform.GetInstanceID()))
                    {
                        continue;
                    }

                    float distance = Vector2.Distance(candidate.bounds.center, anchor.position);
                    if (distance <= captureRadius && distance < bestDistance)
                    {
                        best = candidate;
                        bestDistance = distance;
                    }
                }

                if (best == null)
                {
                    break;
                }

                Transform obstacleRoot = best.transform;
                capturedIds.Add(obstacleRoot.GetInstanceID());
                capturedObstacles.Add(new CapturedObstacle
                {
                    root = obstacleRoot,
                    body = obstacleRoot.GetComponent<Rigidbody2D>(),
                    collider = best,
                    anchor = anchor,
                    orbitalVelocity = Vector2.zero
                });
            }
        }
    }

    private void CollectObstacleCandidates(Transform root, List<Collider2D> candidates)
    {
        if (root == null)
        {
            return;
        }

        Collider2D[] colliders = root.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D collider = colliders[i];
            if (collider == null || collider.isTrigger || !collider.gameObject.activeInHierarchy)
            {
                continue;
            }

            float halfExtent = Mathf.Max(collider.bounds.extents.x, collider.bounds.extents.y);
            if (halfExtent <= Mathf.Max(0.25f, maximumObstacleHalfExtent))
            {
                candidates.Add(collider);
            }
        }
    }

    private void ApplyFieldToCapturedObstacles()
    {
        if (arena == null)
        {
            return;
        }

        for (int i = capturedObstacles.Count - 1; i >= 0; i--)
        {
            CapturedObstacle obstacle = capturedObstacles[i];
            if (obstacle == null || obstacle.root == null || obstacle.collider == null)
            {
                capturedObstacles.RemoveAt(i);
                continue;
            }

            float halfExtent = Mathf.Max(obstacle.collider.bounds.extents.x, obstacle.collider.bounds.extents.y);
            float orbitRadius = Mathf.Max(minimumOrbitRadius, anchorRadius + halfExtent + 0.35f);
            Vector2 position = obstacle.body != null ? obstacle.body.position : (Vector2)obstacle.root.position;
            if (!TryResolveOrbitalAcceleration(
                    position,
                    obstaclePullSpeed,
                    obstacle.orbitalVelocity,
                    out Vector2 acceleration,
                    orbitRadius,
                    influenceRadius * Mathf.Max(1f, obstacleCaptureRadiusMultiplier),
                    obstacle.anchor,
                    true))
            {
                obstacle.orbitalVelocity = DampOrbitalVelocity(obstacle.orbitalVelocity);
                continue;
            }

            obstacle.orbitalVelocity = IntegrateOrbitalVelocity(obstacle.orbitalVelocity, acceleration);
            Vector2 target = ClampPointToArena(position + obstacle.orbitalVelocity * Time.deltaTime, halfExtent + 0.45f);
            if (obstacle.body != null)
            {
                obstacle.body.position = target;
            }

            obstacle.root.position = new Vector3(target.x, target.y, obstacle.root.position.z);
        }
    }

    private Vector2 ClampPointToArena(Vector2 point, float margin)
    {
        Vector2 center = centerTransform != null ? centerTransform.position : Vector2.zero;
        point.x = Mathf.Clamp(
            point.x,
            center.x - arena.ArenaWidth * 0.5f + margin,
            center.x + arena.ArenaWidth * 0.5f - margin);
        point.y = Mathf.Clamp(
            point.y,
            center.y - arena.ArenaHeight * 0.5f + margin,
            center.y + arena.ArenaHeight * 0.5f - margin);
        return point;
    }

    private bool TryResolveOrbitalAcceleration(
        Vector2 position,
        float pullScale,
        Vector2 currentVelocity,
        out Vector2 acceleration,
        float targetRadiusOverride = -1f,
        float fieldRadiusOverride = -1f,
        Anchor preferredAnchor = null,
        bool persistentCapture = false)
    {
        acceleration = Vector2.zero;
        float fieldRadius = fieldRadiusOverride > 0f ? fieldRadiusOverride : influenceRadius;
        Anchor selectedAnchor = IsAnchorValid(preferredAnchor) ? preferredAnchor : null;
        float selectedDistance = selectedAnchor != null
            ? Vector2.Distance(selectedAnchor.position, position)
            : float.MaxValue;

        if (selectedAnchor == null && !TryFindNearestAnchor(position, fieldRadius, out selectedAnchor))
        {
            return false;
        }

        selectedDistance = Vector2.Distance(selectedAnchor.position, position);
        if (selectedDistance <= 0.001f)
        {
            return false;
        }

        Vector2 inward = (selectedAnchor.position - position) / selectedDistance;
        Vector2 tangent = new Vector2(-inward.y, inward.x) * selectedAnchor.orbitDirection;
        float normalizedDistance = Mathf.Clamp01(selectedDistance / fieldRadius);
        float edgeStrength = Mathf.SmoothStep(0f, 1f, 1f - normalizedDistance);
        float fieldStrength = persistentCapture ? Mathf.Max(0.62f, edgeStrength) : edgeStrength;
        float targetRadius = targetRadiusOverride > 0f
            ? targetRadiusOverride
            : Mathf.Max(anchorRadius + 0.18f, minimumOrbitRadius);
        float radialError = selectedDistance - targetRadius;

        // La fuerza radial estabiliza la distancia y la tangencial curva la trayectoria sin anular la inercia.
        float radialAcceleration = Mathf.Clamp(radialError * pullScale, -pullScale * 1.6f, pullScale * 2.4f);
        float currentTangentialSpeed = Vector2.Dot(currentVelocity, tangent);
        float desiredTangentialSpeed = orbitSpeed * Mathf.Lerp(0.86f, 1.15f, normalizedDistance);
        float tangentialAcceleration = (desiredTangentialSpeed - currentTangentialSpeed) * orbitAcceleration * 0.48f;

        acceleration = (inward * radialAcceleration + tangent * tangentialAcceleration) * fieldStrength;
        return acceleration.sqrMagnitude > 0.0001f;
    }

    private bool TryFindNearestAnchor(Vector2 position, float radius, out Anchor selectedAnchor)
    {
        selectedAnchor = null;
        float selectedDistance = float.MaxValue;
        for (int i = 0; i < anchors.Count; i++)
        {
            Anchor anchor = anchors[i];
            if (!IsAnchorValid(anchor))
            {
                continue;
            }

            float distance = Vector2.Distance(anchor.position, position);
            if (distance <= radius && distance < selectedDistance)
            {
                selectedAnchor = anchor;
                selectedDistance = distance;
            }
        }

        return selectedAnchor != null;
    }

    private bool IsAnchorValid(Anchor anchor)
    {
        return anchor != null && anchor.root != null && anchors.Contains(anchor);
    }

    private Vector2 IntegrateOrbitalVelocity(Vector2 velocity, Vector2 acceleration)
    {
        velocity += acceleration * Time.deltaTime;
        velocity *= Mathf.Exp(-Mathf.Max(0f, orbitDrag) * Time.deltaTime * 0.22f);
        return Vector2.ClampMagnitude(velocity, Mathf.Max(0.5f, maximumOrbitalSpeed));
    }

    private Vector2 DampOrbitalVelocity(Vector2 velocity)
    {
        return Vector2.MoveTowards(velocity, Vector2.zero, Mathf.Max(0.1f, orbitDrag) * Time.deltaTime);
    }

    private void ClearAnchors()
    {
        for (int i = 0; i < anchors.Count; i++)
        {
            if (anchors[i]?.root != null)
            {
                Destroy(anchors[i].root);
            }
        }

        anchors.Clear();
        playerOrbitalVelocity = Vector2.zero;
        enemyOrbitalVelocity = Vector2.zero;
        pickupOrbitalVelocities.Clear();
        cloneOrbitalVelocities.Clear();
        projectileAnchors.Clear();
        pickupAnchors.Clear();
        cloneAnchors.Clear();
        capturedObstacles.Clear();
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

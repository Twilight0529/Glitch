using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class LabSweepEventController : MonoBehaviour
{
    // Evento del laboratorio: mueve obstaculos y crea barridos de esterilizacion con zonas seguras.
    private struct ObstacleBinding
    {
        public Transform transform;
        public Rigidbody2D rigidbody;
        public DynamicObstacleController dynamicController;
        public float obstacleRadius;
    }

    private struct ObstacleSnapshot
    {
        public ObstacleBinding binding;
        public Vector2 startPosition;
        public float startRotationZ;
        public bool dynamicWasEnabled;
        public float laneOffset;
        public float rotationOffset;
        public SpriteRenderer[] renderers;
        public Color[] baseColors;
    }

    [Header("Event Timing")]
    [SerializeField] private float intervalMin = 9f;
    [SerializeField] private float intervalMax = 15f;
    [SerializeField] private float durationMin = 5.5f;
    [SerializeField] private float durationMax = 8.5f;
    [SerializeField, Range(0.4f, 1f)] private float cadenceIntervalMultiplier = 0.72f;
    [SerializeField, Range(0.8f, 1.5f)] private float cadenceDurationMultiplier = 1.16f;

    [Header("Lab Sweep")]
    [SerializeField] private float laneHeight = 2.1f;
    [SerializeField] private Vector2 laneOffsetRange = new Vector2(0.55f, 1.2f);
    [SerializeField] private Vector2 rotationOffsetRange = new Vector2(3f, 11f);
    [SerializeField] private int minCycles = 2;
    [SerializeField] private int maxCycles = 4;
    [SerializeField, Range(0.35f, 0.95f)] private float envelopeFactor = 0.75f;
    [SerializeField] private float boundsPadding = 0.08f;

    [Header("Lab Distinctive Event - Sterilization Sweep")]
    [SerializeField] private bool enableSterilizationSweep = true;
    [SerializeField] private float sterilizationBandWidth = 2.25f;
    [SerializeField, Range(0.2f, 0.9f)] private float sterilizationCoverageMin = 0.38f;
    [SerializeField, Range(0.25f, 0.95f)] private float sterilizationCoverageMax = 0.62f;
    [SerializeField, Range(0.1f, 0.6f)] private float sterilizationTelegraphFraction = 0.28f;
    [SerializeField] private int sterilizationPassesMin = 2;
    [SerializeField] private int sterilizationPassesMax = 4;
    [SerializeField] private int sterilizationSafeCorridorsMin = 1;
    [SerializeField] private int sterilizationSafeCorridorsMax = 2;
    [SerializeField] private float sterilizationSafeCorridorHalfWidth = 1.2f;
    [SerializeField, Range(0.2f, 1f)] private float sterilizationSlowMultiplier = 0.62f;
    [SerializeField] private float sterilizationSlowDuration = 0.24f;
    [SerializeField] private float sterilizationEnemyBoostMultiplier = 1.18f;
    [SerializeField] private float sterilizationEnemyBoostDuration = 0.9f;
    [SerializeField] private float sterilizationEnemyBoostCooldown = 0.33f;
    [SerializeField] private float sterilizationHazardPulseSpeed = 2.4f;
    [SerializeField, Range(0.2f, 0.95f)] private float sterilizationHazardDutyCycle = 0.66f;
    [SerializeField] private Color sterilizationColor = new Color(0.98f, 0.57f, 0.63f, 0.68f);
    [SerializeField] private Color sterilizationTelegraphColor = new Color(1f, 0.87f, 0.55f, 0.45f);
    [SerializeField] private float sterilizationPulseSpeed = 4.2f;
    [SerializeField] private float sterilizationTelegraphPulseSpeed = 3.1f;
    [SerializeField] private Color sterilizationSafeColor = new Color(0.32f, 0.94f, 1f, 0.45f);
    [SerializeField] private float sterilizationSafeMarkerThickness = 0.14f;
    [SerializeField] private float sterilizationSafePulseSpeed = 2.8f;

    [Header("Lab Distinctive Event - Security Grid")]
    [SerializeField] private bool enableSecurityGrid = true;
    [SerializeField] private int securityGridLinesMin = 2;
    [SerializeField] private int securityGridLinesMax = 4;
    [SerializeField] private float securityGridTelegraphSeconds = 1.05f;
    [SerializeField] private Vector2 securityGridThicknessRange = new Vector2(0.26f, 0.42f);
    [SerializeField] private float securityGridPlayerSlowMultiplier = 0.55f;
    [SerializeField] private float securityGridPlayerSlowDuration = 0.18f;
    [SerializeField] private float securityGridEnemyBoostMultiplier = 1.16f;
    [SerializeField] private float securityGridEnemyBoostDuration = 0.48f;
    [SerializeField] private float securityGridEnemyBoostCooldown = 0.32f;
    [SerializeField] private Color securityGridTelegraphColor = new Color(1f, 0.86f, 0.54f, 0.58f);
    [SerializeField] private Color securityGridActiveColor = new Color(0.98f, 0.28f, 0.42f, 0.82f);

    [Header("Visual Telegraph")]
    [SerializeField, Range(0f, 1f)] private float activeColorPulseStrength = 0.6f;
    [SerializeField, Range(0f, 1f)] private float activeColorLightenAmount = 0.34f;
    [SerializeField] private float activeColorPulseSpeed = 2.2f;

    [Header("Debug")]
    [SerializeField] private bool debugTriggerEnabled = true;
    [SerializeField] private Key debugTriggerKey = Key.R;

    private Transform centerTransform;
    private readonly List<ObstacleBinding> obstacles = new List<ObstacleBinding>();
    private readonly List<ObstacleSnapshot> snapshots = new List<ObstacleSnapshot>();
    private readonly HashSet<Transform> dedupe = new HashSet<Transform>();

    private float interiorLeft;
    private float interiorRight;
    private float interiorBottom;
    private float interiorTop;

    private float nextEventTimer;
    private float eventTimer;
    private float eventDuration;
    private float eventCycles;
    private bool moveAlongX;
    private bool initialized;
    private bool eventActive;
    private bool mapEventsWereUnlocked;
    private EnemyController enemyController;
    private GameManager gameManager;
    private PlayerController playerController;
    private bool sterilizationAlongX;
    private int sterilizationPassCount;
    private float sterilizationEnemyBoostCooldownTimer;
    private GameObject sterilizationVisualRoot;
    private SpriteRenderer sterilizationVisualRenderer;
    private GameObject sterilizationTelegraphRoot;
    private SpriteRenderer sterilizationTelegraphRenderer;
    private GameObject sterilizationSafeRoot;
    private readonly List<SpriteRenderer> sterilizationSafeMarkers = new List<SpriteRenderer>();
    private readonly List<float> sterilizationSafeAxisValues = new List<float>();
    private readonly List<float> sterilizationPassPerpCenters = new List<float>();
    private readonly List<float> sterilizationPassPerpHalfExtents = new List<float>();
    private readonly List<ThemedEventZoneFx> securityGridZones = new List<ThemedEventZoneFx>();

    public void Configure(Transform center, Transform staticObstaclesRoot, Transform dynamicObstaclesRoot)
    {
        centerTransform = center != null ? center : transform;
        BuildInteriorBounds();
        RebuildObstacleList(staticObstaclesRoot, dynamicObstaclesRoot);

        initialized = true;
        eventActive = false;
        eventTimer = 0f;
        snapshots.Clear();
        ScheduleNextEvent();
    }

    private void OnDisable()
    {
        EndEvent(restoreControllers: true, snapBackToStart: true);
        DestroySterilizationVisuals();
        ClearSecurityGridZones();
    }

    private void Update()
    {
        if (!initialized || centerTransform == null)
        {
            return;
        }

        if (!IsMapEventsUnlocked())
        {
            mapEventsWereUnlocked = false;
            if (eventActive)
            {
                EndEvent(restoreControllers: true, snapBackToStart: true);
                ScheduleNextEvent();
            }

            return;
        }

        if (!mapEventsWereUnlocked)
        {
            mapEventsWereUnlocked = true;
            nextEventTimer = Mathf.Min(nextEventTimer, 0.75f);
        }

        if (IsMapEventSuppressed())
        {
            if (eventActive)
            {
                EndEvent(restoreControllers: true, snapBackToStart: true);
                ScheduleNextEvent();
            }

            return;
        }

        if (eventActive)
        {
            TickEvent();
            return;
        }

        if (WasDebugTriggerPressed())
        {
            BeginEvent();
            return;
        }

        nextEventTimer -= Time.deltaTime;
        if (nextEventTimer <= 0f)
        {
            BeginEvent();
        }
    }

    private bool WasDebugTriggerPressed()
    {
        if (!debugTriggerEnabled)
        {
            return false;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return false;
        }

        var keyControl = keyboard[debugTriggerKey];
        return keyControl != null && keyControl.wasPressedThisFrame;
    }

    private void RebuildObstacleList(Transform staticObstaclesRoot, Transform dynamicObstaclesRoot)
    {
        obstacles.Clear();
        dedupe.Clear();
        AddObstaclesFromRoot(staticObstaclesRoot);
        AddObstaclesFromRoot(dynamicObstaclesRoot);
    }

    private void AddObstaclesFromRoot(Transform root)
    {
        if (root == null)
        {
            return;
        }

        Collider2D[] colliders = root.GetComponentsInChildren<Collider2D>(includeInactive: true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D col = colliders[i];
            if (col == null || col.isTrigger)
            {
                continue;
            }

            Transform target = col.transform;
            if (target == null || !dedupe.Add(target))
            {
                continue;
            }

            if (target.GetComponent<PlayerController>() != null || target.GetComponent<EnemyController>() != null)
            {
                continue;
            }

            obstacles.Add(new ObstacleBinding
            {
                transform = target,
                rigidbody = target.GetComponent<Rigidbody2D>(),
                dynamicController = target.GetComponent<DynamicObstacleController>(),
                obstacleRadius = Mathf.Max(col.bounds.extents.x, col.bounds.extents.y)
            });
        }
    }

    private void BeginEvent()
    {
        if (centerTransform == null)
        {
            ScheduleNextEvent();
            return;
        }

        BuildInteriorBounds();
        snapshots.Clear();
        moveAlongX = Random.value < 0.5f;

        // El sweep mueve obstaculos y peligro de esterilizacion en ejes opuestos para que se lea mejor.
        eventDuration = Random.Range(Mathf.Min(durationMin, durationMax), Mathf.Max(durationMin, durationMax));
        eventDuration *= Mathf.Max(0.1f, cadenceDurationMultiplier);
        eventCycles = Random.Range(Mathf.Min(minCycles, maxCycles), Mathf.Max(minCycles, maxCycles) + 1);
        sterilizationAlongX = !moveAlongX;
        sterilizationPassCount = RollSterilizationPassCount();
        sterilizationEnemyBoostCooldownTimer = 0f;
        EnsureSterilizationVisual();
        BuildSterilizationPassLayout();
        HideSterilizationVisuals();
        SpawnSecurityGrid(eventDuration);

        for (int i = 0; i < obstacles.Count; i++)
        {
            ObstacleBinding binding = obstacles[i];
            if (binding.transform == null)
            {
                continue;
            }

            Vector2 start = binding.transform.position;

            snapshots.Add(new ObstacleSnapshot
            {
                binding = binding,
                startPosition = start,
                startRotationZ = binding.transform.eulerAngles.z,
                dynamicWasEnabled = binding.dynamicController != null && binding.dynamicController.enabled,
                laneOffset = 0f,
                rotationOffset = 0f,
                renderers = binding.transform.GetComponentsInChildren<SpriteRenderer>(includeInactive: true),
                baseColors = null
            });

            int last = snapshots.Count - 1;
            ObstacleSnapshot created = snapshots[last];
            created.baseColors = CaptureBaseColors(created.renderers);
            snapshots[last] = created;
        }

        eventTimer = 0f;
        eventActive = true;
    }

    private void TickEvent()
    {
        float dt = Time.deltaTime;
        eventTimer += dt;
        float progress = Mathf.Clamp01(eventTimer / Mathf.Max(0.0001f, eventDuration));

        // La logica del peligro y el pulso de color usan el mismo progreso normalizado del evento.
        TickSterilizationSweep(progress, dt);
        ApplyActiveColorPulse(progress);

        if (progress >= 1f)
        {
            EndEvent(restoreControllers: true, snapBackToStart: true);
            ScheduleNextEvent();
        }
    }

    private void EndEvent(bool restoreControllers, bool snapBackToStart)
    {
        if (!eventActive && snapshots.Count == 0)
        {
            return;
        }

        RestoreAllColors();

        snapshots.Clear();
        eventActive = false;
        eventTimer = 0f;
        sterilizationEnemyBoostCooldownTimer = 0f;
        HideSterilizationVisuals();
        sterilizationSafeAxisValues.Clear();
        sterilizationPassPerpCenters.Clear();
        sterilizationPassPerpHalfExtents.Clear();
        ClearSecurityGridZones();
    }

    private void ApplyActiveColorPulse(float progress)
    {
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Max(0.01f, activeColorPulseSpeed));
        float envelope = Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI);
        float blend = Mathf.Clamp01(activeColorPulseStrength) * pulse * envelope;
        float lighten = Mathf.Clamp01(activeColorLightenAmount);

        for (int i = 0; i < snapshots.Count; i++)
        {
            ObstacleSnapshot snapshot = snapshots[i];
            ApplyColors(snapshot.renderers, snapshot.baseColors, blend, lighten);
        }
    }

    private void RestoreAllColors()
    {
        for (int i = 0; i < snapshots.Count; i++)
        {
            ObstacleSnapshot snapshot = snapshots[i];
            ApplyColors(snapshot.renderers, snapshot.baseColors, 0f, 0f);
        }
    }

    private static Color[] CaptureBaseColors(SpriteRenderer[] renderers)
    {
        if (renderers == null || renderers.Length == 0)
        {
            return null;
        }

        Color[] colors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            colors[i] = renderer != null ? renderer.color : Color.white;
        }

        return colors;
    }

    private static void ApplyColors(SpriteRenderer[] renderers, Color[] baseColors, float blend, float lightenAmount)
    {
        if (renderers == null || baseColors == null)
        {
            return;
        }

        int count = Mathf.Min(renderers.Length, baseColors.Length);
        for (int i = 0; i < count; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Color baseColor = baseColors[i];
            Color lighter = Color.Lerp(baseColor, Color.white, Mathf.Clamp01(lightenAmount));
            lighter.a = baseColor.a;
            renderer.color = Color.Lerp(baseColor, lighter, Mathf.Clamp01(blend));
        }
    }

    private Vector2 ClampToInterior(Vector2 position, float radius)
    {
        float margin = Mathf.Max(0f, radius + boundsPadding);
        position.x = Mathf.Clamp(position.x, interiorLeft + margin, interiorRight - margin);
        position.y = Mathf.Clamp(position.y, interiorBottom + margin, interiorTop - margin);
        return position;
    }

    private static void MoveTransform(ObstacleBinding binding, Vector2 target)
    {
        if (binding.rigidbody != null && binding.rigidbody.bodyType == RigidbodyType2D.Kinematic)
        {
            binding.rigidbody.MovePosition(target);
        }
        else if (binding.transform != null)
        {
            binding.transform.position = new Vector3(target.x, target.y, binding.transform.position.z);
        }
    }

    private static void RotateTransform(ObstacleBinding binding, float rotationZ)
    {
        if (binding.rigidbody != null && binding.rigidbody.bodyType == RigidbodyType2D.Kinematic)
        {
            binding.rigidbody.MoveRotation(rotationZ);
        }
        else if (binding.transform != null)
        {
            binding.transform.rotation = Quaternion.Euler(0f, 0f, rotationZ);
        }
    }

    private void BuildInteriorBounds()
    {
        Vector2 center = centerTransform != null ? (Vector2)centerTransform.position : Vector2.zero;

        float halfW = 16f;
        float halfH = 9f;
        ProceduralArenaGenerator generator = centerTransform != null ? centerTransform.GetComponent<ProceduralArenaGenerator>() : null;
        if (generator != null)
        {
            halfW = generator.ArenaWidth * 0.5f;
            halfH = generator.ArenaHeight * 0.5f;
        }

        interiorLeft = center.x - halfW + 0.5f;
        interiorRight = center.x + halfW - 0.5f;
        interiorBottom = center.y - halfH + 0.5f;
        interiorTop = center.y + halfH - 0.5f;
        TryReadInteriorFromWalls(center);
    }

    private void TryReadInteriorFromWalls(Vector2 center)
    {
        if (centerTransform == null)
        {
            return;
        }

        Transform boundsRoot = centerTransform.Find("Bounds");
        if (boundsRoot == null)
        {
            return;
        }

        BoxCollider2D[] walls = boundsRoot.GetComponentsInChildren<BoxCollider2D>(true);
        bool hasLeft = false;
        bool hasRight = false;
        bool hasBottom = false;
        bool hasTop = false;
        float left = float.NegativeInfinity;
        float right = float.PositiveInfinity;
        float bottom = float.NegativeInfinity;
        float top = float.PositiveInfinity;

        for (int i = 0; i < walls.Length; i++)
        {
            BoxCollider2D wall = walls[i];
            if (wall == null)
            {
                continue;
            }

            Bounds b = wall.bounds;
            if (b.size.x >= b.size.y)
            {
                if (b.center.y >= center.y)
                {
                    top = Mathf.Min(top, b.min.y);
                    hasTop = true;
                }
                else
                {
                    bottom = Mathf.Max(bottom, b.max.y);
                    hasBottom = true;
                }
            }
            else
            {
                if (b.center.x >= center.x)
                {
                    right = Mathf.Min(right, b.min.x);
                    hasRight = true;
                }
                else
                {
                    left = Mathf.Max(left, b.max.x);
                    hasLeft = true;
                }
            }
        }

        if (hasLeft)
        {
            interiorLeft = left;
        }

        if (hasRight)
        {
            interiorRight = right;
        }

        if (hasBottom)
        {
            interiorBottom = bottom;
        }

        if (hasTop)
        {
            interiorTop = top;
        }
    }

    private void ScheduleNextEvent()
    {
        float min = Mathf.Min(intervalMin, intervalMax);
        float max = Mathf.Max(intervalMin, intervalMax);
        float cadence = Mathf.Max(0.1f, cadenceIntervalMultiplier);
        nextEventTimer = Random.Range(min, max) * cadence;
    }

    private bool IsMapEventSuppressed()
    {
        if (enemyController == null)
        {
            enemyController = FindAnyObjectByType<EnemyController>();
        }

        return enemyController != null && enemyController.IsMapEventSuppressed();
    }

    private bool IsMapEventsUnlocked()
    {
        if (gameManager == null)
        {
            gameManager = FindAnyObjectByType<GameManager>();
        }

        return gameManager != null && gameManager.AreMapEventsUnlocked;
    }

    private int RollSterilizationPassCount()
    {
        int min = Mathf.Max(1, Mathf.Min(sterilizationPassesMin, sterilizationPassesMax));
        int max = Mathf.Max(min, Mathf.Max(sterilizationPassesMin, sterilizationPassesMax));
        return Random.Range(min, max + 1);
    }

    private void BuildSterilizationPassLayout()
    {
        sterilizationPassPerpCenters.Clear();
        sterilizationPassPerpHalfExtents.Clear();

        int passCount = Mathf.Max(1, sterilizationPassCount);
        float perpendicularMin = sterilizationAlongX ? interiorBottom : interiorLeft;
        float perpendicularMax = sterilizationAlongX ? interiorTop : interiorRight;
        float perpendicularSpan = Mathf.Max(0.5f, perpendicularMax - perpendicularMin);

        float minCoverage = Mathf.Clamp01(Mathf.Min(sterilizationCoverageMin, sterilizationCoverageMax));
        float maxCoverage = Mathf.Clamp01(Mathf.Max(sterilizationCoverageMin, sterilizationCoverageMax));
        minCoverage = Mathf.Clamp(minCoverage, 0.2f, 0.95f);
        maxCoverage = Mathf.Clamp(maxCoverage, minCoverage, 0.98f);

        for (int i = 0; i < passCount; i++)
        {
            float coverage = Random.Range(minCoverage, maxCoverage);
            float halfExtent = Mathf.Max(0.4f, perpendicularSpan * coverage * 0.5f);
            halfExtent = Mathf.Min(halfExtent, perpendicularSpan * 0.5f - 0.1f);

            float centerMin = perpendicularMin + halfExtent;
            float centerMax = perpendicularMax - halfExtent;
            float center = centerMin <= centerMax
                ? Random.Range(centerMin, centerMax)
                : (perpendicularMin + perpendicularMax) * 0.5f;

            sterilizationPassPerpCenters.Add(center);
            sterilizationPassPerpHalfExtents.Add(halfExtent);
        }
    }

    private float GetSterilizationPassPerpendicularCenter(int passIndex, float fallback)
    {
        if (passIndex >= 0 && passIndex < sterilizationPassPerpCenters.Count)
        {
            return sterilizationPassPerpCenters[passIndex];
        }

        return fallback;
    }

    private float GetSterilizationPassPerpendicularHalfExtent(int passIndex, float fallback)
    {
        if (passIndex >= 0 && passIndex < sterilizationPassPerpHalfExtents.Count)
        {
            return sterilizationPassPerpHalfExtents[passIndex];
        }

        return fallback;
    }

    private void TickSterilizationSweep(float progress, float dt)
    {
        if (!enableSterilizationSweep)
        {
            HideSterilizationVisuals();
            return;
        }

        if (playerController == null)
        {
            playerController = FindAnyObjectByType<PlayerController>();
        }

        if (enemyController == null)
        {
            enemyController = FindAnyObjectByType<EnemyController>();
        }

        EnsureSterilizationVisual();
        if (sterilizationVisualRoot == null || sterilizationVisualRenderer == null || sterilizationTelegraphRoot == null || sterilizationTelegraphRenderer == null)
        {
            return;
        }

        if (sterilizationEnemyBoostCooldownTimer > 0f)
        {
            sterilizationEnemyBoostCooldownTimer -= dt;
        }

        int passCount = Mathf.Max(1, sterilizationPassCount);
        float raw = Mathf.Clamp01(progress) * passCount;
        int passIndex = Mathf.Clamp(Mathf.FloorToInt(raw), 0, passCount - 1);
        float passT = Mathf.Clamp01(raw - passIndex);
        float telegraphFraction = Mathf.Clamp(sterilizationTelegraphFraction, 0.1f, 0.6f);
        bool inTelegraph = passT < telegraphFraction;
        float sweepT = inTelegraph ? 0f : Mathf.Clamp01((passT - telegraphFraction) / Mathf.Max(0.01f, 1f - telegraphFraction));

        bool reverse = (passIndex & 1) == 1;
        float axisT = reverse ? 1f - sweepT : sweepT;
        float bandHalf = Mathf.Max(0.2f, sterilizationBandWidth) * 0.5f;
        float minAxis = sterilizationAlongX ? interiorLeft : interiorBottom;
        float maxAxis = sterilizationAlongX ? interiorRight : interiorTop;
        float axisValue = Mathf.Lerp(minAxis, maxAxis, axisT);

        float perpMin = sterilizationAlongX ? interiorBottom : interiorLeft;
        float perpMax = sterilizationAlongX ? interiorTop : interiorRight;
        float perpSpan = Mathf.Max(0.5f, perpMax - perpMin);
        float perpendicularCenter = GetSterilizationPassPerpendicularCenter(passIndex, (perpMin + perpMax) * 0.5f);
        float perpendicularHalfExtent = GetSterilizationPassPerpendicularHalfExtent(passIndex, perpSpan * 0.25f);
        perpendicularHalfExtent = Mathf.Clamp(perpendicularHalfExtent, 0.35f, perpSpan * 0.5f);
        perpendicularCenter = Mathf.Clamp(perpendicularCenter, perpMin + perpendicularHalfExtent, perpMax - perpendicularHalfExtent);
        float segmentLength = perpendicularHalfExtent * 2f;

        Vector2 sweepCenter = new Vector2((interiorLeft + interiorRight) * 0.5f, (interiorBottom + interiorTop) * 0.5f);
        Vector2 sweepSize;
        Vector2 telegraphCenter = sweepCenter;
        Vector2 telegraphSize;
        if (sterilizationAlongX)
        {
            sweepCenter.x = axisValue;
            sweepCenter.y = perpendicularCenter;
            sweepSize = new Vector2(Mathf.Max(0.5f, sterilizationBandWidth), Mathf.Max(0.5f, segmentLength));

            telegraphCenter.x = (interiorLeft + interiorRight) * 0.5f;
            telegraphCenter.y = perpendicularCenter;
            telegraphSize = new Vector2(Mathf.Max(0.5f, interiorRight - interiorLeft), Mathf.Max(0.5f, segmentLength));
        }
        else
        {
            sweepCenter.x = perpendicularCenter;
            sweepCenter.y = axisValue;
            sweepSize = new Vector2(Mathf.Max(0.5f, segmentLength), Mathf.Max(0.5f, sterilizationBandWidth));

            telegraphCenter.x = perpendicularCenter;
            telegraphCenter.y = (interiorBottom + interiorTop) * 0.5f;
            telegraphSize = new Vector2(Mathf.Max(0.5f, segmentLength), Mathf.Max(0.5f, interiorTop - interiorBottom));
        }

        sterilizationVisualRoot.transform.position = new Vector3(sweepCenter.x, sweepCenter.y, 0f);
        sterilizationVisualRenderer.size = sweepSize;
        sterilizationTelegraphRoot.transform.position = new Vector3(telegraphCenter.x, telegraphCenter.y, 0f);
        sterilizationTelegraphRenderer.size = telegraphSize;

        if (!sterilizationVisualRoot.activeSelf)
        {
            sterilizationVisualRoot.SetActive(true);
        }
        if (!sterilizationTelegraphRoot.activeSelf)
        {
            sterilizationTelegraphRoot.SetActive(true);
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Max(0.1f, sterilizationPulseSpeed));
        float hazardCycle = Mathf.Repeat(Time.time * Mathf.Max(0.1f, sterilizationHazardPulseSpeed), 1f);
        bool hazardActive = !inTelegraph && hazardCycle <= Mathf.Clamp01(sterilizationHazardDutyCycle);
        Color c = sterilizationColor;
        c.a = hazardActive
            ? Mathf.Lerp(0.42f, 0.82f, pulse)
            : Mathf.Lerp(0.15f, 0.28f, pulse);
        sterilizationVisualRenderer.color = c;

        float tPulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Max(0.1f, sterilizationTelegraphPulseSpeed));
        Color tColor = sterilizationTelegraphColor;
        tColor.a = inTelegraph
            ? Mathf.Lerp(0.22f, 0.58f, tPulse)
            : Mathf.Lerp(0.04f, 0.12f, tPulse);
        sterilizationTelegraphRenderer.color = tColor;

        if (playerController == null)
        {
            return;
        }

        if (inTelegraph || !hazardActive)
        {
            return;
        }

        Vector2 playerPos = playerController.GetPosition();
        float playerAxis = sterilizationAlongX ? playerPos.x : playerPos.y;
        bool playerInside = Mathf.Abs(playerAxis - axisValue) <= bandHalf;
        if (!playerInside)
        {
            return;
        }

        float playerPerpendicularAxis = sterilizationAlongX ? playerPos.y : playerPos.x;
        bool inSegment = Mathf.Abs(playerPerpendicularAxis - perpendicularCenter) <= perpendicularHalfExtent;
        if (!inSegment)
        {
            return;
        }

        playerController.ApplyMovementSlow(sterilizationSlowMultiplier, sterilizationSlowDuration);
        if (enemyController != null && sterilizationEnemyBoostCooldownTimer <= 0f)
        {
            enemyController.ApplyExternalSpeedModifier(sterilizationEnemyBoostMultiplier, sterilizationEnemyBoostDuration);
            sterilizationEnemyBoostCooldownTimer = Mathf.Max(0.05f, sterilizationEnemyBoostCooldown);
        }
    }

    private void EnsureSterilizationVisual()
    {
        if (sterilizationVisualRoot != null
            && sterilizationVisualRenderer != null
            && sterilizationTelegraphRoot != null
            && sterilizationTelegraphRenderer != null)
        {
            return;
        }

        if (sterilizationVisualRoot == null)
        {
            sterilizationVisualRoot = new GameObject("LabSterilizationBand");
        }

        sterilizationVisualRoot.transform.SetParent(centerTransform != null ? centerTransform : transform, false);
        sterilizationVisualRoot.transform.localScale = Vector3.one;

        sterilizationVisualRenderer = sterilizationVisualRoot.GetComponent<SpriteRenderer>();
        if (sterilizationVisualRenderer == null)
        {
            sterilizationVisualRenderer = sterilizationVisualRoot.AddComponent<SpriteRenderer>();
        }

        sterilizationVisualRenderer.sprite = SquareSpriteProvider.Get();
        sterilizationVisualRenderer.drawMode = SpriteDrawMode.Sliced;
        sterilizationVisualRenderer.sortingOrder = 7;
        sterilizationVisualRenderer.color = sterilizationColor;
        sterilizationVisualRoot.SetActive(false);

        if (sterilizationTelegraphRoot == null)
        {
            sterilizationTelegraphRoot = new GameObject("LabSterilizationTelegraph");
        }

        sterilizationTelegraphRoot.transform.SetParent(centerTransform != null ? centerTransform : transform, false);
        sterilizationTelegraphRoot.transform.localScale = Vector3.one;

        sterilizationTelegraphRenderer = sterilizationTelegraphRoot.GetComponent<SpriteRenderer>();
        if (sterilizationTelegraphRenderer == null)
        {
            sterilizationTelegraphRenderer = sterilizationTelegraphRoot.AddComponent<SpriteRenderer>();
        }

        sterilizationTelegraphRenderer.sprite = SquareSpriteProvider.Get();
        sterilizationTelegraphRenderer.drawMode = SpriteDrawMode.Sliced;
        sterilizationTelegraphRenderer.sortingOrder = 6;
        sterilizationTelegraphRenderer.color = sterilizationTelegraphColor;
        sterilizationTelegraphRoot.SetActive(false);
    }

    private void HideSterilizationVisuals()
    {
        if (sterilizationVisualRoot != null && sterilizationVisualRoot.activeSelf)
        {
            sterilizationVisualRoot.SetActive(false);
        }

        if (sterilizationTelegraphRoot != null && sterilizationTelegraphRoot.activeSelf)
        {
            sterilizationTelegraphRoot.SetActive(false);
        }

        if (sterilizationSafeRoot != null && sterilizationSafeRoot.activeSelf)
        {
            sterilizationSafeRoot.SetActive(false);
        }
    }

    private void DestroySterilizationVisuals()
    {
        DestroyVisualGameObject(ref sterilizationVisualRoot);
        DestroyVisualGameObject(ref sterilizationTelegraphRoot);
        DestroyVisualGameObject(ref sterilizationSafeRoot);
        sterilizationVisualRenderer = null;
        sterilizationTelegraphRenderer = null;
        sterilizationSafeMarkers.Clear();
        sterilizationSafeAxisValues.Clear();
        sterilizationPassPerpCenters.Clear();
        sterilizationPassPerpHalfExtents.Clear();
    }

    private void BuildSterilizationSafeCorridors()
    {
        sterilizationSafeAxisValues.Clear();

        float axisMin = sterilizationAlongX ? interiorBottom : interiorLeft;
        float axisMax = sterilizationAlongX ? interiorTop : interiorRight;
        float halfWidth = Mathf.Max(0.2f, sterilizationSafeCorridorHalfWidth);
        int corridorMin = Mathf.Max(2, Mathf.Min(sterilizationSafeCorridorsMin, sterilizationSafeCorridorsMax));
        int corridorMax = Mathf.Max(corridorMin, Mathf.Max(sterilizationSafeCorridorsMin, sterilizationSafeCorridorsMax));
        int corridorCount = Random.Range(corridorMin, corridorMax + 1);

        float innerMin = axisMin + halfWidth;
        float innerMax = axisMax - halfWidth;
        if (innerMax <= innerMin)
        {
            sterilizationSafeAxisValues.Add((axisMin + axisMax) * 0.5f);
            return;
        }

        float innerSpan = Mathf.Max(0.01f, innerMax - innerMin);
        float desiredSpacing = Mathf.Max(halfWidth * 2f + 0.45f, 0.8f);
        int maxNonOverlapping = Mathf.Max(1, Mathf.FloorToInt(innerSpan / desiredSpacing));
        corridorCount = Mathf.Clamp(corridorCount, 1, maxNonOverlapping);

        float segment = innerSpan / Mathf.Max(1, corridorCount + 1);
        for (int i = 0; i < corridorCount; i++)
        {
            float target = innerMin + segment * (i + 1);
            float jitter = Random.Range(-segment * 0.18f, segment * 0.18f);
            float axisValue = Mathf.Clamp(target + jitter, innerMin, innerMax);
            sterilizationSafeAxisValues.Add(axisValue);
        }

        sterilizationSafeAxisValues.Sort();
        float minSpacing = Mathf.Max(halfWidth * 1.7f, 0.7f);
        for (int i = 1; i < sterilizationSafeAxisValues.Count; i++)
        {
            float prev = sterilizationSafeAxisValues[i - 1];
            float current = sterilizationSafeAxisValues[i];
            if (current - prev >= minSpacing)
            {
                continue;
            }

            float corrected = Mathf.Clamp(prev + minSpacing, innerMin, innerMax);
            sterilizationSafeAxisValues[i] = corrected;
        }

        if (sterilizationSafeAxisValues.Count == 0)
        {
            sterilizationSafeAxisValues.Add((axisMin + axisMax) * 0.5f);
        }
    }

    private void UpdateSterilizationSafeMarkers()
    {
        EnsureSterilizationSafeRoot();
        if (sterilizationSafeRoot == null)
        {
            return;
        }

        while (sterilizationSafeMarkers.Count > sterilizationSafeAxisValues.Count)
        {
            int last = sterilizationSafeMarkers.Count - 1;
            SpriteRenderer marker = sterilizationSafeMarkers[last];
            sterilizationSafeMarkers.RemoveAt(last);
            if (marker != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(marker.gameObject);
                }
                else
                {
                    DestroyImmediate(marker.gameObject);
                }
            }
        }

        while (sterilizationSafeMarkers.Count < sterilizationSafeAxisValues.Count)
        {
            GameObject markerGo = new GameObject($"SafeCorridor_{sterilizationSafeMarkers.Count}");
            markerGo.transform.SetParent(sterilizationSafeRoot.transform, false);
            SpriteRenderer sr = markerGo.AddComponent<SpriteRenderer>();
            sr.sprite = SquareSpriteProvider.Get();
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.sortingOrder = 6;
            sterilizationSafeMarkers.Add(sr);
        }

        float thickness = Mathf.Max(0.03f, sterilizationSafeMarkerThickness);
        float left = interiorLeft;
        float right = interiorRight;
        float bottom = interiorBottom;
        float top = interiorTop;
        for (int i = 0; i < sterilizationSafeMarkers.Count; i++)
        {
            SpriteRenderer marker = sterilizationSafeMarkers[i];
            if (marker == null)
            {
                continue;
            }

            float axis = sterilizationSafeAxisValues[i];
            if (sterilizationAlongX)
            {
                marker.transform.position = new Vector3((left + right) * 0.5f, axis, 0f);
                marker.size = new Vector2(Mathf.Max(0.5f, right - left), thickness);
            }
            else
            {
                marker.transform.position = new Vector3(axis, (bottom + top) * 0.5f, 0f);
                marker.size = new Vector2(thickness, Mathf.Max(0.5f, top - bottom));
            }
        }

        sterilizationSafeRoot.SetActive(true);
        UpdateSterilizationSafeMarkersPulse();
    }

    private void UpdateSterilizationSafeMarkersPulse()
    {
        if (sterilizationSafeMarkers.Count == 0)
        {
            return;
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Max(0.1f, sterilizationSafePulseSpeed));
        Color color = sterilizationSafeColor;
        color.a = Mathf.Lerp(0.24f, 0.62f, pulse);
        for (int i = 0; i < sterilizationSafeMarkers.Count; i++)
        {
            if (sterilizationSafeMarkers[i] != null)
            {
                sterilizationSafeMarkers[i].color = color;
            }
        }
    }

    private bool IsInsideSafeCorridor(float axisValue)
    {
        float halfWidth = Mathf.Max(0.05f, sterilizationSafeCorridorHalfWidth);
        for (int i = 0; i < sterilizationSafeAxisValues.Count; i++)
        {
            if (Mathf.Abs(axisValue - sterilizationSafeAxisValues[i]) <= halfWidth)
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureSterilizationSafeRoot()
    {
        if (sterilizationSafeRoot != null)
        {
            return;
        }

        sterilizationSafeRoot = new GameObject("LabSterilizationSafeLanes");
        sterilizationSafeRoot.transform.SetParent(centerTransform != null ? centerTransform : transform, false);
        sterilizationSafeRoot.transform.localScale = Vector3.one;
        sterilizationSafeRoot.SetActive(false);
    }

    private void SpawnSecurityGrid(float totalDuration)
    {
        ClearSecurityGridZones();
        if (!enableSecurityGrid)
        {
            return;
        }

        int min = Mathf.Max(1, Mathf.Min(securityGridLinesMin, securityGridLinesMax));
        int max = Mathf.Max(min, Mathf.Max(securityGridLinesMin, securityGridLinesMax));
        int count = Random.Range(min, max + 1);
        float activeTime = Mathf.Max(0.75f, totalDuration - Mathf.Max(0.05f, securityGridTelegraphSeconds));
        for (int i = 0; i < count; i++)
        {
            bool vertical = Random.value < 0.5f;
            float thickness = Random.Range(
                Mathf.Min(securityGridThicknessRange.x, securityGridThicknessRange.y),
                Mathf.Max(securityGridThicknessRange.x, securityGridThicknessRange.y));

            Vector2 position;
            Vector2 size;
            if (vertical)
            {
                float x = Random.Range(interiorLeft + 1.1f, interiorRight - 1.1f);
                position = new Vector2(x, (interiorBottom + interiorTop) * 0.5f);
                size = new Vector2(thickness, Mathf.Max(0.5f, interiorTop - interiorBottom));
            }
            else
            {
                float y = Random.Range(interiorBottom + 1.1f, interiorTop - 1.1f);
                position = new Vector2((interiorLeft + interiorRight) * 0.5f, y);
                size = new Vector2(Mathf.Max(0.5f, interiorRight - interiorLeft), thickness);
            }

            GameObject zone = new GameObject($"LabSecurityGrid_{i}");
            zone.transform.SetParent(centerTransform != null ? centerTransform : transform, false);
            zone.transform.position = new Vector3(position.x, position.y, 0f);
            ThemedEventZoneFx fx = zone.AddComponent<ThemedEventZoneFx>();
            fx.ConfigureRect(
                ThemedEventZoneFx.ZoneKind.LabSecurityGrid,
                size,
                securityGridTelegraphSeconds,
                activeTime,
                securityGridTelegraphColor,
                securityGridActiveColor,
                securityGridPlayerSlowMultiplier,
                securityGridPlayerSlowDuration,
                securityGridEnemyBoostMultiplier,
                securityGridEnemyBoostDuration,
                securityGridEnemyBoostCooldown);
            securityGridZones.Add(fx);
        }
    }

    private void ClearSecurityGridZones()
    {
        for (int i = securityGridZones.Count - 1; i >= 0; i--)
        {
            ThemedEventZoneFx zone = securityGridZones[i];
            if (zone != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(zone.gameObject);
                }
                else
                {
                    DestroyImmediate(zone.gameObject);
                }
            }
        }

        securityGridZones.Clear();
    }

    private static void DestroyVisualGameObject(ref GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }

        target = null;
    }
}

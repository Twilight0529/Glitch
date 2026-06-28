using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Eventos activos de Storage. Convierte sus carriles y carga en obstáculos con ritmo propio.
public class StorageSurgeEventController : MonoBehaviour, IThemedEventStatusProvider
{
    // Evento de Storage: reacomoda carga y activa carriles transportadores que empujan actores.
    private enum StorageEventVariant
    {
        None,
        ConveyorOverload,
        MagneticCranes,
        CargoTransit
    }

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
        public float displacementScale;
        public float phaseDelay;
        public float laneSign;
        public float lateralOffset;
        public SpriteRenderer[] renderers;
        public Color[] baseColors;
    }

    [Header("Event Timing")]
    [SerializeField] private float intervalMin = 11f;
    [SerializeField] private float intervalMax = 17f;
    [SerializeField] private float durationMin = 5f;
    [SerializeField] private float durationMax = 7.5f;
    [SerializeField, Range(0.4f, 1f)] private float cadenceIntervalMultiplier = 0.7f;
    [SerializeField, Range(0.8f, 1.5f)] private float cadenceDurationMultiplier = 1.15f;

    [Header("Storage Distinctive Event - Cargo Reflow")]
    [SerializeField] private Vector2 displacementRange = new Vector2(1.2f, 2.2f);
    [SerializeField] private Vector2 rotationRange = new Vector2(5f, 16f);
    [SerializeField] private float waveStaggerSeconds = 0.6f;
    [SerializeField] private float recoilStrength = 0.22f;
    [SerializeField] private float laneHeight = 2.6f;
    [SerializeField] private float lateralOffsetMax = 0.2f;
    [SerializeField] private float laneFlipChance = 0.35f;
    [SerializeField] private float boundsPadding = 0.08f;

    [Header("Storage Distinctive Event - Conveyor Overload")]
    [SerializeField] private bool enableConveyorOverload = true;
    [SerializeField] private Vector2 conveyorStartRange = new Vector2(0.22f, 0.46f);
    [SerializeField] private Vector2 conveyorDurationRange = new Vector2(0.22f, 0.34f);
    [SerializeField, Range(0.05f, 0.35f)] private float conveyorTelegraphLead = 0.14f;
    [SerializeField] private int conveyorLaneCountMin = 1;
    [SerializeField] private int conveyorLaneCountMax = 2;
    [SerializeField] private Vector2 conveyorLaneWidthRange = new Vector2(1.55f, 2.45f);
    [SerializeField] private float conveyorLaneSpacing = 0.9f;
    [SerializeField] private float conveyorPlayerPushSpeed = 4.8f;
    [SerializeField] private float conveyorEnemyPushSpeed = 3.9f;
    [SerializeField] private Color conveyorTelegraphColor = new Color(0.47f, 0.85f, 1f, 0.34f);
    [SerializeField] private Color conveyorActiveColor = new Color(0.26f, 0.68f, 1f, 0.6f);
    [SerializeField] private float conveyorPulseSpeed = 5f;
    [SerializeField] private float conveyorMarkerPulseSpeed = 6.4f;
    [SerializeField] private Vector2 conveyorMarkerSize = new Vector2(0.36f, 0.12f);
    [SerializeField] private float conveyorMarkerTravelSpeed = 4.1f;

    [Header("Storage Distinctive Event - Magnetic Cranes")]
    [SerializeField] private bool enableMagneticCranes = true;
    [SerializeField, Range(0.05f, 0.45f)] private float magneticCraneTelegraphFraction = 0.24f;
    [SerializeField, Range(0.02f, 0.35f)] private float magneticCraneReturnFraction = 0.16f;
    [SerializeField] private float magneticCraneLateralOffsetMax = 1.15f;
    [SerializeField] private Color magneticCraneTelegraphColor = new Color(0.96f, 0.74f, 0.34f, 0.46f);
    [SerializeField] private Color magneticCraneActiveColor = new Color(0.34f, 0.92f, 1f, 0.72f);

    [Header("Storage Distinctive Event - Cargo Transit")]
    [SerializeField] private bool enableCargoTransit = true;
    [SerializeField] private int cargoBlockCountMin = 2;
    [SerializeField] private int cargoBlockCountMax = 4;
    [SerializeField] private float cargoTelegraphSeconds = 1.05f;
    [SerializeField] private Vector2 cargoSizeMin = new Vector2(1.25f, 0.75f);
    [SerializeField] private Vector2 cargoSizeMax = new Vector2(2.55f, 1.45f);
    [SerializeField] private float cargoRouteMargin = 0.9f;
    [SerializeField] private float cargoRoutePerpendicularJitter = 0.28f;
    [SerializeField] private float cargoRouteAlongJitter = 0.32f;
    [SerializeField] private float cargoAvoidActorRadius = 2.2f;
    [SerializeField] private Color cargoTelegraphColor = new Color(1f, 0.76f, 0.36f, 0.58f);
    [SerializeField] private Color cargoActiveColor = new Color(0.34f, 0.78f, 1f, 0.86f);

    [Header("Visual Telegraph")]
    [SerializeField, Range(0f, 1f)] private float activeColorPulseStrength = 0.62f;
    [SerializeField, Range(0f, 1f)] private float activeColorLightenAmount = 0.36f;
    [SerializeField] private float activeColorPulseSpeed = 2.1f;

    [Header("Event Pressure")]
    [SerializeField] private float eventPressureCost = 0.9f;
    [SerializeField] private float eventPressureCooldown = 4f;
    [SerializeField] private float eventPressureDurationPadding = 0.75f;

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
    private float baseDisplacement;
    private float baseRotation;
    private Vector2 surgeAxis;
    private Vector2 lateralAxis;
    private float conveyorStartT;
    private float conveyorEndT;
    private bool initialized;
    private bool eventActive;
    private bool mapEventsWereUnlocked;
    private EnemyController enemyController;
    private GameManager gameManager;
    private PlayerController playerController;
    private Rigidbody2D playerRigidbody;
    private Rigidbody2D enemyRigidbody;
    private GameObject conveyorVisualRoot;
    private readonly List<SpriteRenderer> conveyorLaneRenderers = new List<SpriteRenderer>();
    private readonly List<SpriteRenderer> conveyorLaneMarkers = new List<SpriteRenderer>();
    private readonly List<float> conveyorLaneCenters = new List<float>();
    private readonly List<float> conveyorLaneHalfWidths = new List<float>();
    private readonly List<float> conveyorLaneMarkerPhases = new List<float>();
    private readonly List<Vector2> conveyorLaneDirections = new List<Vector2>();
    private readonly List<StorageCargoBlockFx> cargoTransitBlocks = new List<StorageCargoBlockFx>();
    private StorageEventVariant currentVariant = StorageEventVariant.None;
    private ThemedEventSignatureFx signatureFx;
    private const string EventPressureKey = "ThemeStorageSurge";

    public string ActiveThemedEventLabel
    {
        get
        {
            if (GetLiveCargoTransitCount() > 0)
            {
                return "RUTA DE CARGA";
            }

            if (!eventActive)
            {
                return string.Empty;
            }

            switch (currentVariant)
            {
                case StorageEventVariant.MagneticCranes:
                    return "REORDEN DE CARGA";
                case StorageEventVariant.CargoTransit:
                    return "RUTA DE CARGA";
                default:
                    return "TRANSPORTADORES";
            }
        }
    }

    public string ActiveThemedEventHint
    {
        get
        {
            if (GetLiveCargoTransitCount() > 0)
            {
                return "La carga forma una ruta con espacios de paso";
            }

            if (!eventActive)
            {
                return string.Empty;
            }

            switch (currentVariant)
            {
                case StorageEventVariant.MagneticCranes:
                    return "Las gruas mueven obstaculos: lee el hueco";
                case StorageEventVariant.ConveyorOverload:
                    return "Los carriles arrastran todo en su direccion";
                case StorageEventVariant.CargoTransit:
                    return "La carga forma una ruta con espacios de paso";
                default:
                    return string.Empty;
            }
        }
    }

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
        DestroyConveyorVisuals();
        ClearCargoTransit();
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

    private StorageEventVariant PickVariant()
    {
        List<StorageEventVariant> variants = new List<StorageEventVariant>();
        if (enableConveyorOverload)
        {
            variants.Add(StorageEventVariant.ConveyorOverload);
        }
        if (enableMagneticCranes)
        {
            variants.Add(StorageEventVariant.MagneticCranes);
        }
        if (enableCargoTransit)
        {
            variants.Add(StorageEventVariant.CargoTransit);
        }

        if (variants.Count == 0)
        {
            return StorageEventVariant.None;
        }

        return variants[Random.Range(0, variants.Count)];
    }

    // Prepara una sola variante por vez y registra todo lo que deberá restaurarse al finalizar.
    private void BeginEvent()
    {
        if (centerTransform == null)
        {
            ScheduleNextEvent();
            return;
        }

        BuildInteriorBounds();
        snapshots.Clear();
        surgeAxis = PickPrimaryAxis();
        lateralAxis = new Vector2(-surgeAxis.y, surgeAxis.x);
        currentVariant = PickVariant();
        if (currentVariant == StorageEventVariant.None)
        {
            ScheduleNextEvent();
            return;
        }

        // El reacomodo de carga elige un eje principal; la sobrecarga de transportadores reutiliza ese ritmo.
        eventDuration = Random.Range(Mathf.Min(durationMin, durationMax), Mathf.Max(durationMin, durationMax));
        eventDuration *= Mathf.Max(0.1f, cadenceDurationMultiplier);
        if (!TryReserveEventPressure(eventDuration))
        {
            SchedulePressureRetry();
            return;
        }

        baseDisplacement = Random.Range(Mathf.Min(displacementRange.x, displacementRange.y), Mathf.Max(displacementRange.x, displacementRange.y));
        baseRotation = Random.Range(Mathf.Min(rotationRange.x, rotationRange.y), Mathf.Max(rotationRange.x, rotationRange.y));
        HideConveyorVisuals();

        if (currentVariant == StorageEventVariant.ConveyorOverload)
        {
            BuildConveyorLayout();
            EnsureConveyorVisuals();
            SpawnSignature(ThemedEventSignatureFx.SignatureKind.StorageConveyor, conveyorActiveColor, conveyorTelegraphColor);
        }
        else if (currentVariant == StorageEventVariant.MagneticCranes)
        {
            SpawnSignature(ThemedEventSignatureFx.SignatureKind.StorageMagnet, magneticCraneActiveColor, magneticCraneTelegraphColor);
        }
        else if (currentVariant == StorageEventVariant.CargoTransit)
        {
            SpawnCargoTransit(eventDuration);
            SpawnSignature(ThemedEventSignatureFx.SignatureKind.StorageCargo, cargoActiveColor, cargoTelegraphColor);
        }

        for (int i = 0; i < obstacles.Count; i++)
        {
            ObstacleBinding binding = obstacles[i];
            if (binding.transform == null)
            {
                continue;
            }

            snapshots.Add(new ObstacleSnapshot
            {
                binding = binding,
                startPosition = binding.transform.position,
                startRotationZ = binding.transform.eulerAngles.z,
                dynamicWasEnabled = binding.dynamicController != null && binding.dynamicController.enabled,
                displacementScale = 0f,
                phaseDelay = 0f,
                laneSign = 0f,
                lateralOffset = 0f,
                renderers = binding.transform.GetComponentsInChildren<SpriteRenderer>(includeInactive: true),
                baseColors = null
            });

            int last = snapshots.Count - 1;
            ObstacleSnapshot created = snapshots[last];
            created.baseColors = CaptureBaseColors(created.renderers);
            snapshots[last] = created;
        }

        if (currentVariant == StorageEventVariant.MagneticCranes)
        {
            PrepareMagneticCraneReorder();
        }

        eventTimer = 0f;
        eventActive = true;
    }

    private void TickEvent()
    {
        float dt = Time.deltaTime;
        eventTimer += dt;
        float eventProgress = Mathf.Clamp01(eventTimer / Mathf.Max(0.0001f, eventDuration));

        // Visuales del transportador, empuje de actores y pulso de obstaculos usan el mismo progreso del evento.
        if (currentVariant == StorageEventVariant.ConveyorOverload)
        {
            TickConveyorOverload(eventProgress, dt);
            ApplyActiveColorPulse(eventProgress);
        }
        else if (currentVariant == StorageEventVariant.MagneticCranes)
        {
            TickMagneticCraneReorder(eventProgress);
            ApplyMagneticCraneColorPulse(eventProgress);
        }

        if (eventProgress >= 1f)
        {
            EndEvent(restoreControllers: true, snapBackToStart: true);
            ScheduleNextEvent();
        }
    }

    private void EndEvent(bool restoreControllers, bool snapBackToStart)
    {
        if (!eventActive && snapshots.Count == 0 && cargoTransitBlocks.Count == 0)
        {
            return;
        }

        RestoreAllColors();
        RestoreSnapshots(restoreControllers, snapBackToStart);
        ReleaseEventPressure();

        snapshots.Clear();
        eventActive = false;
        eventTimer = 0f;
        HideConveyorVisuals();
        ClearCargoTransit();
        ClearSignature();
        currentVariant = StorageEventVariant.None;
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

    private void ApplyMagneticCraneColorPulse(float progress)
    {
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Max(0.01f, activeColorPulseSpeed + 1.4f));
        float envelope = Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI);
        float blend = Mathf.Clamp01(0.34f + activeColorPulseStrength * pulse * envelope);
        Color target = progress < Mathf.Clamp01(magneticCraneTelegraphFraction)
            ? magneticCraneTelegraphColor
            : magneticCraneActiveColor;

        for (int i = 0; i < snapshots.Count; i++)
        {
            ObstacleSnapshot snapshot = snapshots[i];
            ApplyColorsToward(snapshot.renderers, snapshot.baseColors, target, blend);
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

    private void PrepareMagneticCraneReorder()
    {
        if (snapshots.Count == 0)
        {
            return;
        }

        float staggerFraction = eventDuration > 0.01f
            ? Mathf.Clamp01(waveStaggerSeconds / eventDuration)
            : 0.08f;
        staggerFraction = Mathf.Min(staggerFraction, 0.18f);

        for (int i = 0; i < snapshots.Count; i++)
        {
            ObstacleSnapshot snapshot = snapshots[i];
            if (snapshot.binding.dynamicController != null)
            {
                snapshot.binding.dynamicController.enabled = false;
            }

            float orderT = snapshots.Count <= 1 ? 0f : i / (float)(snapshots.Count - 1);
            float waveDelay = orderT * staggerFraction;
            float alternatingSign = (i & 1) == 0 ? 1f : -1f;
            if (Random.value < Mathf.Clamp01(laneFlipChance))
            {
                alternatingSign *= -1f;
            }

            snapshot.displacementScale = Random.Range(0.72f, 1.28f);
            snapshot.phaseDelay = waveDelay + Random.Range(0f, 0.035f);
            snapshot.laneSign = alternatingSign;
            snapshot.lateralOffset = Random.Range(-Mathf.Abs(magneticCraneLateralOffsetMax), Mathf.Abs(magneticCraneLateralOffsetMax));
            snapshots[i] = snapshot;
        }
    }

    private void TickMagneticCraneReorder(float eventProgress)
    {
        if (snapshots.Count == 0)
        {
            return;
        }

        float telegraphEnd = Mathf.Clamp(magneticCraneTelegraphFraction, 0.04f, 0.65f);
        float returnStart = Mathf.Clamp01(1f - Mathf.Clamp(magneticCraneReturnFraction, 0.02f, 0.45f));

        for (int i = 0; i < snapshots.Count; i++)
        {
            ObstacleSnapshot snapshot = snapshots[i];
            if (snapshot.binding.transform == null)
            {
                continue;
            }

            float moveIn = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(telegraphEnd + snapshot.phaseDelay, 0.58f + snapshot.phaseDelay, eventProgress));
            float moveOut = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(returnStart + snapshot.phaseDelay * 0.25f, 0.99f, eventProgress));
            float amount = Mathf.Clamp01(moveIn * moveOut);
            float shudder = Mathf.Sin(Time.time * 11.5f + snapshot.phaseDelay * 37f) * recoilStrength * Mathf.Sin(amount * Mathf.PI);
            Vector2 cargoOffset = surgeAxis * (baseDisplacement * snapshot.displacementScale * snapshot.laneSign);
            cargoOffset += lateralAxis * snapshot.lateralOffset;
            Vector2 target = snapshot.startPosition + cargoOffset * amount + lateralAxis * shudder;
            target = ClampToInterior(target, snapshot.binding.obstacleRadius);

            float rotationTarget = snapshot.startRotationZ + baseRotation * snapshot.laneSign * amount;
            rotationTarget += shudder * 18f;
            MoveTransform(snapshot.binding, target);
            RotateTransform(snapshot.binding, rotationTarget);
        }
    }

    private void RestoreSnapshots(bool restoreControllers, bool snapBackToStart)
    {
        for (int i = 0; i < snapshots.Count; i++)
        {
            ObstacleSnapshot snapshot = snapshots[i];
            if (snapshot.binding.transform == null)
            {
                continue;
            }

            if (snapBackToStart)
            {
                MoveTransform(snapshot.binding, snapshot.startPosition);
                RotateTransform(snapshot.binding, snapshot.startRotationZ);
            }

            if (restoreControllers && snapshot.binding.dynamicController != null)
            {
                snapshot.binding.dynamicController.enabled = snapshot.dynamicWasEnabled;
            }
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

    private static void ApplyColorsToward(SpriteRenderer[] renderers, Color[] baseColors, Color target, float blend)
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
            Color tinted = Color.Lerp(baseColor, target, Mathf.Clamp01(blend));
            tinted.a = baseColor.a;
            renderer.color = tinted;
        }
    }

    private void BuildConveyorLayout()
    {
        conveyorLaneCenters.Clear();
        conveyorLaneHalfWidths.Clear();
        conveyorLaneDirections.Clear();
        conveyorLaneMarkerPhases.Clear();

        if (!enableConveyorOverload)
        {
            return;
        }

        float startMin = Mathf.Clamp01(Mathf.Min(conveyorStartRange.x, conveyorStartRange.y));
        float startMax = Mathf.Clamp01(Mathf.Max(conveyorStartRange.x, conveyorStartRange.y));
        conveyorStartT = Random.Range(startMin, startMax);

        float durationMinT = Mathf.Clamp01(Mathf.Min(conveyorDurationRange.x, conveyorDurationRange.y));
        float durationMaxT = Mathf.Clamp01(Mathf.Max(conveyorDurationRange.x, conveyorDurationRange.y));
        float durationT = Random.Range(durationMinT, durationMaxT);
        conveyorEndT = Mathf.Clamp(conveyorStartT + durationT, conveyorStartT + 0.08f, 0.97f);

        float axisMin = Mathf.Abs(surgeAxis.x) > 0.5f ? interiorBottom : interiorLeft;
        float axisMax = Mathf.Abs(surgeAxis.x) > 0.5f ? interiorTop : interiorRight;
        int minCount = Mathf.Max(1, Mathf.Min(conveyorLaneCountMin, conveyorLaneCountMax));
        int maxCount = Mathf.Max(minCount, Mathf.Max(conveyorLaneCountMin, conveyorLaneCountMax));
        int laneCount = Random.Range(minCount, maxCount + 1);

        float halfWidthFloor = Mathf.Max(0.24f, Mathf.Min(conveyorLaneWidthRange.x, conveyorLaneWidthRange.y) * 0.5f);
        float halfWidthCeil = Mathf.Max(halfWidthFloor, Mathf.Max(conveyorLaneWidthRange.x, conveyorLaneWidthRange.y) * 0.5f);

        int attempts = Mathf.Max(8, laneCount * 24);
        for (int i = 0; i < attempts && conveyorLaneCenters.Count < laneCount; i++)
        {
            float halfWidth = Random.Range(halfWidthFloor, halfWidthCeil);
            float center = Random.Range(axisMin + halfWidth, axisMax - halfWidth);
            bool overlaps = false;
            for (int j = 0; j < conveyorLaneCenters.Count; j++)
            {
                float requiredGap = conveyorLaneHalfWidths[j] + halfWidth + Mathf.Max(0f, conveyorLaneSpacing);
                if (Mathf.Abs(center - conveyorLaneCenters[j]) < requiredGap)
                {
                    overlaps = true;
                    break;
                }
            }

            if (overlaps)
            {
                continue;
            }

            conveyorLaneCenters.Add(center);
            conveyorLaneHalfWidths.Add(halfWidth);
            float dirSign = ((i & 1) == 0) ? 1f : -1f;
            conveyorLaneDirections.Add(surgeAxis * dirSign);
            conveyorLaneMarkerPhases.Add(Random.value);
        }

        if (conveyorLaneCenters.Count == 0)
        {
            conveyorLaneCenters.Add((axisMin + axisMax) * 0.5f);
            conveyorLaneHalfWidths.Add(halfWidthFloor);
            conveyorLaneDirections.Add(surgeAxis);
            conveyorLaneMarkerPhases.Add(Random.value);
        }
    }

    private void TickConveyorOverload(float eventProgress, float dt)
    {
        if (!enableConveyorOverload || conveyorLaneCenters.Count == 0)
        {
            HideConveyorVisuals();
            return;
        }

        EnsureConveyorVisuals();
        if (conveyorVisualRoot == null || conveyorLaneRenderers.Count == 0)
        {
            return;
        }

        float telegraphStartT = Mathf.Clamp01(conveyorStartT - Mathf.Clamp01(conveyorTelegraphLead));
        bool inTelegraph = eventProgress >= telegraphStartT && eventProgress < conveyorStartT;
        bool inActive = eventProgress >= conveyorStartT && eventProgress <= conveyorEndT;
        if (!inTelegraph && !inActive)
        {
            HideConveyorVisuals();
            return;
        }

        UpdateConveyorVisuals(inTelegraph, inActive, dt);
        if (!inActive)
        {
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
        if (playerRigidbody == null && playerController != null)
        {
            playerRigidbody = playerController.GetComponent<Rigidbody2D>();
        }
        if (enemyRigidbody == null && enemyController != null)
        {
            enemyRigidbody = enemyController.GetComponent<Rigidbody2D>();
        }

        ApplyConveyorDriftToActor(playerController != null ? playerController.transform : null, playerRigidbody, conveyorPlayerPushSpeed, dt);
        ApplyConveyorDriftToActor(enemyController != null ? enemyController.transform : null, enemyRigidbody, conveyorEnemyPushSpeed, dt);
    }

    private void ApplyConveyorDriftToActor(Transform actor, Rigidbody2D actorRb, float pushSpeed, float dt)
    {
        if (actor == null || pushSpeed <= 0f || conveyorLaneCenters.Count == 0)
        {
            return;
        }

        Vector2 pos = actor.position;
        float actorAxisCoord = Mathf.Abs(surgeAxis.x) > 0.5f ? pos.y : pos.x;
        for (int i = 0; i < conveyorLaneCenters.Count; i++)
        {
            if (i >= conveyorLaneHalfWidths.Count || i >= conveyorLaneDirections.Count)
            {
                continue;
            }

            if (Mathf.Abs(actorAxisCoord - conveyorLaneCenters[i]) > conveyorLaneHalfWidths[i])
            {
                continue;
            }

            Vector2 drift = conveyorLaneDirections[i].normalized * pushSpeed * dt;
            Vector2 target = ClampToInterior(pos + drift, 0.15f);
            if (actorRb != null && actorRb.bodyType == RigidbodyType2D.Kinematic)
            {
                actorRb.MovePosition(target);
            }
            else
            {
                actor.position = new Vector3(target.x, target.y, actor.position.z);
            }

            break;
        }
    }

    private void EnsureConveyorVisuals()
    {
        if (conveyorVisualRoot == null)
        {
            conveyorVisualRoot = new GameObject("StorageConveyorOverload");
            conveyorVisualRoot.transform.SetParent(centerTransform != null ? centerTransform : transform, false);
            conveyorVisualRoot.transform.localScale = Vector3.one;
            conveyorVisualRoot.SetActive(false);
        }

        while (conveyorLaneRenderers.Count > conveyorLaneCenters.Count)
        {
            int last = conveyorLaneRenderers.Count - 1;
            SpriteRenderer lane = conveyorLaneRenderers[last];
            conveyorLaneRenderers.RemoveAt(last);
            if (lane != null)
            {
                if (Application.isPlaying) Destroy(lane.gameObject); else DestroyImmediate(lane.gameObject);
            }
        }

        while (conveyorLaneMarkers.Count > conveyorLaneCenters.Count)
        {
            int last = conveyorLaneMarkers.Count - 1;
            SpriteRenderer marker = conveyorLaneMarkers[last];
            conveyorLaneMarkers.RemoveAt(last);
            if (marker != null)
            {
                if (Application.isPlaying) Destroy(marker.gameObject); else DestroyImmediate(marker.gameObject);
            }
        }

        while (conveyorLaneRenderers.Count < conveyorLaneCenters.Count)
        {
            int idx = conveyorLaneRenderers.Count;
            GameObject lane = new GameObject($"ConveyorLane_{idx}");
            lane.transform.SetParent(conveyorVisualRoot.transform, false);
            SpriteRenderer laneSr = lane.AddComponent<SpriteRenderer>();
            laneSr.sprite = SquareSpriteProvider.Get();
            laneSr.drawMode = SpriteDrawMode.Sliced;
            laneSr.sortingOrder = 8;
            conveyorLaneRenderers.Add(laneSr);

            GameObject marker = new GameObject($"ConveyorMarker_{idx}");
            marker.transform.SetParent(conveyorVisualRoot.transform, false);
            SpriteRenderer markerSr = marker.AddComponent<SpriteRenderer>();
            markerSr.sprite = SquareSpriteProvider.Get();
            markerSr.drawMode = SpriteDrawMode.Sliced;
            markerSr.sortingOrder = 9;
            conveyorLaneMarkers.Add(markerSr);
        }
    }

    private void UpdateConveyorVisuals(bool inTelegraph, bool inActive, float dt)
    {
        if (conveyorVisualRoot == null)
        {
            return;
        }

        if (!conveyorVisualRoot.activeSelf)
        {
            conveyorVisualRoot.SetActive(true);
        }

        float fullWidth = Mathf.Max(0.4f, interiorRight - interiorLeft);
        float fullHeight = Mathf.Max(0.4f, interiorTop - interiorBottom);
        float centerX = (interiorLeft + interiorRight) * 0.5f;
        float centerY = (interiorBottom + interiorTop) * 0.5f;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Max(0.1f, conveyorPulseSpeed));
        float markerPulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Max(0.1f, conveyorMarkerPulseSpeed));
        float markerTravelHalfSpan = Mathf.Abs(surgeAxis.x) > 0.5f ? fullWidth * 0.45f : fullHeight * 0.45f;

        for (int i = 0; i < conveyorLaneRenderers.Count; i++)
        {
            if (i >= conveyorLaneCenters.Count || i >= conveyorLaneHalfWidths.Count || i >= conveyorLaneDirections.Count)
            {
                continue;
            }

            SpriteRenderer lane = conveyorLaneRenderers[i];
            SpriteRenderer marker = i < conveyorLaneMarkers.Count ? conveyorLaneMarkers[i] : null;
            if (lane == null)
            {
                continue;
            }

            float laneCenter = conveyorLaneCenters[i];
            float laneHalfWidth = conveyorLaneHalfWidths[i];
            Vector2 dir = conveyorLaneDirections[i].normalized;
            if (Mathf.Abs(surgeAxis.x) > 0.5f)
            {
                lane.transform.position = new Vector3(centerX, laneCenter, 0f);
                lane.size = new Vector2(fullWidth, Mathf.Max(0.2f, laneHalfWidth * 2f));
            }
            else
            {
                lane.transform.position = new Vector3(laneCenter, centerY, 0f);
                lane.size = new Vector2(Mathf.Max(0.2f, laneHalfWidth * 2f), fullHeight);
            }

            Color laneColor = inActive ? conveyorActiveColor : conveyorTelegraphColor;
            laneColor.a = inActive ? Mathf.Lerp(0.32f, 0.72f, pulse) : Mathf.Lerp(0.14f, 0.42f, pulse);
            lane.color = laneColor;

            if (marker == null)
            {
                continue;
            }

            conveyorLaneMarkerPhases[i] = Mathf.Repeat(
                conveyorLaneMarkerPhases[i] + (dt * Mathf.Max(0.1f, conveyorMarkerTravelSpeed)),
                1f);
            float markerOffset = Mathf.Lerp(-markerTravelHalfSpan, markerTravelHalfSpan, conveyorLaneMarkerPhases[i]);
            Vector2 laneCenterPos = (Mathf.Abs(surgeAxis.x) > 0.5f)
                ? new Vector2(centerX, laneCenter)
                : new Vector2(laneCenter, centerY);
            Vector2 markerPos = laneCenterPos + dir * markerOffset;
            marker.transform.position = new Vector3(markerPos.x, markerPos.y, 0f);
            marker.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            marker.size = conveyorMarkerSize;
            Color markerColor = inActive ? Color.Lerp(conveyorActiveColor, Color.white, 0.35f) : Color.Lerp(conveyorTelegraphColor, Color.white, 0.22f);
            markerColor.a = inActive ? Mathf.Lerp(0.45f, 0.9f, markerPulse) : Mathf.Lerp(0.2f, 0.5f, markerPulse);
            marker.color = markerColor;
        }
    }

    private void HideConveyorVisuals()
    {
        if (conveyorVisualRoot != null && conveyorVisualRoot.activeSelf)
        {
            conveyorVisualRoot.SetActive(false);
        }
    }

    private void DestroyConveyorVisuals()
    {
        if (conveyorVisualRoot != null)
        {
            if (Application.isPlaying) Destroy(conveyorVisualRoot); else DestroyImmediate(conveyorVisualRoot);
            conveyorVisualRoot = null;
        }

        conveyorLaneRenderers.Clear();
        conveyorLaneMarkers.Clear();
        conveyorLaneCenters.Clear();
        conveyorLaneHalfWidths.Clear();
        conveyorLaneMarkerPhases.Clear();
        conveyorLaneDirections.Clear();
    }

    private void SpawnCargoTransit(float totalDuration)
    {
        ClearCargoTransit();
        if (!enableCargoTransit)
        {
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

        int min = Mathf.Max(1, Mathf.Min(cargoBlockCountMin, cargoBlockCountMax));
        int max = Mathf.Max(min, Mathf.Max(cargoBlockCountMin, cargoBlockCountMax));
        int count = Random.Range(min, max + 1);
        float activeTime = Mathf.Max(0.6f, totalDuration - Mathf.Max(0.1f, cargoTelegraphSeconds) - 0.25f);
        bool horizontalRoute = Random.value < 0.5f;
        float routeCenter = PickCargoRouteCenter(horizontalRoute);

        for (int i = 0; i < count; i++)
        {
            Vector2 size = RollCargoRouteSize(i, horizontalRoute);
            Vector2 position = PickCargoRoutePosition(size, i, count, horizontalRoute, routeCenter);
            GameObject cargoGo = new GameObject($"StorageCargoTransit_{i}");
            cargoGo.transform.SetParent(centerTransform != null ? centerTransform : transform, false);
            cargoGo.transform.position = new Vector3(position.x, position.y, 0f);

            StorageCargoBlockFx cargo = cargoGo.AddComponent<StorageCargoBlockFx>();
            cargo.Configure(size, cargoTelegraphSeconds, activeTime, cargoTelegraphColor, cargoActiveColor);
            cargoTransitBlocks.Add(cargo);
        }
    }

    private int GetLiveCargoTransitCount()
    {
        int count = 0;
        for (int i = 0; i < cargoTransitBlocks.Count; i++)
        {
            if (cargoTransitBlocks[i] != null)
            {
                count++;
            }
        }

        return count;
    }

    private Vector2 RollCargoRouteSize(int index, bool horizontalRoute)
    {
        float width = Random.Range(
            Mathf.Min(cargoSizeMin.x, cargoSizeMax.x),
            Mathf.Max(cargoSizeMin.x, cargoSizeMax.x));
        float height = Random.Range(
            Mathf.Min(cargoSizeMin.y, cargoSizeMax.y),
            Mathf.Max(cargoSizeMin.y, cargoSizeMax.y));

        if (!horizontalRoute)
        {
            float swap = width;
            width = height;
            height = swap;
        }

        return new Vector2(Mathf.Max(0.35f, width), Mathf.Max(0.35f, height));
    }

    private float PickCargoRouteCenter(bool horizontalRoute)
    {
        float min = horizontalRoute ? interiorBottom : interiorLeft;
        float max = horizontalRoute ? interiorTop : interiorRight;
        float margin = Mathf.Max(0.7f, cargoRouteMargin);
        float low = Mathf.Min(min + margin, max - margin);
        float high = Mathf.Max(min + margin, max - margin);
        float best = (low + high) * 0.5f;
        float bestScore = float.NegativeInfinity;

        for (int attempt = 0; attempt < 28; attempt++)
        {
            float candidate = Random.Range(low, high);
            float score = Random.value * 0.15f;
            if (playerController != null)
            {
                Vector2 playerPos = playerController.GetPosition();
                score += Mathf.Abs((horizontalRoute ? playerPos.y : playerPos.x) - candidate);
            }
            if (enemyController != null)
            {
                Vector2 enemyPos = enemyController.GetCurrentPosition();
                score += Mathf.Abs((horizontalRoute ? enemyPos.y : enemyPos.x) - candidate) * 0.65f;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    private Vector2 PickCargoRoutePosition(Vector2 size, int index, int count, bool horizontalRoute, float routeCenter)
    {
        float marginX = Mathf.Max(0.8f, size.x * 0.5f + 0.2f);
        float marginY = Mathf.Max(0.8f, size.y * 0.5f + 0.2f);
        float xMin = Mathf.Min(interiorLeft + marginX, interiorRight - marginX);
        float xMax = Mathf.Max(interiorLeft + marginX, interiorRight - marginX);
        float yMin = Mathf.Min(interiorBottom + marginY, interiorTop - marginY);
        float yMax = Mathf.Max(interiorBottom + marginY, interiorTop - marginY);
        float t = (index + 1f) / Mathf.Max(2f, count + 1f);
        float alongMin = horizontalRoute ? xMin : yMin;
        float alongMax = horizontalRoute ? xMax : yMax;
        float baseAlong = Mathf.Lerp(alongMin, alongMax, t);
        Vector2 fallback = horizontalRoute
            ? new Vector2(baseAlong, Mathf.Clamp(routeCenter, yMin, yMax))
            : new Vector2(Mathf.Clamp(routeCenter, xMin, xMax), baseAlong);

        for (int attempt = 0; attempt < 34; attempt++)
        {
            float alongJitter = attempt == 0 ? 0f : Random.Range(-Mathf.Abs(cargoRouteAlongJitter), Mathf.Abs(cargoRouteAlongJitter));
            float perpendicularJitter = attempt == 0 ? 0f : Random.Range(-Mathf.Abs(cargoRoutePerpendicularJitter), Mathf.Abs(cargoRoutePerpendicularJitter));
            Vector2 candidate = horizontalRoute
                ? new Vector2(Mathf.Clamp(baseAlong + alongJitter, xMin, xMax), Mathf.Clamp(routeCenter + perpendicularJitter, yMin, yMax))
                : new Vector2(Mathf.Clamp(routeCenter + perpendicularJitter, xMin, xMax), Mathf.Clamp(baseAlong + alongJitter, yMin, yMax));
            fallback = candidate;
            if (IsGoodCargoTransitPosition(candidate, size))
            {
                return candidate;
            }
        }

        return PickCargoTransitFallbackPosition(size, fallback);
    }

    private Vector2 PickCargoTransitFallbackPosition(Vector2 size, Vector2 preferred)
    {
        if (IsGoodCargoTransitPosition(preferred, size))
        {
            return preferred;
        }

        float marginX = Mathf.Max(0.8f, size.x * 0.5f + 0.2f);
        float marginY = Mathf.Max(0.8f, size.y * 0.5f + 0.2f);
        float xMin = Mathf.Min(interiorLeft + marginX, interiorRight - marginX);
        float xMax = Mathf.Max(interiorLeft + marginX, interiorRight - marginX);
        float yMin = Mathf.Min(interiorBottom + marginY, interiorTop - marginY);
        float yMax = Mathf.Max(interiorBottom + marginY, interiorTop - marginY);

        for (int attempt = 0; attempt < 48; attempt++)
        {
            Vector2 candidate = new Vector2(Random.Range(xMin, xMax), Random.Range(yMin, yMax));
            if (IsGoodCargoTransitPosition(candidate, size))
            {
                return candidate;
            }
        }

        return preferred;
    }

    private bool IsGoodCargoTransitPosition(Vector2 candidate, Vector2 size)
    {
        float avoid = Mathf.Max(0.8f, cargoAvoidActorRadius);
        if (playerController != null && Vector2.Distance(playerController.GetPosition(), candidate) < avoid)
        {
            return false;
        }
        if (enemyController != null && Vector2.Distance(enemyController.GetCurrentPosition(), candidate) < avoid)
        {
            return false;
        }

        Collider2D[] hits = Physics2D.OverlapBoxAll(candidate, size + Vector2.one * 0.24f, 0f);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.isTrigger)
            {
                continue;
            }

            if (hit.GetComponent<PlayerController>() != null || hit.GetComponent<EnemyController>() != null || hit.GetComponent<SplitAnomalyCloneController>() != null)
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private void ClearCargoTransit()
    {
        for (int i = cargoTransitBlocks.Count - 1; i >= 0; i--)
        {
            StorageCargoBlockFx block = cargoTransitBlocks[i];
            if (block != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(block.gameObject);
                }
                else
                {
                    DestroyImmediate(block.gameObject);
                }
            }
        }

        cargoTransitBlocks.Clear();
    }

    private void SpawnSignature(ThemedEventSignatureFx.SignatureKind kind, Color primary, Color secondary)
    {
        ClearSignature();
        GameObject signature = new GameObject($"StorageSignature_{kind}");
        signature.transform.SetParent(centerTransform != null ? centerTransform : transform, false);
        signatureFx = signature.AddComponent<ThemedEventSignatureFx>();
        signatureFx.Configure(kind, interiorLeft, interiorRight, interiorBottom, interiorTop, eventDuration, primary, secondary);
    }

    private void ClearSignature()
    {
        if (signatureFx == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(signatureFx.gameObject);
        }
        else
        {
            DestroyImmediate(signatureFx.gameObject);
        }

        signatureFx = null;
    }

    private static Vector2 PickPrimaryAxis()
    {
        switch (Random.Range(0, 2))
        {
            case 0:
                return Vector2.right;
            default:
                return Vector2.up;
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

    private void SchedulePressureRetry()
    {
        if (gameManager == null)
        {
            gameManager = FindAnyObjectByType<GameManager>();
        }

        nextEventTimer = gameManager != null ? gameManager.EventPressureRetryDelay : 1.25f;
    }

    private bool TryReserveEventPressure(float duration)
    {
        if (gameManager == null)
        {
            gameManager = FindAnyObjectByType<GameManager>();
        }

        return gameManager == null ||
               gameManager.TryReserveEventPressure(
                   EventPressureKey,
                   eventPressureCost,
                   Mathf.Max(0.1f, duration + eventPressureDurationPadding),
                   eventPressureCooldown);
    }

    private void ReleaseEventPressure()
    {
        if (gameManager != null)
        {
            gameManager.ReleaseEventPressure(EventPressureKey, eventPressureCooldown);
        }
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
}

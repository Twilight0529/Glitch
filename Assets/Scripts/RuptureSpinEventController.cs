using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class RuptureSpinEventController : MonoBehaviour, IThemedEventStatusProvider
{
    // Evento de Rupture: rota el campo de obstaculos alrededor del centro sin cruzar los limites.
    private enum RuptureEventVariant
    {
        None,
        SpinMotion,
        RiftEchoes,
        EchoPortals
    }

    private struct ObstacleBinding
    {
        public Transform transform;
        public Rigidbody2D rigidbody;
        public DynamicObstacleController dynamicController;
        public float obstacleRadius;
        public float spinMultiplier;
    }

    private struct ObstacleSnapshot
    {
        public ObstacleBinding binding;
        public Vector2 startOffset;
        public float startRotationZ;
        public bool dynamicWasEnabled;
        public SpriteRenderer[] renderers;
        public Color[] baseColors;
    }

    [Header("Event Timing")]
    [SerializeField] private float intervalMin = 8f;
    [SerializeField] private float intervalMax = 14f;
    [SerializeField] private float durationMin = 10f;
    [SerializeField] private float durationMax = 15f;
    [SerializeField, Range(0.4f, 1f)] private float cadenceIntervalMultiplier = 0.68f;
    [SerializeField, Range(0.8f, 1.8f)] private float cadenceDurationMultiplier = 1.32f;

    [Header("Spin Motion (2D)")]
    [SerializeField] private bool enableSpinMotion = true;
    [SerializeField] private float maxSweepAngle = 70f;
    [SerializeField] private float minSweepAngle = 3f;
    [SerializeField, Range(0.5f, 0.99f)] private float safeAngleFactor = 0.92f;
    [SerializeField] private float angleSampleStep = 0.75f;
    [SerializeField] private Vector2 spinMultiplierRange = new Vector2(0.8f, 1.25f);
    [SerializeField] private Vector2 angularSpeedRange = new Vector2(8f, 16f);
    [SerializeField] private Vector2 directionChangeIntervalRange = new Vector2(0.9f, 4.5f);
    [SerializeField] private float angularAcceleration = 22f;
    [SerializeField] private float angularDeceleration = 30f;
    [SerializeField, Range(0.5f, 0.95f)] private float boundaryTurnRatio = 0.82f;

    [Header("Bounds")]
    [SerializeField] private float boundsPadding = 0.08f;

    [Header("Visual Telegraph")]
    [SerializeField, Range(0f, 1f)] private float activeColorPulseStrength = 0.65f;
    [SerializeField, Range(0f, 1f)] private float activeColorLightenAmount = 0.38f;
    [SerializeField] private float activeColorPulseSpeed = 2.4f;

    [Header("Rupture Distinctive Event - Rift Echoes")]
    [SerializeField] private bool enableRiftEchoes = true;
    [SerializeField] private int riftEchoCountMin = 2;
    [SerializeField] private int riftEchoCountMax = 4;
    [SerializeField] private float riftEchoRadius = 1.35f;
    [SerializeField] private float riftEchoTelegraphSeconds = 0.85f;
    [SerializeField] private float riftEchoLinkDuration = 7.2f;
    [SerializeField] private float riftEchoLinkTriggerRadius = 0.42f;
    [SerializeField] private float riftEchoTrapStunDuration = 1.2f;
    [SerializeField] private float riftEchoAnchorFirewallReward = 2f;
    [SerializeField] private float riftEchoTrapFirewallReward = 11f;
    [SerializeField] private Color riftEchoTelegraphColor = new Color(0.95f, 0.42f, 1f, 0.42f);
    [SerializeField] private Color riftEchoActiveColor = new Color(0.42f, 0.96f, 1f, 0.76f);

    [Header("Rupture Distinctive Event - Echo Portals")]
    [SerializeField] private bool enableEchoPortals = true;
    [SerializeField] private float echoPortalRadius = 0.72f;
    [SerializeField] private float echoPortalCooldown = 0.85f;
    [SerializeField] private float echoTrapRadius = 1.05f;
    [SerializeField] private float echoTrapDuration = 1.35f;
    [SerializeField] private float echoTrapStunDuration = 1.15f;
    [SerializeField] private float echoFirewallReward = 8f;
    [SerializeField] private float echoSuccessLabelSeconds = 1.25f;
    [SerializeField] private Color echoPortalColor = new Color(0.48f, 0.96f, 1f, 0.9f);

    [Header("Event Pressure")]
    [SerializeField] private float eventPressureCost = 1f;
    [SerializeField] private float eventPressureCooldown = 4.5f;
    [SerializeField] private float eventPressureDurationPadding = 0.9f;

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
    private float currentAngleDeg;
    private float currentAngularSpeedDeg;
    private float targetAngularSpeedDeg;
    private float directionChangeTimer;
    private float safePositiveAngleDeg;
    private float safeNegativeAngleDeg;
    private bool initialized;
    private bool eventActive;
    private bool mapEventsWereUnlocked;
    private EnemyController enemyController;
    private GameManager gameManager;
    private PlayerController playerController;
    private readonly List<RuptureResonanceAnchor> riftEchoAnchors = new List<RuptureResonanceAnchor>();
    private readonly List<RuptureResonanceLink> riftEchoLinks = new List<RuptureResonanceLink>();
    private readonly List<RuptureEchoPortal> echoPortals = new List<RuptureEchoPortal>();
    private RuptureResonanceAnchor lastTriggeredRiftEcho;
    private float echoPortalCooldownTimer;
    private float echoSuccessTimer;
    private RuptureEventVariant currentVariant = RuptureEventVariant.None;
    private ThemedEventSignatureFx signatureFx;
    private const string EventPressureKey = "ThemeRuptureSpin";

    public string ActiveThemedEventLabel
    {
        get
        {
            if (echoSuccessTimer > 0f)
            {
                return "ECO ANCLADO";
            }

            if (GetLiveEchoPortalCount() > 1)
            {
                return "PORTALES DE ECO";
            }

            if (GetLiveRiftEchoCount() > 0)
            {
                return "CADENA DE ECOS";
            }

            if (!eventActive)
            {
                return string.Empty;
            }

            switch (currentVariant)
            {
                case RuptureEventVariant.RiftEchoes:
                    return "CADENA DE ECOS";
                case RuptureEventVariant.EchoPortals:
                    return "PORTALES DE ECO";
                default:
                    return "RUPTURA ROTATIVA";
            }
        }
    }

    public string ActiveThemedEventHint
    {
        get
        {
            if (echoSuccessTimer > 0f)
            {
                return "Eco activado: la anomalia quedo vulnerable";
            }

            if (GetLiveEchoPortalCount() > 1)
            {
                return "Cruza un portal y deja un eco para baitar a la anomalia";
            }

            if (GetLiveRiftEchoCount() > 0)
            {
                return "Toca dos ecos y cruza la anomalia por la linea";
            }

            if (!eventActive)
            {
                return string.Empty;
            }

            return currentVariant == RuptureEventVariant.SpinMotion
                ? "Los obstaculos rotan: lee el ritmo antes de cruzar"
                : string.Empty;
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
        currentAngleDeg = 0f;
        currentAngularSpeedDeg = 0f;
        targetAngularSpeedDeg = 0f;
        directionChangeTimer = 0f;
        safePositiveAngleDeg = 0f;
        safeNegativeAngleDeg = 0f;
        snapshots.Clear();
        ScheduleNextEvent();
    }

    private void OnDisable()
    {
        EndEvent(restoreControllers: true);
        ClearRiftEchoes();
        ClearEchoPortals();
    }

    private void Update()
    {
        if (echoPortalCooldownTimer > 0f)
        {
            echoPortalCooldownTimer -= Time.deltaTime;
        }
        if (echoSuccessTimer > 0f)
        {
            echoSuccessTimer -= Time.deltaTime;
        }

        if (!initialized || centerTransform == null)
        {
            return;
        }

        if (!IsMapEventsUnlocked())
        {
            mapEventsWereUnlocked = false;
            if (eventActive)
            {
                EndEvent(restoreControllers: true);
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
                EndEvent(restoreControllers: true);
                ScheduleNextEvent();
            }

            return;
        }

        if (!eventActive && WasDebugTriggerPressed())
        {
            BeginEvent(forceImmediate: true);
            return;
        }

        if (eventActive)
        {
            TickEvent();
            return;
        }

        nextEventTimer -= Time.deltaTime;
        if (nextEventTimer <= 0f)
        {
            BeginEvent(forceImmediate: false);
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

            ObstacleBinding binding = new ObstacleBinding
            {
                transform = target,
                rigidbody = target.GetComponent<Rigidbody2D>(),
                dynamicController = target.GetComponent<DynamicObstacleController>(),
                obstacleRadius = Mathf.Max(col.bounds.extents.x, col.bounds.extents.y),
                spinMultiplier = Random.Range(spinMultiplierRange.x, spinMultiplierRange.y)
            };

            obstacles.Add(binding);
        }
    }

    private RuptureEventVariant PickVariant()
    {
        List<RuptureEventVariant> variants = new List<RuptureEventVariant>();
        if (enableSpinMotion && obstacles.Count > 0)
        {
            variants.Add(RuptureEventVariant.SpinMotion);
        }
        if (enableRiftEchoes)
        {
            variants.Add(RuptureEventVariant.RiftEchoes);
        }
        if (enableEchoPortals)
        {
            variants.Add(RuptureEventVariant.EchoPortals);
        }

        if (variants.Count == 0)
        {
            return RuptureEventVariant.None;
        }

        return variants[Random.Range(0, variants.Count)];
    }

    private void BeginEvent(bool forceImmediate)
    {
        if (centerTransform == null)
        {
            ScheduleNextEvent();
            return;
        }

        BuildInteriorBounds();
        snapshots.Clear();
        currentVariant = PickVariant();
        if (currentVariant == RuptureEventVariant.None)
        {
            ScheduleNextEvent();
            return;
        }

        eventDuration = Random.Range(Mathf.Min(durationMin, durationMax), Mathf.Max(durationMin, durationMax));
        eventDuration *= Mathf.Max(0.1f, cadenceDurationMultiplier);
        if (!TryReserveEventPressure(eventDuration))
        {
            SchedulePressureRetry();
            return;
        }

        if (currentVariant == RuptureEventVariant.SpinMotion)
        {
            // Guarda offsets desde el centro para rotar todo el campo de obstaculos como una estructura.
            Vector2 center = centerTransform.position;
            for (int i = 0; i < obstacles.Count; i++)
            {
                ObstacleBinding binding = obstacles[i];
                if (binding.transform == null)
                {
                    continue;
                }

                bool dynamicWasEnabled = false;
                if (binding.dynamicController != null)
                {
                    dynamicWasEnabled = binding.dynamicController.enabled;
                    binding.dynamicController.enabled = false;
                }

                ObstacleSnapshot snapshot = new ObstacleSnapshot
                {
                    binding = binding,
                    startOffset = (Vector2)binding.transform.position - center,
                    startRotationZ = binding.transform.eulerAngles.z,
                    dynamicWasEnabled = dynamicWasEnabled,
                    renderers = binding.transform.GetComponentsInChildren<SpriteRenderer>(includeInactive: true),
                    baseColors = null
                };
                snapshot.baseColors = CaptureBaseColors(snapshot.renderers);

                snapshots.Add(snapshot);
            }

            if (snapshots.Count == 0)
            {
                ReleaseEventPressure();
                EndEvent(restoreControllers: true);
                currentVariant = RuptureEventVariant.None;
                ScheduleNextEvent();
                return;
            }

            // Limita los angulos antes de mover para evitar que los obstaculos crucen paredes.
            safePositiveAngleDeg = Mathf.Min(maxSweepAngle, ComputeSafeSweepAngle(1f) * safeAngleFactor);
            safeNegativeAngleDeg = Mathf.Min(maxSweepAngle, ComputeSafeSweepAngle(-1f) * safeAngleFactor);
            if (safePositiveAngleDeg < minSweepAngle && safeNegativeAngleDeg < minSweepAngle)
            {
                EndEvent(restoreControllers: true);
                ScheduleNextEvent();
                return;
            }
        }

        currentAngleDeg = 0f;
        currentAngularSpeedDeg = 0f;
        targetAngularSpeedDeg = 0f;
        directionChangeTimer = 0f;
        if (currentVariant == RuptureEventVariant.SpinMotion)
        {
            ChooseNextDirectionAndSpeed(forceDirectionChange: false);
            SpawnSignature(ThemedEventSignatureFx.SignatureKind.RuptureSpin, new Color(1f, 0.42f, 0.95f, 0.92f), new Color(0.42f, 0.96f, 1f, 0.86f));
        }
        else if (currentVariant == RuptureEventVariant.RiftEchoes)
        {
            SpawnRiftEchoes(eventDuration);
            SpawnSignature(ThemedEventSignatureFx.SignatureKind.RuptureEcho, riftEchoActiveColor, riftEchoTelegraphColor);
        }
        else if (currentVariant == RuptureEventVariant.EchoPortals)
        {
            SpawnEchoPortals(eventDuration);
            SpawnSignature(ThemedEventSignatureFx.SignatureKind.RupturePortal, echoPortalColor, riftEchoTelegraphColor);
        }

        eventTimer = 0f;
        eventActive = true;

        if (forceImmediate)
        {
            nextEventTimer = Mathf.Max(intervalMin * Mathf.Max(0.1f, cadenceIntervalMultiplier), 0.45f);
        }
    }

    private void TickEvent()
    {
        float dt = Time.deltaTime;
        eventTimer += dt;
        float progress = Mathf.Clamp01(eventTimer / Mathf.Max(0.0001f, eventDuration));

        if (currentVariant == RuptureEventVariant.SpinMotion)
        {
            directionChangeTimer -= dt;
            if (directionChangeTimer <= 0f)
            {
                ChooseNextDirectionAndSpeed(forceDirectionChange: true);
            }

            float gateIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.14f));
            float gateOut = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - progress) / 0.22f));
            float envelope = gateIn * gateOut;
            float targetSpeed = targetAngularSpeedDeg * envelope;

            // Aceleracion/desaceleracion hacen que la rotacion sea legible y no cambie de golpe.
            float response = Mathf.Abs(targetSpeed) > Mathf.Abs(currentAngularSpeedDeg) ? angularAcceleration : angularDeceleration;
            currentAngularSpeedDeg = Mathf.MoveTowards(currentAngularSpeedDeg, targetSpeed, response * dt);

            float candidateAngle = currentAngleDeg + currentAngularSpeedDeg * dt;
            float maxPositive = Mathf.Max(minSweepAngle, safePositiveAngleDeg);
            float maxNegative = Mathf.Max(minSweepAngle, safeNegativeAngleDeg);

            if (candidateAngle > maxPositive)
            {
                candidateAngle = maxPositive;
                currentAngularSpeedDeg = -Mathf.Abs(currentAngularSpeedDeg) * 0.35f;
                ChooseNextDirectionAndSpeed(forceDirectionChange: true, forcedSign: -1f);
            }
            else if (candidateAngle < -maxNegative)
            {
                candidateAngle = -maxNegative;
                currentAngularSpeedDeg = Mathf.Abs(currentAngularSpeedDeg) * 0.35f;
                ChooseNextDirectionAndSpeed(forceDirectionChange: true, forcedSign: 1f);
            }

            currentAngleDeg = candidateAngle;
            ApplyAngle(currentAngleDeg);
            ApplyActiveColorPulse(progress);
        }

        if (progress >= 1f)
        {
            EndEvent(restoreControllers: true);
            ScheduleNextEvent();
        }
    }

    private void ChooseNextDirectionAndSpeed(bool forceDirectionChange, float forcedSign = 0f)
    {
        float sign;
        if (forcedSign != 0f)
        {
            sign = Mathf.Sign(forcedSign);
        }
        else if (currentAngleDeg >= Mathf.Max(minSweepAngle, safePositiveAngleDeg) * boundaryTurnRatio)
        {
            sign = -1f;
        }
        else if (currentAngleDeg <= -Mathf.Max(minSweepAngle, safeNegativeAngleDeg) * boundaryTurnRatio)
        {
            sign = 1f;
        }
        else
        {
            float currentSign = Mathf.Sign(targetAngularSpeedDeg);
            if (currentSign == 0f)
            {
                currentSign = Random.value < 0.5f ? -1f : 1f;
            }

            if (forceDirectionChange && Random.value < 0.75f)
            {
                sign = -currentSign;
            }
            else
            {
                sign = Random.value < 0.5f ? -1f : 1f;
            }
        }

        float speed = Random.Range(Mathf.Min(angularSpeedRange.x, angularSpeedRange.y), Mathf.Max(angularSpeedRange.x, angularSpeedRange.y));
        targetAngularSpeedDeg = sign * speed;
        float minInterval = Mathf.Clamp(Mathf.Min(directionChangeIntervalRange.x, directionChangeIntervalRange.y), 0.1f, 5f);
        float maxInterval = Mathf.Clamp(Mathf.Max(directionChangeIntervalRange.x, directionChangeIntervalRange.y), minInterval, 5f);
        directionChangeTimer = Random.Range(minInterval, maxInterval);
    }

    private void ApplyAngle(float angleDeg)
    {
        if (centerTransform == null)
        {
            return;
        }

        Vector2 center = centerTransform.position;
        float angleRad = angleDeg * Mathf.Deg2Rad;
        float cos = Mathf.Cos(angleRad);
        float sin = Mathf.Sin(angleRad);

        for (int i = 0; i < snapshots.Count; i++)
        {
            ObstacleSnapshot snapshot = snapshots[i];
            ObstacleBinding binding = snapshot.binding;
            if (binding.transform == null)
            {
                continue;
            }

            Vector2 start = snapshot.startOffset;
            Vector2 rotated = new Vector2(
                start.x * cos - start.y * sin,
                start.x * sin + start.y * cos);

            Vector2 targetPosition = center + rotated;
            float margin = binding.obstacleRadius + boundsPadding;
            targetPosition.x = Mathf.Clamp(targetPosition.x, interiorLeft + margin, interiorRight - margin);
            targetPosition.y = Mathf.Clamp(targetPosition.y, interiorBottom + margin, interiorTop - margin);

            if (binding.rigidbody != null && binding.rigidbody.bodyType == RigidbodyType2D.Kinematic)
            {
                binding.rigidbody.MovePosition(targetPosition);
            }
            else
            {
                binding.transform.position = new Vector3(targetPosition.x, targetPosition.y, binding.transform.position.z);
            }

            float targetRotZ = snapshot.startRotationZ + angleDeg * binding.spinMultiplier;
            if (binding.rigidbody != null && binding.rigidbody.bodyType == RigidbodyType2D.Kinematic)
            {
                binding.rigidbody.MoveRotation(targetRotZ);
            }
            else
            {
                binding.transform.rotation = Quaternion.Euler(0f, 0f, targetRotZ);
            }
        }
    }

    private float ComputeSafeSweepAngle(float direction)
    {
        float dir = Mathf.Sign(direction);
        float step = Mathf.Max(0.2f, angleSampleStep);
        float safe = 0f;

        for (float absAngle = step; absAngle <= maxSweepAngle; absAngle += step)
        {
            float signedAngle = absAngle * dir;
            if (!IsAngleSafe(signedAngle))
            {
                break;
            }

            safe = absAngle;
        }

        float low = safe;
        float high = Mathf.Min(maxSweepAngle, safe + step);
        for (int i = 0; i < 8; i++)
        {
            float mid = (low + high) * 0.5f;
            if (IsAngleSafe(mid * dir))
            {
                low = mid;
            }
            else
            {
                high = mid;
            }
        }

        return low;
    }

    private bool IsAngleSafe(float angleDeg)
    {
        if (centerTransform == null)
        {
            return false;
        }

        Vector2 center = centerTransform.position;
        float angleRad = angleDeg * Mathf.Deg2Rad;
        float cos = Mathf.Cos(angleRad);
        float sin = Mathf.Sin(angleRad);

        for (int i = 0; i < snapshots.Count; i++)
        {
            ObstacleSnapshot snapshot = snapshots[i];
            ObstacleBinding binding = snapshot.binding;
            if (binding.transform == null)
            {
                continue;
            }

            Vector2 start = snapshot.startOffset;
            Vector2 rotated = new Vector2(
                start.x * cos - start.y * sin,
                start.x * sin + start.y * cos);

            Vector2 position = center + rotated;
            float margin = binding.obstacleRadius + boundsPadding;
            if (position.x < interiorLeft + margin ||
                position.x > interiorRight - margin ||
                position.y < interiorBottom + margin ||
                position.y > interiorTop - margin)
            {
                return false;
            }
        }

        return true;
    }

    private void EndEvent(bool restoreControllers)
    {
        if (!eventActive && snapshots.Count == 0 && riftEchoAnchors.Count == 0 && riftEchoLinks.Count == 0 && echoPortals.Count == 0)
        {
            return;
        }

        eventActive = false;
        eventTimer = 0f;
        currentAngleDeg = 0f;
        currentAngularSpeedDeg = 0f;
        targetAngularSpeedDeg = 0f;
        directionChangeTimer = 0f;
        safePositiveAngleDeg = 0f;
        safeNegativeAngleDeg = 0f;
        RestoreAllColors();
        ReleaseEventPressure();

        if (restoreControllers)
        {
            for (int i = 0; i < snapshots.Count; i++)
            {
                ObstacleSnapshot snapshot = snapshots[i];
                ObstacleBinding binding = snapshot.binding;
                if (binding.dynamicController == null)
                {
                    continue;
                }

                binding.dynamicController.enabled = snapshot.dynamicWasEnabled;
            }
        }

        snapshots.Clear();
        ClearRiftEchoes();
        ClearEchoPortals();
        ClearSignature();
        currentVariant = RuptureEventVariant.None;
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

    private void SpawnRiftEchoes(float totalDuration)
    {
        ClearRiftEchoes();
        if (!enableRiftEchoes)
        {
            return;
        }

        int min = Mathf.Max(1, Mathf.Min(riftEchoCountMin, riftEchoCountMax));
        int max = Mathf.Max(min, Mathf.Max(riftEchoCountMin, riftEchoCountMax));
        int count = Random.Range(min, max + 1);
        float radius = Mathf.Max(0.4f, riftEchoRadius);
        float lifeSeconds = Mathf.Max(1.2f, totalDuration - 0.2f);
        for (int i = 0; i < count; i++)
        {
            Vector2 position = PickRiftEchoPosition(radius, i, count);
            GameObject zone = new GameObject($"RuptureRiftEcho_{i}");
            zone.transform.SetParent(centerTransform != null ? centerTransform : transform, false);
            zone.transform.position = new Vector3(position.x, position.y, 0f);
            RuptureResonanceAnchor anchor = zone.AddComponent<RuptureResonanceAnchor>();
            anchor.Configure(
                this,
                i,
                radius,
                lifeSeconds,
                riftEchoTelegraphSeconds,
                riftEchoTelegraphColor,
                riftEchoActiveColor);
            riftEchoAnchors.Add(anchor);
        }
    }

    private Vector2 PickRiftEchoPosition(float radius, int index, int count)
    {
        Vector2 center = centerTransform != null ? (Vector2)centerTransform.position : Vector2.zero;
        float angle = ((Mathf.PI * 2f) * index) / Mathf.Max(1, count) + Random.Range(-0.45f, 0.45f);
        float arenaRadius = Mathf.Min(interiorRight - interiorLeft, interiorTop - interiorBottom) * 0.38f;
        float distance = Random.Range(Mathf.Max(radius + 0.4f, arenaRadius * 0.35f), Mathf.Max(radius + 0.8f, arenaRadius));
        Vector2 candidate = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
        float margin = Mathf.Max(0.7f, radius + boundsPadding);
        candidate.x = Mathf.Clamp(candidate.x, interiorLeft + margin, interiorRight - margin);
        candidate.y = Mathf.Clamp(candidate.y, interiorBottom + margin, interiorTop - margin);
        return candidate;
    }

    public void NotifyRiftEchoAnchorTriggered(RuptureResonanceAnchor anchor, PlayerController player)
    {
        if (!eventActive ||
            currentVariant != RuptureEventVariant.RiftEchoes ||
            anchor == null ||
            player == null)
        {
            return;
        }

        player.AddFirewallCharge(riftEchoAnchorFirewallReward);
        GlitchAudioManager.PlayRuptureFragmentMaterialize(anchor.transform.position);

        if (lastTriggeredRiftEcho != null && lastTriggeredRiftEcho != anchor)
        {
            CreateRiftEchoLink(lastTriggeredRiftEcho.Position, anchor.Position);
        }

        lastTriggeredRiftEcho = anchor;
    }

    public bool TryDetonateRiftEchoLink(Vector2 start, Vector2 end, float triggerRadius)
    {
        Vector2 midpoint = (start + end) * 0.5f;
        bool affected = false;

        if (enemyController == null)
        {
            enemyController = FindAnyObjectByType<EnemyController>();
        }

        if (enemyController != null &&
            DistancePointToSegment(enemyController.GetCurrentPosition(), start, end) <= triggerRadius)
        {
            enemyController.ApplyContainmentLock(midpoint, riftEchoTrapStunDuration);
            affected = true;
        }

        SplitAnomalyCloneController[] clones = FindObjectsByType<SplitAnomalyCloneController>(FindObjectsSortMode.None);
        for (int i = 0; i < clones.Length; i++)
        {
            SplitAnomalyCloneController clone = clones[i];
            if (clone == null)
            {
                continue;
            }

            if (DistancePointToSegment(clone.transform.position, start, end) <= triggerRadius)
            {
                clone.ApplyContainmentLock(riftEchoTrapStunDuration * 0.85f);
                affected = true;
            }
        }

        if (!affected)
        {
            return false;
        }

        if (playerController == null)
        {
            playerController = FindAnyObjectByType<PlayerController>();
        }

        playerController?.AddFirewallCharge(riftEchoTrapFirewallReward);
        FindAnyObjectByType<GameManager>()?.NotifyRuptureEchoTrapSuccess();
        echoSuccessTimer = Mathf.Max(echoSuccessTimer, Mathf.Max(0.25f, echoSuccessLabelSeconds));
        GlitchAudioManager.PlayRuptureRiftOpen(midpoint);
        return true;
    }

    private void CreateRiftEchoLink(Vector2 start, Vector2 end)
    {
        if ((end - start).sqrMagnitude < 0.25f)
        {
            return;
        }

        GameObject linkGo = new GameObject("RuptureResonanceLink");
        linkGo.transform.SetParent(centerTransform != null ? centerTransform : transform, false);
        RuptureResonanceLink link = linkGo.AddComponent<RuptureResonanceLink>();
        link.Configure(
            this,
            start,
            end,
            Mathf.Max(0.4f, riftEchoLinkDuration),
            Mathf.Max(0.08f, riftEchoLinkTriggerRadius),
            riftEchoActiveColor,
            riftEchoTelegraphColor);
        riftEchoLinks.Add(link);
    }

    private static float DistancePointToSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float lengthSq = ab.sqrMagnitude;
        if (lengthSq <= 0.0001f)
        {
            return Vector2.Distance(point, a);
        }

        float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / lengthSq);
        Vector2 closest = a + ab * t;
        return Vector2.Distance(point, closest);
    }

    private void ClearRiftEchoes()
    {
        lastTriggeredRiftEcho = null;
        for (int i = riftEchoLinks.Count - 1; i >= 0; i--)
        {
            RuptureResonanceLink link = riftEchoLinks[i];
            if (link != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(link.gameObject);
                }
                else
                {
                    DestroyImmediate(link.gameObject);
                }
            }
        }

        for (int i = riftEchoAnchors.Count - 1; i >= 0; i--)
        {
            RuptureResonanceAnchor anchor = riftEchoAnchors[i];
            if (anchor != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(anchor.gameObject);
                }
                else
                {
                    DestroyImmediate(anchor.gameObject);
                }
            }
        }

        riftEchoLinks.Clear();
        riftEchoAnchors.Clear();
    }

    private void SpawnEchoPortals(float totalDuration)
    {
        ClearEchoPortals();
        echoPortalCooldownTimer = 0f;
        if (!enableEchoPortals)
        {
            return;
        }

        if (playerController == null)
        {
            playerController = FindAnyObjectByType<PlayerController>();
        }

        float lifeSeconds = Mathf.Max(1.2f, totalDuration - 0.35f);
        Vector2 first;
        Vector2 second;
        PickEchoPortalPair(out first, out second);
        CreateEchoPortal(first, 0, lifeSeconds);
        CreateEchoPortal(second, 1, lifeSeconds);
    }

    private void CreateEchoPortal(Vector2 position, int index, float lifeSeconds)
    {
        GameObject portalGo = new GameObject($"RuptureEchoPortal_{index}");
        portalGo.transform.SetParent(centerTransform != null ? centerTransform : transform, false);
        portalGo.transform.position = new Vector3(position.x, position.y, 0f);
        RuptureEchoPortal portal = portalGo.AddComponent<RuptureEchoPortal>();
        portal.Configure(this, index, echoPortalRadius, lifeSeconds, echoPortalColor);
        echoPortals.Add(portal);
    }

    private void PickEchoPortalPair(out Vector2 first, out Vector2 second)
    {
        Vector2 center = centerTransform != null ? (Vector2)centerTransform.position : Vector2.zero;
        bool horizontal = Random.value < 0.5f;
        float margin = Mathf.Max(1.2f, echoPortalRadius + 0.8f);
        float jitterX = Random.Range(-2.4f, 2.4f);
        float jitterY = Random.Range(-2f, 2f);

        if (horizontal)
        {
            first = new Vector2(interiorLeft + margin, Mathf.Clamp(center.y + jitterY, interiorBottom + margin, interiorTop - margin));
            second = new Vector2(interiorRight - margin, Mathf.Clamp(center.y - jitterY, interiorBottom + margin, interiorTop - margin));
        }
        else
        {
            first = new Vector2(Mathf.Clamp(center.x + jitterX, interiorLeft + margin, interiorRight - margin), interiorBottom + margin);
            second = new Vector2(Mathf.Clamp(center.x - jitterX, interiorLeft + margin, interiorRight - margin), interiorTop - margin);
        }

        if (playerController != null && Vector2.Distance(playerController.GetPosition(), first) < Vector2.Distance(playerController.GetPosition(), second))
        {
            Vector2 swap = first;
            first = second;
            second = swap;
        }
    }

    public void NotifyRupturePortalEntered(RuptureEchoPortal portal, PlayerController player)
    {
        if (!eventActive || portal == null || player == null || echoPortalCooldownTimer > 0f)
        {
            return;
        }

        RuptureEchoPortal exitPortal = GetOtherEchoPortal(portal);
        if (exitPortal == null)
        {
            return;
        }

        Vector2 oldPosition = player.GetPosition();
        Vector2 exitPosition = ResolveEchoPortalExitPosition(exitPortal.Position);
        player.TeleportTo(exitPosition);
        player.AddFirewallCharge(echoFirewallReward);
        SpawnPlayerEchoTrap(oldPosition);
        echoPortalCooldownTimer = Mathf.Max(0.05f, echoPortalCooldown);
    }

    private RuptureEchoPortal GetOtherEchoPortal(RuptureEchoPortal portal)
    {
        for (int i = 0; i < echoPortals.Count; i++)
        {
            RuptureEchoPortal candidate = echoPortals[i];
            if (candidate != null && candidate != portal)
            {
                return candidate;
            }
        }

        return null;
    }

    private Vector2 ResolveEchoPortalExitPosition(Vector2 portalPosition)
    {
        Vector2 center = centerTransform != null ? (Vector2)centerTransform.position : Vector2.zero;
        Vector2 away = portalPosition - center;
        if (away.sqrMagnitude < 0.001f)
        {
            away = Vector2.right;
        }

        return ClampToInterior(portalPosition + away.normalized * Mathf.Max(0.25f, echoPortalRadius * 1.35f), 0.35f);
    }

    private void SpawnPlayerEchoTrap(Vector2 position)
    {
        GameObject echoGo = new GameObject("RupturePlayerEchoTrap");
        echoGo.transform.SetParent(centerTransform != null ? centerTransform : transform, false);
        echoGo.transform.position = new Vector3(position.x, position.y, 0f);
        RupturePlayerEchoFx echo = echoGo.AddComponent<RupturePlayerEchoFx>();
        echo.Configure(this, echoTrapRadius, echoTrapDuration, echoTrapStunDuration, echoPortalColor);
    }

    public void NotifyRuptureEchoTrapTriggered()
    {
        echoSuccessTimer = Mathf.Max(echoSuccessTimer, Mathf.Max(0.25f, echoSuccessLabelSeconds));
    }

    private Vector2 ClampToInterior(Vector2 position, float radius)
    {
        float margin = Mathf.Max(0f, radius + boundsPadding);
        position.x = Mathf.Clamp(position.x, interiorLeft + margin, interiorRight - margin);
        position.y = Mathf.Clamp(position.y, interiorBottom + margin, interiorTop - margin);
        return position;
    }

    private int GetLiveEchoPortalCount()
    {
        int count = 0;
        for (int i = 0; i < echoPortals.Count; i++)
        {
            if (echoPortals[i] != null)
            {
                count++;
            }
        }

        return count;
    }

    private int GetLiveRiftEchoCount()
    {
        int count = 0;
        for (int i = 0; i < riftEchoAnchors.Count; i++)
        {
            if (riftEchoAnchors[i] != null)
            {
                count++;
            }
        }

        for (int i = 0; i < riftEchoLinks.Count; i++)
        {
            if (riftEchoLinks[i] != null)
            {
                count++;
            }
        }

        return count;
    }

    private void ClearEchoPortals()
    {
        for (int i = echoPortals.Count - 1; i >= 0; i--)
        {
            RuptureEchoPortal portal = echoPortals[i];
            if (portal != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(portal.gameObject);
                }
                else
                {
                    DestroyImmediate(portal.gameObject);
                }
            }
        }

        echoPortals.Clear();
    }

    private void SpawnSignature(ThemedEventSignatureFx.SignatureKind kind, Color primary, Color secondary)
    {
        ClearSignature();
        GameObject signature = new GameObject($"RuptureSignature_{kind}");
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

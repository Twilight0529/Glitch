using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class RuptureSpinEventController : MonoBehaviour
{
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
    [SerializeField] private float durationMin = 7f;
    [SerializeField] private float durationMax = 11f;

    [Header("Spin Motion (2D)")]
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
    private EnemyController enemyController;

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
    }

    private void Update()
    {
        if (!initialized || centerTransform == null)
        {
            return;
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

    private void BeginEvent(bool forceImmediate)
    {
        if (obstacles.Count == 0 || centerTransform == null)
        {
            ScheduleNextEvent();
            return;
        }

        BuildInteriorBounds();
        snapshots.Clear();

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
            EndEvent(restoreControllers: true);
            ScheduleNextEvent();
            return;
        }

        safePositiveAngleDeg = Mathf.Min(maxSweepAngle, ComputeSafeSweepAngle(1f) * safeAngleFactor);
        safeNegativeAngleDeg = Mathf.Min(maxSweepAngle, ComputeSafeSweepAngle(-1f) * safeAngleFactor);
        if (safePositiveAngleDeg < minSweepAngle && safeNegativeAngleDeg < minSweepAngle)
        {
            EndEvent(restoreControllers: true);
            ScheduleNextEvent();
            return;
        }

        currentAngleDeg = 0f;
        currentAngularSpeedDeg = 0f;
        targetAngularSpeedDeg = 0f;
        directionChangeTimer = 0f;
        eventDuration = Random.Range(Mathf.Min(durationMin, durationMax), Mathf.Max(durationMin, durationMax));
        ChooseNextDirectionAndSpeed(forceDirectionChange: false);
        eventTimer = 0f;
        eventActive = true;

        if (forceImmediate)
        {
            nextEventTimer = Mathf.Max(intervalMin, 0.5f);
        }
    }

    private void TickEvent()
    {
        float dt = Time.deltaTime;
        eventTimer += dt;
        float progress = Mathf.Clamp01(eventTimer / Mathf.Max(0.0001f, eventDuration));

        directionChangeTimer -= dt;
        if (directionChangeTimer <= 0f)
        {
            ChooseNextDirectionAndSpeed(forceDirectionChange: true);
        }

        float gateIn = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress / 0.14f));
        float gateOut = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - progress) / 0.22f));
        float envelope = gateIn * gateOut;
        float targetSpeed = targetAngularSpeedDeg * envelope;
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
        if (!eventActive && snapshots.Count == 0)
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
        nextEventTimer = Random.Range(min, max);
    }

    private bool IsMapEventSuppressed()
    {
        if (enemyController == null)
        {
            enemyController = FindAnyObjectByType<EnemyController>();
        }

        return enemyController != null && enemyController.IsMapEventSuppressed();
    }
}

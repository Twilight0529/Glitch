using System.Collections.Generic;
using UnityEngine;

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
    }

    [Header("Event Timing")]
    [SerializeField] private float intervalMin = 8f;
    [SerializeField] private float intervalMax = 14f;
    [SerializeField] private float durationMin = 3f;
    [SerializeField] private float durationMax = 5f;

    [Header("Spin Burst (2D)")]
    [SerializeField] private float maxSweepAngle = 70f;
    [SerializeField] private float minSweepAngle = 3f;
    [SerializeField, Range(0.5f, 0.99f)] private float safeAngleFactor = 0.92f;
    [SerializeField] private float angleSampleStep = 0.75f;
    [SerializeField] private Vector2 spinMultiplierRange = new Vector2(0.8f, 1.25f);

    [Header("Bounds")]
    [SerializeField] private float boundsPadding = 0.08f;

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
    private float plannedAngleDeg;
    private bool initialized;
    private bool eventActive;

    public void Configure(Transform center, Transform staticObstaclesRoot, Transform dynamicObstaclesRoot)
    {
        centerTransform = center != null ? center : transform;
        BuildInteriorBounds();
        RebuildObstacleList(staticObstaclesRoot, dynamicObstaclesRoot);

        initialized = true;
        eventActive = false;
        eventTimer = 0f;
        plannedAngleDeg = 0f;
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

        if (eventActive)
        {
            TickEvent();
            return;
        }

        nextEventTimer -= Time.deltaTime;
        if (nextEventTimer <= 0f)
        {
            BeginEvent();
        }
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

    private void BeginEvent()
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
                dynamicWasEnabled = dynamicWasEnabled
            };

            snapshots.Add(snapshot);
        }

        if (snapshots.Count == 0)
        {
            EndEvent(restoreControllers: true);
            ScheduleNextEvent();
            return;
        }

        float direction = Random.value < 0.5f ? -1f : 1f;
        float safeAngle = ComputeSafeSweepAngle(direction);
        float targetAbsAngle = Mathf.Min(maxSweepAngle, safeAngle * safeAngleFactor);

        targetAbsAngle = Mathf.Max(minSweepAngle, targetAbsAngle);

        plannedAngleDeg = targetAbsAngle * direction;
        eventDuration = Random.Range(Mathf.Min(durationMin, durationMax), Mathf.Max(durationMin, durationMax));
        eventTimer = 0f;
        eventActive = true;
    }

    private void TickEvent()
    {
        eventTimer += Time.deltaTime;
        float progress = Mathf.Clamp01(eventTimer / Mathf.Max(0.0001f, eventDuration));

        // 2D pulse: rotate out and come back, so the layout does not drift between events.
        float upDown = 1f - Mathf.Abs(progress * 2f - 1f); // 0 -> 1 -> 0
        float eased = upDown * upDown * (3f - 2f * upDown);
        float angleDeg = plannedAngleDeg * eased;

        ApplyAngle(angleDeg);

        if (progress >= 1f)
        {
            EndEvent(restoreControllers: true);
            ScheduleNextEvent();
        }
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
        plannedAngleDeg = 0f;

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
}

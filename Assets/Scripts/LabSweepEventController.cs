using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class LabSweepEventController : MonoBehaviour
{
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
    }

    [Header("Event Timing")]
    [SerializeField] private float intervalMin = 9f;
    [SerializeField] private float intervalMax = 15f;
    [SerializeField] private float durationMin = 5.5f;
    [SerializeField] private float durationMax = 8.5f;

    [Header("Lab Sweep")]
    [SerializeField] private float laneHeight = 2.1f;
    [SerializeField] private Vector2 laneOffsetRange = new Vector2(0.55f, 1.2f);
    [SerializeField] private Vector2 rotationOffsetRange = new Vector2(3f, 11f);
    [SerializeField] private int minCycles = 2;
    [SerializeField] private int maxCycles = 4;
    [SerializeField, Range(0.35f, 0.95f)] private float envelopeFactor = 0.75f;
    [SerializeField] private float boundsPadding = 0.08f;

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
        if (obstacles.Count == 0 || centerTransform == null)
        {
            ScheduleNextEvent();
            return;
        }

        BuildInteriorBounds();
        snapshots.Clear();
        moveAlongX = Random.value < 0.5f;
        eventDuration = Random.Range(Mathf.Min(durationMin, durationMax), Mathf.Max(durationMin, durationMax));
        eventCycles = Random.Range(Mathf.Min(minCycles, maxCycles), Mathf.Max(minCycles, maxCycles) + 1);

        Vector2 center = centerTransform.position;
        float laneStep = Mathf.Max(0.8f, laneHeight);
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

            Vector2 start = binding.transform.position;
            float axisCoord = moveAlongX ? start.y - center.y : start.x - center.x;
            int laneIndex = Mathf.FloorToInt(axisCoord / laneStep);
            float laneSign = (laneIndex & 1) == 0 ? 1f : -1f;
            float offsetMag = Random.Range(Mathf.Min(laneOffsetRange.x, laneOffsetRange.y), Mathf.Max(laneOffsetRange.x, laneOffsetRange.y));
            float rotMag = Random.Range(Mathf.Min(rotationOffsetRange.x, rotationOffsetRange.y), Mathf.Max(rotationOffsetRange.x, rotationOffsetRange.y));

            snapshots.Add(new ObstacleSnapshot
            {
                binding = binding,
                startPosition = start,
                startRotationZ = binding.transform.eulerAngles.z,
                dynamicWasEnabled = dynamicWasEnabled,
                laneOffset = laneSign * offsetMag,
                rotationOffset = laneSign * rotMag
            });
        }

        if (snapshots.Count == 0)
        {
            EndEvent(restoreControllers: true, snapBackToStart: true);
            ScheduleNextEvent();
            return;
        }

        eventTimer = 0f;
        eventActive = true;
    }

    private void TickEvent()
    {
        float dt = Time.deltaTime;
        eventTimer += dt;
        float progress = Mathf.Clamp01(eventTimer / Mathf.Max(0.0001f, eventDuration));

        float swing = Mathf.Sin(progress * Mathf.PI * eventCycles);
        float envelope = Mathf.Sin(progress * Mathf.PI);
        envelope = Mathf.Lerp(1f, envelope, Mathf.Clamp01(envelopeFactor));
        float phaseValue = swing * envelope;

        Vector2 axis = moveAlongX ? Vector2.right : Vector2.up;
        for (int i = 0; i < snapshots.Count; i++)
        {
            ObstacleSnapshot snapshot = snapshots[i];
            ObstacleBinding binding = snapshot.binding;
            if (binding.transform == null)
            {
                continue;
            }

            Vector2 target = snapshot.startPosition + axis * (snapshot.laneOffset * phaseValue);
            target = ClampToInterior(target, binding.obstacleRadius);
            MoveTransform(binding, target);

            float rot = snapshot.startRotationZ + snapshot.rotationOffset * phaseValue;
            RotateTransform(binding, rot);
        }

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

        if (snapBackToStart)
        {
            for (int i = 0; i < snapshots.Count; i++)
            {
                ObstacleSnapshot snapshot = snapshots[i];
                ObstacleBinding binding = snapshot.binding;
                if (binding.transform == null)
                {
                    continue;
                }

                MoveTransform(binding, snapshot.startPosition);
                RotateTransform(binding, snapshot.startRotationZ);
            }
        }

        if (restoreControllers)
        {
            for (int i = 0; i < snapshots.Count; i++)
            {
                ObstacleSnapshot snapshot = snapshots[i];
                if (snapshot.binding.dynamicController != null)
                {
                    snapshot.binding.dynamicController.enabled = snapshot.dynamicWasEnabled;
                }
            }
        }

        snapshots.Clear();
        eventActive = false;
        eventTimer = 0f;
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
        nextEventTimer = Random.Range(min, max);
    }
}

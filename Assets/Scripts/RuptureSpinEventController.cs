using System.Collections.Generic;
using UnityEngine;

public class RuptureSpinEventController : MonoBehaviour
{
    private struct ObstacleBinding
    {
        public Transform transform;
        public DynamicObstacleController dynamicController;
        public float orbitMultiplier;
        public float spinMultiplier;
    }

    private struct ObstacleSnapshot
    {
        public Vector2 initialOffset;
        public float initialRotationZ;
        public bool dynamicWasEnabled;
    }

    [Header("Event Timing")]
    [SerializeField] private float intervalMin = 8f;
    [SerializeField] private float intervalMax = 14f;
    [SerializeField] private float durationMin = 3f;
    [SerializeField] private float durationMax = 5f;

    [Header("Spin Motion")]
    [SerializeField] private float angularSpeedMin = 20f;
    [SerializeField] private float angularSpeedMax = 38f;
    [SerializeField] private Vector2 orbitMultiplierRange = new Vector2(0.9f, 1.15f);
    [SerializeField] private Vector2 spinMultiplierRange = new Vector2(0.75f, 1.35f);

    private Transform centerTransform;
    private readonly List<ObstacleBinding> obstacles = new List<ObstacleBinding>();
    private readonly Dictionary<Transform, ObstacleSnapshot> snapshots = new Dictionary<Transform, ObstacleSnapshot>();
    private readonly HashSet<Transform> dedupe = new HashSet<Transform>();

    private float nextEventTimer;
    private float eventTimer;
    private float activeDuration;
    private float globalAngularSpeed;
    private bool initialized;
    private bool eventActive;

    public void Configure(Transform center, Transform staticObstaclesRoot, Transform dynamicObstaclesRoot)
    {
        centerTransform = center != null ? center : transform;
        RebuildObstacleList(staticObstaclesRoot, dynamicObstaclesRoot);
        initialized = true;
        eventActive = false;
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
            TickActiveEvent();
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

            DynamicObstacleController dynamicController = target.GetComponent<DynamicObstacleController>();
            ObstacleBinding binding = new ObstacleBinding
            {
                transform = target,
                dynamicController = dynamicController,
                orbitMultiplier = Random.Range(orbitMultiplierRange.x, orbitMultiplierRange.y),
                spinMultiplier = Random.Range(spinMultiplierRange.x, spinMultiplierRange.y)
            };

            obstacles.Add(binding);
        }
    }

    private void BeginEvent()
    {
        if (obstacles.Count == 0)
        {
            ScheduleNextEvent();
            return;
        }

        eventActive = true;
        eventTimer = 0f;
        activeDuration = Random.Range(Mathf.Min(durationMin, durationMax), Mathf.Max(durationMin, durationMax));
        globalAngularSpeed = Random.Range(Mathf.Min(angularSpeedMin, angularSpeedMax), Mathf.Max(angularSpeedMin, angularSpeedMax));
        if (Random.value < 0.5f)
        {
            globalAngularSpeed = -globalAngularSpeed;
        }

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

            snapshots[binding.transform] = new ObstacleSnapshot
            {
                initialOffset = (Vector2)binding.transform.position - center,
                initialRotationZ = binding.transform.eulerAngles.z,
                dynamicWasEnabled = dynamicWasEnabled
            };
        }
    }

    private void TickActiveEvent()
    {
        eventTimer += Time.deltaTime;
        Vector2 center = centerTransform.position;

        for (int i = 0; i < obstacles.Count; i++)
        {
            ObstacleBinding binding = obstacles[i];
            if (binding.transform == null)
            {
                continue;
            }

            if (!snapshots.TryGetValue(binding.transform, out ObstacleSnapshot snapshot))
            {
                continue;
            }

            float angle = globalAngularSpeed * eventTimer * binding.orbitMultiplier;
            Vector2 offset = Rotate(snapshot.initialOffset, angle);
            Vector2 targetPosition = center + offset;
            binding.transform.position = new Vector3(targetPosition.x, targetPosition.y, binding.transform.position.z);

            float targetRotZ = snapshot.initialRotationZ + angle * binding.spinMultiplier;
            binding.transform.rotation = Quaternion.Euler(0f, 0f, targetRotZ);
        }

        if (eventTimer >= activeDuration)
        {
            EndEvent(restoreControllers: true);
            ScheduleNextEvent();
        }
    }

    private void EndEvent(bool restoreControllers)
    {
        if (!eventActive && snapshots.Count == 0)
        {
            return;
        }

        eventActive = false;

        if (restoreControllers)
        {
            for (int i = 0; i < obstacles.Count; i++)
            {
                ObstacleBinding binding = obstacles[i];
                if (binding.transform == null || binding.dynamicController == null)
                {
                    continue;
                }

                if (snapshots.TryGetValue(binding.transform, out ObstacleSnapshot snapshot))
                {
                    binding.dynamicController.enabled = snapshot.dynamicWasEnabled;
                }
            }
        }

        snapshots.Clear();
    }

    private void ScheduleNextEvent()
    {
        float min = Mathf.Min(intervalMin, intervalMax);
        float max = Mathf.Max(intervalMin, intervalMax);
        nextEventTimer = Random.Range(min, max);
    }

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(r);
        float s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }
}

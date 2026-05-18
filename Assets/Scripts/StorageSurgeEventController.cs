using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class StorageSurgeEventController : MonoBehaviour
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

    [Header("Visual Telegraph")]
    [SerializeField, Range(0f, 1f)] private float activeColorPulseStrength = 0.62f;
    [SerializeField, Range(0f, 1f)] private float activeColorLightenAmount = 0.36f;
    [SerializeField] private float activeColorPulseSpeed = 2.1f;

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
        if (obstacles.Count == 0 || centerTransform == null)
        {
            ScheduleNextEvent();
            return;
        }

        BuildInteriorBounds();
        snapshots.Clear();
        surgeAxis = PickPrimaryAxis();
        lateralAxis = new Vector2(-surgeAxis.y, surgeAxis.x);
        eventDuration = Random.Range(Mathf.Min(durationMin, durationMax), Mathf.Max(durationMin, durationMax));
        eventDuration *= Mathf.Max(0.1f, cadenceDurationMultiplier);
        baseDisplacement = Random.Range(Mathf.Min(displacementRange.x, displacementRange.y), Mathf.Max(displacementRange.x, displacementRange.y));
        baseRotation = Random.Range(Mathf.Min(rotationRange.x, rotationRange.y), Mathf.Max(rotationRange.x, rotationRange.y));

        float laneStep = Mathf.Max(1f, laneHeight);
        bool flipAllLanes = Random.value < Mathf.Clamp01(laneFlipChance);

        float minProj = float.PositiveInfinity;
        float maxProj = float.NegativeInfinity;
        for (int i = 0; i < obstacles.Count; i++)
        {
            Transform tr = obstacles[i].transform;
            if (tr == null)
            {
                continue;
            }

            float proj = Vector2.Dot((Vector2)tr.position, surgeAxis);
            if (proj < minProj)
            {
                minProj = proj;
            }

            if (proj > maxProj)
            {
                maxProj = proj;
            }
        }

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

            float proj = Vector2.Dot((Vector2)binding.transform.position, surgeAxis);
            float normalized = Mathf.InverseLerp(minProj, maxProj, proj);
            float delay = normalized * Mathf.Max(0f, waveStaggerSeconds);
            float laneCoord = Vector2.Dot((Vector2)binding.transform.position, lateralAxis);
            int laneIndex = Mathf.FloorToInt(laneCoord / laneStep);
            float laneSign = (laneIndex & 1) == 0 ? 1f : -1f;
            if (flipAllLanes)
            {
                laneSign *= -1f;
            }

            snapshots.Add(new ObstacleSnapshot
            {
                binding = binding,
                startPosition = binding.transform.position,
                startRotationZ = binding.transform.eulerAngles.z,
                dynamicWasEnabled = dynamicWasEnabled,
                displacementScale = Random.Range(0.78f, 1.18f),
                phaseDelay = delay,
                laneSign = laneSign,
                lateralOffset = Random.Range(-Mathf.Abs(lateralOffsetMax), Mathf.Abs(lateralOffsetMax)),
                renderers = binding.transform.GetComponentsInChildren<SpriteRenderer>(includeInactive: true),
                baseColors = null
            });

            int last = snapshots.Count - 1;
            ObstacleSnapshot created = snapshots[last];
            created.baseColors = CaptureBaseColors(created.renderers);
            snapshots[last] = created;
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
        float eventProgress = Mathf.Clamp01(eventTimer / Mathf.Max(0.0001f, eventDuration));

        float activeWindow = Mathf.Max(0.3f, eventDuration - waveStaggerSeconds);
        for (int i = 0; i < snapshots.Count; i++)
        {
            ObstacleSnapshot snapshot = snapshots[i];
            ObstacleBinding binding = snapshot.binding;
            if (binding.transform == null)
            {
                continue;
            }

            float localT = Mathf.Clamp01((eventTimer - snapshot.phaseDelay) / activeWindow);
            float primary = Mathf.Sin(localT * Mathf.PI);
            float recoil = Mathf.Sin(localT * Mathf.PI * 2f) * Mathf.Exp(-3.6f * localT) * recoilStrength;
            float envelope = Mathf.Sin(eventProgress * Mathf.PI);
            float displacement = (primary + recoil) * envelope * baseDisplacement * snapshot.displacementScale * snapshot.laneSign;

            float lateralWave = Mathf.Sin((localT * Mathf.PI * 2f) + (snapshot.phaseDelay * 3f)) * 0.5f;
            float lateralDisplacement = lateralWave * snapshot.lateralOffset * envelope;
            Vector2 target = snapshot.startPosition + (surgeAxis * displacement) + (lateralAxis * lateralDisplacement);
            target = ClampToInterior(target, binding.obstacleRadius);
            MoveTransform(binding, target);

            float rot = snapshot.startRotationZ + displacement * baseRotation * 0.48f;
            RotateTransform(binding, rot);
        }
        ApplyActiveColorPulse(eventProgress);

        if (eventProgress >= 1f)
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

    private bool IsMapEventSuppressed()
    {
        if (enemyController == null)
        {
            enemyController = FindAnyObjectByType<EnemyController>();
        }

        return enemyController != null && enemyController.IsMapEventSuppressed();
    }
}

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class ProceduralArenaGenerator : MonoBehaviour
{
    public enum ArenaTheme
    {
        ContainmentLab,
        StorageBay,
        RuptureZone
    }

    private struct ThemePalette
    {
        public Color wall;
        public Color obstacleBase;
        public Color obstacleAccent;
        public Color detail;
        public Color dynamic;
    }

    [Header("Arena")]
    [SerializeField] private float arenaWidth = 32f;
    [SerializeField] private float arenaHeight = 18f;
    [SerializeField] private float wallThickness = 1f;

    [Header("Flow Rules")]
    [SerializeField] private float primaryLaneWidth = 3.4f;
    [SerializeField] private float secondaryLaneWidth = 2.6f;

    [Header("Static Obstacles")]
    [SerializeField] private int minObstacles = 10;
    [SerializeField] private int maxObstacles = 16;
    [SerializeField] private Vector2 obstacleSizeMin = new Vector2(1.2f, 1.2f);
    [SerializeField] private Vector2 obstacleSizeMax = new Vector2(3.1f, 2.4f);
    [SerializeField] private float edgeClearance = 0.7f;
    [SerializeField] private float obstacleGap = 0.45f;
    [SerializeField] private int placementAttemptsPerObstacle = 36;

    [Header("Dynamic Obstacles")]
    [SerializeField] private int minDynamicObstacles = 1;
    [SerializeField] private int maxDynamicObstacles = 3;
    [SerializeField] private float dynamicObstacleGap = 0.5f;
    [SerializeField] private Vector2 sliderDistanceRange = new Vector2(0.9f, 1.8f);
    [SerializeField] private Vector2 sliderSpeedRange = new Vector2(0.7f, 1.35f);
    [SerializeField] private Vector2 pulseScaleRange = new Vector2(0.72f, 1.28f);
    [SerializeField] private Vector2 pulseSpeedRange = new Vector2(0.9f, 1.8f);

    [Header("Spawn Safety")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform anomalyTransform;
    [SerializeField] private float spawnSafeRadius = 2.2f;

    [Header("Theme")]
    [SerializeField] private bool randomizeThemeEachRun = true;
    [SerializeField] private ArenaTheme fixedTheme = ArenaTheme.ContainmentLab;

    [Header("Rupture Pattern")]
    [SerializeField] private int minRuptureAnchors = 4;
    [SerializeField] private int maxRuptureAnchors = 7;
    [SerializeField] private int ruptureRingsPerAnchor = 3;
    [SerializeField] private int ruptureSlotsPerRing = 12;
    [SerializeField] private int rupturePlacementsPerAnchorBurst = 4;
    [SerializeField] private float ruptureRingRadiusMin = 1.2f;
    [SerializeField] private float ruptureRingRadiusMax = 4.8f;
    [SerializeField] private float ruptureAnchorPadding = 1.8f;
    [SerializeField] private float ruptureAnchorMinSeparation = 4.8f;
    [SerializeField] private float ruptureMinCenterSpacing = 1.65f;
    [SerializeField] private float ruptureCoreDiameter = 1.55f;

    [Header("Rupture Dynamic Orbit")]
    [SerializeField] private int ruptureDynamicMin = 4;
    [SerializeField] private int ruptureDynamicMax = 7;
    [SerializeField] private float ruptureOrbitInnerPadding = 1.9f;
    [SerializeField] private float ruptureOrbitRadiusGap = 1.35f;
    [SerializeField] private Vector2 ruptureOrbitObstacleSizeMin = new Vector2(1.0f, 0.55f);
    [SerializeField] private Vector2 ruptureOrbitObstacleSizeMax = new Vector2(2.2f, 0.95f);
    [SerializeField] private Vector2 ruptureOrbitAngularSpeedRange = new Vector2(18f, 55f);
    [SerializeField] private Vector2 ruptureOrbitRadialAmplitudeRange = new Vector2(0.12f, 0.55f);
    [SerializeField] private Vector2 ruptureOrbitRadialSpeedRange = new Vector2(0.55f, 1.35f);
    [SerializeField] private int ruptureOrbitPathSamples = 32;

    [Header("Random")]
    [SerializeField] private bool randomizeSeedEachRun = true;
    [SerializeField] private int fixedSeed = 12345;
    [SerializeField] private bool generateOnAwake = true;

    private const string BoundsRootName = "Bounds";
    private const string ObstaclesRootName = "Obstacles";
    private const string DynamicRootName = "DynamicObstacles";
    private const string DetailsRootName = "Details";

    private System.Random rng;
    private ArenaTheme activeTheme;
    private ThemePalette palette;

    private readonly List<Rect> blockedAreas = new List<Rect>();
    private readonly List<Rect> placedObstacleRects = new List<Rect>();
    private readonly List<Rect> reservedLanes = new List<Rect>();
    private readonly List<Vector2> ruptureAnchors = new List<Vector2>();
    private readonly List<List<Vector2>> ruptureAnchorSlots = new List<List<Vector2>>();
    private readonly List<int> ruptureAnchorSlotCursor = new List<int>();
    private readonly List<float> ruptureOrbitRadii = new List<float>();
    private int ruptureAnchorCursor;
    private int ruptureActiveAnchorIndex = -1;
    private int ruptureAnchorBurstCounter;
    private float ruptureOuterRadius;
    private float ruptureOuterExtentX = 1f;
    private float ruptureOuterExtentY = 1f;

    public float ArenaWidth => arenaWidth;
    public float ArenaHeight => arenaHeight;
    public ArenaTheme ActiveTheme => activeTheme;
    public string ActiveThemeLabel => ToThemeLabel(activeTheme);

    private void Awake()
    {
        if (!generateOnAwake)
        {
            return;
        }

        GenerateNow();
    }

    [ContextMenu("Generate Arena Now")]
    public void GenerateNow()
    {
        InitializeRandom();
        activeTheme = SelectTheme();
        palette = GetPalette(activeTheme);

        ClearGeneratedGeometry();
        blockedAreas.Clear();
        placedObstacleRects.Clear();
        reservedLanes.Clear();
        ruptureAnchors.Clear();
        ruptureAnchorSlots.Clear();
        ruptureAnchorSlotCursor.Clear();
        ruptureOrbitRadii.Clear();
        ruptureAnchorCursor = 0;
        ruptureActiveAnchorIndex = -1;
        ruptureAnchorBurstCounter = 0;
        ruptureOuterRadius = 1f;
        ruptureOuterExtentX = 1f;
        ruptureOuterExtentY = 1f;

        BuildReservedLanes();
        if (activeTheme == ArenaTheme.RuptureZone)
        {
            BuildRuptureAnchors();
        }

        Transform boundsRoot = new GameObject(BoundsRootName).transform;
        boundsRoot.SetParent(transform, false);

        Transform obstaclesRoot = new GameObject(ObstaclesRootName).transform;
        obstaclesRoot.SetParent(transform, false);

        Transform dynamicRoot = new GameObject(DynamicRootName).transform;
        dynamicRoot.SetParent(transform, false);

        Transform detailsRoot = new GameObject(DetailsRootName).transform;
        detailsRoot.SetParent(transform, false);

        CreateArenaBounds(boundsRoot);
        CreateThemedObstacles(obstaclesRoot);
        CreateThemedDynamicObstacles(dynamicRoot);
        CreateThemedDetails(detailsRoot);
        ConfigureRuptureMapEvent(obstaclesRoot, dynamicRoot);
    }

    public void SetRuntimeReferences(Transform player, Transform anomaly)
    {
        playerTransform = player;
        anomalyTransform = anomaly;
    }

    private static string ToThemeLabel(ArenaTheme theme)
    {
        switch (theme)
        {
            case ArenaTheme.ContainmentLab:
                return "Lab";
            case ArenaTheme.StorageBay:
                return "Storage";
            default:
                return "Rupture";
        }
    }

    private void InitializeRandom()
    {
        int seed = randomizeSeedEachRun ? unchecked(Environment.TickCount ^ DateTime.UtcNow.Millisecond) : fixedSeed;
        rng = new System.Random(seed);
    }

    private ArenaTheme SelectTheme()
    {
        if (!randomizeThemeEachRun)
        {
            return fixedTheme;
        }

        int count = Enum.GetValues(typeof(ArenaTheme)).Length;
        return (ArenaTheme)rng.Next(0, count);
    }

    private static ThemePalette GetPalette(ArenaTheme theme)
    {
        switch (theme)
        {
            case ArenaTheme.ContainmentLab:
                return new ThemePalette
                {
                    wall = new Color(0.12f, 0.13f, 0.17f),
                    obstacleBase = new Color(0.28f, 0.32f, 0.40f),
                    obstacleAccent = new Color(0.36f, 0.43f, 0.53f),
                    detail = new Color(0.54f, 0.66f, 0.82f, 0.22f),
                    dynamic = new Color(0.70f, 0.84f, 1f, 0.9f)
                };
            case ArenaTheme.StorageBay:
                return new ThemePalette
                {
                    wall = new Color(0.15f, 0.12f, 0.10f),
                    obstacleBase = new Color(0.36f, 0.30f, 0.23f),
                    obstacleAccent = new Color(0.52f, 0.43f, 0.30f),
                    detail = new Color(0.78f, 0.63f, 0.42f, 0.22f),
                    dynamic = new Color(0.93f, 0.76f, 0.43f, 0.9f)
                };
            default:
                return new ThemePalette
                {
                    wall = new Color(0.10f, 0.11f, 0.14f),
                    obstacleBase = new Color(0.30f, 0.24f, 0.34f),
                    obstacleAccent = new Color(0.48f, 0.34f, 0.53f),
                    detail = new Color(0.86f, 0.44f, 0.70f, 0.20f),
                    dynamic = new Color(1f, 0.56f, 0.78f, 0.9f)
                };
        }
    }

    private void ClearGeneratedGeometry()
    {
        DestroyGeneratedChild(BoundsRootName);
        DestroyGeneratedChild(ObstaclesRootName);
        DestroyGeneratedChild(DynamicRootName);
        DestroyGeneratedChild(DetailsRootName);
    }

    private void DestroyGeneratedChild(string childName)
    {
        Transform child = transform.Find(childName);
        if (child != null)
        {
            DestroyByPlayState(child.gameObject);
        }
    }

    private static void DestroyByPlayState(GameObject target)
    {
        if (Application.isPlaying)
        {
            Destroy(target);
            return;
        }

        DestroyImmediate(target);
    }

    private static void DestroyComponentByPlayState(Component component)
    {
        if (component == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(component);
            return;
        }

        DestroyImmediate(component);
    }

    private void ConfigureRuptureMapEvent(Transform obstaclesRoot, Transform dynamicRoot)
    {
        Type controllerType = FindTypeInLoadedAssemblies("RuptureSpinEventController");
        if (controllerType == null)
        {
            return;
        }

        Component controller = GetComponent(controllerType);
        if (activeTheme != ArenaTheme.RuptureZone)
        {
            if (controller != null)
            {
                DestroyComponentByPlayState(controller);
            }

            return;
        }

        if (controller == null)
        {
            controller = gameObject.AddComponent(controllerType);
        }

        MethodInfo configureMethod = controllerType.GetMethod("Configure", BindingFlags.Instance | BindingFlags.Public);
        if (configureMethod != null)
        {
            configureMethod.Invoke(controller, new object[] { transform, obstaclesRoot, dynamicRoot });
        }
    }

    private static Type FindTypeInLoadedAssemblies(string typeName)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            Type[] types;
            try
            {
                types = assemblies[i].GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types;
            }

            if (types == null)
            {
                continue;
            }

            for (int t = 0; t < types.Length; t++)
            {
                Type type = types[t];
                if (type != null && type.Name == typeName)
                {
                    return type;
                }
            }
        }

        return null;
    }

    private void BuildReservedLanes()
    {
        float innerWidth = arenaWidth - edgeClearance * 2f;
        float innerHeight = arenaHeight - edgeClearance * 2f;

        switch (activeTheme)
        {
            case ArenaTheme.ContainmentLab:
                reservedLanes.Add(new Rect(-innerWidth * 0.5f, -primaryLaneWidth * 0.5f, innerWidth, primaryLaneWidth));
                reservedLanes.Add(new Rect(-secondaryLaneWidth * 0.5f, -innerHeight * 0.5f, secondaryLaneWidth, innerHeight));
                break;

            case ArenaTheme.StorageBay:
                reservedLanes.Add(new Rect(-innerWidth * 0.5f, -primaryLaneWidth * 0.5f, innerWidth, primaryLaneWidth));
                reservedLanes.Add(new Rect(-secondaryLaneWidth * 0.5f, -innerHeight * 0.5f, secondaryLaneWidth, innerHeight * 0.65f));
                break;

            case ArenaTheme.RuptureZone:
                break;
        }
    }

    private void BuildRuptureAnchors()
    {
        // Rupture is a single bullseye: one center, concentric rings.
        ruptureAnchors.Add(Vector2.zero);
        BuildRuptureSlots();
    }

    private List<Vector2> BuildRuptureCoverageCandidates()
    {
        List<Vector2> candidates = new List<Vector2>();
        float margin = edgeClearance + 0.6f;
        float minX = -arenaWidth * 0.5f + margin;
        float maxX = arenaWidth * 0.5f - margin;
        float minY = -arenaHeight * 0.5f + margin;
        float maxY = arenaHeight * 0.5f - margin;

        for (int a = 0; a < ruptureAnchorSlots.Count; a++)
        {
            List<Vector2> slots = ruptureAnchorSlots[a];
            for (int i = 0; i < slots.Count; i++)
            {
                Vector2 p = slots[i];
                p.x = Mathf.Clamp(p.x, minX, maxX);
                p.y = Mathf.Clamp(p.y, minY, maxY);
                candidates.Add(p);

                if (i % 2 == 0)
                {
                    Vector2 jittered = p + RandomInsideUnitCircle() * 0.35f;
                    jittered.x = Mathf.Clamp(jittered.x, minX, maxX);
                    jittered.y = Mathf.Clamp(jittered.y, minY, maxY);
                    candidates.Add(jittered);
                }
            }
        }

        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = Range(0, i + 1);
            Vector2 tmp = candidates[i];
            candidates[i] = candidates[j];
            candidates[j] = tmp;
        }

        return candidates;
    }

    private void BuildRuptureSlots()
    {
        ruptureAnchorSlots.Clear();
        ruptureAnchorSlotCursor.Clear();
        ruptureAnchorCursor = 0;

        float margin = edgeClearance + 0.6f;
        float minX = -arenaWidth * 0.5f + margin;
        float maxX = arenaWidth * 0.5f - margin;
        float minY = -arenaHeight * 0.5f + margin;
        float maxY = arenaHeight * 0.5f - margin;

        for (int i = 0; i < ruptureAnchors.Count; i++)
        {
            Vector2 anchor = ruptureAnchors[i];
            List<Vector2> slots = new List<Vector2>();
            float extentX = Mathf.Min(anchor.x - minX, maxX - anchor.x) - 0.2f;
            float extentY = Mathf.Min(anchor.y - minY, maxY - anchor.y) - 0.2f;
            extentX = Mathf.Max(ruptureRingRadiusMin + 0.1f, extentX);
            extentY = Mathf.Max(ruptureRingRadiusMin + 0.1f, extentY);

            ruptureOuterExtentX = Mathf.Max(ruptureOuterExtentX, extentX);
            ruptureOuterExtentY = Mathf.Max(ruptureOuterExtentY, extentY);
            ruptureOuterRadius = Mathf.Max(ruptureOuterRadius, Mathf.Min(extentX, extentY));

            float maxAxisExtent = Mathf.Max(extentX, extentY);
            int ringCount = Mathf.Max(Mathf.Max(8, ruptureRingsPerAnchor), Mathf.CeilToInt(maxAxisExtent / 1.05f));
            float basePhase = Range(0f, 360f);

            for (int ring = 0; ring < ringCount; ring++)
            {
                float t = ringCount == 1 ? 1f : (ring + 1f) / ringCount;
                float ringX = Mathf.Lerp(ruptureRingRadiusMin, extentX, t);
                float ringY = Mathf.Lerp(ruptureRingRadiusMin, extentY, t);
                int points = Mathf.Max(12, ruptureSlotsPerRing) + ring * 3;

                for (int p = 0; p < points; p++)
                {
                    float angleDeg = basePhase + (360f / points) * p;
                    Vector2 dir = new Vector2(Mathf.Cos(angleDeg * Mathf.Deg2Rad), Mathf.Sin(angleDeg * Mathf.Deg2Rad));
                    Vector2 candidate = anchor + new Vector2(dir.x * ringX, dir.y * ringY);

                    if (candidate.x < minX || candidate.x > maxX || candidate.y < minY || candidate.y > maxY)
                    {
                        continue;
                    }

                    slots.Add(candidate);
                }
            }

            if (slots.Count > 0)
            {
                ruptureAnchorSlots.Add(slots);
                ruptureAnchorSlotCursor.Add(0);
            }
        }
    }

    private Vector2 GetRuptureCircularPosition()
    {
        if (ruptureAnchorSlots.Count == 0)
        {
            return GetRandomRupturePosition();
        }

        int anchorCount = ruptureAnchorSlots.Count;
        if (ruptureAnchorCursor >= anchorCount)
        {
            ruptureAnchorCursor = 0;
        }

        bool needsNewAnchor =
            ruptureActiveAnchorIndex < 0 ||
            ruptureActiveAnchorIndex >= anchorCount ||
            ruptureAnchorBurstCounter <= 0 ||
            ruptureAnchorSlots[ruptureActiveAnchorIndex].Count == 0;

        if (needsNewAnchor)
        {
            int guard = 0;
            ruptureActiveAnchorIndex = -1;
            while (guard < anchorCount)
            {
                int anchorIndex = ruptureAnchorCursor;
                ruptureAnchorCursor = (ruptureAnchorCursor + 1) % anchorCount;
                guard++;

                if (ruptureAnchorSlots[anchorIndex].Count == 0)
                {
                    continue;
                }

                ruptureActiveAnchorIndex = anchorIndex;
                ruptureAnchorBurstCounter = Mathf.Max(1, rupturePlacementsPerAnchorBurst);
                break;
            }
        }

        if (ruptureActiveAnchorIndex >= 0)
        {
            List<Vector2> slots = ruptureAnchorSlots[ruptureActiveAnchorIndex];
            if (slots.Count > 0)
            {
                int cursor = ruptureAnchorSlotCursor[ruptureActiveAnchorIndex];
                if (cursor >= slots.Count)
                {
                    cursor = 0;
                }

                Vector2 candidate = slots[cursor];
                ruptureAnchorSlotCursor[ruptureActiveAnchorIndex] = cursor + 1;
                ruptureAnchorBurstCounter--;

                float clampMargin = edgeClearance + 0.6f;
                candidate.x = Mathf.Clamp(candidate.x, -arenaWidth * 0.5f + clampMargin, arenaWidth * 0.5f - clampMargin);
                candidate.y = Mathf.Clamp(candidate.y, -arenaHeight * 0.5f + clampMargin, arenaHeight * 0.5f - clampMargin);
                return candidate;
            }

            ruptureAnchorBurstCounter = 0;
        }

        // Fallback if something unexpected happens.
        Vector2 fallback = GetRandomRupturePosition();

        float margin = edgeClearance + 0.6f;
        fallback.x = Mathf.Clamp(fallback.x, -arenaWidth * 0.5f + margin, arenaWidth * 0.5f - margin);
        fallback.y = Mathf.Clamp(fallback.y, -arenaHeight * 0.5f + margin, arenaHeight * 0.5f - margin);
        return fallback;
    }

    private float GetRuptureFlowAngle(Vector2 position)
    {
        if (ruptureAnchors.Count == 0)
        {
            return Range(0f, 360f);
        }

        Vector2 nearest = ruptureAnchors[0];
        float nearestDist = Vector2.SqrMagnitude(position - nearest);

        for (int i = 1; i < ruptureAnchors.Count; i++)
        {
            float d = Vector2.SqrMagnitude(position - ruptureAnchors[i]);
            if (d < nearestDist)
            {
                nearestDist = d;
                nearest = ruptureAnchors[i];
            }
        }

        Vector2 radial = (position - nearest).normalized;
        if (radial.sqrMagnitude < 0.0001f)
        {
            radial = Vector2.right;
        }

        Vector2 tangent = new Vector2(-radial.y, radial.x);
        return Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;
    }

    private float GetRuptureRingT(Vector2 position)
    {
        float ex = Mathf.Max(0.001f, ruptureOuterExtentX);
        float ey = Mathf.Max(0.001f, ruptureOuterExtentY);
        float nx = position.x / ex;
        float ny = position.y / ey;
        return Mathf.Clamp01(Mathf.Sqrt(nx * nx + ny * ny));
    }

    private static bool HasMinCenterSpacing(Vector2 candidate, List<Vector2> centers, float minSpacing)
    {
        float sqr = minSpacing * minSpacing;
        for (int i = 0; i < centers.Count; i++)
        {
            if ((centers[i] - candidate).sqrMagnitude < sqr)
            {
                return false;
            }
        }

        return true;
    }

    private void CreateArenaBounds(Transform parent)
    {
        CreateWall("Wall_Top", new Vector2(0f, arenaHeight * 0.5f), new Vector2(arenaWidth + wallThickness * 2f, wallThickness), parent);
        CreateWall("Wall_Bottom", new Vector2(0f, -arenaHeight * 0.5f), new Vector2(arenaWidth + wallThickness * 2f, wallThickness), parent);
        CreateWall("Wall_Left", new Vector2(-arenaWidth * 0.5f, 0f), new Vector2(wallThickness, arenaHeight), parent);
        CreateWall("Wall_Right", new Vector2(arenaWidth * 0.5f, 0f), new Vector2(wallThickness, arenaHeight), parent);
    }

    private void CreateThemedObstacles(Transform parent)
    {
        switch (activeTheme)
        {
            case ArenaTheme.ContainmentLab:
                GenerateContainmentLab(parent);
                break;
            case ArenaTheme.StorageBay:
                GenerateStorageBay(parent);
                break;
            case ArenaTheme.RuptureZone:
                GenerateRuptureZone(parent);
                break;
        }
    }

    private void GenerateContainmentLab(Transform parent)
    {
        int target = Range(minObstacles, maxObstacles + 1);
        int placed = 0;
        int serial = 0;
        int attempts = 0;
        int maxAttempts = target * placementAttemptsPerObstacle;

        while (placed < target && attempts < maxAttempts)
        {
            attempts++;

            float shapeRoll = Range(0f, 1f);
            bool horizontal = Range(0f, 1f) < 0.5f;
            bool success;

            if (shapeRoll < 0.62f)
            {
                Vector2 size = horizontal
                    ? new Vector2(Range(3.2f, 5.8f), Range(0.8f, 1.35f))
                    : new Vector2(Range(0.8f, 1.35f), Range(3.2f, 5.8f));

                Vector2 center = GetRandomInsideArena(size);
                if ((horizontal && Mathf.Abs(center.y) < primaryLaneWidth * 0.65f) || (!horizontal && Mathf.Abs(center.x) < secondaryLaneWidth * 0.65f))
                {
                    continue;
                }

                success = TryPlaceRectangleObstacle($"Obstacle_Lab_{serial}", center, size, 0f, parent);
            }
            else if (shapeRoll < 0.84f)
            {
                Vector2 size = horizontal
                    ? new Vector2(Range(2.0f, 4.2f), Range(0.85f, 1.4f))
                    : new Vector2(Range(0.85f, 1.4f), Range(2.0f, 4.2f));

                Vector2 center = GetRandomInsideArena(size);
                success = TryPlaceCapsuleObstacle($"Obstacle_LabCapsule_{serial}", center, size, !horizontal, 0f, parent);
            }
            else
            {
                float diameter = Range(1.0f, 1.8f);
                Vector2 center = GetRandomInsideArena(new Vector2(diameter, diameter));
                success = TryPlaceDiamondObstacle($"Obstacle_LabDiamond_{serial}", center, diameter, parent);
            }

            if (success)
            {
                placed++;
                serial++;
            }
        }

        int pillars = Mathf.Max(2, target / 4);
        for (int i = 0; i < pillars; i++)
        {
            Vector2 center = new Vector2(
                Range(-arenaWidth * 0.35f, arenaWidth * 0.35f),
                Range(-arenaHeight * 0.35f, arenaHeight * 0.35f));

            float radius = Range(0.45f, 0.9f);
            TryPlacePillarObstacle($"Obstacle_LabPillar_{i}", center, radius, parent);
        }
    }

    private void GenerateStorageBay(Transform parent)
    {
        int clusters = 4;
        int target = Range(minObstacles, maxObstacles + 1);
        int placed = 0;
        int serial = 0;

        for (int c = 0; c < clusters; c++)
        {
            if (placed >= target)
            {
                break;
            }

            Vector2 clusterCenter = GetQuadrantCenter(c);
            int clusterItems = Range(2, 5);

            for (int i = 0; i < clusterItems; i++)
            {
                if (placed >= target)
                {
                    break;
                }

                Vector2 offset = RandomInsideUnitCircle() * Range(0.4f, 2.0f);
                Vector2 center = clusterCenter + offset;

                float shapeRoll = Range(0f, 1f);
                bool success;

                if (shapeRoll < 0.48f)
                {
                    Vector2 size = new Vector2(Range(1.4f, 2.8f), Range(1.2f, 2.4f));
                    success = TryPlaceRectangleObstacle($"Obstacle_Storage_{serial}", center, size, 0f, parent);
                }
                else if (shapeRoll < 0.68f)
                {
                    Vector2 size = new Vector2(Range(1.5f, 2.9f), Range(1.0f, 1.4f));
                    success = TryPlaceCapsuleObstacle($"Obstacle_StorageCapsule_{serial}", center, size, false, 0f, parent);
                }
                else if (shapeRoll < 0.83f)
                {
                    success = TryPlacePillarObstacle($"Obstacle_StoragePillar_{serial}", center, Range(0.5f, 1.0f), parent);
                }
                else if (shapeRoll < 0.93f)
                {
                    success = TryPlaceDiamondObstacle($"Obstacle_StorageDiamond_{serial}", center, Range(1.1f, 1.9f), parent);
                }
                else
                {
                    success = TryPlaceLObstacle($"Obstacle_StorageL_{serial}", center, Range(2.2f, 3.6f), Range(2.0f, 3.2f), Range(0.7f, 1.0f), Range(0, 4), parent);
                }

                if (success)
                {
                    placed++;
                    serial++;
                }
            }
        }

        int fallbackAttempts = target * 2;
        for (int i = 0; i < fallbackAttempts && placed < target; i++)
        {
            Vector2 size = new Vector2(Range(1.2f, 2.5f), Range(1.2f, 2.3f));
            Vector2 center = GetRandomInsideArena(size);

            if (TryPlaceRectangleObstacle($"Obstacle_StorageFallback_{serial}", center, size, 0f, parent))
            {
                placed++;
                serial++;
            }
        }
    }

    private void GenerateRuptureZone(Transform parent)
    {
        int totalSlots = 0;
        for (int i = 0; i < ruptureAnchorSlots.Count; i++)
        {
            totalSlots += ruptureAnchorSlots[i].Count;
        }

        int areaBoost = Mathf.RoundToInt((arenaWidth * arenaHeight) / 140f);
        int anchorBoost = ruptureAnchors.Count * 2;
        int desiredMin = minObstacles + 16 + areaBoost + anchorBoost;
        int desiredMax = maxObstacles + 28 + areaBoost + anchorBoost;
        int target = Range(desiredMin, desiredMax + 1);
        int slotCap = Mathf.Max(desiredMin, Mathf.RoundToInt(totalSlots * 0.90f));
        target = Mathf.Min(target, slotCap);

        int placed = 0;
        int serial = 0;
        List<Vector2> placedCenters = new List<Vector2>();

        // Mandatory center piece for Rupture: always keep a circle at (0,0).
        if (CreateMandatoryRuptureCore(parent))
        {
            placed++;
            serial++;
            placedCenters.Add(Vector2.zero);
        }

        // Pass 1: cover ring slots (and near-ring jitter) to avoid large empty patches.
        List<Vector2> candidates = BuildRuptureCoverageCandidates();
        for (int i = 0; i < candidates.Count && placed < target; i++)
        {
            Vector2 center = candidates[i];
            float angle = GetRuptureFlowAngle(center);
            float ringT = GetRuptureRingT(center);
            float roll = Range(0f, 1f);
            bool success;

            if (!HasMinCenterSpacing(center, placedCenters, ruptureMinCenterSpacing))
            {
                continue;
            }

            if (ringT < 0.30f)
            {
                // Core: compact blockers.
                success = roll < 0.55f
                    ? TryPlacePillarObstacle($"Obstacle_RuptureCorePillar_{serial}", center, Range(0.45f, 0.76f), parent)
                    : TryPlaceDiamondObstacle($"Obstacle_RuptureCoreDiamond_{serial}", center, Range(0.85f, 1.25f), parent);
            }
            else if (ringT < 0.70f && roll < 0.45f)
            {
                Vector2 size = new Vector2(Range(1.15f, 2.0f), Range(0.55f, 0.88f));
                float rotation = angle + Range(-11f, 11f);
                success = TryPlaceRectangleObstacle($"Obstacle_Rupture_{serial}", center, size, rotation, parent);
            }
            else if (ringT < 0.85f && roll < 0.72f)
            {
                Vector2 size = new Vector2(Range(1.1f, 1.9f), Range(0.58f, 0.92f));
                float rotation = angle + Range(-9f, 9f);
                success = TryPlaceCapsuleObstacle($"Obstacle_RuptureCapsule_{serial}", center, size, false, rotation, parent);
            }
            else if (roll < 0.88f)
            {
                success = TryPlaceDiamondObstacle($"Obstacle_RuptureDiamond_{serial}", center, Range(0.9f, 1.45f), parent);
            }
            else
            {
                success = TryPlacePillarObstacle($"Obstacle_RupturePillar_{serial}", center, Range(0.42f, 0.72f), parent);
            }

            if (success)
            {
                placed++;
                serial++;
                placedCenters.Add(center);
            }
        }

        int attempts = 0;
        int maxAttempts = target * placementAttemptsPerObstacle * 2;

        while (placed < target && attempts < maxAttempts)
        {
            attempts++;

            float roll = Range(0f, 1f);
            Vector2 center = GetRuptureCircularPosition();
            float angle = GetRuptureFlowAngle(center);

            bool success;
            if (!HasMinCenterSpacing(center, placedCenters, ruptureMinCenterSpacing))
            {
                continue;
            }

            if (roll < 0.48f)
            {
                Vector2 size = new Vector2(Range(1.4f, 2.4f), Range(0.62f, 0.95f));
                float rotation = angle + Range(-10f, 10f);
                success = TryPlaceRectangleObstacle($"Obstacle_Rupture_{serial}", center, size, rotation, parent);
            }
            else if (roll < 0.70f)
            {
                Vector2 size = new Vector2(Range(1.25f, 2.1f), Range(0.66f, 1.0f));
                float rotation = angle + Range(-8f, 8f);
                success = TryPlaceCapsuleObstacle($"Obstacle_RuptureCapsule_{serial}", center, size, false, rotation, parent);
            }
            else if (roll < 0.84f)
            {
                success = TryPlacePillarObstacle($"Obstacle_RupturePillar_{serial}", center, Range(0.5f, 0.95f), parent);
            }
            else if (roll < 0.94f)
            {
                success = TryPlaceDiamondObstacle($"Obstacle_RuptureDiamond_{serial}", center, Range(1.0f, 1.8f), parent);
            }
            else
            {
                success = TryPlaceLObstacle($"Obstacle_RuptureL_{serial}", center, Range(1.6f, 2.7f), Range(1.5f, 2.6f), Range(0.55f, 0.82f), Range(0, 4), parent);
            }

            if (success)
            {
                placed++;
                serial++;
                placedCenters.Add(center);
            }
        }

        int fallbackAttempts = target * 3;
        for (int i = 0; i < fallbackAttempts && placed < target; i++)
        {
            Vector2 center = GetRuptureCircularPosition();
            bool success;

            if (!HasMinCenterSpacing(center, placedCenters, ruptureMinCenterSpacing))
            {
                continue;
            }

            if (Range(0f, 1f) < 0.5f)
            {
                success = TryPlacePillarObstacle($"Obstacle_RuptureFillPillar_{serial}", center, Range(0.45f, 0.78f), parent);
            }
            else
            {
                success = TryPlaceDiamondObstacle($"Obstacle_RuptureFillDiamond_{serial}", center, Range(0.9f, 1.4f), parent);
            }

            if (success)
            {
                placed++;
                serial++;
                placedCenters.Add(center);
            }
        }

    }

    private bool CreateMandatoryRuptureCore(Transform parent)
    {
        float diameter = Mathf.Max(0.8f, ruptureCoreDiameter);
        Vector2 center = Vector2.zero;
        Vector2 size = new Vector2(diameter, diameter);
        Color color = Color.Lerp(palette.obstacleBase, palette.obstacleAccent, 0.55f);

        GameObject go = CreateBlock("Obstacle_RuptureCore_Center", center, size, color, parent);
        SpriteRenderer renderer = go.GetComponent<SpriteRenderer>();
        renderer.sprite = CircleSpriteProvider.Get();
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.size = size;

        CircleCollider2D collider = go.AddComponent<CircleCollider2D>();
        collider.radius = diameter * 0.5f;

        RegisterFootprint(CenterRect(center, size));
        return true;
    }

    private void CreateThemedDynamicObstacles(Transform parent)
    {
        int target = Range(minDynamicObstacles, maxDynamicObstacles + 1);
        if (activeTheme == ArenaTheme.RuptureZone)
        {
            int ruptureTarget = Range(Mathf.Max(1, ruptureDynamicMin), Mathf.Max(ruptureDynamicMin + 1, ruptureDynamicMax + 1));
            target = Mathf.Max(target, ruptureTarget);
        }

        int placed = 0;
        int attempts = 0;
        int maxAttempts = target * placementAttemptsPerObstacle;

        while (placed < target && attempts < maxAttempts)
        {
            attempts++;
            bool success;

            switch (activeTheme)
            {
                case ArenaTheme.ContainmentLab:
                    success = TryCreateLabDynamic($"Dynamic_Lab_{placed}", parent);
                    break;
                case ArenaTheme.StorageBay:
                    success = TryCreateStorageDynamic($"Dynamic_Storage_{placed}", parent);
                    break;
                default:
                    success = TryCreateRuptureDynamic($"Dynamic_Rupture_{placed}", parent);
                    break;
            }

            if (success)
            {
                placed++;
            }
        }
    }

    private bool TryCreateLabDynamic(string name, Transform parent)
    {
        bool horizontal = Range(0f, 1f) < 0.5f;
        Vector2 size = horizontal ? new Vector2(Range(1.8f, 2.8f), Range(0.7f, 1.0f)) : new Vector2(Range(0.7f, 1.0f), Range(1.8f, 2.8f));

        Vector2 center = new Vector2(
            Range(-arenaWidth * 0.38f, arenaWidth * 0.38f),
            Range(-arenaHeight * 0.38f, arenaHeight * 0.38f));

        if (Mathf.Abs(center.y) < primaryLaneWidth * 0.55f)
        {
            float sign = center.y == 0f ? (Range(0f, 1f) < 0.5f ? -1f : 1f) : Mathf.Sign(center.y);
            center.y += sign * (primaryLaneWidth * 0.35f);
        }

        Vector2 axis = horizontal ? Vector2.up : Vector2.right;
        return TryPlaceDynamicSlider(name, center, size, axis, parent);
    }

    private bool TryCreateStorageDynamic(string name, Transform parent)
    {
        float roll = Range(0f, 1f);
        if (roll < 0.55f)
        {
            Vector2 center = GetQuadrantCenter(Range(0, 4)) + RandomInsideUnitCircle() * Range(0.5f, 1.8f);
            Vector2 size = new Vector2(Range(1.4f, 2.4f), Range(0.7f, 1.0f));
            Vector2 axis = Range(0f, 1f) < 0.5f ? Vector2.right : Vector2.up;
            return TryPlaceDynamicSlider(name, center, size, axis, parent);
        }

        Vector2 pulseCenter = GetQuadrantCenter(Range(0, 4)) + RandomInsideUnitCircle() * Range(0.2f, 1.6f);
        return TryPlaceDynamicPulse(name, pulseCenter, Range(0.8f, 1.2f), parent);
    }

    private bool TryCreateRuptureDynamic(string name, Transform parent)
    {
        if (TryPlaceDynamicRuptureOrbiter(name + "_Orbit", parent))
        {
            return true;
        }

        float roll = Range(0f, 1f);
        Vector2 center = GetRuptureCircularPosition();
        float angle = GetRuptureFlowAngle(center);

        if (roll < 0.55f)
        {
            Vector2 size = new Vector2(Range(1.8f, 2.9f), Range(0.65f, 1.0f));
            Vector2 axis = new Vector2(Mathf.Cos((angle + 90f) * Mathf.Deg2Rad), Mathf.Sin((angle + 90f) * Mathf.Deg2Rad)).normalized;
            return TryPlaceDynamicSlider(name, center, size, axis, parent);
        }

        return TryPlaceDynamicPulse(name, center, Range(0.9f, 1.4f), parent);
    }

    private bool TryPlaceDynamicRuptureOrbiter(string name, Transform parent)
    {
        Vector2 orbitCenter = transform.position;
        float edgeMargin = edgeClearance + 0.95f;
        float maxOrbitRadius = Mathf.Min(arenaWidth, arenaHeight) * 0.5f - edgeMargin;
        float minOrbitRadius = ruptureCoreDiameter * 0.5f + ruptureOrbitInnerPadding;

        if (maxOrbitRadius <= minOrbitRadius + 0.25f)
        {
            return false;
        }

        float selectedRadius = -1f;
        const int radiusAttempts = 20;
        for (int i = 0; i < radiusAttempts; i++)
        {
            float t = Mathf.Pow(Range(0f, 1f), 0.72f);
            float candidate = Mathf.Lerp(minOrbitRadius, maxOrbitRadius, t);

            bool hasGap = true;
            for (int j = 0; j < ruptureOrbitRadii.Count; j++)
            {
                if (Mathf.Abs(ruptureOrbitRadii[j] - candidate) < ruptureOrbitRadiusGap)
                {
                    hasGap = false;
                    break;
                }
            }

            if (hasGap)
            {
                selectedRadius = candidate;
                break;
            }
        }

        if (selectedRadius < 0f)
        {
            selectedRadius = Range(minOrbitRadius, maxOrbitRadius);
        }

        Vector2 size = new Vector2(
            Range(ruptureOrbitObstacleSizeMin.x, ruptureOrbitObstacleSizeMax.x),
            Range(ruptureOrbitObstacleSizeMin.y, ruptureOrbitObstacleSizeMax.y));
        float halfExtent = Mathf.Max(size.x, size.y) * 0.5f;

        float maxAmplitudeByBounds = Mathf.Max(0.04f, maxOrbitRadius - selectedRadius - halfExtent - 0.1f);
        float maxAmplitudeByCore = Mathf.Max(0.04f, selectedRadius - minOrbitRadius);
        float allowedAmplitude = Mathf.Min(maxAmplitudeByBounds, maxAmplitudeByCore, ruptureOrbitRadialAmplitudeRange.y);
        float radialAmplitude = Mathf.Clamp(ruptureOrbitRadialAmplitudeRange.x, 0.04f, allowedAmplitude);
        if (allowedAmplitude > radialAmplitude + 0.01f)
        {
            radialAmplitude = Range(radialAmplitude, allowedAmplitude);
        }

        float startAngleDeg = Range(0f, 360f);
        Vector2 dir = new Vector2(Mathf.Cos(startAngleDeg * Mathf.Deg2Rad), Mathf.Sin(startAngleDeg * Mathf.Deg2Rad));
        Vector2 spawnPosition = orbitCenter + dir * selectedRadius;
        Rect spawnFootprint = CenterRect(spawnPosition, size);

        if (!IsValidFootprint(spawnFootprint))
        {
            return false;
        }

        float minRadius = Mathf.Max(0.05f, selectedRadius - radialAmplitude);
        float maxRadius = selectedRadius + radialAmplitude;
        if (!IsOrbitPathClear(orbitCenter, minRadius, maxRadius, halfExtent))
        {
            return false;
        }

        GameObject go = CreateBlock(name, spawnPosition, size, palette.dynamic, parent);
        BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
        collider.size = size;
        ConfigureRuptureOrbitCollision(collider);

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.freezeRotation = true;

        float angularSpeed = Range(ruptureOrbitAngularSpeedRange.x, ruptureOrbitAngularSpeedRange.y);
        if (Range(0f, 1f) < 0.5f)
        {
            angularSpeed = -angularSpeed;
        }

        float radialSpeed = Range(ruptureOrbitRadialSpeedRange.x, ruptureOrbitRadialSpeedRange.y);
        float phase = Range(0f, Mathf.PI * 2f);
        DynamicObstacleController controller = go.AddComponent<DynamicObstacleController>();
        controller.ConfigureOrbitSpiral(
            orbitCenter,
            selectedRadius,
            angularSpeed,
            radialAmplitude,
            radialSpeed,
            phase,
            true);

        ruptureOrbitRadii.Add(selectedRadius);
        RegisterDynamicFootprint(spawnFootprint);
        return true;
    }

    private bool TryPlaceDynamicSlider(string name, Vector2 center, Vector2 size, Vector2 axis, Transform parent)
    {
        float distance = Range(sliderDistanceRange.x, sliderDistanceRange.y);
        Vector2 nAxis = axis.sqrMagnitude > 0.0001f ? axis.normalized : Vector2.right;

        Vector2 sweptSize = new Vector2(
            size.x + Mathf.Abs(nAxis.x) * distance * 2f,
            size.y + Mathf.Abs(nAxis.y) * distance * 2f);

        Rect footprint = CenterRect(center, sweptSize);
        if (!IsValidFootprint(footprint))
        {
            return false;
        }

        GameObject go = CreateBlock(name, center, size, palette.dynamic, parent);

        BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
        collider.size = size;

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.freezeRotation = true;

        DynamicObstacleController controller = go.AddComponent<DynamicObstacleController>();
        controller.ConfigureSlide(
            nAxis,
            distance,
            Range(sliderSpeedRange.x, sliderSpeedRange.y),
            Range(0f, Mathf.PI * 2f));

        RegisterDynamicFootprint(footprint);
        return true;
    }

    private bool TryPlaceDynamicPulse(string name, Vector2 center, float baseDiameter, Transform parent)
    {
        float maxScale = pulseScaleRange.y;
        float maxDiameter = baseDiameter * maxScale;
        Rect footprint = CenterRect(center, new Vector2(maxDiameter, maxDiameter));

        if (!IsValidFootprint(footprint))
        {
            return false;
        }

        GameObject go = CreateBlock(name, center, new Vector2(baseDiameter, baseDiameter), palette.dynamic, parent);

        CircleCollider2D collider = go.AddComponent<CircleCollider2D>();
        collider.radius = baseDiameter * 0.5f;

        DynamicObstacleController controller = go.AddComponent<DynamicObstacleController>();
        controller.ConfigurePulse(
            pulseScaleRange.x,
            pulseScaleRange.y,
            Range(pulseSpeedRange.x, pulseSpeedRange.y),
            Range(0f, Mathf.PI * 2f));

        RegisterDynamicFootprint(footprint);
        return true;
    }

    private Vector2 GetQuadrantCenter(int quadrant)
    {
        float x = (quadrant % 2 == 0 ? -1f : 1f) * Range(arenaWidth * 0.18f, arenaWidth * 0.32f);
        float y = (quadrant < 2 ? 1f : -1f) * Range(arenaHeight * 0.16f, arenaHeight * 0.30f);
        return new Vector2(x, y);
    }

    private bool TryPlaceRectangleObstacle(string name, Vector2 center, Vector2 size, float rotationDeg, Transform parent)
    {
        Rect footprint = GetAabb(center, size, rotationDeg);
        if (!IsValidFootprint(footprint))
        {
            return false;
        }

        Color color = Color.Lerp(palette.obstacleBase, palette.obstacleAccent, Range(0.1f, 0.9f));
        GameObject go = CreateBlock(name, center, size, color, parent);
        go.transform.rotation = Quaternion.Euler(0f, 0f, rotationDeg);

        BoxCollider2D collider = go.AddComponent<BoxCollider2D>();
        collider.size = size;
        collider.offset = Vector2.zero;

        RegisterFootprint(footprint);
        return true;
    }

    private bool TryPlaceCapsuleObstacle(string name, Vector2 center, Vector2 size, bool vertical, float rotationDeg, Transform parent)
    {
        Rect footprint = GetAabb(center, size, rotationDeg);
        if (!IsValidFootprint(footprint))
        {
            return false;
        }

        Color color = Color.Lerp(palette.obstacleBase, palette.obstacleAccent, Range(0.2f, 0.9f));
        GameObject go = CreateBlock(name, center, size, color, parent);
        go.transform.rotation = Quaternion.Euler(0f, 0f, rotationDeg);

        CapsuleCollider2D collider = go.AddComponent<CapsuleCollider2D>();
        collider.size = size;
        collider.direction = vertical ? CapsuleDirection2D.Vertical : CapsuleDirection2D.Horizontal;

        RegisterFootprint(footprint);
        return true;
    }

    private bool TryPlaceDiamondObstacle(string name, Vector2 center, float diameter, Transform parent)
    {
        Vector2 size = new Vector2(diameter, diameter);
        Rect footprint = CenterRect(center, size);
        if (!IsValidFootprint(footprint))
        {
            return false;
        }

        Color color = Color.Lerp(palette.obstacleBase, palette.obstacleAccent, Range(0.2f, 0.95f));
        GameObject go = CreateBlock(name, center, size, color, parent);
        go.transform.rotation = Quaternion.Euler(0f, 0f, 45f);

        float h = diameter * 0.5f;
        PolygonCollider2D collider = go.AddComponent<PolygonCollider2D>();
        collider.points = new[]
        {
            new Vector2(0f, h),
            new Vector2(h, 0f),
            new Vector2(0f, -h),
            new Vector2(-h, 0f)
        };

        RegisterFootprint(footprint);
        return true;
    }

    private bool TryPlacePillarObstacle(string name, Vector2 center, float radius, Transform parent)
    {
        Vector2 size = Vector2.one * (radius * 2f);
        Rect footprint = CenterRect(center, size);
        if (!IsValidFootprint(footprint))
        {
            return false;
        }

        Color color = Color.Lerp(palette.obstacleBase, palette.obstacleAccent, Range(0.2f, 0.95f));
        GameObject go = CreateBlock(name, center, size, color, parent);
        CircleCollider2D collider = go.AddComponent<CircleCollider2D>();
        collider.radius = radius;

        RegisterFootprint(footprint);
        return true;
    }

    private bool TryPlaceLObstacle(string name, Vector2 corner, float armA, float armB, float thickness, int orientation, Transform parent)
    {
        Vector2 dirX;
        Vector2 dirY;

        switch (orientation % 4)
        {
            case 0:
                dirX = Vector2.right;
                dirY = Vector2.up;
                break;
            case 1:
                dirX = Vector2.left;
                dirY = Vector2.up;
                break;
            case 2:
                dirX = Vector2.left;
                dirY = Vector2.down;
                break;
            default:
                dirX = Vector2.right;
                dirY = Vector2.down;
                break;
        }

        Vector2 hSize = new Vector2(armA, thickness);
        Vector2 vSize = new Vector2(thickness, armB);

        Vector2 hCenter = corner + dirX * (armA * 0.5f - thickness * 0.5f);
        Vector2 vCenter = corner + dirY * (armB * 0.5f - thickness * 0.5f);

        Rect hFootprint = CenterRect(hCenter, hSize);
        Rect vFootprint = CenterRect(vCenter, vSize);

        if (!IsValidFootprint(hFootprint) || !IsValidFootprint(vFootprint))
        {
            return false;
        }

        Color color = Color.Lerp(palette.obstacleBase, palette.obstacleAccent, Range(0.15f, 0.85f));

        GameObject h = CreateBlock(name + "_A", hCenter, hSize, color, parent);
        BoxCollider2D hCollider = h.AddComponent<BoxCollider2D>();
        hCollider.size = hSize;

        GameObject v = CreateBlock(name + "_B", vCenter, vSize, color, parent);
        BoxCollider2D vCollider = v.AddComponent<BoxCollider2D>();
        vCollider.size = vSize;

        RegisterFootprint(hFootprint);
        RegisterFootprint(vFootprint);
        return true;
    }

    private bool IsValidFootprint(Rect candidate)
    {
        float minX = -arenaWidth * 0.5f + edgeClearance;
        float maxX = arenaWidth * 0.5f - edgeClearance;
        float minY = -arenaHeight * 0.5f + edgeClearance;
        float maxY = arenaHeight * 0.5f - edgeClearance;

        if (candidate.xMin < minX || candidate.xMax > maxX || candidate.yMin < minY || candidate.yMax > maxY)
        {
            return false;
        }

        for (int i = 0; i < reservedLanes.Count; i++)
        {
            if (reservedLanes[i].Overlaps(candidate))
            {
                return false;
            }
        }

        for (int i = 0; i < blockedAreas.Count; i++)
        {
            if (blockedAreas[i].Overlaps(candidate))
            {
                return false;
            }
        }

        if (IsInsideSafeRadius(candidate, playerTransform) || IsInsideSafeRadius(candidate, anomalyTransform))
        {
            return false;
        }

        return true;
    }

    private void RegisterFootprint(Rect rect)
    {
        placedObstacleRects.Add(rect);
        blockedAreas.Add(InflateRect(rect, obstacleGap));
    }

    private void RegisterDynamicFootprint(Rect rect)
    {
        blockedAreas.Add(InflateRect(rect, dynamicObstacleGap));
    }

    private bool IsOrbitPathClear(Vector2 orbitCenter, float minRadius, float maxRadius, float clearance)
    {
        int samples = Mathf.Max(12, ruptureOrbitPathSamples);
        float inner = Mathf.Max(0.05f, minRadius);
        float outer = Mathf.Max(inner, maxRadius);
        float middle = (inner + outer) * 0.5f;
        float[] ringRadii = { inner, middle, outer };
        float minX = -arenaWidth * 0.5f + edgeClearance + clearance;
        float maxX = arenaWidth * 0.5f - edgeClearance - clearance;
        float minY = -arenaHeight * 0.5f + edgeClearance + clearance;
        float maxY = arenaHeight * 0.5f - edgeClearance - clearance;

        for (int ring = 0; ring < ringRadii.Length; ring++)
        {
            float radius = ringRadii[ring];
            for (int i = 0; i < samples; i++)
            {
                float angle = (i / (float)samples) * Mathf.PI * 2f;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 point = orbitCenter + dir * radius;
                if (point.x < minX || point.x > maxX || point.y < minY || point.y > maxY)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static void ConfigureRuptureOrbitCollision(Collider2D orbitCollider)
    {
        if (orbitCollider == null)
        {
            return;
        }

        Collider2D[] all = FindObjectsByType<Collider2D>();
        for (int i = 0; i < all.Length; i++)
        {
            Collider2D other = all[i];
            if (other == null || other == orbitCollider || other.isTrigger)
            {
                continue;
            }

            if (other.GetComponent<PlayerController>() != null || other.GetComponent<EnemyController>() != null)
            {
                continue;
            }

            Physics2D.IgnoreCollision(orbitCollider, other, true);
        }
    }

    private bool IsInsideSafeRadius(Rect rect, Transform actor)
    {
        if (actor == null)
        {
            return false;
        }

        Vector2 actorPos = actor.position;
        float closestX = Mathf.Clamp(actorPos.x, rect.xMin, rect.xMax);
        float closestY = Mathf.Clamp(actorPos.y, rect.yMin, rect.yMax);
        Vector2 closest = new Vector2(closestX, closestY);

        return Vector2.Distance(actorPos, closest) < spawnSafeRadius;
    }

    private Vector2 GetRandomInsideArena(Vector2 size)
    {
        float minX = -arenaWidth * 0.5f + edgeClearance + size.x * 0.5f;
        float maxX = arenaWidth * 0.5f - edgeClearance - size.x * 0.5f;
        float minY = -arenaHeight * 0.5f + edgeClearance + size.y * 0.5f;
        float maxY = arenaHeight * 0.5f - edgeClearance - size.y * 0.5f;

        return new Vector2(Range(minX, maxX), Range(minY, maxY));
    }

    private void CreateWall(string name, Vector2 position, Vector2 size, Transform parent)
    {
        GameObject wall = CreateBlock(name, position, size, palette.wall, parent);
        BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
        collider.size = size;
        collider.offset = Vector2.zero;
    }

    private void CreateThemedDetails(Transform parent)
    {
        CreateLaneStrips(parent);
        CreateWallMarkers(parent);
        if (activeTheme == ArenaTheme.RuptureZone)
        {
            CreateRuptureRingGuides(parent);
        }
        CreateObstacleEdgeLights(parent);
    }

    private void CreateRuptureRingGuides(Transform parent)
    {
        int segments = 48;
        float margin = edgeClearance + 0.35f;
        float minX = -arenaWidth * 0.5f + margin;
        float maxX = arenaWidth * 0.5f - margin;
        float minY = -arenaHeight * 0.5f + margin;
        float maxY = arenaHeight * 0.5f - margin;

        for (int a = 0; a < ruptureAnchors.Count; a++)
        {
            Vector2 anchor = ruptureAnchors[a];
            float extentX = Mathf.Min(anchor.x - minX, maxX - anchor.x) - 0.1f;
            float extentY = Mathf.Min(anchor.y - minY, maxY - anchor.y) - 0.1f;
            extentX = Mathf.Max(ruptureRingRadiusMin + 0.1f, extentX);
            extentY = Mathf.Max(ruptureRingRadiusMin + 0.1f, extentY);

            float maxAxisExtent = Mathf.Max(extentX, extentY);
            int ringCount = Mathf.Max(Mathf.Max(8, ruptureRingsPerAnchor), Mathf.CeilToInt(maxAxisExtent / 1.05f));

            for (int ring = 0; ring < ringCount; ring++)
            {
                float t = ringCount == 1 ? 1f : (ring + 1f) / ringCount;
                float rx = Mathf.Lerp(ruptureRingRadiusMin, extentX, t);
                float ry = Mathf.Lerp(ruptureRingRadiusMin, extentY, t);
                float segLen = Mathf.Max(0.16f, (2f * Mathf.PI * Mathf.Max(rx, ry)) / segments * 0.28f);

                for (int s = 0; s < segments; s++)
                {
                    float ang = (360f / segments) * s;
                    Vector2 dir = new Vector2(Mathf.Cos(ang * Mathf.Deg2Rad), Mathf.Sin(ang * Mathf.Deg2Rad));
                    Vector2 pos = anchor + new Vector2(dir.x * rx, dir.y * ry);

                    if (pos.x <= -arenaWidth * 0.5f + margin || pos.x >= arenaWidth * 0.5f - margin ||
                        pos.y <= -arenaHeight * 0.5f + margin || pos.y >= arenaHeight * 0.5f - margin)
                    {
                        continue;
                    }

                    Vector2 size = new Vector2(segLen, 0.06f);
                    GameObject segment = CreateBlock($"RuptureGuide_{a}_{ring}_{s}", pos, size, new Color(palette.detail.r, palette.detail.g, palette.detail.b, palette.detail.a * 0.6f), parent);
                    Vector2 tangent = new Vector2(-Mathf.Sin(ang * Mathf.Deg2Rad) * rx, Mathf.Cos(ang * Mathf.Deg2Rad) * ry);
                    float tangentAngle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;
                    segment.transform.rotation = Quaternion.Euler(0f, 0f, tangentAngle);
                    segment.GetComponent<SpriteRenderer>().sortingOrder = -2;
                }
            }
        }
    }

    private void CreateLaneStrips(Transform parent)
    {
        for (int i = 0; i < reservedLanes.Count; i++)
        {
            Rect lane = reservedLanes[i];
            Vector2 size = lane.width >= lane.height
                ? new Vector2(lane.width * 0.96f, 0.12f)
                : new Vector2(0.12f, lane.height * 0.96f);

            Vector2 center = lane.center;
            Color tint = new Color(palette.detail.r, palette.detail.g, palette.detail.b, palette.detail.a * 0.8f);
            GameObject strip = CreateBlock($"LaneStrip_{i}", center, size, tint, parent);
            strip.GetComponent<SpriteRenderer>().sortingOrder = -1;
        }
    }

    private void CreateWallMarkers(Transform parent)
    {
        float spacing = 3.5f;
        float yTop = arenaHeight * 0.5f - wallThickness * 0.55f;
        float yBottom = -arenaHeight * 0.5f + wallThickness * 0.55f;

        int idx = 0;
        for (float x = -arenaWidth * 0.5f + 1.5f; x <= arenaWidth * 0.5f - 1.5f; x += spacing)
        {
            Vector2 size = new Vector2(0.45f, 0.18f);
            GameObject top = CreateBlock($"WallMark_T_{idx}", new Vector2(x, yTop), size, palette.detail, parent);
            top.GetComponent<SpriteRenderer>().sortingOrder = 1;

            GameObject bottom = CreateBlock($"WallMark_B_{idx}", new Vector2(x, yBottom), size, palette.detail, parent);
            bottom.GetComponent<SpriteRenderer>().sortingOrder = 1;
            idx++;
        }
    }

    private void CreateObstacleEdgeLights(Transform parent)
    {
        for (int i = 0; i < placedObstacleRects.Count; i++)
        {
            if (Range(0f, 1f) > 0.55f)
            {
                continue;
            }

            Rect r = placedObstacleRects[i];
            bool horizontal = r.width >= r.height;
            Vector2 size = horizontal ? new Vector2(r.width * 0.65f, 0.1f) : new Vector2(0.1f, r.height * 0.65f);

            Vector2 center = r.center;
            if (horizontal)
            {
                center.y += (Range(0f, 1f) < 0.5f ? -1f : 1f) * (r.height * 0.5f + 0.05f);
            }
            else
            {
                center.x += (Range(0f, 1f) < 0.5f ? -1f : 1f) * (r.width * 0.5f + 0.05f);
            }

            Color tint = new Color(palette.detail.r, palette.detail.g, palette.detail.b, palette.detail.a * 0.9f);
            GameObject edge = CreateBlock($"ObstacleLight_{i}", center, size, tint, parent);
            edge.GetComponent<SpriteRenderer>().sortingOrder = 2;
        }
    }

    private static GameObject CreateBlock(string name, Vector2 position, Vector2 size, Color color, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(position.x, position.y, 0f);
        go.transform.localScale = Vector3.one;

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = SquareSpriteProvider.Get();
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.size = size;
        renderer.color = color;
        renderer.sortingOrder = 0;

        return go;
    }

    private static Rect GetAabb(Vector2 center, Vector2 size, float rotationDeg)
    {
        float rad = rotationDeg * Mathf.Deg2Rad;
        float c = Mathf.Abs(Mathf.Cos(rad));
        float s = Mathf.Abs(Mathf.Sin(rad));

        float hx = size.x * 0.5f;
        float hy = size.y * 0.5f;

        float extX = hx * c + hy * s;
        float extY = hx * s + hy * c;

        return new Rect(center.x - extX, center.y - extY, extX * 2f, extY * 2f);
    }

    private static Rect CenterRect(Vector2 center, Vector2 size)
    {
        return new Rect(center.x - size.x * 0.5f, center.y - size.y * 0.5f, size.x, size.y);
    }

    private static Rect InflateRect(Rect rect, float amount)
    {
        return new Rect(
            rect.xMin - amount,
            rect.yMin - amount,
            rect.width + amount * 2f,
            rect.height + amount * 2f);
    }

    private Vector2 RandomInsideUnitCircle()
    {
        float angle = Range(0f, Mathf.PI * 2f);
        float radius = Mathf.Sqrt(Range(0f, 1f));
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    }

    private Vector2 GetRandomRupturePosition()
    {
        float margin = Mathf.Max(edgeClearance + 0.6f, ruptureAnchorPadding);
        float minX = -arenaWidth * 0.5f + margin;
        float maxX = arenaWidth * 0.5f - margin;
        float minY = -arenaHeight * 0.5f + margin;
        float maxY = arenaHeight * 0.5f - margin;

        return new Vector2(Range(minX, maxX), Range(minY, maxY));
    }

    private float Range(float min, float max)
    {
        return min + (float)rng.NextDouble() * (max - min);
    }

    private int Range(int minInclusive, int maxExclusive)
    {
        return rng.Next(minInclusive, maxExclusive);
    }
}

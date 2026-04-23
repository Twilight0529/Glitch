using System;
using System.Collections.Generic;
using UnityEngine;

public class ProceduralArenaGenerator : MonoBehaviour
{
    private enum ArenaTheme
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
    }

    [Header("Arena")]
    [SerializeField] private float arenaWidth = 32f;
    [SerializeField] private float arenaHeight = 18f;
    [SerializeField] private float wallThickness = 1f;

    [Header("Flow Rules")]
    [SerializeField] private float primaryLaneWidth = 3.4f;
    [SerializeField] private float secondaryLaneWidth = 2.6f;

    [Header("Obstacles")]
    [SerializeField] private int minObstacles = 10;
    [SerializeField] private int maxObstacles = 16;
    [SerializeField] private Vector2 obstacleSizeMin = new Vector2(1.2f, 1.2f);
    [SerializeField] private Vector2 obstacleSizeMax = new Vector2(3.1f, 2.4f);
    [SerializeField] private float edgeClearance = 0.7f;
    [SerializeField] private float obstacleGap = 0.45f;
    [SerializeField] private int placementAttemptsPerObstacle = 36;

    [Header("Spawn Safety")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform anomalyTransform;
    [SerializeField] private float spawnSafeRadius = 2.2f;

    [Header("Theme")]
    [SerializeField] private bool randomizeThemeEachRun = true;
    [SerializeField] private ArenaTheme fixedTheme = ArenaTheme.ContainmentLab;

    [Header("Random")]
    [SerializeField] private bool randomizeSeedEachRun = true;
    [SerializeField] private int fixedSeed = 12345;
    [SerializeField] private bool generateOnAwake = true;

    private const string BoundsRootName = "Bounds";
    private const string ObstaclesRootName = "Obstacles";
    private const string DetailsRootName = "Details";

    private System.Random rng;
    private ArenaTheme activeTheme;
    private ThemePalette palette;

    private readonly List<Rect> blockedAreas = new List<Rect>();
    private readonly List<Rect> placedObstacleRects = new List<Rect>();
    private readonly List<Rect> reservedLanes = new List<Rect>();

    public float ArenaWidth => arenaWidth;
    public float ArenaHeight => arenaHeight;

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

        BuildReservedLanes();

        Transform boundsRoot = new GameObject(BoundsRootName).transform;
        boundsRoot.SetParent(transform, false);

        Transform obstaclesRoot = new GameObject(ObstaclesRootName).transform;
        obstaclesRoot.SetParent(transform, false);

        Transform detailsRoot = new GameObject(DetailsRootName).transform;
        detailsRoot.SetParent(transform, false);

        CreateArenaBounds(boundsRoot);
        CreateThemedObstacles(obstaclesRoot);
        CreateThemedDetails(detailsRoot);
    }

    public void SetRuntimeReferences(Transform player, Transform anomaly)
    {
        playerTransform = player;
        anomalyTransform = anomaly;
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
                    detail = new Color(0.54f, 0.66f, 0.82f, 0.22f)
                };
            case ArenaTheme.StorageBay:
                return new ThemePalette
                {
                    wall = new Color(0.15f, 0.12f, 0.10f),
                    obstacleBase = new Color(0.36f, 0.30f, 0.23f),
                    obstacleAccent = new Color(0.52f, 0.43f, 0.30f),
                    detail = new Color(0.78f, 0.63f, 0.42f, 0.22f)
                };
            default:
                return new ThemePalette
                {
                    wall = new Color(0.10f, 0.11f, 0.14f),
                    obstacleBase = new Color(0.30f, 0.24f, 0.34f),
                    obstacleAccent = new Color(0.48f, 0.34f, 0.53f),
                    detail = new Color(0.86f, 0.44f, 0.70f, 0.20f)
                };
        }
    }

    private void ClearGeneratedGeometry()
    {
        DestroyGeneratedChild(BoundsRootName);
        DestroyGeneratedChild(ObstaclesRootName);
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
                reservedLanes.Add(new Rect(-innerWidth * 0.5f, -secondaryLaneWidth * 0.5f, innerWidth, secondaryLaneWidth));
                break;
        }
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
            bool horizontal = Range(0f, 1f) < 0.5f;
            Vector2 size = horizontal
                ? new Vector2(Range(3.2f, 5.8f), Range(0.8f, 1.35f))
                : new Vector2(Range(0.8f, 1.35f), Range(3.2f, 5.8f));

            Vector2 center = GetRandomInsideArena(size);
            if (horizontal && Mathf.Abs(center.y) < primaryLaneWidth * 0.65f)
            {
                continue;
            }

            if (!horizontal && Mathf.Abs(center.x) < secondaryLaneWidth * 0.65f)
            {
                continue;
            }

            if (TryPlaceRectangleObstacle($"Obstacle_Lab_{serial}", center, size, 0f, parent))
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

                if (shapeRoll < 0.58f)
                {
                    Vector2 size = new Vector2(Range(1.4f, 2.8f), Range(1.2f, 2.4f));
                    success = TryPlaceRectangleObstacle($"Obstacle_Storage_{serial}", center, size, 0f, parent);
                }
                else if (shapeRoll < 0.82f)
                {
                    success = TryPlacePillarObstacle($"Obstacle_StoragePillar_{serial}", center, Range(0.5f, 1.0f), parent);
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
        int target = Range(minObstacles, maxObstacles + 2);
        int placed = 0;
        int serial = 0;
        int attempts = 0;
        int maxAttempts = target * placementAttemptsPerObstacle;

        while (placed < target && attempts < maxAttempts)
        {
            attempts++;
            float roll = Range(0f, 1f);
            float angle = Range(0f, 360f);
            float radius = Range(1.5f, Mathf.Min(arenaWidth, arenaHeight) * 0.43f);
            Vector2 center = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * radius;

            bool success;
            if (roll < 0.62f)
            {
                Vector2 size = new Vector2(Range(2.2f, 4.4f), Range(0.75f, 1.25f));
                float rotation = angle + Range(-28f, 28f);
                success = TryPlaceRectangleObstacle($"Obstacle_Rupture_{serial}", center, size, rotation, parent);
            }
            else if (roll < 0.84f)
            {
                success = TryPlacePillarObstacle($"Obstacle_RupturePillar_{serial}", center, Range(0.5f, 0.95f), parent);
            }
            else
            {
                success = TryPlaceLObstacle($"Obstacle_RuptureL_{serial}", center, Range(2.0f, 3.4f), Range(1.9f, 3.0f), Range(0.65f, 0.95f), Range(0, 4), parent);
            }

            if (success)
            {
                placed++;
                serial++;
            }
        }
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
        CreateObstacleEdgeLights(parent);
    }

    private void CreateLaneStrips(Transform parent)
    {
        for (int i = 0; i < reservedLanes.Count; i++)
        {
            Rect lane = reservedLanes[i];
            Vector2 size;
            if (lane.width >= lane.height)
            {
                size = new Vector2(lane.width * 0.96f, 0.12f);
            }
            else
            {
                size = new Vector2(0.12f, lane.height * 0.96f);
            }

            Vector2 center = new Vector2(lane.center.x, lane.center.y);
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

    private float Range(float min, float max)
    {
        return min + (float)rng.NextDouble() * (max - min);
    }

    private int Range(int minInclusive, int maxExclusive)
    {
        return rng.Next(minInclusive, maxExclusive);
    }
}

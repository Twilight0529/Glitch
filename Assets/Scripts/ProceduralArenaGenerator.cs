using System;
using System.Collections.Generic;
using UnityEngine;

public class ProceduralArenaGenerator : MonoBehaviour
{
    [Header("Arena")]
    [SerializeField] private float arenaWidth = 24f;
    [SerializeField] private float arenaHeight = 14f;
    [SerializeField] private float wallThickness = 1f;

    [Header("Obstacles")]
    [SerializeField] private int minObstacles = 7;
    [SerializeField] private int maxObstacles = 12;
    [SerializeField] private Vector2 obstacleSizeMin = new Vector2(1.2f, 1.2f);
    [SerializeField] private Vector2 obstacleSizeMax = new Vector2(3.1f, 2.4f);
    [SerializeField] private float edgeClearance = 0.6f;
    [SerializeField] private float obstacleGap = 0.4f;
    [SerializeField] private int placementAttemptsPerObstacle = 30;

    [Header("Spawn Safety")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform anomalyTransform;
    [SerializeField] private float spawnSafeRadius = 2.2f;

    [Header("Random")]
    [SerializeField] private bool randomizeSeedEachRun = true;
    [SerializeField] private int fixedSeed = 12345;
    [SerializeField] private bool generateOnAwake = true;

    private const string BoundsRootName = "Bounds";
    private const string ObstaclesRootName = "Obstacles";

    private System.Random rng;

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
        ClearGeneratedGeometry();

        Transform boundsRoot = new GameObject(BoundsRootName).transform;
        boundsRoot.SetParent(transform, false);

        Transform obstaclesRoot = new GameObject(ObstaclesRootName).transform;
        obstaclesRoot.SetParent(transform, false);

        CreateArenaBounds(boundsRoot);
        CreateProceduralObstacles(obstaclesRoot);
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

    private void ClearGeneratedGeometry()
    {
        Transform bounds = transform.Find(BoundsRootName);
        if (bounds != null)
        {
            DestroyByPlayState(bounds.gameObject);
        }

        Transform obstacles = transform.Find(ObstaclesRootName);
        if (obstacles != null)
        {
            DestroyByPlayState(obstacles.gameObject);
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

    private void CreateArenaBounds(Transform parent)
    {
        CreateWall("Wall_Top", new Vector2(0f, arenaHeight * 0.5f), new Vector2(arenaWidth + wallThickness * 2f, wallThickness), parent);
        CreateWall("Wall_Bottom", new Vector2(0f, -arenaHeight * 0.5f), new Vector2(arenaWidth + wallThickness * 2f, wallThickness), parent);
        CreateWall("Wall_Left", new Vector2(-arenaWidth * 0.5f, 0f), new Vector2(wallThickness, arenaHeight), parent);
        CreateWall("Wall_Right", new Vector2(arenaWidth * 0.5f, 0f), new Vector2(wallThickness, arenaHeight), parent);
    }

    private void CreateProceduralObstacles(Transform parent)
    {
        List<Rect> takenRects = new List<Rect>();
        int obstacleCount = Range(minObstacles, maxObstacles + 1);

        for (int i = 0; i < obstacleCount; i++)
        {
            bool placed = false;

            for (int attempt = 0; attempt < placementAttemptsPerObstacle; attempt++)
            {
                Vector2 size = new Vector2(
                    Range(obstacleSizeMin.x, obstacleSizeMax.x),
                    Range(obstacleSizeMin.y, obstacleSizeMax.y));

                Vector2 candidatePosition = GetRandomInsideArena(size);
                Rect candidateRect = CenterRect(candidatePosition, size);

                if (!IsValidObstaclePlacement(candidateRect, takenRects))
                {
                    continue;
                }

                CreateObstacle($"Obstacle_{i + 1}", candidatePosition, size, parent);
                takenRects.Add(InflateRect(candidateRect, obstacleGap));
                placed = true;
                break;
            }

            if (!placed)
            {
                break;
            }
        }
    }

    private bool IsValidObstaclePlacement(Rect candidate, List<Rect> takenRects)
    {
        for (int i = 0; i < takenRects.Count; i++)
        {
            if (takenRects[i].Overlaps(candidate))
            {
                return false;
            }
        }

        if (IsInsideSafeRadius(candidate, playerTransform))
        {
            return false;
        }

        if (IsInsideSafeRadius(candidate, anomalyTransform))
        {
            return false;
        }

        return true;
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
        float minX = -arenaWidth * 0.5f + edgeClearance + (size.x * 0.5f);
        float maxX = arenaWidth * 0.5f - edgeClearance - (size.x * 0.5f);
        float minY = -arenaHeight * 0.5f + edgeClearance + (size.y * 0.5f);
        float maxY = arenaHeight * 0.5f - edgeClearance - (size.y * 0.5f);

        return new Vector2(Range(minX, maxX), Range(minY, maxY));
    }

    private void CreateWall(string name, Vector2 position, Vector2 size, Transform parent)
    {
        GameObject wall = CreateBlock(name, position, size, new Color(0.15f, 0.16f, 0.2f), parent);
        BoxCollider2D collider = wall.AddComponent<BoxCollider2D>();
        collider.size = size;
        collider.offset = Vector2.zero;
    }

    private void CreateObstacle(string name, Vector2 position, Vector2 size, Transform parent)
    {
        float tint = Range(0.20f, 0.32f);
        GameObject obstacle = CreateBlock(name, position, size, new Color(tint, tint + 0.03f, tint + 0.08f), parent);
        BoxCollider2D collider = obstacle.AddComponent<BoxCollider2D>();
        collider.size = size;
        collider.offset = Vector2.zero;
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

    private Rect CenterRect(Vector2 center, Vector2 size)
    {
        return new Rect(center.x - (size.x * 0.5f), center.y - (size.y * 0.5f), size.x, size.y);
    }

    private static Rect InflateRect(Rect rect, float amount)
    {
        return new Rect(
            rect.xMin - amount,
            rect.yMin - amount,
            rect.width + amount * 2f,
            rect.height + amount * 2f);
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

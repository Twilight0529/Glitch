#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LevelSceneGenerator
{
    private const string RootName = "__GeneratedArena";

    [MenuItem("Glitch/Generate/Setup Current Level Scene")]
    public static void SetupCurrentLevelScene()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("Stop Play Mode before generating the scene.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("No active loaded scene found.");
            return;
        }

        GameObject existingRoot = GameObject.Find(RootName);
        if (existingRoot != null)
        {
            Object.DestroyImmediate(existingRoot);
        }

        EnsureCamera();

        GameObject root = new GameObject(RootName);

        GameManager gameManager = CreateGameManager(root.transform);
        PlayerController player = CreatePlayer(root.transform);
        EnemyController enemy = CreateEnemy(root.transform, player, gameManager);

        CreateArenaBounds(root.transform);
        CreateObstacles(root.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = root;

        Debug.Log("Level scene generated: GameManager, Player, Anomaly, walls and obstacles are ready.");
    }

    private static void EnsureCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            cam = camObj.AddComponent<Camera>();
            camObj.tag = "MainCamera";
        }

        cam.orthographic = true;
        cam.orthographicSize = 8f;
        cam.transform.position = new Vector3(0f, 0f, -10f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.05f, 0.07f);
    }

    private static GameManager CreateGameManager(Transform parent)
    {
        GameObject go = new GameObject("GameManager");
        go.transform.SetParent(parent, false);
        return go.AddComponent<GameManager>();
    }

    private static PlayerController CreatePlayer(Transform parent)
    {
        GameObject go = CreateActorVisual("Player", new Vector2(0f, -2f), new Color(0.35f, 0.9f, 1f), 0.9f, parent);

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.45f;

        return go.AddComponent<PlayerController>();
    }

    private static EnemyController CreateEnemy(Transform parent, PlayerController player, GameManager gameManager)
    {
        GameObject go = CreateActorVisual("Anomaly", new Vector2(0f, 2f), new Color(0.95f, 0.2f, 0.25f), 0.95f, parent);

        Rigidbody2D rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        CircleCollider2D col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.47f;

        EnemyController enemy = go.AddComponent<EnemyController>();

        SerializedObject so = new SerializedObject(enemy);
        so.FindProperty("player").objectReferenceValue = player;
        so.FindProperty("gameManager").objectReferenceValue = gameManager;
        so.ApplyModifiedPropertiesWithoutUndo();

        return enemy;
    }

    private static void CreateArenaBounds(Transform parent)
    {
        Transform boundsRoot = new GameObject("Bounds").transform;
        boundsRoot.SetParent(parent, false);

        float width = 24f;
        float height = 14f;
        float thickness = 1f;

        CreateWall("Wall_Top", new Vector2(0f, height * 0.5f), new Vector2(width + thickness * 2f, thickness), boundsRoot);
        CreateWall("Wall_Bottom", new Vector2(0f, -height * 0.5f), new Vector2(width + thickness * 2f, thickness), boundsRoot);
        CreateWall("Wall_Left", new Vector2(-width * 0.5f, 0f), new Vector2(thickness, height), boundsRoot);
        CreateWall("Wall_Right", new Vector2(width * 0.5f, 0f), new Vector2(thickness, height), boundsRoot);
    }

    private static void CreateObstacles(Transform parent)
    {
        Transform obstaclesRoot = new GameObject("Obstacles").transform;
        obstaclesRoot.SetParent(parent, false);

        CreateObstacle("Obstacle_Center", new Vector2(0f, 0f), new Vector2(2.6f, 2.0f), obstaclesRoot);
        CreateObstacle("Obstacle_Left", new Vector2(-5.2f, 2.1f), new Vector2(2.2f, 1.4f), obstaclesRoot);
        CreateObstacle("Obstacle_Right", new Vector2(5.3f, -2.3f), new Vector2(2.4f, 1.5f), obstaclesRoot);
        CreateObstacle("Obstacle_Top", new Vector2(1.6f, 4.1f), new Vector2(3.0f, 1.2f), obstaclesRoot);
        CreateObstacle("Obstacle_Bottom", new Vector2(-1.9f, -4.0f), new Vector2(2.8f, 1.3f), obstaclesRoot);
    }

    private static void CreateWall(string name, Vector2 position, Vector2 size, Transform parent)
    {
        GameObject go = CreateBlockVisual(name, position, size, new Color(0.15f, 0.16f, 0.2f), parent);
        go.AddComponent<BoxCollider2D>();
    }

    private static void CreateObstacle(string name, Vector2 position, Vector2 size, Transform parent)
    {
        GameObject go = CreateBlockVisual(name, position, size, new Color(0.22f, 0.25f, 0.3f), parent);
        go.AddComponent<BoxCollider2D>();
    }

    private static GameObject CreateActorVisual(string name, Vector2 position, Color color, float scale, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(position.x, position.y, 0f);
        go.transform.localScale = new Vector3(scale, scale, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetBuiltinSquareSprite();
        sr.color = color;
        sr.sortingOrder = 10;

        return go;
    }

    private static GameObject CreateBlockVisual(string name, Vector2 position, Vector2 size, Color color, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(position.x, position.y, 0f);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = GetBuiltinSquareSprite();
        sr.color = color;
        sr.sortingOrder = 0;

        return go;
    }

    private static Sprite GetBuiltinSquareSprite()
    {
        Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        if (sprite == null)
        {
            sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        }

        return sprite;
    }
}
#endif

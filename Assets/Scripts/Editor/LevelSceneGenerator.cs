#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class LevelSceneGenerator
{
    private const string RootName = "__GeneratedArena";
    private const float TargetArenaWidth = 32f;
    private const float TargetArenaHeight = 18f;
    private const float CameraPadding = 1.4f;

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

        EnsureCamera(TargetArenaWidth, TargetArenaHeight);

        GameObject root = new GameObject(RootName);

        GameManager gameManager = CreateGameManager(root.transform);
        PlayerController player = CreatePlayer(root.transform);
        EnemyController enemy = CreateEnemy(root.transform, player, gameManager);
        CreateArenaGenerator(root.transform, player.transform, enemy.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = root;

        Debug.Log("Level scene generated: GameManager, Player, Anomaly and procedural arena are ready.");
    }

    private static void EnsureCamera(float arenaWidth, float arenaHeight)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            GameObject camObj = new GameObject("Main Camera");
            cam = camObj.AddComponent<Camera>();
            camObj.tag = "MainCamera";
        }

        cam.orthographic = true;
        float halfHeight = arenaHeight * 0.5f + CameraPadding;
        float halfWidth = arenaWidth * 0.5f + CameraPadding;
        float aspect = Mathf.Max(0.1f, cam.aspect);
        cam.orthographicSize = Mathf.Max(halfHeight, halfWidth / aspect);
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

    private static void CreateArenaGenerator(Transform parent, Transform player, Transform anomaly)
    {
        ProceduralArenaGenerator generator = parent.gameObject.AddComponent<ProceduralArenaGenerator>();
        SerializedObject so = new SerializedObject(generator);
        so.FindProperty("arenaWidth").floatValue = TargetArenaWidth;
        so.FindProperty("arenaHeight").floatValue = TargetArenaHeight;
        so.FindProperty("minObstacles").intValue = 10;
        so.FindProperty("maxObstacles").intValue = 16;
        so.FindProperty("generateOnAwake").boolValue = true;
        so.FindProperty("randomizeSeedEachRun").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();

        generator.SetRuntimeReferences(player, anomaly);
        generator.GenerateNow();
    }

    private static GameObject CreateActorVisual(string name, Vector2 position, Color color, float scale, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(position.x, position.y, 0f);
        go.transform.localScale = new Vector3(scale, scale, 1f);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SquareSpriteProvider.Get();
        sr.color = color;
        sr.sortingOrder = 10;

        return go;
    }
}
#endif

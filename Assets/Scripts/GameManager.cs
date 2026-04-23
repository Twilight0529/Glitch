using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    [Header("Run State")]
    [SerializeField] private bool autoReloadSceneOnGameOver = false;
    [SerializeField] private float reloadDelay = 1.2f;

    [Header("Difficulty")]
    [SerializeField] private float behaviorChangeInterval = 5f;
    [SerializeField] private float difficultyRampPerSecond = 0.03f;

    public bool IsGameOver { get; private set; }
    public float SurvivalTime { get; private set; }
    public float DifficultyMultiplier => 1f + (SurvivalTime * difficultyRampPerSecond);

    public float CurrentBehaviorChangeInterval
    {
        get { return behaviorChangeInterval; }
    }

    private float reloadTimer;
    private ProceduralArenaGenerator arenaGenerator;
    private string levelType = "Unknown";

    private void Start()
    {
        RefreshLevelType();
    }

    private void Update()
    {
        if (arenaGenerator == null)
        {
            RefreshLevelType();
        }
        else if (levelType != arenaGenerator.ActiveThemeLabel)
        {
            levelType = arenaGenerator.ActiveThemeLabel;
        }

        if (IsGameOver)
        {
            HandleOptionalReload();
            return;
        }

        SurvivalTime += Time.deltaTime;
    }

    public void TriggerGameOver()
    {
        if (IsGameOver)
        {
            return;
        }

        IsGameOver = true;
        Debug.Log($"GAME OVER | Time Survived: {SurvivalTime:F2}s");
    }

    private void HandleOptionalReload()
    {
        if (!autoReloadSceneOnGameOver)
        {
            return;
        }

        reloadTimer += Time.deltaTime;

        if (reloadTimer >= reloadDelay)
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }

    private void OnGUI()
    {
        const int margin = 12;

        GUI.Label(new Rect(margin, margin, 350f, 25f), $"Survival Time: {SurvivalTime:F1}s");
        GUI.Label(new Rect(margin, margin + 22f, 350f, 25f), $"Threat Level: x{DifficultyMultiplier:F2}");
        GUI.Label(new Rect(margin, margin + 44f, 350f, 25f), $"Pattern Shift: {CurrentBehaviorChangeInterval:F1}s");
        GUI.Label(new Rect(margin, margin + 66f, 350f, 25f), $"Level Type: {levelType}");

        if (IsGameOver)
        {
            GUI.Label(new Rect(margin, margin + 96f, 500f, 25f), "GAME OVER - The anomaly reached you.");
        }
    }

    private void RefreshLevelType()
    {
        arenaGenerator = FindAnyObjectByType<ProceduralArenaGenerator>();
        if (arenaGenerator != null)
        {
            levelType = arenaGenerator.ActiveThemeLabel;
        }
    }
}

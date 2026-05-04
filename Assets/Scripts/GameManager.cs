using UnityEngine;

public class GameManager : MonoBehaviour
{
    private enum RunPhase
    {
        StartupDelay,
        Countdown,
        Active
    }

    [Header("Run State")]
    [SerializeField] private bool autoReloadSceneOnGameOver = false;
    [SerializeField] private float reloadDelay = 1.2f;
    [SerializeField] private float startupDelaySeconds = 1f;
    [SerializeField] private float countdownSeconds = 3f;

    [Header("Difficulty")]
    [SerializeField] private float behaviorChangeInterval = 5f;
    [SerializeField] private float difficultyRampPerSecond = 0.03f;

    public bool IsGameOver { get; private set; }
    public bool IsRunActive => runPhase == RunPhase.Active && !IsGameOver;
    public float SurvivalTime { get; private set; }
    public float DifficultyMultiplier => 1f + (SurvivalTime * difficultyRampPerSecond);
    public string CurrentLevelTypeLabel => levelType;

    public float CurrentBehaviorChangeInterval
    {
        get { return behaviorChangeInterval; }
    }

    private float reloadTimer;
    private RunPhase runPhase;
    private float startupTimer;
    private float countdownTimer;
    private ProceduralArenaGenerator arenaGenerator;
    private string levelType = "Unknown";
    private GUIStyle countdownStyle;
    private GUIStyle countdownSubStyle;

    private void Awake()
    {
        EnsureMenuController();
    }

    private void Start()
    {
        RefreshLevelType();
        BeginStartupSequence();
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

        if (runPhase != RunPhase.Active)
        {
            UpdateStartupSequence();
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
        runPhase = RunPhase.Active;
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
            SceneTransitionController.ReloadActiveScene();
        }
    }

    private void OnGUI()
    {
        if (GameMenuController.ShouldHideGameplayHud || SceneTransitionController.IsFading)
        {
            return;
        }

        const int margin = 12;

        GUI.Label(new Rect(margin, margin, 350f, 25f), $"Survival Time: {SurvivalTime:F1}s");
        GUI.Label(new Rect(margin, margin + 22f, 350f, 25f), $"Threat Level: x{DifficultyMultiplier:F2}");
        GUI.Label(new Rect(margin, margin + 44f, 350f, 25f), $"Pattern Shift: {CurrentBehaviorChangeInterval:F1}s");
        GUI.Label(new Rect(margin, margin + 66f, 350f, 25f), $"Level Type: {levelType}");

        if (IsGameOver)
        {
            GUI.Label(new Rect(margin, margin + 96f, 500f, 25f), "GAME OVER - The anomaly reached you.");
        }

        if (runPhase == RunPhase.StartupDelay || runPhase == RunPhase.Countdown)
        {
            DrawCountdownOverlay();
        }
    }

    private void BeginStartupSequence()
    {
        runPhase = RunPhase.StartupDelay;
        startupTimer = 0f;
        countdownTimer = Mathf.Max(0.1f, countdownSeconds);
        SurvivalTime = 0f;
        reloadTimer = 0f;
        Time.timeScale = 0f;
    }

    private void UpdateStartupSequence()
    {
        float dt = Time.unscaledDeltaTime;

        if (runPhase == RunPhase.StartupDelay)
        {
            startupTimer += dt;
            if (startupTimer >= Mathf.Max(0f, startupDelaySeconds))
            {
                runPhase = RunPhase.Countdown;
            }
            return;
        }

        if (runPhase == RunPhase.Countdown)
        {
            countdownTimer -= dt;
            if (countdownTimer <= 0f)
            {
                runPhase = RunPhase.Active;
                Time.timeScale = 1f;
            }
        }
    }

    private void DrawCountdownOverlay()
    {
        EnsureCountdownStyles();

        string main;
        string sub;

        if (runPhase == RunPhase.StartupDelay)
        {
            main = "...";
            sub = "Inicializando contencion";
        }
        else
        {
            int remaining = Mathf.Max(1, Mathf.CeilToInt(countdownTimer));
            main = remaining.ToString();
            sub = "Preparate";
        }

        float centerX = Screen.width * 0.5f;
        float centerY = Screen.height * 0.5f;
        Rect mainRect = new Rect(centerX - 140f, centerY - 90f, 280f, 96f);
        Rect subRect = new Rect(centerX - 220f, centerY + 12f, 440f, 36f);

        GUI.Label(mainRect, main, countdownStyle);
        GUI.Label(subRect, sub, countdownSubStyle);
    }

    private void EnsureCountdownStyles()
    {
        if (countdownStyle != null)
        {
            return;
        }

        countdownStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 72,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        countdownStyle.normal.textColor = new Color(0.96f, 0.90f, 0.95f, 0.96f);

        countdownSubStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 24,
            fontStyle = FontStyle.Italic,
            alignment = TextAnchor.MiddleCenter
        };
        countdownSubStyle.normal.textColor = new Color(0.86f, 0.78f, 0.90f, 0.92f);
    }

    private void RefreshLevelType()
    {
        arenaGenerator = FindAnyObjectByType<ProceduralArenaGenerator>();
        if (arenaGenerator != null)
        {
            levelType = arenaGenerator.ActiveThemeLabel;
        }
    }

    private void EnsureMenuController()
    {
        if (FindAnyObjectByType<GameMenuController>() != null)
        {
            return;
        }

        GameObject go = new GameObject("GameMenuController");
        go.AddComponent<GameMenuController>();
    }
}

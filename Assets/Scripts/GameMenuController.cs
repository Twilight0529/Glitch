using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class GameMenuController : MonoBehaviour
{
    private enum OverlayState
    {
        Playing,
        Paused,
        Defeat
    }

    [Header("References")]
    [SerializeField] private GameManager gameManager;

    [Header("Scene Flow")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Defeat Theme")]
    [SerializeField] private string defeatTitle = "CONTAINMENT FAILURE";
    [SerializeField] private string defeatSubtitle = "La anomalia te alcanzo.";
    [SerializeField] private float defeatPulseSpeed = 1.8f;

    public static bool ShouldHideGameplayHud { get; private set; }

    private OverlayState state;
    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle bodyStyle;
    private GUIStyle buttonStyle;

    private void Awake()
    {
        if (gameManager == null)
        {
            gameManager = FindAnyObjectByType<GameManager>();
        }

        SetState(OverlayState.Playing);
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
        ShouldHideGameplayHud = false;
        Cursor.visible = true;
    }

    private void Update()
    {
        if (gameManager == null)
        {
            gameManager = FindAnyObjectByType<GameManager>();
        }

        if (gameManager != null && gameManager.IsGameOver && state != OverlayState.Defeat)
        {
            SetState(OverlayState.Defeat);
            return;
        }

        if (state == OverlayState.Defeat)
        {
            return;
        }

        if (GetEscapePressedThisFrame())
        {
            if (state == OverlayState.Paused)
            {
                SetState(OverlayState.Playing);
            }
            else if (state == OverlayState.Playing)
            {
                SetState(OverlayState.Paused);
            }
        }
    }

    private static bool GetEscapePressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Escape);
#else
        return false;
#endif
    }

    private void SetState(OverlayState nextState)
    {
        state = nextState;
        bool gameplayActive = state == OverlayState.Playing;

        Time.timeScale = gameplayActive ? 1f : 0f;
        ShouldHideGameplayHud = !gameplayActive;
        Cursor.visible = !gameplayActive;
    }

    private void RestartLevel()
    {
        Time.timeScale = 1f;
        Scene active = SceneManager.GetActiveScene();
        SceneManager.LoadScene(active.buildIndex);
    }

    private void ReturnToMainMenu()
    {
        Time.timeScale = 1f;
        if (!string.IsNullOrWhiteSpace(mainMenuSceneName) && Application.CanStreamedLevelBeLoaded(mainMenuSceneName))
        {
            SceneManager.LoadScene(mainMenuSceneName);
            return;
        }

        Debug.LogWarning($"Main menu scene '{mainMenuSceneName}' is not in Build Settings. Loading scene index 0.");
        SceneManager.LoadScene(0);
    }

    private void OnGUI()
    {
        EnsureStyles();

        if (state == OverlayState.Paused)
        {
            DrawPauseMenu();
        }
        else if (state == OverlayState.Defeat)
        {
            DrawDefeatMenu();
        }
    }

    private void EnsureStyles()
    {
        if (titleStyle != null)
        {
            return;
        }

        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 34,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        titleStyle.normal.textColor = new Color(1f, 0.58f, 0.64f, 1f);

        subtitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Italic,
            alignment = TextAnchor.MiddleCenter
        };
        subtitleStyle.normal.textColor = new Color(0.95f, 0.90f, 0.92f, 0.95f);

        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            alignment = TextAnchor.MiddleLeft
        };
        bodyStyle.normal.textColor = new Color(0.90f, 0.93f, 1f, 0.95f);

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 17,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
    }

    private static Rect CenterRect(float width, float height)
    {
        return new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
    }

    private void DrawPauseMenu()
    {
        DrawScreenFade(0.62f);
        Rect panel = CenterRect(340f, 270f);
        DrawPanel(panel, new Color(0.05f, 0.07f, 0.12f, 0.90f), new Color(0.58f, 0.66f, 0.86f, 0.52f));

        Rect area = new Rect(panel.x + 16f, panel.y + 14f, panel.width - 32f, panel.height - 24f);
        GUILayout.BeginArea(area);
        GUILayout.Label("Pausa", titleStyle);
        GUILayout.Space(16f);

        if (GUILayout.Button("Continuar", buttonStyle, GUILayout.Height(38f)))
        {
            SetState(OverlayState.Playing);
        }

        GUILayout.Space(8f);
        if (GUILayout.Button("Reiniciar", buttonStyle, GUILayout.Height(38f)))
        {
            RestartLevel();
        }

        GUILayout.Space(8f);
        if (GUILayout.Button("Menu Principal", buttonStyle, GUILayout.Height(38f)))
        {
            ReturnToMainMenu();
        }

        GUILayout.EndArea();
    }

    private void DrawDefeatMenu()
    {
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * defeatPulseSpeed);
        DrawScreenFade(0.70f);
        DrawDefeatBackdrop(pulse);

        Rect panel = CenterRect(470f, 360f);
        DrawPanel(panel, new Color(0.07f, 0.03f, 0.06f, 0.92f), new Color(0.94f, 0.42f, 0.55f, 0.65f));

        Rect area = new Rect(panel.x + 20f, panel.y + 14f, panel.width - 40f, panel.height - 24f);
        GUILayout.BeginArea(area);
        GUILayout.Label(defeatTitle, titleStyle);
        GUILayout.Space(2f);
        GUILayout.Label(defeatSubtitle, subtitleStyle);
        GUILayout.Space(16f);

        string level = gameManager != null ? gameManager.CurrentLevelTypeLabel : "Unknown";
        float time = gameManager != null ? gameManager.SurvivalTime : 0f;
        float threat = gameManager != null ? gameManager.DifficultyMultiplier : 1f;

        GUILayout.Label($"Tiempo sobrevivido: {time:F1}s", bodyStyle);
        GUILayout.Label($"Nivel de amenaza final: x{threat:F2}", bodyStyle);
        GUILayout.Label($"Zona de contencion: {level}", bodyStyle);

        GUILayout.Space(24f);

        if (GUILayout.Button("Reiniciar", buttonStyle, GUILayout.Height(40f)))
        {
            RestartLevel();
        }

        GUILayout.Space(10f);
        if (GUILayout.Button("Menu Principal", buttonStyle, GUILayout.Height(40f)))
        {
            ReturnToMainMenu();
        }

        GUILayout.EndArea();
    }

    private static void DrawDefeatBackdrop(float pulse)
    {
        Color ring = new Color(0.98f, 0.45f, 0.60f, 0.18f + pulse * 0.10f);
        Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        DrawEllipseRing(center, Screen.width * 0.18f, Screen.height * 0.20f, ring);
        DrawEllipseRing(center, Screen.width * 0.30f, Screen.height * 0.33f, ring);

        Color stripe = new Color(1f, 0.28f, 0.42f, 0.11f + pulse * 0.06f);
        float spacing = 90f;
        for (float y = 0f; y < Screen.height + spacing; y += spacing)
        {
            DrawSolidRect(new Rect(-40f, y, Screen.width + 80f, 8f), stripe);
        }
    }

    private static void DrawEllipseRing(Vector2 center, float radiusX, float radiusY, Color color)
    {
        const int segments = 110;
        for (int i = 0; i < segments; i += 2)
        {
            float t = i / (float)segments * Mathf.PI * 2f;
            float x = center.x + Mathf.Cos(t) * radiusX;
            float y = center.y + Mathf.Sin(t) * radiusY;
            DrawSolidRect(new Rect(x, y, 3f, 3f), color);
        }
    }

    private static void DrawScreenFade(float alpha)
    {
        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = old;
    }

    private static void DrawPanel(Rect rect, Color fill, Color border)
    {
        DrawSolidRect(rect, fill);
        DrawSolidRect(new Rect(rect.x, rect.y, rect.width, 2f), border);
        DrawSolidRect(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), border);
        DrawSolidRect(new Rect(rect.x, rect.y, 2f, rect.height), border);
        DrawSolidRect(new Rect(rect.xMax - 2f, rect.y, 2f, rect.height), border);
    }

    private static void DrawSolidRect(Rect rect, Color color)
    {
        Color old = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = old;
    }
}

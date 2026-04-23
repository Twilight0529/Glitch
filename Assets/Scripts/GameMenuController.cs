using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class GameMenuController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameManager gameManager;

    [Header("Scene Flow")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    public static bool ShouldHideGameplayHud { get; private set; }

    private bool isPaused;

    private void Awake()
    {
        if (gameManager == null)
        {
            gameManager = FindAnyObjectByType<GameManager>();
        }

        SetPaused(false);
    }

    private void OnDestroy()
    {
        // Prevent a stuck paused timescale when object is destroyed on scene changes.
        Time.timeScale = 1f;
        ShouldHideGameplayHud = false;
    }

    private void Update()
    {
        if (GetEscapePressedThisFrame())
        {
            SetPaused(!isPaused);
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

    private void SetPaused(bool paused)
    {
        isPaused = paused;
        Time.timeScale = isPaused ? 0f : 1f;
        ShouldHideGameplayHud = isPaused;
        Cursor.visible = isPaused;
    }

    private void RestartLevel()
    {
        SetPaused(false);
        Scene active = SceneManager.GetActiveScene();
        SceneManager.LoadScene(active.buildIndex);
    }

    private void ReturnToMainMenu()
    {
        SetPaused(false);
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
        if (isPaused)
        {
            DrawPauseMenu();
        }
    }

    private static Rect CenterRect(float width, float height)
    {
        return new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
    }

    private void DrawPauseMenu()
    {
        DrawScreenFade(0.62f);
        Rect panel = CenterRect(320f, 260f);
        GUILayout.BeginArea(panel, GUI.skin.box);
        GUILayout.Space(8f);
        GUILayout.Label("Pausa");
        GUILayout.Space(18f);

        if (GUILayout.Button("Continuar", GUILayout.Height(36f)))
        {
            SetPaused(false);
        }

        GUILayout.Space(8f);
        if (GUILayout.Button("Reiniciar", GUILayout.Height(36f)))
        {
            RestartLevel();
        }

        GUILayout.Space(8f);
        if (GUILayout.Button("Menu Principal", GUILayout.Height(36f)))
        {
            ReturnToMainMenu();
        }

        GUILayout.EndArea();
    }

    private static void DrawScreenFade(float alpha)
    {
        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = old;
    }
}

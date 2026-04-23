using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    [Header("Main Menu")]
    [SerializeField] private string gameTitle = "GLITCH";
    [SerializeField] private string gameSubtitle = "Containment Breach";
    [SerializeField] private string gameplaySceneName = "Game";

    [Header("Options (Generic)")]
    [SerializeField] private float masterVolume = 0.8f;
    [SerializeField] private float uiScale = 1f;

    private bool showOptions;

    private void Awake()
    {
        Time.timeScale = 1f;
        Cursor.visible = true;
    }

    private void OnGUI()
    {
        if (showOptions)
        {
            DrawOptionsMenu();
            return;
        }

        DrawMainMenu();
    }

    private static Rect CenterRect(float width, float height)
    {
        return new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
    }

    private void DrawMainMenu()
    {
        DrawScreenFade(0.72f);
        Rect panel = CenterRect(340f, 320f);
        GUILayout.BeginArea(panel, GUI.skin.box);
        GUILayout.Space(8f);
        GUILayout.Label(gameTitle);
        GUILayout.Label(gameSubtitle);
        GUILayout.Space(18f);

        if (GUILayout.Button("Jugar", GUILayout.Height(40f)))
        {
            StartGameplay();
        }

        GUILayout.Space(8f);
        if (GUILayout.Button("Opciones", GUILayout.Height(36f)))
        {
            showOptions = true;
        }

        GUILayout.Space(8f);
        if (GUILayout.Button("Salir", GUILayout.Height(36f)))
        {
            ExitGame();
        }

        GUILayout.EndArea();
    }

    private void DrawOptionsMenu()
    {
        DrawScreenFade(0.78f);
        Rect panel = CenterRect(380f, 340f);
        GUILayout.BeginArea(panel, GUI.skin.box);
        GUILayout.Space(8f);
        GUILayout.Label("Opciones");
        GUILayout.Space(10f);
        GUILayout.Label("Ajustes genericos (placeholder)");
        GUILayout.Space(10f);

        GUILayout.Label($"Volumen Maestro: {masterVolume:F2}");
        masterVolume = GUILayout.HorizontalSlider(masterVolume, 0f, 1f);

        GUILayout.Space(10f);
        GUILayout.Label($"Escala UI: {uiScale:F2}");
        uiScale = GUILayout.HorizontalSlider(uiScale, 0.8f, 1.2f);

        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Volver", GUILayout.Height(36f)))
        {
            showOptions = false;
        }

        GUILayout.EndArea();
    }

    private void StartGameplay()
    {
        if (!string.IsNullOrWhiteSpace(gameplaySceneName) && Application.CanStreamedLevelBeLoaded(gameplaySceneName))
        {
            SceneManager.LoadScene(gameplaySceneName);
            return;
        }

        Debug.LogError($"Gameplay scene '{gameplaySceneName}' is not in Build Settings.");
    }

    private static void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static void DrawScreenFade(float alpha)
    {
        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, Mathf.Clamp01(alpha));
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = old;
    }
}

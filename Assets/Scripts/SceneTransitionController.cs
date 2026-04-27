using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneTransitionController : MonoBehaviour
{
    [Header("Fade")]
    [SerializeField] private float fadeOutDuration = 1.15f;
    [SerializeField] private float fadeInDuration = 1.15f;
    [SerializeField] private float loadedSceneHoldDuration = 0.12f;
    [SerializeField] private Color fadeColor = Color.black;

    private static SceneTransitionController instance;
    private Coroutine transitionRoutine;
    private Coroutine entryFadeRoutine;
    private float overlayAlpha;
    private CanvasGroup overlayCanvasGroup;

    public static bool IsTransitioning => instance != null && instance.transitionRoutine != null;
    public static bool IsFading => instance != null && (instance.transitionRoutine != null || instance.entryFadeRoutine != null || instance.overlayAlpha > 0.001f);
    public static float OverlayAlpha => instance != null ? instance.overlayAlpha : 0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    public static void LoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("SceneTransitionController.LoadScene called with empty scene name.");
            return;
        }

        EnsureInstance();
        instance.BeginTransitionToScene(sceneName, null);
    }

    public static void LoadScene(int buildIndex)
    {
        EnsureInstance();
        instance.BeginTransitionToScene(null, buildIndex);
    }

    public static void ReloadActiveScene()
    {
        Scene active = SceneManager.GetActiveScene();
        LoadScene(active.buildIndex);
    }

    private static void EnsureInstance()
    {
        if (instance != null)
        {
            return;
        }

        GameObject go = new GameObject("SceneTransitionController");
        instance = go.AddComponent<SceneTransitionController>();
        // Start black so the first loaded scene can reveal with fade-out.
        instance.overlayAlpha = 1f;
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureOverlayCanvas();
        ApplyOverlayAlpha(overlayAlpha);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        // Safety net for the very first scene in case sceneLoaded timing is skipped.
        if (transitionRoutine == null && entryFadeRoutine == null && overlayAlpha > 0.001f)
        {
            StartEntryFade(forceBlack: false);
        }
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // If scene was loaded externally (not via this controller), force an entry reveal.
        if (transitionRoutine == null)
        {
            StartEntryFade(forceBlack: true);
        }
    }

    private void BeginTransitionToScene(string sceneName, int? buildIndex)
    {
        if (transitionRoutine != null)
        {
            return;
        }

        transitionRoutine = StartCoroutine(TransitionRoutine(sceneName, buildIndex));
    }

    private IEnumerator TransitionRoutine(string sceneName, int? buildIndex)
    {
        yield return FadeTo(1f, fadeOutDuration);

        AsyncOperation loadOperation = buildIndex.HasValue
            ? SceneManager.LoadSceneAsync(buildIndex.Value)
            : SceneManager.LoadSceneAsync(sceneName);

        while (!loadOperation.isDone)
        {
            yield return null;
        }

        if (loadedSceneHoldDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(loadedSceneHoldDuration);
        }

        // Let the new scene render for a frame before revealing it.
        yield return null;
        StartEntryFade(forceBlack: false);
        while (entryFadeRoutine != null)
        {
            yield return null;
        }

        transitionRoutine = null;
    }

    private void StartEntryFade(bool forceBlack)
    {
        if (forceBlack)
        {
            ApplyOverlayAlpha(1f);
        }

        if (entryFadeRoutine != null)
        {
            StopCoroutine(entryFadeRoutine);
        }

        entryFadeRoutine = StartCoroutine(EntryFadeRoutine());
    }

    private IEnumerator EntryFadeRoutine()
    {
        yield return FadeTo(0f, fadeInDuration);
        entryFadeRoutine = null;
    }

    private IEnumerator FadeTo(float targetAlpha, float duration)
    {
        float startAlpha = overlayAlpha;
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);

        while (elapsed < safeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            ApplyOverlayAlpha(Mathf.Lerp(startAlpha, targetAlpha, elapsed / safeDuration));
            yield return null;
        }

        ApplyOverlayAlpha(targetAlpha);
    }

    private void EnsureOverlayCanvas()
    {
        if (overlayCanvasGroup != null)
        {
            return;
        }

        GameObject root = new GameObject("__SceneFadeCanvas");
        root.transform.SetParent(transform, false);

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        root.AddComponent<CanvasScaler>();
        root.AddComponent<GraphicRaycaster>();

        GameObject imageGo = new GameObject("Fade");
        imageGo.transform.SetParent(root.transform, false);
        RectTransform rt = imageGo.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image image = imageGo.AddComponent<Image>();
        image.color = fadeColor;
        image.raycastTarget = true;

        overlayCanvasGroup = imageGo.AddComponent<CanvasGroup>();
        overlayCanvasGroup.alpha = overlayAlpha;
        overlayCanvasGroup.interactable = false;
        overlayCanvasGroup.blocksRaycasts = overlayAlpha > 0.001f;
    }

    private void ApplyOverlayAlpha(float alpha)
    {
        overlayAlpha = Mathf.Clamp01(alpha);
        EnsureOverlayCanvas();

        if (overlayCanvasGroup != null)
        {
            overlayCanvasGroup.alpha = overlayAlpha;
            overlayCanvasGroup.blocksRaycasts = overlayAlpha > 0.001f;
        }
    }
}

using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneTransitionController : MonoBehaviour
{
    private enum TransitionTheme
    {
        Signal,
        Lab,
        Storage,
        Rupture,
        Core,
        Archive
    }

    // Instancia unica persistente que oscurece la pantalla al cargar o recargar escenas.
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
    private Image fadeImage;
    private readonly Image[] motifImages = new Image[6];
    private readonly RectTransform[] motifRects = new RectTransform[6];
    private TransitionTheme transitionTheme = TransitionTheme.Signal;

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
        instance.transitionTheme = ResolveTheme(sceneName);
        instance.BeginTransitionToScene(sceneName, null);
    }

    public static void LoadScene(int buildIndex)
    {
        EnsureInstance();
        string path = SceneUtility.GetScenePathByBuildIndex(buildIndex);
        instance.transitionTheme = ResolveTheme(path);
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
        // Arranca en negro para que la primera escena aparezca con fundido de salida.
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
        // Respaldo para la primera escena si se saltea el aviso de escena cargada.
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
        ProceduralArenaGenerator arena = FindAnyObjectByType<ProceduralArenaGenerator>();
        if (arena != null)
        {
            transitionTheme = ResolveTheme(arena.ActiveTheme);
        }
        // Si la escena se cargo por fuera de este controlador, fuerza el fundido de entrada.
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

        // Deja que la escena nueva renderice un fotograma antes de mostrarla.
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

        fadeImage = imageGo.AddComponent<Image>();
        fadeImage.color = fadeColor;
        fadeImage.raycastTarget = true;

        overlayCanvasGroup = imageGo.AddComponent<CanvasGroup>();
        overlayCanvasGroup.alpha = overlayAlpha;
        overlayCanvasGroup.interactable = false;
        overlayCanvasGroup.blocksRaycasts = overlayAlpha > 0.001f;

        for (int i = 0; i < motifImages.Length; i++)
        {
            GameObject motif = new GameObject($"TransitionSignal_{i:00}");
            motif.transform.SetParent(imageGo.transform, false);
            RectTransform motifRect = motif.AddComponent<RectTransform>();
            motifRect.anchorMin = new Vector2(0.5f, 0.5f);
            motifRect.anchorMax = new Vector2(0.5f, 0.5f);
            motifRect.pivot = new Vector2(0.5f, 0.5f);
            Image motifImage = motif.AddComponent<Image>();
            motifImage.raycastTarget = false;
            motifImage.color = Color.clear;
            motifRects[i] = motifRect;
            motifImages[i] = motifImage;
        }
    }

    private void ApplyOverlayAlpha(float alpha)
    {
        overlayAlpha = Mathf.Clamp01(alpha);
        EnsureOverlayCanvas();

        if (overlayCanvasGroup != null)
        {
            overlayCanvasGroup.alpha = overlayAlpha;
            overlayCanvasGroup.blocksRaycasts = overlayAlpha > 0.001f;
            UpdateTransitionMotif();
        }
    }

    private static TransitionTheme ResolveTheme(string sceneNameOrPath)
    {
        string value = string.IsNullOrWhiteSpace(sceneNameOrPath) ? string.Empty : sceneNameOrPath.ToLowerInvariant();
        if (value.Contains("storage")) return TransitionTheme.Storage;
        if (value.Contains("rupture")) return TransitionTheme.Rupture;
        if (value.Contains("core")) return TransitionTheme.Core;
        if (value.Contains("archive")) return TransitionTheme.Archive;
        if (value.Contains("game") || value.Contains("level") || value.Contains("lab")) return TransitionTheme.Lab;
        return TransitionTheme.Signal;
    }

    private static TransitionTheme ResolveTheme(ProceduralArenaGenerator.ArenaTheme theme)
    {
        switch (theme)
        {
            case ProceduralArenaGenerator.ArenaTheme.StorageBay: return TransitionTheme.Storage;
            case ProceduralArenaGenerator.ArenaTheme.RuptureZone: return TransitionTheme.Rupture;
            case ProceduralArenaGenerator.ArenaTheme.DataCore: return TransitionTheme.Core;
            case ProceduralArenaGenerator.ArenaTheme.NullArchive: return TransitionTheme.Archive;
            default: return TransitionTheme.Lab;
        }
    }

    private void UpdateTransitionMotif()
    {
        if (fadeImage == null)
        {
            return;
        }

        Color accent = GetThemeAccent(transitionTheme);
        fadeImage.color = Color.Lerp(fadeColor, new Color(accent.r * 0.18f, accent.g * 0.18f, accent.b * 0.18f, 1f), 0.22f);
        float width = Mathf.Max(320f, Screen.width);
        float height = Mathf.Max(180f, Screen.height);
        float clock = Time.unscaledTime;
        float visibility = Mathf.SmoothStep(0f, 1f, overlayAlpha) * 0.72f;

        for (int i = 0; i < motifImages.Length; i++)
        {
            RectTransform rect = motifRects[i];
            Image image = motifImages[i];
            if (rect == null || image == null) continue;

            float fraction = (i + 0.5f) / motifImages.Length;
            float pulse = 0.62f + 0.38f * Mathf.Sin(clock * 5.5f + i * 1.7f);
            rect.localRotation = Quaternion.identity;

            switch (transitionTheme)
            {
                case TransitionTheme.Storage:
                    rect.sizeDelta = new Vector2(Mathf.Lerp(3f, 10f, pulse), height * 1.15f);
                    rect.anchoredPosition = new Vector2(Mathf.Lerp(-width * 0.48f, width * 0.48f, fraction), 0f);
                    break;
                case TransitionTheme.Rupture:
                    rect.sizeDelta = new Vector2(width * Mathf.Lerp(0.34f, 0.72f, pulse), Mathf.Lerp(2f, 7f, pulse));
                    rect.anchoredPosition = new Vector2(Mathf.Sin(clock * 1.7f + i) * 55f, Mathf.Lerp(-height * 0.46f, height * 0.46f, fraction));
                    rect.localRotation = Quaternion.Euler(0f, 0f, i % 2 == 0 ? 24f : -31f);
                    break;
                case TransitionTheme.Core:
                    rect.sizeDelta = i == 0
                        ? new Vector2(width * 1.1f, Mathf.Lerp(2f, 8f, pulse))
                        : new Vector2(Mathf.Lerp(2f, 5f, pulse), height * 1.1f);
                    rect.anchoredPosition = i == 0
                        ? new Vector2(0f, Mathf.Lerp(-height * 0.5f, height * 0.5f, Mathf.Repeat(clock * 0.72f, 1f)))
                        : new Vector2(Mathf.Lerp(-width * 0.42f, width * 0.42f, fraction), 0f);
                    break;
                case TransitionTheme.Archive:
                {
                    float angle = fraction * Mathf.PI * 2f + clock * 0.35f;
                    float radius = Mathf.Lerp(34f, Mathf.Min(width, height) * 0.32f, 1f - overlayAlpha * 0.58f);
                    rect.sizeDelta = new Vector2(Mathf.Lerp(70f, 210f, pulse), 3f);
                    rect.anchoredPosition = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    rect.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
                    break;
                }
                case TransitionTheme.Lab:
                    rect.sizeDelta = new Vector2(width * Mathf.Lerp(0.48f, 0.94f, overlayAlpha), Mathf.Lerp(2f, 6f, pulse));
                    rect.anchoredPosition = new Vector2(i % 2 == 0 ? -width * 0.10f : width * 0.10f, Mathf.Lerp(-height * 0.44f, height * 0.44f, fraction));
                    break;
                default:
                    rect.sizeDelta = new Vector2(width * Mathf.Lerp(0.20f, 0.62f, pulse), 2f);
                    rect.anchoredPosition = new Vector2(Mathf.Sin(clock + i) * width * 0.12f, Mathf.Lerp(-height * 0.44f, height * 0.44f, fraction));
                    break;
            }

            image.color = new Color(accent.r, accent.g, accent.b, visibility * Mathf.Lerp(0.20f, 0.62f, pulse));
        }
    }

    private static Color GetThemeAccent(TransitionTheme theme)
    {
        switch (theme)
        {
            case TransitionTheme.Storage: return new Color(1f, 0.72f, 0.32f, 1f);
            case TransitionTheme.Rupture: return GlitchUiPalette.Special;
            case TransitionTheme.Core: return GlitchUiPalette.Success;
            case TransitionTheme.Archive: return new Color(0.48f, 0.62f, 1f, 1f);
            case TransitionTheme.Lab: return GlitchUiPalette.Information;
            default: return GlitchUiPalette.Neutral;
        }
    }
}

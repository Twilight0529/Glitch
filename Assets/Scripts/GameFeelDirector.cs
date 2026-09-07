using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Punto unico para impactos de camara y respuesta ambiental. Los callers solo
// expresan la importancia del evento; este componente decide la intensidad.
[DefaultExecutionOrder(1000)]
public sealed class GameFeelDirector : MonoBehaviour
{
    private const int AmbientParticleCount = 14;
    private static GameFeelDirector instance;

    private Camera gameplayCamera;
    private GameManager gameManager;
    private PlayerController player;
    private EnemyController enemy;
    private Vector3 appliedCameraOffset;
    private float appliedZoomOffset;
    private float shakeRemaining;
    private float shakeDuration;
    private float shakeStrength;
    private Vector2 shakeDirection;
    private float zoomRemaining;
    private float zoomDuration;
    private float zoomStrength;
    private float environmentPulse;
    private Vector2 pulseOrigin;
    private Color pulseColor = Color.white;
    private readonly Transform[] ambientParticles = new Transform[AmbientParticleCount];
    private readonly SpriteRenderer[] ambientRenderers = new SpriteRenderer[AmbientParticleCount];
    private readonly Vector2[] ambientVelocities = new Vector2[AmbientParticleCount];
    private readonly float[] ambientSeeds = new float[AmbientParticleCount];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<GameManager>() == null)
        {
            return;
        }

        Ensure();
    }

    public static void Minor(Vector2 position, Vector2 direction, Color color)
    {
        GameFeelDirector director = Ensure();
        if (director == null) return;
        director.AddImpulse(0.035f, 0.09f, direction, 0f, 0f);
        director.EmitEnvironmentPulse(position, color, 0.35f);
    }

    public static void Major(Vector2 position, Vector2 direction, Color color, float scale = 1f)
    {
        GameFeelDirector director = Ensure();
        if (director == null) return;
        float safeScale = Mathf.Clamp(scale, 0.35f, 1.5f);
        director.AddImpulse(0.11f * safeScale, 0.18f, direction, 0.12f * safeScale, 0.22f);
        director.EmitEnvironmentPulse(position, color, 0.72f * safeScale);
    }

    public static void Critical(Vector2 position, Vector2 direction, Color color, float scale = 1f)
    {
        GameFeelDirector director = Ensure();
        if (director == null) return;
        float safeScale = Mathf.Clamp(scale, 0.5f, 1.6f);
        director.AddImpulse(0.18f * safeScale, 0.27f, direction, 0.22f * safeScale, 0.32f);
        director.EmitEnvironmentPulse(position, color, safeScale);
        director.TryHitStop(0.030f);
    }

    public static void StateShift(Vector2 position, Color color)
    {
        GameFeelDirector director = Ensure();
        if (director == null) return;
        director.AddImpulse(0.045f, 0.13f, Vector2.zero, 0.055f, 0.18f);
        director.EmitEnvironmentPulse(position, color, 0.52f);
    }

    private static GameFeelDirector Ensure()
    {
        if (instance != null)
        {
            return instance;
        }

        if (FindAnyObjectByType<GameManager>() == null)
        {
            return null;
        }

        GameObject root = new GameObject("__GameFeelDirector");
        instance = root.AddComponent<GameFeelDirector>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        gameManager = FindAnyObjectByType<GameManager>();
        gameplayCamera = Camera.main;
        CreateAmbientParticles();
    }

    private void OnDestroy()
    {
        RestoreCameraOffsets();
        if (instance == this) instance = null;
    }

    private void AddImpulse(float strength, float duration, Vector2 direction, float zoom, float zoomTime)
    {
        shakeStrength = Mathf.Max(shakeStrength, Mathf.Max(0f, strength));
        shakeDuration = Mathf.Max(shakeDuration, Mathf.Max(0.02f, duration));
        shakeRemaining = Mathf.Max(shakeRemaining, shakeDuration);
        shakeDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.zero;
        zoomStrength = Mathf.Max(zoomStrength, Mathf.Max(0f, zoom));
        zoomDuration = Mathf.Max(zoomDuration, Mathf.Max(0.02f, zoomTime));
        zoomRemaining = Mathf.Max(zoomRemaining, zoomDuration);
    }

    private void EmitEnvironmentPulse(Vector2 position, Color color, float strength)
    {
        pulseOrigin = position;
        pulseColor = color;
        environmentPulse = Mathf.Max(environmentPulse, Mathf.Clamp(strength, 0.1f, 1.5f));

        GameObject wave = new GameObject("EnvironmentResponseWave");
        wave.transform.position = position;
        SpriteRenderer renderer = wave.AddComponent<SpriteRenderer>();
        renderer.sprite = CircleSpriteProvider.Get();
        renderer.color = new Color(color.r, color.g, color.b, 0.18f);
        renderer.sortingOrder = -4;
        wave.transform.localScale = Vector3.one * 0.22f;
        wave.AddComponent<GameFeelPulseFx>().Configure(renderer, Mathf.Lerp(1.2f, 3.8f, Mathf.Clamp01(strength)), 0.48f, color);
        Destroy(wave, 0.58f);
    }

    private void TryHitStop(float duration)
    {
        if (Time.timeScale < 0.5f || gameManager == null || gameManager.IsGameOver)
        {
            return;
        }

        StartCoroutine(HitStopRoutine(duration));
    }

    private static IEnumerator HitStopRoutine(float duration)
    {
        float previous = Time.timeScale;
        Time.timeScale = 0.06f;
        yield return new WaitForSecondsRealtime(Mathf.Max(0.01f, duration));
        if (Mathf.Approximately(Time.timeScale, 0.06f))
        {
            Time.timeScale = previous;
        }
    }

    private void LateUpdate()
    {
        if (gameplayCamera == null) gameplayCamera = Camera.main;
        if (player == null) player = FindAnyObjectByType<PlayerController>();
        if (enemy == null) enemy = FindAnyObjectByType<EnemyController>();

        UpdateCameraFeedback();
        UpdateAmbientParticles();
        environmentPulse = Mathf.MoveTowards(environmentPulse, 0f, Time.unscaledDeltaTime * 2.8f);
    }

    private void RestoreCameraOffsets()
    {
        if (gameplayCamera == null) return;
        gameplayCamera.transform.localPosition -= appliedCameraOffset;
        gameplayCamera.orthographicSize = Mathf.Max(0.1f, gameplayCamera.orthographicSize - appliedZoomOffset);
        appliedCameraOffset = Vector3.zero;
        appliedZoomOffset = 0f;
    }

    private void UpdateCameraFeedback()
    {
        if (gameplayCamera == null) return;

        RestoreCameraOffsets();
        float dt = Time.unscaledDeltaTime;
        if (shakeRemaining > 0f)
        {
            shakeRemaining = Mathf.Max(0f, shakeRemaining - dt);
            float envelope = shakeDuration > 0f ? Mathf.Pow(shakeRemaining / shakeDuration, 1.6f) : 0f;
            float clock = Time.unscaledTime * 83f;
            Vector2 noise = new Vector2(Mathf.Sin(clock * 1.13f), Mathf.Sin(clock * 1.71f + 1.9f));
            Vector2 directional = shakeDirection.sqrMagnitude > 0.001f
                ? shakeDirection * Mathf.Sin(clock * 0.83f) * 0.55f
                : Vector2.zero;
            appliedCameraOffset = (noise * 0.55f + directional) * shakeStrength * envelope;
            gameplayCamera.transform.localPosition += appliedCameraOffset;
        }

        if (zoomRemaining > 0f && (gameManager == null || !gameManager.IsLocalVersus))
        {
            zoomRemaining = Mathf.Max(0f, zoomRemaining - dt);
            float normalized = zoomDuration > 0f ? zoomRemaining / zoomDuration : 0f;
            appliedZoomOffset = -Mathf.Sin(normalized * Mathf.PI) * zoomStrength;
            gameplayCamera.orthographicSize = Mathf.Max(0.1f, gameplayCamera.orthographicSize + appliedZoomOffset);
        }
    }

    private void CreateAmbientParticles()
    {
        for (int i = 0; i < AmbientParticleCount; i++)
        {
            GameObject particle = new GameObject($"ReactiveSignal_{i:00}");
            particle.transform.SetParent(transform, false);
            SpriteRenderer renderer = particle.AddComponent<SpriteRenderer>();
            renderer.sprite = SquareSpriteProvider.Get();
            renderer.sortingOrder = -18;
            renderer.color = Color.clear;
            float seed = i * 0.731f + 0.17f;
            particle.transform.localScale = new Vector3(Mathf.Lerp(0.035f, 0.085f, Mathf.PerlinNoise(seed, 1.2f)), 0.025f, 1f);
            ambientParticles[i] = particle.transform;
            ambientRenderers[i] = renderer;
            ambientSeeds[i] = seed;
            ambientVelocities[i] = new Vector2(Mathf.Sin(seed * 7.1f), Mathf.Cos(seed * 5.3f)) * 0.12f;
        }
    }

    private void UpdateAmbientParticles()
    {
        if (gameplayCamera == null || !gameplayCamera.orthographic) return;

        float halfHeight = gameplayCamera.orthographicSize;
        float halfWidth = halfHeight * gameplayCamera.aspect;
        Vector2 center = gameplayCamera.transform.position;
        Vector2 min = center - new Vector2(halfWidth, halfHeight);
        Vector2 max = center + new Vector2(halfWidth, halfHeight);
        float dt = Time.unscaledDeltaTime;
        Vector2 playerPosition = player != null ? player.GetPosition() : center;
        Vector2 enemyPosition = enemy != null ? enemy.GetCurrentPosition() : center;
        bool dash = player != null && player.IsGhostDashing;

        for (int i = 0; i < ambientParticles.Length; i++)
        {
            Transform particle = ambientParticles[i];
            SpriteRenderer renderer = ambientRenderers[i];
            if (particle == null || renderer == null) continue;

            if (particle.position == Vector3.zero)
            {
                float x = Mathf.Lerp(min.x, max.x, Mathf.PerlinNoise(ambientSeeds[i], 2.3f));
                float y = Mathf.Lerp(min.y, max.y, Mathf.PerlinNoise(ambientSeeds[i], 7.9f));
                particle.position = new Vector3(x, y, 0f);
            }

            Vector2 position = particle.position;
            Vector2 velocity = ambientVelocities[i];
            float enemyDistance = Vector2.Distance(position, enemyPosition);
            float enemyResponse = Mathf.InverseLerp(4.8f, 0.5f, enemyDistance);
            if (dash)
            {
                Vector2 away = position - playerPosition;
                float distance = away.magnitude;
                if (distance < 2.4f && distance > 0.01f)
                {
                    velocity += away / distance * (1f - distance / 2.4f) * dt * 5.2f;
                }
            }

            if (environmentPulse > 0f)
            {
                Vector2 away = position - pulseOrigin;
                float distance = away.magnitude;
                if (distance < 4.5f && distance > 0.01f)
                {
                    velocity += away / distance * (1f - distance / 4.5f) * environmentPulse * dt * 4.5f;
                }
            }

            velocity = Vector2.Lerp(velocity, new Vector2(Mathf.Sin(ambientSeeds[i] * 7.1f), Mathf.Cos(ambientSeeds[i] * 5.3f)) * 0.12f, dt * 0.8f);
            position += velocity * dt;
            if (position.x < min.x) position.x = max.x;
            else if (position.x > max.x) position.x = min.x;
            if (position.y < min.y) position.y = max.y;
            else if (position.y > max.y) position.y = min.y;
            particle.position = new Vector3(position.x, position.y, 0f);
            ambientVelocities[i] = velocity;

            float flicker = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (2.2f + i * 0.11f) + ambientSeeds[i] * 9f);
            Color tint = environmentPulse > 0.05f ? pulseColor : GlitchUiPalette.Information;
            float alpha = 0.025f + flicker * 0.035f + enemyResponse * 0.075f + environmentPulse * 0.045f;
            renderer.color = new Color(tint.r, tint.g, tint.b, Mathf.Clamp01(alpha));
            particle.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg);
        }
    }
}

public sealed class GameFeelPulseFx : MonoBehaviour
{
    private SpriteRenderer rendererRef;
    private float radius;
    private float lifetime;
    private Color color;
    private float age;

    public void Configure(SpriteRenderer target, float maxRadius, float duration, Color tint)
    {
        rendererRef = target;
        radius = Mathf.Max(0.2f, maxRadius);
        lifetime = Mathf.Max(0.08f, duration);
        color = tint;
    }

    private void Update()
    {
        if (rendererRef == null)
        {
            Destroy(gameObject);
            return;
        }

        age += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(age / lifetime);
        float eased = 1f - Mathf.Pow(1f - t, 3f);
        transform.localScale = Vector3.one * Mathf.Lerp(0.22f, radius, eased);
        rendererRef.color = new Color(color.r, color.g, color.b, (1f - t) * 0.16f);
        if (t >= 1f) Destroy(gameObject);
    }
}

using System.Collections.Generic;
using UnityEngine;

public class ArenaBreachSweepFx : MonoBehaviour
{
    // Desintegra visualmente la arena por capas, desde el lado opuesto hasta la brecha.
    [SerializeField] private int stripCount = 7;
    [SerializeField] private int fragmentCount = 28;
    [SerializeField] private float dissolveWindow = 0.34f;

    private sealed class RendererSlice
    {
        public SpriteRenderer renderer;
        public Color baseColor;
        public float startT;
        public float noiseSeed;
    }

    private SpriteRenderer guideRenderer;
    private readonly SpriteRenderer[] guideStrips = new SpriteRenderer[10];
    private readonly SpriteRenderer[] fragments = new SpriteRenderer[32];
    private readonly float[] fragmentSeeds = new float[32];
    private RendererSlice[] slices;
    private Vector2 startPosition;
    private Vector2 endPosition;
    private Vector2 direction = Vector2.right;
    private Vector2 perpendicular = Vector2.up;
    private Vector2 arenaSize = new Vector2(32f, 18f);
    private Color tint = Color.magenta;
    private float duration = 8f;
    private float age;
    private bool restoreOnDestroy = true;

    public void Configure(ProceduralArenaGenerator arena, Vector2 breachPosition, Vector2 sweepDirection, float seconds, Color color)
    {
        if (arena != null)
        {
            arenaSize = new Vector2(arena.ArenaWidth, arena.ArenaHeight);
        }

        direction = sweepDirection.sqrMagnitude > 0.001f ? sweepDirection.normalized : Vector2.right;
        perpendicular = new Vector2(-direction.y, direction.x);
        tint = color;
        duration = Mathf.Max(0.1f, seconds);

        float travel = Mathf.Abs(direction.x) > 0.5f ? arenaSize.x : arenaSize.y;
        startPosition = breachPosition - direction * travel;
        endPosition = breachPosition;

        CaptureArenaSlices(arena);
        CreateGuideVisuals();
    }

    public void DisableRestoreOnDestroy()
    {
        restoreOnDestroy = false;
    }

    public void RestoreAndDestroy()
    {
        RestoreArena();
        restoreOnDestroy = false;
        Destroy(gameObject);
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / duration);
        float sweepT = Mathf.SmoothStep(0f, 1f, t);
        Vector2 guidePosition = Vector2.Lerp(startPosition, endPosition, sweepT);
        transform.position = guidePosition;

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 10f);
        float guideAlpha = Mathf.Sin(t * Mathf.PI) * 0.28f;
        if (guideRenderer != null)
        {
            guideRenderer.color = new Color(tint.r, tint.g, tint.b, guideAlpha);
        }

        UpdateGuideStrips(guideAlpha, pulse);
        UpdateFragments(t, pulse);
        ApplySlowDisintegration(t, pulse);

        if (age >= duration)
        {
            enabled = false;
        }
    }

    private void CaptureArenaSlices(ProceduralArenaGenerator arena)
    {
        if (arena == null)
        {
            slices = new RendererSlice[0];
            return;
        }

        SpriteRenderer[] allRenderers = arena.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
        List<SpriteRenderer> renderers = new List<SpriteRenderer>(allRenderers.Length);
        for (int i = 0; i < allRenderers.Length; i++)
        {
            if (ShouldCaptureRenderer(allRenderers[i], arena.transform))
            {
                renderers.Add(allRenderers[i]);
            }
        }

        slices = new RendererSlice[renderers.Count];

        float minProjection = float.MaxValue;
        float maxProjection = float.MinValue;
        float[] projections = new float[renderers.Count];

        for (int i = 0; i < renderers.Count; i++)
        {
            SpriteRenderer sr = renderers[i];
            float projection = sr != null ? Vector2.Dot((Vector2)sr.transform.position, direction) : 0f;
            projections[i] = projection;
            minProjection = Mathf.Min(minProjection, projection);
            maxProjection = Mathf.Max(maxProjection, projection);
        }

        float range = Mathf.Max(0.001f, maxProjection - minProjection);
        for (int i = 0; i < renderers.Count; i++)
        {
            SpriteRenderer sr = renderers[i];
            float normalized = Mathf.Clamp01((projections[i] - minProjection) / range);
            float noise = sr != null ? Mathf.PerlinNoise(sr.transform.position.x * 0.21f, sr.transform.position.y * 0.21f) : 0f;

            slices[i] = new RendererSlice
            {
                renderer = sr,
                baseColor = sr != null ? sr.color : Color.white,
                startT = Mathf.Clamp01(normalized * (1f - dissolveWindow) + noise * 0.08f),
                noiseSeed = noise * 31f + i * 0.137f
            };
        }
    }

    private bool ShouldCaptureRenderer(SpriteRenderer sr, Transform arenaRoot)
    {
        if (sr == null || arenaRoot == null)
        {
            return false;
        }

        if (sr.transform == transform || sr.transform.IsChildOf(transform))
        {
            return false;
        }

        Transform directChild = sr.transform;
        while (directChild.parent != null && directChild.parent != arenaRoot)
        {
            directChild = directChild.parent;
        }

        if (directChild.parent != arenaRoot)
        {
            return false;
        }

        string rootName = directChild.name;
        return rootName == "Bounds" ||
               rootName == "Obstacles" ||
               rootName == "DynamicObstacles" ||
               rootName == "Details";
    }

    private void CreateGuideVisuals()
    {
        guideRenderer = gameObject.AddComponent<SpriteRenderer>();
        guideRenderer.sprite = SquareSpriteProvider.Get();
        guideRenderer.drawMode = SpriteDrawMode.Sliced;
        guideRenderer.sortingOrder = 18;
        guideRenderer.color = new Color(tint.r, tint.g, tint.b, 0f);

        bool horizontal = Mathf.Abs(direction.x) > 0.5f;
        guideRenderer.size = horizontal
            ? new Vector2(0.55f, arenaSize.y + 3f)
            : new Vector2(arenaSize.x + 3f, 0.55f);

        int count = Mathf.Min(guideStrips.Length, Mathf.Max(2, stripCount));
        for (int i = 0; i < count; i++)
        {
            GameObject strip = new GameObject($"BreachDissolveGuide_{i}");
            strip.transform.SetParent(transform, false);
            SpriteRenderer sr = strip.AddComponent<SpriteRenderer>();
            sr.sprite = SquareSpriteProvider.Get();
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.sortingOrder = 19;
            guideStrips[i] = sr;
        }

        int glitchFragments = Mathf.Min(fragments.Length, Mathf.Max(8, fragmentCount));
        for (int i = 0; i < glitchFragments; i++)
        {
            GameObject fragment = new GameObject($"BreachPixelTear_{i}");
            fragment.transform.SetParent(transform, false);
            SpriteRenderer sr = fragment.AddComponent<SpriteRenderer>();
            sr.sprite = SquareSpriteProvider.Get();
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.sortingOrder = 20;
            sr.color = new Color(tint.r, tint.g, tint.b, 0f);
            fragments[i] = sr;
            fragmentSeeds[i] = Random.Range(0.1f, 100f);
        }
    }

    private void UpdateGuideStrips(float alpha, float pulse)
    {
        float span = Mathf.Abs(direction.x) > 0.5f ? arenaSize.y + 2f : arenaSize.x + 2f;
        for (int i = 0; i < guideStrips.Length; i++)
        {
            SpriteRenderer sr = guideStrips[i];
            if (sr == null)
            {
                continue;
            }

            float n = (i + 0.5f) / guideStrips.Length;
            float offset = Mathf.Lerp(-span * 0.48f, span * 0.48f, n);
            float jitter = Mathf.Sin(Time.time * (5f + i) + i * 1.9f) * 0.16f;
            sr.transform.localPosition = perpendicular * (offset + jitter);

            bool horizontal = Mathf.Abs(direction.x) > 0.5f;
            sr.size = horizontal
                ? new Vector2(0.55f, Mathf.Lerp(0.06f, 0.16f, pulse))
                : new Vector2(Mathf.Lerp(0.06f, 0.16f, pulse), 0.55f);
            sr.color = new Color(1f, 0.78f, 0.96f, alpha * Mathf.Lerp(0.3f, 0.9f, pulse));
        }
    }

    private void UpdateFragments(float globalT, float pulse)
    {
        float span = Mathf.Abs(direction.x) > 0.5f ? arenaSize.y + 2f : arenaSize.x + 2f;
        float lifeAlpha = Mathf.Sin(globalT * Mathf.PI);
        bool horizontal = Mathf.Abs(direction.x) > 0.5f;

        for (int i = 0; i < fragments.Length; i++)
        {
            SpriteRenderer sr = fragments[i];
            if (sr == null)
            {
                continue;
            }

            float seed = fragmentSeeds[i];
            float flow = Mathf.Repeat(seed * 0.173f + Time.time * Mathf.Lerp(0.18f, 0.38f, Mathf.PerlinNoise(seed, 1.7f)), 1f);
            float lateral = Mathf.Lerp(-span * 0.5f, span * 0.5f, flow);
            float trail = Mathf.Lerp(0.15f, 1.45f, Mathf.PerlinNoise(seed, Time.time * 1.6f));
            float jitter = Mathf.Sin(Time.time * (9f + seed * 0.08f) + seed) * 0.18f;

            sr.transform.localPosition = (Vector3)(perpendicular * (lateral + jitter) - direction * trail);
            sr.size = horizontal
                ? new Vector2(Mathf.Lerp(0.08f, 0.36f, pulse), Mathf.Lerp(0.04f, 0.16f, flow))
                : new Vector2(Mathf.Lerp(0.04f, 0.16f, flow), Mathf.Lerp(0.08f, 0.36f, pulse));

            float alpha = lifeAlpha * Mathf.Lerp(0.12f, 0.55f, Mathf.PerlinNoise(seed + 9.4f, Time.time * 5.5f));
            sr.color = new Color(1f, Mathf.Lerp(0.45f, 0.82f, pulse), 0.98f, alpha);
        }
    }

    private void ApplySlowDisintegration(float globalT, float pulse)
    {
        if (slices == null)
        {
            return;
        }

        for (int i = 0; i < slices.Length; i++)
        {
            RendererSlice slice = slices[i];
            if (slice == null || slice.renderer == null)
            {
                continue;
            }

            float localT = Mathf.Clamp01((globalT - slice.startT) / Mathf.Max(0.02f, dissolveWindow));
            if (localT <= 0f)
            {
                slice.renderer.color = slice.baseColor;
                continue;
            }

            float glitchPulse = Mathf.PerlinNoise(slice.noiseSeed, Time.time * 4.2f);
            float flicker = Mathf.PerlinNoise(slice.noiseSeed + 12.4f, Time.time * 11f);
            float stepped = Mathf.Floor(localT * 7f) / 7f;
            float alpha = slice.baseColor.a * Mathf.Lerp(1f, 0.08f + flicker * 0.18f, stepped);
            if (globalT > 0.88f)
            {
                float finalFade = Mathf.InverseLerp(0.88f, 1f, globalT);
                alpha = Mathf.Lerp(alpha, slice.baseColor.a * 0.03f, finalFade);
            }

            Color glitched = Color.Lerp(slice.baseColor, tint, Mathf.Clamp01(0.22f + localT * 0.68f + glitchPulse * 0.22f));
            glitched.a = alpha;
            slice.renderer.color = glitched;
        }
    }

    private void OnDestroy()
    {
        if (restoreOnDestroy)
        {
            RestoreArena();
        }
    }

    private void RestoreArena()
    {
        if (slices == null)
        {
            return;
        }

        for (int i = 0; i < slices.Length; i++)
        {
            RendererSlice slice = slices[i];
            if (slice == null || slice.renderer == null)
            {
                continue;
            }

            slice.renderer.color = slice.baseColor;
        }
    }
}

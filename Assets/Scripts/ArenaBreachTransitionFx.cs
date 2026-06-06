using UnityEngine;

public class ArenaBreachTransitionFx : MonoBehaviour
{
    // Cubre el salto entre sectores con una ruptura visual world-space.
    private SpriteRenderer blackoutRenderer;
    private SpriteRenderer flashRenderer;
    private SpriteRenderer coreRenderer;
    private readonly SpriteRenderer[] bars = new SpriteRenderer[14];
    private readonly SpriteRenderer[] shutters = new SpriteRenderer[4];
    private readonly SpriteRenderer[] shards = new SpriteRenderer[28];
    private readonly float[] shardSeeds = new float[28];
    private Vector2 arenaSize = new Vector2(32f, 18f);
    private Color tint = Color.magenta;
    private float duration = 0.8f;
    private float age;

    public void Configure(ProceduralArenaGenerator arena, float seconds, Color color)
    {
        if (arena != null)
        {
            arenaSize = new Vector2(arena.ArenaWidth, arena.ArenaHeight);
        }

        duration = Mathf.Max(0.1f, seconds);
        tint = color;
        CreateVisuals();
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / duration);
        float close = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.38f));
        float open = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.62f) / 0.38f));
        float cover = close * (1f - open);
        float impact = Mathf.Sin(t * Mathf.PI);
        float noise = 0.5f + 0.5f * Mathf.Sin(Time.time * 26f);

        if (blackoutRenderer != null)
        {
            blackoutRenderer.color = new Color(0f, 0f, 0.01f, Mathf.Lerp(0.1f, 0.92f, cover));
        }

        if (flashRenderer != null)
        {
            flashRenderer.color = new Color(tint.r, tint.g, tint.b, Mathf.Lerp(0.08f, 0.48f, impact) * (0.5f + cover * 0.8f));
        }

        if (coreRenderer != null)
        {
            coreRenderer.transform.rotation = Quaternion.Euler(0f, 0f, Time.time * 140f);
            coreRenderer.transform.localScale = Vector3.one * Mathf.Lerp(0.8f, 2.7f, impact);
            coreRenderer.color = new Color(1f, 0.9f, 1f, Mathf.Lerp(0.15f, 0.72f, impact) * (1f - Mathf.Abs(t - 0.5f)));
        }

        UpdateShutters(cover, noise);
        UpdateShards(cover, impact, noise);

        for (int i = 0; i < bars.Length; i++)
        {
            SpriteRenderer bar = bars[i];
            if (bar == null)
            {
                continue;
            }

            float y = Mathf.Lerp(-arenaSize.y * 0.5f, arenaSize.y * 0.5f, (i + 0.5f) / bars.Length);
            float drift = Mathf.Sin(Time.time * (10f + i) + i) * Mathf.Lerp(0.2f, 1.0f, cover);
            bar.transform.localPosition = new Vector3(drift, y, -0.01f);
            bar.size = new Vector2(arenaSize.x * Mathf.Lerp(0.45f, 1.18f, cover), Mathf.Lerp(0.04f, 0.22f, noise));
            bar.color = new Color(1f, 0.78f, 0.96f, Mathf.Lerp(0.04f, 0.44f, cover) * Mathf.Lerp(0.55f, 1f, noise));
        }

        if (age >= duration)
        {
            Destroy(gameObject);
        }
    }

    private void CreateVisuals()
    {
        blackoutRenderer = CreateRenderer("BreachTransitionBlackout", 29);
        blackoutRenderer.size = arenaSize + Vector2.one * 6f;
        blackoutRenderer.color = new Color(0f, 0f, 0f, 0f);

        flashRenderer = gameObject.AddComponent<SpriteRenderer>();
        flashRenderer.sprite = SquareSpriteProvider.Get();
        flashRenderer.drawMode = SpriteDrawMode.Sliced;
        flashRenderer.size = arenaSize + Vector2.one * 4f;
        flashRenderer.sortingOrder = 30;
        flashRenderer.color = new Color(tint.r, tint.g, tint.b, 0f);

        coreRenderer = CreateRenderer("BreachTransitionCore", 33);
        coreRenderer.sprite = CircleSpriteProvider.Get();
        coreRenderer.drawMode = SpriteDrawMode.Sliced;
        coreRenderer.size = Vector2.one * 2.2f;

        for (int i = 0; i < bars.Length; i++)
        {
            GameObject barGo = new GameObject($"BreachTransitionBar_{i}");
            barGo.transform.SetParent(transform, false);
            SpriteRenderer bar = barGo.AddComponent<SpriteRenderer>();
            bar.sprite = SquareSpriteProvider.Get();
            bar.drawMode = SpriteDrawMode.Sliced;
            bar.sortingOrder = 31;
            bars[i] = bar;
        }

        for (int i = 0; i < shutters.Length; i++)
        {
            SpriteRenderer shutter = CreateRenderer($"BreachTransitionShutter_{i}", 32);
            shutters[i] = shutter;
        }

        for (int i = 0; i < shards.Length; i++)
        {
            SpriteRenderer shard = CreateRenderer($"BreachTransitionShard_{i}", 34);
            shards[i] = shard;
            shardSeeds[i] = Random.Range(0.1f, 100f);
        }
    }

    private SpriteRenderer CreateRenderer(string objectName, int sortingOrder)
    {
        GameObject go = new GameObject(objectName);
        go.transform.SetParent(transform, false);
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SquareSpriteProvider.Get();
        sr.drawMode = SpriteDrawMode.Sliced;
        sr.sortingOrder = sortingOrder;
        sr.color = Color.clear;
        return sr;
    }

    private void UpdateShutters(float cover, float noise)
    {
        if (shutters.Length < 4)
        {
            return;
        }

        float width = arenaSize.x + 5f;
        float height = arenaSize.y + 5f;
        float horizontalWidth = Mathf.Lerp(0f, width * 0.54f, cover);
        float verticalHeight = Mathf.Lerp(0f, height * 0.54f, cover);

        ConfigureShutter(shutters[0], new Vector3(-width * 0.5f + horizontalWidth * 0.5f, 0f, 0f), new Vector2(horizontalWidth, height), noise);
        ConfigureShutter(shutters[1], new Vector3(width * 0.5f - horizontalWidth * 0.5f, 0f, 0f), new Vector2(horizontalWidth, height), noise);
        ConfigureShutter(shutters[2], new Vector3(0f, height * 0.5f - verticalHeight * 0.5f, 0f), new Vector2(width, verticalHeight), noise);
        ConfigureShutter(shutters[3], new Vector3(0f, -height * 0.5f + verticalHeight * 0.5f, 0f), new Vector2(width, verticalHeight), noise);
    }

    private void ConfigureShutter(SpriteRenderer shutter, Vector3 localPosition, Vector2 size, float noise)
    {
        if (shutter == null)
        {
            return;
        }

        shutter.transform.localPosition = localPosition;
        shutter.size = size;
        shutter.color = new Color(0f, 0f, Mathf.Lerp(0.01f, 0.04f, noise), Mathf.Lerp(0.35f, 0.86f, noise));
    }

    private void UpdateShards(float cover, float impact, float noise)
    {
        float maxRadius = Mathf.Max(arenaSize.x, arenaSize.y) * 0.58f;
        for (int i = 0; i < shards.Length; i++)
        {
            SpriteRenderer shard = shards[i];
            if (shard == null)
            {
                continue;
            }

            float seed = shardSeeds[i];
            float angle = seed * 17.13f;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float radius = Mathf.Lerp(0.2f, maxRadius, Mathf.Repeat(seed * 0.137f + impact * 0.65f, 1f));
            float lateral = Mathf.Sin(Time.time * (6f + seed * 0.04f)) * 0.45f;
            Vector2 tangent = new Vector2(-dir.y, dir.x);
            shard.transform.localPosition = (Vector3)(dir * radius + tangent * lateral);
            shard.transform.rotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg + Time.time * Mathf.Lerp(-120f, 120f, Mathf.PerlinNoise(seed, 2.1f)));
            shard.size = new Vector2(Mathf.Lerp(0.08f, 0.5f, noise), Mathf.Lerp(0.03f, 0.12f, Mathf.PerlinNoise(seed, Time.time * 5f)));
            shard.color = new Color(1f, Mathf.Lerp(0.44f, 0.82f, noise), 1f, cover * Mathf.Lerp(0.08f, 0.55f, Mathf.PerlinNoise(seed + 8.4f, Time.time * 4.5f)));
        }
    }
}

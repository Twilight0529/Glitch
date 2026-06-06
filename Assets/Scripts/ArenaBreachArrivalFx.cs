using UnityEngine;

public class ArenaBreachArrivalFx : MonoBehaviour
{
    // Marca la salida del jugador al sector nuevo para unir visualmente el portal con la arena regenerada.
    private SpriteRenderer ringRenderer;
    private SpriteRenderer haloRenderer;
    private SpriteRenderer coreRenderer;
    private SpriteRenderer wakeRenderer;
    private SpriteRenderer stabilizerRenderer;
    private readonly SpriteRenderer[] phaseRings = new SpriteRenderer[3];
    private readonly SpriteRenderer[] scanBands = new SpriteRenderer[8];
    private readonly SpriteRenderer[] rays = new SpriteRenderer[14];
    private readonly float[] raySeeds = new float[14];
    private Color tint = Color.magenta;
    private Vector2 entryDirection = Vector2.right;
    private Vector2 perpendicular = Vector2.up;
    private float duration = 1.15f;
    private float age;

    public void Configure(Vector2 position, Vector2 direction, Color color, float seconds)
    {
        transform.position = position;
        entryDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
        perpendicular = new Vector2(-entryDirection.y, entryDirection.x);
        tint = color;
        duration = Mathf.Max(0.25f, seconds);
        CreateVisuals();
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / duration);
        float appear = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.28f));
        float fade = 1f - Mathf.SmoothStep(0.68f, 1f, t);
        float held = appear * fade;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 18f);

        if (ringRenderer != null)
        {
            ringRenderer.transform.localScale = Vector3.one * Mathf.Lerp(0.55f, 3.25f, Mathf.SmoothStep(0f, 1f, t));
            ringRenderer.color = new Color(tint.r, tint.g, tint.b, held * Mathf.Lerp(0.54f, 0.9f, pulse));
        }

        if (haloRenderer != null)
        {
            haloRenderer.transform.localScale = Vector3.one * Mathf.Lerp(0.95f, 1.35f, pulse);
            haloRenderer.color = new Color(tint.r, tint.g, tint.b, held * 0.22f);
        }

        if (coreRenderer != null)
        {
            coreRenderer.transform.rotation = Quaternion.Euler(0f, 0f, Time.time * 140f);
            coreRenderer.transform.localScale = Vector3.one * Mathf.Lerp(0.44f, 0.72f, pulse) * Mathf.Lerp(1.3f, 1f, appear);
            coreRenderer.color = new Color(1f, 0.9f, 1f, held * Mathf.Lerp(0.58f, 0.95f, pulse));
        }

        if (wakeRenderer != null)
        {
            wakeRenderer.transform.localPosition = -entryDirection * Mathf.Lerp(0.25f, 1.85f, appear);
            wakeRenderer.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(entryDirection.y, entryDirection.x) * Mathf.Rad2Deg);
            wakeRenderer.size = new Vector2(Mathf.Lerp(0.6f, 3.1f, appear), Mathf.Lerp(1.45f, 0.38f, appear));
            wakeRenderer.color = new Color(tint.r, tint.g, tint.b, held * 0.52f);
        }

        if (stabilizerRenderer != null)
        {
            stabilizerRenderer.transform.rotation = Quaternion.Euler(0f, 0f, Time.time * -70f);
            stabilizerRenderer.transform.localScale = Vector3.one * Mathf.Lerp(1.65f, 2.15f, pulse);
            stabilizerRenderer.color = new Color(1f, 0.82f, 0.98f, held * Mathf.Lerp(0.12f, 0.28f, pulse));
        }

        UpdatePhaseRings(t, held, pulse);
        UpdateScanBands(t, held, pulse);

        float rayFade = held * Mathf.Lerp(0.45f, 1f, pulse);
        float raySpread = Mathf.Lerp(0.45f, 1.15f, appear);
        for (int i = 0; i < rays.Length; i++)
        {
            SpriteRenderer ray = rays[i];
            if (ray == null)
            {
                continue;
            }

            float seed = raySeeds[i];
            float side = Mathf.Lerp(-1.45f, 1.45f, i / (float)Mathf.Max(1, rays.Length - 1));
            float forward = Mathf.Lerp(-0.45f, 2.7f, appear) + Mathf.Sin(Time.time * (5f + seed * 0.03f)) * 0.18f;
            ray.transform.localPosition = (Vector3)(entryDirection * forward + perpendicular * side * raySpread);
            ray.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(entryDirection.y, entryDirection.x) * Mathf.Rad2Deg);
            ray.transform.localScale = new Vector3(Mathf.Lerp(0.82f, 0.16f, t), Mathf.Lerp(0.05f, 0.16f, pulse), 1f);
            ray.color = new Color(1f, Mathf.Lerp(0.56f, 0.9f, pulse), 1f, rayFade * Mathf.Lerp(0.18f, 0.72f, Mathf.PerlinNoise(seed, Time.time * 4f)));
        }

        if (age >= duration)
        {
            Destroy(gameObject);
        }
    }

    private void CreateVisuals()
    {
        ringRenderer = CreateRenderer("ArrivalRing", 35);
        ringRenderer.sprite = CircleSpriteProvider.Get();
        ringRenderer.drawMode = SpriteDrawMode.Sliced;
        ringRenderer.size = Vector2.one * 1.4f;

        haloRenderer = CreateRenderer("ArrivalHalo", 33);
        haloRenderer.sprite = CircleSpriteProvider.Get();
        haloRenderer.drawMode = SpriteDrawMode.Sliced;
        haloRenderer.size = Vector2.one * 2.4f;

        coreRenderer = CreateRenderer("ArrivalCore", 36);
        coreRenderer.size = Vector2.one * 0.5f;

        wakeRenderer = CreateRenderer("ArrivalWake", 34);
        wakeRenderer.drawMode = SpriteDrawMode.Sliced;

        stabilizerRenderer = CreateRenderer("ArrivalStabilizer", 35);
        stabilizerRenderer.size = Vector2.one * 1.4f;

        for (int i = 0; i < phaseRings.Length; i++)
        {
            phaseRings[i] = CreateRenderer($"ArrivalPhaseRing_{i}", 35);
            phaseRings[i].sprite = CircleSpriteProvider.Get();
            phaseRings[i].drawMode = SpriteDrawMode.Sliced;
            phaseRings[i].size = Vector2.one * 1.1f;
        }

        for (int i = 0; i < scanBands.Length; i++)
        {
            scanBands[i] = CreateRenderer($"ArrivalScanBand_{i}", 34);
            scanBands[i].drawMode = SpriteDrawMode.Sliced;
        }

        for (int i = 0; i < rays.Length; i++)
        {
            rays[i] = CreateRenderer($"ArrivalRay_{i}", 35);
            raySeeds[i] = Random.Range(0.1f, 100f);
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

    private void UpdatePhaseRings(float t, float held, float pulse)
    {
        for (int i = 0; i < phaseRings.Length; i++)
        {
            SpriteRenderer ring = phaseRings[i];
            if (ring == null)
            {
                continue;
            }

            float phase = Mathf.Repeat(t * 1.25f + i / (float)phaseRings.Length, 1f);
            float scale = Mathf.Lerp(0.75f, 2.65f, phase);
            ring.transform.localScale = Vector3.one * scale;
            ring.color = new Color(tint.r, tint.g, tint.b, held * Mathf.Lerp(0.32f, 0f, phase) * Mathf.Lerp(0.75f, 1.2f, pulse));
        }
    }

    private void UpdateScanBands(float t, float held, float pulse)
    {
        float angle = Mathf.Atan2(entryDirection.y, entryDirection.x) * Mathf.Rad2Deg;
        for (int i = 0; i < scanBands.Length; i++)
        {
            SpriteRenderer band = scanBands[i];
            if (band == null)
            {
                continue;
            }

            float phase = Mathf.Repeat(t * 1.6f + i / (float)scanBands.Length, 1f);
            float forward = Mathf.Lerp(-1.8f, 2.8f, phase);
            float side = Mathf.Sin((i + 1f) * 2.1f + Time.time * 4f) * 0.45f;
            band.transform.localPosition = (Vector3)(entryDirection * forward + perpendicular * side);
            band.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            band.size = new Vector2(Mathf.Lerp(0.22f, 1.35f, pulse), Mathf.Lerp(0.045f, 0.12f, 1f - phase));
            band.color = new Color(1f, 0.82f, 0.98f, held * Mathf.Lerp(0.28f, 0f, phase));
        }
    }
}

using UnityEngine;

public class ArenaBreachArrivalFx : MonoBehaviour
{
    // Marca la salida del jugador al sector nuevo para unir visualmente el portal con la arena regenerada.
    private SpriteRenderer ringRenderer;
    private SpriteRenderer coreRenderer;
    private SpriteRenderer wakeRenderer;
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
        float appear = 1f - Mathf.Pow(1f - t, 2f);
        float fade = 1f - t;
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 18f);

        if (ringRenderer != null)
        {
            ringRenderer.transform.localScale = Vector3.one * Mathf.Lerp(0.35f, 2.8f, appear);
            ringRenderer.color = new Color(tint.r, tint.g, tint.b, Mathf.Lerp(0.78f, 0f, t));
        }

        if (coreRenderer != null)
        {
            coreRenderer.transform.rotation = Quaternion.Euler(0f, 0f, Time.time * 140f);
            coreRenderer.transform.localScale = Vector3.one * Mathf.Lerp(0.85f, 0.25f, appear);
            coreRenderer.color = new Color(1f, 0.9f, 1f, Mathf.Lerp(0.86f, 0f, t));
        }

        if (wakeRenderer != null)
        {
            wakeRenderer.transform.localPosition = -entryDirection * Mathf.Lerp(0.2f, 1.4f, appear);
            wakeRenderer.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(entryDirection.y, entryDirection.x) * Mathf.Rad2Deg);
            wakeRenderer.size = new Vector2(Mathf.Lerp(0.35f, 2.4f, appear), Mathf.Lerp(1.2f, 0.25f, appear));
            wakeRenderer.color = new Color(tint.r, tint.g, tint.b, Mathf.Lerp(0.62f, 0f, t));
        }

        for (int i = 0; i < rays.Length; i++)
        {
            SpriteRenderer ray = rays[i];
            if (ray == null)
            {
                continue;
            }

            float seed = raySeeds[i];
            float side = Mathf.Lerp(-1.45f, 1.45f, i / (float)Mathf.Max(1, rays.Length - 1));
            float forward = Mathf.Lerp(-0.35f, 2.2f, appear) + Mathf.Sin(Time.time * (5f + seed * 0.03f)) * 0.12f;
            ray.transform.localPosition = (Vector3)(entryDirection * forward + perpendicular * side * Mathf.Lerp(0.25f, 1f, appear));
            ray.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(entryDirection.y, entryDirection.x) * Mathf.Rad2Deg);
            ray.transform.localScale = new Vector3(Mathf.Lerp(0.55f, 0.08f, t), Mathf.Lerp(0.05f, 0.14f, pulse), 1f);
            ray.color = new Color(1f, Mathf.Lerp(0.56f, 0.9f, pulse), 1f, fade * Mathf.Lerp(0.18f, 0.72f, Mathf.PerlinNoise(seed, Time.time * 4f)));
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

        coreRenderer = CreateRenderer("ArrivalCore", 36);
        coreRenderer.size = Vector2.one * 0.5f;

        wakeRenderer = CreateRenderer("ArrivalWake", 34);
        wakeRenderer.drawMode = SpriteDrawMode.Sliced;

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
}

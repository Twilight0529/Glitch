using UnityEngine;

public class ArenaBreachTransitionFx : MonoBehaviour
{
    // Cubre el salto entre sectores con una ruptura visual world-space.
    private SpriteRenderer flashRenderer;
    private readonly SpriteRenderer[] bars = new SpriteRenderer[10];
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
        float pulse = Mathf.Sin(t * Mathf.PI);

        if (flashRenderer != null)
        {
            flashRenderer.color = new Color(tint.r, tint.g, tint.b, Mathf.Lerp(0.18f, 0.62f, pulse));
        }

        for (int i = 0; i < bars.Length; i++)
        {
            SpriteRenderer bar = bars[i];
            if (bar == null)
            {
                continue;
            }

            float y = Mathf.Lerp(-arenaSize.y * 0.5f, arenaSize.y * 0.5f, (i + 0.5f) / bars.Length);
            float drift = Mathf.Sin(Time.time * (8f + i) + i) * 0.5f * pulse;
            bar.transform.localPosition = new Vector3(drift, y, -0.01f);
            bar.size = new Vector2(arenaSize.x * Mathf.Lerp(0.3f, 1.15f, pulse), 0.08f + pulse * 0.18f);
            bar.color = new Color(1f, 0.78f, 0.96f, Mathf.Lerp(0.08f, 0.44f, pulse));
        }

        if (age >= duration)
        {
            Destroy(gameObject);
        }
    }

    private void CreateVisuals()
    {
        flashRenderer = gameObject.AddComponent<SpriteRenderer>();
        flashRenderer.sprite = SquareSpriteProvider.Get();
        flashRenderer.drawMode = SpriteDrawMode.Sliced;
        flashRenderer.size = arenaSize + Vector2.one * 4f;
        flashRenderer.sortingOrder = 30;
        flashRenderer.color = new Color(tint.r, tint.g, tint.b, 0f);

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
    }
}

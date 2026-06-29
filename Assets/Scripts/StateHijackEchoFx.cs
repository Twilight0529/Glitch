using UnityEngine;

// Fantasma visual del Echo Decoy. Parpadea como una señal corrupta y se apaga cuando termina el engaño.
public class StateHijackEchoFx : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private float duration = 1f;
    private float age;
    private Color color = Color.white;
    private Vector3 baseScale;
    private Vector3 origin;

    public void Configure(SpriteRenderer rendererRef, float lifetime, Color tint)
    {
        spriteRenderer = rendererRef;
        duration = Mathf.Max(0.1f, lifetime);
        color = tint;
        baseScale = transform.localScale;
        origin = transform.position;
        Destroy(gameObject, duration + 0.05f);
    }

    private void Update()
    {
        age += Time.deltaTime;
        float progress = Mathf.Clamp01(age / duration);
        float glitch = Mathf.Sin(Time.unscaledTime * 31f) * 0.035f;
        transform.position = origin + new Vector3(glitch, -glitch * 0.35f, 0f);
        transform.localScale = baseScale * Mathf.Lerp(1f, 0.76f, progress);

        if (spriteRenderer != null)
        {
            float flicker = 0.46f + 0.20f * Mathf.Sin(Time.unscaledTime * 19f);
            spriteRenderer.color = new Color(color.r, color.g, color.b, flicker * (1f - progress));
        }
    }
}

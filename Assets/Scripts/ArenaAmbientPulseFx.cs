using UnityEngine;

public class ArenaAmbientPulseFx : MonoBehaviour
{
    // Pulso visual liviano para que fondos, decals y detalles de arena respiren sin tocar gameplay.
    private SpriteRenderer spriteRenderer;
    private Color baseColor;
    private float minAlpha;
    private float maxAlpha;
    private float speed;
    private float phase;
    private Vector2 driftDirection;
    private float driftAmplitude;
    private Vector3 startLocalPosition;

    public void Configure(
        SpriteRenderer rendererRef,
        Color color,
        float minOpacity,
        float maxOpacity,
        float pulseSpeed,
        float pulsePhase,
        Vector2 drift,
        float driftAmount)
    {
        spriteRenderer = rendererRef;
        baseColor = color;
        minAlpha = Mathf.Clamp01(Mathf.Min(minOpacity, maxOpacity));
        maxAlpha = Mathf.Clamp01(Mathf.Max(minOpacity, maxOpacity));
        speed = Mathf.Max(0.01f, pulseSpeed);
        phase = pulsePhase;
        driftDirection = drift.sqrMagnitude > 0.001f ? drift.normalized : Vector2.zero;
        driftAmplitude = Mathf.Max(0f, driftAmount);
        startLocalPosition = transform.localPosition;
    }

    private void Awake()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        baseColor = spriteRenderer != null ? spriteRenderer.color : Color.white;
        startLocalPosition = transform.localPosition;
        minAlpha = minAlpha <= 0f && maxAlpha <= 0f ? baseColor.a * 0.45f : minAlpha;
        maxAlpha = maxAlpha <= 0f ? baseColor.a : maxAlpha;
        speed = speed <= 0f ? 0.8f : speed;
    }

    private void Update()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * speed + phase);
        Color color = baseColor;
        color.a = Mathf.Lerp(minAlpha, maxAlpha, pulse);
        spriteRenderer.color = color;

        if (driftAmplitude > 0f && driftDirection.sqrMagnitude > 0.001f)
        {
            Vector2 offset = driftDirection * (Mathf.Sin(Time.time * speed * 0.7f + phase) * driftAmplitude);
            transform.localPosition = startLocalPosition + new Vector3(offset.x, offset.y, 0f);
        }
    }
}

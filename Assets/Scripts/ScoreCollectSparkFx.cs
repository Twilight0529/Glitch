using UnityEngine;

// Una de las chispas que salen disparadas al juntar score. Solo sabe viajar y desaparecer.
public class ScoreCollectSparkFx : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Vector2 direction = Vector2.right;
    private float distance = 1f;
    private float lifetime = 0.22f;
    private float age;
    private Vector3 origin;

    public void Configure(SpriteRenderer rendererRef, Vector2 travelDirection, float travelDistance, float duration)
    {
        spriteRenderer = rendererRef;
        direction = travelDirection.sqrMagnitude > 0.001f ? travelDirection.normalized : Vector2.right;
        distance = Mathf.Max(0.1f, travelDistance);
        lifetime = Mathf.Max(0.08f, duration);
        origin = transform.position;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float progress = Mathf.Clamp01(age / lifetime);
        float easedProgress = 1f - Mathf.Pow(1f - progress, 2f);

        transform.position = origin + (Vector3)(direction * distance * easedProgress);
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        transform.localScale = new Vector3(
            Mathf.Lerp(0.12f, 0.02f, progress),
            Mathf.Lerp(0.05f, 0.02f, progress),
            1f);

        if (spriteRenderer != null)
        {
            Color color = spriteRenderer.color;
            color.a = 1f - progress;
            spriteRenderer.color = color;
        }
    }
}

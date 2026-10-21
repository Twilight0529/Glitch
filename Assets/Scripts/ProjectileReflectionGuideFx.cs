using UnityEngine;

// A brief, world-sized arrow makes the reflected direction visible even when the
// projectile's own sprite is tiny. It follows subsequent steering, not a guessed aim.
public sealed class ProjectileReflectionGuideFx : MonoBehaviour
{
    private const int MaximumVisibleGuides = 4;
    private const float Lifetime = 0.28f;
    private static int liveGuides;
    private readonly SpriteRenderer[] wings = new SpriteRenderer[2];
    private AnomalyProjectile projectile;
    private float age;
    private bool registered;

    public static void TryShow(AnomalyProjectile target)
    {
        if (target == null || liveGuides >= MaximumVisibleGuides) return;
        GameObject root = new GameObject("ReflectedProjectileDirection");
        ProjectileReflectionGuideFx guide = root.AddComponent<ProjectileReflectionGuideFx>();
        guide.projectile = target;
        guide.registered = true;
        liveGuides++;
        for (int i = 0; i < guide.wings.Length; i++)
        {
            GameObject part = new GameObject("DirectionWing_" + i);
            part.transform.SetParent(root.transform, false);
            SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
            renderer.sprite = SquareSpriteProvider.Get();
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = new Vector2(0.3f, 0.065f);
            renderer.sortingOrder = 18;
            guide.wings[i] = renderer;
        }
        guide.UpdateDirection();
    }

    private void LateUpdate()
    {
        age += Time.deltaTime;
        if (projectile == null || !projectile.isActiveAndEnabled || age >= Lifetime)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }
        UpdateDirection();
    }

    private void UpdateDirection()
    {
        if (projectile == null) return;
        Vector2 direction = projectile.CurrentVelocity.normalized;
        if (direction.sqrMagnitude < 0.001f) direction = Vector2.right;
        Vector2 side = new Vector2(-direction.y, direction.x);
        Vector2 position = (Vector2)projectile.transform.position + direction * 0.32f;
        transform.position = new Vector3(position.x, position.y, 0f);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        Color color = GlitchUiPalette.Success;
        color.a = 0.95f * Mathf.Clamp01((Lifetime - age) / 0.1f);
        for (int i = 0; i < wings.Length; i++)
        {
            float sign = i == 0 ? 1f : -1f;
            wings[i].transform.localPosition = side * (sign * 0.095f);
            wings[i].transform.localRotation = Quaternion.Euler(0f, 0f, angle - sign * 40f);
            wings[i].color = color;
        }
    }

    private void OnDisable()
    {
        if (!registered) return;
        registered = false;
        liveGuides = Mathf.Max(0, liveGuides - 1);
    }
}

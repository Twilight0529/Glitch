using UnityEngine;

public class RuptureRiftAnchorFx : MonoBehaviour
{
    // Ancla ambiental de Rupture: una grieta visual que anticipa materializacion de fragmentos.
    private SpriteRenderer coreRenderer;
    private SpriteRenderer haloRenderer;
    private readonly SpriteRenderer[] shardRenderers = new SpriteRenderer[14];

    private float duration = 4f;
    private float age;
    private float radius = 2.4f;
    private Color warningColor = Color.magenta;
    private Color activeColor = Color.cyan;

    public void Configure(float lifeSeconds, float visualRadius, Color warning, Color active)
    {
        duration = Mathf.Max(0.25f, lifeSeconds);
        radius = Mathf.Max(0.4f, visualRadius);
        warningColor = warning;
        activeColor = active;
        EnsureVisuals();
        UpdateVisuals(0f);
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= duration)
        {
            Destroy(gameObject);
            return;
        }

        UpdateVisuals(Mathf.Clamp01(age / duration));
    }

    private void EnsureVisuals()
    {
        if (coreRenderer == null)
        {
            coreRenderer = CreateRenderer("RiftCore", CircleSpriteProvider.Get(), 8);
        }
        if (haloRenderer == null)
        {
            haloRenderer = CreateRenderer("RiftHalo", CircleSpriteProvider.Get(), 7);
        }

        for (int i = 0; i < shardRenderers.Length; i++)
        {
            if (shardRenderers[i] == null)
            {
                shardRenderers[i] = CreateRenderer($"RiftShard_{i}", SquareSpriteProvider.Get(), 9);
            }
        }
    }

    private SpriteRenderer CreateRenderer(string childName, Sprite sprite, int sortingOrder)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(transform, false);
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.sortingOrder = sortingOrder;
        renderer.color = Color.clear;
        return renderer;
    }

    private void UpdateVisuals(float progress)
    {
        EnsureVisuals();

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4.6f);
        float envelope = Mathf.Sin(progress * Mathf.PI);
        Color color = Color.Lerp(warningColor, activeColor, Mathf.SmoothStep(0f, 1f, progress));
        color = Color.Lerp(color, Color.white, pulse * 0.12f);

        coreRenderer.transform.localPosition = Vector3.zero;
        coreRenderer.transform.localRotation = Quaternion.identity;
        coreRenderer.size = Vector2.one * Mathf.Lerp(0.24f, 0.46f, pulse);
        Color coreColor = color;
        coreColor.a = Mathf.Lerp(0.08f, 0.34f, envelope);
        coreRenderer.color = coreColor;

        haloRenderer.transform.localPosition = Vector3.zero;
        haloRenderer.transform.localRotation = Quaternion.identity;
        haloRenderer.size = Vector2.one * Mathf.Lerp(radius * 0.9f, radius * 1.28f, pulse);
        Color haloColor = color;
        haloColor.a = Mathf.Lerp(0.012f, 0.052f, envelope);
        haloRenderer.color = haloColor;

        for (int i = 0; i < shardRenderers.Length; i++)
        {
            SpriteRenderer shard = shardRenderers[i];
            if (shard == null)
            {
                continue;
            }

            float t = i / (float)shardRenderers.Length;
            float angle = t * Mathf.PI * 2f + Time.time * (0.18f + t * 0.11f);
            float armRadius = radius * Mathf.Lerp(0.32f, 0.95f, Mathf.PingPong(progress + t, 1f));
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            shard.transform.localPosition = dir * armRadius;
            shard.transform.localRotation = Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg + 90f);
            shard.size = new Vector2(Mathf.Lerp(0.07f, 0.18f, pulse), 0.028f);
            Color shardColor = color;
            shardColor.a = Mathf.Lerp(0.025f, 0.18f, envelope) * (0.55f + 0.45f * Mathf.PingPong(t + Time.time * 0.52f, 1f));
            shard.color = shardColor;
        }
    }
}

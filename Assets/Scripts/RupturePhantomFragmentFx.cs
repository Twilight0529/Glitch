using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class RupturePhantomFragmentFx : MonoBehaviour
{
    // Fragmento ambiental de Rupture: aparece como eco, se vuelve solido y se desintegra.
    private BoxCollider2D fragmentCollider;
    private Rigidbody2D fragmentRigidbody;
    private SpriteRenderer bodyRenderer;
    private SpriteRenderer echoRenderer;
    private readonly SpriteRenderer[] tearRenderers = new SpriteRenderer[8];

    private Vector2 fragmentSize = Vector2.one;
    private Color warningColor = Color.magenta;
    private Color activeColor = Color.cyan;
    private float duration = 5f;
    private float telegraphEndFraction = 0.2f;
    private float materializeEndFraction = 0.34f;
    private float dissolveStartFraction = 0.78f;
    private float age;
    private float baseRotationZ;
    private float jitterSeed;

    public void Configure(
        Vector2 size,
        float rotationZ,
        float telegraphSeconds,
        float materializeSeconds,
        float solidSeconds,
        float dissolveSeconds,
        Color warning,
        Color active)
    {
        fragmentSize = new Vector2(Mathf.Max(0.25f, size.x), Mathf.Max(0.25f, size.y));
        baseRotationZ = rotationZ;
        duration = Mathf.Max(0.5f, telegraphSeconds + materializeSeconds + solidSeconds + dissolveSeconds);
        telegraphEndFraction = Mathf.Clamp01(telegraphSeconds / duration);
        materializeEndFraction = Mathf.Clamp01((telegraphSeconds + materializeSeconds) / duration);
        dissolveStartFraction = Mathf.Clamp01((telegraphSeconds + materializeSeconds + solidSeconds) / duration);
        warningColor = warning;
        activeColor = active;
        jitterSeed = Random.Range(0f, 100f);
        EnsureVisuals();
        UpdateVisuals(0f);
    }

    private void Awake()
    {
        fragmentCollider = GetComponent<BoxCollider2D>();
        fragmentRigidbody = GetComponent<Rigidbody2D>();
        fragmentRigidbody.bodyType = RigidbodyType2D.Kinematic;
        fragmentRigidbody.gravityScale = 0f;
        fragmentCollider.isTrigger = false;
        fragmentCollider.enabled = false;
        EnsureVisuals();
    }

    private void Update()
    {
        age += Time.deltaTime;
        float progress = Mathf.Clamp01(age / Mathf.Max(0.001f, duration));
        if (progress >= 1f)
        {
            Destroy(gameObject);
            return;
        }

        UpdateVisuals(progress);
    }

    private void EnsureVisuals()
    {
        if (bodyRenderer == null)
        {
            bodyRenderer = GetComponent<SpriteRenderer>();
            if (bodyRenderer == null)
            {
                bodyRenderer = gameObject.AddComponent<SpriteRenderer>();
            }
        }

        bodyRenderer.sprite = SquareSpriteProvider.Get();
        bodyRenderer.drawMode = SpriteDrawMode.Sliced;
        bodyRenderer.sortingOrder = 13;

        if (echoRenderer == null)
        {
            echoRenderer = CreateRenderer("PhantomEcho", 12);
        }

        for (int i = 0; i < tearRenderers.Length; i++)
        {
            if (tearRenderers[i] == null)
            {
                tearRenderers[i] = CreateRenderer($"PhantomTear_{i}", 15);
            }
        }
    }

    private SpriteRenderer CreateRenderer(string childName, int sortingOrder)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(transform, false);
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = SquareSpriteProvider.Get();
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.sortingOrder = sortingOrder;
        renderer.color = Color.clear;
        return renderer;
    }

    private void UpdateVisuals(float progress)
    {
        EnsureVisuals();

        float materialize = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(telegraphEndFraction, materializeEndFraction, progress));
        float dissolve = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(dissolveStartFraction, 0.98f, progress));
        float solidAlpha = Mathf.Clamp01(materialize * (1f - dissolve));
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 10.5f + jitterSeed);
        float jitter = Mathf.Sin(Time.time * 15f + jitterSeed) * Mathf.Lerp(2.4f, 0.4f, solidAlpha);

        transform.rotation = Quaternion.Euler(0f, 0f, baseRotationZ + jitter);

        if (fragmentCollider != null)
        {
            fragmentCollider.size = fragmentSize;
            fragmentCollider.enabled = progress >= materializeEndFraction && progress < dissolveStartFraction;
        }

        Color color = Color.Lerp(warningColor, activeColor, materialize);
        Color bodyColor = Color.Lerp(color, new Color(0.07f, 0.06f, 0.10f, 1f), 0.38f);
        bodyColor.a = Mathf.Lerp(0.10f, 0.88f, solidAlpha);
        bodyRenderer.size = fragmentSize;
        bodyRenderer.color = bodyColor;

        echoRenderer.transform.localPosition = Vector3.zero;
        echoRenderer.transform.localRotation = Quaternion.identity;
        echoRenderer.size = fragmentSize + Vector2.one * Mathf.Lerp(0.22f, 0.58f, pulse);
        Color echoColor = color;
        echoColor.a = Mathf.Lerp(0.26f, 0.08f, solidAlpha) * Mathf.Clamp01(1f - dissolve * 0.7f);
        echoRenderer.color = echoColor;

        UpdateTears(color, progress, solidAlpha, pulse);
    }

    private void UpdateTears(Color color, float progress, float solidAlpha, float pulse)
    {
        for (int i = 0; i < tearRenderers.Length; i++)
        {
            SpriteRenderer tear = tearRenderers[i];
            if (tear == null)
            {
                continue;
            }

            float t = (i + 0.5f) / tearRenderers.Length;
            float side = (i & 1) == 0 ? -1f : 1f;
            float x = Mathf.Lerp(-fragmentSize.x * 0.45f, fragmentSize.x * 0.45f, t);
            float y = side * fragmentSize.y * Mathf.Lerp(0.18f, 0.55f, Mathf.PingPong(t + Time.time * 0.45f, 1f));
            tear.transform.localPosition = new Vector3(x, y, 0f);
            tear.transform.localRotation = Quaternion.Euler(0f, 0f, side * 90f + Mathf.Sin(Time.time * 8f + i) * 10f);
            tear.size = new Vector2(Mathf.Lerp(0.12f, 0.32f, pulse), 0.045f);
            Color tearColor = Color.Lerp(color, Color.white, pulse * 0.28f);
            float edgeEnvelope = Mathf.Max(1f - solidAlpha, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(dissolveStartFraction, 1f, progress)));
            tearColor.a = Mathf.Lerp(0.10f, 0.72f, edgeEnvelope) * (0.55f + 0.45f * pulse);
            tear.color = tearColor;
        }
    }
}

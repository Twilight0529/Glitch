using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class ArenaObjectiveNode : MonoBehaviour
{
    // Nodo de objetivo: se activa al mantener al jugador dentro durante unos segundos.
    [SerializeField] private float pulseSpeed = 8f;
    [SerializeField] private float idleScale = 0.9f;
    [SerializeField] private float activeScale = 1.15f;

    private ArenaChaosDirector owner;
    private SpriteRenderer spriteRenderer;
    private SpriteRenderer progressRenderer;
    private GameObject progressObject;
    private Color nodeColor = Color.cyan;
    private float activationSeconds = 1f;
    private float lifetime = 12f;
    private float lifeTimer;
    private float activationTimer;
    private bool playerInside;
    private bool activated;

    public void Configure(ArenaChaosDirector director, float secondsToActivate, float lifeSeconds, Color tint)
    {
        owner = director;
        activationSeconds = Mathf.Max(0.1f, secondsToActivate);
        lifetime = Mathf.Max(1f, lifeSeconds);
        nodeColor = tint;
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        EnsureProgressVisual();
    }

    private void Update()
    {
        if (activated)
        {
            return;
        }

        lifeTimer += Time.deltaTime;

        if (playerInside)
        {
            activationTimer += Time.deltaTime;
            if (activationTimer >= activationSeconds)
            {
                Activate();
                return;
            }
        }
        else
        {
            activationTimer = Mathf.Max(0f, activationTimer - Time.deltaTime * 1.6f);
        }

        UpdateVisuals();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other != null && other.GetComponent<PlayerController>() != null)
        {
            playerInside = true;
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other != null && other.GetComponent<PlayerController>() != null)
        {
            playerInside = false;
        }
    }

    private void EnsureProgressVisual()
    {
        progressObject = new GameObject("ObjectiveNodeProgress");
        progressObject.transform.SetParent(transform, false);
        progressObject.transform.localPosition = Vector3.zero;
        progressObject.transform.localScale = Vector3.one * 0.18f;

        progressRenderer = progressObject.AddComponent<SpriteRenderer>();
        progressRenderer.sprite = CircleSpriteProvider.Get();
        progressRenderer.drawMode = SpriteDrawMode.Sliced;
        progressRenderer.size = Vector2.one * 0.82f;
        progressRenderer.sortingOrder = 13;
    }

    private void UpdateVisuals()
    {
        float progress = Mathf.Clamp01(activationTimer / Mathf.Max(0.01f, activationSeconds));
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed);
        float lifeWarn = 1f - Mathf.Clamp01(lifeTimer / Mathf.Max(0.01f, lifetime));
        float warnPulse = lifeWarn < 0.28f ? 0.45f + 0.55f * Mathf.Sin(Time.time * 18f) : 1f;

        if (spriteRenderer != null)
        {
            float alpha = Mathf.Lerp(0.42f, 0.92f, playerInside ? 1f : pulse) * warnPulse;
            spriteRenderer.color = new Color(nodeColor.r, nodeColor.g, nodeColor.b, alpha);
        }

        transform.localScale = Vector3.one * Mathf.Lerp(idleScale, activeScale, Mathf.Max(progress, pulse * 0.18f));

        if (progressRenderer != null)
        {
            Color fill = Color.Lerp(new Color(1f, 1f, 1f, 0.12f), Color.white, progress);
            fill.a = Mathf.Lerp(0.08f, 0.72f, progress);
            progressRenderer.color = fill;
            progressObject.transform.localScale = Vector3.one * Mathf.Lerp(0.15f, 0.95f, progress);
        }
    }

    private void Activate()
    {
        activated = true;
        SpawnActivationFx();
        owner?.NotifyObjectiveNodeActivated(this);
        Destroy(gameObject);
    }

    private void SpawnActivationFx()
    {
        GameObject ring = new GameObject("ObjectiveNodeSyncFx");
        ring.transform.position = transform.position;
        SpriteRenderer ringRenderer = ring.AddComponent<SpriteRenderer>();
        ringRenderer.sprite = CircleSpriteProvider.Get();
        ringRenderer.color = new Color(nodeColor.r, nodeColor.g, nodeColor.b, 0.88f);
        ringRenderer.sortingOrder = 15;
        ring.transform.localScale = Vector3.one * 0.2f;
        ring.AddComponent<ObjectiveNodeBurstFx>().Configure(ringRenderer, 1.25f, 0.26f, nodeColor);
        Destroy(ring, 0.36f);
    }
}

public class ObjectiveNodeBurstFx : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private float radius = 1f;
    private float lifetime = 0.25f;
    private Color tint = Color.white;
    private float age;

    public void Configure(SpriteRenderer rendererRef, float maxRadius, float lifeSeconds, Color color)
    {
        spriteRenderer = rendererRef;
        radius = Mathf.Max(0.2f, maxRadius);
        lifetime = Mathf.Max(0.08f, lifeSeconds);
        tint = color;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / lifetime);
        transform.localScale = Vector3.one * Mathf.Lerp(0.2f, radius, t);
        if (spriteRenderer != null)
        {
            spriteRenderer.color = new Color(tint.r, tint.g, tint.b, Mathf.Lerp(0.88f, 0f, t));
        }
    }
}

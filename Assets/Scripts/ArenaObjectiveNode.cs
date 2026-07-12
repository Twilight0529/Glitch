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
    private SpriteRenderer ringRenderer;
    private readonly SpriteRenderer[] markerRenderers = new SpriteRenderer[4];
    private GameObject progressObject;
    private GameObject ringObject;
    private GameObject markerRoot;
    private Color nodeColor = new Color(0.38f, 1f, 0.66f, 1f);
    private float activationSeconds = 1f;
    private float lifetime = 12f;
    private float lifeTimer;
    private float activationTimer;
    private bool playerInside;
    private bool wasPlayerInside;
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
        EnsureRingVisual();
        EnsureMarkerVisuals();
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
            if (!wasPlayerInside)
            {
                SpawnHoldStartFx();
            }

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

        wasPlayerInside = playerInside;
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

        if (ringRenderer != null)
        {
            float ringAlpha = Mathf.Lerp(0.16f, 0.46f, playerInside ? 1f : pulse) * warnPulse;
            ringRenderer.color = new Color(nodeColor.r, nodeColor.g, nodeColor.b, ringAlpha);
            ringObject.transform.localScale = Vector3.one * Mathf.Lerp(1.35f, 1.8f, playerInside ? progress : pulse);
        }

        UpdateMarkers(progress, pulse, warnPulse);
    }

    private void EnsureRingVisual()
    {
        ringObject = new GameObject("ObjectiveNodeHoldRing");
        ringObject.transform.SetParent(transform, false);
        ringObject.transform.localPosition = Vector3.zero;
        ringObject.transform.localScale = Vector3.one * 1.35f;

        ringRenderer = ringObject.AddComponent<SpriteRenderer>();
        ringRenderer.sprite = CircleSpriteProvider.Get();
        ringRenderer.drawMode = SpriteDrawMode.Sliced;
        ringRenderer.size = Vector2.one * 0.92f;
        ringRenderer.sortingOrder = 11;
    }

    private void EnsureMarkerVisuals()
    {
        markerRoot = new GameObject("ObjectiveNodeDirectionMarkers");
        markerRoot.transform.SetParent(transform, false);
        markerRoot.transform.localPosition = Vector3.zero;

        for (int i = 0; i < markerRenderers.Length; i++)
        {
            GameObject marker = new GameObject($"ObjectiveNodeMarker_{i}");
            marker.transform.SetParent(markerRoot.transform, false);
            markerRenderers[i] = marker.AddComponent<SpriteRenderer>();
            markerRenderers[i].sprite = SquareSpriteProvider.Get();
            markerRenderers[i].sortingOrder = 14;
        }
    }

    private void UpdateMarkers(float progress, float pulse, float warnPulse)
    {
        if (markerRoot == null)
        {
            return;
        }

        markerRoot.transform.rotation = Quaternion.Euler(0f, 0f, Time.time * (playerInside ? 90f : 45f));
        float radius = Mathf.Lerp(0.98f, 0.64f, progress) + pulse * 0.12f;
        float alpha = Mathf.Lerp(0.36f, 0.86f, playerInside ? 1f : pulse) * warnPulse;

        for (int i = 0; i < markerRenderers.Length; i++)
        {
            SpriteRenderer marker = markerRenderers[i];
            if (marker == null)
            {
                continue;
            }

            float angle = (Mathf.PI * 2f * i) / markerRenderers.Length;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            marker.transform.localPosition = dir * radius;
            marker.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            marker.transform.localScale = new Vector3(0.26f, 0.07f, 1f);
            marker.color = new Color(nodeColor.r, nodeColor.g, nodeColor.b, alpha);
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

    private void SpawnHoldStartFx()
    {
        GameObject ring = new GameObject("ObjectiveNodeHoldStartFx");
        ring.transform.position = transform.position;
        SpriteRenderer ringRenderer = ring.AddComponent<SpriteRenderer>();
        ringRenderer.sprite = CircleSpriteProvider.Get();
        ringRenderer.color = new Color(nodeColor.r, nodeColor.g, nodeColor.b, 0.46f);
        ringRenderer.sortingOrder = 14;
        ring.transform.localScale = Vector3.one * 0.32f;
        ring.AddComponent<ObjectiveNodeBurstFx>().Configure(ringRenderer, 0.9f, 0.18f, nodeColor);
        Destroy(ring, 0.28f);
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

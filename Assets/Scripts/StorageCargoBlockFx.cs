using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class StorageCargoBlockFx : MonoBehaviour
{
    // Bloque de carga temporal: primero avisa, despues se vuelve obstaculo real.
    private BoxCollider2D blockCollider;
    private SpriteRenderer bodyRenderer;
    private SpriteRenderer scanRenderer;
    private readonly SpriteRenderer[] edgeRenderers = new SpriteRenderer[4];
    private readonly SpriteRenderer[] boltRenderers = new SpriteRenderer[8];

    private Vector2 blockSize = Vector2.one;
    private float telegraphSeconds = 1f;
    private float activeSeconds = 3f;
    private float age;
    private Color telegraphColor = new Color(1f, 0.78f, 0.24f, 1f);
    private Color activeColor = new Color(1f, 0.28f, 0.40f, 1f);
    private bool active;

    public void Configure(Vector2 size, float telegraphTime, float activeTime, Color warningColor, Color liveColor)
    {
        blockSize = new Vector2(Mathf.Max(0.35f, size.x), Mathf.Max(0.35f, size.y));
        telegraphSeconds = Mathf.Max(0.1f, telegraphTime);
        activeSeconds = Mathf.Max(0.25f, activeTime);
        telegraphColor = warningColor;
        activeColor = liveColor;
        EnsureVisuals();
        UpdateVisuals();
    }

    private void Awake()
    {
        blockCollider = GetComponent<BoxCollider2D>();
        blockCollider.isTrigger = false;
        blockCollider.enabled = false;
        EnsureVisuals();
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (!active && age >= telegraphSeconds)
        {
            active = true;
            if (blockCollider != null)
            {
                blockCollider.enabled = true;
                blockCollider.size = blockSize;
            }
        }

        if (age >= telegraphSeconds + activeSeconds)
        {
            Destroy(gameObject);
            return;
        }

        UpdateVisuals();
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
        bodyRenderer.sortingOrder = 12;

        if (scanRenderer == null)
        {
            scanRenderer = CreateRenderer("CargoScan", 15);
        }

        for (int i = 0; i < edgeRenderers.Length; i++)
        {
            if (edgeRenderers[i] == null)
            {
                edgeRenderers[i] = CreateRenderer($"CargoEdge_{i}", 14);
            }
        }

        for (int i = 0; i < boltRenderers.Length; i++)
        {
            if (boltRenderers[i] == null)
            {
                boltRenderers[i] = CreateRenderer($"CargoBolt_{i}", 16);
            }
        }
    }

    private SpriteRenderer CreateRenderer(string childName, int sortingOrder)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(transform, false);
        child.transform.localPosition = Vector3.zero;
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = SquareSpriteProvider.Get();
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.sortingOrder = sortingOrder;
        renderer.color = Color.clear;
        return renderer;
    }

    private void UpdateVisuals()
    {
        EnsureVisuals();

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (active ? 7.5f : 11.5f));
        Color baseColor = active
            ? Color.Lerp(activeColor, new Color(0.08f, 0.12f, 0.18f, 1f), 0.46f)
            : telegraphColor;
        baseColor.a = active ? Mathf.Lerp(0.70f, 0.92f, pulse) : Mathf.Lerp(0.14f, 0.34f, pulse);
        bodyRenderer.size = blockSize;
        bodyRenderer.color = baseColor;

        Color edgeColor = active ? Color.Lerp(activeColor, Color.white, pulse * 0.22f) : Color.Lerp(telegraphColor, Color.white, pulse * 0.18f);
        edgeColor.a = active ? Mathf.Lerp(0.58f, 0.92f, pulse) : Mathf.Lerp(0.24f, 0.64f, pulse);
        SetEdge(0, new Vector2(0f, blockSize.y * 0.5f), new Vector2(blockSize.x, 0.07f), edgeColor);
        SetEdge(1, new Vector2(0f, -blockSize.y * 0.5f), new Vector2(blockSize.x, 0.07f), edgeColor);
        SetEdge(2, new Vector2(-blockSize.x * 0.5f, 0f), new Vector2(0.07f, blockSize.y), edgeColor);
        SetEdge(3, new Vector2(blockSize.x * 0.5f, 0f), new Vector2(0.07f, blockSize.y), edgeColor);

        if (scanRenderer != null)
        {
            float travel = Mathf.Repeat(Time.time * (active ? 1.8f : 3.2f), 1f);
            bool horizontal = blockSize.x >= blockSize.y;
            float longAxis = horizontal ? blockSize.x : blockSize.y;
            float shortAxis = horizontal ? blockSize.y : blockSize.x;
            scanRenderer.transform.localPosition = horizontal
                ? new Vector3(Mathf.Lerp(-longAxis * 0.5f, longAxis * 0.5f, travel), 0f, 0f)
                : new Vector3(0f, Mathf.Lerp(-longAxis * 0.5f, longAxis * 0.5f, travel), 0f);
            scanRenderer.transform.localRotation = horizontal ? Quaternion.identity : Quaternion.Euler(0f, 0f, 90f);
            scanRenderer.size = new Vector2(Mathf.Max(0.08f, shortAxis * 1.12f), active ? 0.09f : 0.14f);
            Color scan = Color.Lerp(edgeColor, Color.white, 0.3f);
            scan.a *= active ? 0.52f : 0.78f;
            scanRenderer.color = scan;
        }

        UpdateBolts(edgeColor, pulse);
    }

    private void SetEdge(int index, Vector2 localPosition, Vector2 size, Color color)
    {
        if (index < 0 || index >= edgeRenderers.Length || edgeRenderers[index] == null)
        {
            return;
        }

        SpriteRenderer edge = edgeRenderers[index];
        edge.transform.localPosition = localPosition;
        edge.transform.localRotation = Quaternion.identity;
        edge.size = size;
        edge.color = color;
    }

    private void UpdateBolts(Color color, float pulse)
    {
        for (int i = 0; i < boltRenderers.Length; i++)
        {
            SpriteRenderer bolt = boltRenderers[i];
            if (bolt == null)
            {
                continue;
            }

            float n = (i + 0.5f) / boltRenderers.Length;
            bool topBottom = (i & 1) == 0;
            float x = topBottom ? Mathf.Lerp(-blockSize.x * 0.42f, blockSize.x * 0.42f, n) : (i % 4 < 2 ? -blockSize.x * 0.44f : blockSize.x * 0.44f);
            float y = topBottom ? (i % 4 < 2 ? -blockSize.y * 0.44f : blockSize.y * 0.44f) : Mathf.Lerp(-blockSize.y * 0.42f, blockSize.y * 0.42f, n);
            bolt.transform.localPosition = new Vector3(x, y, 0f);
            bolt.transform.localRotation = topBottom ? Quaternion.identity : Quaternion.Euler(0f, 0f, 90f);
            bolt.size = new Vector2(active ? 0.26f : 0.18f, active ? 0.055f : 0.04f);
            Color boltColor = color;
            boltColor.a *= Mathf.Lerp(0.55f, 1f, pulse);
            bolt.color = boltColor;
        }
    }
}

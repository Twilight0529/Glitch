using UnityEngine;

public class ArenaBreachDirectionIndicatorFx : MonoBehaviour
{
    // Flechas temporales que guian al jugador hacia la brecha antes del colapso.
    [SerializeField] private int arrowCount = 7;

    private Transform playerTransform;
    private Vector2 breachPosition;
    private Color tint = Color.magenta;
    private float lifetime = 4f;
    private float age;
    private bool fadingOut;
    private readonly Transform[] arrowRoots = new Transform[8];
    private readonly SpriteRenderer[] arrowBodies = new SpriteRenderer[8];
    private readonly SpriteRenderer[] arrowHeadA = new SpriteRenderer[8];
    private readonly SpriteRenderer[] arrowHeadB = new SpriteRenderer[8];
    private SpriteRenderer beaconRenderer;
    private SpriteRenderer beaconCoreRenderer;
    private SpriteRenderer beaconBackRenderer;

    public void Configure(Transform player, Vector2 target, float lifeSeconds, Color color)
    {
        playerTransform = player;
        breachPosition = target;
        lifetime = Mathf.Max(0.2f, lifeSeconds);
        tint = color;
        CreateVisuals();
    }

    public void FadeOutAndDestroy()
    {
        fadingOut = true;
        age = Mathf.Max(age, lifetime - 0.35f);
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (playerTransform == null)
        {
            Destroy(gameObject);
            return;
        }

        float lifeN = Mathf.Clamp01(age / Mathf.Max(0.01f, lifetime));
        float fade = fadingOut ? Mathf.Clamp01((lifetime - age) / 0.35f) : Mathf.Sin(Mathf.Clamp01(lifeN) * Mathf.PI * 0.5f);
        Vector2 playerPos = playerTransform.position;
        Vector2 toBreach = breachPosition - playerPos;
        Vector2 dir = toBreach.sqrMagnitude > 0.001f ? toBreach.normalized : Vector2.right;
        float distance = Mathf.Max(0.8f, toBreach.magnitude);

        for (int i = 0; i < arrowRoots.Length; i++)
        {
            Transform arrow = arrowRoots[i];
            if (arrow == null)
            {
                continue;
            }

            float flow = Mathf.Repeat((i / (float)arrowRoots.Length) + Time.time * 0.55f, 1f);
            float d = Mathf.Lerp(0.8f, Mathf.Max(0.9f, distance - 0.55f), flow);
            Vector2 pos = playerPos + dir * d;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 9f + i);
            arrow.position = pos;
            arrow.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            arrow.localScale = Vector3.one * Mathf.Lerp(0.82f, 1.2f, pulse);

            Color bodyColor = new Color(tint.r, tint.g, tint.b, Mathf.Lerp(0.16f, 0.62f, pulse) * fade);
            Color headColor = new Color(1f, 0.92f, 1f, Mathf.Lerp(0.46f, 1f, pulse) * fade);
            SetArrowColor(arrowBodies[i], bodyColor);
            SetArrowColor(arrowHeadA[i], headColor);
            SetArrowColor(arrowHeadB[i], headColor);
        }

        if (beaconBackRenderer != null)
        {
            float backPulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 7.5f);
            beaconBackRenderer.transform.position = breachPosition;
            beaconBackRenderer.transform.localScale = Vector3.one * Mathf.Lerp(1.20f, 1.48f, backPulse);
            beaconBackRenderer.color = new Color(0.015f, 0.004f, 0.026f, Mathf.Lerp(0.64f, 0.84f, backPulse) * fade);
        }

        if (beaconRenderer != null)
        {
            float beaconPulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 12f);
            beaconRenderer.transform.position = breachPosition;
            beaconRenderer.transform.localScale = Vector3.one * Mathf.Lerp(0.9f, 1.35f, beaconPulse);
            beaconRenderer.color = new Color(tint.r, tint.g, tint.b, Mathf.Lerp(0.34f, 0.86f, beaconPulse) * fade);
        }

        if (beaconCoreRenderer != null)
        {
            float corePulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 16f);
            beaconCoreRenderer.transform.position = breachPosition;
            beaconCoreRenderer.transform.rotation = Quaternion.Euler(0f, 0f, Time.time * 90f);
            beaconCoreRenderer.transform.localScale = Vector3.one * Mathf.Lerp(0.52f, 0.72f, corePulse);
            beaconCoreRenderer.color = new Color(1f, 0.94f, 1f, Mathf.Lerp(0.58f, 1f, corePulse) * fade);
        }

        if (age >= lifetime)
        {
            Destroy(gameObject);
        }
    }

    private static void SetArrowColor(SpriteRenderer renderer, Color color)
    {
        if (renderer != null)
        {
            renderer.color = color;
        }
    }

    private void CreateVisuals()
    {
        int count = Mathf.Min(arrowRoots.Length, Mathf.Max(2, arrowCount));
        for (int i = 0; i < count; i++)
        {
            GameObject arrow = new GameObject($"BreachArrow_{i}");
            arrow.transform.SetParent(transform, false);
            arrowRoots[i] = arrow.transform;

            arrowBodies[i] = CreateArrowPart(arrow.transform, "Body", new Vector3(-0.14f, 0f, 0f), Quaternion.identity, new Vector3(0.5f, 0.075f, 1f), 22);
            arrowHeadA[i] = CreateArrowPart(arrow.transform, "HeadA", new Vector3(0.15f, 0.08f, 0f), Quaternion.Euler(0f, 0f, -42f), new Vector3(0.28f, 0.075f, 1f), 23);
            arrowHeadB[i] = CreateArrowPart(arrow.transform, "HeadB", new Vector3(0.15f, -0.08f, 0f), Quaternion.Euler(0f, 0f, 42f), new Vector3(0.28f, 0.075f, 1f), 23);
        }

        GameObject back = new GameObject("BreachTargetBackplate");
        back.transform.SetParent(transform, false);
        beaconBackRenderer = back.AddComponent<SpriteRenderer>();
        beaconBackRenderer.sprite = CircleSpriteProvider.Get();
        beaconBackRenderer.drawMode = SpriteDrawMode.Sliced;
        beaconBackRenderer.size = Vector2.one * 1.45f;
        beaconBackRenderer.sortingOrder = 20;

        GameObject beacon = new GameObject("BreachTargetBeacon");
        beacon.transform.SetParent(transform, false);
        beaconRenderer = beacon.AddComponent<SpriteRenderer>();
        beaconRenderer.sprite = CircleSpriteProvider.Get();
        beaconRenderer.drawMode = SpriteDrawMode.Sliced;
        beaconRenderer.size = Vector2.one * 1.3f;
        beaconRenderer.sortingOrder = 21;

        GameObject core = new GameObject("BreachTargetCore");
        core.transform.SetParent(transform, false);
        beaconCoreRenderer = core.AddComponent<SpriteRenderer>();
        beaconCoreRenderer.sprite = SquareSpriteProvider.Get();
        beaconCoreRenderer.sortingOrder = 22;
    }

    private static SpriteRenderer CreateArrowPart(Transform parent, string name, Vector3 localPosition, Quaternion localRotation, Vector3 localScale, int sortingOrder)
    {
        GameObject part = new GameObject(name);
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = localRotation;
        part.transform.localScale = localScale;

        SpriteRenderer sr = part.AddComponent<SpriteRenderer>();
        sr.sprite = SquareSpriteProvider.Get();
        sr.sortingOrder = sortingOrder;
        return sr;
    }
}

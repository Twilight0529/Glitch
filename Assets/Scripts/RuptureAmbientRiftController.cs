using System.Collections.Generic;
using UnityEngine;

public class RuptureAmbientRiftController : MonoBehaviour
{
    // Sistema ambiental de Rupture: grietas que materializan fragmentos temporales de realidad.
    [Header("Cadence")]
    [SerializeField] private bool enableAmbientRifts = true;
    [SerializeField] private Vector2 riftIntervalRange = new Vector2(6.8f, 11.5f);
    [SerializeField] private float firstRiftDelayMin = 1.2f;
    [SerializeField] private float firstRiftDelayMax = 2.4f;
    [SerializeField] private int maxActiveRiftBursts = 1;

    [Header("Fragments")]
    [SerializeField] private int fragmentCountMin = 2;
    [SerializeField] private int fragmentCountMax = 4;
    [SerializeField] private Vector2 fragmentSizeMin = new Vector2(0.85f, 0.42f);
    [SerializeField] private Vector2 fragmentSizeMax = new Vector2(1.75f, 0.82f);
    [SerializeField] private Vector2 fragmentRadiusRange = new Vector2(1.7f, 4.2f);
    [SerializeField] private float fragmentPlacementClearance = 0.28f;
    [SerializeField] private float actorSafetyRadius = 2.6f;
    [SerializeField] private int placementAttempts = 56;

    [Header("Timing")]
    [SerializeField] private float telegraphSeconds = 1.05f;
    [SerializeField] private float materializeSeconds = 0.72f;
    [SerializeField] private Vector2 solidHoldRange = new Vector2(4.4f, 6.8f);
    [SerializeField] private float dissolveSeconds = 1.05f;

    [Header("Visuals")]
    [SerializeField] private Color warningColor = new Color(1f, 0.38f, 0.96f, 0.76f);
    [SerializeField] private Color activeColor = new Color(0.44f, 0.96f, 1f, 0.86f);

    private Transform centerTransform;
    private Transform dynamicRoot;
    private GameManager gameManager;
    private PlayerController playerController;
    private EnemyController enemyController;
    private readonly List<RuptureRiftAnchorFx> activeAnchors = new List<RuptureRiftAnchorFx>();
    private readonly List<RupturePhantomFragmentFx> activeFragments = new List<RupturePhantomFragmentFx>();

    private float interiorLeft;
    private float interiorRight;
    private float interiorBottom;
    private float interiorTop;
    private float riftTimer;

    public void Configure(Transform center, Transform staticObstaclesRoot, Transform dynamicObstaclesRoot)
    {
        centerTransform = center != null ? center : transform;
        dynamicRoot = dynamicObstaclesRoot != null ? dynamicObstaclesRoot : centerTransform;
        RefreshReferences();
        BuildInteriorBounds();
        ClearRifts();
        ScheduleNextRift(firstRiftDelayMin, firstRiftDelayMax);
    }

    private void OnDisable()
    {
        ClearRifts();
    }

    private void Update()
    {
        if (!enableAmbientRifts || centerTransform == null)
        {
            return;
        }

        RefreshReferences();
        PruneRifts();
        if (gameManager == null || !gameManager.IsRunActive || gameManager.IsGameOver)
        {
            return;
        }

        if (GetLiveBurstCount() >= Mathf.Max(1, maxActiveRiftBursts))
        {
            return;
        }

        riftTimer -= Time.deltaTime;
        if (riftTimer <= 0f)
        {
            if (!TrySpawnRiftBurst())
            {
                ScheduleNextRift(1.6f, 3.0f);
                return;
            }

            ScheduleNextRift();
        }
    }

    private bool TrySpawnRiftBurst()
    {
        BuildInteriorBounds();
        for (int attempt = 0; attempt < Mathf.Max(8, placementAttempts); attempt++)
        {
            Vector2 anchor = PickAnchorCandidate();
            if (!IsSafeFromActors(anchor))
            {
                continue;
            }

            List<FragmentSpawnData> fragments = BuildFragmentLayout(anchor);
            if (fragments.Count <= 0)
            {
                continue;
            }

            float solidHold = Random.Range(Mathf.Min(solidHoldRange.x, solidHoldRange.y), Mathf.Max(solidHoldRange.x, solidHoldRange.y));
            float totalDuration = Mathf.Max(0.5f, telegraphSeconds + materializeSeconds + solidHold + dissolveSeconds);
            SpawnAnchor(anchor, totalDuration);
            for (int i = 0; i < fragments.Count; i++)
            {
                SpawnFragment(fragments[i], solidHold);
            }

            return true;
        }

        return false;
    }

    private List<FragmentSpawnData> BuildFragmentLayout(Vector2 anchor)
    {
        int min = Mathf.Max(1, Mathf.Min(fragmentCountMin, fragmentCountMax));
        int max = Mathf.Max(min, Mathf.Max(fragmentCountMin, fragmentCountMax));
        int targetCount = Random.Range(min, max + 1);
        List<FragmentSpawnData> fragments = new List<FragmentSpawnData>();

        for (int i = 0; i < placementAttempts && fragments.Count < targetCount; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float radius = Random.Range(Mathf.Min(fragmentRadiusRange.x, fragmentRadiusRange.y), Mathf.Max(fragmentRadiusRange.x, fragmentRadiusRange.y));
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 size = RollFragmentSize();
            Vector2 position = ClampToInterior(anchor + dir * radius, Mathf.Max(size.x, size.y) * 0.5f);
            float rotation = angle * Mathf.Rad2Deg + Random.Range(-20f, 20f);

            if (!IsGoodFragmentPosition(position, size, rotation))
            {
                continue;
            }

            fragments.Add(new FragmentSpawnData
            {
                position = position,
                size = size,
                rotation = rotation
            });
        }

        return fragments;
    }

    private Vector2 PickAnchorCandidate()
    {
        float margin = 2.0f;
        float x = Random.Range(interiorLeft + margin, interiorRight - margin);
        float y = Random.Range(interiorBottom + margin, interiorTop - margin);
        if (playerController != null && Random.value < 0.58f)
        {
            Vector2 playerPos = playerController.GetPosition();
            Vector2 dir = Random.insideUnitCircle;
            if (dir.sqrMagnitude < 0.05f)
            {
                dir = Vector2.right;
            }

            Vector2 nearPlayer = playerPos + dir.normalized * Random.Range(4.2f, 7.2f);
            x = Mathf.Clamp(nearPlayer.x, interiorLeft + margin, interiorRight - margin);
            y = Mathf.Clamp(nearPlayer.y, interiorBottom + margin, interiorTop - margin);
        }

        return new Vector2(x, y);
    }

    private Vector2 RollFragmentSize()
    {
        float width = Random.Range(Mathf.Min(fragmentSizeMin.x, fragmentSizeMax.x), Mathf.Max(fragmentSizeMin.x, fragmentSizeMax.x));
        float height = Random.Range(Mathf.Min(fragmentSizeMin.y, fragmentSizeMax.y), Mathf.Max(fragmentSizeMin.y, fragmentSizeMax.y));
        if (Random.value < 0.38f)
        {
            float swap = width;
            width = height;
            height = swap;
        }

        return new Vector2(Mathf.Max(0.25f, width), Mathf.Max(0.25f, height));
    }

    private bool IsGoodFragmentPosition(Vector2 position, Vector2 size, float rotation)
    {
        if (!IsSafeFromActors(position))
        {
            return false;
        }

        Collider2D[] hits = Physics2D.OverlapBoxAll(position, size + Vector2.one * fragmentPlacementClearance, rotation);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.isTrigger)
            {
                continue;
            }
            if (hit.GetComponent<RupturePhantomFragmentFx>() != null)
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private bool IsSafeFromActors(Vector2 position)
    {
        float safeRadius = Mathf.Max(0.5f, actorSafetyRadius);
        if (playerController != null && Vector2.Distance(playerController.GetPosition(), position) < safeRadius)
        {
            return false;
        }
        if (enemyController != null && Vector2.Distance(enemyController.GetCurrentPosition(), position) < safeRadius)
        {
            return false;
        }

        return true;
    }

    private void SpawnAnchor(Vector2 position, float totalDuration)
    {
        GameObject anchorGo = new GameObject("RuptureAmbientRiftAnchor");
        anchorGo.transform.SetParent(dynamicRoot != null ? dynamicRoot : centerTransform, false);
        anchorGo.transform.position = new Vector3(position.x, position.y, 0f);
        RuptureRiftAnchorFx anchor = anchorGo.AddComponent<RuptureRiftAnchorFx>();
        float radius = Mathf.Max(fragmentRadiusRange.x, fragmentRadiusRange.y);
        anchor.Configure(totalDuration, radius, warningColor, activeColor);
        activeAnchors.Add(anchor);
    }

    private void SpawnFragment(FragmentSpawnData data, float solidHold)
    {
        GameObject fragmentGo = new GameObject("RuptureAmbientPhantomFragment");
        fragmentGo.transform.SetParent(dynamicRoot != null ? dynamicRoot : centerTransform, false);
        fragmentGo.transform.position = new Vector3(data.position.x, data.position.y, 0f);
        fragmentGo.transform.rotation = Quaternion.Euler(0f, 0f, data.rotation);
        RupturePhantomFragmentFx fragment = fragmentGo.AddComponent<RupturePhantomFragmentFx>();
        fragment.Configure(data.size, data.rotation, telegraphSeconds, materializeSeconds, solidHold, dissolveSeconds, warningColor, activeColor);
        activeFragments.Add(fragment);
    }

    private int GetLiveBurstCount()
    {
        PruneRifts();
        return activeAnchors.Count;
    }

    private void PruneRifts()
    {
        for (int i = activeAnchors.Count - 1; i >= 0; i--)
        {
            if (activeAnchors[i] == null)
            {
                activeAnchors.RemoveAt(i);
            }
        }

        for (int i = activeFragments.Count - 1; i >= 0; i--)
        {
            if (activeFragments[i] == null)
            {
                activeFragments.RemoveAt(i);
            }
        }
    }

    private void ClearRifts()
    {
        for (int i = activeAnchors.Count - 1; i >= 0; i--)
        {
            if (activeAnchors[i] != null)
            {
                DestroyByPlayState(activeAnchors[i].gameObject);
            }
        }

        for (int i = activeFragments.Count - 1; i >= 0; i--)
        {
            if (activeFragments[i] != null)
            {
                DestroyByPlayState(activeFragments[i].gameObject);
            }
        }

        activeAnchors.Clear();
        activeFragments.Clear();
    }

    private Vector2 ClampToInterior(Vector2 position, float radius)
    {
        float margin = Mathf.Max(0f, radius + 0.2f);
        position.x = Mathf.Clamp(position.x, interiorLeft + margin, interiorRight - margin);
        position.y = Mathf.Clamp(position.y, interiorBottom + margin, interiorTop - margin);
        return position;
    }

    private void ScheduleNextRift()
    {
        ScheduleNextRift(riftIntervalRange.x, riftIntervalRange.y);
    }

    private void ScheduleNextRift(float minSeconds, float maxSeconds)
    {
        riftTimer = Random.Range(Mathf.Min(minSeconds, maxSeconds), Mathf.Max(minSeconds, maxSeconds));
    }

    private void BuildInteriorBounds()
    {
        Vector2 center = centerTransform != null ? (Vector2)centerTransform.position : Vector2.zero;
        float halfW = 16f;
        float halfH = 9f;
        ProceduralArenaGenerator generator = centerTransform != null ? centerTransform.GetComponent<ProceduralArenaGenerator>() : null;
        if (generator != null)
        {
            halfW = generator.ArenaWidth * 0.5f;
            halfH = generator.ArenaHeight * 0.5f;
        }

        interiorLeft = center.x - halfW + 0.5f;
        interiorRight = center.x + halfW - 0.5f;
        interiorBottom = center.y - halfH + 0.5f;
        interiorTop = center.y + halfH - 0.5f;
    }

    private void RefreshReferences()
    {
        if (gameManager == null)
        {
            gameManager = FindAnyObjectByType<GameManager>();
        }
        if (playerController == null)
        {
            playerController = FindAnyObjectByType<PlayerController>();
        }
        if (enemyController == null)
        {
            enemyController = FindAnyObjectByType<EnemyController>();
        }
    }

    private static void DestroyByPlayState(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
            DestroyImmediate(target);
        }
    }

    private struct FragmentSpawnData
    {
        public Vector2 position;
        public Vector2 size;
        public float rotation;
    }
}

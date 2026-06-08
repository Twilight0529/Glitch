using System.Collections.Generic;
using UnityEngine;

public class LabAmbientSecurityController : MonoBehaviour
{
    // Sistema ambiental de Lab: seguridad automatica que escanea una linea y cierra compuertas coordinadas.
    private enum GateSide
    {
        Left,
        Right,
        Bottom,
        Top
    }

    [Header("Cadence")]
    [SerializeField] private bool enableAmbientSecurity = true;
    [SerializeField] private Vector2 gateIntervalRange = new Vector2(5.8f, 9.4f);
    [SerializeField] private float firstGateDelayMin = 1.1f;
    [SerializeField] private float firstGateDelayMax = 2.2f;
    [SerializeField] private int maxActiveGates = 2;

    [Header("Gate Timing")]
    [SerializeField] private Vector2 gateDurationRange = new Vector2(7f, 10f);
    [SerializeField, Range(0.08f, 0.45f)] private float gateTelegraphFraction = 0.24f;
    [SerializeField, Range(0.30f, 0.72f)] private float gateDeployEndFraction = 0.43f;
    [SerializeField, Range(0.55f, 0.92f)] private float gateRetractStartFraction = 0.78f;

    [Header("Gate Shape")]
    [SerializeField] private Vector2 gateLengthRange = new Vector2(5.2f, 8.6f);
    [SerializeField] private Vector2 gateThicknessRange = new Vector2(0.42f, 0.68f);
    [SerializeField] private float edgeAnchorOffset = 0.85f;
    [SerializeField] private float placementClearance = 0.22f;
    [SerializeField] private float actorSafetyRadius = 2.6f;
    [SerializeField] private int placementAttempts = 42;

    [Header("Visuals")]
    [SerializeField] private Color warningColor = new Color(1f, 0.82f, 0.42f, 0.75f);
    [SerializeField] private Color activeColor = new Color(0.38f, 0.93f, 1f, 0.92f);

    private Transform centerTransform;
    private GameManager gameManager;
    private PlayerController playerController;
    private EnemyController enemyController;
    private readonly List<LabContainmentGateFx> activeGates = new List<LabContainmentGateFx>();
    private readonly List<LabSecurityScanFx> activeScans = new List<LabSecurityScanFx>();

    private float interiorLeft;
    private float interiorRight;
    private float interiorBottom;
    private float interiorTop;
    private float gateTimer;

    public void Configure(Transform center, Transform staticObstaclesRoot, Transform dynamicObstaclesRoot)
    {
        centerTransform = center != null ? center : transform;
        RefreshReferences();
        BuildInteriorBounds();
        ClearGates();
        ScheduleNextGate(firstGateDelayMin, firstGateDelayMax);
    }

    private void OnDisable()
    {
        ClearGates();
    }

    private void Update()
    {
        if (!enableAmbientSecurity || centerTransform == null)
        {
            return;
        }

        RefreshReferences();
        PruneGates();
        PruneScans();
        if (gameManager == null || !gameManager.IsRunActive || gameManager.IsGameOver)
        {
            return;
        }

        if (activeGates.Count >= Mathf.Max(1, maxActiveGates))
        {
            return;
        }

        gateTimer -= Time.deltaTime;
        if (gateTimer <= 0f)
        {
            if (!TrySpawnSecurityLockdown())
            {
                ScheduleNextGate(1.4f, 2.6f);
                return;
            }

            ScheduleNextGate();
        }
    }

    private bool TrySpawnSecurityLockdown()
    {
        BuildInteriorBounds();
        int attempts = Mathf.Max(4, placementAttempts);
        for (int i = 0; i < attempts; i++)
        {
            bool horizontal = Random.value < 0.5f;
            GateSide firstSide = horizontal ? GateSide.Left : GateSide.Bottom;
            GateSide secondSide = horizontal ? GateSide.Right : GateSide.Top;
            Vector2 firstSize = RollGateSize(firstSide);
            Vector2 secondSize = RollGateSize(secondSide);
            float laneCenter = PickLockdownLaneCenter(horizontal, firstSize, secondSize);
            Vector2 firstTarget = PickGateTarget(firstSide, firstSize, laneCenter);
            Vector2 secondTarget = PickGateTarget(secondSide, secondSize, laneCenter);
            if (!IsGoodGatePosition(firstTarget, firstSize) || !IsGoodGatePosition(secondTarget, secondSize))
            {
                continue;
            }

            float lockdownDuration = Random.Range(
                Mathf.Min(gateDurationRange.x, gateDurationRange.y),
                Mathf.Max(gateDurationRange.x, gateDurationRange.y));
            SpawnScan(horizontal, laneCenter, lockdownDuration);
            SpawnGate(firstSide, firstSize, firstTarget, lockdownDuration);
            SpawnGate(secondSide, secondSize, secondTarget, lockdownDuration);
            return true;
        }

        return false;
    }

    private void SpawnGate(GateSide side, Vector2 size, Vector2 target, float lockdownDuration)
    {
        Vector2 start = PickGateStart(side, size, target);
        GameObject gateGo = new GameObject($"LabAmbientGate_{side}");
        gateGo.transform.SetParent(centerTransform != null ? centerTransform : transform, false);
        gateGo.transform.position = new Vector3(start.x, start.y, 0f);
        LabContainmentGateFx gate = gateGo.AddComponent<LabContainmentGateFx>();
        gate.Configure(
            start,
            target,
            size,
            lockdownDuration,
            gateTelegraphFraction,
            gateDeployEndFraction,
            gateRetractStartFraction,
            warningColor,
            activeColor);
        activeGates.Add(gate);
    }

    private void SpawnScan(bool horizontal, float laneCenter, float lockdownDuration)
    {
        GameObject scanGo = new GameObject(horizontal ? "LabSecurityHorizontalScan" : "LabSecurityVerticalScan");
        scanGo.transform.SetParent(centerTransform != null ? centerTransform : transform, false);
        LabSecurityScanFx scan = scanGo.AddComponent<LabSecurityScanFx>();
        scan.Configure(horizontal, interiorLeft, interiorRight, interiorBottom, interiorTop, laneCenter, lockdownDuration, gateTelegraphFraction, warningColor, activeColor);
        activeScans.Add(scan);
    }

    private Vector2 RollGateSize(GateSide side)
    {
        float length = Random.Range(
            Mathf.Min(gateLengthRange.x, gateLengthRange.y),
            Mathf.Max(gateLengthRange.x, gateLengthRange.y));
        float thickness = Random.Range(
            Mathf.Min(gateThicknessRange.x, gateThicknessRange.y),
            Mathf.Max(gateThicknessRange.x, gateThicknessRange.y));

        return side == GateSide.Left || side == GateSide.Right
            ? new Vector2(length, thickness)
            : new Vector2(thickness, length);
    }

    private float PickLockdownLaneCenter(bool horizontal, Vector2 firstSize, Vector2 secondSize)
    {
        float marginY = Mathf.Max(firstSize.y, secondSize.y) * 0.5f + 0.55f;
        float marginX = Mathf.Max(firstSize.x, secondSize.x) * 0.5f + 0.55f;
        float yMin = Mathf.Min(interiorBottom + marginY, interiorTop - marginY);
        float yMax = Mathf.Max(interiorBottom + marginY, interiorTop - marginY);
        float xMin = Mathf.Min(interiorLeft + marginX, interiorRight - marginX);
        float xMax = Mathf.Max(interiorLeft + marginX, interiorRight - marginX);

        if (playerController != null && Random.value < 0.62f)
        {
            Vector2 playerPos = playerController.GetPosition();
            float offset = Random.Range(2.0f, 4.4f) * (Random.value < 0.5f ? -1f : 1f);
            return horizontal
                ? Mathf.Clamp(playerPos.y + offset, yMin, yMax)
                : Mathf.Clamp(playerPos.x + offset, xMin, xMax);
        }

        return horizontal ? Random.Range(yMin, yMax) : Random.Range(xMin, xMax);
    }

    private Vector2 PickGateTarget(GateSide side, Vector2 size, float laneCenter)
    {
        switch (side)
        {
            case GateSide.Left:
                return new Vector2(interiorLeft + size.x * 0.5f + placementClearance, laneCenter);
            case GateSide.Right:
                return new Vector2(interiorRight - size.x * 0.5f - placementClearance, laneCenter);
            case GateSide.Bottom:
                return new Vector2(laneCenter, interiorBottom + size.y * 0.5f + placementClearance);
            default:
                return new Vector2(laneCenter, interiorTop - size.y * 0.5f - placementClearance);
        }
    }

    private Vector2 PickGateStart(GateSide side, Vector2 size, Vector2 target)
    {
        switch (side)
        {
            case GateSide.Left:
                return new Vector2(interiorLeft - size.x * 0.5f - edgeAnchorOffset, target.y);
            case GateSide.Right:
                return new Vector2(interiorRight + size.x * 0.5f + edgeAnchorOffset, target.y);
            case GateSide.Bottom:
                return new Vector2(target.x, interiorBottom - size.y * 0.5f - edgeAnchorOffset);
            default:
                return new Vector2(target.x, interiorTop + size.y * 0.5f + edgeAnchorOffset);
        }
    }

    private bool IsGoodGatePosition(Vector2 target, Vector2 size)
    {
        if (!IsSafeFromActors(target))
        {
            return false;
        }

        Collider2D[] hits = Physics2D.OverlapBoxAll(target, size + Vector2.one * placementClearance, 0f);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.isTrigger)
            {
                continue;
            }
            if (hit.GetComponent<LabContainmentGateFx>() != null)
            {
                continue;
            }
            if (hit.GetComponent<PlayerController>() != null || hit.GetComponent<EnemyController>() != null || hit.GetComponent<SplitAnomalyCloneController>() != null)
            {
                return false;
            }

            return false;
        }

        return true;
    }

    private bool IsSafeFromActors(Vector2 target)
    {
        float safeRadius = Mathf.Max(0.4f, actorSafetyRadius);
        if (playerController != null && Vector2.Distance(playerController.GetPosition(), target) < safeRadius)
        {
            return false;
        }
        if (enemyController != null && Vector2.Distance(enemyController.GetCurrentPosition(), target) < safeRadius)
        {
            return false;
        }

        return true;
    }

    private void ScheduleNextGate()
    {
        ScheduleNextGate(gateIntervalRange.x, gateIntervalRange.y);
    }

    private void ScheduleNextGate(float minSeconds, float maxSeconds)
    {
        gateTimer = Random.Range(Mathf.Min(minSeconds, maxSeconds), Mathf.Max(minSeconds, maxSeconds));
    }

    private void PruneGates()
    {
        for (int i = activeGates.Count - 1; i >= 0; i--)
        {
            if (activeGates[i] == null)
            {
                activeGates.RemoveAt(i);
            }
        }
    }

    private void PruneScans()
    {
        for (int i = activeScans.Count - 1; i >= 0; i--)
        {
            if (activeScans[i] == null)
            {
                activeScans.RemoveAt(i);
            }
        }
    }

    private void ClearGates()
    {
        for (int i = activeGates.Count - 1; i >= 0; i--)
        {
            LabContainmentGateFx gate = activeGates[i];
            if (gate != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(gate.gameObject);
                }
                else
                {
                    DestroyImmediate(gate.gameObject);
                }
            }
        }

        activeGates.Clear();
        ClearScans();
    }

    private void ClearScans()
    {
        for (int i = activeScans.Count - 1; i >= 0; i--)
        {
            LabSecurityScanFx scan = activeScans[i];
            if (scan != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(scan.gameObject);
                }
                else
                {
                    DestroyImmediate(scan.gameObject);
                }
            }
        }

        activeScans.Clear();
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
}

using System.Collections.Generic;
using UnityEngine;

// Vida pasiva de Storage: las grúas evalúan espacios válidos y mueven carga sin depender de un evento puntual.
public class StorageAmbientCraneController : MonoBehaviour
{
    // Maquinaria propia de Storage: gruas laterales que mantienen viva la arena fuera de eventos puntuales.
    private enum CraneOperationKind
    {
        MoveObstacle,
        AddCargo,
        RemoveCargo
    }

    private struct MovableObstacle
    {
        public Transform transform;
        public Rigidbody2D rigidbody;
        public Collider2D collider;
        public DynamicObstacleController dynamicController;
        public float radius;
    }

    [Header("Cadence")]
    [SerializeField] private bool enableAmbientCranes = true;
    [SerializeField] private Vector2 operationIntervalRange = new Vector2(5.5f, 9.5f);
    [SerializeField] private float operationDuration = 3.1f;
    [SerializeField, Range(0.05f, 0.45f)] private float telegraphFraction = 0.22f;
    [SerializeField, Range(0.55f, 0.95f)] private float activeEndFraction = 0.82f;

    [Header("Movement")]
    [SerializeField] private Vector2 relocationDistanceRange = new Vector2(1.4f, 3.6f);
    [SerializeField] private float relocationAttempts = 30f;
    [SerializeField] private float edgeAnchorOffset = 0.95f;
    [SerializeField] private float actorSafetyRadius = 2.45f;
    [SerializeField] private float obstacleClearance = 0.28f;

    [Header("Cargo")]
    [SerializeField] private int maxAmbientCargoBlocks = 5;
    [SerializeField] private Vector2 cargoSizeMin = new Vector2(0.9f, 0.65f);
    [SerializeField] private Vector2 cargoSizeMax = new Vector2(1.8f, 1.2f);
    [SerializeField, Range(0f, 1f)] private float addCargoChance = 0.34f;
    [SerializeField, Range(0f, 1f)] private float removeCargoChance = 0.22f;

    [Header("Visuals")]
    [SerializeField] private Color craneWarningColor = new Color(1f, 0.78f, 0.24f, 0.72f);
    [SerializeField] private Color craneActiveColor = new Color(1f, 0.28f, 0.40f, 0.92f);
    [SerializeField] private Color cargoBodyColor = new Color(0.44f, 0.35f, 0.23f, 0.94f);
    [SerializeField] private Color cargoAccentColor = new Color(0.95f, 0.72f, 0.38f, 0.9f);

    private Transform centerTransform;
    private Transform obstaclesRoot;
    private Transform dynamicRoot;
    private GameManager gameManager;
    private PlayerController playerController;
    private EnemyController enemyController;

    private readonly List<MovableObstacle> movableObstacles = new List<MovableObstacle>();
    private readonly List<GameObject> ambientCargoBlocks = new List<GameObject>();
    private readonly HashSet<Transform> dedupe = new HashSet<Transform>();

    private float interiorLeft;
    private float interiorRight;
    private float interiorBottom;
    private float interiorTop;
    private float operationTimer;
    private float operationAge;
    private bool operationActive;
    private bool operationFromLeft;
    private CraneOperationKind currentOperation;
    private MovableObstacle activeObstacle;
    private GameObject activeCargo;
    private Collider2D activeCargoCollider;
    private DynamicObstacleController activeDynamicController;
    private bool activeDynamicWasEnabled;
    private Vector2 operationStartPosition;
    private Vector2 operationTargetPosition;
    private StorageCraneHookFx activeHook;
    private bool operationImpactPlayed;
    private bool operationModifiersApplied;

    public void Configure(Transform center, Transform staticObstaclesRoot, Transform dynamicObstaclesRoot)
    {
        craneWarningColor = GlitchUiPalette.WithAlpha(GlitchUiPalette.Alert, 0.72f);
        craneActiveColor = GlitchUiPalette.WithAlpha(GlitchUiPalette.Danger, 0.92f);
        centerTransform = center != null ? center : transform;
        obstaclesRoot = staticObstaclesRoot;
        dynamicRoot = dynamicObstaclesRoot != null ? dynamicObstaclesRoot : staticObstaclesRoot;
        ApplyOperationModifiersOnce();
        RefreshReferences();
        BuildInteriorBounds();
        ClearOperation();
        ScheduleNextOperation(0.8f, 1.8f);
    }

    private void OnDisable()
    {
        ClearOperation();
        ClearAmbientCargo();
    }

    private void Update()
    {
        if (!enableAmbientCranes || centerTransform == null)
        {
            return;
        }

        RefreshReferences();
        if (gameManager == null || !gameManager.IsRunActive || gameManager.IsGameOver)
        {
            return;
        }

        if (operationActive)
        {
            TickOperation();
            return;
        }

        operationTimer -= Time.deltaTime;
        if (operationTimer <= 0f)
        {
            BeginOperation();
        }
    }

    private void BeginOperation()
    {
        BuildInteriorBounds();
        RebuildMovableObstacles();
        currentOperation = PickOperationKind();

        bool started = false;
        switch (currentOperation)
        {
            case CraneOperationKind.AddCargo:
                started = TryBeginAddCargo();
                break;
            case CraneOperationKind.RemoveCargo:
                started = TryBeginRemoveCargo();
                break;
            default:
                started = TryBeginMoveObstacle();
                break;
        }

        if (!started && currentOperation != CraneOperationKind.MoveObstacle)
        {
            currentOperation = CraneOperationKind.MoveObstacle;
            started = TryBeginMoveObstacle();
        }
        if (!started)
        {
            ScheduleNextOperation(2f, 4f);
        }
    }

    private CraneOperationKind PickOperationKind()
    {
        if (ambientCargoBlocks.Count >= Mathf.Max(1, maxAmbientCargoBlocks) && Random.value < 0.7f)
        {
            return CraneOperationKind.RemoveCargo;
        }

        float roll = Random.value;
        if (roll < addCargoChance && ambientCargoBlocks.Count < Mathf.Max(1, maxAmbientCargoBlocks))
        {
            return CraneOperationKind.AddCargo;
        }
        if (roll < addCargoChance + removeCargoChance && ambientCargoBlocks.Count > 0)
        {
            return CraneOperationKind.RemoveCargo;
        }

        return CraneOperationKind.MoveObstacle;
    }

    private bool TryBeginMoveObstacle()
    {
        if (movableObstacles.Count == 0)
        {
            return false;
        }

        for (int attempt = 0; attempt < 24; attempt++)
        {
            MovableObstacle obstacle = movableObstacles[Random.Range(0, movableObstacles.Count)];
            if (obstacle.transform == null)
            {
                continue;
            }

            Vector2 start = obstacle.transform.position;
            if (!TryPickRelocation(start, obstacle.radius, obstacle.transform, out Vector2 target))
            {
                continue;
            }

            activeObstacle = obstacle;
            activeDynamicController = obstacle.dynamicController;
            activeDynamicWasEnabled = activeDynamicController != null && activeDynamicController.enabled;
            if (activeDynamicController != null)
            {
                activeDynamicController.enabled = false;
            }

            StartOperation(obstacle.transform.gameObject, start, target, obstacle.collider);
            return true;
        }

        return false;
    }

    private bool TryBeginAddCargo()
    {
        Vector2 size = RollCargoSize();
        if (!TryPickClearPosition(size, null, out Vector2 target))
        {
            return false;
        }

        operationFromLeft = Random.value < 0.5f;
        float startX = operationFromLeft ? interiorLeft - size.x - edgeAnchorOffset : interiorRight + size.x + edgeAnchorOffset;
        Vector2 start = new Vector2(startX, target.y);
        GameObject cargo = CreateCargoBlock("StorageAmbientCargo", size, start, colliderEnabled: false);
        ambientCargoBlocks.Add(cargo);
        activeCargo = cargo;
        activeCargoCollider = cargo.GetComponent<Collider2D>();
        StartOperation(cargo, start, target, activeCargoCollider, forceSide: operationFromLeft);
        return true;
    }

    private bool TryBeginRemoveCargo()
    {
        PruneAmbientCargo();
        if (ambientCargoBlocks.Count == 0)
        {
            return false;
        }

        activeCargo = ambientCargoBlocks[Random.Range(0, ambientCargoBlocks.Count)];
        if (activeCargo == null)
        {
            PruneAmbientCargo();
            return false;
        }

        Vector2 start = activeCargo.transform.position;
        operationFromLeft = start.x < (interiorLeft + interiorRight) * 0.5f;
        float targetX = operationFromLeft ? interiorLeft - 2.2f : interiorRight + 2.2f;
        Vector2 target = new Vector2(targetX, start.y);
        activeCargoCollider = activeCargo.GetComponent<Collider2D>();
        StartOperation(activeCargo, start, target, activeCargoCollider, forceSide: operationFromLeft);
        return true;
    }

    private void StartOperation(GameObject targetObject, Vector2 start, Vector2 target, Collider2D activeCollider, bool? forceSide = null)
    {
        operationAge = 0f;
        operationActive = true;
        operationStartPosition = start;
        operationTargetPosition = target;
        activeCargoCollider = activeCollider;
        operationImpactPlayed = false;
        if (forceSide.HasValue)
        {
            operationFromLeft = forceSide.Value;
        }
        else
        {
            operationFromLeft = start.x < (interiorLeft + interiorRight) * 0.5f;
        }

        if (activeCargoCollider != null && currentOperation == CraneOperationKind.AddCargo)
        {
            activeCargoCollider.enabled = false;
        }

        GameObject hookGo = new GameObject("StorageAmbientCraneHook");
        hookGo.transform.SetParent(centerTransform != null ? centerTransform : transform, false);
        activeHook = hookGo.AddComponent<StorageCraneHookFx>();
        float sideX = operationFromLeft ? interiorLeft : interiorRight;
        activeHook.ConfigureSide(targetObject.transform, sideX, operationFromLeft, operationDuration, telegraphFraction, craneWarningColor, craneActiveColor);
        GlitchAudioManager.PlayStorageCraneStart(targetObject.transform.position);
        gameManager?.NotifyMapEventStarted(
            "storage_ambient_crane",
            "GRUA DE CARGA",
            "El gancho amarillo anticipa qué objeto va a mover, agregar o retirar la grúa. No cruces su recorrido y revisa el nuevo espacio cuando termine.");
    }

    private void TickOperation()
    {
        operationAge += Time.deltaTime;
        float progress = Mathf.Clamp01(operationAge / Mathf.Max(0.1f, operationDuration));
        float moveStart = Mathf.Clamp01(telegraphFraction);
        float moveEnd = Mathf.Clamp(activeEndFraction, moveStart + 0.05f, 0.98f);
        float moveT = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(moveStart, moveEnd, progress));

        Vector2 position = Vector2.Lerp(operationStartPosition, operationTargetPosition, moveT);
        if (currentOperation == CraneOperationKind.MoveObstacle)
        {
            MoveObstacle(activeObstacle, position);
        }
        else if (activeCargo != null)
        {
            activeCargo.transform.position = new Vector3(position.x, position.y, activeCargo.transform.position.z);
        }

        if (currentOperation == CraneOperationKind.AddCargo && activeCargoCollider != null && progress >= moveEnd)
        {
            activeCargoCollider.enabled = true;
        }
        if (!operationImpactPlayed && progress >= moveEnd)
        {
            operationImpactPlayed = true;
            if (currentOperation == CraneOperationKind.RemoveCargo)
            {
                GlitchAudioManager.PlayStorageCargoRemove(position);
            }
            else
            {
                GlitchAudioManager.PlayStorageCargoImpact(position);
            }
        }

        if (progress >= 1f)
        {
            FinishOperation();
        }
    }

    private void FinishOperation()
    {
        if (currentOperation == CraneOperationKind.RemoveCargo && activeCargo != null)
        {
            ambientCargoBlocks.Remove(activeCargo);
            DestroyByPlayState(activeCargo);
        }

        if (currentOperation == CraneOperationKind.MoveObstacle && activeDynamicController != null)
        {
            activeDynamicController.enabled = activeDynamicWasEnabled;
        }

        ClearHook();
        operationActive = false;
        activeCargo = null;
        activeCargoCollider = null;
        activeDynamicController = null;
        ScheduleNextOperation();
    }

    private void ClearOperation()
    {
        if (operationActive && currentOperation == CraneOperationKind.MoveObstacle && activeDynamicController != null)
        {
            activeDynamicController.enabled = activeDynamicWasEnabled;
        }

        ClearHook();
        operationActive = false;
        activeCargo = null;
        activeCargoCollider = null;
        activeDynamicController = null;
    }

    private void RebuildMovableObstacles()
    {
        movableObstacles.Clear();
        dedupe.Clear();
        AddMovablesFromRoot(obstaclesRoot);
        AddMovablesFromRoot(dynamicRoot);
    }

    private void AddMovablesFromRoot(Transform root)
    {
        if (root == null)
        {
            return;
        }

        Collider2D[] colliders = root.GetComponentsInChildren<Collider2D>(includeInactive: true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D col = colliders[i];
            if (col == null || col.isTrigger || !col.gameObject.activeInHierarchy)
            {
                continue;
            }

            Transform target = col.transform;
            if (target == null || !dedupe.Add(target))
            {
                continue;
            }
            if (target.GetComponent<PlayerController>() != null || target.GetComponent<EnemyController>() != null)
            {
                continue;
            }

            movableObstacles.Add(new MovableObstacle
            {
                transform = target,
                rigidbody = target.GetComponent<Rigidbody2D>(),
                collider = col,
                dynamicController = target.GetComponent<DynamicObstacleController>(),
                radius = Mathf.Max(col.bounds.extents.x, col.bounds.extents.y, 0.35f)
            });
        }
    }

    private bool TryPickRelocation(Vector2 start, float radius, Transform ignored, out Vector2 target)
    {
        int attempts = Mathf.Max(4, Mathf.RoundToInt(relocationAttempts));
        for (int i = 0; i < attempts; i++)
        {
            Vector2 dir = Random.insideUnitCircle;
            if (dir.sqrMagnitude < 0.05f)
            {
                dir = Random.value < 0.5f ? Vector2.right : Vector2.up;
            }

            float distance = Random.Range(
                Mathf.Min(relocationDistanceRange.x, relocationDistanceRange.y),
                Mathf.Max(relocationDistanceRange.x, relocationDistanceRange.y));
            Vector2 candidate = ClampToInterior(start + dir.normalized * distance, radius);
            if (IsClearCircle(candidate, radius, ignored))
            {
                target = candidate;
                return true;
            }
        }

        target = start;
        return false;
    }

    private bool TryPickClearPosition(Vector2 size, Transform ignored, out Vector2 target)
    {
        float marginX = Mathf.Max(0.8f, size.x * 0.5f + 0.2f);
        float marginY = Mathf.Max(0.8f, size.y * 0.5f + 0.2f);
        float xMin = Mathf.Min(interiorLeft + marginX, interiorRight - marginX);
        float xMax = Mathf.Max(interiorLeft + marginX, interiorRight - marginX);
        float yMin = Mathf.Min(interiorBottom + marginY, interiorTop - marginY);
        float yMax = Mathf.Max(interiorBottom + marginY, interiorTop - marginY);

        for (int i = 0; i < 40; i++)
        {
            Vector2 candidate = new Vector2(Random.Range(xMin, xMax), Random.Range(yMin, yMax));
            if (IsClearBox(candidate, size, ignored))
            {
                target = candidate;
                return true;
            }
        }

        target = Vector2.zero;
        return false;
    }

    private bool IsClearCircle(Vector2 position, float radius, Transform ignored)
    {
        if (!IsSafeFromActors(position))
        {
            return false;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(position, radius + obstacleClearance);
        return HasOnlyAllowedHits(hits, ignored);
    }

    private bool IsClearBox(Vector2 position, Vector2 size, Transform ignored)
    {
        if (!IsSafeFromActors(position))
        {
            return false;
        }

        Collider2D[] hits = Physics2D.OverlapBoxAll(position, size + Vector2.one * obstacleClearance, 0f);
        return HasOnlyAllowedHits(hits, ignored);
    }

    private bool HasOnlyAllowedHits(Collider2D[] hits, Transform ignored)
    {
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.isTrigger)
            {
                continue;
            }
            if (ignored != null && (hit.transform == ignored || hit.transform.IsChildOf(ignored)))
            {
                continue;
            }
            if (hit.GetComponent<PlayerController>() != null || hit.GetComponent<EnemyController>() != null || hit.GetComponent<SplitAnomalyCloneController>() != null)
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

    private Vector2 RollCargoSize()
    {
        float width = Random.Range(Mathf.Min(cargoSizeMin.x, cargoSizeMax.x), Mathf.Max(cargoSizeMin.x, cargoSizeMax.x));
        float height = Random.Range(Mathf.Min(cargoSizeMin.y, cargoSizeMax.y), Mathf.Max(cargoSizeMin.y, cargoSizeMax.y));
        if (Random.value < 0.5f)
        {
            float swap = width;
            width = height;
            height = swap;
        }

        return new Vector2(Mathf.Max(0.35f, width), Mathf.Max(0.35f, height));
    }

    private GameObject CreateCargoBlock(string objectName, Vector2 size, Vector2 position, bool colliderEnabled)
    {
        GameObject cargo = new GameObject(objectName);
        cargo.transform.SetParent(dynamicRoot != null ? dynamicRoot : centerTransform, false);
        cargo.transform.position = new Vector3(position.x, position.y, 0f);

        Rigidbody2D rb = cargo.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        BoxCollider2D box = cargo.AddComponent<BoxCollider2D>();
        box.size = size;
        box.enabled = colliderEnabled;

        SpriteRenderer body = cargo.AddComponent<SpriteRenderer>();
        body.sprite = SquareSpriteProvider.Get();
        body.drawMode = SpriteDrawMode.Sliced;
        body.sortingOrder = 4;
        body.size = size;
        body.color = cargoBodyColor;

        CreateCargoStripe(cargo.transform, "CargoStripeA", new Vector2(size.x, 0.08f), new Vector2(0f, size.y * 0.28f));
        CreateCargoStripe(cargo.transform, "CargoStripeB", new Vector2(size.x, 0.08f), new Vector2(0f, -size.y * 0.28f));
        return cargo;
    }

    private void CreateCargoStripe(Transform parent, string objectName, Vector2 size, Vector2 localPosition)
    {
        GameObject stripeGo = new GameObject(objectName);
        stripeGo.transform.SetParent(parent, false);
        stripeGo.transform.localPosition = localPosition;
        SpriteRenderer stripe = stripeGo.AddComponent<SpriteRenderer>();
        stripe.sprite = SquareSpriteProvider.Get();
        stripe.drawMode = SpriteDrawMode.Sliced;
        stripe.sortingOrder = 5;
        stripe.size = size;
        stripe.color = cargoAccentColor;
    }

    private void MoveObstacle(MovableObstacle obstacle, Vector2 target)
    {
        if (obstacle.rigidbody != null && obstacle.rigidbody.bodyType == RigidbodyType2D.Kinematic)
        {
            obstacle.rigidbody.MovePosition(target);
        }
        else if (obstacle.transform != null)
        {
            obstacle.transform.position = new Vector3(target.x, target.y, obstacle.transform.position.z);
        }
    }

    private void ClearHook()
    {
        if (activeHook == null)
        {
            return;
        }

        DestroyByPlayState(activeHook.gameObject);
        activeHook = null;
    }

    private void ClearAmbientCargo()
    {
        for (int i = ambientCargoBlocks.Count - 1; i >= 0; i--)
        {
            if (ambientCargoBlocks[i] != null)
            {
                DestroyByPlayState(ambientCargoBlocks[i]);
            }
        }

        ambientCargoBlocks.Clear();
    }

    private void PruneAmbientCargo()
    {
        for (int i = ambientCargoBlocks.Count - 1; i >= 0; i--)
        {
            if (ambientCargoBlocks[i] == null)
            {
                ambientCargoBlocks.RemoveAt(i);
            }
        }
    }

    private void ScheduleNextOperation()
    {
        ScheduleNextOperation(operationIntervalRange.x, operationIntervalRange.y);
    }

    private void ScheduleNextOperation(float minOverride, float maxOverride)
    {
        operationTimer = Random.Range(Mathf.Min(minOverride, maxOverride), Mathf.Max(minOverride, maxOverride));
    }

    private void ApplyOperationModifiersOnce()
    {
        if (operationModifiersApplied)
        {
            return;
        }

        operationModifiersApplied = true;
        if (ContainmentOperationStorage.SelectedOperation.id != ContainmentOperationStorage.AmbientOverdriveId)
        {
            return;
        }

        operationIntervalRange = new Vector2(1.1f, 2.2f);
        operationDuration = Mathf.Max(1.35f, operationDuration * 0.62f);
        telegraphFraction = Mathf.Clamp(telegraphFraction * 0.62f, 0.05f, 0.42f);
        maxAmbientCargoBlocks += 10;
        addCargoChance = Mathf.Clamp01(addCargoChance + 0.45f);
        removeCargoChance = Mathf.Clamp01(removeCargoChance + 0.18f);
        relocationDistanceRange = new Vector2(
            Mathf.Max(relocationDistanceRange.x, 2.8f),
            Mathf.Max(relocationDistanceRange.y, 6.0f));
    }

    private Vector2 ClampToInterior(Vector2 position, float radius)
    {
        float margin = Mathf.Max(0f, radius + 0.12f);
        position.x = Mathf.Clamp(position.x, interiorLeft + margin, interiorRight - margin);
        position.y = Mathf.Clamp(position.y, interiorBottom + margin, interiorTop - margin);
        return position;
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
}

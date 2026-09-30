using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

// Banco de pruebas local para builds de desarrollo. Recorre estados, eventos y arenas,
// observa problemas frecuentes y deja un informe sin intervenir en una run normal.
[DisallowMultipleComponent]
[DefaultExecutionOrder(850)]
public sealed class GameplayQaDirector : MonoBehaviour
{
    private enum QaMode
    {
        CurrentArena,
        FullSweep
    }

    [Serializable]
    private sealed class QaReport
    {
        public string startedAtUtc;
        public string endedAtUtc;
        public string unityVersion;
        public string gameVersion;
        public string mode;
        public string originalArena;
        public string result;
        public float durationSeconds;
        public float averageFps;
        public float lowestFps;
        public float maximumFrameMilliseconds;
        public int framesOver33Milliseconds;
        public int framesOver50Milliseconds;
        public int framesOver100Milliseconds;
        public long peakAllocatedMemoryBytes;
        public int peakSceneObjects;
        public int peakColliders;
        public int peakProjectiles;
        public int peakPickups;
        public int arenasValidated;
        public int statesAttempted;
        public int statesActivated;
        public int eventsAttempted;
        public int eventsActivated;
        public int routeChecks;
        public int routeFailures;
        public int embeddedSpawnWarnings;
        public int hazardOverlapWarnings;
        public int enemyStuckEpisodes;
        public float longestEnemyStuckSeconds;
        public int unexpectedDestroyerActivations;
        public int capturedRuntimeErrors;
        public int warnings;
        public int failures;
        public List<string> checks = new List<string>();
        public List<string> runtimeErrors = new List<string>();
    }

    private const float StateObservationSeconds = 0.9f;
    private const float EventObservationSeconds = 1.8f;
    private const float EnemyStuckDistance = 1.65f;
    private const float EnemyStuckSpeed = 0.28f;
    private const float EnemyStuckMovement = 0.035f;
    private const float EnemyStuckConfirmSeconds = 1.35f;
    private const int MaxRuntimeErrors = 40;

    private ProceduralArenaGenerator arena;
    private PlayerController player;
    private EnemyController enemy;
    private GameManager gameManager;
    private Coroutine activeRoutine;
    private QaReport report;
    private ProceduralArenaGenerator.ArenaTheme originalTheme;
    private EnemyController.AnomalyState originalState;
    private string statusLine = "Listo: F7 actual / Shift+F7 total";
    private string lastReportPath = string.Empty;
    private float runStartedRealtime;
    private float frameTimeTotal;
    private int sampledFrames;
    private float lowestFps = float.PositiveInfinity;
    private float metricsRefreshTimer;
    private Vector2 lastEnemyPosition;
    private float stuckCandidateSeconds;
    private bool stuckEpisodeRegistered;
    private bool forcingDestroyer;
    private EnemyController.AnomalyState lastEnemyState;
    private bool hasLastEnemyState;
    private int completedSteps;
    private int estimatedSteps;

    public bool IsRunning => activeRoutine != null;
    public string LastReportPath => lastReportPath;
    public string StatusLine
    {
        get
        {
            if (!IsRunning || estimatedSteps <= 0)
            {
                return statusLine;
            }

            int progress = Mathf.RoundToInt(100f * completedSteps / Mathf.Max(1f, estimatedSteps));
            return $"{statusLine} ({Mathf.Clamp(progress, 0, 99)}%)";
        }
    }

    public void Configure(ProceduralArenaGenerator arenaReference)
    {
        arena = arenaReference;
        ResolveReferences();
    }

    private void OnEnable()
    {
        Application.logMessageReceived += CaptureRuntimeLog;
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= CaptureRuntimeLog;
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }
        RestoreQaOverrides();
    }

    private void Update()
    {
        if (!IsRunning || report == null)
        {
            return;
        }

        SampleFrameMetrics();
        SampleEnemyNavigation();

        metricsRefreshTimer -= Time.unscaledDeltaTime;
        if (metricsRefreshTimer <= 0f)
        {
            metricsRefreshTimer = 0.75f;
            SampleScenePeaks();
        }
    }

    public void StartCurrentArenaCheck()
    {
        StartQa(QaMode.CurrentArena);
    }

    public void StartFullSweep()
    {
        StartQa(QaMode.FullSweep);
    }

    private void StartQa(QaMode mode)
    {
        if (!Application.isEditor && !Debug.isDebugBuild)
        {
            statusLine = "QA solo esta disponible en Editor o Development Build.";
            return;
        }
        if (IsRunning)
        {
            statusLine = "Ya hay una prueba en curso.";
            return;
        }

        ResolveReferences();
        if (arena == null || player == null || enemy == null || gameManager == null)
        {
            statusLine = "Faltan arena, jugador, anomalia o GameManager.";
            return;
        }
        if (!gameManager.IsRunActive || gameManager.IsGameOver || Time.timeScale <= 0f ||
            gameManager.IsIntroTutorialOpen || gameManager.ShouldShowCursorForContextTutorial)
        {
            statusLine = "Inicia una run activa y cierra tutoriales/pausa antes del QA.";
            return;
        }

        ResetMetrics(mode);
        gameManager.SetDebugQaMode(true);
        player.SetDebugQaInvulnerable(true);
        activeRoutine = StartCoroutine(RunQa(mode));
    }

    public void StopQa(string reason)
    {
        if (!IsRunning)
        {
            return;
        }

        StopCoroutine(activeRoutine);
        activeRoutine = null;
        AddWarning(string.IsNullOrWhiteSpace(reason) ? "Prueba cancelada." : reason);
        FinishReport("CANCELADO");
    }

    private void ResetMetrics(QaMode mode)
    {
        originalTheme = arena.ActiveTheme;
        originalState = enemy.CurrentState;
        report = new QaReport
        {
            startedAtUtc = DateTime.UtcNow.ToString("o"),
            unityVersion = Application.unityVersion,
            gameVersion = Application.version,
            mode = mode == QaMode.FullSweep ? "Recorrido total" : "Arena actual",
            originalArena = arena.ActiveThemeLabel
        };
        runStartedRealtime = Time.realtimeSinceStartup;
        frameTimeTotal = 0f;
        sampledFrames = 0;
        lowestFps = float.PositiveInfinity;
        metricsRefreshTimer = 0f;
        lastEnemyPosition = enemy.transform.position;
        stuckCandidateSeconds = 0f;
        stuckEpisodeRegistered = false;
        forcingDestroyer = false;
        lastEnemyState = enemy.CurrentState;
        hasLastEnemyState = true;
        completedSteps = 0;
        int stateCount = Enum.GetValues(typeof(EnemyController.AnomalyState)).Length;
        estimatedSteps = stateCount + (mode == QaMode.FullSweep ? 15 : 3);
        lastReportPath = string.Empty;
        statusLine = "Preparando prueba";
        SampleScenePeaks();
        AddPass($"Inicio valido en {arena.ActiveThemeLabel}.");
    }

    private IEnumerator RunQa(QaMode mode)
    {
        yield return null;
        ValidateArena("inicio");

        if (mode == QaMode.FullSweep)
        {
            Array themes = Enum.GetValues(typeof(ProceduralArenaGenerator.ArenaTheme));
            for (int i = 0; i < themes.Length; i++)
            {
                ProceduralArenaGenerator.ArenaTheme theme = (ProceduralArenaGenerator.ArenaTheme)themes.GetValue(i);
                statusLine = $"Montando arena {theme}";
                if (arena.ActiveTheme != theme)
                {
                    float transitionStart = Time.realtimeSinceStartup;
                    if (!arena.ForceThemeForDebug(theme))
                    {
                        AddFailure($"No se pudo iniciar la transicion a {theme}.");
                        continue;
                    }
                    yield return WaitForThemeTransition(5f);
                    float transitionSeconds = Time.realtimeSinceStartup - transitionStart;
                    if (transitionSeconds > 1.25f)
                    {
                        AddWarning($"La transicion de {theme} demoro {transitionSeconds:0.00}s.");
                    }
                }

                yield return new WaitForSecondsRealtime(0.55f);
                ResolveReferences();
                ValidateArena(theme.ToString());
                completedSteps++;
                yield return TestArenaEvents();
            }
        }
        else
        {
            yield return TestArenaEvents();
        }

        yield return TestEnemyStates();

        if (mode == QaMode.FullSweep && arena != null && arena.ActiveTheme != originalTheme)
        {
            statusLine = "Restaurando arena original";
            if (arena.ForceThemeForDebug(originalTheme))
            {
                yield return WaitForThemeTransition(5f);
                yield return new WaitForSecondsRealtime(0.45f);
                ValidateArena("restauracion");
            }
            else
            {
                AddWarning("No se pudo restaurar la arena original.");
            }
        }

        ResolveReferences();
        if (enemy != null)
        {
            enemy.ForceStateForDebug(originalState);
        }

        activeRoutine = null;
        FinishReport("AUTO");
    }

    private IEnumerator WaitForThemeTransition(float timeoutSeconds)
    {
        float started = Time.realtimeSinceStartup;
        yield return null;
        while (arena != null && arena.IsDebugThemeTransitionActive &&
               Time.realtimeSinceStartup - started < timeoutSeconds)
        {
            yield return null;
        }

        if (arena != null && arena.IsDebugThemeTransitionActive)
        {
            AddFailure("La reconstruccion de arena excedio el timeout de QA.");
        }
    }

    private IEnumerator TestEnemyStates()
    {
        ResolveReferences();
        if (enemy == null)
        {
            AddFailure("No se encontro la anomalia para probar sus estados.");
            yield break;
        }

        Array states = Enum.GetValues(typeof(EnemyController.AnomalyState));
        for (int i = 0; i < states.Length; i++)
        {
            EnemyController.AnomalyState state = (EnemyController.AnomalyState)states.GetValue(i);
            statusLine = $"Estado {state}";
            report.statesAttempted++;
            forcingDestroyer = state == EnemyController.AnomalyState.Destroyer;
            bool activated = enemy.ForceStateForDebug(state);
            if (activated)
            {
                report.statesActivated++;
            }
            else
            {
                AddFailure($"El estado {state} rechazo la activacion de prueba.");
            }

            yield return new WaitForSecondsRealtime(StateObservationSeconds);
            if (enemy == null)
            {
                AddFailure($"La anomalia desaparecio durante {state}.");
                yield break;
            }
            completedSteps++;
            forcingDestroyer = false;
        }

        AddPass($"Estados activados: {report.statesActivated}/{report.statesAttempted}.");
    }

    private IEnumerator TestArenaEvents()
    {
        if (arena == null)
        {
            yield break;
        }

        List<string> labels = new List<string>();
        arena.GetDebugEventLabels(labels);
        if (labels.Count == 0)
        {
            AddFailure($"{arena.ActiveThemeLabel}: no hay eventos de debug registrados.");
            yield break;
        }

        string arenaLabel = arena.ActiveThemeLabel;
        for (int i = 0; i < labels.Count; i++)
        {
            string expectedLabel = labels[i];
            statusLine = $"Evento {expectedLabel}";
            report.eventsAttempted++;
            bool activated = false;
            string actualLabel = expectedLabel;
            for (int retry = 0; retry < 3 && !activated; retry++)
            {
                activated = arena.TriggerDebugEventAt(i, out actualLabel);
                if (!activated)
                {
                    yield return new WaitForSecondsRealtime(0.35f);
                }
            }

            if (activated)
            {
                report.eventsActivated++;
                AddPass($"{arenaLabel}: evento '{actualLabel}' activado.");
                yield return new WaitForSecondsRealtime(EventObservationSeconds);
                ValidateEmbeddedSpawns($"evento {actualLabel}");
            }
            else
            {
                AddFailure($"{arenaLabel}: el evento '{expectedLabel}' no pudo activarse tras 3 intentos.");
            }
            completedSteps++;
        }
    }

    private void ValidateArena(string context)
    {
        if (arena == null)
        {
            AddFailure($"{context}: referencia de arena perdida.");
            return;
        }

        report.arenasValidated++;
        ValidateGenerationRoots(context);
        ValidateThemeControllers(context);
        ValidateTemporaryObjectOwnership(context);
        ValidateEmbeddedSpawns(context);
        ValidateRoutes(context);
    }

    private void ValidateGenerationRoots(string context)
    {
        string[] requiredRoots = { "Bounds", "Obstacles", "DynamicObstacles", "Details" };
        for (int r = 0; r < requiredRoots.Length; r++)
        {
            int count = 0;
            for (int i = 0; i < arena.transform.childCount; i++)
            {
                Transform child = arena.transform.GetChild(i);
                if (child != null && child.name == requiredRoots[r])
                {
                    count++;
                }
            }

            if (count != 1)
            {
                AddFailure($"{context}: raiz {requiredRoots[r]} aparece {count} veces.");
            }
        }
    }

    private void ValidateThemeControllers(string context)
    {
        string expectedPrefix = arena.ActiveTheme == ProceduralArenaGenerator.ArenaTheme.ContainmentLab ? "Lab" :
            arena.ActiveTheme == ProceduralArenaGenerator.ArenaTheme.StorageBay ? "Storage" :
            arena.ActiveTheme == ProceduralArenaGenerator.ArenaTheme.RuptureZone ? "Rupture" :
            arena.ActiveTheme == ProceduralArenaGenerator.ArenaTheme.DataCore ? "Core" : "Archive";

        List<string> labels = new List<string>();
        arena.GetDebugEventLabels(labels);
        for (int i = 0; i < labels.Count; i++)
        {
            if (!labels[i].StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                AddFailure($"{context}: quedo vivo un controlador ajeno ('{labels[i]}').");
            }
        }

        if (labels.Count == 0)
        {
            AddFailure($"{context}: {expectedPrefix} no registro controladores de evento.");
        }
        else
        {
            AddPass($"{context}: {labels.Count} controlador(es) tematico(s) coherentes.");
        }
    }

    private void ValidateTemporaryObjectOwnership(string context)
    {
        Transform[] descendants = arena.GetComponentsInChildren<Transform>(true);
        int staleObjects = 0;
        for (int i = 0; i < descendants.Length; i++)
        {
            Transform candidate = descendants[i];
            if (candidate == null || candidate == arena.transform)
            {
                continue;
            }

            ProceduralArenaGenerator.ArenaTheme owner;
            if (!TryGetTemporaryObjectTheme(candidate.name, out owner) || owner == arena.ActiveTheme)
            {
                continue;
            }

            staleObjects++;
            if (staleObjects <= 4)
            {
                AddFailure($"{context}: objeto temporal viejo '{candidate.name}' pertenece a {owner}.");
            }
        }

        if (staleObjects > 4)
        {
            AddFailure($"{context}: hay {staleObjects - 4} objetos temporales viejos adicionales.");
        }
        if (staleObjects == 0)
        {
            AddPass($"{context}: sin residuos temporales de otras arenas.");
        }
    }

    private static bool TryGetTemporaryObjectTheme(string objectName, out ProceduralArenaGenerator.ArenaTheme theme)
    {
        string name = objectName ?? string.Empty;
        if (name.StartsWith("LabSterilization", StringComparison.Ordinal) ||
            name.StartsWith("LabSecurity", StringComparison.Ordinal) ||
            name.StartsWith("LabContainment", StringComparison.Ordinal) ||
            name.StartsWith("LabAmbient", StringComparison.Ordinal) ||
            name.StartsWith("LabSignature", StringComparison.Ordinal) ||
            name.StartsWith("SafeCorridor", StringComparison.Ordinal))
        {
            theme = ProceduralArenaGenerator.ArenaTheme.ContainmentLab;
            return true;
        }
        if (name.StartsWith("StorageConveyor", StringComparison.Ordinal) ||
            name.StartsWith("StorageCargo", StringComparison.Ordinal) ||
            name.StartsWith("StorageSignature", StringComparison.Ordinal) ||
            name.StartsWith("StorageAmbient", StringComparison.Ordinal))
        {
            theme = ProceduralArenaGenerator.ArenaTheme.StorageBay;
            return true;
        }
        if (name.StartsWith("RuptureRift", StringComparison.Ordinal) ||
            name.StartsWith("RuptureResonance", StringComparison.Ordinal) ||
            name.StartsWith("RuptureEcho", StringComparison.Ordinal) ||
            name.StartsWith("RupturePlayerEcho", StringComparison.Ordinal) ||
            name.StartsWith("RuptureSignature", StringComparison.Ordinal) ||
            name.StartsWith("RuptureAmbient", StringComparison.Ordinal))
        {
            theme = ProceduralArenaGenerator.ArenaTheme.RuptureZone;
            return true;
        }
        if (name.StartsWith("CoreRelay", StringComparison.Ordinal) ||
            name.StartsWith("CoreProgrammed", StringComparison.Ordinal) ||
            name.StartsWith("CoreContainment", StringComparison.Ordinal) ||
            name.StartsWith("CoreSpikes", StringComparison.Ordinal))
        {
            theme = ProceduralArenaGenerator.ArenaTheme.DataCore;
            return true;
        }
        if (name.StartsWith("ArchiveNull", StringComparison.Ordinal))
        {
            theme = ProceduralArenaGenerator.ArenaTheme.NullArchive;
            return true;
        }

        theme = default;
        return false;
    }

    private void ValidateEmbeddedSpawns(string context)
    {
        List<Collider2D> obstacles = CollectObstacleColliders();
        int embedded = 0;
        embedded += CountEmbeddedComponents(UnityEngine.Object.FindObjectsByType<ArenaScorePickup>(), obstacles);
        embedded += CountEmbeddedComponents(UnityEngine.Object.FindObjectsByType<ArenaPowerupPickup>(), obstacles);
        embedded += CountEmbeddedComponents(UnityEngine.Object.FindObjectsByType<ArenaObjectiveNode>(), obstacles);

        if (player != null && IsPointBlocked(player.transform.position, obstacles, 0.08f))
        {
            embedded++;
            AddWarning($"{context}: el jugador quedo dentro de un obstaculo.");
        }
        if (enemy != null && IsPointBlocked(enemy.transform.position, obstacles, 0.08f))
        {
            embedded++;
            AddWarning($"{context}: la anomalia quedo dentro de un obstaculo.");
        }

        if (embedded > 0)
        {
            report.embeddedSpawnWarnings += embedded;
            AddWarning($"{context}: se detectaron {embedded} objetos/actores solapados con geometria.");
        }

        List<Collider2D> hazards = CollectHazardColliders();
        int hazardOverlaps = 0;
        hazardOverlaps += CountHazardOverlaps(UnityEngine.Object.FindObjectsByType<ArenaScorePickup>(), hazards);
        hazardOverlaps += CountHazardOverlaps(UnityEngine.Object.FindObjectsByType<ArenaPowerupPickup>(), hazards);
        hazardOverlaps += CountHazardOverlaps(UnityEngine.Object.FindObjectsByType<ArenaObjectiveNode>(), hazards);
        if (hazardOverlaps > 0)
        {
            report.hazardOverlapWarnings += hazardOverlaps;
            AddWarning($"{context}: {hazardOverlaps} recompensa(s)/objetivo(s) aparecieron dentro de un peligro activo.");
        }
    }

    private int CountEmbeddedComponents<T>(T[] components, List<Collider2D> obstacles) where T : Component
    {
        int count = 0;
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component == null || !component.gameObject.activeInHierarchy || !IsInsideArena(component.transform.position))
            {
                continue;
            }
            if (IsPointBlocked(component.transform.position, obstacles, 0.04f))
            {
                count++;
            }
        }
        return count;
    }

    private int CountHazardOverlaps<T>(T[] components, List<Collider2D> hazards) where T : Component
    {
        int count = 0;
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component == null || !component.gameObject.activeInHierarchy || !IsInsideArena(component.transform.position))
            {
                continue;
            }
            Vector2 point = component.transform.position;
            for (int h = 0; h < hazards.Count; h++)
            {
                if (hazards[h] != null && hazards[h].enabled && hazards[h].gameObject.activeInHierarchy && hazards[h].OverlapPoint(point))
                {
                    count++;
                    break;
                }
            }
        }
        return count;
    }

    private void ValidateRoutes(string context)
    {
        if (player == null || enemy == null)
        {
            AddFailure($"{context}: no se puede validar rutas sin ambos actores.");
            return;
        }

        const float cellSize = 0.65f;
        const float actorRadius = 0.23f;
        float halfWidth = Mathf.Max(2f, arena.ArenaWidth * 0.5f - 0.85f);
        float halfHeight = Mathf.Max(2f, arena.ArenaHeight * 0.5f - 0.85f);
        Vector2 center = arena.transform.position;
        int width = Mathf.Max(3, Mathf.FloorToInt(halfWidth * 2f / cellSize) + 1);
        int height = Mathf.Max(3, Mathf.FloorToInt(halfHeight * 2f / cellSize) + 1);
        Vector2 origin = center - new Vector2((width - 1) * cellSize * 0.5f, (height - 1) * cellSize * 0.5f);
        List<Collider2D> obstacles = CollectObstacleColliders();
        bool[,] walkable = new bool[width, height];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2 point = origin + new Vector2(x * cellSize, y * cellSize);
                walkable[x, y] = !IsPointBlocked(point, obstacles, actorRadius);
            }
        }

        Vector2Int start = FindNearestWalkable(WorldToGrid(player.transform.position, origin, cellSize, width, height), walkable);
        bool[,] reached = FloodFill(start, walkable);
        List<Transform> targets = new List<Transform> { enemy.transform };
        AddActiveTargets(UnityEngine.Object.FindObjectsByType<ArenaObjectiveNode>(), targets);
        AddActiveTargets(UnityEngine.Object.FindObjectsByType<LabContainmentTerminal>(), targets);
        AddActiveTargets(UnityEngine.Object.FindObjectsByType<CoreRelayNodeTrigger>(), targets);
        AddActiveTargets(UnityEngine.Object.FindObjectsByType<ArenaBreachGate>(), targets);

        int failuresBefore = report.routeFailures;
        for (int i = 0; i < targets.Count; i++)
        {
            Transform target = targets[i];
            if (target == null || !target.gameObject.activeInHierarchy || !IsInsideArena(target.position))
            {
                continue;
            }

            report.routeChecks++;
            Vector2Int cell = FindNearestWalkable(WorldToGrid(target.position, origin, cellSize, width, height), walkable);
            if (!IsGridValid(cell, width, height) || !reached[cell.x, cell.y])
            {
                report.routeFailures++;
                AddFailure($"{context}: no hay ruta base jugador -> {target.name}.");
            }
        }

        if (report.routeFailures == failuresBefore)
        {
            AddPass($"{context}: {targets.Count} destino(s) alcanzables en la malla independiente.");
        }
    }

    private void AddActiveTargets<T>(T[] components, List<Transform> targets) where T : Component
    {
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] != null && components[i].gameObject.activeInHierarchy && IsInsideArena(components[i].transform.position))
            {
                targets.Add(components[i].transform);
            }
        }
    }

    private static Vector2Int WorldToGrid(Vector2 world, Vector2 origin, float cellSize, int width, int height)
    {
        return new Vector2Int(
            Mathf.Clamp(Mathf.RoundToInt((world.x - origin.x) / cellSize), 0, width - 1),
            Mathf.Clamp(Mathf.RoundToInt((world.y - origin.y) / cellSize), 0, height - 1));
    }

    private static Vector2Int FindNearestWalkable(Vector2Int source, bool[,] walkable)
    {
        int width = walkable.GetLength(0);
        int height = walkable.GetLength(1);
        if (walkable[source.x, source.y])
        {
            return source;
        }

        int maxRadius = Mathf.Max(width, height);
        for (int radius = 1; radius < maxRadius; radius++)
        {
            for (int x = source.x - radius; x <= source.x + radius; x++)
            {
                for (int y = source.y - radius; y <= source.y + radius; y++)
                {
                    if (x < 0 || y < 0 || x >= width || y >= height ||
                        (Mathf.Abs(x - source.x) != radius && Mathf.Abs(y - source.y) != radius))
                    {
                        continue;
                    }
                    if (walkable[x, y])
                    {
                        return new Vector2Int(x, y);
                    }
                }
            }
        }

        return new Vector2Int(-1, -1);
    }

    private static bool[,] FloodFill(Vector2Int start, bool[,] walkable)
    {
        int width = walkable.GetLength(0);
        int height = walkable.GetLength(1);
        bool[,] reached = new bool[width, height];
        if (!IsGridValid(start, width, height))
        {
            return reached;
        }

        Queue<Vector2Int> open = new Queue<Vector2Int>();
        reached[start.x, start.y] = true;
        open.Enqueue(start);
        Vector2Int[] directions = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
        while (open.Count > 0)
        {
            Vector2Int current = open.Dequeue();
            for (int i = 0; i < directions.Length; i++)
            {
                Vector2Int next = current + directions[i];
                if (!IsGridValid(next, width, height) || reached[next.x, next.y] || !walkable[next.x, next.y])
                {
                    continue;
                }
                reached[next.x, next.y] = true;
                open.Enqueue(next);
            }
        }
        return reached;
    }

    private static bool IsGridValid(Vector2Int cell, int width, int height)
    {
        return cell.x >= 0 && cell.y >= 0 && cell.x < width && cell.y < height;
    }

    private List<Collider2D> CollectObstacleColliders()
    {
        List<Collider2D> colliders = new List<Collider2D>();
        if (arena == null)
        {
            return colliders;
        }

        AddRootColliders(arena.transform.Find("Obstacles"), colliders);
        AddRootColliders(arena.transform.Find("DynamicObstacles"), colliders);
        return colliders;
    }

    private static List<Collider2D> CollectHazardColliders()
    {
        List<Collider2D> colliders = new List<Collider2D>();
        ContainmentHazardZone[] zones = UnityEngine.Object.FindObjectsByType<ContainmentHazardZone>();
        for (int i = 0; i < zones.Length; i++)
        {
            Collider2D collider = zones[i] != null ? zones[i].GetComponent<Collider2D>() : null;
            if (collider != null && collider.enabled) colliders.Add(collider);
        }
        CoreSpikeHazard[] spikes = UnityEngine.Object.FindObjectsByType<CoreSpikeHazard>();
        for (int i = 0; i < spikes.Length; i++)
        {
            Collider2D collider = spikes[i] != null ? spikes[i].GetComponent<Collider2D>() : null;
            if (collider != null && collider.enabled) colliders.Add(collider);
        }
        return colliders;
    }

    private static void AddRootColliders(Transform root, List<Collider2D> output)
    {
        if (root == null)
        {
            return;
        }
        Collider2D[] found = root.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < found.Length; i++)
        {
            if (found[i] != null && found[i].enabled && !found[i].isTrigger && found[i].gameObject.activeInHierarchy)
            {
                output.Add(found[i]);
            }
        }
    }

    private static bool IsPointBlocked(Vector2 point, List<Collider2D> obstacles, float clearance)
    {
        float clearanceSqr = Mathf.Max(0f, clearance) * Mathf.Max(0f, clearance);
        for (int i = 0; i < obstacles.Count; i++)
        {
            Collider2D obstacle = obstacles[i];
            if (obstacle == null || !obstacle.enabled || !obstacle.gameObject.activeInHierarchy)
            {
                continue;
            }
            Vector2 closest = obstacle.ClosestPoint(point);
            if ((closest - point).sqrMagnitude <= clearanceSqr)
            {
                return true;
            }
        }
        return false;
    }

    private bool IsInsideArena(Vector2 point)
    {
        if (arena == null)
        {
            return false;
        }
        Vector2 local = point - (Vector2)arena.transform.position;
        return Mathf.Abs(local.x) <= arena.ArenaWidth * 0.5f && Mathf.Abs(local.y) <= arena.ArenaHeight * 0.5f;
    }

    private void SampleFrameMetrics()
    {
        float dt = Mathf.Max(0.00001f, Time.unscaledDeltaTime);
        float milliseconds = dt * 1000f;
        float fps = 1f / dt;
        frameTimeTotal += dt;
        sampledFrames++;
        lowestFps = Mathf.Min(lowestFps, fps);
        report.maximumFrameMilliseconds = Mathf.Max(report.maximumFrameMilliseconds, milliseconds);
        if (milliseconds > 33.333f) report.framesOver33Milliseconds++;
        if (milliseconds > 50f) report.framesOver50Milliseconds++;
        if (milliseconds > 100f) report.framesOver100Milliseconds++;
    }

    private void SampleScenePeaks()
    {
        report.peakAllocatedMemoryBytes = Math.Max(report.peakAllocatedMemoryBytes, Profiler.GetTotalAllocatedMemoryLong());
        report.peakSceneObjects = Mathf.Max(report.peakSceneObjects,
            UnityEngine.Object.FindObjectsByType<Transform>().Length);
        report.peakColliders = Mathf.Max(report.peakColliders,
            UnityEngine.Object.FindObjectsByType<Collider2D>().Length);
        report.peakProjectiles = Mathf.Max(report.peakProjectiles,
            UnityEngine.Object.FindObjectsByType<AnomalyProjectile>().Length);
        int pickups = UnityEngine.Object.FindObjectsByType<ArenaScorePickup>().Length +
                      UnityEngine.Object.FindObjectsByType<ArenaPowerupPickup>().Length;
        report.peakPickups = Mathf.Max(report.peakPickups, pickups);
    }

    private void SampleEnemyNavigation()
    {
        ResolveReferences();
        if (enemy == null || player == null)
        {
            return;
        }

        if (hasLastEnemyState && enemy.CurrentState != lastEnemyState &&
            enemy.CurrentState == EnemyController.AnomalyState.Destroyer && !forcingDestroyer)
        {
            report.unexpectedDestroyerActivations++;
        }
        lastEnemyState = enemy.CurrentState;
        hasLastEnemyState = true;

        float dt = Time.unscaledDeltaTime;
        Vector2 enemyPosition = enemy.transform.position;
        float moved = Vector2.Distance(enemyPosition, lastEnemyPosition);
        float targetDistance = Vector2.Distance(enemyPosition, player.transform.position);
        bool candidate = targetDistance > EnemyStuckDistance &&
                         enemy.CurrentVelocity.magnitude < EnemyStuckSpeed &&
                         moved < EnemyStuckMovement;
        if (candidate)
        {
            stuckCandidateSeconds += dt;
            if (!stuckEpisodeRegistered && stuckCandidateSeconds >= EnemyStuckConfirmSeconds)
            {
                stuckEpisodeRegistered = true;
                report.enemyStuckEpisodes++;
            }
            if (stuckEpisodeRegistered)
            {
                report.longestEnemyStuckSeconds = Mathf.Max(report.longestEnemyStuckSeconds, stuckCandidateSeconds);
            }
        }
        else
        {
            stuckCandidateSeconds = 0f;
            stuckEpisodeRegistered = false;
        }
        lastEnemyPosition = enemyPosition;
    }

    private void CaptureRuntimeLog(string condition, string stackTrace, LogType type)
    {
        if (!IsRunning || report == null ||
            (type != LogType.Error && type != LogType.Exception && type != LogType.Assert))
        {
            return;
        }

        report.capturedRuntimeErrors++;
        if (report.runtimeErrors.Count < MaxRuntimeErrors)
        {
            string firstLine = string.IsNullOrEmpty(stackTrace) ? string.Empty : stackTrace.Split('\n')[0];
            report.runtimeErrors.Add($"{type}: {condition} | {firstLine}");
        }
    }

    private void FinishReport(string result)
    {
        RestoreQaOverrides();
        if (report == null)
        {
            statusLine = result;
            return;
        }

        if (stuckEpisodeRegistered)
        {
            report.longestEnemyStuckSeconds = Mathf.Max(report.longestEnemyStuckSeconds, stuckCandidateSeconds);
        }
        if (report.enemyStuckEpisodes > 0)
        {
            AddWarning($"La anomalia mostro {report.enemyStuckEpisodes} episodio(s) de atasco; maximo {report.longestEnemyStuckSeconds:0.00}s.");
        }
        if (report.unexpectedDestroyerActivations > 0)
        {
            AddWarning($"Destroyer aparecio {report.unexpectedDestroyerActivations} vez/veces fuera de su prueba forzada.");
        }
        if (report.capturedRuntimeErrors > 0)
        {
            AddFailure($"Unity emitio {report.capturedRuntimeErrors} error(es)/excepcion(es) durante el recorrido.");
        }

        if (result == "AUTO")
        {
            result = report.failures > 0 ? "CON FALLOS" : report.warnings > 0 ? "CON AVISOS" : "CORRECTO";
        }

        report.result = result;
        report.endedAtUtc = DateTime.UtcNow.ToString("o");
        report.durationSeconds = Mathf.Max(0f, Time.realtimeSinceStartup - runStartedRealtime);
        report.averageFps = frameTimeTotal > 0.0001f ? sampledFrames / frameTimeTotal : 0f;
        report.lowestFps = float.IsPositiveInfinity(lowestFps) ? 0f : lowestFps;
        SampleScenePeaks();

        try
        {
            string directory = Path.Combine(Application.persistentDataPath, "qa-reports");
            Directory.CreateDirectory(directory);
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string baseName = $"gameplay-qa_{stamp}";
            string jsonPath = Path.Combine(directory, baseName + ".json");
            string textPath = Path.Combine(directory, baseName + ".txt");
            File.WriteAllText(jsonPath, JsonUtility.ToJson(report, true));
            File.WriteAllText(textPath, BuildReadableReport());
            lastReportPath = textPath;
            statusLine = $"{result}: {report.failures} fallos, {report.warnings} avisos";
            Debug.Log($"QA de gameplay finalizado: {statusLine}. Informe: {textPath}");
        }
        catch (Exception exception)
        {
            lastReportPath = string.Empty;
            statusLine = $"QA terminado, pero no se pudo guardar: {exception.Message}";
            Debug.LogWarning(statusLine);
        }
    }

    private string BuildReadableReport()
    {
        StringBuilder builder = new StringBuilder(4096);
        builder.AppendLine("GLITCH - INFORME DE QA AUTOMATIZADO");
        builder.AppendLine($"Resultado: {report.result}");
        builder.AppendLine($"Modo: {report.mode}");
        builder.AppendLine($"Inicio: {report.startedAtUtc}");
        builder.AppendLine($"Duracion: {report.durationSeconds:0.0}s");
        builder.AppendLine();
        builder.AppendLine("COBERTURA");
        builder.AppendLine($"Arenas validadas: {report.arenasValidated}");
        builder.AppendLine($"Estados: {report.statesActivated}/{report.statesAttempted}");
        builder.AppendLine($"Eventos: {report.eventsActivated}/{report.eventsAttempted}");
        builder.AppendLine($"Rutas: {report.routeChecks - report.routeFailures}/{report.routeChecks}");
        builder.AppendLine();
        builder.AppendLine("RENDIMIENTO");
        builder.AppendLine($"FPS promedio / minimo: {report.averageFps:0.0} / {report.lowestFps:0.0}");
        builder.AppendLine($"Frame maximo: {report.maximumFrameMilliseconds:0.00} ms");
        builder.AppendLine($"Frames >33 / >50 / >100 ms: {report.framesOver33Milliseconds} / {report.framesOver50Milliseconds} / {report.framesOver100Milliseconds}");
        builder.AppendLine($"Memoria asignada pico: {report.peakAllocatedMemoryBytes / (1024f * 1024f):0.0} MB");
        builder.AppendLine($"Picos objetos / colliders / proyectiles / pickups: {report.peakSceneObjects} / {report.peakColliders} / {report.peakProjectiles} / {report.peakPickups}");
        builder.AppendLine();
        builder.AppendLine("NAVEGACION Y ESTABILIDAD");
        builder.AppendLine($"Atascos: {report.enemyStuckEpisodes} (max {report.longestEnemyStuckSeconds:0.00}s)");
        builder.AppendLine($"Destroyer no forzado: {report.unexpectedDestroyerActivations}");
        builder.AppendLine($"Spawns/actores solapados: {report.embeddedSpawnWarnings}");
        builder.AppendLine($"Recompensas dentro de peligros: {report.hazardOverlapWarnings}");
        builder.AppendLine($"Errores de runtime: {report.capturedRuntimeErrors}");
        builder.AppendLine();
        builder.AppendLine("CHEQUEOS");
        for (int i = 0; i < report.checks.Count; i++) builder.AppendLine(report.checks[i]);
        if (report.runtimeErrors.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("ERRORES CAPTURADOS");
            for (int i = 0; i < report.runtimeErrors.Count; i++) builder.AppendLine(report.runtimeErrors[i]);
        }
        return builder.ToString();
    }

    private void RestoreQaOverrides()
    {
        if (player != null) player.SetDebugQaInvulnerable(false);
        if (gameManager != null) gameManager.SetDebugQaMode(false);
        forcingDestroyer = false;
    }

    private void ResolveReferences()
    {
        if (arena == null) arena = GetComponent<ProceduralArenaGenerator>();
        if (player == null) player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        if (enemy == null) enemy = UnityEngine.Object.FindAnyObjectByType<EnemyController>();
        if (gameManager == null) gameManager = UnityEngine.Object.FindAnyObjectByType<GameManager>();
    }

    private void AddPass(string message)
    {
        report?.checks.Add("[OK] " + message);
    }

    private void AddWarning(string message)
    {
        if (report == null) return;
        report.warnings++;
        report.checks.Add("[AVISO] " + message);
    }

    private void AddFailure(string message)
    {
        if (report == null) return;
        report.failures++;
        report.checks.Add("[FALLO] " + message);
    }
}

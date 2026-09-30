using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Capa transversal de lectura. No modifica fisica ni balance: despeja visualmente
// actores ocultos, mantiene contraste local y ofrece herramientas de validacion.
[DisallowMultipleComponent]
[DefaultExecutionOrder(700)]
public sealed class GameplayReadabilityDirector : MonoBehaviour
{
    private sealed class ObstacleGroup
    {
        public Transform Root;
        public Collider2D Collider;
        public SpriteRenderer[] Renderers;
        public Color[] BaseColors;
        public bool Occluded;
        public float Visibility = 1f;
    }

    private ProceduralArenaGenerator arena;
    private Transform obstaclesRoot;
    private Transform dynamicRoot;
    private PlayerController player;
    private EnemyController enemy;
    private GameplayQaDirector qaDirector;
    private readonly List<ObstacleGroup> obstacles = new List<ObstacleGroup>();

    private float refreshTimer;
    private bool debugPanelVisible;
    private int selectedStateIndex;
    private string debugMessage = string.Empty;
    private float debugMessageUntil;

    public void Configure(ProceduralArenaGenerator arenaReference, Transform staticRoot, Transform movingRoot)
    {
        arena = arenaReference;
        obstaclesRoot = staticRoot;
        dynamicRoot = movingRoot;
        qaDirector = GetComponent<GameplayQaDirector>();
        ResolveActors();
        RebuildObstacleCache();
        RemoveLegacyFocusPlates();
    }

    private void Update()
    {
        HandleDebugInput();
    }

    private void LateUpdate()
    {
        ResolveActors();

        refreshTimer -= Time.deltaTime;
        if (refreshTimer <= 0f)
        {
            refreshTimer = 0.8f;
            RefreshDynamicObstacleCache();
        }

        UpdateObstacleOcclusion();
    }

    private void ResolveActors()
    {
        if (player == null)
        {
            player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        }
        if (enemy == null)
        {
            enemy = UnityEngine.Object.FindAnyObjectByType<EnemyController>();
        }
    }

    private void RemoveLegacyFocusPlates()
    {
        RemoveLegacyFocusPlate("PlayerLocalContrast");
        RemoveLegacyFocusPlate("AnomalyLocalContrast");
    }

    private void RemoveLegacyFocusPlate(string objectName)
    {
        Transform plate = transform.Find(objectName);
        if (plate == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(plate.gameObject);
        }
        else
        {
            DestroyImmediate(plate.gameObject);
        }
    }

    private void RebuildObstacleCache()
    {
        RestoreAllObstacleColors();
        obstacles.Clear();
        AddRootObstacles(obstaclesRoot);
        AddRootObstacles(dynamicRoot);
    }

    private void RefreshDynamicObstacleCache()
    {
        int expected = CountColliderChildren(obstaclesRoot) + CountColliderChildren(dynamicRoot);
        int live = 0;
        for (int i = 0; i < obstacles.Count; i++)
        {
            if (obstacles[i].Root != null && obstacles[i].Collider != null)
            {
                live++;
            }
        }

        if (live != expected)
        {
            RebuildObstacleCache();
        }
    }

    private static int CountColliderChildren(Transform root)
    {
        if (root == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < root.childCount; i++)
        {
            if (root.GetChild(i).GetComponent<Collider2D>() != null)
            {
                count++;
            }
        }
        return count;
    }

    private void AddRootObstacles(Transform root)
    {
        if (root == null)
        {
            return;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            Collider2D collider = child.GetComponent<Collider2D>();
            if (collider == null || collider.isTrigger)
            {
                continue;
            }

            SpriteRenderer[] renderers = child.GetComponentsInChildren<SpriteRenderer>(true);
            Color[] colors = new Color[renderers.Length];
            for (int r = 0; r < renderers.Length; r++)
            {
                colors[r] = renderers[r] != null ? renderers[r].color : Color.clear;
            }

            obstacles.Add(new ObstacleGroup
            {
                Root = child,
                Collider = collider,
                Renderers = renderers,
                BaseColors = colors
            });
        }
    }

    private void UpdateObstacleOcclusion()
    {
        Vector2 playerPosition = player != null ? (Vector2)player.transform.position : Vector2.positiveInfinity;
        Vector2 enemyPosition = enemy != null ? (Vector2)enemy.transform.position : Vector2.positiveInfinity;
        bool phaseDash = player != null && player.IsIntangibleDashActive;
        float step = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 15f);

        for (int i = obstacles.Count - 1; i >= 0; i--)
        {
            ObstacleGroup group = obstacles[i];
            if (group.Root == null || group.Collider == null)
            {
                obstacles.RemoveAt(i);
                continue;
            }

            Bounds bounds = group.Collider.bounds;
            bounds.Expand(0.20f);
            bool shouldFade = (phaseDash && bounds.Contains(playerPosition)) || bounds.Contains(enemyPosition);
            bool wasOccluded = group.Occluded;
            if (shouldFade && !wasOccluded)
            {
                CaptureObstacleColors(group);
            }

            group.Occluded = shouldFade;
            group.Visibility = Mathf.Lerp(group.Visibility, shouldFade ? 0.20f : 1f, step);
            if (!group.Occluded && group.Visibility > 0.995f)
            {
                if (wasOccluded)
                {
                    group.Visibility = 1f;
                    ApplyObstacleVisibility(group);
                }
                else
                {
                    // Mientras no hay oclusion, las animaciones de arena siguen
                    // siendo la fuente de verdad para el color del obstaculo.
                    CaptureObstacleColors(group);
                }
                continue;
            }
            ApplyObstacleVisibility(group);
        }
    }

    private static void CaptureObstacleColors(ObstacleGroup group)
    {
        for (int i = 0; i < group.Renderers.Length; i++)
        {
            if (group.Renderers[i] != null)
            {
                group.BaseColors[i] = group.Renderers[i].color;
            }
        }
    }

    private static void ApplyObstacleVisibility(ObstacleGroup group)
    {
        for (int i = 0; i < group.Renderers.Length; i++)
        {
            SpriteRenderer renderer = group.Renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Color color = group.BaseColors[i];
            color.a *= group.Visibility;
            renderer.color = color;
        }
    }

    private void RestoreAllObstacleColors()
    {
        for (int i = 0; i < obstacles.Count; i++)
        {
            ObstacleGroup group = obstacles[i];
            if (group.Renderers == null || group.BaseColors == null)
            {
                continue;
            }
            for (int r = 0; r < group.Renderers.Length; r++)
            {
                if (group.Renderers[r] != null)
                {
                    group.Renderers[r].color = group.BaseColors[r];
                }
            }
        }
    }

    private void HandleDebugInput()
    {
        if (!Application.isEditor && !Debug.isDebugBuild)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.f10Key.wasPressedThisFrame)
        {
            debugPanelVisible = !debugPanelVisible;
            if (debugPanelVisible && enemy != null)
            {
                Array states = Enum.GetValues(typeof(EnemyController.AnomalyState));
                selectedStateIndex = Mathf.Max(0, Array.IndexOf(states, enemy.CurrentState));
            }
        }
        if (keyboard.pageUpKey.wasPressedThisFrame)
        {
            CycleEnemyState(1);
        }
        if (keyboard.pageDownKey.wasPressedThisFrame)
        {
            CycleEnemyState(-1);
        }
        if (keyboard.f8Key.wasPressedThisFrame)
        {
            TriggerArenaEvent();
        }
        if (keyboard.f7Key.wasPressedThisFrame)
        {
            ResolveQaDirector();
            bool fullSweep = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            if (qaDirector != null)
            {
                if (fullSweep) qaDirector.StartFullSweep();
                else qaDirector.StartCurrentArenaCheck();
            }
        }
        if (keyboard.f6Key.wasPressedThisFrame)
        {
            ResolveQaDirector();
            qaDirector?.StopQa("Cancelado manualmente");
        }
    }

    private void CycleEnemyState(int direction)
    {
        ResolveActors();
        if (enemy == null)
        {
            SetDebugMessage("No se encontro la anomalia.");
            return;
        }

        Array values = Enum.GetValues(typeof(EnemyController.AnomalyState));
        selectedStateIndex = (selectedStateIndex + direction + values.Length) % values.Length;
        EnemyController.AnomalyState state = (EnemyController.AnomalyState)values.GetValue(selectedStateIndex);
        enemy.ForceStateForDebug(state);
        SetDebugMessage($"Estado forzado: {state}");
    }

    private void TriggerArenaEvent()
    {
        if (arena != null && arena.TriggerPrimaryThemedEventForDebug(out string label))
        {
            SetDebugMessage($"Evento forzado: {label}");
        }
        else
        {
            SetDebugMessage("El evento ya esta activo o aun no esta listo.");
        }
    }

    private void SetDebugMessage(string message)
    {
        debugMessage = message;
        debugMessageUntil = Time.unscaledTime + 3f;
    }

    private void ResolveQaDirector()
    {
        if (qaDirector == null)
        {
            qaDirector = GetComponent<GameplayQaDirector>();
        }
    }

    private void OnGUI()
    {
        if (!debugPanelVisible || (!Application.isEditor && !Debug.isDebugBuild))
        {
            return;
        }

        ResolveQaDirector();
        Rect panel = new Rect(18f, 70f, 330f, 310f);
        GUI.Box(panel, "PRUEBA DE LEGIBILIDAD");
        GUI.Label(new Rect(34f, 100f, 295f, 22f), $"Arena: {(arena != null ? arena.ActiveThemeLabel : "-")}");
        GUI.Label(new Rect(34f, 124f, 295f, 22f), $"Estado: {(enemy != null ? enemy.CurrentStateLabel : "-")}");
        if (GUI.Button(new Rect(34f, 152f, 90f, 30f), "< Estado")) CycleEnemyState(-1);
        if (GUI.Button(new Rect(132f, 152f, 90f, 30f), "Estado >")) CycleEnemyState(1);
        if (GUI.Button(new Rect(230f, 152f, 100f, 30f), "Evento F8")) TriggerArenaEvent();
        GUI.Label(new Rect(34f, 190f, 295f, 22f), "Page Up/Down: estados · F10: cerrar");
        if (Time.unscaledTime < debugMessageUntil)
        {
            GUI.Label(new Rect(34f, 218f, 295f, 22f), debugMessage);
        }

        GUI.Label(new Rect(34f, 244f, 295f, 22f), "QA AUTOMATIZADO");
        GUI.enabled = qaDirector != null && !qaDirector.IsRunning;
        if (GUI.Button(new Rect(34f, 270f, 92f, 30f), "Actual F7")) qaDirector.StartCurrentArenaCheck();
        if (GUI.Button(new Rect(132f, 270f, 96f, 30f), "Total Shift+F7")) qaDirector.StartFullSweep();
        GUI.enabled = qaDirector != null && qaDirector.IsRunning;
        if (GUI.Button(new Rect(234f, 270f, 96f, 30f), "Detener F6")) qaDirector.StopQa("Cancelado manualmente");
        GUI.enabled = true;
        string qaStatus = qaDirector != null ? qaDirector.StatusLine : "QA no disponible";
        GUI.Label(new Rect(34f, 306f, 295f, 40f), qaStatus);
        if (qaDirector != null && !string.IsNullOrWhiteSpace(qaDirector.LastReportPath))
        {
            GUI.Label(new Rect(34f, 350f, 295f, 22f), "Informe guardado en persistentDataPath/qa-reports");
        }
    }

    private void OnDisable()
    {
        RestoreAllObstacleColors();
    }
}

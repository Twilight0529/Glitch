using System.Collections.Generic;
using UnityEngine;

// Convierte el estado actual del jefe en una herramienta de un solo uso para el jugador.
// PlayerController se ocupa del input y del parry; este componente solo almacena y ejecuta la habilidad robada.
public class PlayerStateHijack : MonoBehaviour
{
    public enum HijackAbility
    {
        None,
        Overclock,
        PhaseStep,
        EchoDecoy,
        OrbitGuard,
        BreachCutter,
        ContainmentLock
    }

    [Header("Duraciones")]
    [SerializeField] private float overclockDuration = 5f;
    [SerializeField] private float overclockSpeedMultiplier = 1.48f;
    [SerializeField] private float phaseStepDistance = 4.2f;
    [SerializeField] private float echoDecoyDuration = 5.5f;
    [SerializeField] private float orbitGuardDuration = 4.5f;
    [SerializeField] private float orbitGuardRadius = 1.35f;
    [SerializeField] private float containmentLockDuration = 2.2f;
    [SerializeField] private float breachCutterRadius = 2.3f;
    [SerializeField] private int breachCutterMaxObstacles = 4;

    private readonly List<Transform> orbitNodes = new List<Transform>();
    private PlayerController player;
    private GameManager gameManager;
    private EnemyController enemy;
    private HijackAbility storedAbility;
    private EnemyController.AnomalyState capturedState;
    private float orbitGuardTimer;
    private float orbitScanTimer;
    private GameObject orbitRoot;

    public bool HasStoredAbility => storedAbility != HijackAbility.None;
    public bool IsOrbitGuardActive => orbitGuardTimer > 0f;
    public HijackAbility StoredAbility => storedAbility;
    public string StoredLabel => GetAbilityLabel(storedAbility);
    public string StoredHint => GetAbilityHint(storedAbility);
    public string CapturedStateLabel => capturedState.ToString();
    public Color StoredColor => GetAbilityColor(storedAbility);

    public void Configure(PlayerController owner)
    {
        player = owner;
        gameManager = FindAnyObjectByType<GameManager>();
        enemy = FindAnyObjectByType<EnemyController>();
    }

    private void Update()
    {
        if (player == null || player.IsInDeathSequence || (ResolveGameManager() != null && gameManager.IsBreachSensitiveSuppressionActive))
        {
            if (orbitGuardTimer > 0f)
            {
                DestroyOrbitGuard();
            }
            return;
        }

        if (orbitGuardTimer <= 0f)
        {
            return;
        }

        orbitGuardTimer -= Time.deltaTime;
        UpdateOrbitGuardVisual();
        orbitScanTimer -= Time.deltaTime;
        if (orbitScanTimer <= 0f)
        {
            orbitScanTimer = 0.07f;
            ReflectNearbyProjectiles();
        }

        if (orbitGuardTimer <= 0f)
        {
            DestroyOrbitGuard();
        }
    }

    public bool TryCapture(EnemyController.AnomalyState state)
    {
        GameManager manager = ResolveGameManager();
        if (HasStoredAbility || LocalVersusModeStorage.IsLocalVersus || manager == null || !manager.IsStateHijackUnlocked)
        {
            return false;
        }

        HijackAbility ability = ResolveAbility(state);
        if (ability == HijackAbility.None)
        {
            return false;
        }

        storedAbility = ability;
        capturedState = state;
        Color color = GetAbilityColor(ability);
        SpawnPulse(transform.position, color, 1.35f, 0.34f);
        GlitchAudioManager.PlayStateHijackCapture(transform.position);
        manager.NotifyStateHijackCaptured(GetAbilityLabel(ability), GetAbilityHint(ability), color);
        return true;
    }

    public bool TryActivate()
    {
        if (!HasStoredAbility || player == null || player.IsInDeathSequence || LocalVersusModeStorage.IsLocalVersus)
        {
            return false;
        }

        GameManager manager = ResolveGameManager();
        if (manager != null && manager.IsBreachSensitiveSuppressionActive)
        {
            return false;
        }

        EnemyController targetEnemy = ResolveEnemy();
        HijackAbility ability = storedAbility;
        Color color = GetAbilityColor(ability);
        storedAbility = HijackAbility.None;

        switch (ability)
        {
            case HijackAbility.Overclock:
                player.ApplySpeedBoost(Mathf.Max(1f, overclockSpeedMultiplier), Mathf.Max(0.2f, overclockDuration));
                break;
            case HijackAbility.PhaseStep:
                ExecutePhaseStep();
                break;
            case HijackAbility.EchoDecoy:
                ExecuteEchoDecoy(targetEnemy, color);
                break;
            case HijackAbility.OrbitGuard:
                BeginOrbitGuard(color);
                break;
            case HijackAbility.BreachCutter:
                ExecuteBreachCutter(targetEnemy);
                break;
            case HijackAbility.ContainmentLock:
                targetEnemy?.ApplyContainmentLock(player.GetPosition(), Mathf.Max(0.2f, containmentLockDuration));
                break;
        }

        SpawnPulse(transform.position, color, 2.1f, 0.42f);
        GlitchAudioManager.PlayStateHijackActivate(transform.position);
        manager?.NotifyStateHijackActivated(GetAbilityLabel(ability), color);
        return true;
    }

    private void ExecutePhaseStep()
    {
        Vector2 direction = player.CurrentMoveInput.sqrMagnitude > 0.01f
            ? player.CurrentMoveInput.normalized
            : player.LastMoveDirection.normalized;
        if (direction.sqrMagnitude < 0.01f)
        {
            direction = Vector2.right;
        }

        Vector2 origin = player.GetPosition();
        Vector2 destination = origin;
        for (int i = 6; i >= 1; i--)
        {
            float distance = Mathf.Max(0.5f, phaseStepDistance) * i / 6f;
            Vector2 candidate = player.ClampToPlayableArena(origin + direction * distance);
            if (IsPhaseDestinationFree(candidate))
            {
                destination = candidate;
                break;
            }
        }

        SpawnPulse(origin, GetAbilityColor(HijackAbility.PhaseStep), 0.85f, 0.22f);
        player.TeleportTo(destination, true);
        SpawnPulse(destination, GetAbilityColor(HijackAbility.PhaseStep), 1.15f, 0.28f);
    }

    private bool IsPhaseDestinationFree(Vector2 candidate)
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(candidate, 0.32f);
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit.isTrigger || hit.GetComponent<PlayerController>() != null || hit.GetComponent<EnemyController>() != null)
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private void ExecuteEchoDecoy(EnemyController targetEnemy, Color color)
    {
        Vector2 direction = player.LastMoveDirection.sqrMagnitude > 0.01f ? player.LastMoveDirection.normalized : Vector2.right;
        Vector2 decoyPosition = player.ClampToPlayableArena(player.GetPosition() - direction * 2.4f);
        targetEnemy?.ApplyHijackDecoy(decoyPosition, Mathf.Max(0.5f, echoDecoyDuration));

        GameObject decoy = new GameObject("HijackEchoDecoy");
        decoy.transform.position = decoyPosition;
        SpriteRenderer source = player.GetComponent<SpriteRenderer>();
        SpriteRenderer renderer = decoy.AddComponent<SpriteRenderer>();
        renderer.sprite = source != null ? source.sprite : SquareSpriteProvider.Get();
        renderer.color = new Color(color.r, color.g, color.b, 0.62f);
        renderer.sortingOrder = source != null ? source.sortingOrder + 1 : 12;
        decoy.AddComponent<StateHijackEchoFx>().Configure(renderer, Mathf.Max(0.5f, echoDecoyDuration), color);
    }

    private void BeginOrbitGuard(Color color)
    {
        DestroyOrbitGuard();
        orbitGuardTimer = Mathf.Max(0.5f, orbitGuardDuration);
        orbitScanTimer = 0f;
        orbitRoot = new GameObject("HijackOrbitGuard");
        orbitRoot.transform.SetParent(transform, false);

        for (int i = 0; i < 4; i++)
        {
            GameObject node = new GameObject($"OrbitNode_{i}");
            node.transform.SetParent(orbitRoot.transform, false);
            node.transform.localScale = Vector3.one * 0.18f;
            SpriteRenderer renderer = node.AddComponent<SpriteRenderer>();
            renderer.sprite = SquareSpriteProvider.Get();
            renderer.color = color;
            renderer.sortingOrder = 14;
            orbitNodes.Add(node.transform);
        }
    }

    private void UpdateOrbitGuardVisual()
    {
        float radius = Mathf.Max(0.4f, orbitGuardRadius);
        float baseAngle = Time.unscaledTime * 220f;
        for (int i = 0; i < orbitNodes.Count; i++)
        {
            Transform node = orbitNodes[i];
            if (node == null)
            {
                continue;
            }

            float angle = (baseAngle + i * 90f) * Mathf.Deg2Rad;
            node.localPosition = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            node.localRotation = Quaternion.Euler(0f, 0f, -baseAngle * 0.6f);
        }
    }

    private void ReflectNearbyProjectiles()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(player.GetPosition(), Mathf.Max(0.5f, orbitGuardRadius + 0.28f));
        for (int i = 0; i < hits.Length; i++)
        {
            AnomalyProjectile projectile = hits[i] != null ? hits[i].GetComponent<AnomalyProjectile>() : null;
            projectile?.TryReflectByParry(player.GetPosition());
        }
    }

    private void ExecuteBreachCutter(EnemyController targetEnemy)
    {
        Vector2 direction = player.LastMoveDirection.sqrMagnitude > 0.01f ? player.LastMoveDirection.normalized : Vector2.right;
        Vector2 center = player.GetPosition() + direction * 1.25f;
        int broken = targetEnemy != null
            ? targetEnemy.BreakObstaclesForHijack(center, Mathf.Max(0.5f, breachCutterRadius), Mathf.Max(1, breachCutterMaxObstacles))
            : 0;
        if (broken <= 0)
        {
            targetEnemy?.ApplyContainmentLock(player.GetPosition(), 0.75f);
        }
    }

    private void DestroyOrbitGuard()
    {
        orbitGuardTimer = 0f;
        orbitNodes.Clear();
        if (orbitRoot != null)
        {
            Destroy(orbitRoot);
            orbitRoot = null;
        }
    }

    private GameManager ResolveGameManager()
    {
        if (gameManager == null)
        {
            gameManager = FindAnyObjectByType<GameManager>();
        }

        return gameManager;
    }

    private EnemyController ResolveEnemy()
    {
        if (enemy == null)
        {
            enemy = FindAnyObjectByType<EnemyController>();
        }

        return enemy;
    }

    private static HijackAbility ResolveAbility(EnemyController.AnomalyState state)
    {
        switch (state)
        {
            case EnemyController.AnomalyState.Destroyer:
            case EnemyController.AnomalyState.MapRecompile:
                return HijackAbility.BreachCutter;
            case EnemyController.AnomalyState.PhaseBlink:
            case EnemyController.AnomalyState.InputDesync:
            case EnemyController.AnomalyState.SignalJam:
                return HijackAbility.PhaseStep;
            case EnemyController.AnomalyState.Split:
            case EnemyController.AnomalyState.ReplayPredator:
            case EnemyController.AnomalyState.SignalPossession:
                return HijackAbility.EchoDecoy;
            case EnemyController.AnomalyState.ExpansionShoot:
            case EnemyController.AnomalyState.PincerBarrage:
            case EnemyController.AnomalyState.OrbitBarrage:
                return HijackAbility.OrbitGuard;
            case EnemyController.AnomalyState.ChecksumLattice:
            case EnemyController.AnomalyState.PhaseContract:
            case EnemyController.AnomalyState.AdaptiveCountermeasure:
            case EnemyController.AnomalyState.SignalTether:
            case EnemyController.AnomalyState.BlindspotProtocol:
                return HijackAbility.ContainmentLock;
            default:
                return HijackAbility.Overclock;
        }
    }

    public static string GetAbilityLabel(HijackAbility ability)
    {
        switch (ability)
        {
            case HijackAbility.Overclock: return "OVERCLOCK";
            case HijackAbility.PhaseStep: return "PHASE STEP";
            case HijackAbility.EchoDecoy: return "ECHO DECOY";
            case HijackAbility.OrbitGuard: return "ORBIT GUARD";
            case HijackAbility.BreachCutter: return "BREACH CUTTER";
            case HijackAbility.ContainmentLock: return "CONTAINMENT LOCK";
            default: return "SIN CAPTURA";
        }
    }

    public static Color GetAbilityColor(HijackAbility ability)
    {
        switch (ability)
        {
            case HijackAbility.Overclock: return new Color(1f, 0.78f, 0.34f, 1f);
            case HijackAbility.PhaseStep: return new Color(0.62f, 0.70f, 1f, 1f);
            case HijackAbility.EchoDecoy: return new Color(0.96f, 0.46f, 1f, 1f);
            case HijackAbility.OrbitGuard: return new Color(0.42f, 0.96f, 1f, 1f);
            case HijackAbility.BreachCutter: return new Color(1f, 0.42f, 0.64f, 1f);
            case HijackAbility.ContainmentLock: return new Color(0.52f, 1f, 0.70f, 1f);
            default: return new Color(0.42f, 0.56f, 0.72f, 1f);
        }
    }

    public static string GetAbilityHint(HijackAbility ability)
    {
        switch (ability)
        {
            case HijackAbility.Overclock: return "AUMENTA TU VELOCIDAD";
            case HijackAbility.PhaseStep: return "TELETRANSPORTE DIRECCIONAL";
            case HijackAbility.EchoDecoy: return "DISTRAE A LA ANOMALIA";
            case HijackAbility.OrbitGuard: return "REFLEJA PROYECTILES CERCANOS";
            case HijackAbility.BreachCutter: return "ROMPE OBSTACULOS AL FRENTE";
            case HijackAbility.ContainmentLock: return "INMOVILIZA A LA ANOMALIA";
            default: return "PARRY AL JEFE PARA CAPTURAR";
        }
    }

    private static void SpawnPulse(Vector3 position, Color color, float radius, float duration)
    {
        GameObject pulse = new GameObject("StateHijackPulse");
        pulse.transform.position = position;
        SpriteRenderer renderer = pulse.AddComponent<SpriteRenderer>();
        renderer.sprite = CircleSpriteProvider.Get();
        renderer.color = color;
        renderer.sortingOrder = 15;
        pulse.transform.localScale = Vector3.one * 0.24f;
        pulse.AddComponent<PlayerParryBurstFx>().Configure(renderer, radius, duration, color);
        Destroy(pulse, duration + 0.08f);
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerController : MonoBehaviour
{
    // Controla movimiento, mejoras defensivas/ofensivas y la secuencia visual de muerte.
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 11f;
    [SerializeField] private float maxBoostMultiplier = 1.9f;
    [SerializeField, Range(0.2f, 1f)] private float minSlowMultiplier = 0.3f;

    [Header("Shield Visual")]
    [SerializeField] private Color shieldColor = new Color(0.95f, 0.64f, 0.88f, 0.85f);
    [SerializeField] private float shieldPulseSpeed = 7.2f;
    [SerializeField] private float shieldRadius = 0.72f;
    [SerializeField] private float maxShieldDurationMultiplier = 1.8f;

    [Header("Firewall Parry")]
    [SerializeField] private float parryActiveDuration = 0.16f;
    [SerializeField] private float parryCooldown = 0.85f;
    [SerializeField] private float parryRadius = 0.95f;
    [SerializeField] private float maxParryActiveDuration = 0.32f;
    [SerializeField] private float minParryCooldown = 0.42f;
    [SerializeField] private float maxParryRadius = 1.55f;
    [SerializeField] private Color parryReadyColor = new Color(0.46f, 0.96f, 1f, 0.62f);
    [SerializeField] private Color parrySuccessColor = new Color(1f, 0.96f, 0.64f, 1f);
    [SerializeField] private float parryFxRadius = 1.65f;
    [SerializeField] private float parryFxDuration = 0.2f;
    [SerializeField] private int parryFxRayCount = 12;

    [Header("Firewall Burst")]
    [SerializeField] private float firewallChargeMax = 100f;
    [SerializeField] private float firewallChargeFromScorePickup = 2.2f;
    [SerializeField] private float firewallChargeFromPowerup = 14f;
    [SerializeField] private float firewallChargeFromObjectiveNode = 22f;
    [SerializeField] private float firewallChargeFromParry = 18f;
    [SerializeField] private float firewallBurstRadius = 4.4f;
    [SerializeField] private float maxFirewallBurstRadius = 6.4f;
    [SerializeField] private float firewallBurstStunDuration = 0.95f;
    [SerializeField] private float maxFirewallBurstStunDuration = 1.55f;
    [SerializeField] private float firewallBurstKnockbackMultiplier = 1.35f;
    [SerializeField] private float maxFirewallChargeGainMultiplier = 1.9f;
    [SerializeField] private Color firewallBurstColor = new Color(0.46f, 0.96f, 1f, 1f);
    [SerializeField] private Color firewallReadyColor = new Color(1f, 0.90f, 0.54f, 1f);
    [SerializeField] private int firewallBurstRayCount = 18;

    [Header("Movement Trail")]
    [SerializeField] private bool enableMovementTrail = true;
    [SerializeField] private float maxPermanentMoveSpeed = 15.5f;
    [SerializeField] private Color trailColor = new Color(0.42f, 0.92f, 1f, 0.85f);
    [SerializeField] private float trailParticleLifetime = 0.36f;
    [SerializeField] private float trailStartSize = 0.17f;
    [SerializeField] private float trailEmissionAtMaxSpeed = 42f;
    [SerializeField] private float trailMinVelocityToEmit = 1.1f;

    [Header("Death Explosion")]
    [SerializeField] private bool enableDeathExplosion = true;
    [SerializeField] private float deathChargeDuration = 0.26f;
    [SerializeField] private float deathFlashDuration = 0.08f;
    [SerializeField] private float deathAfterglowDuration = 0.34f;
    [SerializeField] private float deathScaleBoost = 1.7f;
    [SerializeField] private Color deathChargeColor = new Color(1f, 0.58f, 0.70f, 1f);
    [SerializeField] private Color deathFlashColor = new Color(1f, 0.96f, 1f, 1f);
    [SerializeField] private Color deathShardColor = new Color(1f, 0.62f, 0.78f, 1f);
    [SerializeField] private int deathShardCount = 12;
    [SerializeField] private float deathShardDistance = 2.1f;
    [SerializeField] private float deathShockwaveRadius = 2.9f;
    [SerializeField] private float shieldBreakBurstRadius = 1.25f;
    [SerializeField] private float shieldBreakBurstDuration = 0.22f;
    [SerializeField] private int shieldBreakBurstRayCount = 8;

    [Header("Breach Consumption")]
    [SerializeField] private int breachGlitchStripCount = 7;
    [SerializeField] private float breachGlitchRadius = 0.82f;
    [SerializeField] private float breachGlitchPulseSpeed = 18f;

    private Rigidbody2D rb;
    private Collider2D bodyCollider;
    private SpriteRenderer bodyRenderer;
    private Vector2 moveInput;
    private float speedBoostTimer;
    private float speedBoostMultiplier = 1f;
    private float movementSlowTimer;
    private float movementSlowMultiplier = 1f;
    private float shieldTimer;
    private float shieldDurationMultiplier = 1f;
    private SpriteRenderer shieldRenderer;
    private GameObject shieldVisual;
    private float parryTimer;
    private float parryCooldownTimer;
    private SpriteRenderer parryRenderer;
    private GameObject parryVisual;
    private ParticleSystem movementTrail;
    private float firewallCharge;
    private float firewallChargeGainMultiplier = 1f;
    private bool deathSequenceActive;
    private bool breachConsumptionActive;
    private GameObject breachGlitchVisual;
    private SpriteRenderer[] breachGlitchRenderers;
    private Color breachGlitchColor = new Color(1f, 0.42f, 0.78f, 1f);
    private Color baseBodyColor = Color.white;
    private Vector3 baseBodyScale = Vector3.one;

    public Vector2 CurrentVelocity => rb != null ? rb.linearVelocity : Vector2.zero;
    public bool IsInDeathSequence => deathSequenceActive;
    public float DeathExplosionDuration => Mathf.Max(0.02f, deathChargeDuration) + Mathf.Max(0.02f, deathFlashDuration) + Mathf.Max(0.02f, deathAfterglowDuration);
    public bool HasShield => shieldTimer > 0f;
    public bool HasSpeedBoost => speedBoostTimer > 0f;
    public bool HasMovementSlow => movementSlowTimer > 0f;
    public bool IsParryActive => parryTimer > 0f;
    public float FirewallCharge => firewallCharge;
    public float FirewallChargeMax => Mathf.Max(1f, firewallChargeMax);
    public float FirewallChargeNormalized => Mathf.Clamp01(firewallCharge / FirewallChargeMax);
    public bool IsFirewallBurstReady => firewallCharge >= FirewallChargeMax;
    public float FirewallBurstRadius => firewallBurstRadius;
    public string ActivePowerupLabel
    {
        get
        {
            if (HasMovementSlow && HasShield && HasSpeedBoost)
            {
                return "Shield + Speed + Suppressed";
            }

            if (HasMovementSlow && HasShield)
            {
                return "Shield + Suppressed";
            }

            if (HasMovementSlow && HasSpeedBoost)
            {
                return "Speed + Suppressed";
            }

            if (HasMovementSlow)
            {
                return "Suppressed";
            }

            if (HasShield && HasSpeedBoost)
            {
                return "Shield + Speed";
            }

            if (HasShield)
            {
                return "Shield";
            }

            if (HasSpeedBoost)
            {
                return "Speed";
            }

            return "None";
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        bodyRenderer = GetComponent<SpriteRenderer>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        baseBodyScale = transform.localScale;
        if (bodyRenderer != null)
        {
            baseBodyColor = bodyRenderer.color;
        }

        EnsureShieldVisual();
        EnsureParryVisual();
        EnsureMovementTrail();
    }

    private void Update()
    {
        if (deathSequenceActive)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (breachConsumptionActive)
        {
            rb.linearVelocity = Vector2.zero;
            UpdateBreachConsumptionVisual();
            return;
        }

        UpdatePowerupTimers();
        UpdateShieldVisual();
        UpdateParryTimers();
        UpdateParryVisual();

        if (WasParryPressed() && parryCooldownTimer <= 0f)
        {
            StartParry();
        }

        ScanParryWindow();
        if (WasFirewallBurstPressed())
        {
            TryActivateFirewallBurst();
        }

        moveInput = ReadMoveInput().normalized;
        float effectiveSpeed = moveSpeed;
        if (speedBoostTimer > 0f)
        {
            effectiveSpeed *= speedBoostMultiplier;
        }
        if (movementSlowTimer > 0f)
        {
            effectiveSpeed *= movementSlowMultiplier;
        }

        rb.linearVelocity = moveInput * effectiveSpeed;
        UpdateMovementTrail(Mathf.Max(0.01f, effectiveSpeed));
    }

    private static bool WasFirewallBurstPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (keyboard != null && (keyboard.qKey.wasPressedThisFrame || keyboard.rKey.wasPressedThisFrame))
        {
            return true;
        }

        if (mouse != null && mouse.rightButton.wasPressedThisFrame)
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.R) || Input.GetMouseButtonDown(1);
#else
        return false;
#endif
    }

    private static bool WasParryPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.eKey.wasPressedThisFrame))
        {
            return true;
        }

        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0);
#else
        return false;
#endif
    }

    private static Vector2 ReadMoveInput()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            float horizontal = 0f;
            float vertical = 0f;

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) horizontal -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) horizontal += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) vertical -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) vertical += 1f;

            return new Vector2(horizontal, vertical);
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#else
        return Vector2.zero;
#endif
    }

    public Vector2 GetPosition()
    {
        return rb.position;
    }

    public void ApplySpeedBoost(float multiplier, float duration)
    {
        speedBoostMultiplier = Mathf.Clamp(multiplier, 1f, Mathf.Max(1f, maxBoostMultiplier));
        speedBoostTimer = Mathf.Max(speedBoostTimer, Mathf.Max(0.1f, duration));
    }

    public void ApplyShield(float duration)
    {
        shieldTimer = Mathf.Max(shieldTimer, Mathf.Max(0.1f, duration * shieldDurationMultiplier));
    }

    public void ApplyMovementSlow(float multiplier, float duration)
    {
        movementSlowMultiplier = Mathf.Min(
            movementSlowTimer > 0f ? movementSlowMultiplier : 1f,
            Mathf.Clamp(multiplier, minSlowMultiplier, 1f));
        movementSlowTimer = Mathf.Max(movementSlowTimer, Mathf.Max(0.05f, duration));
    }

    public bool TryAbsorbHit()
    {
        if (deathSequenceActive)
        {
            return false;
        }

        if (shieldTimer <= 0f)
        {
            return false;
        }

        shieldTimer = 0f;
        SpawnShieldBreakFx();
        GlitchAudioManager.PlayShieldBreak(transform.position);
        return true;
    }

    public void AddPermanentMoveSpeed(float amount)
    {
        moveSpeed = Mathf.Min(Mathf.Max(0.1f, maxPermanentMoveSpeed), moveSpeed + Mathf.Max(0f, amount));
    }

    public void ExtendParryWindow(float extraSeconds)
    {
        parryActiveDuration = Mathf.Min(Mathf.Max(0.04f, maxParryActiveDuration), parryActiveDuration + Mathf.Max(0f, extraSeconds));
    }

    public void ReduceParryCooldown(float multiplier)
    {
        parryCooldown = Mathf.Max(Mathf.Max(0.05f, minParryCooldown), parryCooldown * Mathf.Clamp(multiplier, 0.2f, 1f));
        parryCooldownTimer = Mathf.Min(parryCooldownTimer, parryCooldown);
    }

    public void ExpandParryRadius(float extraRadius)
    {
        parryRadius = Mathf.Min(Mathf.Max(0.2f, maxParryRadius), parryRadius + Mathf.Max(0f, extraRadius));
        parryFxRadius = Mathf.Max(parryFxRadius, parryRadius + 0.55f);

        if (parryRenderer != null)
        {
            parryRenderer.size = Vector2.one * Mathf.Max(0.25f, parryRadius * 2f);
        }
    }

    public void ImproveShieldDuration(float multiplier)
    {
        shieldDurationMultiplier = Mathf.Min(
            Mathf.Max(1f, maxShieldDurationMultiplier),
            shieldDurationMultiplier * Mathf.Max(1f, multiplier));
    }

    public void AddFirewallChargeFromScore(int scorePoints)
    {
        AddFirewallCharge(firewallChargeFromScorePickup * Mathf.Max(1, scorePoints));
    }

    public void AddFirewallChargeFromPowerup()
    {
        AddFirewallCharge(firewallChargeFromPowerup);
    }

    public void AddFirewallChargeFromObjectiveNode()
    {
        AddFirewallCharge(firewallChargeFromObjectiveNode);
    }

    public void ImproveFirewallChargeGain(float multiplier)
    {
        firewallChargeGainMultiplier = Mathf.Min(
            Mathf.Max(1f, maxFirewallChargeGainMultiplier),
            firewallChargeGainMultiplier * Mathf.Max(1f, multiplier));
    }

    public void ExpandFirewallBurstRadius(float extraRadius)
    {
        firewallBurstRadius = Mathf.Min(
            Mathf.Max(0.5f, maxFirewallBurstRadius),
            firewallBurstRadius + Mathf.Max(0f, extraRadius));
    }

    public void ImproveFirewallBurstStun(float extraSeconds)
    {
        firewallBurstStunDuration = Mathf.Min(
            Mathf.Max(0.1f, maxFirewallBurstStunDuration),
            firewallBurstStunDuration + Mathf.Max(0f, extraSeconds));
    }

    public void AddFirewallCharge(float amount)
    {
        if (deathSequenceActive || breachConsumptionActive || amount <= 0f)
        {
            return;
        }

        bool wasReady = IsFirewallBurstReady;
        firewallCharge = Mathf.Min(FirewallChargeMax, firewallCharge + amount * Mathf.Max(0.1f, firewallChargeGainMultiplier));
        if (!wasReady && IsFirewallBurstReady)
        {
            GlitchAudioManager.PlayFirewallReady(transform.position);
            SpawnFirewallReadyFx();
        }
    }

    public bool TryActivateFirewallBurst()
    {
        if (!IsFirewallBurstReady || deathSequenceActive || breachConsumptionActive)
        {
            return false;
        }

        firewallCharge = 0f;
        Vector2 origin = GetPosition();
        float radius = Mathf.Max(0.5f, firewallBurstRadius);
        Collider2D[] hits = Physics2D.OverlapCircleAll(origin, radius);
        HashSet<EnemyController> affectedEnemies = new HashSet<EnemyController>();
        HashSet<SplitAnomalyCloneController> affectedClones = new HashSet<SplitAnomalyCloneController>();

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null)
            {
                continue;
            }

            AnomalyProjectile projectile = hit.GetComponent<AnomalyProjectile>();
            if (projectile != null)
            {
                projectile.TryReflectByParry(origin);
                continue;
            }

            EnemyController enemy = hit.GetComponent<EnemyController>();
            if (enemy != null && affectedEnemies.Add(enemy))
            {
                enemy.ApplyFirewallBurst(origin, radius, firewallBurstStunDuration, firewallBurstKnockbackMultiplier);
                continue;
            }

            SplitAnomalyCloneController clone = hit.GetComponent<SplitAnomalyCloneController>();
            if (clone != null && affectedClones.Add(clone))
            {
                clone.ApplyFirewallBurst(origin, radius, firewallBurstStunDuration, firewallBurstKnockbackMultiplier);
            }
        }

        SpawnFirewallBurstFx(radius);
        GlitchAudioManager.PlayFirewallBurst(transform.position);
        return true;
    }

    public bool TryParryHit(Vector2 threatPosition, out Vector2 parryDirection)
    {
        parryDirection = ((Vector2)transform.position - threatPosition).sqrMagnitude > 0.0001f
            ? ((Vector2)transform.position - threatPosition).normalized
            : Vector2.right;

        if (deathSequenceActive || parryTimer <= 0f)
        {
            return false;
        }

        parryDirection = -parryDirection;
        parryTimer = 0f;
        parryCooldownTimer = Mathf.Max(parryCooldownTimer, Mathf.Max(0.05f, parryCooldown * 0.55f));
        AddFirewallCharge(firewallChargeFromParry);
        SpawnParrySuccessFx(parryDirection);
        GlitchAudioManager.PlayParrySuccess(transform.position);
        return true;
    }

    public bool StartDeathExplosion()
    {
        if (deathSequenceActive)
        {
            return false;
        }

        deathSequenceActive = true;
        breachConsumptionActive = false;
        DestroyBreachConsumptionVisual();
        GlitchAudioManager.PlayPlayerDeath(transform.position);
        StartCoroutine(DeathExplosionRoutine());
        return true;
    }

    public bool BeginBreachConsumption(Color glitchColor)
    {
        if (deathSequenceActive)
        {
            return false;
        }

        breachConsumptionActive = true;
        breachGlitchColor = glitchColor;
        speedBoostTimer = 0f;
        speedBoostMultiplier = 1f;
        movementSlowTimer = 0f;
        movementSlowMultiplier = 1f;
        shieldTimer = 0f;
        parryTimer = 0f;
        parryCooldownTimer = 0f;
        moveInput = Vector2.zero;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        if (bodyCollider != null)
        {
            bodyCollider.enabled = false;
        }

        if (shieldVisual != null)
        {
            shieldVisual.SetActive(false);
        }

        if (parryVisual != null)
        {
            parryVisual.SetActive(false);
        }

        if (movementTrail != null)
        {
            ParticleSystem.EmissionModule emission = movementTrail.emission;
            emission.rateOverTime = 0f;
        }

        EnsureBreachConsumptionVisual();
        UpdateBreachConsumptionVisual();
        return true;
    }

    private void UpdatePowerupTimers()
    {
        if (speedBoostTimer > 0f)
        {
            speedBoostTimer -= Time.deltaTime;
            if (speedBoostTimer <= 0f)
            {
                speedBoostTimer = 0f;
                speedBoostMultiplier = 1f;
            }
        }

        if (shieldTimer > 0f)
        {
            shieldTimer -= Time.deltaTime;
            if (shieldTimer < 0f)
            {
                shieldTimer = 0f;
            }
        }

        if (movementSlowTimer > 0f)
        {
            movementSlowTimer -= Time.deltaTime;
            if (movementSlowTimer <= 0f)
            {
                movementSlowTimer = 0f;
                movementSlowMultiplier = 1f;
            }
        }
    }

    private void UpdateParryTimers()
    {
        if (parryTimer > 0f)
        {
            parryTimer -= Time.deltaTime;
            if (parryTimer < 0f)
            {
                parryTimer = 0f;
            }
        }

        if (parryCooldownTimer > 0f)
        {
            parryCooldownTimer -= Time.deltaTime;
            if (parryCooldownTimer < 0f)
            {
                parryCooldownTimer = 0f;
            }
        }
    }

    private void StartParry()
    {
        parryTimer = Mathf.Max(0.04f, parryActiveDuration);
        parryCooldownTimer = Mathf.Max(0.05f, parryCooldown);
        SpawnParryStartFx();
        GlitchAudioManager.PlayParryStart(transform.position);
    }

    private void ScanParryWindow()
    {
        if (parryTimer <= 0f)
        {
            return;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, Mathf.Max(0.1f, parryRadius));
        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hit = hits[i];
            if (hit == null || hit == bodyCollider)
            {
                continue;
            }

            AnomalyProjectile projectile = hit.GetComponent<AnomalyProjectile>();
            if (projectile != null && TryParryHit(projectile.transform.position, out _))
            {
                projectile.TryReflectByParry(transform.position);
                return;
            }

            EnemyController enemy = hit.GetComponent<EnemyController>();
            if (enemy != null && TryParryHit(enemy.transform.position, out Vector2 parryDirection))
            {
                enemy.ApplyParryImpact(GetPosition(), parryDirection);
                return;
            }
        }
    }

    private void EnsureShieldVisual()
    {
        if (shieldVisual != null)
        {
            return;
        }

        shieldVisual = new GameObject("ShieldVisual");
        shieldVisual.transform.SetParent(transform, false);
        shieldVisual.transform.localPosition = Vector3.zero;
        shieldVisual.transform.localScale = Vector3.one;

        shieldRenderer = shieldVisual.AddComponent<SpriteRenderer>();
        shieldRenderer.sprite = CircleSpriteProvider.Get();
        shieldRenderer.drawMode = SpriteDrawMode.Sliced;
        shieldRenderer.size = Vector2.one * Mathf.Max(0.25f, shieldRadius * 2f);
        shieldRenderer.sortingOrder = 9;
        shieldRenderer.color = new Color(shieldColor.r, shieldColor.g, shieldColor.b, 0f);
        shieldVisual.SetActive(false);
    }

    private void EnsureParryVisual()
    {
        if (parryVisual != null)
        {
            return;
        }

        parryVisual = new GameObject("ParryWindowVisual");
        parryVisual.transform.SetParent(transform, false);
        parryVisual.transform.localPosition = Vector3.zero;
        parryVisual.transform.localScale = Vector3.one;

        parryRenderer = parryVisual.AddComponent<SpriteRenderer>();
        parryRenderer.sprite = CircleSpriteProvider.Get();
        parryRenderer.drawMode = SpriteDrawMode.Sliced;
        parryRenderer.size = Vector2.one * Mathf.Max(0.25f, parryRadius * 2f);
        parryRenderer.sortingOrder = 10;
        parryRenderer.color = new Color(parryReadyColor.r, parryReadyColor.g, parryReadyColor.b, 0f);
        parryVisual.SetActive(false);
    }

    private void UpdateShieldVisual()
    {
        if (shieldVisual == null || shieldRenderer == null)
        {
            return;
        }

        if (shieldTimer <= 0f)
        {
            if (shieldVisual.activeSelf)
            {
                shieldVisual.SetActive(false);
            }

            return;
        }

        if (!shieldVisual.activeSelf)
        {
            shieldVisual.SetActive(true);
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Max(0.1f, shieldPulseSpeed));
        Color c = shieldColor;
        c.a = Mathf.Lerp(0.24f, 0.64f, pulse);
        shieldRenderer.color = c;
        shieldVisual.transform.localScale = Vector3.one * Mathf.Lerp(0.96f, 1.05f, pulse);
    }

    private void UpdateParryVisual()
    {
        if (parryVisual == null || parryRenderer == null)
        {
            return;
        }

        if (parryTimer <= 0f)
        {
            if (parryVisual.activeSelf)
            {
                parryVisual.SetActive(false);
            }

            return;
        }

        if (!parryVisual.activeSelf)
        {
            parryVisual.SetActive(true);
        }

        float t = Mathf.Clamp01(parryTimer / Mathf.Max(0.01f, parryActiveDuration));
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 34f);
        parryRenderer.color = new Color(parryReadyColor.r, parryReadyColor.g, parryReadyColor.b, Mathf.Lerp(0.08f, 0.58f, t) * (0.8f + pulse * 0.2f));
        parryVisual.transform.localScale = Vector3.one * Mathf.Lerp(1.2f, 0.88f, t);
    }

    private void EnsureMovementTrail()
    {
        if (movementTrail != null || !enableMovementTrail)
        {
            return;
        }

        GameObject trailGo = new GameObject("MovementTrail");
        trailGo.transform.SetParent(transform, false);
        trailGo.transform.localPosition = Vector3.zero;
        trailGo.transform.localScale = Vector3.one;

        movementTrail = trailGo.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = movementTrail.main;
        main.duration = 1f;
        main.loop = true;
        main.startLifetime = Mathf.Max(0.08f, trailParticleLifetime);
        main.startSize = Mathf.Max(0.03f, trailStartSize);
        main.startSpeed = 0.02f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = true;
        main.maxParticles = 120;
        main.scalingMode = ParticleSystemScalingMode.Local;
        main.startColor = trailColor;

        ParticleSystem.EmissionModule emission = movementTrail.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = movementTrail.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.12f;
        shape.radiusThickness = 1f;

        ParticleSystem.ColorOverLifetimeModule colorLifetime = movementTrail.colorOverLifetime;
        colorLifetime.enabled = true;
        Gradient colorGradient = new Gradient();
        colorGradient.SetKeys(
            new[]
            {
                new GradientColorKey(trailColor, 0f),
                new GradientColorKey(new Color(trailColor.r * 0.85f, trailColor.g * 0.95f, trailColor.b, 1f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.65f, 0f),
                new GradientAlphaKey(0.25f, 0.6f),
                new GradientAlphaKey(0f, 1f)
            });
        colorLifetime.color = new ParticleSystem.MinMaxGradient(colorGradient);

        ParticleSystem.SizeOverLifetimeModule sizeLifetime = movementTrail.sizeOverLifetime;
        sizeLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.55f, 0.7f),
            new Keyframe(1f, 0.2f));
        sizeLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ParticleSystemRenderer trailRenderer = movementTrail.GetComponent<ParticleSystemRenderer>();
        trailRenderer.material = new Material(Shader.Find("Sprites/Default"));
        trailRenderer.sortingOrder = 8;
        trailRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        trailRenderer.alignment = ParticleSystemRenderSpace.View;

        movementTrail.Play();
    }

    private void UpdateMovementTrail(float effectiveSpeed)
    {
        if (!enableMovementTrail || movementTrail == null)
        {
            return;
        }

        float velocityMagnitude = rb != null ? rb.linearVelocity.magnitude : 0f;
        if (velocityMagnitude <= trailMinVelocityToEmit)
        {
            ParticleSystem.EmissionModule idleEmission = movementTrail.emission;
            idleEmission.rateOverTime = 0f;
            return;
        }

        float normalized = Mathf.Clamp01(velocityMagnitude / Mathf.Max(0.01f, effectiveSpeed));
        float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 18f);
        float rate = Mathf.Lerp(8f, Mathf.Max(10f, trailEmissionAtMaxSpeed), normalized) * pulse;

        ParticleSystem.EmissionModule emission = movementTrail.emission;
        emission.rateOverTime = rate;
    }

    private void EnsureBreachConsumptionVisual()
    {
        if (breachGlitchVisual != null)
        {
            breachGlitchVisual.SetActive(true);
            return;
        }

        breachGlitchVisual = new GameObject("BreachConsumptionGlitch");
        breachGlitchVisual.transform.SetParent(transform, false);
        breachGlitchVisual.transform.localPosition = Vector3.zero;
        breachGlitchVisual.transform.localScale = Vector3.one;

        int count = Mathf.Max(3, breachGlitchStripCount);
        breachGlitchRenderers = new SpriteRenderer[count];
        for (int i = 0; i < count; i++)
        {
            GameObject strip = new GameObject($"BreachGlitchStrip_{i}");
            strip.transform.SetParent(breachGlitchVisual.transform, false);
            SpriteRenderer sr = strip.AddComponent<SpriteRenderer>();
            sr.sprite = SquareSpriteProvider.Get();
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.sortingOrder = 21;
            sr.color = Color.clear;
            breachGlitchRenderers[i] = sr;
        }
    }

    private void UpdateBreachConsumptionVisual()
    {
        if (bodyRenderer != null)
        {
            float bodyPulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * Mathf.Max(0.1f, breachGlitchPulseSpeed));
            bodyRenderer.color = Color.Lerp(baseBodyColor, breachGlitchColor, 0.46f + bodyPulse * 0.32f);
            transform.localScale = baseBodyScale * Mathf.Lerp(0.92f, 1.08f, bodyPulse);
        }

        if (breachGlitchVisual == null || breachGlitchRenderers == null)
        {
            return;
        }

        float radius = Mathf.Max(0.1f, breachGlitchRadius);
        for (int i = 0; i < breachGlitchRenderers.Length; i++)
        {
            SpriteRenderer sr = breachGlitchRenderers[i];
            if (sr == null)
            {
                continue;
            }

            float seed = i * 2.17f + 0.31f;
            float snap = Mathf.Floor(Mathf.PerlinNoise(seed, Time.unscaledTime * 11f) * 7f) / 7f;
            float lateral = Mathf.Lerp(-radius, radius, Mathf.PerlinNoise(seed + 4.4f, Time.unscaledTime * 5.3f));
            float vertical = Mathf.Lerp(-radius, radius, Mathf.PerlinNoise(seed + 8.8f, Time.unscaledTime * 4.7f));
            sr.transform.localPosition = new Vector3(lateral + (snap - 0.5f) * 0.24f, vertical, -0.01f);
            sr.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-12f, 12f, Mathf.PerlinNoise(seed + 1.9f, Time.unscaledTime * 6f)));

            bool horizontal = i % 2 == 0;
            float length = Mathf.Lerp(0.22f, 0.82f, Mathf.PerlinNoise(seed + 12.1f, Time.unscaledTime * 7f));
            float thickness = Mathf.Lerp(0.035f, 0.13f, Mathf.PerlinNoise(seed + 16.5f, Time.unscaledTime * 9f));
            sr.size = horizontal ? new Vector2(length, thickness) : new Vector2(thickness, length);

            float blink = Mathf.PerlinNoise(seed + 21f, Time.unscaledTime * 14f);
            Color c = Color.Lerp(new Color(0.1f, 0.95f, 1f, 0.78f), breachGlitchColor, blink);
            c.a = Mathf.Lerp(0.22f, 0.88f, blink);
            sr.color = c;
        }
    }

    private void DestroyBreachConsumptionVisual()
    {
        if (breachGlitchVisual == null)
        {
            return;
        }

        Destroy(breachGlitchVisual);
        breachGlitchVisual = null;
        breachGlitchRenderers = null;
    }

    private IEnumerator DeathExplosionRoutine()
    {
        speedBoostTimer = 0f;
        speedBoostMultiplier = 1f;
        movementSlowTimer = 0f;
        movementSlowMultiplier = 1f;
        shieldTimer = 0f;
        parryTimer = 0f;
        parryCooldownTimer = 0f;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        if (bodyCollider != null)
        {
            bodyCollider.enabled = false;
        }

        if (shieldVisual != null)
        {
            shieldVisual.SetActive(false);
        }

        if (parryVisual != null)
        {
            parryVisual.SetActive(false);
        }

        if (movementTrail != null)
        {
            ParticleSystem.EmissionModule emission = movementTrail.emission;
            emission.rateOverTime = 0f;
            movementTrail.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (!enableDeathExplosion)
        {
            if (bodyRenderer != null)
            {
                Color c = bodyRenderer.color;
                c.a = 0f;
                bodyRenderer.color = c;
            }
            yield break;
        }

        Vector3 origin = transform.position;
        Vector3 startScale = baseBodyScale;
        Color startColor = bodyRenderer != null ? bodyRenderer.color : baseBodyColor;

        GameObject shockwave = CreateDeathShockwave(origin);
        GameObject flash = CreateDeathFlashCore(origin);
        SpriteRenderer[] shards = CreateDeathShards(origin, Mathf.Max(4, deathShardCount), out Vector2[] shardDirs, out float[] shardRotSpeed);

        float chargeDuration = Mathf.Max(0.02f, deathChargeDuration);
        float elapsed = 0f;
        while (elapsed < chargeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / chargeDuration);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 24f);
            if (bodyRenderer != null)
            {
                bodyRenderer.color = Color.Lerp(startColor, deathChargeColor, Mathf.Lerp(0.35f, 1f, t) * (0.75f + pulse * 0.25f));
            }

            transform.localScale = Vector3.Lerp(startScale, startScale * Mathf.Max(1f, deathScaleBoost), Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }

        float flashDuration = Mathf.Max(0.02f, deathFlashDuration);
        elapsed = 0f;
        while (elapsed < flashDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / flashDuration);
            if (bodyRenderer != null)
            {
                bodyRenderer.color = Color.Lerp(deathFlashColor, new Color(deathFlashColor.r, deathFlashColor.g, deathFlashColor.b, 0f), t);
            }
            yield return null;
        }

        if (bodyRenderer != null)
        {
            Color hidden = bodyRenderer.color;
            hidden.a = 0f;
            bodyRenderer.color = hidden;
        }

        float afterglowDuration = Mathf.Max(0.02f, deathAfterglowDuration);
        elapsed = 0f;
        while (elapsed < afterglowDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / afterglowDuration);
            TickDeathFx(origin, shockwave, flash, shards, shardDirs, shardRotSpeed, t);
            yield return null;
        }

        if (shockwave != null)
        {
            Destroy(shockwave);
        }
        if (flash != null)
        {
            Destroy(flash);
        }
        if (shards != null)
        {
            for (int i = 0; i < shards.Length; i++)
            {
                if (shards[i] != null)
                {
                    Destroy(shards[i].gameObject);
                }
            }
        }

        transform.localScale = startScale;
    }

    private void TickDeathFx(
        Vector3 origin,
        GameObject shockwave,
        GameObject flash,
        SpriteRenderer[] shards,
        Vector2[] shardDirs,
        float[] shardRotSpeed,
        float t)
    {
        if (shockwave != null)
        {
            Transform tr = shockwave.transform;
            float r = Mathf.Lerp(0.08f, Mathf.Max(0.2f, deathShockwaveRadius), t);
            tr.position = origin;
            tr.localScale = new Vector3(r, r, 1f);
            SpriteRenderer sr = shockwave.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                Color c = sr.color;
                c.a = Mathf.Lerp(0.72f, 0f, t);
                sr.color = c;
            }
        }

        if (flash != null)
        {
            Transform tr = flash.transform;
            float s = Mathf.Lerp(0.5f, Mathf.Max(0.7f, deathShockwaveRadius * 0.72f), t);
            tr.position = origin;
            tr.localScale = new Vector3(s, s, 1f);
            SpriteRenderer sr = flash.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                Color c = sr.color;
                c.a = Mathf.Lerp(0.65f, 0f, t);
                sr.color = c;
            }
        }

        if (shards == null || shardDirs == null || shardRotSpeed == null)
        {
            return;
        }

        float shardDistance = Mathf.Max(0.2f, deathShardDistance);
        float shardScaleA = Mathf.Lerp(0.19f, 0.07f, t);
        for (int i = 0; i < shards.Length; i++)
        {
            SpriteRenderer sr = shards[i];
            if (sr == null)
            {
                continue;
            }

            Vector2 dir = shardDirs[i];
            float wobble = Mathf.Sin((Time.unscaledTime * 11f) + i * 1.8f) * 0.09f;
            Vector3 pos = origin + (Vector3)(dir * shardDistance * (t + wobble));
            sr.transform.position = pos;
            sr.transform.localScale = new Vector3(shardScaleA, shardScaleA * 0.45f, 1f);
            sr.transform.rotation = Quaternion.Euler(0f, 0f, shardRotSpeed[i] * t);
            Color c = sr.color;
            c.a = Mathf.Lerp(1f, 0f, t);
            sr.color = c;
        }
    }

    private GameObject CreateDeathShockwave(Vector3 position)
    {
        GameObject go = new GameObject("PlayerDeathShockwave");
        go.transform.position = position;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CircleSpriteProvider.Get();
        sr.color = new Color(deathChargeColor.r, deathChargeColor.g, deathChargeColor.b, 0.72f);
        sr.sortingOrder = 15;
        go.transform.localScale = Vector3.one * 0.08f;
        return go;
    }

    private GameObject CreateDeathFlashCore(Vector3 position)
    {
        GameObject go = new GameObject("PlayerDeathFlash");
        go.transform.position = position;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = CircleSpriteProvider.Get();
        sr.color = new Color(deathFlashColor.r, deathFlashColor.g, deathFlashColor.b, 0.66f);
        sr.sortingOrder = 16;
        go.transform.localScale = Vector3.one * 0.5f;
        return go;
    }

    private SpriteRenderer[] CreateDeathShards(Vector3 position, int count, out Vector2[] dirs, out float[] rotationSpeeds)
    {
        dirs = new Vector2[count];
        rotationSpeeds = new float[count];
        SpriteRenderer[] shards = new SpriteRenderer[count];
        for (int i = 0; i < count; i++)
        {
            float angle = (Mathf.PI * 2f * i) / count + Random.Range(-0.14f, 0.14f);
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
            dirs[i] = dir;
            rotationSpeeds[i] = Random.Range(-320f, 320f);

            GameObject shard = new GameObject($"PlayerDeathShard_{i}");
            shard.transform.position = position;
            SpriteRenderer sr = shard.AddComponent<SpriteRenderer>();
            sr.sprite = SquareSpriteProvider.Get();
            sr.color = deathShardColor;
            sr.sortingOrder = 14;
            shard.transform.localScale = new Vector3(0.19f, 0.09f, 1f);
            shards[i] = sr;
        }

        return shards;
    }

    private void SpawnShieldBreakFx()
    {
        Color burst = new Color(1f, 0.76f, 0.92f, 1f);
        GameObject ring = new GameObject("ShieldBreakRingFx");
        ring.transform.position = transform.position;
        SpriteRenderer ringRenderer = ring.AddComponent<SpriteRenderer>();
        ringRenderer.sprite = CircleSpriteProvider.Get();
        ringRenderer.color = new Color(burst.r, burst.g, burst.b, 0.92f);
        ringRenderer.sortingOrder = 16;
        ring.transform.localScale = Vector3.one * 0.24f;
        ring.AddComponent<PlayerShieldBreakBurstFx>().Configure(ringRenderer, Mathf.Max(0.2f, shieldBreakBurstRadius), Mathf.Max(0.08f, shieldBreakBurstDuration), burst);
        Destroy(ring, Mathf.Max(0.12f, shieldBreakBurstDuration + 0.08f));

        int rays = Mathf.Max(4, shieldBreakBurstRayCount);
        for (int i = 0; i < rays; i++)
        {
            float angle = ((Mathf.PI * 2f) * i) / rays + Random.Range(-0.12f, 0.12f);
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            GameObject ray = new GameObject($"ShieldBreakRay_{i}");
            ray.transform.position = transform.position;
            SpriteRenderer sr = ray.AddComponent<SpriteRenderer>();
            sr.sprite = SquareSpriteProvider.Get();
            sr.color = burst;
            sr.sortingOrder = 16;
            ray.transform.localScale = new Vector3(0.2f, 0.07f, 1f);
            ray.AddComponent<PlayerShieldBreakRayFx>().Configure(sr, dir, shieldBreakBurstRadius, shieldBreakBurstDuration);
            Destroy(ray, Mathf.Max(0.12f, shieldBreakBurstDuration + 0.08f));
        }
    }

    private void SpawnParryStartFx()
    {
        GameObject ring = new GameObject("ParryStartRingFx");
        ring.transform.position = transform.position;
        SpriteRenderer ringRenderer = ring.AddComponent<SpriteRenderer>();
        ringRenderer.sprite = CircleSpriteProvider.Get();
        ringRenderer.color = new Color(parryReadyColor.r, parryReadyColor.g, parryReadyColor.b, 0.48f);
        ringRenderer.sortingOrder = 15;
        ring.transform.localScale = Vector3.one * 0.28f;
        ring.AddComponent<PlayerParryBurstFx>().Configure(ringRenderer, Mathf.Max(0.25f, parryRadius), Mathf.Max(0.06f, parryActiveDuration), parryReadyColor);
        Destroy(ring, Mathf.Max(0.1f, parryActiveDuration + 0.06f));
    }

    private void SpawnParrySuccessFx(Vector2 direction)
    {
        Color burst = parrySuccessColor;
        GameObject ring = new GameObject("ParrySuccessRingFx");
        ring.transform.position = transform.position;
        SpriteRenderer ringRenderer = ring.AddComponent<SpriteRenderer>();
        ringRenderer.sprite = CircleSpriteProvider.Get();
        ringRenderer.color = new Color(burst.r, burst.g, burst.b, 0.96f);
        ringRenderer.sortingOrder = 17;
        ring.transform.localScale = Vector3.one * 0.34f;
        ring.AddComponent<PlayerParryBurstFx>().Configure(ringRenderer, Mathf.Max(0.3f, parryFxRadius), Mathf.Max(0.08f, parryFxDuration), burst);
        Destroy(ring, Mathf.Max(0.12f, parryFxDuration + 0.08f));

        int rays = Mathf.Max(4, parryFxRayCount);
        for (int i = 0; i < rays; i++)
        {
            float spread = Random.Range(-38f, 38f);
            Vector3 baseDir = direction.sqrMagnitude > 0.001f ? (Vector3)direction.normalized : Vector3.right;
            Vector2 rayDir = Quaternion.Euler(0f, 0f, spread) * baseDir;
            GameObject ray = new GameObject($"ParrySuccessRay_{i}");
            ray.transform.position = transform.position;
            SpriteRenderer sr = ray.AddComponent<SpriteRenderer>();
            sr.sprite = SquareSpriteProvider.Get();
            sr.color = burst;
            sr.sortingOrder = 17;
            ray.transform.localScale = new Vector3(0.26f, 0.08f, 1f);
            ray.AddComponent<PlayerShieldBreakRayFx>().Configure(sr, rayDir, parryFxRadius, parryFxDuration);
            Destroy(ray, Mathf.Max(0.12f, parryFxDuration + 0.08f));
        }
    }

    private void SpawnFirewallReadyFx()
    {
        GameObject ring = new GameObject("FirewallReadyRingFx");
        ring.transform.position = transform.position;
        SpriteRenderer ringRenderer = ring.AddComponent<SpriteRenderer>();
        ringRenderer.sprite = CircleSpriteProvider.Get();
        ringRenderer.color = new Color(firewallReadyColor.r, firewallReadyColor.g, firewallReadyColor.b, 0.72f);
        ringRenderer.sortingOrder = 15;
        ring.transform.localScale = Vector3.one * 0.45f;
        ring.AddComponent<PlayerParryBurstFx>().Configure(ringRenderer, 1.35f, 0.32f, firewallReadyColor);
        Destroy(ring, 0.42f);
    }

    private void SpawnFirewallBurstFx(float radius)
    {
        Color burst = firewallBurstColor;
        GameObject ring = new GameObject("FirewallBurstRingFx");
        ring.transform.position = transform.position;
        SpriteRenderer ringRenderer = ring.AddComponent<SpriteRenderer>();
        ringRenderer.sprite = CircleSpriteProvider.Get();
        ringRenderer.color = new Color(burst.r, burst.g, burst.b, 0.88f);
        ringRenderer.sortingOrder = 16;
        ring.transform.localScale = Vector3.one * 0.35f;
        ring.AddComponent<PlayerParryBurstFx>().Configure(ringRenderer, Mathf.Max(0.8f, radius), 0.42f, burst);
        Destroy(ring, 0.54f);

        int rays = Mathf.Max(6, firewallBurstRayCount);
        for (int i = 0; i < rays; i++)
        {
            float angle = ((Mathf.PI * 2f) * i) / rays + Random.Range(-0.08f, 0.08f);
            Vector2 rayDir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            GameObject ray = new GameObject($"FirewallBurstRay_{i}");
            ray.transform.position = transform.position;
            SpriteRenderer sr = ray.AddComponent<SpriteRenderer>();
            sr.sprite = SquareSpriteProvider.Get();
            sr.sortingOrder = 16;
            sr.color = i % 3 == 0 ? firewallReadyColor : burst;
            ray.transform.localScale = new Vector3(0.34f, 0.08f, 1f);
            ray.AddComponent<PlayerShieldBreakRayFx>().Configure(sr, rayDir, radius, 0.36f);
            Destroy(ray, 0.48f);
        }
    }
}

public class PlayerParryBurstFx : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private float radius = 1f;
    private float life = 0.2f;
    private Color baseColor = Color.white;
    private float age;

    public void Configure(SpriteRenderer rendererRef, float maxRadius, float duration, Color tint)
    {
        spriteRenderer = rendererRef;
        radius = Mathf.Max(0.15f, maxRadius);
        life = Mathf.Max(0.06f, duration);
        baseColor = tint;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / life);
        transform.localScale = Vector3.one * Mathf.Lerp(0.24f, radius, t);
        if (spriteRenderer != null)
        {
            spriteRenderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Lerp(0.85f, 0f, t));
        }
    }
}

public class PlayerShieldBreakBurstFx : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private float radius = 1f;
    private float life = 0.2f;
    private Color baseColor = Color.white;
    private float age;

    public void Configure(SpriteRenderer rendererRef, float maxRadius, float duration, Color tint)
    {
        spriteRenderer = rendererRef;
        radius = Mathf.Max(0.15f, maxRadius);
        life = Mathf.Max(0.08f, duration);
        baseColor = tint;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / life);
        transform.localScale = Vector3.one * Mathf.Lerp(0.24f, radius, t);
        if (spriteRenderer != null)
        {
            spriteRenderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Lerp(0.9f, 0f, t));
        }
    }
}

public class PlayerShieldBreakRayFx : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Vector2 direction = Vector2.right;
    private float distance = 1f;
    private float life = 0.22f;
    private float age;
    private Vector3 origin;

    public void Configure(SpriteRenderer rendererRef, Vector2 dir, float travelDistance, float duration)
    {
        spriteRenderer = rendererRef;
        direction = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector2.right;
        distance = Mathf.Max(0.15f, travelDistance);
        life = Mathf.Max(0.08f, duration);
        origin = transform.position;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / life);
        float eased = 1f - Mathf.Pow(1f - t, 2f);
        transform.position = origin + (Vector3)(direction * distance * eased);
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        transform.localScale = new Vector3(Mathf.Lerp(0.2f, 0.03f, t), Mathf.Lerp(0.07f, 0.02f, t), 1f);
        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = Mathf.Lerp(1f, 0f, t);
            spriteRenderer.color = c;
        }
    }
}

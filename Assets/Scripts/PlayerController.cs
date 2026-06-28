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

    [Header("Arena Containment")]
    [SerializeField] private float arenaContainmentPadding = 0.08f;
    [SerializeField] private Vector2 fallbackArenaSize = new Vector2(32f, 18f);

    [Header("Ghost Dash")]
    [SerializeField] private float ghostDashSpeed = 24f;
    [SerializeField] private float ghostDashDuration = 0.18f;
    [SerializeField] private float ghostDashCooldown = 2.4f;
    [SerializeField] private Color ghostDashColor = new Color(0.55f, 1f, 0.95f, 0.82f);
    [SerializeField] private int ghostDashAfterimageCount = 4;

    [Header("Compact Mode")]
    [SerializeField] private float compactScaleMultiplier = 0.58f;
    [SerializeField, Range(0.25f, 1f)] private float compactMoveMultiplier = 0.76f;
    [SerializeField] private Color compactColor = new Color(0.76f, 1f, 0.74f, 0.96f);

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

    [Header("Local Versus Parry")]
    [SerializeField] private int localVersusParryChargesMax = 3;
    [SerializeField] private float localVersusParryRechargeSeconds = 40f;
    [SerializeField] private Color parryReadyColor = new Color(0.46f, 0.96f, 1f, 0.62f);
    [SerializeField] private Color parrySuccessColor = new Color(1f, 0.96f, 0.64f, 1f);
    [SerializeField] private float parryFxRadius = 1.65f;
    [SerializeField] private float parryFxDuration = 0.2f;
    [SerializeField] private int parryFxRayCount = 12;

    [Header("Firewall Burst")]
    [SerializeField] private float firewallChargeMax = 100f;
    [SerializeField] private float firewallChargeFromScorePickup = 1.25f;
    [SerializeField] private float firewallChargeFromPowerup = 9f;
    [SerializeField] private float firewallChargeFromObjectiveNode = 14f;
    [SerializeField] private float firewallChargeFromParry = 11f;
    [SerializeField] private float firewallBurstRadius = 4.4f;
    [SerializeField] private float maxFirewallBurstRadius = 6.4f;
    [SerializeField] private float firewallBurstStunDuration = 0.95f;
    [SerializeField] private float maxFirewallBurstStunDuration = 1.55f;
    [SerializeField] private float firewallBurstKnockbackMultiplier = 1.35f;
    [SerializeField] private float maxFirewallChargeGainMultiplier = 1.55f;
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

    [Header("Movement Deformation")]
    [SerializeField, Range(0f, 0.35f)] private float movementStretch = 0.16f;
    [SerializeField, Range(0f, 0.3f)] private float movementSquash = 0.10f;
    [SerializeField, Range(0f, 0.2f)] private float accelerationDeformation = 0.07f;
    [SerializeField] private float movementDeformationResponse = 15f;

    // Mejoras acumulables que permiten adaptar al jugador a eventos agresivos de la arena.
    [Header("Environmental Upgrades")]
    [SerializeField, Range(0.2f, 1f)] private float minExternalDisplacementMultiplier = 0.45f;
    [SerializeField, Range(0.2f, 1f)] private float minSlowDurationUpgradeMultiplier = 0.45f;
    [SerializeField, Range(0.2f, 1f)] private float minSlowSeverityUpgradeMultiplier = 0.55f;
    [SerializeField] private float hazardFirewallChargeCooldown = 0.7f;
    [SerializeField] private float maxHazardFirewallChargeBonus = 9f;

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
    private BoxCollider2D bodyBoxCollider;
    private SpriteRenderer bodyRenderer;
    private ProceduralArenaGenerator arenaGenerator;
    private Vector2 moveInput;
    private Vector2 lastMoveDirection = Vector2.right;
    private Vector2 ghostDashDirection = Vector2.right;
    private float ghostDashTimer;
    private float ghostDashCooldownTimer;
    private float compactTimer;
    private float compactPulsePhase;
    private float speedBoostTimer;
    private float speedBoostMultiplier = 1f;
    private float movementSlowTimer;
    private float movementSlowMultiplier = 1f;
    private float shieldTimer;
    private float shieldDurationMultiplier = 1f;
    private float externalDisplacementUpgradeMultiplier = 1f;
    private float movementSlowDurationUpgradeMultiplier = 1f;
    private float movementSlowSeverityUpgradeMultiplier = 1f;
    private float hazardFirewallChargeBonus;
    private float hazardFirewallChargeTimer;
    private SpriteRenderer shieldRenderer;
    private GameObject shieldVisual;
    private float parryTimer;
    private float parryCooldownTimer;
    private int localVersusParryCharges;
    private float localVersusParryRechargeTimer;
    private SpriteRenderer parryRenderer;
    private GameObject parryVisual;
    private ParticleSystem movementTrail;
    private GameObject skinPatternRoot;
    private readonly List<SpriteRenderer> skinPatternRenderers = new List<SpriteRenderer>();
    private readonly List<Vector3> skinPatternPositions = new List<Vector3>();
    private readonly List<Vector3> skinPatternScales = new List<Vector3>();
    private readonly List<Quaternion> skinPatternRotations = new List<Quaternion>();
    private readonly List<Color> skinPatternColors = new List<Color>();
    private MetaProgressionStorage.SkinPattern selectedSkinPattern;
    private MetaProgressionStorage.TrailStyle selectedTrailStyle;
    private Color selectedSkinAccent = Color.white;
    private Color appliedTrailColor = new Color(-1f, -1f, -1f, -1f);
    private float firewallCharge;
    private float firewallChargeGainMultiplier = 1f;
    private bool deathSequenceActive;
    private bool breachConsumptionActive;
    private GameObject breachGlitchVisual;
    private SpriteRenderer[] breachGlitchRenderers;
    private Color breachGlitchColor = new Color(1f, 0.42f, 0.78f, 1f);
    private Color baseBodyColor = Color.white;
    private Vector3 baseBodyScale = Vector3.one;
    private Vector2 baseBodyColliderSize;
    private Vector2 movementScaleFactor = Vector2.one;
    private Vector2 previousVisualVelocity;

    private static bool tutorialInputLocked;

    public Vector2 CurrentVelocity => rb != null ? rb.linearVelocity : Vector2.zero;
    public Vector2 CurrentMoveInput => moveInput;
    public Vector2 LastMoveDirection => lastMoveDirection;
    public bool IsInDeathSequence => deathSequenceActive;
    public float DeathExplosionDuration => Mathf.Max(0.02f, deathChargeDuration) + Mathf.Max(0.02f, deathFlashDuration) + Mathf.Max(0.02f, deathAfterglowDuration);
    public bool HasShield => shieldTimer > 0f;
    public bool HasSpeedBoost => speedBoostTimer > 0f;
    public bool HasMovementSlow => movementSlowTimer > 0f;
    public bool HasCompactMode => compactTimer > 0f;
    public bool IsParryActive => parryTimer > 0f;
    public bool IsParryReady => parryCooldownTimer <= 0f && HasLocalVersusParryCharge;
    public float ParryCooldownNormalized => IsParryReady
        ? 1f
        : Mathf.Clamp01(1f - parryCooldownTimer / Mathf.Max(0.05f, parryCooldown));
    public bool IsGhostDashing => ghostDashTimer > 0f;
    public bool IsGhostDashReady => ghostDashTimer <= 0f && ghostDashCooldownTimer <= 0f;
    public float GhostDashCooldownNormalized => IsGhostDashReady
        ? 1f
        : Mathf.Clamp01(1f - ghostDashCooldownTimer / Mathf.Max(0.05f, ghostDashCooldown));
    public float ParryRadius => Mathf.Max(0.1f, parryRadius);
    public int LocalVersusParryCharges => LocalVersusModeStorage.IsLocalVersus
        ? Mathf.Clamp(localVersusParryCharges, 0, Mathf.Max(1, localVersusParryChargesMax))
        : Mathf.Max(1, localVersusParryChargesMax);
    public int LocalVersusParryChargesMax => Mathf.Max(1, localVersusParryChargesMax);
    public float LocalVersusParryRechargeNormalized => !LocalVersusModeStorage.IsLocalVersus ||
                                                       localVersusParryCharges >= LocalVersusParryChargesMax
        ? 1f
        : Mathf.Clamp01(1f - localVersusParryRechargeTimer / Mathf.Max(1f, localVersusParryRechargeSeconds));
    public float LocalVersusParryRechargeRemaining => LocalVersusModeStorage.IsLocalVersus
        ? Mathf.Max(0f, localVersusParryRechargeTimer)
        : 0f;
    private bool HasLocalVersusParryCharge =>
        !LocalVersusModeStorage.IsLocalVersus || localVersusParryCharges > 0;
    public float FirewallCharge => firewallCharge;
    public float FirewallChargeMax => Mathf.Max(1f, firewallChargeMax);
    public float FirewallChargeNormalized => Mathf.Clamp01(firewallCharge / FirewallChargeMax);
    public bool IsFirewallBurstReady => firewallCharge >= FirewallChargeMax;
    public float FirewallBurstRadius => firewallBurstRadius;
    public string ActivePowerupLabel
    {
        get
        {
            List<string> active = new List<string>();
            if (HasShield)
            {
                active.Add("Shield");
            }
            if (HasSpeedBoost)
            {
                active.Add("Speed");
            }
            if (HasCompactMode)
            {
                active.Add("Compact");
            }
            if (HasMovementSlow)
            {
                active.Add("Suppressed");
            }

            return active.Count > 0 ? string.Join(" + ", active) : "None";
        }
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        bodyBoxCollider = bodyCollider as BoxCollider2D;
        bodyRenderer = GetComponent<SpriteRenderer>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        localVersusParryCharges = Mathf.Max(1, localVersusParryChargesMax);
        localVersusParryRechargeTimer = 0f;

        baseBodyScale = transform.localScale;
        if (bodyBoxCollider != null)
        {
            baseBodyColliderSize = bodyBoxCollider.size;
        }
        if (bodyRenderer != null)
        {
            baseBodyColor = bodyRenderer.color;
        }

        ApplySelectedMetaSkin();
        EnsureSelectedSkinVisual();
        EnsureShieldVisual();
        EnsureParryVisual();
        EnsureMovementTrail();
    }

    private void LateUpdate()
    {
        UpdateSelectedSkinVisual();

        if (deathSequenceActive || breachConsumptionActive || rb == null)
        {
            return;
        }

        UpdateMovementDeformation();

        Vector2 contained = ClampToPlayableArena(rb.position);
        if ((contained - rb.position).sqrMagnitude <= 0.000001f)
        {
            return;
        }

        rb.position = contained;
        transform.position = new Vector3(contained.x, contained.y, transform.position.z);
        RemoveVelocityTowardArenaExterior(contained);
    }

    private void OnDisable()
    {
        movementScaleFactor = Vector2.one;
        previousVisualVelocity = Vector2.zero;
        if (bodyBoxCollider != null)
        {
            bodyBoxCollider.size = baseBodyColliderSize;
        }
    }

    private void UpdateMovementDeformation()
    {
        Vector3 authoredScale = transform.localScale;
        Vector2 velocity = rb.linearVelocity;
        float speedReference = Mathf.Max(0.1f, moveSpeed * 1.1f);
        float speed = Mathf.Clamp01(velocity.magnitude / speedReference);
        Vector2 direction = velocity.sqrMagnitude > 0.001f ? velocity.normalized : lastMoveDirection;
        Vector2 absoluteDirection = new Vector2(Mathf.Abs(direction.x), Mathf.Abs(direction.y));

        float velocityDelta = (velocity - previousVisualVelocity).magnitude;
        float acceleration = Mathf.Clamp01(velocityDelta / Mathf.Max(0.1f, moveSpeed * 0.8f));
        float targetX = 1f + speed * (absoluteDirection.x * movementStretch - absoluteDirection.y * movementSquash);
        float targetY = 1f + speed * (absoluteDirection.y * movementStretch - absoluteDirection.x * movementSquash);
        targetX += acceleration * accelerationDeformation * absoluteDirection.x;
        targetY += acceleration * accelerationDeformation * absoluteDirection.y;

        Vector2 targetFactor = new Vector2(Mathf.Max(0.72f, targetX), Mathf.Max(0.72f, targetY));
        float response = 1f - Mathf.Exp(-Mathf.Max(1f, movementDeformationResponse) * Time.unscaledDeltaTime);
        movementScaleFactor = Vector2.Lerp(movementScaleFactor, targetFactor, response);
        transform.localScale = new Vector3(
            authoredScale.x * movementScaleFactor.x,
            authoredScale.y * movementScaleFactor.y,
            authoredScale.z);

        // Compensa solo la deformacion visual para conservar el area de colision original.
        if (bodyBoxCollider != null)
        {
            bodyBoxCollider.size = new Vector2(
                baseBodyColliderSize.x / Mathf.Max(0.1f, movementScaleFactor.x),
                baseBodyColliderSize.y / Mathf.Max(0.1f, movementScaleFactor.y));
        }

        previousVisualVelocity = velocity;
    }

    private void ApplySelectedMetaSkin()
    {
        if (!MetaProgressionStorage.TryGetSelectedSkin(out MetaProgressionStorage.UnlockDefinition skin))
        {
            return;
        }

        trailColor = skin.bodyColor;
        trailColor.a = Mathf.Clamp01(skin.trailColor.a);
        baseBodyColor = skin.bodyColor;
        selectedSkinAccent = skin.accentColor;
        selectedSkinPattern = skin.skinPattern;
        selectedTrailStyle = skin.trailStyle;
        if (bodyRenderer != null)
        {
            bodyRenderer.color = baseBodyColor;
        }

        SyncMovementTrailColor(baseBodyColor, true);
    }

    private void EnsureSelectedSkinVisual()
    {
        if (bodyRenderer == null || bodyRenderer.sprite == null)
        {
            return;
        }

        if (skinPatternRoot != null)
        {
            Destroy(skinPatternRoot);
        }

        skinPatternRenderers.Clear();
        skinPatternPositions.Clear();
        skinPatternScales.Clear();
        skinPatternRotations.Clear();
        skinPatternColors.Clear();

        skinPatternRoot = new GameObject("PlayerSkinPattern");
        skinPatternRoot.transform.SetParent(transform, false);

        switch (selectedSkinPattern)
        {
            case MetaProgressionStorage.SkinPattern.Split:
                AddSkinPart("Falla", Vector2.zero, new Vector2(0.11f, 0.92f), selectedSkinAccent, -12f);
                AddSkinPart("CorteA", new Vector2(-0.30f, 0.28f), new Vector2(0.32f, 0.10f), selectedSkinAccent, -12f);
                AddSkinPart("CorteB", new Vector2(0.28f, -0.26f), new Vector2(0.32f, 0.10f), selectedSkinAccent, -12f);
                break;
            case MetaProgressionStorage.SkinPattern.Circuit:
                AddSkinPart("CircuitoH", Vector2.zero, new Vector2(0.70f, 0.08f), selectedSkinAccent);
                AddSkinPart("CircuitoV", new Vector2(0.12f, 0.05f), new Vector2(0.08f, 0.62f), selectedSkinAccent);
                AddSkinPart("Nodo", new Vector2(0.12f, 0.05f), new Vector2(0.20f, 0.20f), Color.Lerp(selectedSkinAccent, Color.white, 0.48f));
                break;
            case MetaProgressionStorage.SkinPattern.Hazard:
                AddSkinPart("FranjaA", new Vector2(-0.28f, 0f), new Vector2(0.13f, 1.08f), selectedSkinAccent, -28f);
                AddSkinPart("FranjaB", Vector2.zero, new Vector2(0.13f, 1.08f), selectedSkinAccent, -28f);
                AddSkinPart("FranjaC", new Vector2(0.28f, 0f), new Vector2(0.13f, 1.08f), selectedSkinAccent, -28f);
                break;
            case MetaProgressionStorage.SkinPattern.Firewall:
                AddSkinFrame(selectedSkinAccent, 0.10f, 0.80f);
                break;
            case MetaProgressionStorage.SkinPattern.Fracture:
                AddSkinPart("FracturaA", new Vector2(-0.18f, 0.20f), new Vector2(0.10f, 0.62f), selectedSkinAccent, -38f);
                AddSkinPart("FracturaB", new Vector2(0.18f, -0.18f), new Vector2(0.09f, 0.54f), selectedSkinAccent, 36f);
                AddSkinPart("FracturaC", new Vector2(0.18f, 0.28f), new Vector2(0.26f, 0.08f), selectedSkinAccent, -12f);
                AddSkinPart("FracturaD", new Vector2(-0.26f, -0.30f), new Vector2(0.22f, 0.07f), selectedSkinAccent, 18f);
                break;
            case MetaProgressionStorage.SkinPattern.Signal:
                AddSkinPart("Escaner", new Vector2(0f, -0.28f), new Vector2(0.82f, 0.09f), selectedSkinAccent);
                AddSkinPart("Visor", new Vector2(0f, 0.18f), new Vector2(0.54f, 0.13f), Color.Lerp(selectedSkinAccent, Color.white, 0.35f));
                AddSkinPart("Marca", new Vector2(0.31f, -0.20f), new Vector2(0.11f, 0.22f), selectedSkinAccent);
                break;
            case MetaProgressionStorage.SkinPattern.Prism:
                AddSkinPart("PrismaA", new Vector2(-0.22f, 0.22f), new Vector2(0.38f, 0.38f), selectedSkinAccent);
                AddSkinPart("PrismaB", new Vector2(0.22f, 0.22f), new Vector2(0.38f, 0.38f), new Color(1f, 0.48f, 0.78f, 1f));
                AddSkinPart("PrismaC", new Vector2(-0.22f, -0.22f), new Vector2(0.38f, 0.38f), new Color(0.48f, 0.72f, 1f, 1f));
                AddSkinPart("PrismaD", new Vector2(0.22f, -0.22f), new Vector2(0.38f, 0.38f), new Color(1f, 0.86f, 0.42f, 1f));
                break;
            case MetaProgressionStorage.SkinPattern.Orbit:
                AddSkinPart("Nucleo", Vector2.zero, new Vector2(0.24f, 0.24f), selectedSkinAccent);
                AddSkinPart("OrbitaN", new Vector2(0f, 0.40f), new Vector2(0.14f, 0.14f), selectedSkinAccent);
                AddSkinPart("OrbitaE", new Vector2(0.40f, 0f), new Vector2(0.14f, 0.14f), selectedSkinAccent);
                AddSkinPart("OrbitaS", new Vector2(0f, -0.40f), new Vector2(0.14f, 0.14f), selectedSkinAccent);
                AddSkinPart("OrbitaO", new Vector2(-0.40f, 0f), new Vector2(0.14f, 0.14f), selectedSkinAccent);
                break;
            case MetaProgressionStorage.SkinPattern.Containment:
                AddSkinFrame(selectedSkinAccent, 0.08f, 0.50f);
                AddSkinPart("Sello", Vector2.zero, new Vector2(0.24f, 0.24f), Color.Lerp(selectedSkinAccent, Color.white, 0.45f), 45f);
                break;
            default:
                AddSkinPart("Nucleo", Vector2.zero, new Vector2(0.24f, 0.24f), selectedSkinAccent, 45f);
                break;
        }
    }

    private void AddSkinFrame(Color color, float thickness, float length)
    {
        float edge = 0.40f;
        AddSkinPart("MarcoL", new Vector2(-edge, 0f), new Vector2(thickness, length), color);
        AddSkinPart("MarcoR", new Vector2(edge, 0f), new Vector2(thickness, length), color);
        AddSkinPart("MarcoT", new Vector2(0f, edge), new Vector2(length, thickness), color);
        AddSkinPart("MarcoB", new Vector2(0f, -edge), new Vector2(length, thickness), color);
    }

    private void AddSkinPart(string partName, Vector2 position, Vector2 scale, Color color, float rotation = 0f)
    {
        GameObject part = new GameObject(partName);
        part.transform.SetParent(skinPatternRoot.transform, false);
        part.transform.localPosition = new Vector3(position.x, position.y, -0.01f);
        part.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        part.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);

        SpriteRenderer renderer = part.AddComponent<SpriteRenderer>();
        renderer.sprite = bodyRenderer.sprite;
        renderer.color = color;
        renderer.sortingLayerID = bodyRenderer.sortingLayerID;
        renderer.sortingOrder = bodyRenderer.sortingOrder + 1;

        skinPatternRenderers.Add(renderer);
        skinPatternPositions.Add(part.transform.localPosition);
        skinPatternScales.Add(part.transform.localScale);
        skinPatternRotations.Add(part.transform.localRotation);
        skinPatternColors.Add(color);
    }

    private void UpdateSelectedSkinVisual()
    {
        if (skinPatternRoot == null || bodyRenderer == null)
        {
            return;
        }

        float time = Time.unscaledTime;
        float pulse = 0.5f + 0.5f * Mathf.Sin(time * 6.5f);
        skinPatternRoot.transform.localRotation = selectedSkinPattern == MetaProgressionStorage.SkinPattern.Orbit
            ? Quaternion.Euler(0f, 0f, time * 52f)
            : Quaternion.identity;

        for (int i = 0; i < skinPatternRenderers.Count; i++)
        {
            SpriteRenderer renderer = skinPatternRenderers[i];
            if (renderer == null)
            {
                continue;
            }

            Transform part = renderer.transform;
            part.localPosition = skinPatternPositions[i];
            part.localScale = skinPatternScales[i];
            part.localRotation = skinPatternRotations[i];
            Color color = skinPatternColors[i];

            if (selectedSkinPattern == MetaProgressionStorage.SkinPattern.Signal && i == 0)
            {
                Vector3 position = skinPatternPositions[i];
                position.y = Mathf.Lerp(-0.34f, 0.34f, Mathf.PingPong(time * 1.4f, 1f));
                part.localPosition = position;
            }
            else if (selectedSkinPattern == MetaProgressionStorage.SkinPattern.Fracture)
            {
                Vector3 position = skinPatternPositions[i];
                position += new Vector3(Mathf.Sin(time * 17f + i * 2.1f), Mathf.Cos(time * 13f + i), 0f) * 0.012f;
                part.localPosition = position;
            }
            else if (selectedSkinPattern == MetaProgressionStorage.SkinPattern.Prism)
            {
                color = Color.HSVToRGB(Mathf.Repeat(time * 0.10f + i * 0.21f, 1f), 0.58f, 1f);
            }

            color.a = bodyRenderer.color.a * Mathf.Lerp(0.72f, 0.96f, pulse);
            renderer.color = color;
        }
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

        if (tutorialInputLocked)
        {
            rb.linearVelocity = Vector2.zero;
            UpdatePowerupTimers();
            UpdateShieldVisual();
            UpdateParryTimers();
            UpdateParryVisual();
            UpdateGhostDashTimers();
            UpdateGhostDashVisual();
            UpdateMovementTrail(0f);
            return;
        }

        UpdatePowerupTimers();
        UpdateShieldVisual();
        UpdateParryTimers();
        UpdateLocalVersusParryCharges();
        UpdateParryVisual();
        UpdateGhostDashTimers();
        UpdateGhostDashVisual();

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
        if (moveInput.sqrMagnitude > 0.001f)
        {
            lastMoveDirection = moveInput.normalized;
        }

        if (WasGhostDashPressed())
        {
            TryStartGhostDash(moveInput);
        }

        float effectiveSpeed = moveSpeed;
        if (speedBoostTimer > 0f)
        {
            effectiveSpeed *= speedBoostMultiplier;
        }
        if (movementSlowTimer > 0f)
        {
            effectiveSpeed *= movementSlowMultiplier;
        }
        if (compactTimer > 0f)
        {
            effectiveSpeed *= Mathf.Clamp(compactMoveMultiplier, 0.25f, 1f);
        }

        if (ghostDashTimer > 0f)
        {
            rb.linearVelocity = ghostDashDirection * Mathf.Max(effectiveSpeed, ghostDashSpeed);
            UpdateMovementTrail(Mathf.Max(effectiveSpeed, ghostDashSpeed));
            return;
        }

        rb.linearVelocity = moveInput * effectiveSpeed;
        UpdateMovementTrail(Mathf.Max(0.01f, effectiveSpeed));
    }

    private static bool WasGhostDashPressed()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.leftShiftKey.wasPressedThisFrame || keyboard.rightShiftKey.wasPressedThisFrame))
        {
            return true;
        }
        if (LocalVersusModeStorage.IsLocalVersus && Gamepad.all.Count > 0 && Gamepad.all[0].buttonEast.wasPressedThisFrame)
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift);
#else
        return false;
#endif
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

        if (!LocalVersusModeStorage.IsLocalVersus && mouse != null && mouse.rightButton.wasPressedThisFrame)
        {
            return true;
        }
        if (LocalVersusModeStorage.IsLocalVersus && Gamepad.all.Count > 0 && Gamepad.all[0].buttonNorth.wasPressedThisFrame)
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Q) || Input.GetKeyDown(KeyCode.R) ||
               (!LocalVersusModeStorage.IsLocalVersus && Input.GetMouseButtonDown(1));
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

        if (!LocalVersusModeStorage.IsLocalVersus && mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            return true;
        }
        if (LocalVersusModeStorage.IsLocalVersus && Gamepad.all.Count > 0 && Gamepad.all[0].buttonSouth.wasPressedThisFrame)
        {
            return true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E) ||
               (!LocalVersusModeStorage.IsLocalVersus && Input.GetMouseButtonDown(0));
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

            if (keyboard.aKey.isPressed || (!LocalVersusModeStorage.IsLocalVersus && keyboard.leftArrowKey.isPressed)) horizontal -= 1f;
            if (keyboard.dKey.isPressed || (!LocalVersusModeStorage.IsLocalVersus && keyboard.rightArrowKey.isPressed)) horizontal += 1f;
            if (keyboard.sKey.isPressed || (!LocalVersusModeStorage.IsLocalVersus && keyboard.downArrowKey.isPressed)) vertical -= 1f;
            if (keyboard.wKey.isPressed || (!LocalVersusModeStorage.IsLocalVersus && keyboard.upArrowKey.isPressed)) vertical += 1f;

            Vector2 combined = new Vector2(horizontal, vertical);
            if (LocalVersusModeStorage.IsLocalVersus && Gamepad.all.Count > 0)
            {
                combined += Gamepad.all[0].leftStick.ReadValue();
            }

            return Vector2.ClampMagnitude(combined, 1f);
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
#else
        return Vector2.zero;
#endif
    }

    public static void SetTutorialInputLocked(bool locked)
    {
        tutorialInputLocked = locked;
    }

    public Vector2 GetPosition()
    {
        return rb.position;
    }

    public void ApplyExternalDisplacement(Vector2 delta)
    {
        if (deathSequenceActive || breachConsumptionActive || ghostDashTimer > 0f || delta.sqrMagnitude <= 0f)
        {
            return;
        }

        Vector2 adjustedDelta = delta * Mathf.Clamp(externalDisplacementUpgradeMultiplier, minExternalDisplacementMultiplier, 1f);
        Vector2 target = ClampToPlayableArena(rb.position + adjustedDelta);
        rb.position = target;
        transform.position = new Vector3(target.x, target.y, transform.position.z);
        TryConvertHazardPressureToFirewall();
    }

    public void TeleportTo(Vector2 position)
    {
        TeleportTo(position, false);
    }

    public void TeleportTo(Vector2 position, bool preserveVelocity)
    {
        if (deathSequenceActive || breachConsumptionActive || rb == null)
        {
            return;
        }

        Vector2 velocity = rb.linearVelocity;
        Vector2 target = ClampToPlayableArena(position);
        rb.position = target;
        rb.linearVelocity = preserveVelocity ? velocity : Vector2.zero;
        transform.position = new Vector3(target.x, target.y, transform.position.z);
        if (preserveVelocity)
        {
            RemoveVelocityTowardArenaExterior(target);
        }
    }

    public Vector2 ClampToPlayableArena(Vector2 position)
    {
        ResolveArenaGenerator();
        Vector2 size = arenaGenerator != null
            ? new Vector2(arenaGenerator.ArenaWidth, arenaGenerator.ArenaHeight)
            : fallbackArenaSize;
        Vector2 center = arenaGenerator != null ? (Vector2)arenaGenerator.transform.position : Vector2.zero;
        Vector2 extents = bodyCollider != null
            ? new Vector2(bodyCollider.bounds.extents.x, bodyCollider.bounds.extents.y)
            : Vector2.one * 0.45f;
        float padding = Mathf.Max(0.02f, arenaContainmentPadding);
        float marginX = Mathf.Min(size.x * 0.45f, Mathf.Max(0.1f, extents.x + padding));
        float marginY = Mathf.Min(size.y * 0.45f, Mathf.Max(0.1f, extents.y + padding));
        Vector2 half = size * 0.5f;

        return new Vector2(
            Mathf.Clamp(position.x, center.x - half.x + marginX, center.x + half.x - marginX),
            Mathf.Clamp(position.y, center.y - half.y + marginY, center.y + half.y - marginY));
    }

    private void ResolveArenaGenerator()
    {
        if (arenaGenerator == null)
        {
            arenaGenerator = FindAnyObjectByType<ProceduralArenaGenerator>();
        }
    }

    private void RemoveVelocityTowardArenaExterior(Vector2 containedPosition)
    {
        ResolveArenaGenerator();
        Vector2 size = arenaGenerator != null
            ? new Vector2(arenaGenerator.ArenaWidth, arenaGenerator.ArenaHeight)
            : fallbackArenaSize;
        Vector2 center = arenaGenerator != null ? (Vector2)arenaGenerator.transform.position : Vector2.zero;
        Vector2 extents = bodyCollider != null
            ? new Vector2(bodyCollider.bounds.extents.x, bodyCollider.bounds.extents.y)
            : Vector2.one * 0.45f;
        float padding = Mathf.Max(0.02f, arenaContainmentPadding);
        Vector2 half = size * 0.5f;
        float minX = center.x - half.x + extents.x + padding;
        float maxX = center.x + half.x - extents.x - padding;
        float minY = center.y - half.y + extents.y + padding;
        float maxY = center.y + half.y - extents.y - padding;
        Vector2 velocity = rb.linearVelocity;

        if ((containedPosition.x <= minX + 0.001f && velocity.x < 0f) ||
            (containedPosition.x >= maxX - 0.001f && velocity.x > 0f))
        {
            velocity.x = 0f;
        }
        if ((containedPosition.y <= minY + 0.001f && velocity.y < 0f) ||
            (containedPosition.y >= maxY - 0.001f && velocity.y > 0f))
        {
            velocity.y = 0f;
        }

        rb.linearVelocity = velocity;
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

    public void ApplyCompactMode(float duration, float scaleMultiplier, float moveMultiplier)
    {
        if (deathSequenceActive || breachConsumptionActive)
        {
            return;
        }

        compactScaleMultiplier = Mathf.Clamp(scaleMultiplier, 0.35f, 0.88f);
        compactMoveMultiplier = Mathf.Clamp(moveMultiplier, 0.25f, 1f);
        compactTimer = Mathf.Max(compactTimer, Mathf.Max(0.25f, duration));
        compactPulsePhase = Random.Range(0f, Mathf.PI * 2f);
        SpawnCompactModeFx();
    }

    public void ApplyMovementSlow(float multiplier, float duration)
    {
        // La resistencia ambiental acerca el slow a velocidad normal y acorta su duracion.
        float clampedMultiplier = Mathf.Clamp(multiplier, minSlowMultiplier, 1f);
        float mitigatedMultiplier = Mathf.Lerp(1f, clampedMultiplier, Mathf.Clamp01(movementSlowSeverityUpgradeMultiplier));
        float mitigatedDuration = Mathf.Max(0.05f, duration * Mathf.Clamp(movementSlowDurationUpgradeMultiplier, minSlowDurationUpgradeMultiplier, 1f));
        movementSlowMultiplier = Mathf.Min(
            movementSlowTimer > 0f ? movementSlowMultiplier : 1f,
            Mathf.Clamp(mitigatedMultiplier, minSlowMultiplier, 1f));
        movementSlowTimer = Mathf.Max(movementSlowTimer, mitigatedDuration);
        TryConvertHazardPressureToFirewall();
    }

    public bool TryAbsorbHit()
    {
        if (deathSequenceActive)
        {
            return false;
        }

        if (ghostDashTimer > 0f)
        {
            SpawnGhostDashContactFx();
            return true;
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

    public void ApplyDefensiveSystemDegradation(float parryCooldownMultiplier, float firewallGainMultiplier)
    {
        parryCooldown = Mathf.Max(0.05f, parryCooldown * Mathf.Max(1f, parryCooldownMultiplier));
        firewallChargeGainMultiplier = Mathf.Clamp(
            firewallChargeGainMultiplier * Mathf.Clamp(firewallGainMultiplier, 0.1f, 1f),
            0.1f,
            Mathf.Max(1f, maxFirewallChargeGainMultiplier));
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

    public void ImproveHazardResistance(float multiplier)
    {
        float clamped = Mathf.Clamp(multiplier, 0.2f, 1f);
        movementSlowDurationUpgradeMultiplier = Mathf.Max(
            Mathf.Max(0.05f, minSlowDurationUpgradeMultiplier),
            movementSlowDurationUpgradeMultiplier * clamped);
        movementSlowSeverityUpgradeMultiplier = Mathf.Max(
            Mathf.Max(0.05f, minSlowSeverityUpgradeMultiplier),
            movementSlowSeverityUpgradeMultiplier * clamped);

        if (movementSlowTimer > 0f)
        {
            movementSlowTimer *= clamped;
            movementSlowMultiplier = Mathf.Lerp(1f, movementSlowMultiplier, Mathf.Clamp01(movementSlowSeverityUpgradeMultiplier));
        }
    }

    public void ImproveExternalDisplacementResistance(float multiplier)
    {
        externalDisplacementUpgradeMultiplier = Mathf.Max(
            Mathf.Max(0.05f, minExternalDisplacementMultiplier),
            externalDisplacementUpgradeMultiplier * Mathf.Clamp(multiplier, 0.2f, 1f));
    }

    public void ImproveHazardFirewallCharge(float bonus)
    {
        hazardFirewallChargeBonus = Mathf.Min(
            Mathf.Max(0f, maxHazardFirewallChargeBonus),
            hazardFirewallChargeBonus + Mathf.Max(0f, bonus));
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

    public bool TryStartParryFromTutorial()
    {
        if (deathSequenceActive || breachConsumptionActive || parryCooldownTimer > 0f)
        {
            return false;
        }

        StartParry();
        ScanParryWindow();
        return true;
    }

    public bool TryStartGhostDashFromTutorial()
    {
        if (deathSequenceActive || breachConsumptionActive)
        {
            return false;
        }

        Vector2 direction = ReadMoveInput();
        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = lastMoveDirection;
        }

        return TryStartGhostDash(direction);
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

    private void TryConvertHazardPressureToFirewall()
    {
        // El riesgo ambiental puede transformarse en recurso, pero con cooldown para evitar carga gratis por frame.
        if (hazardFirewallChargeBonus <= 0f || hazardFirewallChargeTimer > 0f)
        {
            return;
        }

        AddFirewallCharge(hazardFirewallChargeBonus);
        hazardFirewallChargeTimer = Mathf.Max(0.05f, hazardFirewallChargeCooldown);
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
        FindAnyObjectByType<GameManager>()?.NotifyFirewallBurstActivated();
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
        FindAnyObjectByType<GameManager>()?.NotifyParrySuccess();
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
        compactTimer = 0f;
        shieldTimer = 0f;
        parryTimer = 0f;
        parryCooldownTimer = 0f;
        ghostDashTimer = 0f;
        ghostDashCooldownTimer = 0f;
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

        if (compactTimer > 0f)
        {
            compactTimer -= Time.deltaTime;
            if (compactTimer < 0f)
            {
                compactTimer = 0f;
            }
        }

        if (ghostDashCooldownTimer > 0f)
        {
            ghostDashCooldownTimer -= Time.deltaTime;
            if (ghostDashCooldownTimer < 0f)
            {
                ghostDashCooldownTimer = 0f;
            }
        }

        if (hazardFirewallChargeTimer > 0f)
        {
            hazardFirewallChargeTimer -= Time.deltaTime;
            if (hazardFirewallChargeTimer < 0f)
            {
                hazardFirewallChargeTimer = 0f;
            }
        }
    }

    private void UpdateGhostDashTimers()
    {
        if (ghostDashTimer <= 0f)
        {
            return;
        }

        ghostDashTimer -= Time.deltaTime;
        if (ghostDashTimer <= 0f)
        {
            ghostDashTimer = 0f;
        }
    }

    private bool TryStartGhostDash(Vector2 requestedDirection)
    {
        if (deathSequenceActive || breachConsumptionActive || ghostDashTimer > 0f || ghostDashCooldownTimer > 0f)
        {
            return false;
        }

        Vector2 direction = requestedDirection.sqrMagnitude > 0.001f ? requestedDirection.normalized : lastMoveDirection;
        if (direction.sqrMagnitude <= 0.001f)
        {
            direction = Vector2.right;
        }

        ghostDashDirection = direction.normalized;
        lastMoveDirection = ghostDashDirection;
        ghostDashTimer = Mathf.Max(0.05f, ghostDashDuration);
        ghostDashCooldownTimer = Mathf.Max(0.1f, ghostDashCooldown);
        SpawnGhostDashStartFx(ghostDashDirection);
        GlitchAudioManager.PlayGhostDash(transform.position);
        return true;
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

    private void UpdateLocalVersusParryCharges()
    {
        if (!LocalVersusModeStorage.IsLocalVersus)
        {
            localVersusParryCharges = Mathf.Max(1, localVersusParryChargesMax);
            localVersusParryRechargeTimer = 0f;
            return;
        }

        int maximum = Mathf.Max(1, localVersusParryChargesMax);
        if (localVersusParryCharges >= maximum)
        {
            localVersusParryCharges = maximum;
            localVersusParryRechargeTimer = 0f;
            return;
        }

        localVersusParryRechargeTimer -= Time.deltaTime;
        if (localVersusParryRechargeTimer <= 0f)
        {
            localVersusParryCharges = maximum;
            localVersusParryRechargeTimer = 0f;
        }
    }

    private void StartParry()
    {
        if (!HasLocalVersusParryCharge)
        {
            return;
        }

        if (LocalVersusModeStorage.IsLocalVersus)
        {
            int maximum = Mathf.Max(1, localVersusParryChargesMax);
            bool wasFull = localVersusParryCharges >= maximum;
            localVersusParryCharges = Mathf.Max(0, localVersusParryCharges - 1);
            if (wasFull || localVersusParryRechargeTimer <= 0f)
            {
                localVersusParryRechargeTimer = Mathf.Max(1f, localVersusParryRechargeSeconds);
            }
        }

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

            SplitAnomalyCloneController clone = hit.GetComponent<SplitAnomalyCloneController>();
            if (clone != null && TryParryHit(clone.transform.position, out Vector2 cloneParryDirection))
            {
                clone.ApplyParryImpact(GetPosition(), cloneParryDirection);
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

    private void UpdateGhostDashVisual()
    {
        if (bodyRenderer == null || deathSequenceActive || breachConsumptionActive)
        {
            return;
        }

        Vector3 targetScale = GetCurrentBodyScale();
        if (ghostDashTimer <= 0f)
        {
            bodyRenderer.color = GetCurrentBodyColor();
            transform.localScale = targetScale;
            return;
        }

        float t = Mathf.Clamp01(ghostDashTimer / Mathf.Max(0.05f, ghostDashDuration));
        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 38f);
        Color c = Color.Lerp(GetCurrentBodyColor(), ghostDashColor, 0.68f + pulse * 0.20f);
        c.a = Mathf.Lerp(0.38f, 0.74f, t);
        bodyRenderer.color = c;
        transform.localScale = targetScale * Mathf.Lerp(0.78f, 1.04f, t);
    }

    private Vector3 GetCurrentBodyScale()
    {
        if (compactTimer <= 0f)
        {
            return baseBodyScale;
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 10f + compactPulsePhase);
        float compactScale = Mathf.Clamp(compactScaleMultiplier, 0.35f, 0.88f) * Mathf.Lerp(0.96f, 1.05f, pulse);
        return baseBodyScale * compactScale;
    }

    private Color GetCurrentBodyColor()
    {
        if (compactTimer <= 0f)
        {
            return baseBodyColor;
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 12f + compactPulsePhase);
        Color c = Color.Lerp(baseBodyColor, compactColor, 0.52f + pulse * 0.18f);
        c.a = Mathf.Lerp(0.76f, 0.96f, pulse);
        return c;
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
        main.startColor = Color.white;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

        ParticleSystem.EmissionModule emission = movementTrail.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = movementTrail.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.12f;
        shape.radiusThickness = 1f;

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

        ConfigureMovementTrailStyle();
        SyncMovementTrailColor(bodyRenderer != null ? bodyRenderer.color : baseBodyColor, true);
        movementTrail.Play();
    }

    private void ConfigureMovementTrailStyle()
    {
        if (movementTrail == null)
        {
            return;
        }

        ParticleSystem.MainModule main = movementTrail.main;
        ParticleSystem.ShapeModule shape = movementTrail.shape;
        switch (selectedTrailStyle)
        {
            case MetaProgressionStorage.TrailStyle.Echo:
                main.startLifetime = Mathf.Max(0.12f, trailParticleLifetime * 1.35f);
                main.startSize = Mathf.Max(0.04f, trailStartSize * 1.18f);
                shape.radius = 0.15f;
                break;
            case MetaProgressionStorage.TrailStyle.Pixels:
                main.startLifetime = Mathf.Max(0.10f, trailParticleLifetime * 0.88f);
                main.startSize = Mathf.Max(0.04f, trailStartSize * 1.32f);
                shape.radius = 0.10f;
                break;
            case MetaProgressionStorage.TrailStyle.Sparks:
                main.startLifetime = Mathf.Max(0.08f, trailParticleLifetime * 0.72f);
                main.startSize = Mathf.Max(0.03f, trailStartSize * 0.68f);
                shape.radius = 0.17f;
                break;
            case MetaProgressionStorage.TrailStyle.Pulse:
                main.startLifetime = Mathf.Max(0.12f, trailParticleLifetime * 1.12f);
                main.startSize = Mathf.Max(0.04f, trailStartSize * 1.06f);
                shape.radius = 0.08f;
                break;
            default:
                main.startLifetime = Mathf.Max(0.08f, trailParticleLifetime);
                main.startSize = Mathf.Max(0.03f, trailStartSize);
                shape.radius = 0.12f;
                break;
        }
    }

    private void SyncMovementTrailColor(Color visibleBodyColor, bool force = false)
    {
        if (movementTrail == null)
        {
            return;
        }

        Color visualColor = visibleBodyColor;
        visualColor.a = 1f;
        Vector4 colorDelta = (Vector4)visualColor - (Vector4)appliedTrailColor;
        if (!force && colorDelta.sqrMagnitude < 0.0004f)
        {
            return;
        }

        appliedTrailColor = visualColor;
        ParticleSystem.MainModule main = movementTrail.main;
        main.startColor = Color.white;

        Color endColor = Color.Lerp(visualColor, Color.white, 0.12f);
        Gradient colorGradient = new Gradient();
        colorGradient.SetKeys(
            new[]
            {
                new GradientColorKey(visualColor, 0f),
                new GradientColorKey(visualColor, 0.62f),
                new GradientColorKey(endColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(Mathf.Clamp01(trailColor.a), 0f),
                new GradientAlphaKey(Mathf.Clamp01(trailColor.a * 0.48f), 0.62f),
                new GradientAlphaKey(0f, 1f)
            });

        ParticleSystem.ColorOverLifetimeModule colorLifetime = movementTrail.colorOverLifetime;
        colorLifetime.enabled = true;
        colorLifetime.color = new ParticleSystem.MinMaxGradient(colorGradient);
    }

    private void UpdateMovementTrail(float effectiveSpeed)
    {
        if (!enableMovementTrail || movementTrail == null)
        {
            return;
        }

        SyncMovementTrailColor(bodyRenderer != null ? bodyRenderer.color : baseBodyColor);

        float velocityMagnitude = rb != null ? rb.linearVelocity.magnitude : 0f;
        if (velocityMagnitude <= trailMinVelocityToEmit)
        {
            ParticleSystem.EmissionModule idleEmission = movementTrail.emission;
            idleEmission.rateOverTime = 0f;
            return;
        }

        float normalized = Mathf.Clamp01(velocityMagnitude / Mathf.Max(0.01f, effectiveSpeed));
        float pulse = 0.85f + 0.15f * Mathf.Sin(Time.time * 18f);
        float styleMultiplier = selectedTrailStyle == MetaProgressionStorage.TrailStyle.Sparks
            ? 1.32f
            : selectedTrailStyle == MetaProgressionStorage.TrailStyle.Pixels ? 0.72f : 1f;
        float rate = Mathf.Lerp(8f, Mathf.Max(10f, trailEmissionAtMaxSpeed), normalized) * pulse * styleMultiplier;

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
        compactTimer = 0f;
        shieldTimer = 0f;
        parryTimer = 0f;
        parryCooldownTimer = 0f;
        ghostDashTimer = 0f;
        ghostDashCooldownTimer = 0f;

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

    private void SpawnGhostDashStartFx(Vector2 direction)
    {
        Vector2 dashDir = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
        int count = Mathf.Max(2, ghostDashAfterimageCount);
        for (int i = 0; i < count; i++)
        {
            float delay = i / (float)count;
            Vector3 offset = -(Vector3)(dashDir * Mathf.Lerp(0.16f, 0.82f, delay));
            GameObject afterimage = new GameObject($"GhostDashAfterimage_{i}");
            afterimage.transform.position = transform.position + offset;
            afterimage.transform.rotation = transform.rotation;
            afterimage.transform.localScale = baseBodyScale * Mathf.Lerp(0.98f, 0.70f, delay);
            SpriteRenderer sr = afterimage.AddComponent<SpriteRenderer>();
            sr.sprite = bodyRenderer != null ? bodyRenderer.sprite : SquareSpriteProvider.Get();
            sr.drawMode = bodyRenderer != null ? bodyRenderer.drawMode : SpriteDrawMode.Simple;
            sr.color = new Color(ghostDashColor.r, ghostDashColor.g, ghostDashColor.b, Mathf.Lerp(0.46f, 0.16f, delay));
            sr.sortingOrder = bodyRenderer != null ? bodyRenderer.sortingOrder - 1 : 7;
            afterimage.AddComponent<PlayerGhostDashAfterimageFx>().Configure(sr, dashDir, Mathf.Lerp(0.18f, 0.28f, delay), ghostDashColor);
            Destroy(afterimage, 0.36f);
        }

        GameObject streak = new GameObject("GhostDashStreakFx");
        streak.transform.position = transform.position;
        streak.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dashDir.y, dashDir.x) * Mathf.Rad2Deg);
        SpriteRenderer streakRenderer = streak.AddComponent<SpriteRenderer>();
        streakRenderer.sprite = SquareSpriteProvider.Get();
        streakRenderer.drawMode = SpriteDrawMode.Sliced;
        streakRenderer.size = new Vector2(1.15f, 0.12f);
        streakRenderer.color = new Color(ghostDashColor.r, ghostDashColor.g, ghostDashColor.b, 0.64f);
        streakRenderer.sortingOrder = 14;
        streak.AddComponent<PlayerGhostDashStreakFx>().Configure(streakRenderer, dashDir, 0.24f, ghostDashColor);
        Destroy(streak, 0.32f);
    }

    private void SpawnGhostDashContactFx()
    {
        GameObject ring = new GameObject("GhostDashContactFx");
        ring.transform.position = transform.position;
        SpriteRenderer sr = ring.AddComponent<SpriteRenderer>();
        sr.sprite = CircleSpriteProvider.Get();
        sr.color = new Color(ghostDashColor.r, ghostDashColor.g, ghostDashColor.b, 0.52f);
        sr.sortingOrder = 16;
        ring.transform.localScale = Vector3.one * 0.18f;
        ring.AddComponent<PlayerParryBurstFx>().Configure(sr, 0.82f, 0.14f, ghostDashColor);
        Destroy(ring, 0.22f);
    }

    private void SpawnCompactModeFx()
    {
        Color fxColor = compactColor;

        GameObject ring = new GameObject("CompactModeRingFx");
        ring.transform.position = transform.position;
        SpriteRenderer ringRenderer = ring.AddComponent<SpriteRenderer>();
        ringRenderer.sprite = CircleSpriteProvider.Get();
        ringRenderer.sortingOrder = 14;
        ringRenderer.color = new Color(fxColor.r, fxColor.g, fxColor.b, 0.82f);
        ring.transform.localScale = Vector3.one * 1.35f;
        ring.AddComponent<PlayerParryBurstFx>().Configure(ringRenderer, 0.28f, 0.18f, fxColor);
        Destroy(ring, 0.28f);

        const int rayCount = 8;
        for (int i = 0; i < rayCount; i++)
        {
            float angle = ((Mathf.PI * 2f) * i) / rayCount;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            GameObject ray = new GameObject($"CompactModeInRay_{i}");
            ray.transform.position = transform.position + (Vector3)(dir * 0.95f);
            SpriteRenderer sr = ray.AddComponent<SpriteRenderer>();
            sr.sprite = SquareSpriteProvider.Get();
            sr.sortingOrder = 14;
            sr.color = new Color(fxColor.r, fxColor.g, fxColor.b, 0.9f);
            ray.transform.localScale = new Vector3(0.26f, 0.07f, 1f);
            ray.AddComponent<PlayerCompactInRayFx>().Configure(sr, -dir, 0.72f, 0.22f);
            Destroy(ray, 0.28f);
        }
    }
}

public class PlayerGhostDashAfterimageFx : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Vector2 driftDirection = Vector2.right;
    private Color tint = Color.white;
    private float life = 0.22f;
    private float age;
    private Vector3 origin;

    public void Configure(SpriteRenderer rendererRef, Vector2 direction, float duration, Color color)
    {
        spriteRenderer = rendererRef;
        driftDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
        life = Mathf.Max(0.06f, duration);
        tint = color;
        origin = transform.position;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / life);
        transform.position = origin - (Vector3)(driftDirection * Mathf.Lerp(0f, 0.22f, t));
        transform.localScale *= 1f - Time.deltaTime * 1.6f;
        if (spriteRenderer != null)
        {
            spriteRenderer.color = new Color(tint.r, tint.g, tint.b, Mathf.Lerp(0.42f, 0f, t));
        }
    }
}

public class PlayerCompactInRayFx : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Vector2 direction = Vector2.left;
    private float distance = 0.6f;
    private float duration = 0.18f;
    private float age;
    private Vector3 origin;

    public void Configure(SpriteRenderer rendererRef, Vector2 travelDirection, float travelDistance, float life)
    {
        spriteRenderer = rendererRef;
        direction = travelDirection.sqrMagnitude > 0.001f ? travelDirection.normalized : Vector2.left;
        distance = Mathf.Max(0.1f, travelDistance);
        duration = Mathf.Max(0.05f, life);
        origin = transform.position;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / duration);
        transform.position = origin + (Vector3)(direction * distance * Mathf.SmoothStep(0f, 1f, t));
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        transform.localScale = new Vector3(Mathf.Lerp(0.28f, 0.04f, t), Mathf.Lerp(0.08f, 0.025f, t), 1f);
        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = Mathf.Lerp(0.9f, 0f, t);
            spriteRenderer.color = c;
        }
    }
}

public class PlayerGhostDashStreakFx : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Vector2 direction = Vector2.right;
    private Color tint = Color.white;
    private float life = 0.22f;
    private float age;
    private Vector3 origin;

    public void Configure(SpriteRenderer rendererRef, Vector2 dashDirection, float duration, Color color)
    {
        spriteRenderer = rendererRef;
        direction = dashDirection.sqrMagnitude > 0.001f ? dashDirection.normalized : Vector2.right;
        life = Mathf.Max(0.06f, duration);
        tint = color;
        origin = transform.position;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / life);
        transform.position = origin - (Vector3)(direction * Mathf.Lerp(0f, 0.55f, t));
        transform.localScale = new Vector3(Mathf.Lerp(1.15f, 0.12f, t), Mathf.Lerp(0.14f, 0.035f, t), 1f);
        if (spriteRenderer != null)
        {
            spriteRenderer.color = new Color(tint.r, tint.g, tint.b, Mathf.Lerp(0.62f, 0f, t));
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

using UnityEngine;

// Rig visual procedural del jugador. El componente vive en el root fisico, pero
// anima un hijo independiente para que squash, stretch y feedback no alteren colisiones.
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class PlayerVisualAnimator : MonoBehaviour
{
    private const int FinCount = 4;
    private const int NodeCount = 6;

    private PlayerController owner;
    private SpriteRenderer sourceRenderer;
    private Transform visualRoot;
    private Transform ornamentRoot;
    private SpriteRenderer bodyRenderer;
    private SpriteRenderer shellRenderer;
    private SpriteRenderer shadowRenderer;
    private SpriteRenderer coreRenderer;
    private SpriteRenderer headingRenderer;
    private readonly SpriteRenderer[] fins = new SpriteRenderer[FinCount];
    private readonly SpriteRenderer[] nodes = new SpriteRenderer[NodeCount];

    private Color basePresentationColor = Color.white;
    private Color specialPulseColor = Color.white;
    private Vector2 movementDirection = Vector2.right;
    private Vector2 previousVelocity;
    private Vector2 movementScale = Vector2.one;
    private float moveSpeedReference = 11f;
    private float movementStretch = 0.16f;
    private float movementSquash = 0.10f;
    private float accelerationDeformation = 0.07f;
    private float movementResponse = 15f;
    private float animationClock;
    private float motionIntensity = 1f;
    private float settingsRefreshTimer;
    private float dashPulse;
    private float parryPulse;
    private float parrySuccessPulse;
    private float firewallReadyPulse;
    private float firewallBurstPulse;
    private float shieldPulse;
    private float speedPulse;
    private float slowPulse;
    private float compactPulse;
    private float phasePulse;
    private float impactPulse;
    private float specialPulse;
    private bool terminalSequence;
    private bool initialized;

    public SpriteRenderer BodyRenderer => bodyRenderer;
    public Transform VisualRoot => visualRoot;

    public SpriteRenderer Initialize(PlayerController ownerReference, SpriteRenderer source)
    {
        owner = ownerReference;
        if (initialized && bodyRenderer != null)
        {
            return bodyRenderer;
        }

        sourceRenderer = source;
        if (sourceRenderer == null)
        {
            return null;
        }

        motionIntensity = UserSettings.GetMenuMotion();
        basePresentationColor = sourceRenderer.color;
        BuildVisualRig();
        initialized = bodyRenderer != null;
        return bodyRenderer != null ? bodyRenderer : sourceRenderer;
    }

    public void ConfigureMovement(float speedReference, float stretch, float squash, float acceleration, float response)
    {
        moveSpeedReference = Mathf.Max(0.1f, speedReference);
        movementStretch = Mathf.Clamp(stretch, 0f, 0.35f);
        movementSquash = Mathf.Clamp(squash, 0f, 0.30f);
        accelerationDeformation = Mathf.Clamp(acceleration, 0f, 0.20f);
        movementResponse = Mathf.Max(1f, response);
    }

    public void SetBasePresentation(Color color)
    {
        basePresentationColor = color;
    }

    public void PlayDash(Vector2 direction, bool phaseDash)
    {
        if (direction.sqrMagnitude > 0.001f)
        {
            movementDirection = direction.normalized;
        }

        dashPulse = 1f;
        if (phaseDash)
        {
            phasePulse = 1f;
        }
    }

    public void PlayParryStart()
    {
        parryPulse = 1f;
    }

    public void PlayParrySuccess(Vector2 direction)
    {
        if (direction.sqrMagnitude > 0.001f)
        {
            movementDirection = direction.normalized;
        }

        parrySuccessPulse = 1f;
        impactPulse = Mathf.Max(impactPulse, 0.72f);
    }

    public void PlayFirewallReady()
    {
        firewallReadyPulse = 1f;
    }

    public void PlayFirewallBurst()
    {
        firewallBurstPulse = 1f;
    }

    public void PlayShieldGain()
    {
        shieldPulse = 1f;
    }

    public void PlayShieldBreak()
    {
        shieldPulse = 1f;
        impactPulse = 1f;
    }

    public void PlaySpeedBoost()
    {
        speedPulse = 1f;
    }

    public void PlaySlow()
    {
        slowPulse = 1f;
    }

    public void PlayCompact()
    {
        compactPulse = 1f;
    }

    public void PlayPhaseDashReady()
    {
        phasePulse = 1f;
    }

    public void PlayImpact(float strength = 1f)
    {
        impactPulse = Mathf.Max(impactPulse, Mathf.Clamp(strength, 0.15f, 1.25f));
    }

    public void PlaySpecialPulse(Color color, float strength = 1f)
    {
        specialPulseColor = color;
        specialPulse = Mathf.Max(specialPulse, Mathf.Clamp(strength, 0.25f, 1.5f));
    }

    public void BeginTerminalSequence()
    {
        terminalSequence = true;
        ResetRootTransform();
        SetOrnamentsEnabled(false);
    }

    public void ResetVisuals()
    {
        terminalSequence = false;
        previousVelocity = Vector2.zero;
        movementScale = Vector2.one;
        dashPulse = 0f;
        parryPulse = 0f;
        parrySuccessPulse = 0f;
        firewallReadyPulse = 0f;
        firewallBurstPulse = 0f;
        shieldPulse = 0f;
        speedPulse = 0f;
        slowPulse = 0f;
        compactPulse = 0f;
        phasePulse = 0f;
        impactPulse = 0f;
        specialPulse = 0f;
        ResetRootTransform();
    }

    private void BuildVisualRig()
    {
        GameObject rootObject = new GameObject("PlayerVisualRoot");
        rootObject.transform.SetParent(transform, false);
        visualRoot = rootObject.transform;

        GameObject shadowObject = new GameObject("Shadow");
        shadowObject.transform.SetParent(visualRoot, false);
        shadowRenderer = shadowObject.AddComponent<SpriteRenderer>();
        ConfigureRenderer(shadowRenderer, CircleSpriteProvider.Get(), sourceRenderer.sortingOrder - 2);

        GameObject bodyObject = new GameObject("Body");
        bodyObject.transform.SetParent(visualRoot, false);
        bodyRenderer = bodyObject.AddComponent<SpriteRenderer>();
        CopyRenderer(sourceRenderer, bodyRenderer);
        PlayerAvatarSpriteProvider.Warmup();
        bodyRenderer.sprite = PlayerAvatarSpriteProvider.Get();
        bodyRenderer.drawMode = SpriteDrawMode.Simple;

        GameObject shellObject = new GameObject("ReactiveShell");
        shellObject.transform.SetParent(visualRoot, false);
        shellRenderer = shellObject.AddComponent<SpriteRenderer>();
        CopyRenderer(sourceRenderer, shellRenderer);
        shellRenderer.sprite = PlayerAvatarSpriteProvider.Get();
        shellRenderer.drawMode = SpriteDrawMode.Simple;
        shellRenderer.sortingOrder = sourceRenderer.sortingOrder - 1;
        shellRenderer.transform.localScale = Vector3.one * 1.12f;

        GameObject ornaments = new GameObject("PersistentPlayerFx");
        ornaments.transform.SetParent(visualRoot, false);
        ornamentRoot = ornaments.transform;

        coreRenderer = CreateOrnament("Core", SquareSpriteProvider.Get(), sourceRenderer.sortingOrder + 2);
        headingRenderer = CreateOrnament("HeadingChevron", TriangleSpriteProvider.Get(), sourceRenderer.sortingOrder + 4);
        for (int i = 0; i < fins.Length; i++)
        {
            fins[i] = CreateOrnament($"Fin_{i}", SquareSpriteProvider.Get(), sourceRenderer.sortingOrder - 1);
        }
        for (int i = 0; i < nodes.Length; i++)
        {
            nodes[i] = CreateOrnament($"Node_{i}", SquareSpriteProvider.Get(), sourceRenderer.sortingOrder + 3);
        }

        sourceRenderer.enabled = false;
    }

    private static void CopyRenderer(SpriteRenderer source, SpriteRenderer destination)
    {
        destination.sprite = source.sprite;
        destination.color = source.color;
        destination.sharedMaterial = source.sharedMaterial;
        destination.sortingLayerID = source.sortingLayerID;
        destination.sortingOrder = source.sortingOrder;
        destination.flipX = source.flipX;
        destination.flipY = source.flipY;
        destination.drawMode = source.drawMode;
        destination.size = source.size;
        destination.maskInteraction = source.maskInteraction;
        destination.spriteSortPoint = source.spriteSortPoint;
        destination.enabled = source.enabled;
    }

    private SpriteRenderer CreateOrnament(string objectName, Sprite sprite, int sortingOrder)
    {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(ornamentRoot, false);
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        ConfigureRenderer(renderer, sprite, sortingOrder);
        return renderer;
    }

    private void ConfigureRenderer(SpriteRenderer renderer, Sprite sprite, int sortingOrder)
    {
        renderer.sprite = sprite;
        renderer.sharedMaterial = sourceRenderer.sharedMaterial;
        renderer.sortingLayerID = sourceRenderer.sortingLayerID;
        renderer.sortingOrder = sortingOrder;
        renderer.color = Color.clear;
    }

    private void LateUpdate()
    {
        if (!initialized || owner == null || bodyRenderer == null || visualRoot == null)
        {
            return;
        }

        if (terminalSequence || owner.IsInDeathSequence || owner.IsInBreachConsumption)
        {
            SetOrnamentsEnabled(false);
            return;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f)
        {
            return;
        }

        settingsRefreshTimer -= dt;
        if (settingsRefreshTimer <= 0f)
        {
            settingsRefreshTimer = 0.5f;
            motionIntensity = UserSettings.GetMenuMotion();
        }

        float motion01 = Mathf.InverseLerp(UserSettings.MinMenuMotion, UserSettings.MaxMenuMotion, motionIntensity);
        float amplitude = Mathf.Lerp(0.58f, 1.18f, motion01);
        float clockRate = Mathf.Lerp(0.82f, 1.22f, motion01);
        if (owner.HasSpeedBoost)
        {
            clockRate *= 1.18f;
        }
        if (owner.HasMovementSlow)
        {
            clockRate *= 0.72f;
        }
        animationClock += dt * clockRate;

        TickPulses(dt);
        UpdateMovement(dt);
        UpdateBody(amplitude);
        UpdateOrnaments(amplitude);
        SetOrnamentsEnabled(bodyRenderer.enabled && bodyRenderer.color.a > 0.001f);
    }

    private void TickPulses(float dt)
    {
        dashPulse = Mathf.MoveTowards(dashPulse, 0f, dt * 4.6f);
        parryPulse = Mathf.MoveTowards(parryPulse, 0f, dt * 6.2f);
        parrySuccessPulse = Mathf.MoveTowards(parrySuccessPulse, 0f, dt * 5.2f);
        firewallReadyPulse = Mathf.MoveTowards(firewallReadyPulse, 0f, dt * 2.8f);
        firewallBurstPulse = Mathf.MoveTowards(firewallBurstPulse, 0f, dt * 2.9f);
        shieldPulse = Mathf.MoveTowards(shieldPulse, 0f, dt * 3.8f);
        speedPulse = Mathf.MoveTowards(speedPulse, 0f, dt * 3.6f);
        slowPulse = Mathf.MoveTowards(slowPulse, 0f, dt * 3.2f);
        compactPulse = Mathf.MoveTowards(compactPulse, 0f, dt * 3.8f);
        phasePulse = Mathf.MoveTowards(phasePulse, 0f, dt * 3.2f);
        impactPulse = Mathf.MoveTowards(impactPulse, 0f, dt * 5.8f);
        specialPulse = Mathf.MoveTowards(specialPulse, 0f, dt * 3.2f);
    }

    private void UpdateMovement(float dt)
    {
        Vector2 velocity = owner.CurrentVelocity;
        float speed01 = Mathf.Clamp01(velocity.magnitude / Mathf.Max(0.1f, moveSpeedReference * 1.1f));
        if (velocity.sqrMagnitude > 0.001f)
        {
            movementDirection = velocity.normalized;
        }
        else if (owner.LastMoveDirection.sqrMagnitude > 0.001f)
        {
            movementDirection = owner.LastMoveDirection.normalized;
        }

        Vector2 absoluteDirection = new Vector2(Mathf.Abs(movementDirection.x), Mathf.Abs(movementDirection.y));
        float velocityDelta = (velocity - previousVelocity).magnitude;
        float acceleration01 = Mathf.Clamp01(velocityDelta / Mathf.Max(0.1f, moveSpeedReference * 0.8f));
        float targetX = 1f + speed01 * (absoluteDirection.x * movementStretch - absoluteDirection.y * movementSquash);
        float targetY = 1f + speed01 * (absoluteDirection.y * movementStretch - absoluteDirection.x * movementSquash);
        targetX += acceleration01 * accelerationDeformation * absoluteDirection.x;
        targetY += acceleration01 * accelerationDeformation * absoluteDirection.y;

        float response = 1f - Mathf.Exp(-movementResponse * dt);
        movementScale = Vector2.Lerp(movementScale, new Vector2(targetX, targetY), response);
        previousVelocity = velocity;
    }

    private void UpdateBody(float amplitude)
    {
        Vector2 velocity = owner.CurrentVelocity;
        float speed01 = Mathf.Clamp01(velocity.magnitude / Mathf.Max(0.1f, moveSpeedReference * 1.1f));
        float idle = Mathf.Sin(animationClock * 3.1f);
        float fast = Mathf.Sin(animationClock * 10.5f);
        float dash = PulseEnvelope(dashPulse);
        float parry = Mathf.Max(PulseEnvelope(parryPulse), owner.IsParryActive ? 0.34f : 0f);
        float parrySuccess = PulseEnvelope(parrySuccessPulse);
        float firewallBurst = PulseEnvelope(firewallBurstPulse);
        float impact = PulseEnvelope(impactPulse);
        float special = PulseEnvelope(specialPulse);
        Vector2 absoluteDirection = new Vector2(Mathf.Abs(movementDirection.x), Mathf.Abs(movementDirection.y));

        Sprite currentFrame = PlayerAvatarSpriteProvider.GetFrame(animationClock * Mathf.PI * 1.15f + speed01 * 0.7f);
        bodyRenderer.sprite = currentFrame;
        if (shellRenderer != null)
        {
            shellRenderer.sprite = currentFrame;
        }

        float idleBreath = idle * 0.018f * amplitude;
        float compactBreath = owner.HasCompactMode ? Mathf.Sin(animationClock * 7.5f) * 0.030f * amplitude : 0f;
        float x = movementScale.x * (1f + idleBreath + compactBreath);
        float y = movementScale.y * (1f - idleBreath * 0.72f + compactBreath);
        x *= 1f + dash * absoluteDirection.x * 0.24f * amplitude - dash * absoluteDirection.y * 0.10f * amplitude;
        y *= 1f + dash * absoluteDirection.y * 0.24f * amplitude - dash * absoluteDirection.x * 0.10f * amplitude;
        x *= 1f + (parry + firewallBurst) * 0.09f * amplitude + impact * 0.11f * amplitude;
        y *= 1f + (parry + firewallBurst) * 0.09f * amplitude - impact * 0.08f * amplitude;
        x *= 1f + parrySuccess * 0.08f * amplitude;
        y *= 1f + parrySuccess * 0.08f * amplitude;

        visualRoot.localScale = new Vector3(Mathf.Clamp(x, 0.70f, 1.42f), Mathf.Clamp(y, 0.70f, 1.42f), 1f);

        Vector2 offset = new Vector2(0f, idle * 0.020f * amplitude);
        offset -= movementDirection * speed01 * 0.022f * amplitude;
        offset -= movementDirection * dash * 0.075f * amplitude;
        offset += new Vector2(-movementDirection.y, movementDirection.x) * parrySuccess * 0.035f * amplitude;
        if (owner.HasMovementSlow)
        {
            offset += new Vector2(fast, Mathf.Sin(animationClock * 8.3f)) * 0.012f * amplitude;
        }
        visualRoot.localPosition = new Vector3(offset.x, offset.y, 0f);

        float rotation = movementDirection.x * movementDirection.y * -3.2f * speed01 * amplitude;
        rotation += parry * fast * 4.5f * amplitude;
        rotation += special * fast * 3f * amplitude;
        if (owner.HasMovementSlow)
        {
            rotation += Mathf.Round(fast * 2f) * 0.75f * amplitude;
        }
        visualRoot.localRotation = Quaternion.Euler(0f, 0f, rotation);

        Color color = basePresentationColor;
        if (owner.HasSpeedBoost)
        {
            color = Color.Lerp(color, GlitchUiPalette.Information, 0.17f);
        }
        if (owner.HasShield)
        {
            color = Color.Lerp(color, GlitchUiPalette.Special, 0.12f);
        }
        if (owner.HasMovementSlow)
        {
            color = Color.Lerp(color, GlitchUiPalette.Danger, 0.22f + Mathf.Abs(fast) * 0.07f);
        }
        if (owner.HasPhaseDashPowerup)
        {
            color = Color.Lerp(color, GlitchUiPalette.Special, 0.14f);
        }
        if (owner.IsFirewallBurstReady)
        {
            color = Color.Lerp(color, GlitchUiPalette.Alert, 0.08f);
        }
        if (owner.IsGhostDashing)
        {
            Color dashColor = owner.IsIntangibleDashActive ? GlitchUiPalette.Special : GlitchUiPalette.Information;
            color = Color.Lerp(color, dashColor, 0.38f + dash * 0.28f);
            color.a = basePresentationColor.a * (owner.IsIntangibleDashActive ? 0.56f : 0.78f);
        }
        else
        {
            color.a = basePresentationColor.a;
        }

        float whiteFlash = Mathf.Clamp01(firewallBurst * 0.78f + parrySuccess * 0.48f + impact * 0.42f);
        color = Color.Lerp(color, Color.white, whiteFlash);
        color = Color.Lerp(color, specialPulseColor, special * 0.62f);
        bodyRenderer.color = color;

        if (shellRenderer != null)
        {
            Color accent = ResolveAccent();
            float readiness = owner.IsFirewallBurstReady ? 1f : 0f;
            float shellPulse = 0.5f + 0.5f * Mathf.Sin(animationClock * (readiness > 0f ? 9.5f : 4.8f));
            float shellScale = 1.105f + shellPulse * 0.018f + dash * 0.055f + parry * 0.035f;
            shellRenderer.transform.localScale = Vector3.one * shellScale;
            Color shellColor = Color.Lerp(GlitchUiPalette.Surface, accent, 0.72f);
            shellColor = Color.Lerp(shellColor, Color.white, parrySuccess * 0.52f + firewallBurst * 0.68f);
            shellColor.a = Mathf.Clamp01(color.a * Mathf.Lerp(0.64f, 0.94f, Mathf.Clamp01(shellPulse + readiness * 0.25f)));
            shellRenderer.color = shellColor;
        }
    }

    private void UpdateOrnaments(float amplitude)
    {
        float idle = Mathf.Sin(animationClock * 3.1f);
        float fast = Mathf.Sin(animationClock * 10.5f);
        float dash = PulseEnvelope(dashPulse);
        float parry = Mathf.Max(PulseEnvelope(parryPulse), owner.IsParryActive ? 0.34f : 0f);
        float parrySuccess = PulseEnvelope(parrySuccessPulse);
        float readyArrival = PulseEnvelope(firewallReadyPulse);
        float firewallBurst = PulseEnvelope(firewallBurstPulse);
        float shieldArrival = PulseEnvelope(shieldPulse);
        float speedArrival = PulseEnvelope(speedPulse);
        float slowArrival = PulseEnvelope(slowPulse);
        float compactArrival = PulseEnvelope(compactPulse);
        float phaseArrival = PulseEnvelope(phasePulse);
        float special = PulseEnvelope(specialPulse);
        float speed01 = Mathf.Clamp01(owner.CurrentVelocity.magnitude / Mathf.Max(0.1f, moveSpeedReference * 1.1f));

        UpdateShadow(speed01, dash, amplitude);

        Color accent = ResolveAccent();
        float corePulse = 0.5f + 0.5f * Mathf.Sin(animationClock * (owner.IsFirewallBurstReady ? 8.5f : 5.2f));
        float coreScale = 0.20f + corePulse * 0.055f;
        coreScale += readyArrival * 0.12f + firewallBurst * 0.22f + parrySuccess * 0.10f + special * 0.10f;
        if (owner.HasCompactMode)
        {
            coreScale *= 0.82f;
        }
        coreRenderer.transform.localPosition = Vector3.zero;
        coreRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, animationClock * (owner.IsFirewallBurstReady ? 170f : 82f));
        coreRenderer.transform.localScale = Vector3.one * Mathf.Max(0.08f, coreScale * amplitude);
        Color coreColor = Color.Lerp(accent, Color.white, 0.48f + firewallBurst * 0.44f);
        coreColor.a = Mathf.Clamp01(0.52f + corePulse * 0.20f + readyArrival * 0.20f);
        coreRenderer.color = coreColor;

        UpdateHeadingIndicator(accent, speed01, dash, parry, firewallBurst, amplitude);
        UpdateFins(accent, speed01, dash, parry, firewallBurst, speedArrival, slowArrival, compactArrival, amplitude);
        UpdateNodes(accent, speed01, dash, parry, parrySuccess, readyArrival, firewallBurst, shieldArrival, phaseArrival, special, fast, amplitude);
    }

    private void UpdateHeadingIndicator(Color accent, float speed01, float dash, float parry, float firewallBurst, float amplitude)
    {
        if (headingRenderer == null)
        {
            return;
        }

        float angle = Mathf.Atan2(movementDirection.y, movementDirection.x) * Mathf.Rad2Deg - 90f;
        float radius = owner.HasCompactMode ? 0.36f : 0.47f;
        radius += dash * 0.11f + firewallBurst * 0.08f;
        headingRenderer.transform.localPosition = movementDirection * radius;
        headingRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        float pulse = 0.5f + 0.5f * Mathf.Sin(animationClock * (owner.IsFirewallBurstReady ? 10f : 5.4f));
        float scale = 0.19f + speed01 * 0.045f + dash * 0.08f + parry * 0.035f;
        headingRenderer.transform.localScale = Vector3.one * scale * Mathf.Lerp(0.94f, 1.08f, pulse) * amplitude;
        Color color = owner.IsFirewallBurstReady ? GlitchUiPalette.Alert : Color.Lerp(accent, Color.white, 0.42f);
        color.a = Mathf.Clamp01(0.62f + speed01 * 0.22f + dash * 0.16f);
        headingRenderer.color = color;
        headingRenderer.enabled = true;
    }

    private void UpdateShadow(float speed01, float dash, float amplitude)
    {
        shadowRenderer.transform.localPosition = new Vector3(0.065f, -0.095f, 0f);
        shadowRenderer.transform.localRotation = Quaternion.identity;
        shadowRenderer.transform.localScale = new Vector3(
            1.08f + speed01 * 0.15f + dash * 0.12f * amplitude,
            0.78f - speed01 * 0.08f,
            1f);
        shadowRenderer.color = new Color(GlitchUiPalette.Surface.r, GlitchUiPalette.Surface.g, GlitchUiPalette.Surface.b, 0.28f);
    }

    private void UpdateFins(
        Color accent,
        float speed01,
        float dash,
        float parry,
        float firewallBurst,
        float speedArrival,
        float slowArrival,
        float compactArrival,
        float amplitude)
    {
        Vector2 perpendicular = new Vector2(-movementDirection.y, movementDirection.x);
        bool trailing = speed01 > 0.12f || owner.IsGhostDashing || owner.HasSpeedBoost;
        float movementAngle = Mathf.Atan2(movementDirection.y, movementDirection.x) * Mathf.Rad2Deg;
        for (int i = 0; i < fins.Length; i++)
        {
            SpriteRenderer fin = fins[i];
            Vector2 position;
            Vector2 scale;
            float rotation;
            if (trailing)
            {
                float side = i - (fins.Length - 1) * 0.5f;
                position = -movementDirection * (0.48f + Mathf.Abs(side) * 0.075f + dash * 0.14f)
                           + perpendicular * side * 0.18f;
                scale = new Vector2(0.18f + speed01 * 0.14f + dash * 0.18f, 0.050f);
                rotation = movementAngle;
            }
            else
            {
                float angle = i / (float)fins.Length * Mathf.PI * 2f;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float dockingPulse = Mathf.Sin(animationClock * 3.6f + i * 1.57f) * 0.018f;
                float radius = owner.HasCompactMode ? 0.42f : 0.50f;
                radius += dockingPulse;
                radius += parry * 0.15f + firewallBurst * 0.25f;
                position = direction * radius;
                scale = new Vector2(0.14f + parry * 0.08f, 0.055f);
                rotation = angle * Mathf.Rad2Deg;
            }

            if (owner.HasMovementSlow)
            {
                position += new Vector2(Mathf.Sin(animationClock * 12f + i), Mathf.Sin(animationClock * 9f + i * 2f)) * 0.025f;
            }

            float arrival = Mathf.Max(speedArrival, Mathf.Max(slowArrival, compactArrival));
            fin.transform.localPosition = new Vector3(position.x, position.y, 0f) * amplitude;
            fin.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            fin.transform.localScale = new Vector3(scale.x * (1f + arrival * 0.34f), scale.y, 1f);
            Color finColor = i % 2 == 0 ? accent : Color.Lerp(accent, Color.white, 0.30f);
            finColor.a = Mathf.Clamp01(0.38f + speed01 * 0.28f + parry * 0.22f + firewallBurst * 0.28f);
            fin.color = finColor;
        }
    }

    private void UpdateNodes(
        Color accent,
        float speed01,
        float dash,
        float parry,
        float parrySuccess,
        float readyArrival,
        float firewallBurst,
        float shieldArrival,
        float phaseArrival,
        float special,
        float fast,
        float amplitude)
    {
        int visibleNodes = 2;
        if (owner.HasSpeedBoost || owner.HasCompactMode || owner.HasPhaseDashPowerup)
        {
            visibleNodes = 4;
        }
        if (owner.HasShield || owner.IsParryActive || owner.IsFirewallBurstReady || parry > 0f || firewallBurst > 0f)
        {
            visibleNodes = NodeCount;
        }

        float spin = owner.IsFirewallBurstReady ? 2.6f : owner.HasShield ? -1.45f : 0.72f;
        if (owner.HasMovementSlow)
        {
            spin *= 0.35f;
        }
        float radius = owner.HasCompactMode ? 0.53f : 0.69f;
        radius += parry * 0.19f + readyArrival * 0.16f + firewallBurst * 0.46f + shieldArrival * 0.12f + special * 0.12f;
        Vector2 perpendicular = new Vector2(-movementDirection.y, movementDirection.x);

        for (int i = 0; i < nodes.Length; i++)
        {
            SpriteRenderer node = nodes[i];
            bool visible = i < visibleNodes;
            node.enabled = visible;
            if (!visible)
            {
                continue;
            }

            float fraction = i / (float)Mathf.Max(1, visibleNodes);
            float angle = fraction * Mathf.PI * 2f + animationClock * spin;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 position = direction * radius;
            Vector2 scale = new Vector2(0.105f, 0.055f);

            if (owner.IsGhostDashing)
            {
                float lane = i - (visibleNodes - 1) * 0.5f;
                position = -movementDirection * (0.38f + i * 0.12f) + perpendicular * lane * 0.08f;
                scale = new Vector2(0.13f + dash * 0.09f, 0.040f);
            }
            else if (owner.HasMovementSlow)
            {
                position += new Vector2(Mathf.Sin(i * 7.31f + animationClock * 9f), Mathf.Sin(i * 4.17f - animationClock * 7f)) * 0.055f;
            }

            float eventScale = 1f + Mathf.Max(parrySuccess, Mathf.Max(firewallBurst, Mathf.Max(phaseArrival, special))) * 0.55f;
            node.transform.localPosition = new Vector3(position.x, position.y, 0f) * amplitude;
            node.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + 45f);
            node.transform.localScale = new Vector3(scale.x * eventScale, scale.y * eventScale, 1f);
            Color nodeColor = i % 2 == 0 ? accent : Color.Lerp(accent, GlitchUiPalette.Special, 0.34f);
            nodeColor = Color.Lerp(nodeColor, Color.white, firewallBurst * 0.62f + parrySuccess * 0.34f);
            nodeColor.a = Mathf.Clamp01(0.44f + Mathf.Abs(fast) * 0.20f + speed01 * 0.12f + readyArrival * 0.20f);
            node.color = nodeColor;
        }
    }

    private Color ResolveAccent()
    {
        if (owner.IsFirewallBurstReady)
        {
            return GlitchUiPalette.Alert;
        }
        if (owner.IsParryActive)
        {
            return GlitchUiPalette.Special;
        }
        if (owner.HasShield)
        {
            return GlitchUiPalette.Special;
        }
        if (owner.HasMovementSlow)
        {
            return GlitchUiPalette.Danger;
        }
        if (owner.HasPhaseDashPowerup)
        {
            return Color.Lerp(GlitchUiPalette.Information, GlitchUiPalette.Special, 0.62f);
        }
        if (owner.HasSpeedBoost)
        {
            return GlitchUiPalette.Information;
        }
        if (owner.HasCompactMode)
        {
            return GlitchUiPalette.Success;
        }
        if (specialPulse > 0f)
        {
            return specialPulseColor;
        }

        return Color.Lerp(basePresentationColor, Color.white, 0.38f);
    }

    private void SetOrnamentsEnabled(bool visible)
    {
        if (shadowRenderer != null)
        {
            shadowRenderer.enabled = visible;
        }
        if (shellRenderer != null)
        {
            shellRenderer.enabled = visible;
        }
        if (coreRenderer != null)
        {
            coreRenderer.enabled = visible;
        }
        if (headingRenderer != null)
        {
            headingRenderer.enabled = visible;
        }
        for (int i = 0; i < fins.Length; i++)
        {
            if (fins[i] != null)
            {
                fins[i].enabled = visible;
            }
        }
        if (!visible)
        {
            for (int i = 0; i < nodes.Length; i++)
            {
                if (nodes[i] != null)
                {
                    nodes[i].enabled = false;
                }
            }
        }
    }

    private void ResetRootTransform()
    {
        if (visualRoot == null)
        {
            return;
        }

        visualRoot.localPosition = Vector3.zero;
        visualRoot.localRotation = Quaternion.identity;
        visualRoot.localScale = Vector3.one;
    }

    private static float PulseEnvelope(float remaining)
    {
        if (remaining <= 0f)
        {
            return 0f;
        }

        return Mathf.Sin((1f - Mathf.Clamp01(remaining)) * Mathf.PI);
    }
}

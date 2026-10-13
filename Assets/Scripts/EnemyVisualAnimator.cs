using UnityEngine;

// Capa visual procedural de la anomalia. Mantiene todas las deformaciones fuera del root fisico.
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class EnemyVisualAnimator : MonoBehaviour
{
    private enum NodePattern
    {
        Orbit,
        Split,
        Trail,
        Weave,
        Pincer,
        Grid,
        Desync,
        Tether
    }

    private const int NodeCount = 8;
    private const int MotionEchoCount = 7;

    private EnemyController owner;
    private SpriteRenderer sourceRenderer;
    private Transform visualPivot;
    private Transform ornamentRoot;
    private SpriteRenderer bodyRenderer;
    private SpriteRenderer shadowRenderer;
    private SpriteRenderer haloRenderer;
    private SpriteRenderer coreRenderer;
    private readonly SpriteRenderer[] nodes = new SpriteRenderer[NodeCount];
    private Transform faceRoot;
    private SpriteRenderer leftEyeRenderer;
    private SpriteRenderer rightEyeRenderer;
    private Transform signatureRoot;
    private SpriteRenderer signatureRingRenderer;
    private SpriteRenderer signatureGlyphRenderer;
    private Transform motionEchoRoot;
    private readonly SpriteRenderer[] motionEchoRenderers = new SpriteRenderer[MotionEchoCount];
    private readonly float[] motionEchoTimers = new float[MotionEchoCount];
    private readonly float[] motionEchoDurations = new float[MotionEchoCount];
    private readonly Color[] motionEchoColors = new Color[MotionEchoCount];

    private Color authoredBaseColor = Color.white;
    private Vector3 authoredPulseScale = Vector3.one;
    private Vector2 movementScale = Vector2.one;
    private Vector2 movementDirection = Vector2.right;
    private float speedNormalized;
    private float accelerationNormalized;
    private float animationClock;
    private float motionIntensity = 1f;
    private float transitionPulse;
    private float impactPulse;
    private float actionPulse;
    private float transitionStrength = 1f;
    private float impactStrength = 1f;
    private float actionStrength = 1f;
    private Vector2 smoothedLookDirection = Vector2.right;
    private bool presentationVisible = true;
    private EnemyController.AnomalyState lastState;
    private bool wasPreparingAttack;
    private float signatureTimer;
    private float signatureDuration;
    private float motionEchoCooldown;
    private int motionEchoCursor;
    private bool forceMotionEcho;
    private bool initialized;

    public SpriteRenderer BodyRenderer => bodyRenderer;
    public Transform VisualPivot => visualPivot;

    public SpriteRenderer Initialize(EnemyController ownerReference, SpriteRenderer source)
    {
        owner = ownerReference;
        if (initialized && bodyRenderer != null)
        {
            return bodyRenderer;
        }

        sourceRenderer = source;
        motionIntensity = UserSettings.GetMenuMotion();
        BuildVisualRig();
        initialized = bodyRenderer != null;
        lastState = owner != null ? owner.CurrentState : EnemyController.AnomalyState.BasePursuit;
        transitionPulse = 1f;
        ConfigureStateSignature(lastState);
        return bodyRenderer != null ? bodyRenderer : sourceRenderer;
    }

    public void SetBasePresentation(Color color, Vector3 pulseScale)
    {
        authoredBaseColor = color;
        authoredPulseScale = new Vector3(
            Mathf.Max(0.01f, pulseScale.x),
            Mathf.Max(0.01f, pulseScale.y),
            1f);
    }

    public void SetMovementPresentation(Vector2 scaleFactor, Vector2 direction, float speed01, float acceleration01)
    {
        movementScale = new Vector2(Mathf.Max(0.4f, scaleFactor.x), Mathf.Max(0.4f, scaleFactor.y));
        if (direction.sqrMagnitude > 0.001f)
        {
            movementDirection = direction.normalized;
        }

        speedNormalized = Mathf.Clamp01(speed01);
        accelerationNormalized = Mathf.Clamp01(acceleration01);
    }

    public void PlayStateTransition(EnemyController.AnomalyState previous, EnemyController.AnomalyState next)
    {
        lastState = next;
        transitionPulse = 1f;
        transitionStrength = previous == next ? 0.45f : 1f;
        ConfigureStateSignature(next);
        PlayAction(previous == next ? 0.35f : 0.58f);
    }

    public void PlayImpact(float strength = 1f)
    {
        float clampedStrength = Mathf.Clamp(strength, 0.2f, 1.5f);
        impactStrength = impactPulse > 0f ? Mathf.Max(impactStrength, clampedStrength) : clampedStrength;
        impactPulse = 1f;
    }

    public void PlayAction(float strength = 1f)
    {
        float clampedStrength = Mathf.Clamp(strength, 0.2f, 1.5f);
        actionStrength = actionPulse > 0f ? Mathf.Max(actionStrength, clampedStrength) : clampedStrength;
        actionPulse = 1f;
        if (owner != null && EnemyStatePresentationCatalog.Get(owner.CurrentState).trailStrength > 0.08f)
        {
            forceMotionEcho = true;
            motionEchoCooldown = 0f;
        }
    }

    public void SetPresentationVisible(bool visible)
    {
        presentationVisible = visible;
        if (bodyRenderer != null)
        {
            bodyRenderer.enabled = visible;
        }
        if (!visible)
        {
            ClearMotionEchoes();
        }
        SetOrnamentsEnabled(visible);
    }

    public void ResetVisuals()
    {
        movementScale = Vector2.one;
        speedNormalized = 0f;
        accelerationNormalized = 0f;
        transitionPulse = 0f;
        impactPulse = 0f;
        actionPulse = 0f;
        transitionStrength = 1f;
        impactStrength = 1f;
        actionStrength = 1f;
        signatureTimer = 0f;
        motionEchoCooldown = 0f;
        ClearMotionEchoes();
        if (visualPivot != null)
        {
            visualPivot.localPosition = Vector3.zero;
            visualPivot.localRotation = Quaternion.identity;
            visualPivot.localScale = Vector3.one;
        }
    }

    private void BuildVisualRig()
    {
        if (sourceRenderer == null)
        {
            return;
        }

        GameObject pivotObject = new GameObject("AnomalyVisualPivot");
        pivotObject.transform.SetParent(transform, false);
        visualPivot = pivotObject.transform;

        GameObject bodyObject = new GameObject("Body");
        bodyObject.transform.SetParent(visualPivot, false);
        bodyRenderer = bodyObject.AddComponent<SpriteRenderer>();
        CopyRenderer(sourceRenderer, bodyRenderer);
        // Precalcula los cuadros para que cambiar de contorno no genere texturas
        // durante la persecucion.
        CompactSlimeSpriteProvider.Warmup();
        AnomalyStateGlyphSpriteProvider.Warmup();
        // Una unica silueta compacta evita que la masa se lea como un racimo.
        bodyRenderer.sprite = CompactSlimeSpriteProvider.Get();
        bodyRenderer.drawMode = SpriteDrawMode.Simple;
        bodyRenderer.size = Vector2.one;

        GameObject ornamentObject = new GameObject("PersistentStateFx");
        ornamentObject.transform.SetParent(visualPivot, false);
        ornamentRoot = ornamentObject.transform;

        int bodyOrder = sourceRenderer.sortingOrder;
        shadowRenderer = CreateRenderer("Shadow", CompactSlimeSpriteProvider.Get(), bodyOrder - 4);
        // El halo repite la silueta y funciona como borde; un circulo exterior
        // hacia que todo el personaje volviera a leerse como una esfera.
        haloRenderer = CreateRenderer("StateHalo", CompactSlimeSpriteProvider.Get(), bodyOrder - 3);
        coreRenderer = CreateRenderer("StateCore", SquareSpriteProvider.Get(), bodyOrder + 1);
        for (int i = 0; i < nodes.Length; i++)
        {
            nodes[i] = CreateRenderer($"StateNode_{i}", SquareSpriteProvider.Get(), bodyOrder + 1);
        }

        GameObject faceObject = new GameObject("SlimeFace");
        faceObject.transform.SetParent(visualPivot, false);
        faceRoot = faceObject.transform;
        leftEyeRenderer = CreateRenderer("LeftEye", SlimeEyeSpriteProvider.Get(), bodyOrder + 4, faceRoot);
        rightEyeRenderer = CreateRenderer("RightEye", SlimeEyeSpriteProvider.Get(), bodyOrder + 4, faceRoot);
        rightEyeRenderer.flipX = true;

        GameObject signatureObject = new GameObject("StateSignature");
        signatureObject.transform.SetParent(transform, false);
        signatureRoot = signatureObject.transform;
        signatureRingRenderer = CreateRenderer("SignatureRing", ContainmentRingSpriteProvider.Get(), bodyOrder + 5, signatureRoot);
        signatureGlyphRenderer = CreateRenderer("SignatureGlyph", AnomalyStateGlyphSpriteProvider.Get(EnemyStatePresentationCatalog.Glyph.Pursuit), bodyOrder + 6, signatureRoot);
        signatureRingRenderer.enabled = false;
        signatureGlyphRenderer.enabled = false;

        CreateMotionEchoRig(bodyOrder);

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

    private SpriteRenderer CreateRenderer(string objectName, Sprite sprite, int sortingOrder)
    {
        return CreateRenderer(objectName, sprite, sortingOrder, ornamentRoot);
    }

    private SpriteRenderer CreateRenderer(string objectName, Sprite sprite, int sortingOrder, Transform parent)
    {
        GameObject child = new GameObject(objectName);
        child.transform.SetParent(parent, false);
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = sourceRenderer.sharedMaterial;
        renderer.sortingLayerID = sourceRenderer.sortingLayerID;
        renderer.sortingOrder = sortingOrder;
        renderer.color = Color.clear;
        return renderer;
    }

    private void LateUpdate()
    {
        if (!initialized || owner == null || bodyRenderer == null || visualPivot == null)
        {
            return;
        }

        bool visible = presentationVisible && bodyRenderer.enabled && owner.gameObject.activeInHierarchy;
        SetOrnamentsEnabled(visible);
        if (!visible)
        {
            ClearMotionEchoes();
            return;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f)
        {
            return;
        }

        motionIntensity = UserSettings.GetMenuMotion();
        float intensity = Mathf.Clamp(motionIntensity, UserSettings.MinMenuMotion, UserSettings.MaxMenuMotion);
        animationClock += dt * Mathf.Lerp(0.78f, 1.24f, Mathf.InverseLerp(UserSettings.MinMenuMotion, UserSettings.MaxMenuMotion, intensity));
        transitionPulse = Mathf.MoveTowards(transitionPulse, 0f, dt * 3.8f);
        impactPulse = Mathf.MoveTowards(impactPulse, 0f, dt * 6.8f);
        actionPulse = Mathf.MoveTowards(actionPulse, 0f, dt * 4.8f);
        signatureTimer = Mathf.Max(0f, signatureTimer - dt);
        motionEchoCooldown = Mathf.Max(0f, motionEchoCooldown - dt);

        EnemyController.AnomalyState state = owner.CurrentState;
        if (state != lastState)
        {
            PlayStateTransition(lastState, state);
        }

        UpdatePresentation(state, intensity);
    }

    private void UpdatePresentation(EnemyController.AnomalyState state, float intensity)
    {
        EnemyStatePresentationCatalog.Profile profile = EnemyStatePresentationCatalog.Get(state);
        float stateProgress = owner.CurrentStateNormalized;
        float stateEnvelope = Mathf.Sin(stateProgress * Mathf.PI);
        float anticipation = 1f - Mathf.SmoothStep(0f, 0.22f, stateProgress);
        float execution = Mathf.SmoothStep(0.16f, 0.42f, stateProgress) * (1f - Mathf.SmoothStep(0.72f, 1f, stateProgress));
        float recovery = Mathf.SmoothStep(0.76f, 1f, stateProgress);
        if (profile.HasAttackPreparation)
        {
            // Los ataques se repiten dentro del estado: su aviso real manda sobre el reloj de entrada.
            anticipation = owner.IsPreparingAttack ? 1f : 0f;
            execution = 1f - anticipation;
            recovery = 0f;
        }
        float idle = Mathf.Sin(animationClock * 2.8f);
        float fast = Mathf.Sin(animationClock * profile.rhythm);
        float transitionEnvelope = Mathf.Sin((1f - Mathf.Clamp01(transitionPulse)) * Mathf.PI) * transitionStrength;
        float impactEnvelope = Mathf.Sin((1f - Mathf.Clamp01(impactPulse)) * Mathf.PI) * impactStrength;
        float actionEnvelope = Mathf.Sin((1f - Mathf.Clamp01(actionPulse)) * Mathf.PI) * actionStrength;
        float amplitude = Mathf.Lerp(0.62f, 1.18f, Mathf.InverseLerp(UserSettings.MinMenuMotion, UserSettings.MaxMenuMotion, intensity));

        Vector2 stateScale = Vector2.one;
        Vector2 stateOffset = Vector2.zero;
        float stateRotation = 0f;
        float haloScale = 1.25f;
        float coreScale = 0.30f;
        float nodeRadius = 0.72f;
        float nodeSpin = 0.7f;
        int visibleNodes = 4;
        float bodyAlpha = 1f;
        NodePattern nodePattern = NodePattern.Orbit;
        Color accent = owner.CurrentStateColor;

        switch (state)
        {
            case EnemyController.AnomalyState.BasePursuit:
                stateRotation = idle * 1.3f;
                nodeRadius = 0.64f + idle * 0.025f;
                nodeSpin = 0.42f;
                visibleNodes = 0;
                break;
            case EnemyController.AnomalyState.Split:
                stateScale = new Vector2(
                    1.03f + execution * 0.12f + fast * 0.025f,
                    1.02f - anticipation * 0.11f - execution * 0.06f + recovery * 0.04f - fast * 0.018f);
                nodeRadius = 0.82f;
                nodeSpin = 1.15f;
                visibleNodes = 6;
                nodePattern = NodePattern.Split;
                break;
            case EnemyController.AnomalyState.ExpansionShoot:
                stateScale = Vector2.one * (0.94f + anticipation * -0.05f + execution * 0.17f + recovery * 0.06f + (0.5f + 0.5f * fast) * 0.045f);
                haloScale = 1.42f + stateEnvelope * 0.12f;
                nodeRadius = 0.88f;
                nodeSpin = -1.35f;
                visibleNodes = 8;
                break;
            case EnemyController.AnomalyState.SpeedSurge:
                stateRotation = Mathf.Atan2(movementDirection.y, movementDirection.x) * Mathf.Rad2Deg * 0.035f;
                stateScale = new Vector2(1.04f + execution * 0.11f, 0.98f - anticipation * 0.08f - execution * 0.06f);
                stateOffset = -movementDirection * (0.025f + anticipation * 0.045f) + movementDirection * execution * 0.035f;
                haloScale = 1.38f;
                nodeRadius = 0.76f;
                nodeSpin = 0f;
                visibleNodes = 6;
                nodePattern = NodePattern.Trail;
                break;
            case EnemyController.AnomalyState.WeaveHunter:
                stateRotation = Mathf.Sin(animationClock * 9.5f) * 7.5f;
                stateOffset = new Vector2(-movementDirection.y, movementDirection.x) * Mathf.Sin(animationClock * 9.5f) * 0.045f;
                nodeRadius = 0.74f;
                nodeSpin = 1.8f;
                visibleNodes = 6;
                nodePattern = NodePattern.Weave;
                break;
            case EnemyController.AnomalyState.Destroyer:
                stateScale = new Vector2(
                    1.06f + anticipation * 0.07f + execution * 0.12f + idle * 0.025f,
                    1.01f - anticipation * 0.13f - execution * 0.06f + recovery * 0.05f - idle * 0.014f);
                stateOffset = -movementDirection * anticipation * 0.055f + movementDirection * execution * 0.045f;
                stateRotation = fast * (1.8f + actionEnvelope * 4f);
                haloScale = 1.52f;
                coreScale = 0.38f;
                nodeRadius = 0.78f;
                nodeSpin = -0.35f;
                visibleNodes = 8;
                break;
            case EnemyController.AnomalyState.PhaseBlink:
                stateScale = new Vector2(
                    1.02f + anticipation * 0.12f - execution * 0.06f,
                    0.98f - anticipation * 0.18f + execution * 0.05f + Mathf.Abs(fast) * 0.05f);
                stateOffset = new Vector2(fast, idle) * 0.025f;
                haloScale = 1.32f + Mathf.Abs(fast) * 0.22f;
                nodeRadius = 0.92f;
                nodeSpin = 2.6f;
                visibleNodes = 4;
                bodyAlpha = Mathf.Lerp(0.74f, 1f, Mathf.Abs(fast));
                break;
            case EnemyController.AnomalyState.PincerBarrage:
                stateScale = new Vector2(0.98f - anticipation * 0.08f - fast * 0.025f, 1.02f + anticipation * 0.10f + execution * 0.08f + fast * 0.025f);
                nodeRadius = 0.90f;
                nodeSpin = 0.35f;
                visibleNodes = 8;
                nodePattern = NodePattern.Pincer;
                break;
            case EnemyController.AnomalyState.SignalJam:
            {
                float stepped = Mathf.Floor(animationClock * 18f);
                stateOffset = new Vector2(Mathf.Sin(stepped * 12.9898f), Mathf.Sin(stepped * 7.233f)) * 0.040f;
                stateRotation = Mathf.Sin(stepped * 3.17f) * 3.2f;
                haloScale = 1.48f + fast * 0.08f;
                coreScale = 0.22f + Mathf.Abs(fast) * 0.18f;
                nodeRadius = 0.82f;
                nodeSpin = -2.2f;
                visibleNodes = 8;
                bodyAlpha = 0.88f + Mathf.Abs(fast) * 0.12f;
                nodePattern = NodePattern.Desync;
                break;
            }
            case EnemyController.AnomalyState.OrbitBarrage:
                stateRotation = -animationClock * 18f;
                haloScale = 1.58f;
                nodeRadius = 1.02f;
                nodeSpin = 3.4f;
                visibleNodes = 8;
                break;
            case EnemyController.AnomalyState.ReplayPredator:
                stateOffset = -movementDirection * (0.035f + Mathf.Abs(idle) * 0.025f);
                stateScale = new Vector2(1.04f, 0.97f);
                haloScale = 1.34f + stateEnvelope * 0.16f;
                nodeRadius = 0.82f;
                nodeSpin = -0.75f;
                visibleNodes = 7;
                nodePattern = NodePattern.Trail;
                break;
            case EnemyController.AnomalyState.ChecksumLattice:
                stateRotation = Mathf.Floor(animationClock * 4f) * 2.5f;
                haloScale = 1.48f;
                coreScale = 0.26f + (0.5f + 0.5f * fast) * 0.16f;
                nodeRadius = 0.88f;
                nodeSpin = 0f;
                visibleNodes = 8;
                nodePattern = NodePattern.Grid;
                break;
            case EnemyController.AnomalyState.InputDesync:
                stateOffset = -movementDirection * speedNormalized * (0.045f + Mathf.Max(0f, fast) * 0.055f);
                stateRotation = Mathf.Round(fast * 2f) * 2.5f;
                nodeRadius = 0.72f;
                nodeSpin = 0f;
                visibleNodes = 5;
                nodePattern = NodePattern.Desync;
                break;
            case EnemyController.AnomalyState.MapRecompile:
                stateOffset = new Vector2(Mathf.Round(idle * 2f), Mathf.Round(fast * 2f)) * 0.018f;
                stateRotation = Mathf.Round(idle * 2f) * 2f;
                haloScale = 1.50f;
                nodeRadius = 0.86f;
                nodeSpin = 0f;
                visibleNodes = 8;
                nodePattern = NodePattern.Grid;
                break;
            case EnemyController.AnomalyState.SignalPossession:
                stateScale = Vector2.one * (1f + Mathf.Max(0f, fast) * 0.055f);
                haloScale = 1.38f + stateEnvelope * 0.18f;
                nodeRadius = 0.86f + idle * 0.06f;
                nodeSpin = 1.9f;
                visibleNodes = 7;
                nodePattern = NodePattern.Tether;
                break;
            case EnemyController.AnomalyState.PhaseContract:
                stateScale = Vector2.one * (1.04f - stateEnvelope * 0.045f);
                haloScale = Mathf.Lerp(1.62f, 1.14f, stateProgress);
                coreScale = 0.34f;
                nodeRadius = Mathf.Lerp(1.02f, 0.68f, stateProgress);
                nodeSpin = 1.4f + stateProgress * 2.2f;
                visibleNodes = 8;
                break;
            case EnemyController.AnomalyState.AdaptiveCountermeasure:
                stateRotation = Mathf.Sin(animationClock * 3.5f) * 2.2f;
                haloScale = 1.56f;
                coreScale = 0.40f;
                nodeRadius = 0.94f;
                nodeSpin = 1.35f + stateProgress * 1.6f;
                visibleNodes = 8;
                break;
            case EnemyController.AnomalyState.SignalTether:
                stateScale = new Vector2(1.09f + fast * 0.025f, 0.94f);
                stateOffset = movementDirection * 0.025f;
                stateRotation = fast * 2.2f;
                haloScale = 1.44f;
                nodeRadius = 0.82f;
                nodeSpin = 0.2f;
                visibleNodes = 6;
                nodePattern = NodePattern.Tether;
                break;
            case EnemyController.AnomalyState.BlindspotProtocol:
                stateOffset = -movementDirection * 0.065f;
                stateScale = new Vector2(1.12f, 0.90f);
                stateRotation = fast * 1.8f;
                haloScale = 1.26f;
                nodeRadius = 0.78f;
                nodeSpin = -1.7f;
                visibleNodes = 5;
                bodyAlpha = 0.78f + Mathf.Abs(fast) * 0.18f;
                nodePattern = NodePattern.Trail;
                break;
        }

        float signatureProgress = signatureDuration > 0.001f ? Mathf.Clamp01(signatureTimer / signatureDuration) : 0f;
        float silhouetteEnvelope = Mathf.SmoothStep(0f, 1f, signatureProgress);
        stateScale = Vector2.Scale(stateScale, Vector2.Lerp(Vector2.one, profile.introSilhouette, silhouetteEnvelope * 0.72f));
        stateOffset *= amplitude;
        stateRotation *= amplitude;
        float idleScale = 1f + idle * 0.012f * amplitude;
        Vector2 animatedMovement = Vector2.LerpUnclamped(Vector2.one, movementScale, Mathf.Clamp(intensity * 0.55f, 0.28f, 0.82f));
        float transitionScale = 1f + transitionEnvelope * 0.065f * amplitude;
        float actionScale = 1f + actionEnvelope * 0.042f * amplitude;
        Vector2 hitScale = new Vector2(1f + impactEnvelope * 0.065f, 1f - impactEnvelope * 0.050f);
        float locomotion = Mathf.Sin(animationClock * Mathf.Lerp(3.4f, 10.8f, speedNormalized));
        float flow = locomotion * speedNormalized * 0.052f * amplitude;
        Vector2 absoluteDirection = new Vector2(Mathf.Abs(movementDirection.x), Mathf.Abs(movementDirection.y));
        Vector2 movementSide = new Vector2(-movementDirection.y, movementDirection.x);
        // El paso se expresa como balanceo y transferencia de peso. Asi resulta
        // visible incluso en una silueta pequena sin convertirla en goma elastica.
        Vector2 gaitOffset = movementSide * locomotion * speedNormalized * 0.064f * amplitude;
        gaitOffset += Vector2.up * Mathf.Abs(locomotion) * speedNormalized * 0.038f * amplitude;
        Vector2 idleOffset = Vector2.up * idle * (1f - speedNormalized) * 0.014f * amplitude;
        float gaitRotation = locomotion * speedNormalized * 7.2f * amplitude;

        float finalX = authoredPulseScale.x * animatedMovement.x * stateScale.x * idleScale * transitionScale * actionScale * hitScale.x;
        float finalY = authoredPulseScale.y * animatedMovement.y * stateScale.y * idleScale * transitionScale * actionScale * hitScale.y;
        finalX *= 1f + flow * absoluteDirection.x - flow * absoluteDirection.y * 0.55f;
        finalY *= 1f + flow * absoluteDirection.y - flow * absoluteDirection.x * 0.55f;
        // Conserva volumen y limita la anisotropia: se siente viscoso, pero no elastico.
        float compactVolume = Mathf.Sqrt(Mathf.Max(0.01f, finalX * finalY));
        finalX = Mathf.Lerp(compactVolume, finalX, 0.64f);
        finalY = Mathf.Lerp(compactVolume, finalY, 0.64f);
        visualPivot.localScale = new Vector3(Mathf.Clamp(finalX, 0.78f, 1.28f), Mathf.Clamp(finalY, 0.78f, 1.28f), 1f);
        visualPivot.localPosition = stateOffset
            - movementDirection * speedNormalized * (0.009f + Mathf.Max(0f, -locomotion) * 0.010f) * amplitude
            + gaitOffset
            + idleOffset
            + new Vector2(-movementDirection.y, movementDirection.x) * impactEnvelope * 0.025f * amplitude;
        visualPivot.localRotation = Quaternion.Euler(0f, 0f, stateRotation * 0.72f + gaitRotation + impactEnvelope * fast * 3.5f);

        Color bodyColor = Color.Lerp(authoredBaseColor, accent, GetStateTintAmount(state));
        // El cuerpo conserva el matiz del estado, pero parte de una base oscura
        // para separarse de los indicadores luminosos y reforzar su identidad.
        bodyColor = Color.Lerp(GlitchUiPalette.Surface, bodyColor, 0.48f);
        // La identidad cromatica del estado permanece en el cuerpo, pero el final
        // recupera un valor neutral para comunicar que la ventana especial se cierra.
        bodyColor = Color.Lerp(bodyColor, GlitchUiPalette.Neutral, recovery * 0.14f);
        bodyColor = Color.Lerp(bodyColor, Color.white, transitionEnvelope * 0.42f + impactEnvelope * 0.58f);
        bodyColor.a = authoredBaseColor.a * Mathf.Clamp01(bodyAlpha);
        bodyRenderer.color = bodyColor;

        UpdateSlimeIdentity(state, bodyColor, accent, actionEnvelope, impactEnvelope, amplitude);
        UpdateOrnaments(accent, nodePattern, visibleNodes, nodeRadius, nodeSpin, haloScale, coreScale, fast, actionEnvelope, impactEnvelope, profile.ornamentPulse);
        UpdateStateSignature(profile, accent, amplitude);
        UpdateMotionEchoes(profile, bodyColor, amplitude);
    }

    private void UpdateSlimeIdentity(EnemyController.AnomalyState state, Color bodyColor, Color accent, float actionEnvelope, float impactEnvelope, float amplitude)
    {
        Vector2 targetLook = movementDirection.sqrMagnitude > 0.001f ? movementDirection.normalized : Vector2.right;
        float lookResponse = 1f - Mathf.Exp(-8.5f * Time.deltaTime);
        smoothedLookDirection = Vector2.Lerp(smoothedLookDirection, targetLook, lookResponse).normalized;
        if (smoothedLookDirection.sqrMagnitude < 0.001f)
        {
            smoothedLookDirection = Vector2.right;
        }

        float gait = Mathf.Sin(animationClock * Mathf.Lerp(3.6f, 9.2f, speedNormalized));
        float settle = Mathf.Sin(animationClock * 2.35f) * (1f - speedNormalized);
        float contourPhase = animationClock * Mathf.Lerp(2.5f, 8.2f, speedNormalized);
        Sprite animatedSilhouette = CompactSlimeSpriteProvider.GetFrame(contourPhase);
        bodyRenderer.sprite = animatedSilhouette;
        if (haloRenderer != null)
        {
            haloRenderer.sprite = animatedSilhouette;
        }
        if (shadowRenderer != null)
        {
            shadowRenderer.sprite = animatedSilhouette;
        }
        float frontBulge = speedNormalized * (0.026f + Mathf.Max(0f, gait) * 0.022f)
            + accelerationNormalized * 0.026f;
        Vector2 bodySide = new Vector2(-smoothedLookDirection.y, smoothedLookDirection.x);
        float weightShift = gait * speedNormalized;
        bodyRenderer.transform.localPosition = smoothedLookDirection * frontBulge + bodySide * weightShift * 0.018f;
        bodyRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, weightShift * 2.4f + settle * 0.8f);
        float stepCompression = Mathf.Abs(gait) * speedNormalized * 0.052f;
        float accelerationKick = accelerationNormalized * 0.045f;
        bodyRenderer.transform.localScale = new Vector3(
            1.06f + speedNormalized * 0.030f + settle * 0.026f + stepCompression + accelerationKick + impactEnvelope * 0.050f,
            1.05f - speedNormalized * 0.016f - settle * 0.021f - stepCompression * 0.78f - accelerationKick * 0.62f - impactEnvelope * 0.038f,
            1f);

        UpdateSlimeFace(state, actionEnvelope, impactEnvelope, bodyColor.a);
    }

    private void UpdateSlimeFace(EnemyController.AnomalyState state, float actionEnvelope, float impactEnvelope, float bodyAlpha)
    {
        if (faceRoot == null)
        {
            return;
        }

        // La masa puede rotar y deformarse por los estados, pero la mirada conserva
        // una direccion legible hacia el desplazamiento del enemigo.
        // La cara permanece legible en pantalla. Solo se desplaza suavemente
        // hacia la direccion de persecucion, sin transformarse en dos rayas.
        faceRoot.localRotation = Quaternion.Inverse(visualPivot.localRotation);
        faceRoot.localPosition = Vector3.zero;
        float spacing = Mathf.Lerp(0.15f, 0.125f, actionEnvelope);
        Vector2 faceCenter = new Vector2(smoothedLookDirection.x * 0.045f, 0.13f + smoothedLookDirection.y * 0.025f);
        Vector2 leftPosition = faceCenter - Vector2.right * spacing;
        Vector2 rightPosition = faceCenter + Vector2.right * spacing;
        float blink = Mathf.Abs(Mathf.Sin(animationClock * 0.73f + 0.4f)) > 0.985f ? 0.24f : 1f;
        float eyeHeight = Mathf.Lerp(0.17f, 0.20f, actionEnvelope) * blink;
        float eyeWidth = Mathf.Lerp(0.31f, 0.26f, impactEnvelope);
        float eyeSlant = 0f;

        switch (state)
        {
            case EnemyController.AnomalyState.Split:
                spacing += 0.045f;
                eyeSlant = 8f;
                break;
            case EnemyController.AnomalyState.ExpansionShoot:
            case EnemyController.AnomalyState.PincerBarrage:
                eyeHeight *= 0.68f;
                eyeSlant = 14f;
                break;
            case EnemyController.AnomalyState.SpeedSurge:
            case EnemyController.AnomalyState.WeaveHunter:
                faceCenter += smoothedLookDirection * 0.035f;
                eyeHeight *= 0.82f;
                eyeSlant = 9f;
                break;
            case EnemyController.AnomalyState.Destroyer:
            case EnemyController.AnomalyState.AdaptiveCountermeasure:
                eyeHeight *= 0.62f;
                eyeWidth *= 1.10f;
                eyeSlant = 18f;
                break;
            case EnemyController.AnomalyState.SignalJam:
            case EnemyController.AnomalyState.InputDesync:
                break;
            case EnemyController.AnomalyState.PhaseBlink:
            case EnemyController.AnomalyState.BlindspotProtocol:
                bodyAlpha *= 0.72f + Mathf.Abs(Mathf.Sin(animationClock * 11f)) * 0.28f;
                break;
            case EnemyController.AnomalyState.SignalPossession:
            case EnemyController.AnomalyState.SignalTether:
                spacing *= 0.72f;
                eyeHeight *= 1.16f;
                break;
        }

        leftPosition = faceCenter - Vector2.right * spacing;
        rightPosition = faceCenter + Vector2.right * spacing;
        if (state == EnemyController.AnomalyState.SignalJam || state == EnemyController.AnomalyState.InputDesync)
        {
            leftPosition.y += Mathf.Sin(animationClock * 17f) * 0.035f;
            rightPosition.y -= Mathf.Sin(animationClock * 13f) * 0.035f;
        }

        Color eyeColor = Color.white;
        eyeColor.a = Mathf.Clamp01(bodyAlpha * 0.94f);
        ConfigureEye(leftEyeRenderer, leftPosition, eyeWidth, eyeHeight, eyeColor);
        ConfigureEye(rightEyeRenderer, rightPosition, eyeWidth, eyeHeight, eyeColor);
        leftEyeRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, eyeSlant);
        rightEyeRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, -eyeSlant);
    }

    private static void ConfigureEye(SpriteRenderer renderer, Vector2 position, float width, float height, Color color)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.enabled = true;
        renderer.transform.localPosition = position;
        renderer.transform.localRotation = Quaternion.identity;
        renderer.transform.localScale = new Vector3(width, height, 1f);
        renderer.color = color;
    }

    private void UpdateOrnaments(
        Color accent,
        NodePattern pattern,
        int visibleNodes,
        float nodeRadius,
        float nodeSpin,
        float haloScale,
        float coreScale,
        float fast,
        float actionEnvelope,
        float impactEnvelope,
        float ornamentPulse)
    {
        if (shadowRenderer != null)
        {
            shadowRenderer.transform.localPosition = new Vector3(0.07f, -0.09f, 0f);
            shadowRenderer.transform.localScale = new Vector3(1.12f + speedNormalized * 0.04f, 1.08f - speedNormalized * 0.025f, 1f);
            shadowRenderer.color = new Color(GlitchUiPalette.Surface.r, GlitchUiPalette.Surface.g, GlitchUiPalette.Surface.b, 0.30f);
        }

        if (haloRenderer != null)
        {
            float pulse = 1f + Mathf.Sin(animationClock * (3.8f + ornamentPulse * 2.4f)) * (0.022f + ornamentPulse * 0.020f);
            haloRenderer.transform.localPosition = Vector3.zero;
            haloRenderer.transform.localRotation = Quaternion.identity;
            float silhouetteBorderScale = 1.08f + (haloScale - 1.25f) * 0.22f;
            haloRenderer.transform.localScale = Vector3.one * silhouetteBorderScale * pulse;
            haloRenderer.color = new Color(accent.r, accent.g, accent.b, 0.22f + speedNormalized * 0.04f + actionEnvelope * 0.12f);
        }

        if (coreRenderer != null)
        {
            // Solo aparece como destello de accion; deja libre la cara durante
            // la persecucion normal.
            coreRenderer.enabled = actionEnvelope > 0.035f || impactEnvelope > 0.035f;
            coreRenderer.transform.localPosition = Vector3.zero;
            coreRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, animationClock * 105f);
            coreRenderer.transform.localScale = Vector3.one * (coreScale * 0.55f + actionEnvelope * 0.07f);
            Color coreColor = Color.Lerp(accent, Color.white, 0.48f + impactEnvelope * 0.42f);
            coreColor.a = 0.22f + actionEnvelope * 0.38f;
            coreRenderer.color = coreColor;
        }

        for (int i = 0; i < nodes.Length; i++)
        {
            SpriteRenderer node = nodes[i];
            bool nodeVisible = i < visibleNodes;
            node.enabled = nodeVisible;
            if (!nodeVisible)
            {
                continue;
            }

            float fraction = i / (float)Mathf.Max(1, visibleNodes);
            float angle = fraction * Mathf.PI * 2f + animationClock * nodeSpin;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 position = direction * nodeRadius;
            Vector2 scale = new Vector2(0.15f, 0.055f);

            switch (pattern)
            {
                case NodePattern.Split:
                    position = new Vector2((i < visibleNodes / 2 ? -1f : 1f) * (0.58f + (i % 3) * 0.13f), Mathf.Sin(angle) * 0.42f);
                    break;
                case NodePattern.Trail:
                    position = -movementDirection * (0.48f + i * 0.14f) + new Vector2(-movementDirection.y, movementDirection.x) * Mathf.Sin(animationClock * 7f + i) * 0.10f;
                    scale = new Vector2(Mathf.Lerp(0.20f, 0.07f, fraction), 0.045f);
                    break;
                case NodePattern.Weave:
                    position = direction * nodeRadius + new Vector2(-movementDirection.y, movementDirection.x) * Mathf.Sin(animationClock * 9f + i * 1.4f) * 0.18f;
                    break;
                case NodePattern.Pincer:
                    position = new Vector2(i % 2 == 0 ? -nodeRadius : nodeRadius, Mathf.Lerp(-0.58f, 0.58f, (i / 2f) / Mathf.Max(1f, visibleNodes / 2f - 1f)));
                    scale = new Vector2(0.22f, 0.055f);
                    break;
                case NodePattern.Grid:
                    position = new Vector2((i % 3 - 1) * 0.50f, (i / 3 - 1) * 0.48f);
                    scale = Vector2.one * 0.085f;
                    break;
                case NodePattern.Desync:
                    position = direction * nodeRadius + new Vector2(Mathf.Sin(i * 8.17f + animationClock * 13f), Mathf.Sin(i * 3.91f - animationClock * 11f)) * 0.08f;
                    scale = new Vector2(0.12f + (i % 3) * 0.04f, 0.045f);
                    break;
                case NodePattern.Tether:
                    position = Vector2.Lerp(-movementDirection * nodeRadius, movementDirection * nodeRadius, visibleNodes <= 1 ? 0.5f : i / (float)(visibleNodes - 1));
                    position += new Vector2(-movementDirection.y, movementDirection.x) * Mathf.Sin(animationClock * 6f + i) * 0.08f;
                    break;
            }

            node.transform.localPosition = position;
            node.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            node.transform.localScale = new Vector3(scale.x * (1f + actionEnvelope * 0.35f), scale.y, 1f);
            Color nodeColor = i % 2 == 0 ? accent : Color.Lerp(accent, GlitchUiPalette.Special, 0.55f);
            nodeColor.a = 0.50f + Mathf.Abs(fast) * 0.28f;
            node.color = nodeColor;
        }
    }

    private static float GetStateTintAmount(EnemyController.AnomalyState state)
    {
        switch (state)
        {
            case EnemyController.AnomalyState.BasePursuit:
                return 0.16f;
            case EnemyController.AnomalyState.SpeedSurge:
            case EnemyController.AnomalyState.PhaseBlink:
            case EnemyController.AnomalyState.PhaseContract:
                return 0.30f;
            case EnemyController.AnomalyState.Destroyer:
            case EnemyController.AnomalyState.ExpansionShoot:
            case EnemyController.AnomalyState.PincerBarrage:
            case EnemyController.AnomalyState.SignalJam:
                return 0.42f;
            default:
                return 0.34f;
        }
    }

    private void SetOrnamentsEnabled(bool visible)
    {
        if (shadowRenderer != null)
        {
            shadowRenderer.enabled = visible;
        }
        if (haloRenderer != null)
        {
            haloRenderer.enabled = visible;
        }
        if (coreRenderer != null)
        {
            coreRenderer.enabled = visible;
        }
        for (int i = 0; i < nodes.Length; i++)
        {
            if (nodes[i] != null && !visible)
            {
                nodes[i].enabled = false;
            }
        }
        if (!visible)
        {
            if (leftEyeRenderer != null) leftEyeRenderer.enabled = false;
            if (rightEyeRenderer != null) rightEyeRenderer.enabled = false;
            if (signatureRingRenderer != null) signatureRingRenderer.enabled = false;
            if (signatureGlyphRenderer != null) signatureGlyphRenderer.enabled = false;
        }
    }

    private void ConfigureStateSignature(EnemyController.AnomalyState state)
    {
        wasPreparingAttack = false;
        EnemyStatePresentationCatalog.Profile profile = EnemyStatePresentationCatalog.Get(state);
        signatureDuration = Mathf.Max(0f, profile.signatureSeconds);
        signatureTimer = profile.ShowsSignature ? signatureDuration : 0f;
        if (signatureGlyphRenderer != null)
        {
            signatureGlyphRenderer.sprite = AnomalyStateGlyphSpriteProvider.Get(profile.glyph);
        }
    }

    private void UpdateStateSignature(EnemyStatePresentationCatalog.Profile profile, Color accent, float amplitude)
    {
        if (signatureRoot == null || signatureRingRenderer == null || signatureGlyphRenderer == null)
        {
            return;
        }

        bool preparing = profile.HasAttackPreparation && owner.IsPreparingAttack;
        if (wasPreparingAttack && !preparing)
        {
            signatureTimer = Mathf.Max(signatureTimer, 0.22f);
        }
        wasPreparingAttack = preparing;
        bool visible = profile.ShowsSignature && (signatureTimer > 0f || preparing) && presentationVisible;
        signatureRingRenderer.enabled = visible;
        signatureGlyphRenderer.enabled = visible;
        if (!visible)
        {
            return;
        }

        float remaining = Mathf.Clamp01(signatureTimer / Mathf.Max(0.01f, signatureDuration));
        float elapsed = 1f - remaining;
        float fade = Mathf.SmoothStep(0f, 1f, Mathf.Min(elapsed * 8f, remaining * 3.4f));
        if (preparing)
        {
            fade = 0.88f + Mathf.Sin(animationClock * 8f) * 0.12f;
        }
        float tierScale = profile.tier >= 3 ? 0.86f : profile.tier == 2 ? 0.93f : 1f;
        float pop = 0.82f + Mathf.Sin(Mathf.Clamp01(elapsed * 1.7f) * Mathf.PI * 0.5f) * 0.18f;
        signatureRoot.localPosition = new Vector3(0f, 1.13f + Mathf.Sin(animationClock * 4.4f) * 0.025f * amplitude, 0f);
        signatureRoot.localRotation = Quaternion.identity;
        signatureRoot.localScale = Vector3.one * tierScale * pop;

        // Amarillo solo durante el aviso real; entrar en un estado no implica una carga.
        Color warning = preparing ? GlitchUiPalette.Alert : GlitchUiPalette.Danger;
        signatureRingRenderer.transform.localPosition = Vector3.zero;
        signatureRingRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, animationClock * 52f);
        signatureRingRenderer.transform.localScale = Vector3.one * 0.82f;
        signatureRingRenderer.color = GlitchUiPalette.WithAlpha(warning, fade * 0.45f);
        signatureGlyphRenderer.transform.localPosition = Vector3.zero;
        signatureGlyphRenderer.transform.localRotation = Quaternion.identity;
        signatureGlyphRenderer.transform.localScale = Vector3.one * 0.58f;
        signatureGlyphRenderer.color = GlitchUiPalette.WithAlpha(Color.Lerp(warning, Color.white, 0.18f), fade * 0.96f);
    }

    private void CreateMotionEchoRig(int bodyOrder)
    {
        GameObject root = new GameObject($"{name}_MotionEchoPool");
        motionEchoRoot = root.transform;
        for (int i = 0; i < motionEchoRenderers.Length; i++)
        {
            GameObject echo = new GameObject($"Echo_{i:00}");
            echo.transform.SetParent(motionEchoRoot, false);
            SpriteRenderer renderer = echo.AddComponent<SpriteRenderer>();
            CopyRenderer(sourceRenderer, renderer);
            renderer.sprite = CompactSlimeSpriteProvider.Get();
            renderer.sortingOrder = bodyOrder - 2;
            renderer.enabled = false;
            motionEchoRenderers[i] = renderer;
        }
    }

    private void UpdateMotionEchoes(EnemyStatePresentationCatalog.Profile profile, Color bodyColor, float amplitude)
    {
        float dt = Time.deltaTime;
        for (int i = 0; i < motionEchoRenderers.Length; i++)
        {
            SpriteRenderer echo = motionEchoRenderers[i];
            if (echo == null || motionEchoTimers[i] <= 0f)
            {
                if (echo != null) echo.enabled = false;
                continue;
            }

            motionEchoTimers[i] = Mathf.Max(0f, motionEchoTimers[i] - dt);
            float normalized = motionEchoTimers[i] / Mathf.Max(0.01f, motionEchoDurations[i]);
            Color color = motionEchoColors[i];
            color.a *= normalized * normalized;
            echo.color = color;
            echo.transform.localScale *= 1f - dt * 0.22f;
            echo.enabled = motionEchoTimers[i] > 0f;
        }

        bool forced = forceMotionEcho;
        float motionPreference = Mathf.Clamp(motionIntensity / Mathf.Max(0.01f, UserSettings.DefaultMenuMotion), 0.35f, 1.30f);
        float strength = profile.trailStrength * Mathf.Lerp(0.45f, 1f, speedNormalized) * motionPreference;
        if (strength <= 0.08f || (!forced && speedNormalized < 0.34f) || motionEchoCooldown > 0f || bodyRenderer == null)
        {
            return;
        }

        float interval = Mathf.Lerp(0.13f, 0.045f, Mathf.Clamp01(strength * speedNormalized));
        motionEchoCooldown = interval;
        if (!GameFeelDirector.TryReserveCosmetic(0.11f))
        {
            forceMotionEcho = false;
            return;
        }

        forceMotionEcho = false;

        int index = motionEchoCursor++ % motionEchoRenderers.Length;
        SpriteRenderer target = motionEchoRenderers[index];
        if (target == null) return;
        float duration = Mathf.Lerp(0.13f, 0.27f, Mathf.Clamp01(strength)) * Mathf.Lerp(0.75f, 1f, amplitude);
        motionEchoTimers[index] = duration;
        motionEchoDurations[index] = duration;
        Color accent = Color.Lerp(bodyColor, owner.CurrentStateColor, 0.58f);
        accent.a = Mathf.Lerp(0.10f, 0.30f, Mathf.Clamp01(strength));
        motionEchoColors[index] = accent;
        target.sprite = bodyRenderer.sprite;
        target.transform.position = bodyRenderer.transform.position;
        target.transform.rotation = bodyRenderer.transform.rotation;
        target.transform.localScale = bodyRenderer.transform.lossyScale;
        target.color = accent;
        target.enabled = true;
    }

    private void ClearMotionEchoes()
    {
        for (int i = 0; i < motionEchoRenderers.Length; i++)
        {
            motionEchoTimers[i] = 0f;
            if (motionEchoRenderers[i] != null) motionEchoRenderers[i].enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (motionEchoRoot != null)
        {
            Destroy(motionEchoRoot.gameObject);
        }
    }
}

// Variante liviana para el clon de Split. La fisica permanece en el objeto padre.
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class SplitCloneVisualAnimator : MonoBehaviour
{
    private static int cloneSequence;
    private SplitAnomalyCloneController owner;
    private Transform visualPivot;
    private SpriteRenderer bodyRenderer;
    private SpriteRenderer haloRenderer;
    private SpriteRenderer coreRenderer;
    private SpriteRenderer leftEyeRenderer;
    private SpriteRenderer rightEyeRenderer;
    private Transform faceRoot;
    private float animationClock;
    private float impactPulse;
    private float impactStrength = 1f;
    private float actionPulse;
    private float actionStrength = 1f;
    private float motionIntensity = 1f;
    private Color baseColor = Color.white;
    private Vector2 lastDirection = Vector2.right;
    private float phaseOffset;

    public SpriteRenderer BodyRenderer => bodyRenderer;

    public void Initialize(SplitAnomalyCloneController ownerReference, SpriteRenderer sourceRenderer)
    {
        owner = ownerReference;
        motionIntensity = UserSettings.GetMenuMotion();
        phaseOffset = (cloneSequence++ % 17) * 0.19f;
        if (sourceRenderer == null || visualPivot != null)
        {
            return;
        }

        GameObject pivot = new GameObject("SplitCloneVisualPivot");
        pivot.transform.SetParent(transform, false);
        visualPivot = pivot.transform;

        GameObject body = new GameObject("Body");
        body.transform.SetParent(visualPivot, false);
        bodyRenderer = body.AddComponent<SpriteRenderer>();
        bodyRenderer.sprite = sourceRenderer.sprite;
        bodyRenderer.color = sourceRenderer.color;
        bodyRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
        bodyRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
        bodyRenderer.sortingOrder = sourceRenderer.sortingOrder;
        bodyRenderer.flipX = sourceRenderer.flipX;
        bodyRenderer.flipY = sourceRenderer.flipY;
        bodyRenderer.drawMode = sourceRenderer.drawMode;
        bodyRenderer.size = sourceRenderer.size;
        bodyRenderer.maskInteraction = sourceRenderer.maskInteraction;
        bodyRenderer.spriteSortPoint = sourceRenderer.spriteSortPoint;
        bodyRenderer.enabled = sourceRenderer.enabled;
        baseColor = sourceRenderer.color;
        bodyRenderer.sprite = CompactSlimeSpriteProvider.Get();
        bodyRenderer.drawMode = SpriteDrawMode.Simple;
        bodyRenderer.size = Vector2.one;

        haloRenderer = CreateRenderer("SplitHalo", CompactSlimeSpriteProvider.Get(), sourceRenderer, sourceRenderer.sortingOrder - 2);
        coreRenderer = CreateRenderer("SplitCore", SquareSpriteProvider.Get(), sourceRenderer, sourceRenderer.sortingOrder + 1);
        GameObject face = new GameObject("SplitSlimeFace");
        face.transform.SetParent(visualPivot, false);
        faceRoot = face.transform;
        leftEyeRenderer = CreateRenderer("SplitLeftEye", SlimeEyeSpriteProvider.Get(), sourceRenderer, sourceRenderer.sortingOrder + 3, faceRoot);
        rightEyeRenderer = CreateRenderer("SplitRightEye", SlimeEyeSpriteProvider.Get(), sourceRenderer, sourceRenderer.sortingOrder + 3, faceRoot);
        rightEyeRenderer.flipX = true;
        sourceRenderer.enabled = false;
    }

    public void PlayImpact(float strength = 1f)
    {
        float clampedStrength = Mathf.Clamp(strength, 0.2f, 1.5f);
        impactStrength = impactPulse > 0f ? Mathf.Max(impactStrength, clampedStrength) : clampedStrength;
        impactPulse = 1f;
    }

    public void PlayAction(float strength = 1f)
    {
        float clampedStrength = Mathf.Clamp(strength, 0.2f, 1.5f);
        actionStrength = actionPulse > 0f ? Mathf.Max(actionStrength, clampedStrength) : clampedStrength;
        actionPulse = 1f;
    }

    private SpriteRenderer CreateRenderer(string name, Sprite sprite, SpriteRenderer source, int order)
    {
        return CreateRenderer(name, sprite, source, order, visualPivot);
    }

    private static SpriteRenderer CreateRenderer(string name, Sprite sprite, SpriteRenderer source, int order, Transform parent)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = source.sharedMaterial;
        renderer.sortingLayerID = source.sortingLayerID;
        renderer.sortingOrder = order;
        return renderer;
    }

    private void LateUpdate()
    {
        if (owner == null || visualPivot == null || bodyRenderer == null || !bodyRenderer.enabled)
        {
            return;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f)
        {
            return;
        }

        animationClock += dt * Mathf.Lerp(0.8f, 1.2f, Mathf.InverseLerp(UserSettings.MinMenuMotion, UserSettings.MaxMenuMotion, motionIntensity));
        impactPulse = Mathf.MoveTowards(impactPulse, 0f, dt * 7f);
        actionPulse = Mathf.MoveTowards(actionPulse, 0f, dt * 4.5f);
        float impact = Mathf.Sin((1f - Mathf.Clamp01(impactPulse)) * Mathf.PI) * impactStrength;
        float action = Mathf.Sin((1f - Mathf.Clamp01(actionPulse)) * Mathf.PI) * actionStrength;
        Vector2 velocity = owner.CurrentVelocity;
        float speed = Mathf.Clamp01(velocity.magnitude / 8f);
        if (velocity.sqrMagnitude > 0.001f)
        {
            lastDirection = velocity.normalized;
        }
        Vector2 direction = lastDirection;
        Vector2 absolute = new Vector2(Mathf.Abs(direction.x), Mathf.Abs(direction.y));
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);
        float intensity01 = Mathf.InverseLerp(UserSettings.MinMenuMotion, UserSettings.MaxMenuMotion, motionIntensity);
        float visualAmplitude = Mathf.Lerp(0.65f, 1.2f, intensity01);
        float gait = Mathf.Sin(animationClock * Mathf.Lerp(3.8f, 9.4f, speed));
        Sprite animatedSilhouette = CompactSlimeSpriteProvider.GetFrame(
            animationClock * Mathf.Lerp(2.7f, 8f, speed) + phaseOffset);
        bodyRenderer.sprite = animatedSilhouette;
        haloRenderer.sprite = animatedSilhouette;
        float stretch = speed * 0.090f * visualAmplitude;
        float x = 1f + absolute.x * stretch - absolute.y * stretch * 0.55f;
        float y = 1f + absolute.y * stretch - absolute.x * stretch * 0.55f;

        if (owner.IsMergeActive)
        {
            float mergePulse = 0.5f + 0.5f * Mathf.Sin(animationClock * 12f);
            x *= 0.88f + mergePulse * 0.10f + action * 0.06f;
            y *= 0.88f + (1f - mergePulse) * 0.10f - action * 0.035f;
            visualPivot.localRotation = Quaternion.Euler(0f, 0f, animationClock * 145f * visualAmplitude);
        }
        else
        {
            visualPivot.localRotation = Quaternion.Euler(
                0f,
                0f,
                (gait * speed * 7.8f + Mathf.Sin(animationClock * 2.3f) * (1f - speed) * 1.8f) * visualAmplitude);
        }

        visualPivot.localScale = new Vector3(
            Mathf.Clamp(x * (1f + impact * 0.085f), 0.82f, 1.24f),
            Mathf.Clamp(y * (1f - impact * 0.065f), 0.82f, 1.24f),
            1f);
        visualPivot.localPosition = perpendicular * (gait * speed * 0.064f + impact * 0.042f)
            + Vector2.up * (Mathf.Abs(gait) * speed * 0.038f + Mathf.Sin(animationClock * 2.2f) * (1f - speed) * 0.014f);

        float pulse = 0.5f + 0.5f * Mathf.Sin(animationClock * 6.5f);
        Color accent = Color.Lerp(GlitchUiPalette.Danger, GlitchUiPalette.Special, 0.48f);
        haloRenderer.transform.localScale = Vector3.one * (1.07f + pulse * 0.025f);
        haloRenderer.color = new Color(accent.r, accent.g, accent.b, 0.18f + pulse * 0.07f);
        coreRenderer.enabled = action > 0.035f || impact > 0.035f;
        coreRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, -animationClock * 120f);
        coreRenderer.transform.localScale = Vector3.one * (0.13f + action * 0.06f);
        coreRenderer.color = new Color(1f, 0.72f, 0.86f, 0.28f + action * 0.30f);
        Color animatedColor = Color.Lerp(baseColor, GlitchUiPalette.Special, 0.22f);
        animatedColor = Color.Lerp(GlitchUiPalette.Surface, animatedColor, 0.48f);
        animatedColor = Color.Lerp(animatedColor, Color.white, Mathf.Clamp01(impact * 0.55f + action * 0.20f));
        animatedColor.a = baseColor.a;
        bodyRenderer.color = animatedColor;

        if (faceRoot != null)
        {
            faceRoot.localRotation = Quaternion.Inverse(visualPivot.localRotation);
        }
        Vector2 faceCenter = new Vector2(direction.x * 0.04f, 0.12f + direction.y * 0.02f);
        Color eyeColor = new Color(1f, 1f, 1f, Mathf.Clamp01(baseColor.a * 0.92f));
        ConfigureCloneEye(leftEyeRenderer, faceCenter - Vector2.right * 0.14f, 0.27f, 0.15f, 0f, eyeColor);
        ConfigureCloneEye(rightEyeRenderer, faceCenter + Vector2.right * 0.14f, 0.27f, 0.15f, 0f, eyeColor);
    }

    private static void ConfigureCloneEye(SpriteRenderer renderer, Vector2 position, float width, float height, float rotation, Color color)
    {
        if (renderer == null)
        {
            return;
        }

        renderer.transform.localPosition = position;
        renderer.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
        renderer.transform.localScale = new Vector3(width, height, 1f);
        renderer.color = color;
    }
}

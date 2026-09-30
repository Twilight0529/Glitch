using UnityEngine;

// Rig visual compartido por pickups, objetivos y proyectiles. Nunca modifica el root fisico.
[DisallowMultipleComponent]
public sealed class WorldObjectAnimationFx : MonoBehaviour
{
    private static int phaseSerial;

    public enum VisualKind
    {
        Score,
        DataCore,
        PowerupSpeed,
        PowerupShield,
        PowerupCompact,
        PowerupPhaseDash,
        Objective,
        Projectile
    }

    public enum ProjectileStyle
    {
        Direct,
        Pincer,
        Orbit,
        Possessed
    }

    private SpriteRenderer sourceRenderer;
    private SpriteRenderer bodyRenderer;
    private SpriteRenderer haloRenderer;
    private SpriteRenderer coreRenderer;
    private SpriteRenderer accentRendererA;
    private SpriteRenderer accentRendererB;
    private SpriteRenderer reflectionRenderer;
    private Transform presentationRoot;
    private Transform contentRoot;
    private Transform ornamentRoot;

    private VisualKind visualKind;
    private ProjectileStyle projectileStyle;
    private Color tint = Color.white;
    private Vector3 requestedContentScale = Vector3.one;
    private float age;
    private float lifetime = 10f;
    private float activity;
    private float clock;
    private float phase;
    private float reflectionPulse;
    private float motionIntensity = 1f;
    private bool initialized;
    private bool reflected;

    public SpriteRenderer BodyRenderer => bodyRenderer != null ? bodyRenderer : sourceRenderer;
    public Transform VisualRoot => contentRoot != null ? contentRoot : transform;
    public float MotionIntensity => motionIntensity;

    private void Awake()
    {
        // La preferencia se lee una sola vez por objeto, nunca durante Update/LateUpdate.
        motionIntensity = UserSettings.GetMenuMotion();
        phase = (phaseSerial++ % 997) * 0.017f;
    }

    public SpriteRenderer Initialize(SpriteRenderer source, VisualKind kind, Color initialTint)
    {
        sourceRenderer = source;
        visualKind = kind;
        tint = initialTint;

        if (!initialized)
        {
            BuildRig();
        }

        SetStyle(kind, initialTint);
        return BodyRenderer;
    }

    public void SetStyle(VisualKind kind, Color styleTint)
    {
        visualKind = kind;
        tint = styleTint;
        ApplyOrnamentTint();
    }

    public void SetProjectileStyle(ProjectileStyle style)
    {
        projectileStyle = style;
    }

    public void SetLifetimeState(float currentAge, float totalLifetime)
    {
        age = Mathf.Max(0f, currentAge);
        lifetime = Mathf.Max(0.1f, totalLifetime);
    }

    public void SetContentScale(float uniformScale)
    {
        float scale = Mathf.Max(0.01f, uniformScale);
        requestedContentScale = new Vector3(scale, scale, 1f);
    }

    public void SetContentScale(Vector3 scale)
    {
        requestedContentScale = new Vector3(
            Mathf.Max(0.01f, scale.x),
            Mathf.Max(0.01f, scale.y),
            1f);
    }

    public void SetActivity(float normalizedActivity)
    {
        activity = Mathf.Clamp01(normalizedActivity);
    }

    public void PlayReflection(Color reflectionColor)
    {
        reflected = true;
        reflectionPulse = 1f;
        tint = reflectionColor;

        if (bodyRenderer != null)
        {
            bodyRenderer.color = reflectionColor;
            bodyRenderer.sortingOrder = Mathf.Max(bodyRenderer.sortingOrder, 13);
        }

        ApplyOrnamentTint();
        if (reflectionRenderer != null)
        {
            reflectionRenderer.enabled = true;
        }
    }

    private void BuildRig()
    {
        if (sourceRenderer == null)
        {
            return;
        }

        presentationRoot = CreateChild("WorldVisualPresentation", transform);
        contentRoot = CreateChild("Content", presentationRoot);
        ornamentRoot = CreateChild("Ornaments", contentRoot);

        bodyRenderer = CreateRenderer("Body", contentRoot, sourceRenderer.sprite);
        CopyRenderer(sourceRenderer, bodyRenderer);
        sourceRenderer.enabled = false;

        haloRenderer = CreateRenderer("Halo", ornamentRoot, CircleSpriteProvider.Get());
        coreRenderer = CreateRenderer("Core", ornamentRoot, CircleSpriteProvider.Get());
        accentRendererA = CreateRenderer("AccentA", ornamentRoot, SquareSpriteProvider.Get());
        accentRendererB = CreateRenderer("AccentB", ornamentRoot, SquareSpriteProvider.Get());
        reflectionRenderer = CreateRenderer("ReflectionPulse", ornamentRoot, CircleSpriteProvider.Get());
        reflectionRenderer.enabled = false;

        initialized = true;
        ApplyOrnamentTint();
    }

    private void LateUpdate()
    {
        if (!initialized || presentationRoot == null || contentRoot == null)
        {
            return;
        }

        float dt = Time.deltaTime;
        clock += dt;
        reflectionPulse = Mathf.MoveTowards(reflectionPulse, 0f, dt * 2.9f);

        float spawnT = Mathf.Clamp01(age / 0.28f);
        float spawnEnvelope = Mathf.SmoothStep(0f, 1f, spawnT) + Mathf.Sin(spawnT * Mathf.PI) * 0.12f;
        float remaining = Mathf.Max(0f, lifetime - age);
        float expiryT = Mathf.Clamp01(remaining / 0.72f);
        float expiryEnvelope = remaining < 0.72f
            ? Mathf.Lerp(0.72f, 1f, Mathf.SmoothStep(0f, 1f, expiryT))
            : 1f;
        float expiryBeat = remaining < 0.72f
            ? 1f + Mathf.Sin(clock * 25f) * 0.055f * motionIntensity * (1f - expiryT)
            : 1f;

        presentationRoot.localScale = Vector3.one * Mathf.Max(0.01f, spawnEnvelope * expiryEnvelope * expiryBeat);

        Vector3 offset = Vector3.zero;
        Vector3 styleScale = Vector3.one;
        float rotation = 0f;
        ApplyStyleMotion(ref offset, ref styleScale, ref rotation);

        presentationRoot.localPosition = offset;
        contentRoot.localScale = Vector3.Scale(requestedContentScale, styleScale);
        contentRoot.localRotation = Quaternion.Euler(0f, 0f, rotation);
        UpdateOrnaments(remaining, expiryT);
    }

    private void ApplyStyleMotion(ref Vector3 offset, ref Vector3 scale, ref float rotation)
    {
        float m = motionIntensity;
        float slow = Mathf.Sin(clock * 3.1f + phase);
        float quick = Mathf.Sin(clock * 7.2f + phase * 1.7f);

        switch (visualKind)
        {
            case VisualKind.Score:
                offset.y = slow * 0.075f * m;
                scale = Vector3.one * (1f + quick * 0.055f * m);
                rotation = slow * 4f * m;
                break;
            case VisualKind.DataCore:
                offset.y = slow * 0.095f * m;
                scale = Vector3.one * (1f + quick * 0.075f * m);
                rotation = clock * 18f * m;
                break;
            case VisualKind.PowerupSpeed:
                offset.y = slow * 0.14f * m;
                scale = new Vector3(1f + quick * 0.08f * m, 1f - quick * 0.045f * m, 1f);
                rotation = clock * 72f * m;
                break;
            case VisualKind.PowerupShield:
                offset.y = slow * 0.12f * m;
                scale = Vector3.one * (1f + quick * 0.045f * m);
                rotation = slow * 7f * m;
                break;
            case VisualKind.PowerupCompact:
                offset.y = slow * 0.10f * m;
                scale = new Vector3(1f - quick * 0.055f * m, 1f + quick * 0.085f * m, 1f);
                rotation = 0f;
                break;
            case VisualKind.PowerupPhaseDash:
                offset.y = slow * 0.10f * m;
                offset.x = Mathf.Sin(clock * 11f + phase) * 0.025f * m;
                scale = new Vector3(1f + quick * 0.06f * m, 1f, 1f);
                rotation = 0f;
                break;
            case VisualKind.Objective:
                offset.y = slow * 0.025f * m;
                scale = Vector3.one * (1f + Mathf.Sin(clock * 4.8f + phase) * 0.025f * m * (0.35f + activity));
                break;
            case VisualKind.Projectile:
                float projectilePulse = Mathf.Sin(clock * 17f + phase);
                scale = new Vector3(1f + projectilePulse * 0.10f * m, 0.94f - projectilePulse * 0.055f * m, 1f);
                break;
        }

        if (reflectionPulse > 0f)
        {
            float kick = Mathf.Sin(reflectionPulse * Mathf.PI) * 0.28f;
            scale *= 1f + kick;
        }
    }

    private void UpdateOrnaments(float remaining, float expiryT)
    {
        int bodyOrder = bodyRenderer != null ? bodyRenderer.sortingOrder : 0;
        haloRenderer.sortingOrder = bodyOrder - 1;
        coreRenderer.sortingOrder = bodyOrder + 1;
        accentRendererA.sortingOrder = bodyOrder + 1;
        accentRendererB.sortingOrder = bodyOrder + 1;
        reflectionRenderer.sortingOrder = bodyOrder + 2;

        haloRenderer.enabled = true;
        coreRenderer.enabled = true;
        accentRendererA.enabled = true;
        accentRendererB.enabled = true;

        float m = motionIntensity;
        float pulse = 0.5f + 0.5f * Mathf.Sin(clock * 5.2f + phase);
        float fade = remaining < 0.72f ? Mathf.Lerp(0.25f, 1f, expiryT) : 1f;
        Color halo = tint;
        Color core = Color.Lerp(tint, Color.white, 0.72f);

        haloRenderer.sprite = CircleSpriteProvider.Get();
        coreRenderer.sprite = CircleSpriteProvider.Get();
        accentRendererA.sprite = SquareSpriteProvider.Get();
        accentRendererB.sprite = SquareSpriteProvider.Get();
        ornamentRoot.localRotation = Quaternion.identity;

        switch (visualKind)
        {
            case VisualKind.Score:
                haloRenderer.transform.localScale = Vector3.one * Mathf.Lerp(0.82f, 1.12f, pulse);
                halo.a = (0.07f + pulse * 0.10f) * fade;
                coreRenderer.transform.localScale = Vector3.one * 0.12f;
                core.a = 0.72f * fade;
                SetAccentVisible(false);
                break;
            case VisualKind.DataCore:
                haloRenderer.transform.localScale = Vector3.one * Mathf.Lerp(1.18f, 1.48f, pulse);
                halo.a = (0.10f + pulse * 0.14f) * fade;
                coreRenderer.transform.localScale = Vector3.one * Mathf.Lerp(0.14f, 0.23f, pulse);
                core.a = 0.78f * fade;
                SetOrbitingAccents(0.72f, 50f, 0.11f, fade);
                break;
            case VisualKind.PowerupSpeed:
                haloRenderer.transform.localScale = Vector3.one * Mathf.Lerp(1.48f, 1.88f, pulse);
                halo.a = (0.08f + pulse * 0.14f) * fade;
                coreRenderer.transform.localScale = Vector3.one * 0.13f;
                core.a = 0.78f * fade;
                SetSpeedAccents(pulse, fade);
                break;
            case VisualKind.PowerupShield:
                haloRenderer.transform.localScale = Vector3.one * Mathf.Lerp(1.45f, 1.72f, pulse);
                halo.a = (0.12f + pulse * 0.15f) * fade;
                coreRenderer.transform.localScale = Vector3.one * 0.10f;
                core.a = 0.65f * fade;
                SetOrbitingAccents(0.72f, 34f, 0.12f, fade);
                break;
            case VisualKind.PowerupCompact:
                haloRenderer.transform.localScale = new Vector3(Mathf.Lerp(1.65f, 1.38f, pulse), Mathf.Lerp(1.35f, 1.62f, pulse), 1f);
                halo.a = (0.09f + pulse * 0.12f) * fade;
                coreRenderer.transform.localScale = Vector3.one * 0.11f;
                core.a = 0.76f * fade;
                SetCompactAccents(pulse, fade);
                break;
            case VisualKind.PowerupPhaseDash:
                haloRenderer.transform.localScale = new Vector3(Mathf.Lerp(1.55f, 1.92f, pulse), 1.42f, 1f);
                halo.a = (0.07f + pulse * 0.13f) * fade;
                coreRenderer.transform.localScale = Vector3.one * 0.10f;
                core.a = 0.72f * fade;
                SetPhaseAccents(pulse, fade);
                break;
            case VisualKind.Objective:
                haloRenderer.transform.localScale = Vector3.one * Mathf.Lerp(1.55f, 1.92f, pulse);
                halo.a = (0.055f + activity * 0.13f + pulse * 0.045f) * fade;
                coreRenderer.transform.localScale = Vector3.one * Mathf.Lerp(0.10f, 0.20f, activity);
                core.a = Mathf.Lerp(0.28f, 0.82f, activity) * fade;
                SetAccentVisible(false);
                break;
            case VisualKind.Projectile:
                haloRenderer.transform.localScale = new Vector3(Mathf.Lerp(1.18f, 1.44f, pulse), Mathf.Lerp(0.82f, 1.08f, pulse), 1f);
                halo.a = (0.09f + pulse * 0.13f) * fade;
                coreRenderer.transform.localScale = Vector3.one * Mathf.Lerp(0.12f, 0.19f, pulse);
                core.a = 0.90f * fade;
                SetProjectileTrail(pulse, fade);
                break;
        }

        haloRenderer.color = halo;
        coreRenderer.color = core;
        UpdateReflectionPulse();
    }

    private void SetOrbitingAccents(float radius, float speed, float size, float fade)
    {
        float angle = (clock * speed * motionIntensity + phase * 24f) * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        accentRendererA.transform.localPosition = dir * radius;
        accentRendererB.transform.localPosition = -dir * radius;
        accentRendererA.transform.localScale = Vector3.one * size;
        accentRendererB.transform.localScale = Vector3.one * size;
        SetAccentColor(0.76f * fade);
    }

    private void SetSpeedAccents(float pulse, float fade)
    {
        accentRendererA.transform.localPosition = new Vector3(-0.58f - pulse * 0.16f, 0.22f, 0f);
        accentRendererB.transform.localPosition = new Vector3(-0.48f - (1f - pulse) * 0.18f, -0.24f, 0f);
        accentRendererA.transform.localScale = new Vector3(0.42f, 0.065f, 1f);
        accentRendererB.transform.localScale = new Vector3(0.30f, 0.055f, 1f);
        SetAccentColor(0.70f * fade);
    }

    private void SetCompactAccents(float pulse, float fade)
    {
        float x = Mathf.Lerp(0.78f, 0.50f, pulse);
        accentRendererA.transform.localPosition = new Vector3(-x, 0f, 0f);
        accentRendererB.transform.localPosition = new Vector3(x, 0f, 0f);
        accentRendererA.transform.localScale = new Vector3(0.08f, 0.46f, 1f);
        accentRendererB.transform.localScale = new Vector3(0.08f, 0.46f, 1f);
        SetAccentColor(0.72f * fade);
    }

    private void SetPhaseAccents(float pulse, float fade)
    {
        accentRendererA.sprite = bodyRenderer != null ? bodyRenderer.sprite : SquareSpriteProvider.Get();
        accentRendererB.sprite = accentRendererA.sprite;
        float spread = Mathf.Lerp(0.20f, 0.42f, pulse);
        accentRendererA.transform.localPosition = new Vector3(-spread, 0f, 0f);
        accentRendererB.transform.localPosition = new Vector3(spread, 0f, 0f);
        accentRendererA.transform.localScale = Vector3.one * 0.92f;
        accentRendererB.transform.localScale = Vector3.one * 0.92f;
        SetAccentColor(0.16f * fade);
    }

    private void SetProjectileTrail(float pulse, float fade)
    {
        accentRendererA.transform.localPosition = new Vector3(-0.34f - pulse * 0.08f, 0f, 0f);
        accentRendererB.transform.localPosition = new Vector3(-0.62f - (1f - pulse) * 0.10f, 0f, 0f);
        accentRendererA.transform.localScale = new Vector3(0.34f, 0.10f, 1f);
        accentRendererB.transform.localScale = new Vector3(0.24f, 0.065f, 1f);
        SetAccentColor((reflected ? 0.78f : 0.55f) * fade);
    }

    private void SetAccentVisible(bool visible)
    {
        accentRendererA.enabled = visible;
        accentRendererB.enabled = visible;
    }

    private void SetAccentColor(float alpha)
    {
        Color accent = Color.Lerp(tint, Color.white, 0.34f);
        accent.a = alpha;
        accentRendererA.color = accent;
        accentRendererB.color = accent;
    }

    private void UpdateReflectionPulse()
    {
        if (reflectionRenderer == null)
        {
            return;
        }

        if (reflectionPulse <= 0f)
        {
            reflectionRenderer.enabled = false;
            return;
        }

        reflectionRenderer.enabled = true;
        float t = 1f - reflectionPulse;
        reflectionRenderer.transform.localScale = Vector3.one * Mathf.Lerp(0.30f, 1.85f, t);
        Color color = GlitchUiPalette.Success;
        color.a = reflectionPulse * 0.74f;
        reflectionRenderer.color = color;
    }

    private void ApplyOrnamentTint()
    {
        if (!initialized)
        {
            return;
        }

        Color halo = tint;
        halo.a = 0.16f;
        haloRenderer.color = halo;

        Color core = Color.Lerp(tint, Color.white, 0.72f);
        core.a = 0.74f;
        coreRenderer.color = core;
        SetAccentColor(0.65f);
    }

    private static Transform CreateChild(string name, Transform parent)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        return child.transform;
    }

    private static SpriteRenderer CreateRenderer(string name, Transform parent, Sprite sprite)
    {
        Transform child = CreateChild(name, parent);
        SpriteRenderer renderer = child.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        return renderer;
    }

    private static void CopyRenderer(SpriteRenderer source, SpriteRenderer target)
    {
        target.sprite = source.sprite;
        target.color = source.color;
        target.flipX = source.flipX;
        target.flipY = source.flipY;
        // Los sprites procedurales no tienen borde Full Rect. Copiarles Sliced/Tiled
        // hace que Unity emita un warning por cada pickup. En ese caso conservamos
        // el mismo tamano visible mediante la escala del cuerpo y usamos Simple.
        bool supportsAdvancedDrawMode = source.sprite != null && source.sprite.border.sqrMagnitude > 0.0001f;
        if (source.drawMode == SpriteDrawMode.Simple || supportsAdvancedDrawMode)
        {
            target.drawMode = source.drawMode;
            target.size = source.size;
        }
        else
        {
            target.drawMode = SpriteDrawMode.Simple;
            Vector2 spriteSize = source.sprite.bounds.size;
            target.transform.localScale = new Vector3(
                source.size.x / Mathf.Max(0.0001f, spriteSize.x),
                source.size.y / Mathf.Max(0.0001f, spriteSize.y),
                1f);
        }
        target.tileMode = source.tileMode;
        target.maskInteraction = source.maskInteraction;
        target.spriteSortPoint = source.spriteSortPoint;
        target.sortingLayerID = source.sortingLayerID;
        target.sortingOrder = source.sortingOrder;
        target.sharedMaterial = source.sharedMaterial;
        target.enabled = source.enabled;
    }
}

// Acento animado para superficies grandes. Se instala solo en un hijo visual, nunca en el obstaculo fisico.
[DisallowMultipleComponent]
public sealed class SurfaceMotionFx : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private ProceduralArenaGenerator.ArenaTheme theme;
    private Vector2 travelAxis = Vector2.right;
    private Vector3 origin;
    private Color tint = Color.white;
    private float travelDistance = 0.5f;
    private float clock;
    private float phase;
    private float motionIntensity = 1f;
    private bool configured;

    private void Awake()
    {
        motionIntensity = UserSettings.GetMenuMotion();
    }

    public void Configure(
        SpriteRenderer rendererReference,
        ProceduralArenaGenerator.ArenaTheme arenaTheme,
        Vector2 axis,
        float distance,
        Color color,
        float phaseOffset)
    {
        spriteRenderer = rendererReference;
        theme = arenaTheme;
        travelAxis = axis.sqrMagnitude > 0.001f ? axis.normalized : Vector2.right;
        travelDistance = Mathf.Max(0.05f, distance);
        tint = color;
        phase = phaseOffset;
        origin = transform.localPosition;
        configured = spriteRenderer != null;
    }

    private void Update()
    {
        if (!configured)
        {
            return;
        }

        clock += Time.deltaTime;
        float t;
        float alpha;
        float stretch = 1f;

        switch (theme)
        {
            case ProceduralArenaGenerator.ArenaTheme.ContainmentLab:
                t = Mathf.PingPong(clock * 0.62f * motionIntensity + phase, 1f) * 2f - 1f;
                alpha = 0.30f + 0.38f * (0.5f + 0.5f * Mathf.Sin(clock * 5.2f + phase));
                break;
            case ProceduralArenaGenerator.ArenaTheme.StorageBay:
                t = Mathf.PingPong(clock * 0.42f * motionIntensity + phase, 1f) * 2f - 1f;
                t = Mathf.Round(t * 4f) / 4f;
                alpha = 0.30f + 0.28f * (0.5f + 0.5f * Mathf.Sin(clock * 3.1f + phase));
                break;
            case ProceduralArenaGenerator.ArenaTheme.RuptureZone:
                t = Mathf.Sin(clock * 1.9f * motionIntensity + phase);
                t += Mathf.Sin(clock * 6.7f + phase * 2f) * 0.12f * motionIntensity;
                alpha = 0.22f + 0.42f * Mathf.Abs(Mathf.Sin(clock * 4.4f + phase));
                stretch = 0.78f + 0.38f * Mathf.Abs(Mathf.Sin(clock * 3.6f + phase));
                break;
            case ProceduralArenaGenerator.ArenaTheme.DataCore:
                t = Mathf.Repeat(clock * 0.58f * motionIntensity + phase, 1f) * 2f - 1f;
                alpha = 0.38f + 0.36f * (0.5f + 0.5f * Mathf.Sin(clock * 7.5f + phase));
                stretch = 0.85f + 0.30f * (0.5f + 0.5f * Mathf.Sin(clock * 4.8f + phase));
                break;
            default:
                t = Mathf.PingPong(clock * 0.34f * motionIntensity + phase, 1f) * 2f - 1f;
                t = Mathf.Round(t * 6f) / 6f;
                alpha = 0.18f + 0.34f * Mathf.Abs(Mathf.Sin(clock * 2.7f + phase));
                break;
        }

        transform.localPosition = origin + (Vector3)(travelAxis * (travelDistance * 0.5f * t));
        transform.localScale = travelAxis.x != 0f
            ? new Vector3(stretch, 1f, 1f)
            : new Vector3(1f, stretch, 1f);

        Color color = tint;
        color.a = Mathf.Clamp01(alpha);
        spriteRenderer.color = color;
    }
}

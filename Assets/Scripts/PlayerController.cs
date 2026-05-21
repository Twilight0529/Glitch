using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 11f;
    [SerializeField] private float maxBoostMultiplier = 1.9f;
    [SerializeField, Range(0.2f, 1f)] private float minSlowMultiplier = 0.3f;

    [Header("Shield Visual")]
    [SerializeField] private Color shieldColor = new Color(0.95f, 0.64f, 0.88f, 0.85f);
    [SerializeField] private float shieldPulseSpeed = 7.2f;
    [SerializeField] private float shieldRadius = 0.72f;

    [Header("Movement Trail")]
    [SerializeField] private bool enableMovementTrail = true;
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

    private Rigidbody2D rb;
    private Collider2D bodyCollider;
    private SpriteRenderer bodyRenderer;
    private Vector2 moveInput;
    private float speedBoostTimer;
    private float speedBoostMultiplier = 1f;
    private float movementSlowTimer;
    private float movementSlowMultiplier = 1f;
    private float shieldTimer;
    private SpriteRenderer shieldRenderer;
    private GameObject shieldVisual;
    private ParticleSystem movementTrail;
    private bool deathSequenceActive;
    private Color baseBodyColor = Color.white;
    private Vector3 baseBodyScale = Vector3.one;

    public Vector2 CurrentVelocity => rb != null ? rb.linearVelocity : Vector2.zero;
    public bool IsInDeathSequence => deathSequenceActive;
    public float DeathExplosionDuration => Mathf.Max(0.02f, deathChargeDuration) + Mathf.Max(0.02f, deathFlashDuration) + Mathf.Max(0.02f, deathAfterglowDuration);
    public bool HasShield => shieldTimer > 0f;
    public bool HasSpeedBoost => speedBoostTimer > 0f;
    public bool HasMovementSlow => movementSlowTimer > 0f;
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
        EnsureMovementTrail();
    }

    private void Update()
    {
        if (deathSequenceActive)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        UpdatePowerupTimers();
        UpdateShieldVisual();

        Vector2 rawInput = ReadMoveInput();
        float horizontal = rawInput.x;
        float vertical = rawInput.y;

        // 4-directions priority: keeps movement cardinal and readable under pressure.
        if (Mathf.Abs(horizontal) > 0f)
        {
            vertical = 0f;
        }

        moveInput = new Vector2(horizontal, vertical).normalized;
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
        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
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
        shieldTimer = Mathf.Max(shieldTimer, Mathf.Max(0.1f, duration));
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
        return true;
    }

    public bool StartDeathExplosion()
    {
        if (deathSequenceActive)
        {
            return false;
        }

        deathSequenceActive = true;
        StartCoroutine(DeathExplosionRoutine());
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

    private IEnumerator DeathExplosionRoutine()
    {
        speedBoostTimer = 0f;
        speedBoostMultiplier = 1f;
        movementSlowTimer = 0f;
        movementSlowMultiplier = 1f;
        shieldTimer = 0f;

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

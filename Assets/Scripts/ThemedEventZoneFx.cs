using UnityEngine;

public class ThemedEventZoneFx : MonoBehaviour
{
    // Zona temporal de evento tematico: telegrapha, activa un efecto y se autodestruye.
    public enum ZoneKind
    {
        LabSecurityGrid,
        StorageMagnetField,
        RuptureRift
    }

    private ZoneKind kind;
    private PlayerController player;
    private EnemyController enemy;
    private SpriteRenderer spriteRenderer;
    private bool circular;
    private Vector2 size = Vector2.one;
    private float radius = 1f;
    private float telegraphSeconds = 1f;
    private float activeSeconds = 2f;
    private float age;
    private Color telegraphColor = Color.white;
    private Color activeColor = Color.white;
    private float playerSlowMultiplier = 0.72f;
    private float playerSlowDuration = 0.14f;
    private float enemyBoostMultiplier = 1.12f;
    private float enemyBoostDuration = 0.38f;
    private float enemyBoostCooldown = 0.28f;
    private float enemyBoostTimer;
    private float driftSpeed;
    private bool driftToCenter;
    private Vector2 driftDirection = Vector2.right;
    private float firewallChargePerSecond;
    private float pulseSpeed = 7f;

    public void ConfigureRect(
        ZoneKind zoneKind,
        Vector2 zoneSize,
        float telegraphTime,
        float activeTime,
        Color warningColor,
        Color liveColor,
        float slowMultiplier,
        float slowDuration,
        float boostMultiplier,
        float boostDuration,
        float boostCooldown)
    {
        kind = zoneKind;
        circular = false;
        size = new Vector2(Mathf.Max(0.05f, zoneSize.x), Mathf.Max(0.05f, zoneSize.y));
        telegraphSeconds = Mathf.Max(0.05f, telegraphTime);
        activeSeconds = Mathf.Max(0.1f, activeTime);
        telegraphColor = warningColor;
        activeColor = liveColor;
        playerSlowMultiplier = Mathf.Clamp(slowMultiplier, 0.1f, 1f);
        playerSlowDuration = Mathf.Max(0.02f, slowDuration);
        enemyBoostMultiplier = Mathf.Max(1f, boostMultiplier);
        enemyBoostDuration = Mathf.Max(0.02f, boostDuration);
        enemyBoostCooldown = Mathf.Max(0.02f, boostCooldown);
        EnsureVisual();
    }

    public void ConfigureCircle(
        ZoneKind zoneKind,
        float zoneRadius,
        float telegraphTime,
        float activeTime,
        Color warningColor,
        Color liveColor,
        float slowMultiplier,
        float slowDuration,
        float boostMultiplier,
        float boostDuration,
        float boostCooldown,
        float externalDriftSpeed,
        bool driftTowardCenter,
        Vector2 externalDriftDirection,
        float firewallChargeRate)
    {
        kind = zoneKind;
        circular = true;
        radius = Mathf.Max(0.1f, zoneRadius);
        size = Vector2.one * radius * 2f;
        telegraphSeconds = Mathf.Max(0.05f, telegraphTime);
        activeSeconds = Mathf.Max(0.1f, activeTime);
        telegraphColor = warningColor;
        activeColor = liveColor;
        playerSlowMultiplier = Mathf.Clamp(slowMultiplier, 0.1f, 1f);
        playerSlowDuration = Mathf.Max(0.02f, slowDuration);
        enemyBoostMultiplier = Mathf.Max(1f, boostMultiplier);
        enemyBoostDuration = Mathf.Max(0.02f, boostDuration);
        enemyBoostCooldown = Mathf.Max(0.02f, boostCooldown);
        driftSpeed = Mathf.Max(0f, externalDriftSpeed);
        driftToCenter = driftTowardCenter;
        driftDirection = externalDriftDirection.sqrMagnitude > 0.001f ? externalDriftDirection.normalized : Vector2.right;
        firewallChargePerSecond = Mathf.Max(0f, firewallChargeRate);
        EnsureVisual();
    }

    private void Update()
    {
        ResolveActors();
        age += Time.deltaTime;
        enemyBoostTimer = Mathf.Max(0f, enemyBoostTimer - Time.deltaTime);
        float activeStart = telegraphSeconds;
        float total = telegraphSeconds + activeSeconds;
        if (age >= total)
        {
            Destroy(gameObject);
            return;
        }

        bool active = age >= activeStart;
        UpdateVisual(active, Mathf.Clamp01(active ? (age - activeStart) / activeSeconds : age / telegraphSeconds));
        if (active)
        {
            ApplyEffects();
        }
    }

    private void EnsureVisual()
    {
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
            {
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            }
        }

        spriteRenderer.sprite = circular ? CircleSpriteProvider.Get() : SquareSpriteProvider.Get();
        spriteRenderer.drawMode = SpriteDrawMode.Sliced;
        spriteRenderer.sortingOrder = kind == ZoneKind.RuptureRift ? 13 : 8;
        spriteRenderer.size = size;
        spriteRenderer.color = telegraphColor;
    }

    private void ResolveActors()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<PlayerController>();
        }
        if (enemy == null)
        {
            enemy = FindAnyObjectByType<EnemyController>();
        }
    }

    private void UpdateVisual(bool active, float phase)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Max(0.1f, pulseSpeed));
        Color baseColor = active ? activeColor : telegraphColor;
        float alpha = active
            ? Mathf.Lerp(0.34f, 0.76f, pulse)
            : Mathf.Lerp(0.16f, 0.48f, pulse);
        baseColor.a *= alpha;
        spriteRenderer.color = baseColor;

        float scalePulse = active ? Mathf.Lerp(0.96f, 1.06f, pulse) : Mathf.Lerp(0.88f, 1.02f, phase);
        transform.localScale = Vector3.one * scalePulse;
    }

    private void ApplyEffects()
    {
        if (player != null && ContainsPoint(player.GetPosition()))
        {
            player.ApplyMovementSlow(playerSlowMultiplier, playerSlowDuration);
            if (firewallChargePerSecond > 0f)
            {
                player.AddFirewallCharge(firewallChargePerSecond * Time.deltaTime);
            }

            ApplyDriftToPlayer();
        }

        if (enemy != null && ContainsPoint(enemy.GetCurrentPosition()))
        {
            if (enemyBoostTimer <= 0f)
            {
                enemy.ApplyExternalSpeedModifier(enemyBoostMultiplier, enemyBoostDuration);
                enemyBoostTimer = enemyBoostCooldown;
            }

            ApplyDriftToEnemy();
        }
    }

    private bool ContainsPoint(Vector2 point)
    {
        Vector2 local = transform.InverseTransformPoint(point);
        if (circular)
        {
            return local.sqrMagnitude <= radius * radius;
        }

        return Mathf.Abs(local.x) <= size.x * 0.5f && Mathf.Abs(local.y) <= size.y * 0.5f;
    }

    private void ApplyDriftToPlayer()
    {
        if (player == null || driftSpeed <= 0f)
        {
            return;
        }

        Vector2 dir = ResolveDriftDirection(player.transform.position);
        player.ApplyExternalDisplacement(dir * driftSpeed * Time.deltaTime);
    }

    private void ApplyDriftToEnemy()
    {
        if (enemy == null || driftSpeed <= 0f)
        {
            return;
        }

        Vector2 dir = ResolveDriftDirection(enemy.transform.position);
        enemy.ApplyExternalDisplacement(dir * driftSpeed * Time.deltaTime);
    }

    private Vector2 ResolveDriftDirection(Vector2 actorPosition)
    {
        if (!driftToCenter)
        {
            return driftDirection;
        }

        Vector2 toCenter = (Vector2)transform.position - actorPosition;
        return toCenter.sqrMagnitude > 0.001f ? toCenter.normalized : driftDirection;
    }
}

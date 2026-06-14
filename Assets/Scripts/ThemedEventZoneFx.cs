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
    private SpriteRenderer coreRenderer;
    private SpriteRenderer sweepRenderer;
    private readonly SpriteRenderer[] edgeRenderers = new SpriteRenderer[4];
    private readonly SpriteRenderer[] markerRenderers = new SpriteRenderer[16];
    private readonly float[] markerSeeds = new float[16];
    private int markerCount;
    private bool activationPulsePlayed;

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
        if (active && !activationPulsePlayed)
        {
            activationPulsePlayed = true;
            SpawnActivationPulse();
        }

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
        EnsureAccentVisuals();
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
            ? Mathf.Lerp(0.18f, 0.46f, pulse)
            : Mathf.Lerp(0.08f, 0.28f, pulse);
        if (kind == ZoneKind.RuptureRift)
        {
            alpha *= active ? 0.72f : 0.62f;
        }

        baseColor.a *= alpha;
        spriteRenderer.color = baseColor;

        transform.localScale = Vector3.one;
        if (circular)
        {
            UpdateCircleAccents(active, phase, pulse);
        }
        else
        {
            UpdateRectAccents(active, phase, pulse);
        }
    }

    private void EnsureAccentVisuals()
    {
        if (coreRenderer == null)
        {
            coreRenderer = CreateChildRenderer("ZoneCore", circular ? CircleSpriteProvider.Get() : SquareSpriteProvider.Get(), kind == ZoneKind.RuptureRift ? 15 : 10);
        }

        if (sweepRenderer == null)
        {
            sweepRenderer = CreateChildRenderer("ZoneSweep", SquareSpriteProvider.Get(), kind == ZoneKind.RuptureRift ? 16 : 11);
        }

        if (!circular)
        {
            for (int i = 0; i < edgeRenderers.Length; i++)
            {
                if (edgeRenderers[i] == null)
                {
                    edgeRenderers[i] = CreateChildRenderer($"ZoneEdge_{i}", SquareSpriteProvider.Get(), 12);
                }
            }
        }
        else
        {
            for (int i = 0; i < edgeRenderers.Length; i++)
            {
                if (edgeRenderers[i] != null)
                {
                    edgeRenderers[i].gameObject.SetActive(false);
                }
            }
        }

        markerCount = circular
            ? (kind == ZoneKind.RuptureRift ? 10 : 8)
            : 10;
        for (int i = 0; i < markerRenderers.Length; i++)
        {
            if (i >= markerCount)
            {
                if (markerRenderers[i] != null)
                {
                    markerRenderers[i].gameObject.SetActive(false);
                }

                continue;
            }

            if (markerRenderers[i] == null)
            {
                markerRenderers[i] = CreateChildRenderer($"ZoneMarker_{i}", SquareSpriteProvider.Get(), kind == ZoneKind.RuptureRift ? 17 : 13);
                markerSeeds[i] = Random.value;
            }

            markerRenderers[i].gameObject.SetActive(true);
        }
    }

    private SpriteRenderer CreateChildRenderer(string childName, Sprite sprite, int sortingOrder)
    {
        GameObject child = new GameObject(childName);
        child.transform.SetParent(transform, false);
        child.transform.localPosition = Vector3.zero;
        child.transform.localRotation = Quaternion.identity;
        child.transform.localScale = Vector3.one;

        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.sortingOrder = sortingOrder;
        renderer.color = Color.clear;
        return renderer;
    }

    private void UpdateRectAccents(bool active, float phase, float pulse)
    {
        Color live = active ? activeColor : telegraphColor;
        Color bright = Color.Lerp(live, Color.white, active ? 0.35f : 0.18f);
        float edgeAlpha = active ? Mathf.Lerp(0.42f, 0.78f, pulse) : Mathf.Lerp(0.20f, 0.52f, pulse);
        bright.a *= edgeAlpha;

        float edgeThickness = active ? 0.09f : 0.055f;
        SetRectEdge(0, new Vector2(0f, size.y * 0.5f), new Vector2(size.x, edgeThickness), bright);
        SetRectEdge(1, new Vector2(0f, -size.y * 0.5f), new Vector2(size.x, edgeThickness), bright);
        SetRectEdge(2, new Vector2(-size.x * 0.5f, 0f), new Vector2(edgeThickness, size.y), bright);
        SetRectEdge(3, new Vector2(size.x * 0.5f, 0f), new Vector2(edgeThickness, size.y), bright);

        bool horizontal = size.x >= size.y;
        float longAxis = Mathf.Max(size.x, size.y);
        float shortAxis = Mathf.Min(size.x, size.y);
        float scan = Mathf.Repeat((Time.time * (active ? 4.4f : 2.5f)) + phase, 1f);
        if (sweepRenderer != null)
        {
            sweepRenderer.gameObject.SetActive(true);
            sweepRenderer.transform.localPosition = horizontal
                ? new Vector3(Mathf.Lerp(-longAxis * 0.5f, longAxis * 0.5f, scan), 0f, 0f)
                : new Vector3(0f, Mathf.Lerp(-longAxis * 0.5f, longAxis * 0.5f, scan), 0f);
            sweepRenderer.transform.localRotation = horizontal ? Quaternion.identity : Quaternion.Euler(0f, 0f, 90f);
            sweepRenderer.size = new Vector2(Mathf.Max(0.08f, shortAxis * 1.25f), active ? 0.18f : 0.12f);
            Color sweepColor = Color.Lerp(live, Color.white, 0.5f);
            sweepColor.a *= active ? 0.42f : 0.24f;
            sweepRenderer.color = sweepColor;
        }

        if (coreRenderer != null)
        {
            coreRenderer.gameObject.SetActive(true);
            coreRenderer.transform.localPosition = Vector3.zero;
            coreRenderer.transform.localRotation = horizontal ? Quaternion.identity : Quaternion.Euler(0f, 0f, 90f);
            coreRenderer.size = new Vector2(longAxis, Mathf.Max(0.06f, shortAxis * (active ? 0.22f : 0.12f)));
            Color coreColor = active ? activeColor : telegraphColor;
            coreColor.a *= active ? 0.16f + pulse * 0.12f : 0.07f + pulse * 0.08f;
            coreRenderer.color = coreColor;
        }

        for (int i = 0; i < markerCount; i++)
        {
            SpriteRenderer marker = markerRenderers[i];
            if (marker == null)
            {
                continue;
            }

            float n = (i + 0.5f) / markerCount;
            float offset = Mathf.Repeat(n + Time.time * (active ? 0.42f : 0.18f), 1f);
            float localLong = Mathf.Lerp(-longAxis * 0.5f, longAxis * 0.5f, offset);
            float side = i % 2 == 0 ? -1f : 1f;
            float localShort = side * shortAxis * (active ? 0.34f : 0.42f);
            marker.transform.localPosition = horizontal
                ? new Vector3(localLong, localShort, 0f)
                : new Vector3(localShort, localLong, 0f);
            marker.transform.localRotation = horizontal ? Quaternion.identity : Quaternion.Euler(0f, 0f, 90f);
            marker.size = new Vector2(active ? 0.42f : 0.30f, active ? 0.085f : 0.06f);
            Color markerColor = Color.Lerp(live, Color.white, active ? 0.46f : 0.24f);
            markerColor.a *= active ? Mathf.Lerp(0.34f, 0.74f, pulse) : Mathf.Lerp(0.18f, 0.46f, pulse);
            marker.color = markerColor;
        }
    }

    private void SetRectEdge(int index, Vector2 localPosition, Vector2 edgeSize, Color color)
    {
        if (index < 0 || index >= edgeRenderers.Length || edgeRenderers[index] == null)
        {
            return;
        }

        SpriteRenderer edge = edgeRenderers[index];
        edge.gameObject.SetActive(true);
        edge.transform.localPosition = localPosition;
        edge.transform.localRotation = Quaternion.identity;
        edge.size = edgeSize;
        edge.color = color;
    }

    private void UpdateCircleAccents(bool active, float phase, float pulse)
    {
        Color live = active ? activeColor : telegraphColor;
        Color bright = Color.Lerp(live, Color.white, active ? 0.42f : 0.22f);
        float ringScale = kind == ZoneKind.RuptureRift
            ? Mathf.Lerp(0.50f, 0.76f, active ? pulse : phase)
            : Mathf.Lerp(0.78f, 1.08f, active ? pulse : phase);

        if (coreRenderer != null)
        {
            coreRenderer.gameObject.SetActive(true);
            coreRenderer.transform.localPosition = Vector3.zero;
            coreRenderer.transform.localRotation = Quaternion.identity;
            coreRenderer.size = Vector2.one * radius * (kind == ZoneKind.RuptureRift ? 0.92f : 0.58f) * ringScale;
            Color coreColor = kind == ZoneKind.RuptureRift
                ? new Color(0.01f, 0.005f, 0.025f, active ? 0.38f + pulse * 0.12f : 0.20f + pulse * 0.09f)
                : live;
            if (kind != ZoneKind.RuptureRift)
            {
                coreColor.a *= active ? 0.16f + pulse * 0.12f : 0.07f + pulse * 0.08f;
            }

            coreRenderer.color = coreColor;
        }

        if (sweepRenderer != null)
        {
            sweepRenderer.gameObject.SetActive(true);
            float sweepAngle = Time.time * (kind == ZoneKind.RuptureRift ? 110f : 82f);
            sweepRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, sweepAngle);
            sweepRenderer.transform.localPosition = Vector3.zero;
            sweepRenderer.size = new Vector2(radius * (kind == ZoneKind.RuptureRift ? 1.32f : 1.9f), active ? 0.09f : 0.065f);
            Color sweepColor = bright;
            sweepColor.a *= active ? 0.36f : 0.18f;
            sweepRenderer.color = sweepColor;
        }

        for (int i = 0; i < markerCount; i++)
        {
            SpriteRenderer marker = markerRenderers[i];
            if (marker == null)
            {
                continue;
            }

            float seed = markerSeeds[i];
            float angle = ((Mathf.PI * 2f) * i / Mathf.Max(1, markerCount)) + Time.time * (kind == ZoneKind.RuptureRift ? -0.9f : 0.55f) + seed * 0.5f;
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float jitter = kind == ZoneKind.RuptureRift ? Mathf.Sin(Time.time * 9f + seed * 20f) * 0.10f : 0f;
            float markerRadius = radius * (kind == ZoneKind.RuptureRift ? 0.76f + jitter : 0.92f);
            marker.transform.localPosition = dir * markerRadius;

            float rotDeg;
            if (kind == ZoneKind.StorageMagnetField)
            {
                Vector2 arrowDir = driftToCenter ? -dir : driftDirection;
                rotDeg = Mathf.Atan2(arrowDir.y, arrowDir.x) * Mathf.Rad2Deg;
            }
            else
            {
                rotDeg = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f;
            }

            marker.transform.localRotation = Quaternion.Euler(0f, 0f, rotDeg);
            marker.size = kind == ZoneKind.RuptureRift
                ? new Vector2(Mathf.Lerp(0.16f, 0.42f, pulse), 0.055f)
                : new Vector2(active ? 0.34f : 0.24f, active ? 0.10f : 0.07f);

            Color markerColor;
            if (kind == ZoneKind.RuptureRift && (i & 1) == 0)
            {
                markerColor = Color.Lerp(new Color(1f, 0.42f, 0.95f, 1f), bright, pulse);
            }
            else
            {
                markerColor = bright;
            }

            markerColor.a *= active ? Mathf.Lerp(0.36f, 0.76f, pulse) : Mathf.Lerp(0.18f, 0.48f, pulse);
            marker.color = markerColor;
        }
    }

    private void SpawnActivationPulse()
    {
        Color pulseColor = Color.Lerp(activeColor, Color.white, 0.28f);
        pulseColor.a = kind == ZoneKind.RuptureRift ? 0.62f : 0.78f;

        GameObject pulse = new GameObject("ZoneActivationPulse");
        pulse.transform.SetParent(transform, false);
        pulse.transform.localPosition = Vector3.zero;
        SpriteRenderer pulseRenderer = pulse.AddComponent<SpriteRenderer>();
        pulseRenderer.sprite = circular ? CircleSpriteProvider.Get() : SquareSpriteProvider.Get();
        pulseRenderer.drawMode = SpriteDrawMode.Sliced;
        pulseRenderer.sortingOrder = kind == ZoneKind.RuptureRift ? 19 : 15;
        pulseRenderer.size = circular ? Vector2.one * radius * 2f : size;
        pulseRenderer.color = pulseColor;
        pulse.AddComponent<ThemedZoneActivationPulseFx>().Configure(pulseRenderer, circular ? radius : Mathf.Max(size.x, size.y), 0.34f, pulseColor);
        Destroy(pulse, 0.45f);

        if (kind == ZoneKind.RuptureRift)
        {
            SpawnRuptureFragments(pulseColor);
        }
    }

    private void SpawnRuptureFragments(Color color)
    {
        int count = kind == ZoneKind.RuptureRift ? 8 : 10;
        for (int i = 0; i < count; i++)
        {
            float angle = ((Mathf.PI * 2f) * i) / count + Random.Range(-0.16f, 0.16f);
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            GameObject fragment = new GameObject($"RiftActivationFragment_{i}");
            fragment.transform.SetParent(transform, false);
            fragment.transform.localPosition = Vector3.zero;
            SpriteRenderer sr = fragment.AddComponent<SpriteRenderer>();
            sr.sprite = SquareSpriteProvider.Get();
            sr.drawMode = SpriteDrawMode.Sliced;
            sr.sortingOrder = 20;
            sr.size = new Vector2(Random.Range(0.12f, 0.32f), Random.Range(0.04f, 0.09f));
            sr.color = i % 2 == 0 ? color : new Color(1f, 0.42f, 0.96f, 0.92f);
            fragment.AddComponent<ThemedZoneFragmentFx>().Configure(sr, dir, radius * Random.Range(0.65f, 1.25f), 0.42f);
            Destroy(fragment, 0.52f);
        }
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

public class ThemedZoneActivationPulseFx : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private float baseRadius = 1f;
    private float duration = 0.32f;
    private Color color = Color.white;
    private float age;

    public void Configure(SpriteRenderer rendererRef, float radius, float lifeSeconds, Color tint)
    {
        spriteRenderer = rendererRef;
        baseRadius = Mathf.Max(0.1f, radius);
        duration = Mathf.Max(0.05f, lifeSeconds);
        color = tint;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / duration);
        float pulse = Mathf.Sin(t * Mathf.PI);
        transform.localScale = Vector3.one * Mathf.Lerp(0.92f, 1.28f, t);
        if (spriteRenderer != null)
        {
            Color c = color;
            c.a = Mathf.Lerp(0.86f, 0f, t) * (0.65f + pulse * 0.35f);
            spriteRenderer.color = c;
        }
    }
}

public class ThemedZoneFragmentFx : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Vector2 direction = Vector2.right;
    private float distance = 1f;
    private float duration = 0.4f;
    private float age;
    private Vector3 origin;

    public void Configure(SpriteRenderer rendererRef, Vector2 travelDirection, float travelDistance, float lifeSeconds)
    {
        spriteRenderer = rendererRef;
        direction = travelDirection.sqrMagnitude > 0.001f ? travelDirection.normalized : Vector2.right;
        distance = Mathf.Max(0.1f, travelDistance);
        duration = Mathf.Max(0.05f, lifeSeconds);
        origin = transform.localPosition;
    }

    private void Update()
    {
        age += Time.deltaTime;
        float t = Mathf.Clamp01(age / duration);
        float eased = 1f - Mathf.Pow(1f - t, 2f);
        Vector2 jitter = new Vector2(
            Mathf.Sin(Time.time * 31f + distance) * 0.08f,
            Mathf.Cos(Time.time * 27f + distance) * 0.08f);
        transform.localPosition = origin + (Vector3)((direction * distance * eased) + jitter);
        transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + Mathf.Sin(Time.time * 18f) * 14f);
        transform.localScale = Vector3.one * Mathf.Lerp(1.05f, 0.22f, t);
        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = Mathf.Lerp(c.a, 0f, t);
            spriteRenderer.color = c;
        }
    }
}

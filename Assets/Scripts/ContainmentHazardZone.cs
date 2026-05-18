using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class ContainmentHazardZone : MonoBehaviour
{
    [SerializeField] private float pulseSpeed = 5.2f;
    [SerializeField] private float activeLineSpeed = 10f;

    private GameManager gameManager;
    private PlayerController player;
    private EnemyController enemy;
    private SpriteRenderer spriteRenderer;
    private CircleCollider2D circleCollider;
    private Color baseColor = new Color(1f, 0.42f, 0.56f, 0.9f);
    private float telegraphDuration = 0.9f;
    private float activeDuration = 2.3f;
    private float playerSlowMultiplier = 0.58f;
    private float playerSlowDuration = 0.24f;
    private float enemyBoostMultiplier = 1.23f;
    private float enemyBoostDuration = 1.1f;
    private float enemyBoostCooldown = 0.33f;
    private float enemyBoostCooldownTimer;
    private float timer;
    private bool isActive;
    private LineRenderer tetherLine;

    public void Configure(
        GameManager manager,
        PlayerController playerController,
        EnemyController enemyController,
        float radius,
        float telegraphSeconds,
        float activeSeconds,
        Color color,
        float slowMultiplier,
        float slowDuration,
        float boostMultiplier,
        float boostDuration,
        float boostCooldown)
    {
        gameManager = manager;
        player = playerController;
        enemy = enemyController;
        telegraphDuration = Mathf.Max(0.1f, telegraphSeconds);
        activeDuration = Mathf.Max(0.1f, activeSeconds);
        baseColor = color;
        playerSlowMultiplier = Mathf.Clamp(slowMultiplier, 0.1f, 1f);
        playerSlowDuration = Mathf.Max(0.05f, slowDuration);
        enemyBoostMultiplier = Mathf.Max(1f, boostMultiplier);
        enemyBoostDuration = Mathf.Max(0.05f, boostDuration);
        enemyBoostCooldown = Mathf.Max(0.05f, boostCooldown);

        if (circleCollider == null)
        {
            circleCollider = GetComponent<CircleCollider2D>();
        }

        circleCollider.isTrigger = true;
        circleCollider.radius = Mathf.Max(0.15f, radius);

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        spriteRenderer.sprite = CircleSpriteProvider.Get();
        spriteRenderer.drawMode = SpriteDrawMode.Sliced;
        spriteRenderer.size = Vector2.one * (circleCollider.radius * 2f);
        spriteRenderer.sortingOrder = 7;
        EnsureLine();
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        circleCollider = GetComponent<CircleCollider2D>();
        circleCollider.isTrigger = true;
        EnsureLine();
    }

    private void Update()
    {
        if (gameManager != null && (!gameManager.IsRunActive || gameManager.IsGameOver))
        {
            return;
        }

        if (enemy == null)
        {
            enemy = FindAnyObjectByType<EnemyController>();
        }

        if (enemyBoostCooldownTimer > 0f)
        {
            enemyBoostCooldownTimer -= Time.deltaTime;
        }

        timer += Time.deltaTime;
        if (!isActive && timer >= telegraphDuration)
        {
            isActive = true;
            timer = 0f;
        }

        if (isActive && timer >= activeDuration)
        {
            Destroy(gameObject);
            return;
        }

        UpdateVisual();
        UpdateTether();
    }

    private void UpdateVisual()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Max(0.1f, pulseSpeed));
        if (!isActive)
        {
            float charge = Mathf.Clamp01(timer / telegraphDuration);
            float alpha = Mathf.Lerp(0.2f, 0.68f, charge) + pulse * 0.09f;
            Color c = baseColor;
            c.a = alpha;
            spriteRenderer.color = c;
            transform.localScale = Vector3.one * Mathf.Lerp(0.92f, 1.12f, pulse);
            return;
        }

        float activeProgress = Mathf.Clamp01(timer / activeDuration);
        float flash = Mathf.Lerp(0.55f, 1f, pulse);
        Color active = Color.Lerp(baseColor, Color.white, flash * 0.38f);
        active.a = Mathf.Lerp(0.92f, 0.34f, activeProgress);
        spriteRenderer.color = active;
        transform.localScale = Vector3.one * Mathf.Lerp(1.05f, 1.2f, pulse);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!isActive || other == null)
        {
            return;
        }

        PlayerController hitPlayer = other.GetComponent<PlayerController>();
        if (hitPlayer == null)
        {
            return;
        }

        hitPlayer.ApplyMovementSlow(playerSlowMultiplier, playerSlowDuration);

        if (enemy != null && enemyBoostCooldownTimer <= 0f)
        {
            enemy.ApplyExternalSpeedModifier(enemyBoostMultiplier, enemyBoostDuration);
            enemyBoostCooldownTimer = enemyBoostCooldown;
        }
    }

    private void EnsureLine()
    {
        if (tetherLine != null)
        {
            return;
        }

        tetherLine = gameObject.AddComponent<LineRenderer>();
        tetherLine.positionCount = 2;
        tetherLine.useWorldSpace = true;
        tetherLine.alignment = LineAlignment.TransformZ;
        tetherLine.textureMode = LineTextureMode.Stretch;
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Hidden/Internal-Colored");
        }
        if (shader != null)
        {
            tetherLine.material = new Material(shader);
        }
        tetherLine.startWidth = 0.05f;
        tetherLine.endWidth = 0.05f;
        tetherLine.sortingOrder = 6;
    }

    private void UpdateTether()
    {
        if (tetherLine == null)
        {
            return;
        }

        if (enemy == null)
        {
            tetherLine.enabled = false;
            return;
        }

        tetherLine.enabled = true;
        Vector3 from = transform.position;
        Vector3 to = enemy.transform.position;
        tetherLine.SetPosition(0, from);
        tetherLine.SetPosition(1, to);

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Max(0.1f, activeLineSpeed));
        Color lineColor = Color.Lerp(baseColor, Color.white, isActive ? (0.35f + pulse * 0.4f) : 0.18f);
        lineColor.a = isActive ? 0.6f : 0.24f;
        tetherLine.startColor = lineColor;
        tetherLine.endColor = lineColor;
        float width = isActive ? Mathf.Lerp(0.04f, 0.07f, pulse) : 0.03f;
        tetherLine.startWidth = width;
        tetherLine.endWidth = width;
    }
}

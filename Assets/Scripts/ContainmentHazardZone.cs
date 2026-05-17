using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class ContainmentHazardZone : MonoBehaviour
{
    [SerializeField] private float pulseSpeed = 5.2f;

    private GameManager gameManager;
    private PlayerController player;
    private SpriteRenderer spriteRenderer;
    private CircleCollider2D circleCollider;
    private Color baseColor = new Color(1f, 0.42f, 0.56f, 0.9f);
    private float telegraphDuration = 0.9f;
    private float activeDuration = 1.8f;
    private float timer;
    private bool isActive;
    private bool resolvedHit;

    public void Configure(
        GameManager manager,
        PlayerController playerController,
        float radius,
        float telegraphSeconds,
        float activeSeconds,
        Color color)
    {
        gameManager = manager;
        player = playerController;
        telegraphDuration = Mathf.Max(0.1f, telegraphSeconds);
        activeDuration = Mathf.Max(0.1f, activeSeconds);
        baseColor = color;

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
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        circleCollider = GetComponent<CircleCollider2D>();
        circleCollider.isTrigger = true;
    }

    private void Update()
    {
        if (gameManager != null && (!gameManager.IsRunActive || gameManager.IsGameOver))
        {
            return;
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
            float alpha = Mathf.Lerp(0.14f, 0.45f, charge) + pulse * 0.05f;
            Color c = baseColor;
            c.a = alpha;
            spriteRenderer.color = c;
            transform.localScale = Vector3.one * Mathf.Lerp(0.94f, 1.04f, pulse);
            return;
        }

        float activeProgress = Mathf.Clamp01(timer / activeDuration);
        float flash = Mathf.Lerp(0.55f, 1f, pulse);
        Color active = Color.Lerp(baseColor, Color.white, flash * 0.38f);
        active.a = Mathf.Lerp(0.75f, 0.24f, activeProgress);
        spriteRenderer.color = active;
        transform.localScale = Vector3.one * Mathf.Lerp(1.02f, 1.15f, pulse);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!isActive || resolvedHit || other == null)
        {
            return;
        }

        PlayerController hitPlayer = other.GetComponent<PlayerController>();
        if (hitPlayer == null)
        {
            return;
        }

        if (hitPlayer.TryAbsorbHit())
        {
            resolvedHit = true;
            Destroy(gameObject);
            return;
        }

        if (gameManager == null)
        {
            gameManager = FindAnyObjectByType<GameManager>();
        }

        if (gameManager != null && gameManager.IsRunActive && !gameManager.IsGameOver)
        {
            gameManager.TriggerGameOver();
        }
    }
}


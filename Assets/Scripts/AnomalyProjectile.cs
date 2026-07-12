using UnityEngine;

[RequireComponent(typeof(Collider2D))]
// Proyectil compartido por varios estados: viaja, rebota o se refleja y siempre valida los bordes antes de dañar.
public class AnomalyProjectile : MonoBehaviour
{
    // Proyectil de la anomalia: puede ser reflejado por el parry o derrotar al jugador si no tiene defensa.
    [SerializeField] private float speed = 8.5f;
    [SerializeField] private float lifetime = 3.5f;
    [SerializeField] private LayerMask obstacleMask = ~0;
    [SerializeField] private Color reflectedColor = new Color(0.38f, 1f, 0.66f, 1f);

    private Vector2 direction = Vector2.right;
    private GameManager gameManager;
    private float lifeTimer;
    private bool reflected;
    private SpriteRenderer spriteRenderer;

    public Vector2 CurrentVelocity => direction * speed;

    public void Configure(Vector2 travelDirection, float projectileSpeed, float projectileLifetime, LayerMask obstacles, GameManager manager)
    {
        direction = travelDirection.sqrMagnitude > 0.0001f ? travelDirection.normalized : Vector2.right;
        speed = Mathf.Max(0.1f, projectileSpeed);
        lifetime = Mathf.Max(0.1f, projectileLifetime);
        obstacleMask = obstacles;
        gameManager = manager;
    }

    public bool TryReflectByParry(Vector2 parryOrigin)
    {
        if (reflected)
        {
            return false;
        }

        Vector2 reflectedDirection = ((Vector2)transform.position - parryOrigin);
        Reflect(reflectedDirection.sqrMagnitude > 0.0001f ? reflectedDirection.normalized : -direction);
        return true;
    }

    public void ApplyExternalDisplacement(Vector2 delta, float steeringStrength = 0f)
    {
        if (delta.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        transform.position += (Vector3)delta;
        if (steeringStrength > 0f)
        {
            Vector2 steered = Vector2.Lerp(direction, delta.normalized, Mathf.Clamp01(steeringStrength));
            if (steered.sqrMagnitude > 0.0001f)
            {
                direction = steered.normalized;
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
            }
        }
    }

    public void ApplyOrbitalAcceleration(Vector2 acceleration, float deltaTime, float maxTurnRate)
    {
        if (acceleration.sqrMagnitude <= 0.000001f || deltaTime <= 0f)
        {
            return;
        }

        Vector2 currentVelocity = direction * speed;
        Vector2 desiredVelocity = currentVelocity + acceleration * deltaTime;
        if (desiredVelocity.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        float maxRadians = Mathf.Max(1f, maxTurnRate) * Mathf.Deg2Rad * deltaTime;
        direction = Vector3.RotateTowards(direction, desiredVelocity.normalized, maxRadians, 0f).normalized;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
    }

    private void Awake()
    {
        reflectedColor = GlitchUiPalette.Success;
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (gameManager != null && (!gameManager.IsRunActive || gameManager.IsGameOver))
        {
            Destroy(gameObject);
            return;
        }

        float dt = Time.deltaTime;
        transform.position += (Vector3)(direction * speed * dt);

        lifeTimer += dt;
        if (lifeTimer >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        // Respaldo por si algun colisionador no dispara OnTriggerEnter2D correctamente.
        Collider2D hit = Physics2D.OverlapCircle(transform.position, 0.08f, obstacleMask);
        if (hit != null && hit.GetComponent<PlayerController>() == null && hit.GetComponent<EnemyController>() == null && !hit.isTrigger)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        PlayerController player = other.GetComponent<PlayerController>();
        if (player != null)
        {
            if (!reflected && player.TryParryHit(transform.position, out Vector2 parryDirection))
            {
                Reflect(parryDirection);
                return;
            }

            if (player.TryAbsorbHit())
            {
                Destroy(gameObject);
                return;
            }

            if (gameManager == null)
            {
                gameManager = FindAnyObjectByType<GameManager>();
            }

            if (gameManager != null && gameManager.IsRunActive && !gameManager.IsGameOver)
            {
                gameManager.RequestPlayerDefeat(player);
            }

            Destroy(gameObject);
            return;
        }

        EnemyController enemy = other.GetComponent<EnemyController>();
        if (enemy != null)
        {
            if (reflected)
            {
                enemy.ApplyParryImpact(transform.position, direction);
                Destroy(gameObject);
            }

            return;
        }

        if (!other.isTrigger)
        {
            Destroy(gameObject);
        }
    }

    private void Reflect(Vector2 reflectedDirection)
    {
        reflected = true;
        direction = reflectedDirection.sqrMagnitude > 0.0001f ? reflectedDirection.normalized : -direction;
        lifeTimer = 0f;
        speed *= 1.18f;

        if (spriteRenderer != null)
        {
            spriteRenderer.color = reflectedColor;
            spriteRenderer.sortingOrder = 13;
        }

        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        GlitchAudioManager.PlayProjectileReflect(transform.position);
    }
}

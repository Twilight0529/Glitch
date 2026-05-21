using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class AnomalyProjectile : MonoBehaviour
{
    [SerializeField] private float speed = 8.5f;
    [SerializeField] private float lifetime = 3.5f;
    [SerializeField] private LayerMask obstacleMask = ~0;

    private Vector2 direction = Vector2.right;
    private GameManager gameManager;
    private float lifeTimer;

    public void Configure(Vector2 travelDirection, float projectileSpeed, float projectileLifetime, LayerMask obstacles, GameManager manager)
    {
        direction = travelDirection.sqrMagnitude > 0.0001f ? travelDirection.normalized : Vector2.right;
        speed = Mathf.Max(0.1f, projectileSpeed);
        lifetime = Mathf.Max(0.1f, projectileLifetime);
        obstacleMask = obstacles;
        gameManager = manager;
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

        // Fail-safe against missing trigger callbacks on some collider setups.
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

        if (other.GetComponent<EnemyController>() != null)
        {
            return;
        }

        if (!other.isTrigger)
        {
            Destroy(gameObject);
        }
    }
}

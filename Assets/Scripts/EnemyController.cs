using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemyController : MonoBehaviour
{
    private enum BehaviorPattern
    {
        DirectChase,
        PredictiveIntercept,
        ErraticBurst
    }

    [Header("References")]
    [SerializeField] private PlayerController player;
    [SerializeField] private GameManager gameManager;

    [Header("Base Movement")]
    [SerializeField] private float baseMoveSpeed = 3.25f;

    [Header("Erratic Pattern")]
    [SerializeField] private float erraticDirectionRefresh = 0.35f;
    [SerializeField] private float erraticOffsetRadius = 2.2f;
    [SerializeField] private float erraticBurstMultiplier = 1.35f;

    private Rigidbody2D rb;
    private BehaviorPattern currentPattern;
    private float behaviorChangeTimer;
    private float erraticRefreshTimer;
    private Vector2 erraticTarget;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
    }

    private void Start()
    {
        if (player == null)
        {
            player = FindFirstObjectByType<PlayerController>();
        }

        if (gameManager == null)
        {
            gameManager = FindFirstObjectByType<GameManager>();
        }

        PickNextBehavior();
    }

    private void Update()
    {
        if (gameManager == null || player == null || gameManager.IsGameOver)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        HandleBehaviorSwitch();
        MoveByCurrentPattern();
    }

    private void HandleBehaviorSwitch()
    {
        behaviorChangeTimer += Time.deltaTime;
        float interval = gameManager.CurrentBehaviorChangeInterval;

        if (behaviorChangeTimer >= interval)
        {
            behaviorChangeTimer = 0f;
            PickNextBehavior();
        }
    }

    private void PickNextBehavior()
    {
        currentPattern = (BehaviorPattern)Random.Range(0, 3);

        if (currentPattern == BehaviorPattern.ErraticBurst)
        {
            erraticRefreshTimer = 0f;
            RefreshErraticTarget();
        }
    }

    private void MoveByCurrentPattern()
    {
        Vector2 enemyPosition = rb.position;
        Vector2 playerPosition = player.GetPosition();
        float speed = baseMoveSpeed * gameManager.DifficultyMultiplier;

        Vector2 direction = currentPattern switch
        {
            BehaviorPattern.DirectChase => GetDirectDirection(enemyPosition, playerPosition),
            BehaviorPattern.PredictiveIntercept => GetPredictiveDirection(enemyPosition, playerPosition, speed),
            BehaviorPattern.ErraticBurst => GetErraticDirection(enemyPosition, playerPosition, speed, out speed),
            _ => Vector2.zero
        };

        rb.linearVelocity = direction * speed;
    }

    private Vector2 GetDirectDirection(Vector2 enemyPosition, Vector2 playerPosition)
    {
        return (playerPosition - enemyPosition).normalized;
    }

    private Vector2 GetPredictiveDirection(Vector2 enemyPosition, Vector2 playerPosition, float enemySpeed)
    {
        Vector2 playerVelocity = player.CurrentVelocity;
        float distance = Vector2.Distance(enemyPosition, playerPosition);
        float leadTime = distance / Mathf.Max(enemySpeed, 0.01f);
        leadTime = Mathf.Clamp(leadTime, 0.1f, 1.2f);

        Vector2 predictedPosition = playerPosition + playerVelocity * leadTime;
        return (predictedPosition - enemyPosition).normalized;
    }

    private Vector2 GetErraticDirection(Vector2 enemyPosition, Vector2 playerPosition, float baseSpeed, out float speed)
    {
        erraticRefreshTimer += Time.deltaTime;

        if (erraticRefreshTimer >= erraticDirectionRefresh)
        {
            erraticRefreshTimer = 0f;
            RefreshErraticTarget();
        }

        speed = baseSpeed * erraticBurstMultiplier;
        Vector2 direction = (erraticTarget - enemyPosition);

        if (direction.sqrMagnitude < 0.03f)
        {
            RefreshErraticTarget();
            direction = erraticTarget - enemyPosition;
        }

        if (direction.sqrMagnitude < 0.001f)
        {
            direction = playerPosition - enemyPosition;
        }

        return direction.normalized;
    }

    private void RefreshErraticTarget()
    {
        if (player == null)
        {
            erraticTarget = rb.position;
            return;
        }

        Vector2 randomOffset = Random.insideUnitCircle * erraticOffsetRadius;
        erraticTarget = player.GetPosition() + randomOffset;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.collider.GetComponent<PlayerController>() != null)
        {
            gameManager?.TriggerGameOver();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<PlayerController>() != null)
        {
            gameManager?.TriggerGameOver();
        }
    }
}

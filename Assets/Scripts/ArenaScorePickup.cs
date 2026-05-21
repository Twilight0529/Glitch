using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class ArenaScorePickup : MonoBehaviour
{
    [SerializeField] private float bobAmplitude = 0.08f;
    [SerializeField] private float bobSpeed = 4.5f;
    [SerializeField] private float pulseSpeed = 5.4f;
    [SerializeField] private float baseScale = 1f;

    private ArenaChaosDirector owner;
    private GameManager gameManager;
    private float lifeTimer;
    private float lifetime;
    private int scoreValue;
    private Vector3 basePosition;
    private SpriteRenderer spriteRenderer;

    public int ScoreValue => scoreValue;

    public void Configure(ArenaChaosDirector ownerController, GameManager manager, int points, float lifeSeconds)
    {
        owner = ownerController;
        gameManager = manager;
        scoreValue = Mathf.Max(1, points);
        lifetime = Mathf.Max(0.5f, lifeSeconds);
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        basePosition = transform.position;
        ApplyVisual();
    }

    private void Update()
    {
        lifeTimer += Time.deltaTime;
        if (lifeTimer >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
        transform.position = new Vector3(basePosition.x, basePosition.y + bob, basePosition.z);

        float pulse = 0.9f + 0.12f * (0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed));
        transform.localScale = Vector3.one * (baseScale * pulse);
    }

    private void ApplyVisual()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        spriteRenderer.color = new Color(0.97f, 0.97f, 0.98f, 0.96f);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        PlayerController player = other.GetComponent<PlayerController>();
        if (player == null)
        {
            return;
        }

        if (gameManager != null)
        {
            gameManager.AddScore(scoreValue);
        }

        owner?.NotifyScorePickupConsumed(this);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        owner?.NotifyScorePickupDestroyed(this);
    }
}

using UnityEngine;

[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class ArenaPowerupPickup : MonoBehaviour
{
    public enum PickupKind
    {
        SpeedBurst,
        Shield
    }

    [SerializeField] private float bobAmplitude = 0.14f;
    [SerializeField] private float bobSpeed = 3.2f;
    [SerializeField] private float spinSpeed = 75f;

    private ArenaChaosDirector owner;
    private PickupKind kind;
    private float lifeTimer;
    private float lifetime;
    private float speedMultiplier;
    private float speedDuration;
    private float shieldDuration;
    private Vector3 basePosition;
    private SpriteRenderer spriteRenderer;

    public PickupKind Kind => kind;

    public void Configure(
        ArenaChaosDirector ownerController,
        PickupKind pickupKind,
        float pickupLifetime,
        float boostMultiplier,
        float boostDuration,
        float shieldSeconds)
    {
        owner = ownerController;
        kind = pickupKind;
        lifetime = Mathf.Max(0.5f, pickupLifetime);
        speedMultiplier = Mathf.Max(1f, boostMultiplier);
        speedDuration = Mathf.Max(0.1f, boostDuration);
        shieldDuration = Mathf.Max(0.1f, shieldSeconds);
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        basePosition = transform.position;
    }

    private void Start()
    {
        ApplyVisualTheme();
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
        transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime, Space.Self);
    }

    private void ApplyVisualTheme()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        switch (kind)
        {
            case PickupKind.Shield:
                spriteRenderer.color = new Color(1f, 0.66f, 0.86f, 0.95f);
                break;
            default:
                spriteRenderer.color = new Color(0.46f, 0.96f, 1f, 0.95f);
                break;
        }
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

        if (kind == PickupKind.Shield)
        {
            player.ApplyShield(shieldDuration);
        }
        else
        {
            player.ApplySpeedBoost(speedMultiplier, speedDuration);
        }

        owner?.NotifyPickupConsumed(this);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        owner?.NotifyPickupDestroyed(this);
    }
}


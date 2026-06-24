using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class SplitAnomalyCloneController : MonoBehaviour
{
    // Clon temporal de la anomalia: persigue/flanquea al jugador y luego se fusiona con su anomalia original.
    private PlayerController player;
    private GameManager gameManager;
    private EnemyController owner;
    private LayerMask obstacleMask = ~0;

    private Rigidbody2D rb;
    private Collider2D ownCollider;

    private float moveSpeed = 3f;
    private float velocityResponsiveness = 20f;
    private float sideOffset = 1.1f;
    private float mergeSpeed = 7.5f;
    private int sideSign = 1;
    private float parryStunDuration = 0.3f;
    private float parryKnockbackDuration = 0.14f;
    private float parryKnockbackSpeedMultiplier = 3.2f;

    private bool splitStateActive;
    private bool splitMergeActive;
    private float firewallStunTimer;
    private float firewallKnockbackTimer;
    private Vector2 lastMoveDirection = Vector2.right;

    public Vector2 GetCurrentPosition()
    {
        return rb != null ? rb.position : (Vector2)transform.position;
    }

    public Vector2 CurrentVelocity => rb != null ? rb.linearVelocity : Vector2.zero;

    public void ApplyExternalDisplacement(Vector2 delta)
    {
        if (rb == null || delta.sqrMagnitude <= 0.000001f)
        {
            return;
        }

        Vector2 target = rb.position + delta;
        rb.position = target;
        transform.position = new Vector3(target.x, target.y, transform.position.z);
    }

    public void ConfigureMovement(float speed, float responsiveness, float flankOffset, float mergeMoveSpeed, int sign)
    {
        moveSpeed = Mathf.Max(0.1f, speed);
        velocityResponsiveness = Mathf.Max(0.1f, responsiveness);
        sideOffset = Mathf.Max(0f, flankOffset);
        mergeSpeed = Mathf.Max(0.1f, mergeMoveSpeed);
        sideSign = sign == 0 ? 1 : (sign > 0 ? 1 : -1);
    }

    public void ConfigureRuntime(PlayerController playerRef, GameManager managerRef, EnemyController ownerRef, LayerMask obstacles)
    {
        player = playerRef;
        gameManager = managerRef;
        owner = ownerRef;
        obstacleMask = obstacles;
    }

    public void SetSplitState(bool active, bool merging)
    {
        splitStateActive = active;
        splitMergeActive = merging;
    }

    public void ApplyParryImpact(Vector2 impactPosition, Vector2 pushDirection)
    {
        Vector2 direction = pushDirection.sqrMagnitude > 0.0001f
            ? pushDirection.normalized
            : ((Vector2)transform.position - impactPosition).normalized;

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = -lastMoveDirection;
        }

        firewallStunTimer = Mathf.Max(firewallStunTimer, Mathf.Max(0.04f, parryStunDuration));
        firewallKnockbackTimer = Mathf.Max(firewallKnockbackTimer, Mathf.Max(0.02f, parryKnockbackDuration));

        if (rb != null)
        {
            rb.linearVelocity = direction.normalized * Mathf.Max(0.1f, moveSpeed * parryKnockbackSpeedMultiplier);
        }

        GlitchAudioManager.PlayEnemyParried(transform.position);
    }

    public void ApplyFirewallBurst(Vector2 burstOrigin, float burstRadius, float stunSeconds, float knockbackMultiplier)
    {
        Vector2 direction = ((Vector2)transform.position - burstOrigin);
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = -lastMoveDirection;
        }

        float distance = Vector2.Distance(transform.position, burstOrigin);
        float radius = Mathf.Max(0.5f, burstRadius);
        float force = Mathf.Lerp(1.15f, 0.66f, Mathf.Clamp01(distance / radius));
        firewallStunTimer = Mathf.Max(firewallStunTimer, Mathf.Max(0.08f, stunSeconds));
        firewallKnockbackTimer = Mathf.Max(firewallKnockbackTimer, Mathf.Max(0.08f, stunSeconds * 0.34f));

        if (rb != null)
        {
            rb.linearVelocity = direction.normalized * Mathf.Max(0.1f, moveSpeed * 2.35f * Mathf.Max(0.1f, knockbackMultiplier) * force);
        }

        GlitchAudioManager.PlayEnemyParried(transform.position);
    }

    public void ApplyContainmentLock(float seconds)
    {
        firewallStunTimer = Mathf.Max(firewallStunTimer, Mathf.Max(0.08f, seconds));
        firewallKnockbackTimer = 0f;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        GlitchAudioManager.PlayEnemyParried(transform.position);
    }

    public void AbsorbIntoBreach(Vector2 breachPosition)
    {
        splitStateActive = false;
        splitMergeActive = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        if (ownCollider != null)
        {
            ownCollider.enabled = false;
        }

        transform.position = breachPosition;

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = false;
            }
        }

        Destroy(gameObject);
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        ownCollider = GetComponent<Collider2D>();
    }

    private void Update()
    {
        if (owner == null)
        {
            Destroy(gameObject);
            return;
        }

        if (!owner.CanDamagePlayer())
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        if (TickFirewallStun())
        {
            return;
        }

        splitStateActive = owner.IsSplitStateActive();
        splitMergeActive = owner.IsSplitMergeInProgress();

        if (splitMergeActive)
        {
            TickMerge();
            return;
        }

        if (!splitStateActive)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        TickChase();
    }

    private void TickChase()
    {
        Vector2 target;
        if (owner != null)
        {
            target = owner.GetCurrentTargetForSplitClone();
        }
        else if (player != null)
        {
            Vector2 toPlayer = player.GetPosition() - (Vector2)transform.position;
            Vector2 side = toPlayer.sqrMagnitude > 0.0001f ? new Vector2(-toPlayer.y, toPlayer.x).normalized * sideSign : Vector2.right * sideSign;
            target = player.GetPosition() + side * sideOffset;
        }
        else
        {
            target = transform.position;
        }

        Vector2 desiredDirection = target - rb.position;
        if (desiredDirection.sqrMagnitude < 0.0001f)
        {
            desiredDirection = lastMoveDirection;
        }
        else
        {
            desiredDirection.Normalize();
            lastMoveDirection = desiredDirection;
        }

        desiredDirection = ApplySimpleRepulsion(desiredDirection);
        Vector2 desiredVelocity = desiredDirection * moveSpeed;
        rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, desiredVelocity, velocityResponsiveness * Time.deltaTime);
    }

    private void TickMerge()
    {
        Vector2 ownerPos = owner != null ? owner.GetCurrentPosition() : (Vector2)transform.position;
        Vector2 toOwner = ownerPos - rb.position;
        float distance = toOwner.magnitude;
        float mergeThreshold = owner != null ? owner.GetSplitMergeDistanceThreshold() : 0.18f;

        if (distance <= mergeThreshold)
        {
            owner?.NotifySplitCloneMerged(this);
            Destroy(gameObject);
            return;
        }

        Vector2 dir = distance > 0.001f ? toOwner / distance : Vector2.zero;
        rb.linearVelocity = dir * mergeSpeed;
    }

    private bool TickFirewallStun()
    {
        if (firewallStunTimer <= 0f)
        {
            return false;
        }

        firewallStunTimer -= Time.deltaTime;
        firewallKnockbackTimer -= Time.deltaTime;
        if (firewallKnockbackTimer <= 0f)
        {
            rb.linearVelocity = Vector2.MoveTowards(rb.linearVelocity, Vector2.zero, velocityResponsiveness * 1.8f * Time.deltaTime);
        }

        return true;
    }

    private Vector2 ApplySimpleRepulsion(Vector2 desiredDirection)
    {
        const float probeDistance = 0.9f;
        const float raySpread = 85f;
        const int rayCount = 5;
        const float repulsionStrength = 0.9f;
        const float probeRadius = 0.18f;

        Vector2 baseDir = desiredDirection.sqrMagnitude > 0.0001f ? desiredDirection.normalized : lastMoveDirection;
        Vector2 repulsion = Vector2.zero;

        for (int i = 0; i < rayCount; i++)
        {
            float t = rayCount == 1 ? 0.5f : i / (float)(rayCount - 1);
            float angle = Mathf.Lerp(-raySpread * 0.5f, raySpread * 0.5f, t);
            Vector2 dir = Rotate(baseDir, angle);

            RaycastHit2D hit = Physics2D.CircleCast(rb.position, probeRadius, dir, probeDistance, obstacleMask);
            if (!IsBlockingCollider(hit.collider))
            {
                continue;
            }

            float proximity = 1f - (hit.distance / probeDistance);
            Vector2 away = (rb.position - hit.point).normalized;
            repulsion += away * proximity;
        }

        if (repulsion.sqrMagnitude < 0.0001f)
        {
            return baseDir;
        }

        return (baseDir + repulsion.normalized * repulsionStrength).normalized;
    }

    private bool IsBlockingCollider(Collider2D col)
    {
        if (col == null || col == ownCollider || col.isTrigger)
        {
            return false;
        }

        if (col.GetComponent<PlayerController>() != null || col.GetComponent<EnemyController>() != null || col.GetComponent<SplitAnomalyCloneController>() != null)
        {
            return false;
        }

        return true;
    }

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        float s = Mathf.Sin(r);
        float c = Mathf.Cos(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (owner == null || !owner.CanDamagePlayer())
        {
            return;
        }

        if (collision.collider.GetComponent<PlayerController>() != null)
        {
            PlayerController hitPlayer = collision.collider.GetComponent<PlayerController>();
            if (hitPlayer != null && hitPlayer.TryParryHit(rb.position, out Vector2 parryDirection))
            {
                ApplyParryImpact(hitPlayer.GetPosition(), parryDirection);
                return;
            }

            if (hitPlayer != null && hitPlayer.TryAbsorbHit())
            {
                return;
            }

            gameManager?.RequestPlayerDefeat(hitPlayer);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (owner == null || !owner.CanDamagePlayer())
        {
            return;
        }

        if (other.GetComponent<PlayerController>() != null)
        {
            PlayerController hitPlayer = other.GetComponent<PlayerController>();
            if (hitPlayer != null && hitPlayer.TryParryHit(rb.position, out Vector2 parryDirection))
            {
                ApplyParryImpact(hitPlayer.GetPosition(), parryDirection);
                return;
            }

            if (hitPlayer != null && hitPlayer.TryAbsorbHit())
            {
                return;
            }

            gameManager?.RequestPlayerDefeat(hitPlayer);
        }
    }
}

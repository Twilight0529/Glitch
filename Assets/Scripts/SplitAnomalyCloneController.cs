using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class SplitAnomalyCloneController : MonoBehaviour
{
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

    private bool splitStateActive;
    private bool splitMergeActive;
    private Vector2 lastMoveDirection = Vector2.right;

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

        if (distance <= 0.14f)
        {
            owner?.NotifySplitCloneMerged(this);
            Destroy(gameObject);
            return;
        }

        Vector2 dir = distance > 0.001f ? toOwner / distance : Vector2.zero;
        rb.linearVelocity = dir * mergeSpeed;
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
            gameManager?.TriggerGameOver();
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
            gameManager?.TriggerGameOver();
        }
    }
}

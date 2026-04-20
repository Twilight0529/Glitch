using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5.5f;

    private Rigidbody2D rb;
    private Vector2 moveInput;

    public Vector2 CurrentVelocity => rb != null ? rb.linearVelocity : Vector2.zero;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
    }

    private void Update()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        // 4-directions priority: keeps movement cardinal and readable under pressure.
        if (Mathf.Abs(horizontal) > 0f)
        {
            vertical = 0f;
        }

        moveInput = new Vector2(horizontal, vertical).normalized;
        rb.linearVelocity = moveInput * moveSpeed;
    }

    public Vector2 GetPosition()
    {
        return rb.position;
    }
}

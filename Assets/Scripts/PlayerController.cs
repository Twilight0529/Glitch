using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

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
        Vector2 rawInput = ReadMoveInput();
        float horizontal = rawInput.x;
        float vertical = rawInput.y;

        // 4-directions priority: keeps movement cardinal and readable under pressure.
        if (Mathf.Abs(horizontal) > 0f)
        {
            vertical = 0f;
        }

        moveInput = new Vector2(horizontal, vertical).normalized;
        rb.linearVelocity = moveInput * moveSpeed;
    }

    private static Vector2 ReadMoveInput()
    {
#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            float horizontal = 0f;
            float vertical = 0f;

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) horizontal -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) horizontal += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) vertical -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) vertical += 1f;

            return new Vector2(horizontal, vertical);
        }
#endif
        return new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
    }

    public Vector2 GetPosition()
    {
        return rb.position;
    }
}

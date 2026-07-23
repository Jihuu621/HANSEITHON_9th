using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float jumpForce = 7f;
    public LayerMask groundLayer;

    [Header("Footsteps")]
    [SerializeField, Min(0.05f)] private float footstepInterval = 0.32f;
    [SerializeField, Min(0f)] private float minimumFootstepSpeed = 0.1f;

    private Rigidbody2D rb;
    private CapsuleCollider2D col;
    private SpriteRenderer spriteRenderer;
    private Animator animator;

    private bool isJumping;
    private float nextFootstepTime;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<CapsuleCollider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        Move();
        Jump();
        UpdateAnimations();
        CheckLanding();
        PlayFootsteps();
    }

    void Move()
    {
        float moveInput = 0f;

        if (Input.GetKey(KeyCode.A))
        {
            moveInput = -1f;
            spriteRenderer.flipX = false;
        }
        else if (Input.GetKey(KeyCode.D))
        {
            moveInput = 1f;
            spriteRenderer.flipX = true;
        }

        rb.linearVelocity = new Vector2(moveInput * moveSpeed, rb.linearVelocity.y);
    }

    void Jump()
    {
        if ((Input.GetKeyDown(KeyCode.W) && IsGrounded()))
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            isJumping = true;
        }
    }

    

    bool IsGrounded()
    {
        float castDistance = 0.1f;
        Bounds bounds = col.bounds;

        Vector2 center = bounds.center;
        Vector2 left = new Vector2(bounds.min.x + 0.05f, bounds.min.y);
        Vector2 right = new Vector2(bounds.max.x - 0.05f, bounds.min.y);

        RaycastHit2D centerHit = Physics2D.Raycast(center, Vector2.down, castDistance, groundLayer);
        Debug.DrawLine(center, center + Vector2.down * castDistance, Color.green);

        RaycastHit2D leftHit = Physics2D.Raycast(left, Vector2.down, castDistance, groundLayer);
        Debug.DrawLine(left, left + Vector2.down * castDistance, Color.red);

        RaycastHit2D rightHit = Physics2D.Raycast(right, Vector2.down, castDistance, groundLayer);
        Debug.DrawLine(right, right + Vector2.down * castDistance, Color.blue);

        return centerHit.collider != null || leftHit.collider != null || rightHit.collider != null;
    }




    private void PlayFootsteps()
    {
        if (rb == null || Time.time < nextFootstepTime)
            return;

        bool walkingOnGround = IsGrounded() && Mathf.Abs(rb.linearVelocity.x) >= minimumFootstepSpeed;
        if (!walkingOnGround)
        {
            nextFootstepTime = Time.time;
            return;
        }

        RuntimeSfx.PlayFootstep();
        nextFootstepTime = Time.time + footstepInterval;
    }
    void UpdateAnimations()
    {
        bool grounded = IsGrounded();

        animator.SetFloat("Speed", Mathf.Abs(rb.linearVelocity.x));
        animator.SetBool("Grounded", grounded);
        animator.SetFloat("YVelocity", rb.linearVelocity.y);
    }

    void CheckLanding()
    {
        if (isJumping && IsGrounded() && rb.linearVelocity.y <= 0f)
        {
            isJumping = false;
        }
    }

}
using UnityEngine;

/// <summary>
/// Điều khiển chuyển động vật lý của Player (di chuyển, nhảy, crouch).
/// Yêu cầu: Rigidbody2D + Collider2D trên GameObject.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerInputHandler))]
[RequireComponent(typeof(PlayerStats))]
public class PlayerController : MonoBehaviour
{
    // ────────────────────────────────────────────────────────────────
    [Header("=== KIỂM TRA MẶT ĐẤT ===")]
    [SerializeField] private Transform groundCheck;         // Empty GameObject ở dưới chân
    [SerializeField] private float     groundCheckRadius = 0.15f;
    [SerializeField] private LayerMask groundLayer;

    [Header("=== THAM SỐ NHẢY ===")]
    [SerializeField] private float fallMultiplier     = 2.5f;  // rơi nhanh hơn
    [SerializeField] private float lowJumpMultiplier  = 2f;    // nhảy thấp khi nhả phím sớm
    [SerializeField] private int   maxJumpCount       = 2;     // cho phép double-jump

    [Header("=== THAM SỐ CROUCH ===")]
    [SerializeField] private float crouchSpeedMultiplier = 0.5f;
    [SerializeField] private Vector2 crouchColliderOffset = new Vector2(0, -0.25f);
    [SerializeField] private Vector2 crouchColliderSize   = new Vector2(0.8f, 1f);

    // ────────────────────────────────────────────────────────────────
    // Components
    private Rigidbody2D       _rb;
    private PlayerInputHandler _input;
    private PlayerStats        _stats;
    private CapsuleCollider2D  _col;        // hoặc BoxCollider2D

    // Collider gốc (để khôi phục khi đứng dậy)
    private Vector2 _defaultColliderOffset;
    private Vector2 _defaultColliderSize;

    // Trạng thái
    public bool IsGrounded   { get; private set; }
    public bool IsCrouching  { get; private set; }
    public bool IsFacingRight{ get; private set; } = true;

    // Trạng thái nhảy nâng cao
    private int   _jumpsLeft;
    private float _coyoteTimeCounter;
    private const float CoyoteTime = 0.15f;    // Thời gian cho phép nhảy sau khi rời khỏi mặt đất
    private float _jumpBufferCounter;
    private const float JumpBufferTime = 0.15f; // Thời gian đệm phím bấm trước khi chạm đất

    // ────────────────────────────────────────────────────────────────
    private void Awake()
    {
        _rb    = GetComponent<Rigidbody2D>();
        _input = GetComponent<PlayerInputHandler>();
        _stats = GetComponent<PlayerStats>();
        _col   = GetComponent<CapsuleCollider2D>();

        if (_col != null)
        {
            _defaultColliderOffset = _col.offset;
            _defaultColliderSize   = _col.size;
        }

        // Khởi tạo số lần nhảy ngay từ đầu
        _jumpsLeft = maxJumpCount;
    }

    // ────────────────────────────────────────────────────────────────
    private void Update()
    {
        if (_stats.isDead) return;

        HandleFacing();

        // Xử lý Jump Buffer trong Update (vì input thu thập theo frame đồ họa)
        if (_input.JumpPressed)
        {
            _jumpBufferCounter = JumpBufferTime;
        }
        else
        {
            _jumpBufferCounter -= Time.deltaTime;
        }
    }

    private void FixedUpdate()
    {
        if (_stats.isDead)
        {
            _rb.linearVelocity = Vector2.zero;
            return;
        }

        // CheckGrounded trước HandleJump để _jumpsLeft luôn được cập nhật kịp
        CheckGrounded();
        HandleMovement();
        HandleJump();
        HandleCrouch();
        ApplyBetterGravity();
    }

    // ────────────────────────────────────────────────────────────────
    /// <summary>Phát hiện mặt đất bằng OverlapCircle.</summary>
    private void CheckGrounded()
    {
        bool wasGrounded = IsGrounded;
        IsGrounded = Physics2D.OverlapCircle(
            groundCheck != null ? groundCheck.position : transform.position + Vector3.down * 0.5f,
            groundCheckRadius,
            groundLayer
        );

        // Xử lý Coyote Time và Reset số lần nhảy
        if (IsGrounded)
        {
            _coyoteTimeCounter = CoyoteTime;
            if (!wasGrounded)
            {
                _jumpsLeft = maxJumpCount;
            }
        }
        else
        {
            _coyoteTimeCounter -= Time.fixedDeltaTime;
        }
    }

    /// <summary>Di chuyển ngang.</summary>
    private void HandleMovement()
    {
        // Nếu đang tấn công hoặc đỡ đòn thì không di chuyển
        if (GetComponent<PlayerCombat>()?.IsActing == true) return;

        float horizontal = _input.MoveInput.x;
        float speed = _input.SprintHeld ? _stats.runSpeed : _stats.walkSpeed;

        if (IsCrouching)
            speed *= crouchSpeedMultiplier;

        _rb.linearVelocity = new Vector2(horizontal * speed, _rb.linearVelocity.y);
    }

    /// <summary>Nhảy (hỗ trợ double-jump, Coyote Time và Jump Buffer).</summary>
    private void HandleJump()
    {
        if (_jumpBufferCounter > 0f)
        {
            // Nhảy lần 1 bằng Coyote Time (khi vừa đi lệch khỏi mép vực hoặc ngay khi chạm đất)
            if (_coyoteTimeCounter > 0f && _jumpsLeft == maxJumpCount)
            {
                _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, _stats.jumpForce);
                _jumpsLeft--;
                _jumpBufferCounter = 0f;
                _coyoteTimeCounter = 0f;
            }
            // Nhảy lần 2 (Double Jump)
            else if (_jumpsLeft > 0 && !IsCrouching)
            {
                _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, _stats.jumpForce);
                _jumpsLeft--;
                _jumpBufferCounter = 0f;
            }
        }
    }

    /// <summary>Crouch: chỉ cho phép khi đứng trên mặt đất.</summary>
    private void HandleCrouch()
    {
        bool wantCrouch = _input.CrouchHeld && IsGrounded;

        if (wantCrouch == IsCrouching) return;

        IsCrouching = wantCrouch;

        if (_col == null) return;

        if (IsCrouching)
        {
            _col.offset = crouchColliderOffset;
            _col.size   = crouchColliderSize;
        }
        else
        {
            _col.offset = _defaultColliderOffset;
            _col.size   = _defaultColliderSize;
        }
    }

    /// <summary>Trọng lực tùy biến để cảm giác nhảy mượt hơn.</summary>
    private void ApplyBetterGravity()
    {
        if (_rb.linearVelocity.y < 0)
        {
            // Rơi xuống nhanh hơn
            _rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
        }
        else if (_rb.linearVelocity.y > 0 && !_input.JumpPressed)
        {
            // Nhảy thấp nếu nhả phím sớm
            _rb.linearVelocity += Vector2.up * Physics2D.gravity.y * (lowJumpMultiplier - 1) * Time.fixedDeltaTime;
        }
    }

    /// <summary>Lật sprite theo hướng di chuyển.</summary>
    private void HandleFacing()
    {
        float horizontal = _input.MoveInput.x;

        if (horizontal > 0.01f && !IsFacingRight)
            Flip();
        else if (horizontal < -0.01f && IsFacingRight)
            Flip();
    }

    private void Flip()
    {
        IsFacingRight = !IsFacingRight;
        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }

    // Gizmos debug
    private void OnDrawGizmosSelected()
    {
        Vector3 pos = groundCheck != null ? groundCheck.position : transform.position + Vector3.down * 0.5f;
        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(pos, groundCheckRadius);
    }
}

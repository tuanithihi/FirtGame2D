using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Xử lý tất cả input từ bàn phím / tay cầm cho Player.
/// Chỉ cần kéo file InputSystem_Actions.inputactions vào slot "Input Actions Asset".
/// Script tự tìm action theo tên trong code — không cần kéo từng action riêng lẻ.
/// </summary>
public class PlayerInputHandler : MonoBehaviour
{
    // ─── Chỉ kéo 1 file .inputactions vào đây ───────────────────────
    [Header("=== KÉO FILE .inputactions VÀO ĐÂY ===")]
    [SerializeField] private InputActionAsset inputActionsAsset;

    // ─── Dữ liệu input đọc mỗi frame ───────────────────────────────
    public Vector2 MoveInput     { get; private set; }
    public bool    JumpPressed   { get; private set; }   // chỉ true đúng 1 frame
    public bool    SprintHeld    { get; private set; }
    public bool    CrouchHeld    { get; private set; }
    public bool    AttackPressed { get; private set; }   // chỉ true đúng 1 frame
    public bool    DefendHeld    { get; private set; }

    // ─── Internal: Action references ────────────────────────────────
    private InputAction _move;
    private InputAction _jump;
    private InputAction _sprint;
    private InputAction _crouch;
    private InputAction _attack;
    private InputAction _interact;

    private bool _jumpBuffered;
    private bool _attackBuffered;

    // ================================================================
    private void Awake()
    {
        if (inputActionsAsset == null)
        {
            Debug.LogError("[PlayerInputHandler] Chưa gán InputActionAsset! Kéo file InputSystem_Actions.inputactions vào slot.");
            return;
        }

        // Tìm Action Map "Player" rồi lấy từng action theo tên
        var playerMap = inputActionsAsset.FindActionMap("Player", throwIfNotFound: true);

        _move     = playerMap.FindAction("Move",     throwIfNotFound: true);
        _jump     = playerMap.FindAction("Jump",     throwIfNotFound: true);
        _sprint   = playerMap.FindAction("Sprint",   throwIfNotFound: true);
        _crouch   = playerMap.FindAction("Crouch",   throwIfNotFound: true);
        _attack   = playerMap.FindAction("Attack",   throwIfNotFound: true);
        _interact = playerMap.FindAction("Interact", throwIfNotFound: true);
    }

    private void OnEnable()
    {
        if (_jump == null) return;

        _move.Enable();
        _jump.Enable();
        _sprint.Enable();
        _crouch.Enable();
        _attack.Enable();
        _interact.Enable();

        _jump.performed   += OnJumpPerformed;
        _attack.performed += OnAttackPerformed;
    }

    private void OnDisable()
    {
        if (_jump == null) return;

        _jump.performed   -= OnJumpPerformed;
        _attack.performed -= OnAttackPerformed;

        _move.Disable();
        _jump.Disable();
        _sprint.Disable();
        _crouch.Disable();
        _attack.Disable();
        _interact.Disable();
    }

    // ================================================================
    private void Update()
    {
        if (_move == null) return;

        MoveInput  = _move.ReadValue<Vector2>();
        SprintHeld = _sprint.IsPressed();
        CrouchHeld = _crouch.IsPressed();
        
        // Đọc phím R giữ trực tiếp từ bàn phím để đỡ đòn
        DefendHeld = Keyboard.current != null && Keyboard.current.rKey.isPressed;

        // Đọc các phím Q, W, E trực tiếp từ bàn phím làm chiêu thức
        if (Keyboard.current != null)
        {
            if (Keyboard.current.qKey.wasPressedThisFrame) _skill1Buffered = true; // Q -> Skill 1 (Đánh 1)
            if (Keyboard.current.wKey.wasPressedThisFrame) _skill2Buffered = true; // W -> Skill 2 (Đánh 2)
            if (Keyboard.current.eKey.wasPressedThisFrame) _skill3Buffered = true; // E -> Skill 3 (Đánh 3)
        }

        // Consume buffers — chỉ true đúng 1 frame
        JumpPressed   = _jumpBuffered;
        AttackPressed = _attackBuffered;
        Skill1Pressed = _skill1Buffered;
        Skill2Pressed = _skill2Buffered;
        Skill3Pressed = _skill3Buffered;

        _jumpBuffered   = false;
        _attackBuffered = false;
        _skill1Buffered = false;
        _skill2Buffered = false;
        _skill3Buffered = false;
    }

    // Dữ liệu phím tắt chiêu thức
    public bool Skill1Pressed { get; private set; }
    public bool Skill2Pressed { get; private set; }
    public bool Skill3Pressed { get; private set; }

    private bool _skill1Buffered;
    private bool _skill2Buffered;
    private bool _skill3Buffered;

    // ================================================================
    private void OnJumpPerformed(InputAction.CallbackContext ctx)   => _jumpBuffered   = true;
    private void OnAttackPerformed(InputAction.CallbackContext ctx) => _attackBuffered = true;
}

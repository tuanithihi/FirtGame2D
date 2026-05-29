using UnityEngine;

/// <summary>
/// Điều khiển Animator của Player.
/// Cập nhật tất cả parameters dựa trên trạng thái của các script khác.
///
/// Parameters trong Animator Controller (IDLE_0.controller):
///   - isRunning  : bool
///   - attack     : trigger  (dùng cho Đánh 1, Đánh 2, Đánh 3 qua int attackStep)
///
/// Script này cũng tự thêm các parameter cần thiết nếu chưa có trong controller.
/// </summary>
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(PlayerInputHandler))]
[RequireComponent(typeof(PlayerStats))]
public class PlayerAnimator : MonoBehaviour
{
    // ─── Hash tên parameter (tối ưu hiệu năng) ───
    private static readonly int HashIsRunning  = Animator.StringToHash("isRunning");
    private static readonly int HashAttack     = Animator.StringToHash("attack");
    private static readonly int HashAttackStep = Animator.StringToHash("attackStep");  // int: 1/2/3
    private static readonly int HashIsWalking  = Animator.StringToHash("isWalking");
    private static readonly int HashIsJumping  = Animator.StringToHash("isJumping");
    private static readonly int HashIsCrouching= Animator.StringToHash("isCrouching");
    private static readonly int HashIsDefending= Animator.StringToHash("isDefending");
    private static readonly int HashHurt       = Animator.StringToHash("hurt");
    private static readonly int HashDeath      = Animator.StringToHash("isDead");

    // ─── Components ───
    private Animator           _anim;
    private PlayerController   _controller;
    private PlayerInputHandler _input;
    private PlayerStats        _stats;

    // Tránh play Nhảy ngay frame đầu khi spawn
    private bool _hasLandedOnce = false;

    // ────────────────────────────────────────────────────────────────
    private void Awake()
    {
        _anim       = GetComponent<Animator>();
        _controller = GetComponent<PlayerController>();
        _input      = GetComponent<PlayerInputHandler>();
        _stats      = GetComponent<PlayerStats>();
    }

    // ────────────────────────────────────────────────────────────────
    private void Update()
    {
        if (_stats.isDead) return;

        UpdateLocomotion();
    }

    // ────────────────────────────────────────────────────────────────
    /// <summary>Cập nhật các animation di chuyển mỗi frame.</summary>
    private void UpdateLocomotion()
    {
        float horizontal = Mathf.Abs(_input.MoveInput.x);
        bool  isMoving   = horizontal > 0.05f;
        bool  isSprinting= isMoving && _input.SprintHeld;
        bool  isWalking  = isMoving && !_input.SprintHeld;

        // isRunning: dùng cho controller gốc (Đứng <-> Chạy)
        _anim.SetBool(HashIsRunning, isSprinting);

        // Đánh dấu đã chạm đất ít nhất 1 lần
        if (_controller.IsGrounded) _hasLandedOnce = true;

        // Các parameter bổ sung
        TrySetBool(HashIsWalking,   isWalking);
        // isJumping chỉ được bật sau khi đã chạm đất 1 lần (tránh bug spawn)
        TrySetBool(HashIsJumping,   _hasLandedOnce && !_controller.IsGrounded);
        TrySetBool(HashIsCrouching, _controller.IsCrouching);
    }

    // ────────────────────────────────────────────────────────────────
    // Gọi từ PlayerCombat

    /// <summary>Kích hoạt animation tấn công theo bước combo.</summary>
    public void TriggerAttack(int step)
    {
        _anim.ResetTrigger(HashAttack); // Xóa sạch Trigger cũ trước khi set mới
        _anim.SetInteger(HashAttackStep, step);
        _anim.SetTrigger(HashAttack);
    }

    /// <summary>Bật/tắt animation đỡ đòn.</summary>
    public void SetDefending(bool defending)
    {
        TrySetBool(HashIsDefending, defending);
    }

    /// <summary>Kích hoạt animation bị đánh.</summary>
    public void TriggerHurt()
    {
        _anim.SetTrigger(HashHurt);
    }

    /// <summary>Kích hoạt animation chết.</summary>
    public void TriggerDeath()
    {
        _anim.SetBool(HashDeath, true);
    }

    /// <summary>Đặt lại trạng thái hoạt ảnh để hồi sinh.</summary>
    public void ResetDeath()
    {
        TrySetBool(HashDeath, false);
        _anim.Play("Đứng", 0, 0f); // Trả về trạng thái đứng yên mặc định ("Đứng")
    }

    // ────────────────────────────────────────────────────────────────
    // Helper: SetBool / SetInt chỉ gọi khi parameter tồn tại trong controller
    // (tránh lỗi nếu controller cũ chưa có đủ parameters)

    private void TrySetBool(int hash, bool value)
    {
        foreach (var param in _anim.parameters)
            if (param.nameHash == hash) { _anim.SetBool(hash, value); return; }
    }

    private void TrySetInt(int hash, int value)
    {
        foreach (var param in _anim.parameters)
            if (param.nameHash == hash) { _anim.SetInteger(hash, value); return; }
    }
}

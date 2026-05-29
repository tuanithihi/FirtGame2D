using System.Collections;
using UnityEngine;

/// <summary>
/// Xử lý hệ thống chiến đấu: tấn công combo 3 đòn, đỡ đòn, nhận damage, chết.
/// Phải có PlayerController, PlayerStats, PlayerInputHandler trên cùng GameObject.
/// </summary>
[RequireComponent(typeof(PlayerStats))]
[RequireComponent(typeof(PlayerInputHandler))]
[RequireComponent(typeof(PlayerAnimator))]
public class PlayerCombat : MonoBehaviour
{
    // ────────────────────────────────────────────────────────────────
    [Header("=== TẤN CÔNG ===")]
    [SerializeField] private Transform attackPoint;         // vị trí vùng đánh
    [SerializeField] private float     attackRadius = 0.5f;
    [SerializeField] private LayerMask enemyLayer;

    [Header("=== ĐỠ ĐÒN ===")]
    [SerializeField] private float defendDamageMultiplier = 0.3f; // nhận 30% sát thương khi đỡ

    // ────────────────────────────────────────────────────────────────
    // Components
    private PlayerInputHandler _input;
    private PlayerStats        _stats;
    private PlayerAnimator     _animator;

    // Trạng thái chiến đấu
    private int   _comboStep    = 0;        // 0: rảnh, 1: đánh 1, 2: đánh 2, 3: đánh 3
    private float _lastAttackTime;
    private bool  _isAttacking  = false;
    private bool  _isHurt       = false;

    /// <summary>True khi nhân vật đang trong trạng thái khóa hành động dưới đất (chỉ đỡ đòn hoặc bị đau mới khóa).</summary>
    public bool IsActing 
    {
        get
        {
            // BỎ HOÀN TOÀN TẤN CÔNG khỏi IsActing để cho phép vừa di chuyển vừa chém dưới đất lẫn trên không
            return _isHurt || IsDefending;
        }
    }
    public bool IsDefending { get; private set; }

    // ────────────────────────────────────────────────────────────────
    private void Awake()
    {
        _input    = GetComponent<PlayerInputHandler>();
        _stats    = GetComponent<PlayerStats>();
        _animator = GetComponent<PlayerAnimator>();
    }

    // ────────────────────────────────────────────────────────────────
    private void Update()
    {
        if (_stats.isDead) return;

        HandleDefend();
        HandleAttack();
        CheckComboReset();
    }

    // ────────────────────────────────────────────────────────────────
    /// <summary>Đỡ đòn khi giữ phím.</summary>
    private void HandleDefend()
    {
        IsDefending = _input.DefendHeld && !_isAttacking;
        _animator.SetDefending(IsDefending);
    }

    /// <summary>Xử lý combo tấn công 3 đòn.</summary>
    private void HandleAttack()
    {
        if (_isAttacking) return;          // BẮT BUỘC: Nếu đang chém thì không thể làm gì khác (chặn spam/hủy animation)
        if (IsDefending)  return;

        int targetStep = 0;

        // Chỉ đọc các phím số Q, W, E trực tiếp
        if (_input.Skill1Pressed) targetStep = 1;
        else if (_input.Skill2Pressed) targetStep = 2;
        else if (_input.Skill3Pressed) targetStep = 3;

        if (targetStep == 0) return;

        _comboStep = targetStep;
        _lastAttackTime = Time.time;
        StartCoroutine(PerformAttack(targetStep));
    }

    /// <summary>Reset combo nếu không tấn công trong khoảng thời gian cho phép.</summary>
    private void CheckComboReset()
    {
        if (_comboStep > 0 && !_isAttacking &&
            Time.time - _lastAttackTime > _stats.attackComboResetTime)
        {
            _comboStep = 0;
        }
    }

    /// <summary>Coroutine thực hiện một đòn tấn công.</summary>
    private IEnumerator PerformAttack(int step)
    {
        _isAttacking = true;
        _animator.TriggerAttack(step);

        // Chờ thời gian ngắn để áp sát thương (hit frame)
        float hitDelay = GetAnimationLength(step) * 0.3f;
        yield return new WaitForSeconds(hitDelay);

        ApplyAttackDamage();

        // Chờ một chút xíu nữa là mở khóa _isAttacking ngay (cho phép bấm chiêu tiếp theo hoặc di chuyển ngay)
        float remaining = GetAnimationLength(step) * 0.3f;
        yield return new WaitForSeconds(remaining);

        _isAttacking = false;
    }

    /// <summary>Phát hiện và gây sát thương cho Enemy trong vùng tấn công.</summary>
    private void ApplyAttackDamage()
    {
        if (attackPoint == null) return;

        Collider2D[] hits = Physics2D.OverlapCircleAll(attackPoint.position, attackRadius, enemyLayer);
        foreach (var hit in hits)
        {
            // Gọi hàm TakeDamage nếu enemy có interface IDamageable
            if (hit.TryGetComponent<IDamageable>(out var damageable))
            {
                damageable.TakeDamage(_stats.attackDamage);
            }
        }
    }

    // ────────────────────────────────────────────────────────────────
    /// <summary>Gọi từ bên ngoài khi nhân vật bị đánh.</summary>
    public void ReceiveDamage(float damage)
    {
        if (_stats.isDead) return;

        // Nếu đang đỡ thì giảm sát thương
        float actualDamage = IsDefending ? damage * defendDamageMultiplier : damage;

        bool died = _stats.TakeDamage(actualDamage);

        if (died)
        {
            StartCoroutine(Die());
        }
        else if (!IsDefending)
        {
            StartCoroutine(HurtRoutine());
        }
    }

    private IEnumerator HurtRoutine()
    {
        _isHurt = true;
        _animator.TriggerHurt();
        yield return new WaitForSeconds(0.4f);
        _isHurt = false;
    }

    private IEnumerator Die()
    {
        _animator.TriggerDeath();
        yield return new WaitForSeconds(1.5f);
        // TODO: Mở màn Game Over hoặc respawn
        gameObject.SetActive(false);
    }

    // ────────────────────────────────────────────────────────────────
    /// <summary>Thời gian xấp xỉ của mỗi animation đòn đánh thực tế (giây).</summary>
    private float GetAnimationLength(int step)
    {
        return step switch
        {
            1 => 0.28f, // Rút ngắn từ 0.5f xuống 0.28f để khớp hoạt ảnh chém nhanh
            2 => 0.28f, // Rút ngắn từ 0.5f xuống 0.28f
            3 => 0.35f, // Rút ngắn từ 0.7f xuống 0.35f
            _ => 0.3f,
        };
    }

    // ────────────────────────────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, attackRadius);
    }
}

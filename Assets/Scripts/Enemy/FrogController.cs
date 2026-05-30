using System.Collections;
using UnityEngine;

/// <summary>
/// Điều khiển riêng cho Ếch (Frog Enemy):
/// Di chuyển bằng cách nhảy từng nhịp (nhảy cóc) hướng về phía Player hoặc đi tuần tra.
/// Tự động dò mặt đất để chuyển trạng thái từ Nhảy sang Idle.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class FrogController : MonoBehaviour, IDamageable
{
    [Header("=== CHỈ SỐ CƠ BẢN ===")]
    [SerializeField] private float maxHP = 30f;
    public float MaxHP => maxHP;
    public event System.Action<float, float> OnHPChanged;
    [SerializeField] private float jumpForceX = 3f;     // Lực nhảy xa
    [SerializeField] private float jumpForceY = 5f;     // Lực nhảy cao

    [Header("=== PHÁT HIỆN & TẤN CÔNG ===")]
    [SerializeField] private float detectionRange = 6f;
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackRange = 1.2f;
    [SerializeField] private float attackCooldown = 1.5f; // Thời gian giãn cách giữa các đòn đánh

    [Header("=== HIỆU ỨNG ĐẨY LÙI (KNOCKBACK) ===")]
    [SerializeField] private float knockbackForceX = 2f;   // Lực đẩy lùi ngang vừa phải
    [SerializeField] private float knockbackForceY = 1f;   // Lực nẩy nhẹ lên
    [SerializeField] private float knockbackDuration = 0.15f; // Thời gian khựng đẩy lùi ngắn hơn

    [Header("=== TUẦN TRA & ĐUỔI BẮT (PATROL & LEASH) ===")]
    [SerializeField] private float patrolRange = 3f;       // Tầm nhảy tuần tra quanh điểm xuất phát
    [SerializeField] private float leashRange = 6f;        // Giới hạn khoảng cách đuổi Player từ điểm xuất phát
    [SerializeField] private float jumpInterval = 2f;      // Thời gian dừng thở giữa mỗi cú nhảy

    [Header("=== KIỂM TRA MẶT ĐẤT ===")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private float groundCheckRadius = 0.15f;
    [SerializeField] private LayerMask groundLayer;

    [Header("=== THAM CHIẾU ===")]
    [SerializeField] private LayerMask playerLayer;

    // Trạng thái nội bộ
    private float _currentHP;
    private bool _isDead = false;
    private bool _isHurt = false;
    private bool _isGrounded = true;
    private bool _patrollingRight = true; // Đi tuần trái phải tự động
    private float _jumpTimer;
    private float _nextAttackTime; // Thời điểm được phép tấn công tiếp theo
    
    // Components
    private Rigidbody2D _rb;
    private Animator _animator;
    private Transform _playerTransform;

    // Animator Parameters
    private static readonly int IsGroundedParam = Animator.StringToHash("isGrounded");
    private static readonly int SpeedYParam = Animator.StringToHash("speedY");
    private static readonly int AttackTriggerParam = Animator.StringToHash("Attack");
    private static readonly int HurtTriggerParam = Animator.StringToHash("Hurt");
    private static readonly int DieTriggerParam = Animator.StringToHash("Die");
    private static readonly int IsDeadParam = Animator.StringToHash("isDead");

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();
        _currentHP = maxHP;
    }

    private Vector3 _startPosition; // Lưu vị trí ban đầu để hồi sinh

    private void Start()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            _playerTransform = player.transform;
        }

        // Đảm bảo Rigidbody2D khóa xoay Z
        _rb.freezeRotation = true;
        
        _startPosition = transform.position; // Ghi nhận vị trí xuất phát
        OnHPChanged?.Invoke(_currentHP, maxHP);
    }

    private void Update()
    {
        if (_isDead || _isHurt)
        {
            return;
        }

        CheckGrounded();

        // Đồng bộ trạng thái mặt đất lên Animator một cách an toàn
        TrySetBool(IsGroundedParam, _isGrounded);

        if (_playerTransform == null) return;

        float distanceToPlayer = Vector2.Distance(transform.position, _playerTransform.position);
        float distanceFromHomeToPlayer = Vector2.Distance(_startPosition, _playerTransform.position);

        // Chỉ đuổi theo nếu Player nằm trong tầm phát hiện VÀ Player không chạy quá xa khỏi vị trí xuất phát
        bool canChase = distanceToPlayer <= detectionRange && distanceFromHomeToPlayer <= leashRange;

        // 1. TẤN CÔNG TỨC THỜI: Khi chạm đất và Player ở trong tầm đánh, đánh ngay lập tức
        if (_isGrounded && distanceToPlayer <= attackRange)
        {
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y); // Đứng yên khi đánh

            // Luôn luôn quay mặt về phía Player trước khi đánh
            float directionX = _playerTransform.position.x - transform.position.x;
            Flip(directionX > 0);

            if (Time.time >= _nextAttackTime)
            {
                Attack();
                _nextAttackTime = Time.time + attackCooldown;
                _jumpTimer = 0f; // Reset thời gian chờ nhảy
            }
            return;
        }

        // 2. DI CHUYỂN NHẢY CÓC: Đếm ngược để nhảy đuổi theo hoặc tuần tra tự động
        if (_isGrounded)
        {
            _jumpTimer += Time.deltaTime;
            if (_jumpTimer >= jumpInterval)
            {
                if (canChase)
                {
                    // Nhảy đuổi theo người chơi
                    JumpTowards(_playerTransform.position);
                }
                else
                {
                    // Tự động nhảy đi tuần quanh điểm xuất phát
                    PatrolBehavior();
                }
                _jumpTimer = 0f;
            }
        }
    }

    /// <summary>
    /// AI đi tuần tự động trái phải quanh điểm xuất phát cho Ếch.
    /// </summary>
    private void PatrolBehavior()
    {
        if (!_isGrounded) return;

        // Tính toán biên tuần tra dựa trên vị trí xuất phát
        float targetX = _patrollingRight ? _startPosition.x + patrolRange : _startPosition.x - patrolRange;
        float distanceToTarget = Mathf.Abs(targetX - transform.position.x);

        // Nếu đã nhảy tới sát biên, đổi chiều tuần tra
        if (distanceToTarget < 0.3f)
        {
            _patrollingRight = !_patrollingRight;
            targetX = _patrollingRight ? _startPosition.x + patrolRange : _startPosition.x - patrolRange;
        }

        // Nhảy về phía biên tuần tra
        JumpTowards(new Vector3(targetX, transform.position.y, transform.position.z));
    }

    /// <summary>
    /// Thực hiện cú nhảy hướng về mục tiêu.
    /// </summary>
    private void JumpTowards(Vector3 targetPos)
    {
        if (!_isGrounded) return;

        float directionX = targetPos.x - transform.position.x;
        bool faceRight = directionX > 0;
        Flip(faceRight);

        // Áp dụng lực nhảy
        float forceX = faceRight ? jumpForceX : -jumpForceX;
        _rb.linearVelocity = new Vector2(forceX, jumpForceY);
        _isGrounded = false;
    }

    /// <summary>
    /// Kiểm tra xem ếch có đang chạm đất không (Tự động bỏ qua chính nó để tránh lỗi nhận nhầm).
    /// </summary>
    private void CheckGrounded()
    {
        // THỦ THUẬT: Nếu ếch đang bay lên (vận tốc Y dương lớn hơn 0.1), chắc chắn nó KHÔNG THỂ đang chạm đất.
        // Điều này ngăn chặn lỗi nhận nhầm chạm đất ngay lập tức ở khung hình đầu tiên khi vừa nhảy.
        if (_rb.linearVelocity.y > 0.1f)
        {
            _isGrounded = false;
            return;
        }

        Vector2 checkPos = groundCheck != null ? (Vector2)groundCheck.position : (Vector2)transform.position + Vector2.down * 0.5f;
        
        // Quét tất cả các Collider trong vùng Ground Check
        Collider2D[] colliders = Physics2D.OverlapCircleAll(checkPos, groundCheckRadius, groundLayer);
        _isGrounded = false;

        foreach (var col in colliders)
        {
            // Chỉ công nhận chạm đất nếu collider quét được KHÔNG phải là của chính con Ếch này và không phải trigger
            if (col.gameObject != gameObject && !col.isTrigger)
            {
                _isGrounded = true;
                break;
            }
        }
    }

    /// <summary>
    /// Tấn công lưỡi hoặc húc.
    /// </summary>
    private void Attack()
    {
        TrySetTrigger(AttackTriggerParam);

        // Áp dụng sát thương ngay lập tức khi ở cự ly gần
        Collider2D hit = Physics2D.OverlapCircle(transform.position, attackRange, playerLayer);
        if (hit != null && hit.TryGetComponent<PlayerCombat>(out var playerCombat))
        {
            playerCombat.ReceiveDamage(attackDamage);
        }
    }

    [Header("=== HIỆU ỨNG TÓE MÁU ===")]
    [Tooltip("Prefab Particle System hiệu ứng tóe máu")]
    [SerializeField] private GameObject bloodParticlePrefab;

    /// <summary>
    /// Nhận sát thương từ người chơi.
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (_isDead) return;

        _currentHP = Mathf.Max(0f, _currentHP - damage);
        OnHPChanged?.Invoke(_currentHP, maxHP);

        // Sinh hiệu ứng tóe máu nếu có gán Prefab
        if (bloodParticlePrefab != null)
        {
            Vector3 spawnPos = transform.position;
            if (TryGetComponent<Collider2D>(out var col))
            {
                spawnPos = col.bounds.center; // Sinh ở tâm nhân vật thay vì dưới chân
            }
            GameObject bloodSplash = Instantiate(bloodParticlePrefab, spawnPos, Quaternion.identity);
            Destroy(bloodSplash, 1.5f); // Tự động xóa sau 1.5 giây để tránh đầy bộ nhớ
        }

        if (_currentHP <= 0f)
        {
            Die();
        }
        else
        {
            StartCoroutine(HurtRoutine());
        }
    }

    private IEnumerator HurtRoutine()
    {
        _isHurt = true;

        // Tính toán hướng đẩy lùi (ngược hướng Player)
        float pushDirectionX = 1f;
        if (_playerTransform != null)
        {
            pushDirectionX = transform.position.x > _playerTransform.position.x ? 1f : -1f;
        }

        // Áp dụng lực đẩy lùi ngang và hơi nảy nhẹ lên trời để tạo cảm giác chém rất lực
        _rb.linearVelocity = new Vector2(pushDirectionX * knockbackForceX, knockbackForceY);
        
        TrySetTrigger(HurtTriggerParam);

        yield return new WaitForSeconds(knockbackDuration);
        _isHurt = false;
    }

    private void Die()
    {
        _isDead = true;
        _rb.linearVelocity = Vector2.zero;
        _rb.bodyType = RigidbodyType2D.Kinematic;

        TrySetTrigger(DieTriggerParam);
        TrySetBool(IsDeadParam, true);

        // Bắt đầu đếm ngược hồi sinh
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(1.5f); // Đợi chạy hết hoạt ảnh chết
        
        // Ẩn quái vật tạm thời và vô hiệu hóa va chạm
        if (TryGetComponent<Collider2D>(out var col)) col.enabled = false;
        if (TryGetComponent<SpriteRenderer>(out var sr)) sr.enabled = false;

        yield return new WaitForSeconds(5.0f); // Chờ 5 giây để hồi sinh

        // Đưa quái về vị trí ban đầu và khôi phục chỉ số
        transform.position = _startPosition;
        _currentHP = maxHP;
        OnHPChanged?.Invoke(_currentHP, maxHP);
        _isDead = false;
        _isHurt = false;

        // Bật lại va chạm và hiển thị
        if (col != null) col.enabled = true;
        if (sr != null) sr.enabled = true;
        _rb.bodyType = RigidbodyType2D.Dynamic;

        // Reset hoạt ảnh của quái một cách an toàn
        TrySetBool(IsDeadParam, false);
        TryPlay("Ếch"); // Quay lại đứng yên (Idle) - Đúng chính tả state trong Animator là "Ếch"
    }

    private void Flip(bool faceRight)
    {
        Vector3 scale = transform.localScale;
        // Đảm bảo chỉ lật X và luôn giữ Y dương
        if (scale.y < 0) scale.y = Mathf.Abs(scale.y);

        if ((faceRight && scale.x < 0) || (!faceRight && scale.x > 0))
        {
            scale.x *= -1;
        }
        transform.localScale = scale;
    }

    // ────────────────────────────────────────────────────────────────
    // CÁC HÀM BẢO VỆ TRÁNH LỖI ANIMATOR (TrySet)
    // ────────────────────────────────────────────────────────────────
    private void TrySetTrigger(int hash)
    {
        if (_animator == null) return;
        foreach (var param in _animator.parameters)
        {
            if (param.nameHash == hash)
            {
                _animator.SetTrigger(hash);
                return;
            }
        }
    }

    private void TrySetBool(int hash, bool value)
    {
        if (_animator == null) return;
        foreach (var param in _animator.parameters)
        {
            if (param.nameHash == hash)
            {
                _animator.SetBool(hash, value);
                return;
            }
        }
    }

    private void TryPlay(string stateName)
    {
        if (_animator == null) return;
        if (_animator.HasState(0, Animator.StringToHash(stateName)))
        {
            _animator.Play(stateName, 0, 0f);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);

        Vector3 gPos = groundCheck != null ? groundCheck.position : transform.position + Vector3.down * 0.5f;
        Gizmos.color = _isGrounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(gPos, groundCheckRadius);
    }
}

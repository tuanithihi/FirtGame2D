using System.Collections;
using UnityEngine;

/// <summary>
/// Điều khiển hành vi của Quái vật Orge (Ogre): tuần tra, đuổi theo và tấn công Player.
/// Kèm theo nhận sát thương (IDamageable), lực đẩy lùi (Knockback) và hiệu ứng hoạt ảnh.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class OrgeController : MonoBehaviour, IDamageable
{
    [Header("=== CHỈ SỐ CƠ BẢN ===")]
    [SerializeField] private float maxHP = 50f;
    [SerializeField] private float walkSpeed = 1f;        // Tốc độ đi tuần
    [SerializeField] private float chaseSpeed = 1.8f;      // Tốc độ đuổi theo Player
    
    [Header("=== PHÁT HIỆN & TẤN CÔNG ===")]
    [SerializeField] private float detectionRange = 5f;
    [SerializeField] private float attackRange = 1.5f;     // Tầm đánh của Ogre
    [SerializeField] private float attackDamage = 10f;
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private bool spriteFacesLeftByDefault = true; // Tích chọn nếu Sprite vẽ quay mặt sang trái mặc định

    [Header("=== HIỆU ỨNG ĐẨY LÙI (KNOCKBACK) ===")]
    [SerializeField] private float knockbackForceX = 1.5f;   // Lực đẩy lùi ngang nhẹ (Ogre nặng)
    [SerializeField] private float knockbackForceY = 0.8f;   // Lực nẩy lên cực nhẹ
    [SerializeField] private float knockbackDuration = 0.15f; // Thời gian khựng đẩy lùi ngắn hơn

    [Header("=== TUẦN TRA (PATROL) ===")]
    [SerializeField] private float patrolRange = 3f;       // Tầm tuần tra trái phải quanh điểm xuất phát
    [SerializeField] private float leashRange = 5f;        // Giới hạn khoảng cách đuổi Player từ điểm xuất phát
    [SerializeField] private float patrolWaitTime = 1f;

    [Header("=== THAM CHIẾU ===")]
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private Transform attackPoint;
    [SerializeField] private float attackRadius = 0.3f;

    // Trạng thái nội bộ
    private float _currentHP;
    private bool _isDead = false;
    private bool _isHurt = false;
    private bool _isAttacking = false;
    private bool _patrollingRight = true; // Hướng đi tuần tự động
    private float _nextAttackTime;
    private float _patrolTimer;
    private Vector3 _startPosition; // Vị trí ban đầu để hồi sinh

    // Components
    private Rigidbody2D _rb;
    private Animator _animator;
    private Transform _playerTransform;
    private SpriteRenderer _spriteRenderer;

    // Tên các Animator Parameter chuẩn hóa
    private static readonly int SpeedAnimParam = Animator.StringToHash("Speed");
    private static readonly int HurtAnimParam = Animator.StringToHash("Hurt");
    private static readonly int AttackAnimParam = Animator.StringToHash("Attack");
    private static readonly int DieAnimParam = Animator.StringToHash("Die");
    private static readonly int IsDeadAnimParam = Animator.StringToHash("isDead");

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
        _animator = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _currentHP = maxHP;
    }

    private void Start()
    {
        // Tự động tìm Player trong Scene
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            _playerTransform = player.transform;
        }

        // Đảm bảo Rigidbody2D khóa xoay Z không cho quái vật bị ngã lộn đầu
        if (_rb != null)
        {
            _rb.freezeRotation = true;
        }

        _startPosition = transform.position; // Lưu vị trí xuất phát
    }

    private void Update()
    {
        if (_isDead || _isHurt)
        {
            return; // Tránh ghi đè lực đẩy lùi (Knockback) vật lý
        }

        if (_playerTransform == null)
        {
            PatrolBehavior();
            SafeSetFloat(SpeedAnimParam, Mathf.Abs(_rb.linearVelocity.x));
            return;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, _playerTransform.position);
        float distanceFromHomeToPlayer = Vector2.Distance(_startPosition, _playerTransform.position);

        // Ogre chỉ được đuổi hoặc đánh nếu Player nằm trong tầm phát hiện VÀ Player chưa chạy ra quá xa vị trí xuất phát
        bool canChase = distanceToPlayer <= detectionRange && distanceFromHomeToPlayer <= leashRange;

        if (canChase && distanceToPlayer <= attackRange)
        {
            // Trong tầm đánh -> Tấn công
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);

            // Luôn quay mặt về phía Player trước khi đánh
            float direction = _playerTransform.position.x - transform.position.x;
            Flip(direction > 0);

            if (Time.time >= _nextAttackTime && !_isAttacking)
            {
                StartCoroutine(PerformAttack());
            }
        }
        else if (canChase)
        {
            // Trong tầm phát hiện -> Đuổi theo Player
            ChasePlayer();
        }
        else
        {
            // Vượt quá giới hạn hoặc Player quá xa -> Quay đầu đi tuần tra quanh vị trí ban đầu
            PatrolBehavior();
        }

        // Cập nhật tốc độ di chuyển vào Animator một cách an toàn
        SafeSetFloat(SpeedAnimParam, Mathf.Abs(_rb.linearVelocity.x));
    }

    /// <summary>
    /// Đuổi theo Player.
    /// </summary>
    private void ChasePlayer()
    {
        if (_isAttacking) return;

        float direction = _playerTransform.position.x - transform.position.x;
        _rb.linearVelocity = new Vector2(Mathf.Sign(direction) * chaseSpeed, _rb.linearVelocity.y);
        
        // Quay mặt về phía Player
        Flip(direction > 0);
    }

    /// <summary>
    /// Tuần tra qua lại tự động quanh vị trí xuất phát.
    /// </summary>
    private void PatrolBehavior()
    {
        if (_isAttacking) return;

        // Tính tọa độ điểm mốc biên tuần tra trái/phải dựa trên vị trí xuất phát ban đầu
        float targetX = _patrollingRight ? _startPosition.x + patrolRange : _startPosition.x - patrolRange;
        float distanceToTarget = Mathf.Abs(targetX - transform.position.x);

        if (distanceToTarget < 0.2f)
        {
            // Đã đến biên -> Đứng đợi nghỉ ngơi
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            _patrolTimer += Time.deltaTime;
            if (_patrolTimer >= patrolWaitTime)
            {
                _patrollingRight = !_patrollingRight; // Đổi chiều tuần tra
                _patrolTimer = 0f;
            }
        }
        else
        {
            // Đi tiếp tới biên tuần tra
            float direction = targetX - transform.position.x;
            _rb.linearVelocity = new Vector2(Mathf.Sign(direction) * walkSpeed, _rb.linearVelocity.y);
            Flip(direction > 0);
        }
    }

    /// <summary>
    /// Thực hiện đòn đánh.
    /// </summary>
    private IEnumerator PerformAttack()
    {
        _isAttacking = true;
        _nextAttackTime = Time.time + attackCooldown;

        SafeSetTrigger(AttackAnimParam);

        // Chờ giây lát (hit frame) để khớp hoạt ảnh rồi gây sát thương
        yield return new WaitForSeconds(0.3f);

        if (!_isDead && !_isHurt)
        {
            ApplyDamageToPlayer();
        }

        yield return new WaitForSeconds(0.4f);
        _isAttacking = false;
    }

    /// <summary>
    /// Gây sát thương lên Player.
    /// </summary>
    private void ApplyDamageToPlayer()
    {
        Vector2 checkPos = attackPoint != null ? (Vector2)attackPoint.position : (Vector2)transform.position;
        float radius = attackPoint != null ? attackRadius : attackRange;

        Collider2D hit = Physics2D.OverlapCircle(checkPos, radius, playerLayer);
        if (hit != null)
        {
            if (hit.TryGetComponent<PlayerCombat>(out var playerCombat))
            {
                playerCombat.ReceiveDamage(attackDamage);
            }
        }
    }

    [Header("=== HIỆU ỨNG TÓE MÁU ===")]
    [Tooltip("Prefab Particle System hiệu ứng tóe máu")]
    [SerializeField] private GameObject bloodParticlePrefab;

    /// <summary>
    /// Cổng nhận sát thương từ IDamageable.
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (_isDead) return;

        _currentHP = Mathf.Max(0f, _currentHP - damage);

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

        // Áp dụng lực đẩy lùi ngang và hơi nảy nhẹ lên
        _rb.linearVelocity = new Vector2(pushDirectionX * knockbackForceX, knockbackForceY);
        
        SafeSetTrigger(HurtAnimParam);

        yield return new WaitForSeconds(knockbackDuration);
        _isHurt = false;
    }

    private void Die()
    {
        _isDead = true;
        _rb.linearVelocity = Vector2.zero;
        _rb.bodyType = RigidbodyType2D.Kinematic;

        SafeSetTrigger(DieAnimParam);
        SafeSetBool(IsDeadAnimParam, true);

        // Bắt đầu đếm ngược hồi sinh Ogre
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(2.0f); // Đợi chạy hết hoạt ảnh chết
        
        // Ẩn quái vật tạm thời và vô hiệu hóa va chạm
        if (TryGetComponent<Collider2D>(out var col)) col.enabled = false;
        if (TryGetComponent<SpriteRenderer>(out var sr)) sr.enabled = false;

        yield return new WaitForSeconds(5.0f); // Chờ 5 giây để hồi sinh

        // Đưa quái về vị trí ban đầu và khôi phục chỉ số
        transform.position = _startPosition;
        _currentHP = maxHP;
        _isDead = false;
        _isHurt = false;
        _isAttacking = false;

        // Bật lại va chạm và hiển thị
        if (col != null) col.enabled = true;
        if (sr != null) sr.enabled = true;
        _rb.bodyType = RigidbodyType2D.Dynamic;

        // Reset hoạt ảnh của quái
        SafeSetBool(IsDeadAnimParam, false);
        SafePlay("Orge"); // Quay lại đứng yên (Idle) - Đúng chính tả O-R-G-E
    }

    /// <summary>
    /// Lật hướng nhân vật.
    /// </summary>
    private void Flip(bool faceRight)
    {
        Vector3 scale = transform.localScale;
        
        // Đảm bảo trục Y luôn dương, tránh lỗi quái bị lật ngược đầu
        if (scale.y < 0) scale.y = Mathf.Abs(scale.y);

        // Nếu sprite mặc định quay sang trái, chúng ta đảo ngược logic kiểm tra hướng
        if (spriteFacesLeftByDefault)
        {
            faceRight = !faceRight;
        }

        if ((faceRight && scale.x < 0) || (!faceRight && scale.x > 0))
        {
            scale.x *= -1;
        }
        transform.localScale = scale;
    }

    private void OnDrawGizmosSelected()
    {
        // Vùng phát hiện (Vàng)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Vùng tấn công (Đỏ)
        Gizmos.color = Color.red;
        if (attackPoint != null)
        {
            Gizmos.DrawWireSphere(attackPoint.position, attackRadius);
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }

    // ==========================================
    // CÁC PHƯƠNG THỨC TRÁNH LỖI HOẠT ẢNH AN TOÀN
    // ==========================================
    private void SafeSetFloat(int paramHash, float value)
    {
        if (_animator == null) return;
        foreach (var param in _animator.parameters)
        {
            if (param.nameHash == paramHash)
            {
                _animator.SetFloat(paramHash, value);
                return;
            }
        }
    }

    private void SafeSetTrigger(int paramHash)
    {
        if (_animator == null) return;
        foreach (var param in _animator.parameters)
        {
            if (param.nameHash == paramHash)
            {
                _animator.SetTrigger(paramHash);
                return;
            }
        }
    }

    private void SafeSetBool(int paramHash, bool value)
    {
        if (_animator == null) return;
        foreach (var param in _animator.parameters)
        {
            if (param.nameHash == paramHash)
            {
                _animator.SetBool(paramHash, value);
                return;
            }
        }
    }

    private void SafePlay(string stateName)
    {
        if (_animator == null) return;
        if (_animator.HasState(0, Animator.StringToHash(stateName)))
        {
            _animator.Play(stateName, 0, 0f);
        }
    }
}

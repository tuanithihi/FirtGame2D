using System.Collections;
using UnityEngine;

/// <summary>
/// Điều khiển hành vi của Boss Level 1: tuần tra, đuổi theo, tấn công cận chiến
/// và đặc biệt là kỹ năng cast phép nổ chậm (hồi chiêu cố định 20 giây).
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class BossController : MonoBehaviour, IDamageable
{
    [Header("=== CHỈ SỐ CƠ BẢN ===")]
    [SerializeField] private float maxHP = 150f;
    public float MaxHP => maxHP;
    public event System.Action<float, float> OnHPChanged;
    [SerializeField] private float walkSpeed = 1.2f;       // Tốc độ tuần tra
    [SerializeField] private float chaseSpeed = 2f;         // Tốc độ đuổi theo Player

    [Header("=== PHÁT HIỆN & TẤN CÔNG CẬN CHIẾN ===")]
    [SerializeField] private float detectionRange = 8f;
    [SerializeField] private float meleeAttackRange = 1.8f; // Tầm đánh cận chiến
    [SerializeField] private float meleeDamage = 15f;
    [SerializeField] private float meleeCooldown = 1.5f;
    [SerializeField] private Transform meleeAttackPoint;
    [SerializeField] private float meleeAttackRadius = 0.4f;

    [Header("=== KỸ NĂNG CAST PHÉP (SPELL) ===")]
    [SerializeField] private GameObject warningAreaPrefab;   // Vùng cảnh báo nổ chậm
    [SerializeField] private GameObject spellPrefab;         // Prefab vụ nổ Spell
    [SerializeField] private float spellCooldown = 20f;      // Hồi chiêu phép (cố định 20 giây)
    [SerializeField] private float castDelay = 1.2f;         // Độ trễ từ lúc gồng phép đến lúc nổ (để né)

    [Header("=== HIỆU ỨNG ĐẨY LÙI (KNOCKBACK) ===")]
    [SerializeField] private float knockbackForceX = 1f;     // Kháng knockback cao (Boss nặng)
    [SerializeField] private float knockbackForceY = 0.5f;
    [SerializeField] private float knockbackDuration = 0.15f;

    [Header("=== TUẦN TRA (PATROL) ===")]
    [SerializeField] private float patrolRange = 4f;         // Phạm vi tuần tra quanh vị trí xuất phát
    [SerializeField] private float leashRange = 10f;         // Khoảng cách giới hạn đuổi theo Player
    [SerializeField] private float patrolWaitTime = 1.5f;

    [Header("=== THAM CHIẾU ===")]
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private bool spriteFacesLeftByDefault = true; // Chỉnh theo hướng vẽ mặc định của sprite

    // Trạng thái nội bộ
    private float _currentHP;
    private bool _isDead = false;
    private bool _isHurt = false;
    private bool _isAttacking = false;   // Đang trong hoạt ảnh tấn công cận chiến hoặc cast phép
    private bool _patrollingRight = true;
    private float _nextMeleeTime;
    private float _nextSpellTime;
    private float _patrolTimer;
    private Vector3 _startPosition;

    // Components
    private Rigidbody2D _rb;
    private Animator _animator;
    private Transform _playerTransform;
    private SpriteRenderer _spriteRenderer;

    // Animator Parameters
    private static readonly int SpeedAnimParam = Animator.StringToHash("Speed");
    private static readonly int AttackAnimParam = Animator.StringToHash("Attack");
    private static readonly int CastAnimParam = Animator.StringToHash("Cast");
    private static readonly int HurtAnimParam = Animator.StringToHash("Hurt");
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
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            _playerTransform = player.transform;
        }

        if (_rb != null)
        {
            _rb.freezeRotation = true;
        }

        _startPosition = transform.position;
        _nextSpellTime = Time.time + 5f; // Cast lần đầu sau 5 giây khi bắt đầu trận đấu
        OnHPChanged?.Invoke(_currentHP, maxHP);
    }

    private void Update()
    {
        if (_isDead || _isHurt || _isAttacking)
        {
            return;
        }

        if (_playerTransform == null)
        {
            PatrolBehavior();
            SafeSetFloat(SpeedAnimParam, Mathf.Abs(_rb.linearVelocity.x));
            return;
        }

        float distanceToPlayer = Vector2.Distance(transform.position, _playerTransform.position);
        float distanceFromHomeToPlayer = Vector2.Distance(_startPosition, _playerTransform.position);

        bool canChase = distanceToPlayer <= detectionRange && distanceFromHomeToPlayer <= leashRange;

        // 1. Kiểm tra kỹ năng phép (Spell): Cố định 20 giây 1 lần, ưu tiên hơn cận chiến nếu tới thời điểm
        if (canChase && Time.time >= _nextSpellTime)
        {
            StartCoroutine(PerformSpellCast());
            return;
        }

        // 2. Kiểm tra tấn công cận chiến (Melee)
        if (canChase && distanceToPlayer <= meleeAttackRange)
        {
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);

            // Quay mặt về phía Player trước khi đánh
            float direction = _playerTransform.position.x - transform.position.x;
            Flip(direction > 0);

            if (Time.time >= _nextMeleeTime)
            {
                StartCoroutine(PerformMeleeAttack());
            }
        }
        else if (canChase)
        {
            // Trong tầm phát hiện -> Đuổi theo
            ChasePlayer();
        }
        else
        {
            // Đi tuần
            PatrolBehavior();
        }

        SafeSetFloat(SpeedAnimParam, Mathf.Abs(_rb.linearVelocity.x));
    }

    private void ChasePlayer()
    {
        float direction = _playerTransform.position.x - transform.position.x;
        _rb.linearVelocity = new Vector2(Mathf.Sign(direction) * chaseSpeed, _rb.linearVelocity.y);
        Flip(direction > 0);
    }

    private void PatrolBehavior()
    {
        float targetX = _patrollingRight ? _startPosition.x + patrolRange : _startPosition.x - patrolRange;
        float distanceToTarget = Mathf.Abs(targetX - transform.position.x);

        if (distanceToTarget < 0.2f)
        {
            _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            _patrolTimer += Time.deltaTime;
            if (_patrolTimer >= patrolWaitTime)
            {
                _patrollingRight = !_patrollingRight;
                _patrolTimer = 0f;
            }
        }
        else
        {
            float direction = targetX - transform.position.x;
            _rb.linearVelocity = new Vector2(Mathf.Sign(direction) * walkSpeed, _rb.linearVelocity.y);
            Flip(direction > 0);
        }
    }

    private IEnumerator PerformMeleeAttack()
    {
        _isAttacking = true;
        _nextMeleeTime = Time.time + meleeCooldown;

        SafeSetTrigger(AttackAnimParam);

        // Chờ đến frame ra đòn (được điều chỉnh theo hoạt ảnh của Boss)
        yield return new WaitForSeconds(0.4f);

        if (!_isDead && !_isHurt)
        {
            ApplyMeleeDamage();
        }

        yield return new WaitForSeconds(0.4f);
        _isAttacking = false;
    }

    private void ApplyMeleeDamage()
    {
        Vector2 checkPos = meleeAttackPoint != null ? (Vector2)meleeAttackPoint.position : (Vector2)transform.position;
        float radius = meleeAttackPoint != null ? meleeAttackRadius : meleeAttackRange;

        Collider2D hit = Physics2D.OverlapCircle(checkPos, radius, playerLayer);
        if (hit != null)
        {
            if (hit.TryGetComponent<PlayerCombat>(out var playerCombat))
            {
                playerCombat.ReceiveDamage(meleeDamage);
            }
        }
    }

    private IEnumerator PerformSpellCast()
    {
        _isAttacking = true;
        _nextSpellTime = Time.time + spellCooldown; // Thiết lập thời điểm cast phép tiếp theo (+20s)
        _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);

        // Luôn quay mặt về phía Player khi bắt đầu cast phép
        if (_playerTransform != null)
        {
            float dir = _playerTransform.position.x - transform.position.x;
            Flip(dir > 0);
        }

        // Bắt đầu hoạt ảnh gồng phép (Cast) của Boss
        SafeSetTrigger(CastAnimParam);

        // Khóa vị trí của Player dưới đất ngay lúc này
        Vector3 targetPosition = _playerTransform.position;

        // 1. Sinh ra vòng tròn đỏ cảnh báo nổ chậm dưới đất
        GameObject warningIndicator = null;
        if (warningAreaPrefab != null)
        {
            warningIndicator = Instantiate(warningAreaPrefab, targetPosition, Quaternion.identity);
        }

        // 2. Sinh hiệu ứng phép thuật (Spell) trên đầu Player ngay lập tức để chạy hoạt ảnh rơi xuống
        if (!_isDead && spellPrefab != null)
        {
            // Sinh ở vị trí trên cao cách mặt đất 3.5m
            Vector3 spawnPosition = targetPosition + new Vector3(0f, 3.5f, 0f);
            GameObject spellInstance = Instantiate(spellPrefab, spawnPosition, Quaternion.identity);
            
            // Khởi tạo vị trí nổ dưới đất cho quả cầu phép
            if (spellInstance.TryGetComponent<BossSpell>(out var bossSpell))
            {
                bossSpell.Initialize(targetPosition);
            }
        }

        // Chờ một khoảng trễ cố định (castDelay) bằng thời gian hoạt ảnh rơi dội xuống
        yield return new WaitForSeconds(castDelay);

        // Xóa vòng cảnh báo đỏ sau khi phép đã nổ xong dưới đất
        if (warningIndicator != null)
        {
            Destroy(warningIndicator);
        }

        // Chờ phần còn lại của hoạt ảnh cast kết thúc
        yield return new WaitForSeconds(0.5f);
        _isAttacking = false;
    }

    [Header("=== HIỆU ỨNG TÓE MÁU ===")]
    [SerializeField] private GameObject bloodParticlePrefab;

    public void TakeDamage(float damage)
    {
        if (_isDead) return;

        _currentHP = Mathf.Max(0f, _currentHP - damage);
        OnHPChanged?.Invoke(_currentHP, maxHP);

        if (bloodParticlePrefab != null)
        {
            Vector3 spawnPos = transform.position;
            if (TryGetComponent<Collider2D>(out var col))
            {
                spawnPos = col.bounds.center;
            }
            GameObject bloodSplash = Instantiate(bloodParticlePrefab, spawnPos, Quaternion.identity);
            Destroy(bloodSplash, 1.5f);
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
        _isAttacking = false; // Ngắt trạng thái tấn công nếu bị đơ khi trúng đòn

        float pushDirectionX = 1f;
        if (_playerTransform != null)
        {
            pushDirectionX = transform.position.x > _playerTransform.position.x ? 1f : -1f;
        }

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

        // Báo tử vong cho UI thanh máu ẩn đi
        OnHPChanged?.Invoke(0f, maxHP);

        // Vô hiệu hóa va chạm để Player có thể đi xuyên qua xác lúc đang chạy animation chết
        if (TryGetComponent<Collider2D>(out var col))
        {
            col.enabled = false;
        }

        // Tự động xóa (Destroy) Boss khỏi Scene sau 2.0 giây để biến mất hoàn toàn
        Destroy(gameObject, 2.0f);
    }

    private void Flip(bool faceRight)
    {
        Vector3 scale = transform.localScale;
        if (scale.y < 0) scale.y = Mathf.Abs(scale.y);

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
        // Tầm phát hiện
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Tầm đánh cận chiến
        Gizmos.color = Color.red;
        if (meleeAttackPoint != null)
        {
            Gizmos.DrawWireSphere(meleeAttackPoint.position, meleeAttackRadius);
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position, meleeAttackRange);
        }
    }

    // Các phương thức an toàn để kích hoạt Animator Parameter
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
}

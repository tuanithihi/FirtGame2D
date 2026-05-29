using UnityEngine;

/// <summary>
/// Chứa toàn bộ chỉ số của nhân vật Player.
/// Attach script này lên GameObject Player.
/// </summary>
public class PlayerStats : MonoBehaviour
{
    [Header("=== SỨC KHỎE ===")]
    [SerializeField] private float maxHP = 100f;
    private float currentHP;

    [Header("=== DI CHUYỂN ===")]
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    public float jumpForce = 10f;

    [Header("=== CHIẾN ĐẤU ===")]
    public float attackDamage = 20f;
    public float attackComboResetTime = 1.2f;   // thời gian reset combo tấn công

    [Header("=== TRẠNG THÁI ===")]
    public bool isDead = false;

    // Sự kiện thông báo khi HP thay đổi
    public System.Action<float, float> OnHPChanged;   // (currentHP, maxHP)
    public System.Action OnPlayerDied;

    // ---------------------------------------------------------------
    private void Awake()
    {
        currentHP = maxHP;
    }

    // ---------------------------------------------------------------
    // GETTERS
    public float CurrentHP  => currentHP;
    public float MaxHP      => maxHP;
    public float HPPercent  => currentHP / maxHP;

    // ---------------------------------------------------------------
    /// <summary>Nhận sát thương. Trả về true nếu nhân vật chết.</summary>
    public bool TakeDamage(float damage)
    {
        if (isDead) return false;

        currentHP = Mathf.Max(0f, currentHP - damage);
        OnHPChanged?.Invoke(currentHP, maxHP);

        if (currentHP <= 0f)
        {
            isDead = true;
            OnPlayerDied?.Invoke();
            return true;
        }
        return false;
    }

    /// <summary>Hồi máu.</summary>
    public void Heal(float amount)
    {
        if (isDead) return;
        currentHP = Mathf.Min(maxHP, currentHP + amount);
        OnHPChanged?.Invoke(currentHP, maxHP);
    }
}

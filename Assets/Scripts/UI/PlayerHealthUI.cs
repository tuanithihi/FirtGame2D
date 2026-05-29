using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Quản lý giao diện thanh máu (Health Bar UI) đơn giản cho Player.
/// Hỗ trợ hiệu ứng co rút mượt mà (smooth decrease) khi bị tấn công.
/// </summary>
public class PlayerHealthUI : MonoBehaviour
{
    [Header("=== THAM CHIẾU PLAYER ===")]
    [SerializeField] private PlayerStats playerStats;

    [Header("=== THANH MÁU UI ===")]
    [Tooltip("Ảnh hiển thị lượng máu màu đỏ (máu_1) - Image Type cần set là Filled")]
    [SerializeField] private Image healthBarFill;

    [Header("=== HIỆU ỨNG GIẢM MÁU ===")]
    [Tooltip("Tốc độ co rút mượt mà của thanh máu khi bị đánh trúng")]
    [SerializeField] private float decreaseSpeed = 5f;

    private float _targetFill = 1f;

    private void Start()
    {
        // Tự động tìm PlayerStats nếu chưa gán
        if (playerStats == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerStats = player.GetComponent<PlayerStats>();
            }
        }

        if (playerStats != null)
        {
            // Đăng ký sự kiện thay đổi máu
            playerStats.OnHPChanged += HandleHPChanged;
            
            // Khởi tạo lượng máu đầy ban đầu lập tức
            _targetFill = Mathf.Clamp01(playerStats.CurrentHP / playerStats.MaxHP);
            if (healthBarFill != null)
            {
                healthBarFill.fillAmount = _targetFill;
            }
        }
    }

    private void OnDestroy()
    {
        if (playerStats != null)
        {
            playerStats.OnHPChanged -= HandleHPChanged;
        }
    }

    private void Update()
    {
        // Luôn co rút mượt mà từ lượng fill hiện tại về target fill mới
        if (healthBarFill != null && !Mathf.Approximately(healthBarFill.fillAmount, _targetFill))
        {
            healthBarFill.fillAmount = Mathf.Lerp(healthBarFill.fillAmount, _targetFill, Time.deltaTime * decreaseSpeed);
        }
    }

    /// <summary>
    /// Xử lý khi nhận được event đổi HP từ PlayerStats.
    /// </summary>
    private void HandleHPChanged(float currentHP, float maxHP)
    {
        UpdateUI(currentHP, maxHP);
    }

    /// <summary>
    /// Cập nhật mục tiêu co rút của UI thanh máu đỏ.
    /// </summary>
    private void UpdateUI(float currentHP, float maxHP)
    {
        _targetFill = Mathf.Clamp01(currentHP / maxHP);
    }
}

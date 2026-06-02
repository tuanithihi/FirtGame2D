using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Quản lý giao diện thanh máu (Health Bar) cho Boss Lv1 hiển thị trong World Space Canvas.
/// Tự động cập nhật lượng máu, giữ thanh máu thẳng đứng không bị lật ngược (flip) khi Boss quay đầu.
/// Theo đúng cơ chế thanh máu của Ogre và Ếch.
/// </summary>
public class BossHealthBar : MonoBehaviour
{
    [Header("=== THANH MÁU UI ===")]
    [Tooltip("Ảnh hiển thị thanh máu (máu quái.png) - Image Type cần set là Filled")]
    [SerializeField] private Image healthBarFill;

    [Header("=== HIỆU ỨNG GIẢM MÁU ===")]
    [Tooltip("Tốc độ co rút mượt mà của thanh máu khi bị đánh trúng")]
    [SerializeField] private float decreaseSpeed = 5f;

    [Header("=== THAM CHIẾU BOSS ===")]
    [SerializeField] private BossController bossController;

    [Header("=== VỊ TRÍ HỖ TRỢ (FIX CỨNG) ===")]
    [Tooltip("Độ lệch vị trí (X, Y) so với tâm gốc của quái vật trong thế giới")]
    [SerializeField] private Vector3 positionOffset = new Vector3(0f, 2.5f, 0f);

    private float _targetFill = 1f;
    private Vector3 _initialScale;
    private Canvas _canvas;

    private void Awake()
    {
        _initialScale = transform.localScale;
        _canvas = GetComponent<Canvas>();

        // Tự động tìm tham chiếu nếu chưa được gán
        if (bossController == null)
        {
            bossController = GetComponentInParent<BossController>();
        }
    }

    private void Start()
    {
        // Đăng ký sự kiện thay đổi máu
        if (bossController != null)
        {
            bossController.OnHPChanged += HandleHPChanged;
            UpdateHealthImmediate(bossController.MaxHP, bossController.MaxHP); // Khởi tạo đầy máu
        }
    }

    private void OnDestroy()
    {
        // Hủy đăng ký sự kiện tránh leak bộ nhớ
        if (bossController != null)
        {
            bossController.OnHPChanged -= HandleHPChanged;
        }
    }

    private void Update()
    {
        // Co rút mượt mà thanh máu đỏ về target fill
        if (healthBarFill != null && !Mathf.Approximately(healthBarFill.fillAmount, _targetFill))
        {
            healthBarFill.fillAmount = Mathf.Lerp(healthBarFill.fillAmount, _targetFill, Time.deltaTime * decreaseSpeed);
        }
    }

    private void LateUpdate()
    {
        // FIX CỨNG TRÊN ĐẦU & LOẠI BỎ LỆCH KHI FLIP:
        if (transform.parent != null)
        {
            transform.position = transform.parent.position + positionOffset;

            // Đảm bảo triệt tiêu chiều âm khi quái bị lật (Flip) để thanh máu không bị ngược chữ/ảnh
            transform.localScale = new Vector3(
                Mathf.Sign(transform.parent.localScale.x) * _initialScale.x,
                Mathf.Sign(transform.parent.localScale.y) * _initialScale.y,
                _initialScale.z
            );
            
            // Giữ cho thanh máu luôn hướng thẳng đứng (không bị xoay theo cha)
            transform.rotation = Quaternion.identity;
        }
    }

    private void HandleHPChanged(float currentHP, float maxHP)
    {
        _targetFill = Mathf.Clamp01(currentHP / maxHP);
        
        // Tự động ẩn thanh máu khi quái hết HP và hiện lại khi hồi sinh
        if (_canvas != null)
        {
            _canvas.enabled = (currentHP > 0f);
        }
    }

    private void UpdateHealthImmediate(float currentHP, float maxHP)
    {
        _targetFill = Mathf.Clamp01(currentHP / maxHP);
        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = _targetFill;
        }
        
        if (_canvas != null)
        {
            _canvas.enabled = (currentHP > 0f);
        }
    }
}

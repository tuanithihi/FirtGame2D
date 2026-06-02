using UnityEngine;
using UnityEngine.InputSystem; // Thêm thư viện Input System mới

/// <summary>
/// Cổng dịch chuyển thông minh giữa các phân khu (GameArea).
/// Hướng phát triển tương lai cực kỳ dễ mở rộng cho mọi Level, Map, và Boss mới mà không cần sửa code.
/// </summary>
public class TeleportPortal : MonoBehaviour
{
    [Header("=== CẤU HÌNH DỊCH CHUYỂN ===")]
    [Tooltip("Khu vực hiện tại (nơi Player đứng trước khi dịch chuyển)")]
    public GameArea currentArea;

    [Tooltip("Khu vực đích đến (Phòng Boss, Khu vực mới của level mới...)")]
    public GameArea targetArea;

    [Tooltip("Chữ thông báo hướng dẫn dịch chuyển (ví dụ: PortalText)")]
    public GameObject textPrompt;

    private bool isPlayerInRange = false;
    private Transform playerTransform;

    private void Start()
    {
        // Ẩn chữ thông báo khi mới vào game
        if (textPrompt != null)
        {
            textPrompt.SetActive(false);
        }

        // Tự động kích hoạt khu vực xuất phát ban đầu để cài đặt Camera và Background đúng ngay từ đầu
        if (currentArea != null)
        {
            // Truyền vào null để không dịch chuyển vị trí Player ở frame đầu tiên
            currentArea.ActivateArea(null);
        }
    }

    private void Update()
    {
        // Đọc phím T từ bàn phím bằng New Input System
        if (isPlayerInRange && Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame)
        {
            Teleport();
        }
    }

    private void Teleport()
    {
        if (targetArea != null && playerTransform != null)
        {
            // 1. Tắt khu vực cũ (tự động ẩn các background cũ của map cũ đi)
            if (currentArea != null)
            {
                currentArea.DeactivateArea();
            }

            // 2. Kích hoạt khu vực mới (tự động di chuyển player, set camera bounds mới, bật background mới)
            targetArea.ActivateArea(playerTransform);

            // 3. Ẩn chữ thông báo sau khi đã dịch chuyển xong
            if (textPrompt != null)
            {
                textPrompt.SetActive(false);
            }
        }
    }

    // Khi Player đi vào vùng cổng
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Chỉ nhận diện đối tượng có PlayerController chính, bỏ qua collider phụ
        PlayerController player = collision.GetComponentInParent<PlayerController>();
        if (player != null && !collision.isTrigger)
        {
            isPlayerInRange = true;
            playerTransform = player.transform;

            // Hiện chữ thông báo lên
            if (textPrompt != null)
            {
                textPrompt.SetActive(true);
            }
        }
    }

    // Khi Player đi ra khỏi vùng cổng
    private void OnTriggerExit2D(Collider2D collision)
    {
        PlayerController player = collision.GetComponentInParent<PlayerController>();
        if (player != null && !collision.isTrigger)
        {
            isPlayerInRange = false;
            
            // Ẩn chữ thông báo đi
            if (textPrompt != null)
            {
                textPrompt.SetActive(false);
            }
        }
    }
}

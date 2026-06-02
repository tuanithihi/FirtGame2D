using UnityEngine;

/// <summary>
/// Quản lý một phân khu/khu vực trong màn chơi (Ví dụ: Map 1, Phòng Boss 1, Map 2, Phòng Boss 2...).
/// Giúp dễ dàng quản lý Camera Bounds và Background riêng cho từng khu vực một cách modular.
/// </summary>
public class GameArea : MonoBehaviour
{
    [Header("=== CẤU HÌNH CAMERA ===")]
    [Tooltip("Có tự động khóa giới hạn camera riêng cho khu vực này không?")]
    public bool customCameraBounds = true;
    public Vector2 minCameraBounds;
    public Vector2 maxCameraBounds;
    public bool limitVertical = true;

    [Header("=== ĐỐI TƯỢNG TRONG KHU VỰC ===")]
    [Tooltip("Danh sách các đối tượng (như Background, ánh sáng riêng...) chỉ bật khi người chơi ở khu vực này")]
    public GameObject[] areaObjects;

    [Header("=== ĐIỂM XUẤT PHÁT ===")]
    [Tooltip("Điểm xuất hiện của Player khi dịch chuyển vào khu vực này")]
    public Transform spawnPoint;

    /// <summary>
    /// Kích hoạt khu vực này: dịch chuyển người chơi, cài đặt camera và bật các background tương ứng.
    /// </summary>
    public void ActivateArea(Transform player)
    {
        // 1. Dịch chuyển người chơi tới điểm spawn của khu vực này
        if (player != null && spawnPoint != null)
        {
            player.position = spawnPoint.position;
        }

        // 2. Gán khu vực hoạt động sang cho CameraController để camera tự động đọc tọa độ liên tục
        CameraController cam = Camera.main != null ? Camera.main.GetComponent<CameraController>() : null;
        if (cam == null)
        {
            cam = FindFirstObjectByType<CameraController>();
        }

        if (cam != null)
        {
            cam.activeArea = this; // Gán khu vực đang hoạt động sang cho camera
            
            // DỊCH CHUYỂN CAMERA LẬP TỨC: Đưa camera chính đến ngay tọa độ của Player mới
            // Việc này giúp camera không bị kẹt ở Map 1 hoặc lướt quá chậm gây giật hình.
            if (player != null)
            {
                cam.transform.position = new Vector3(
                    player.position.x,
                    player.position.y,
                    cam.transform.position.z // Giữ nguyên tọa độ Z (-10) để tránh lỗi hiển thị
                );
            }
        }

        // 3. Bật toàn bộ các background/vật thể thuộc khu vực này lên
        SetAreaObjectsActive(true);
    }

    /// <summary>
    /// Tắt khu vực này: ẩn toàn bộ background/vật thể để tối ưu hiệu năng và tránh bị đè lên khu vực khác.
    /// </summary>
    public void DeactivateArea()
    {
        SetAreaObjectsActive(false);
    }

    private void SetAreaObjectsActive(bool activeState)
    {
        if (areaObjects == null) return;
        
        foreach (var obj in areaObjects)
        {
            if (obj != null)
            {
                obj.SetActive(activeState);
            }
        }
    }
}

using UnityEngine;

/// <summary>
/// Giúp Background tự động khóa vị trí và chạy theo Camera chính ở trục X và Y.
/// Giữ nguyên trục Z ban đầu để tránh lỗi đè lên Player/Tilemap.
/// </summary>
public class BackgroundFollow : MonoBehaviour
{
    private Transform _cameraTransform;

    [Header("=== CẤU HÌNH LỆCH ===")]
    [Tooltip("Độ lệch vị trí so với tâm Camera nếu cần")]
    [SerializeField] private Vector2 offset = Vector2.zero;

    void Start()
    {
        // Tự động tìm Camera chính trong Scene
        if (Camera.main != null)
        {
            _cameraTransform = Camera.main.transform;
        }
    }

    void LateUpdate()
    {
        if (_cameraTransform != null)
        {
            // Cập nhật vị trí X, Y theo Camera, giữ nguyên Z gốc của ảnh nền
            transform.position = new Vector3(
                _cameraTransform.position.x + offset.x,
                _cameraTransform.position.y + offset.y,
                transform.position.z
            );
        }
    }
}

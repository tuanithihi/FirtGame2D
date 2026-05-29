using UnityEngine;

/// <summary>
/// CameraController điều khiển Camera chính chạy theo Player một cách mượt mà.
/// Sử dụng Vector3.SmoothDamp để triệt tiêu hiện tượng rung giật (jitter).
/// Hỗ trợ: giới hạn biên bản đồ (bounds), nhìn trước hướng di chuyển (look ahead), và hiệu ứng rung màn hình (camera shake).
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("=== TARGET ===")]
    [Tooltip("Target cần đi theo (thường là Player)")]
    [SerializeField] private Transform target;

    [Header("=== SMOOTHING & OFFSET ===")]
    [Tooltip("Độ trễ/mượt của camera (càng nhỏ càng bám sát)")]
    [SerializeField] private float smoothTime = 0.25f;
    [Tooltip("Khoảng cách lệch giữa Camera và Player")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 1f, -10f);

    [Header("=== GIỚI HẠN BẢN ĐỒ (BOUNDS) ===")]
    [SerializeField] private bool enableBounds = false;
    [SerializeField] private Vector2 minBounds;
    [SerializeField] private Vector2 maxBounds;

    [Header("=== NHÌN TRƯỚC HƯỚNG DI CHUYỂN (LOOK AHEAD) ===")]
    [SerializeField] private bool enableLookAhead = true;
    [SerializeField] private float lookAheadDistance = 2f;
    [SerializeField] private float lookAheadSpeed = 3f;

    [Header("=== HIỆU ỨNG RUNG MÀN HÌNH (SCREEN SHAKE) ===")]
    [SerializeField] private float shakeMagnitude = 0.1f;
    [SerializeField] private float dampingSpeed = 1f;

    // Các biến phụ trợ nội bộ
    private Vector3 _velocity = Vector3.zero;
    private Vector3 _currentOffset;
    private PlayerController _playerController;
    private Vector3 _initialShakePosition;
    private float _currentShakeDuration;

    private void Start()
    {
        // Tự động tìm Player nếu chưa gán Target trong Inspector
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
            else
            {
                // Tìm kiếm thông qua Class PlayerController nếu không có Tag
                PlayerController pc = FindFirstObjectByType<PlayerController>();
                if (pc != null)
                {
                    target = pc.transform;
                }
            }
        }

        if (target != null)
        {
            _playerController = target.GetComponent<PlayerController>();
            // Đưa camera về vị trí mục tiêu ngay lập tức khi bắt đầu để tránh bị lướt từ gốc tọa độ
            Vector3 targetPosition = target.position + offset;
            if (enableBounds)
            {
                targetPosition.x = Mathf.Clamp(targetPosition.x, minBounds.x, maxBounds.x);
                targetPosition.y = Mathf.Clamp(targetPosition.y, minBounds.y, maxBounds.y);
            }
            transform.position = targetPosition;
        }

        _currentOffset = offset;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // 1. Xử lý Look Ahead (nhìn trước theo hướng quay mặt của Player)
        if (enableLookAhead)
        {
            float targetLookAheadX = 0f;

            if (_playerController != null)
            {
                try
                {
                    // Sử dụng hướng đối mặt từ PlayerController
                    targetLookAheadX = _playerController.IsFacingRight ? lookAheadDistance : -lookAheadDistance;
                }
                catch (UnityEngine.MissingReferenceException)
                {
                    _playerController = null;
                    targetLookAheadX = 0f;
                }
            }
            else
            {
                // Nếu không có PlayerController, dựa trên hướng di chuyển hiện tại của target
                float targetVelocityX = (target.position - transform.position).x;
                if (Mathf.Abs(targetVelocityX) > 0.01f)
                {
                    targetLookAheadX = targetVelocityX > 0 ? lookAheadDistance : -lookAheadDistance;
                }
            }

            // Lerp mượt mà offset X
            _currentOffset.x = Mathf.Lerp(_currentOffset.x, offset.x + targetLookAheadX, Time.deltaTime * lookAheadSpeed);
        }
        else
        {
            _currentOffset.x = offset.x;
        }

        _currentOffset.y = offset.y;
        _currentOffset.z = offset.z;

        // 2. Tính toán vị trí mong muốn của Camera
        Vector3 targetPosition = target.position + _currentOffset;

        // 3. Giới hạn camera trong khoảng biên bản đồ (nếu bật)
        if (enableBounds)
        {
            targetPosition.x = Mathf.Clamp(targetPosition.x, minBounds.x, maxBounds.x);
            targetPosition.y = Mathf.Clamp(targetPosition.y, minBounds.y, maxBounds.y);
        }

        // 4. Di chuyển camera mượt mà bằng SmoothDamp
        Vector3 newPos = Vector3.SmoothDamp(transform.position, targetPosition, ref _velocity, smoothTime);

        // 5. Xử lý Screen Shake (Rung màn hình) nếu có yêu cầu
        if (_currentShakeDuration > 0)
        {
            newPos += (Vector3)Random.insideUnitCircle * shakeMagnitude;
            _currentShakeDuration -= Time.deltaTime * dampingSpeed;
        }

        transform.position = newPos;
    }

    /// <summary>
    /// Kích hoạt hiệu ứng rung màn hình (ví dụ: khi nhận sát thương, nổ, hoặc tung chiêu mạnh).
    /// </summary>
    /// <param name="duration">Thời gian rung (giây)</param>
    /// <param name="magnitude">Độ mạnh/biên độ rung</param>
    public void Shake(float duration, float magnitude)
    {
        _currentShakeDuration = duration;
        shakeMagnitude = magnitude;
    }
}

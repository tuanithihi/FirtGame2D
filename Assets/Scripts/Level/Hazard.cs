using UnityEngine;

/// <summary>
/// Quản lý các vùng nguy hiểm (chông, gai, vực thẳm, dung nham).
/// Tự động gây sát thương hoặc giết chết người chơi ngay lập tức khi va chạm.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class Hazard : MonoBehaviour
{
    [Header("=== CẤU HÌNH CẠM BẪY ===")]
    [Tooltip("Tích chọn nếu muốn người chơi chết ngay lập tức khi chạm vào (Rơi xuống vực, chông nhọn)")]
    [SerializeField] private bool isInstantKill = true;

    [Tooltip("Lượng sát thương gây ra nếu không phải là chết ngay lập tức")]
    [SerializeField] private float damage = 20f;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Xử lý khi va chạm dạng Trigger (đi xuyên qua)
        HandleHazardCollision(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Xử lý khi va chạm dạng Vật lý cứng (đâm vào)
        HandleHazardCollision(collision.gameObject);
    }

    /// <summary>
    /// Áp dụng sát thương hoặc cái chết lên đối tượng chạm phải.
    /// </summary>
    private void HandleHazardCollision(GameObject target)
    {
        // Kiểm tra xem đối tượng va chạm có phải là Player không
        if (target.CompareTag("Player"))
        {
            if (target.TryGetComponent<PlayerCombat>(out var playerCombat))
            {
                if (isInstantKill)
                {
                    // Gây lượng sát thương cực đại để người chơi chết ngay lập tức
                    playerCombat.ReceiveDamage(99999f);
                }
                else
                {
                    // Trừ một lượng máu theo cấu hình
                    playerCombat.ReceiveDamage(damage);
                }
            }
        }
    }
}

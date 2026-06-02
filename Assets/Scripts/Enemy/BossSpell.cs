using System.Collections;
using UnityEngine;

public class BossSpell : MonoBehaviour
{
    [Header("=== Cấu Hình Sát Thương ===")]
    [SerializeField] private float damage = 20f;
    [SerializeField] private float damageRadius = 1.5f;     // Bán kính vụ nổ dưới mặt đất
    [SerializeField] private LayerMask playerLayer;         // Layer chứa Player
    [SerializeField] private float damageDelay = 1.2f;       // Thời gian trễ chờ hiệu ứng rơi xuống nổ (né được)
    [SerializeField] private float destroyDelay = 2.0f;     // Thời gian tự hủy tổng cộng của cả Prefab

    private Vector3 _groundPosition;

    // Hàm khởi tạo vị trí nổ dưới đất (được gọi từ BossController khi sinh ra phép)
    public void Initialize(Vector3 groundPos)
    {
        _groundPosition = groundPos;
        StartCoroutine(ExplosionRoutine());
    }

    private void Start()
    {
        // Tự động xóa Prefab sau khi kết thúc hoàn toàn hoạt ảnh
        Destroy(gameObject, destroyDelay);
    }

    private IEnumerator ExplosionRoutine()
    {
        // Chờ dải phép rơi từ trên đầu xuống đất (trong thời gian này Player kịp di chuyển ra khỏi vòng tròn)
        yield return new WaitForSeconds(damageDelay);

        // Thực hiện nổ gây sát thương tại vị trí mặt đất đã khóa
        Explode();
    }

    private void Explode()
    {
        // Quét tìm Player tại vị trí mặt đất khóa ban đầu
        Collider2D hit = Physics2D.OverlapCircle(_groundPosition, damageRadius, playerLayer);
        if (hit != null)
        {
            if (hit.TryGetComponent<PlayerCombat>(out var playerCombat))
            {
                playerCombat.ReceiveDamage(damage);
                Debug.Log($"[Boss Spell] Player bị trúng vụ nổ phép thuật dưới đất! Sát thương: {damage}");
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Hiển thị vòng tròn sát thương dưới mặt đất trong Editor
        Gizmos.color = Color.red;
        if (Application.isPlaying)
        {
            Gizmos.DrawWireSphere(_groundPosition, damageRadius);
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position + Vector3.down * 3.5f, damageRadius);
        }
    }
}

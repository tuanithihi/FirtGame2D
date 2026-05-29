using UnityEngine;

/// <summary>
/// Interface chung để nhận sát thương.
/// Implement interface này trên Enemy, Boss, hoặc bất kỳ object nào có thể bị đánh.
/// </summary>
public interface IDamageable
{
    void TakeDamage(float damage);
}

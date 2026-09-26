/// <summary>
/// Sasaran yang bisa menerima damage.
///
/// Ada karena sebelumnya tidak ada jalur damage generik sama sekali: codebase
/// cuma punya <see cref="IDamageableParryable"/> yang isinya cuma TryParry(),
/// jadi satu-satunya cara melukai sesuatu adalah tahu tipe konkretnya
/// (GetComponent&lt;EnemyHealth&gt;().Health.TakeDamage()). Bullet tidak bisa
/// begitu - dia harus bisa mengenai apa pun yang punya health, tanpa perlu
/// referensi ke PlayerHealth atau EnemyHealth.
///
/// Health ikut di-expose (bukan cuma TakeDamage) supaya pemanggil bisa baca
/// MaxHealth / CurrentHealth / IsDead tanpa casting ke tipe konkret.
/// </summary>
public interface IDamageable
{
    HealthSystem Health { get; }

    void TakeDamage(float amount);
}

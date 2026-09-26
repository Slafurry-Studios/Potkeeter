using Slafurry.Utils.Pooling;
using UnityEngine;

/// <summary>
/// Peluru yang punya health, dan geraknya dicek dengan BoxCast2D - tidak ada
/// Rigidbody2D sama sekali.
///
/// Kenapa tidak Rigidbody2D: peluru cuma butuh "bergerak lurus + cek nabrak".
/// Rigidbody2D untuk itu mahal, sementara BoxCast2D memberi hasil yang sama
/// dengan satu query.
///
/// Stats-nya ada di komponen ini, jadi satu prefab = satu tipe peluru.
/// BulletManager membuat pool sendiri untuk tiap prefab, jadi banyak tipe
/// peluru bisa hidup berdampingan tanpa perlu daftarkan apa pun.
///
/// Trade-off: BoxCast hanya mengembalikan hit PERTAMA dalam satu langkah, jadi
/// peluru sangat cepat bisa melewati target tipis. Turunkan speed, jangan
/// ganti balik ke Rigidbody2D.
///
/// Karena tidak ada collider, peluru tidak bisa di-detect lewat
/// Physics2D.Overlap* dari script lain.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class Bullet : MonoBehaviour, IPoolable, IDamageable
{
    public HealthSystem Health { get; private set; }

    [Header("Collision")]
    [Tooltip("Ukuran kotak BoxCast: x = panjang (ikut arah gerak, membentang ke depan DAN ke belakang), y = lebar. Diukur dari titik pusat peluru, bukan dari sprite.")]
    [SerializeField] private Vector2 boxSize = new Vector2(0.15f, 0.15f);
    [Tooltip("Layer yang boleh terkena peluru ini. Layer Bullet sendiri WAJIB dikecualikan, kalau tidak peluru akan nabrak peluru lain.")]
    [SerializeField] private LayerMask hitMask;

    // Ukuran Kotak selalu dipaksa positif. Nilai negatif di inspector membuat
    // Physics2D.BoxCast membalik shape-nya dan diam-diam tidak pernah hit
    // apa pun - dan karena itu juga menggeser asal cast ke belakang, gejalanya
    // tidak kelihatan sama sekali.
    private Vector2 EffectiveBoxSize => new Vector2(Mathf.Abs(boxSize.x), Mathf.Abs(boxSize.y));

    [Header("Stats")]
    [SerializeField, Min(0.1f)] private float speed = 6f;
    [SerializeField, Min(0f)] private float damage = 1f;
    [Tooltip("Health awal. Peluru bisa dihancurkan atau diparry sebelum kena target.")]
    [SerializeField, Min(1f)] private float maxHealth = 1f;
    [Tooltip("Maksimum umur peluru sebelum dibersihkan sendiri, supaya tidak menumpuk kalau tembakannya meleset semua.")]
    [SerializeField, Min(0.1f)] private float maxLifetime = 4f;

    [Header("Gizmos")]
    [Tooltip("Gambar kotak BoxCast dan jalur peluru di editor.")]
    [SerializeField] private bool showGizmos = true;
    [Tooltip("Panjang jalur yang digambar, dalam unit. Sweep asli per FixedUpdate cuma speed * fixedDeltaTime, jadi pendek sekali untuk dilihat.")]
    [SerializeField, Min(0f)] private float gizmoPathLength = 3f;

    private Vector2 _direction;
    private float _lifetime;
    private bool _spawned;

    /// <summary>Atur posisi dan arah, lalu mulai terbang. Dipanggil BulletManager setelah Get() dari pool.</summary>
    public void Launch(Vector2 origin, Vector2 direction)
    {
        // HealthSystem dibuat ulang tiap spawn karena tidak punya Reset(), dan
        // Heal() tidak jalan kalau object sudah IsDead - jadi peluru yang sudah
        // hancur tidak bisa di-heal balik ke hidup.
        Health = new HealthSystem(maxHealth);
        Health.OnDeath += HandleDeath;

        _direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        _lifetime = maxLifetime;

        transform.position = origin;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(_direction.y, _direction.x) * Mathf.Rad2Deg);
    }

    private void FixedUpdate()
    {
        // Pool mengaktifkan object sebelum Launch() dipanggil, jadi cek ini
        // mencegah satu FixedUpdate dengan arah sisa dari penggunaan sebelumnya.
        if (!_spawned) return;

        _lifetime -= Time.fixedDeltaTime;
        if (_lifetime <= 0f)
        {
            Despawn();
            return;
        }

        float step = speed * Time.fixedDeltaTime;
        float angle = transform.eulerAngles.z;
        Vector2 size = EffectiveBoxSize;

        // Cast dari titik pusat peluru, jadi size.x membentang sama ke depan
        // dan ke belakang. Kalau asal cast digeser maju, kotaknya cuma
        // menggambar di sebelah kanan dan kelihatan rusak.
        //
        // Aman soal collider muzzle: BoxCast tidak melaporkan collider yang
        // sudah tumpang tindih di posisi awal, cuma yang benar-benar dimasuki
        // sewaktu menyapu.
        Vector2 origin = (Vector2)transform.position;

        RaycastHit2D hit = Physics2D.BoxCast(origin, size, angle, _direction, step, hitMask);
        if (hit.collider != null)
        {
            DealDamage(hit);
            Despawn();
            return;
        }

        transform.position += (Vector3)(_direction * step);
    }

    private void DealDamage(RaycastHit2D hit)
    {
        // GetComponentInParent, karena collider yang kena bisa berada di child
        // sementara health-nya ada di object root.
        IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
        if (target == null) return;

        target.TakeDamage(damage);
    }

    private void HandleDeath() => Despawn();

    /// <summary>Peluru bisa dilukai, jadi bisa diparry atau ditembak jatuh sebelum sampai target.</summary>
    public void TakeDamage(float amount)
    {
        if (Health == null) return;
        Health.TakeDamage(amount);
    }

    private void Despawn()
    {
        // collectionCheck di GenericPool bernilai true, jadi Release dua kali
        // akan throw. Fade-out dan OnDeath bisa menumpuk di frame yang sama.
        if (!_spawned) return;
        _spawned = false;
        BulletManager.Instance?.Release(this);
    }

    // ===== IPoolable - dipanggil GenericPool otomatis =====

    public void OnSpawn() => _spawned = true;

    public void OnDespawn() => _spawned = false;

    #region Gizmos

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        // Launch() sudah memutar transform ke arah gerak, jadi local +X adalah
        // arah cast. Pakai transform.right supaya gizmo juga benar saat
        // object belum pernah di-launch (misalnya prefab yang baru dibuat).
        Vector2 direction = transform.right;
        float angle = transform.eulerAngles.z;

        // Sweep yang FixedUpdate benar-benar lakukan frame ini.
        Vector2 size = EffectiveBoxSize;
        float step = speed * Time.fixedDeltaTime;
        Vector2 castOrigin = (Vector2)transform.position;
        Vector2 castEnd = castOrigin + direction * step;

        Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.95f);
        DrawBox(castOrigin, angle, size);
        Gizmos.color = new Color(1f, 0.85f, 0.1f, 0.5f);
        DrawBox(castEnd, angle, size);
        Gizmos.DrawLine(castOrigin, castEnd);

        // Jalur perkiraan yang panjang, supaya arah dan jangkauan bisa
        // dibaca tanpa mengejar peluru yang bergerak.
        if (gizmoPathLength > 0f)
        {
            Vector2 pathEnd = (Vector2)transform.position + direction * gizmoPathLength;
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.55f);
            Gizmos.DrawLine((Vector2)transform.position, pathEnd);

            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
            Gizmos.DrawWireSphere(pathEnd, 0.04f);
        }
    }

    private void DrawBox(Vector2 center, float angleDegrees, Vector2 size)
    {
        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(center, Quaternion.Euler(0f, 0f, angleDegrees), Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, size.y, 0f));
        Gizmos.matrix = previous;
    }

    #endregion
}

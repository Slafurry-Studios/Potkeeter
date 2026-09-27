using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Jebakan berbentuk area yang melukai player setiap kali player masuk ke
/// dalamnya, dan bisa dicounter parry seperti peluru atau musuh.
///
/// Parry tidak menghapus jebakan ini. Yang dilumpuhkan hanya damage-nya:
/// begitu OnParried() dipanggil, area ini tidak melukai sampai parryGraceDuration
/// habis. Parry juga mendorong player keluar dari area (parryLaunchForce di
/// BayonetController), jadi grace period itulah yang menutup diri sendiri saat
/// player masuk lagi. Kalau parryGraceDuration diisi 0, jebakan lumpuh
/// permanen sampai objeknya di-disable.
///
/// Dua hal yang harus dicek di editor kalau parry terlihat tidak bekerja:
/// 1. Collider2D-nya harus centang isTrigger, kalau tidak OnTriggerEnter2D
///    tidak akan pernah dipanggil sama sekali.
/// 2. Layer objek ini harus termasuk di parryRadiusMask milik
///    BayonetController. Kalau tidak, Physics2D.OverlapCircleAll di
///    ExecuteParryLogic() tidak akan menemukan jebakan ini, dan parry hanya
///    akan log "[Parry Missed]" tanpa penjelasan lain.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class HazardArea : MonoBehaviour, IParryable
{
    [Header("Damage")]
    [Tooltip("Damage yang diberikan setiap kali player masuk ke area ini.")]
    [SerializeField] private float damage = 10f;

    [Header("Parry")]
    [Tooltip("Berapa detik area ini tidak melukai setelah diparry. 0 = lumpuh permanen sampai objeknya di-disable.")]
    [SerializeField] private float parryGraceDuration = 1f;

    [Header("Events")]
    [Tooltip("Dipanggil saat player benar-benar kena damage dari area ini.")]
    [SerializeField] private UnityEvent onPlayerDamaged;

    [Tooltip("Dipanggil saat area ini berhasil diparry.")]
    [SerializeField] private UnityEvent onParried;

    private float defusedUntil;
    private bool latchedDefused;

    /// <summary>
    /// True kalau area ini sedang tidak bisa melukai, baik karena grace period
    /// yang masih berjalan atau karena sudah lumpuh permanen.
    /// </summary>
    public bool IsDefused => latchedDefused || Time.time < defusedUntil;

    private void Awake()
    {
        Collider2D col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning($"[{nameof(HazardArea)}] Collider2D di '{name}' belum centang isTrigger, " +
                             "jadi OnTriggerEnter2D tidak akan pernah dipanggil.", this);
        }

        if (gameObject.layer == 0)
        {
            Debug.LogWarning($"[{nameof(HazardArea)}] '{name}' masih di layer Default. Kalau parry tidak menemukan " +
                             "jebakan ini, cek parryRadiusMask di BayonetController - yang defaultnya hanya centang Enemy.", this);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsDefused) return;

        PlayerHealth player = FindPlayer(other);
        if (player == null) return;

        player.TakeDamage(damage);
        onPlayerDamaged?.Invoke();
    }

    /// <summary>
    /// Dipanggil BayonetController saat area ini berada di dalam parryRadius
    /// pada saat player menekan parry. Tidak mengembalikan apa pun, sama seperti
    /// implementasi IParryable lainnya: parry tidak punya syarat, satu-satunya
    /// penentu berhasil atau tidaknya adalah apa area ini ada di dalam area parry.
    /// </summary>
    public void OnParried()
    {
        if (parryGraceDuration <= 0f) latchedDefused = true;
        else defusedUntil = Time.time + parryGraceDuration;

        onParried?.Invoke();
    }

    /// <summary>
    /// PlayerHealth ada di objek 'Pot', tapi lengan dan bayonet punya collider
    /// sendiri yang tidak berada di bawah 'Pot'. Karena itu jangan hanya
    /// GetComponentInParent per collider, yang akan melewatkan lengan; bandingkan
    /// saja dengan root player.
    /// </summary>
    private static PlayerHealth FindPlayer(Collider2D other)
    {
        PlayerHealth player = PlayerHealth.Instance;
        if (player == null) return null;

        Transform root = player.transform.root;
        return other.transform == root || other.transform.IsChildOf(root) ? player : null;
    }
}

using UnityEngine;

/// <summary>
/// Satu laras pada satu musuh. Semua state tembaknya ada di instance ini
/// sendiri, bukan di struct bersama yang dibagi ke semua laras.
///
/// Ini poin kuncinya untuk "satu musuh bisa punya lebih dari satu shooter":
/// kalau cooldown, sisa burst, dan timer aim disimpan di DroneController,
/// dua laras akan saling menimpa timer yang sama dan drone itu hanya akan
/// menembak dari satu laras saja. Dengan satu set state per component, drone
/// dengan tiga laras akan benar-benar menembak tiga laras, masing-masing
/// dengan ritmenya sendiri.
///
/// Siklusnya: putar laras ke target, tunggu sampai sudah cukup aimed
/// (aimTolerance), baru mulai burst. Setelah burst habis, baru cooldown.
/// </summary>
public class EnemyShooter : MonoBehaviour
{
    [Header("Mount")]
    [Tooltip("Transform ujung laras. Kalau kosong, pakai transform milik component ini.")]
    [SerializeField] private Transform muzzle;
    [Tooltip("Transform yang diputar untuk membidik. Kalau kosong, laras tidak diputar dan dianggap selalu siap.")]
    [SerializeField] private Transform barrel;

    [Header("Timing")]
    [Tooltip("Jeda antar burst, dihitung setelah burst sebelumnya habis.")]
    [SerializeField, Min(0.05f)] private float fireInterval = 1.4f;
    [Tooltip("Jeda antar peluru di dalam satu burst.")]
    [SerializeField, Min(0.02f)] private float burstInterval = 0.1f;
    [Tooltip("Jumlah peluru per burst.")]
    [SerializeField, Min(1)] private int burstCount = 2;

    [Header("Aiming")]
    [Tooltip("Seberapa dekat laras harus mengarah ke target sebelum boleh tembak, dalam derajat.")]
    [SerializeField, Range(1f, 45f)] private float aimTolerance = 10f;
    [Tooltip("Kecepatan memutar laras, derajat per detik.")]
    [SerializeField, Min(10f)] private float turnSpeed = 540f;
    [Tooltip("Koreksi orientasi laras terhadap sprite. Sprite laras digambar terbalik, jadi transform harus diberi tambahan 180 derajat supaya ujung laras dan muzzle berada di sisi depan, bukan belakang. 0 = tanpa koreksi. TIDAK memengaruhi arah peluru: peluru tetap lurus ke target.")]
    [SerializeField, Range(-180f, 180f)] private float angleOffsetDegrees = 0f;

    [Header("Bullet")]
    [Tooltip("Prefab peluru yang ditembakkan. Stats peluru diset di prefab ini, jadi satu shooter bisa ditukar tipe pelurunya tanpa menyentuh kode.")]
    [SerializeField] private Bullet bulletPrefab;
    [Tooltip("Sebar acak arah tembakan, dalam derajat total. 0 = presisi. Ini milik shooter, bukan peluru, jadi tetap ada di sini.")]
    [SerializeField, Range(0f, 45f)] private float spreadDegrees = 0f;

    [Header("SFX")]
    [Tooltip("Nama key di sfxSounds milik AudioSystem yang diputar tiap peluru keluar. Kosongkan kalau laras ini tidak berbunyi. Bukan nama file, tapi key yang terdaftar di array sfxSounds di Boot.unity.")]
    [SerializeField] private string fireSFX = "EnemyLaser";

    // Semua milik instance ini. Jangan dipindah ke DroneController.
    private float _nextBurstTime;
    private int _burstRemaining;
    private float _nextShotTime;

    private Transform Muzzle => muzzle != null ? muzzle : transform;

    /// <summary>
    /// Dipanggil drone setiap frame saat dalam state Attack.
    /// Menjalankan satu tick penuh: putar laras, cek aim, lalu tembak kalau
    /// cooldown sudah siap dan laras sudah terbidik.
    /// </summary>
    public void Tick(Vector2 target)
    {
        bool aimed = AimAt(target);
        float now = Time.time;

        if (_burstRemaining > 0)
        {
            if (now < _nextShotTime) return;

            Fire(target);
            _burstRemaining--;
            _nextShotTime = now + burstInterval;

            if (_burstRemaining == 0) _nextBurstTime = now + fireInterval;
            return;
        }

        // Burst baru hanya boleh mulai kalau laras sudah cukup mengarah ke
        // target. Ini yang bikin "aim lalu shoot" kelihatan, dan sekaligus
        // mencegah drone menembak ke tembok karena rotasi laras belum
        // selesai.
        if (!aimed || now < _nextBurstTime) return;

        _burstRemaining = burstCount;
        _nextShotTime = now;
    }

    /// <summary>
    /// Membatalkan burst yang sedang jalan dan menunda burst berikutnya.
    /// Dipanggil DroneController.OnParried() supaya counter tidak langsung
    /// diserobot sisa peluru yang sudah terlanjur keluar dari laras.
    /// </summary>
    public void Interrupt()
    {
        _burstRemaining = 0;
        _nextBurstTime = Time.time + fireInterval;
    }

    /// <summary>Memutar laras ke arah target. Mengembalikan true kalau sudah cukup terbidik.</summary>
    private bool AimAt(Vector2 target)
    {
        if (barrel == null) return true;

        Vector2 from = Muzzle.position;
        float desired = Mathf.Atan2(target.y - from.y, target.x - from.x) * Mathf.Rad2Deg;

        // Offset ditambahkan di sini, bukan ke arah peluru. Barrel dirotasi ke
        // sudut aim absolut, jadi +X lokal barrel selalu mengarah ke arah
        // tembak: tanpa koreksi, muzzle yang ada di -X lokal berakhir di
        // belakang drone dan peluru lahir di dalam badannya sendiri. Memutar
        // laras 180 derajat membalikkan itu tanpa mengubah arah peluru,
        // karena Fire() tetap menghitung arah dari muzzle ke target terpisah.
        desired += angleOffsetDegrees;

        float current = barrel.eulerAngles.z;
        float next = Mathf.MoveTowardsAngle(current, desired, turnSpeed * Time.deltaTime);

        barrel.rotation = Quaternion.Euler(0f, 0f, next);

        return Mathf.Abs(Mathf.DeltaAngle(next, desired)) <= aimTolerance;
    }

    private void Fire(Vector2 target)
    {
        Vector2 origin = Muzzle.position;
        Vector2 direction = target - origin;

        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;
        direction.Normalize();

        if (spreadDegrees > 0f)
        {
            float jitter = Random.Range(-spreadDegrees * 0.5f, spreadDegrees * 0.5f);
            direction = Quaternion.Euler(0f, 0f, jitter) * direction;
        }

        Bullet bullet = BulletManager.Instance?.Spawn(bulletPrefab, origin, direction);

        // Spawn mengembalikan null kalau pool-nya tidak bisa dibuat (prefab
        // null, atau BulletManager belum ada), jadi ini dicek betulan, bukan
        // hanya di komentar: kalau peluru gagal dibuat, jangan sampai
        // kedengaran suara tembakan tanpa apa-apa yang keluar.
        //
        // waitForCompletion sengaja false. Dalam satu burst, peluru ditembak
        // burstInterval (0.1s) apart, dan PlaySFX memakai satu AudioSource per
        // key yang di-Stop() dulu sebelum Play(). Kalau di-queue, bunyi laras
        // kedua baru berbunyi setelah yang pertama selesai, padahal bunyi
        // pertama sudah berhenti sebelum peluru kedua terbit - jadi bunyi tidak
        // sinkron dengan peluru sejak peluru pertama. Dipotong begini justru
        // terbaca seperti tembakan otomatis yang cepat.
        if (bullet == null) return;

        if (!string.IsNullOrEmpty(fireSFX))
        {
            AudioSystem.Instance?.PlaySFX(fireSFX, waitForCompletion: false);
        }
    }
}

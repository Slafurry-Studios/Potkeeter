using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Satu tentakel MindMaster: keluar dari anchor, menahan diri sambil menembak
/// lewat anak-anaknya, lalu ditarik balik dan diam sampai giliran berikutnya.
///
/// Yang digerakkan adalah GameObject ini sendiri, bukan cuma obyek visualnya.
/// Kalau hanya visual yang bergerak, shooter dan collider HazardArea akan
/// tertinggal di anchor sehingga jebakannya tidak ikut keluar, padahal
/// forearnya sudah menjulang ke arena.
///
/// GameObject ini juga tidak pernah dinonaktifkan - hanya collider hazard-nya
/// yang dimatikan. Alasannya Awake: Unity menunda Awake pada obyek yang
/// nonaktif, jadi kalau GameObject ini starts hidden, GetComponentsInChildren
/// di dalam Awake tidak akan pernah jalan dan daftar shooternya akan kosong.
/// Collider yang dimatikan lalu dihidupkan lagi tiap siklus tetap memicu
/// OnTriggerEnter2D untuk player yang sudah ada di dalam area, jadi hazard
/// selalu ter-trigger ulang setiap kali tentakel keluar.
///
/// Sisa-sisa burst dibersihkan lewat EnemyShooter.Interrupt() saat menarik
/// diri, bukan cuma dengan SetActive: SetActive tidak mereset field private
/// milik EnemyShooter, jadi tanpa itu sisa burst yang belum tembak akan
/// diteruskan lagi di serangan berikutnya.
/// </summary>
public class BossTentacle : MonoBehaviour
{
    /// <summary>
    /// Jarak titik bantu di luar ujung tentakel. Cukup jauh supaya selisih
    /// sudut ke arah mana pun tetap terbaca, tapi tidak perlu lebih besar dari
    /// ukuran arena karena yang dipakai cuma arahnya.
    /// </summary>
    private const float BlindAimDistance = 20f;

    [Header("Gerakan (offset dari posisi anchor)")]
    [Tooltip("Posisi saat disembunyikan. Taruh anchor di dalam plafon atau dinding agar ini tidak terlihat.")]
    [SerializeField] private Vector2 retractedOffset = Vector2.zero;

    [Tooltip("Posisi saat keluar penuh ke arena. Ingat +Y itu atas di Unity 2D, jadi tendakel dari plafon memakai y negatif, dan dari dinding kiri memakai x positif.")]
    [SerializeField] private Vector2 extendedOffset = new Vector2(0f, -2.5f);

    [SerializeField, Min(0.05f)] private float extendDuration = 0.45f;
    [SerializeField, Min(0f)] private float holdDuration = 2.5f;
    [SerializeField, Min(0.05f)] private float retractDuration = 0.4f;

    [Tooltip("Jeda setelah menarik diri sebelum boleh dipanggil lagi. Selama cooldown ini IsBusy masih true, jadi controller tidak akan memilih tentakel ini lagi.")]
    [SerializeField, Min(0f)] private float cooldown = 1.2f;

    [Header("Pembidikan")]
    [Tooltip("Kalau false, laras anak-anak tidak mengejar player dan menembak lurus ke luar mengikuti arah keluarnya tentakel ini. Dipakai controller untuk memberi titik buta pada tentakel terdekat. HazardArea di badan tetap aktif, jadi ini cuma soal membidik, bukan mengurangi damage kontak.")]
    [SerializeField] private bool tracksPlayer = true;

    private EnemyShooter[] _shooters = Array.Empty<EnemyShooter>();
    private HazardArea[] _hazards = Array.Empty<HazardArea>();

    private Coroutine _routine;
    private Vector3 _anchor;
    private Vector2 _offset;
    private int _shooterLimit = int.MaxValue;

    /// <summary>True selama satu siklus penuh, termasuk cooldown.</summary>
    public bool IsBusy { get; private set; }

    /// <summary>True hanya ketika tentakel benar-benar keluar di arena.</summary>
    public bool IsExtended { get; private set; }

    /// <summary>Jumlah child shooter yang ditemukan di bawah tentakel ini.</summary>
    public int ShooterCount => _shooters.Length;

    private void Awake()
    {
        _anchor = transform.position;

        // includeInactive wajib. Shooter dan hazard duduk di anak-anak yang
        // GameObject-nya sengaja dinonaktifkan, dan tanpa flag ini keduanya
        // tidak akan ditemukan sama sekali.
        _shooters = GetComponentsInChildren<EnemyShooter>(true);
        _hazards = GetComponentsInChildren<HazardArea>(true);

        _offset = retractedOffset;
        ApplyOffset();
        SetHazardActive(false);
    }

    /// <summary>
    /// Memulai satu siklus keluar-tahan-masuk. Diabaikan kalau tentakel ini
    /// sedang sibuk, jadi controller boleh memanggilnya tanpa mengecek dulu.
    /// </summary>
    public void PlayAttack()
    {
        if (IsBusy || !isActiveAndEnabled) return;
        _routine = StartCoroutine(AttackRoutine());
    }

    /// <summary>
    /// Memberi tahu shooter anak-anak arah target. Dipanggil controller tiap
    /// frame, hanya untuk shooter sesuai shooterLimit.
    /// </summary>
    public void Tick(Vector2 target)
    {
        if (!IsExtended) return;

        int count = Mathf.Min(_shooterLimit, _shooters.Length);
        if (count <= 0) return;

        Vector2 aim = tracksPlayer ? target : BlindAimPoint();

        for (int i = 0; i < count; i++)
        {
            if (_shooters[i] != null) _shooters[i].Tick(aim);
        }
    }

    /// <summary>
    /// Mengatur apakah tentakel ini membidik player. Dipanggil controller
    /// tiap frame untuk menandai mana yang jadi titik buta.
    /// </summary>
    public void SetTracking(bool value)
    {
        tracksPlayer = value;
    }

    /// <summary>True kalau tentakel ini sedang membidik player.</summary>
    public bool IsTracking => tracksPlayer;

    /// <summary>
    /// Titik yang diarahkan laras saat titik buta aktif: jauh ke luar mengikuti
    /// arah keluarnya tentakel ini sendiri, jadi peluru terbang menjauhi badan
    /// dan tidak pernah menoleh ke player.
    ///
    /// Yang dikembalikan adalah titik, bukan arah tetap, karena
    /// EnemyShooter menghitung sudut laras dan arah peluru dari muzzle ke
    /// target. Selama titik ini berada di luar ujung laras, keduanya mengarah
    /// ke sana tanpa pernah perlu tahu posisi player.
    /// </summary>
    private Vector2 BlindAimPoint()
    {
        Vector2 outDir = (extendedOffset - retractedOffset);
        if (outDir.sqrMagnitude < 0.0001f) outDir = Vector2.down;
        return (Vector2)transform.position + outDir.normalized * BlindAimDistance;
    }

    /// <summary>
    /// Membatasi berapa-many shooter pertama yang boleh menembak. Urutan
    /// mengikuti hierarki, jadi anak yang lebih dekat ke anchor ditembakkan
    /// lebih dulu - itu yang membuat fase awal bisa menyalakan shooter
    /// terdekat saja lalu melepaskannya semua di fase akhir.
    /// </summary>
    public void SetShooterLimit(int limit)
    {
        _shooterLimit = Mathf.Max(0, limit);
    }

    /// <summary>
    /// Menghentikan apa pun yang sedang jalan dan mengembalikan tentakel ke
    /// posisi tersembunyi. Dipakai saat pindah fase dan saat boss mati.
    /// </summary>
    public void StopAll()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        InterruptShooters();
        _offset = retractedOffset;
        ApplyOffset();
        SetHazardActive(false);
        IsBusy = false;
        IsExtended = false;
    }

    private void OnDisable()
    {
        // Menonaktifkan GameObject menghentikan coroutine tanpa pernah
        // menjalankan sisa barisnya, jadi IsBusy akan menggantung di true
        // selamanya kalau tidak direset di sini. Controller lalu menganggap
        // tentakel ini masih sibuk sampai fight berakhir.
        StopAll();
    }

    private IEnumerator AttackRoutine()
    {
        IsBusy = true;
        IsExtended = true;

        yield return MoveTo(extendedOffset, extendDuration);

        // Baru berbahaya setelah terlihat. Kalau collidernya dihidupkan sebelum
        // animasi keluar selesai, player bisa kena damage dari jebakan yang
        // masih tersembunyi di dalam plafon - tidak ada telegraph-nya sama sekali.
        SetHazardActive(true);

        if (holdDuration > 0f) yield return new WaitForSeconds(holdDuration);

        InterruptShooters();
        SetHazardActive(false);
        yield return MoveTo(retractedOffset, retractDuration);

        IsExtended = false;

        if (cooldown > 0f) yield return new WaitForSeconds(cooldown);

        IsBusy = false;
        _routine = null;
    }

    private IEnumerator MoveTo(Vector2 target, float duration)
    {
        Vector2 from = _offset;

        if (duration <= 0f)
        {
            _offset = target;
            ApplyOffset();
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _offset = Vector2.Lerp(from, target, Mathf.Clamp01(elapsed / duration));
            ApplyOffset();
            yield return null;
        }

        _offset = target;
        ApplyOffset();
    }

    private void ApplyOffset()
    {
        transform.position = _anchor + (Vector3)_offset;
    }

    private void SetHazardActive(bool active)
    {
        for (int i = 0; i < _hazards.Length; i++)
        {
            HazardArea hazard = _hazards[i];
            if (hazard == null) continue;

            // Collider yang dimatikan, bukan GameObject-nya, supaya visual
            // di anak yang sama tetap ikut bergerak.
            Collider2D col = hazard.GetComponent<Collider2D>();
            if (col != null) col.enabled = active;
        }
    }

    private void InterruptShooters()
    {
        for (int i = 0; i < _shooters.Length; i++)
        {
            if (_shooters[i] != null) _shooters[i].Interrupt();
        }
    }

    private void OnValidate()
    {
        extendDuration = Mathf.Max(0.05f, extendDuration);
        retractDuration = Mathf.Max(0.05f, retractDuration);
        holdDuration = Mathf.Max(0f, holdDuration);
        cooldown = Mathf.Max(0f, cooldown);

        if (extendedOffset == retractedOffset)
        {
            Debug.LogWarning(
                $"[{nameof(BossTentacle)}] '{name}' punya retractedOffset == extendedOffset, jadi tentakel ini akan muncul menghilang di tempat yang sama tanpa terlihat bergerak. Isi salah satunya.",
                this);
        }
    }
}

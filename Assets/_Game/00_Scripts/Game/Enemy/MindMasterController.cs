using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Satu fase fight MindMaster. Fase aktif dipilih dari sisa HP badan, jadi
/// daftar ini harus diisi dari atas ke bawah dengan threshold menurun
/// (misal 1 / 0.7 / 0.35): fase pertama selalu terpenuhi, jadi fight langsung
/// aggression dari frame pertama tanpa perlu jeda.
/// </summary>
[Serializable]
public class BossPhase
{
    [Tooltip("Fase ini aktif saat HealthFraction <= nilai ini. Isi menurun dari atas ke bawah.")]
    [Range(0f, 1f)] public float healthThreshold = 1f;

    [Tooltip("Berapa banyak tentakel yang boleh keluar bersamaan di fase ini. 0 = badan saja yang menyerang.")]
    [Min(0)] public int maxConcurrentOut = 1;

    [Tooltip("Jeda antar pemanggilan PlayAttack. Makin kecil, makin agresif. Kode ini dipanggil berulang selama fase berjalan.")]
    [Min(0f)] public float betweenAttacks = 1.2f;

    [Tooltip("Berapa-many child shooter pertama per tentakel yang boleh menembak. Urutan mengikuti hierarki: anak yang lebih dekat ke anchor didahulukan.")]
    [Min(0)] public int shooterLimitPerTentacle = 1;

    [Tooltip("Jeda sebelum fase ini mulai menyerang. 0 = langsung agresif.")]
    [Min(0f)] public float startupDelay;
}

/// <summary>
/// Satu kondisi visual badan: sprite yang dipakai saat sisa HP sudah turun ke
/// healthThreshold atau di bawahnya, plus posisi lokal kalau sprite itu harus
/// digeser supaya tradisinya pas dengan art (misal kepala yang memarah jadi
/// lebih tinggi atau miring).
///
/// Sifatnya bertingkat, bukan bertukar: element pertama yang threshold-nya sudah
/// terpenuhi yang dipakai, sehingga daftar diisi dari kondisi paling ringan ke
/// terberat dan hanya perlu satu threshold per tahap luka.
/// </summary>
[Serializable]
public class BodyState
{
    [Tooltip("Pakai sprite ini saat sisa HP <= nilai ini. Isi menurun dari atas ke bawah.")]
    [Range(0f, 1f)] public float healthThreshold = 1f;

    [Tooltip("Sprite untuk kondisi ini. Kosongkan = pakai sprite yang sekarang.")]
    public Sprite sprite;

    [Tooltip("Posisi lokal badan untuk kondisi ini, relatif ke parent. Dipakai kalau artnya butuh digeser. (0,0) = biarlah di tempat.")]
    public Vector2 localPosition;
}

/// <summary>
/// Otak MindMaster. Menentukan fase dari sisa HP badan, lalu menjadwalkan
/// tentakel mana yang boleh keluar dan kapan. Badannya mengayun sendiri di
/// sini karena SpriteAnimator yang ada di repo ini hanya bisa menggerakkan
/// UGUI Image, bukan SpriteRenderer world-space.
///
/// Boss ini sengaja tidak mengimplementasikan IParryable: parry tidak
/// menjatuhkan, menunda, atau membatalkan apa pun milik boss - ia hanya
/// memberi pemain kebal sesaat lewat penetralan sumber serangannya.
/// Karena itu badan diletakkan di layer Enemy, yang memang tidak termasuk
/// parryRadiusMask, sehingga parry yang menyentuhnya memang tidak bereaksi.
/// </summary>
public class MindMasterController : MonoBehaviour
{
    [Header("Referensi (dibuat otomatis kalau dikosongkan)")]
    [Tooltip("EnemyHealth pada badan. Kosongkan untuk mencari otomatis di bawah boss ini.")]
    [SerializeField] private EnemyHealth health;

    [Tooltip("Keempat tentakel. Kosongkan untuk mencari otomatis di bawah boss ini.")]
    [SerializeField] private BossTentacle[] tentacles = Array.Empty<BossTentacle>();

    [SerializeField, Tooltip("Kalau kosong, diisi dari PlayerHealth.Instance saat pertama kali dibutuhkan.")]
    private Transform playerTarget;

    [Header("Badan")]
    [SerializeField] private SpriteRenderer bodyRenderer;

    [Tooltip("Sprite badan per kondisi luka, diurutkan dari kondisi terluka paling ringan. Dipilih otomatis dari sisa HP, bukan diputar sebagai animasi.")]
    [SerializeField] private BodyState[] bodyStates = Array.Empty<BodyState>();

    [Tooltip("Sway tipis untuk badan, ditambahkan di atas offset lokal per state. 0 = diam.")]
    [SerializeField] private Vector2 bodyBobAmplitude = new Vector2(0f, 0.12f);
    [SerializeField, Min(0f)] private float bodyBobFrequency = 0.6f;

    [Header("Fase")]
    [Tooltip("Terisi otomatis saat komponen ditambahkan. Elemen yang dikosongkan akan dilewati.")]
    [SerializeField] private BossPhase[] phases = Array.Empty<BossPhase>();

    [Tooltip("Tarik semua tentakel saat pindah fase. Jedanya dipakai pemain untuk menghajar badan tanpa dihajar balik.")]
    [SerializeField] private bool retractAllOnPhaseChange = true;

    [Header("Titik buta")]
    [Tooltip("Tentakel yang paling dekat dengan player tidak membidik dia, dan menembak lurus ke luar. Memberi pemain satu sisi yang aman untuk didekati. Hanya berlaku kalau ada 2 atau lebih tentakel keluar; kalau tidak, semua tetap membidik - kalau tidak, satu-satunya tentakel di fase 1 jadi titik buta dan fase itu kehilangan tekanan.")]
    [SerializeField] private bool nearestTentacleIsBlind = true;

    [Tooltip("Selisih jarak (dalam unit world) yang harus dilampaui sebelum titik buta pindah ke tentakel lain. Mencegah titik buta berpindah tiap frame saat player berdiri di tengah. 0 = langsung pindah begitu ada yang lebih dekat.")]
    [SerializeField, Min(0f)] private float blindSwitchMargin = 1.5f;

    [Header("Kematian")]
    [SerializeField] private UnityEvent onDeath;

    [Header("Audio")]
    [Tooltip("Key SFX yang terdaftar di AudioSystem. Kosongkan jika serangan tentakel tidak perlu suara.")]
    [SerializeField] private string sfxAttackCue = "EnemyLaser";

    private readonly List<BossTentacle> _idlePool = new List<BossTentacle>(4);

    private Coroutine _bindRoutine;
    private Coroutine _phaseRoutine;

    private int _phaseIndex = -1;
    private int _shooterLimit = int.MaxValue;
    private int _blindIndex = -1;
    private bool _defeated;
    private float _fraction = 1f;

    private Vector3 _bodyStateLocalPosition;
    private int _bodyStateIndex = -1;
    private float _bobTime;

    /// <summary>Sisa HP badan, 0 sampai 1. Untuk bar HP.</summary>
    public float HealthFraction => _fraction;

    public bool IsDefeated => _defeated;

    /// <summary>Indeks fase yang sedang jalan, -1 kalau belum mulai.</summary>
    public int CurrentPhaseIndex => _phaseIndex;

    public event Action<float> OnHealthFractionChanged;
    public event Action OnBossDefeated;

    /// <summary>
    /// Nilai untuk shooterLimitPerTentacle yang berarti pakai semua shooter.
    /// Aman dipakai berapa pun jumlah shooternya karena BossTentacle memotong
    /// dengan Mathf.Min terhadap jumlah yang benar-benar ditemukan.
    /// </summary>
    public const int AllShooters = 999;

    /// <summary>
    /// Dipanggil Unity saat komponen ini ditambahkan ke GameObject (dan
    /// saat Reset dipanggil dari menu konteks), jadi prefab boss langsung
    /// terisi tiga fase dengan angka yang sudah disepakati - tidak perlu
    /// mengisi array phases satu per satu di Inspector.
    /// </summary>
    private void Reset()
    {
        // Sprite-nya sengaja tidak diisi di sini: slice art hanya bisa
        // di-drag dari editor, dan Reset menimpanya jadi kosong setiap kali
        // komponen di-reset. Yang di-set cuma threshold-nya supaya urutan
        // kondisi luka langsung benar begitu art-nya dipasang.
        bodyStates = new[]
        {
            new BodyState { healthThreshold = 1f },
            new BodyState { healthThreshold = 0.7f },
            new BodyState { healthThreshold = 0.35f },
            new BodyState { healthThreshold = 0f },
        };

        phases = new[]
        {
            new BossPhase
            {
                healthThreshold = 1f,
                maxConcurrentOut = 1,
                betweenAttacks = 1.2f,
                shooterLimitPerTentacle = 5,
                startupDelay = 0f,
            },
            new BossPhase
            {
                healthThreshold = 0.7f,
                maxConcurrentOut = 2,
                betweenAttacks = 0.9f,
                shooterLimitPerTentacle = 10,
                startupDelay = 0f,
            },
            new BossPhase
            {
                healthThreshold = 0.35f,
                maxConcurrentOut = 4,
                betweenAttacks = 0.6f,
                shooterLimitPerTentacle = AllShooters,
                startupDelay = 0f,
            },
        };
    }

    private void Awake()
    {
        if (health == null) health = GetComponentInChildren<EnemyHealth>(true);
        if (tentacles == null || tentacles.Length == 0)
            tentacles = GetComponentsInChildren<BossTentacle>(true);

        // Estado 0 dianggap sudah aktif sejak awal supaya element pertamanya
        // tidak di-skip hanya karena index-nya masih -1.
        if (bodyStates != null && bodyStates.Length > 0 && bodyStates[0] != null)
        {
            _bodyStateIndex = 0;
            if (bodyStates[0].sprite != null && bodyRenderer != null) bodyRenderer.sprite = bodyStates[0].sprite;
            _bodyStateLocalPosition = bodyStates[0].localPosition;
        }
        else if (bodyRenderer != null)
        {
            _bodyStateLocalPosition = bodyRenderer.transform.localPosition;
        }
    }

    private void OnEnable()
    {
        _bindRoutine = StartCoroutine(Bind());
    }

    private void OnDisable()
    {
        if (_bindRoutine != null)
        {
            StopCoroutine(_bindRoutine);
            _bindRoutine = null;
        }

        Unbind();

        if (_phaseRoutine != null)
        {
            StopCoroutine(_phaseRoutine);
            _phaseRoutine = null;
        }
    }

    private void Update()
    {
        if (_defeated) return;

        Vector2 target = ResolveTarget();

        UpdateBlindTentacle(target);

        for (int i = 0; i < tentacles.Length; i++)
        {
            if (tentacles[i] != null) tentacles[i].Tick(target);
        }

        UpdateBodyState();
    }

    /// <summary>
    /// Menunggu EnemyHealth selesai membuat HealthSystem-nya di Awake, lalu
    /// attaching ke event-nya. Urutan Awake antar-komponen tidak dijamin,
    /// jadi reference-nya ditunggu dulu - pola yang sama dipakai HUD di repo ini.
    /// </summary>
    private IEnumerator Bind()
    {
        float waited = 0f;
        while ((health == null || health.Health == null) && waited < 5f)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }

        if (health == null || health.Health == null)
        {
            Debug.LogError(
                $"[{nameof(MindMasterController)}] '{name}' tidak menemukan EnemyHealth dengan HealthSystem aktif, jadi boss tidak akan menerima damage dan fase tidak akan berganti. Tempelkan EnemyHealth ke badan boss.",
                this);
            yield break;
        }

        HealthSystem system = health.Health;
        system.OnHealthChanged += HandleHealthChanged;
        system.OnDeath += HandleDeath;

        _fraction = system.MaxHealth > 0f ? system.CurrentHealth / system.MaxHealth : 1f;

        if (!_defeated) StartPhase(0);
    }

    private void Unbind()
    {
        if (health == null || health.Health == null) return;

        HealthSystem system = health.Health;
        system.OnHealthChanged -= HandleHealthChanged;
        system.OnDeath -= HandleDeath;
    }

    private void HandleHealthChanged(float current, float max)
    {
        if (max <= 0f) return;

        _fraction = current / max;
        OnHealthFractionChanged?.Invoke(_fraction);

        if (_defeated) return;

        // Fase terdalam yang sudah terpenuhi. Karena threshold diurutkan
        // menurun, ini hanya bisa maju dan tidak pernah mundur.
        int wanted = 0;
        for (int i = 0; i < phases.Length; i++)
        {
            if (phases[i] != null && _fraction <= phases[i].healthThreshold) wanted = i + 1;
        }

        if (wanted > 0 && wanted - 1 > _phaseIndex) StartPhase(wanted - 1);
    }

    private void HandleDeath()
    {
        if (_defeated) return;
        _defeated = true;

        if (_phaseRoutine != null)
        {
            StopCoroutine(_phaseRoutine);
            _phaseRoutine = null;
        }

        StopAllTentacles();

        OnBossDefeated?.Invoke();
        onDeath?.Invoke();
    }

    private void StartPhase(int index)
    {
        _phaseIndex = index;

        BossPhase phase = index >= 0 && index < phases.Length ? phases[index] : null;
        if (phase == null) return;

        _shooterLimit = phase.shooterLimitPerTentacle;

        if (retractAllOnPhaseChange) StopAllTentacles();

        for (int i = 0; i < tentacles.Length; i++)
        {
            if (tentacles[i] != null) tentacles[i].SetShooterLimit(_shooterLimit);
        }

        if (_phaseRoutine != null) StopCoroutine(_phaseRoutine);
        _phaseRoutine = StartCoroutine(PhaseRoutine(phase));
    }

    private IEnumerator PhaseRoutine(BossPhase phase)
    {
        if (phase.startupDelay > 0f) yield return new WaitForSeconds(phase.startupDelay);

        float wait = Mathf.Max(0.05f, phase.betweenAttacks);

        while (!_defeated)
        {
            if (phase.maxConcurrentOut > 0 && CountExtended() < phase.maxConcurrentOut)
            {
                BossTentacle next = PickIdleTentacle();
                if (next != null)
                {
                    next.PlayAttack();
                    if (!string.IsNullOrEmpty(sfxAttackCue))
                        AudioSystem.Instance?.PlaySFX(sfxAttackCue, waitForCompletion: false);
                }
            }

            yield return new WaitForSeconds(wait);
        }
    }

    private int CountExtended()
    {
        int count = 0;
        for (int i = 0; i < tentacles.Length; i++)
        {
            if (tentacles[i] != null && tentacles[i].IsExtended) count++;
        }
        return count;
    }

    /// <summary>
    /// Memilih tentakel yang lagi tidak siklus apa pun, dari yang cooldown-nya
    /// sudah habis. Pakai buffer yang sama tiap panggilan supaya tidak
    /// menyisakan garbage di Update.
    /// </summary>
    private BossTentacle PickIdleTentacle()
    {
        _idlePool.Clear();

        for (int i = 0; i < tentacles.Length; i++)
        {
            BossTentacle tentacle = tentacles[i];
            if (tentacle != null && !tentacle.IsBusy) _idlePool.Add(tentacle);
        }

        if (_idlePool.Count == 0) return null;
        return _idlePool[UnityEngine.Random.Range(0, _idlePool.Count)];
    }

    private void StopAllTentacles()
    {
        for (int i = 0; i < tentacles.Length; i++)
        {
            if (tentacles[i] != null) tentacles[i].StopAll();
        }
    }

    private Vector2 ResolveTarget()
    {
        if (playerTarget == null)
        {
            PlayerHealth player = PlayerHealth.Instance;
            if (player != null) playerTarget = player.transform;
        }

        if (playerTarget != null) return playerTarget.position;
        return transform.position;
    }

    /// <summary>
    /// Memilih sprite dan posisi badan dari sisa HP. Dipanggil ulang tiap
    /// frame karena sway menimpa localPosition yang ditulis state, jadi
    /// urutan apply-nya penting: offset state lebih dulu, baru sway.
    /// </summary>
    private void UpdateBodyState()
    {
        if (bodyRenderer == null || bodyStates == null) return;

        int wanted = -1;
        for (int i = 0; i < bodyStates.Length; i++)
        {
            if (bodyStates[i] != null && _fraction <= bodyStates[i].healthThreshold) wanted = i;
        }

        // Hanya menulis ulang transform kalau state benar-benar berganti, supaya
        // sway yang dihitung dari _bobTime tidak di-reset tiap frame.
        if (wanted != _bodyStateIndex)
        {
            _bodyStateIndex = wanted;

            if (wanted >= 0)
            {
                BodyState state = bodyStates[wanted];
                if (state.sprite != null) bodyRenderer.sprite = state.sprite;
                _bodyStateLocalPosition = state.localPosition;
            }
        }

        if (bodyBobFrequency <= 0f && bodyBobAmplitude == Vector2.zero)
        {
            bodyRenderer.transform.localPosition = _bodyStateLocalPosition;
            return;
        }

        _bobTime += Time.deltaTime;
        float cycle = _bobTime * bodyBobFrequency * Mathf.PI * 2f;
        bodyRenderer.transform.localPosition = _bodyStateLocalPosition + new Vector3(
            Mathf.Cos(cycle) * bodyBobAmplitude.x,
            Mathf.Sin(cycle) * bodyBobAmplitude.y,
            0f);
    }

    private void OnValidate()
    {
        if (bodyBobFrequency < 0f) bodyBobFrequency = 0f;

        if (bodyStates != null)
        {
            for (int i = 0; i < bodyStates.Length; i++)
            {
                if (bodyStates[i] != null && bodyStates[i].healthThreshold > 1f) bodyStates[i].healthThreshold = 1f;
            }
        }

        if (phases == null) return;
        if (phases.Length == 0)
        {
            Debug.LogWarning(
                $"[{nameof(MindMasterController)}] '{name}' punya array phases kosong, jadi tidak ada fase yang bisa aktif dan boss tidak akan pernah menyerang. Ubah Size-nya jadi 3 lalu isi threshold 1 / 0.7 / 0.35.",
                this);
            return;
        }

        for (int i = 1; i < phases.Length; i++)
        {
            if (phases[i] == null || phases[i - 1] == null) continue;

            if (phases[i].healthThreshold > phases[i - 1].healthThreshold)
            {
                Debug.LogWarning(
                    $"[{nameof(MindMasterController)}] '{name}': threshold fase ke-{i} ({phases[i].healthThreshold}) lebih besar dari fase sebelumnya ({phases[i - 1].healthThreshold}). Threshold harus menurun dari atas ke bawah, misal 1 / 0.7 / 0.35.",
                    this);
            }
        }
    }

    /// <summary>
    /// Menandai satu tentakel sebagai titik buta: yang paling dekat dengan
    /// player tidak membidik, jadi pemain bisa mendekati sisi itu tanpa
    /// ditembak dari belakang. Hanya SATU yang dibutakan, dan hanya kandidat
    /// yang sedang keluar - membutakan tentakel yang sedang cooldown justru
    /// menghilangkan safe spot tanpa ada gantinya.
    ///
    /// Kalau dua kandidat jaraknya hampir sama, incumbent (_blindIndex)
    /// dipertahankan sampai selisihnya lewat blindSwitchMargin. Tanpa itu
    /// titik buta berpindah tiap frame saat pemain berdiri di tengah, dan
    /// dua tentakel ikut bolak-balik berputar.
    /// </summary>
    private void UpdateBlindTentacle(Vector2 playerPos)
    {
        if (!nearestTentacleIsBlind)
        {
            SetBlindIndex(-1);
            return;
        }

        int best = -1;
        float bestDist = float.MaxValue;
        float incumbentDist = float.MaxValue;
        int extendedCount = 0;

        for (int i = 0; i < tentacles.Length; i++)
        {
            BossTentacle tentacle = tentacles[i];
            if (tentacle == null || !tentacle.IsExtended) continue;

            extendedCount++;

            float dist = Vector2.Distance(playerPos, (Vector2)tentacle.transform.position);
            if (i == _blindIndex) incumbentDist = dist;

            if (dist < bestDist)
            {
                bestDist = dist;
                best = i;
            }
        }

        // Buta hanya bermakna kalau ada pilihan lain. Dengan satu tentakel
        // keluar, membutakannya berarti fase 1 tidak menekan sama sekali.
        if (best == -1 || extendedCount < 2)
        {
            SetBlindIndex(-1);
            return;
        }

        if (_blindIndex >= 0 && _blindIndex != best &&
            incumbentDist - bestDist < blindSwitchMargin)
            return;

        SetBlindIndex(best);
    }

    private void SetBlindIndex(int index)
    {
        if (_blindIndex == index) return;

        for (int i = 0; i < tentacles.Length; i++)
        {
            if (tentacles[i] != null) tentacles[i].SetTracking(i != index);
        }

        _blindIndex = index;
    }
}

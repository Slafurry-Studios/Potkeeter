using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// AI drone: patrol, kejar, lalu aim dan tembak kalau target sudah dekat.
///
/// Pakai StateMachine/IState yang sudah ada (folder "System/State Machine"),
/// yang isinya nol referensi ke bayonet jadi bisa dipakai ulang di sini.
/// Semua angka range dan kecepatan di inspector, jadi tuning tidak perlu
/// compile ulang.
///
/// Dijeda oleh PauseSystem gratis di sini: PauseSystem men-set
/// Time.timeScale = 0, dan movement digerakkan dari FixedUpdate, jadi drone
/// ikut berhenti tanpa ada pengecekan pause tambahan.
///
/// Gerak memakai velocity, bukan MovePosition, karena parry dari
/// BayonetController mengirim AddForce ke Rigidbody2D musuh. Body kinematic
/// akan mengabaikan AddForce, jadi knockback parry tidak akan terlihat sama
/// sekali. Karena itu state Stunned sengaja TIDAK menulis velocity, biar
/// impuls knockback itu bebas meredup sendiri lewat drag.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class DroneController : MonoBehaviour, IDamageableParryable
{
    [Header("References")]
    [SerializeField] private Rigidbody2D body;
    [Tooltip("Component health. Drone tidak mengimplement IDamageable sendiri, EnemyHealth yang punya interface itu.")]
    [SerializeField] private EnemyHealth health;
    [Tooltip("Child visual, dipakai untuk efek hover. Kosong = pakai transform milik drone ini.")]
    [SerializeField] private Transform visual;
    [Tooltip("Target pembidikan. Kosong = PlayerHealth.Instance.")]
    [SerializeField] private Transform playerTarget;

    [Header("Ranges")]
    [Tooltip("Seberapa jauh player terlihat, untuk masuk ke mode kejar.")]
    [SerializeField] private float detectRange = 6f;
    [Tooltip("Seberapa jauh drone boleh menembak. Harus lebih kecil dari detectRange.")]
    [SerializeField] private float fireRange = 4f;
    [Tooltip("Seberapa jauh player harus pergi sebelum drone menyerah. Sengaja lebih besar dari detectRange supaya drone tidak flip-flop di batas.")]
    [SerializeField] private float loseRange = 9f;
    [Tooltip("Jarak yang mau dijaga saat Chase dan Attack, supaya drone tidak menabrak player.")]
    [SerializeField] private float standoffRange = 2.4f;
    [Tooltip("Radius dianggap sudah sampai di titik patrol.")]
    [SerializeField] private float waypointTolerance = 0.2f;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float patrolSpeed = 1.4f;
    [SerializeField, Min(0f)] private float chaseSpeed = 2.6f;
    [Tooltip("Kecepatan berputar menghadap arah gerak, derajat per detik.")]
    [SerializeField, Min(10f)] private float turnSpeed = 240f;

    [Header("Hover")]
    [Tooltip("Seberapa jauh visual naik-turun, supaya terbaca terbang dan bukan meluncur.")]
    [SerializeField, Min(0f)] private float hoverAmplitude = 0.07f;
    [SerializeField, Min(0f)] private float hoverFrequency = 1.5f;

    [Header("Patrol")]
    [Tooltip("Titik yang dipatroli berurutan. Kosong = drone diam di tempat sampai player terdeteksi.")]
    [SerializeField] private Transform[] patrolPoints;
    [Tooltip("Berhenti sebentar di tiap titik patrol.")]
    [SerializeField, Min(0f)] private float patrolWait = 0.7f;

    [Header("Parry")]
    [Tooltip("Berapa lama drone lumpuh setelah diparry.")]
    [SerializeField, Min(0.1f)] private float stunDuration = 1.2f;

    // Dipisah supaya state tidak perlu memanggil Time.time sendiri.
    public StateMachine StateMachine { get; private set; }
    public DronePatrolState PatrolState { get; private set; }
    public DroneChaseState ChaseState { get; private set; }
    public DroneAttackState AttackState { get; private set; }
    public DroneStunnedState StunnedState { get; private set; }

    public IReadOnlyList<EnemyShooter> Shooters => _shooters;

    private readonly List<EnemyShooter> _shooters = new List<EnemyShooter>();
    private Vector2 _spawnPosition;
    private int _patrolIndex;
    private float _patrolWaitUntil;
    private float _hoverPhase;
    private float _visualBaseY;
    private float _stunUntil;
    private bool _stunned;

    private void Awake()
    {
        if (body == null) body = GetComponent<Rigidbody2D>();
        if (health == null) health = GetComponent<EnemyHealth>();

        // Terbang: tanpa gravitasi, dan tidak boleh berotasi karena fisika -
        // facing dikendalikan manual supaya bisa diinterpolasi halus.
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.drag = 2.5f;

        // Pengumpulan per-drone, jadi tiap drone punya daftar laras sendiri.
        // includeInactive = true supaya laras yang sengaja dimatikan di
        // inspector tetap ikut terdaftar.
        GetComponentsInChildren(true, _shooters);
        if (_shooters.Count == 0)
        {
            Debug.LogWarning($"[{nameof(DroneController)}] '{name}' tidak punya EnemyShooter, jadi tidak akan pernah menembak.", this);
        }
        else
        {
            Debug.Log($"[{nameof(DroneController)}] '{name}' punya {_shooters.Count} laras.", this);
        }

        _spawnPosition = body.position;
        _visualBaseY = visual != null ? visual.localPosition.y : 0f;
        _hoverPhase = Random.Range(0f, Mathf.PI * 2f);

        StateMachine = new StateMachine();
        PatrolState = new DronePatrolState(this);
        ChaseState = new DroneChaseState(this);
        AttackState = new DroneAttackState(this);
        StunnedState = new DroneStunnedState(this);
    }

    private void Start()
    {
        StateMachine.Initialize(PatrolState);
    }

    private void Update()
    {
        if (StateMachine != null) StateMachine.Update();
        TickHover();
    }

    private void FixedUpdate()
    {
        if (StateMachine != null) StateMachine.FixedUpdate();
    }

    // ===== Dipakai oleh state =====

    public Vector2 TargetPosition
    {
        get
        {
            if (playerTarget != null) return playerTarget.position;
            // PlayerHealth.Instance adalah pola yang dipakai script lain
            // (TestAttack, DummyEnemyTest, HealthHUD) dan sudah punya duplicate
            // guard, jadi tidak perlu FindObjectOfType.
            return PlayerHealth.Instance != null ? (Vector2)PlayerHealth.Instance.transform.position : _spawnPosition;
        }
    }

    public float DistanceToTarget => Vector2.Distance(body.position, TargetPosition);

    /// <summary>Jangkauan tembak maksimum, diekspos supaya state bisa memakai toleransi sendiri.</summary>
    public float FireRange => fireRange;

    public bool TargetInDetectRange => DistanceToTarget <= detectRange;

    public bool TargetInFireRange => DistanceToTarget <= fireRange;

    /// <summary>
    /// Sudah dikejar atau ditembak, jadi batas keluar pakai angka yang lebih
    /// longgar daripada batas masuk.
    /// </summary>
    public bool TargetBeyondEngageRange => DistanceToTarget > loseRange;

    public bool IsStunned => _stunned;

    public void EnterStun()
    {
        _stunned = true;
        _stunUntil = Time.time + stunDuration;
        StateMachine.ChangeState(StunnedState);
    }

    public void UpdateStun()
    {
        if (_stunned && Time.time >= _stunUntil) _stunned = false;
    }

    public void PatrolStep()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;

        if (Time.time < _patrolWaitUntil) return;

        Transform point = patrolPoints[_patrolIndex];
        if (point == null)
        {
            AdvancePatrolPoint();
            return;
        }

        Vector2 destination = point.position;
        if (Vector2.Distance(body.position, destination) <= waypointTolerance)
        {
            HoldStill();
            AdvancePatrolPoint();
            return;
        }

        MoveTowards(destination, patrolSpeed);
    }

    public void OnEnterPatrol()
    {
        // Kembali ke titik patrol terdekat, bukan ke titik pertama, supaya
        // drone tidak menyeberang scene setiap kali kehilangan target.
        _patrolIndex = NearestPatrolIndex();
        _patrolWaitUntil = 0f;
    }

    private int NearestPatrolIndex()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return 0;

        int best = 0;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < patrolPoints.Length; i++)
        {
            if (patrolPoints[i] == null) continue;
            // Cast ke Vector2 eksplisit: Transform.position itu Vector3 dan
            // body.position itu Vector2, jadi operator '-' jadi ambigu.
            float sqr = ((Vector2)patrolPoints[i].position - body.position).sqrMagnitude;
            if (sqr >= bestSqr) continue;
            bestSqr = sqr;
            best = i;
        }
        return best;
    }

    private void AdvancePatrolPoint()
    {
        if (patrolPoints == null || patrolPoints.Length == 0) return;
        _patrolIndex = (_patrolIndex + 1) % patrolPoints.Length;
        _patrolWaitUntil = Time.time + patrolWait;
    }

    /// <summary>Kejar target tapi berhenti di standoffRange, tidak sampai menabrak.</summary>
    public void ChaseStep()
    {
        if (DistanceToTarget <= standoffRange)
        {
            HoldStill();
            FaceTowards(TargetPosition);
            return;
        }

        MoveTowards(TargetPosition, chaseSpeed);
    }

    /// <summary>Berdiri di tempat sambil menghadap target. Penembakan ditangani oleh TickShooters().</summary>
    public void AttackStep()
    {
        HoldStill();
        FaceTowards(TargetPosition);
    }

    /// <summary>
    /// Memberi tahu SEMUA laras di drone ini untuk satu tick. Tiap laras
    /// memutuskan sendiri apakah sudah siap menembak, jadi drone dengan
    /// banyak laras akan menembak dari semuanya dengan ritme terpisah.
    /// </summary>
    public void TickShooters()
    {
        Vector2 target = TargetPosition;
        for (int i = 0; i < _shooters.Count; i++)
        {
            EnemyShooter shooter = _shooters[i];
            if (shooter != null && shooter.isActiveAndEnabled) shooter.Tick(target);
        }
    }

    /// <summary>Memberi tahu drone untuk berhenti bergerak tanpa mengubah facing.</summary>
    public void HoldStill()
    {
        body.velocity = Vector2.zero;
    }

    public void MoveTowards(Vector2 destination, float speed)
    {
        Vector2 direction = destination - body.position;
        if (direction.sqrMagnitude < 0.0001f) return;

        body.velocity = direction.normalized * speed;
        FaceDirection(direction);
    }

    private void FaceTowards(Vector2 target)
    {
        Vector2 direction = target - body.position;
        if (direction.sqrMagnitude > 0.0001f) FaceDirection(direction);
    }

    private void FaceDirection(Vector2 direction)
    {
        float desired = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float next = Mathf.MoveTowardsAngle(transform.eulerAngles.z, desired, turnSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Euler(0f, 0f, next);
    }

    /// <summary>
    /// Efek hover jalan di Update dan hanya menyentuh child visual, bukan
    /// collider, jadi boxcast peluru dan trigger enemy tidak ikut bergeser
    /// hanya karena animasi.
    /// </summary>
    private void TickHover()
    {
        if (visual == null || hoverAmplitude <= 0f) return;

        _hoverPhase += hoverFrequency * Time.deltaTime * Mathf.PI * 2f;
        Vector3 local = visual.localPosition;
        local.y = _visualBaseY + Mathf.Sin(_hoverPhase) * hoverAmplitude;
        visual.localPosition = local;
    }

    // ===== IDamageableParryable =====

    /// <summary>
    /// Dipanggil BayonetController saat parry kena. Knockback tidak diterapkan
    /// di sini karena BayonetController sudah AddForce sendiri ke Rigidbody2D
    /// target.
    /// </summary>
    public bool TryParry()
    {
        // Kalau sudah lumpuh, parry kedua tidak menambah durasi stun. Ini juga
        // alasan method ini mengembalikan false: parry di luar window.
        if (_stunned) return false;

        EnterStun();
        return true;
    }
}

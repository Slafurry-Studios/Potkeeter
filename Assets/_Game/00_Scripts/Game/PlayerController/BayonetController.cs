using UnityEngine;
using Slafurry.System.InputHub;
using Slafurry.Utils.VFX;

[RequireComponent(typeof(Rigidbody2D), typeof(HingeJoint2D))]
public class BayonetController : MonoBehaviour
{
    [Header("Sensitivity & Limits")]
    [SerializeField] private float rotationSensitivity = 30f;
    [SerializeField] private float maxAngularSpeed = 2000f;

    [Header("Pivot & Radius Settings")]
    [Tooltip("Transform acuan pusat rotasi & jarak (mis. child 'BasePivot' di bawah UpperArm).")]
    [SerializeField] private Transform basePivot;
    [Tooltip("Radius minimum agar bayonet tidak terlalu dekat ke basePivot")]
    [SerializeField] private float minPivotRadius = 0.5f;
    [Tooltip("Radius maksimum area kontrol (Getting Over It Style)")]
    [SerializeField] private float controlRadius = 2.5f;
    [Tooltip("Kecepatan lerp pergeseran anchor bayonet")]
    [SerializeField] private float distanceResponseSpeed = 15f;

    [Header("Sprite Settings")]
    [Tooltip("Offset sudut jika sprite tidak menghadap ke kanan secara default")]
    [SerializeField] private float spriteAngleOffset = 0f;

    [Header("Shoot & Knockback Settings")]
    [Tooltip("Transform di ujung bayonet untuk trajektori")]
    [SerializeField] private Transform shootDir;
    [Tooltip("Panjang garis gizmo trajektori. Tidak ada role gameplay sejak tembakan jadi peluru - damage dan jangkauan ikut dari prefab peluru.")]
    [SerializeField] private float shootRange = 5f;
    [Tooltip("Prefab peluru yang ditembakkan. Stats dan sprite-nya diset di prefab ini. Kosong = tidak ada yang keluar.")]
    [SerializeField] private Bullet bulletPrefab;
    [SerializeField] private float shootForce = 15f;
    [SerializeField] private float recoilForce = 5f;
    [SerializeField] private float shootCooldown = 0.3f;
    [SerializeField] private float shootTransitionTime = 0.3f;

    [Header("Parry Settings")]
    [Tooltip("Jarak jangkauan bayonet untuk mendeteksi serangan/hitbox musuh")]
    [SerializeField] private float parryRadius = 1.5f;
    [Tooltip("Kekuatan dorongan peluncuran pemain saat Parry sukses")]
    [SerializeField] private float parryLaunchForce = 20f;
    [Tooltip("Kekuatan knockback yang diberikan ke musuh saat ter-parry")]
    [SerializeField] private float parryEnemyKnockback = 12f;
    [Tooltip("Waktu jeda/transisi dari Parry sebelum bisa melakukan Shoot atau Parry lagi")]
    [SerializeField] private float parryTransitionTime = 0.4f;
    [SerializeField] private LayerMask enemyLayer;

    [Header("References")]
    [SerializeField] private HingeJoint2D hinge;
    [Tooltip("Spawner VFX parry. Kalau kosong, dicari otomatis di object ini atau parent's-nya.")]
    [SerializeField] private ParryVFX parryVFX;

    private const float DirectionEpsilonSqr = 0.001f;
    private const float TrajectoryEpsilonSqr = 0.0001f;

    private Rigidbody2D bayonetRb;
    private Camera mainCamera;
    private Vector2 currentScreenMousePos;
    private float lastActionTime = -999f;

    // Properties State Machine & Timing
    public StateMachine StateMachine { get; private set; }
    public BayonetIdleState IdleState { get; private set; }
    public BayonetShootingState ShootingState { get; private set; }
    public BayonetParryingState ParryingState { get; private set; }

    public float ShootTransitionTime => shootTransitionTime;
    public float ParryTransitionTime => parryTransitionTime;

    /// <summary>
    /// shootCooldown setelah buff ParryMeter diterapkan. ParryMeter tidak
    /// push apa pun ke sini; multiplier-nya dibaca on demand supaya tidak
    /// perlu referensi silang dua arah antar komponen.
    /// </summary>
    public float EffectiveShootCooldown => shootCooldown * (ParryMeter.Instance != null ? ParryMeter.Instance.ShootCooldownMultiplier : 1f);

    private void Awake()
    {
        bayonetRb = GetComponent<Rigidbody2D>();
        if (hinge == null) hinge = GetComponent<HingeJoint2D>();
        if (parryVFX == null) parryVFX = GetComponentInParent<ParryVFX>();
        mainCamera = Camera.main;

        hinge.useMotor = false;
        bayonetRb.useFullKinematicContacts = true;

        if (basePivot == null)
        {
            Debug.LogError($"[{nameof(BayonetController)}] '{nameof(basePivot)}' belum di-assign pada '{name}'.", this);
        }

        // Inisialisasi State Machine & States
        StateMachine = new StateMachine();
        IdleState = new BayonetIdleState(this);
        ShootingState = new BayonetShootingState(this);
        ParryingState = new BayonetParryingState(this);
    }

    private void Start()
    {
        StateMachine.Initialize(IdleState);
    }

    private void OnEnable()
    {
        Controls.OnLookAtChanged += HandleLookAtChanged;
        Controls.OnShootStarted += HandleShootStarted;
        Controls.OnParryPressed += HandleParryPressed;
    }

    private void OnDisable()
    {
        Controls.OnLookAtChanged -= HandleLookAtChanged;
        Controls.OnShootStarted -= HandleShootStarted;
        Controls.OnParryPressed -= HandleParryPressed;
    }

    private void OnValidate()
    {
        minPivotRadius = Mathf.Max(0f, minPivotRadius);
        controlRadius = Mathf.Max(minPivotRadius, controlRadius);
    }

    private void Update()
    {
        StateMachine.Update();
    }

    private void FixedUpdate()
    {
        StateMachine.FixedUpdate();
    }

    #region Input Handlers

    private void HandleLookAtChanged(Vector2 mouseScreenPosition) => currentScreenMousePos = mouseScreenPosition;

    private void HandleShootStarted()
    {
        if (!Controls.IsInputEnabled) return;
        if (StateMachine.CurrentState != IdleState) return;
        if (Time.time < lastActionTime + EffectiveShootCooldown) return;

        StateMachine.ChangeState(ShootingState);
    }

    private void HandleParryPressed()
    {
        if (!Controls.IsInputEnabled) return;
        if (StateMachine.CurrentState != IdleState) return;

        StateMachine.ChangeState(ParryingState);
    }

    #endregion

    #region Movement & Aiming

    public void ProcessAimingAndPositioning()
    {
        if (!Controls.IsInputEnabled || hinge.connectedBody == null || basePivot == null)
            return;

        Vector2 pivotPosition = basePivot.position;
        Vector2 mouseWorldPosition = GetMouseWorldPosition();
        Vector2 toMouse = mouseWorldPosition - pivotPosition;

        if (toMouse.sqrMagnitude > DirectionEpsilonSqr)
        {
            UpdateAnchorPosition(pivotPosition, toMouse);
            UpdateRotation(toMouse);
        }
        else
        {
            bayonetRb.angularVelocity = 0f;
        }
    }

    private Vector2 GetMouseWorldPosition()
    {
        float depth = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
        Vector3 screenPointWithDepth = new Vector3(currentScreenMousePos.x, currentScreenMousePos.y, depth);
        return mainCamera.ScreenToWorldPoint(screenPointWithDepth);
    }

    private void UpdateAnchorPosition(Vector2 pivotPosition, Vector2 toMouse)
    {
        float targetDistance = Mathf.Clamp(toMouse.magnitude, minPivotRadius, controlRadius);
        Vector2 targetWorldAnchor = pivotPosition + toMouse.normalized * targetDistance;
        Vector2 targetLocalAnchor = hinge.connectedBody.transform.InverseTransformPoint(targetWorldAnchor);

        hinge.connectedAnchor = Vector2.Lerp(
            hinge.connectedAnchor,
            targetLocalAnchor,
            distanceResponseSpeed * Time.fixedDeltaTime);
    }

    private void UpdateRotation(Vector2 toMouse)
    {
        float desiredAngle = Mathf.Atan2(toMouse.y, toMouse.x) * Mathf.Rad2Deg + spriteAngleOffset;
        float angleDifference = Mathf.DeltaAngle(bayonetRb.rotation, desiredAngle);

        bayonetRb.angularVelocity = Mathf.Clamp(
            angleDifference * rotationSensitivity,
            -maxAngularSpeed,
            maxAngularSpeed);
    }

    private Vector2 GetActualAnchorWorldPosition()
    {
        return hinge.connectedBody.transform.TransformPoint(hinge.connectedAnchor);
    }

    private Vector2 GetTrajectoryDirection()
    {
        if (basePivot == null) return transform.right;

        Vector2 pivotPoint = basePivot.position;
        Vector2 tipPoint = shootDir != null ? (Vector2)shootDir.position : (Vector2)transform.position;
        Vector2 trajectoryVector = tipPoint - pivotPoint;

        return trajectoryVector.sqrMagnitude > TrajectoryEpsilonSqr
            ? trajectoryVector.normalized
            : (Vector2)transform.right;
    }

    #endregion

    #region Action Execution Logic

    public void UpdateLastActionTime() => lastActionTime = Time.time;

    public void ExecuteShootAndKnockback()
    {
        Vector2 trajectoryDir = GetTrajectoryDirection();

        bayonetRb.AddForce(trajectoryDir * shootForce, ForceMode2D.Impulse);

        Rigidbody2D playerRb = hinge.connectedBody;
        if (playerRb != null)
        {
            playerRb.AddForce(-trajectoryDir * recoilForce, ForceMode2D.Impulse);
        }

        // Sebelum ini, tembakan ini hitscan: Physics2D.Raycast dari anchor
        // sejauh shootRange, 10 damage ke EnemyHealth pertama yang kena. Sekarang
        // peluru betulan yang terbang, jadi damage, kecepatan, dan knockback-nya
        // pindah ke prefab peluru - bukan lagi angka yang ditulis di sini.
        // Lunge dan recoil di atas sengaja dibiarkan, karena itu fisika
        // bayonetnya, bukan bagian dari damage hit.
        //
        // Muzzle di ujung bayonet (shootDir), bukan di anchor. Bullet meng-cast
        // kotak mulai dari titik spawn-nya ke depan, jadi selama shootDir ada
        // di ujung bilah, peluru tidak akan kena bilah bayonet sendiri.
        Vector2 muzzlePosition = shootDir != null ? (Vector2)shootDir.position : GetActualAnchorWorldPosition();
        BulletManager.Instance?.Spawn(bulletPrefab, muzzlePosition, trajectoryDir);
    }

    public void ExecuteParryLogic()
    {
        Vector2 trajectoryDir = GetTrajectoryDirection();
        Vector2 parryPoint = shootDir != null ? (Vector2)shootDir.position : (Vector2)transform.position;

        Collider2D hitEnemy = Physics2D.OverlapCircle(parryPoint, parryRadius, enemyLayer);

        if (hitEnemy != null)
        {
            IDamageableParryable parryableTarget = hitEnemy.GetComponentInParent<IDamageableParryable>();

            if (parryableTarget != null && parryableTarget.TryParry())
            {
                Debug.Log($"<color=green>[Parry Success]</color> Berhasil mem-parry {hitEnemy.name}!");

                Rigidbody2D playerRb = hinge.connectedBody;
                if (playerRb != null)
                {
                    playerRb.velocity = Vector2.zero;
                    playerRb.AddForce(-trajectoryDir * parryLaunchForce, ForceMode2D.Impulse);
                }

                Rigidbody2D enemyRb = hitEnemy.GetComponentInParent<Rigidbody2D>();
                if (enemyRb != null)
                {
                    enemyRb.AddForce(trajectoryDir * parryEnemyKnockback, ForceMode2D.Impulse);
                }
    
                bayonetRb.AddForce(trajectoryDir * shootForce, ForceMode2D.Impulse);
                AudioSystem.Instance?.PlaySFX("ParrySFX", waitForCompletion: false);

                // Posisi dari ujung bayonet, rotasi dari bayonet itu sendiri.
                // shootDir adalah child, jadi rotasinya bisa berbeda dari
                // rotasi bayonet kalau ada offset lokal di tip-nya.
                parryVFX?.PlayAt(parryPoint, transform.eulerAngles.z);

                ParryMeter.Instance?.RegisterParry();
            }
            else
            {
                Debug.Log("<color=orange>[Parry Missed / Failed]</color> Musuh sedang tidak dalam Parry Window!");
            }
        }
        else
        {
            Debug.Log("[Parry Missed] Tidak ada target dalam radius parry.");
        }
    }

    #endregion

    #region Gizmos

    private void OnDrawGizmos()
    {
        if (basePivot == null) return;

        Vector2 pivotPosition = basePivot.position;
        Vector2 currentAnchorPoint = Application.isPlaying && hinge != null && hinge.connectedBody != null
            ? GetActualAnchorWorldPosition()
            : pivotPosition;

        Gizmos.color = new Color(1f, 0.3f, 0f);
        Gizmos.DrawWireSphere(pivotPosition, minPivotRadius);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(pivotPosition, controlRadius);

        Vector2 trajectoryDirection = GetTrajectoryDirection();

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(pivotPosition, pivotPosition + trajectoryDirection * controlRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(currentAnchorPoint, 0.08f);

        Vector2 parryPoint = shootDir != null ? (Vector2)shootDir.position : (Vector2)transform.position;
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(parryPoint, parryRadius);

        Gizmos.color = Color.magenta;
        Gizmos.DrawRay(pivotPosition, -trajectoryDirection * (parryLaunchForce * 0.1f));

        if (shootDir != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(currentAnchorPoint, (Vector2)shootDir.position + trajectoryDirection * shootRange);
        }
    }

    #endregion
}
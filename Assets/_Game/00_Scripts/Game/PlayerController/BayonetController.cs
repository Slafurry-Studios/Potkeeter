using UnityEngine;
using Slafurry.System.InputHub;

[RequireComponent(typeof(Rigidbody2D), typeof(HingeJoint2D))]
public class BayonetController : MonoBehaviour
{
    [Header("Sensitivity & Limits")]
    [SerializeField] private float rotationSensitivity = 30f;
    [SerializeField] private float maxAngularSpeed = 2000f;

    [Header("Pivot & Radius Settings")]
    [Tooltip("Transform acuan pusat rotasi & jarak (mis. child 'BasePivot' di bawah UpperArm). " +
             "Transform ini mengikuti rig lengan, sehingga jarak & rotasi Bayonet otomatis mengikuti posisi lengan " +
             "meskipun BayonetParent/Body bergeser.")]
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
    [SerializeField] private float shootRange = 5f;
    [SerializeField] private float shootForce = 15f;
    [SerializeField] private float recoilForce = 5f;
    [SerializeField] private float shootCooldown = 0.3f;

    [Header("References")]
    [SerializeField] private HingeJoint2D hinge;

    private const float DirectionEpsilonSqr = 0.001f;
    private const float TrajectoryEpsilonSqr = 0.0001f;

    private Rigidbody2D bayonetRb;
    private Camera mainCamera;

    private Vector2 currentScreenMousePos;
    private bool isShootPending;
    private float lastShootTime = -999f;

    private void Awake()
    {
        bayonetRb = GetComponent<Rigidbody2D>();
        if (hinge == null) hinge = GetComponent<HingeJoint2D>();
        mainCamera = Camera.main;

        hinge.useMotor = false;
        bayonetRb.useFullKinematicContacts = true;

        if (basePivot == null)
        {
            Debug.LogError(
                $"[{nameof(BayonetController)}] '{nameof(basePivot)}' belum di-assign pada '{name}'. " +
                "Assign transform BasePivot (child dari rig lengan) di Inspector.", this);
        }
    }

    private void OnEnable()
    {
        Controls.OnLookAtChanged += HandleLookAtChanged;
        Controls.OnShootStarted += HandleShootStarted;
    }

    private void OnDisable()
    {
        Controls.OnLookAtChanged -= HandleLookAtChanged;
        Controls.OnShootStarted -= HandleShootStarted;
    }

    private void OnValidate()
    {
        minPivotRadius = Mathf.Max(0f, minPivotRadius);
        controlRadius = Mathf.Max(minPivotRadius, controlRadius);
    }

    private void HandleLookAtChanged(Vector2 mouseScreenPosition) => currentScreenMousePos = mouseScreenPosition;

    private void HandleShootStarted()
    {
        if (!Controls.IsInputEnabled) return;
        if (Time.time < lastShootTime + shootCooldown) return;

        isShootPending = true;
        lastShootTime = Time.time;
    }

    private void FixedUpdate()
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

        if (isShootPending)
        {
            ExecuteShootAndKnockback();
            isShootPending = false;
        }
    }

    private Vector2 GetMouseWorldPosition()
    {
        float depth = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
        Vector3 screenPointWithDepth = new Vector3(currentScreenMousePos.x, currentScreenMousePos.y, depth);
        return mainCamera.ScreenToWorldPoint(screenPointWithDepth);
    }

    /// <summary>
    /// Menggeser connectedAnchor hinge sepanjang arah mouse, dibatasi antara minPivotRadius
    /// dan controlRadius dari basePivot saat ini (mengikuti rig lengan).
    /// </summary>
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

    private void ExecuteShootAndKnockback()
    {
        Vector2 trajectoryDir = GetTrajectoryDirection();

        bayonetRb.AddForce(trajectoryDir * shootForce, ForceMode2D.Impulse);

        Rigidbody2D playerRb = hinge.connectedBody;
        if (playerRb != null)
        {
            playerRb.AddForce(-trajectoryDir * recoilForce, ForceMode2D.Impulse);
        }

        RaycastHit2D raycastCollider = Physics2D.Raycast(GetActualAnchorWorldPosition(), trajectoryDir, shootRange, LayerMask.GetMask("Enemy"));
        if (!raycastCollider) return;

        EnemyHealth targetHealth = raycastCollider.collider.GetComponentInParent<EnemyHealth>();
        Rigidbody2D targetRb = raycastCollider.collider.GetComponentInParent<Rigidbody2D>();

        if (targetHealth != null)
        {
            if (targetHealth.Health != null)
            {
                targetHealth.Health.TakeDamage(10f);
            }

            Debug.Log($"Hit {raycastCollider.collider.name} for 10 damage!");
        }
        else
        {
            Debug.LogWarning($"Hit {raycastCollider.collider.name}, tapi komponen 'EnemyHealth' tidak ditemukan!");
        }

        // Apply Knockback jika Rigidbody2D ditemukan
        if (targetRb != null)
        {
            targetRb.AddForce(trajectoryDir * shootForce, ForceMode2D.Impulse);
        }
    }

    private void OnDrawGizmos()
    {
        if (basePivot == null) return;

        Vector2 pivotPosition = basePivot.position;
        Vector2 currentAnchorPoint = Application.isPlaying && hinge != null && hinge.connectedBody != null
            ? GetActualAnchorWorldPosition()
            : pivotPosition;

        // Min radius (batas terdekat)
        Gizmos.color = new Color(1f, 0.3f, 0f);
        Gizmos.DrawWireSphere(pivotPosition, minPivotRadius);

        // Control radius (batas terjauh)
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(pivotPosition, controlRadius);

        Vector2 trajectoryDirection = GetTrajectoryDirection();

        Gizmos.color = Color.blue;
        Gizmos.DrawLine(pivotPosition, pivotPosition + trajectoryDirection * controlRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(currentAnchorPoint, 0.08f);

        if (shootDir != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(currentAnchorPoint, (Vector2)shootDir.position + trajectoryDirection * shootRange);
        }
    }
}
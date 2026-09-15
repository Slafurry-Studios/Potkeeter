using UnityEngine;
using Slafurry.System.InputHub;

[RequireComponent(typeof(Rigidbody2D), typeof(HingeJoint2D))]
public class BayonetController : MonoBehaviour
{
    [Header("Sensitivity & Limits")]
    [SerializeField] private float rotationSensitivity = 30f;
    [SerializeField] private float maxAngularSpeed = 2000f;

    [Header("Sprite & Pivot Settings")]
    [Tooltip("Ubah ini jika sprite bayonet tidak menghadap ke kanan secara default (misal: set 90 jika menghadap ke atas)")]
    [SerializeField] private float spriteAngleOffset = 0f;

    [Tooltip("Offset titik pivot jika rotasi tidak persis di center connectedBody")]
    [SerializeField] private Vector2 pivotOffset = Vector2.zero;
    
    [Header("Shoot & Knockback Settings")]
    [Tooltip("Transform di ujung bayonet yang berfungsi sebagai Titik 2 trajektori")]
    [SerializeField] private Transform shootDir;
    [SerializeField] private float shootForce = 15f;      // Dorongan ke depan bayonet
    [SerializeField] private float recoilForce = 5f;       // Knockback yang diterima Player
    [SerializeField] private float shootCooldown = 0.3f;   // Jeda antartembakan

    private Rigidbody2D bayonetRb;
    private HingeJoint2D hinge;
    private Camera mainCamera;
    
    private Vector2 currentScreenMousePos;
    private bool isShootPending = false;
    private float lastShootTime = -999f;

    private void Start()
    {
        bayonetRb = GetComponent<Rigidbody2D>();
        hinge = GetComponent<HingeJoint2D>();
        mainCamera = Camera.main;

        hinge.useMotor = false;
        bayonetRb.useFullKinematicContacts = true;

        Controls.OnLookAtChanged += HandleLookAtChanged;
        Controls.OnShootStarted += HandleShootStarted;
    }

    private void OnDestroy()
    {
        Controls.OnLookAtChanged -= HandleLookAtChanged;
        Controls.OnShootStarted -= HandleShootStarted;
    }

    private void HandleLookAtChanged(Vector2 mouseScreenPosition)
    {
        currentScreenMousePos = mouseScreenPosition;
    }

    private void HandleShootStarted()
    {
        if (!Controls.IsInputEnabled)
            return;

        // Cek Cooldown
        if (Time.time >= lastShootTime + shootCooldown)
        {
            isShootPending = true;
            lastShootTime = Time.time;
        }
    }

    private void FixedUpdate()
    {
        if (!Controls.IsInputEnabled || bayonetRb == null || hinge.connectedBody == null)
            return;

        // --- 1. ROTASI BAYONET ---
        Vector3 mouseScreenWithZ = new Vector3(
            currentScreenMousePos.x, 
            currentScreenMousePos.y, 
            Mathf.Abs(mainCamera.transform.position.z - transform.position.z)
        );
        Vector2 mouseWorldPosition = mainCamera.ScreenToWorldPoint(mouseScreenWithZ);

        Vector2 actualPivot = GetActualPivot();
        Vector2 direction = mouseWorldPosition - actualPivot;

        if (direction.sqrMagnitude > 0.001f)
        {
            float desiredAngle = (Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg) + spriteAngleOffset;
            float currentAngle = bayonetRb.rotation;
            float angleDifference = Mathf.DeltaAngle(currentAngle, desiredAngle);

            float targetAngularVelocity = Mathf.Clamp(angleDifference * rotationSensitivity, -maxAngularSpeed, maxAngularSpeed);
            bayonetRb.angularVelocity = targetAngularVelocity;
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

    private Vector2 GetActualPivot()
    {
        return (Vector2)hinge.connectedBody.transform.TransformPoint(hinge.connectedAnchor) + pivotOffset;
    }

    private Vector2 GetTrajectoryDirection()
    {
        Vector2 pivotPoint = GetActualPivot();
        
        Vector2 tipPoint = shootDir != null ? (Vector2)shootDir.position : (Vector2)transform.position;

        Vector2 trajectoryVector = tipPoint - pivotPoint;

        if (trajectoryVector.sqrMagnitude <= 0.0001f)
            return transform.right;

        return trajectoryVector.normalized;
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
    }

    private void OnDrawGizmos()
    {
        if (hinge != null && hinge.connectedBody != null)
        {
            Vector2 pivotPoint = GetActualPivot();
            
            // Visualisasi Titik 1 (Pivot)
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(pivotPoint, 0.1f);

            if (shootDir != null)
            {
                // Visualisasi Titik 2 (shootDir)
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(shootDir.position, 0.08f);

                // Visualisasi Garis Trajektori (Titik 1 -> Titik 2 dan seterusnya)
                Vector2 trajectoryDir = GetTrajectoryDirection();
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(pivotPoint, (Vector2)shootDir.position + (trajectoryDir * 2f));
            }
        }
    }
}
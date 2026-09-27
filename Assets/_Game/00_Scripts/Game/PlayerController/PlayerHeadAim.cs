using UnityEngine;

/// <summary>
/// Mencondongkan kepala mengikuti aim baionet. Sisi kiri/kanan diurus
/// PlayerFacingFlip lewat flipX; script ini hanya menambah rotation.
/// </summary>
public class PlayerHeadAim : MonoBehaviour
{
    [SerializeField] private Transform head;
    [SerializeField] private BayonetController bayonet;

    [Tooltip("Sudut tambahan kepala, untuk sprite kepala yang tidak persis mendatar.")]
    [SerializeField] private float angleOffset;

    [Tooltip("Harus sama dengan deadzone di PlayerFacingFlip.")]
    [SerializeField, Range(0f, 0.5f)] private float deadzone = 0.1f;

    [Tooltip("0 = kepala langsung snap. Makin besar, makin lambek berputar.")]
    [SerializeField, Min(0f)] private float smoothTime = 0.05f;

    private bool facingLeft;

    private void LateUpdate()
    {
        if (head == null || bayonet == null) return;

        Vector2 aim = bayonet.AimDirection;

        // Syarat sama persis dengan PlayerFacingFlip.UpdateFacing.
        if (aim.x < -deadzone) facingLeft = true;
        else if (aim.x > deadzone) facingLeft = false;

        // |aim.x| supaya tidak ada rotasi 180 derajat tambahan waktu sudah
        // dicermin, dan tanda dibalik supaya tetap tegak: flipX menggeser
        // arah dasar sprite 0 ke 180 derajat, jadi rotation harus dihitung
        // di kerangka yang sudah ikut bergeser itu.
        float angle = Mathf.Atan2(aim.y, Mathf.Abs(aim.x)) * Mathf.Rad2Deg;
        if (facingLeft) angle = -angle;
        angle += angleOffset;

        Quaternion world = Quaternion.Euler(0f, 0f, angle);

        // Head anak dari Pot dan Pot ikut bergerak, jadi angle di atas itu
        // sudut dunia - harus dikonversi ke local sebelum ditulis.
        Transform parent = head.parent;
        Quaternion local = parent == null ? world : Quaternion.Inverse(parent.rotation) * world;

        // Slerp lewat quaternion, bukan LerpAngle.
        float t = smoothTime <= 0f ? 1f : 1f - Mathf.Exp(-Time.deltaTime / smoothTime);
        head.localRotation = Quaternion.Slerp(head.localRotation, local, t);
    }
}

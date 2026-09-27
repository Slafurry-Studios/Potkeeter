using UnityEngine;

/// <summary>
/// Cermin sprite badan dan pot saat baionet mengarah ke kiri, lalu
/// mengembalikannya ke kanan saat baionet mengarah ke kanan. Semua sprite
/// digambar default menghadap kanan, jadi ke kanan memang tidak perlu
/// dibalik. Arah diambil dari <see cref="BayonetController.AimDirection"/>,
/// jadi badannya pasti sama dengan arah baionet.
/// </summary>
public class PlayerFacingFlip : MonoBehaviour
{
    [SerializeField] private BayonetController bayonet;
    [SerializeField] private SpriteRenderer body;
    [SerializeField] private SpriteRenderer pot;
    [SerializeField] private SpriteRenderer head;

    [Tooltip("Seberapa dekat ke vertikal sebelum arah dianggap berubah, supaya sprite tidak berkedip-ganti. 0.1 = sekitar 6 derajat.")]
    [SerializeField, Range(0f, 0.5f)] private float deadzone = 0.1f;

    private bool facingLeft;

    private void LateUpdate()
    {
        if (bayonet == null) return;

        Vector2 aim = bayonet.AimDirection;
        if (aim.sqrMagnitude < 0.0001f) return;

        // Dua ambang, bukan satu: ke kiri baru dibalik kalau aim sudah jelas
        // kiri, ke kanan baru kalau aim sudah jelas kanan. Di antara keduanya
        // tahan posisi sekarang, jadi tidak berkedip-ganti pas aim menyeberangi
        // vertikal.
        bool left;
        if (aim.x < -deadzone) left = true;
        else if (aim.x > deadzone) left = false;
        else return;

        if (left == facingLeft) return;

        facingLeft = left;
        if (body != null) body.flipX = left;
        if (pot != null) pot.flipX = left;
        if (head != null) head.flipX = left;
    }
}

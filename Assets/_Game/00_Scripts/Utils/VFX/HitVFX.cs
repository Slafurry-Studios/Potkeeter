using UnityEngine;

namespace Slafurry.Utils.VFX
{
    /// <summary>
    /// Spawns a short-lived VFX prefab (SpriteRenderer + Animator) at the point
    /// of contact, then lets it vanish. Bentuknya sama dengan ParryVFX: prefab
    ///-nya cuma art, komponen ini hanya mengurus spawn lalu destroy.
    ///
    /// Setup:
    /// 1. Assign a VFX prefab. Its root should be active with a SpriteRenderer
    ///    and an Animator whose default state auto-plays - a fresh Instantiate
    ///    starts the controller on its default state on its own, so nothing here
    ///    has to poke the Animator.
    /// 2. rotationOffset dipakai untuk meluruskan art dengan permukaan yang
    ///    kena. Sudut yang masuk ke PlayAt sudah berupa normal permukaan, jadi
    ///    offset ini untuk adjusting arah art, bukan arah tumbukan.
    ///
    /// Bedanya dengan ParryVFX: yang di sini dipanggil per-hit peluru, jadi
    /// jauh lebih sering. Lifetime pendek menjaga jumlah instance tetap kecil.
    /// Kalau nanti jadi terlalu banyak, VFXCleaner + pool adalah jalan
    /// replace Destroy dengan Clean().
    /// </summary>
    public class HitVFX : MonoBehaviour
    {
        [Tooltip("Prefab VFX yang di-spawn (SpriteRenderer + Animator)")]
        [SerializeField] private GameObject vfxPrefab;

        [Tooltip("Berapa detik VFX hidup sebelum dihancurkan otomatis")]
        [SerializeField, Min(0.05f)] private float lifetime = 0.2f;

        [Tooltip("Offset sudut dari permukaan yang kena, kalau sprite VFX tidak menghadap arah yang sama")]
        [SerializeField] private float rotationOffset = 90f;

        private bool _warnedMissingPrefab;

        /// <summary>
        /// Spawn the VFX at an explicit world position and Z rotation.
        /// </summary>
        public void PlayAt(Vector3 worldPosition, float rotationZ)
        {
            if (vfxPrefab == null)
            {
                // Diwarn sekali saja, bukan tiap kali peluru kena, supaya
                // prefab yang belum di-assign tidak membanjiri console.
                if (!_warnedMissingPrefab)
                {
                    Debug.LogWarning($"[{nameof(HitVFX)}] '{nameof(vfxPrefab)}' belum di-assign pada '{name}'.", this);
                    _warnedMissingPrefab = true;
                }
                return;
            }

            // Overload (Object, Vector3, Quaternion) - BUKAN (Object, Transform).
            // Yang kedua itu me-parent hasil instantiate ke transform Bullet,
            // dan peluru ini dikembalikan ke pool right after hit - jadi VFX-nya
            // akan ikut hibernasi dan ikut ter-reuse bareng peluru.
            // rotationOffset ditambahkan, bukan replaces, jadi VFX tetap ikut
            // arah permukaan dan cuma digeser sebesar offset.
            GameObject instance = Instantiate(vfxPrefab, worldPosition, Quaternion.Euler(0f, 0f, rotationZ + rotationOffset));

            // Object.Destroy(obj, t) dicatat Unity secara internal, jadi
            // bersahabungan dengan umur component ini - kalau peluru di-release
            // ke pool atau scene di-reload sebelum lifetime habis, VFX-nya
            // tetap dibersihkan.
            // Delay-nya memakai scaled time, jadi VFX ikut membeku saat pause.
            Destroy(instance, lifetime);
        }
    }
}

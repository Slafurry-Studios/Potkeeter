using UnityEngine;

namespace Slafurry.Utils.VFX
{
    /// <summary>
    /// Spawns a short-lived VFX prefab (SpriteRenderer + Animator) at the
    /// parry point, rotated to match the parry object.
    ///
    /// Setup:
    /// 1. Assign a VFX prefab. Its root should be active with a SpriteRenderer
    ///    and an Animator whose default state auto-plays - a fresh Instantiate
    ///    starts the controller on its default state on its own, so nothing
    ///    here has to poke the Animator.
    /// 2. Leave spawnPoint empty to spawn on this transform, or assign a
    ///    child (e.g. the bayonet tip) to spawn at the visual contact point.
    ///
    /// Play() is no-argument so it can be driven straight from a UnityEvent.
    /// Call PlayAt() from code when position and rotation come from different
    /// transforms - the tip's position and the bayonet's rotation, for example.
    /// </summary>
    public class ParryVFX : MonoBehaviour
    {
        [Tooltip("Prefab VFX yang di-spawn (SpriteRenderer + Animator)")]
        [SerializeField] private GameObject vfxPrefab;

        [Tooltip("Transform asal posisi & rotasi. Kalau kosong, pakai transform milik component ini.")]
        [SerializeField] private Transform spawnPoint;

        [Tooltip("Berapa detik VFX hidup sebelum dihancurkan otomatis")]
        [SerializeField, Min(0.05f)] private float lifetime = 1f;

        [Tooltip("Offset sudut dari rotasi parry, kalau sprite VFX tidak menghadap arah yang sama dengan bayonet")]
        [SerializeField] private float rotationOffset = 90f;

        private bool _warnedMissingPrefab;

        /// <summary>
        /// Spawn the VFX at spawnPoint (or this transform when unassigned).
        /// No parameters, so it can be wired to a UnityEvent directly.
        /// </summary>
        public void Play()
        {
            Transform source = spawnPoint != null ? spawnPoint : transform;
            PlayAt(source.position, source.eulerAngles.z);
        }

        /// <summary>
        /// Spawn the VFX at an explicit world position and Z rotation.
        /// </summary>
        public void PlayAt(Vector3 worldPosition, float rotationZ)
        {
            if (vfxPrefab == null)
            {
                // Diwarn sekali saja, bukan tiap kali parry sukses, supaya
                // prefab yang belum di-assign tidak membanjiri console.
                if (!_warnedMissingPrefab)
                {
                    Debug.LogWarning($"[{nameof(ParryVFX)}] '{nameof(vfxPrefab)}' belum di-assign pada '{name}'.", this);
                    _warnedMissingPrefab = true;
                }
                return;
            }

            // Overload (Object, Vector3, Quaternion) - BUKAN (Object, Transform).
            // Yang kedua itu me-parent hasil instantiate ke transform tadi, dan
            // VFX ini harus hidup di world space: kalau ikut parry, efeknya
            // ikut berputar bersama bayonet sampai lifetime habis.
            // rotationOffset ditambahkan, bukan replaces, jadi VFX tetap
            // ikut arah parry dan cuma digeser 90 derajat.
            GameObject instance = Instantiate(vfxPrefab, worldPosition, Quaternion.Euler(0f, 0f, rotationZ + rotationOffset));

            // Object.Destroy(obj, t) dicatat Unity secara internal, jadi
            // bersahabungan dengan umur component ini - kalau player di-destroy
            // atau scene di-reload sebelum lifetime habis, VFX-nya tetap
            // dibersihkan. Coroutine di spawner tidak punya jaminan itu.
            // Delay-nya memakai scaled time, jadi VFX ikut membeku saat pause,
            // sama seperti fade musik dan ScreenFlash.
            Destroy(instance, lifetime);
        }
    }
}

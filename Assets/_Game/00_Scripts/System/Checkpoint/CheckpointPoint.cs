using System.Collections.Generic;
using UnityEngine;

namespace Slafurry.System.Checkpoint
{
    /// <summary>
    /// Penanda satu titik checkpoint di dalam scene. Taruh di GameObject kosong
    /// di posisi yang mau dipakai untuk respawn, isi checkpointId, lalu sambung
    /// CollideTrigger.onTriggerEnter ke Save() milik objek ini.
    ///
    /// Sengaja tidak punya trigger sendiri: pemicunya tetap milik
    /// CollideTrigger, dan id-nya hanya hidup di sini - bukan juga ditulis ulang
    /// sebagai argumen di UnityEvent.
    /// </summary>
    public class CheckpointPoint : MonoBehaviour
    {
        [Tooltip("Id checkpoint ini. Harus unik dalam satu scene.")]
        [SerializeField] private int checkpointId;

        public int CheckpointId => checkpointId;

        private static readonly Dictionary<int, CheckpointPoint> Registry = new Dictionary<int, CheckpointPoint>();

        private void OnEnable() => Register(this);
        private void OnDisable() => Unregister(this);

        /// <summary>
        /// Simpan checkpoint ini sebagai checkpoint terakhir. Tanpa argumen
        /// supaya bisa langsung disambungkan ke UnityEvent.
        /// </summary>
        public void Save()
        {
            if (CheckpointManager.Instance == null)
            {
                Debug.LogWarning($"[{nameof(CheckpointPoint)}] Tidak ada CheckpointManager aktif, checkpoint '{checkpointId}' tidak disimpan.", this);
                return;
            }

            CheckpointManager.Instance.SaveCheckpoint(gameObject.scene.name, checkpointId);
        }

        /// <summary>
        /// Cari titik checkpoint berdasarkan id. Melembalikan false kalau id-nya
        /// tidak ada di scene yang sedang aktif.
        /// </summary>
        public static bool TryFind(int id, out CheckpointPoint point)
        {
            if (Registry.TryGetValue(id, out point) && point != null) return true;

            point = null;
            return false;
        }

        private static void Register(CheckpointPoint point)
        {
            // Dua titik dengan id yang sama akan diam-diam memakai yang terakhir
            // terdaftar, jadi lebih baik brethrenkan di inspector.
            if (Registry.TryGetValue(point.checkpointId, out CheckpointPoint existing) && existing != null && existing != point)
            {
                Debug.LogWarning(
                    $"[{nameof(CheckpointPoint)}] Id {point.checkpointId} dipakai oleh '{existing.name}' dan '{point.name}'. " +
                    "Pakai id yang berbeda.", point);
                return;
            }

            Registry[point.checkpointId] = point;
        }

        private static void Unregister(CheckpointPoint point)
        {
            // Hanya hapus kalau yang terdaftar memang objek ini. Kalau Register
            // menolak karena id-nya bentrok, entri milik orang lain harus tetap
            // ada - kalau tidak, Unregister di sini akan menghapusnya tanpa sebab.
            if (Registry.TryGetValue(point.checkpointId, out CheckpointPoint existing) && existing == point)
            {
                Registry.Remove(point.checkpointId);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.5f);
        }
#endif
    }
}

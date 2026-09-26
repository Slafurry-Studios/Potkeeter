using System;
using Slafurry.System.Scene;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Slafurry.System.Checkpoint
{
    /// <summary>
    /// Menyimpan satu checkpoint sebagai (sceneName, checkpointId) di PlayerPrefs,
    /// lalu memuatnya kembali. Sengaja tidak punya trigger sendiri: seluruh API
    /// adalah method publik tanpa argumen (atau satu int) supaya bisa disambungkan
    /// ke UnityEvent milik CollideTrigger / CountTrigger di scene.
    ///
    /// Datanya ada di PlayerPrefs, jadi tetap ada meski scene dirombak ulang atau
    /// game ditutup. Komponen ini hanya lived-in selama scene aktif.
    ///
    /// Menaruhnya di scene (bukan Boot) disengaja: UnityEvent pada objek scene
    /// hanya bisa menunjuk objek di scene yang sama, dan objek hasil
    /// DontDestroyOnLoad tidak muncul di hierarchy scene. Players.prefab
    /// di-instance di dalam Game.unity, jadi komponen di dalamnya bisa jadi
    /// target UnityEvent milik CollideTrigger yang ada di scene itu juga.
    /// </summary>
    public class CheckpointManager : MonoBehaviour
    {
        public static CheckpointManager Instance { get; private set; }

        private const string SceneKey = "checkpoint.scene";
        private const string IdKey = "checkpoint.id";
        private const int NoCheckpoint = -1;

        /// <summary>Scene yang tersimpan, atau string kosong kalau belum ada.</summary>
        public string SavedSceneName { get; private set; } = string.Empty;

        /// <summary>Id checkpoint yang tersimpan, atau -1 kalau belum ada.</summary>
        public int SavedCheckpointId { get; private set; } = NoCheckpoint;

        public bool HasCheckpoint => !string.IsNullOrEmpty(SavedSceneName);

        /// <summary>
        /// Teranggil setelah scene checkpoint benar-benar selesai dimuat, bukan
        /// saat permintaannya dikirim. Ini titik yang tepat untuk memindahkan
        /// pemain ke posisi checkpoint.
        /// </summary>
        public event Action<string, int> OnCheckpointLoaded;

        private bool loadPending;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            ReadSaved();
        }

        private void OnEnable()
        {
            if (SceneLoader.Instance != null) SceneLoader.Instance.OnSceneLoadCompleted += HandleSceneLoadCompleted;
        }

        private void OnDisable()
        {
            if (SceneLoader.Instance != null) SceneLoader.Instance.OnSceneLoadCompleted -= HandleSceneLoadCompleted;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Simpan checkpoint memakai nama scene yang sedang aktif. Bentuk ini
        /// yang dipakai di inspector: CollideTrigger cuma butuh satu argumen int.
        /// </summary>
        public void SaveCheckpoint(int checkpointId)
        {
            SaveCheckpoint(SceneManager.GetActiveScene().name, checkpointId);
        }

        public void SaveCheckpoint(string sceneName, int checkpointId)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogWarning($"[{nameof(CheckpointManager)}] Nama scene kosong, checkpoint tidak disimpan.", this);
                return;
            }

            PlayerPrefs.SetString(SceneKey, sceneName);
            PlayerPrefs.SetInt(IdKey, checkpointId);
            PlayerPrefs.Save();

            SavedSceneName = sceneName;
            SavedCheckpointId = checkpointId;
        }

        /// <summary>
        /// Muat scene checkpoint yang tersimpan. Mengembalikan false kalau
        /// belum ada checkpoint, jadi pemanggil bisa fallback ke scene utama.
        /// </summary>
        public bool LoadCheckpoint()
        {
            if (!HasCheckpoint)
            {
                Debug.Log($"[{nameof(CheckpointManager)}] Belum ada checkpoint tersimpan.", this);
                return false;
            }

            loadPending = true;
            SceneSystem.Load(SavedSceneName);
            return true;
        }

        public void ClearCheckpoint()
        {
            PlayerPrefs.DeleteKey(SceneKey);
            PlayerPrefs.DeleteKey(IdKey);
            PlayerPrefs.Save();

            SavedSceneName = string.Empty;
            SavedCheckpointId = NoCheckpoint;
            loadPending = false;
        }

        private void ReadSaved()
        {
            SavedSceneName = PlayerPrefs.GetString(SceneKey, string.Empty);
            SavedCheckpointId = PlayerPrefs.GetInt(IdKey, NoCheckpoint);
        }

        private void HandleSceneLoadCompleted(string sceneName)
        {
            // OnSceneLoadCompleted dipanggil untuk semua scene, bukan cuma yang kita
            // minta, jadi yang lolos hanya yang scene-nya sama.
            if (!loadPending || sceneName != SavedSceneName) return;

            loadPending = false;
            MovePlayerToCheckpoint(SavedCheckpointId);
            OnCheckpointLoaded?.Invoke(SavedSceneName, SavedCheckpointId);
        }

        /// <summary>
        /// Pindahkan pemain ke titik checkpoint yang id-nya cocok. Kalau tidak
        /// ketemu, scene tetap dimuat normal dan pemain dibiarkan di tempatnya -
        /// gagal respawn tidak boleh menjebak pemain di layar loading.
        /// </summary>
        private void MovePlayerToCheckpoint(int checkpointId)
        {
            if (!CheckpointPoint.TryFind(checkpointId, out CheckpointPoint point))
            {
                Debug.LogWarning(
                    $"[{nameof(CheckpointManager)}] Tidak ada CheckpointPoint dengan id {checkpointId} di scene '{SavedSceneName}'. " +
                    "Pemain tidak dipindahkan.", this);
                return;
            }

            if (PlayerHealth.Instance == null)
            {
                Debug.LogWarning($"[{nameof(CheckpointManager)}] PlayerHealth.Instance belum ada, pemain tidak dipindahkan.", this);
                return;
            }

            Transform player = PlayerHealth.Instance.transform;
            Vector2 target = point.transform.position;

            // GetComponent, bukan GetComponentInChildren: bayonet adalah anak
            // pemain dan punya Rigidbody2D-nya sendiri, jadi pencarian rekursif
            // bisa grabbing RB bayonet dan membuat player-nya diam.
            Rigidbody2D playerRb = player.GetComponent<Rigidbody2D>();
            if (playerRb != null)
            {
                // velocity harus nol dulu: rigid body yang masih bergerak akan
                // langsung-carry player menjauh dari checkpoint setelah teleport.
                playerRb.velocity = Vector2.zero;
                playerRb.angularVelocity = 0f;
                playerRb.position = target;
            }
            else
            {
                player.position = target;
            }
        }
    }
}

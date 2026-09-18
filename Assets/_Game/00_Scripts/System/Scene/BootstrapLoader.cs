using System.Collections;
using Slafurry.System.Scene;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Slafurry.System.Bootstrap
{
    /// <summary>
    /// Diletakkan di Bootstrap scene (scene paling pertama di Build Settings).
    /// Tugasnya: pastikan semua GameSystem<T> (SceneLoader, AudioSystem, dll.)
    /// sudah selesai Initialize() + PostInitialize(), baru pindah ke scene tujuan.
    /// </summary>
    public class BootstrapLoader : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("Nama scene yang akan dimuat setelah semua system siap.")]
        [SerializeField] private string targetSceneName = "MainMenu";

        [Header("Systems To Wait For")]
        [Tooltip("Referensi ke semua GameSystem yang harus siap sebelum pindah scene. " +
                 "Drag komponennya di sini (SceneLoader, AudioSystem, dst).")]
        [SerializeField] private MonoBehaviour[] systemsToWaitFor;

        [Header("Optional")]
        [Tooltip("Delay tambahan (detik) setelah semua system siap, sebelum load scene. Berguna untuk splash screen minimal duration.")]
        [SerializeField] private float minimumDelay = 0f;

        private void Start()
        {
            StartCoroutine(BootstrapRoutine());
        }

        private IEnumerator BootstrapRoutine()
        {
            // Tunggu 1 frame supaya semua Awake()/OnSingletonAwake() dari system lain
            // (yang di-set lewat Script Execution Order lebih awal) selesai duluan.
            yield return null;

            // Inisialisasi setiap system secara berurutan.
            foreach (var systemObj in systemsToWaitFor)
            {
                if (systemObj == null)
                {
                    Debug.LogWarning("[BootstrapLoader] Ada slot system yang kosong di inspector, dilewati.");
                    continue;
                }

                if (systemObj is IGameSystemLifecycle lifecycle)
                {
                    yield return StartCoroutine(lifecycle.Initialize());
                    lifecycle.PostInitialize();
                }
                else
                {
                    Debug.LogWarning($"[BootstrapLoader] {systemObj.GetType().Name} bukan GameSystem, dilewati dari lifecycle wait.");
                }
            }

            if (minimumDelay > 0f)
                yield return new WaitForSecondsRealtime(minimumDelay);

            Debug.Log($"[BootstrapLoader] Semua system siap. Pindah ke scene '{targetSceneName}'.");

            if (string.IsNullOrEmpty(targetSceneName))
            {
                Debug.LogError("[BootstrapLoader] targetSceneName kosong! Set di inspector.");
                yield break;
            }

            SceneSystem.Load(targetSceneName);
        }
    }

    /// <summary>
    /// Interface bantu supaya BootstrapLoader bisa memanggil Initialize()/PostInitialize()
    /// tanpa harus tahu tipe generik T dari GameSystem<T>.
    /// Implementasikan ini di GameSystem<T> (lihat catatan di bawah).
    /// </summary>
    public interface IGameSystemLifecycle
    {
        IEnumerator Initialize();
        void PostInitialize();
    }
}
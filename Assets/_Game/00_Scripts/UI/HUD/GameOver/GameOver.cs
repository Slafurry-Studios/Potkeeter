using UnityEngine;
using Slafurry.System.Pause;
using Slafurry.System.Scene;
using UnityEngine.SceneManagement;
using System.Collections;
using Slafurry.System.InputHub;

public class GameOver : MonoBehaviour
{
    // Pisah dari "Global" yang dipakai PauseHUD. PauseSystem menyimpan stack
    // sebagai HashSet<string>, jadi kalau dua layar berbagi satu key,
    // HideGameOver() ikut membuka pause milik menu pause.
    private const string PauseKey = "GameOver";

    // Nama scene beneran ada spasi. "MainMenu" tidak ada dan LoadSceneAsync
    // gagal diam-diam kalau string-nya salah.
    private const string MainMenuScene = "Main Menu";

    public static GameOver Instance { get; private set; }

    [Header("References")]
    [Tooltip("Object yang berisi visuals, BUKAN object yang memegang script ini.")]
    [SerializeField] private GameObject gameOverUI;

    [Header("Settings")]
    [SerializeField] private float showDelay = 1f;

    private bool _isShowing;

    public bool IsShowing => _isShowing;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Start()
    {
        // Panel sudah inactive di prefab, tapi tetap diamankan kalau ada yang
        // menghidupkannya lewat inspector atau animasi.
        if (gameOverUI != null)
            gameOverUI.SetActive(false);
    }

    public void ShowGameOver()
    {
        // Satu-satunya pemanggil adalah HealthSystem.OnDeath, tapi guard ini
        // membuat ShowGameOver() aman kalau suatu saat dipanggil dua kali.
        if (_isShowing) return;
        _isShowing = true;

        // Kontrol dimatikan sekarang, bukan setelah showDelay, supaya player
        // tidak sempat masih jalan atau shoot di detik terakhirnya.
        SetInputEnabled(false);

        StartCoroutine(ShowGameOverCoroutine());
    }

    private IEnumerator ShowGameOverCoroutine()
    {
        // Realtime, bukan WaitForSeconds: kalau ada Pause() yang jalan lebih
        // dulu, timeScale 0 akan membekukan delay ini selamanya.
        yield return new WaitForSecondsRealtime(showDelay);

        if (gameOverUI != null)
            gameOverUI.SetActive(true);

        Pause.On(PauseKey);
    }

    public void HideGameOver()
    {
        _isShowing = false;

        if (gameOverUI != null)
            gameOverUI.SetActive(false);

        Pause.Off(PauseKey);
        SetInputEnabled(true);
    }

    /// <summary>Wired ke Button Retry.</summary>
    public void Retry()
    {
        HideGameOver();
        SceneSystem.Load(SceneManager.GetActiveScene().name);
    }

    /// <summary>Wired ke Button Back to Main Menu.</summary>
    public void BackToMainMenu()
    {
        HideGameOver();

        // ObjectiveManager belum dipasang di scene maupun prefab mana pun,
        // jadi Instance-nya null dan pemanggilan polos jadi NRE. Pakai
        // ?. supaya aman kalau nanti memang belum ada.
        ObjectiveManager.Instance?.ClearObjectives();

        SceneSystem.Load(MainMenuScene);
    }

    private static void SetInputEnabled(bool enabled)
    {
        // Controls.EnableInput()/DisableInput() memanggil InputHub.Instance
        // tanpa null check, jadi akan NRE kalau scene diputar tanpa Boot.
        if (!Controls.IsHubAvailable) return;

        if (enabled) Controls.EnableInput();
        else Controls.DisableInput();
    }
}

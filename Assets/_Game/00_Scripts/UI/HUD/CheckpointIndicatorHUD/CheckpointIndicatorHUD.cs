using System.Collections;
using Slafurry.Utils.UI;
using UnityEngine;

/// <summary>
/// Menyalakan indikator checkpoint sebentar lalu menyembunyikannya lagi.
/// Show() dan Hide() sama-sama tanpa argumen supaya bisa langsung disambungkan
/// ke UnityEvent CollideTrigger.onTriggerEnter / onTriggerExit, atau ke
/// SpriteAnimator.onFinished kalau mau waktu sembunyinya pas di frame akhir.
///
/// Indikator disembunyikan di Awake, jadi baru muncul setelah Show() dipanggil.
/// </summary>
public class CheckpointIndicatorHUD : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Animator pada objek 'CheckpointIndicator'. Kalau dikosongkan, dicari otomatis.")]
    [SerializeField] private SpriteAnimator indicatorAnimator;
    [Tooltip("Objek yang dinyalakan saat Show(). Default: objek milik indicatorAnimator.")]
    [SerializeField] private GameObject indicatorRoot;

    [Header("Visibility")]
    [Tooltip("Berapa lama indikator tetap terlihat setelah Show() sebelum disembunyikan.")]
    [SerializeField, Min(0.05f)] private float visibleSeconds = 1.5f;

    private Coroutine hideRoutine;
    private bool warnedMissingIndicator;

    private void Awake()
    {
        if (indicatorAnimator == null) indicatorAnimator = GetComponentInChildren<SpriteAnimator>(true);
        if (indicatorRoot == null && indicatorAnimator != null) indicatorRoot = indicatorAnimator.gameObject;

        // Mulai dalam keadaan tersembunyi. Kalau dibiarkan aktif, playOnEnable
        // milik SpriteAnimator akan memutar animasi seketika scene dimuat.
        Hide();
    }

    /// <summary>
    /// Munculkan indikator, putar animasinya, lalu Hide() otomatis setelah
    /// visibleSeconds. Memanggilnya dua kali akan menimer ulang, bukan menumpuk.
    /// </summary>
    public void Show()
    {
        if (indicatorRoot == null)
        {
            if (warnedMissingIndicator) return;
            warnedMissingIndicator = true;
            Debug.LogError($"[{nameof(CheckpointIndicatorHUD)}] Tidak menemukan SpriteAnimator di '{name}'.", this);
            return;
        }

        if (hideRoutine != null) StopCoroutine(hideRoutine);

        indicatorRoot.SetActive(true);

        // playOnEnable sudah memicu Play() lewat OnEnable, tapi memanggilnya
        // lagi di sini aman (Play() selalu restart dari frame pertama) dan
        // tetap jalan kalau playOnTrim dimatikan.
        indicatorAnimator?.Play();

        hideRoutine = StartCoroutine(HideAfterDelay());
    }

    /// <summary>
    /// Sembunyikan indikator. Aman dipanggil berulang kali, jadi Hide() dari
    /// timer dan Hide() dari UnityEvent tidak akan saling bentrok.
    /// </summary>
    public void Hide()
    {
        if (hideRoutine != null)
        {
            StopCoroutine(hideRoutine);
            hideRoutine = null;
        }

        if (indicatorRoot == null) return;

        // Stop() juga mengembalikan sprite asli Image, jadi kalau objek ini
        // dinyalakan lagi tidak mulai dari frame terakhir yang tertinggal.
        indicatorAnimator?.Stop();
        indicatorRoot.SetActive(false);
    }

    private IEnumerator HideAfterDelay()
    {
        // Realtime, sama seperti useUnscaledTime milik SpriteAnimator: kalau
        // game di-pause di tengah indikator, animasi ikut membeku dan Hide()
        // tidak akan pernah dipanggil kalau ini memakai WaitForSeconds.
        yield return new WaitForSecondsRealtime(visibleSeconds);

        hideRoutine = null;
        Hide();
    }
}

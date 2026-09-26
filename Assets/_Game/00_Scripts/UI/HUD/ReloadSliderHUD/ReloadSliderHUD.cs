using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Bar reload pistol. Hanya kelihatan selama pistol sedang dimuat ulang
/// (setelah tembakan, sampai pistol siap ditembak lagi), terisi 0..1
/// seiring sisa cooldown, lalu hilang sendiri.
///
/// Sumber datanya BayonetController, bukan jam lokal, jadi bar ini
/// tidak mungkin bilang "siap" kalau pistolnya masih terkunci.
/// </summary>
public class ReloadSliderHUD : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Slider di prefab ReloadSliderHUD. Kalau dikosongkan, dicari otomatis.")]
    [SerializeField] private Slider reloadSlider;
    [Tooltip("GameObject yang disembunyikan saat tidak reload - isi child yang memegang art, BUKAN objek yang punya script ini.")]
    [SerializeField] private GameObject visuals;
    [Tooltip("BayonetController yang jadi sumber reload. Kalau dikosongkan, dicari lewat parent.")]
    [SerializeField] private BayonetController bayonet;

    private Coroutine bindRoutine;
    private bool isBound;
    private bool isVisible;

    private void Awake()
    {
        if (reloadSlider == null) reloadSlider = GetComponentInChildren<Slider>(true);
        if (reloadSlider == null)
        {
            Debug.LogError($"[{nameof(ReloadSliderHUD)}] Tidak menemukan Slider di '{name}'.", this);
            enabled = false;
            return;
        }

        // Bar ini display-only. Tanpa ini pemain bisa menggeser HUD pakai mouse.
        reloadSlider.interactable = false;
        reloadSlider.SetValueWithoutNotify(0f);

        if (visuals == null) visuals = reloadSlider.gameObject;

        // Kalau visuals menunjuk ke objek yang sama dengan script ini,
        // SetActive(false) akan mematikan HUD-nya sendiri - bar tidak akan
        // pernah bisa muncul lagi setelah reload pertama.
        if (visuals == gameObject)
        {
            Debug.LogError(
                $"[{nameof(ReloadSliderHUD)}] 'visuals' menunjuk ke objek '{name}' yang memegang script ini. " +
                "Arahkan ke child yang berisi art bar, kalau tidak bar tidak akan muncul lagi setelah reload pertama.",
                this);
            enabled = false;
            return;
        }

        if (bayonet == null) bayonet = GetComponentInParent<BayonetController>();
    }

    private void OnEnable()
    {
        SetVisible(false);
        bindRoutine = StartCoroutine(BindAndSubscribe());
    }

    private IEnumerator BindAndSubscribe()
    {
        // HUD ini bisa OnEnable sebelum BayonetController selesai Awake kalau
        // ditempatkan di scene sendiri, jadi tunggu dulu.
        while (bayonet == null) yield return null;

        bayonet.OnReloadStarted += HandleReloadStarted;
        bayonet.OnReloadFinished += HandleReloadFinished;
        isBound = true;

        // HUD baru aktif di tengah reload yang sedang jalan.
        if (bayonet.IsReloading) HandleReloadStarted();
    }

    private void OnDisable()
    {
        // Coroutine hanya mati otomatis kalau GameObject-nya dimatikan, bukan
        // kalau komponennya, jadi hentikan sendiri supaya enable berikutnya
        // tidak daftar dua kali ke event yang sama.
        if (bindRoutine != null)
        {
            StopCoroutine(bindRoutine);
            bindRoutine = null;
        }

        SetVisible(false);

        if (!isBound) return;

        bayonet.OnReloadStarted -= HandleReloadStarted;
        bayonet.OnReloadFinished -= HandleReloadFinished;
        isBound = false;
    }

    private void Update()
    {
        if (!isBound || !isVisible) return;

        reloadSlider.SetValueWithoutNotify(bayonet.ReloadProgress);
    }

    private void HandleReloadStarted()
    {
        reloadSlider.SetValueWithoutNotify(0f);
        SetVisible(true);
    }

    private void HandleReloadFinished() => SetVisible(false);

    private void SetVisible(bool value)
    {
        isVisible = value;
        if (visuals != null) visuals.SetActive(value);
    }
}

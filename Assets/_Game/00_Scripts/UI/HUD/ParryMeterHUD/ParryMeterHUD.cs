using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Slafurry.Utils.VFX;

/// <summary>
/// Menampilkan charge Parry sebagai bar. Menyetir Slider yang sudah ada di
/// prefab ParryMeterHUD dan mewarnai Fill-nya mengikuti gradasi
/// hijau -> kuning -> oranye -> merah seiring bar terisi.
/// </summary>
public class ParryMeterHUD : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Slider di prefab ParryMeterHUD. Kalau dikosongkan, dicari otomatis.")]
    [SerializeField] private Slider parrySlider;
    [Tooltip("Image di dalam Fill Area. Kalau dikosongkan, diambil dari parrySlider.fillRect.")]
    [SerializeField] private Image fillImage;

    [Header("Fill Color Gradation")]
    [Tooltip("Warna bar kosong (charge = 0). Hijau di awal, lalu naik ke merah saat penuh.")]
    [SerializeField] private Color startColor = new Color(0.25f, 0.85f, 0.35f);
    [Tooltip("Warna charge rendah, ~1/3 bar.")]
    [SerializeField] private Color lowColor = new Color(0.95f, 0.90f, 0.20f);
    [Tooltip("Warna charge menengah, ~2/3 bar.")]
    [SerializeField] private Color midColor = new Color(1f, 0.55f, 0.15f);
    [Tooltip("Warna bar penuh (charge = 1).")]
    [SerializeField] private Color endColor = new Color(0.85f, 0.20f, 0.20f);

    [Header("Shake")]
    [Tooltip("Guncangan maksimum dalam piksel saat bar penuh. Bar kosong = tidak berguncang.")]
    [SerializeField, Range(0f, 20f)] private float shakeAmplitude = 6f;
    [Tooltip("Kecepatan guncangan. Makin besar, makin frantic.")]
    [SerializeField, Range(1f, 40f)] private float shakeFrequency = 16f;
    [Tooltip("0 = guncang vertikal saja, 1 = sama besar di kedua sumbu.")]
    [SerializeField, Range(0f, 1f)] private float horizontalRatio = 0.2f;

    [Header("Debug")]
    [Tooltip("Button asli untuk memicu parry. Kalau kosong, dipakai tombol OnGUI di bawah.")]
    [SerializeField] private Button debugParryButton;
    [Tooltip("Tampilkan tombol parry makeshift (OnGUI) kalau debugParryButton kosong. Matikan sebelum build.")]
    [SerializeField] private bool showDebugGuiButton = true;
    [Tooltip("Spawner VFX parry, biar spark ikut muncul waktu debug. Kalau kosong, dicari otomatis.")]
    [SerializeField] private ParryVFX parryVFX;

    private Coroutine bindRoutine;
    private bool isBound;
    private RectTransform shakeTarget;
    private Vector2 shakeOrigin;
    private float noiseSeed;
    private float currentCharge;

    private void Awake()
    {
        if (parrySlider == null) parrySlider = GetComponentInChildren<Slider>(true);
        if (parrySlider == null)
        {
            Debug.LogError($"[{nameof(ParryMeterHUD)}] Tidak menemukan Slider di '{name}'.", this);
            enabled = false;
            return;
        }

        // Bar ini display-only. Tanpa ini pemain bisa menggeser HUD pakai mouse.
        parrySlider.interactable = false;
        parrySlider.SetValueWithoutNotify(0f);

        if (fillImage == null && parrySlider.fillRect != null)
        {
            fillImage = parrySlider.fillRect.GetComponent<Image>();
        }

        // Guncangan ditambahkan ke anchoredPosition, jadi posisi asli harus
        // disimpan dulu supaya tidak terakumulasi tiap frame.
        shakeTarget = parrySlider.GetComponent<RectTransform>();
        shakeOrigin = shakeTarget.anchoredPosition;
        noiseSeed = Random.Range(0f, 500f);

        if (debugParryButton != null) debugParryButton.onClick.AddListener(DebugRegisterParry);

        if (parryVFX == null) parryVFX = GetComponentInParent<ParryVFX>();
    }

    private void OnEnable() => bindRoutine = StartCoroutine(BindAndSubscribe());

    private IEnumerator BindAndSubscribe()
    {
        // HUD ini ikut Players.prefab, jadi bisa OnEnable sebelum
        // ParryMeter.Awake selesai. Tunggu singleton-nya dulu.
        while (ParryMeter.Instance == null) yield return null;

        ParryMeter meter = ParryMeter.Instance;
        meter.OnChargeChanged += HandleChargeChanged;
        isBound = true;
        HandleChargeChanged(meter.Charge);
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

        if (shakeTarget != null) shakeTarget.anchoredPosition = shakeOrigin;

        if (debugParryButton != null) debugParryButton.onClick.RemoveListener(DebugRegisterParry);

        if (!isBound) return;

        ParryMeter.Instance.OnChargeChanged -= HandleChargeChanged;
        isBound = false;
    }

    private void Update()
    {
        if (shakeTarget == null || currentCharge <= 0f) return;

        // Makin penuh bar, makin keras guncangnya. Pakai Perlin, bukan sinus:
        // sinus itu periodik, jadi mata mengunci ritmenya dan guncangan sekeras
        // apa pun tetap terbaca sebagai "bergoyang pelan". Noise tidak punya
        // periode, jadi tidak pernah resolve jadi ayunan.
        float t = Time.unscaledTime * shakeFrequency;
        float amplitude = shakeAmplitude * currentCharge;

        shakeTarget.anchoredPosition = shakeOrigin + new Vector2(
            Tremor(t, noiseSeed) * amplitude * horizontalRatio,
            Tremor(t, noiseSeed + 17.31f) * amplitude);
    }

    private static float Tremor(float t, float seed)
    {
        float coarse = Mathf.PerlinNoise(t, seed);
        float fine = Mathf.PerlinNoise(t * 2.37f, seed + 4.19f);
        return (coarse + fine * 0.5f) / 1.5f - 0.5f;
    }

    private void HandleChargeChanged(float charge)
    {
        currentCharge = charge;

        // Slider yang mengisi fillAmount-nya sendiri, jadi jangan sentuh
        // fillAmount di sini - hanya nilai Slider dan warna Image.
        parrySlider.SetValueWithoutNotify(charge);

        if (fillImage != null)
        {
            fillImage.color = EvaluateColor(charge);
        }
    }

    /// <summary>
    /// Trik pengujian: isi charge tanpa perlu musuh untuk dip-parry.
    ///
    /// Jalur ini tidak lewat BayonetController.ExecuteParryLogic(), jadi
    /// tidak ada apa pun dari parry sungguhan yang terjadi - tidak ada
    /// knockback musuh, tidak ada cek radius, tidak ada log [Parry Success].
    /// Yang dipalsukan di sini hanya meter, SFX, dan VFX-nya. Jadi ini
    /// cuma bisa dipakai buat ngukur gauge dan iterasi sprite/animasi spark,
    /// bukan buat memverifikasi mekanik parry-nya.
    ///
    /// Play() tanpa argumen memakai spawnPoint kalau di-assign, jadi arahkan
    /// spawnPoint ke shootDir milik bayonet supaya posisi dan rotasinya sama
    /// dengan parry sungguhan.
    /// </summary>
    private void DebugRegisterParry()
    {
        if (ParryMeter.Instance == null) return;

        ParryMeter.Instance.RegisterParry();
        AudioSystem.Instance?.PlaySFX("ParrySFX", waitForCompletion: false);
        parryVFX?.Play();
    }

    private void OnGUI()
    {
        if (debugParryButton != null || !showDebugGuiButton) return;
        if (GUI.Button(new Rect(12f, 12f, 120f, 34f), "Parry (Debug)")) DebugRegisterParry();
    }

    /// <summary>
    /// Gradasi 4 titik terhadap charge ternormalisasi 0..1.
    /// </summary>
    private Color EvaluateColor(float t)
    {
        const float FirstThird = 1f / 3f;
        const float SecondThird = 2f / 3f;

        if (t < FirstThird) return Color.Lerp(startColor, lowColor, t / FirstThird);
        if (t < SecondThird) return Color.Lerp(lowColor, midColor, (t - FirstThird) / FirstThird);
        return Color.Lerp(midColor, endColor, (t - SecondThird) / FirstThird);
    }
}

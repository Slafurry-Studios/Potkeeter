using System;
using UnityEngine;

/// <summary>
/// Nyimpanan charge Parry pemain. Bertambah setiap parry sukses, berkurang
/// sendiri seiring waktu, dan memberi buff (cooldown tembakan lebih cepat)
/// selama charge berada di atas ambang buff.
/// </summary>
public class ParryMeter : MonoBehaviour
{
    public static ParryMeter Instance { get; private set; }

    /// <summary>Charge ternormalisasi, 0 (kosong) sampai 1 (penuh).</summary>
    public float Charge => charge;

    /// <summary>
    /// Pengali untuk shootCooldown milik BayonetController. Dibaca dari sana
    /// per percobaan shoot, jadi tidak perlu push pembaruan ke zwei arah.
    /// </summary>
    public float ShootCooldownMultiplier => IsBuffActive ? buffedShootCooldownMultiplier : 1f;

    public bool IsBuffActive => charge >= buffThreshold;

    /// <summary>Dipanggil HUD lewat ParryMeterHUD. Argumennya 0..1.</summary>
    public event Action<float> OnChargeChanged;

    [Header("Charge Settings")]
    [Tooltip("Charge yang didapat tiap parry sukses (0-1). 0.25 = 4 parry untuk bar penuh.")]
    [SerializeField, Range(0.05f, 1f)] private float chargePerParry = 0.25f;
    [Tooltip("Seberapa cepat charge habis, dalam charge per detik. 0.1 = bar penuh habis dalam 10 detik.")]
    [SerializeField, Range(0f, 1f)] private float drainPerSecond = 0.1f;

    [Header("Buff Settings")]
    [Tooltip("Ambang charge minimum agar buff aktif. 0.5 = butuh bar terisi setengah. 0 = buff selalu aktif.")]
    [SerializeField, Range(0f, 1f)] private float buffThreshold = 0.5f;
    [Tooltip("Pengali shootCooldown saat buff aktif. 0.5 = cooldown jadi separuh (lebih cepat shoot).")]
    [SerializeField, Range(0f, 1f)] private float buffedShootCooldownMultiplier = 0.5f;

    private float charge;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (charge <= 0f) return;

        // Pakai Time.deltaTime, bukan unscaledTime: charge ikut berhenti saat
        // game di-pause, sama seperti animasi gameplay lainnya.
        SetCharge(charge - drainPerSecond * Time.deltaTime);
    }

    /// <summary>
    /// Dipanggil BayonetController saat TryParry() berhasil. Parry yang gagal
    /// atau tidak mengenai musuh tidak menambah charge.
    /// </summary>
    public void RegisterParry() => SetCharge(charge + chargePerParry);

    public void ResetCharge() => SetCharge(0f);

    /// <summary>
    /// Bisa diklik dari gear-menu komponen saat Play mode, buat nge-cek
    /// charge/buff tanpa harus parry musuh sungguhan.
    /// </summary>
    [ContextMenu("Debug/Register Parry")]
    private void ContextRegisterParry() => RegisterParry();

    [ContextMenu("Debug/Reset Charge")]
    private void ContextResetCharge() => ResetCharge();

    private void SetCharge(float value)
    {
        float clamped = Mathf.Clamp01(value);
        if (Mathf.Approximately(clamped, charge)) return;

        charge = clamped;
        OnChargeChanged?.Invoke(charge);
    }
}

using UnityEngine;
using UnityEngine.UI;
using TMPro; // Gunakan ini jika Anda memakai TextMeshPro untuk teks UI

public class PlayerHealth : MonoBehaviour, IDamageable
{
    public HealthSystem Health { get; private set; }
    public static PlayerHealth Instance { get; private set; }

    /// <summary>Durasi i-frame, dibaca PlayerDamageBlink supaya angka yang sama dipakai kedua script.</summary>
    public float InvincibilityDuration => invincibilityDuration;

    /// <summary>
    /// True selama i-frame masih aktif.
    ///
    /// Pakai unscaledTime, bukan time. HitStop (Utils/GameFeel/Effects/HitStop.cs)
    /// men-set timeScale 0 tepat saat player terkena damage, jadi kalau timer-nya
    /// scaled, i-frame ikut membeku selama hitstop dan durasinya jadi tidak
    /// bisa diprediksi.
    /// </summary>
    public bool IsInvincible => Time.unscaledTime < invincibleUntil;

    private float invincibleUntil;

    [Header("Settings")]
    [SerializeField] private float maxHealth = 100f;

    [Header("Invincibility")]
    [Tooltip("Detik kebal setelah terkena damage. Selama window ini damage berikutnya diabaikan, dan PlayerDamageBlink membuat sprite berkedip. 0 = tidak ada i-frame.")]
    [SerializeField] private float invincibilityDuration = 1f;

    [Header("UI References")]
    [SerializeField] private Slider healthBarSlider;
    [SerializeField] private TextMeshProUGUI healthText;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        Health = new HealthSystem(maxHealth);
    }

    private void Start()
    {
        Health.OnHealthChanged += UpdateHealthBarUI;
        Health.OnDamageReceived += HandleDamageReceived;
        Health.OnDeath += HandlePlayerDeath;

        UpdateHealthBarUI(Health.CurrentHealth, Health.MaxHealth);
    }

    private void OnDestroy()
    {
        if (Health != null)
        {
            Health.OnHealthChanged -= UpdateHealthBarUI;
            Health.OnDamageReceived -= HandleDamageReceived;
            Health.OnDeath -= HandlePlayerDeath;
        }
    }

    /// <summary>
    /// Dipakai event OnDamageReceived, bukan TakeDamage(), supaya SFX tidak
    /// berbunyi saat damage ditolak. HealthSystem sudah menahan panggilan
    /// kalau player sudah mati atau damage-nya nol.
    ///
    /// Window i-frame juga dibuka di sini, bukan di TakeDamage(), karena itu
    /// satu-satunya tempat yang dijamin hanya jalan kalau damage benar-benar
    /// mengenai player. Kalau dibuka di TakeDamage(), tiap panggilan yang
    /// ditolak ikut me-reset timer dan i-frame jadi tidak pernah habis saat
    /// damage terus-menerus masuk.
    /// </summary>
    private void HandleDamageReceived(float amount)
    {
        invincibleUntil = Time.unscaledTime + invincibilityDuration;
        AudioSystem.Instance?.PlaySFX("TakeDamage", waitForCompletion: false);
    }

    private void UpdateHealthBarUI(float current, float max)
    {
        if (healthBarSlider != null)
        {
            healthBarSlider.maxValue = max;
            healthBarSlider.value = current;
        }

        if (healthText != null)
        {
            healthText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }

        Debug.Log($"[UI Updated] Health: {current} / {max}");
    }

    private void HandlePlayerDeath()
    {
        Debug.Log("Player Die!");

        // GameOverHUD.prefab ada sebagai anak Players.prefab, tapi bukan anak
        // GameObject ini, jadi GetComponentInParent dari sini tidak akan
        // menemukannya.
        if (GameOver.Instance == null)
        {
            Debug.LogError("[GameOver] Player mati tapi tidak ada component GameOver. " +
                           "Pasang script GameOver di GameOverHUD.prefab.");
            return;
        }

        GameOver.Instance.ShowGameOver();
    }

    /// <summary>
    /// Membuka jendela i-frame dari luar, tanpa lewat damage.
    ///
    /// Dipakai perfect parry: parry yang benar-benar menetralkan sesuatu
    /// memberi waktu napas sekejap, jadi retaliation di detik yang sama tidak
    /// langsung cabut HP.
    ///
    /// Pakai Mathf.Max, bukan assignment biasa, supaya jendela yang sudah
    /// terbuka dari damage sebelumnya tidak ikut dipotong kalau parry happen
    /// di tengah i-frame itu -(window yang lebih panjang harus menang, bukan
    /// yang baru dan lebih pendek).
    ///
    /// Sadar: ini tidak memicu Health.OnDamageReceived, jadi
    /// PlayerDamageBlink tidak menyala dan SFX TakeDamage tidak berbunyi.
    /// Kebal dari parry karena itu senyap, tidak terlihat.
    /// </summary>
    public void GrantInvincibility(float duration)
    {
        if (duration <= 0f) return;
        invincibleUntil = Mathf.Max(invincibleUntil, Time.unscaledTime + duration);
    }

    /// <summary>
    /// Pintu masuk generik untuk damage dari mana saja, termasuk Bullet.
    ///
    /// Ini satu-satunya titik yang menahan i-frame, jadi semua sumber damage
    /// (Bullet lewat IDamageable, HazardArea) ikut kebal tanpa tambahan apa pun.
    /// Kalau ada pemanggil yang mau melewati i-frame, dia harus sengaja
    /// menembak Health.TakeDamage() secara langsung.
    /// </summary>
    public void TakeDamage(float amount)
    {
        if (Health == null) return;
        if (IsInvincible) return;
        Health.TakeDamage(amount);
    }
}
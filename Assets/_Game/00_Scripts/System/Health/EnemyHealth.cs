using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    public HealthSystem Health { get; private set; }

    [Header("Settings")]
    [SerializeField] private float maxHealth = 50f;

    [Header("SFX")]
    [Tooltip("Key di sfxSounds (AudioSystem) yang diputar tiap enemy ini kena damage. Kosongkan untuk diam.")]
    [SerializeField] private string hitSFX = "EnemyHit";
    [Tooltip("Key di sfxSounds (AudioSystem) yang diputar saat enemy ini mati. Kosongkan untuk diam.")]
    [SerializeField] private string deathSFX = "EnemyDeath";

    [Header("Hit Blink")]
    [Tooltip("Lama total kedipan dalam detik. 0 = matikan.")]
    [SerializeField, Range(0f, 1f)] private float hitBlinkDuration = 0.12f;
    [Tooltip("Satu siklus nyala-padam dalam detik. 0.04 dengan durasi 0.12 = tiga kedipan.")]
    [SerializeField, Range(0.01f, 0.5f)] private float hitBlinkInterval = 0.04f;
    [Tooltip("Alpha saat fase mati, dikalikan ke alpha asli sprite. 0 = hilang total, 0.3 = transparan.")]
    [SerializeField, Range(0f, 1f)] private float hitBlinkAlpha = 0.2f;
    [Tooltip("Sprite yang ikut berkedip. Kosongkan = semua SpriteRenderer di bawah enemy ini.")]
    [SerializeField] private SpriteRenderer[] blinkRenderers = new SpriteRenderer[0];

    [Header("UI References (Optional)")]
    [SerializeField] private Slider healthBarSlider;
    [SerializeField] private TextMeshProUGUI healthText;

    private float[] blinkOriginalAlpha;
    private Coroutine blinkRoutine;

    private void Awake()
    {
        Health = new HealthSystem(maxHealth);
        CollectBlinkRenderers();
    }

    private void Start()
    {
        // Subscribe events
        Health.OnHealthChanged += UpdateHealthBarUI;
        Health.OnDamageReceived += PlayHitAnimation;
        Health.OnDeath += HandleEnemyDeath;

        UpdateHealthBarUI(Health.CurrentHealth, Health.MaxHealth);
    }

    private void OnDestroy()
    {
        if (Health != null)
        {
            Health.OnHealthChanged -= UpdateHealthBarUI;
            Health.OnDamageReceived -= PlayHitAnimation;
            Health.OnDeath -= HandleEnemyDeath;
        }
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

        // Log hanya kalau enemy ini memang punya UI. Tanpa guard ini, setiap
        // peluru yang kena drone akan menyiram console dengan baris yang sama.
        if (healthBarSlider != null || healthText != null)
        {
            Debug.Log($"[{gameObject.name} UI Updated] Health: {current} / {max}");
        }
    }

    private void PlayHitAnimation(float damage)
    {
        Debug.Log($"[{gameObject.name}] Terkena Damage: {damage}");

        PlayHitBlink();

        // Dipakai event OnDamageReceived, bukan TakeDamage(), supaya tidak
        // berbunyi untuk panggilan yang ditolak (sudah mati atau damage 0).
        if (!string.IsNullOrEmpty(hitSFX))
        {
            AudioSystem.Instance?.PlaySFX(hitSFX, waitForCompletion: false);
        }
    }

    /// <summary>
    /// Memulai kedipan singkat. Kalau masih kedip dari hit sebelumnya,
    /// kedipan yang lama dibatalkan dan diulang dari awal - bukan ditumpuk,
    /// jadi tembakan cepat tetap terbaca sebagai satu kedipan tunggal yang
    /// terus di-refresh.
    /// </summary>
    private void PlayHitBlink()
    {
        if (hitBlinkDuration <= 0f) return;
        if (blinkRenderers == null || blinkRenderers.Length == 0) return;

        SnapshotAlpha();

        if (blinkRoutine != null) StopCoroutine(blinkRoutine);
        blinkRoutine = StartCoroutine(BlinkRoutine());
    }

    /// <summary>
    /// Hanya alpha yang dicatat, dan hanya alpha yang diubah. DummyEnemyTest
    /// menulis spriteRenderer.color penuh untuk telegraph serangannya
    /// (idle/warning/attack/parried) pada objek yang sama, jadi kalau warna
    /// penuh ikut di-cache lalu dikembalikan, blink akan menimpa warna
    /// telegraph itu dengan warna yang lebih lama. Dengan alpha-only, perubahan
    /// RGB dari sistem lain tetap utuh.
    /// </summary>
    private void SnapshotAlpha()
    {
        blinkOriginalAlpha = new float[blinkRenderers.Length];
        for (int i = 0; i < blinkRenderers.Length; i++)
        {
            blinkOriginalAlpha[i] = blinkRenderers[i] != null
                ? blinkRenderers[i].color.a
                : 1f;
        }
    }

    private IEnumerator BlinkRoutine()
    {
        float step = Mathf.Max(0.01f, hitBlinkInterval);
        int steps = Mathf.Max(1, Mathf.RoundToInt(hitBlinkDuration / step));

        bool visible = false;

        for (int i = 0; i < steps; i++)
        {
            ApplyBlinkVisibility(visible);
            yield return new WaitForSeconds(step);
            visible = !visible;
        }

        blinkRoutine = null;
        RestoreAlpha();
    }

    /// <summary>
    /// Alpha dikalikan ke alpha asli, bukan di-set langsung, supaya sprite yang
    /// memang sengaja disembunyikan (alpha 0) tetap tidak terlihat saat enemy
    /// ini berkedip. Warna dibaca ulang tiap panggilan supaya komponen lain
    /// yang mengubah RGB di frame yang sama tidak ikut tertimpa.
    /// </summary>
    private void ApplyBlinkVisibility(bool visible)
    {
        if (blinkOriginalAlpha == null) return;

        for (int i = 0; i < blinkRenderers.Length; i++)
        {
            if (blinkRenderers[i] == null) continue;

            Color color = blinkRenderers[i].color;
            color.a = visible ? blinkOriginalAlpha[i] : blinkOriginalAlpha[i] * hitBlinkAlpha;
            blinkRenderers[i].color = color;
        }
    }

    private void RestoreAlpha()
    {
        if (blinkOriginalAlpha == null) return;

        for (int i = 0; i < blinkRenderers.Length; i++)
        {
            if (blinkRenderers[i] == null) continue;

            Color color = blinkRenderers[i].color;
            color.a = blinkOriginalAlpha[i];
            blinkRenderers[i].color = color;
        }
    }

    private void CollectBlinkRenderers()
    {
        if (blinkRenderers != null && blinkRenderers.Length > 0) return;
        blinkRenderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    private void OnDisable()
    {
        // Wajib: coroutine hanya mati otomatis kalau GameObject-nya dimatikan,
        // tapi HandleEnemyDeath memang memanggil SetActive(false) di tengah
        // kedipan. Tanpa restore di sini, enemy yang dipakai ulang nanti akan
        // muncul dengan alpha yang tertinggal dari fase gelap.
        if (blinkRoutine != null)
        {
            StopCoroutine(blinkRoutine);
            blinkRoutine = null;
        }

        RestoreAlpha();
    }

    private void HandleEnemyDeath()
    {
        Debug.Log($"[{gameObject.name}] Die!");

        if (!string.IsNullOrEmpty(deathSFX))
        {
            AudioSystem.Instance?.PlaySFX(deathSFX, waitForCompletion: false);
        }

        // Aman men-disable gameObject di sini: AudioSystem membuat AudioSource
        // di GameObject miliknya sendiri yang DontDestroyOnLoad, bukan di enemy
        // ini. Kalau AudioSource-nya ikut nempel di enemy, SetActive(false)
        // akan ikut memutus bunyi yang baru saja diputar.
        gameObject.SetActive(false);
    }

    /// <summary>Pintu masuk generik untuk damage dari mana saja, termasuk Bullet.</summary>
    public void TakeDamage(float amount)
    {
        if (Health == null) return;
        Health.TakeDamage(amount);
    }
}
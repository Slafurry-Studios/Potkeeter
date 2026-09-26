using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EnemyHealth : MonoBehaviour, IDamageable
{
    public HealthSystem Health { get; private set; }

    [Header("Settings")]
    [SerializeField] private float maxHealth = 50f;

    [Header("UI References (Optional)")]
    [SerializeField] private Slider healthBarSlider;
    [SerializeField] private TextMeshProUGUI healthText;

    private void Awake()
    {
        Health = new HealthSystem(maxHealth);
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
    }

    private void HandleEnemyDeath()
    {
        Debug.Log($"[{gameObject.name}] Die!");

        gameObject.SetActive(false);
    }

    /// <summary>Pintu masuk generik untuk damage dari mana saja, termasuk Bullet.</summary>
    public void TakeDamage(float amount)
    {
        if (Health == null) return;
        Health.TakeDamage(amount);
    }
}
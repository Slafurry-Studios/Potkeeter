using UnityEngine;
using UnityEngine.UI;
using TMPro; // Gunakan ini jika Anda memakai TextMeshPro untuk teks UI

public class PlayerHealth : MonoBehaviour, IDamageable
{
    public HealthSystem Health { get; private set; }
    public static PlayerHealth Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private float maxHealth = 100f;

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
        Health.OnDeath += HandlePlayerDeath;

        UpdateHealthBarUI(Health.CurrentHealth, Health.MaxHealth);
    }

    private void OnDestroy()
    {
        if (Health != null)
        {
            Health.OnHealthChanged -= UpdateHealthBarUI;
            Health.OnDeath -= HandlePlayerDeath;
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

        Debug.Log($"[UI Updated] Health: {current} / {max}");
    }

    private void HandlePlayerDeath()
    {
        Debug.Log("Player Die!");
    }

    /// <summary>Pintu masuk generik untuk damage dari mana saja, termasuk Bullet.</summary>
    public void TakeDamage(float amount)
    {
        if (Health == null) return;
        Health.TakeDamage(amount);
    }
}
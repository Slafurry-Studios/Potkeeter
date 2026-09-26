using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class HealthHUD : MonoBehaviour
{
    private const int MaxHealth = 5;

    [Header("Quasos")]
    [Tooltip("Isi dengan 5 Image quaso, diurutkan dari KIRI ke KANAN. Quaso paling kanan yang pertama jadi 'broken' saat health kurang dari 5.")]
    [SerializeField] private Image[] quasos;
    [SerializeField] private Sprite goodQuaso;
    [SerializeField] private Sprite brokenQuaso;

    [Header("Shake")]
    [Tooltip("Amplitudo getar pada health = 1 (maksimum). Health penuh = diam sama sekali.")]
    [Range(0f, 10f)]
    [SerializeField] private float shakeAmplitude = 2.5f;
    [Tooltip("Kcepatan getar. Makin besar makin rapat dan gugup.")]
    [SerializeField] private float shakeFrequency = 14f;
    [Tooltip("0 = hanya getar vertikal, 1 = sama besar ke kiri-kanan.")]
    [Range(0f, 1f)]
    [SerializeField] private float horizontalRatio = 0.2f;
    [Tooltip("Keterlambatan tiap quaso. 0 = semua getar bareng, makin besar makin kelihatan rambat.")]
    [Range(0f, 1f)]
    [SerializeField] private float quasoLag = 0.05f;

    private Vector2[] restPositions;
    private Coroutine bindRoutine;
    private PlayerHealth playerHealth;
    private int lastFilled = -1;
    private float noiseSeed;

    private void Awake()
    {
        if (quasos == null || quasos.Length == 0)
        {
            Debug.LogWarning("HealthHUD: 'quasos' belum diisi, HUD tidak bisa digambar.", this);
            enabled = false;
            return;
        }

        if (quasos.Length != MaxHealth)
        {
            Debug.LogWarning($"HealthHUD: '{quasos.Length}' quaso diisi tapi MaxHealth di kode adalah {MaxHealth}.", this);
        }

        if (goodQuaso == null || brokenQuaso == null)
        {
            Debug.LogWarning("HealthHUD: goodQuaso / brokenQuaso belum diisi.", this);
        }

        noiseSeed = Random.Range(0f, 500f);

        restPositions = new Vector2[quasos.Length];
        for (int i = 0; i < quasos.Length; i++)
        {
            if (quasos[i] == null) continue;
            restPositions[i] = ((RectTransform)quasos[i].transform).anchoredPosition;
        }
    }

    private void OnEnable()
    {
        if (!enabled) return;

        lastFilled = -1;
        Refresh();

        bindRoutine = StartCoroutine(BindAndSubscribe());
    }

    private void OnDisable()
    {
        if (bindRoutine != null)
        {
            StopCoroutine(bindRoutine);
            bindRoutine = null;
        }

        Unsubscribe();

        ResetShake();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private IEnumerator BindAndSubscribe()
    {
        while (playerHealth == null)
        {
            if (PlayerHealth.Instance == null)
            {
                yield return null;
                continue;
            }

            playerHealth = PlayerHealth.Instance;

            if (playerHealth.Health == null)
            {
                playerHealth = null;
                yield return null;
                continue;
            }

            playerHealth.Health.OnHealthChanged += HandleHealthChanged;

            HandleHealthChanged(playerHealth.Health.CurrentHealth, playerHealth.Health.MaxHealth);
        }

        bindRoutine = null;
    }

    private void Unsubscribe()
    {
        if (playerHealth == null || playerHealth.Health == null) return;

        playerHealth.Health.OnHealthChanged -= HandleHealthChanged;
        playerHealth = null;
    }

    private void HandleHealthChanged(float current, float max)
    {
        int filled = ToFilledCount(current, max);
        ApplyQuasoSprites(filled);
    }

    private int ToFilledCount(float current, float max)
    {
        if (max <= 0f) return 0;

        return Mathf.Clamp(Mathf.RoundToInt(current / max * MaxHealth), 0, MaxHealth);
    }

    private void Refresh()
    {
        int filled = lastFilled >= 0 ? lastFilled : MaxHealth;
        lastFilled = -1;
        ApplyQuasoSprites(filled);
    }

    private void ApplyQuasoSprites(int filled)
    {
        if (filled == lastFilled) return;
        lastFilled = filled;

        Sprite filledSprite = goodQuaso != null ? goodQuaso : (quasos[0] != null ? quasos[0].sprite : null);
        Sprite emptySprite = brokenQuaso != null ? brokenQuaso : filledSprite;

        for (int i = 0; i < quasos.Length; i++)
        {
            Image quaso = quasos[i];
            if (quaso == null) continue;

            quaso.sprite = i < filled ? filledSprite : emptySprite;
        }
    }

    private void Update()
    {
        if (restPositions == null || quasos == null) return;

        int missing = Mathf.Clamp(MaxHealth - Mathf.Max(lastFilled, 0), 0, MaxHealth);

        if (missing == 0)
        {
            ResetShake();
            return;
        }

        float t = Time.unscaledTime * shakeFrequency;
        float strength = shakeAmplitude * (missing / (float)MaxHealth);
        float horizontal = strength * horizontalRatio;

        for (int i = 0; i < quasos.Length; i++)
        {
            Image quaso = quasos[i];
            if (quaso == null) continue;

            RectTransform rect = (RectTransform)quaso.transform;
            float phase = t - i * quasoLag;

            rect.anchoredPosition = restPositions[i] + new Vector2(
                Tremor(phase, noiseSeed) * horizontal,
                Tremor(phase, noiseSeed + 17.31f) * strength
            );
        }
    }

    private static float Tremor(float t, float seed)
    {
        float coarse = Mathf.PerlinNoise(t, seed);
        float fine = Mathf.PerlinNoise(t * 2.37f, seed + 4.19f);

        return (coarse + fine * 0.5f) / 1.5f - 0.5f;
    }

    private void ResetShake()
    {
        if (restPositions == null || quasos == null) return;

        for (int i = 0; i < quasos.Length; i++)
        {
            Image quaso = quasos[i];
            if (quaso == null) continue;

            ((RectTransform)quaso.transform).anchoredPosition = restPositions[i];
        }
    }
}

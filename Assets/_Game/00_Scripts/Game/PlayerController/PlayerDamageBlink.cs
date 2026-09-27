using System.Collections;
using UnityEngine;

/// <summary>
/// Membuat sprite player berkedip selama i-frame aktif, jadi efek kebal
/// setelah terkena damage kelihatan tanpa harus fokus ke health bar.
///
/// Dipasangkan dengan i-frame di <see cref="PlayerHealth"/>: script itu yang
/// menahan damage, script ini cuma memberi tanda visual. Keduanya membaca
/// angka yang sama (<see cref="PlayerHealth.InvincibilityDuration"/>) supaya
/// durasi kedipnya tidak berbeda dari durasi kebalnya.
///
/// Dipasang di root Players, bukan di object Pot seperti PlayerHealth dan
/// PlayerFacingFlip, karena baionet ada di luar subtree Pot dan masih bagian
/// dari siluet player. Kalau dipasang di Pot, baionetnya tetap solid.
/// </summary>
public class PlayerDamageBlink : MonoBehaviour
{
    [Tooltip("Sprite yang ikut berkedip. Kalau kosong, semua SpriteRenderer di bawah objek ini, kecuali yang punya ParallaxLayer.")]
    [SerializeField] private SpriteRenderer[] blinkRenderers;

    [Header("Blink")]
    [Tooltip("Satu siklus nyala-padam dalam detik. 0.1 = 10 siklus per detik.")]
    [SerializeField, Range(0.02f, 1f)] private float blinkInterval = 0.1f;

    [Tooltip("Alpha saat fase mati, dikalikan ke alpha asli sprite. 0 = hilang total, 0.3 = transparan.")]
    [SerializeField, Range(0f, 1f)] private float offAlpha = 0f;

    private Color[] originalColors;
    private Coroutine blinkRoutine;
    private Coroutine bindRoutine;
    private PlayerHealth health;
    private bool isBound;

    private void Awake()
    {
        CollectRenderers();
        CacheOriginalColors();
    }

    private void OnEnable() => bindRoutine = StartCoroutine(BindAndSubscribe());

    private IEnumerator BindAndSubscribe()
    {
        // Komponen ini ikut Players.prefab, jadi bisa OnEnable sebelum
        // PlayerHealth.Awake selesai. Tunggu singleton-nya dulu, sama seperti
        // ParryMeterHUD.BindAndSubscribe.
        while (PlayerHealth.Instance == null) yield return null;

        health = PlayerHealth.Instance;
        health.Health.OnDamageReceived += HandleDamageReceived;
        health.Health.OnDeath += HandleDeath;
        isBound = true;
        bindRoutine = null;
    }

    private void OnDisable()
    {
        // Coroutine hanya mati otomatis kalau GameObject-nya dimatikan, bukan
        // kalau komponennya, jadi hentikan sendiri. Kalau coroutine bind
        // dibiarkan jalan, dia akan tetap daftar ke event meski komponennya
        // sudah nonaktif, dan enable berikutnya daftar dua kali.
        if (bindRoutine != null)
        {
            StopCoroutine(bindRoutine);
            bindRoutine = null;
        }

        if (blinkRoutine != null)
        {
            StopCoroutine(blinkRoutine);
            blinkRoutine = null;
        }

        if (isBound && health?.Health != null)
        {
            health.Health.OnDamageReceived -= HandleDamageReceived;
            health.Health.OnDeath -= HandleDeath;
        }
        isBound = false;

        // Wajib: tanpa restore, enable berikutnya akan tetap invisible karena
        // tidak ada yang menyalakannya lagi sampai player terkena damage.
        RestoreColors();
    }

    private void HandleDamageReceived(float amount)
    {
        if (blinkRoutine != null) StopCoroutine(blinkRoutine);
        blinkRoutine = StartCoroutine(BlinkFor(health.InvincibilityDuration));
    }

    private void HandleDeath()
    {
        // Kalau player mati di tengah kedip, sprite-nya akan tetap tidak terlihat
        // di layar game over: GameOver mem-pause game, jadi blink-nya tidak akan
        // pernah jalan lagi untuk menyelesaikan dirinya sendiri.
        if (blinkRoutine != null)
        {
            StopCoroutine(blinkRoutine);
            blinkRoutine = null;
        }

        RestoreColors();
    }

    private IEnumerator BlinkFor(float duration)
    {
        if (duration <= 0f || originalColors == null) yield break;

        float endTime = Time.unscaledTime + duration;
        bool visible = false;
        ApplyVisibility(visible);

        // WaitForSecondsRealtime, bukan WaitForSeconds: i-frame sengaja tidak
        // ikut ter-freeze oleh pause maupun hitstop.
        while (Time.unscaledTime < endTime)
        {
            yield return new WaitForSecondsRealtime(blinkInterval);
            visible = !visible;
            ApplyVisibility(visible);
        }

        blinkRoutine = null;
        RestoreColors();
    }

    /// <summary>
    /// Alpha dikalikan ke alpha asli, bukan di-set langsung.
    ///
    /// Di Players.prefab ada Bayonet dan Square yang alpha aslinya 0 karena
    /// sengaja disembunyikan, dan Pot yang warnanya merah. Kalau alpha di-set
    /// jadi offAlpha, dua sprite yang tadinya tak terlihat itu ikut muncul
    /// saat blink, dan tint merah Pot hilang di frame terakhir.
    /// </summary>
    private void ApplyVisibility(bool visible)
    {
        for (int i = 0; i < blinkRenderers.Length; i++)
        {
            if (blinkRenderers[i] == null) continue;

            Color color = originalColors[i];
            color.a = visible ? color.a : color.a * offAlpha;
            blinkRenderers[i].color = color;
        }
    }

    private void RestoreColors()
    {
        if (originalColors == null) return;

        for (int i = 0; i < blinkRenderers.Length; i++)
        {
            if (blinkRenderers[i] == null) continue;
            blinkRenderers[i].color = originalColors[i];
        }
    }

    private void CollectRenderers()
    {
        if (blinkRenderers != null && blinkRenderers.Length > 0) return;

        SpriteRenderer[] found = GetComponentsInChildren<SpriteRenderer>(true);
        System.Collections.Generic.List<SpriteRenderer> result = new(found.Length);

        foreach (SpriteRenderer renderer in found)
        {
            // Parallax Bg juga anak dari Players, tapi dia latar, bukan player.
            // Dicek lewat komponen ParallaxLayer, bukan nama object, biar tetap
            // aman kalau namanya diganti.
            if (renderer.GetComponentInParent<ParallaxLayer>() != null) continue;
            result.Add(renderer);
        }

        blinkRenderers = result.ToArray();
    }

    private void CacheOriginalColors()
    {
        originalColors = new Color[blinkRenderers.Length];
        for (int i = 0; i < blinkRenderers.Length; i++)
        {
            originalColors[i] = blinkRenderers[i] != null
                ? blinkRenderers[i].color
                : Color.white;
        }
    }
}

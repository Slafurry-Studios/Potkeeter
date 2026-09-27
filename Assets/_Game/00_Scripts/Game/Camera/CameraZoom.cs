using UnityEngine;
using Cinemachine; // Cinemachine 2.x (default untuk Unity 2022)

/// <summary>
/// Zoom kamera 2D orthografis lewat scroll mouse, diklem di antara
/// <see cref="minSize"/> dan <see cref="maxSize"/>.
///
/// Yang dikontrol adalah OrthographicSize di lens Cinemachine, bukan
/// Camera.orthographicSize langsung - supaya tetap kompatibel dengan
/// blending/priority Cinemachine dan tidak "dilawan" tiap frame oleh
/// CinemachineBrain.
///
/// Tidak bergantung pada InputHub/Slafurry apa pun: scroll dibaca langsung
/// tiap frame lewat Input.GetAxis("Mouse ScrollWheel") bawaan Unity, jadi
/// script ini jalan sendiri di scene mana pun tanpa Boot.unity.
///
/// Nilai awal diambil dari OrthographicSize yang sudah ada di lens, jadi
/// scene tetap menentukan posisi zoom-nya pada frame pertama.
/// </summary>
public class CameraZoom : MonoBehaviour
{
    [Header("Kamera")]
    [Tooltip("Kosongkan kalau script ini nempel di GameObject yang sama dengan CinemachineVirtualCamera.")]
    [SerializeField] private CinemachineVirtualCamera targetCamera;

    [Header("Batas zoom")]
    [Tooltip("OrthographicSize terkecil = paling zoom in.")]
    [SerializeField] private float minSize = 2.5f;
    [Tooltip("OrthographicSize terbesar = paling zoom out.")]
    [SerializeField] private float maxSize = 7f;

    [Header("Kecepatan")]
    [Tooltip("Seberapa besar satu langkah scroll mengubah OrthographicSize.")]
    [SerializeField] private float scrollSensitivity = 15f; // Input.GetAxis scroll skalanya kecil (~0.1), jadi sensitivity dinaikkan dibanding versi lama.
    [Tooltip("0 = langsung ikut, makin besar makin halus. Satuan: detik.")]
    [SerializeField] private float smoothTime = 0.08f;

    [Header("Debug")]
    [Tooltip("Nyalakan kalau zoom terasa tidak jalan, supaya console memberi tahu di mana rantainya putus.")]
    [SerializeField] private bool debugLog;

    private float targetSize;

    private void Awake()
    {
        if (targetCamera == null) targetCamera = GetComponent<CinemachineVirtualCamera>();

        if (targetCamera == null)
        {
            Debug.LogError($"[{nameof(CameraZoom)}] Tidak ada CinemachineVirtualCamera yang ditemukan/di-assign, " +
                           "zoom tidak akan berpengaruh ke mana pun.", this);
            enabled = false;
            return;
        }

        // Clamp di sini juga supaya kamera yang mulai di luar batas (misal
        // lens ortho 5 di menu) langsung masuk ke rentang yang valid.
        targetSize = Mathf.Clamp(targetCamera.m_Lens.OrthographicSize, minSize, maxSize);

        if (debugLog)
            Debug.Log($"[CameraZoom] start size={targetCamera.m_Lens.OrthographicSize} -> target={targetSize} " +
                      $"range=[{minSize}, {maxSize}]", this);
    }

    private void Update()
    {
        // Input.GetAxis sudah mouse-agnostic (jalan di semua platform desktop)
        // dan tidak butuh subscription/event apa pun, jadi aman dipanggil
        // tiap frame tanpa event hub eksternal.
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll == 0f) return;

        // scroll positif saat scroll ke atas, dan zoom in berarti size mengecil.
        targetSize = Mathf.Clamp(targetSize - scroll * scrollSensitivity, minSize, maxSize);

        if (debugLog) Debug.Log($"[CameraZoom] scroll={scroll} -> target={targetSize}", this);
    }

    private void LateUpdate()
    {
        if (targetCamera == null) return;

        // Pakai eksponensial supaya lama menuju target tidak bergantung pada
        // frame rate, sama seperti pola PlayerHeadAim.
        float t = smoothTime <= 0f ? 1f : 1f - Mathf.Exp(-Time.deltaTime / smoothTime);

        var lens = targetCamera.m_Lens;
        lens.OrthographicSize = Mathf.Lerp(lens.OrthographicSize, targetSize, t);
        targetCamera.m_Lens = lens;
    }

    private void OnValidate()
    {
        // Dicek di editor juga, kalau tidak min yang lebih besar dari max
        // akan membuat Mathf.Clamp throw tiap scroll.
        if (minSize > maxSize) maxSize = minSize;

        if (scrollSensitivity < 0f) scrollSensitivity = 0f;
        if (smoothTime < 0f) smoothTime = 0f;
    }
}
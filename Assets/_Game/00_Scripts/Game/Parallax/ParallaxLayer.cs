using System.Collections.Generic;
using UnityEngine;
using Slafurry.System.InputHub;

/// <summary>
/// Latar parallax yang mengulang satu gambar secara horizontal DAN vertikal
/// tanpa sambungan yang terlihat.
///
/// Cara kerjanya digeneralisasi dari trik 1D ke grid 2D: satu sprite jadi
/// banyak salinan yang berjajar tepat satu lebar/tinggi sprite di sekitar
/// art asli, membentuk grid (2*pairsX+1) x (2*pairsY+1) dengan art asli di
/// titik tengah grid itu. Pita 2D itu terus bergerak, dan begitu scroll di
/// salah satu sumbu melewati satu ubin, semua salinan di sumbu itu bergeser
/// tepat satu ubin sehingga salinan berikutnya menempati posisi yang tadi
/// ditempati salinan sebelumnya. Karena semuanya gambar yang sama,
/// pergeseran itu tidak kelihatan. Ini sebabnya gambar HARUS benar-benar
/// bisa disambung di keempat sisinya; kode tidak akan membuat sambungan
/// yang tidak ada di art-nya.
///
/// Salinan dibuat sebagai GameObject baru + SpriteRenderer baru yang
/// propertinya disalin manual dari 'art' (lihat CreateCopy). SENGAJA TIDAK
/// pakai Instantiate(art, transform): art biasanya menempel di GameObject
/// yang SAMA dengan script ini (lihat Awake), dan Instantiate atas sebuah
/// Component meng-clone SELURUH GameObject pemiliknya - termasuk
/// ParallaxLayer ini sendiri. Clone itu langsung Awake pada frame yang sama
/// dan langsung membuat clone-clone berikutnya, jadi rekursi bercabang yang
/// meledak dalam hitungan frame dan meng-crash Unity. Cara di bawah ini
/// hanya pernah menyalin tampilannya (SpriteRenderer), tidak pernah
/// menyalin script apa pun, jadi rekursi ini tidak mungkin terjadi.
///
/// Tidak pakai drawMode Tiled supaya tidak perlu mengubah Texture Wrap Mode
/// di import setting. Semua texture di repo ini wrapU: 0 (Clamp), dan Tiled
/// dengan Clamp memunculkan garis sambungan di setiap ujung ubin.
///
/// Berjalan sendiri, tidak butuh movement: dengan source Camera ia ikut
/// kamera di kedua sumbu, dengan Mouse ia bergeser mengikuti arah aim di
/// kedua sumbu, dengan Auto ia mengalir sendiri ke arah manapun.
///
/// Catatan: script ini mengambil alih X dan Y lewat runtime (Z dibiarkan
/// seperti di scene, biasanya buat depth/sorting), dan mengasumsikan scale
/// parent seragam tanpa rotasi.
/// </summary>
public class ParallaxLayer : MonoBehaviour
{
    // Batas atas jumlah salinan PER SISI PER SUMBU, biar grid 2D tidak
    // meledak. 8 berarti paling banyak grid 17x17-1 = 288 GameObject, jauh
    // dari titik yang bisa bikin Unity crash tapi tetap cukup buat kamera
    // zoom out ekstrem atau ubin yang sangat kecil.
    private const int MaxAxisPairs = 8;

    // Batas bawah lebar/tinggi ubin dalam units dunia. 0.01 artinya sprite
    // 1px dengan PPU 100, yang sudah jauh di bawah ambang bg.background yang
    // masuk akal.
    private const float MinWidth = 0.01f;

    /// <summary>
    /// Apa yang mendorong parallax. Camera = standar platformer, tapi diam
    /// kalau kamera tidak bergerak. Mouse = ikut arah aim, jalan tanpa
    /// movement. Auto = mengalir sendiri, murni dekoratif.
    /// </summary>
    public enum Source
    {
        Camera,
        Mouse,
        Auto
    }

    [Header("Sumber")]
    [Tooltip("Camera = ikut gerak kamera. Mouse = bergeser mengikuti arah aim. Auto = mengalir sendiri.")]
    [SerializeField] private Source source = Source.Camera;

    [Tooltip("Sprite gambar yang diulang. Kalau kosong, ambil dari objek ini sendiri. Salinan lain dibuat otomatis oleh script (lihat CreateCopy - bukan Instantiate langsung).")]
    [SerializeField] private SpriteRenderer art;

    [Tooltip("Kosongkan untuk memakai Camera.main.")]
    [SerializeField] private Camera targetCamera;

    [Header("Gerak")]
    [Tooltip("Seberapa jauh layer ini mengikuti kamera di kedua sumbu. 1 = menempel di kamera, paling jauh / langit. 0 = diam di dunia, paling dekat / foreground.")]
    [Range(0f, 1f)]
    [SerializeField] private float factor = 0.5f;

    [Tooltip("Source Mouse: berapa units dunia (X, Y) yang digeser saat mouse bergerak dari titik tengah ke tepi layar.")]
    [SerializeField] private Vector2 mouseRange = new Vector2(2f, 2f);

    [Tooltip("Source Auto: kecepatan mengalir per sumbu, units per detik. Boleh diagonal, mis. (0.5, 0.2).")]
    [SerializeField] private Vector2 autoVelocity = new Vector2(0.5f, 0f);

    [Header("Setup")]
    [Tooltip("Lebar satu ubin dalam units dunia. 0 = ambil dari ukuran sprite. Pakai ini kalau sprite-nya cuma sebagian dari satu ubin secara horizontal.")]
    [SerializeField] private float tileWidthOverride;

    [Tooltip("Tinggi satu ubin dalam units dunia. 0 = ambil dari ukuran sprite. Pakai ini kalau sprite-nya cuma sebagian dari satu ubin secara vertikal.")]
    [SerializeField] private float tileHeightOverride;

    [Tooltip("Jumlah salinan di tiap SISI sumbu X, di luar art asli. 0 = otomatis dari lebar layar, dan ikut bertambah saat kamera di-zoom out.")]
    [SerializeField] private int extraPairsX;

    [Tooltip("Jumlah salinan di tiap SISI sumbu Y, di luar art asli. 0 = otomatis dari tinggi layar, dan ikut bertambah saat kamera di-zoom out.")]
    [SerializeField] private int extraPairsY;

    [Header("Debug")]
    [Tooltip("Nyalakan kalau parallax terasa tidak jalan, supaya console memberi tahu di mana rantainya putus.")]
    [SerializeField] private bool debugLog;

    private readonly List<SpriteRenderer> copies = new List<SpriteRenderer>();
    private Camera cam;
    private float tileWidthWorld;
    private float tileHeightWorld;
    private float stepLocalX;
    private float stepLocalY;
    private float scrollX;
    private float scrollY;
    private float lastCamX;
    private float lastCamY;
    private float mouseOriginX;
    private float mouseOriginY;
    private bool mouseOriginSet;
    private float baseOffsetX;
    private float baseOffsetY;
    private float baseZ;
    private int currentPairsX = -1;
    private int currentPairsY = -1;

    private void Awake()
    {
        if (art == null) art = GetComponent<SpriteRenderer>();
        cam = targetCamera != null ? targetCamera : Camera.main;

        if (art == null)
        {
            Debug.LogError($"[{nameof(ParallaxLayer)}] '{nameof(art)}' belum di-assign pada '{name}', jadi tidak ada yang bisa diulang.", this);
            enabled = false;
            return;
        }

        if (cam == null)
        {
            Debug.LogError($"[{nameof(ParallaxLayer)}] Tidak ada kamera. Isi '{nameof(targetCamera)}' atau pastikan ada Camera.main.", this);
            enabled = false;
            return;
        }

        if (art.sprite == null)
        {
            Debug.LogError($"[{nameof(ParallaxLayer)}] Sprite di '{name}' kosong, jadi tidak ada yang bisa di-loop.", this);
            enabled = false;
            return;
        }

        // Penempatan ubin mengasumsikan art ada di objek yang sama dengan
        // script ini, karena objek itulah yang digeser jadi titik tengah pita.
        if (art.transform != transform)
        {
            Debug.LogWarning($"[{nameof(ParallaxLayer)}] '{nameof(art)}' ada di objek lain, bukan di '{name}'. " +
                             "Penempatan ubin mengasumsikan keduanya satu objek, jadi hasilnya bisa bergeser.", this);
        }

        // Scale 0 akan membuat lebar/tinggi ubin dan jarak antar ubin
        // sama-sama 0. Ini mudah terjadi karena objek background sering ikut
        // mewarisi scale 0 dari parent, dan hasil pembagiannya NaN.
        float scaleX = Mathf.Abs(transform.lossyScale.x);
        float scaleY = Mathf.Abs(transform.lossyScale.y);
        if (scaleX < Mathf.Epsilon || scaleY < Mathf.Epsilon)
        {
            Debug.LogError($"[{nameof(ParallaxLayer)}] Scale '{name}' adalah 0 di salah satu sumbu ({transform.lossyScale}), jadi ukurannya tidak bisa dihitung. " +
                           "Beri scale normal ke objek ini.", this);
            enabled = false;
            return;
        }

        // bounds.size itu units lokal sprite, dikali scale supaya jadi units
        // dunia. Override tetap dipakai kalau diisi, buat kasus gambar yang
        // dipotong jadi tidak penuh satu ubin.
        tileWidthWorld = tileWidthOverride > 0f
            ? tileWidthOverride
            : art.sprite.bounds.size.x * scaleX;
        tileHeightWorld = tileHeightOverride > 0f
            ? tileHeightOverride
            : art.sprite.bounds.size.y * scaleY;

        if (!IsUsableWidth(tileWidthWorld) || !IsUsableWidth(tileHeightWorld))
        {
            Debug.LogError($"[{nameof(ParallaxLayer)}] Ukuran ubin '{name}' dihitung sebagai ({tileWidthWorld}, {tileHeightWorld}), itu tidak bisa dipakai. " +
                           "Kalau sprite-nya sengaja cuma potongan, isi 'Tile Width/Height Override' dengan ukuran aslinya.", this);
            enabled = false;
            return;
        }

        // Jarak antar ubin dalam units lokal, karena salinan dibuat child dari
        // objek ini dan digeser lewat localPosition.
        stepLocalX = tileWidthWorld / scaleX;
        stepLocalY = tileHeightWorld / scaleY;

        // Posisi awal yang sudah di-set di scene (mis. sky ditaruh lebih
        // tinggi dari ground) DIPERTAHANKAN sebagai offset relatif terhadap
        // kamera, bukan dibuang - supaya susunan antar layer tetap terjaga
        // walau sekarang keduanya ikut mengikuti kamera naik-turun.
        lastCamX = cam.transform.position.x;
        lastCamY = cam.transform.position.y;
        baseOffsetX = transform.position.x - lastCamX;
        baseOffsetY = transform.position.y - lastCamY;
        baseZ = transform.position.z;
        scrollX = baseOffsetX;
        scrollY = baseOffsetY;

        if (debugLog)
            Debug.Log($"[ParallaxLayer] '{name}' siap. tile=({tileWidthWorld}, {tileHeightWorld}) " +
                      $"offset=({baseOffsetX}, {baseOffsetY})", this);

        EnsureCopies();
    }

    private void LateUpdate()
    {
        if (art == null || cam == null) return;

        // Zoom mengubah lebar/tinggi layar, jadi jumlah ubin yang dibutuhkan
        // bisa berubah. Dicek tiap frame tapi dibangun ulang hanya kalau
        // grid-nya benar-benar berbeda, jadi tidak ada alokasi tiap frame.
        EnsureCopies();

        AccumulateScroll();

        float wrapX = Mathf.Repeat(scrollX, tileWidthWorld);
        float wrapY = Mathf.Repeat(scrollY, tileHeightWorld);
        float x = cam.transform.position.x + wrapX;
        float y = cam.transform.position.y + wrapY;

        // Penjaga terakhir: transform dengan NaN atau Infinity bisa merosakkan
        // editor, jadi jangan pernah menulisnya sama sekali.
        if (float.IsNaN(x) || float.IsNaN(y) || float.IsInfinity(x) || float.IsInfinity(y))
        {
            enabled = false;
            Debug.LogError($"[{nameof(ParallaxLayer)}] Posisi hasil hitungan di '{name}' bukan angka ({x}, {y}), script dimatikan.", this);
            return;
        }

        // X dan Y diambil alih sepenuhnya supaya pita selalu menempel di
        // kamera dan menutup layar di kedua sumbu. Z dibiarkan seperti yang
        // di-set di scene (biasanya dipakai buat depth/sorting antar layer).
        transform.position = new Vector3(x, y, baseZ);
    }

    /// <summary>
    /// True kalau nilai ini layak dipakai untuk membagi. Menahan nol, NaN, dan
    /// Infinity di satu tempat, karena salah satu saja akan membuat pembagian
    /// di EnsureCopies jadi NaN dan posisi GameObject jadi NaN, yang bisa
    /// membuat editor ke-close paksa.
    /// </summary>
    private static bool IsUsableWidth(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value) && value >= MinWidth;
    }

    private void AccumulateScroll()
    {
        switch (source)
        {
            case Source.Camera:
                float camX = cam.transform.position.x;
                float camY = cam.transform.position.y;

                // Berbasis delta, bukan posisi absolut, supaya tidak bergantung
                // pada koordinat asal scene dan tidak menumpuk error.
                scrollX += (camX - lastCamX) * factor;
                scrollY += (camY - lastCamY) * factor;
                lastCamX = camX;
                lastCamY = camY;
                break;

            case Source.Mouse:
                Vector2 mouse = Controls.MousePosition;
                Vector3 world = cam.ScreenToWorldPoint(new Vector3(mouse.x, mouse.y, 0f));
                if (!mouseOriginSet)
                {
                    mouseOriginX = world.x;
                    mouseOriginY = world.y;
                    mouseOriginSet = true;
                }

                // Hanya selisih dari titik mouse pertama yang dipakai, jadi
                // tidak ada lompatan besar di frame pertama mouse dibaca.
                // baseOffset tetap dijaga supaya susunan antar layer tidak
                // hilang begitu source-nya Mouse.
                scrollX = baseOffsetX + (world.x - mouseOriginX) * factor * mouseRange.x;
                scrollY = baseOffsetY + (world.y - mouseOriginY) * factor * mouseRange.y;
                break;

            case Source.Auto:
                scrollX += autoVelocity.x * Time.deltaTime;
                scrollY += autoVelocity.y * Time.deltaTime;
                break;
        }
    }

    private void EnsureCopies()
    {
        // Ubin divalidasi ulang di sini, bukan cuma di Awake, karena scale
        // objek bisa diubah dari inspector atau script lain setelah Awake.
        if (!IsUsableWidth(tileWidthWorld) || !IsUsableWidth(tileHeightWorld))
        {
            Debug.LogError($"[{nameof(ParallaxLayer)}] Ukuran ubin di '{name}' tidak valid ({tileWidthWorld}, {tileHeightWorld}). " +
                           "Coba isi 'Tile Width/Height Override' dengan angka kecil yang wajar.", this);
            enabled = false;
            return;
        }

        float viewWidth = cam.orthographicSize * 2f * cam.aspect;
        float viewHeight = cam.orthographicSize * 2f;
        if (!IsUsableWidth(viewWidth) || !IsUsableWidth(viewHeight))
        {
            // orthographicSize atau aspect kamera belum valid, dan membiarkan
            // ini lewat akan membuat pembagian di bawah jadi NaN atau Infinity.
            return;
        }

        int pairsX = ComputePairs(viewWidth, tileWidthWorld, extraPairsX);
        int pairsY = ComputePairs(viewHeight, tileHeightWorld, extraPairsY);

        // Grid-nya sama seperti sebelumnya, tidak perlu dibangun ulang.
        if (pairsX == currentPairsX && pairsY == currentPairsY) return;

        // Grid (2*pairsX+1) x (2*pairsY+1), minus satu untuk titik tengah
        // yang ditempati art asli. Dibatasi lewat MaxAxisPairs per sumbu,
        // jadi hasil kali ini juga otomatis terbatas (maks 17x17-1 = 288).
        int want = (2 * pairsX + 1) * (2 * pairsY + 1) - 1;

        while (copies.Count > want)
        {
            SpriteRenderer extra = copies[copies.Count - 1];
            copies.RemoveAt(copies.Count - 1);
            if (extra != null) Destroy(extra.gameObject);
        }

        while (copies.Count < want)
        {
            copies.Add(CreateCopy());
        }

        currentPairsX = pairsX;
        currentPairsY = pairsY;

        // Art asli sudah ada di tengah grid (0,0), karena yang kita geser
        // adalah transform induknya. Salinan mengisi tiap sel grid dari
        // -pairsY..pairsY dan -pairsX..pairsX, KECUALI (0,0) yang DILEWATI -
        // kalau tidak, satu salinan akan menumpuk persis di atas art asli.
        int idx = 0;
        for (int ky = -pairsY; ky <= pairsY; ky++)
        {
            for (int kx = -pairsX; kx <= pairsX; kx++)
            {
                if (kx == 0 && ky == 0) continue;
                if (idx >= copies.Count) continue; // penjaga, seharusnya tidak pernah kepakai
                copies[idx].transform.localPosition = new Vector3(kx * stepLocalX, ky * stepLocalY, 0f);
                idx++;
            }
        }
    }

    /// <summary>
    /// Jumlah salinan yang dibutuhkan di TIAP SISI satu sumbu, supaya satu
    /// layar penuh (plus satu ubin ekstra buat buffer saat pita bergeser)
    /// selalu tertutup di sumbu itu. Dibatasi MaxAxisPairs, karena tanpa
    /// batas ini ubin yang sangat sempit/pendek akan meminta grid raksasa
    /// dan editor-nya bisa ke-close paksa.
    /// </summary>
    private static int ComputePairs(float viewSize, float tileSize, int extraPairs)
    {
        if (extraPairs > 0) return Mathf.Clamp(extraPairs, 1, MaxAxisPairs);

        int perSide = Mathf.CeilToInt(viewSize / tileSize / 2f) + 1;
        return Mathf.Clamp(perSide, 1, MaxAxisPairs);
    }

    /// <summary>
    /// Membuat satu salinan tampilan dari 'art'. SENGAJA membuat GameObject
    /// baru yang polos + SpriteRenderer baru dan menyalin propertinya satu-
    /// satu, BUKAN Instantiate(art, transform). 'art' pada pemakaian normal
    /// menempel di GameObject yang sama dengan ParallaxLayer ini (lihat
    /// Awake), jadi Instantiate atas Component itu akan meng-clone SELURUH
    /// GameObject-nya - termasuk komponen ParallaxLayer ini sendiri. Clone
    /// itu langsung Awake pada frame yang sama dan membuat clone berikutnya,
    /// jadi rekursi bercabang yang meledak dan meng-crash Unity dalam
    /// hitungan frame. Cara di bawah ini tidak pernah menyalin script apa
    /// pun, cuma tampilannya, jadi rekursi ini tidak mungkin terjadi.
    /// </summary>
    private SpriteRenderer CreateCopy()
    {
        var go = new GameObject($"{art.name} (Parallax Copy)");
        go.transform.SetParent(transform, false);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = art.sprite;
        sr.color = art.color;
        sr.flipX = art.flipX;
        sr.flipY = art.flipY;
        sr.drawMode = art.drawMode;
        sr.size = art.size;
        sr.sortingLayerID = art.sortingLayerID;
        sr.sortingOrder = art.sortingOrder;
        sr.maskInteraction = art.maskInteraction;
        sr.sharedMaterial = art.sharedMaterial;

        return sr;
    }
}
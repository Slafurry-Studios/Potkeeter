/// <summary>
/// Objek yang bisa dicounter oleh parry, yaitu apa pun yang bisa memberi
/// damage ke player: peluru, musuh yang menyerang, jebakan.
///
/// Parry tidak punya window waktu dan tidak punya syarat state. Lolos atau
/// tidaknya parry ditentukan satu hal saja: apakah objek ini masih berada di
/// dalam area parry ketika player menekan parry. Karena itu OnParried() tidak
/// mengembalikan bool - tidak ada kondisi yang bisa menggagalkannya.
///
/// Catatan: parry tidak membatalkan damage yang sudah masuk ke player. Ini
/// hanya kesempatan untuk menetralkan sumbernya sebelum serangannya mengenai.
/// </summary>
public interface IParryable
{
    /// <summary>
    /// Dipanggil BayonetController saat objek ini terdeteksi di area parry.
    /// Implementasi harus menetralkan damage dari objek ini.
    /// </summary>
    void OnParried();
}

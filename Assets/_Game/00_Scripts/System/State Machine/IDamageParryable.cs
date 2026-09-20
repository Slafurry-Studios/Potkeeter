public interface IDamageableParryable
{
    /// <summary>
    /// Mencoba melakukan parry ke musuh.
    /// Mengembalikan true jika parry berhasil (dilakukan saat Parry Window aktif).
    /// </summary>
    bool TryParry();
}   
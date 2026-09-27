using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider2D))]
public class CollideTrigger : MonoBehaviour
{
    [Tooltip("Hanya collider dengan tag ini yang memicu trigger. Kosongkan untuk menerima semua. Player = hanya player yang bisa masuk.")]
    [SerializeField] private string requiredTag = "Player";

    [SerializeField] private UnityEvent onTriggerEnter;
    [SerializeField] private UnityEvent onTriggerExit;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!Matches(other)) return;
        onTriggerEnter?.Invoke();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!Matches(other)) return;
        onTriggerExit?.Invoke();
    }

    private bool Matches(Collider2D other)
    {
        if (other == null) return false;
        if (string.IsNullOrEmpty(requiredTag)) return true;

        // other.tag, bukan CompareTag: CompareTag melempar UnityException kalau
        // tag-nya tidak terdaftar di TagManager, jadi satu salah ketik di
        // inspector akan meledak setiap kali ada yang menabrak trigger ini.
        // Perbandingan string tidak throws, dan trigger ini hanya dipanggil
        // sesekali saat player lewat, jadi biayanya tidak terasa.
        return other.tag == requiredTag;
    }
}

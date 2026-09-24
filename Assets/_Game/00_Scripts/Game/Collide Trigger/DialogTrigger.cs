using Game.UI.HUD;
using Game.Dialog;
using UnityEngine;
using UnityEngine.Events;

public class DialogTrigger : MonoBehaviour
{
    [Header("Dialog")]
    [SerializeField] private DialogBucket bucket;
    [SerializeField] private DialogHUD dialogHUD;

    [Header("Events")]
    [SerializeField] private UnityEvent onDialogEnd;
    [SerializeField] private UnityEvent[] onLineComplete;

    private void Start()
    {
        if (dialogHUD == null)
            dialogHUD = FindObjectOfType<DialogHUD>();
    }

    public void StartDialog()
    {
        if (bucket == null || dialogHUD == null) return;
        if (dialogHUD.IsBusy) return; // opsional: cegah numpuk dialog trigger lain

        dialogHUD.SetLineEvents(onLineComplete);
        dialogHUD.StartDialog(bucket, HandleDialogEnd);
    }

    private void HandleDialogEnd()
    {
        onDialogEnd?.Invoke();
    }
}
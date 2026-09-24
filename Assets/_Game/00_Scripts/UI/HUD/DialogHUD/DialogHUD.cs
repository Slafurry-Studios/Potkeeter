using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;
using DialogSystem = Game.Dialog;
using System;
using Slafurry.System.Pause;

namespace Game.UI.HUD
{
    public class DialogHUD : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI dialogText;
        [SerializeField] private GameObject nextDialogClue;
        [SerializeField] private GameObject dialogUIPrefab;

        [Header("Type Effect")]
        [SerializeField] private float typingSpeed = 0.03f;

        [Header("SFX")]
        [SerializeField] private string sfxCategory = "UI";
        [SerializeField] private string typeSFX = "typing";

        [Header("Options")]
        [SerializeField] private bool allowSkip = true;

        private DialogSystem.DialogBucket currentBucket;
        private int currentIndex;
        private bool isLast;
        private bool isTyping;

        private string currentDialog;
        private Coroutine typingCoroutine;
        private UnityEvent[] currentLineEvents;

        // Callback khusus untuk pemanggil StartDialog() saat ini.
        // Digunakan sekali lalu di-null-kan, jadi tidak "nempel" ke trigger lain.
        private Action onEndCallback;

        private void Update()
        {
            if (!allowSkip) return;
            if (!dialogUIPrefab.activeSelf) return;

            if (Input.GetKeyDown(KeyCode.Space))
            {
                NextDialog();
            }
        }

        public bool IsBusy => currentBucket != null;

        public void StartDialog(DialogSystem.DialogBucket bucket, Action onEnd = null)
        {
            if (bucket == null || bucket.dialogs.Length == 0) return;

            // Cegah dialog baru "menimpa" dialog yang sedang berjalan
            // (opsional, tapi disarankan agar tidak ada dua dialog nabrak).
            if (currentBucket != null) return;

            currentBucket = bucket;
            currentIndex = 0;
            onEndCallback = onEnd;

            Pause.On("Dialog");
            dialogUIPrefab.SetActive(true);

            ShowCurrentDialog();
        }

        public void SetLineEvents(UnityEvent[] events)
        {
            currentLineEvents = events;
        }

        public void NextDialog()
        {
            if (currentBucket == null) return;

            if (isTyping)
            {
                StopCoroutine(typingCoroutine);
                StopTypeSfx();

                dialogText.text = currentDialog;
                isTyping = false;

                if (!isLast)
                    nextDialogClue.SetActive(true);

                return;
            }

            if (isLast)
            {
                EndDialog();
                return;
            }

            currentIndex++;
            ShowCurrentDialog();
        }

        public void SkipDialog()
        {
            if (typingCoroutine != null)
            {
                StopCoroutine(typingCoroutine);
                typingCoroutine = null;
            }

            StopTypeSfx();
            isTyping = false;

            EndDialog();
        }

        private void EndDialog()
        {
            var callback = onEndCallback;
            onEndCallback = null;

            isLast = false;
            currentBucket = null;
            currentLineEvents = null;

            Pause.Off("Dialog");
            dialogUIPrefab.SetActive(false);

            callback?.Invoke();
        }

        private void ShowCurrentDialog()
        {
            DialogSystem.Dialog dialog = currentBucket.dialogs[currentIndex];
            isLast = currentIndex >= currentBucket.dialogs.Length - 1;

            nameText.text = dialog.name;
            currentDialog = dialog.dialog;

            nextDialogClue.SetActive(false);

            if (typingCoroutine != null)
                StopCoroutine(typingCoroutine);

            typingCoroutine = StartCoroutine(TypeDialog());
        }

        private IEnumerator TypeDialog()
        {
            isTyping = true;
            dialogText.text = "";

            PlayTypeSfx();

            foreach (char c in currentDialog)
            {
                dialogText.text += c;
                yield return new WaitForSecondsRealtime(typingSpeed);
            }

            StopTypeSfx();

            dialogText.text = currentDialog;
            isTyping = false;

            InvokeLineEvent();

            if (!isLast)
                nextDialogClue.SetActive(true);
        }

        private void InvokeLineEvent()
        {
            if (currentLineEvents == null) return;
            if (currentIndex < currentLineEvents.Length)
                currentLineEvents[currentIndex]?.Invoke();
        }

        private void PlayTypeSfx()
        {
            if (AudioSystem.Instance == null) return;
            AudioSystem.Instance.PlaySFX(sfxCategory);
        }

        private void StopTypeSfx()
        {
            if (AudioSystem.Instance == null) return;
            AudioSystem.Instance.StopSFX(sfxCategory);
        }
    }
}
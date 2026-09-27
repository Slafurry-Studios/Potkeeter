using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Slafurry.Utils.UI
{
    /// <summary>
    /// One-shot fade in/out controller for a CanvasGroup.
    /// Useful for screen transitions, panels, tooltips, and other UI elements
    /// that need smooth visibility changes.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class UIFade : MonoBehaviour
    {
        #region Inspector

        [Header("References")]
        [Tooltip("CanvasGroup controlled by this component.")]
        [SerializeField]
        private CanvasGroup canvasGroup;

        [Header("Fade Settings")]
        [Min(0f)]
        [Tooltip("Default duration of the fade in seconds.")]
        [SerializeField]
        private float duration = 0.3f;

        [Tooltip("Use unscaled time so the fade continues while Time.timeScale is 0.")]
        [SerializeField]
        private bool useUnscaledTime = true;

        [Tooltip(
            "When enabled, the CanvasGroup becomes non-interactable and stops " +
            "blocking raycasts when fully hidden."
        )]
        [SerializeField]
        private bool disableInteractionWhileHidden = true;

        [Header("Startup")]
        [Tooltip(
            "Automatically fade when the object starts. " +
            "If the initial alpha is above 0.5, it fades out; otherwise it fades in."
        )]
        [SerializeField]
        private bool playOnAwake = false;

        [Header("Events")]
        [Tooltip("Invoked when Fade In finishes.")]
        [SerializeField]
        private UnityEvent onFadeInComplete;

        [Tooltip("Invoked when Fade Out finishes.")]
        [SerializeField]
        private UnityEvent onFadeOutComplete;

        #endregion

        private Coroutine _routine;

        #region Unity

        private void Reset()
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        private void Awake()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();
        }

        private void Start()
        {
            if (!playOnAwake)
                return;

            if (canvasGroup.alpha > 0.5f)
                FadeOut();
            else
                FadeIn();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Fades the CanvasGroup to fully visible.
        /// </summary>
        public void FadeIn(float overrideDuration = -1f)
        {
            float fadeDuration = GetDuration(overrideDuration);

            StartFade(
                canvasGroup.alpha,
                1f,
                fadeDuration,
                () => onFadeInComplete?.Invoke()
            );
        }

        /// <summary>
        /// Fades the CanvasGroup to fully hidden.
        /// </summary>
        public void FadeOut(float overrideDuration = -1f)
        {
            float fadeDuration = GetDuration(overrideDuration);

            StartFade(
                canvasGroup.alpha,
                0f,
                fadeDuration,
                () => onFadeOutComplete?.Invoke()
            );
        }

        /// <summary>
        /// Immediately sets the CanvasGroup alpha without animation.
        /// </summary>
        public void SetImmediate(float alpha)
        {
            StopCurrentFade();

            ApplyAlpha(Mathf.Clamp01(alpha));
        }

        /// <summary>
        /// Immediately makes the CanvasGroup fully visible.
        /// </summary>
        public void ShowImmediate()
        {
            SetImmediate(1f);
        }

        /// <summary>
        /// Immediately hides the CanvasGroup.
        /// </summary>
        public void HideImmediate()
        {
            SetImmediate(0f);
        }

        /// <summary>
        /// Stops the current fade and keeps the current alpha.
        /// </summary>
        public void StopFade()
        {
            StopCurrentFade();
        }

        #endregion

        #region Fade

        private float GetDuration(float overrideDuration)
        {
            return overrideDuration >= 0f
                ? overrideDuration
                : duration;
        }

        private void StartFade(
            float from,
            float to,
            float fadeDuration,
            Action onComplete)
        {
            StopCurrentFade();

            if (fadeDuration <= 0f)
            {
                ApplyAlpha(to);
                onComplete?.Invoke();
                return;
            }

            _routine = StartCoroutine(
                FadeRoutine(from, to, fadeDuration, onComplete)
            );
        }

        private IEnumerator FadeRoutine(
            float from,
            float to,
            float fadeDuration,
            Action onComplete)
        {
            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += useUnscaledTime
                    ? Time.unscaledDeltaTime
                    : Time.deltaTime;

                float progress = Mathf.Clamp01(elapsed / fadeDuration);

                ApplyAlpha(Mathf.Lerp(from, to, progress));

                yield return null;
            }

            ApplyAlpha(to);

            _routine = null;

            onComplete?.Invoke();
        }

        private void StopCurrentFade()
        {
            if (_routine == null)
                return;

            StopCoroutine(_routine);
            _routine = null;
        }

        #endregion

        #region CanvasGroup

        private void ApplyAlpha(float alpha)
        {
            alpha = Mathf.Clamp01(alpha);

            canvasGroup.alpha = alpha;

            if (!disableInteractionWhileHidden)
                return;

            bool visible = alpha > 0.01f;

            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        #endregion
    }
}
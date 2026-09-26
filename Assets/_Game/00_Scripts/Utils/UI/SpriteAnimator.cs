using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Slafurry.Utils.UI
{
    /// <summary>
    /// Steps an Image through a sequence of sprites at a fixed frame rate.
    /// Generic frame-by-frame animation for UI: glitch bursts, loading dots,
    /// blinking cursors, sprite-swap promos, and anything else that just needs
    /// to show a different sprite over time.
    ///
    /// Null entries in the frame list are skipped rather than shown, so a
    /// sprite that lost its asset does not blank the Image mid-animation.
    /// </summary>
    public class SpriteAnimator : MonoBehaviour
    {
        [Header("Reference")]
        [SerializeField] private Image targetImage;

        [Header("Frames")]
        [SerializeField] private Sprite[] frames;

        [Header("Settings")]
        [Tooltip("Frames per second. 0 or less shows one frame per rendered frame.")]
        [SerializeField] private float fps = 12f;
        [Tooltip("Keep cycling after the last frame.")]
        [SerializeField] private bool loop;
        [Tooltip("Play backwards on the return trip, skipping the two end frames so they are not held twice. Ignored unless Loop is on.")]
        [SerializeField] private bool pingPong;

        [Header("Options")]
        [SerializeField] private bool playOnEnable = true;
        [Tooltip("Keeps animating while the game is paused or timeScale is 0.")]
        [SerializeField] private bool useUnscaledTime = true;
        [Tooltip("Put the Image's original sprite back when the animation ends or is stopped.")]
        [SerializeField] private bool restoreSpriteOnStop = true;

        [Header("Events")]
        [Tooltip("Fired after every pass through the frame list.")]
        [SerializeField] private UnityEvent onCycle;
        [Tooltip("Fired once when a non-looping animation reaches the end.")]
        [SerializeField] private UnityEvent onFinished;

        private Coroutine _routine;

        // Typed as object on purpose: WaitForSeconds derives from
        // YieldInstruction but WaitForSecondsRealtime derives from
        // CustomYieldInstruction, so the two have no common base type
        // narrower than System.Object.
        private object _frameWait;
        private float _cachedFps = -1f;
        private bool _cachedUnscaled;
        private Sprite _spriteBeforePlay;
        private bool _capturedOriginal;
        private bool _warnedNoFrames;
        private bool _warnedNullFrame;

        private void Awake()
        {
            // The Image is often a child of the object this is attached to, but
            // falling back to a same-object Image keeps the common case
            // drag-and-drop without any setup.
            if (targetImage == null)
                targetImage = GetComponent<Image>();
        }

        private void OnEnable()
        {
            if (playOnEnable)
                Play();
        }

        private void OnDisable()
        {
            // A disable is not a completion, so onFinished is deliberately left
            // alone here. Without this the coroutine would keep running after
            // the object is disabled, since Unity only auto-stops them on
            // GameObject deactivation, not on a component disable.
            Stop();
        }

        /// <summary>
        /// Restarts the sequence from the first frame.
        /// </summary>
        public void Play()
        {
            if (targetImage == null)
            {
                WarnNoFrames("no target Image is assigned");
                return;
            }

            if (HasPlayableFrame() == false)
            {
                WarnNoFrames("no frame in the list has a sprite");
                return;
            }

            // Only capture on a fresh start. Re-capturing mid-animation would
            // record whatever frame is currently on screen as the "original",
            // so restarting would leave the Image stuck on a glitch frame.
            bool wasRunning = _routine != null;
            StopInternal(false);

            if (wasRunning == false)
            {
                _spriteBeforePlay = targetImage.sprite;
                _capturedOriginal = true;
            }

            _routine = StartCoroutine(Run());
        }

        /// <summary>
        /// Halts the animation and restores the sprite the Image had before it
        /// started. Takes no arguments so it can be wired straight to a
        /// UnityEvent or button.
        /// </summary>
        public void Stop()
        {
            StopInternal(true);
        }

        private void StopInternal(bool restoreSprite)
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            if (restoreSprite)
                RestoreSprite();
        }

        private IEnumerator Run()
        {
            while (true)
            {
                for (int i = 0; i < frames.Length; i++)
                {
                    Show(i);
                    yield return FrameDelay();
                }

                if (loop && pingPong)
                {
                    // Skip frames.Length - 1 and 0: both were just shown, and
                    // holding them again is what reads as a stutter.
                    for (int i = frames.Length - 2; i > 0; i--)
                    {
                        Show(i);
                        yield return FrameDelay();
                    }
                }

                onCycle?.Invoke();

                if (loop == false)
                    break;
            }

            RestoreSprite();
            onFinished?.Invoke();
            _routine = null;
        }

        private void Show(int index)
        {
            Sprite sprite = frames[index];

            if (sprite == null)
            {
                if (_warnedNullFrame == false)
                {
                    _warnedNullFrame = true;
                    Debug.LogWarning($"[SpriteAnimator] Frame {index} on '{name}' has no sprite and will be skipped.", this);
                }

                return;
            }

            targetImage.sprite = sprite;
        }

        private void RestoreSprite()
        {
            if (restoreSpriteOnStop == false || _capturedOriginal == false || targetImage == null)
                return;

            // Assigned unconditionally: an Image that started out with no
            // sprite has to end up with no sprite again, so guarding on
            // _spriteBeforePlay != null would leave the last frame stuck.
            targetImage.sprite = _spriteBeforePlay;
        }

        private bool HasPlayableFrame()
        {
            if (frames == null || frames.Length == 0)
                return false;

            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i] != null)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns the per-frame wait, rebuilding it only when fps or the time
        /// mode actually changes. Allocating a WaitForSeconds every frame is
        /// the usual way a sprite animator turns into GC pressure.
        /// </summary>
        private object FrameDelay()
        {
            if (fps <= 0f)
                return null;

            if (_frameWait == null || _cachedFps != fps || _cachedUnscaled != useUnscaledTime)
            {
                float interval = 1f / fps;
                _frameWait = useUnscaledTime
                    ? (object)new WaitForSecondsRealtime(interval)
                    : new WaitForSeconds(interval);
                _cachedFps = fps;
                _cachedUnscaled = useUnscaledTime;
            }

            return _frameWait;
        }

        private void WarnNoFrames(string reason)
        {
            if (_warnedNoFrames)
                return;

            _warnedNoFrames = true;
            Debug.LogWarning($"[SpriteAnimator] '{name}' did not start: {reason}.", this);
        }
    }
}

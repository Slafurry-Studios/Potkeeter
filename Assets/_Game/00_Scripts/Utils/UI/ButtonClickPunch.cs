using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Slafurry.Utils.UI
{
    /// <summary>
    /// Click feedback for UI buttons: shrinks under the pointer/finger while
    /// held, then pops past rest scale on release. Attach to the same object as
    /// ButtonHover to combine hover-grow with press-squash.
    ///
    /// Note that both scripts animate localScale, so they read the rest scale
    /// in Awake. If you also animate this transform elsewhere (UIFloat, a
    /// LayoutGroup, a tween), point `target` at a child object instead so the
    /// two do not overwrite each other.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ButtonClickPunch : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, ISubmitHandler
    {
        [Header("Reference")]
        [SerializeField] private RectTransform target;
        [SerializeField] private Graphic graphic;

        [Header("Press")]
        [Tooltip("Scale while held down. Below 1 squashes the button.")]
        [SerializeField] private float pressScale = 0.92f;
        [SerializeField] private float pressDuration = 0.08f;

        [Header("Release")]
        [Tooltip("Peak scale on release. Above 1 gives the pop.")]
        [SerializeField] private float releaseScale = 1.12f;
        [SerializeField] private float releaseDuration = 0.25f;
        [SerializeField] private AnimationCurve pressCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private AnimationCurve releaseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("Scales the release overshoot. 1 = punch exactly to releaseScale, 0.6 = a smaller bump.")]
        [SerializeField] private float overshoot = 1f;

        [Header("Brightness")]
        [SerializeField] private bool brightenWhilePressed = true;
        [Tooltip("Multiplied into the resting colour while pressed. Below 1 darkens.")]
        [SerializeField] private float pressedBrightness = 0.9f;

        [Header("Options")]
        [SerializeField] private bool useUnscaledTime = true;
        [Tooltip("Release pops when the pointer is dragged off the button. Off = cancel back to rest.")]
        [SerializeField] private bool releaseOnDragOff = true;

        private Vector3 _restScale;
        private Color _restColor;
        private Coroutine _routine;

        private void Awake()
        {
            if (target == null) target = transform as RectTransform;
            if (graphic == null) graphic = GetComponent<Graphic>();

            _restScale = target.localScale;
            if (graphic != null) _restColor = graphic.color;
        }

        private void OnDisable()
        {
            // A button disabled mid-press (e.g. screen closed on click) would
            // otherwise stay squashed.
            Reset();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            PlayPress();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerPress != null && eventData.pointerPress != gameObject && !releaseOnDragOff)
                Reset();
            else
                PlayRelease();
        }

        // Keyboard/controller "submit" fires without pointer down/up.
        public void OnSubmit(BaseEventData eventData)
        {
            PlayRelease();
        }

        public void PlayPress()
        {
            StartTransition(_restScale * pressScale, pressDuration, pressCurve, BrightnessWhilePressed());
        }

        public void PlayRelease()
        {
            StartTransition(_restScale * releaseScale, releaseDuration, releaseCurve, 1f, thenRest: true);
        }

        private float BrightnessWhilePressed()
        {
            return brightenWhilePressed ? pressedBrightness : 1f;
        }

        public void Reset()
        {
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            target.localScale = _restScale;
            if (graphic != null) graphic.color = _restColor;
        }

        private void StartTransition(Vector3 to, float dur, AnimationCurve curve, float brightness, bool thenRest = false)
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(TransitionRoutine(to, dur, curve, brightness, thenRest));
        }

        private IEnumerator TransitionRoutine(Vector3 to, float dur, AnimationCurve curve, float brightness, bool thenRest)
        {
            Vector3 from = target.localScale;
            Color fromColor = graphic != null ? graphic.color : Color.white;
            Color toColor = _restColor * brightness;

            float t = 0f;
            while (t < dur)
            {
                t += DeltaTime();
                float k = curve.Evaluate(Mathf.Clamp01(dur > 0f ? t / dur : 1f));
                target.localScale = Vector3.LerpUnclamped(from, to, k);
                if (graphic != null) graphic.color = Color.LerpUnclamped(fromColor, toColor, k);
                yield return null;
            }

            target.localScale = to;
            _routine = null;

            if (!thenRest) yield break;

            // Settle from the peak back down to rest.
            Vector3 peak = target.localScale;
            float settleDur = releaseDuration * Mathf.Max(0.01f, overshoot);
            t = 0f;
            while (t < settleDur)
            {
                t += DeltaTime();
                float k = Mathf.Clamp01(dur > 0f ? t / settleDur : 1f);
                target.localScale = Vector3.LerpUnclamped(peak, _restScale, k);
                if (graphic != null) graphic.color = Color.LerpUnclamped(toColor, _restColor, k);
                yield return null;
            }

            target.localScale = _restScale;
            if (graphic != null) graphic.color = _restColor;
        }

        private float DeltaTime()
        {
            return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        }
    }
}

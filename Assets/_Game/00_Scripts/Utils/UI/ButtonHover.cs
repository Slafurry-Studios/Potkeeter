using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Slafurry.Utils.UI
{
    /// <summary>
    /// Hover feedback for UI buttons: scales up slightly and brightens while
    /// the pointer is over the element, and eases back on exit. Works on
    /// Button, Image, or any Graphic with raycastTarget enabled.
    /// Pair with UIScalePunch if you also want a click "pop".
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [Header("Reference")]
        [SerializeField] private RectTransform target;
        [SerializeField] private CanvasGroup canvasGroup;
        [Tooltip("Optional. Tints toward white on hover when assigned.")]
        [SerializeField] private Graphic graphic;

        [Header("Scale")]
        [SerializeField] private float hoverScale = 1.08f;
        [SerializeField] private float scaleDuration = 0.15f;

        [Header("Brightness")]
        [SerializeField] private bool brighten = true;
        [SerializeField] private float hoverBrightness = 1.15f;
        [SerializeField] private float brightnessDuration = 0.15f;

        [Header("Options")]
        [SerializeField] private bool useUnscaledTime = true;
        [Tooltip("Also fire when the button is focused by keyboard/controller navigation, not just hovered.")]
        [SerializeField] private bool respondToSelect = true;

        private Vector3 _restScale;
        private Color _restColor;
        private Coroutine _scaleRoutine;
        private Coroutine _brightnessRoutine;
        private bool _hovering;

        private void Awake()
        {
            if (target == null) target = transform as RectTransform;
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (graphic == null) graphic = GetComponent<Graphic>();

            _restScale = target.localScale;
            if (graphic != null) _restColor = graphic.color;
        }

        private void OnDisable()
        {
            // Snap back so a disabled-while-hovered button does not stay
            // stuck at hover scale.
            ResetVisuals();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            SetHover(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetHover(false);
        }

        // Keyboard and controller navigation highlight a button without any
        // pointer entering it, so mirror the same visual on select. These take
        // BaseEventData, not PointerEventData.
        public void OnSelect(BaseEventData eventData)
        {
            if (respondToSelect) SetHover(true);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            if (respondToSelect) SetHover(false);
        }

        private void SetHover(bool value)
        {
            if (_hovering == value) return;
            _hovering = value;
            StartScale(_hovering);
            StartBrightness(_hovering);
        }

        private void StartScale(bool toHover)
        {
            if (_scaleRoutine != null) StopCoroutine(_scaleRoutine);
            _scaleRoutine = StartCoroutine(ScaleRoutine(ScaleTarget(toHover), scaleDuration));
        }

        private Vector3 ScaleTarget(bool toHover)
        {
            return toHover ? _restScale * hoverScale : _restScale;
        }

        private IEnumerator ScaleRoutine(Vector3 to, float dur)
        {
            Vector3 from = target.localScale;
            yield return LerpScale(from, to, dur);
            target.localScale = to;
            _scaleRoutine = null;
        }

        private IEnumerator LerpScale(Vector3 from, Vector3 to, float dur)
        {
            float t = 0f;
            while (t < dur)
            {
                t += DeltaTime();
                target.localScale = Vector3.LerpUnclamped(from, to, Mathf.Clamp01(dur > 0f ? t / dur : 1f));
                yield return null;
            }
        }

        private void StartBrightness(bool toHover)
        {
            if (!brighten || graphic == null) return;
            if (_brightnessRoutine != null) StopCoroutine(_brightnessRoutine);
            _brightnessRoutine = StartCoroutine(BrightnessRoutine(toHover, brightnessDuration));
        }

        private IEnumerator BrightnessRoutine(bool toHover, float dur)
        {
            Color targetColor = toHover
                ? _restColor * hoverBrightness
                : _restColor;

            Color from = graphic.color;
            float t = 0f;
            while (t < dur)
            {
                t += DeltaTime();
                graphic.color = Color.LerpUnclamped(from, targetColor, Mathf.Clamp01(dur > 0f ? t / dur : 1f));
                yield return null;
            }
            graphic.color = targetColor;
            _brightnessRoutine = null;
        }

        private void ResetVisuals()
        {
            if (_scaleRoutine != null) { StopCoroutine(_scaleRoutine); _scaleRoutine = null; }
            if (_brightnessRoutine != null) { StopCoroutine(_brightnessRoutine); _brightnessRoutine = null; }

            target.localScale = _restScale;
            if (graphic != null) graphic.color = _restColor;
        }

        private float DeltaTime()
        {
            return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        }
    }
}

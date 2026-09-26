using UnityEngine;

namespace Slafurry.Utils.UI
{
    /// <summary>
    /// Drifts a UI element up and down in a slow sine wave, so it reads as
    /// a gentle "breathing" float. Attach to any UI object with a
    /// RectTransform. Siblings can be given a phaseOffset so they do not all
    /// bob in lockstep.
    /// </summary>
    public class UIFloat : MonoBehaviour
    {
        private const float Tau = Mathf.PI * 2f;

        [Header("Reference")]
        [SerializeField] private RectTransform rectTransform;

        [Header("Float")]
        [SerializeField] private float amplitude = 6f;
        [SerializeField] private float frequency = 0.25f;
        [Tooltip("0 = 1 cycle per second. 0.25 = one full breath every 4 seconds.")]
        [SerializeField] private float phaseOffset;

        [Header("Options")]
        [SerializeField] private bool useUnscaledTime = true;

        private Vector2 _origin;
        private float _time;

        private void Awake()
        {
            if (rectTransform == null)
                rectTransform = GetComponent<RectTransform>();
        }

        private void OnEnable()
        {
            // Capture the authored position so re-enabling does not accumulate
            // offset, and so layout changes made while disabled are respected.
            if (rectTransform != null)
                _origin = rectTransform.anchoredPosition;
            _time = phaseOffset;
        }

        private void Update()
        {
            if (rectTransform == null)
                return;

            _time += DeltaTime() * frequency;
            float offset = Mathf.Sin(_time * Tau) * amplitude;
            rectTransform.anchoredPosition = _origin + new Vector2(0f, offset);
        }

        private float DeltaTime()
        {
            return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        }
    }
}

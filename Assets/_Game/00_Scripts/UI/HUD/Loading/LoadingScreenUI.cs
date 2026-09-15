using System.Collections;
using Slafurry.System.Pause;
using Slafurry.System.Scene;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LoadingScreenUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text loadingText;
    [SerializeField] private Slider progressBar;

    [Header("Dot Animation")]
    [SerializeField] private float dotInterval = 0.4f;
    [SerializeField] private int maxDots = 3;

    [Header("Display Time")]
    [SerializeField] private float minDisplayTime = 2f;
    [SerializeField] private float maxDisplayTime = 4f;

    [Header("Fade")]
    [SerializeField] private float fadeInDuration = 0.5f;
    [SerializeField] private float fadeOutDuration = 0.5f;

    [Header("Excluded Scenes")]
    [SerializeField] private string[] excludedScenes;

    private Coroutine _dotCoroutine;
    private Coroutine _displayCoroutine;
    private bool _sceneLoadComplete;

    private void Awake()
    {
        DontDestroyOnLoad(gameObject);

        if (canvas != null)
            canvas.enabled = false;

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        if (progressBar != null)
            progressBar.value = 0f;
    }

    private void Start()
    {
        if (SceneLoader.Instance == null) return;

        SceneLoader.Instance.OnBeforeSceneLoad += HandleBeforeSceneLoad;
        SceneLoader.Instance.OnSceneLoadStarted += HandleSceneLoadStarted;
        SceneLoader.Instance.OnSceneLoadProgress += HandleSceneLoadProgress;
        SceneLoader.Instance.OnSceneLoadCompleted += HandleSceneLoadCompleted;
    }

    private void OnDestroy()
    {
        if (SceneLoader.Instance == null) return;

        SceneLoader.Instance.OnBeforeSceneLoad -= HandleBeforeSceneLoad;
        SceneLoader.Instance.OnSceneLoadStarted -= HandleSceneLoadStarted;
        SceneLoader.Instance.OnSceneLoadProgress -= HandleSceneLoadProgress;
        SceneLoader.Instance.OnSceneLoadCompleted -= HandleSceneLoadCompleted;
    }

    private void HandleBeforeSceneLoad(string sceneName, System.Action onReady)
    {
        if (IsExcluded(sceneName))
        {
            onReady();
            return;
        }

        StartCoroutine(PreLoadSequence(onReady));
    }

    private IEnumerator PreLoadSequence(System.Action onReady)
    {
        if (canvas != null)
            canvas.enabled = true;

        Pause.On("LoadingScreen");

        yield return StartCoroutine(Fade(0f, 1f, fadeInDuration));

        onReady();
    }

    private void HandleSceneLoadStarted(string sceneName)
    {
        if (IsExcluded(sceneName)) return;

        _sceneLoadComplete = false;

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        if (progressBar != null)
            progressBar.value = 0f;

        StartDotAnimation();
        StartDisplayTimer();
    }

    private void HandleSceneLoadProgress(float progress)
    {
        if (progressBar != null)
            progressBar.value = progress;
    }

    private void HandleSceneLoadCompleted(string sceneName)
    {
        _sceneLoadComplete = true;
    }

    private void StartDisplayTimer()
    {
        StopDisplayTimer();
        _displayCoroutine = StartCoroutine(DisplayTimerRoutine());
    }

    private void StopDisplayTimer()
    {
        if (_displayCoroutine != null)
        {
            StopCoroutine(_displayCoroutine);
            _displayCoroutine = null;
        }
    }

    private IEnumerator DisplayTimerRoutine()
    {
        float duration = Random.Range(minDisplayTime, maxDisplayTime);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        while (!_sceneLoadComplete)
            yield return null;

        if (progressBar != null)
            progressBar.value = 1f;

        StopDotAnimation();
        yield return StartCoroutine(Fade(1f, 0f, fadeOutDuration));

        if (canvas != null)
            canvas.enabled = false;

        Pause.Off("LoadingScreen");
    }

    private void StartDotAnimation()
    {
        StopDotAnimation();
        _dotCoroutine = StartCoroutine(AnimateDots());
    }

    private void StopDotAnimation()
    {
        if (_dotCoroutine != null)
        {
            StopCoroutine(_dotCoroutine);
            _dotCoroutine = null;
        }
    }

    private IEnumerator AnimateDots()
    {
        int dotCount = 1;

        while (true)
        {
            string dots = new string('.', dotCount);

            if (loadingText != null)
                loadingText.text = "Loading" + dots;

            dotCount = dotCount >= maxDots ? 1 : dotCount + 1;

            yield return new WaitForSecondsRealtime(dotInterval);
        }
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (canvasGroup == null) yield break;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        canvasGroup.alpha = to;
    }

    private bool IsExcluded(string sceneName)
    {
        if (excludedScenes == null) return false;

        for (int i = 0; i < excludedScenes.Length; i++)
        {
            if (string.Equals(excludedScenes[i], sceneName))
                return true;
        }

        return false;
    }
}
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class ObjectiveHUD : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text objectiveText;

    [Tooltip("Opsional. Kalau kosong, akan otomatis diambil dari ObjectiveManager.Instance saat OnEnable.")]
    [SerializeField] private ObjectiveManager objectiveManager;

    [Header("Display")]
    [SerializeField] private string header = "OBJECTIVES";
    [SerializeField] private string incompleteFormat = "• {0}  {1}/{2}";
    [SerializeField] private string completedFormat = "• {0}  ✓";

    [Header("Completed Objective")]
    [SerializeField] private float completedDisplayDuration = 2f;
    [SerializeField] private float fadeDuration = 0.5f;

    private Coroutine fadeRoutine;
    private Coroutine bindRoutine;

    private void OnEnable()
    {
        bindRoutine = StartCoroutine(BindAndSubscribe());
    }

    private void OnDisable()
    {
        if (bindRoutine != null)
        {
            StopCoroutine(bindRoutine);
            bindRoutine = null;
        }

        if (objectiveManager != null)
            objectiveManager.OnObjectivesChanged -= Refresh;

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }
    }

    private IEnumerator BindAndSubscribe()
    {
        // Kalau belum di-assign manual di Inspector, ambil dari singleton.
        // Tunggu sampai instance-nya siap (menghindari masalah urutan Awake).
        while (objectiveManager == null)
        {
            objectiveManager = ObjectiveManager.Instance;
            yield return null;
        }

        objectiveManager.OnObjectivesChanged += Refresh;
        Refresh();

        bindRoutine = null;
    }

    private void Refresh()
    {
        if (objectiveText == null || objectiveManager == null)
            return;

        BuildText(objectiveManager);

        if (HasCompletedObjective(objectiveManager))
        {
            if (fadeRoutine != null)
                StopCoroutine(fadeRoutine);

            fadeRoutine = StartCoroutine(FadeCompletedObjectiveRoutine());
        }
        else
        {
            SetAlpha(1f);
        }
    }

    private void BuildText(ObjectiveManager manager)
    {
        StringBuilder builder = new StringBuilder();

        if (!string.IsNullOrEmpty(header))
        {
            builder.AppendLine(header);
            builder.AppendLine();
        }

        foreach (Objective objective in manager.Objectives)
        {
            if (objective.IsCompleted)
            {
                builder.AppendLine(string.Format(completedFormat, objective.ObjectiveName));
            }
            else
            {
                builder.AppendLine(string.Format(
                    incompleteFormat,
                    objective.ObjectiveName,
                    objective.Progress,
                    objective.Threshold
                ));
            }
        }

        objectiveText.text = builder.ToString();
    }

    private bool HasCompletedObjective(ObjectiveManager manager)
    {
        foreach (Objective objective in manager.Objectives)
        {
            if (objective.IsCompleted)
                return true;
        }

        return false;
    }

    private IEnumerator FadeCompletedObjectiveRoutine()
    {
        yield return new WaitForSeconds(completedDisplayDuration);

        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
            SetAlpha(alpha);
            yield return null;
        }

        SetAlpha(0f);

        RemoveCompletedObjectives();

        SetAlpha(1f);

        fadeRoutine = null;
    }

    private void RemoveCompletedObjectives()
    {
        if (objectiveManager == null)
            return;

        List<string> completedObjectives = new List<string>();

        foreach (Objective objective in objectiveManager.Objectives)
        {
            if (objective.IsCompleted)
                completedObjectives.Add(objective.ObjectiveName);
        }

        foreach (string objectiveName in completedObjectives)
        {
            objectiveManager.RemoveObjective(objectiveName);
        }
    }

    private void SetAlpha(float alpha)
    {
        Color color = objectiveText.color;
        color.a = alpha;
        objectiveText.color = color;
    }
}
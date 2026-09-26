using UnityEngine;
using UnityEngine.Events;

public class SceneStartTrigger : BaseTrigger
{
    [Header("Scene Start Trigger Settings")]
    [SerializeField] private bool triggerOnStart = true;

    [Header("Events")]
    [SerializeField] private UnityEvent onTrigger;

    private void Start()
    {
        if (triggerOnStart && CanTrigger())
        {
            Trigger();
        }
    }

    public void Trigger()
    {
        if (!CanTrigger())
            return;

        AddTriggerCount();
        onTrigger?.Invoke();
    }
}
using System;

[Serializable]
public class Objective
{
    public string ObjectiveName { get; }
    public int Threshold { get; }
    public string SceneName { get; }

    public int Progress { get; private set; }
    public bool IsCompleted { get; private set; }

    private readonly ObjectiveData data;

    public Objective(ObjectiveData data, string sceneName)
    {
        this.data = data;

        ObjectiveName = data.objectiveName;
        Threshold = data.threshold;
        SceneName = sceneName;

        Progress = 0;
        IsCompleted = false;
    }

    public void AddProgress(int amount)
    {
        if (IsCompleted)
            return;

        Progress += amount;

        if (Progress >= Threshold)
        {
            Progress = Threshold;
            Complete();
        }
    }

    public void SetProgress(int value)
    {
        if (IsCompleted)
            return;

        Progress = Math.Clamp(value, 0, Threshold);

        if (Progress >= Threshold)
            Complete();
    }

    public void Complete()
    {
        if (IsCompleted)
            return;

        Progress = Threshold;
        IsCompleted = true;

        data.onCompleted?.Invoke();
    }

    public void Reset()
    {
        Progress = 0;
        IsCompleted = false;
    }
}
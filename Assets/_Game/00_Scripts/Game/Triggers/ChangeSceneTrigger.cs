using Slafurry.System.Scene;
using UnityEngine;

public class ChangeSceneTrigger: MonoBehaviour
{
    public void ChangeScene(string sceneName)
    {
        SceneSystem.Load(sceneName);
    }
}
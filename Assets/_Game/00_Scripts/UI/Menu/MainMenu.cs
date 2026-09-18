using Slafurry.System.Scene;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Runtime.InteropServices;


public class MainMenu : MonoBehaviour
{
    [Header("Scenes")]
    [SerializeField] private string _gameSceneName = "GameScene";
    [SerializeField] private string _settingsSceneName = "SettingsScene";
    [SerializeField] private string _aboutSceneName = "AboutScene";

    public void StartGame()
    {
        SceneSystem.Load(_gameSceneName);
    }

    public void ContinueGame()
    {
        // Load System
        SceneSystem.Load(_gameSceneName);
    }

    public void ShowAbout()
    {
        SceneManager.LoadScene(_aboutSceneName);
    }

    public void ShowSettings()
    {
        SceneManager.LoadScene(_settingsSceneName);
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void QuitToWebsite();
#endif

    public void QuitGame()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        QuitToWebsite();
#else
        Application.Quit();
#endif

    }
}
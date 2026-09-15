using UnityEngine;
using UnityEngine.SceneManagement;

public class AboutMenu : MonoBehaviour
{
    [Header("Scenes")]
    [SerializeField] private string _mainMenuSceneName = "MainMenu";

    public void BackToMainMenu()
    {
        // ObjectiveManager.Instance.ClearObjectives();
        SceneManager.LoadScene(_mainMenuSceneName);
    }
}
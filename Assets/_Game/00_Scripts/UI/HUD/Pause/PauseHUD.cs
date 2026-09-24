using Slafurry.System.Pause;
using UnityEngine;
using Slafurry.System.Scene;
using UnityEngine.SceneManagement;
using Slafurry.System.InputHub;

public class PauseHUD : MonoBehaviour
{
    [Header("Pause HUD Settings")]
    [SerializeField] private GameObject pauseMenu;

    private void Awake()
    {
        if (pauseMenu == null)
            Debug.LogWarning("Pause menu is not assigned in the inspector.");
    }

    public void Retry()
    {
        Pause.ForceResume();
        SceneSystem.Load(SceneManager.GetActiveScene().name);
    }

    private void OnEnable()
    {
        Controls.EnableInput();
        Controls.OnPauseMenuPressed += TogglePauseMenu;
    }

    private void TogglePauseMenu()
    {
        if (pauseMenu != null)
        {
            bool isActive = pauseMenu.activeSelf;
            pauseMenu.SetActive(!isActive);

            if (!isActive)
                Pause.On("Global");
            else
                Pause.Off("Global");
        }
    }
    private void OnDisable()
    {
        Controls.OnPauseMenuPressed -= TogglePauseMenu;
    }

    public void ShowPauseMenu()
    {
        if (pauseMenu != null)
            pauseMenu.SetActive(true);
        Pause.On("Global");

    }

    public void HidePauseMenu()
    {
        if (pauseMenu != null)
            pauseMenu.SetActive(false);
        Pause.Off("Global");
    }
}
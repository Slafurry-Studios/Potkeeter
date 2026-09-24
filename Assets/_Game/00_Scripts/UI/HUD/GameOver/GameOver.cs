using UnityEngine;
using Slafurry.System.Pause;
using Slafurry.System.Scene;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameOver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject gameOverUI;

    [Header("Settings")]
    [SerializeField] private float showDelay = 1f;

    [Header("Debug")]
    [SerializeField] private bool debug = true;

    void Start()
    {
        gameOverUI.SetActive(false);
    }

    void Update()
    {
        // if (debug && Input.GetKeyDown(KeyCode.R))
        // {
        //     StartCoroutine(ShowGameOverCoroutine());
        // }
    }

    public void Retry()
    {
        HideGameOver();
        SceneSystem.Load(SceneManager.GetActiveScene().name);
    }

    public void Quit()
    {
        HideGameOver();
        ObjectiveManager.Instance.ClearObjectives();
        SceneSystem.Load("MainMenu");
    }

    public void ShowGameOver()
    {
        StartCoroutine(ShowGameOverCoroutine());
    }

    private IEnumerator ShowGameOverCoroutine()
    {
        yield return new WaitForSecondsRealtime(showDelay);

        gameOverUI.SetActive(true);
        Pause.On();
    }

    public void HideGameOver()
    {
        gameOverUI.SetActive(false);
        Pause.Off();
    }
}
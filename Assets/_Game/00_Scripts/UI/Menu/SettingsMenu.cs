using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SettingsMenu : MonoBehaviour
{
    [Header("Scenes")]
    [SerializeField] private string _mainMenuSceneName = "MainMenu";

    [Header("References")]
    [SerializeField] private Slider _masterVolSlider;
    [SerializeField] private Slider _musicVolSlider;
    [SerializeField] private Slider _sfxVolSlider;

    private const string MasterKey = "MasterVolume";
    private const string MusicKey = "MusicVolume";
    private const string SFXKey = "SFXVolume";

    private void Start()
    {
        // Listen first, then assign. Assigning .value fires the listener that
        // was just added, so the saved value reaches both the slider and the
        // mixer. SetValueWithoutNotify only moves the slider, which left the
        // mixer on its own default until the player happened to drag something.
        _masterVolSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        _musicVolSlider.onValueChanged.AddListener(OnMusicVolumeChanged);
        _sfxVolSlider.onValueChanged.AddListener(OnSFXVolumeChanged);

        _masterVolSlider.value = PlayerPrefs.GetFloat(MasterKey, 1f);
        _musicVolSlider.value = PlayerPrefs.GetFloat(MusicKey, 1f);
        _sfxVolSlider.value = PlayerPrefs.GetFloat(SFXKey, 1f);
    }

    private void OnMasterVolumeChanged(float value)
    {
        AudioSystem.Instance.UpdateMasterVolume(value);
    }

    private void OnMusicVolumeChanged(float value)
    {
        AudioSystem.Instance.UpdateMusicVolume(value);
    }

    private void OnSFXVolumeChanged(float value)
    {
        AudioSystem.Instance.UpdateSFXVolume(value);
    }

    public void BackToMainMenu()
    {
        // ObjectiveManager.Instance.ClearObjectives();
        SceneManager.LoadScene(_mainMenuSceneName);
    }

    private void OnDestroy()
    {
        if (_masterVolSlider != null)
            _masterVolSlider.onValueChanged.RemoveListener(OnMasterVolumeChanged);

        if (_musicVolSlider != null)
            _musicVolSlider.onValueChanged.RemoveListener(OnMusicVolumeChanged);

        if (_sfxVolSlider != null)
            _sfxVolSlider.onValueChanged.RemoveListener(OnSFXVolumeChanged);
    }
}
using UnityEngine;

/// <summary>
/// Plays a track registered in AudioSystem.musicTracks. The track's intro
/// (if it has one) plays once, then the loop clip repeats until stopped or
/// replaced. Attach to a GameObject in a scene to set that scene's music.
/// </summary>
public class MusicPlayer : MonoBehaviour
{
    [SerializeField] private string trackName;
    [SerializeField] private bool playOnStart = true;
    [SerializeField] private bool stopOnDestroy = true;
    [SerializeField] private float fadeDuration = -1f;

    public string TrackName
    {
        get => trackName;
        set => trackName = value;
    }

    private void Start()
    {
        if (playOnStart)
            Play();
    }

    public void Play()
    {
        if (AudioSystem.Instance == null)
        {
            Debug.LogWarning("MusicPlayer: AudioSystem belum siap!");
            return;
        }

        AudioSystem.Instance.PlayMusic(trackName, fadeDuration);
    }

    public void Stop()
    {
        if (AudioSystem.Instance == null)
            return;

        AudioSystem.Instance.StopMusicWithFade(fadeDuration < 0f ? 1f : fadeDuration);
    }

    private void OnDestroy()
    {
        if (!stopOnDestroy || AudioSystem.Instance == null)
            return;

        AudioSystem.Instance.StopMusic(trackName);
    }
}

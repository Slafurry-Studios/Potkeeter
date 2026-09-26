using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Audio;
using Slafurry.Core.Abstract;

[Serializable]
public class Sound
{
    public string name;
    public AudioClip clip;

    [Range(0f, 1f)]
    public float volume = 0.7f;

    [Range(0.1f, 3f)]
    public float pitch = 1f;

    public bool loop;

    [HideInInspector]
    public AudioSource source;
}

/// <summary>
/// A music track with an optional intro. The intro plays once and then hands
/// off to the loop clip, which always repeats. Register these in
/// AudioSystem.musicTracks and call PlayMusic(name).
/// </summary>
[Serializable]
public class MusicTrack
{
    public string name;

    [Tooltip("Optional. Plays once, then hands off to Loop.")]
    public AudioClip intro;

    [Tooltip("Repeats forever until the track is stopped or replaced.")]
    public AudioClip loop;

    [Range(0f, 1f)]
    public float volume = 0.7f;

    [Range(0.1f, 3f)]
    public float pitch = 1f;

    [Min(0f)]
    [Tooltip("Seconds to crossfade when this track starts.")]
    public float crossfade = 1f;

    [HideInInspector]
    public AudioSource introSource;

    [HideInInspector]
    public AudioSource loopSource;
}

public class AudioSystem : GameSystem<AudioSystem>
{
    [Header("Audio Mixer Groups")]
    [SerializeField] private AudioMixerGroup masterGroup;
    [SerializeField] private AudioMixerGroup musicGroup;
    [SerializeField] private AudioMixerGroup sfxGroup;

    [Header("Tracks Lists")]
    [SerializeField] private Sound[] musicSounds;
    [SerializeField] private Sound[] sfxSounds;
    [SerializeField] private MusicTrack[] musicTracks;

    private Sound currentMusicTrack;
    private MusicTrack currentMusicTrackEntry;
    private Coroutine musicFadeCoroutine;

    public override int Priority => 0;

    public override IEnumerator Initialize()
    {
        InitializeTracks(musicSounds, musicGroup);
        InitializeTracks(sfxSounds, sfxGroup);
        InitializeMusicTracks();

        yield break;
    }

    public override void PostInitialize() { }

    private void InitializeTracks(Sound[] sounds, AudioMixerGroup group)
    {
        if (sounds == null)
            return;

        foreach (Sound sound in sounds)
        {
            if (sound.clip == null)
            {
                Debug.LogError($"Sound '{sound.name}' tidak memiliki AudioClip!");
                continue;
            }

            sound.source = CreateSource(sound.clip, sound.volume, sound.pitch, sound.loop, group);
        }
    }

    private void InitializeMusicTracks()
    {
        if (musicTracks == null)
            return;

        foreach (MusicTrack track in musicTracks)
        {
            if (track.loop == null && track.intro == null)
            {
                Debug.LogError($"MusicTrack '{track.name}' tidak memiliki AudioClip!");
                continue;
            }

            if (track.intro != null)
                track.introSource = CreateSource(track.intro, track.volume, track.pitch, false, musicGroup);

            // loop = true is the whole point of a MusicTrack: the loop clip
            // repeats until something stops or replaces it.
            if (track.loop != null)
                track.loopSource = CreateSource(track.loop, track.volume, track.pitch, true, musicGroup);
        }
    }

    private AudioSource CreateSource(AudioClip clip, float volume, float pitch, bool loop, AudioMixerGroup group)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();

        source.spatialBlend = 0f;
        source.clip = clip;
        source.volume = volume;
        source.pitch = pitch;
        source.loop = loop;
        source.playOnAwake = false;
        source.outputAudioMixerGroup = group;

        return source;
    }

    private float EaseInOut(float t)
    {
        return t * t * (3f - 2f * t);
    }

    public void PlayMusic(string name, float fadeDuration = 1.0f)
    {
        MusicTrack track = FindMusicTrack(name);

        if (track != null)
        {
            PlayMusicTrack(track, fadeDuration < 0f ? track.crossfade : fadeDuration);
            return;
        }

        Sound newTrack = FindSound(musicSounds, name);

        if (newTrack == null || newTrack.source == null)
            return;

        if (currentMusicTrack == newTrack && currentMusicTrack.source.isPlaying)
            return;

        if (musicFadeCoroutine != null)
            StopCoroutine(musicFadeCoroutine);

        musicFadeCoroutine = StartCoroutine(CrossfadeMusicRoutine(newTrack, fadeDuration));
    }

    private void PlayMusicTrack(MusicTrack track, float fadeDuration)
    {
        bool isCurrent = currentMusicTrackEntry == track;
        bool introPlaying = isCurrent && track.introSource != null && track.introSource.isPlaying;
        bool loopPlaying = isCurrent && track.loopSource != null && track.loopSource.isPlaying;

        // Already in the requested state. Re-triggering would restart the intro.
        if (isCurrent && (introPlaying || loopPlaying))
            return;

        if (musicFadeCoroutine != null)
            StopCoroutine(musicFadeCoroutine);

        musicFadeCoroutine = StartCoroutine(MusicTrackRoutine(track, fadeDuration));
    }

    private MusicTrack FindMusicTrack(string name)
    {
        if (musicTracks == null)
            return null;

        return Array.Find(musicTracks, item => item != null && item.name == name);
    }

    private IEnumerator MusicTrackRoutine(MusicTrack track, float duration)
    {
        MusicTrack previousEntry = currentMusicTrackEntry;
        Sound previousSound = currentMusicTrack;

        currentMusicTrackEntry = track;
        currentMusicTrack = null;

        AudioSource intro = track.introSource;
        AudioSource loop = track.loopSource;

        if (intro != null)
        {
            intro.Stop();
            intro.volume = 0f;
            intro.Play();
        }
        else if (loop != null)
        {
            loop.Stop();
            loop.volume = 0f;
        }

        AudioSource incoming = intro != null ? intro : loop;
        float timer = 0f;

        // Fade the outgoing music out while the incoming intro fades in, so
        // switching tracks crossfades instead of stacking two loops.
        while (timer < duration)
        {
            timer += Time.deltaTime;
            float progress = EaseInOut(Mathf.Clamp01(timer / duration));

            if (incoming != null)
                incoming.volume = Mathf.Lerp(0f, track.volume, progress);

            FadeOutOutgoing(previousEntry, previousSound, progress);

            yield return null;
        }

        StopOutgoing(previousEntry, previousSound);

        if (intro != null)
        {
            yield return new WaitUntil(() => !intro.isPlaying);
        }

        if (loop != null)
        {
            loop.Stop();
            loop.volume = 0f;
            loop.loop = true;
            loop.Play();

            yield return FadeSourceRoutine(loop, 0f, track.volume, duration);
        }

        musicFadeCoroutine = null;
    }

    private void FadeOutOutgoing(MusicTrack previousEntry, Sound previousSound, float progress)
    {
        if (previousEntry != null)
        {
            if (previousEntry.introSource != null)
                previousEntry.introSource.volume = Mathf.Lerp(previousEntry.volume, 0f, progress);

            if (previousEntry.loopSource != null)
                previousEntry.loopSource.volume = Mathf.Lerp(previousEntry.volume, 0f, progress);
        }

        if (previousSound != null && previousSound.source != null)
            previousSound.source.volume = Mathf.Lerp(previousSound.volume, 0f, progress);
    }

    private void StopOutgoing(MusicTrack previousEntry, Sound previousSound)
    {
        if (previousEntry != null)
        {
            if (previousEntry.introSource != null)
            {
                previousEntry.introSource.Stop();
                previousEntry.introSource.volume = previousEntry.volume;
            }

            if (previousEntry.loopSource != null)
            {
                previousEntry.loopSource.Stop();
                previousEntry.loopSource.volume = previousEntry.volume;
            }
        }

        if (previousSound != null && previousSound.source != null)
        {
            previousSound.source.Stop();
            previousSound.source.volume = previousSound.volume;
        }
    }

    private IEnumerator FadeSourceRoutine(AudioSource source, float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            source.volume = to;
            yield break;
        }

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / duration);
            source.volume = Mathf.Lerp(from, to, EaseInOut(progress));
            yield return null;
        }

        source.volume = to;
    }

    public void StopMusicWithFade(float fadeDuration = 1.0f)
    {
        if (currentMusicTrackEntry != null)
        {
            StopMusicTrackWithFade(currentMusicTrackEntry, fadeDuration);
            return;
        }

        if (currentMusicTrack == null || !currentMusicTrack.source.isPlaying)
            return;

        if (musicFadeCoroutine != null)
            StopCoroutine(musicFadeCoroutine);

        musicFadeCoroutine = StartCoroutine(FadeOutCurrentMusicRoutine(fadeDuration));
    }

    private void StopMusicTrackWithFade(MusicTrack track, float fadeDuration)
    {
        AudioSource intro = track.introSource;
        AudioSource loop = track.loopSource;

        bool introPlaying = intro != null && intro.isPlaying;
        bool loopPlaying = loop != null && loop.isPlaying;

        if (!introPlaying && !loopPlaying)
            return;

        if (musicFadeCoroutine != null)
            StopCoroutine(musicFadeCoroutine);

        musicFadeCoroutine = StartCoroutine(FadeOutMusicTrackRoutine(track, fadeDuration));
    }

    private IEnumerator FadeOutMusicTrackRoutine(MusicTrack track, float duration)
    {
        AudioSource intro = track.introSource;
        AudioSource loop = track.loopSource;

        float introStart = intro != null ? intro.volume : 0f;
        float loopStart = loop != null ? loop.volume : 0f;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float progress = EaseInOut(Mathf.Clamp01(timer / duration));

            if (intro != null)
                intro.volume = Mathf.Lerp(introStart, 0f, progress);

            if (loop != null)
                loop.volume = Mathf.Lerp(loopStart, 0f, progress);

            yield return null;
        }

        if (intro != null)
        {
            intro.Stop();
            intro.volume = track.volume;
        }

        if (loop != null)
        {
            loop.Stop();
            loop.volume = track.volume;
        }

        if (currentMusicTrackEntry == track)
            currentMusicTrackEntry = null;

        musicFadeCoroutine = null;
    }

    private IEnumerator CrossfadeMusicRoutine(Sound newTrack, float duration)
    {
        Sound oldTrack = currentMusicTrack;
        MusicTrack oldEntry = currentMusicTrackEntry;

        currentMusicTrack = newTrack;
        currentMusicTrackEntry = null;

        newTrack.source.volume = 0f;
        if (!newTrack.source.isPlaying)
            newTrack.source.Play();

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / duration);
            float easedProgress = EaseInOut(progress);

            // Fade out old track
            if (oldTrack != null && oldTrack.source != null)
            {
                oldTrack.source.volume = Mathf.Lerp(oldTrack.volume, 0f, easedProgress);
            }

            FadeOutOutgoing(oldEntry, null, easedProgress);

            // Fade in new track
            newTrack.source.volume = Mathf.Lerp(0f, newTrack.volume, easedProgress);

            yield return null;
        }

        // Finalize volumes and states
        if (oldTrack != null && oldTrack.source != null)
        {
            oldTrack.source.Stop();
            oldTrack.source.volume = oldTrack.volume;
        }

        StopOutgoing(oldEntry, null);

        newTrack.source.volume = newTrack.volume;
        musicFadeCoroutine = null;
    }

    private IEnumerator FadeOutCurrentMusicRoutine(float duration)
    {
        Sound oldTrack = currentMusicTrack;
        float startVolume = oldTrack.source.volume;
        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float progress = Mathf.Clamp01(timer / duration);
            float easedProgress = EaseInOut(progress);

            oldTrack.source.volume = Mathf.Lerp(startVolume, 0f, easedProgress);
            yield return null;
        }

        oldTrack.source.Stop();
        oldTrack.source.volume = oldTrack.volume;
        currentMusicTrack = null;
        musicFadeCoroutine = null;
    }

    public void PlaySFX(string name, bool waitForCompletion = false)
    {
        Sound sound = FindSound(sfxSounds, name);

        if (sound == null || sound.source == null)
            return;

        if (waitForCompletion && sound.source.isPlaying)
        {
            StartCoroutine(PlayAfterCompletion(sound));
            return;
        }

        sound.source.Stop();
        sound.source.Play();
    }

    public void StopMusic(string name)
    {
        MusicTrack track = FindMusicTrack(name);

        if (track != null)
        {
            if (track.introSource != null)
            {
                track.introSource.Stop();
                track.introSource.volume = track.volume;
            }

            if (track.loopSource != null)
            {
                track.loopSource.Stop();
                track.loopSource.volume = track.volume;
            }

            if (currentMusicTrackEntry == track)
                currentMusicTrackEntry = null;

            return;
        }

        Sound sound = FindSound(musicSounds, name);

        if (sound?.source != null && sound.source.isPlaying)
        {
            sound.source.Stop();
            if (currentMusicTrack == sound)
                currentMusicTrack = null;
        }
    }

    public void StopSFX(string name)
    {
        Sound sound = FindSound(sfxSounds, name);

        if (sound?.source != null && sound.source.isPlaying)
            sound.source.Stop();
    }

    public bool IsMusicPlaying(string name)
    {
        MusicTrack track = FindMusicTrack(name);

        if (track != null)
        {
            return (track.loopSource != null && track.loopSource.isPlaying)
                || (track.introSource != null && track.introSource.isPlaying);
        }

        Sound sound = FindSound(musicSounds, name);
        return sound?.source != null && sound.source.isPlaying;
    }

    public bool IsSFXPlaying(string name)
    {
        Sound sound = FindSound(sfxSounds, name);
        return sound?.source != null && sound.source.isPlaying;
    }

    private Sound FindSound(Sound[] sounds, string name)
    {
        Sound sound = Array.Find(sounds, item => item != null && item.name == name);

        if (sound == null)
        {
            Debug.LogError($"Sound '{name}' tidak ditemukan!");
            return null;
        }

        if (sound.source == null)
        {
            Debug.LogError($"AudioSource untuk '{name}' belum dibuat!");
            return null;
        }

        return sound;
    }

    private IEnumerator PlayAfterCompletion(Sound sound)
    {
        yield return new WaitUntil(() => !sound.source.isPlaying);
        sound.source.Play();
    }

    public void UpdateMasterVolume(float volume)
    {
        SetMixerVolume(masterGroup, "MasterVolume", volume, "MasterVolume");
    }

    public void UpdateMusicVolume(float volume)
    {
        SetMixerVolume(musicGroup, "MusicVolume", volume, "MusicVolume");
    }

    public void UpdateSFXVolume(float volume)
    {
        SetMixerVolume(sfxGroup, "SFXVolume", volume, "SFXVolume");
    }

    private void SetMixerVolume(AudioMixerGroup group, string parameter, float volume, string prefsKey)
    {
        PlayerPrefs.SetFloat(prefsKey, volume);

        if (group == null || group.audioMixer == null)
            return;

        // The parameter only exists in the mixer once it is exposed in the
        // inspector. SetFloat is a silent no-op on an unexposed name, so warn
        // once instead of letting the slider look broken.
        if (!group.audioMixer.SetFloat(parameter, LinearToDecibel(volume)))
            Debug.LogWarning($"Mixer parameter '{parameter}' belum di-expose di AudioMixer!");
    }

    private float LinearToDecibel(float volume)
    {
        return Mathf.Log10(Mathf.Max(0.0001f, volume)) * 20f;
    }
}
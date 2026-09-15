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

public class AudioSystem : GameSystem<AudioSystem>
{
    [Header("Audio Mixer Groups")]
    [SerializeField] private AudioMixerGroup masterGroup;
    [SerializeField] private AudioMixerGroup musicGroup;
    [SerializeField] private AudioMixerGroup sfxGroup;

    [Header("Tracks Lists")]
    [SerializeField] private Sound[] musicSounds;
    [SerializeField] private Sound[] sfxSounds;

    public override int Priority => 0;

    public override IEnumerator Initialize()
    {
        InitializeTracks(musicSounds, musicGroup);
        InitializeTracks(sfxSounds, sfxGroup);

        yield break;
    }

    public override void PostInitialize()
    {
    }

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

            AudioSource source = gameObject.AddComponent<AudioSource>();

            source.spatialBlend = 0f;
            source.clip = sound.clip;
            source.volume = sound.volume;
            source.pitch = sound.pitch;
            source.loop = sound.loop;
            source.playOnAwake = false;
            source.outputAudioMixerGroup = group;

            sound.source = source;
        }
    }

    public void PlayMusic(string name, bool waitForCompletion = false)
    {
        Sound sound = FindSound(musicSounds, name);

        if (sound == null || sound.source == null)
            return;

        if (waitForCompletion && sound.source.isPlaying)
        {
            StartCoroutine(PlayAfterCompletion(sound));
            return;
        }

        if (!sound.source.isPlaying)
            sound.source.Play();
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
        Sound sound = FindSound(musicSounds, name);

        if (sound?.source != null && sound.source.isPlaying)
            sound.source.Stop();
    }

    public void StopSFX(string name)
    {
        Sound sound = FindSound(sfxSounds, name);

        if (sound?.source != null && sound.source.isPlaying)
            sound.source.Stop();
    }

    public bool IsMusicPlaying(string name)
    {
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
        Sound sound = Array.Find(
            sounds,
            item => item != null && item.name == name
        );

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
        masterGroup.audioMixer.SetFloat("MasterVolume", Mathf.Log10(volume) * 20);
        PlayerPrefs.SetFloat("MasterVolume", volume);
    }

    public void UpdateMusicVolume(float volume)
    {
        musicGroup.audioMixer.SetFloat("MusicVolume", Mathf.Log10(volume) * 20);
        PlayerPrefs.SetFloat("MusicVolume", volume);
    }

    public void UpdateSFXVolume(float volume)
    {
        sfxGroup.audioMixer.SetFloat("SFXVolume", Mathf.Log10(volume) * 20);
        PlayerPrefs.SetFloat("SFXVolume", volume);
    }
}
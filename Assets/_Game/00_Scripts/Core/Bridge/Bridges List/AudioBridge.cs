using UnityEngine;
using Slafurry.System;

namespace Slafurry.Core.Bridge
{
    [AddComponentMenu("Slafurry/Bridges/Audio Bridge")]
    public class AudioBridge : MonoBehaviour, ISubBridge
    {
        public void PlayMusic(string trackName) => AudioSystem.Instance?.PlayMusic(trackName);
        public void StopMusicWithFade(float duration) => AudioSystem.Instance?.StopMusicWithFade(duration);
        public void PlaySFX(string soundName) => AudioSystem.Instance?.PlaySFX(soundName);
        public void StopSFX(string soundName) => AudioSystem.Instance?.StopSFX(soundName);
        public void StopMusic(string trackName) => AudioSystem.Instance?.StopMusic(trackName);
    }
}
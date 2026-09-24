using System;
using System.Collections;
using Slafurry.Core.Abstract;
using UnityEngine;

namespace Slafurry.System.Story
{
    public static class Story
    {
        public static bool GetFlag(string key, bool fallback = false)
            => StoryManager.Instance.GetFlag(key, fallback);

        public static void SetFlag(string key, bool value)
            => StoryManager.Instance.SetFlag(key, value);

        public static int GetInt(string key, int fallback = 0)
            => StoryManager.Instance.GetInt(key, fallback);

        public static void SetInt(string key, int value)
            => StoryManager.Instance.SetInt(key, value);

        public static float GetFloat(string key, float fallback = 0f)
            => StoryManager.Instance.GetFloat(key, fallback);

        public static void SetFloat(string key, float value)
            => StoryManager.Instance.SetFloat(key, value);

        public static string GetString(string key, string fallback = "")
            => StoryManager.Instance.GetString(key, fallback);

        public static void SetString(string key, string value)
            => StoryManager.Instance.SetString(key, value);

        public static void Delete(string key)
            => StoryManager.Instance.Delete(key);

        public static void DeleteAll()
            => StoryManager.Instance.DeleteAll();
    }

    public class StoryManager : GameSystem<StoryManager>
    {
        private const string Prefix = "story_";

        public event Action<string, bool> OnFlagChanged;
        public event Action<string, int> OnIntChanged;
        public event Action<string, float> OnFloatChanged;
        public event Action<string, string> OnStringChanged;

        public override IEnumerator Initialize() { yield return null; }
        public override void PostInitialize() { }

        protected override void OnSingletonAwake()
        {
            base.OnSingletonAwake();
        }

        // ─── Flag ────────────────────────────────────────────

        public bool GetFlag(string key, bool fallback = false)
        {
            return PlayerPrefs.GetInt(Prefix + key, fallback ? 1 : 0) == 1;
        }

        public void SetFlag(string key, bool value)
        {
            PlayerPrefs.SetInt(Prefix + key, value ? 1 : 0);
            PlayerPrefs.Save();
            OnFlagChanged?.Invoke(key, value);
        }

        // ─── Int ─────────────────────────────────────────────

        public int GetInt(string key, int fallback = 0)
        {
            return PlayerPrefs.GetInt(Prefix + key, fallback);
        }

        public void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt(Prefix + key, value);
            PlayerPrefs.Save();
            OnIntChanged?.Invoke(key, value);
        }

        // ─── Float ───────────────────────────────────────────

        public float GetFloat(string key, float fallback = 0f)
        {
            return PlayerPrefs.GetFloat(Prefix + key, fallback);
        }

        public void SetFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(Prefix + key, value);
            PlayerPrefs.Save();
            OnFloatChanged?.Invoke(key, value);
        }

        // ─── String ──────────────────────────────────────────

        public string GetString(string key, string fallback = "")
        {
            return PlayerPrefs.GetString(Prefix + key, fallback);
        }

        public void SetString(string key, string value)
        {
            PlayerPrefs.SetString(Prefix + key, value);
            PlayerPrefs.Save();
            OnStringChanged?.Invoke(key, value);
        }

        // ─── Delete ──────────────────────────────────────────

        public void Delete(string key)
        {
            PlayerPrefs.DeleteKey(Prefix + key);
            PlayerPrefs.Save();
        }

        public void DeleteAll()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }
    }
}

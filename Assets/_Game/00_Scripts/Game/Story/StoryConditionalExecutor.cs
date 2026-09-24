using System;
using UnityEngine;
using UnityEngine.Events;
using StoryAPI = Slafurry.System.Story.Story;

namespace Slafurry.Game.Story
{
    public enum StoryConditionType
    {
        FlagIsTrue,
        FlagIsFalse,
        IntEquals,
        IntGreaterThan,
        IntLessThan,
        FloatGreaterThan,
        FloatLessThan,
        StringEquals,
    }

    [Serializable]
    public struct StoryCondition
    {
        public StoryConditionType type;
        public string key;

        public bool boolValue;
        public int intValue;
        public float floatValue;
        public string stringValue;

        public bool Evaluate()
        {
            if (Slafurry.System.Story.StoryManager.Instance == null) return false;

            switch (type)
            {
                case StoryConditionType.FlagIsTrue:
                    return StoryAPI.GetFlag(key);

                case StoryConditionType.FlagIsFalse:
                    return !StoryAPI.GetFlag(key);

                case StoryConditionType.IntEquals:
                    return StoryAPI.GetInt(key) == intValue;

                case StoryConditionType.IntGreaterThan:
                    return StoryAPI.GetInt(key) > intValue;

                case StoryConditionType.IntLessThan:
                    return StoryAPI.GetInt(key) < intValue;

                case StoryConditionType.FloatGreaterThan:
                    return StoryAPI.GetFloat(key) > floatValue;

                case StoryConditionType.FloatLessThan:
                    return StoryAPI.GetFloat(key) < floatValue;

                case StoryConditionType.StringEquals:
                    return StoryAPI.GetString(key) == stringValue;

                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// Evaluates story conditions and fires events. All conditions must be true (AND).
    /// </summary>
    public class StoryConditionalExecutor : MonoBehaviour
    {
        [Header("Conditions (AND)")]
        [SerializeField] private StoryCondition[] conditions;

        [Header("Options")]
        [SerializeField] private bool evaluateOnStart = true;
        [SerializeField] private bool listenToStoryChanges = true;

        [Header("Events")]
        [SerializeField] private UnityEvent onConditionsMet;
        [SerializeField] private UnityEvent onConditionsNotMet;

        private void OnEnable()
        {
            if (evaluateOnStart)
                Evaluate();

            if (listenToStoryChanges)
            {
                if (Slafurry.System.Story.StoryManager.Instance != null)
                {
                    Slafurry.System.Story.StoryManager.Instance.OnFlagChanged += OnStoryChanged;
                    Slafurry.System.Story.StoryManager.Instance.OnIntChanged += OnStoryChanged;
                    Slafurry.System.Story.StoryManager.Instance.OnFloatChanged += OnStoryChanged;
                    Slafurry.System.Story.StoryManager.Instance.OnStringChanged += OnStoryChanged;
                }
            }
        }

        private void OnDisable()
        {
            if (Slafurry.System.Story.StoryManager.Instance != null)
            {
                Slafurry.System.Story.StoryManager.Instance.OnFlagChanged -= OnStoryChanged;
                Slafurry.System.Story.StoryManager.Instance.OnIntChanged -= OnStoryChanged;
                Slafurry.System.Story.StoryManager.Instance.OnFloatChanged -= OnStoryChanged;
                Slafurry.System.Story.StoryManager.Instance.OnStringChanged -= OnStoryChanged;
            }
        }

        public void Evaluate()
        {
            if (conditions == null || conditions.Length == 0)
            {
                onConditionsMet?.Invoke();
                return;
            }

            bool allMet = true;

            for (int i = 0; i < conditions.Length; i++)
            {
                if (!conditions[i].Evaluate())
                {
                    allMet = false;
                    break;
                }
            }

            if (allMet)
                onConditionsMet?.Invoke();
            else
                onConditionsNotMet?.Invoke();
        }

        private void OnStoryChanged(string key, bool value) => Evaluate();
        private void OnStoryChanged(string key, int value) => Evaluate();
        private void OnStoryChanged(string key, float value) => Evaluate();
        private void OnStoryChanged(string key, string value) => Evaluate();
    }
}

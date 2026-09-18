using UnityEngine;
using TMPro;
using Slafurry.System.Localization;

namespace Slafurry.UI.Generic
{
    [RequireComponent(typeof(TMP_Text))]
    public class LocalizedText : MonoBehaviour
    {
        [SerializeField] private string key;
        private TMP_Text _label;

        void Awake()
        {
            _label = GetComponent<TMP_Text>();
        }

        void OnEnable()
        {
            LocalizationSystem.Instance.OnLanguageChanged += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            if (LocalizationSystem.Instance != null)
                LocalizationSystem.Instance.OnLanguageChanged -= Refresh;
        }

        void Refresh() => _label.text = Localize.Text(key);
    }
}
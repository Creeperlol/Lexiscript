using TMPro;
using UnityEngine;

namespace Lexiscript
{
    /// <summary>地板上的說明文字：執行時依語言設定換成中文，字型一起換。編輯模式下維持英文。</summary>
    [RequireComponent(typeof(TMP_Text))]
    public sealed class LocalizedText : MonoBehaviour
    {
        /// <summary>建場景時寫入的英文原文，也是翻譯表的鍵。</summary>
        public string english = "";

        TMP_Text text;
        TMP_FontAsset englishFont;

        void Awake()
        {
            text = GetComponent<TMP_Text>();
            englishFont = text.font;
            if (string.IsNullOrEmpty(english)) english = text.text;
        }

        void OnEnable()
        {
            GameSettings.Changed += Apply;
            Apply();
        }

        void OnDisable() => GameSettings.Changed -= Apply;

        void Apply() => Localization.Apply(text, english, englishFont);
    }
}

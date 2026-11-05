using TMPro;
using UnityEngine;

namespace Lexiscript
{
    /// <summary>
    /// 答案欄打出提示答案時，在畫面上方（顏色名稱底下）顯示提示句。
    /// 方塊之間只隔半格，畫在盤面上會蓋住上一格的指向符號與連接橋，所以放在介面上。
    /// </summary>
    public sealed class HintLabel : MonoBehaviour
    {
        const float FadeSpeed = 8f, PaddingX = 28, Height = 52, Top = ColorNameLabel.Top - ColorNameLabel.Height - 12;

        CanvasGroup group;
        TextMeshProUGUI label;
        RectTransform rect;
        Board board;

        /// <summary>目前顯示的提示句；沒有時是 null。</summary>
        public string Phrase { get; private set; }

        void Awake()
        {
            rect = (RectTransform)transform;
            Ui.Place(rect, new Vector2(0.5f, 1), new Vector2(0, Top), new Vector2(200, Height));
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0;
            group.blocksRaycasts = false;
            Ui.Fill(Ui.Image("Background", transform, Art.LabelBackground).rectTransform);
            label = Ui.Text("Label", transform, 28, Color.white);
            label.alignment = TextAlignmentOptions.Center;
            Ui.Fill(label.rectTransform);
        }

        void Update()
        {
            if (board == null) board = FindAnyObjectByType<Board>();
            Phrase = board != null && board.HintPhrase.Length > 0 ? board.HintPhrase : null;
            if (Phrase != null && label.text != Phrase)
            {
                label.text = Phrase;
                rect.sizeDelta = new Vector2(label.GetPreferredValues(Phrase).x + 2 * PaddingX, Height);
            }
            group.alpha = Mathf.MoveTowards(group.alpha, Phrase != null ? 1 : 0, FadeSpeed * Time.unscaledDeltaTime);
        }
    }
}

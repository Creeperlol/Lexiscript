using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lexiscript
{
    /// <summary>
    /// 左上角的總答題數：深色斜切標籤、小字 SOLVED、大數字與淺金強調條。
    /// 玩家解開題目時舊數字往上滑走、新數字從下方彈進來，強調條閃一下；
    /// 場景載入、讀檔還原這類不是當場解開的變化直接顯示新數字。設定裡關掉時淡出。
    /// </summary>
    public sealed class SolvedCounter : MonoBehaviour
    {
        const float Margin = 24, Width = 224, Height = 94, Chamfer = 22, Inset = 20, StripWidth = 5;
        const float FadeInSeconds = 0.3f, FadeOutSeconds = 0.22f, HiddenShift = -32;
        const float RollDistance = 50, OutSeconds = 0.24f, InDelay = 0.05f, InSeconds = 0.42f;
        const float FlashSeconds = 0.6f, SweepSeconds = 0.3f;
        const int MinDigits = 3;
        // 補位的 0 只是排版用，調淡才不會跟真正的數字搶視線
        const string PaddingColor = "#FCFCFA38";

        RectTransform rect, strip, sweep;
        UiShape stripShape, sweepShape;
        CanvasGroup group, currentGroup, previousGroup;
        TextMeshProUGUI current, previous;
        Board board;
        bool solvedByPlayer;
        float shown, changedAt = float.NegativeInfinity;

        /// <summary>目前顯示的題數（換數字的動畫中是要換成的那個）。</summary>
        public int Count { get; private set; }
        public bool Animating => Time.unscaledTime - changedAt < Mathf.Max(InDelay + InSeconds, FlashSeconds);
        /// <summary>整個計數器的不透明度，設定關掉時淡到 0。</summary>
        public float Alpha => group.alpha;
        public TMP_Text Number => current;

        void Awake()
        {
            rect = (RectTransform)transform;
            group = gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            Build();
            current.text = Format(0);
            shown = GameSettings.ShowSolvedCounter ? 1 : 0;
            Animate();
        }

        void OnDestroy()
        {
            if (board != null) board.Solved -= OnSolved;
        }

        void OnSolved(Block block) => solvedByPlayer = true;

        void Update()
        {
            if (board == null)
            {
                board = FindAnyObjectByType<Board>();
                if (board != null) board.Solved += OnSolved;
            }
            int count = board != null && board.Puzzle != null ? board.Puzzle.SolvedCount : 0;
            // 讀檔還原不會觸發 Board.Solved，那種變化直接換數字
            if (count != Count) Show(count, solvedByPlayer && count > Count);
            solvedByPlayer = false;
            Animate();
        }

        void Show(int count, bool animate)
        {
            previous.text = current.text;
            current.text = Format(count);
            Count = count;
            changedAt = animate ? Time.unscaledTime : float.NegativeInfinity;
        }

        static string Format(int count)
        {
            var digits = count.ToString();
            int padding = MinDigits - digits.Length;
            return padding > 0 ? $"<color={PaddingColor}>{new string('0', padding)}</color>{digits}" : digits;
        }

        void Animate()
        {
            bool visible = GameSettings.ShowSolvedCounter;
            shown = Mathf.MoveTowards(shown, visible ? 1 : 0, Time.unscaledDeltaTime / (visible ? FadeInSeconds : FadeOutSeconds));
            float eased = Ease.OutCubic(shown);
            group.alpha = eased;
            rect.anchoredPosition = new Vector2(Margin + HiddenShift * (1 - eased), -Margin);

            float t = Time.unscaledTime - changedAt;
            previous.rectTransform.anchoredPosition = new Vector2(0, RollDistance * Ease.OutCubic(t / OutSeconds));
            previousGroup.alpha = 1 - Ease.OutCubic(t / OutSeconds);
            float entering = (t - InDelay) / InSeconds;
            current.rectTransform.anchoredPosition = new Vector2(0, -RollDistance * (1 - Ease.OutBack(entering)));
            currentGroup.alpha = Ease.OutCubic(entering * 3);

            float flash = Ease.Pulse(t / FlashSeconds);
            strip.localScale = new Vector3(1 + 1.4f * flash, 1, 1);
            stripShape.color = Color.Lerp(Art.Gold, Art.PaleGold, flash);
            sweep.localScale = new Vector3(Ease.OutCubic(t / SweepSeconds), 1, 1);
            sweepShape.color = HudStyle.WithAlpha(Art.Gold, 1 - Ease.InCubic((t - SweepSeconds) / (FlashSeconds - SweepSeconds)));
        }

        void Build()
        {
            Ui.Place(rect, new Vector2(0, 1), new Vector2(Margin, -Margin), new Vector2(Width, Height));

            var body = Ui.Shape("Body", transform);
            Ui.Place(body.rectTransform, new Vector2(0, 1), Vector2.zero, new Vector2(Width, Height));
            var outline = HudStyle.Chamfered(new Rect(0, -Height, Width, Height), 0, 0, Chamfer, 0);
            body.Add(outline, HudStyle.WithAlpha(Art.Ink, 0.88f));
            // 三位數只佔數字欄左半，右下角的空位墊一點斜紋
            var corner = Shapes.Clip(Shapes.Clip(outline, Vector2.left, 76 - Width), Vector2.up, -46);
            body.AddAll(HudStyle.Hatch(corner, 7, 2.5f), HudStyle.WithAlpha(Art.Paper, 0.07f));
            body.Add(Shapes.Rect(new Vector2(Inset, -35), new Vector2(Width - 18, -34)), HudStyle.WithAlpha(Art.Paper, 0.18f));
            body.Add(Shapes.Rect(new Vector2(Inset, -36), new Vector2(Inset + 22, -33)), Art.Gold);
            body.AddAll(HudStyle.Barcode(new Vector2(Width - 52, -24), 11, "2131121312", 2), HudStyle.WithAlpha(Art.Paper, 0.4f));

            stripShape = Ui.Shape("Strip", transform);
            strip = stripShape.rectTransform;
            Ui.Place(strip, new Vector2(0, 1), Vector2.zero, new Vector2(StripWidth, Height));
            stripShape.Add(Shapes.Rect(new Vector2(0, -Height), new Vector2(StripWidth, 0)), Color.white);

            var label = Ui.Text("Label", transform, 15, HudStyle.WithAlpha(Art.Paper, 0.72f));
            label.font = Fonts.Display;
            label.characterSpacing = 28;
            label.alignment = TextAlignmentOptions.Left;
            label.text = "SOLVED";
            Ui.Place(label.rectTransform, new Vector2(0, 1), new Vector2(Inset, -9), new Vector2(140, 22));

            var digits = Ui.Rect("Digits", transform);
            Ui.Place(digits, new Vector2(0, 1), new Vector2(Inset - 2, -38), new Vector2(Width - Inset - 24, 52));
            // 換數字時舊的往上、新的從下面進來，超出數字欄的部分裁掉，看起來像翻牌
            digits.gameObject.AddComponent<RectMask2D>();
            previous = Digits("Previous", digits, out previousGroup);
            current = Digits("Current", digits, out currentGroup);

            sweepShape = Ui.Shape("Sweep", transform);
            sweep = sweepShape.rectTransform;
            Ui.Place(sweep, new Vector2(0, 1), new Vector2(Inset, -88), new Vector2(Width - Inset - 40, 2));
            sweepShape.Add(Shapes.Rect(new Vector2(0, -2), new Vector2(Width - Inset - 40, 0)), Color.white);
        }

        static TextMeshProUGUI Digits(string name, Transform parent, out CanvasGroup fade)
        {
            var text = Ui.Text(name, parent, 56, Art.Paper);
            text.font = Fonts.Display;
            text.characterSpacing = 2;
            text.richText = true;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            Ui.Fill(text.rectTransform);
            fade = text.gameObject.AddComponent<CanvasGroup>();
            fade.blocksRaycasts = false;
            return text;
        }
    }
}

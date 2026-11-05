using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Lexiscript
{
    /// <summary>
    /// 左下角的聊天列，參考 Minecraft：沒在打字時按 / 打開輸入列，Enter 送出、Esc 取消。
    /// 打對指令才會執行，其他內容直接顯示在聊天欄。關著時每則訊息顯示一段時間後淡出；
    /// 打開時顯示較多歷史訊息（滾輪可往回捲），整體高度不超過畫面的三分之一。
    /// </summary>
    public sealed class ChatConsole : MonoBehaviour, ITextTarget
    {
        sealed class Message
        {
            public string Markup;
            public string Plain;
            public float Time;
            public float Height;
        }

        sealed class Line
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public TextMeshProUGUI Text;
        }

        public const float VisibleSeconds = 10f, FadeSeconds = 1.5f;
        public const int MaxClosedLines = 10;
        const int MaxMessages = 100, MaxInput = 256;
        const float Width = 640, InputHeight = 40, Margin = 16, Gap = 4, FontSize = 22, PadX = 8, PadY = 3;
        const float CaretWidth = 2, BlinkHalfPeriod = 0.53f, DoubleClickTime = 0.35f;
        static readonly Color LineColor = new Color(0, 0, 0, 0.45f), InputColor = new Color(0, 0, 0, 0.6f);
        const string FeedbackColor = "#FFE066", ErrorColor = "#FF8A80";

        static readonly Dictionary<string, Action<ChatConsole>> Commands = new Dictionary<string, Action<ChatConsole>>
        {
            { "/showanswer", console => console.ShowAnswer() },
        };

        readonly TextEditBuffer input = new TextEditBuffer(TextEditBuffer.Printable, MaxInput);
        readonly List<Message> messages = new List<Message>();
        readonly List<string> sent = new List<string>();
        readonly List<Line> lines = new List<Line>();
        RectTransform rect, log, inputBar, inputViewport, inputContent, caret;
        Image caretImage;
        TextMeshProUGUI inputText, measure;
        string composition = "";
        string draft = "";
        int recall = -1, scrolledLines, renderedVersion = -1;
        float blinkStart, inputScroll, lastClickTime = float.NegativeInfinity, logSteps;
        bool dragging, inputDirty = true;
        Vector2 imePosition;

        public bool IsOpen => ReferenceEquals(TextFocus.Current, this);
        public TextEditBuffer Input => input;
        public bool WantsIme => true;
        /// <summary>所有訊息的純文字（不含顏色標籤），最新的在最後。</summary>
        public IEnumerable<string> Messages => messages.Select(message => message.Plain);
        /// <summary>目前畫面上看得到的訊息行數（含淡出中的）。</summary>
        public int VisibleLineCount => lines.Count(line => line.Rect.gameObject.activeSelf && line.Group.alpha > 0);
        /// <summary>聊天區（含輸入列）目前占用的高度，給測試確認不超過畫面三分之一。</summary>
        public float UsedHeight { get; private set; }

        void Awake()
        {
            rect = (RectTransform)transform;
            Ui.Place(rect, Vector2.zero, new Vector2(Margin, Margin), new Vector2(Width, InputHeight));

            log = Ui.Rect("Log", transform);
            log.anchorMin = log.anchorMax = log.pivot = Vector2.zero;
            log.anchoredPosition = new Vector2(0, InputHeight + Gap);

            inputBar = Ui.Image("Input", transform, InputColor).rectTransform;
            Ui.Place(inputBar, Vector2.zero, Vector2.zero, new Vector2(Width, InputHeight));
            inputViewport = Ui.Rect("Viewport", inputBar);
            Ui.Fill(inputViewport, PadX, PadX, 0, 0);
            inputViewport.gameObject.AddComponent<RectMask2D>();
            inputText = Ui.Text("Text", inputViewport, FontSize, Color.white);
            inputText.richText = true;
            inputText.alignment = TextAlignmentOptions.MidlineLeft;
            inputContent = inputText.rectTransform;
            inputContent.anchorMin = Vector2.zero;
            inputContent.anchorMax = new Vector2(0, 1);
            inputContent.pivot = new Vector2(0, 0.5f);
            inputContent.sizeDelta = new Vector2(Width * 4, 0);
            caretImage = Ui.Image("Caret", inputContent, Color.white);
            caret = caretImage.rectTransform;
            caret.anchorMin = caret.anchorMax = new Vector2(0, 0.5f);
            caret.pivot = new Vector2(0, 0.5f);

            // 量訊息高度用；保持啟用才量得到，但文字是透明的，量完也會清空
            measure = Ui.Text("Measure", transform, FontSize, Color.clear);
            measure.textWrappingMode = TextWrappingModes.Normal;
            measure.richText = true;
            inputBar.gameObject.SetActive(false);
        }

        void OnEnable() => TextFocus.Unfocused += OnUnfocusedText;

        void OnDisable() => TextFocus.Unfocused -= OnUnfocusedText;

        void OnDestroy() => TextFocus.Release(this);

        void OnUnfocusedText(char c)
        {
            if (c == '/') Open("/");
        }

        public void Open(string text = "")
        {
            TextFocus.Set(this);
            input.Reset(text);
            recall = -1;
            blinkStart = Time.unscaledTime;
            inputDirty = true;
        }

        /// <summary>在聊天欄顯示一則訊息；text 是純文字，不會被當成 rich text 標籤。</summary>
        public void Post(string text, string color = null)
        {
            var markup = Ui.Escape(text);
            if (color != null) markup = $"<color={color}>{markup}</color>";
            var message = new Message
            {
                Markup = markup,
                Plain = text,
                Time = Time.unscaledTime,
                Height = measure.GetPreferredValues(markup, Width - 2 * PadX, 0).y + 2 * PadY,
            };
            messages.Add(message);
            if (messages.Count > MaxMessages) messages.RemoveAt(0);
            scrolledLines = 0;
        }

        public bool Covers(Vector2 screen) => IsOpen && (Ui.Contains(inputBar, screen) || Ui.Contains(log, screen));

        public bool WantsScroll(Vector2 screen) => IsOpen && Ui.Contains(log, screen);

        public void OnText(char c)
        {
            input.Type(c);
            blinkStart = Time.unscaledTime;
        }

        public void OnComposition(string value)
        {
            // 每次用輸入法送出的字各自是一步復原
            if (composition.Length == 0 && value.Length > 0) input.BreakTypingGroup();
            composition = value;
            inputDirty = true;
        }

        public void OnKey(Key key, bool shift, bool ctrl)
        {
            blinkStart = Time.unscaledTime;
            switch (key)
            {
                case Key.Enter:
                case Key.NumpadEnter: Submit(); break;
                case Key.Escape: TextFocus.Release(this); break;
                case Key.UpArrow: Recall(-1); break;
                case Key.DownArrow: Recall(1); break;
                case Key.PageUp: Scroll(5); break;
                case Key.PageDown: Scroll(-5); break;
                default: TextEditKeys.Apply(input, key, shift, ctrl); break;
            }
        }

        public void OnFocusChanged(bool focused)
        {
            composition = "";
            scrolledLines = 0;
            logSteps = 0;
            dragging = false;
            if (!focused) input.Reset("");
            inputDirty = true;
            // 筆記可能把選字窗移走了，即使游標位置沒變也要重送
            if (focused) imePosition = new Vector2(float.NaN, float.NaN);
        }

        void Submit()
        {
            var text = input.Text.Trim();
            TextFocus.Release(this);
            if (text.Length == 0) return;
            if (sent.Count == 0 || sent[sent.Count - 1] != text) sent.Add(text);
            if (Commands.TryGetValue(text, out var command)) command(this);
            else Post(text);
        }

        /// <summary>上下鍵叫出之前送出過的內容，跟 Minecraft 一樣；回到最底時還原正在打的草稿。</summary>
        void Recall(int step)
        {
            if (sent.Count == 0) return;
            if (recall < 0)
            {
                if (step > 0) return;
                draft = input.Text;
                recall = sent.Count;
            }
            recall = Mathf.Clamp(recall + step, 0, sent.Count);
            input.Reset(recall == sent.Count ? draft : sent[recall]);
            if (recall == sent.Count) recall = -1;
        }

        void Scroll(int step) => scrolledLines = Mathf.Clamp(scrolledLines + step, 0, Mathf.Max(0, messages.Count - 1));

        void ShowAnswer()
        {
            var board = FindAnyObjectByType<Board>();
            var player = FindAnyObjectByType<Player>();
            Block nearest = null;
            if (board != null && board.Blocks != null && player != null)
            {
                Vector2 from = player.transform.position;
                nearest = board.Blocks
                    // 還沒開門的房間裡的方塊看不到，不能透過指令先拿到答案
                    .Where(block => !string.IsNullOrEmpty(block.answer) && !RoomCover.Hides(block.transform.position))
                    .OrderBy(block => ((Vector2)block.transform.position - from).sqrMagnitude)
                    .FirstOrDefault();
            }
            if (nearest == null)
            {
                Post("There is no block with an answer.", ErrorColor);
                return;
            }
            var clue = string.IsNullOrEmpty(nearest.clue) ? "" : $" ({nearest.clue})";
            Post($"Answer: {nearest.answer}{clue}", FeedbackColor);
        }

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 screen = mouse.position.ReadValue();
            if (IsOpen)
            {
                float notches = Mathf.Clamp(mouse.scroll.ReadValue().y, -3, 3);
                if (notches != 0 && WantsScroll(screen))
                {
                    // 觸控板一次手勢會在好幾幀各送零點幾格，累積滿一格才捲一則
                    logSteps += notches;
                    int steps = (int)logSteps;
                    logSteps -= steps;
                    if (steps != 0) Scroll(steps);
                }
            }
            if (dragging)
            {
                if (IsOpen && mouse.leftButton.isPressed) input.MoveTo(IndexAt(screen), true);
                else dragging = false;
            }
            if (!IsOpen || !mouse.leftButton.wasPressedThisFrame) return;
            if (!Covers(screen))
            {
                TextFocus.Release(this);
                return;
            }
            if (!Ui.Contains(inputBar, screen)) return;
            float now = Time.unscaledTime;
            if (now - lastClickTime <= DoubleClickTime)
            {
                input.SelectWordAt(CharacterAt(screen));
                lastClickTime = float.NegativeInfinity;
            }
            else
            {
                var keyboard = Keyboard.current;
                input.MoveTo(IndexAt(screen), keyboard != null && keyboard.shiftKey.isPressed);
                dragging = true;
                lastClickTime = now;
            }
            blinkStart = now;
        }

        void LateUpdate()
        {
            LayoutLog();
            inputBar.gameObject.SetActive(IsOpen);
            if (!IsOpen) return;
            RenderInput();
            caretImage.enabled = (Time.unscaledTime - blinkStart) % (2 * BlinkHalfPeriod) < BlinkHalfPeriod;
            // 讓輸入法的選字窗跟著游標（游標底邊）；Input System 的座標原點在左上，uGUI 的在左下
            var bottom = RectTransformUtility.WorldToScreenPoint(null, caret.TransformPoint(new Vector3(0, caret.rect.yMin)));
            var position = new Vector2(bottom.x, Screen.height - bottom.y);
            if (position != imePosition) TextFocus.SetImeCursor(position);
            imePosition = position;
        }

        void LayoutLog()
        {
            float maxHeight = Mathf.Max(0, ((RectTransform)rect.parent).rect.height / 3 - InputHeight - Gap);
            float now = Time.unscaledTime, y = 0;
            int used = 0;
            for (int i = messages.Count - 1 - (IsOpen ? scrolledLines : 0); i >= 0; i--)
            {
                var message = messages[i];
                float alpha = IsOpen ? 1 : Mathf.Clamp01((VisibleSeconds - (now - message.Time)) / FadeSeconds);
                if (alpha <= 0 || (!IsOpen && used == MaxClosedLines) || y + message.Height > maxHeight) break;
                var line = LineAt(used++);
                line.Rect.gameObject.SetActive(true);
                if (line.Text.text != message.Markup) line.Text.text = message.Markup;
                line.Rect.anchoredPosition = new Vector2(0, y);
                line.Rect.sizeDelta = new Vector2(Width, message.Height);
                line.Group.alpha = alpha;
                y += message.Height;
            }
            for (int i = used; i < lines.Count; i++) lines[i].Rect.gameObject.SetActive(false);
            log.sizeDelta = new Vector2(Width, y);
            UsedHeight = (IsOpen ? InputHeight + Gap : 0) + y;
        }

        Line LineAt(int index)
        {
            while (lines.Count <= index)
            {
                var background = Ui.Image("Line", log, LineColor);
                var lineRect = background.rectTransform;
                lineRect.anchorMin = lineRect.anchorMax = lineRect.pivot = Vector2.zero;
                var text = Ui.Text("Text", lineRect, FontSize, Color.white);
                text.richText = true;
                text.textWrappingMode = TextWrappingModes.Normal;
                text.alignment = TextAlignmentOptions.MidlineLeft;
                Ui.Fill(text.rectTransform, PadX, PadX, PadY, PadY);
                lines.Add(new Line { Rect = lineRect, Group = background.gameObject.AddComponent<CanvasGroup>(), Text = text });
            }
            return lines[index];
        }

        void RenderInput()
        {
            if (renderedVersion != input.Version || inputDirty)
            {
                renderedVersion = input.Version;
                inputDirty = false;
                inputText.text = InputMarkup();
                inputText.ForceMeshUpdate();
            }
            float x = CaretX(BufferToTmp(input.Caret));
            float view = inputViewport.rect.width;
            if (x - inputScroll > view - CaretWidth) inputScroll = x - view + CaretWidth;
            if (x < inputScroll) inputScroll = x;
            inputContent.anchoredPosition = new Vector2(-inputScroll, 0);
            caret.anchoredPosition = new Vector2(x, 0);
            caret.sizeDelta = new Vector2(CaretWidth, FontSize * 1.2f);
        }

        string InputMarkup()
        {
            var markup = new StringBuilder(input.Length + 32);
            for (int i = 0; i <= input.Length; i++)
            {
                if (i == input.Caret && composition.Length > 0)
                    markup.Append("<u>").Append(Ui.Escape(composition)).Append("</u>");
                if (i == input.Length) break;
                if (input.HasSelection && i == input.SelectionStart) markup.Append($"<mark={Art.SelectionMark}>");
                markup.Append(Ui.Escape(input[i]));
                if (input.HasSelection && i == input.SelectionEnd - 1) markup.Append("</mark>");
            }
            return markup.ToString();
        }

        /// <summary>游標在 TMP 文字座標中的 x。</summary>
        float CaretX(int index)
        {
            var info = inputText.textInfo;
            return info.characterCount == 0 ? 0
                : index < info.characterCount ? info.characterInfo[index].origin
                : info.characterInfo[info.characterCount - 1].xAdvance;
        }

        /// <summary>滑鼠位置最近的字縫（buffer 索引）。</summary>
        int IndexAt(Vector2 screen) => TmpToBuffer(TmpIndexAt(screen));

        int TmpIndexAt(Vector2 screen)
        {
            inputText.ForceMeshUpdate();
            if (inputText.textInfo.characterCount == 0) return 0;
            return TMP_TextUtilities.GetCursorIndexFromPosition(inputText, screen, null);
        }

        /// <summary>雙擊選字要的是指標底下那個字；指標在字的右半邊時，最近的字縫已經是下一個字前面。</summary>
        int CharacterAt(Vector2 screen)
        {
            int index = TmpIndexAt(screen);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(inputContent, screen, null, out var local);
            if (index > 0 && local.x < CaretX(index)) index--;
            return TmpToBuffer(index);
        }

        int BufferToTmp(int index) => composition.Length > 0 && index >= input.Caret ? index + composition.Length : index;

        int TmpToBuffer(int index)
        {
            if (composition.Length > 0 && index > input.Caret) index = Mathf.Max(input.Caret, index - composition.Length);
            return Mathf.Clamp(index, 0, input.Length);
        }
    }
}

using System;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Lexiscript
{
    /// <summary>
    /// 右下角的筆記：可縮小/展開，像一般文字檔一樣編輯（方向鍵、選取、Ctrl+Z/C/V/X/A、換行），
    /// 可對選取的文字套粗體、底線、斜體；背景透明度輸入百分比調整；Ctrl+滾輪調整字體大小。
    /// 內容與設定自動存檔。
    /// </summary>
    // 按縮小鈕後面板當場變小；排在方塊、暴雷條之後處理點擊，同一下點擊才不會穿透到後面的世界
    [DefaultExecutionOrder(100)]
    public sealed class NotesPanel : MonoBehaviour, ITextTarget
    {
        [Serializable]
        sealed class SaveData
        {
            public string text = "";
            public string styles = "";
            public int opacity = 100;
            public float fontSize = DefaultFontSize;
            public bool minimized;
        }

        sealed class Button
        {
            public RectTransform Rect;
            public Image Background;
        }

        public const float DefaultFontSize = 24, MinFontSize = 12, MaxFontSize = 64, FontStep = 2;
        public const int MaxLength = 10000;
        static readonly Vector2 ExpandedSize = new Vector2(460, 600), MinimizedSize = new Vector2(150, 44);
        const float Margin = 24, ToolbarHeight = 44, Padding = 14, Border = 2, CaretWidth = 2;
        const float BlinkHalfPeriod = 0.53f, MultiClickTime = 0.35f, SaveDelay = 1f;
        static readonly Color PageColor = Color.white, ToolbarColor = new Color(0.914f, 0.922f, 0.933f);
        static readonly Color HoverColor = new Color(0, 0, 0, 0.08f), ActiveColor = new Color(0, 0, 0, 0.2f);

        /// <summary>存檔位置；null 代表 persistentDataPath/notes.json。測試改到暫存檔，以免蓋掉玩家的筆記。</summary>
        public static string StoragePath;

        readonly TextEditBuffer buffer = new TextEditBuffer(c => c == '\t' || TextEditBuffer.Printable(c), MaxLength, multiline: true);
        RectTransform rect, viewport, content, caret;
        GameObject expanded, minimized;
        Image page, caretImage;
        TextMeshProUGUI text, placeholder;
        Button bold, underline, italic, minimize;
        OpacityField opacityField;
        string composition = "";
        string clipboardText;
        TextStyle[] clipboardStyles;
        float blinkStart, lastClickTime = float.NegativeInfinity, saveAt = float.PositiveInfinity;
        float scroll, preferredX = float.NaN, renderedWidth, fontSteps;
        int clicks, renderedVersion = -1;
        /// <summary>游標停在軟換行的行尾（而不是下一行開頭）時的 buffer 版本；游標一動或內容一改就失效。</summary>
        int lineEndVersion = -1;
        bool dragging, layoutDirty = true, revealCaret;
        Vector2 imePosition;

        public TextEditBuffer Buffer => buffer;
        public bool Minimized { get; private set; }
        public int Opacity { get; private set; } = 100;
        public float FontSize { get; private set; } = DefaultFontSize;
        public bool IsFocused => ReferenceEquals(TextFocus.Current, this);
        public bool OpacityFocused => TextFocus.Current == opacityField;
        public bool WantsIme => true;
        public TextMeshProUGUI TextComponent => text;
        public RectTransform Viewport => viewport;
        public RectTransform Caret => caret;
        public RectTransform BoldButton => bold.Rect;
        public RectTransform UnderlineButton => underline.Rect;
        public RectTransform ItalicButton => italic.Rect;
        public RectTransform MinimizeButton => minimize.Rect;
        public RectTransform OpacityInput => opacityField.Rect;
        public float PageAlpha => page.color.a;

        static string FilePath => StoragePath ?? Path.Combine(Application.persistentDataPath, "notes.json");

        void Awake()
        {
            rect = (RectTransform)transform;
            Build();
            Load();
            buffer.Changed += ScheduleSave;
            ApplyLayout();
        }

        void OnDestroy()
        {
            buffer.Changed -= ScheduleSave;
            if (saveAt < float.PositiveInfinity) Save();
            TextFocus.Release(this);
            TextFocus.Release(opacityField);
        }

        void OnApplicationQuit()
        {
            if (saveAt < float.PositiveInfinity) Save();
        }

        void Build()
        {
            expanded = Ui.Rect("Expanded", transform).gameObject;
            Ui.Fill((RectTransform)expanded.transform);
            Ui.Fill(Ui.Image("Border", expanded.transform, Art.Ink).rectTransform);
            page = Ui.Image("Page", expanded.transform, PageColor);
            Ui.Fill(page.rectTransform, Border, Border, ToolbarHeight + Border, Border);

            var toolbar = Ui.Image("Toolbar", expanded.transform, ToolbarColor).rectTransform;
            toolbar.anchorMin = new Vector2(0, 1);
            toolbar.anchorMax = Vector2.one;
            toolbar.pivot = new Vector2(0.5f, 1);
            toolbar.offsetMin = new Vector2(Border, -ToolbarHeight - Border);
            toolbar.offsetMax = new Vector2(-Border, -Border);
            bold = ToolButton(toolbar, "Bold", "<b>B</b>", new Vector2(0, 0.5f), new Vector2(8, 0));
            underline = ToolButton(toolbar, "Underline", "<u>U</u>", new Vector2(0, 0.5f), new Vector2(48, 0));
            italic = ToolButton(toolbar, "Italic", "<b><i>I</i></b>", new Vector2(0, 0.5f), new Vector2(88, 0));
            minimize = ToolButton(toolbar, "Minimize", "—", new Vector2(1, 0.5f), new Vector2(-8, 0));
            opacityField = new OpacityField(this, toolbar);

            viewport = Ui.Rect("Viewport", expanded.transform);
            Ui.Fill(viewport, Border + Padding, Border + Padding, ToolbarHeight + Border + Padding / 2, Border + Padding / 2);
            viewport.gameObject.AddComponent<RectMask2D>();
            text = Ui.Text("Text", viewport, FontSize, Art.Ink);
            text.textWrappingMode = TextWrappingModes.Normal;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.richText = true;
            content = text.rectTransform;
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0, 1);
            content.sizeDelta = Vector2.zero;
            placeholder = Ui.Text("Placeholder", viewport, FontSize, Art.PlaceholderInk);
            placeholder.alignment = TextAlignmentOptions.TopLeft;
            placeholder.text = "Write down what you discover...";
            Ui.Fill(placeholder.rectTransform);
            caretImage = Ui.Image("Caret", content, Art.Ink);
            caret = caretImage.rectTransform;
            caret.anchorMin = caret.anchorMax = new Vector2(0, 1);
            caret.pivot = Vector2.zero;

            minimized = Ui.Rect("Minimized", transform).gameObject;
            Ui.Fill((RectTransform)minimized.transform);
            Ui.Fill(Ui.Image("Border", minimized.transform, Art.Ink).rectTransform);
            Ui.Fill(Ui.Image("Background", minimized.transform, ToolbarColor).rectTransform, Border, Border, Border, Border);
            var label = Ui.Text("Label", minimized.transform, 22, Art.Ink);
            label.alignment = TextAlignmentOptions.Center;
            label.text = "Notes  ▲";
            Ui.Fill(label.rectTransform);
        }

        Button ToolButton(Transform toolbar, string name, string label, Vector2 anchor, Vector2 position)
        {
            var background = Ui.Image(name, toolbar, Color.clear);
            Ui.Place(background.rectTransform, anchor, position, new Vector2(36, 34));
            var caption = Ui.Text("Label", background.transform, 24, Art.Ink);
            caption.alignment = TextAlignmentOptions.Center;
            caption.text = label;
            Ui.Fill(caption.rectTransform);
            return new Button { Rect = background.rectTransform, Background = background };
        }

        public bool Covers(Vector2 screen) => Ui.Contains(rect, screen);

        /// <summary>滑鼠在筆記上時捲動筆記；Ctrl+滾輪在筆記有焦點時也算（調整字體大小）。</summary>
        public bool WantsScroll(Vector2 screen) => !Minimized && (Covers(screen) || (IsFocused && CtrlHeld()));

        public void SetMinimized(bool value)
        {
            if (Minimized == value) return;
            Minimized = value;
            if (value)
            {
                TextFocus.Release(this);
                TextFocus.Release(opacityField);
            }
            ApplyLayout();
            ScheduleSave();
        }

        public void SetOpacity(int percent)
        {
            Opacity = Mathf.Clamp(percent, 0, 100);
            ApplyLayout();
            ScheduleSave();
        }

        public void SetFontSize(float size)
        {
            FontSize = Mathf.Clamp(size, MinFontSize, MaxFontSize);
            ApplyLayout();
            ScheduleSave();
        }

        void ApplyLayout()
        {
            Ui.Place(rect, new Vector2(1, 0), new Vector2(-Margin, Margin), Minimized ? MinimizedSize : ExpandedSize);
            expanded.SetActive(!Minimized);
            minimized.SetActive(Minimized);
            var color = PageColor;
            color.a = Opacity / 100f;
            page.color = color;
            text.fontSize = placeholder.fontSize = FontSize;
            opacityField.Redraw();
            layoutDirty = true;
        }

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse != null) HandleMouse(mouse);
            if (Time.unscaledTime >= saveAt) Save();
        }

        void LateUpdate()
        {
            if (Minimized) return;
            Render();
            if (revealCaret) RevealCaret();
            revealCaret = false;
            content.anchoredPosition = new Vector2(0, scroll);
            caretImage.enabled = IsFocused && (Time.unscaledTime - blinkStart) % (2 * BlinkHalfPeriod) < BlinkHalfPeriod;
            if (IsFocused)
            {
                // 讓輸入法的選字窗跟著游標（游標底邊）；Input System 的座標原點在左上，uGUI 的在左下
                var bottom = RectTransformUtility.WorldToScreenPoint(null, caret.position);
                var position = new Vector2(bottom.x, Screen.height - bottom.y);
                if (position != imePosition) Keyboard.current?.SetIMECursorPosition(position);
                imePosition = position;
            }
            opacityField.Blink();
            bold.Background.color = ButtonColor(bold, buffer.Has(TextStyle.Bold));
            underline.Background.color = ButtonColor(underline, buffer.Has(TextStyle.Underline));
            italic.Background.color = ButtonColor(italic, buffer.Has(TextStyle.Italic));
            minimize.Background.color = ButtonColor(minimize, false);
        }

        Color ButtonColor(Button button, bool active)
        {
            if (active) return ActiveColor;
            var mouse = Mouse.current;
            return mouse != null && Ui.Contains(button.Rect, mouse.position.ReadValue()) ? HoverColor : Color.clear;
        }

        void HandleMouse(Mouse mouse)
        {
            if (Hud.ModalOpen) return;
            Vector2 screen = mouse.position.ReadValue();
            if (dragging)
            {
                if (mouse.leftButton.isPressed)
                {
                    var (index, lineEnd) = CaretAt(screen);
                    MoveCaret(index, lineEnd, true);
                    revealCaret = true;
                }
                else
                {
                    dragging = false;
                }
            }

            float notches = Mathf.Clamp(mouse.scroll.ReadValue().y, -3, 3);
            if (!CtrlHeld()) fontSteps = 0;
            if (notches != 0 && WantsScroll(screen))
            {
                if (CtrlHeld())
                {
                    // 觸控板一次手勢會在好幾幀各送零點幾格，累積滿一格才調一級字體
                    fontSteps += notches;
                    int steps = (int)fontSteps;
                    fontSteps -= steps;
                    if (steps != 0) SetFontSize(FontSize + FontStep * steps);
                }
                else
                {
                    scroll = Mathf.Clamp(scroll - notches * 3 * LineHeight(), 0, MaxScroll());
                }
            }

            if (!mouse.leftButton.wasPressedThisFrame) return;
            if (!Covers(screen))
            {
                TextFocus.Release(this);
                TextFocus.Release(opacityField);
                return;
            }
            if (Minimized)
            {
                SetMinimized(false);
                return;
            }
            if (Ui.Contains(bold.Rect, screen)) ToggleStyle(TextStyle.Bold);
            else if (Ui.Contains(underline.Rect, screen)) ToggleStyle(TextStyle.Underline);
            else if (Ui.Contains(italic.Rect, screen)) ToggleStyle(TextStyle.Italic);
            else if (Ui.Contains(minimize.Rect, screen)) SetMinimized(true);
            else if (Ui.Contains(opacityField.Rect, screen)) TextFocus.Set(opacityField);
            else if (Ui.Contains(viewport, screen)) ClickText(screen);
        }

        void ToggleStyle(TextStyle style)
        {
            TextFocus.Set(this);
            buffer.ToggleStyle(style);
        }

        void ClickText(Vector2 screen)
        {
            float now = Time.unscaledTime;
            clicks = now - lastClickTime <= MultiClickTime ? clicks + 1 : 1;
            lastClickTime = now;
            TextFocus.Set(this);
            var (index, lineEnd) = CaretAt(screen);
            if (clicks == 2)
            {
                buffer.SelectWordAt(TmpToBuffer(CharacterAt(index, lineEnd, LocalPoint(screen).x)));
            }
            else if (clicks >= 3)
            {
                int at = TmpToBuffer(index);
                buffer.Select(buffer.LineStart(at), buffer.LineEnd(at));
            }
            else
            {
                var keyboard = Keyboard.current;
                MoveCaret(index, lineEnd, keyboard != null && keyboard.shiftKey.isPressed);
                dragging = true;
            }
            preferredX = float.NaN;
            blinkStart = now;
        }

        /// <summary>
        /// 滑鼠位置對應的游標（TMP 索引）。TMP 的 GetCursorIndexFromPosition 在所有行裡找最近的字，
        /// 點在短行右邊的空白會跑到別行，所以先依高度決定是哪一行，再找行內最近的字縫。
        /// </summary>
        (int index, bool lineEnd) CaretAt(Vector2 screen)
        {
            Render();
            var info = text.textInfo;
            if (info.characterCount == 0) return (0, false);
            var local = LocalPoint(screen);
            int nearest = 0;
            float nearestDistance = float.MaxValue;
            for (int line = 0; line < info.lineCount; line++)
            {
                float distance = DistanceToBand(local.y, info.lineInfo[line].descender, info.lineInfo[line].ascender);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearest = line;
                }
            }
            // 結尾換行之後的空行沒有 lineInfo
            var (end, height) = CaretGeometry(info.characterCount);
            if (info.characterInfo[info.characterCount - 1].character == '\n' && DistanceToBand(local.y, end.y, end.y + height) < nearestDistance)
                return (info.characterCount, false);
            return NearestOnLine(nearest, local.x);
        }

        static float DistanceToBand(float y, float bottom, float top) => y < bottom ? bottom - y : y > top ? y - top : 0;

        Vector2 LocalPoint(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(content, screen, null, out var local);
            return local;
        }

        /// <summary>雙擊選字要的是指標底下那個字；指標在字的右半邊時，最近的字縫已經是下一個字前面。</summary>
        int CharacterAt(int index, bool lineEnd, float x) =>
            lineEnd || (index > 0 && x < CaretGeometry(index).position.x) ? index - 1 : index;

        void MoveCaret(int index, bool lineEnd, bool extend)
        {
            buffer.MoveTo(TmpToBuffer(index), extend);
            if (lineEnd) lineEndVersion = buffer.Version;
        }

        bool AtLineEnd => lineEndVersion == buffer.Version && composition.Length == 0;

        public void OnText(char c)
        {
            buffer.Type(c);
            preferredX = float.NaN;
            Edited();
        }

        public void OnComposition(string value)
        {
            // 每次用輸入法送出的字各自是一步復原；中文沒有空白，不分段的話一次 Ctrl+Z 會吃掉好幾句
            if (composition.Length == 0 && value.Length > 0) buffer.BreakTypingGroup();
            composition = value;
            layoutDirty = true;
            revealCaret = true;
        }

        public void OnKey(Key key, bool shift, bool ctrl)
        {
            bool vertical = key == Key.UpArrow || key == Key.DownArrow || key == Key.PageUp || key == Key.PageDown;
            if (!vertical) preferredX = float.NaN;
            switch (key)
            {
                case Key.Escape: TextFocus.Release(this); return;
                case Key.Enter:
                case Key.NumpadEnter: buffer.Type('\n'); break;
                case Key.Tab: buffer.Type('\t'); break;
                case Key.UpArrow: MoveVertical(-1, shift); break;
                case Key.DownArrow: MoveVertical(1, shift); break;
                case Key.PageUp: MoveVertical(-PageLines(), shift); break;
                case Key.PageDown: MoveVertical(PageLines(), shift); break;
                case Key.Home: MoveToLineEdge(false, shift, ctrl); break;
                case Key.End: MoveToLineEdge(true, shift, ctrl); break;
                case Key.B when ctrl: buffer.ToggleStyle(TextStyle.Bold); break;
                case Key.I when ctrl: buffer.ToggleStyle(TextStyle.Italic); break;
                case Key.U when ctrl: buffer.ToggleStyle(TextStyle.Underline); break;
                case Key.C when ctrl: Copy(); break;
                case Key.X when ctrl:
                    Copy();
                    buffer.DeleteSelection();
                    break;
                case Key.V when ctrl: Paste(); break;
                default: TextEditKeys.Apply(buffer, key, shift, ctrl); break;
            }
            Edited();
        }

        public void OnFocusChanged(bool focused)
        {
            composition = "";
            dragging = false;
            layoutDirty = true;
            if (focused)
            {
                blinkStart = Time.unscaledTime;
                revealCaret = true;
                // 聊天列可能把選字窗移走了，即使游標位置沒變也要重送
                imePosition = new Vector2(float.NaN, float.NaN);
            }
        }

        void Edited()
        {
            blinkStart = Time.unscaledTime;
            revealCaret = true;
        }

        void Copy()
        {
            if (!buffer.HasSelection) return;
            clipboardText = buffer.SelectedText;
            clipboardStyles = buffer.Styles(buffer.SelectionStart, clipboardText.Length);
            TextClipboard.Text = clipboardText;
        }

        void Paste()
        {
            // Windows 剪貼簿會把 \n 存成 \r\n，比對前先統一
            var pasted = TextClipboard.Text.Replace("\r\n", "\n");
            // 從筆記複製出去的文字貼回來時保留粗體等樣式；外面複製來的套用目前的打字樣式
            buffer.Insert(pasted, pasted == clipboardText ? clipboardStyles : null);
        }

        void MoveVertical(int lines, bool extend)
        {
            Render();
            var info = text.textInfo;
            int index = BufferToTmp(buffer.Caret);
            bool atLineEnd = AtLineEnd;
            var (position, _) = CaretGeometry(index, atLineEnd);
            if (float.IsNaN(preferredX)) preferredX = position.x;
            int target = LineOf(index, atLineEnd) + lines;
            if (target < 0)
            {
                buffer.MoveTo(0, extend);
            }
            else if (target >= info.lineCount || info.characterCount == 0)
            {
                buffer.MoveTo(buffer.Length, extend);
            }
            else
            {
                var (nearest, lineEnd) = NearestOnLine(target, preferredX);
                MoveCaret(nearest, lineEnd, extend);
            }
        }

        void MoveToLineEdge(bool end, bool extend, bool document)
        {
            if (document)
            {
                buffer.MoveTo(end ? buffer.Length : 0, extend);
                return;
            }
            Render();
            var info = text.textInfo;
            int line = LineOf(BufferToTmp(buffer.Caret), AtLineEnd);
            if (info.characterCount == 0 || line >= info.lineCount) return;
            var lineInfo = info.lineInfo[line];
            int index = lineInfo.firstCharacterIndex;
            bool lineEnd = false;
            if (end)
            {
                index = lineInfo.lastCharacterIndex + 1;
                char last = info.characterInfo[lineInfo.lastCharacterIndex].character;
                // 換行字元前就是行尾；自動換行處的空白也停在它前面，否則游標會跑到下一行開頭
                if (last == '\n' || (char.IsWhiteSpace(last) && index < info.characterCount)) index--;
                else lineEnd = SoftLineEnd(index);
            }
            MoveCaret(index, lineEnd, extend);
        }

        int PageLines() => Mathf.Max(1, Mathf.FloorToInt(viewport.rect.height / LineHeight()) - 1);

        /// <summary>這個 TMP 字元索引的游標在第幾行；結尾是換行字元時，最後的游標在下一行。</summary>
        int LineOf(int index, bool lineEnd = false)
        {
            var info = text.textInfo;
            if (info.characterCount == 0) return 0;
            if (lineEnd && SoftLineEnd(index)) return info.characterInfo[index - 1].lineNumber;
            if (index < info.characterCount) return info.characterInfo[index].lineNumber;
            var last = info.characterInfo[info.characterCount - 1];
            return last.character == '\n' ? last.lineNumber + 1 : last.lineNumber;
        }

        /// <returns>行內離 x 最近的字縫；lineEnd 表示選到的是軟換行的行尾。</returns>
        (int index, bool lineEnd) NearestOnLine(int line, float x)
        {
            var info = text.textInfo;
            var lineInfo = info.lineInfo[line];
            int best = lineInfo.firstCharacterIndex;
            if (best >= info.characterCount) return (info.characterCount, false);
            float bestDistance = float.MaxValue;
            bool bestLineEnd = false;
            for (int i = lineInfo.firstCharacterIndex; i <= lineInfo.lastCharacterIndex + 1 && i <= info.characterCount; i++)
            {
                bool lineEnd = false;
                if (i == lineInfo.lastCharacterIndex + 1)
                {
                    // 換行字元或空白造成的換行，之後的位置已經是下一行；只有字中間的軟換行算這一行的行尾
                    if (i < info.characterCount ? !SoftLineEnd(i) : info.characterInfo[i - 1].character == '\n') break;
                    lineEnd = i < info.characterCount;
                }
                float distance = Mathf.Abs(CaretGeometry(i, lineEnd).position.x - x);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                    bestLineEnd = lineEnd;
                }
            }
            return (best, bestLineEnd);
        }

        /// <summary>
        /// 中文或過長的字在字中間自動換行時，同一個索引既是這行行尾也是下一行開頭。
        /// 換行字元與空白造成的換行不算：那裡的行尾停在換行字元或空白前面。
        /// </summary>
        bool SoftLineEnd(int index)
        {
            var info = text.textInfo;
            if (index <= 0 || index >= info.characterCount) return false;
            var before = info.characterInfo[index - 1];
            return before.lineNumber != info.characterInfo[index].lineNumber && before.character != '\n' && !char.IsWhiteSpace(before.character);
        }

        /// <summary>游標在 TMP 文字座標中的左下角與高度；lineEnd 時軟換行處的游標畫在上一行行尾。</summary>
        (Vector2 position, float height) CaretGeometry(int index, bool lineEnd = false)
        {
            var info = text.textInfo;
            int count = info.characterCount;
            if (count == 0) return (new Vector2(0, -LineHeight()), LineHeight());
            if (lineEnd && SoftLineEnd(index))
            {
                var before = info.characterInfo[index - 1];
                var beforeLine = info.lineInfo[before.lineNumber];
                return (new Vector2(before.xAdvance, beforeLine.descender), beforeLine.ascender - beforeLine.descender);
            }
            if (index < count)
            {
                var ch = info.characterInfo[index];
                var line = info.lineInfo[ch.lineNumber];
                return (new Vector2(ch.origin, line.descender), line.ascender - line.descender);
            }
            var last = info.characterInfo[count - 1];
            var lastLine = info.lineInfo[last.lineNumber];
            float height = lastLine.ascender - lastLine.descender;
            if (last.character == '\n') return (new Vector2(0, lastLine.descender - lastLine.lineHeight), height);
            return (new Vector2(last.xAdvance, lastLine.descender), height);
        }

        float LineHeight()
        {
            var face = text.font.faceInfo;
            return FontSize * face.lineHeight / face.pointSize;
        }

        float MaxScroll() => Mathf.Max(0, content.rect.height - viewport.rect.height);

        void RevealCaret()
        {
            var (position, height) = CaretGeometry(BufferToTmp(buffer.Caret), AtLineEnd);
            float view = viewport.rect.height;
            float top = -(position.y + height), bottom = -position.y;
            if (top < scroll) scroll = top;
            if (bottom > scroll + view) scroll = bottom - view;
            scroll = Mathf.Clamp(scroll, 0, MaxScroll());
        }

        void Render()
        {
            // 第一幀 Canvas 還沒排版時寬度是 0，寬度變了就要重新換行
            float width = viewport.rect.width;
            if (renderedVersion == buffer.Version && !layoutDirty && width == renderedWidth) return;
            renderedVersion = buffer.Version;
            renderedWidth = width;
            layoutDirty = false;
            text.text = Markup();
            placeholder.enabled = buffer.Length == 0 && composition.Length == 0 && !IsFocused;
            text.ForceMeshUpdate();
            // TMP 的 preferredHeight 不含結尾換行之後的空行，游標停在那裡時會捲不到、被裁掉
            var (end, _) = CaretGeometry(text.textInfo.characterCount);
            content.sizeDelta = new Vector2(0, Mathf.Max(viewport.rect.height, text.preferredHeight, -end.y));
            scroll = Mathf.Clamp(scroll, 0, MaxScroll());
            var (position, height) = CaretGeometry(BufferToTmp(buffer.Caret), AtLineEnd);
            caret.anchoredPosition = position;
            caret.sizeDelta = new Vector2(CaretWidth, height);
        }

        /// <summary>
        /// 每個 buffer 字元恰好對應一個 TMP characterInfo（玩家的 &lt; 用 noparse 包起來），
        /// 所以 buffer 索引加上組字長度就是 TMP 索引。
        /// </summary>
        string Markup()
        {
            var markup = new StringBuilder(buffer.Length + 64);
            var open = TextStyle.None;
            bool marking = false;
            for (int i = 0; i <= buffer.Length; i++)
            {
                if (i == buffer.Caret && composition.Length > 0)
                    markup.Append("<u>").Append(Ui.Escape(composition)).Append("</u>");
                if (i == buffer.Length) break;
                bool selected = buffer.HasSelection && i >= buffer.SelectionStart && i < buffer.SelectionEnd;
                if (selected != marking)
                {
                    markup.Append(selected ? $"<mark={Art.SelectionMark}>" : "</mark>");
                    marking = selected;
                }
                var style = buffer.StyleAt(i);
                AppendStyleChange(markup, open, style, TextStyle.Bold, "b");
                AppendStyleChange(markup, open, style, TextStyle.Italic, "i");
                AppendStyleChange(markup, open, style, TextStyle.Underline, "u");
                open = style;
                markup.Append(Ui.Escape(buffer[i]));
            }
            if (marking) markup.Append("</mark>");
            return markup.ToString();
        }

        static void AppendStyleChange(StringBuilder markup, TextStyle from, TextStyle to, TextStyle flag, string tag)
        {
            bool was = (from & flag) != 0, now = (to & flag) != 0;
            if (was && !now) markup.Append("</").Append(tag).Append('>');
            else if (!was && now) markup.Append('<').Append(tag).Append('>');
        }

        int BufferToTmp(int index) => composition.Length > 0 && index >= buffer.Caret ? index + composition.Length : index;

        int TmpToBuffer(int index)
        {
            if (composition.Length > 0 && index > buffer.Caret) index = Mathf.Max(buffer.Caret, index - composition.Length);
            return Mathf.Clamp(index, 0, buffer.Length);
        }

        static bool CtrlHeld()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && keyboard.ctrlKey.isPressed;
        }

        void ScheduleSave() => saveAt = Time.unscaledTime + SaveDelay;

        /// <summary>重置進度用：清空筆記、刪掉存檔，而且之後（包括場景卸載時）不會再存回去。</summary>
        public void Discard()
        {
            buffer.Reset("");
            saveAt = float.PositiveInfinity;
            DeleteSave();
        }

        /// <summary>刪掉筆記存檔；場景裡有筆記時改用 <see cref="Discard"/>，否則它卸載時會把內容存回去。</summary>
        public static void DeleteSave()
        {
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not delete notes at {FilePath}: {exception.Message}");
            }
        }

        void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
                var styles = new TextStyle[data.text.Length];
                for (int i = 0; i < styles.Length && i < data.styles.Length; i++)
                    styles[i] = (TextStyle)(data.styles[i] - '0');
                buffer.Reset(data.text, styles);
                Opacity = Mathf.Clamp(data.opacity, 0, 100);
                FontSize = Mathf.Clamp(data.fontSize, MinFontSize, MaxFontSize);
                Minimized = data.minimized;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not read notes from {FilePath}: {exception.Message}");
            }
            saveAt = float.PositiveInfinity;
        }

        void Save()
        {
            saveAt = float.PositiveInfinity;
            var styles = new StringBuilder(buffer.Length);
            for (int i = 0; i < buffer.Length; i++) styles.Append((char)('0' + (int)buffer.StyleAt(i)));
            var data = new SaveData { text = buffer.Text, styles = styles.ToString(), opacity = Opacity, fontSize = FontSize, minimized = Minimized };
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, JsonUtility.ToJson(data));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not save notes to {FilePath}: {exception.Message}");
            }
        }

        /// <summary>工具列上的透明度欄：只收數字，Enter 或點別處時套用，Esc 取消。</summary>
        sealed class OpacityField : ITextTarget
        {
            readonly NotesPanel owner;
            readonly TextEditBuffer digits = new TextEditBuffer(c => c >= '0' && c <= '9', 3);
            readonly TextMeshProUGUI label;
            float blinkStart;
            bool cancelled;

            public OpacityField(NotesPanel owner, Transform toolbar)
            {
                this.owner = owner;
                var background = Ui.Image("Opacity", toolbar, Color.white);
                Ui.Place(background.rectTransform, new Vector2(1, 0.5f), new Vector2(-52, 0), new Vector2(84, 32));
                Rect = background.rectTransform;
                Ui.Fill(Ui.Image("Border", Rect, Art.Ink).rectTransform);
                Ui.Fill(Ui.Image("Fill", Rect, Color.white).rectTransform, 1.5f, 1.5f, 1.5f, 1.5f);
                label = Ui.Text("Label", Rect, 22, Art.Ink);
                label.alignment = TextAlignmentOptions.Center;
                Ui.Fill(label.rectTransform);
            }

            public RectTransform Rect { get; }
            public bool WantsIme => false;
            public TextEditBuffer Digits => digits;

            public void OnText(char c)
            {
                digits.Type(c);
                Touch();
            }

            public void OnComposition(string composition) { }

            public void OnKey(Key key, bool shift, bool ctrl)
            {
                if (key == Key.Enter || key == Key.NumpadEnter)
                {
                    TextFocus.Release(this);
                    return;
                }
                if (key == Key.Escape)
                {
                    cancelled = true;
                    TextFocus.Release(this);
                    return;
                }
                if (TextEditKeys.Apply(digits, key, shift, ctrl)) Touch();
            }

            public void OnFocusChanged(bool focused)
            {
                if (focused)
                {
                    cancelled = false;
                    digits.Reset(owner.Opacity.ToString());
                    digits.SelectAll();
                }
                else if (!cancelled && int.TryParse(digits.Text, out int percent))
                {
                    owner.SetOpacity(percent);
                }
                Touch();
            }

            void Touch()
            {
                blinkStart = Time.unscaledTime;
                Redraw();
            }

            public void Blink()
            {
                if (TextFocus.Current == this) Redraw();
            }

            public void Redraw()
            {
                if (TextFocus.Current != this)
                {
                    label.text = $"{owner.Opacity}%";
                    return;
                }
                bool caretOn = (Time.unscaledTime - blinkStart) % (2 * BlinkHalfPeriod) < BlinkHalfPeriod;
                var markup = new StringBuilder();
                for (int i = 0; i <= digits.Length; i++)
                {
                    if (i == digits.Caret) markup.Append(caretOn ? "|" : "<alpha=#00>|<alpha=#FF>");
                    if (i == digits.Length) break;
                    bool selected = digits.HasSelection && i >= digits.SelectionStart && i < digits.SelectionEnd;
                    markup.Append(selected ? $"<mark={Art.SelectionMark}>{digits[i]}</mark>" : digits[i].ToString());
                }
                label.text = markup.Append('%').ToString();
            }
        }
    }
}

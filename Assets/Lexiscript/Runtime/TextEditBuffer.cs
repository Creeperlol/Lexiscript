using System;
using System.Collections.Generic;
using System.Text;

namespace Lexiscript
{
    [Flags]
    public enum TextStyle : byte
    {
        None = 0,
        Bold = 1,
        Italic = 2,
        Underline = 4,
    }

    /// <summary>
    /// 不依賴 Unity 的文字欄編輯模型：逐字樣式、游標與選取範圍、復原與重做。
    /// 答案欄、聊天列、筆記共用；怎麼畫、哪個鍵對應哪個操作由各自的元件決定。
    /// </summary>
    public sealed class TextEditBuffer
    {
        const int MaxUndo = 200;

        enum Edit
        {
            None,
            Typing,
            Deleting,
            Other,
        }

        enum CharKind
        {
            Space,
            Word,
            Other,
        }

        struct Snapshot
        {
            public string Text;
            public TextStyle[] Styles;
            public int Caret, Anchor;
        }

        readonly StringBuilder text = new StringBuilder();
        readonly List<TextStyle> styles = new List<TextStyle>();
        readonly Func<char, bool> accepts;
        readonly List<Snapshot> undo = new List<Snapshot>();
        readonly List<Snapshot> redo = new List<Snapshot>();
        Edit lastEdit;
        bool lastTypedSpace;

        /// <param name="accepts">單一字元能不能進來；換行另由 <paramref name="multiline"/> 決定。</param>
        /// <param name="multiline">false 時貼上的換行會變成空白，再交給 <paramref name="accepts"/> 判斷。</param>
        public TextEditBuffer(Func<char, bool> accepts, int maxLength = int.MaxValue, bool multiline = false)
        {
            this.accepts = accepts;
            MaxLength = maxLength;
            Multiline = multiline;
        }

        public int MaxLength { get; }
        public bool Multiline { get; }
        public string Text => text.ToString();
        public int Length => text.Length;
        public char this[int index] => text[index];
        public int Caret { get; private set; }
        /// <summary>選取範圍的另一端；沒有選取時等於 <see cref="Caret"/>。</summary>
        public int Anchor { get; private set; }
        public bool HasSelection => Caret != Anchor;
        public int SelectionStart => Math.Min(Caret, Anchor);
        public int SelectionEnd => Math.Max(Caret, Anchor);
        public string SelectedText => text.ToString(SelectionStart, SelectionEnd - SelectionStart);
        /// <summary>下一個打進來的字套用的樣式；移動游標時會改成游標前一個字的樣式，跟一般文書軟體相同。</summary>
        public TextStyle TypingStyle { get; private set; }
        /// <summary>內容、樣式或游標有任何改變就加一，畫面用它判斷要不要重畫。</summary>
        public int Version { get; private set; }
        public bool CanUndo => undo.Count > 0;
        public bool CanRedo => redo.Count > 0;

        /// <summary>只在文字或樣式改變時觸發，單純移動游標不會。</summary>
        public event Action Changed;

        /// <summary>
        /// 一般可見字元；擋掉控制字元、emoji 這類代理對，以及 TMP 會併進前一個字的變體選擇符（U+FE00–FE0F），
        /// 否則畫面上的字和 buffer 的逐字索引會對不上。
        /// </summary>
        public static bool Printable(char c) => !char.IsControl(c) && !char.IsSurrogate(c) && (c < '︀' || c > '️');

        public TextStyle StyleAt(int index) => styles[index];

        public TextStyle[] Styles(int start, int length) => styles.GetRange(start, length).ToArray();

        /// <summary>整個換掉內容並清空復原紀錄，游標移到最後。</summary>
        public void Reset(string value, IList<TextStyle> valueStyles = null)
        {
            text.Clear();
            styles.Clear();
            Filter(value, valueStyles, MaxLength, text, styles);
            Caret = Anchor = text.Length;
            undo.Clear();
            redo.Clear();
            lastEdit = Edit.None;
            TypingStyle = StyleBefore(Caret);
            Touch(true);
        }

        /// <summary>打一個字；連續打字合併成一步復原，以「一個字加上後面的空白」為單位。</summary>
        public void Type(char c)
        {
            bool space = char.IsWhiteSpace(c);
            bool split = HasSelection || (lastEdit == Edit.Typing && lastTypedSpace && !space);
            if (Insert(c.ToString(), null, Edit.Typing, split)) lastTypedSpace = space;
        }

        /// <summary>接下來打的字另起一步復原，例如輸入法每次送出的字。</summary>
        public void BreakTypingGroup()
        {
            if (lastEdit == Edit.Typing) lastEdit = Edit.None;
        }

        /// <summary>貼上或用程式插入（取代選取範圍）；自成一步復原。</summary>
        public void Insert(string value, IList<TextStyle> valueStyles = null) => Insert(value, valueStyles, Edit.Other, true);

        public void Backspace(bool word = false)
        {
            if (HasSelection)
            {
                DeleteSelection();
                return;
            }
            if (Caret == 0) return;
            int start = word ? PreviousWordStart(Caret) : Caret - 1;
            BeginEdit(Edit.Deleting);
            Remove(start, Caret - start);
            Caret = Anchor = start;
            Touch(true);
        }

        public void Delete(bool word = false)
        {
            if (HasSelection)
            {
                DeleteSelection();
                return;
            }
            if (Caret == Length) return;
            int end = word ? NextWordStart(Caret) : Caret + 1;
            BeginEdit(Edit.Deleting);
            Remove(Caret, end - Caret);
            Touch(true);
        }

        public void DeleteSelection()
        {
            if (!HasSelection) return;
            BeginEdit(Edit.Other);
            RemoveSelection();
            Touch(true);
        }

        /// <returns>剪下的文字；沒有選取時回傳空字串。</returns>
        public string Cut()
        {
            var cut = SelectedText;
            DeleteSelection();
            return cut;
        }

        public void MoveTo(int index, bool extend = false)
        {
            Caret = Clamp(index);
            if (!extend) Anchor = Caret;
            lastEdit = Edit.None;
            TypingStyle = HasSelection ? styles[SelectionStart] : StyleBefore(Caret);
            Touch(false);
        }

        public void Select(int anchor, int caret)
        {
            Anchor = Clamp(anchor);
            MoveTo(caret, true);
        }

        public void SelectAll() => Select(0, Length);

        /// <summary>雙擊選字：選取 index 所在的一段同類字元（不跨行）。</summary>
        public void SelectWordAt(int index)
        {
            if (Length == 0) return;
            int probe = Math.Min(Clamp(index), Length - 1);
            if (text[probe] == '\n' && probe > 0) probe--;
            var kind = KindOf(text[probe]);
            int start = probe, end = probe + 1;
            while (start > 0 && text[start - 1] != '\n' && KindOf(text[start - 1]) == kind) start--;
            while (end < Length && text[end] != '\n' && KindOf(text[end]) == kind) end++;
            Select(start, end);
        }

        public void MoveLeft(bool extend = false, bool word = false)
        {
            if (HasSelection && !extend && !word) MoveTo(SelectionStart);
            else MoveTo(word ? PreviousWordStart(Caret) : Caret - 1, extend);
        }

        public void MoveRight(bool extend = false, bool word = false)
        {
            if (HasSelection && !extend && !word) MoveTo(SelectionEnd);
            else MoveTo(word ? NextWordStart(Caret) : Caret + 1, extend);
        }

        /// <summary>Ctrl+← 與 Ctrl+Backspace 的停點：先跳過左邊的空白，再跳過一整段同類字元。</summary>
        public int PreviousWordStart(int index)
        {
            int i = Clamp(index);
            while (i > 0 && KindOf(text[i - 1]) == CharKind.Space) i--;
            if (i == 0) return 0;
            var kind = KindOf(text[i - 1]);
            while (i > 0 && KindOf(text[i - 1]) == kind) i--;
            return i;
        }

        /// <summary>Ctrl+→ 與 Ctrl+Delete 的停點（Windows 慣例）：跳過一整段同類字元，再跳過後面的空白。</summary>
        public int NextWordStart(int index)
        {
            int i = Clamp(index);
            if (i < Length && KindOf(text[i]) != CharKind.Space)
            {
                var kind = KindOf(text[i]);
                while (i < Length && KindOf(text[i]) == kind) i++;
            }
            while (i < Length && KindOf(text[i]) == CharKind.Space) i++;
            return i;
        }

        /// <summary>index 所在這一行（以換行字元分行）的開頭。</summary>
        public int LineStart(int index)
        {
            int i = Clamp(index);
            while (i > 0 && text[i - 1] != '\n') i--;
            return i;
        }

        /// <summary>index 所在這一行的結尾（換行字元之前）。</summary>
        public int LineEnd(int index)
        {
            int i = Clamp(index);
            while (i < Length && text[i] != '\n') i++;
            return i;
        }

        /// <summary>選取範圍裡每個字都有這個樣式；沒有選取時看 <see cref="TypingStyle"/>。工具列按鈕用它決定亮不亮。</summary>
        public bool Has(TextStyle style)
        {
            if (!HasSelection) return (TypingStyle & style) != 0;
            for (int i = SelectionStart; i < SelectionEnd; i++)
                if ((styles[i] & style) == 0) return false;
            return true;
        }

        /// <summary>選取範圍全都有這個樣式就拿掉，否則全部加上；沒有選取時只切換接下來打字的樣式。</summary>
        public void ToggleStyle(TextStyle style)
        {
            bool on = !Has(style);
            if (HasSelection)
            {
                BeginEdit(Edit.Other);
                for (int i = SelectionStart; i < SelectionEnd; i++)
                    styles[i] = on ? styles[i] | style : styles[i] & ~style;
            }
            TypingStyle = on ? TypingStyle | style : TypingStyle & ~style;
            Touch(HasSelection);
        }

        public bool Undo()
        {
            if (undo.Count == 0) return false;
            redo.Add(Capture());
            Restore(Pop(undo));
            return true;
        }

        public bool Redo()
        {
            if (redo.Count == 0) return false;
            undo.Add(Capture());
            Restore(Pop(redo));
            return true;
        }

        bool Insert(string value, IList<TextStyle> valueStyles, Edit kind, bool split)
        {
            var keptText = new StringBuilder();
            var keptStyles = new List<TextStyle>();
            int room = MaxLength - (Length - (SelectionEnd - SelectionStart));
            Filter(value, valueStyles, room, keptText, keptStyles);
            if (keptText.Length == 0) return false;
            BeginEdit(kind, split);
            RemoveSelection();
            text.Insert(Caret, keptText.ToString());
            styles.InsertRange(Caret, keptStyles);
            Caret += keptText.Length;
            Anchor = Caret;
            Touch(true);
            return true;
        }

        void Filter(string value, IList<TextStyle> valueStyles, int room, StringBuilder keptText, List<TextStyle> keptStyles)
        {
            for (int i = 0; i < value.Length && keptText.Length < room; i++)
            {
                char c = value[i];
                if (c == '\r')
                {
                    if (i + 1 < value.Length && value[i + 1] == '\n') continue;
                    c = '\n';
                }
                if (c == '\n' && !Multiline) c = ' ';
                if (c != '\n' && !accepts(c)) continue;
                keptText.Append(c);
                keptStyles.Add(valueStyles != null && i < valueStyles.Count ? valueStyles[i] : TypingStyle);
            }
        }

        void BeginEdit(Edit kind, bool split = false)
        {
            if (split || kind != lastEdit || kind == Edit.Other)
            {
                undo.Add(Capture());
                if (undo.Count > MaxUndo) undo.RemoveAt(0);
            }
            redo.Clear();
            lastEdit = kind;
        }

        void RemoveSelection()
        {
            if (!HasSelection) return;
            int start = SelectionStart;
            Remove(start, SelectionEnd - start);
            Caret = Anchor = start;
        }

        void Remove(int start, int count)
        {
            text.Remove(start, count);
            styles.RemoveRange(start, count);
        }

        Snapshot Capture() => new Snapshot { Text = text.ToString(), Styles = styles.ToArray(), Caret = Caret, Anchor = Anchor };

        void Restore(Snapshot snapshot)
        {
            text.Clear().Append(snapshot.Text);
            styles.Clear();
            styles.AddRange(snapshot.Styles);
            Caret = snapshot.Caret;
            Anchor = snapshot.Anchor;
            lastEdit = Edit.None;
            TypingStyle = HasSelection ? styles[SelectionStart] : StyleBefore(Caret);
            Touch(true);
        }

        static Snapshot Pop(List<Snapshot> stack)
        {
            var top = stack[stack.Count - 1];
            stack.RemoveAt(stack.Count - 1);
            return top;
        }

        TextStyle StyleBefore(int index) =>
            index > 0 ? styles[index - 1] : Length > 0 ? styles[0] : TypingStyle;

        int Clamp(int index) => Math.Max(0, Math.Min(Length, index));

        static CharKind KindOf(char c) =>
            char.IsWhiteSpace(c) ? CharKind.Space
            : char.IsLetterOrDigit(c) || c == '_' ? CharKind.Word
            : CharKind.Other;

        void Touch(bool content)
        {
            Version++;
            if (content) Changed?.Invoke();
        }
    }
}

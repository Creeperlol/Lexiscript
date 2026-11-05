using NUnit.Framework;
using UnityEngine.InputSystem;

namespace Lexiscript
{
    public sealed class TextEditBufferTests
    {
        static TextEditBuffer Letters() => new TextEditBuffer(Puzzle.IsLetter, 8);

        static TextEditBuffer Notes(string text = "")
        {
            var buffer = new TextEditBuffer(c => c == '\t' || TextEditBuffer.Printable(c), 1000, multiline: true);
            buffer.Reset(text);
            return buffer;
        }

        static void Type(TextEditBuffer buffer, string text)
        {
            foreach (var c in text) buffer.Type(c);
        }

        [SetUp]
        public void UseLocalClipboard() => TextClipboard.UseSystem = false;

        [TearDown]
        public void RestoreClipboard() => TextClipboard.UseSystem = true;

        [Test]
        public void TypingInsertsAtTheCaret()
        {
            var buffer = Letters();
            Type(buffer, "aple");
            buffer.MoveTo(1);
            buffer.Type('p');
            Assert.AreEqual("apple", buffer.Text);
            Assert.AreEqual(2, buffer.Caret);
        }

        [Test]
        public void RejectedCharactersAndOverflowAreDropped()
        {
            var buffer = Letters();
            Type(buffer, "ab1 c-d");
            Assert.AreEqual("abcd", buffer.Text);
            buffer.Insert("efghijk");
            Assert.AreEqual("abcdefgh", buffer.Text);
            Assert.AreEqual(8, buffer.Caret);
        }

        [Test]
        public void RejectedCharacterDoesNotEraseTheSelection()
        {
            var buffer = Letters();
            Type(buffer, "abc");
            buffer.SelectAll();
            buffer.Type('1');
            Assert.AreEqual("abc", buffer.Text);
            Assert.IsTrue(buffer.HasSelection);
        }

        [Test]
        public void PastingReplacesTheSelectionAndNormalisesNewlines()
        {
            var buffer = Notes("hello world");
            buffer.Select(6, 11);
            buffer.Insert("there\r\nfriend");
            Assert.AreEqual("hello there\nfriend", buffer.Text);

            var line = new TextEditBuffer(TextEditBuffer.Printable, 100);
            line.Insert("a\r\nb\nc");
            Assert.AreEqual("a b c", line.Text);
        }

        [Test]
        public void BackspaceAndDeleteWorkOnCharactersWordsAndSelections()
        {
            var buffer = Notes("one two three");
            buffer.Backspace();
            Assert.AreEqual("one two thre", buffer.Text);
            buffer.Backspace(word: true);
            Assert.AreEqual("one two ", buffer.Text);
            buffer.MoveTo(0);
            buffer.Delete(word: true);
            Assert.AreEqual("two ", buffer.Text);
            buffer.Delete();
            Assert.AreEqual("wo ", buffer.Text);
            buffer.Select(0, 2);
            buffer.Backspace();
            Assert.AreEqual(" ", buffer.Text);
        }

        [Test]
        public void ArrowsMoveAndExtendTheSelection()
        {
            var buffer = Notes("hello world");
            buffer.MoveLeft(word: true);
            Assert.AreEqual(6, buffer.Caret);
            buffer.MoveLeft(extend: true, word: true);
            Assert.AreEqual("hello ", buffer.SelectedText);
            buffer.MoveRight();
            Assert.IsFalse(buffer.HasSelection);
            Assert.AreEqual(6, buffer.Caret);
            buffer.MoveRight(extend: true);
            buffer.MoveRight(extend: true);
            Assert.AreEqual("wo", buffer.SelectedText);
            buffer.MoveLeft();
            Assert.AreEqual(6, buffer.Caret);
            buffer.MoveRight(word: true);
            Assert.AreEqual(11, buffer.Caret);
        }

        [Test]
        public void LinesAndWordSelection()
        {
            var buffer = Notes("first line\nsecond line");
            Assert.AreEqual(11, buffer.LineStart(15));
            Assert.AreEqual(22, buffer.LineEnd(15));
            Assert.AreEqual(10, buffer.LineEnd(3));
            buffer.SelectWordAt(13);
            Assert.AreEqual("second", buffer.SelectedText);
        }

        [Test]
        public void UndoGroupsTypingByWordAndRedoReplaysIt()
        {
            var buffer = Notes();
            Type(buffer, "hello world");
            Assert.IsTrue(buffer.Undo());
            Assert.AreEqual("hello ", buffer.Text);
            Assert.AreEqual(6, buffer.Caret);
            Assert.IsTrue(buffer.Undo());
            Assert.AreEqual("", buffer.Text);
            Assert.IsFalse(buffer.Undo());
            Assert.IsTrue(buffer.Redo());
            Assert.AreEqual("hello ", buffer.Text);
            buffer.Type('x');
            Assert.IsFalse(buffer.Redo());
        }

        [Test]
        public void MovingTheCaretStartsANewUndoStep()
        {
            var buffer = Notes();
            Type(buffer, "ab");
            buffer.MoveTo(0);
            Type(buffer, "cd");
            buffer.Undo();
            Assert.AreEqual("ab", buffer.Text);
        }

        [Test]
        public void EachImeCommitIsItsOwnUndoStep()
        {
            var buffer = Notes();
            Type(buffer, "今天天氣很好。");
            buffer.BreakTypingGroup();
            Type(buffer, "明天去公園。");
            buffer.Undo();
            Assert.AreEqual("今天天氣很好。", buffer.Text, "中文沒有空白，一次 Ctrl+Z 只退回最後一次送出的字");
        }

        [Test]
        public void VariationSelectorsAreNotPrintable()
        {
            // TMP 會把 U+FE0F 併進前一個字，留著的話畫面上的字和 buffer 索引就差一格
            var buffer = Notes();
            buffer.Insert("❤️ab");
            Assert.AreEqual("❤ab", buffer.Text);
        }

        [Test]
        public void StylesFollowTheSelectionAndTheTypingStyle()
        {
            var buffer = Notes("plain bold");
            buffer.Select(6, 10);
            buffer.ToggleStyle(TextStyle.Bold);
            Assert.AreEqual(TextStyle.Bold, buffer.StyleAt(6));
            Assert.AreEqual(TextStyle.None, buffer.StyleAt(5));
            Assert.IsTrue(buffer.Has(TextStyle.Bold));

            buffer.MoveTo(10);
            Assert.AreEqual(TextStyle.Bold, buffer.TypingStyle);
            buffer.Type('!');
            Assert.AreEqual(TextStyle.Bold, buffer.StyleAt(10));

            buffer.ToggleStyle(TextStyle.Bold);
            buffer.ToggleStyle(TextStyle.Italic);
            buffer.Type('?');
            Assert.AreEqual(TextStyle.Italic, buffer.StyleAt(11));

            buffer.Select(0, 12);
            buffer.ToggleStyle(TextStyle.Underline);
            buffer.ToggleStyle(TextStyle.Underline);
            Assert.AreEqual(TextStyle.None, buffer.StyleAt(0) & TextStyle.Underline);
            Assert.IsTrue(buffer.Undo());
            Assert.AreEqual(TextStyle.Underline, buffer.StyleAt(0));
        }

        [Test]
        public void ChangedFiresForContentButNotForCaretMoves()
        {
            var buffer = Notes("abc");
            int changes = 0;
            buffer.Changed += () => changes++;
            int version = buffer.Version;
            buffer.MoveLeft();
            Assert.AreEqual(0, changes);
            Assert.Greater(buffer.Version, version);
            buffer.Type('x');
            Assert.AreEqual(1, changes);
        }

        [Test]
        public void StandardKeysCopyCutPasteAndUndo()
        {
            var buffer = new TextEditBuffer(TextEditBuffer.Printable, 100);
            buffer.Insert("cut me");
            Assert.IsTrue(TextEditKeys.Apply(buffer, Key.A, false, true));
            Assert.IsTrue(TextEditKeys.Apply(buffer, Key.C, false, true));
            Assert.AreEqual("cut me", TextClipboard.Text);
            TextEditKeys.Apply(buffer, Key.X, false, true);
            Assert.AreEqual("", buffer.Text);
            TextEditKeys.Apply(buffer, Key.V, false, true);
            TextEditKeys.Apply(buffer, Key.V, false, true);
            Assert.AreEqual("cut mecut me", buffer.Text);
            TextEditKeys.Apply(buffer, Key.Z, false, true);
            Assert.AreEqual("cut me", buffer.Text);
            TextEditKeys.Apply(buffer, Key.Z, true, true);
            Assert.AreEqual("cut mecut me", buffer.Text);
            TextEditKeys.Apply(buffer, Key.Home, true, false);
            Assert.AreEqual("cut mecut me", buffer.SelectedText);
            Assert.IsFalse(TextEditKeys.Apply(buffer, Key.A, false, false));
        }
    }
}

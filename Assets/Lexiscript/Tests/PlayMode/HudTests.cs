using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lexiscript
{
    public sealed class HudTests
    {
        readonly InputHarness input = new InputHarness();

        [SetUp]
        public void SetUp() => input.SetUp();

        [UnityTearDown]
        public IEnumerator TearDown() => input.TearDown();

        [UnityTest]
        public IEnumerator NotesEditLikeATextFileAndRememberTheirSettings()
        {
            SceneManager.LoadScene("Demo");
            yield return null;
            yield return null;
            var notes = Hud.Instance.Notes;
            var buffer = notes.Buffer;
            var body = Object.FindAnyObjectByType<Player>().GetComponent<Rigidbody2D>();
            Assert.IsFalse(notes.Minimized);

            yield return input.Click(InputHarness.Screen(notes.Viewport));
            Assert.IsTrue(notes.IsFocused);
            input.Type("white: same");
            yield return input.Tap(Key.Enter);
            input.Type("black: <b>x</b>");
            Assert.AreEqual("white: same\nblack: <b>x</b>", buffer.Text);

            var y0 = body.position.y;
            input.Hold(Key.W);
            yield return new WaitForSeconds(0.2f);
            input.Release();
            yield return null;
            Assert.Less(Mathf.Abs(body.position.y - y0), 0.01f, "在筆記打字時不能移動");

            yield return input.Tap(Key.LeftCtrl, Key.A);
            InputHarness.Shot("15_notes_selection", Camera.main.transform.position, Art.CameraSize, hud: true);
            yield return input.Tap(Key.LeftCtrl, Key.B);
            Assert.AreEqual(TextStyle.Bold, buffer.StyleAt(0));
            yield return null;
            StringAssert.Contains("<b>", notes.TextComponent.text);
            StringAssert.Contains("<b>x</b>", InputHarness.Parsed(notes.TextComponent), "玩家打的 < 原樣顯示，不會被當成標籤");
            yield return input.Tap(Key.LeftCtrl, Key.Z);
            Assert.AreEqual(TextStyle.None, buffer.StyleAt(0));

            yield return input.Click(InputHarness.Screen(notes.ItalicButton));
            Assert.IsTrue(notes.IsFocused, "按工具列不會讓筆記失焦");
            Assert.AreEqual(TextStyle.Italic, buffer.StyleAt(0), "選取範圍套用斜體");
            yield return input.Tap(Key.LeftCtrl, Key.C);
            yield return input.Tap(Key.LeftCtrl, Key.End);
            yield return input.Tap(Key.LeftCtrl, Key.V);
            Assert.AreEqual("white: same\nblack: <b>x</b>white: same\nblack: <b>x</b>", buffer.Text);
            Assert.AreEqual(TextStyle.Italic, buffer.StyleAt(buffer.Length - 1), "從筆記複製的文字貼回來保留樣式");

            yield return input.Tap(Key.UpArrow);
            Assert.AreEqual(12, buffer.LineStart(buffer.Caret), "上鍵移到上一行");
            Assert.Greater(buffer.Caret, 12, "上鍵保持差不多的水平位置");
            yield return input.Tap(Key.LeftCtrl, Key.End);

            input.Type("筆記");
            StringAssert.Contains("筆記", buffer.Text, "可以打中文");
            input.Compose("ㄅ");
            yield return null;
            yield return null;
            StringAssert.Contains("<u>ㄅ</u>", notes.TextComponent.text, "組字中的注音顯示在游標處");
            int length = buffer.Length;
            yield return input.Tap(Key.Enter);
            Assert.AreEqual(length, buffer.Length, "選字用的 Enter 不能變成換行");
            input.Compose("");
            yield return null;
            yield return null;
            yield return null;

            input.Point(InputHarness.Screen(notes.Viewport));
            var zoom = Camera.main.GetComponent<CameraZoom>().TargetSize;
            input.Hold(Key.LeftCtrl);
            yield return null;
            yield return input.Scroll(0.4f);
            yield return input.Scroll(0.4f);
            Assert.AreEqual(NotesPanel.DefaultFontSize, notes.FontSize, "觸控板的零碎捲動量累積滿一格才調字體");
            yield return input.Scroll(0.4f);
            input.Release();
            yield return null;
            Assert.AreEqual(NotesPanel.DefaultFontSize + NotesPanel.FontStep, notes.FontSize, "Ctrl+滾輪放大字體");
            yield return input.Scroll(-1);
            Assert.AreEqual(zoom, Camera.main.GetComponent<CameraZoom>().TargetSize, "滑鼠在筆記上時滾輪不會縮放鏡頭");

            yield return input.Click(InputHarness.Screen(notes.OpacityInput));
            Assert.IsTrue(notes.OpacityFocused);
            input.Type("35");
            yield return input.Tap(Key.Enter);
            Assert.AreEqual(35, notes.Opacity);
            Assert.AreEqual(0.35f, notes.PageAlpha, 1e-3f);
            yield return input.Click(InputHarness.Screen(notes.OpacityInput));
            input.Type("7");
            yield return input.Tap(Key.Escape);
            Assert.AreEqual(35, notes.Opacity, "Esc 取消");
            yield return input.Click(InputHarness.Screen(notes.OpacityInput));
            input.Type("250");
            yield return input.Tap(Key.Enter);
            Assert.AreEqual(100, notes.Opacity, "超過 100% 就用 100%");

            yield return input.Click(InputHarness.Screen(notes.MinimizeButton));
            Assert.IsTrue(notes.Minimized);
            Assert.IsFalse(notes.IsFocused);
            InputHarness.Shot("11_notes_minimized", Camera.main.transform.position, Art.CameraSize, hud: true);
            yield return input.Click(InputHarness.Screen((RectTransform)notes.transform));
            Assert.IsFalse(notes.Minimized);

            yield return new WaitForSeconds(1.2f);
            Assert.IsTrue(File.Exists(input.NotesPath), "筆記自動存檔");
            StringAssert.Contains("white: same", File.ReadAllText(input.NotesPath));

            var board = Object.FindAnyObjectByType<Board>();
            var p = board.Blocks.Single(block => block.name == "P");
            yield return input.Click(InputHarness.Screen(notes.Viewport));
            Assert.IsTrue(notes.IsFocused);
            yield return input.Click(p);
            Assert.AreEqual(p, board.Focused);
            Assert.IsFalse(notes.IsFocused, "點方塊作答時筆記失焦");
            input.Type("abc");
            InputHarness.Shot("12_notes_and_answer", Camera.main.transform.position, Art.CameraSize, hud: true);
            yield return input.Click(InputHarness.Screen(notes.MinimizeButton));
            Assert.IsNull(board.Focused, "點筆記不拿焦點的地方也算離開答案欄");
            Assert.AreEqual("", board.Puzzle.Input("P"), "離開答案欄就清掉輸入");
            Assert.IsTrue(notes.Minimized);

            // 設定都改成非預設值，重新載入場景後要原樣還原
            yield return input.Click(InputHarness.Screen((RectTransform)notes.transform));
            yield return input.Click(InputHarness.Screen(notes.OpacityInput));
            input.Type("35");
            yield return input.Tap(Key.Enter);
            yield return input.Click(InputHarness.Screen(notes.MinimizeButton));
            yield return new WaitForSeconds(1.2f);
            var text = buffer.Text;
            var styles = buffer.Styles(0, text.Length);
            SceneManager.LoadScene("Demo");
            yield return null;
            yield return null;
            var reloaded = Hud.Instance.Notes;
            Assert.AreNotSame(notes, reloaded);
            Assert.AreEqual(text, reloaded.Buffer.Text, "筆記內容");
            CollectionAssert.AreEqual(styles, reloaded.Buffer.Styles(0, text.Length), "逐字樣式");
            Assert.AreEqual(35, reloaded.Opacity);
            Assert.AreEqual(NotesPanel.DefaultFontSize + NotesPanel.FontStep, reloaded.FontSize);
            Assert.IsTrue(reloaded.Minimized);
        }

        [UnityTest]
        public IEnumerator NotesCaretFollowsTheLinesOnScreen()
        {
            SceneManager.LoadScene("Demo");
            yield return null;
            yield return null;
            var notes = Hud.Instance.Notes;
            var buffer = notes.Buffer;
            var text = notes.TextComponent;
            Vector2 At(int line, float x)
            {
                text.ForceMeshUpdate();
                var info = text.textInfo.lineInfo[line];
                Canvas.ForceUpdateCanvases();
                return RectTransformUtility.WorldToScreenPoint(null, text.rectTransform.TransformPoint(new Vector3(x, (info.ascender + info.descender) / 2)));
            }

            yield return input.Click(InputHarness.Screen(notes.Viewport));
            input.Type("hi");
            yield return input.Tap(Key.Enter);
            input.Type("hello world");
            // 隔久一點再點，才不會被當成雙擊
            yield return new WaitForSeconds(0.4f);
            text.ForceMeshUpdate();
            yield return input.Click(At(0, text.textInfo.characterInfo[1].xAdvance + 150));
            Assert.AreEqual(2, buffer.Caret, "點短行右邊的空白，游標停在那一行行尾，不會跑到下一行");

            yield return new WaitForSeconds(0.4f);
            text.ForceMeshUpdate();
            float onO = text.textInfo.characterInfo[7].xAdvance - 1;
            yield return input.Click(At(1, onO));
            yield return input.Click(At(1, onO));
            Assert.AreEqual("hello", buffer.SelectedText, "雙擊字尾字母的右半邊，選到的是那個字而不是後面的空白");

            // 中文沒有空白，自動換行在字和字之間：End 停在這一行行尾，再按也不會跑到下一行
            yield return new WaitForSeconds(0.4f);
            yield return input.Tap(Key.LeftCtrl, Key.End);
            yield return input.Tap(Key.Enter);
            int start = buffer.Length;
            input.Type(new string('字', 40));
            yield return null;
            text.ForceMeshUpdate();
            int first = text.textInfo.characterInfo[start].lineNumber;
            var line = text.textInfo.lineInfo[first];
            Assert.Greater(text.textInfo.characterInfo[buffer.Length - 1].lineNumber, first, "這段要夠長才會自動換行");
            yield return input.Click(At(first, 2));
            Assert.AreEqual(start, buffer.Caret);
            yield return input.Tap(Key.End);
            Assert.AreEqual(line.lastCharacterIndex + 1, buffer.Caret, "End 到這一行行尾");
            yield return input.Tap(Key.End);
            Assert.AreEqual(line.lastCharacterIndex + 1, buffer.Caret, "再按 End 還是這一行行尾");
            yield return input.Tap(Key.Home);
            Assert.AreEqual(start, buffer.Caret, "Home 回到同一行開頭，不是下一行");

            // 結尾連續換行：TMP 的高度不算這些空行，游標仍要捲得到、看得見
            yield return input.Tap(Key.LeftCtrl, Key.End);
            TextClipboard.Text = new string('\n', 30);
            yield return input.Tap(Key.LeftCtrl, Key.V);
            yield return null;
            Canvas.ForceUpdateCanvases();
            var caretCorners = new Vector3[4];
            var viewCorners = new Vector3[4];
            notes.Caret.GetWorldCorners(caretCorners);
            notes.Viewport.GetWorldCorners(viewCorners);
            Assert.GreaterOrEqual(caretCorners[0].y, viewCorners[0].y - 0.5f, "游標在可見範圍內");
            Assert.LessOrEqual(caretCorners[1].y, viewCorners[1].y + 0.5f, "游標在可見範圍內");
        }

        [UnityTest]
        public IEnumerator HeldMoveKeyIsNotTypedIntoTheFieldItFocused()
        {
            SceneManager.LoadScene("Demo");
            yield return null;
            Hud.Instance.Notes.SetMinimized(true);
            var chat = Hud.Instance.Chat;

            input.Hold(Key.W);
            yield return null;
            input.Type("/");
            Assert.IsTrue(chat.IsOpen);
            input.Type("ww");
            Assert.AreEqual("/", chat.Input.Text, "打開聊天列時就按著的 W，系統連發的 w 不會打進去");
            input.Hold(Key.W, Key.A);
            yield return null;
            input.Type("a");
            Assert.AreEqual("/a", chat.Input.Text, "之後才按下的鍵照常打字");
            input.Release();
            yield return null;
            input.Hold(Key.W);
            yield return null;
            input.Type("w");
            Assert.AreEqual("/aw", chat.Input.Text, "放開後再按一次 W 就照常打字");
            input.Release();
            yield return null;
            yield return input.Tap(Key.Escape);

            var board = Object.FindAnyObjectByType<Board>();
            var p = board.Blocks.Single(block => block.name == "P");
            input.Hold(Key.D);
            yield return null;
            yield return input.Click(p);
            Assert.AreEqual(p, board.Focused);
            input.Type("d");
            Assert.AreEqual("", board.Field.Text, "點方塊時就按著的 D 不會打進答案欄");
            input.Release();
            yield return null;
            input.Hold(Key.D);
            yield return null;
            input.Type("d");
            Assert.AreEqual("d", board.Field.Text, "放開後再按一次 D 就照常打字");
            input.Release();
            yield return null;
        }

        [UnityTest]
        public IEnumerator ChatRunsCommandsAndEchoesEverythingElse()
        {
            SceneManager.LoadScene("Demo");
            yield return null;
            var chat = Hud.Instance.Chat;
            var body = Object.FindAnyObjectByType<Player>().GetComponent<Rigidbody2D>();
            string Last() => chat.Messages.Last();

            input.Type("/");
            Assert.IsTrue(chat.IsOpen, "沒在打字時按 / 打開聊天列");
            Assert.AreEqual("/", chat.Input.Text);
            input.Type("showanswer");
            var y0 = body.position.y;
            input.Hold(Key.W);
            yield return new WaitForSeconds(0.2f);
            input.Release();
            yield return null;
            Assert.Less(Mathf.Abs(body.position.y - y0), 0.01f, "聊天列打開時不能移動");
            yield return input.Tap(Key.Enter);
            Assert.IsFalse(chat.IsOpen);
            Assert.AreEqual("Answer: Byzantium (perpetual)", Last(), "顯示離玩家最近的方塊答案");
            InputHarness.Shot("13_chat_showanswer", Camera.main.transform.position, Art.CameraSize, hud: true);

            input.Type("/hello");
            yield return input.Tap(Key.Enter);
            Assert.AreEqual("/hello", Last(), "不是指令就原樣送出");

            input.Type("/");
            yield return input.Tap(Key.Backspace);
            input.Type("hi 你好 <b>");
            yield return input.Tap(Key.Enter);
            Assert.AreEqual("hi 你好 <b>", Last());
            Assert.IsTrue(chat.GetComponentsInChildren<TMPro.TextMeshProUGUI>().Any(text => InputHarness.Parsed(text).Contains("hi 你好 <b>")),
                "玩家打的 < 原樣顯示，不會被當成標籤");
            int count = chat.Messages.Count();

            input.Type("/oops");
            yield return input.Tap(Key.Escape);
            Assert.IsFalse(chat.IsOpen);
            Assert.AreEqual(count, chat.Messages.Count(), "Esc 不送出");

            input.Type("/");
            yield return input.Tap(Key.UpArrow);
            Assert.AreEqual("hi 你好 <b>", chat.Input.Text, "上鍵叫出上一句");
            yield return input.Tap(Key.UpArrow);
            Assert.AreEqual("/hello", chat.Input.Text);
            yield return input.Tap(Key.DownArrow);
            yield return input.Tap(Key.DownArrow);
            Assert.AreEqual("/", chat.Input.Text, "回到最底還原草稿");
            yield return input.Click(InputHarness.Screen((Vector2)body.position + new Vector2(2, -2)));
            Assert.IsFalse(chat.IsOpen, "點畫面其他地方關閉聊天列");

            Assert.Greater(chat.VisibleLineCount, 0);
            yield return new WaitForSeconds(ChatConsole.VisibleSeconds + 0.3f);
            Assert.AreEqual(0, chat.VisibleLineCount, "一段時間沒有新訊息就淡出");

            for (int i = 0; i < 40; i++) chat.Post($"message {i}");
            input.Type("/");
            yield return null;
            yield return null;
            float third = Hud.Instance.Root.rect.height / 3;
            Assert.LessOrEqual(chat.UsedHeight, third + 0.5f, "聊天區不超過畫面高度三分之一");
            Assert.Less(chat.VisibleLineCount, 40);
            InputHarness.Shot("14_chat_open", Camera.main.transform.position, Art.CameraSize, hud: true);
            yield return input.Tap(Key.Escape);
        }
    }
}

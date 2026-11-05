using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Lexiscript
{
    /// <summary>筆記的透明度、拖曳位置，以及打字焦點對系統輸入法的開關。</summary>
    public sealed class NotesTests
    {
        readonly InputHarness input = new InputHarness();

        [SetUp]
        public void SetUp() => input.SetUp();

        [UnityTearDown]
        public IEnumerator TearDown() => input.TearDown();

        [UnityTest]
        public IEnumerator OpacityOnlyFadesThePaperSoTheWorldShowsThrough()
        {
            SceneManager.LoadScene("Demo");
            yield return null;
            yield return null;
            var notes = Hud.Instance.Notes;
            yield return input.Click(InputHarness.Screen(notes.Viewport));
            input.Type("see-through notes");

            yield return SetOpacity(notes, 0);
            Assert.AreEqual(0, notes.PageAlpha, 1e-3f);
            CollectionAssert.IsEmpty(VisibleBehindText(notes), "0% 時文字區後面不能墊著任何看得見的東西（以前整塊黑色外框墊在紙底下）");
            Assert.AreEqual(1f, Part(notes, "Toolbar").color.a, "工具列維持不透明");
            InputHarness.Shot("41_notes_opacity_0", Camera.main.transform.position, Art.CameraSize, hud: true);

            yield return SetOpacity(notes, 40);
            CollectionAssert.AreEqual(new[] { "Page" }, VisibleBehindText(notes), "文字區後面只有那張紙");
            var paper = Part(notes, "Page").color;
            Assert.AreEqual(Color.white, new Color(paper.r, paper.g, paper.b), "降低百分比是讓白紙變透明，不是變灰變黑");
            Assert.AreEqual(0.4f, paper.a, 1e-3f);
            InputHarness.Shot("42_notes_opacity_40", Camera.main.transform.position, Art.CameraSize, hud: true);

            yield return SetOpacity(notes, 100);
            Assert.AreEqual(1, notes.PageAlpha, 1e-3f);
        }

        [UnityTest]
        public IEnumerator ToolbarDragsTheNotesAroundAndTheSpotIsRemembered()
        {
            SceneManager.LoadScene("Demo");
            yield return null;
            yield return null;
            var notes = Hud.Instance.Notes;
            var panel = (RectTransform)notes.transform;
            var home = ScreenRect(panel);
            Assert.Greater(home.xMin, 100, "Game view 太小，測不到拖曳");
            Assert.Greater(Screen.height - home.yMax, 20, "Game view 太矮，測不到拖曳");

            var delta = new Vector2(-home.xMin / 2, (Screen.height - home.yMax) / 2);
            var grip = InputHarness.Screen(notes.Grip);
            yield return Drag(grip, grip + delta);
            var dragged = ScreenRect(panel);
            Assert.AreEqual(home.x + delta.x, dragged.x, 1, "筆記跟著滑鼠走");
            Assert.AreEqual(home.y + delta.y, dragged.y, 1);
            Assert.IsNull(TextFocus.Current, "拖曳不會讓筆記拿到打字焦點");
            InputHarness.Shot("43_notes_dragged", Camera.main.transform.position, Art.CameraSize, hud: true);

            var bold = InputHarness.Screen(notes.BoldButton);
            yield return Drag(bold, bold + new Vector2(150, -150));
            Assert.Less(Vector2.Distance(dragged.position, ScreenRect(panel).position), 1, "從粗體鈕上拖不會移動筆記");
            var opacity = InputHarness.Screen(notes.OpacityInput);
            yield return Drag(opacity, opacity + new Vector2(150, -150));
            Assert.Less(Vector2.Distance(dragged.position, ScreenRect(panel).position), 1, "從透明度欄上拖不會移動筆記");
            yield return input.Tap(Key.Escape);

            grip = InputHarness.Screen(notes.Grip);
            yield return Drag(grip, grip + new Vector2(-10000, 10000));
            var corner = ScreenRect(panel);
            Assert.AreEqual(0, corner.xMin, 1, "拖不出畫面左緣");
            Assert.AreEqual(Screen.height, corner.yMax, 1, "拖不出畫面上緣");

            yield return input.Click(InputHarness.Screen(notes.MinimizeButton));
            Assert.IsTrue(notes.Minimized);
            var tab = ScreenRect(panel);
            Assert.AreEqual(home.xMax, tab.xMax, 1, "縮小後固定停在右下角");
            Assert.AreEqual(home.yMin, tab.yMin, 1);
            InputHarness.Shot("44_notes_minimized_after_drag", Camera.main.transform.position, Art.CameraSize, hud: true);
            yield return input.Click(InputHarness.Screen(panel));
            Assert.IsFalse(notes.Minimized);
            Assert.Less(Vector2.Distance(corner.position, ScreenRect(panel).position), 1, "展開後回到拖過的位置");

            yield return new WaitForSeconds(1.2f);
            StringAssert.Contains("\"hasOffset\":true", File.ReadAllText(input.NotesPath), "位置存進筆記的存檔");
            SceneManager.LoadScene("Demo");
            yield return null;
            yield return null;
            panel = (RectTransform)Hud.Instance.Notes.transform;
            Assert.Less(Vector2.Distance(corner.position, ScreenRect(panel).position), 1, "重新載入後還在拖過的位置");

            // 同樣的畫面只剩一半的 Canvas 單位，等於把視窗縮小：拖到左上角的筆記會超出畫面
            var scaler = Hud.Instance.Root.GetComponent<CanvasScaler>();
            var reference = scaler.referenceResolution;
            scaler.referenceResolution = reference / 2;
            yield return null;
            yield return null;
            var squeezed = ScreenRect(panel);
            Assert.GreaterOrEqual(squeezed.xMin, -1, "視窗變小時筆記被推回畫面內");
            Assert.LessOrEqual(squeezed.yMax, Screen.height + 1, "工具列還在畫面內");
            scaler.referenceResolution = reference;
            yield return null;
            yield return null;
            Assert.Less(Vector2.Distance(corner.position, ScreenRect(panel).position), 1, "視窗變回來就回到原處");
        }

        [UnityTest]
        public IEnumerator OldSavesWithoutAPositionOpenInTheDefaultCorner()
        {
            File.WriteAllText(input.NotesPath, "{\"text\":\"old notes\",\"styles\":\"\",\"opacity\":80,\"fontSize\":30.0,\"minimized\":false}");
            SceneManager.LoadScene("Demo");
            yield return null;
            yield return null;
            var notes = Hud.Instance.Notes;
            Assert.AreEqual("old notes", notes.Buffer.Text);
            Assert.AreEqual(80, notes.Opacity);
            Assert.AreEqual(30f, notes.FontSize);
            var position = ((RectTransform)notes.transform).anchoredPosition;
            Assert.Less(Vector2.Distance(NotesPanel.DefaultOffset, position), 0.01f, "沒有位置欄位的舊存檔放在預設的右下角");
        }

        [UnityTest]
        public IEnumerator SystemImeIsOnOnlyWhileTypingInFieldsThatTakeChinese()
        {
            SceneManager.LoadScene("Demo");
            yield return null;
            yield return null;
            var notes = Hud.Instance.Notes;
            var chat = Hud.Instance.Chat;
            Assert.IsFalse(TextFocus.ImeEnabled, "走動時不開輸入法，WASD 才不會被注音吃掉");

            yield return input.Click(InputHarness.Screen(notes.Viewport));
            Assert.IsTrue(notes.IsFocused);
            Assert.IsTrue(TextFocus.ImeEnabled);
            Assert.AreEqual(IMECompositionMode.On, Input.imeCompositionMode,
                "只開 Input System 的專案也要把舊版的輸入法模式設成 On，Windows 的注音才會打開");

            Input.imeCompositionMode = IMECompositionMode.Auto;
            yield return null;
            Assert.AreEqual(IMECompositionMode.On, Input.imeCompositionMode, "編輯器切換視窗等把模式改回 Auto 時，下一幀補回來");

            yield return input.Click(InputHarness.Screen(notes.OpacityInput));
            Assert.IsTrue(notes.OpacityFocused);
            Assert.IsFalse(TextFocus.ImeEnabled, "透明度欄只收數字，開著注音會把 1 打成ㄅ");
            Assert.AreEqual(IMECompositionMode.Auto, Input.imeCompositionMode);
            yield return input.Tap(Key.Escape);

            var board = Object.FindAnyObjectByType<Board>();
            var p = board.Blocks.Single(block => block.name == "P");
            yield return input.Click(p);
            Assert.AreEqual(p, board.Focused);
            Assert.IsFalse(TextFocus.ImeEnabled, "答案欄只收英文字母");
            Assert.AreEqual(IMECompositionMode.Auto, Input.imeCompositionMode);
            yield return input.Tap(Key.Escape);
            Assert.IsNull(TextFocus.Current);

            input.Type("/");
            Assert.IsTrue(chat.IsOpen);
            Assert.AreEqual(IMECompositionMode.On, Input.imeCompositionMode, "聊天列也能打中文");
            yield return input.Tap(Key.Escape);
            Assert.IsFalse(chat.IsOpen);
            Assert.IsFalse(TextFocus.ImeEnabled);
            Assert.AreEqual(IMECompositionMode.Auto, Input.imeCompositionMode, "沒有焦點時還給 Unity 預設的 Auto");

            yield return input.Click(InputHarness.Screen(notes.Viewport));
            Assert.AreEqual(IMECompositionMode.On, Input.imeCompositionMode);
            SceneManager.LoadScene("Demo");
            yield return null;
            yield return null;
            Assert.IsFalse(TextFocus.ImeEnabled, "換場景時關掉輸入法");
            Assert.AreEqual(IMECompositionMode.Auto, Input.imeCompositionMode, "離開時還原，編輯器停止 Play 後不會一直開著注音");
        }

        IEnumerator SetOpacity(NotesPanel notes, int percent)
        {
            yield return input.Click(InputHarness.Screen(notes.OpacityInput));
            Assert.IsTrue(notes.OpacityFocused);
            input.Type(percent.ToString());
            yield return input.Tap(Key.Enter);
            Assert.AreEqual(percent, notes.Opacity);
        }

        /// <summary>按住左鍵從 from 分幾幀拖到 to 再放開。</summary>
        IEnumerator Drag(Vector2 from, Vector2 to)
        {
            InputSystem.QueueStateEvent(input.Mouse, new MouseState { position = from }.WithButton(MouseButton.Left));
            yield return null;
            for (int step = 1; step <= 4; step++)
            {
                InputSystem.QueueStateEvent(input.Mouse, new MouseState { position = Vector2.Lerp(from, to, step / 4f) }.WithButton(MouseButton.Left));
                yield return null;
            }
            InputSystem.QueueStateEvent(input.Mouse, new MouseState { position = to });
            yield return null;
        }

        static Graphic Part(NotesPanel notes, string name) => notes.GetComponentsInChildren<Graphic>().Single(graphic => graphic.name == name);

        /// <summary>筆記裡疊在文字區後面、而且看得見的圖；文字區自己的字與游標不算。</summary>
        static string[] VisibleBehindText(NotesPanel notes)
        {
            var view = ScreenRect(notes.Viewport);
            return notes.GetComponentsInChildren<Graphic>()
                .Where(graphic => graphic.enabled && graphic.color.a > 0.001f && !graphic.transform.IsChildOf(notes.Viewport))
                .Where(graphic => Overlaps(ScreenRect(graphic.rectTransform), view))
                .Select(graphic => graphic.name)
                .ToArray();
        }

        /// <summary>重疊超過一個像素；只是邊貼著邊不算。</summary>
        static bool Overlaps(Rect a, Rect b) => a.xMin < b.xMax - 1 && a.xMax > b.xMin + 1 && a.yMin < b.yMax - 1 && a.yMax > b.yMin + 1;

        static Rect ScreenRect(RectTransform rect)
        {
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}

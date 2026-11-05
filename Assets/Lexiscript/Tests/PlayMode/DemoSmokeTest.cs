using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lexiscript
{
    public sealed class DemoSmokeTest
    {
        readonly InputHarness input = new InputHarness();

        [SetUp]
        public void SetUp() => input.SetUp();

        [UnityTearDown]
        public IEnumerator TearDown() => input.TearDown();

        [UnityTest]
        public IEnumerator DemoScene_PlaysEndToEnd()
        {
            SceneManager.LoadScene("Demo");
            yield return null;

            var board = Object.FindAnyObjectByType<Board>();
            var body = Object.FindAnyObjectByType<Player>().GetComponent<Rigidbody2D>();
            Assert.AreEqual(29, board.Blocks.Length);
            Block Find(string id) => board.Blocks.Single(block => block.name == id);
            string Shown(Block block) => InputHarness.Parsed(block.answerText);
            // batchmode 的畫面很小，展開的筆記會蓋住右下角的方塊
            Hud.Instance.Notes.SetMinimized(true);
            Debug.Log($"Screen {Screen.width}x{Screen.height}");

            InputHarness.Shot("01_overview_start", new Vector2(10.5f, 10.5f), 12.5f);
            InputHarness.Shot("04_sunflower_start", new Vector2(6.25f, 12.5f), 3.4f);
            InputHarness.Shot("05_colorless_start", new Vector2(17.5f, 18.75f), 3.4f);

            float y0 = body.position.y;
            input.Hold(Key.W);
            yield return new WaitForSeconds(0.25f);
            Assert.Greater(body.position.y - y0, 0.5f, "W 應該讓角色往上走");
            input.Release();
            // 排入的放開事件要到下一幀的 input update 才處理；不等的話，接著點擊會以為 W 還按著而擋下輸入
            yield return null;

            float x0 = body.position.x;
            input.Hold(Key.D);
            yield return new WaitForSeconds(0.25f);
            float walked = body.position.x - x0;
            input.Hold(Key.D, Key.LeftShift);
            x0 = body.position.x;
            yield return new WaitForSeconds(0.25f);
            float sprinted = body.position.x - x0;
            input.Release();
            yield return null;
            Assert.Greater(sprinted, walked * 1.5f, "按住 Shift 應該跑得比較快");

            yield return input.Click(Find("G"));
            Assert.IsNull(board.Focused, "純色格不能聚焦");

            var p = Find("P");
            StringAssert.AreEqualIgnoringCase("_________(9)", Shown(p));
            yield return input.Click(p);
            Assert.AreEqual(p, board.Focused);
            Assert.AreEqual("_", Shown(p), "聚焦後只剩游標，提示字收起來");
            input.Type("Eternal");
            Assert.AreEqual("Eternal", board.Field.Text);
            Assert.AreEqual("Eternal", board.Puzzle.Input("P"), "輸入照玩家打的大小寫");
            Assert.IsFalse(board.Puzzle.IsSolved("P"));
            CollectionAssert.Contains(Board.HintPhrases, board.HintPhrase);
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(board.HintPhrase, Hud.Instance.Hint.Phrase, "提示句顯示在畫面上方");
            InputHarness.Shot("02_chain_typing", new Vector2(5f, 20f), 3.4f, hud: true);

            y0 = body.position.y;
            input.Hold(Key.W);
            yield return new WaitForSeconds(0.25f);
            input.Release();
            yield return null;
            Assert.Less(Mathf.Abs(body.position.y - y0), 0.01f, "聚焦時按 W 不能移動");

            yield return input.Tap(Key.LeftArrow, 3);
            Assert.AreEqual(4, board.Field.Caret);
            input.Type("x");
            Assert.AreEqual("Eterxnal", board.Field.Text);
            yield return input.Tap(Key.Backspace);
            yield return input.Tap(Key.Home);
            yield return input.Tap(Key.Delete);
            Assert.AreEqual("ternal", board.Field.Text);
            yield return input.Tap(Key.LeftCtrl, Key.Z);
            Assert.AreEqual("Eternal", board.Field.Text, "Ctrl+Z 復原");
            yield return input.Tap(Key.LeftCtrl, Key.A);
            InputHarness.Shot("02b_field_selection", new Vector2(5f, 20f), 2.2f);
            yield return input.Tap(Key.LeftCtrl, Key.C);
            Assert.AreEqual("Eternal", TextClipboard.Text);
            yield return input.Tap(Key.End);
            yield return input.Tap(Key.LeftCtrl, Key.V);
            Assert.AreEqual("EternalEternal", board.Field.Text);

            yield return input.Click(InputHarness.Screen(new Vector2(12.5f, 15f)));
            Assert.IsNull(board.Focused, "點空地就離開");
            Assert.AreEqual("", board.Puzzle.Input("P"), "沒答對就清掉");
            Assert.AreEqual("", board.HintPhrase);
            StringAssert.AreEqualIgnoringCase("_________(9)", Shown(p));

            yield return input.Click(p, answerHalf: false);
            Assert.AreEqual(Reveal.First, board.RevealOf(p));
            Assert.AreEqual("B________(9)", Shown(p));
            yield return input.Click(p, answerHalf: false, MouseButton.Right);
            Assert.AreEqual("________m(9)", Shown(p), "右鍵切換成字尾，不會同時顯示字首");
            yield return input.Click(p, answerHalf: false, MouseButton.Right);
            Assert.AreEqual(Reveal.None, board.RevealOf(p));
            yield return input.Click(p, answerHalf: false);

            yield return input.Click(p);
            Assert.AreEqual("_", Shown(p));
            input.Type("apex");
            yield return input.Tap(Key.Enter);
            Assert.IsNull(board.Focused, "答錯按 Enter 離開");
            Assert.AreEqual("B________(9)", Shown(p), "回到聚焦前的字首提示");

            yield return input.Click(p);
            input.Type("eternal");
            yield return input.Tap(Key.Enter);
            Assert.IsNull(board.Focused);
            Assert.AreEqual("", board.Puzzle.Input("P"), "打的是提示答案也一樣清掉");
            CollectionAssert.Contains(Board.HintPhrases, board.HintPhrase, "提示句多留一下，打完直接按 Enter 也看得到");
            yield return new WaitForSeconds(Board.HintLingerSeconds + 0.2f);
            Assert.AreEqual("", board.HintPhrase);

            yield return input.Click(p);
            input.Type("bYzAnTiUm");
            Assert.IsTrue(board.Puzzle.IsSolved("P"));
            Assert.IsNull(board.Focused);
            Assert.AreEqual("Byzantium", Shown(p), "答對後換成答案的標準大小寫");
            Assert.AreEqual(Art.Correct, p.answerText.color, "答對變綠色");
            Assert.AreEqual("", board.HintPhrase);

            var wizard = Find("WIZARD");
            yield return input.Click(wizard);
            input.Hold(Key.D);
            yield return null;
            var before = body.position;
            input.Type("lizard");
            Assert.IsTrue(board.Puzzle.IsSolved("WIZARD"));
            Assert.IsNull(board.Focused);
            yield return new WaitForSeconds(0.25f);
            Assert.Less((body.position - before).magnitude, 0.01f, "解題的 D 還按著時不能移動");
            input.Release();
            yield return null;
            Assert.IsTrue(TextFocus.MovementAllowed);

            foreach (var block in board.Blocks.Where(block => board.Puzzle.IsAnswerable(block.name)))
            {
                yield return input.Click(block);
                input.Type(block.answer.ToLowerInvariant());
            }
            Assert.IsTrue(board.Blocks.All(block => block.IsPure || board.Puzzle.IsSolved(block.name)));
            Assert.IsTrue(board.Blocks.All(block => block.IsPure || Shown(block) == block.answer));

            InputHarness.Shot("03_chain_solved", new Vector2(5f, 20f), 3.4f);
            InputHarness.Shot("06_overview_solved", new Vector2(10.5f, 10.5f), 12.5f);
            InputHarness.Shot("07_colorless_solved", new Vector2(17.5f, 18.75f), 3.4f);
            InputHarness.Shot("08_sunflower_solved", new Vector2(6.25f, 12.5f), 3.4f);
            InputHarness.Shot("09_diagonal_bridge", new Vector2(16.25f, 11.25f), 3.4f);
            InputHarness.Shot("10_singles_solved", new Vector2(11.25f, 3.75f), 5.6f);
        }

        [UnityTest]
        public IEnumerator WheelZoomsAndHoverNamesTheColor()
        {
            SceneManager.LoadScene("Demo");
            yield return null;
            var zoom = Camera.main.GetComponent<CameraZoom>();
            var board = Object.FindAnyObjectByType<Board>();
            float start = zoom.TargetSize;

            input.Point(new Vector2(10, 10));
            yield return input.Scroll(1);
            Assert.Less(zoom.TargetSize, start, "滾輪往上拉近");
            yield return input.Scroll(-1);
            yield return input.Scroll(-1);
            Assert.Greater(zoom.TargetSize, start, "滾輪往下拉遠");
            for (int i = 0; i < 40; i++) yield return input.Scroll(-3);
            Assert.AreEqual(Art.MaxCameraSize, zoom.TargetSize, 1e-3f);
            for (int i = 0; i < 40; i++) yield return input.Scroll(3);
            Assert.AreEqual(Art.MinCameraSize, zoom.TargetSize, 1e-3f);
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(Art.MinCameraSize, Camera.main.orthographicSize, 0.05f, "鏡頭平滑追上目標大小");

            var p = board.Blocks.Single(block => block.name == "P");
            var label = Hud.Instance.ColorLabel;
            input.Point(InputHarness.Screen((Vector2)p.transform.position + new Vector2(0, 0.9f)));
            yield return null;
            yield return null;
            Assert.AreEqual("PureWhite", label.ColorName, "指著色框顯示顏色名稱");
            input.Point(InputHarness.Screen(p.transform.position));
            yield return null;
            yield return null;
            Assert.IsNull(label.ColorName, "指著深灰內面不顯示");
            var g = board.Blocks.Single(block => block.name == "G");
            input.Point(InputHarness.Screen(g.transform.position));
            yield return null;
            yield return null;
            Assert.AreEqual("GoldenBrown", label.ColorName, "純色格整格都算色框");
        }
    }
}

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
    public sealed class SolvedCounterTests
    {
        readonly InputHarness input = new InputHarness();

        [SetUp]
        public void SetUp() => input.SetUp();

        [UnityTearDown]
        public IEnumerator TearDown() => input.TearDown();

        [UnityTest]
        public IEnumerator CounterRollsToTheNewTotalWhenAPuzzleIsSolved()
        {
            SceneManager.LoadScene("Demo");
            yield return null;
            yield return null;
            // batchmode 的畫面很小，展開的筆記會蓋住右下角的方塊
            Hud.Instance.Notes.SetMinimized(true);
            var counter = Hud.Instance.Counter;
            var board = Object.FindAnyObjectByType<Board>();
            Block Find(string id) => board.Blocks.Single(block => block.name == id);
            string Shown() => InputHarness.Parsed(counter.Number);

            Assert.AreEqual(0, counter.Count);
            Assert.IsFalse(counter.Animating, "場景載入時直接顯示目前題數，不播動畫");
            Assert.AreEqual("000", Shown(), "不足三位補 0");
            Assert.AreEqual(1f, counter.Alpha, 1e-3f);
            InputHarness.Shot("60_hud_counter", Camera.main.transform.position, Art.CameraSize, hud: true);

            yield return input.Click(Find("L"));
            input.Type("constant");
            Assert.IsTrue(board.Puzzle.IsSolved("L"));
            yield return null;
            Assert.AreEqual(1, counter.Count);
            Assert.IsTrue(counter.Animating, "玩家解開題目時播換數字的動畫，不是直接換字");
            Assert.Less(counter.Number.rectTransform.anchoredPosition.y, -1f, "新數字從下方進來");
            yield return new WaitForSeconds(0.12f);
            InputHarness.Shot("61_hud_counter_rolling", Camera.main.transform.position, Art.CameraSize, hud: true);
            yield return new WaitForSeconds(0.6f);
            Assert.IsFalse(counter.Animating);
            Assert.AreEqual("001", Shown());
            Assert.AreEqual(0f, counter.Number.rectTransform.anchoredPosition.y, 0.01f, "動畫結束後數字停在定位");

            // 連續解開時跟著跳到最新的題數
            yield return input.Click(Find("P"));
            input.Type("Byzantium");
            yield return input.Click(Find("Y"));
            input.Type("Constantinople");
            yield return null;
            Assert.AreEqual(3, counter.Count);
            yield return new WaitForSeconds(0.7f);
            Assert.AreEqual("003", Shown());
            InputHarness.Shot("62_hud_counter_solved", Camera.main.transform.position, Art.CameraSize, hud: true);

            // 讀檔還原不經過 Board.Solved：直接換成新數字
            Assert.IsTrue(board.Puzzle.SetInput("MOON", "moon"));
            yield return null;
            Assert.AreEqual(4, counter.Count);
            Assert.IsFalse(counter.Animating, "不是玩家當場解開的變化不播動畫");
            Assert.AreEqual("004", Shown());

            var middle = InputHarness.Screen((RectTransform)counter.transform);
            Assert.IsTrue(Hud.IsPointerOverUi(middle), "顯示中的計數器擋住後面的世界，點擊不能穿透到看不到的方塊");

            GameSettings.ShowSolvedCounter = false;
            yield return null;
            Assert.Greater(counter.Alpha, 0f, "關掉時淡出，不是直接消失");
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(0f, counter.Alpha, 1e-3f, "設定關掉時看不到");
            Assert.IsFalse(Hud.IsPointerOverUi(middle), "關掉之後就不擋點擊");
            GameSettings.ShowSolvedCounter = true;
            yield return new WaitForSeconds(0.45f);
            Assert.AreEqual(1f, counter.Alpha, 1e-3f);
        }

        [UnityTest]
        public IEnumerator RestoredProgressIsShownStraightAwayWithoutTheRoll()
        {
            yield return LoadGame();
            var board = Object.FindAnyObjectByType<Board>();
            foreach (var block in FirstDoor().blocks.Take(2)) yield return Solve(board, block);
            yield return null;
            Assert.AreEqual(2, Hud.Instance.Counter.Count);
            // 進度在變動後約一秒才寫檔
            yield return new WaitForSeconds(1.2f);
            Assert.IsTrue(File.Exists(input.ProgressPath));

            var before = Hud.Instance;
            SceneManager.LoadScene("Game");
            for (int frame = 0; frame < 10 && (Hud.Instance == null || ReferenceEquals(Hud.Instance, before)); frame++) yield return null;
            var counter = Hud.Instance.Counter;
            Assert.AreEqual(2, counter.Count, "新場景一出現就是讀檔還原後的題數，不會先顯示 000");
            Assert.IsFalse(counter.Animating, "讀檔還原的題數不播換數字的動畫");
            Assert.AreEqual("002", InputHarness.Parsed(counter.Number));
            Assert.AreEqual(0f, counter.Number.rectTransform.anchoredPosition.y, 0.01f);
            yield return new WaitForSeconds(0.3f);
            Assert.IsFalse(counter.Animating);

            Hud.Instance.Notes.SetMinimized(true);
            board = Object.FindAnyObjectByType<Board>();
            var next = FirstDoor().blocks.First(block => !board.Puzzle.IsSolved(block.name));
            yield return Solve(board, next);
            yield return null;
            Assert.AreEqual(3, counter.Count);
            Assert.IsTrue(counter.Animating, "讀檔之後當場解開的題目照常播動畫");
        }

        [UnityTest]
        public IEnumerator CounterAndGearKeepClearOfTheLabelsAtTheTop()
        {
            SceneManager.LoadScene("Demo");
            yield return null;
            yield return null;
            Hud.Instance.Notes.SetMinimized(true);
            var hud = Hud.Instance;
            var board = Object.FindAnyObjectByType<Board>();
            Block Find(string id) => board.Blocks.Single(block => block.name == id);

            // 打出提示答案讓提示句出現，再指著名稱最長的顏色之一，畫面正上方的兩個標籤同時出現
            yield return input.Click(Find("SPOON"));
            input.Type("noon");
            input.Point(InputHarness.Screen((Vector2)Find("Y").transform.position + new Vector2(0, 0.9f)));
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual("BrightYellow", hud.ColorLabel.ColorName);
            Assert.IsNotNull(hud.Hint.Phrase);

            var counter = ScreenRect((RectTransform)hud.Counter.transform);
            var gear = ScreenRect(hud.Settings.Gear);
            foreach (var label in new[] { (RectTransform)hud.ColorLabel.transform, (RectTransform)hud.Hint.transform })
            {
                var rect = ScreenRect(label);
                Assert.IsFalse(rect.Overlaps(counter), $"{label.name} 和左上角的總答題數重疊");
                Assert.IsFalse(rect.Overlaps(gear), $"{label.name} 和右上角的設定鈕重疊");
            }
            InputHarness.Shot("69_hud_top_bar", Camera.main.transform.position, Art.CameraSize, hud: true);
            yield return input.Tap(Key.Escape);
        }

        static IEnumerator LoadGame()
        {
            SceneManager.LoadScene("Game");
            yield return null;
            yield return null;
            // batchmode 的畫面很小，展開的筆記會蓋住右下角的方塊
            Hud.Instance.Notes.SetMinimized(true);
        }

        IEnumerator Solve(Board board, Block block)
        {
            yield return input.Click(block);
            input.Type(block.answer);
            Assert.IsTrue(board.Puzzle.IsSolved(block.name), block.name);
        }

        /// <summary>第一間教學房間的門；它的題目一進場景就點得到。</summary>
        static Door FirstDoor() => Object.FindObjectsByType<Door>(FindObjectsSortMode.None).OrderBy(door => door.transform.position.x).First();

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

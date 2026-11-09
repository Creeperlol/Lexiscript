using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Lexiscript
{
    public sealed class TitleTests
    {
        // 換了繪製目標或 Canvas 模式後，鏡頭比例、CanvasScaler 與標題排版在不同的時間點才跟上，多等幾幀讓它們都穩定下來
        const int SettleFrames = 4;

        readonly InputHarness input = new InputHarness();

        [SetUp]
        public void SetUp() => input.SetUp();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // 轉場跨場景保留、不屬於目前場景；留著的話會在下一個測試途中切換場景
            if (TitleTransition.Current != null) Object.Destroy(TitleTransition.Current.gameObject);
            yield return input.TearDown();
        }

        [UnityTest]
        public IEnumerator TypingStartSolvesTheBlockAndEntersTheGame()
        {
            yield return LoadTitle();
            var title = Object.FindAnyObjectByType<TitleScreen>();
            var board = title.Board;
            var block = title.Block;
            Assert.AreEqual(1, board.Blocks.Length);
            Assert.AreEqual((BlockColor.PureWhite, BlockShape.Plain), (block.color, block.shape), "白色第一型：寫出一樣的字");
            Assert.AreEqual(TitleScreen.Word, block.clue);
            Assert.AreSame(block, board.Focused, "一進標題就聚焦在答案欄");
            Assert.IsNull(Object.FindAnyObjectByType<Hud>(), "標題沒有筆記與聊天");

            yield return Until(() => title.IntroFinished, 5);
            Assert.IsTrue(title.IntroFinished);
            yield return Shot("90_title_screen", title.Canvas);

            input.Type("sta");
            Assert.AreEqual("sta", board.Field.Text);
            yield return Shot("91_title_typing", title.Canvas);
            input.Type("rt");
            Assert.IsTrue(board.Puzzle.IsSolved(block.name), "打出 start 就解開");
            Assert.AreEqual(TitleScreen.Word, board.Puzzle.Input(block.name), "答對後顯示題目的寫法");
            Assert.IsTrue(title.Starting);
            input.Type("s");
            Assert.IsNull(board.Focused, "解開後再打字不會重新聚焦到已解開的方塊");
            Assert.AreEqual(TitleScreen.Word, board.Puzzle.Input(block.name));

            yield return new WaitForSecondsRealtime(TitleScreen.SolveDelay / 2);
            Assert.IsNull(TitleTransition.Current, "先讓解題特效播完才轉場");
            Assert.AreEqual("Title", SceneManager.GetActiveScene().name);
            yield return Shot("92_title_solved", title.Canvas);

            yield return Until(() => TitleTransition.Current != null, TitleScreen.SolveDelay + 1);
            var transition = TitleTransition.Current;
            Assert.IsNotNull(transition, "解開後開始轉場");
            yield return new WaitForSecondsRealtime(TitleTransition.SweepSeconds / 2);
            yield return Shot("93_title_transition", title.Canvas, transition.Canvas);

            yield return Until(() => SceneManager.GetActiveScene().name == TitleScreen.NextScene, 5);
            Assert.AreEqual(TitleScreen.NextScene, SceneManager.GetActiveScene().name, "轉場蓋滿畫面後載入正式關卡");
            Assert.IsNotNull(Object.FindAnyObjectByType<Player>());
            Assert.AreSame(transition, TitleTransition.Current, "載入後轉場還在，接著把畫面掃開");
            yield return Until(() => TitleTransition.Current == null, 5);
            Assert.IsNull(TitleTransition.Current, "掃完就銷毀自己");
            yield return null;
            Assert.IsNull(Object.FindAnyObjectByType<TitleTransition>());
        }

        [UnityTest]
        public IEnumerator TypingWithoutFocusFocusesTheAnswerField()
        {
            yield return LoadTitle();
            var title = Object.FindAnyObjectByType<TitleScreen>();
            var board = title.Board;
            var block = title.Block;

            yield return input.Tap(Key.Escape);
            Assert.IsNull(board.Focused, "Esc 離開答案欄");
            input.Type("s");
            Assert.AreSame(block, board.Focused, "沒有焦點時直接打字會自動聚焦");
            Assert.AreEqual("s", board.Field.Text, "第一個字也要進到答案欄");

            yield return input.Click(InputHarness.Screen((Vector2)block.transform.position + new Vector2(-3.5f, 2)));
            Assert.IsNull(board.Focused, "點空地離開答案欄");
            Assert.AreEqual("", board.Puzzle.Input(block.name), "沒答對就清掉");
            input.Type("st");
            Assert.AreSame(block, board.Focused);
            Assert.AreEqual("st", board.Field.Text);

            yield return input.Tap(Key.Escape);
            Assert.IsNull(board.Focused);
            yield return input.Click(block);
            Assert.AreSame(block, board.Focused, "點答案欄也能聚焦");
            Assert.AreEqual("", board.Field.Text);
            Assert.IsFalse(title.Starting);
        }

        [UnityTest]
        public IEnumerator ArrowPointsAtTheAnswerFieldAtAnyResolution()
        {
            yield return LoadTitle();
            var title = Object.FindAnyObjectByType<TitleScreen>();
            yield return Until(() => title.IntroFinished, 5);
            var camera = Camera.main;
            var canvas = title.Canvas;
            foreach (var size in new[] { new Vector2Int(1920, 1080), new Vector2Int(1024, 768), new Vector2Int(2560, 1080) })
            {
                // 測試中改不了視窗大小，改讓鏡頭與介面畫到指定大小的貼圖上
                var target = new RenderTexture(size.x, size.y, 24);
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                for (int i = 0; i < SettleFrames; i++) yield return null;

                float scale = Mathf.Min(size.x / TitleScreen.DesignSize.x, size.y / TitleScreen.DesignSize.y);
                var center = new Vector2(size.x, size.y) / 2;
                Vector3 world = title.Block.transform.position;
                Vector2 block = camera.WorldToScreenPoint(world);
                Assert.Less((block - (center + TitleScreen.BlockOnScreen * scale)).magnitude, 2f, $"{size} 方塊落在構圖上的同一點");
                float unit = camera.WorldToScreenPoint(world + Vector3.right).x - block.x;
                Assert.AreEqual(TitleScreen.DesignSize.y / (2 * TitleScreen.CameraSize) * scale, unit, 0.5f, $"{size} 方塊跟著構圖等比例縮放");
                Vector2 field = camera.WorldToScreenPoint(world + new Vector3(-Art.Half, Art.AnswerPosition.y));
                var tip = title.ArrowTipScreen;
                Assert.AreEqual(field.y, tip.y, 1.5f, $"{size} 箭頭對準答案欄的高度");
                Assert.That(field.x - tip.x, Is.InRange(0.02f * unit, 0.25f * unit), $"{size} 箭頭停在方塊左側、不碰到方塊");

                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                camera.targetTexture = null;
                target.Release();
                Object.Destroy(target);
            }
        }

        static IEnumerator LoadTitle()
        {
            SceneManager.LoadScene("Title");
            yield return null;
            yield return null;
        }

        static IEnumerator Until(Func<bool> condition, float seconds)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        /// <summary>
        /// 標題與轉場的介面是 Overlay Canvas，不會進 render request：拍照前暫時改成跟著鏡頭的 Canvas，
        /// 並讓鏡頭先畫到截圖大小的貼圖上，介面與箭頭才會照截圖的比例排版。
        /// </summary>
        internal static IEnumerator Shot(string name, params Canvas[] canvases)
        {
            var camera = Camera.main;
            var target = new RenderTexture(1920, 1080, 24);
            camera.targetTexture = target;
            foreach (var canvas in canvases)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
            }
            for (int i = 0; i < SettleFrames; i++) yield return null;
            InputHarness.Shot(name, camera.transform.position, camera.orthographicSize);
            foreach (var canvas in canvases)
                if (canvas != null) canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            camera.targetTexture = null;
            target.Release();
            Object.Destroy(target);
        }
    }
}

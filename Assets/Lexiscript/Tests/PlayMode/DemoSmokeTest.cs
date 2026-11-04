using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lexiscript
{
    public sealed class DemoSmokeTest
    {
        InputSettings savedSettings, testSettings;
        Keyboard keyboard;
        Mouse mouse;

        [SetUp]
        public void SetUp()
        {
            savedSettings = InputSystem.settings;
            testSettings = ScriptableObject.CreateInstance<InputSettings>();
            // batchmode 沒有 Game view 焦點；不改的話，排入的按鍵事件會延到 editor update 才處理
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = testSettings;
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // 場景物件每幀都讀 Keyboard.current，先銷毀，否則移除裝置後會一路丟 NullReferenceException
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(root);
            yield return null;
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
            InputSystem.settings = savedSettings;
            Object.Destroy(testSettings);
        }

        [UnityTest]
        public IEnumerator DemoScene_PlaysEndToEnd()
        {
            SceneManager.LoadScene("Demo");
            yield return null;

            var board = Object.FindAnyObjectByType<Board>();
            var body = Object.FindAnyObjectByType<Player>().GetComponent<Rigidbody2D>();
            Assert.AreEqual(29, board.Blocks.Length);
            Block Find(string id) => board.Blocks.Single(block => block.name == id);

            Shot("01_overview_start", new Vector2(13.5f, 13.5f), 16.5f);
            Shot("04_sunflower_start", new Vector2(7.5f, 15f), 4f);
            Shot("05_colorless_start", new Vector2(21f, 22.5f), 4f);

            float y0 = body.position.y;
            Press(Key.W);
            yield return new WaitForSeconds(0.25f);
            Assert.Greater(body.position.y - y0, 0.5f, "W 應該讓角色往上走");
            Release();
            // 排入的放開事件要到下一幀的 input update 才處理；不等的話，接著點擊會以為 W 還按著而擋下輸入
            yield return null;

            yield return Click(Find("G"));
            Assert.IsNull(board.Focused, "純色格不能聚焦");

            var p = Find("P");
            yield return Click(p);
            Assert.AreEqual(p, board.Focused);
            Type("eternal");
            Assert.AreEqual("ETERNAL_", p.answerText.text);
            Assert.IsFalse(board.Puzzle.IsSolved("P"));
            CollectionAssert.Contains(Board.HintPhrases, board.hint.text);
            Shot("02_chain_typing", new Vector2(7.5f, 24f), 4f);

            y0 = body.position.y;
            Press(Key.W);
            yield return new WaitForSeconds(0.25f);
            Release();
            yield return null;
            Assert.Less(Mathf.Abs(body.position.y - y0), 0.01f, "聚焦時按 W 不能移動");

            yield return Backspace(7);
            Assert.AreEqual("_", p.answerText.text);
            Assert.AreEqual("", board.hint.text);

            Type("Constant");
            Assert.IsFalse(board.Puzzle.IsSolved("P"));
            CollectionAssert.Contains(Board.HintPhrases, board.hint.text);
            yield return Backspace(8);

            Type("byzantium");
            Assert.IsTrue(board.Puzzle.IsSolved("P"));
            Assert.IsNull(board.Focused);
            Assert.AreEqual("BYZANTIUM", p.answerText.text);
            Assert.AreEqual("", board.hint.text);

            var wizard = Find("WIZARD");
            yield return Click(wizard);
            Press(Key.D);
            yield return null;
            var before = body.position;
            Type("lizard");
            Assert.IsTrue(board.Puzzle.IsSolved("WIZARD"));
            Assert.IsNull(board.Focused);
            yield return new WaitForSeconds(0.25f);
            Assert.Less((body.position - before).magnitude, 0.01f, "解題的 D 還按著時不能移動");
            Release();
            yield return null;
            Assert.IsTrue(board.CanMove);

            foreach (var block in board.Blocks.Where(block => board.Puzzle.IsAnswerable(block.name)))
            {
                yield return Click(block);
                Type(block.answers[0].ToLowerInvariant());
            }
            Assert.IsTrue(board.Blocks.All(block => block.IsPure || board.Puzzle.IsSolved(block.name)));
            Assert.IsTrue(board.Blocks.All(block => block.answerText.text.Length > 0 || block.IsPure));

            Shot("03_chain_solved", new Vector2(7.5f, 24f), 4f);
            Shot("06_overview_solved", new Vector2(13.5f, 13.5f), 16.5f);
            Shot("07_colorless_solved", new Vector2(21f, 22.5f), 4f);
            Shot("08_sunflower_solved", new Vector2(7.5f, 15f), 4f);
            Shot("09_diagonal_bridge", new Vector2(19.5f, 13.5f), 4f);
            Shot("10_singles_solved", new Vector2(13.5f, 4.5f), 7f);
        }

        IEnumerator Click(Block block)
        {
            Vector2 answerHalf = (Vector2)block.transform.position + new Vector2(0, -Art.Half / 2);
            Vector2 screen = Camera.main.WorldToScreenPoint(answerHalf);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screen }.WithButton(MouseButton.Left));
            yield return null;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = screen });
            yield return null;
        }

        void Type(string text)
        {
            foreach (var c in text) keyboard.OnTextInput(c);
        }

        void Press(Key key) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));

        void Release() => InputSystem.QueueStateEvent(keyboard, new KeyboardState());

        IEnumerator Backspace(int times)
        {
            for (int i = 0; i < times; i++)
            {
                Press(Key.Backspace);
                yield return null;
                Release();
                yield return null;
            }
        }

        static void Shot(string name, Vector2 center, float orthographicSize)
        {
            const int width = 1920, height = 1080;
            var camera = Camera.main;
            var savedPosition = camera.transform.position;
            var savedSize = camera.orthographicSize;
            camera.transform.position = new Vector3(center.x, center.y, savedPosition.z);
            camera.orthographicSize = orthographicSize;
            // TMP 平常只在畫面更新前重建網格，直接送出 render request 時要手動更新
            foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)) text.ForceMeshUpdate();

            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
#if UNITY_EDITOR
            // 第一次用到的 shader 若非同步編譯，截圖會是青色的替代畫面
            var asyncCompilation = UnityEditor.ShaderUtil.allowAsyncCompilation;
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;
#endif
            RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
#if UNITY_EDITOR
            UnityEditor.ShaderUtil.allowAsyncCompilation = asyncCompilation;
#endif

            // ReadPixels 讀 MSAA 貼圖時會自動 resolve
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var active = RenderTexture.active;
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            RenderTexture.active = active;

            var folder = Path.Combine(Application.dataPath, "..", "Artifacts", "Screenshots");
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());

            Object.Destroy(image);
            target.Release();
            Object.Destroy(target);
            camera.transform.position = savedPosition;
            camera.orthographicSize = savedSize;
        }
    }
}

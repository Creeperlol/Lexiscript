using System.Collections;
using System.IO;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Lexiscript
{
    /// <summary>PlayMode 測試共用的虛擬鍵盤滑鼠、點擊與截圖。</summary>
    sealed class InputHarness
    {
        InputSettings savedSettings, testSettings;
        string notesPath, settingsPath, progressPath;
        public Keyboard Keyboard { get; private set; }
        public Mouse Mouse { get; private set; }
        Vector2 pointer;

        public void SetUp()
        {
            savedSettings = InputSystem.settings;
            testSettings = ScriptableObject.CreateInstance<InputSettings>();
            // batchmode 沒有 Game view 焦點；不改的話，排入的按鍵事件會延到 editor update 才處理
            testSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            testSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings = testSettings;
            Keyboard = InputSystem.AddDevice<Keyboard>();
            Mouse = InputSystem.AddDevice<Mouse>();
            TextClipboard.UseSystem = false;
            notesPath = Path.Combine(Path.GetTempPath(), "lexiscript-test-notes.json");
            File.Delete(notesPath);
            NotesPanel.StoragePath = notesPath;
            settingsPath = Path.Combine(Path.GetTempPath(), "lexiscript-test-settings.json");
            File.Delete(settingsPath);
            GameSettings.StoragePath = settingsPath;
            GameSettings.Reload();
            progressPath = Path.Combine(Path.GetTempPath(), "lexiscript-test-progress.json");
            File.Delete(progressPath);
            GameProgress.StoragePath = progressPath;
        }

        public IEnumerator TearDown()
        {
            // 場景物件每幀都讀 Keyboard.current，先銷毀，否則移除裝置後會一路丟 NullReferenceException
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) Object.Destroy(root);
            yield return null;
            InputSystem.RemoveDevice(Keyboard);
            InputSystem.RemoveDevice(Mouse);
            InputSystem.settings = savedSettings;
            Object.Destroy(testSettings);
            TextClipboard.UseSystem = true;
            NotesPanel.StoragePath = null;
            File.Delete(notesPath);
            GameSettings.StoragePath = null;
            File.Delete(settingsPath);
            GameSettings.Reload();
            GameProgress.StoragePath = null;
            File.Delete(progressPath);
        }

        public string NotesPath => notesPath;
        public string SettingsPath => settingsPath;
        public string ProgressPath => progressPath;

        public static Vector2 Screen(Vector2 world) => Camera.main.WorldToScreenPoint(world);

        /// <summary>介面元件中心的螢幕座標；Canvas 平常在畫面更新前才排版，先強制排一次。</summary>
        public static Vector2 Screen(RectTransform rect)
        {
            Canvas.ForceUpdateCanvases();
            return RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
        }

        public void Point(Vector2 screen)
        {
            pointer = screen;
            InputSystem.QueueStateEvent(Mouse, new MouseState { position = screen });
        }

        public IEnumerator Click(Vector2 screen, MouseButton button = MouseButton.Left)
        {
            pointer = screen;
            InputSystem.QueueStateEvent(Mouse, new MouseState { position = screen }.WithButton(button));
            yield return null;
            InputSystem.QueueStateEvent(Mouse, new MouseState { position = screen });
            yield return null;
        }

        /// <summary>點方塊的答案半格（下半）或題目半格（上半）。</summary>
        public IEnumerator Click(Block block, bool answerHalf = true, MouseButton button = MouseButton.Left)
        {
            var offset = new Vector2(0, answerHalf ? -Art.Half / 2 : Art.Half / 2);
            var screen = Screen((Vector2)block.transform.position + offset);
            Canvas.ForceUpdateCanvases();
            Assert.IsFalse(Hud.IsPointerOverUi(screen), $"{block.name} 被畫面介面擋住，點不到");
            yield return Click(screen, button);
        }

        public IEnumerator Scroll(float notches)
        {
            InputSystem.QueueStateEvent(Mouse, new MouseState { position = pointer, scroll = new Vector2(0, notches) });
            yield return null;
            InputSystem.QueueStateEvent(Mouse, new MouseState { position = pointer });
            yield return null;
        }

        public void Type(string text)
        {
            foreach (var c in text) Keyboard.OnTextInput(c);
        }

        public void Hold(params Key[] keys) => InputSystem.QueueStateEvent(Keyboard, new KeyboardState(keys));

        public void Release() => InputSystem.QueueStateEvent(Keyboard, new KeyboardState());

        /// <summary>按一下再放開；按鍵事件要到下一幀的 input update 才處理，所以中間各等一幀。</summary>
        public IEnumerator Tap(params Key[] keys)
        {
            Hold(keys);
            yield return null;
            Release();
            yield return null;
        }

        public IEnumerator Tap(Key key, int times)
        {
            for (int i = 0; i < times; i++) yield return Tap(key);
        }

        public void Compose(string composition)
        {
            var inputEvent = IMECompositionEvent.Create(Keyboard.deviceId, composition, InputState.currentTime);
            InputSystem.QueueEvent(ref inputEvent);
        }

        public static string Parsed(TMP_Text text)
        {
            text.ForceMeshUpdate();
            return text.GetParsedText();
        }

        /// <param name="hud">連畫面介面一起拍：Overlay 介面不會進 render request，拍照時暫時改成跟著鏡頭的 Canvas。</param>
        public static void Shot(string name, Vector2 center, float orthographicSize, bool hud = false)
        {
            const int width = 1920, height = 1080;
            var camera = Camera.main;
            var savedPosition = camera.transform.position;
            var savedSize = camera.orthographicSize;
            camera.transform.position = new Vector3(center.x, center.y, savedPosition.z);
            camera.orthographicSize = orthographicSize;

            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            var canvas = hud && Hud.Instance != null ? Hud.Instance.GetComponentInChildren<Canvas>() : null;
            if (canvas != null)
            {
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                Canvas.ForceUpdateCanvases();
            }
            // TMP 平常只在畫面更新前重建網格，直接送出 render request 時要手動更新
            foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)) text.ForceMeshUpdate();
#if UNITY_EDITOR
            // 第一次用到的 shader 若非同步編譯，截圖會是青色的替代畫面
            var asyncCompilation = UnityEditor.ShaderUtil.allowAsyncCompilation;
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;
#endif
            RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
#if UNITY_EDITOR
            UnityEditor.ShaderUtil.allowAsyncCompilation = asyncCompilation;
#endif
            if (canvas != null)
            {
                camera.targetTexture = null;
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                Canvas.ForceUpdateCanvases();
            }

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

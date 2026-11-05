using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lexiscript
{
    public sealed class SettingsMenuTests
    {
        readonly InputHarness input = new InputHarness();

        [SetUp]
        public void SetUp() => input.SetUp();

        [UnityTearDown]
        public IEnumerator TearDown() => input.TearDown();

        IEnumerator Load(string scene)
        {
            SceneManager.LoadScene(scene);
            yield return null;
            yield return null;
            // batchmode 的畫面很小，展開的筆記會蓋住右下角
            Hud.Instance.Notes.SetMinimized(true);
        }

        IEnumerator OpenSettings()
        {
            var settings = Hud.Instance.Settings;
            var gear = InputHarness.Screen(settings.Gear);
            input.Point(gear);
            yield return new WaitForSeconds(0.3f);
            yield return input.Click(gear);
            Assert.IsTrue(settings.IsOpen, "點設定鈕打開設定");
            // 等進場動畫跑完，各列滑到定位才點得準
            yield return new WaitForSeconds(0.5f);
        }

        IEnumerator Choose(int row, int option) =>
            input.Click(InputHarness.Screen(Hud.Instance.Settings.Option(row, option)));

        [UnityTest]
        public IEnumerator GearAppearsNearTheCornerAndOpensAModalWindow()
        {
            yield return Load("Demo");
            var settings = Hud.Instance.Settings;
            var board = Object.FindAnyObjectByType<Board>();
            var body = Object.FindAnyObjectByType<Player>().GetComponent<Rigidbody2D>();
            var gear = InputHarness.Screen(settings.Gear);

            input.Point(new Vector2(Screen.width / 2f, Screen.height / 2f));
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(0f, settings.GearAlpha, 1e-3f, "滑鼠不在右上角時看不到設定鈕");
            Assert.IsFalse(Hud.IsPointerOverUi(gear), "看不到的設定鈕不擋世界的點擊");

            input.Point(gear);
            yield return null;
            Assert.Less(settings.GearAlpha, 1f, "淡入，不是直接出現");
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(1f, settings.GearAlpha, 1e-3f, "滑鼠靠近右上角時出現");
            Assert.IsTrue(Hud.IsPointerOverUi(gear), "設定鈕顯示時算介面");
            var turned = settings.Gear.Find("Visual/Icon").localEulerAngles.z;
            Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(0, turned)), 10f, "滑過時齒輪轉了一點");
            InputHarness.Shot("63_hud_gear", Camera.main.transform.position, Art.CameraSize, hud: true);

            yield return input.Click(gear);
            Assert.IsTrue(settings.IsOpen);
            Assert.IsTrue(Hud.ModalOpen);
            Assert.AreSame(settings, TextFocus.Current, "設定視窗拿走打字焦點");
            yield return new WaitForSeconds(0.5f);

            var y0 = body.position.y;
            input.Hold(Key.W);
            yield return new WaitForSeconds(0.2f);
            input.Release();
            yield return null;
            Assert.Less(Mathf.Abs(body.position.y - y0), 0.01f, "設定開著時不能移動");
            input.Type("/");
            Assert.IsFalse(Hud.Instance.Chat.IsOpen, "設定開著時打字不會打開聊天列");

            yield return input.Tap(Key.Escape);
            Assert.IsFalse(settings.IsOpen, "Esc 關閉");
            Assert.IsFalse(Hud.ModalOpen);
            Assert.IsNull(TextFocus.Current);
            input.Point(new Vector2(Screen.width / 2f, Screen.height / 2f));
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(0f, settings.GearAlpha, 1e-3f, "滑鼠離開右上角後設定鈕淡出");

            yield return OpenSettings();
            yield return input.Click(InputHarness.Screen(settings.CloseButton));
            Assert.IsFalse(settings.IsOpen, "關閉鈕");
            yield return new WaitForSeconds(0.3f);

            // 把角色移開，讓 Y 出現在畫面左側、面板外面，點那裡的遮罩來關閉
            var y = board.Blocks.Single(block => block.name == "Y");
            body.position = (Vector2)y.transform.position + new Vector2(5.5f, -2);
            yield return new WaitForFixedUpdate();
            yield return null;
            yield return OpenSettings();
            var behind = InputHarness.Screen((Vector2)y.transform.position + new Vector2(0, -Art.Half / 2));
            Assert.IsFalse(Ui.Contains(settings.Panel, behind), "要點的是面板外的遮罩");
            yield return input.Click(behind);
            Assert.IsFalse(settings.IsOpen, "點視窗外關閉");
            Assert.IsNull(board.Focused, "關閉視窗的那一下點擊不會穿透到後面的方塊");
            yield return new WaitForSeconds(0.3f);
            yield return input.Click(behind);
            Assert.AreEqual(y, board.Focused, "視窗關掉後同一個位置點得到方塊");
            yield return input.Tap(Key.Escape);
        }

        [UnityTest]
        public IEnumerator LanguageSwitchesTheMenuAndTheFloorHints()
        {
            yield return Load("Game");
            var settings = Hud.Instance.Settings;
            var floor = Object.FindObjectsByType<LocalizedText>(FindObjectsSortMode.None).First(text => Localization.HasTranslation(text.english));
            var floorText = floor.GetComponent<TMP_Text>();
            Assert.AreEqual(floor.english, floorText.text);

            yield return OpenSettings();
            Assert.AreEqual("SETTINGS", settings.Title.text);
            Assert.AreSame(Fonts.Display, settings.Title.font);
            InputHarness.Shot("64_hud_settings_en", Camera.main.transform.position, Art.CameraSize, hud: true);

            yield return Choose(SettingsMenu.LanguageRow, 1);
            Assert.AreEqual(Language.Chinese, GameSettings.Language, "切換立即生效");
            Assert.IsTrue(settings.IsOpen, "點選項不會關掉設定");
            Assert.AreEqual("設定", settings.Title.text, "設定介面本身換成中文");
            Assert.AreSame(Hud.UiFont, settings.Title.font, "中文用介面字型");
            Assert.AreEqual(Localization.Floor(floor.english), floorText.text, "教學地板說明換成中文");
            Assert.AreNotEqual(floor.english, floorText.text);
            StringAssert.Contains("Chinese", File.ReadAllText(input.SettingsPath), "存進設定檔");
            yield return new WaitForSeconds(0.4f);
            InputHarness.Shot("65_hud_settings_zh", Camera.main.transform.position, Art.CameraSize, hud: true);

            yield return Choose(SettingsMenu.LanguageRow, 0);
            Assert.AreEqual(Language.English, GameSettings.Language);
            Assert.AreEqual("SETTINGS", settings.Title.text);
            Assert.AreSame(Fonts.Display, settings.Title.font);
            Assert.AreEqual(floor.english, floorText.text);
        }

        [UnityTest]
        public IEnumerator CounterAndPlayerLookOptionsChangeTheSettings()
        {
            yield return Load("Demo");
            var settings = Hud.Instance.Settings;
            var counter = Hud.Instance.Counter;
            yield return OpenSettings();

            Assert.IsTrue(GameSettings.ShowSolvedCounter);
            yield return Choose(SettingsMenu.CounterRow, 1);
            Assert.IsFalse(GameSettings.ShowSolvedCounter, "關掉總答題數");
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(0f, counter.Alpha, 1e-3f, "計數器淡出");
            yield return Choose(SettingsMenu.CounterRow, 0);
            Assert.IsTrue(GameSettings.ShowSolvedCounter);

            Assert.AreEqual(PlayerStyle.Pixel, GameSettings.PlayerStyle);
            yield return Choose(SettingsMenu.StyleRow, 1);
            Assert.AreEqual(PlayerStyle.Square, GameSettings.PlayerStyle, "換成原本的小方塊");
            yield return Choose(SettingsMenu.StyleRow, 0);
            Assert.AreEqual(PlayerStyle.Pixel, GameSettings.PlayerStyle, "換回像素小人");
            Assert.IsTrue(settings.IsOpen, "點選項不會關掉設定");
        }

        [UnityTest]
        public IEnumerator ResetAsksForConfirmationFirst()
        {
            yield return Load("Demo");
            var settings = Hud.Instance.Settings;
            int resets = 0;
            settings.ResetProgress = () => resets++;
            yield return OpenSettings();

            yield return input.Click(InputHarness.Screen(settings.ResetButton));
            Assert.IsTrue(settings.Confirming, "先跳出二次確認");
            Assert.AreEqual(0, resets);
            yield return new WaitForSeconds(0.4f);
            InputHarness.Shot("66_hud_reset_confirm", Camera.main.transform.position, Art.CameraSize, hud: true);
            yield return input.Click(InputHarness.Screen(settings.CancelButton));
            Assert.IsFalse(settings.Confirming, "取消回到設定");
            Assert.IsTrue(settings.IsOpen);
            Assert.AreEqual(0, resets, "取消不重置");
            yield return new WaitForSeconds(0.3f);

            yield return input.Click(InputHarness.Screen(settings.ResetButton));
            yield return new WaitForSeconds(0.4f);
            yield return input.Tap(Key.Escape);
            Assert.IsFalse(settings.Confirming, "Esc 取消確認");
            Assert.IsTrue(settings.IsOpen, "Esc 只關掉確認視窗");
            Assert.AreEqual(0, resets);
            yield return new WaitForSeconds(0.3f);

            GameSettings.Language = Language.Chinese;
            yield return input.Click(InputHarness.Screen(settings.ResetButton));
            yield return new WaitForSeconds(0.4f);
            InputHarness.Shot("67_hud_reset_confirm_zh", Camera.main.transform.position, Art.CameraSize, hud: true);
            yield return input.Click(InputHarness.Screen(settings.ConfirmButton));
            Assert.AreEqual(1, resets, "確認才重置");
            Assert.IsFalse(settings.IsOpen);
            Assert.IsFalse(Hud.ModalOpen);
        }

        [UnityTest]
        public IEnumerator ConfirmedResetDeletesTheSavedProgress()
        {
            yield return Load("Game");
            var board = Object.FindAnyObjectByType<Board>();
            var block = Object.FindObjectsByType<Door>(FindObjectsSortMode.None).OrderBy(door => door.transform.position.x).First().blocks[0];
            yield return input.Click(block);
            input.Type(block.answer);
            Assert.IsTrue(board.Puzzle.IsSolved(block.name));
            yield return new WaitForSeconds(1.2f);
            Assert.IsTrue(File.Exists(input.ProgressPath), "先有一份進度存檔");

            var settings = Hud.Instance.Settings;
            Assert.AreEqual(nameof(GameProgress.ResetAll), settings.ResetProgress.Method.Name, "確認後執行的是真正的重置");
            yield return OpenSettings();
            yield return input.Click(InputHarness.Screen(settings.ResetButton));
            yield return new WaitForSeconds(0.4f);
            yield return input.Click(InputHarness.Screen(settings.ConfirmButton));
            Assert.IsFalse(File.Exists(input.ProgressPath), "確認後清掉進度存檔");
            Assert.IsFalse(Hud.ModalOpen);
            yield return null;
            yield return null;
            Assert.IsTrue(board == null, "重新開始：回到標題畫面，或重新載入關卡");
            yield return new WaitForSeconds(1.2f);
            Assert.IsFalse(File.Exists(input.ProgressPath), "舊場景卸載時沒有把進度存回去");
        }

        [UnityTest]
        public IEnumerator GearStaysOnTopOfNotesDraggedIntoTheCorner()
        {
            SceneManager.LoadScene("Demo");
            yield return null;
            yield return null;
            var settings = Hud.Instance.Settings;
            var notes = Hud.Instance.Notes;
            var panel = (RectTransform)notes.transform;
            Assert.IsFalse(notes.Minimized);
            var grip = InputHarness.Screen(notes.Grip);
            yield return Drag(grip, grip + new Vector2(10000, 10000));

            var gear = ScreenRect(settings.Gear);
            var minimize = ScreenRect(notes.MinimizeButton);
            var overlap = Rect.MinMaxRect(Mathf.Max(gear.xMin, minimize.xMin), Mathf.Max(gear.yMin, minimize.yMin),
                Mathf.Min(gear.xMax, minimize.xMax), Mathf.Min(gear.yMax, minimize.yMax));
            Assert.Greater(overlap.width, 1, "筆記拖到右上角時，縮小鈕有一部分墊在設定鈕底下");
            Assert.Greater(overlap.height, 1, "筆記拖到右上角時，縮小鈕有一部分墊在設定鈕底下");
            Assert.Greater(settings.transform.GetSiblingIndex(), notes.transform.GetSiblingIndex(), "設定鈕畫在筆記上面");

            input.Point(overlap.center);
            yield return new WaitForSeconds(0.4f);
            Assert.AreEqual(1f, settings.GearAlpha, 1e-3f, "滑鼠在筆記上也照樣淡入設定鈕");
            InputHarness.Shot("68_hud_gear_over_notes", Camera.main.transform.position, Art.CameraSize, hud: true);
            var placed = ScreenRect(panel);
            yield return input.Click(overlap.center);
            Assert.IsTrue(settings.IsOpen, "點得到墊著筆記的設定鈕");
            Assert.IsFalse(notes.Minimized, "同一下點擊不會按到底下筆記的縮小鈕");
            Assert.IsFalse(notes.IsFocused);
            Assert.Less(Vector2.Distance(placed.position, ScreenRect(panel).position), 1, "筆記沒有被拖動");
            yield return input.Tap(Key.Escape);
            Assert.IsFalse(settings.IsOpen);
            yield return new WaitForSeconds(0.3f);

            // 設定鈕只佔它自己那一塊，縮小鈕露在外面的部分照常點得到
            var uncovered = new Vector2((gear.xMax + minimize.xMax) / 2, minimize.center.y);
            Assert.IsFalse(gear.Contains(uncovered));
            Assert.IsTrue(minimize.Contains(uncovered));
            yield return input.Click(uncovered);
            Assert.IsTrue(notes.Minimized, "沒被設定鈕蓋住的地方照常點得到筆記");
            Assert.IsFalse(settings.IsOpen);
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

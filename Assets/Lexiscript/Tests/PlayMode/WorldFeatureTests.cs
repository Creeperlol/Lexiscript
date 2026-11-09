using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lexiscript
{
    /// <summary>過橋的演出、傳送、返回主畫面與離開遊戲。</summary>
    public sealed class WorldFeatureTests
    {
        readonly InputHarness input = new InputHarness();

        [SetUp]
        public void SetUp() => input.SetUp();

        [UnityTearDown]
        public IEnumerator TearDown() => input.TearDown();

        [UnityTest]
        public IEnumerator TheBridgeIntroPlaysOnceAndTheCommandBringsItBack()
        {
            yield return LoadGame();
            var rooms = Object.FindAnyObjectByType<Rooms>();
            var body = Object.FindAnyObjectByType<Player>().GetComponent<Rigidbody2D>();
            var entry = Object.FindAnyObjectByType<WorldEntry>();
            var zoom = Camera.main.GetComponent<CameraZoom>();
            FinishTutorial(rooms);
            yield return null;
            Assert.IsFalse(Hud.Instance.Teleport.Available, "還沒走進大地圖，看不到傳送鈕");

            body.position = new Vector2(entry.bridge.xMin + 0.6f, 0);
            yield return new WaitForFixedUpdate();
            input.Hold(Key.D);
            for (float until = Time.time + 3; body.position.x < entry.bridge.center.x && Time.time < until;) yield return null;
            yield return new WaitForSeconds(0.4f);
            Assert.Greater(entry.Intensity, 0.3f, "走到橋中間，畫面暗下來");
            Assert.Less(zoom.Multiplier, 0.9f, "鏡頭拉近");
            InputHarness.Shot("90_bridge_crossing", body.position, Camera.main.orthographicSize, hud: true);
            for (float until = Time.time + 4; !entry.hall.Contains(body.position) && Time.time < until;) yield return null;
            input.Release();
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(GameProgress.Current.BridgeIntroPlayed, "走進大廳就算播過");
            Assert.IsTrue(entry.Releasing);
            Assert.Greater(zoom.Multiplier, 1f, "踏進大廳時鏡頭先拉得比平常遠");
            InputHarness.Shot("91_bridge_release", body.position, Camera.main.orthographicSize, hud: true);
            yield return new WaitForSeconds(2.2f);
            Assert.AreEqual(1f, zoom.Multiplier, 0.02f, "最後回到原本的大小");
            Assert.AreEqual(0f, entry.Intensity, 0.01f);
            Assert.IsTrue(GameProgress.Current.WorldReached);
            Assert.IsTrue(Hud.Instance.Teleport.Available, "走進大地圖之後出現傳送鈕");

            body.position = new Vector2(entry.bridge.center.x, 0);
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(0f, entry.Intensity, 0.01f, "播過之後再過橋不會再暗");

            input.Type("/");
            input.Type("refreshanimation");
            yield return input.Tap(Key.Enter);
            Assert.IsFalse(GameProgress.Current.BridgeIntroPlayed, "/refreshanimation 清掉播過的記錄");
            body.position = new Vector2(entry.bridge.xMin + entry.bridge.width * 0.7f, 0);
            yield return new WaitForSeconds(0.8f);
            Assert.Greater(entry.Intensity, 0.3f, "清掉之後再過橋又會播");

            input.Type("/");
            input.Type("devhelp");
            yield return input.Tap(Key.Enter);
            var messages = Hud.Instance.Chat.Messages.ToList();
            foreach (var command in new[] { "/showanswer", "/refreshanimation", "/devhelp" })
                Assert.IsTrue(messages.Any(message => message.StartsWith(command + " ")), $"/devhelp 列出 {command}");
        }

        [UnityTest]
        public IEnumerator TeleportListsUnlockedPlacesAndAsksFirst()
        {
            yield return LoadGame();
            var rooms = Object.FindAnyObjectByType<Rooms>();
            var body = Object.FindAnyObjectByType<Player>().GetComponent<Rigidbody2D>();
            var teleport = Hud.Instance.Teleport;
            FinishTutorial(rooms);
            GameProgress.Current.BridgeIntroPlayed = true;
            var hall = rooms.floors[System.Array.IndexOf(rooms.kinds, RoomKind.Field)];
            body.position = new Vector2(hall.xMin + 1.6f, 0);
            yield return new WaitForFixedUpdate();
            yield return new WaitForSeconds(0.6f);
            Assert.IsTrue(teleport.Available);
            InputHarness.Shot("92_teleport_button", Camera.main.transform.position, Art.CameraSize, hud: true);

            yield return input.Click(InputHarness.Screen(teleport.Button));
            Assert.IsTrue(teleport.IsOpen);
            Assert.IsTrue(Hud.ModalOpen);
            yield return new WaitForSeconds(0.6f);
            InputHarness.Shot("93_teleport_menu", Camera.main.transform.position, Art.CameraSize, hud: true);
            var points = TeleportPoint.All();
            Assert.IsTrue(points[0].Unlocked && points[1].Unlocked, "教學關與大世界起點都已經解鎖");
            Assert.IsTrue(points.Skip(2).All(point => !point.Unlocked), "其他區域還沒解鎖");

            yield return input.Click(InputHarness.Screen(teleport.RowOf(2)));
            Assert.IsFalse(teleport.Confirming, "沒解鎖的地方點了沒反應");
            Assert.IsTrue(GameSettings.ConfirmTeleport, "預設要再確認一次");
            yield return input.Click(InputHarness.Screen(teleport.RowOf(0)));
            Assert.IsTrue(teleport.Confirming);
            yield return new WaitForSeconds(0.4f);
            InputHarness.Shot("94_teleport_confirm", Camera.main.transform.position, Art.CameraSize, hud: true);
            yield return input.Click(InputHarness.Screen(teleport.ConfirmButton));
            Assert.IsFalse(teleport.IsOpen);
            yield return new WaitForSeconds(0.6f);
            Assert.Less(Vector2.Distance(body.position, points[0].transform.position), 0.1f, "傳送到教學關");

            yield return input.Click(InputHarness.Screen(teleport.Button));
            yield return new WaitForSeconds(0.4f);
            yield return input.Click(InputHarness.Screen(teleport.Toggle));
            Assert.IsFalse(GameSettings.ConfirmTeleport, "上方的開關關掉二次確認");
            yield return input.Click(InputHarness.Screen(teleport.RowOf(1)));
            Assert.IsFalse(teleport.Confirming, "關掉之後直接出發");
            Assert.IsFalse(teleport.IsOpen);
            yield return new WaitForSeconds(0.6f);
            Assert.Less(Vector2.Distance(body.position, points[1].transform.position), 0.1f, "傳送到大世界起點");
        }

        [UnityTest]
        public IEnumerator SettingsCanGoBackToTheTitle()
        {
            SceneManager.LoadScene("Demo");
            yield return null;
            yield return null;
            var settings = Hud.Instance.Settings;
            bool returned = false;
            settings.ReturnToTitle = () => returned = true;
            settings.Open();
            yield return new WaitForSeconds(0.6f);
            InputHarness.Shot("95_settings_title_row", Camera.main.transform.position, Art.CameraSize, hud: true);
            yield return input.Click(InputHarness.Screen(settings.TitleButton));
            Assert.IsTrue(returned);
            Assert.IsFalse(settings.IsOpen);
        }

        [UnityTest]
        public IEnumerator TheTitleHasAQuitButton()
        {
            var quit = TitleScreen.QuitGame;
            bool quitted = false;
            TitleScreen.QuitGame = () => quitted = true;
            try
            {
                SceneManager.LoadScene("Title");
                yield return null;
                var title = Object.FindAnyObjectByType<TitleScreen>();
                yield return new WaitForSeconds(TitleScreen.IntroSeconds + 0.3f);
                yield return TitleTests.Shot("96_title_quit", title.Canvas);
                // 截圖時 Canvas 暫時改成跟著鏡頭，換回來後要等排版跟上，按鈕的螢幕位置才是對的
                for (int i = 0; i < 4; i++) yield return null;
                yield return input.Click(InputHarness.Screen(title.QuitButton));
                Assert.IsTrue(quitted);
                Assert.IsFalse(title.Starting, "按離開不會開始遊戲");
            }
            finally
            {
                TitleScreen.QuitGame = quit;
            }
        }

        static IEnumerator LoadGame()
        {
            SceneManager.LoadScene("Game");
            yield return null;
            yield return null;
            Hud.Instance.Notes.SetMinimized(true);
        }

        static void FinishTutorial(Rooms rooms)
        {
            var board = Object.FindAnyObjectByType<Board>();
            foreach (var block in board.Blocks.Where(block => !string.IsNullOrEmpty(block.answer)))
            {
                int floor = System.Array.FindIndex(rooms.floors, f => f.Contains(block.transform.position));
                if (rooms.kinds[floor] == RoomKind.Tutorial) board.RestoreSolved(block);
            }
            foreach (var door in Object.FindObjectsByType<Door>(FindObjectsSortMode.None)) door.SyncInstantly();
        }
    }
}

using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lexiscript
{
    public sealed class GameSmokeTest
    {
        readonly InputHarness input = new InputHarness();

        [SetUp]
        public void SetUp() => input.SetUp();

        [UnityTearDown]
        public IEnumerator TearDown() => input.TearDown();

        [UnityTest]
        public IEnumerator TutorialDoorOpensOnceEnoughBlocksAreSolved()
        {
            SceneManager.LoadScene("Game");
            yield return null;
            yield return null;
            // batchmode 的畫面很小，展開的筆記會蓋住右下角的方塊
            Hud.Instance.Notes.SetMinimized(true);
            var board = Object.FindAnyObjectByType<Board>();
            var rooms = Object.FindAnyObjectByType<Rooms>();
            var body = Object.FindAnyObjectByType<Player>().GetComponent<Rigidbody2D>();
            var door = Object.FindObjectsByType<Door>(FindObjectsSortMode.None).OrderBy(d => d.transform.position.x).First();
            var doorway = rooms.doorways[0];
            var floor = rooms.floors[0];

            for (int i = 0; i < rooms.floors.Length; i++)
            {
                var room = rooms.floors[i];
                InputHarness.Shot($"2{i}_game_room{i + 1}", room.center, Mathf.Max(room.height, room.width * 9 / 16) / 2 + 0.6f);
            }

            Assert.IsFalse(door.IsOpen);
            Assert.AreEqual($"0/{door.required}", door.counter.text);

            var spoiler = Object.FindObjectsByType<Spoiler>(FindObjectsSortMode.None).Single(s => s.block == door.blocks[0]);
            yield return input.Click(InputHarness.Screen(spoiler.transform.position));
            Assert.IsTrue(spoiler.Revealed, "點暴雷條顯示答案");
            Assert.AreEqual(door.blocks[0].answer, spoiler.label.text);

            body.position = new Vector2(doorway.xMin - 1.2f, doorway.center.y);
            yield return new WaitForFixedUpdate();
            input.Hold(Key.D);
            yield return new WaitForSeconds(0.5f);
            input.Release();
            yield return null;
            Assert.Less(body.position.x, doorway.xMin, "門還沒開時走不過去");

            for (int i = 0; i < door.required; i++)
            {
                Assert.IsFalse(door.IsOpen);
                yield return input.Click(door.blocks[i]);
                input.Type(door.blocks[i].answer);
                Assert.IsTrue(board.Puzzle.IsSolved(door.blocks[i].name), door.blocks[i].name);
                yield return null;
                if (i + 1 < door.required) Assert.AreEqual($"{i + 1}/{door.required}", door.counter.text);
            }
            Assert.IsTrue(door.IsOpen, "答對指定題數就開門");
            Assert.IsFalse(door.GetComponent<BoxCollider2D>().enabled);
            yield return new WaitForSeconds(0.3f);
            Assert.Less(door.transform.localScale.y, 0.01f, "門板收起來了");
            InputHarness.Shot("29_door_open", new Vector2(doorway.center.x, doorway.center.y), 4f);

            input.Hold(Key.D);
            yield return new WaitForSeconds(0.6f);
            input.Release();
            yield return null;
            Assert.Greater(body.position.x, doorway.xMax, "門開了就走得過去");
            Assert.IsFalse(floor.Contains(body.position));
        }
    }
}

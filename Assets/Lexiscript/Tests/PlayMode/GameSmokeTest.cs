using System.Collections;
using System.Collections.Generic;
using System.IO;
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
        /// <summary>progress.json 的格式；測試直接寫檔，模擬玩到一半的存檔。</summary>
        [System.Serializable]
        sealed class SaveFile
        {
            public int version = 1;
            public string[] solved = new string[0];
            public string[] firstLetter = new string[0];
            public string[] lastLetter = new string[0];
            public string[] spoilers = new string[0];
            public Vector2 player;
        }

        readonly InputHarness input = new InputHarness();

        [SetUp]
        public void SetUp() => input.SetUp();

        [UnityTearDown]
        public IEnumerator TearDown() => input.TearDown();

        [UnityTest]
        public IEnumerator TutorialDoorOpensOnlyWhenEveryBlockIsSolved()
        {
            yield return LoadGame();
            var board = Object.FindAnyObjectByType<Board>();
            var rooms = Object.FindAnyObjectByType<Rooms>();
            var body = Object.FindAnyObjectByType<Player>().GetComponent<Rigidbody2D>();
            var door = Object.FindObjectsByType<Door>(FindObjectsSortMode.None).OrderBy(d => d.transform.position.x).First();
            var doorway = rooms.doorways[0];
            var floor = rooms.floors[0];
            var next = rooms.floors[1];
            var cover = Covers().Single(c => c.door == door);

            // 拿掉遮罩再拍，才看得到每間教學房間的擺放；大地圖另外在 GatePuzzleOpensItsRoomAndTheCoverLifts 拍
            SetCoversVisible(false);
            for (int i = 0; i < rooms.floors.Length && rooms.kinds[i] == RoomKind.Tutorial; i++)
            {
                var room = rooms.floors[i];
                InputHarness.Shot($"2{i}_game_room{i + 1}", room.center, Frame(room));
            }
            SetCoversVisible(true);
            var coverFrame = new Vector2(doorway.center.x + 5, doorway.center.y - 2);
            InputHarness.Shot("84_world_cover_tutorial", coverFrame, 6.5f);

            Assert.AreEqual(next, cover.area, "第一扇門打開的是第二間的遮罩");
            Assert.IsTrue(cover.Covering);
            Assert.IsTrue(RoomCover.Hides(next.center), "門還沒開，第二間被蓋住");
            Assert.IsFalse(RoomCover.Hides(floor.center), "第一間沒有遮罩");
            Assert.Greater(cover.GetComponent<MeshFilter>().sharedMesh.vertexCount, 0, "遮罩在執行時畫出來");
            var hidden = board.Blocks.First(block => next.Contains(block.transform.position));
            yield return input.Click(hidden);
            Assert.IsNull(board.Focused, "遮住的方塊點不到");

            Assert.IsFalse(door.IsOpen);
            Assert.AreEqual(door.blocks.Length, door.required, "教學門要全部解開才開，不是差一題就開");
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
                Assert.IsFalse(door.IsOpen, $"解開 {i} 題，還差 {door.required - i} 題");
                Assert.IsTrue(cover.Covering);
                yield return input.Click(door.blocks[i]);
                input.Type(door.blocks[i].answer);
                Assert.IsTrue(board.Puzzle.IsSolved(door.blocks[i].name), door.blocks[i].name);
                yield return null;
                if (i + 1 < door.required) Assert.AreEqual($"{i + 1}/{door.required}", door.counter.text);
            }
            Assert.IsTrue(door.IsOpen, "全部答對就開門");
            Assert.IsFalse(door.GetComponent<BoxCollider2D>().enabled);
            Assert.IsTrue(cover.Covering, "遮罩從門那一側掀開，不是一下子消失");

            for (float until = Time.time + 1; cover.Progress < 0.45f && Time.time < until;) yield return null;
            InputHarness.Shot("85_world_cover_tutorial_reveal", coverFrame, 6.5f);
            Assert.Less(door.transform.localScale.y, 0.01f, "門板收起來了");
            InputHarness.Shot("29_door_open", new Vector2(doorway.center.x, doorway.center.y), 4f);

            yield return new WaitForSeconds(RoomCover.RevealSeconds);
            Assert.IsFalse(cover.Covering, "掀開後不再遮住");
            Assert.IsFalse(RoomCover.Hides(next.center), "遮罩收起後點得到裡面的方塊");
            Assert.AreEqual(0, cover.GetComponent<MeshFilter>().sharedMesh.vertexCount);
            yield return input.Click(hidden);
            Assert.AreEqual(hidden, board.Focused);
            yield return input.Tap(Key.Escape);
            Assert.IsNull(board.Focused);

            input.Hold(Key.D);
            yield return new WaitForSeconds(0.6f);
            input.Release();
            yield return null;
            Assert.Greater(body.position.x, doorway.xMax, "門開了就走得過去");
            Assert.IsFalse(floor.Contains(body.position));
        }

        [UnityTest]
        public IEnumerator GatePuzzleOpensItsRoomAndTheCoverLifts()
        {
            yield return LoadGame();
            var board = Object.FindAnyObjectByType<Board>();
            var rooms = Object.FindAnyObjectByType<Rooms>();
            var body = Object.FindAnyObjectByType<Player>().GetComponent<Rigidbody2D>();
            var field = rooms.floors[System.Array.IndexOf(rooms.kinds, RoomKind.Field)];
            Assert.IsTrue(RoomCover.Hides(field.center), "教學還沒結束，大地圖還蓋著");

            FinishTutorial(board, rooms);
            yield return null;
            Assert.IsFalse(RoomCover.Hides(field.center), "教學最後一扇門通往大地圖，打開就掀開");

            var room = rooms.floors[System.Array.IndexOf(rooms.kinds, RoomKind.Gated)];
            var cover = Covers().Single(c => c.area == room);
            var gate = cover.door;
            Assert.IsFalse(gate.IsOpen);
            Assert.IsTrue(RoomCover.Hides(room.center), "門後房間還蓋著");
            Assert.AreEqual(gate.blocks.Length, gate.required);
            Assert.IsTrue(gate.blocks.All(block => field.Contains(block.transform.position)), "門前題目在大地圖這一側");

            var world = Encapsulate(rooms.floors.Where((_, i) => rooms.kinds[i] != RoomKind.Tutorial));
            InputHarness.Shot("80_world_map", world.center, Frame(world) + 1);
            SetCoversVisible(false);
            InputHarness.Shot("81_world_map_layout", world.center, Frame(world) + 1);
            SetCoversVisible(true);

            var doorway = rooms.doorways.Single(d => d.Contains(gate.transform.position));
            var outward = OutOf(room, doorway);
            body.position = doorway.center + outward * 2.5f;
            // 角色有開插值，瞬移後要等兩次物理更新，跟著角色的鏡頭才停下來，點擊換算的位置才不會偏
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return null;
            InputHarness.Shot("82_world_gate", doorway.center + outward * 1.5f, 5.5f);

            foreach (var block in gate.blocks)
            {
                Assert.IsFalse(gate.IsOpen);
                yield return input.Click(block);
                input.Type(block.answer);
                Assert.IsTrue(board.Puzzle.IsSolved(block.name), block.name);
            }
            yield return null;
            Assert.IsTrue(gate.IsOpen, "門前的題目解開就開門");
            Assert.IsTrue(cover.Covering, "遮罩從門那一側掀開");
            for (float until = Time.time + 1; cover.Progress < 0.45f && Time.time < until;) yield return null;
            InputHarness.Shot("83_world_gated_room_reveal", room.center + outward * 1.5f, Frame(room) + 1.5f);

            yield return new WaitForSeconds(RoomCover.RevealSeconds);
            Assert.IsFalse(cover.Covering);
            Assert.IsFalse(RoomCover.Hides(room.center));
            input.Hold(outward.x != 0 ? (outward.x > 0 ? Key.A : Key.D) : (outward.y > 0 ? Key.S : Key.W));
            yield return new WaitForSeconds(1f);
            input.Release();
            yield return null;
            Assert.IsTrue(room.Contains(body.position), "門開了就走得進門後房間");
        }

        [UnityTest]
        public IEnumerator SavedPlayerInsideAnOpenedGatedRoomIsPutBack()
        {
            yield return LoadGame();
            var board = Object.FindAnyObjectByType<Board>();
            var rooms = Object.FindAnyObjectByType<Rooms>();
            var room = rooms.floors[System.Array.IndexOf(rooms.kinds, RoomKind.Gated)];
            var gate = Covers().Single(c => c.area == room).door;
            var doorway = rooms.doorways.Single(d => d.Contains(gate.transform.position));
            // 門口往房間裡兩步：門那一側留了走道，不會卡在牆或方塊裡
            var inside = doorway.center - OutOf(room, doorway) * 2;
            Assert.IsTrue(room.Contains(inside));
            var solved = board.Blocks
                .Where(block => !string.IsNullOrEmpty(block.answer) && rooms.kinds[RoomOf(rooms, block.transform.position)] == RoomKind.Tutorial)
                .Concat(gate.blocks)
                .Select(block => block.name)
                .ToArray();
            File.WriteAllText(input.ProgressPath, JsonUtility.ToJson(new SaveFile { solved = solved, player = inside }));

            yield return LoadGame();
            rooms = Object.FindAnyObjectByType<Rooms>();
            var body = Object.FindAnyObjectByType<Player>().GetComponent<Rigidbody2D>();
            var covers = Covers();
            RoomKind KindOf(RoomCover c) => rooms.kinds[System.Array.IndexOf(rooms.floors, c.area)];
            var cover = covers.Single(c => c.area == room);
            Assert.IsTrue(cover.door.IsOpen, "存檔裡門前題目已經解開");
            Assert.IsFalse(cover.Covering, "讀檔開的門，遮罩直接收起");
            Assert.AreEqual(0, cover.GetComponent<MeshFilter>().sharedMesh.vertexCount, "不播掀開的動畫");
            Assert.IsFalse(covers.Single(c => KindOf(c) == RoomKind.Field).Covering, "教學全解開，大地圖也掀開");
            Assert.IsTrue(covers.Where(c => c != cover && KindOf(c) == RoomKind.Gated).All(c => c.Covering), "其他門後房間還蓋著");
            Assert.IsFalse(RoomCover.Hides(inside));
            Assert.Less(Vector2.Distance(inside, body.position), 0.05f, "站在已經打開的門後房間裡，讀檔後回到原位");
        }

        [UnityTest]
        public IEnumerator ShowAnswerSkipsBlocksStillUnderACover()
        {
            yield return LoadGame();
            var board = Object.FindAnyObjectByType<Board>();
            var rooms = Object.FindAnyObjectByType<Rooms>();
            var body = Object.FindAnyObjectByType<Player>().GetComponent<Rigidbody2D>();
            FinishTutorial(board, rooms);
            yield return null;
            var room = rooms.floors[System.Array.IndexOf(rooms.kinds, RoomKind.Gated)];
            Assert.IsTrue(RoomCover.Hides(room.center));

            // 站進還蓋著的門後房間正中央；關掉物理，免得角色被方塊推開、位置跟算答案時不一樣
            body.simulated = false;
            body.transform.position = room.center;
            Vector2 from = room.center;
            var nearest = board.Blocks.Where(block => !string.IsNullOrEmpty(block.answer))
                .OrderBy(block => ((Vector2)block.transform.position - from).sqrMagnitude).ToList();
            Assert.IsTrue(RoomCover.Hides(nearest[0].transform.position), "測試前提：離得最近的方塊還在遮罩底下");
            var visible = nearest.First(block => !RoomCover.Hides(block.transform.position));

            input.Type("/");
            input.Type("showanswer");
            yield return input.Tap(Key.Enter);
            var clue = string.IsNullOrEmpty(visible.clue) ? "" : $" ({visible.clue})";
            Assert.AreEqual($"Answer: {visible.answer}{clue}", Hud.Instance.Chat.Messages.Last(), "只挑看得到的方塊，不能先拿到門後房間的答案");
            body.simulated = true;
        }

        static IEnumerator LoadGame()
        {
            SceneManager.LoadScene("Game");
            yield return null;
            yield return null;
            // batchmode 的畫面很小，展開的筆記會蓋住右下角的方塊
            Hud.Instance.Notes.SetMinimized(true);
        }

        static RoomCover[] Covers() => Object.FindObjectsByType<RoomCover>(FindObjectsSortMode.None);

        static void SetCoversVisible(bool visible)
        {
            foreach (var cover in Covers()) cover.GetComponent<MeshRenderer>().enabled = visible;
        }

        /// <summary>教學房間的題目直接設成解開，門照讀檔的方式直接打開，省得一格格打字。</summary>
        static void FinishTutorial(Board board, Rooms rooms)
        {
            foreach (var block in board.Blocks.Where(block =>
                         !string.IsNullOrEmpty(block.answer) && rooms.kinds[RoomOf(rooms, block.transform.position)] == RoomKind.Tutorial))
                board.RestoreSolved(block);
            foreach (var door in Object.FindObjectsByType<Door>(FindObjectsSortMode.None)) door.SyncInstantly();
        }

        static int RoomOf(Rooms rooms, Vector2 point) => System.Array.FindIndex(rooms.floors, floor => floor.Contains(point));

        /// <summary>從房間穿過這個門口走出去的方向。</summary>
        static Vector2 OutOf(Rect room, Rect doorway)
        {
            var away = doorway.center - room.center;
            return Mathf.Abs(away.x) * room.height >= Mathf.Abs(away.y) * room.width
                ? new Vector2(Mathf.Sign(away.x), 0)
                : new Vector2(0, Mathf.Sign(away.y));
        }

        static Rect Encapsulate(IEnumerable<Rect> areas)
        {
            var list = areas.ToList();
            return Rect.MinMaxRect(list.Min(a => a.xMin), list.Min(a => a.yMin), list.Max(a => a.xMax), list.Max(a => a.yMax));
        }

        /// <summary>剛好把這塊範圍框進 16:9 截圖的鏡頭大小。</summary>
        static float Frame(Rect area) => Mathf.Max(area.height, area.width * 9 / 16) / 2 + 0.6f;
    }
}

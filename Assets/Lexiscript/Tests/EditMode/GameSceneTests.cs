using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Lexiscript
{
    /// <summary>正式關卡（教學房間＋開放區）的內容與擺放。</summary>
    public sealed class GameSceneTests
    {
        static readonly string[] ControlLines =
        {
            "WASD to move · hold Shift to sprint · scroll to zoom",
            "Click the lower half of a block to type an answer",
            "Left / right-click the upper half to show the first / last letter",
        };

        static readonly BlockColor[] AllowedColors =
        {
            BlockColor.PureWhite, BlockColor.PureBlack, BlockColor.LightGray, BlockColor.BrightYellow, BlockColor.RoyalBlue,
        };

        Board board;
        Block[] blocks;
        Rooms rooms;
        Door[] doors;
        Spoiler[] spoilers;
        TextMeshPro[] floorTexts;

        [OneTimeSetUp]
        public void OpenGame()
        {
            EditorSceneManager.OpenScene("Assets/Lexiscript/Game.unity");
            board = Object.FindAnyObjectByType<Board>();
            blocks = board.Blocks;
            rooms = Object.FindAnyObjectByType<Rooms>();
            doors = Object.FindObjectsByType<Door>(FindObjectsSortMode.None).OrderBy(door => door.transform.position.x).ToArray();
            spoilers = Object.FindObjectsByType<Spoiler>(FindObjectsSortMode.None);
            floorTexts = GameObject.Find("Guides").GetComponentsInChildren<TextMeshPro>()
                .Where(text => text.GetComponentInParent<Spoiler>() == null)
                .ToArray();
        }

        int RoomOf(Vector2 point) => System.Array.FindIndex(rooms.floors, floor => floor.Contains(point));

        bool IsTutorial(int room) => room < rooms.floors.Length - 1;

        IEnumerable<string> TextsIn(int room) => floorTexts.Where(text => RoomOf(text.transform.position) == room).Select(text => text.text);

        [Test]
        public void IsBuiltAsTutorialRoomsFollowedByAnOpenArea()
        {
            Assert.GreaterOrEqual(rooms.floors.Length, 7, "至少六間教學房間加一個開放區");
            Assert.AreEqual(rooms.floors.Length - 1, rooms.doorways.Length);
            Assert.AreEqual(rooms.floors.Length - 1, doors.Length, "每間教學房間都有出口門");
            Assert.AreEqual(0, RoomOf(Object.FindAnyObjectByType<Player>().transform.position), "從第一間教學房間開始");
        }

        [Test]
        public void WordsAreLettersInCanonicalCaseWithoutPrefixClashes()
        {
            LevelChecks.WordsAreWellFormed(blocks);
        }

        [Test]
        public void LinksJoinNeighboursAndKeepTheirMeaning()
        {
            LevelChecks.LinksJoinNeighbours(blocks);
            LevelChecks.BridgedBlocksShareTheirAnswer(blocks);
            LevelChecks.CellKindsHaveTheirIncomingLinks(blocks);
            foreach (var (from, link) in LevelChecks.Links(blocks))
                Assert.AreEqual(RoomOf(from.transform.position), RoomOf(link.target.transform.position), $"{from.name} 連到別的房間");
        }

        [Test]
        public void EverythingIsInsideTheRooms()
        {
            LevelChecks.EverythingIsInsideTheRooms(rooms, blocks, Object.FindAnyObjectByType<Player>());
        }

        [Test]
        public void BlocksAreVisibleWithoutPlaying()
        {
            LevelChecks.BlocksAreVisibleWithoutPlaying(board);
        }

        [Test]
        public void ColorsAreIntroducedGradually()
        {
            CollectionAssert.IsSubsetOf(blocks.Select(block => block.color).Distinct(), AllowedColors);
            foreach (var color in AllowedColors)
                foreach (var shape in new[] { BlockShape.Plain, BlockShape.Beveled })
                    Assert.IsTrue(blocks.Any(block => block.color == color && block.shape == shape), $"{color} {shape} 沒出現");

            Assert.IsTrue(blocks.Where(block => RoomOf(block.transform.position) == 0)
                .All(block => block.color == BlockColor.PureWhite && block.shape == BlockShape.Plain), "第一間只有純白方塊");
            int open = rooms.floors.Length - 1;
            foreach (var block in blocks.Where(block => block.color == BlockColor.BrightYellow || block.color == BlockColor.RoyalBlue))
                Assert.AreEqual(open, RoomOf(block.transform.position), $"{block.name}：亮黃與皇家藍要在教學之後才出現");
            int firstGray = blocks.Where(block => block.color == BlockColor.LightGray).Min(block => RoomOf(block.transform.position));
            int firstBlack = blocks.Where(block => block.color == BlockColor.PureBlack).Min(block => RoomOf(block.transform.position));
            Assert.Less(firstBlack, firstGray, "先教黑再用連接橋推理淺灰");
            Assert.IsTrue(IsTutorial(firstGray), "淺灰在教學房間裡用連接橋引導");
            Assert.IsTrue(LevelChecks.Links(blocks).Any(x => x.link.kind == LinkKind.Bridge
                && (x.from.color == BlockColor.LightGray || x.link.target.color == BlockColor.LightGray)
                && IsTutorial(RoomOf(x.from.transform.position))), "淺灰要在教學房間透過連接橋讓玩家推理");
        }

        [Test]
        public void TutorialBlocksHaveSpoilersAndTheOpenAreaHasNone()
        {
            foreach (var block in blocks.Where(block => !string.IsNullOrEmpty(block.answer)))
            {
                int count = spoilers.Count(spoiler => spoiler.block == block);
                Assert.AreEqual(IsTutorial(RoomOf(block.transform.position)) ? 1 : 0, count, block.name);
            }
            foreach (var spoiler in spoilers)
            {
                Assert.AreEqual(Spoiler.Prompt, spoiler.label.text);
                Vector2 c = spoiler.transform.position;
                foreach (var block in blocks)
                {
                    Vector2 b = block.transform.position;
                    bool overlaps = Mathf.Abs(c.x - b.x) < Spoiler.Size.x / 2 + Art.Half && Mathf.Abs(c.y - b.y) < Spoiler.Size.y / 2 + Art.Half;
                    Assert.IsFalse(overlaps, $"{spoiler.name} 蓋到 {block.name}");
                }
            }
        }

        [Test]
        public void DoorsCountTheirOwnRoomAndCanBeOpened()
        {
            for (int i = 0; i < doors.Length; i++)
            {
                var door = doors[i];
                Assert.IsTrue(rooms.doorways[i].Contains(door.transform.position), door.name);
                Assert.IsNotEmpty(door.blocks);
                Assert.IsTrue(door.blocks.All(block => RoomOf(block.transform.position) == i), $"第 {i + 1} 間的門只算自己房間的題目");
                Assert.That(door.required, Is.InRange(1, door.blocks.Length));
                Assert.AreEqual(blocks.Count(block => !string.IsNullOrEmpty(block.answer) && RoomOf(block.transform.position) == i), door.blocks.Length);
                Assert.AreEqual($"0/{door.required}", door.counter.text);
            }
        }

        [Test]
        public void TutorialDoorsCannotBeOpenedWithoutTheNewKindOfBlock()
        {
            var taught = new HashSet<(BlockColor, BlockShape)>();
            for (int i = 0; i < doors.Length; i++)
            {
                var door = doors[i];
                var kinds = door.blocks.Select(block => (block.color, block.shape)).Distinct().ToList();
                foreach (var kind in kinds.Where(kind => !taught.Contains(kind)))
                    Assert.Less(door.blocks.Count(block => (block.color, block.shape) != kind), door.required,
                        $"第 {i + 1} 間不解 {kind.Item1} {kind.Item2} 也能開門，等於跳過這一課");
                taught.UnionWith(kinds);
            }
        }

        [Test]
        public void RoomTextsCanBeReadFromTheDoorLine()
        {
            // 所有門口都在 y = 0：從門口進來、沿著門口那條線走，每間房的說明都在畫面高度內
            foreach (var text in floorTexts.Where(text => text.name == "Text"))
            {
                float half = text.GetPreferredValues(text.text).y / 2;
                Assert.LessOrEqual(Mathf.Abs(text.transform.position.y) + half, Art.CameraSize, text.text);
            }
            // 出生點在第一間房中間，16:9 的畫面一開始就看得到整行操作說明
            Vector2 spawn = Object.FindAnyObjectByType<Player>().transform.position;
            foreach (var text in floorTexts.Where(text => text.name == "Text" && RoomOf(text.transform.position) == 0))
            {
                float half = text.GetPreferredValues(text.text).x / 2;
                Assert.LessOrEqual(Mathf.Abs(text.transform.position.x - spawn.x) + half, Art.CameraSize * 16 / 9, text.text);
            }
        }

        [Test]
        public void TutorialTextsTeachControlsAndMentionTheNotes()
        {
            var first = TextsIn(0).ToList();
            foreach (var line in ControlLines) CollectionAssert.Contains(first, line);
            Assert.IsTrue(Enumerable.Range(0, rooms.floors.Length - 1).SelectMany(TextsIn).Any(text => text.ToLowerInvariant().Contains("notes")), "教學要提到筆記");
            Assert.LessOrEqual(TextsIn(rooms.floors.Length - 1).Count(), 1, "開放區沒有提示");
        }

        [Test]
        public void FloorTextsUseGlyphsTheFontHasAndStayClearOfBlocks()
        {
            foreach (var text in floorTexts)
            {
                Assert.IsTrue(text.font.HasCharacters(text.text, out uint[] missing, true), $"{text.text} 缺字：{string.Join(",", missing ?? new uint[0])}");
                Vector2 half = text.GetPreferredValues(text.text) / 2, center = text.transform.position;
                Vector2 min = center - half, max = center + half;
                foreach (var block in blocks)
                {
                    Vector2 b = block.transform.position;
                    bool overlaps = min.x < b.x + Art.Half && max.x > b.x - Art.Half && min.y < b.y + Art.Half && max.y > b.y - Art.Half;
                    Assert.IsFalse(overlaps, $"「{text.text}」蓋到 {block.name}");
                }
                foreach (var other in floorTexts.Where(other => other != text))
                {
                    Vector2 otherHalf = other.GetPreferredValues(other.text) / 2, otherCenter = other.transform.position;
                    bool overlaps = Mathf.Abs(center.x - otherCenter.x) < half.x + otherHalf.x && Mathf.Abs(center.y - otherCenter.y) < half.y + otherHalf.y;
                    Assert.IsFalse(overlaps, $"「{text.text}」與「{other.text}」重疊");
                }
                Assert.IsTrue(rooms.Contains(min) && rooms.Contains(max), $"「{text.text}」超出房間");
            }
        }
    }
}

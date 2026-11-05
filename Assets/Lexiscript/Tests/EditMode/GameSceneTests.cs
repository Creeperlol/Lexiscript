using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Lexiscript
{
    /// <summary>正式關卡（教學房間＋開放式大地圖＋門後房間）的內容與擺放。</summary>
    public sealed class GameSceneTests
    {
        static readonly string[] ControlLines =
        {
            "WASD to move · hold Shift to sprint · scroll to zoom",
            "Click the lower half of a block to type an answer",
            "Left / right-click the upper half to show the first / last letter",
        };

        static readonly BlockColor[] TutorialColors = { BlockColor.PureWhite, BlockColor.PureBlack, BlockColor.LightGray };

        Board board;
        Block[] blocks;
        Rooms rooms;
        Door[] doors;
        RoomCover[] covers;
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
            covers = Object.FindObjectsByType<RoomCover>(FindObjectsSortMode.None);
            spoilers = Object.FindObjectsByType<Spoiler>(FindObjectsSortMode.None);
            floorTexts = GameObject.Find("Guides").GetComponentsInChildren<TextMeshPro>()
                .Where(text => text.GetComponentInParent<Spoiler>() == null)
                .ToArray();
        }

        int RoomOf(Vector2 point) => System.Array.FindIndex(rooms.floors, floor => floor.Contains(point));

        bool IsTutorial(int room) => rooms.kinds[room] == RoomKind.Tutorial;

        int Field => System.Array.IndexOf(rooms.kinds, RoomKind.Field);

        IEnumerable<int> RoomsOf(RoomKind kind) => Enumerable.Range(0, rooms.floors.Length).Where(i => rooms.kinds[i] == kind);

        IEnumerable<string> TextsIn(int room) => floorTexts.Where(text => RoomOf(text.transform.position) == room).Select(text => text.text);

        /// <summary>門口兩側接著的地板。</summary>
        int[] Sides(Rect doorway)
        {
            var touching = new Rect(doorway.position - Vector2.one * 0.01f, doorway.size + Vector2.one * 0.02f);
            return Enumerable.Range(0, rooms.floors.Length).Where(i => rooms.floors[i].Overlaps(touching)).ToArray();
        }

        Door DoorIn(Rect doorway) => doors.Single(door => doorway.Contains(door.transform.position));

        Rect DoorwayOf(Door door) => rooms.doorways.Single(doorway => doorway.Contains(door.transform.position));

        /// <summary>這扇門打開後露出的地板（遮罩蓋著的那一塊）。</summary>
        int Behind(Door door) => System.Array.IndexOf(rooms.floors, covers.Single(cover => cover.door == door).area);

        [Test]
        public void IsBuiltAsTutorialRoomsThenTheWorldAndItsGatedRooms()
        {
            Assert.AreEqual(rooms.floors.Length, rooms.kinds.Length);
            var tutorial = RoomsOf(RoomKind.Tutorial).ToList();
            Assert.GreaterOrEqual(tutorial.Count, 7, "七間教學房間");
            CollectionAssert.AreEqual(Enumerable.Range(0, tutorial.Count), tutorial, "教學房間排在最前面");
            for (int i = 1; i < tutorial.Count; i++)
                Assert.Greater(rooms.floors[i].xMin, rooms.floors[i - 1].xMax, "教學房間由左而右");
            Assert.AreEqual(1, RoomsOf(RoomKind.Field).Count(), "一張開放式大地圖");
            Assert.Greater(rooms.floors[Field].xMin, rooms.floors[tutorial.Count - 1].xMax, "大地圖在教學之後");
            Assert.GreaterOrEqual(RoomsOf(RoomKind.Gated).Count(), 1, "至少一間門後房間");
            Assert.AreEqual(rooms.doorways.Length, doors.Length, "每個門口都有門");
            Assert.AreEqual(0, RoomOf(Object.FindAnyObjectByType<Player>().transform.position), "從第一間教學房間開始");

            var links = rooms.doorways.Select(Sides).ToList();
            Assert.IsTrue(links.All(sides => sides.Length == 2), "每個門口剛好接著兩塊地板");
            for (int i = 0; i < tutorial.Count; i++)
            {
                int next = i + 1 < tutorial.Count ? i + 1 : Field;
                Assert.AreEqual(1, links.Count(sides => sides.Contains(i) && sides.Contains(next)), $"第 {i + 1} 間的出口通往下一塊地板");
            }
            foreach (int room in RoomsOf(RoomKind.Gated))
            {
                Assert.AreEqual(1, links.Count(sides => sides.Contains(room)), "門後房間只有一個門口");
                Assert.IsTrue(links.Any(sides => sides.Contains(room) && sides.Contains(Field)), "門後房間的門開在大地圖上");
            }
        }

        [Test]
        public void TheWorldIsMuchBiggerThanAScreenWithPuzzlesRightPastTheEntrance()
        {
            var field = rooms.floors[Field];
            float screenWidth = 2 * Art.CameraSize * 16 / 9, screenHeight = 2 * Art.CameraSize;
            Assert.Greater(field.width, 2.5f * screenWidth, "大地圖要比一個畫面大很多，才有探索的感覺");
            Assert.Greater(field.height, 2 * screenHeight);
            var entrance = rooms.doorways.Single(d => Sides(d).Contains(Field) && Sides(d).Any(IsTutorial));
            float nearest = blocks.Where(block => RoomOf(block.transform.position) == Field)
                .Min(block => Vector2.Distance(block.transform.position, entrance.center));
            Assert.Less(nearest, screenWidth / 2 + Art.Pitch, "一進大地圖就看得到題目");
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
        public void DoorwaysAreClearAndEverythingCanBeReachedOnFoot()
        {
            LevelChecks.DoorwaysAreClear(rooms, blocks);
            LevelChecks.EverythingCanBeReachedOnFoot(rooms, blocks, Object.FindAnyObjectByType<Player>().transform.position);
        }

        [Test]
        public void BlocksAreVisibleWithoutPlaying()
        {
            LevelChecks.BlocksAreVisibleWithoutPlaying(board);
        }

        [Test]
        public void ColorsAreIntroducedGradually()
        {
            var tutorial = blocks.Where(block => IsTutorial(RoomOf(block.transform.position))).ToList();
            var world = blocks.Except(tutorial).ToList();
            CollectionAssert.IsSubsetOf(tutorial.Select(block => block.color).Distinct(), TutorialColors, "教學只有白、黑、灰，其他顏色都在大地圖");
            foreach (var color in TutorialColors)
                foreach (var shape in new[] { BlockShape.Plain, BlockShape.Beveled })
                    Assert.IsTrue(tutorial.Any(block => block.color == color && block.shape == shape), $"教學沒教到 {color} {shape}");
            foreach (var color in new[] { BlockColor.BrightYellow, BlockColor.RoyalBlue })
                foreach (var shape in new[] { BlockShape.Plain, BlockShape.Beveled })
                    Assert.IsTrue(world.Any(block => block.color == color && block.shape == shape), $"大地圖入口區要有 {color} {shape}");

            Assert.IsTrue(blocks.Where(block => RoomOf(block.transform.position) == 0)
                .All(block => block.color == BlockColor.PureWhite && block.shape == BlockShape.Plain), "第一間只有純白方塊");
            int firstGray = tutorial.Where(block => block.color == BlockColor.LightGray).Min(block => RoomOf(block.transform.position));
            int firstBlack = tutorial.Where(block => block.color == BlockColor.PureBlack).Min(block => RoomOf(block.transform.position));
            Assert.Less(firstBlack, firstGray, "先教黑再用連接橋推理淺灰");
            Assert.IsTrue(LevelChecks.Links(blocks).Any(x => x.link.kind == LinkKind.Bridge
                && (x.from.color == BlockColor.LightGray || x.link.target.color == BlockColor.LightGray)
                && IsTutorial(RoomOf(x.from.transform.position))), "淺灰要在教學房間透過連接橋讓玩家推理");
        }

        [Test]
        public void TheSecondRoomTeachesSynonymsWithAnEasyWord()
        {
            var plain = blocks.Single(block => block.name == "W2_PRETTY_PLAIN");
            var cut = blocks.Single(block => block.name == "W2_PRETTY_CUT");
            Assert.AreEqual(1, RoomOf(plain.transform.position));
            Assert.AreEqual(1, RoomOf(cut.transform.position));
            Assert.AreEqual((BlockColor.PureWhite, BlockShape.Plain, "pretty", "pretty"), (plain.color, plain.shape, plain.clue, plain.answer));
            Assert.AreEqual((BlockColor.PureWhite, BlockShape.Beveled, "pretty", "beautiful"), (cut.color, cut.shape, cut.clue, cut.answer));
            CollectionAssert.IsSubsetOf(new[] { "lovely", "cute", "nice", "gorgeous" }, cut.hintAnswers);
            Assert.IsFalse(blocks.Any(block => block.name.Contains("SICK") || block.answer == "ill"), "sick → ill 太難，已經換掉");
        }

        [Test]
        public void TutorialBlocksHaveSpoilersAndTheWorldHasNone()
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
        public void DoorsOpenOnlyWhenEveryBlockTheyGuardIsSolved()
        {
            foreach (var door in doors)
            {
                Assert.IsNotEmpty(door.blocks, door.name);
                Assert.AreEqual(door.blocks.Length, door.required, "門要全部答對才開，不是差一題就開");
                Assert.AreEqual($"0/{door.required}", door.counter.text);
                int behind = Behind(door);
                var sides = Sides(DoorwayOf(door));
                CollectionAssert.Contains(sides, behind);
                if (rooms.kinds[behind] == RoomKind.Gated)
                {
                    Assert.IsTrue(door.blocks.All(block => RoomOf(block.transform.position) == Field), "門後房間的門算的是門前那道題");
                    continue;
                }
                int room = sides.Single(side => side != behind);
                Assert.IsTrue(IsTutorial(room), "教學房間的出口門");
                Assert.IsTrue(door.blocks.All(block => RoomOf(block.transform.position) == room), $"第 {room + 1} 間的門只算自己房間的題目");
                Assert.AreEqual(blocks.Count(block => !string.IsNullOrEmpty(block.answer) && RoomOf(block.transform.position) == room), door.blocks.Length,
                    $"第 {room + 1} 間的每一題都要解開");
            }
        }

        [Test]
        public void TutorialDoorsCannotBeOpenedWithoutTheNewKindOfBlock()
        {
            var taught = new HashSet<(BlockColor, BlockShape)>();
            var tutorialDoors = doors.Where(door => door.blocks.All(block => IsTutorial(RoomOf(block.transform.position)))).ToArray();
            for (int i = 0; i < tutorialDoors.Length; i++)
            {
                var door = tutorialDoors[i];
                var kinds = door.blocks.Select(block => (block.color, block.shape)).Distinct().ToList();
                foreach (var kind in kinds.Where(kind => !taught.Contains(kind)))
                    Assert.Less(door.blocks.Count(block => (block.color, block.shape) != kind), door.required,
                        $"第 {i + 1} 間不解 {kind.Item1} {kind.Item2} 也能開門，等於跳過這一課");
                taught.UnionWith(kinds);
            }
        }

        [Test]
        public void EveryClosedSpaceIsCoveredUntilItsDoorOpens()
        {
            Assert.AreEqual(rooms.floors.Length - 1, covers.Length, "除了第一間，每塊地板都蓋著");
            Assert.IsFalse(covers.Any(cover => cover.area.Overlaps(rooms.floors[0])), "第一間不蓋");
            for (int i = 1; i < rooms.floors.Length; i++)
            {
                var cover = covers.Single(c => c.area == rooms.floors[i]);
                Assert.IsNotNull(cover.door, cover.name);
                CollectionAssert.Contains(Sides(DoorwayOf(cover.door)), i, $"{cover.name} 跟著通往這塊地板的門");
                Assert.AreEqual(cover.area.center, (Vector2)cover.transform.position, cover.name);
                Assert.AreEqual(Art.CoverOrder, cover.GetComponent<MeshRenderer>().sortingOrder, cover.name);
                foreach (var doorway in rooms.doorways)
                    Assert.IsFalse(cover.area.Overlaps(doorway), $"{cover.name} 只蓋地板，牆和門要露在外面");
            }
            // 遮罩蓋過房間裡的一切，只有角色那一層（Art.PlayerOrder）在它上面
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (renderer.GetComponentInParent<RoomCover>() != null || renderer.GetComponentInParent<Player>() != null) continue;
                if (renderer.sortingOrder >= Art.PlayerOrder) continue;
                Assert.Less(renderer.sortingOrder, Art.CoverOrder, renderer.name);
            }
        }

        [Test]
        public void CoversLiftFromTheirDoorSide()
        {
            foreach (var cover in covers)
            {
                Vector2 size = cover.area.size, door = (Vector2)cover.door.transform.position - cover.area.center;
                var inward = Art.CoverInward(size, door);
                Assert.Greater(Vector2.Dot(door, -inward), 0, $"{cover.name} 要從門那一側收起");
                // 門口內側一點，與正對面的牆邊
                var across = new Vector2(Mathf.Abs(inward.y), Mathf.Abs(inward.x));
                float depth = Vector2.Dot(size / 2, new Vector2(Mathf.Abs(inward.x), Mathf.Abs(inward.y)));
                var near = Vector2.Scale(door, across) - inward * (depth - 0.5f);
                var far = Vector2.Scale(door, across) + inward * (depth - 0.5f);
                Assert.IsTrue(Art.CoverRemains(size, inward, 0, near) && Art.CoverRemains(size, inward, 0, far), cover.name);
                Assert.IsFalse(Art.CoverRemains(size, inward, 0.5f, near), $"{cover.name}：收到一半時門口那邊已經露出來");
                Assert.IsTrue(Art.CoverRemains(size, inward, 0.5f, far), $"{cover.name}：收到一半時對面還蓋著");
                Assert.IsFalse(Art.CoverRemains(size, inward, 1, near) || Art.CoverRemains(size, inward, 1, far), cover.name);
            }
        }

        [Test]
        public void GatePuzzlesSitBesideTheirDoorsOnTheFieldSide()
        {
            foreach (int room in RoomsOf(RoomKind.Gated))
            {
                var doorway = rooms.doorways.Single(d => Sides(d).Contains(room));
                var door = DoorIn(doorway);
                Assert.AreEqual(room, Behind(door));
                foreach (var block in door.blocks)
                {
                    Assert.AreEqual(Field, RoomOf(block.transform.position), $"{block.name} 要在大地圖這一側");
                    Assert.IsFalse(rooms.kinds.Where((kind, i) => kind == RoomKind.Gated && rooms.floors[i].Overlaps(LevelChecks.Bounds(block))).Any());
                }
                float nearest = door.blocks.Min(block => Vector2.Distance(block.transform.position, doorway.center));
                Assert.LessOrEqual(nearest, 4.5f, $"{door.blocks[0].name} 要在門口旁邊");
                // 門前題目不在門口正前方：與門口在沿著牆的方向上錯開
                foreach (var block in door.blocks)
                {
                    Vector2 c = block.transform.position;
                    bool beside = doorway.width > doorway.height
                        ? c.x - Art.Half >= doorway.xMax || c.x + Art.Half <= doorway.xMin
                        : c.y - Art.Half >= doorway.yMax || c.y + Art.Half <= doorway.yMin;
                    Assert.IsTrue(beside, $"{block.name} 擋在門口前面");
                }
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
            Assert.IsTrue(RoomsOf(RoomKind.Tutorial).SelectMany(TextsIn).Any(text => text.ToLowerInvariant().Contains("notes")), "教學要提到筆記");
            var world = floorTexts.Where(text => !IsTutorial(RoomOf(text.transform.position))).ToList();
            Assert.LessOrEqual(world.Count, 1, "大地圖只有入口那一句，沒有提示");
            Assert.IsTrue(world.All(text => text.name == "Text" && RoomOf(text.transform.position) == Field), "大地圖沒有題組說明");
        }

        [Test]
        public void FloorTextsHaveChineseTranslations()
        {
            foreach (var text in floorTexts)
            {
                var localized = text.GetComponent<LocalizedText>();
                Assert.IsNotNull(localized, text.text);
                Assert.AreEqual(text.text, localized.english);
                Assert.IsTrue(Localization.HasTranslation(localized.english), $"「{localized.english}」沒有中文翻譯");
            }
            Assert.IsTrue(Localization.HasTranslation(Spoiler.Prompt));
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

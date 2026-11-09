using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Lexiscript
{
    /// <summary>正式關卡（教學房間＋大廳＋以門相連的房間網路）的內容與擺放。</summary>
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

        /// <summary>這一點所在的房間（走道、側間、橋算它們所屬那間房的主要地板）。</summary>
        int RoomOf(Vector2 point) => rooms.Owner(System.Array.FindIndex(rooms.floors, floor => floor.Contains(point)));

        bool IsTutorial(int room) => rooms.kinds[room] == RoomKind.Tutorial;

        int Field => System.Array.IndexOf(rooms.kinds, RoomKind.Field);

        IEnumerable<int> RoomsOf(RoomKind kind) => Enumerable.Range(0, rooms.floors.Length).Where(i => rooms.kinds[i] == kind && rooms.Owner(i) == i);

        IEnumerable<string> TextsIn(int room) => floorTexts.Where(text => RoomOf(text.transform.position) == room).Select(text => text.text);

        /// <summary>門口兩側接著的房間。</summary>
        int[] Sides(Rect doorway)
        {
            var touching = new Rect(doorway.position - Vector2.one * 0.01f, doorway.size + Vector2.one * 0.02f);
            return Enumerable.Range(0, rooms.floors.Length).Where(i => rooms.floors[i].Overlaps(touching)).Select(rooms.Owner).Distinct().ToArray();
        }

        Rect DoorwayOf(Door door) => rooms.doorways.Single(doorway => doorway.Contains(door.transform.position));


        [Test]
        public void IsBuiltAsTutorialRoomsThenTheHallAndANetworkOfRooms()
        {
            Assert.AreEqual(rooms.floors.Length, rooms.kinds.Length);
            var tutorial = RoomsOf(RoomKind.Tutorial).ToList();
            Assert.GreaterOrEqual(tutorial.Count, 7, "七間教學房間");
            CollectionAssert.AreEqual(Enumerable.Range(0, tutorial.Count), tutorial, "教學房間排在最前面");
            for (int i = 1; i < tutorial.Count; i++)
                Assert.Greater(rooms.floors[i].xMin, rooms.floors[i - 1].xMax, "教學房間由左而右");
            Assert.AreEqual(1, RoomsOf(RoomKind.Field).Count(), "一間大廳");
            Assert.Greater(rooms.floors[Field].xMin, rooms.floors[tutorial.Count - 1].xMax, "大廳在教學之後");
            Assert.GreaterOrEqual(RoomsOf(RoomKind.Gated).Count(), 10, "大廳之外還有許多間門後房間可以探索");
            Assert.AreEqual(rooms.doorways.Length, doors.Length, "每個門口都有門");
            Assert.AreEqual(0, RoomOf(Object.FindAnyObjectByType<Player>().transform.position), "從第一間教學房間開始");

            var links = rooms.doorways.Select(Sides).ToList();
            Assert.IsTrue(links.All(sides => sides.Length == 2), "每個門口剛好接著兩塊地板");
            for (int i = 0; i < tutorial.Count; i++)
            {
                int next = i + 1 < tutorial.Count ? i + 1 : Field;
                Assert.AreEqual(1, links.Count(sides => sides.Contains(i) && sides.Contains(next)), $"第 {i + 1} 間的出口通往下一塊地板");
                Assert.AreEqual(i == 0 ? 1 : 2, links.Count(sides => sides.Contains(i)), $"第 {i + 1} 間只有進出兩個門口");
            }
            Assert.GreaterOrEqual(links.Count(sides => sides.Contains(Field)), 4, "大廳往好幾個方向分岔");
            Assert.IsTrue(RoomsOf(RoomKind.Gated).Any(room => links.Count(sides => sides.Contains(room)) >= 3), "有房間是交叉路口");
        }

        [Test]
        public void EveryDoorCanBeOpenedWithPuzzlesAlreadyInReach()
        {
            // 從第一間開始，只解得到已經打開的房間裡的題目，一扇扇開門；最後每扇門都要打得開，不能卡死。
            // 門前題目（門口旁邊那幾格）用到的顏色與形狀，要在已經走得到的房間裡看得到別的例子，玩家才有機會推出規則
            var gates = new HashSet<Block>(doors.SelectMany(door => door.blocks.Where(block =>
                Vector2.Distance(block.transform.position, DoorwayOf(door).center) <= 4.5f)));
            var open = new HashSet<int> { 0 };
            var opened = new HashSet<Door>();
            bool progress = true;
            while (progress)
            {
                progress = false;
                foreach (var door in doors.Where(door => !opened.Contains(door)).ToList())
                {
                    if (!door.blocks.All(block => open.Contains(RoomOf(block.transform.position)))) continue;
                    var known = new HashSet<(BlockColor, BlockShape)>(blocks
                        .Where(block => !block.IsPure && !gates.Contains(block) && open.Contains(RoomOf(block.transform.position)))
                        .Select(block => (block.color, block.shape)));
                    var unknown = door.blocks.Where(block => gates.Contains(block) && !known.Contains((block.color, block.shape))).ToList();
                    Assert.IsEmpty(unknown, $"{string.Join(", ", unknown.Select(block => block.name))} 用到的顏色在走得到的地方還沒出現過");
                    opened.Add(door);
                    open.UnionWith(Sides(DoorwayOf(door)));
                    progress = true;
                }
            }
            CollectionAssert.AreEquivalent(doors, opened, "有門永遠打不開");
            Assert.AreEqual(Enumerable.Range(0, rooms.floors.Length).Select(rooms.Owner).Distinct().Count(), open.Count, "有房間永遠進不去");
        }

        [Test]
        public void TheWorldIsMuchBiggerThanAScreenWithPuzzlesRightPastTheEntrance()
        {
            var world = Enumerable.Range(0, rooms.floors.Length).Where(i => !IsTutorial(i)).Select(i => rooms.floors[i]).ToList();
            float width = world.Max(f => f.xMax) - world.Min(f => f.xMin), height = world.Max(f => f.yMax) - world.Min(f => f.yMin);
            float screenWidth = 2 * Art.CameraSize * 16 / 9, screenHeight = 2 * Art.CameraSize;
            Assert.Greater(width, 4 * screenWidth, "大地圖要比一個畫面大很多，才有探索的感覺");
            Assert.Greater(height, 4 * screenHeight);
            // 走過橋、一踏進大廳就看得到題目
            var entrance = new Vector2(rooms.floors[Field].xMin, 0);
            float nearest = blocks.Where(block => RoomOf(block.transform.position) == Field)
                .Min(block => Vector2.Distance(block.transform.position, entrance));
            Assert.Less(nearest, screenWidth / 2 + Art.Pitch, "一進大廳就看得到題目");
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
            LevelChecks.ChainsIntoSolidBlocksDoNotHintTheirMiddleWords(blocks);
            foreach (var (from, link) in LevelChecks.Links(blocks))
                Assert.AreEqual(RoomOf(from.transform.position), RoomOf(link.target.transform.position), $"{from.name} 連到別的房間");
        }

        [Test]
        public void EverythingIsInsideTheRooms()
        {
            LevelChecks.EverythingIsInsideTheRooms(rooms, blocks, Object.FindAnyObjectByType<Player>());
        }

        [Test]
        public void ABridgeLeadsFromTheTutorialIntoTheHall()
        {
            var bridges = Enumerable.Range(0, rooms.floors.Length).Where(i => rooms.kinds[i] == RoomKind.Bridge).ToList();
            Assert.AreEqual(1, bridges.Count, "教學出口與大廳之間一座橋");
            var bridge = rooms.floors[bridges[0]];
            Assert.AreEqual(Field, rooms.Owner(bridges[0]), "橋是大廳的一部分，跟著大廳一起掀開");
            Assert.AreEqual(rooms.floors[Field].xMin, bridge.xMax, 1e-3f, "橋接著大廳的左牆");
            var exit = rooms.doorways.Single(d => Sides(d).Contains(Field) && Sides(d).Any(IsTutorial));
            Assert.AreEqual(exit.xMax, bridge.xMin, 1e-3f, "橋的另一頭就是教學出口");
            Assert.Greater(bridge.width, 2 * bridge.height, "橋又長又窄");
            var entry = Object.FindAnyObjectByType<WorldEntry>();
            Assert.IsNotNull(entry, "第一次過橋的演出");
            Assert.AreEqual(bridge, entry.bridge);
            Assert.AreEqual(rooms.floors[Field], entry.hall);
        }

        [Test]
        public void RoomsAreNotAllPlainBoxes()
        {
            var parts = Enumerable.Range(0, rooms.floors.Length).Where(i => rooms.Owner(i) != i && rooms.kinds[i] != RoomKind.Bridge).ToList();
            Assert.GreaterOrEqual(parts.Count, 15, "房間之間有走道、有的房間還有側間");
            var world = RoomsOf(RoomKind.Gated).Select(i => rooms.floors[i]).ToList();
            Assert.Greater(world.Select(f => Mathf.Round(f.width)).Distinct().Count(), 5, "房間大小不一");
            Assert.Greater(world.Select(f => Mathf.Round(f.xMin)).Distinct().Count(), 6, "房間沒有排成整齊的棋盤");
            Assert.GreaterOrEqual(rooms.obstacles.Length, 10, "地圖上有擋路的柱子與盆栽");
            // 房間本身切過角、凹過口：跟主要地板直接相接（中間沒有門口）的同一間房地板
            int shaped = RoomsOf(RoomKind.Gated).Count(room => Enumerable.Range(0, rooms.floors.Length).Any(i => i != room && rooms.Owner(i) == room
                && rooms.floors[i].Overlaps(new Rect(rooms.floors[room].position - Vector2.one * 0.01f, rooms.floors[room].size + Vector2.one * 0.02f))));
            Assert.GreaterOrEqual(shaped, 10, "大部分房間不是單純的長方形");
            Assert.GreaterOrEqual(rooms.obstacles.Select(o => (Mathf.Round(o.width * 10), Mathf.Round(o.height * 10))).Distinct().Count(), 6, "擺設有很多種");
        }

        [Test]
        public void TeleportPointsStandOnOpenFloor()
        {
            var points = Object.FindObjectsByType<TeleportPoint>(FindObjectsSortMode.None).OrderBy(point => point.order).ToList();
            Assert.GreaterOrEqual(points.Count, 4);
            Assert.AreEqual(("Tutorial", "教學關"), (points[0].english, points[0].chinese), "第一個是教學關（出生點）");
            Assert.AreEqual((Vector2)Object.FindAnyObjectByType<Player>().transform.position, (Vector2)points[0].transform.position);
            Assert.AreEqual(("World Gate", "大世界起點"), (points[1].english, points[1].chinese), "第二個是大世界起點");
            Assert.AreEqual(Field, RoomOf(points[1].transform.position));
            CollectionAssert.AllItemsAreUnique(points.Select(point => point.order));
            float clearance = Art.Half + Art.PlayerHalf;
            foreach (var point in points)
            {
                Vector2 at = point.transform.position;
                Assert.IsTrue(rooms.floors.Any(floor => floor.Contains(at)), point.english);
                foreach (var block in blocks)
                {
                    Vector2 c = block.transform.position;
                    Assert.IsTrue(Mathf.Abs(at.x - c.x) > clearance || Mathf.Abs(at.y - c.y) > clearance, $"{point.english} 站在 {block.name} 上");
                }
                Assert.IsFalse(rooms.obstacles.Any(obstacle => obstacle.Contains(at)), $"{point.english} 站在擺設上");
            }
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
                var sides = Sides(DoorwayOf(door));
                if (!sides.Any(IsTutorial)) continue;
                int room = sides.Min();
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
                var entries = doors.Where(door => Sides(DoorwayOf(door)).Contains(rooms.Owner(i))).ToList();
                CollectionAssert.AreEquivalent(entries, cover.doors, $"{cover.name} 跟著通往這塊地板的每一扇門，從哪扇進來都會掀開");
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
            // 只看畫鎖的那塊主要地板：走道、側間離門可能很遠，跟著一起收起就好
            // 地面只會被教學出口掀開：其他通往地面的門都要站在地面上才開得到，那時地面早就掀開了
            foreach (var (cover, entry) in covers.Where(cover => cover.locked).SelectMany(cover => cover.doors.Select(door => (cover, door)))
                .Where(x => x.cover.area != rooms.floors[Field] || Sides(DoorwayOf(x.door)).Any(IsTutorial)))
            {
                // 同一間房的幾塊遮罩照整間房的範圍一起擦
                var frame = cover.frame.width > 0 ? cover.frame : cover.area;
                Assert.IsTrue(frame.xMin <= cover.area.xMin && frame.yMin <= cover.area.yMin && frame.xMax >= cover.area.xMax && frame.yMax >= cover.area.yMax, cover.name);
                Vector2 size = frame.size, door = (Vector2)entry.transform.position - frame.center;
                var inward = Art.CoverInward(size, door);
                Assert.Greater(Vector2.Dot(door, -inward), 0, $"{cover.name} 要從門那一側收起");
                // 門口內側一點，與正對面的牆邊
                var across = new Vector2(Mathf.Abs(inward.y), Mathf.Abs(inward.x));
                float depth = Vector2.Dot(size / 2, new Vector2(Mathf.Abs(inward.x), Mathf.Abs(inward.y)));
                var near = Vector2.Scale(door, across) - inward * (depth - 0.5f);
                // 對面取牆的正中間：門不在牆中間時（大廳這種長房間），斜著擦的切線會先掃到門那一排的對面
                var far = inward * (depth - 0.5f);
                Assert.IsTrue(Art.CoverRemains(size, inward, 0, near) && Art.CoverRemains(size, inward, 0, far), cover.name);
                Assert.IsFalse(Art.CoverRemains(size, inward, 0.5f, near), $"{cover.name}：收到一半時門口那邊已經露出來");
                Assert.IsTrue(Art.CoverRemains(size, inward, 0.5f, far), $"{cover.name}：收到一半時對面還蓋著");
                Assert.IsFalse(Art.CoverRemains(size, inward, 1, near) || Art.CoverRemains(size, inward, 1, far), cover.name);
            }
        }

        [Test]
        public void GatePuzzlesSitBesideTheirDoorsWithoutBlockingThem()
        {
            int gates = 0;
            foreach (var door in doors.Where(door => !Sides(DoorwayOf(door)).Any(IsTutorial)))
            {
                var doorway = DoorwayOf(door);
                var near = door.blocks.Where(block => Vector2.Distance(block.transform.position, doorway.center) <= 4.5f).ToList();
                if (near.Count > 0) gates++;
                foreach (var block in near)
                {
                    CollectionAssert.Contains(Sides(doorway), RoomOf(block.transform.position), $"{block.name} 要在門口兩側的房間裡");
                    // 門前題目不在門口正前方：與門口在沿著牆的方向上錯開
                    Vector2 c = block.transform.position;
                    bool beside = doorway.width > doorway.height
                        ? c.x - Art.Half >= doorway.xMax || c.x + Art.Half <= doorway.xMin
                        : c.y - Art.Half >= doorway.yMax || c.y + Art.Half <= doorway.yMin;
                    Assert.IsTrue(beside, $"{block.name} 擋在門口前面");
                }
            }
            Assert.GreaterOrEqual(gates, 10, "大部分的門前都有一道題");
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
            var entry = world.Where(text => text.name == "Text").ToList();
            Assert.LessOrEqual(entry.Count, 1, "大廳只有入口那一句");
            Assert.IsTrue(entry.All(text => RoomOf(text.transform.position) == Field));
            // 地板提示只出現在第一次遇到「鏈尾是純色格」的那一間，後面不再提示
            var hints = world.Except(entry).ToList();
            Assert.IsTrue(hints.All(text => text.name == "Hint"), "大地圖沒有題組說明");
            Assert.AreEqual(1, hints.Select(text => RoomOf(text.transform.position)).Distinct().Count(), "提示只在一間房裡");
            int hinted = RoomOf(hints[0].transform.position);
            Assert.IsTrue(blocks.Any(block => block.IsPure && RoomOf(block.transform.position) == hinted), "提示所在的房間有純色格的鏈");
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

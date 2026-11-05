using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lexiscript
{
    /// <summary>
    /// 以 <c>-executeMethod Lexiscript.GameSetup.Run</c> 依 GameLevel.json 重建正式關卡：
    /// 由左而右一間間教學房間（出口門、地板說明、暴雷條），最後一間的門通往開放式大地圖——
    /// 一條入口大路兩側自動排好的題目區，邊緣再接幾間門後房間（門前一道題，解開才進得去）。
    /// 還沒開門的地方都蓋著遮罩。手動改過 Game.unity 後就不要再執行，否則會被覆蓋；要改題目就改 json 再重建。
    /// </summary>
    public static class GameSetup
    {
        public const string ScenePath = SceneBuilder.Folder + "Game.unity";
        public const string LevelPath = SceneBuilder.Folder + "Editor/GameLevel.json";

#pragma warning disable 0649 // 欄位由 JsonUtility 填入
        [Serializable]
        sealed class LevelSpec
        {
            public RoomSpec[] rooms;
            public WorldSpec world;
        }

        [Serializable]
        sealed class RoomSpec
        {
            public string key;
            public bool tutorial;
            public string[] texts;
            public GroupSpec[] groups;
        }

        /// <summary>教學之後的開放式大地圖；texts 是入口旁的地板說明，大地圖裡沒有其他文字提示。</summary>
        [Serializable]
        sealed class WorldSpec
        {
            public string[] texts;
            public ZoneSpec[] zones;
            public GatedRoomSpec[] rooms;
        }

        /// <summary>大地圖上的一個題目區；accent 是可省略的 BlockColor 名稱，只用來畫地面的分區標記。</summary>
        [Serializable]
        sealed class ZoneSpec
        {
            public string key;
            public string accent;
            public GroupSpec[] groups;
        }

        /// <summary>接在大地圖邊緣的門後房間；gate 是門前那道題（全部解開才開門）。</summary>
        [Serializable]
        sealed class GatedRoomSpec
        {
            public string key;
            public string accent;
            public GroupSpec gate;
            public GroupSpec[] groups;
        }

        [Serializable]
        sealed class GroupSpec
        {
            public BlockSpec[] blocks;
            public LinkSpec[] links;
            public string caption;
        }

        [Serializable]
        sealed class BlockSpec
        {
            public string id;
            public int dx, dy;
            public string color, shape, clue, answer;
            public string[] hints;
        }

        [Serializable]
        sealed class LinkSpec
        {
            public string from, to, kind;
        }
#pragma warning restore 0649

        sealed class RoomLayout
        {
            public RoomSpec Spec;
            public float Width, Height;
            /// <summary>門口那條線（世界座標 y = 0）在房間裡的高度，以房間頂端為 0、往下為負。</summary>
            public float Entry;
            /// <summary>以房間左上角為原點的位置。</summary>
            public readonly List<(BlockSpec spec, Vector2 position)> Blocks = new List<(BlockSpec, Vector2)>();
            /// <summary>name 是 Text（房間說明）或 Caption（題組說明）。</summary>
            public readonly List<(string name, string text, Vector2 position, float size)> Texts = new List<(string, string, Vector2, float)>();
        }

        /// <summary>一組題組排好後的方塊位置，以方塊範圍的左上角為原點（y 往下為負）。</summary>
        sealed class Pack
        {
            public readonly List<(BlockSpec spec, Vector2 position)> Blocks = new List<(BlockSpec, Vector2)>();
            public float Width, Height;
        }

        sealed class GatedLayout
        {
            public GatedRoomSpec Spec;
            public Pack Inside, Gate;
            /// <summary>接在大地圖上緣（否則是下緣）。</summary>
            public bool Top;
            public float Width, Height;
        }

        /// <summary>入口大路某一側的一排題目區。</summary>
        sealed class ZoneRow
        {
            public bool North;
            public readonly List<int> Zones = new List<int>();
            public float Width, Height;
        }

        /// <summary>整個關卡的擺放結果（世界座標），建場景時照著放。</summary>
        sealed class LevelPlan
        {
            public readonly List<Rect> Floors = new List<Rect>();
            public readonly List<RoomKind> Kinds = new List<RoomKind>();
            public readonly List<string> Keys = new List<string>();
            public readonly List<Rect> Doorways = new List<Rect>();
            public readonly List<(Rect area, BlockColor? accent, bool flipped)> Plots = new List<(Rect, BlockColor?, bool)>();
            /// <summary>spoiler：教學房間的方塊底下要放暴雷條。</summary>
            public readonly List<(BlockSpec spec, Vector2 position, bool spoiler)> Blocks = new List<(BlockSpec, Vector2, bool)>();
            public readonly List<(string name, string text, Vector2 position, float width, float size)> Texts = new List<(string, string, Vector2, float, float)>();
            public readonly List<GroupSpec> Groups = new List<GroupSpec>();
            /// <summary>opens 是這扇門打開前被遮住的地板。</summary>
            public readonly List<(int doorway, List<string> blocks, int opens)> Doors = new List<(int, List<string>, int)>();

            public int AddFloor(Rect floor, RoomKind kind, string key)
            {
                Floors.Add(floor);
                Kinds.Add(kind);
                Keys.Add(key);
                return Floors.Count - 1;
            }
        }

        const float SideMargin = 3f, TopMargin = 1.4f, TextSpacing = 0.85f, BottomMargin = 2f;
        // 門口那條線與上方說明文字（半高約 0.25）、下方第一排方塊各隔 EntryGap；沒有說明的房間，門口上方也至少留 DoorLintel 的牆
        const float EntryGap = 1.1f, TextHalfHeight = 0.25f, DoorLintel = 0.75f;
        const float TutorialRowHeight = 5f, OpenRowGap = 2.2f, CaptionGap = 0.55f, SpoilerGap = 0.45f;
        // 說明文字不換行，房間寬度依最長一行估算（LiberationSans 在 3.6 號字平均每字約 0.2）
        const float MinWidth = 18f, MinHeight = 10f, DoorHeight = 2.5f, CharWidth = 0.22f;
        const int MaxColumns = 7;
        const float TextFontSize = 3.6f, CaptionFontSize = 3f;

        // 大地圖：分區之間的走道要寬到看得出是不同區、走起來不擠，但不能寬到空曠；入口大路沿著門口那條線。
        // 地塊只是地面標記、走得進去，大路不必寬：站在路上（畫面半高 5）就要看得到兩側第一排方塊
        const float PlotPadding = 1.4f, ZoneGap = 4.5f, EdgeGap = 3.5f, StreetHalf = 2.2f, Stagger = 2f, TextInset = 1.5f;
        // 長寬比：分區接近畫面比例，一眼看得完一區；整張地圖比畫面大很多，太扁會變成一條直路、太方又不像往前探索
        const float ZoneAspect = 1.5f, RoomAspect = 1.4f, FieldAspect = 1.6f;
        const int MaxZoneColumns = 13, MaxZoneRows = 4;
        // 門後房間：門那一側多留一段走道；門前題目貼著牆、離門口一小段，與分區之間再留一條走道
        const float RoomSide = 3f, RoomDoorSide = 3.5f, RoomFarSide = 2.5f, MinRoomWidth = 12f, MinRoomHeight = 9f, RoomGap = 4f;
        const float GateWallGap = 0.75f, GateSideGap = 1f, GateClear = 3f;

        public static void Run()
        {
            var level = JsonUtility.FromJson<LevelSpec>(File.ReadAllText(LevelPath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var material = SceneBuilder.Material();
            var plan = Plan(level);
            var floors = plan.Floors;
            SceneBuilder.Rooms(material, floors, plan.Doorways, "GameRooms.asset", 7, plan.Kinds, plan.Plots);

            var boardObject = SceneBuilder.BeginBoard();
            var guides = new GameObject("Guides").transform;
            var blocks = new Dictionary<string, Block>();
            foreach (var (spec, position, _) in plan.Blocks)
                blocks.Add(spec.id, SceneBuilder.Block(boardObject.transform, material, spec.id, position,
                    Parse<BlockColor>(spec.color), Parse<BlockShape>(spec.shape), spec.clue ?? "", spec.answer ?? "", spec.hints ?? new string[0]));
            foreach (var (name, text, position, width, size) in plan.Texts)
                SceneBuilder.FloorText(guides, name, text, position, width, size);
            foreach (var link in plan.Groups.SelectMany(group => group.links ?? new LinkSpec[0]))
                SceneBuilder.Link(blocks[link.from], blocks[link.to], Parse<LinkKind>(link.kind));
            var board = SceneBuilder.EndBoard(boardObject, material);

            foreach (var (spec, _, spoiler) in plan.Blocks)
            {
                if (!spoiler || string.IsNullOrEmpty(spec.answer)) continue;
                var block = blocks[spec.id];
                SceneBuilder.Spoiler(guides, material, block, (Vector2)block.transform.position + Vector2.down * (Art.Half + SpoilerGap));
            }
            foreach (var (doorway, ids, opens) in plan.Doors)
            {
                var required = ids.Select(id => blocks[id]).ToList();
                var door = SceneBuilder.Door(material, board, plan.Doorways[doorway], required, required.Count);
                SceneBuilder.Cover(material, "Cover " + plan.Keys[opens], floors[opens], door);
            }

            // 第一間房正中間的門口那條線上：一開始就看得到上面的操作說明和下面的第一排方塊
            var spawn = new Vector2(floors[0].center.x, 0);
            SceneBuilder.Camera(spawn);
            SceneBuilder.Player(material, spawn);
            SceneBuilder.Hud();
            new GameObject("Progress", typeof(GameProgress));

            EditorSceneManager.SaveScene(scene, ScenePath);
            SceneBuilder.RegisterScenes();
            SceneBuilder.ConfigurePipeline();
        }

        /// <summary>
        /// 算出所有地板、門口、方塊、地板說明與門的位置。教學房間由左而右排，門一律要求房間裡全部答對；
        /// 最後一間的出口通往大地圖（見 <see cref="PlanWorld"/>）。
        /// </summary>
        static LevelPlan Plan(LevelSpec level)
        {
            var plan = new LevelPlan();
            float x = 0;
            foreach (var room in level.rooms.Select(Layout))
            {
                // 所有門口都在 y = 0；房間依自己的 Entry 上下錯開，從門口進來時說明文字和第一排方塊都在畫面裡
                var floor = new Rect(x, -room.Entry - room.Height, room.Width, room.Height);
                var origin = new Vector2(floor.xMin, floor.yMax);
                int index = plan.AddFloor(floor, RoomKind.Tutorial, room.Spec.key);
                foreach (var (spec, position) in room.Blocks) plan.Blocks.Add((spec, origin + position, room.Spec.tutorial));
                foreach (var (name, text, position, size) in room.Texts) plan.Texts.Add((name, text, origin + position, floor.width - 2 * SideMargin, size));
                plan.Groups.AddRange(room.Spec.groups);
                // 出口在右牆的門口那條線上，打開後露出下一塊地板；最後一間的下一塊就是緊接著加入的大地圖
                plan.Doorways.Add(new Rect(floor.xMax, -DoorHeight / 2, Art.WallThickness, DoorHeight));
                plan.Doors.Add((plan.Doorways.Count - 1, Answerable(room.Spec.groups), index + 1));
                x = floor.xMax + Art.WallThickness;
            }
            PlanWorld(level.world, plan, x);
            return plan;
        }

        /// <summary>
        /// 大地圖：門口那條線是一條橫越整張地圖的入口大路，題目區一排排擺在路的南北兩側（入口區在門口右下方），
        /// 區與區之間留走道；門後房間輪流接在上緣與下緣，門前題目貼著牆放在門口右邊。
        /// 排數依整張地圖的長寬比自動決定，地圖大小由內容撐開。
        /// </summary>
        static void PlanWorld(WorldSpec world, LevelPlan plan, float left)
        {
            var zones = world.zones.Select(zone => Arrange(zone.groups, ZoneAspect)).ToList();
            var rooms = world.rooms.Select((spec, i) => Gated(spec, i % 2 == 0)).ToList();
            var top = rooms.Where(room => room.Top).ToList();
            var bottom = rooms.Where(room => !room.Top).ToList();
            var texts = world.texts ?? new string[0];

            float lowestText = EntryGap + TextHalfHeight;
            float streetNorth = Mathf.Max(StreetHalf, lowestText + (texts.Length - 1) * TextSpacing + TextHalfHeight + EntryGap);
            float topBand = Band(top), bottomBand = Band(bottom);
            float roomsWidth = Mathf.Max(EdgeWidth(top), EdgeWidth(bottom));

            List<ZoneRow> rows = null;
            float inner = 0, yMin = 0, yMax = 0, best = float.MaxValue;
            for (int count = 1; count <= Mathf.Min(MaxZoneRows, zones.Count); count++)
            {
                var candidate = ZoneRows(zones, count);
                float candidateInner = Mathf.Max(candidate.Max(row => row.Width), roomsWidth - 2 * EdgeGap);
                float candidateMax = streetNorth + SideHeight(candidate.Where(row => row.North)) + topBand;
                float candidateMin = -StreetHalf - SideHeight(candidate.Where(row => !row.North)) - bottomBand;
                float score = Mathf.Abs(Mathf.Log((candidateInner + 2 * EdgeGap) / (candidateMax - candidateMin) / FieldAspect));
                if (score >= best) continue;
                (best, rows, inner, yMin, yMax) = (score, candidate, candidateInner, candidateMin, candidateMax);
            }

            var field = Rect.MinMaxRect(left, yMin, left + inner + 2 * EdgeGap, yMax);
            plan.AddFloor(field, RoomKind.Field, "world");
            float north = streetNorth, south = -StreetHalf;
            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                int count = row.Zones.Count;
                float slack = inner - row.Width;
                // 入口旁那一排從牆邊排起，入口區就在門口；其他排平均散開，路兩側的區塊才不會對齊成棋盤
                float start = i == 0 ? 0 : slack / (count + 1);
                float between = ZoneGap + (i == 0 ? (count > 1 ? slack / (count - 1) : 0) : slack / (count + 1));
                float x = left + EdgeGap + start;
                for (int slot = 0; slot < count; slot++)
                {
                    int zone = row.Zones[slot];
                    var size = PlotSize(zones[zone]);
                    // 相鄰兩區一前一後錯開一點，路邊不會是一直線
                    float offset = slot % 2 * Stagger;
                    var area = new Rect(x, row.North ? north + offset : south - offset - size.y, size.x, size.y);
                    plan.Plots.Add((area, Accent(world.zones[zone].accent), row.North));
                    Place(plan, zones[zone], new Vector2(area.xMin + PlotPadding, area.yMax - PlotPadding));
                    plan.Groups.AddRange(world.zones[zone].groups);
                    x += size.x + between;
                }
                if (row.North) north += row.Height + ZoneGap;
                else south -= row.Height + ZoneGap;
            }

            float textWidth = texts.Select(text => text.Length * CharWidth).DefaultIfEmpty(0).Max();
            for (int i = 0; i < texts.Length; i++)
                plan.Texts.Add(("Text", texts[i], new Vector2(left + TextInset + textWidth / 2, lowestText + (texts.Length - 1 - i) * TextSpacing), textWidth + 2 * TextInset, TextFontSize));

            foreach (var edge in new[] { top, bottom })
            {
                float gap = (field.width - edge.Sum(room => room.Width)) / (edge.Count + 1);
                float x = left + gap;
                foreach (var room in edge)
                {
                    PlanRoom(plan, room, field, x);
                    x += room.Width + gap;
                }
            }
        }

        static void PlanRoom(LevelPlan plan, GatedLayout room, Rect field, float x)
        {
            var floor = new Rect(x, room.Top ? field.yMax + Art.WallThickness : field.yMin - Art.WallThickness - room.Height, room.Width, room.Height);
            float center = floor.center.x;
            int index = plan.AddFloor(floor, RoomKind.Gated, room.Spec.key);
            plan.Doorways.Add(new Rect(center - DoorHeight / 2, room.Top ? field.yMax : field.yMin - Art.WallThickness, DoorHeight, Art.WallThickness));

            // 房間裡的題目從門那一側留出走道後才開始排
            var inside = new Vector2(center - room.Inside.Width / 2, room.Top ? floor.yMin + RoomDoorSide + room.Inside.Height : floor.yMax - RoomDoorSide);
            var plot = Rect.MinMaxRect(inside.x, inside.y - room.Inside.Height, inside.x + room.Inside.Width, inside.y);
            plan.Plots.Add((Rect.MinMaxRect(plot.xMin - PlotPadding, plot.yMin - PlotPadding, plot.xMax + PlotPadding, plot.yMax + PlotPadding), Accent(room.Spec.accent), room.Top));
            Place(plan, room.Inside, inside);

            // 門前題目在場地這一側、貼著牆排在門口右邊，不擋住門口；房間夠寬（見 Gated），所以不會碰到隔壁房間的門
            var gate = new Vector2(center + DoorHeight / 2 + GateSideGap, room.Top ? field.yMax - GateWallGap : field.yMin + GateWallGap + room.Gate.Height);
            Place(plan, room.Gate, gate);

            plan.Doors.Add((plan.Doorways.Count - 1, Answerable(new[] { room.Spec.gate }), index));
            plan.Groups.Add(room.Spec.gate);
            plan.Groups.AddRange(room.Spec.groups);
        }

        static GatedLayout Gated(GatedRoomSpec spec, bool top)
        {
            var room = new GatedLayout { Spec = spec, Top = top, Inside = Arrange(spec.groups, RoomAspect), Gate = Arrange(new[] { spec.gate }, RoomAspect) };
            // 門前題目要落在房間的寬度範圍內，同一邊相鄰兩間的門前題目和門口才不會擠在一起
            room.Width = Mathf.Max(room.Inside.Width + 2 * RoomSide, MinRoomWidth, 2 * (DoorHeight / 2 + GateSideGap + room.Gate.Width));
            room.Height = Mathf.Max(room.Inside.Height + RoomDoorSide + RoomFarSide, MinRoomHeight);
            return room;
        }

        /// <summary>大地圖上緣或下緣到最外排分區之間的走道：有房間時要放得下門前題目，外加一條走道。</summary>
        static float Band(List<GatedLayout> rooms) =>
            rooms.Count == 0 ? EdgeGap : GateWallGap + rooms.Max(room => room.Gate.Height) + GateClear;

        static float EdgeWidth(List<GatedLayout> rooms) => rooms.Sum(room => room.Width) + (rooms.Count + 1) * RoomGap;

        /// <summary>依離入口大路的遠近輪流放在路的兩側：南 0、北 0、南 1、北 1……，第 k 區放進第 k % count 排。</summary>
        static List<ZoneRow> ZoneRows(List<Pack> zones, int count)
        {
            var rows = Enumerable.Range(0, count).Select(i => new ZoneRow { North = i % 2 == 1 }).ToList();
            for (int zone = 0; zone < zones.Count; zone++) rows[zone % count].Zones.Add(zone);
            foreach (var row in rows)
            {
                row.Width = row.Zones.Sum(zone => PlotSize(zones[zone]).x) + (row.Zones.Count - 1) * ZoneGap;
                row.Height = row.Zones.Select((zone, slot) => PlotSize(zones[zone]).y + slot % 2 * Stagger).Max();
            }
            return rows;
        }

        static float SideHeight(IEnumerable<ZoneRow> rows)
        {
            var list = rows.ToList();
            return list.Count == 0 ? 0 : list.Sum(row => row.Height) + (list.Count - 1) * ZoneGap;
        }

        static Vector2 PlotSize(Pack pack) => new Vector2(pack.Width, pack.Height) + Vector2.one * (2 * PlotPadding);

        static void Place(LevelPlan plan, Pack pack, Vector2 topLeft)
        {
            foreach (var (spec, position) in pack.Blocks) plan.Blocks.Add((spec, topLeft + position, false));
        }

        static List<string> Answerable(IEnumerable<GroupSpec> groups) =>
            groups.SelectMany(group => group.blocks).Where(block => !string.IsNullOrEmpty(block.answer)).Select(block => block.id).ToList();

        static BlockColor? Accent(string name) => string.IsNullOrEmpty(name) ? (BlockColor?)null : Parse<BlockColor>(name);

        /// <summary>依順序把題組一排排排好（題組之間空一格），每排最多幾格挑讓整體長寬比最接近 aspect 的。</summary>
        static Pack Arrange(IList<GroupSpec> groups, float aspect)
        {
            int widest = groups.Max(Columns);
            Pack best = null;
            float bestScore = float.MaxValue;
            for (int columns = widest; columns <= Mathf.Max(widest, MaxZoneColumns); columns++)
            {
                var pack = Arrange(groups, columns);
                float score = Mathf.Abs(Mathf.Log(pack.Width / pack.Height / aspect));
                if (score >= bestScore) continue;
                best = pack;
                bestScore = score;
            }
            return best;
        }

        static Pack Arrange(IList<GroupSpec> groups, int maxColumns)
        {
            var pack = new Pack();
            var rows = PackRows(groups, maxColumns);
            pack.Width = rows.Max(RowWidth);
            float y = 0;
            foreach (var row in rows)
            {
                float left = (pack.Width - RowWidth(row)) / 2 + Art.Half;
                foreach (var group in row)
                {
                    foreach (var (block, offset) in Cells(group)) pack.Blocks.Add((block, new Vector2(left, y) + offset));
                    left += (Columns(group) + 1) * Art.Pitch;
                }
                y -= RowHeight(row) + OpenRowGap;
            }
            pack.Height = -y - OpenRowGap;
            return pack;
        }

        /// <summary>
        /// 一間房的排版：上方是說明文字，接著是門口那條線，下面一排排放題組（一排最多 MaxColumns 格寬，題組之間空一格），
        /// 教學房間每格底下留暴雷條的位置，題組說明放在題組正下方。
        /// </summary>
        static RoomLayout Layout(RoomSpec spec)
        {
            var layout = new RoomLayout { Spec = spec };
            var texts = spec.texts ?? new string[0];
            var rows = PackRows(spec.groups, MaxColumns);

            float textWidth = texts.Concat(spec.groups.Select(group => group.caption ?? "")).Select(text => text.Length * CharWidth).DefaultIfEmpty(0).Max();
            layout.Width = Mathf.Max(MinWidth, textWidth + 2 * SideMargin, rows.Select(RowWidth).DefaultIfEmpty(0).Max() + 2 * SideMargin);

            float y = -TopMargin;
            foreach (var text in texts)
            {
                layout.Texts.Add(("Text", text, new Vector2(layout.Width / 2, y), TextFontSize));
                y -= TextSpacing;
            }
            float textsBottom = texts.Length > 0 ? y + TextSpacing - TextHalfHeight : 0;
            layout.Entry = Mathf.Min(textsBottom - EntryGap, -(DoorHeight / 2 + DoorLintel));
            y = layout.Entry - EntryGap;

            foreach (var row in rows)
            {
                float left = (layout.Width - RowWidth(row)) / 2 + Art.Half;
                int height = row.Max(Rows);
                float lowest = y;
                foreach (var group in row)
                {
                    foreach (var (block, offset) in Cells(group))
                    {
                        var position = new Vector2(left, y) + offset;
                        layout.Blocks.Add((block, position));
                        lowest = Mathf.Min(lowest, position.y);
                    }
                    if (!string.IsNullOrEmpty(group.caption))
                    {
                        float below = Art.Half + (spec.tutorial ? SpoilerGap + Spoiler.Size.y + CaptionGap : CaptionGap);
                        float center = left + (Columns(group) - 1) * Art.Pitch / 2;
                        float groupLowest = layout.Blocks.Where(placed => group.blocks.Contains(placed.spec)).Min(placed => placed.position.y);
                        layout.Texts.Add(("Caption", group.caption, new Vector2(center, groupLowest - below), CaptionFontSize));
                    }
                    left += (Columns(group) + 1) * Art.Pitch;
                }
                y -= spec.tutorial ? TutorialRowHeight * height : RowHeight(row) + OpenRowGap;
            }
            layout.Height = Mathf.Max(MinHeight, -y + BottomMargin);
            return layout;
        }

        /// <summary>依順序把題組放進一排排，每排（含題組之間空的一格）最多 maxColumns 格寬。</summary>
        static List<List<GroupSpec>> PackRows(IEnumerable<GroupSpec> groups, int maxColumns)
        {
            var rows = new List<List<GroupSpec>>();
            int used = 0;
            foreach (var group in groups)
            {
                int columns = Columns(group);
                if (rows.Count == 0 || used + 1 + columns > maxColumns)
                {
                    rows.Add(new List<GroupSpec>());
                    used = -1;
                }
                rows[rows.Count - 1].Add(group);
                used += 1 + columns;
            }
            return rows;
        }

        static float RowWidth(List<GroupSpec> row) => (row.Sum(Columns) + row.Count - 2) * Art.Pitch + 2 * Art.Half;

        static float RowHeight(List<GroupSpec> row) => (row.Max(Rows) - 1) * Art.Pitch + 2 * Art.Half;

        /// <summary>題組裡每格相對於（最左一欄的中心 x, 題組頂端 y）的位置；dy 往上為正。</summary>
        static IEnumerable<(BlockSpec block, Vector2 offset)> Cells(GroupSpec group)
        {
            int minX = group.blocks.Min(block => block.dx), maxY = group.blocks.Max(block => block.dy);
            return group.blocks.Select(block => (block, new Vector2((block.dx - minX) * Art.Pitch, -Art.Half - (maxY - block.dy) * Art.Pitch)));
        }

        static int Columns(GroupSpec group) => group.blocks.Max(block => block.dx) - group.blocks.Min(block => block.dx) + 1;

        static int Rows(GroupSpec group) => group.blocks.Max(block => block.dy) - group.blocks.Min(block => block.dy) + 1;

        static T Parse<T>(string value) where T : struct => (T)Enum.Parse(typeof(T), value);
    }
}

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

        /// <summary>
        /// 教學之後的大地圖：房間與空地排在格子上，第一格是接著教學出口的入口廣場（open），doors 是房間的門。
        /// texts 是橋頭旁的地板說明。
        /// </summary>
        [Serializable]
        sealed class WorldSpec
        {
            public string[] texts;
            public NodeSpec[] rooms;
            public DoorSpec[] doors;
        }

        /// <summary>
        /// 大地圖上的一間房，擺在第 col 欄、第 row 列（列往下數）的格子裡；open 的不是房間，是地面上露天的題目區（廣場）。
        /// accent 是可省略的 BlockColor 名稱，用來畫地面的分區標記與裝飾的顏色；hints 是房間上方的地板提示；
        /// floor 是 <see cref="FloorStyle"/> 名稱，也決定擺什麼（磁磚：展覽、木板：書房、棋盤：廣場、斜紋：庭園）；
        /// teleport 是放在這裡的傳送點；level 是房間比地面高幾階，高低不同的門口有樓梯。
        /// </summary>
        [Serializable]
        sealed class NodeSpec
        {
            public string key;
            public string accent;
            public int col, row, level;
            public bool open;
            public string floor;
            public TeleportSpec teleport;
            public string[] hints;
            public GroupSpec[] groups;
        }

        [Serializable]
        sealed class TeleportSpec
        {
            public string en, zh;
        }

        /// <summary>
        /// 房間的門。有 a 時是格子上相鄰兩間房之間的門，gate 排在 a 那一側、貼著牆放在門口旁；
        /// 沒有 a 時是從地面進 b 的門，開在 b 的 wall 那面牆（Left、Right、Top、Bottom）正中間，gate 擺在牆外的地面上。
        /// side 決定門前題目放在門口的哪一邊（1：右邊或上面，-1：左邊或下面）；needs 是要全部解開才開門的房間。
        /// 門前題目與 needs 的題目全部解開才開門，兩者都可以省略其中一個。
        /// </summary>
        [Serializable]
        sealed class DoorSpec
        {
            public string a, b, wall;
            public int side;
            public GroupSpec gate;
            public string[] needs;
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

        enum Wall
        {
            Left,
            Right,
            Top,
            Bottom,
        }

        sealed class NodeLayout
        {
            public NodeSpec Spec;
            public Pack Content;
            public string[] Texts;
            /// <summary>內容與各面牆（依 <see cref="Wall"/> 的順序）之間至少要留的距離，門前題目也排在這段距離裡。</summary>
            public readonly float[] Margins = new float[4];
            /// <summary>提示文字從北牆往下讓出多少（北牆邊有門前題目時）。</summary>
            public float TextTop;
            /// <summary>整間房的外框；切掉幾個角之後真正的地板是 <see cref="Pieces"/>。</summary>
            public Rect Floor;
            public int Index;
            public Rect Plot;
            /// <summary>切角與擺設都要避開的地方：題目區、門口附近、門前題目、說明文字、側間的入口。</summary>
            public readonly List<Rect> Keep = new List<Rect>();
            public List<Rect> Pieces;
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
            /// <summary>sides 是門口兩側的房間（主要那塊地板）；任何一扇門打開，兩間房的遮罩都會收起。</summary>
            public readonly List<(int doorway, List<string> blocks, int[] sides)> Doors = new List<(int, List<string>, int[])>();
            /// <summary>每塊地板屬於哪一間房（那間房主要那塊地板的索引）；走道、側間、橋跟著它的房間一起蓋著、一起掀開。</summary>
            public readonly List<int> Owners = new List<int>();
            public readonly List<FloorStyle> Styles = new List<FloorStyle>();
            public readonly List<Color?> Tints = new List<Color?>();
            public readonly List<int> Levels = new List<int>();
            public readonly List<Prop> Props = new List<Prop>();
            /// <summary>橋兩側的天空，浮島不放在這裡。</summary>
            public readonly List<Rect> Voids = new List<Rect>();
            public readonly List<(string english, string chinese, Vector2 position, int order)> Teleports = new List<(string, string, Vector2, int)>();
            public Rect Bridge;

            public int AddFloor(Rect floor, RoomKind kind, string key, FloorStyle style = FloorStyle.Plain, BlockColor? accent = null, int owner = -1, int level = 0)
            {
                Floors.Add(floor);
                Kinds.Add(kind);
                Keys.Add(key);
                Styles.Add(style);
                Levels.Add(level);
                Tints.Add(accent.HasValue && accent != BlockColor.Colorless ? Art.Swatch(accent.Value) : (Color?)null);
                Owners.Add(owner < 0 ? Floors.Count - 1 : owner);
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

        const float PlotPadding = 1.4f, TextInset = 1.5f;
        // 露天題目區散開時，兩座台子之間至少留這麼寬的路
        const float SpreadGap = 2.5f;
        // 房間內容接近畫面比例，一眼看得完一間
        const float ZoneAspect = 1.5f, RoomAspect = 1.4f;
        const int MaxZoneColumns = 13;
        // 內容離牆至少 RoomSide：門口兩側的通道（測試檢查 2.5）不會被方塊擋住。
        // 門前題目貼著牆、離門口一小段，與房間內容之間再留一條走道
        const float RoomSide = 3f, MinRoomWidth = 12f, MinRoomHeight = 9f;
        const float GateWallGap = 0.75f, GateSideGap = 1f, GateClear = 3f;
        // 教學出口到大地圖之間的橋：夠長才有走過去的感覺，橋下是天空；VoidHalf 內不放浮島
        const float BridgeLength = 16f, BridgeWidth = 3.5f, VoidHalf = 7f, BridgePostPitch = 2.5f;
        // 房間面對街的那一面各往內縮 StreetHalf，兩間對面的房間之間是一條窄巷，建築擠在一起；島的邊緣再留 Outer 的地面
        const float StreetHalf = 2f, Outer = 1.5f;
        // 空地格子（廣場、題目區）至少這麼大；題目離空地格子邊緣 OpenMargin；入口廣場下緣留一條 EntryLane 寬的路接著橋
        const float MinOpen = 8f, OpenMargin = 1.5f, EntryLane = 5.5f;
        // 房間比內容大一點，切得出角、牆邊放得下一排擺設，但不留大片空地
        const float RoomExtra = 1f, TeleportRoom = 4f;
        // 切角至少這麼大才看得出形狀；keep 的範圍：題目區外留 KeepClear、門口四周 DoorKeep
        const float MinCut = 3f, KeepClear = 1.8f, DoorKeep = 3.5f, TextKeep = 0.8f;
        // 擺設朝房間裡面、離彼此至少留這麼遠，再多擺設也圍不住路；靠牆的那一面貼著牆，只留 Hug
        const float PropGap = 1.2f, RugGap = 0.6f, Hug = 0.25f;
        const float StairTread = 0.5f;
        // 天空裡的浮島離地板（牆外再算上影子）的距離
        const float GroundClear = 2.2f;

        public static void Run()
        {
            var level = JsonUtility.FromJson<LevelSpec>(File.ReadAllText(LevelPath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var material = SceneBuilder.Material();
            var plan = Plan(level);
            var floors = plan.Floors;
            var decor = new RoomsDecor { Styles = plan.Styles, Tints = plan.Tints, Props = plan.Props, Levels = plan.Levels };
            var rooms = SceneBuilder.Rooms(material, floors, plan.Doorways, "GameRooms.asset", 7, plan.Kinds, plan.Plots, decor);
            rooms.owners = plan.Owners.ToArray();

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
            var doors = new List<Door>();
            foreach (var (doorway, ids, _) in plan.Doors)
            {
                var required = ids.Select(id => blocks[id]).ToList();
                doors.Add(SceneBuilder.Door(material, board, plan.Doorways[doorway], required, required.Count));
            }
            // 第一間是出生的地方，不蓋；其他地板跟著通往它那間房的每一扇門，排第一的那扇（進來的主要入口）決定標誌畫在哪一側。
            // 走道、側間、橋是房間的一部分，鎖只畫在主要那塊地板上
            for (int floor = 1; floor < floors.Count; floor++)
            {
                int owner = plan.Owners[floor];
                var opening = Enumerable.Range(0, doors.Count).Where(i => plan.Doors[i].sides.Contains(owner)).Select(i => doors[i]).ToArray();
                var parts = Enumerable.Range(0, floors.Count).Where(i => plan.Owners[i] == owner).Select(i => floors[i]).ToList();
                var frame = Rect.MinMaxRect(parts.Min(f => f.xMin), parts.Min(f => f.yMin), parts.Max(f => f.xMax), parts.Max(f => f.yMax));
                SceneBuilder.Cover(material, "Cover " + plan.Keys[floor], floors[floor], opening, owner == floor, frame);
            }

            // 第一間房正中間的門口那條線上：一開始就看得到上面的操作說明和下面的第一排方塊
            var spawn = new Vector2(floors[0].center.x, 0);
            SceneBuilder.Teleport("Tutorial", "教學關", spawn, 0);
            foreach (var (english, chinese, position, order) in plan.Teleports) SceneBuilder.Teleport(english, chinese, position, order);
            SceneBuilder.WorldEntry(plan.Bridge, floors[plan.Owners[floors.IndexOf(plan.Bridge)]]);
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
                plan.Doors.Add((plan.Doorways.Count - 1, Answerable(room.Spec.groups), new[] { index, index + 1 }));
                x = floor.xMax + Art.WallThickness;
            }
            PlanWorld(level.world, plan, x);
            return plan;
        }

        /// <summary>
        /// 大地圖是一整片地面上蓋滿房間的建築群：每間房（與空地上的題目區 open）佔格子上的一格，格子之間沒有空隙。
        /// 有門相通的兩間房共用一道牆，其他方向往內縮出一條街；沒有東西的格子挖掉，露出天空。
        /// 地面就是大廳：教學出口的橋接在地面左緣 y = 0，走過橋整片地面都能走，房間各自上鎖。
        /// 門有兩種：房間對房間（a、b 在格子上相鄰，門前題目在 a 裡面），或從地面進房間（只給 b 與 b 的哪面牆 wall，門前題目擺在牆外的地面上）。
        /// 房間依格子大小撐滿、切角（<see cref="Carve"/>），地面扣掉房間後拆成幾塊，再放擺設（<see cref="Dress"/>）與島外天空裡的浮島（<see cref="Grounds"/>）。
        /// </summary>
        static void PlanWorld(WorldSpec world, LevelPlan plan, float left)
        {
            float t = Art.WallThickness;
            var nodes = world.rooms.Select(spec => new NodeLayout
            {
                Spec = spec,
                Content = Arrange(spec.groups, ZoneAspect),
                Texts = spec.hints ?? new string[0],
            }).ToList();
            var byKey = nodes.ToDictionary(node => node.Spec.key);
            var entry = nodes[0];
            if (!entry.Spec.open) throw new InvalidOperationException("第一格要是空地：橋接進來的入口廣場");
            var doors = world.doors.Select(spec =>
            {
                var b = byKey[spec.b];
                var a = string.IsNullOrEmpty(spec.a) ? null : byKey[spec.a];
                if (b.Spec.open || (a != null && a.Spec.open)) throw new InvalidOperationException($"{spec.a}–{spec.b}：門只開在房間上");
                var wall = a != null ? Facing(a.Spec, b.Spec) : Parse<Wall>(spec.wall);
                bool gated = spec.gate != null && spec.gate.blocks != null && spec.gate.blocks.Length > 0;
                return (spec, a, b, wall, gate: gated ? Arrange(new[] { spec.gate }, RoomAspect) : null);
            }).ToList();

            // 共用牆的那幾面；從地面進來的門要開在面對街或廣場的牆上
            var flush = new HashSet<(NodeLayout, Wall)>();
            foreach (var door in doors.Where(door => door.a != null))
            {
                flush.Add((door.a, door.wall));
                flush.Add((door.b, Opposite(door.wall)));
            }
            foreach (var door in doors.Where(door => door.a == null && flush.Contains((door.b, door.wall))))
                throw new InvalidOperationException($"{door.b.Spec.key} 的 {door.wall} 牆貼著別的房間，不能從地面開門");

            foreach (var node in nodes)
                for (int wall = 0; wall < 4; wall++) node.Margins[wall] = node.Spec.open ? OpenMargin : RoomSide;
            foreach (var door in doors.Where(door => door.a != null && door.gate != null))
            {
                float depth = door.wall == Wall.Left || door.wall == Wall.Right ? door.gate.Width : door.gate.Height;
                door.a.Margins[(int)door.wall] = Mathf.Max(door.a.Margins[(int)door.wall], GateWallGap + depth + GateClear);
            }
            foreach (var node in nodes.Where(node => node.Texts.Length > 0))
            {
                // 北牆邊有門前題目時，提示文字排在題目下面
                node.TextTop = doors.Any(door => door.a == node && door.wall == Wall.Top && door.gate != null) ? node.Margins[(int)Wall.Top] : 0;
                node.Margins[(int)Wall.Top] = node.TextTop + TopMargin + (node.Texts.Length - 1) * TextSpacing + 2 * TextHalfHeight + EntryGap;
            }
            // 入口廣場下緣留一條路：從橋走進來那條線（y = 0）上沒有題目
            entry.Margins[(int)Wall.Bottom] = Mathf.Max(entry.Margins[(int)Wall.Bottom], EntryLane);
            // 左邊緊貼著建築群外圍那圈地面，題目一下橋就在眼前
            entry.Margins[(int)Wall.Left] = 1;

            // 房間每一面往格子內縮多少：共用牆各縮半道牆；面對街的縮半條街，那面有地面上的門前題目時多縮到放得下題目
            float Inset(NodeLayout node, Wall wall)
            {
                if (node.Spec.open) return 0;
                if (flush.Contains((node, wall))) return t / 2;
                float inset = StreetHalf;
                foreach (var door in doors.Where(door => door.a == null && door.b == node && door.wall == wall && door.gate != null))
                {
                    float depth = wall == Wall.Left || wall == Wall.Right ? door.gate.Width : door.gate.Height;
                    inset = Mathf.Max(inset, t + GateWallGap + depth + GateClear);
                }
                return inset;
            }

            var sizes = nodes.Select(node =>
            {
                int hash = Hash(node.Spec.key);
                float textWidth = node.Texts.Select(text => text.Length * CharWidth).DefaultIfEmpty(0).Max() + 2 * SideMargin;
                float width = Mathf.Max(node.Spec.open ? MinOpen : MinRoomWidth, textWidth, node.Content.Width + node.Margins[(int)Wall.Left] + node.Margins[(int)Wall.Right]);
                float height = Mathf.Max(node.Spec.open ? MinOpen : MinRoomHeight, node.Content.Height + node.Margins[(int)Wall.Top] + node.Margins[(int)Wall.Bottom]);
                if (!node.Spec.open)
                {
                    // 有傳送台的房間再寬一點，題目區和門口四周之外才擺得下
                    bool teleport = node.Spec.teleport != null && !string.IsNullOrEmpty(node.Spec.teleport.en);
                    width += RoomExtra + hash % 3 + (teleport ? TeleportRoom : 0);
                    height += RoomExtra * 0.8f + hash / 4 % 2;
                }
                return new Vector2(width + Inset(node, Wall.Left) + Inset(node, Wall.Right), height + Inset(node, Wall.Top) + Inset(node, Wall.Bottom));
            }).ToList();
            int columns = nodes.Max(node => node.Spec.col) + 1, rows = nodes.Max(node => node.Spec.row) + 1;
            var widths = Enumerable.Repeat(MinOpen, columns).ToArray();
            var heights = Enumerable.Repeat(MinOpen, rows).ToArray();
            for (int i = 0; i < nodes.Count; i++)
            {
                widths[nodes[i].Spec.col] = Mathf.Max(widths[nodes[i].Spec.col], sizes[i].x);
                heights[nodes[i].Spec.row] = Mathf.Max(heights[nodes[i].Spec.row], sizes[i].y);
            }

            // 格子緊貼著排；整片往上下移，讓入口廣場底下那條路剛好在 y = 0
            float x0 = left + BridgeLength + Outer;
            Rect Cell(NodeSpec spec, float shift) => new Rect(x0 + widths.Take(spec.col).Sum(), shift - heights.Take(spec.row + 1).Sum(), widths[spec.col], heights[spec.row]);
            float lift = -(Cell(entry.Spec, 0).yMin + EntryLane / 2);
            var cells = nodes.Select(node => Cell(node.Spec, lift)).ToList();
            var square = Rect.MinMaxRect(x0 - Outer, lift - heights.Sum() - Outer, x0 + widths.Sum() + Outer, lift + Outer);

            int ground = plan.AddFloor(Rect.zero, RoomKind.Field, "ground", FloorStyle.Tiles);
            for (int i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                var cell = cells[i];
                node.Floor = node.Spec.open
                    ? cell
                    : Rect.MinMaxRect(cell.xMin + Inset(node, Wall.Left), cell.yMin + Inset(node, Wall.Bottom), cell.xMax - Inset(node, Wall.Right), cell.yMax - Inset(node, Wall.Top));
                node.Index = node.Spec.open ? ground
                    : plan.AddFloor(node.Floor, RoomKind.Gated, node.Spec.key, Style(node.Spec.floor, FloorStyle.Tiles), Accent(node.Spec.accent), -1, node.Spec.level + 1);
            }

            var bridge = Rect.MinMaxRect(left, -BridgeWidth / 2, square.xMin, BridgeWidth / 2);
            plan.Bridge = bridge;
            plan.Voids.Add(Rect.MinMaxRect(left - t, -VoidHalf, square.xMin + t, VoidHalf));
            for (float x = left + 2; x < bridge.xMax - 1; x += BridgePostPitch)
                foreach (float y in new[] { bridge.yMax + t / 2, bridge.yMin - t / 2 })
                    plan.Props.Add(new Prop { Kind = PropKind.Post, Center = new Vector2(x, y), Size = Vector2.one * 0.45f });
            var gatePad = new Vector2(square.xMin + 1.6f, 0);
            plan.Teleports.Add(("World Gate", "大世界起點", gatePad, 1));
            plan.Props.Add(new Prop { Kind = PropKind.Pad, Center = gatePad, Size = Vector2.one * 1.8f, Accent = Art.Gold });
            // 地面上的東西（入口說明、地面上的門前題目）每個廣場擺設時都要避開
            var groundKeep = new List<Rect> { Grow(new Rect(square.xMin, -DoorHeight / 2, 0, DoorHeight), DoorKeep) };

            float introWidth = world.texts.Select(text => text.Length * CharWidth).DefaultIfEmpty(0).Max();
            for (int i = 0; i < world.texts.Length; i++)
            {
                // 入口說明在橋頭下方，一過橋就看得到
                var at = new Vector2(square.xMin + TextInset + introWidth / 2, -DoorHeight / 2 - 1f - i * TextSpacing);
                plan.Texts.Add(("Text", world.texts[i], at, introWidth + 2 * TextInset, TextFontSize));
                groundKeep.Add(Grow(new Rect(at - new Vector2(introWidth / 2, 0.5f), new Vector2(introWidth, 1)), TextKeep));
            }

            foreach (var node in nodes)
            {
                var floor = node.Floor;
                var inner = Rect.MinMaxRect(floor.xMin + node.Margins[(int)Wall.Left], floor.yMin + node.Margins[(int)Wall.Bottom],
                    floor.xMax - node.Margins[(int)Wall.Right], floor.yMax - node.Margins[(int)Wall.Top]);
                // 露天的題目區不擠成一塊：每個題組各有一座小台子，平均散在整個廣場上；入口廣場照舊排成一塊，一下橋就看得到
                var spread = node.Spec.open && node != entry ? Spread(node.Spec.groups, inner) : null;
                if (spread != null)
                {
                    foreach (var (pack, topLeft) in spread)
                    {
                        var area = Rect.MinMaxRect(topLeft.x - PlotPadding, topLeft.y - pack.Height - PlotPadding, topLeft.x + pack.Width + PlotPadding, topLeft.y + PlotPadding);
                        plan.Plots.Add((area, Accent(node.Spec.accent), false));
                        plan.Props.Add(new Prop { Kind = PropKind.Dais, Center = area.center, Size = area.size });
                        var keep = Grow(Rect.MinMaxRect(area.xMin, area.yMin - Art.DaisRise, area.xMax, area.yMax), KeepClear);
                        node.Keep.Add(keep);
                        groundKeep.Add(keep);
                        Place(plan, pack, topLeft);
                    }
                    node.Plot = inner;
                    plan.Groups.AddRange(node.Spec.groups);
                }
                else
                {
                    var content = node.Content;
                    // 題目區依名稱偏向一邊，另一邊留出空地放擺設；入口廣場的題目靠左下、就在橋頭那條路上方，一過橋就看得到
                    int lean = Hash(node.Spec.key + "/content");
                    float fx = node == entry ? 0 : new[] { 0.2f, 0.5f, 0.8f }[lean % 3];
                    float fy = node == entry ? 0 : new[] { 0.3f, 0.5f, 0.7f }[lean / 3 % 3];
                    var topLeft = new Vector2(inner.xMin + (inner.width - content.Width) * fx, inner.yMin + content.Height + (inner.height - content.Height) * fy);
                    node.Plot = Rect.MinMaxRect(topLeft.x - PlotPadding, topLeft.y - content.Height - PlotPadding, topLeft.x + content.Width + PlotPadding, topLeft.y + PlotPadding);
                    plan.Plots.Add((node.Plot, Accent(node.Spec.accent), false));
                    plan.Props.Add(new Prop { Kind = PropKind.Dais, Center = node.Plot.center, Size = node.Plot.size });
                    var plot = Grow(Rect.MinMaxRect(node.Plot.xMin, node.Plot.yMin - Art.DaisRise, node.Plot.xMax, node.Plot.yMax), KeepClear);
                    node.Keep.Add(plot);
                    if (node.Spec.open) groundKeep.Add(plot);
                    Place(plan, content, topLeft);
                    plan.Groups.AddRange(node.Spec.groups);
                }

                for (int i = 0; i < node.Texts.Length; i++)
                {
                    var at = new Vector2(floor.center.x, floor.yMax - node.TextTop - TopMargin - i * TextSpacing);
                    plan.Texts.Add(("Hint", node.Texts[i], at, floor.width - 2 * SideMargin, TextFontSize));
                    float length = node.Texts[i].Length * CharWidth;
                    node.Keep.Add(Grow(new Rect(at - new Vector2(length / 2, 0.5f), new Vector2(length, 1)), TextKeep));
                }
            }

            foreach (var (spec, a, b, wall, gate) in doors)
            {
                Rect doorway;
                Vector2 gateTopLeft = default;
                if (a != null)
                {
                    if (Overlap(a.Floor, b.Floor, wall == Wall.Left || wall == Wall.Right) < DoorHeight + 2 * DoorKeep)
                        throw new InvalidOperationException($"{a.Spec.key} 與 {b.Spec.key} 共用的牆太短，開不了門");
                    doorway = Doorway(a.Floor, b.Floor, wall);
                    if (gate != null) gateTopLeft = GateTopLeft(a.Floor, doorway, wall, spec.side, gate);
                    a.Keep.Add(Grow(doorway, DoorKeep));
                }
                else
                {
                    // 地面上的門：開在 b 這面牆的正中間，門前題目貼著牆外、在門口旁邊
                    var f = b.Floor;
                    const float far = 100;
                    Rect outside;
                    switch (wall)
                    {
                        case Wall.Left:
                            doorway = new Rect(f.xMin - t, f.center.y - DoorHeight / 2, t, DoorHeight);
                            outside = Rect.MinMaxRect(f.xMin - t - far, f.yMin, f.xMin - t, f.yMax);
                            break;
                        case Wall.Right:
                            doorway = new Rect(f.xMax, f.center.y - DoorHeight / 2, t, DoorHeight);
                            outside = Rect.MinMaxRect(f.xMax + t, f.yMin, f.xMax + t + far, f.yMax);
                            break;
                        case Wall.Top:
                            doorway = new Rect(f.center.x - DoorHeight / 2, f.yMax, DoorHeight, t);
                            outside = Rect.MinMaxRect(f.xMin, f.yMax + t, f.xMax, f.yMax + t + far);
                            break;
                        default:
                            doorway = new Rect(f.center.x - DoorHeight / 2, f.yMin - t, DoorHeight, t);
                            outside = Rect.MinMaxRect(f.xMin, f.yMin - t - far, f.xMax, f.yMin - t);
                            break;
                    }
                    if (gate != null)
                    {
                        gateTopLeft = GateTopLeft(outside, doorway, Opposite(wall), spec.side, gate);
                        groundKeep.Add(Grow(new Rect(gateTopLeft.x, gateTopLeft.y - gate.Height, gate.Width, gate.Height), KeepClear));
                    }
                    groundKeep.Add(Grow(doorway, DoorKeep));
                }
                plan.Doorways.Add(doorway);
                b.Keep.Add(Grow(doorway, DoorKeep));
                var ids = new List<string>();
                if (gate != null)
                {
                    Place(plan, gate, gateTopLeft);
                    a?.Keep.Add(Grow(new Rect(gateTopLeft.x, gateTopLeft.y - gate.Height, gate.Width, gate.Height), KeepClear));
                    plan.Groups.Add(spec.gate);
                    ids.AddRange(Answerable(new[] { spec.gate }));
                }
                foreach (var key in spec.needs ?? new string[0]) ids.AddRange(Answerable(byKey[key].Spec.groups));
                if (ids.Count == 0) throw new InvalidOperationException($"{spec.a}–{spec.b} 的門沒有任何要解開的題目");
                plan.Doors.Add((plan.Doorways.Count - 1, ids, new[] { a != null ? a.Index : ground, b.Index }));
                // 地面上的門 wall 是 b 朝外的那面，從地面往 b 走是反方向
                Stairs(plan, doorway, a != null ? wall : Opposite(wall), a != null ? a.Spec.level + 1 : 0, b.Spec.level + 1);
            }

            foreach (var node in nodes.Where(node => !node.Spec.open))
                Carve(plan, node, new[] { Wall.Left, Wall.Right, Wall.Top, Wall.Bottom }.Where(wall => flush.Contains((node, wall))).ToList());

            // 地面：整塊方形扣掉每間房（連牆），再挖掉沒有東西的格子，島的輪廓才不是方的。
            // 挖掉的格子在島的邊上就連外圍那圈一起挖；旁邊有房間或空地的那一面留 Outer，沿著它還有路走
            bool Used(int col, int row) => nodes.Any(node => node.Spec.col == col && node.Spec.row == row);
            var buildings = Enumerable.Range(0, plan.Floors.Count).Where(i => plan.Kinds[i] == RoomKind.Gated && plan.Floors[i].width > 0)
                .Select(i => Grow(plan.Floors[i], t)).ToList();
            for (int col = 0; col < columns; col++)
                for (int row = 0; row < rows; row++)
                {
                    if (Used(col, row)) continue;
                    var cell = Cell(new NodeSpec { col = col, row = row }, lift);
                    float Edge(int dc, int dr)
                    {
                        int c = col + dc, r = row + dr;
                        if (c < 0 || r < 0 || c >= columns || r >= rows) return -Outer;
                        return Used(c, r) ? Outer : 0;
                    }
                    buildings.Add(Rect.MinMaxRect(cell.xMin + Edge(-1, 0), cell.yMin + Edge(0, 1), cell.xMax - Edge(1, 0), cell.yMax - Edge(0, -1)));
                }
            var pieces = Decompose(square, buildings);
            var anchor = new Vector2(square.xMin + 0.5f, 0);
            int main = pieces.FindIndex(piece => piece.Contains(anchor));
            plan.Floors[ground] = pieces[main];
            for (int i = 0; i < pieces.Count; i++)
                if (i != main) plan.AddFloor(pieces[i], RoomKind.Field, "ground", FloorStyle.Tiles, null, ground);
            plan.AddFloor(bridge, RoomKind.Bridge, "ground", FloorStyle.Planks, null, ground);

            foreach (var node in nodes.Where(node => node.Spec.open))
            {
                node.Pieces = new List<Rect> { node.Floor };
                node.Keep.AddRange(groundKeep);
            }
            foreach (var node in nodes) Dress(plan, node);
            Grounds(plan, square.xMin);
        }

        static Wall Opposite(Wall wall) => wall == Wall.Left ? Wall.Right : wall == Wall.Right ? Wall.Left : wall == Wall.Top ? Wall.Bottom : Wall.Top;

        /// <summary>門兩邊高度不同時，在高的那一邊、緊貼門口放一段往上的樓梯，寬度跟門口一樣。</summary>
        static void Stairs(LevelPlan plan, Rect doorway, Wall wall, int levelA, int levelB)
        {
            if (levelA == levelB) return;
            int rise = Mathf.Abs(levelB - levelA);
            float run = (2 + rise) * StairTread;
            // wall 是從 a 往 b 的方向
            var toB = wall == Wall.Right ? Vector2.right : wall == Wall.Left ? Vector2.left : wall == Wall.Top ? Vector2.up : Vector2.down;
            var up = levelB > levelA ? toB : -toB;
            bool sideways = wall == Wall.Left || wall == Wall.Right;
            var edge = doorway.center + up * ((sideways ? doorway.width : doorway.height) / 2);
            var middle = edge + up * run / 2;
            var size = sideways ? new Vector2(run, doorway.height) : new Vector2(doorway.width, run);
            plan.Props.Add(new Prop { Kind = PropKind.Stairs, Center = middle, Size = size, Facing = up });
        }

        /// <summary>
        /// 島外的天空：遠近散著一些小浮島，草地底下露出一截土，上面種一兩棵樹或有個池塘。
        /// 玩家走不到外面，所以都不擋路；連底下的土都離每塊地板（含牆與影子）至少 <see cref="GroundClear"/>，也不在橋的兩側。
        /// </summary>
        static void Grounds(LevelPlan plan, float left)
        {
            var world = plan.Floors.Where((floor, i) => plan.Kinds[i] != RoomKind.Tutorial).ToList();
            var bounds = Rect.MinMaxRect(left, world.Min(f => f.yMin) - 12, world.Max(f => f.xMax) + 12, world.Max(f => f.yMax) + 12);
            var taken = plan.Floors.Concat(plan.Voids).Select(area => Grow(area, Art.WallThickness + GroundClear)).ToList();
            var random = new System.Random(7);
            float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);

            for (int attempt = 0, islets = 0; attempt < 3000 && islets < 40; attempt++)
            {
                var size = new Vector2(Snap(Range(3, 9)), Snap(Range(2, 5)));
                var center = new Vector2(Range(bounds.xMin + size.x / 2, bounds.xMax - size.x / 2), Range(bounds.yMin + size.y / 2, bounds.yMax - size.y / 2));
                var area = new Rect(center - size / 2, size);
                var whole = Rect.MinMaxRect(area.xMin, area.yMin - size.y * Art.IsletDepth, area.xMax, area.yMax);
                if (taken.Any(other => other.Overlaps(Grow(whole, 1.5f)))) continue;
                taken.Add(Grow(whole, 1.5f));
                islets++;
                plan.Props.Add(new Prop { Kind = PropKind.Islet, Center = center, Size = size });
                plan.Props.Add(new Prop { Kind = PropKind.Grass, Center = center, Size = size });
                if (random.Next(4) == 0 && size.x >= 5 && size.y >= 3)
                {
                    plan.Props.Add(new Prop { Kind = PropKind.Pond, Center = center, Size = size - new Vector2(2.4f, 1.6f) });
                    continue;
                }
                int trees = random.Next(3);
                for (int k = 0; k < trees; k++)
                {
                    var at = new Vector2(Range(area.xMin + 1.2f, area.xMax - 1.2f), Range(area.yMin + 1.2f, area.yMax - 1.2f));
                    plan.Props.Add(new Prop { Kind = PropKind.Tree, Center = at, Size = Vector2.one * Range(1.4f, 2.2f), Accent = Art.Gold });
                }
            }
        }

        /// <summary>
        /// 房間不全是長方形：依名稱挑一種切法（L 形、十字、八角階梯、U 形凹口、鋸齒、T 形），從外框的角或牆中間切掉幾塊，
        /// 切掉的地方不碰到 <see cref="NodeLayout.Keep"/>，也不碰跟別間房共用的牆（flush），切掉的地方就成了街上的空地。
        /// 剩下的地板拆成幾塊長方形，含題目區的那塊當主要地板。
        /// </summary>
        static void Carve(LevelPlan plan, NodeLayout node, List<Wall> flush)
        {
            var room = node.Floor;
            int hash = Hash(node.Spec.key + "/shape");
            var cuts = new List<Rect>();
            bool Free(Rect cut) => !node.Keep.Any(keep => keep.Overlaps(cut));

            void Corner(int sx, int sy, float fw, float fh, int steps)
            {
                if (flush.Contains(sx < 0 ? Wall.Left : Wall.Right) || flush.Contains(sy < 0 ? Wall.Bottom : Wall.Top)) return;
                Rect At(float w, float h) => Rect.MinMaxRect(sx < 0 ? room.xMin : room.xMax - w, sy < 0 ? room.yMin : room.yMax - h,
                    sx < 0 ? room.xMin + w : room.xMax, sy < 0 ? room.yMin + h : room.yMax);
                float width = Snap(room.width * fw), height = Snap(room.height * fh);
                while (width >= MinCut && height >= MinCut && !Free(At(width, height)))
                {
                    if (width > height) width -= 0.5f;
                    else height -= 0.5f;
                }
                if (width < MinCut || height < MinCut) return;
                // 階梯：一階比一階窄、比一階高
                for (int i = 1; i <= steps; i++) cuts.Add(At(width * i / steps, height * (steps - i + 1) / steps));
            }

            bool Notch(Wall wall, float fl, float fd)
            {
                if (flush.Contains(wall)) return false;
                bool sideways = wall == Wall.Left || wall == Wall.Right;
                float along = Snap((sideways ? room.height : room.width) * fl);
                float middle = sideways ? room.center.y : room.center.x;
                Rect At(float d)
                {
                    switch (wall)
                    {
                        case Wall.Top: return Rect.MinMaxRect(middle - along / 2, room.yMax - d, middle + along / 2, room.yMax);
                        case Wall.Bottom: return Rect.MinMaxRect(middle - along / 2, room.yMin, middle + along / 2, room.yMin + d);
                        case Wall.Left: return Rect.MinMaxRect(room.xMin, middle - along / 2, room.xMin + d, middle + along / 2);
                        default: return Rect.MinMaxRect(room.xMax - d, middle - along / 2, room.xMax, middle + along / 2);
                    }
                }
                float depth = Snap((sideways ? room.width : room.height) * fd);
                while (depth >= MinCut && !Free(At(depth))) depth -= 0.5f;
                if (depth < MinCut) return false;
                cuts.Add(At(depth));
                return true;
            }

            var corners = new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) };
            var pick = corners[hash / 6 % 4];
            switch (hash % 6)
            {
                case 0: // L 形：一角切掉一大塊
                    Corner(pick.Item1, pick.Item2, 0.45f, 0.48f, 1);
                    break;
                case 1: // 十字：四角各切一塊
                    foreach (var (sx, sy) in corners) Corner(sx, sy, 0.28f, 0.32f, 1);
                    break;
                case 2: // 八角：四角兩段階梯
                    foreach (var (sx, sy) in corners) Corner(sx, sy, 0.26f, 0.3f, 2);
                    break;
                case 3: // U 形：一面牆中間凹進去，對面兩角小切
                    var walls = new[] { Wall.Bottom, Wall.Top, Wall.Right, Wall.Left };
                    var notched = walls.Skip(hash / 6 % 4).Concat(walls.Take(hash / 6 % 4)).FirstOrDefault(wall => Notch(wall, 0.38f, 0.34f));
                    foreach (var (sx, sy) in corners.Where(c => notched == Wall.Bottom ? c.Item2 > 0 : notched == Wall.Top ? c.Item2 < 0 : notched == Wall.Right ? c.Item1 < 0 : c.Item1 > 0))
                        Corner(sx, sy, 0.2f, 0.24f, 1);
                    break;
                case 4: // 鋸齒：對角兩角三段階梯
                    Corner(pick.Item1, pick.Item2, 0.36f, 0.4f, 3);
                    Corner(-pick.Item1, -pick.Item2, 0.36f, 0.4f, 3);
                    break;
                default: // T 形：同一邊的兩角切大
                    Corner(-1, pick.Item2, 0.32f, 0.44f, 1);
                    Corner(1, pick.Item2, 0.32f, 0.44f, 1);
                    break;
            }

            node.Pieces = Decompose(room, cuts);
            int main = node.Pieces.FindIndex(piece => piece.Contains(node.Plot.center));
            plan.Floors[node.Index] = node.Pieces[main];
            for (int i = 0; i < node.Pieces.Count; i++)
                if (i != main)
                    plan.AddFloor(node.Pieces[i], plan.Kinds[node.Index], node.Spec.key, Style(node.Spec.floor, FloorStyle.Tiles), Accent(node.Spec.accent), node.Index, node.Spec.level + 1);
        }

        /// <summary>
        /// 外框扣掉切掉的幾塊後拆成長方形：先依所有切線分成小格，再把相鄰的小格合併成橫條（或直條），取塊數少的那種拆法。
        /// 外框的邊照原本的值，不經過四捨五入，門口才剛好貼著地板。
        /// </summary>
        static List<Rect> Decompose(Rect room, List<Rect> cuts)
        {
            if (cuts.Count == 0) return new List<Rect> { room };
            List<float> Lines(float min, float max, IEnumerable<float> inner)
            {
                var lines = new List<float> { min };
                foreach (var v in inner.Where(v => v > min + 1e-3f && v < max - 1e-3f).OrderBy(v => v))
                    if (v > lines[lines.Count - 1] + 1e-3f) lines.Add(v);
                lines.Add(max);
                return lines;
            }
            var xs = Lines(room.xMin, room.xMax, cuts.SelectMany(cut => new[] { cut.xMin, cut.xMax }));
            var ys = Lines(room.yMin, room.yMax, cuts.SelectMany(cut => new[] { cut.yMin, cut.yMax }));
            bool Filled(int i, int j)
            {
                var center = new Vector2((xs[i] + xs[i + 1]) / 2, (ys[j] + ys[j + 1]) / 2);
                return !cuts.Any(cut => cut.Contains(center));
            }

            // rows：一列列由左而右合併成橫條，上下兩列的橫條一樣寬就接起來；否則一欄欄合併成直條
            List<Rect> Merge(bool rows)
            {
                int outer = rows ? ys.Count - 1 : xs.Count - 1, inner = rows ? xs.Count - 1 : ys.Count - 1;
                bool Cell(int o, int n) => rows ? Filled(n, o) : Filled(o, n);
                var result = new List<Rect>();
                var open = new Dictionary<(int, int), int>();
                for (int o = 0; o < outer; o++)
                {
                    var next = new Dictionary<(int, int), int>();
                    for (int n = 0; n < inner; n++)
                    {
                        if (!Cell(o, n)) continue;
                        int start = n;
                        while (n + 1 < inner && Cell(o, n + 1)) n++;
                        var key = (start, n);
                        if (open.TryGetValue(key, out int k))
                        {
                            var r = result[k];
                            result[k] = rows ? Rect.MinMaxRect(r.xMin, r.yMin, r.xMax, ys[o + 1]) : Rect.MinMaxRect(r.xMin, r.yMin, xs[o + 1], r.yMax);
                        }
                        else
                        {
                            result.Add(rows ? Rect.MinMaxRect(xs[start], ys[o], xs[n + 1], ys[o + 1]) : Rect.MinMaxRect(xs[o], ys[start], xs[o + 1], ys[n + 1]));
                            k = result.Count - 1;
                        }
                        next[key] = k;
                    }
                    open = next;
                }
                return result;
            }
            var across = Merge(true);
            var down = Merge(false);
            return across.Count <= down.Count ? across : down;
        }

        static float Snap(float value) => Mathf.Round(value * 2) / 2;

        static Rect Grow(Rect rect, float distance) => new Rect(rect.position - Vector2.one * distance, rect.size + Vector2.one * 2 * distance);

        /// <summary>
        /// 房間裡的擺設，依地板花紋決定主題：展覽（雕像、方柱）、書房（書架、木箱、長椅）、廣場（長椅、盆栽、噴水池）、庭園（樹、樹籬、水池）。
        /// 擺設沿著牆排成一列、間距一樣、兩端對稱，北牆掛旗子或靠書架，牆角放一件，地板中間空得夠大才放一件主擺設。
        /// 空地格子沒有牆，沿著格子的邊種一排樹。每件朝房間裡面離牆角、keep、別的擺設至少 <see cref="PropGap"/>，所以再多也不會把路堵死；放不下的就略過。
        /// </summary>
        static void Dress(LevelPlan plan, NodeLayout node)
        {
            var random = new System.Random(Hash(node.Spec.key));
            var accent = Accent(node.Spec.accent);
            var color = accent.HasValue && accent != BlockColor.Colorless ? Art.Swatch(accent.Value) : Art.Gold;
            var style = Style(node.Spec.floor, FloorStyle.Tiles);
            float t = Art.WallThickness;
            bool Inside(Rect rect) => !Shapes.Subtract(rect, node.Pieces).Any();
            bool Clear(Rect rect) => !node.Keep.Any(keep => keep.Overlaps(rect)) && !plan.Props.Any(prop => new Rect(prop.Center - prop.Size / 2, prop.Size).Overlaps(rect));
            // 這段邊外面真的有牆（不是門口、不是接著別塊地板）；空地格子的邊外面是街，當作有牆
            bool Walled(Rect outside) => node.Spec.open || !plan.Floors.Concat(plan.Doorways).Any(other => other.Overlaps(outside));

            // against：貼著的牆，那幾面不用留空
            bool Put(PropKind kind, Rect rect, params Wall[] against)
            {
                bool blocking = kind != PropKind.Rug && kind != PropKind.Banner && kind != PropKind.Pad && kind != PropKind.Terrace;
                var around = Grow(rect, blocking ? PropGap : RugGap);
                var inward = Rect.MinMaxRect(
                    against.Contains(Wall.Left) ? rect.xMin : around.xMin, against.Contains(Wall.Bottom) ? rect.yMin : around.yMin,
                    against.Contains(Wall.Right) ? rect.xMax : around.xMax, against.Contains(Wall.Top) ? rect.yMax : around.yMax);
                if (!Inside(inward) || !Clear(around)) return false;
                plan.Props.Add(new Prop { Kind = kind, Center = rect.center, Size = rect.size, Accent = color, Blocking = blocking });
                return true;
            }

            var teleport = node.Spec.teleport;
            if (teleport != null && !string.IsNullOrEmpty(teleport.en))
            {
                // 房間擠，隨機找常常找不到；依名稱打亂順序，把每個半格的點都試過
                var pad = Vector2.one * 1.8f;
                var candidates = node.Pieces.SelectMany(piece =>
                        Enumerable.Range(0, Mathf.FloorToInt(piece.width * 2)).SelectMany(i => Enumerable.Range(0, Mathf.FloorToInt(piece.height * 2))
                            .Select(j => new Vector2(Snap(piece.xMin) + i * 0.5f, Snap(piece.yMin) + j * 0.5f))))
                    .OrderBy(_ => random.Next()).ToList();
                bool placed = candidates.Any(center => Put(PropKind.Pad, new Rect(center - pad / 2, pad)));
                if (!placed) throw new InvalidOperationException($"{node.Spec.key} 放不下傳送台");
                plan.Teleports.Add((teleport.en, teleport.zh, plan.Props[plan.Props.Count - 1].Center, plan.Teleports.Count + 1));
            }

            var tree = Vector2.one * 1.9f;
            var planter = Vector2.one * 1.3f;
            var bench = new Vector2(2.4f, 0.8f);
            var statue = new Vector2(1.4f, 1.8f);
            var pillar = new Vector2(1.3f, 1.7f);
            var crate = new Vector2(1.3f, 1.4f);
            var flag = new Vector2(0.7f, 1.15f);
            var shelf = new Vector2(3.2f, 1.1f);
            (PropKind, Vector2)[] row, north;
            (PropKind, Vector2)? corner, middle;
            if (node.Spec.open)
            {
                row = north = new[] { (PropKind.Tree, tree) };
                corner = middle = null;
            }
            else switch (style)
            {
                case FloorStyle.Hatch:
                    row = north = new[] { (PropKind.Tree, tree), (PropKind.Hedge, new Vector2(2.6f, 0.9f)) };
                    corner = (PropKind.Tree, tree);
                    middle = (PropKind.Pool, new Vector2(3.8f, 2.4f));
                    break;
                case FloorStyle.Planks:
                    row = new[] { (PropKind.Crate, crate), (PropKind.Bench, bench) };
                    north = new[] { (PropKind.Shelf, shelf) };
                    corner = (PropKind.Crate, new Vector2(1.6f, 1.7f));
                    middle = (PropKind.Rug, new Vector2(4.4f, 2.8f));
                    break;
                case FloorStyle.Checker:
                    row = new[] { (PropKind.Bench, bench), (PropKind.Planter, planter) };
                    north = new[] { (PropKind.Banner, flag) };
                    corner = (PropKind.Planter, planter);
                    middle = (PropKind.Fountain, Vector2.one * 3f);
                    break;
                default:
                    row = new[] { (PropKind.Statue, statue), (PropKind.Pillar, pillar) };
                    north = new[] { (PropKind.Banner, flag) };
                    corner = (PropKind.Planter, planter);
                    middle = (PropKind.Terrace, new Vector2(6.5f, 3.6f));
                    break;
            }

            // 牆角：兩面都是真的牆才放
            if (corner.HasValue)
                foreach (var piece in node.Pieces)
                    foreach (var (sx, sy) in new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) })
                    {
                        var size = corner.Value.Item2;
                        float top = sy > 0 ? Art.WallFaceDepth + Hug : Hug;
                        var x = sx < 0 ? piece.xMin + Hug : piece.xMax - Hug - size.x;
                        var y = sy < 0 ? piece.yMin + Hug : piece.yMax - top - size.y;
                        var rect = new Rect(x, y, size.x, size.y);
                        var side = sx < 0 ? Rect.MinMaxRect(piece.xMin - t, rect.yMin, piece.xMin, rect.yMax) : Rect.MinMaxRect(piece.xMax, rect.yMin, piece.xMax + t, rect.yMax);
                        var end = sy < 0 ? Rect.MinMaxRect(rect.xMin, piece.yMin - t, rect.xMax, piece.yMin) : Rect.MinMaxRect(rect.xMin, piece.yMax, rect.xMax, piece.yMax + t);
                        if (Walled(side) && Walled(end)) Put(corner.Value.Item1, rect, sx < 0 ? Wall.Left : Wall.Right, sy < 0 ? Wall.Bottom : Wall.Top);
                    }

            // 牆邊一列：留出牆角，其餘依最大那件的長度加 PropGap 排開、置中，從兩端往中間交替
            const float CornerRoom = 2.2f;
            foreach (var piece in node.Pieces)
                foreach (var wall in new[] { Wall.Top, Wall.Left, Wall.Right, Wall.Bottom })
                {
                    bool sideways = wall == Wall.Left || wall == Wall.Right;
                    var items = wall == Wall.Top ? north : row;
                    // 左右兩面牆上，長條的擺設轉成直的
                    Vector2 Turn(Vector2 size) => sideways && size.x > size.y ? new Vector2(size.y, size.x) : size;
                    // 空地上的樹和牆上的旗子排疏一點，不然像一圈圍籬
                    float spacing = node.Spec.open ? 6f : items[0].Item1 == PropKind.Banner ? 4.5f : 0;
                    float pitch = Mathf.Max(spacing, items.Max(item => sideways ? Turn(item.Item2).y : item.Item2.x) + PropGap + 0.3f);
                    float from = (sideways ? piece.yMin : piece.xMin) + CornerRoom, to = (sideways ? piece.yMax : piece.xMax) - CornerRoom;
                    if (to - from < 0) continue;
                    int count = Mathf.FloorToInt((to - from) / pitch) + 1;
                    float start = (from + to) / 2 - (count - 1) * pitch / 2;
                    for (int i = 0; i < count; i++)
                    {
                        var (kind, raw) = items[Mathf.Min(i, count - 1 - i) % items.Length];
                        var size = Turn(raw);
                        float along = start + i * pitch;
                        // 旗子掛在牆面上、書架靠著牆面，其他的放在牆面前面
                        float hug = wall != Wall.Top ? Hug : kind == PropKind.Banner ? 0.02f : kind == PropKind.Shelf ? 0.1f : Art.WallFaceDepth + Hug;
                        Rect rect, outside;
                        switch (wall)
                        {
                            case Wall.Top:
                                rect = new Rect(along - size.x / 2, piece.yMax - hug - size.y, size.x, size.y);
                                outside = Rect.MinMaxRect(rect.xMin - 0.3f, piece.yMax, rect.xMax + 0.3f, piece.yMax + t);
                                break;
                            case Wall.Bottom:
                                rect = new Rect(along - size.x / 2, piece.yMin + hug, size.x, size.y);
                                outside = Rect.MinMaxRect(rect.xMin - 0.3f, piece.yMin - t, rect.xMax + 0.3f, piece.yMin);
                                break;
                            case Wall.Left:
                                rect = new Rect(piece.xMin + hug, along - size.y / 2, size.x, size.y);
                                outside = Rect.MinMaxRect(piece.xMin - t, rect.yMin - 0.3f, piece.xMin, rect.yMax + 0.3f);
                                break;
                            default:
                                rect = new Rect(piece.xMax - hug - size.x, along - size.y / 2, size.x, size.y);
                                outside = Rect.MinMaxRect(piece.xMax, rect.yMin - 0.3f, piece.xMax + t, rect.yMax + 0.3f);
                                break;
                        }
                        if (Walled(outside)) Put(kind, rect, wall);
                    }
                }

            // 主擺設：先試題目區和四面牆之間的空地正中間，再試每塊地板的正中間，放得下一件就好
            if (middle.HasValue)
            {
                var (kind, size) = middle.Value;
                var main = node.Pieces.First(piece => piece.Contains(node.Plot.center));
                var spots = new List<Vector2>
                {
                    new Vector2((main.xMin + node.Plot.xMin) / 2, node.Plot.center.y), new Vector2((node.Plot.xMax + main.xMax) / 2, node.Plot.center.y),
                    new Vector2(node.Plot.center.x, (main.yMin + node.Plot.yMin) / 2), new Vector2(node.Plot.center.x, (node.Plot.yMax + main.yMax) / 2),
                };
                spots.AddRange(node.Pieces.OrderByDescending(piece => piece.width * piece.height).Select(piece => piece.center));
                foreach (var spot in spots)
                    if (Put(kind, new Rect(new Vector2(Snap(spot.x), Snap(spot.y)) - size / 2, size))) break;
            }
        }

        /// <summary>b 在 a 的哪一面牆外；兩間房要在格子上相鄰。</summary>
        static Wall Facing(NodeSpec a, NodeSpec b)
        {
            if (a.row == b.row && b.col == a.col + 1) return Wall.Right;
            if (a.row == b.row && a.col == b.col + 1) return Wall.Left;
            if (a.col == b.col && b.row == a.row + 1) return Wall.Bottom;
            if (a.col == b.col && a.row == b.row + 1) return Wall.Top;
            throw new InvalidOperationException($"{a.key} 與 {b.key} 在格子上不相鄰，開不了門");
        }

        /// <summary>兩間房沿著牆的方向重疊的長度。</summary>
        static float Overlap(Rect a, Rect b, bool sideways) => sideways
            ? Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin)
            : Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);

        /// <summary>
        /// 共用一道牆的 a、b 之間的門口，對齊兩間房重疊那一段的中間。
        /// 門口直接取兩間之間的空隙，不另外加減牆厚，免得浮點誤差讓門口和地板重疊一點點。
        /// </summary>
        static Rect Doorway(Rect a, Rect b, Wall wall)
        {
            bool sideways = wall == Wall.Left || wall == Wall.Right;
            float middle = sideways
                ? (Mathf.Max(a.yMin, b.yMin) + Mathf.Min(a.yMax, b.yMax)) / 2
                : (Mathf.Max(a.xMin, b.xMin) + Mathf.Min(a.xMax, b.xMax)) / 2;
            float near = wall == Wall.Right ? a.xMax : wall == Wall.Left ? a.xMin : wall == Wall.Top ? a.yMax : a.yMin;
            float far = wall == Wall.Right ? b.xMin : wall == Wall.Left ? b.xMax : wall == Wall.Top ? b.yMin : b.yMax;
            if (Mathf.Abs(far - near) > Art.WallThickness + 1e-3f) throw new InvalidOperationException("共用牆的兩間房之間隔太遠");
            return sideways
                ? Rect.MinMaxRect(Mathf.Min(near, far), middle - DoorHeight / 2, Mathf.Max(near, far), middle + DoorHeight / 2)
                : Rect.MinMaxRect(middle - DoorHeight / 2, Mathf.Min(near, far), middle + DoorHeight / 2, Mathf.Max(near, far));
        }

        /// <summary>門前題目貼著 a 的這面牆、在門口旁邊錯開一小段，不擋住門口。</summary>
        static Vector2 GateTopLeft(Rect a, Rect doorway, Wall wall, int side, Pack gate)
        {
            bool positive = side >= 0;
            switch (wall)
            {
                case Wall.Left:
                case Wall.Right:
                    float x = wall == Wall.Right ? a.xMax - GateWallGap - gate.Width : a.xMin + GateWallGap;
                    float y = positive ? doorway.yMax + GateSideGap + gate.Height : doorway.yMin - GateSideGap;
                    return new Vector2(x, y);
                default:
                    float left = positive ? doorway.xMax + GateSideGap : doorway.xMin - GateSideGap - gate.Width;
                    float top = wall == Wall.Top ? a.yMax - GateWallGap : a.yMin + GateWallGap + gate.Height;
                    return new Vector2(left, top);
            }
        }

        /// <summary>依名稱算的固定雜湊（string.GetHashCode 每次執行可能不同）。</summary>
        static int Hash(string key)
        {
            int hash = 17;
            foreach (var c in key) hash = unchecked(hash * 31 + c);
            return hash & 0x7fffffff;
        }

        static FloorStyle Style(string name, FloorStyle fallback) => string.IsNullOrEmpty(name) ? fallback : Parse<FloorStyle>(name);

        static void Place(LevelPlan plan, Pack pack, Vector2 topLeft)
        {
            foreach (var (spec, position) in pack.Blocks) plan.Blocks.Add((spec, topLeft + position, false));
        }

        static List<string> Answerable(IEnumerable<GroupSpec> groups) =>
            groups.SelectMany(group => group.blocks).Where(block => !string.IsNullOrEmpty(block.answer)).Select(block => block.id).ToList();

        static BlockColor? Accent(string name) => string.IsNullOrEmpty(name) ? (BlockColor?)null : Parse<BlockColor>(name);

        /// <summary>
        /// 把題組各自排好，平均分到 inner 裡的幾欄幾列，每格正中間放一組；格子放不下（含台子與四周走得過去的空間）就回傳 null。
        /// 欄數挑每格剩下的空間最多的。
        /// </summary>
        static List<(Pack pack, Vector2 topLeft)> Spread(IList<GroupSpec> groups, Rect inner)
        {
            if (groups.Count < 2) return null;
            var packs = groups.Select(group => Arrange(new[] { group }, ZoneAspect)).ToList();
            float needW = packs.Max(pack => pack.Width) + 2 * PlotPadding + SpreadGap;
            float needH = packs.Max(pack => pack.Height) + 2 * PlotPadding + Art.DaisRise + SpreadGap;
            int best = 0;
            float bestRoom = 0;
            for (int columns = 1; columns <= groups.Count; columns++)
            {
                int rows = (groups.Count + columns - 1) / columns;
                float room = Mathf.Min(inner.width / columns - needW, inner.height / rows - needH);
                if (room >= 0 && (best == 0 || room > bestRoom)) (best, bestRoom) = (columns, room);
            }
            if (best == 0) return null;
            int rowCount = (groups.Count + best - 1) / best;
            float cellW = inner.width / best, cellH = inner.height / rowCount;
            var result = new List<(Pack, Vector2)>();
            for (int i = 0; i < packs.Count; i++)
            {
                int row = i / best, inRow = Mathf.Min(best, packs.Count - row * best);
                // 最後一列不滿就置中
                float x = inner.xMin + (inner.width - inRow * cellW) / 2 + (i % best + 0.5f) * cellW;
                float y = inner.yMax - (row + 0.5f) * cellH;
                result.Add((packs[i], new Vector2(Snap(x - packs[i].Width / 2), Snap(y + packs[i].Height / 2))));
            }
            return result;
        }

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

        static T Parse<T>(string value) where T : struct => (T)Enum.Parse(typeof(T), value, true);
    }
}

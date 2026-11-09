using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Lexiscript
{
    /// <summary>地板的花紋。教學房間維持原本的 <see cref="Plain"/>（大塊幾何與點陣）。</summary>
    public enum FloorStyle
    {
        Plain,
        /// <summary>對齊方塊格距的細格線，像磁磚。</summary>
        Tiles,
        /// <summary>沿長邊的木板條與錯開的接縫。</summary>
        Planks,
        /// <summary>淡淡的棋盤格。</summary>
        Checker,
        /// <summary>45° 細斜紋。</summary>
        Hatch,
    }

    public enum PropKind
    {
        /// <summary>方柱（擋路）。</summary>
        Pillar,
        /// <summary>圓形盆栽（擋路）。</summary>
        Planter,
        /// <summary>地毯，只畫在地上。</summary>
        Rug,
        /// <summary>走道邊的小地燈，只畫在地上。</summary>
        Lamp,
        /// <summary>橋欄杆上的柱頭，畫在牆上。</summary>
        Post,
        /// <summary>傳送台，只畫在地上。</summary>
        Pad,
        /// <summary>木箱（擋路）。</summary>
        Crate,
        /// <summary>長椅（擋路）。</summary>
        Bench,
        /// <summary>台座上的雕像（擋路）。</summary>
        Statue,
        /// <summary>圓形噴水池（擋路）。</summary>
        Fountain,
        /// <summary>靠著北牆的書架（擋路）。</summary>
        Shelf,
        /// <summary>樹（擋路）。</summary>
        Tree,
        /// <summary>往下凹的水池（擋路）。</summary>
        Pool,
        /// <summary>墊高一階的平台，南面中間有台階，走得上去。</summary>
        Terrace,
        /// <summary>掛在北牆上的旗子。</summary>
        Banner,
        /// <summary>兩邊高度不同的走道上的樓梯，Facing 是往上的方向。</summary>
        Stairs,
        /// <summary>題目區底下墊高的台子，只畫南面的側邊。</summary>
        Dais,
        /// <summary>房間裡的矮石牆（擋路）。</summary>
        LowWall,
        /// <summary>建築外面的草地。</summary>
        Grass,
        /// <summary>建築外面的樹籬。</summary>
        Hedge,
        /// <summary>建築外面的池塘。</summary>
        Pond,
        /// <summary>天空裡的小浮島：Size 是頂面，底下畫一截往下收的土；頂面另外疊一塊草地。</summary>
        Islet,
    }

    /// <summary>地圖上的一件擺設；Blocking 的會加上碰撞，玩家走不過去。</summary>
    public struct Prop
    {
        public PropKind Kind;
        public Vector2 Center, Size;
        public Color Accent;
        public bool Blocking;
        /// <summary>有方向的擺設（樓梯）朝哪邊。</summary>
        public Vector2 Facing;
    }

    /// <summary><see cref="Art.Rooms"/> 的地板花紋與擺設；Styles、Tints 與地板一一對應。</summary>
    public sealed class RoomsDecor
    {
        public IList<FloorStyle> Styles;
        /// <summary>地板帶一點這個顏色；null 是原本的地板色。</summary>
        public IList<Color?> Tints;
        public IList<Prop> Props;
        /// <summary>每塊地板的高度（0 起算）；越高南面露出的側面越厚、地板越亮。</summary>
        public IList<int> Levels;
    }

    /// <summary>配色、尺寸與各種網格的畫法。調整外觀只改這裡的常數。</summary>
    public static class Art
    {
        public const float Pitch = 2.5f;
        public const float Half = 1f;
        const float Chamfer = 0.30f;
        const float Outline = 0.025f;
        const float FaceInset = 0.24f;
        const float DashThickness = 0.03f, DashLength = 0.10f, DashGap = 0.06f, DashExtent = 0.64f;
        const float BoxLeft = -0.64f, BoxRight = 0.64f, BoxTop = -0.12f, BoxBottom = -0.64f;
        const float HatchPitch = 0.16f, HatchWidth = 0.06f;
        const float DiamondOuter = 0.22f, DiamondInner = 0.185f, ChevronStroke = 0.04f;
        const float BridgeWidth = 0.12f;
        // 方塊之間的縫寬 Pitch − 2 × Half = 0.5；直徑 0.4 的碰撞體扣掉兩側的 contact offset 還有餘裕穿過去
        public const float PlayerHalf = 0.2f;
        const float PlayerOutline = 0.04f;

        public const float CameraSize = 5f;
        public const float MinCameraSize = 2.5f, MaxCameraSize = 14f;

        public static readonly Vector2 CluePosition = new Vector2(0, 0.38f);
        public static readonly Vector2 ClueSize = new Vector2(1.36f, 0.50f);
        public static readonly Vector2 AnswerPosition = new Vector2(0, (BoxTop + BoxBottom) / 2);
        public static readonly Vector2 AnswerSize = new Vector2(BoxRight - BoxLeft - 0.12f, BoxTop - BoxBottom - 0.08f);
        public const float ClueFontMin = 1.0f, ClueFontMax = 3.0f;
        public const float AnswerFontMin = 0.8f, AnswerFontMax = 2.6f;

        public const float WallThickness = 1f;

        /// <summary>天空：整個世界浮在空中。</summary>
        public static readonly Color Background = Hex(0xBCD6EA);
        public static readonly Color FloorColor = Hex(0xDDE0E5);
        public static readonly Color Wall = Hex(0x8B919B);
        public static readonly Color Ink = Hex(0x16181D);
        public static readonly Color Face = Hex(0x3A3F48);
        public static readonly Color Paper = Hex(0xFCFCFA);
        public static readonly Color PlaceholderInk = Hex(0xA9AEB7);
        public static readonly Color Correct = Hex(0x15994F);
        /// <summary>強調色淺金：答對特效與介面重點。</summary>
        public static readonly Color Gold = Hex(0xF2D27A);
        /// <summary>淡金：已解開的答案欄底色。比淺金淡，答對的綠字才看得清楚。</summary>
        public static readonly Color PaleGold = Hex(0xF8EBC2);
        public static readonly Color Hatch = Hex(0x9EA4AE);
        public static readonly Color FloorInk = Hex(0x5C626D);
        public static readonly Color DoorColor = Hex(0x4A505A);
        public static readonly Color SpoilerBar = Hex(0x1B1D22);
        public static readonly Color SpoilerOpen = Hex(0xEEF0F3);
        public static readonly Color Selection = new Color(0.30f, 0.55f, 1f, 0.35f);
        /// <summary>畫面正上方顏色名稱與提示句的底色。</summary>
        public static readonly Color LabelBackground = new Color(0.086f, 0.094f, 0.114f, 0.85f);
        /// <summary>介面（uGUI）文字選取範圍的 TMP &lt;mark&gt; 色碼。</summary>
        public const string SelectionMark = "#4C8DFF55";

        /// <summary>答案欄選取底色畫在方塊（0）之上、方塊文字（2）之下。</summary>
        public const int SelectionOrder = 1;

        /// <summary>世界物件的 sortingOrder，數字大的蓋在上面。房間遮罩蓋住房間裡的一切，角色永遠在最上層。</summary>
        public const int RoomOrder = -20, FloorTextOrder = -15, DoorOrder = -12, BridgeOrder = -10, BlockOrder = 0;
        public const int BlockTextOrder = SelectionOrder + 1, SpoilerOrder = 3, EffectOrder = 6, CoverOrder = 20, PlayerOrder = 50;

        // 跟地板只差幾個百分點的亮度，看得出圖形但不會干擾方塊顏色與文字
        static readonly Color[] Decor = { Hex(0xD4D8DE), Hex(0xE4E6EA), Hex(0xD8DBE1), Hex(0xE1E3E8) };

        static readonly Dictionary<BlockColor, Color> Palette = new Dictionary<BlockColor, Color>
        {
            { BlockColor.PureWhite, Hex(0xFAFAFA) },
            { BlockColor.PureBlack, Hex(0x0A0A0C) },
            { BlockColor.LightGray, Hex(0xA3A8B1) },
            { BlockColor.BrightYellow, Hex(0xFFD60A) },
            { BlockColor.RoyalBlue, Hex(0x2F5BE0) },
            { BlockColor.RoseRed, Hex(0xD7263D) },
            { BlockColor.EmeraldGreen, Hex(0x12A15A) },
            { BlockColor.DeepPurple, Hex(0x6331AE) },
            // 不用 #FF00FF：那是 Unity 錯誤 shader 的顏色，截圖時會分不出來
            { BlockColor.Magenta, Hex(0xD21FC5) },
            { BlockColor.SkyCyan, Hex(0x29C4EA) },
            { BlockColor.SunsetOrange, Hex(0xFF7A1A) },
            { BlockColor.GoldenBrown, Hex(0xA8701E) },
        };

        /// <summary>方塊色框的顏色。無色沒有自己的顏色，色框是地板色再疊斜紋。</summary>
        public static Color Swatch(BlockColor color) => Palette.TryGetValue(color, out var swatch) ? swatch : FloorColor;

        static Color Hex(int rgb) =>
            new Color((rgb >> 16 & 0xFF) / 255f, (rgb >> 8 & 0xFF) / 255f, (rgb & 0xFF) / 255f);

        static float ChamferOf(BlockShape shape) => shape == BlockShape.Beveled ? Chamfer : 0;

        static List<Vector2> Ring(BlockShape shape, float inset) =>
            Shapes.Octagon(Half - inset, Shapes.InsetChamfer(ChamferOf(shape), inset));

        /// <summary>方塊外輪廓（以格中心為原點）。</summary>
        public static List<Vector2> BlockOutline(BlockShape shape) => Ring(shape, 0);

        /// <summary>色框內側的深灰內面；滑鼠在外輪廓內、內面外就是指在色框上。</summary>
        public static List<Vector2> BlockFace(BlockShape shape) => Ring(shape, FaceInset);

        /// <summary>
        /// 畫一格方塊（以格中心為原點）：細外框、色框、深灰內面、白色虛線與白色答案欄，最後是指向符號。
        /// 純色格只有外框與整格填色。
        /// </summary>
        public static void Block(MeshBuilder mesh, BlockColor color, BlockShape shape, bool pure, IEnumerable<Vector2> pointerDirections)
        {
            mesh.Add(Ring(shape, 0), Ink);
            var fill = Ring(shape, Outline);
            if (color == BlockColor.Colorless)
            {
                mesh.Add(fill, FloorColor);
                var across = new Vector2(1, -1).normalized;
                int stripes = Mathf.CeilToInt(Half * Mathf.Sqrt(2) / HatchPitch);
                for (int k = -stripes; k <= stripes; k++)
                {
                    var stripe = Shapes.Clip(fill, across, HatchPitch * k + HatchWidth);
                    mesh.Add(Shapes.Clip(stripe, -across, -HatchPitch * k), Hatch);
                }
            }
            else
            {
                mesh.Add(fill, Palette[color]);
            }

            if (!pure)
            {
                mesh.Add(Ring(shape, FaceInset), Face);
                int dashes = Mathf.RoundToInt((2 * DashExtent + DashGap) / (DashLength + DashGap));
                float start = -(dashes * (DashLength + DashGap) - DashGap) / 2;
                for (int i = 0; i < dashes; i++)
                {
                    float x = start + i * (DashLength + DashGap);
                    mesh.Add(Shapes.Rect(new Vector2(x, -DashThickness / 2), new Vector2(x + DashLength, DashThickness / 2)), Paper);
                }
                mesh.Add(AnswerBox(shape), Paper);
            }

            foreach (var direction in pointerDirections) Pointer(mesh, direction * Half, direction);
        }

        /// <summary>
        /// 白色答案欄。斜切方塊的欄位下緣兩角也切掉，讓它跟斜邊之間的深灰邊距一樣寬。
        /// </summary>
        public static List<Vector2> AnswerBox(BlockShape shape)
        {
            float cut = Shapes.InsetChamfer(ChamferOf(shape), Half + BoxBottom);
            if (cut <= 0) return Shapes.Rect(new Vector2(BoxLeft, BoxBottom), new Vector2(BoxRight, BoxTop));
            return new List<Vector2>
            {
                new Vector2(BoxLeft + cut, BoxBottom), new Vector2(BoxRight - cut, BoxBottom),
                new Vector2(BoxRight, BoxBottom + cut), new Vector2(BoxRight, BoxTop),
                new Vector2(BoxLeft, BoxTop), new Vector2(BoxLeft, BoxBottom + cut),
            };
        }

        /// <summary>菱形指向符號，箭頭朝 direction。</summary>
        public static void Pointer(MeshBuilder mesh, Vector2 center, Vector2 direction)
        {
            var side = new Vector2(-direction.y, direction.x);
            var apex = center + 0.06f * direction;
            mesh.Add(Shapes.Diamond(center, DiamondOuter), Ink);
            mesh.Add(Shapes.Diamond(center, DiamondInner), Paper);
            mesh.Add(Shapes.Segment(center - 0.05f * direction + 0.09f * side, apex, ChevronStroke), Ink);
            mesh.Add(Shapes.Segment(center - 0.05f * direction - 0.09f * side, apex, ChevronStroke), Ink);
        }

        /// <summary>
        /// 兩格中心之間的帶狀連接橋，白底細黑邊。它畫在方塊底下，所以只露出縫隙那一段，不必計算接點。
        /// </summary>
        public static void Bridge(MeshBuilder mesh, Vector2 from, Vector2 to)
        {
            mesh.Add(Shapes.Segment(from, to, BridgeWidth + 2 * Outline), Ink);
            mesh.Add(Shapes.Segment(from, to, BridgeWidth), Paper);
        }

        /// <summary>指向符號畫在來源格朝目標那一側的邊上，取相對位置的主要軸。</summary>
        public static Vector2 Direction(Vector2 delta) =>
            Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                ? new Vector2(Mathf.Sign(delta.x), 0)
                : new Vector2(0, Mathf.Sign(delta.y));

        /// <summary>
        /// 牆、地板、房間之間的門口，以及地板上的平面裝飾；plots 是大地圖分區與門後房間的地面標記（見 <see cref="Plot"/>）。
        /// 牆先畫成比地板大一圈的矩形，再用地板蓋掉內側，相鄰房間共用的牆就只剩一道。
        /// </summary>
        public static Mesh Rooms(IList<Rect> floors, IList<Rect> doorways, int seed, IList<(Rect area, BlockColor? accent, bool flipped)> plots = null,
            RoomsDecor decor = null)
        {
            var mesh = new MeshBuilder();
            var wall = Vector2.one * WallThickness;
            var random = new System.Random(seed);
            // 由低往高一層層畫（影子、牆、地板、花紋）：高的房間蓋在地面上
            foreach (var group in Enumerable.Range(0, floors.Count).GroupBy(i => LevelOf(decor, i)).OrderBy(group => group.Key))
            {
                int level = group.Key;
                if (level == 0)
                {
                    // 地面整片浮在天空上，影子落在背景上，用不透明的顏色，好幾塊地板的影子疊在一起也不會更深
                    foreach (int i in group) mesh.Add(Shapes.Rect(floors[i].min - wall + ShadowStep, floors[i].max + wall + ShadowStep), ShadowColor);
                }
                else
                {
                    // 墊高的房間是坐在地上的台子：牆底下露出一截朝南的側面（越高越厚），再貼著地一道很淡的影子，不拖出整片影子
                    float face = PlinthStep * level;
                    foreach (int i in group)
                    {
                        var f = floors[i];
                        mesh.Add(Shapes.Rect(new Vector2(f.xMin - WallThickness + 0.06f, f.yMin - WallThickness - face - ContactShadow),
                            new Vector2(f.xMax + WallThickness + ContactShadow, f.yMax + WallThickness - 0.2f)), WithAlpha(Ink, 0.1f));
                        mesh.Add(Shapes.Rect(new Vector2(f.xMin - WallThickness, f.yMin - WallThickness - face), new Vector2(f.xMax + WallThickness, f.yMin - WallThickness)), PlinthFace);
                    }
                }
                foreach (int i in group) mesh.Add(Shapes.Rect(floors[i].min - wall, floors[i].max + wall), Wall);
                foreach (int i in group) mesh.Add(Shapes.Rect(floors[i].min, floors[i].max), FloorOf(decor, i));
                foreach (int i in group)
                {
                    var style = decor?.Styles != null && i < decor.Styles.Count ? decor.Styles[i] : FloorStyle.Plain;
                    if (style == FloorStyle.Plain) Decorate(mesh, floors[i], random);
                    else Pattern(mesh, floors[i], style, FloorOf(decor, i));
                }
            }
            foreach (var doorway in doorways) mesh.Add(Shapes.Rect(doorway.min, doorway.max), FloorColor);
            WallFaces(mesh, floors, doorways);
            if (plots != null)
                foreach (var (area, accent, flipped) in plots) Plot(mesh, area, accent, flipped);
            if (decor?.Props != null)
                foreach (var prop in decor.Props) DrawProp(mesh, prop);
            return mesh.Apply(new Mesh());
        }

        static Color FloorOf(RoomsDecor decor, int floor)
        {
            var tint = decor?.Tints != null && floor < decor.Tints.Count ? decor.Tints[floor] : null;
            var color = tint.HasValue ? Color.Lerp(FloorColor, tint.Value, FloorTint) : FloorColor;
            return Color.Lerp(color, Paper, LevelLight * LevelOf(decor, floor));
        }

        static int LevelOf(RoomsDecor decor, int floor) => decor?.Levels != null && floor < decor.Levels.Count ? decor.Levels[floor] : 0;

        /// <summary>
        /// 斜上方俯視時看得到北牆朝南的那一面：每塊地板上緣有牆的地方（扣掉門口與相鄰的地板）畫一條深色的牆面，下面一道淡影子。
        /// </summary>
        static void WallFaces(MeshBuilder mesh, IList<Rect> floors, IList<Rect> doorways)
        {
            for (int i = 0; i < floors.Count; i++)
            {
                var f = floors[i];
                var above = Rect.MinMaxRect(f.xMin, f.yMax, f.xMax, f.yMax + WallThickness);
                var holes = doorways.Concat(floors.Where((_, j) => j != i));
                foreach (var wall in Shapes.Subtract(above, holes))
                {
                    float bottom = f.yMax - WallFaceDepth;
                    mesh.Add(Shapes.Rect(new Vector2(wall.xMin, bottom - WallFaceShadow), new Vector2(wall.xMax, bottom)), WithAlpha(Ink, 0.07f));
                    mesh.Add(Shapes.Rect(new Vector2(wall.xMin, bottom), new Vector2(wall.xMax, f.yMax)), WallFace);
                    mesh.Add(Shapes.Rect(new Vector2(wall.xMin, bottom), new Vector2(wall.xMax, bottom + 0.05f)), WallFaceEdge);
                }
            }
        }

        /// <summary>牆面的高度，地板上緣這麼寬的一條畫成牆面；房間裡的東西都離牆比這遠。</summary>
        public const float WallFaceDepth = 0.45f;
        const float WallFaceShadow = 0.22f, LevelLight = 0.04f;
        static readonly Vector2 ShadowStep = new Vector2(0.35f, -0.55f);
        // 墊高一階露出的側面厚度；側面外貼地的淡影子寬度
        const float PlinthStep = 0.22f, ContactShadow = 0.14f;
        static readonly Color PlinthFace = Hex(0x666C77);
        static readonly Color ShadowColor = Color.Lerp(Background, Ink, 0.14f);
        static readonly Color WallFace = Hex(0x727883), WallFaceEdge = Hex(0x585E68);

        // 每間房的地板明顯帶著它的顏色，一眼看得出是哪一區
        const float FloorTint = 0.22f, PatternShade = 0.1f;
        static readonly Color PillarFill = Hex(0x9AA0AA), PillarTop = Hex(0xB9BEC6), Pot = Hex(0x8E7F72), Soil = Hex(0x4B4038), Leaf = Hex(0x4F8A63), LeafLight = Hex(0x6FAA80);

        /// <summary>地板花紋：比地板深一點的細線或色塊，裁在地板範圍內，對齊世界座標，相鄰的地板花紋才接得起來。</summary>
        static void Pattern(MeshBuilder mesh, Rect floor, FloorStyle style, Color floorColor)
        {
            var ink = Color.Lerp(floorColor, Wall, PatternShade);
            switch (style)
            {
                case FloorStyle.Tiles:
                    for (float x = Mathf.Ceil(floor.xMin / Pitch) * Pitch; x < floor.xMax; x += Pitch)
                        mesh.Add(ClipTo(Shapes.Rect(new Vector2(x - 0.025f, floor.yMin), new Vector2(x + 0.025f, floor.yMax)), floor), ink);
                    for (float y = Mathf.Ceil(floor.yMin / Pitch) * Pitch; y < floor.yMax; y += Pitch)
                        mesh.Add(ClipTo(Shapes.Rect(new Vector2(floor.xMin, y - 0.025f), new Vector2(floor.xMax, y + 0.025f)), floor), ink);
                    break;
                case FloorStyle.Planks:
                    bool across = floor.width >= floor.height;
                    const float plank = 0.9f, board = 3.6f;
                    float start = Mathf.Floor((across ? floor.yMin : floor.xMin) / plank) * plank;
                    float end = across ? floor.yMax : floor.xMax;
                    for (int row = 0; start + row * plank < end; row++)
                    {
                        float at = start + row * plank;
                        mesh.Add(ClipTo(across
                            ? Shapes.Rect(new Vector2(floor.xMin, at - 0.02f), new Vector2(floor.xMax, at + 0.02f))
                            : Shapes.Rect(new Vector2(at - 0.02f, floor.yMin), new Vector2(at + 0.02f, floor.yMax)), floor), ink);
                        float from = (across ? floor.xMin : floor.yMin) - board + row % 3 * board / 3;
                        for (float seam = from; seam < (across ? floor.xMax : floor.yMax); seam += board)
                            mesh.Add(ClipTo(across
                                ? Shapes.Rect(new Vector2(seam - 0.02f, at), new Vector2(seam + 0.02f, at + plank))
                                : Shapes.Rect(new Vector2(at, seam - 0.02f), new Vector2(at + plank, seam + 0.02f)), floor), ink);
                    }
                    break;
                case FloorStyle.Checker:
                    var shade = Color.Lerp(floorColor, Wall, PatternShade * 0.5f);
                    for (float x = Mathf.Floor(floor.xMin / Pitch) * Pitch; x < floor.xMax; x += Pitch)
                        for (float y = Mathf.Floor(floor.yMin / Pitch) * Pitch; y < floor.yMax; y += Pitch)
                            if ((Mathf.RoundToInt(x / Pitch) + Mathf.RoundToInt(y / Pitch)) % 2 == 0)
                                mesh.Add(ClipTo(Shapes.Rect(new Vector2(x, y), new Vector2(x + Pitch, y + Pitch)), floor), shade);
                    break;
                case FloorStyle.Hatch:
                    Stripes(mesh.Add, Shapes.Rect(floor.min, floor.max), 1.2f, 0.06f, ink);
                    break;
            }
        }

        /// <summary>平面設計風的擺設：細黑框、平塗、一點淺金或該區的顏色點綴。</summary>
        static void DrawProp(MeshBuilder mesh, Prop prop)
        {
            var c = prop.Center;
            var half = prop.Size / 2;
            var shadow = new Vector2(0.12f, -0.12f);
            var area = new Rect(c - half, prop.Size);
            switch (prop.Kind)
            {
                case PropKind.Pillar:
                    var top = Box3D(mesh, area, area.height * 0.3f, PillarTop, PillarFill);
                    mesh.Add(Shapes.Rect(top.center - top.size * 0.3f, top.center + top.size * 0.3f), PillarFill);
                    mesh.Add(Shapes.Diamond(top.center, top.width * 0.16f), prop.Accent);
                    break;
                case PropKind.Crate:
                    var lid = Box3D(mesh, area, area.height * 0.3f, WoodTop, WoodFace);
                    var rim = new Rect(lid.position + Vector2.one * 0.12f, lid.size - Vector2.one * 0.24f);
                    Frame(mesh, rim, 0.05f, WoodLine);
                    mesh.Add(Shapes.Segment(rim.min, rim.max, 0.07f), WoodLine);
                    for (float x = area.xMin + area.width / 3; x < area.xMax - 0.1f; x += area.width / 3)
                        mesh.Add(Shapes.Rect(new Vector2(x - 0.025f, area.yMin), new Vector2(x + 0.025f, lid.yMin)), WoodLine);
                    break;
                case PropKind.Bench:
                    var seat = Box3D(mesh, area, Mathf.Min(area.height, area.width) * 0.3f, WoodTop, WoodFace);
                    bool wide = area.width >= area.height;
                    for (int i = 1; i < 3; i++)
                    {
                        float t = i / 3f;
                        mesh.Add(wide
                            ? Shapes.Rect(new Vector2(seat.xMin, Mathf.Lerp(seat.yMin, seat.yMax, t) - 0.02f), new Vector2(seat.xMax, Mathf.Lerp(seat.yMin, seat.yMax, t) + 0.02f))
                            : Shapes.Rect(new Vector2(Mathf.Lerp(seat.xMin, seat.xMax, t) - 0.02f, seat.yMin), new Vector2(Mathf.Lerp(seat.xMin, seat.xMax, t) + 0.02f, seat.yMax)), WoodLine);
                    }
                    break;
                case PropKind.Statue:
                    var plinth = new Rect(area.position, new Vector2(area.width, area.height * 0.5f));
                    var stand = Box3D(mesh, plinth, plinth.height * 0.4f, PillarTop, PillarFill);
                    var foot = new Vector2(stand.center.x, stand.center.y);
                    float body = area.width * 0.26f;
                    var figure = new List<Vector2>
                    {
                        foot + new Vector2(-body, 0), foot + new Vector2(body, 0),
                        foot + new Vector2(body * 0.6f, area.height * 0.42f), foot + new Vector2(-body * 0.6f, area.height * 0.42f),
                    };
                    var head = foot + new Vector2(0, area.height * 0.55f);
                    mesh.Add(Shapes.Polygon(head, body * 0.62f + 0.05f, 24, 0), Ink);
                    mesh.Add(Grow(figure, 0.05f), Ink);
                    mesh.Add(figure, Paper);
                    mesh.Add(Shapes.Polygon(head, body * 0.62f, 24, 0), Paper);
                    mesh.Add(Shapes.Segment(foot + new Vector2(-body * 0.7f, area.height * 0.12f), foot + new Vector2(body * 0.55f, area.height * 0.34f), 0.12f), prop.Accent);
                    break;
                case PropKind.Fountain:
                    float radius = half.x;
                    mesh.Add(Shapes.Polygon(c + shadow, radius, 40, 0), WithAlpha(Ink, 0.18f));
                    mesh.Add(Shapes.Polygon(c, radius, 40, 0), Ink);
                    mesh.Add(Shapes.Polygon(c, radius - 0.06f, 40, 0), PillarTop);
                    // 水面比池緣低：上緣內側一道深色，看起來往下凹
                    mesh.Add(Shapes.Polygon(c, radius - 0.32f, 40, 0), WaterDeep);
                    mesh.Add(Shapes.Polygon(c + new Vector2(0, -0.1f), radius - 0.42f, 40, 0), Water);
                    foreach (float ring in new[] { 0.55f, 0.85f })
                    {
                        mesh.Add(Shapes.Polygon(c, radius * ring, 40, 0), WaterLight);
                        mesh.Add(Shapes.Polygon(c, radius * ring - 0.06f, 40, 0), Water);
                    }
                    mesh.Add(Shapes.Polygon(c, 0.34f, 20, 0), Ink);
                    mesh.Add(Shapes.Polygon(c, 0.29f, 20, 0), PillarTop);
                    mesh.Add(Shapes.Polygon(c + new Vector2(0, 0.04f), 0.14f, 16, 0), WaterLight);
                    mesh.Add(Shapes.Diamond(c + new Vector2(0, radius - 0.18f), 0.12f), prop.Accent);
                    break;
                case PropKind.Shelf:
                    var board = Box3D(mesh, area, area.height * 0.55f, WoodFace, WoodLine);
                    var books = new System.Random(Mathf.RoundToInt(c.x * 7 + c.y * 13));
                    var spines = new[] { prop.Accent, Gold, Hex(0x5C7FA8), Hex(0xA8605C), Hex(0x6E9A70), Paper };
                    for (float x = area.xMin + 0.12f; x < area.xMax - 0.2f;)
                    {
                        float width = 0.1f + (float)books.NextDouble() * 0.1f;
                        float height = (board.yMin - area.yMin) * (0.6f + (float)books.NextDouble() * 0.3f);
                        mesh.Add(Shapes.Rect(new Vector2(x, area.yMin + 0.06f), new Vector2(x + width, area.yMin + 0.06f + height)), spines[books.Next(spines.Length)]);
                        x += width + 0.03f;
                    }
                    break;
                case PropKind.Tree:
                    float crown = half.x;
                    mesh.Add(Shapes.Polygon(c + new Vector2(0.25f, -0.32f), crown, 28, 0), WithAlpha(Ink, 0.16f));
                    mesh.Add(Shapes.Polygon(c, crown, 28, 0), Ink);
                    mesh.Add(Shapes.Polygon(c, crown - 0.06f, 28, 0), Leaf);
                    mesh.Add(Shapes.Polygon(c + new Vector2(-0.12f, 0.12f), crown * 0.72f, 24, 0), LeafLight);
                    mesh.Add(Shapes.Polygon(c + new Vector2(-0.26f, 0.26f), crown * 0.36f, 20, 0), LeafBright);
                    foreach (var direction in new[] { new Vector2(0.55f, -0.2f), new Vector2(-0.4f, -0.45f), new Vector2(0.1f, 0.55f) })
                        mesh.Add(Shapes.Polygon(c + direction * crown, 0.09f, 10, 0), prop.Accent);
                    break;
                case PropKind.Pool:
                    Frame(mesh, area, 0.05f, Ink);
                    var basin = new Rect(area.position + Vector2.one * 0.05f, area.size - Vector2.one * 0.1f);
                    mesh.Add(Shapes.Rect(basin.min, basin.max), PillarTop);
                    var water = new Rect(basin.position + Vector2.one * 0.28f, basin.size - Vector2.one * 0.56f);
                    mesh.Add(Shapes.Rect(water.min, water.max), WaterDeep);
                    mesh.Add(Shapes.Rect(water.min, new Vector2(water.xMax, water.yMax - 0.3f)), Water);
                    for (float x = water.xMin + 0.5f; x < water.xMax - 0.7f; x += 1.1f)
                        mesh.Add(Shapes.Rect(new Vector2(x, water.center.y - 0.15f), new Vector2(x + 0.5f, water.center.y - 0.09f)), WaterLight);
                    break;
                case PropKind.Terrace:
                    const float rise = 0.4f;
                    var deck = Rect.MinMaxRect(area.xMin, area.yMin + rise, area.xMax, area.yMax);
                    mesh.Add(Shapes.Rect(area.min + new Vector2(0.18f, -0.2f), area.max + new Vector2(0.18f, -0.2f)), WithAlpha(Ink, 0.08f));
                    Frame(mesh, area, 0.04f, PlotEdge);
                    mesh.Add(Shapes.Rect(area.min, new Vector2(area.xMax, deck.yMin)), TerraceFace);
                    mesh.Add(Shapes.Rect(deck.min, deck.max), Color.Lerp(FloorColor, Paper, 0.45f));
                    Stripes(mesh.Add, Shapes.Rect(deck.min + Vector2.one * 0.4f, deck.max - Vector2.one * 0.4f), 0.9f, 0.05f, Color.Lerp(FloorColor, prop.Accent, 0.25f));
                    // 南面正中間的台階
                    var steps = new Rect(area.center.x - 0.9f, area.yMin, 1.8f, rise);
                    mesh.Add(Shapes.Rect(steps.min, steps.max), Color.Lerp(FloorColor, Paper, 0.3f));
                    mesh.Add(Shapes.Rect(new Vector2(steps.xMin, steps.yMin + rise / 2 - 0.02f), new Vector2(steps.xMax, steps.yMin + rise / 2 + 0.02f)), PlotEdge);
                    break;
                case PropKind.Banner:
                    // 下緣剪成燕尾；從左上角扇形展開，凹進去的地方才不會被三角形補滿
                    List<Vector2> Flag(Rect r, float notch) => new List<Vector2>
                    {
                        new Vector2(r.xMin, r.yMax), new Vector2(r.xMin, r.yMin), new Vector2(r.center.x, notch),
                        new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax),
                    };
                    float notchY = area.yMin + area.width * 0.4f;
                    mesh.Add(Flag(new Rect(area.position - Vector2.one * 0.05f, area.size + Vector2.one * 0.1f), notchY - 0.07f), Ink);
                    mesh.Add(Flag(area, notchY), prop.Accent);
                    mesh.Add(Shapes.Rect(new Vector2(area.xMin, area.yMax - 0.3f), new Vector2(area.xMax, area.yMax - 0.2f)), Paper);
                    mesh.Add(Shapes.Diamond(new Vector2(area.center.x, area.center.y + 0.05f), area.width * 0.18f), Paper);
                    mesh.Add(Shapes.Rect(new Vector2(area.xMin - 0.12f, area.yMax - 0.06f), new Vector2(area.xMax + 0.12f, area.yMax + 0.04f)), Ink);
                    break;
                case PropKind.Stairs:
                    Stairs(mesh, area, prop.Facing);
                    break;
                case PropKind.LowWall:
                    var cap = Box3D(mesh, area, Mathf.Min(area.width, area.height) * 0.45f, PillarTop, PillarFill);
                    bool across = area.width >= area.height;
                    for (float s = (across ? cap.xMin : cap.yMin) + 0.9f; s < (across ? cap.xMax : cap.yMax) - 0.3f; s += 0.9f)
                        mesh.Add(across
                            ? Shapes.Rect(new Vector2(s - 0.02f, area.yMin), new Vector2(s + 0.02f, cap.yMin))
                            : Shapes.Rect(new Vector2(cap.xMin, s - 0.02f), new Vector2(cap.xMax, s + 0.02f)), WithAlpha(Ink, 0.25f));
                    break;
                case PropKind.Grass:
                    mesh.Add(Rounded(new Rect(area.position - Vector2.one * 0.08f, area.size + Vector2.one * 0.16f), 1.2f), GrassEdge);
                    mesh.Add(Rounded(area, 1.1f), GrassColor);
                    var tufts = new System.Random(Mathf.RoundToInt(c.x * 11 + c.y * 17));
                    int count = Mathf.RoundToInt(area.width * area.height / 3);
                    for (int i = 0; i < count; i++)
                    {
                        var at = new Vector2(area.xMin + 0.6f + (float)tufts.NextDouble() * (area.width - 1.2f), area.yMin + 0.6f + (float)tufts.NextDouble() * (area.height - 1.2f));
                        mesh.Add(Shapes.Segment(at, at + new Vector2(-0.08f, 0.2f), 0.05f), GrassEdge);
                        mesh.Add(Shapes.Segment(at, at + new Vector2(0.08f, 0.2f), 0.05f), GrassEdge);
                    }
                    break;
                case PropKind.Hedge:
                    var crown2 = Box3D(mesh, area, Mathf.Min(area.width, area.height) * 0.4f, Leaf, HedgeFace);
                    for (float x = crown2.xMin + 0.35f; x < crown2.xMax - 0.2f; x += 0.7f)
                        mesh.Add(Shapes.Polygon(new Vector2(x, crown2.center.y), 0.16f, 10, 0), LeafLight);
                    break;
                case PropKind.Pond:
                    mesh.Add(Rounded(new Rect(area.position - Vector2.one * 0.3f, area.size + Vector2.one * 0.6f), 1.3f), PillarTop);
                    mesh.Add(Rounded(area, 1f), WaterDeep);
                    mesh.Add(Rounded(Rect.MinMaxRect(area.xMin, area.yMin, area.xMax, area.yMax - 0.3f), 1f), Water);
                    for (float x = area.xMin + 0.8f; x < area.xMax - 1f; x += 1.4f)
                        mesh.Add(Shapes.Rect(new Vector2(x, area.center.y - 0.2f), new Vector2(x + 0.6f, area.center.y - 0.14f)), WaterLight);
                    break;
                case PropKind.Islet:
                    // 往下收窄的一截土，分兩層顏色，看得出是浮在空中的一塊地
                    float depth = area.height * IsletDepth;
                    var soil = new List<Vector2>
                    {
                        new Vector2(area.xMin + 0.2f, area.yMin + 0.3f), new Vector2(area.xMin + area.width * 0.3f, area.yMin - depth),
                        new Vector2(area.xMax - area.width * 0.35f, area.yMin - depth * 0.85f), new Vector2(area.xMax - 0.2f, area.yMin + 0.3f),
                    };
                    mesh.Add(soil, Earth);
                    mesh.Add(new List<Vector2>
                    {
                        new Vector2(area.xMin + 0.2f, area.yMin + 0.3f), new Vector2(area.xMin + area.width * 0.22f, area.yMin - depth * 0.35f),
                        new Vector2(area.xMax - area.width * 0.25f, area.yMin - depth * 0.3f), new Vector2(area.xMax - 0.2f, area.yMin + 0.3f),
                    }, EarthLight);
                    break;
                case PropKind.Dais:
                    var face = Rect.MinMaxRect(area.xMin, area.yMin - DaisRise, area.xMax, area.yMin);
                    mesh.Add(Shapes.Rect(face.min + new Vector2(0, -0.16f), new Vector2(face.xMax, face.yMin)), WithAlpha(Ink, 0.06f));
                    mesh.Add(Shapes.Rect(face.min, face.max), TerraceFace);
                    mesh.Add(Shapes.Rect(face.min, new Vector2(face.xMax, face.yMin + 0.04f)), PlotEdge);
                    break;
                case PropKind.Planter:
                    mesh.Add(Shapes.Polygon(c + shadow, half.x, 32, 0), WithAlpha(Ink, 0.18f));
                    mesh.Add(Shapes.Polygon(c, half.x, 32, 0), Ink);
                    mesh.Add(Shapes.Polygon(c, half.x - Outline * 2, 32, 0), Pot);
                    mesh.Add(Shapes.Polygon(c, half.x * 0.72f, 32, 0), Soil);
                    for (int i = 0; i < 5; i++)
                    {
                        var direction = (Vector2)(Quaternion.Euler(0, 0, i * 72 + 18) * Vector2.up);
                        mesh.Add(Shapes.Segment(c, c + direction * half.x * 0.95f, half.x * 0.42f), i % 2 == 0 ? Leaf : LeafLight);
                    }
                    mesh.Add(Shapes.Polygon(c, half.x * 0.18f, 12, 0), prop.Accent);
                    break;
                case PropKind.Rug:
                    var rug = new Rect(c - half, prop.Size);
                    mesh.Add(Chamfered(rug.min, rug.max, 0.5f, false), Color.Lerp(FloorColor, prop.Accent, 0.35f));
                    var inner = new Rect(rug.position + Vector2.one * 0.35f, rug.size - Vector2.one * 0.7f);
                    mesh.Add(Chamfered(inner.min, inner.max, 0.35f, false), Color.Lerp(FloorColor, prop.Accent, 0.15f));
                    Stripes(mesh.Add, Shapes.Rect(inner.min + Vector2.one * 0.4f, inner.max - Vector2.one * 0.4f), 0.5f, 0.08f, Color.Lerp(FloorColor, prop.Accent, 0.3f));
                    break;
                case PropKind.Lamp:
                    mesh.Add(Shapes.Polygon(c, half.x, 16, 0), Ink);
                    mesh.Add(Shapes.Polygon(c, half.x * 0.55f, 16, 0), prop.Accent);
                    break;
                case PropKind.Post:
                    mesh.Add(Shapes.Rect(c - half, c + half), Face);
                    mesh.Add(Shapes.Rect(c - half * 0.55f, c + half * 0.55f), PillarTop);
                    break;
                case PropKind.Pad:
                    float r = half.x;
                    mesh.Add(Shapes.Polygon(c, r, 48, 0), Ink);
                    mesh.Add(Shapes.Polygon(c, r - 0.08f, 48, 0), prop.Accent);
                    mesh.Add(Shapes.Polygon(c, r - 0.24f, 48, 0), Ink);
                    mesh.Add(Shapes.Polygon(c, r - 0.3f, 48, 0), Paper);
                    mesh.Add(Shapes.Diamond(c, r * 0.42f), Ink);
                    mesh.Add(Shapes.Diamond(c, r * 0.3f), prop.Accent);
                    foreach (var direction in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right })
                        mesh.Add(Shapes.Segment(c + direction * (r - 0.3f), c + direction * (r * 0.55f), 0.07f), Ink);
                    break;
            }
        }

        /// <summary>題目區台子南面側邊的高度；GameSetup 擺東西時要避開。</summary>
        public const float DaisRise = 0.35f;
        static readonly Color WoodTop = Hex(0xC9A374), WoodFace = Hex(0x9C7651), WoodLine = Hex(0x765639);
        static readonly Color Water = Hex(0x86C6DF), WaterDeep = Hex(0x5E9FBC), WaterLight = Hex(0xC4E6F2);
        static readonly Color LeafBright = Hex(0x93C49F), TerraceFace = Hex(0xB4B9C1);
        static readonly Color GrassColor = Hex(0xB7CBB3), GrassEdge = Hex(0xA2B99E), HedgeFace = Hex(0x3E6B4E);
        static readonly Color Earth = Hex(0x8C7A68), EarthLight = Hex(0xA8957F);
        /// <summary>浮島底下那截土的深度，是頂面高度的幾倍；GameSetup 擺浮島時要算進去。</summary>
        public const float IsletDepth = 0.9f;

        /// <summary>四角是圓角（半徑 radius）的長方形，逆時針。</summary>
        static List<Vector2> Rounded(Rect area, float radius)
        {
            radius = Mathf.Min(radius, area.width / 2, area.height / 2);
            var points = new List<Vector2>();
            var centers = new[]
            {
                new Vector2(area.xMax - radius, area.yMin + radius), new Vector2(area.xMax - radius, area.yMax - radius),
                new Vector2(area.xMin + radius, area.yMax - radius), new Vector2(area.xMin + radius, area.yMin + radius),
            };
            for (int corner = 0; corner < 4; corner++)
                for (int i = 0; i <= 4; i++)
                {
                    float angle = (corner * 90 - 90 + i * 22.5f) * Mathf.Deg2Rad;
                    points.Add(centers[corner] + radius * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
                }
            return points;
        }
        static readonly Color StairLow = Hex(0x9EA4AE), StairHigh = Hex(0xEEF0F3);

        /// <summary>
        /// 斜上方俯視的立體方塊：area 是佔的地面範圍，下緣 height 那一條是朝南的側面，上面是頂面。回傳頂面。
        /// </summary>
        static Rect Box3D(MeshBuilder mesh, Rect area, float height, Color top, Color face)
        {
            var shadow = new Vector2(0.16f, -0.16f);
            mesh.Add(Shapes.Rect(area.min + shadow, area.max + shadow), WithAlpha(Ink, 0.18f));
            Frame(mesh, area, 0.05f, Ink);
            var lid = Rect.MinMaxRect(area.xMin, area.yMin + height, area.xMax, area.yMax);
            mesh.Add(Shapes.Rect(area.min, new Vector2(area.xMax, lid.yMin)), face);
            mesh.Add(Shapes.Rect(lid.min, lid.max), top);
            mesh.Add(Shapes.Rect(new Vector2(area.xMin, lid.yMin - 0.02f), new Vector2(area.xMax, lid.yMin + 0.02f)), WithAlpha(Ink, 0.45f));
            return lid;
        }

        /// <summary>貼在 area 外側、寬 width 的框。</summary>
        static void Frame(MeshBuilder mesh, Rect area, float width, Color color)
        {
            mesh.Add(Shapes.Rect(new Vector2(area.xMin - width, area.yMin - width), new Vector2(area.xMax + width, area.yMin)), color);
            mesh.Add(Shapes.Rect(new Vector2(area.xMin - width, area.yMax), new Vector2(area.xMax + width, area.yMax + width)), color);
            mesh.Add(Shapes.Rect(new Vector2(area.xMin - width, area.yMin), new Vector2(area.xMin, area.yMax)), color);
            mesh.Add(Shapes.Rect(new Vector2(area.xMax, area.yMin), new Vector2(area.xMax + width, area.yMax)), color);
        }

        /// <summary>逆時針凸多邊形每條邊往外推 distance。</summary>
        static List<Vector2> Grow(List<Vector2> polygon, float distance)
        {
            var result = new List<Vector2>();
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 prev = polygon[(i + polygon.Count - 1) % polygon.Count], at = polygon[i], next = polygon[(i + 1) % polygon.Count];
                Vector2 n1 = Outward(at - prev), n2 = Outward(next - at);
                var bisector = (n1 + n2).normalized;
                result.Add(at + bisector * distance / Mathf.Max(0.2f, Vector2.Dot(bisector, n1)));
            }
            return result;
        }

        static Vector2 Outward(Vector2 edge) => new Vector2(edge.y, -edge.x).normalized;

        /// <summary>一階階的樓梯：越往 up 那一頭越亮，每階的前緣一條細線，兩側是深色的側板。</summary>
        static void Stairs(MeshBuilder mesh, Rect area, Vector2 up)
        {
            bool vertical = Mathf.Abs(up.y) > Mathf.Abs(up.x);
            float length = vertical ? area.height : area.width;
            int count = Mathf.Max(2, Mathf.RoundToInt(length / 0.5f));
            float tread = length / count;
            for (int i = 0; i < count; i++)
            {
                // i = 0 是最低的那一階
                float from = Vector2.Dot(up, Vector2.one) > 0 ? i * tread : length - (i + 1) * tread;
                var step = vertical
                    ? Rect.MinMaxRect(area.xMin, area.yMin + from, area.xMax, area.yMin + from + tread)
                    : Rect.MinMaxRect(area.xMin + from, area.yMin, area.xMin + from + tread, area.yMax);
                mesh.Add(Shapes.Rect(step.min, step.max), Color.Lerp(StairLow, StairHigh, i / (count - 1f)));
                // 每階靠低處那一邊是前緣
                var edge = vertical
                    ? (up.y > 0 ? Rect.MinMaxRect(step.xMin, step.yMin, step.xMax, step.yMin + 0.05f) : Rect.MinMaxRect(step.xMin, step.yMax - 0.05f, step.xMax, step.yMax))
                    : (up.x > 0 ? Rect.MinMaxRect(step.xMin, step.yMin, step.xMin + 0.05f, step.yMax) : Rect.MinMaxRect(step.xMax - 0.05f, step.yMin, step.xMax, step.yMax));
                mesh.Add(Shapes.Rect(edge.min, edge.max), WithAlpha(Ink, 0.55f));
            }
            const float rail = 0.2f;
            if (vertical)
            {
                mesh.Add(Shapes.Rect(area.min, new Vector2(area.xMin + rail, area.yMax)), WallFace);
                mesh.Add(Shapes.Rect(new Vector2(area.xMax - rail, area.yMin), area.max), WallFace);
            }
            else
            {
                mesh.Add(Shapes.Rect(area.min, new Vector2(area.xMax, area.yMin + rail)), WallFace);
                mesh.Add(Shapes.Rect(new Vector2(area.xMin, area.yMax - rail), area.max), WallFace);
            }
        }

        static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        /// <summary>平面設計風的大塊幾何與點陣，裁在地板範圍內。</summary>
        static void Decorate(MeshBuilder mesh, Rect floor, System.Random random)
        {
            float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);
            Color Pick() => Decor[random.Next(Decor.Length)];

            int shapes = Mathf.Max(3, Mathf.RoundToInt(floor.width * floor.height / 45f));
            for (int i = 0; i < shapes; i++)
            {
                var center = new Vector2(Range(floor.xMin, floor.xMax), Range(floor.yMin, floor.yMax));
                float size = Range(1.2f, 3.6f), angle = Range(0, 360);
                List<Vector2> shape;
                switch (random.Next(4))
                {
                    case 0: shape = Shapes.Polygon(center, size, 48, 0); break;
                    case 1: shape = Shapes.Polygon(center, size, 3, angle); break;
                    case 2: shape = Shapes.Polygon(center, size, 4, angle); break;
                    default:
                        var along = Quaternion.Euler(0, 0, angle) * Vector2.right;
                        shape = Shapes.Segment(center - (Vector2)along * size * 2, center + (Vector2)along * size * 2, size * 0.35f);
                        break;
                }
                mesh.Add(ClipTo(shape, floor), Pick());
            }

            int grids = Mathf.Max(1, Mathf.RoundToInt(floor.width * floor.height / 160f));
            for (int g = 0; g < grids; g++)
            {
                var origin = new Vector2(Range(floor.xMin, floor.xMax - 2), Range(floor.yMin, floor.yMax - 2));
                var color = Decor[0];
                for (int x = 0; x < 5; x++)
                    for (int y = 0; y < 5; y++)
                        mesh.Add(ClipTo(Shapes.Polygon(origin + new Vector2(x, y) * 0.45f, 0.07f, 12, 0), floor), color);
            }
        }

        const float PlotChamfer = 1.4f, PlotLine = 0.05f, MarkInset = 0.35f, PlotBar = 0.42f, PlotBarLong = 3.2f, PlotBarShort = 1.8f;
        const float PlotBracket = 1.1f, PlotBracketWidth = 0.12f, BarcodeHeight = 0.5f, BarcodeGap = 0.08f;
        static readonly float[] Barcode = { 0.1f, 0.05f, 0.2f, 0.05f, 0.1f, 0.2f, 0.05f, 0.15f, 0.05f };
        static readonly Color PlotPaper = Hex(0xE8EAEE), PlotEdge = Hex(0xB9BDC4), PlotMark = Hex(0x8F96A1);

        const float CoverStripePitch = 0.7f, CoverStripeWidth = 0.16f, CoverFrameInset = 0.45f, CoverLineWidth = 0.06f;
        const float CoverBracket = 1.3f, CoverBracketWidth = 0.16f, CoverWipeBand = 0.35f;
        // 鎖離門那一側的距離：站在門口（畫面半寬約 8.9）就看得到
        const float EmblemReach = 6.5f, EmblemRadius = 2f, EmblemBarcode = 0.75f;
        static readonly Color CoverRoof = Hex(0x767C86), CoverStripe = Hex(0x6D737D), CoverLine = Hex(0xAEB3BB);

        /// <summary>
        /// 大地圖分區（與門後房間）的地面標記：帶一點該色的淺色地塊、右上角 45° 斜切、細外框，
        /// 左上角一組該色的 L 形粗條、右下角括號、左下角條碼。只有圖形沒有字，不會變成提示。
        /// accent 為 null 時是中性灰，無色用斜紋。標記都畫在地塊的內距裡，不會被方塊蓋住。
        /// flipped 時上下顛倒（粗條在左下角）：在路北側的分區、門在下方的房間，從路上或門口走近時先看到粗條。
        /// </summary>
        public static void Plot(MeshBuilder mesh, Rect area, BlockColor? accent, bool flipped = false)
        {
            bool colorless = accent == BlockColor.Colorless;
            Color strong = accent == null ? PlotMark : colorless ? Hatch : Swatch(accent.Value);
            Color paper = accent == null || colorless ? PlotPaper : Color.Lerp(PlotPaper, strong, 0.1f);
            var line = Vector2.one * PlotLine;
            mesh.Add(Chamfered(area.min - line, area.max + line, Shapes.InsetChamfer(PlotChamfer, -PlotLine), flipped), PlotEdge);
            mesh.Add(Chamfered(area.min, area.max, PlotChamfer, flipped), paper);

            // 以沒顛倒時離上緣的距離來擺，顛倒時改成離下緣
            Vector2 At(float x, float fromTop) => new Vector2(x, flipped ? area.yMin + fromTop : area.yMax - fromTop);
            (Vector2 min, Vector2 max) Bar(Vector2 a, Vector2 b) => (Vector2.Min(a, b), Vector2.Max(a, b));
            float left = area.xMin + MarkInset;
            var bars = new[]
            {
                Bar(At(left, MarkInset), At(left + PlotBar, MarkInset + PlotBarLong)),
                Bar(At(left, MarkInset), At(left + PlotBarShort, MarkInset + PlotBar)),
            };
            // 兩條的外框都畫完才填色，交疊處才不會被外框切開；淺色（純白）的粗條靠外框才看得見
            var outline = Vector2.one * Outline;
            foreach (var (min, max) in bars) mesh.Add(Shapes.Rect(min - outline, max + outline), Ink);
            foreach (var (min, max) in bars)
            {
                mesh.Add(Shapes.Rect(min, max), colorless ? FloorColor : strong);
                if (colorless) Stripes(mesh.Add, Shapes.Rect(min, max), HatchPitch, HatchWidth, Hatch);
            }

            float bottom = area.height - MarkInset;
            var end = At(area.xMax - MarkInset, bottom);
            mesh.Add(Box(end - new Vector2(PlotBracket, 0), At(end.x, bottom - PlotBracketWidth)), PlotMark);
            mesh.Add(Box(end - new Vector2(PlotBracketWidth, 0), At(end.x, bottom - PlotBracket)), PlotMark);
            // 條碼從原點往上畫，顛倒時原點要取條碼的另一端
            DrawBarcode(mesh.Add, At(left, flipped ? bottom - BarcodeHeight : bottom), 1, PlotMark);
        }

        /// <summary>遮罩從門所在的那一邊往內收起的方向；door 是門相對於遮罩中心的位置（門在遮罩外，取最靠近的那一邊）。</summary>
        public static Vector2 CoverInward(Vector2 size, Vector2 door) =>
            Mathf.Abs(door.x) * size.y >= Mathf.Abs(door.y) * size.x
                ? new Vector2(-Mathf.Sign(door.x), 0)
                : new Vector2(0, -Mathf.Sign(door.y));

        /// <summary>鎖的位置（以遮罩中心為原點）：從門那一側往內 <see cref="EmblemReach"/>、對齊門口；房間太淺時就在正中間。</summary>
        public static Vector2 CoverEmblem(Vector2 size, Vector2 inward, Vector2 door)
        {
            var half = size / 2;
            var along = new Vector2(Mathf.Abs(inward.x), Mathf.Abs(inward.y));
            var side = new Vector2(along.y, along.x);
            float depth = Vector2.Dot(half, along);
            float room = Vector2.Dot(half, side) - EmblemRadius - EmblemBarcode - CoverFrameInset;
            float lateral = room > 0 ? Mathf.Clamp(Vector2.Dot(door, side), -room, room) : 0;
            return inward * (Mathf.Min(depth, EmblemReach) - depth) + side * lateral;
        }

        /// <summary>
        /// 房間遮罩（以遮罩中心為原點，size 是地板大小）：比牆深一點的灰色屋頂、細斜紋、內框與四角括號，emblem 處一把鎖。
        /// wipe 大於 0 時從門那一側（inward 的反方向）沿 45° 斜線往內擦掉，擦除的邊緣是一條淺金色帶。
        /// </summary>
        /// <param name="world">遮罩中心的世界座標：斜紋對齊世界座標，同一間房分成好幾塊遮罩時斜紋才接得起來。</param>
        /// <param name="frame">
        /// 擦除的範圍（世界座標）：同一間房的每一塊遮罩用同一個 frame，擦除線才會一路連貫地掃過整間房；沒給就是這塊遮罩自己。
        /// </param>
        public static void Cover(MeshBuilder mesh, Vector2 size, Vector2 inward, Vector2 emblem, float wipe, bool locked = true, Vector2 world = default, Rect frame = default)
        {
            var half = size / 2;
            var bounds = new Rect(-half, size);
            if (frame.width <= 0) frame = new Rect(world - half, size);
            CoverCut(frame.size, inward, wipe, out var normal, out float cut);
            cut -= Vector2.Dot(world - frame.center, normal);
            void Add(List<Vector2> polygon, Color color) => mesh.Add(Shapes.Clip(ClipTo(polygon, bounds), -normal, -cut), color);

            var whole = Shapes.Rect(-half, half);
            Add(whole, CoverRoof);
            Stripes((polygon, color) => Add(polygon.Select(point => point - world).ToList(), color),
                whole.Select(point => point + world).ToList(), CoverStripePitch, CoverStripeWidth, CoverStripe);

            // 內框與四角括號只畫在主要那塊上；走道、側間、房間的其他幾塊只有屋頂，連在一起看才像同一片
            if (locked)
            {
                var inner = half - Vector2.one * CoverFrameInset;
                var line = Vector2.one * CoverLineWidth;
                Add(Shapes.Rect(new Vector2(-inner.x, inner.y - line.y), inner), CoverLine);
                Add(Shapes.Rect(-inner, new Vector2(inner.x, -inner.y + line.y)), CoverLine);
                Add(Shapes.Rect(-inner, new Vector2(-inner.x + line.x, inner.y)), CoverLine);
                Add(Shapes.Rect(new Vector2(inner.x - line.x, -inner.y), inner), CoverLine);
                foreach (var direction in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) })
                {
                    var corner = Vector2.Scale(inner, direction);
                    Add(Box(corner, corner - Vector2.Scale(direction, new Vector2(CoverBracket, CoverBracketWidth))), CoverLine);
                    Add(Box(corner, corner - Vector2.Scale(direction, new Vector2(CoverBracketWidth, CoverBracket))), CoverLine);
                }
                Lock(Add, emblem);
            }

            Add(Shapes.Clip(whole, normal, cut + CoverWipeBand), Gold);
        }

        /// <summary>遮罩擦到 wipe 時，local（以遮罩中心為原點）還被蓋著。</summary>
        public static bool CoverRemains(Vector2 size, Vector2 inward, float wipe, Vector2 local)
        {
            CoverCut(size, inward, wipe, out var normal, out float cut);
            return Vector2.Dot(local, normal) >= cut;
        }

        /// <summary>dot(p, normal) ≥ cut 的部分還蓋著；wipe 0 是全部蓋住（金色帶也還在外面），1 是全部收起。</summary>
        static void CoverCut(Vector2 size, Vector2 inward, float wipe, out Vector2 normal, out float cut)
        {
            // 取讓切線呈 "/" 的那條垂直方向，跟屋頂的斜紋同向
            normal = (inward + new Vector2(-inward.y, -inward.x)).normalized;
            float reach = Mathf.Abs(normal.x) * size.x / 2 + Mathf.Abs(normal.y) * size.y / 2;
            cut = Mathf.Lerp(-reach - CoverWipeBand, reach, wipe);
        }

        /// <summary>菱形外框裡的鎖頭（墨色鎖身、淺金鑰匙孔），菱形下方一段條碼。</summary>
        static void Lock(System.Action<List<Vector2>, Color> add, Vector2 center)
        {
            const float shackle = 0.46f, thickness = 0.16f, bodyHalf = 0.66f, bodyTop = 0.1f, bodyBottom = -0.82f, cut = 0.14f;
            const int steps = 10;
            add(Shapes.Diamond(center, EmblemRadius), CoverLine);
            add(Shapes.Diamond(center, EmblemRadius - 0.16f), CoverRoof);
            var hub = center + new Vector2(0, bodyTop + 0.28f);
            for (int i = 0; i < steps; i++)
            {
                float from = Mathf.PI * i / steps, to = Mathf.PI * (i + 1) / steps;
                Vector2 a = new Vector2(Mathf.Cos(from), Mathf.Sin(from)), b = new Vector2(Mathf.Cos(to), Mathf.Sin(to));
                add(new List<Vector2> { hub + a * (shackle - thickness), hub + a * shackle, hub + b * shackle, hub + b * (shackle - thickness) }, Ink);
            }
            add(Box(hub + new Vector2(-shackle, 0), center + new Vector2(-shackle + thickness, bodyTop)), Ink);
            add(Box(hub + new Vector2(shackle - thickness, 0), center + new Vector2(shackle, bodyTop)), Ink);
            add(new List<Vector2>
            {
                center + new Vector2(-bodyHalf + cut, bodyBottom), center + new Vector2(bodyHalf - cut, bodyBottom),
                center + new Vector2(bodyHalf, bodyBottom + cut), center + new Vector2(bodyHalf, bodyTop),
                center + new Vector2(-bodyHalf, bodyTop), center + new Vector2(-bodyHalf, bodyBottom + cut),
            }, Ink);
            add(Shapes.Polygon(center + new Vector2(0, -0.28f), 0.12f, 16, 0), Gold);
            add(new List<Vector2>
            {
                center + new Vector2(-0.05f, -0.3f), center + new Vector2(0.05f, -0.3f),
                center + new Vector2(0.08f, -0.58f), center + new Vector2(-0.08f, -0.58f),
            }, Gold);
            float width = BarcodeWidth(1.4f);
            DrawBarcode(add, center + new Vector2(-width / 2, -EmblemRadius - EmblemBarcode), 1.4f, CoverLine);
        }

        static float BarcodeWidth(float scale)
        {
            float width = -BarcodeGap * scale;
            foreach (var bar in Barcode) width += (bar + BarcodeGap) * scale;
            return width;
        }

        /// <summary>粗細不一的直條；origin 是左下角。</summary>
        static void DrawBarcode(System.Action<List<Vector2>, Color> add, Vector2 origin, float scale, Color color)
        {
            float x = origin.x;
            foreach (var bar in Barcode)
            {
                add(Shapes.Rect(new Vector2(x, origin.y), new Vector2(x + bar * scale, origin.y + BarcodeHeight * scale)), color);
                x += (bar + BarcodeGap) * scale;
            }
        }

        /// <summary>在凸多邊形範圍內畫 "/" 方向的 45° 斜紋。</summary>
        static void Stripes(System.Action<List<Vector2>, Color> add, List<Vector2> polygon, float pitch, float width, Color color)
        {
            var across = new Vector2(1, -1).normalized;
            float min = float.MaxValue, max = float.MinValue;
            foreach (var point in polygon)
            {
                float d = Vector2.Dot(point, across);
                min = Mathf.Min(min, d);
                max = Mathf.Max(max, d);
            }
            for (float d = Mathf.Floor(min / pitch) * pitch; d < max; d += pitch)
                add(Shapes.Clip(Shapes.Clip(polygon, across, d + width), -across, -d), color);
        }

        /// <summary>右上角（bottom 時右下角）切成 45° 斜角的長方形（逆時針）。</summary>
        static List<Vector2> Chamfered(Vector2 min, Vector2 max, float chamfer, bool bottom) => bottom
            ? new List<Vector2>
            {
                min, new Vector2(max.x - chamfer, min.y), new Vector2(max.x, min.y + chamfer), max, new Vector2(min.x, max.y),
            }
            : new List<Vector2>
            {
                min, new Vector2(max.x, min.y), new Vector2(max.x, max.y - chamfer), new Vector2(max.x - chamfer, max.y), new Vector2(min.x, max.y),
            };

        static List<Vector2> Box(Vector2 a, Vector2 b) => Shapes.Rect(Vector2.Min(a, b), Vector2.Max(a, b));

        static List<Vector2> ClipTo(List<Vector2> polygon, Rect rect)
        {
            polygon = Shapes.Clip(polygon, Vector2.right, rect.xMax);
            polygon = Shapes.Clip(polygon, Vector2.left, -rect.xMin);
            polygon = Shapes.Clip(polygon, Vector2.up, rect.yMax);
            return Shapes.Clip(polygon, Vector2.down, -rect.yMin);
        }

        /// <summary>原本的白色小方塊外觀，大小跟碰撞體一樣；原點在底邊中央，跟像素小人一樣站在腳底的錨點上。</summary>
        public static Mesh PlayerSquare()
        {
            var mesh = new MeshBuilder();
            var center = new Vector2(0, PlayerHalf);
            var outer = Vector2.one * PlayerHalf;
            var inner = Vector2.one * (PlayerHalf - PlayerOutline);
            mesh.Add(Shapes.Rect(center - outer, center + outer), Ink);
            mesh.Add(Shapes.Rect(center - inner, center + inner), Color.white);
            return mesh.Apply(new Mesh());
        }

        /// <summary>有細黑框的實心長方形（門、暴雷條用）。</summary>
        public static void Panel(MeshBuilder mesh, Vector2 size, Color fill)
        {
            var half = size / 2;
            var inset = Vector2.one * Outline;
            mesh.Add(Shapes.Rect(-half, half), Ink);
            mesh.Add(Shapes.Rect(-half + inset, half - inset), fill);
        }
    }
}

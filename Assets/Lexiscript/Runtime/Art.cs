using System.Collections.Generic;
using UnityEngine;

namespace Lexiscript
{
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

        public static readonly Color Background = Hex(0xC4C8CF);
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
        public static Mesh Rooms(IList<Rect> floors, IList<Rect> doorways, int seed, IList<(Rect area, BlockColor? accent, bool flipped)> plots = null)
        {
            var mesh = new MeshBuilder();
            var wall = Vector2.one * WallThickness;
            foreach (var floor in floors) mesh.Add(Shapes.Rect(floor.min - wall, floor.max + wall), Wall);
            foreach (var floor in floors) mesh.Add(Shapes.Rect(floor.min, floor.max), FloorColor);
            foreach (var doorway in doorways) mesh.Add(Shapes.Rect(doorway.min, doorway.max), FloorColor);
            var random = new System.Random(seed);
            foreach (var floor in floors) Decorate(mesh, floor, random);
            if (plots != null)
                foreach (var (area, accent, flipped) in plots) Plot(mesh, area, accent, flipped);
            return mesh.Apply(new Mesh());
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
        public static void Cover(MeshBuilder mesh, Vector2 size, Vector2 inward, Vector2 emblem, float wipe)
        {
            var half = size / 2;
            var bounds = new Rect(-half, size);
            CoverCut(size, inward, wipe, out var normal, out float cut);
            void Add(List<Vector2> polygon, Color color) => mesh.Add(Shapes.Clip(ClipTo(polygon, bounds), -normal, -cut), color);

            var whole = Shapes.Rect(-half, half);
            Add(whole, CoverRoof);
            Stripes(Add, whole, CoverStripePitch, CoverStripeWidth, CoverStripe);

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

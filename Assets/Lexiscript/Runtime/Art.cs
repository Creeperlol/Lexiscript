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
        public const float PlayerHalf = 0.25f;
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
        static List<Vector2> AnswerBox(BlockShape shape)
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
        /// 牆、地板、房間之間的門口，以及地板上的平面裝飾。
        /// 牆先畫成比地板大一圈的矩形，再用地板蓋掉內側，相鄰房間共用的牆就只剩一道。
        /// </summary>
        public static Mesh Rooms(IList<Rect> floors, IList<Rect> doorways, int seed)
        {
            var mesh = new MeshBuilder();
            var wall = Vector2.one * WallThickness;
            foreach (var floor in floors) mesh.Add(Shapes.Rect(floor.min - wall, floor.max + wall), Wall);
            foreach (var floor in floors) mesh.Add(Shapes.Rect(floor.min, floor.max), FloorColor);
            foreach (var doorway in doorways) mesh.Add(Shapes.Rect(doorway.min, doorway.max), FloorColor);
            var random = new System.Random(seed);
            foreach (var floor in floors) Decorate(mesh, floor, random);
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

        static List<Vector2> ClipTo(List<Vector2> polygon, Rect rect)
        {
            polygon = Shapes.Clip(polygon, Vector2.right, rect.xMax);
            polygon = Shapes.Clip(polygon, Vector2.left, -rect.xMin);
            polygon = Shapes.Clip(polygon, Vector2.up, rect.yMax);
            return Shapes.Clip(polygon, Vector2.down, -rect.yMin);
        }

        public static Mesh PlayerSquare()
        {
            var mesh = new MeshBuilder();
            var outer = Vector2.one * PlayerHalf;
            var inner = Vector2.one * (PlayerHalf - PlayerOutline);
            mesh.Add(Shapes.Rect(-outer, outer), Ink);
            mesh.Add(Shapes.Rect(-inner, inner), Color.white);
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

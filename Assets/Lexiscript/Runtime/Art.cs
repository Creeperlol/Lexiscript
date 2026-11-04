using System.Collections.Generic;
using UnityEngine;

namespace Lexiscript
{
    /// <summary>配色、尺寸與各種網格的畫法。調整外觀只改這裡的常數。</summary>
    public static class Art
    {
        public const float Pitch = 3f;
        public const float Half = 1f;
        const float Chamfer = 0.30f;
        const float BandInset = 0.04f;
        const float FrameInset = 0.24f;
        const float FaceInset = 0.28f;
        const float DashThickness = 0.03f, DashLength = 0.10f, DashGap = 0.06f, DashExtent = 0.61f;
        public const float CameraSize = 5f;
        const float HatchPitch = 0.16f, HatchWidth = 0.06f;
        const float DiamondOuter = 0.22f, DiamondInner = 0.17f, ChevronStroke = 0.045f;
        const float BridgeWidth = 0.12f;
        public const float PlayerRadius = 0.34f;
        const float PlayerInnerRadius = 0.28f;

        public const float TextOffset = 0.34f;
        public static readonly Vector2 TextSize = new Vector2(1.32f, 0.50f);
        public const float FontMin = 0.8f, FontMax = 2.4f;
        public const float HintOffset = 1.5f;
        public static readonly Vector2 HintSize = new Vector2(12f, 0.6f);
        public const float HintFontSize = 2.2f;

        public static readonly Rect Floor = new Rect(-1.5f, -1.5f, 30f, 30f);
        public const float WallThickness = 1f;

        public static readonly Color Background = Hex(0x15171C);
        public static readonly Color FloorColor = Hex(0x565C69);
        public static readonly Color Wall = Hex(0x2A2E36);
        public static readonly Color Ink = Hex(0x16181D);
        public static readonly Color Paper = Hex(0xEDE8DC);
        public static readonly Color Typing = Hex(0x6B7080);
        public static readonly Color Hatch = Hex(0xD9DDE3);

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

        /// <summary>
        /// 畫一格方塊（以格中心為原點）：外輪廓、色帶、內框線、紙面、中間虛線，最後是指向符號。
        /// 純色格只有外輪廓與整格填色。
        /// </summary>
        public static void Block(MeshBuilder mesh, BlockColor color, BlockShape shape, bool pure, IEnumerable<Vector2> pointerDirections)
        {
            float chamfer = shape == BlockShape.Beveled ? Chamfer : 0;
            List<Vector2> Ring(float inset) => Shapes.Octagon(Half - inset, Shapes.InsetChamfer(chamfer, inset));

            mesh.Add(Ring(0), Ink);
            var fill = Ring(BandInset);
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
                mesh.Add(Ring(FrameInset), Ink);
                mesh.Add(Ring(FaceInset), Paper);
                int dashes = Mathf.RoundToInt((2 * DashExtent + DashGap) / (DashLength + DashGap));
                float start = -(dashes * (DashLength + DashGap) - DashGap) / 2;
                for (int i = 0; i < dashes; i++)
                {
                    float x = start + i * (DashLength + DashGap);
                    mesh.Add(Shapes.Rect(new Vector2(x, -DashThickness / 2), new Vector2(x + DashLength, DashThickness / 2)), Ink);
                }
            }

            foreach (var direction in pointerDirections) Pointer(mesh, direction * Half, direction);
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
        /// 兩格中心之間的帶狀連接橋。它畫在方塊底下，所以只露出縫隙那一段，不必計算接點。
        /// </summary>
        public static void Bridge(MeshBuilder mesh, Vector2 from, Vector2 to, bool withPointer)
        {
            mesh.Add(Shapes.Segment(from, to, BridgeWidth), Paper);
            if (withPointer) Pointer(mesh, (from + to) / 2, (to - from).normalized);
        }

        /// <summary>指向符號畫在來源格朝目標那一側的邊上，取相對位置的主要軸。</summary>
        public static Vector2 Direction(Vector2 delta) =>
            Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                ? new Vector2(Mathf.Sign(delta.x), 0)
                : new Vector2(0, Mathf.Sign(delta.y));

        public static Mesh Room(Rect floor)
        {
            var mesh = new MeshBuilder();
            var wall = Vector2.one * WallThickness;
            mesh.Add(Shapes.Rect(floor.min - wall, floor.max + wall), Wall);
            mesh.Add(Shapes.Rect(floor.min, floor.max), FloorColor);
            return mesh.Apply(new Mesh());
        }

        public static Mesh PlayerDisc()
        {
            var mesh = new MeshBuilder();
            mesh.Add(Shapes.Circle(Vector2.zero, PlayerRadius, 32), Ink);
            mesh.Add(Shapes.Circle(Vector2.zero, PlayerInnerRadius, 32), Color.white);
            return mesh.Apply(new Mesh());
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lexiscript
{
    public enum Facing
    {
        Down,
        Up,
        Left,
        Right,
    }

    /// <summary>
    /// 原創的二次元像素小人：斜戴酒紅貝雷帽、插一支白羽毛筆，墨藍色鮑伯頭、琥珀色大眼睛與腮紅，
    /// 白色短披肩繫金色蝴蝶結。點陣分成頭、上身、腿三個部件，依影格組合；向右是向左的鏡像。
    /// 每個影格建成一個頂點色網格（同一列相鄰的同色像素合併成一個長方形），原點在腳底中央。
    /// </summary>
    public static class PixelHero
    {
        /// <summary>一個點陣像素的世界邊長；整個小人約 0.53 寬、0.92 高（含影子）。</summary>
        public const float PixelSize = 0.028f;
        /// <summary>影格 0–1 是待機（吸氣、吐氣），2–5 是走路一個循環。</summary>
        public const int IdleFrames = 2, WalkFrames = 4, FrameCount = IdleFrames + WalkFrames;
        /// <summary>點陣的欄數與列數（最後一列是腳下的影子）。</summary>
        public const int Columns = 21, Rows = 33;
        /// <summary>眼睛所在的列（頭沒有下沉時）；眨眼只換這幾列。</summary>
        public const int EyeTop = 12, EyeBottom = 15;

        const int Center = 10;
        const int TorsoRow = 19, LegsRow = 28, ShadowRow = 31;
        /// <summary>鞋底那一列；它的下緣是網格的 y = 0，影子再往下一列。</summary>
        const int SoleRow = 31;
        const int Rest = 0, SwingA = 1, SwingB = 2;
        const int Stand = 0, StepA = 1, StepB = 2;

        // 手跟腳反向擺。正面抬腳的那格是單腳撐地、另一腳往前帶的過渡姿勢，身體最高；兩腳都著地時沉一格
        static readonly (int legs, int torso, int drop)[] FrontFrames =
        {
            (Stand, Rest, 0), (Stand, Rest, 1),
            (StepA, SwingB, 0), (Stand, Rest, 1), (StepB, SwingA, 0), (Stand, Rest, 1),
        };

        // 側面跨步的那格兩腳前後著地，身體最低；兩腳交會時最高，起伏跟正面相反
        static readonly (int legs, int torso, int drop)[] SideFrames =
        {
            (Stand, Rest, 0), (Stand, Rest, 1),
            (StepA, SwingB, 1), (Stand, Rest, 0), (StepB, SwingA, 1), (Stand, Rest, 0),
        };

        /// <summary>
        /// 點陣字元的顏色（sRGB）：K 描邊；H h L 頭髮；V v X 貝雷帽；Q q 羽毛；S P m 皮膚、腮紅、嘴；
        /// E A a W 眼睛；C c 披肩；R r g 金色蝴蝶結與滾邊；D d B 裙子；T t 褲襪（t 是側面遠側的腳）；
        /// F f 鞋子；z 腳下半透明的影子。
        /// </summary>
        static readonly Dictionary<char, Color> Palette = new Dictionary<char, Color>
        {
            { 'K', Hex(0x1B1D24) },
            { 'H', Hex(0x3B4470) }, { 'h', Hex(0x262B47) }, { 'L', Hex(0x56639A) },
            { 'V', Hex(0xA8324E) }, { 'v', Hex(0x73203A) }, { 'X', Hex(0xD35A73) },
            { 'Q', Hex(0xFFFFFF) }, { 'q', Hex(0xC3C9D6) },
            { 'S', Hex(0xFFE7D6) }, { 'P', Hex(0xF7A1AE) }, { 'm', Hex(0xB5544F) },
            { 'E', Hex(0x3A2412) }, { 'A', Hex(0xC47A22) }, { 'a', Hex(0xF0B85A) }, { 'W', Hex(0xFFFFFF) },
            { 'C', Hex(0xFAFAF6) }, { 'c', Hex(0xC9CFDB) },
            { 'R', Hex(0xEBC15E) }, { 'r', Hex(0xB98A33) }, { 'g', Hex(0xE3B451) },
            { 'D', Hex(0x454B66) }, { 'd', Hex(0x2E3247) }, { 'B', Hex(0x5D6487) },
            { 'T', Hex(0x353849) }, { 't', Hex(0x22242E) },
            { 'F', Hex(0x6B4632) }, { 'f', Hex(0x4A3124) },
            { 'z', new Color(0x16 / 255f, 0x18 / 255f, 0x1D / 255f, 0.24f) },
        };

        static readonly string[] FrontHead =
        {
            "...............KK....",
            "..............KQQK...",
            ".....KKKKKK..KQqK....",
            "...KKVVVVVVKKQqK.....",
            "..KVVXXVVVVVVKqKK....",
            "..KVXXVVVVVVVVKKHK...",
            ".KVVVVVVVVVVVVvKHHK..",
            ".KvVVVVVVVVVVvvKLHHK.",
            "..KvvvvvvvvvvvKHLHHK.",
            ".KHKKKKKKKKKKKHHHHHK.",
            ".KHHHHHHHHhHHHHHHHHK.",
            ".KHHHhHHhHHHhHHhHHHK.",
            ".KHHhKKKSSSSSKKKhHHK.",
            ".KHHSEWESSSSSEWESHHK.",
            ".KHHSEAASSSSSAAESHHK.",
            ".KhHSAaASSSSSAaASHhK.",
            ".KhHPPSSSSmSSSSPPHhK.",
            ".KhhKSSSSSSSSSSSKhhK.",
            "..KhhKKSSSSSSSKKhhK..",
            "...KKK.........KKK...",
        };

        static readonly (int row, string pixels)[] FrontBlink =
        {
            (12, ".KHHhSSSSSSSSSSShHHK."),
            (13, ".KHHSSSSSSSSSSSSSHHK."),
            (14, ".KHHSKKKSSSSSKKKSHHK."),
            (15, ".KhHSSSSSSSSSSSSSHhK."),
        };

        static readonly string[] BackHead =
        {
            "....KK...............",
            "...KQQK..............",
            "....KqQK..KKKKKK.....",
            ".....KqQKKVVVVVVKK...",
            "....KKqKVXXVVVVVVVK..",
            "...KHKKVXXVVVVVVVVVK.",
            "..KHHKVVVVVVVVVVVVVK.",
            ".KHHLKvVVVVVVVVVVVvK.",
            ".KHLHHKvvvvvvvvvvvvK.",
            ".KHHHHHKKKKKKKKKKKHK.",
            ".KHLLHHHHHHHHHHHHHHK.",
            ".KHLHHLLLLLLLLHHHHHK.",
            ".KHHHHHHHHHHHHHHHHHK.",
            ".KHHHhHHHHhHHHHhHHHK.",
            ".KhHHhHHHHhHHHHhHHhK.",
            ".KhHhHHHHhHHHHHhHHhK.",
            ".KhhhHHHhHHHHhHHhhhK.",
            ".KhhhhHhhHHHhhHhhhhK.",
            "..KhhhhhhhhhhhhhhhK..",
            "...KKKhKKhhhKKhKKK...",
        };

        static readonly string[] SideHead =
        {
            "................KK...",
            "...............KQQK..",
            "......KKKKKKK.KQqK...",
            "....KKVVVVVVVKKqK....",
            "...KVVXXVVVVVVvKKK...",
            "..KVXXVVVVVVVVVvvHK..",
            "..KVVVVVVVVVVVVvvHHK.",
            "..KvvvvvvvvvvvvvKHHK.",
            "..KKKKKKKKKKKKKKHLHK.",
            "..KHHHHHHHHHHHLLHHHK.",
            ".KHHHhHHHHHHHLHHHHHK.",
            ".KSHhSHHhHHHHHHHHHHK.",
            ".KSKKKSShHHHHHHHHHHK.",
            ".KSEWESShHHHHHHHHHhK.",
            ".KSEAASShHHHHHHHHHhK.",
            ".KSAaASShHHHHHHHHhhK.",
            ".KPPSSSShHHHHHHHHhhK.",
            "..KSSSmShHHHHHHHhhK..",
            "...KKSSKhhHHHHHhhK...",
            ".....KK.KKhhhhhKK....",
        };

        static readonly (int row, string pixels)[] SideBlink =
        {
            (12, ".KSSSSSShHHHHHHHHHHK."),
            (13, ".KSSSSSShHHHHHHHHHhK."),
            (14, ".KSKKKSShHHHHHHHHHhK."),
            (15, ".KSSSSSShHHHHHHHHhhK."),
        };

        static readonly string[][] FrontTorso =
        {
            new[]
            {
                ".......KCRrRCK.......",
                "......KCCRKRCcK......",
                ".....KCCCCCCCCcK.....",
                "....KCCCCCCCCCCcK....",
                "....KgggggggggggK....",
                "...KSKDDDDDDDDdKSK...",
                "....KKDDDDDDDDdKK....",
                ".....KDBDDDBDDdK.....",
                ".....KKKKKKKKKKK.....",
            },
            new[]
            {
                ".......KCRrRCK.......",
                "......KCCRKRCcK......",
                ".....KCCCCCCCCcK.....",
                "....KCCCCCCCCCCcK....",
                "...KSgggggggggggK....",
                "....KKDDDDDDDDdKSK...",
                "....KKDDDDDDDDdKSK...",
                ".....KDBDDDBDDdKK....",
                ".....KKKKKKKKKKK.....",
            },
            new[]
            {
                ".......KCRrRCK.......",
                "......KCCRKRCcK......",
                ".....KCCCCCCCCcK.....",
                "....KCCCCCCCCCCcK....",
                "....KgggggggggggSK...",
                "...KSKDDDDDDDDdKK....",
                "...KSKDDDDDDDDdKK....",
                "....KKDBDDDBDDdK.....",
                ".....KKKKKKKKKKK.....",
            },
        };

        static readonly string[][] BackTorso =
        {
            new[]
            {
                ".......KCCCCCK.......",
                "......KCCCCCCcK......",
                ".....KCCCCCCCCcK.....",
                "....KCCCCCCCCCCcK....",
                "....KgggggggggggK....",
                "...KSKDDDDDDDDdKSK...",
                "....KKDDDDDDDDdKK....",
                ".....KDBDDDBDDdK.....",
                ".....KKKKKKKKKKK.....",
            },
            new[]
            {
                ".......KCCCCCK.......",
                "......KCCCCCCcK......",
                ".....KCCCCCCCCcK.....",
                "....KCCCCCCCCCCcK....",
                "...KSgggggggggggK....",
                "....KKDDDDDDDDdKSK...",
                "....KKDDDDDDDDdKSK...",
                ".....KDBDDDBDDdKK....",
                ".....KKKKKKKKKKK.....",
            },
            new[]
            {
                ".......KCCCCCK.......",
                "......KCCCCCCcK......",
                ".....KCCCCCCCCcK.....",
                "....KCCCCCCCCCCcK....",
                "....KgggggggggggSK...",
                "...KSKDDDDDDDDdKK....",
                "...KSKDDDDDDDDdKK....",
                "....KKDBDDDBDDdK.....",
                ".....KKKKKKKKKKK.....",
            },
        };

        static readonly string[][] SideTorso =
        {
            new[]
            {
                ".......KRCCCK........",
                "......KRrCCCcK.......",
                ".....KCCCCCCCcK......",
                ".....KCCCCCCCcK......",
                ".....KgggggggggK.....",
                ".....KDDKSKDDdK......",
                ".....KDDDKDDDdK......",
                ".....KDBDDBDDdK......",
                ".....KKKKKKKKKK......",
            },
            new[]
            {
                ".......KRCCCK........",
                "......KRrCCCcK.......",
                ".....KCCCCCCCcK......",
                ".....KCCCCCCCcK......",
                "....KSgggggggggK.....",
                "....KSKDDDDDDdK......",
                ".....KDDDDDDDdK......",
                ".....KDBDDBDDdK......",
                ".....KKKKKKKKKK......",
            },
            new[]
            {
                ".......KRCCCK........",
                "......KRrCCCcK.......",
                ".....KCCCCCCCcK......",
                ".....KCCCCCCCcK......",
                ".....KggggggggSK.....",
                ".....KDDDDDDDKSK.....",
                ".....KDDDDDDDdK......",
                ".....KDBDDBDDdK......",
                ".....KKKKKKKKKK......",
            },
        };

        static readonly string[][] FrontLegs =
        {
            new[]
            {
                ".......KTK.KTK.......",
                ".......KTK.KTK.......",
                "......KFFK.KFFK......",
                "......KKKK.KKKK......",
            },
            new[]
            {
                ".......KTK.KTK.......",
                "......KFFK.KTK.......",
                "......KKKK.KFFK......",
                "...........KKKK......",
            },
            new[]
            {
                ".......KTK.KTK.......",
                ".......KTK.KFFK......",
                "......KFFK.KKKK......",
                "......KKKK...........",
            },
        };

        static readonly string[][] SideLegs =
        {
            new[]
            {
                "........KTTK.........",
                "........KTTK.........",
                ".......KFFFK.........",
                ".......KKKKK.........",
            },
            new[]
            {
                ".......KTKtK.........",
                "......KTK.KtK........",
                ".....KFFK..KffK......",
                ".....KKKK..KKKK......",
            },
            new[]
            {
                ".......KtKTK.........",
                "......KtK.KTK........",
                ".....KffK..KFFK......",
                ".....KKKK..KKKK......",
            },
        };

        static readonly string[] Shadow =
        {
            "......zzzzzzzzz......",
            ".......zzzzzzz.......",
        };

        /// <summary>一個影格的點陣，由上往下 <see cref="Rows"/> 列、每列 <see cref="Columns"/> 個字元，'.' 是透明。</summary>
        public static string[] Pixels(Facing facing, int frame, bool blink)
        {
            bool mirror = facing == Facing.Right;
            var (head, closedEyes, torsos, legs, frames) = Parts(mirror ? Facing.Left : facing);
            var (legPose, torsoPose, drop) = frames[frame];
            var grid = new char[Rows][];
            for (int row = 0; row < Rows; row++) grid[row] = new string('.', Columns).ToCharArray();
            Stamp(grid, Shadow, ShadowRow);
            Stamp(grid, legs[legPose], LegsRow);
            Stamp(grid, torsos[torsoPose], TorsoRow + drop);
            Stamp(grid, head, drop);
            if (blink && closedEyes != null)
                foreach (var (row, pixels) in closedEyes) Stamp(grid, new[] { pixels }, row + drop);

            var rows = new string[Rows];
            for (int row = 0; row < Rows; row++)
            {
                if (mirror) Array.Reverse(grid[row]);
                rows[row] = new string(grid[row]);
            }
            return rows;
        }

        public static Mesh Build(Facing facing, int frame, bool blink)
        {
            var pixels = Pixels(facing, frame, blink);
            var builder = new MeshBuilder();
            for (int row = 0; row < Rows; row++)
            {
                float bottom = (SoleRow - row) * PixelSize, top = bottom + PixelSize;
                for (int start = 0; start < Columns;)
                {
                    char c = pixels[row][start];
                    int end = start + 1;
                    while (end < Columns && pixels[row][end] == c) end++;
                    if (c != '.')
                        builder.Add(Shapes.Rect(new Vector2(X(start), bottom), new Vector2(X(end), top)), Palette[c]);
                    start = end;
                }
            }
            return builder.Apply(new Mesh { name = $"PixelHero {facing} {frame}{(blink ? " blink" : "")}", hideFlags = HideFlags.DontSave });
        }

        static float X(int column) => (column - Center - 0.5f) * PixelSize;

        static (string[] head, (int row, string pixels)[] closedEyes, string[][] torsos, string[][] legs, (int legs, int torso, int drop)[] frames) Parts(Facing view)
        {
            switch (view)
            {
                case Facing.Up: return (BackHead, null, BackTorso, FrontLegs, FrontFrames);
                case Facing.Left: return (SideHead, SideBlink, SideTorso, SideLegs, SideFrames);
                default: return (FrontHead, FrontBlink, FrontTorso, FrontLegs, FrontFrames);
            }
        }

        static void Stamp(char[][] grid, string[] part, int top)
        {
            for (int i = 0; i < part.Length; i++)
                for (int column = 0; column < Columns; column++)
                    if (part[i][column] != '.') grid[top + i][column] = part[i][column];
        }

        static Color Hex(int rgb) =>
            new Color((rgb >> 16 & 0xFF) / 255f, (rgb >> 8 & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }
}

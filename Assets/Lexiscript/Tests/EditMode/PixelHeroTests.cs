using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Lexiscript
{
    /// <summary>像素小人的點陣與網格：大小、鏡像、走路循環、眨眼。</summary>
    public sealed class PixelHeroTests
    {
        static readonly Facing[] Facings = { Facing.Down, Facing.Up, Facing.Left, Facing.Right };
        static readonly char[] EyeColors = { 'E', 'A', 'a', 'W' };

        static IEnumerable<(Facing facing, int frame, bool blink)> AllFrames() =>
            from facing in Facings
            from frame in Enumerable.Range(0, PixelHero.FrameCount)
            from blink in new[] { false, true }
            select (facing, frame, blink);

        static string[] Pixels(Facing facing, int frame) => PixelHero.Pixels(facing, frame, false);

        /// <summary>最上面有像素的那一列；頭往下沉時會變大。</summary>
        static int Top(string[] pixels) => System.Array.FindIndex(pixels, row => row.Any(c => c != '.'));

        /// <summary>某種顏色的像素平均落在第幾欄。</summary>
        static float MeanColumn(string[] pixels, char color) =>
            (float)pixels.SelectMany(row => row.Select((c, column) => (c, column))).Where(p => p.c == color).Average(p => p.column);

        /// <summary>畫面左半或右半最低的鞋子像素在第幾列；數字小代表那隻腳抬得比較高。</summary>
        static int ShoeRow(string[] pixels, bool leftHalf)
        {
            int center = PixelHero.Columns / 2;
            for (int row = pixels.Length - 1; row >= 0; row--)
                for (int column = 0; column < PixelHero.Columns; column++)
                    if (pixels[row][column] == 'F' && (leftHalf ? column < center : column > center)) return row;
            return -1;
        }

        [Test]
        public void EveryFrameIsTheSameGridAndStandsOnTheOrigin()
        {
            foreach (var (facing, frame, blink) in AllFrames())
            {
                var pixels = PixelHero.Pixels(facing, frame, blink);
                Assert.AreEqual(PixelHero.Rows, pixels.Length);
                Assert.IsTrue(pixels.All(row => row.Length == PixelHero.Columns), $"{facing} {frame}");
                // 點陣裡有調色盤沒有的字元時，建網格會丟例外
                var mesh = PixelHero.Build(facing, frame, blink);
                try
                {
                    var bounds = mesh.bounds;
                    Assert.Greater(mesh.vertexCount, 0);
                    Assert.AreEqual(-PixelHero.PixelSize, bounds.min.y, 1e-4f, "只有影子在腳底以下");
                    Assert.That(bounds.size.y, Is.InRange(0.8f, 1f), $"{facing} {frame}：總高約 0.8–1.0");
                    Assert.LessOrEqual(bounds.size.x, 0.6f, $"{facing} {frame}：寬度跟方塊之間的縫差不多");
                    Assert.AreEqual(0, bounds.center.x, 0.05f, $"{facing} {frame}：站在原點正上方");
                }
                finally
                {
                    Object.DestroyImmediate(mesh);
                }
            }
        }

        [Test]
        public void HeadIsAboutHalfTheHeight()
        {
            foreach (var facing in Facings)
            {
                var pixels = Pixels(facing, 0);
                // 頭的最下緣是髮尾；從那一列往下就是身體
                int chin = System.Array.FindLastIndex(pixels, row => row.Contains('h'));
                int feet = System.Array.FindLastIndex(pixels, row => row.Contains('K'));
                float head = (chin + 1f) / (feet + 1f);
                Assert.That(head, Is.InRange(0.45f, 0.65f), $"{facing}：Q 版大頭身");
            }
        }

        [Test]
        public void FacingRightIsTheMirrorOfFacingLeft()
        {
            foreach (var (_, frame, blink) in AllFrames().Where(f => f.facing == Facing.Left))
            {
                var left = PixelHero.Pixels(Facing.Left, frame, blink);
                var right = PixelHero.Pixels(Facing.Right, frame, blink);
                for (int row = 0; row < PixelHero.Rows; row++)
                    Assert.AreEqual(new string(left[row].Reverse().ToArray()), right[row], $"frame {frame} row {row}");
            }
        }

        [Test]
        public void FaceShowsBigEyesAndBlush()
        {
            Assert.AreEqual(2, Pixels(Facing.Down, 0).Sum(row => row.Count(c => c == 'W')), "正面看得到兩隻眼睛的高光");
            Assert.AreEqual(1, Pixels(Facing.Left, 0).Sum(row => row.Count(c => c == 'W')), "側面只看得到一隻眼睛");
            Assert.IsFalse(Pixels(Facing.Up, 0).Any(row => row.IndexOfAny(EyeColors) >= 0), "背面看不到臉");
            foreach (var facing in new[] { Facing.Down, Facing.Left, Facing.Right })
            {
                var pixels = Pixels(facing, 0);
                Assert.GreaterOrEqual(pixels.Sum(row => row.Count(c => c == 'E' || c == 'A' || c == 'a')), 8, $"{facing}：大眼睛");
                Assert.IsTrue(pixels.Any(row => row.Contains('P')), $"{facing}：有腮紅");
            }
        }

        [Test]
        public void FrontAndBackWalkLiftsTheFeetInTurn()
        {
            foreach (var facing in new[] { Facing.Down, Facing.Up })
            {
                Assert.AreEqual(ShoeRow(Pixels(facing, 0), true), ShoeRow(Pixels(facing, 0), false), "站著時兩腳一樣高");
                Assert.Less(ShoeRow(Pixels(facing, 2), true), ShoeRow(Pixels(facing, 2), false), $"{facing}：先抬左邊的腳");
                Assert.Greater(ShoeRow(Pixels(facing, 4), true), ShoeRow(Pixels(facing, 4), false), $"{facing}：再抬右邊的腳");
                CollectionAssert.AreEqual(Pixels(facing, 3), Pixels(facing, 5), "兩次換腳之間是同一個併腳姿勢");
            }
        }

        [Test]
        public void SideWalkSwapsWhichLegIsInFront()
        {
            // F 是靠近鏡頭那隻腳的鞋子：一步在前、一步在後
            Assert.Less(MeanColumn(Pixels(Facing.Left, 2), 'F'), MeanColumn(Pixels(Facing.Left, 4), 'F'), "面向左時，近側的腳先在前（左）再在後");
            Assert.Greater(MeanColumn(Pixels(Facing.Right, 2), 'F'), MeanColumn(Pixels(Facing.Right, 4), 'F'), "面向右時方向相反");
            CollectionAssert.AreEqual(Pixels(Facing.Left, 3), Pixels(Facing.Left, 5));
        }

        [Test]
        public void BodyBobsWhileWalkingAndBreathingWhileIdle()
        {
            foreach (var facing in Facings)
            {
                int Head(int frame) => Top(Pixels(facing, frame));
                Assert.AreEqual(Head(0) + 1, Head(1), $"{facing}：吐氣時頭沉一格");
                Assert.AreEqual(1, Mathf.Abs(Head(2) - Head(3)), $"{facing}：走路時身體一上一下");
                Assert.AreEqual(Head(2), Head(4), facing.ToString());
                Assert.AreEqual(Head(3), Head(5), facing.ToString());
            }
        }

        [Test]
        public void BlinkingOnlyClosesTheEyes()
        {
            foreach (var (facing, frame, _) in AllFrames().Where(f => !f.blink))
            {
                var open = PixelHero.Pixels(facing, frame, false);
                var closed = PixelHero.Pixels(facing, frame, true);
                if (facing == Facing.Up)
                {
                    CollectionAssert.AreEqual(open, closed, "背面看不到眼睛");
                    continue;
                }
                Assert.IsTrue(open.Any(row => row.IndexOfAny(EyeColors) >= 0));
                Assert.IsFalse(closed.Any(row => row.IndexOfAny(EyeColors) >= 0), $"{facing} {frame}：閉眼時看不到眼珠");
                int drop = Top(open);
                for (int row = 0; row < PixelHero.Rows; row++)
                    if (open[row] != closed[row])
                        Assert.That(row - drop, Is.InRange(PixelHero.EyeTop, PixelHero.EyeBottom), $"{facing} {frame}：只換眼睛那幾列");
            }
        }

        [Test]
        public void SquareLookMatchesTheColliderAndStandsOnTheOrigin()
        {
            var mesh = Art.PlayerSquare();
            try
            {
                var bounds = mesh.bounds;
                Assert.AreEqual(2 * Art.PlayerHalf, bounds.size.x, 1e-5f, "跟碰撞體一樣寬");
                Assert.AreEqual(2 * Art.PlayerHalf, bounds.size.y, 1e-5f, "跟碰撞體一樣高");
                Assert.AreEqual(0, bounds.min.y, 1e-5f, "底邊在原點（腳底）");
                Assert.AreEqual(0, bounds.center.x, 1e-5f);
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }
    }
}

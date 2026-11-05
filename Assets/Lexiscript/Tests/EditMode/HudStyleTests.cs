using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Lexiscript
{
    public sealed class HudStyleTests
    {
        /// <summary>UiShape 以扇形三角化，只有逆時針的凸多邊形才畫得正確（重合的點算直線，可以接受）。</summary>
        static void AssertConvex(List<Vector2> polygon)
        {
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Count], c = polygon[(i + 2) % polygon.Count];
                float cross = (b.x - a.x) * (c.y - b.y) - (b.y - a.y) * (c.x - b.x);
                Assert.GreaterOrEqual(cross, -1e-3f, "要是逆時針的凸多邊形");
            }
        }

        static Vector2 Centroid(List<Vector2> polygon) =>
            polygon.Aggregate(Vector2.zero, (sum, point) => sum + point) / polygon.Count;

        /// <summary>邊上的點往中心挪一點再判斷，避免浮點誤差把剛好在邊上的點算成外面。</summary>
        static bool Inside(List<Vector2> area, Vector2 point) => Shapes.Contains(area, Vector2.Lerp(point, Centroid(area), 0.01f));

        static Vector2 Direction(float degrees) =>
            new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));

        [Test]
        public void ChamferedCutsOnlyTheRequestedCorners()
        {
            var panel = HudStyle.Chamfered(new Rect(0, 0, 100, 50), 10, 0, 20, 0);
            AssertConvex(panel);
            Assert.IsTrue(Shapes.Contains(panel, new Vector2(50, 25)));
            Assert.IsFalse(Shapes.Contains(panel, new Vector2(2, 48)), "左上角切掉");
            Assert.IsTrue(Shapes.Contains(panel, new Vector2(99, 49)), "右上角沒切");
            Assert.IsFalse(Shapes.Contains(panel, new Vector2(98, 2)), "右下角切掉");
            Assert.IsTrue(Shapes.Contains(panel, new Vector2(1, 1)), "左下角沒切");
        }

        [Test]
        public void FrameIsAThinBandAlongTheOutline()
        {
            var rect = new Rect(0, 0, 100, 50);
            var outline = HudStyle.Chamfered(rect, 10, 0, 10, 0);
            var frame = HudStyle.Frame(rect, 10, 0, 10, 0, 2).ToList();
            foreach (var piece in frame)
            {
                AssertConvex(piece);
                Assert.IsTrue(piece.All(point => Inside(outline, point)), "細框不超出外緣");
            }
            Assert.IsFalse(frame.Any(piece => Shapes.Contains(piece, new Vector2(50, 25))), "中間是空的");
            Assert.IsFalse(frame.Any(piece => Shapes.Contains(piece, new Vector2(50, 47))), "框只有 2 像素粗");
            Assert.IsTrue(frame.Any(piece => Shapes.Contains(piece, new Vector2(50, 49))), "上緣");
            Assert.IsTrue(frame.Any(piece => Shapes.Contains(piece, new Vector2(5.5f, 44.5f))), "斜切的角也有框");
        }

        [Test]
        public void HatchStaysInsideItsArea()
        {
            var area = HudStyle.Chamfered(new Rect(0, 0, 60, 20), 6, 0, 6, 0);
            var stripes = HudStyle.Hatch(area, 8, 3).ToList();
            Assert.Greater(stripes.Count, 4);
            foreach (var stripe in stripes)
            {
                AssertConvex(stripe);
                Assert.IsTrue(stripe.All(point => Inside(area, point)), "斜紋裁在範圍內");
            }
            // (x − y) / √2 落在 [8k, 8k + 3] 的是條紋：(34, 10) 在 k = 2 那條上，(30, 10) 在兩條之間
            Assert.IsTrue(stripes.Any(stripe => Shapes.Contains(stripe, new Vector2(34, 10))));
            Assert.IsFalse(stripes.Any(stripe => Shapes.Contains(stripe, new Vector2(30, 10))), "條紋之間有空隙");
        }

        [Test]
        public void BarcodeAlternatesBarsAndGaps()
        {
            var bars = HudStyle.Barcode(Vector2.zero, 10, "2131", 2).ToList();
            Assert.AreEqual(2, bars.Count, "奇數位是空白");
            Assert.AreEqual(new Vector2(0, 0), bars[0][0]);
            Assert.AreEqual(new Vector2(4, 10), bars[0][2]);
            Assert.AreEqual(new Vector2(6, 0), bars[1][0]);
            Assert.AreEqual(new Vector2(12, 10), bars[1][2]);
        }

        [Test]
        public void GearHasARealHoleAndSeparateTeeth()
        {
            var gear = HudStyle.Gear(8, 19, 14, 6).ToList();
            foreach (var polygon in gear) AssertConvex(polygon);
            Assert.IsFalse(gear.Any(polygon => Shapes.Contains(polygon, Vector2.zero)), "中間的孔是透明的，不是拿背景色蓋的");
            Assert.IsTrue(gear.Any(polygon => Shapes.Contains(polygon, Direction(4.5f) * 10)), "圓環");
            Assert.IsTrue(gear.Any(polygon => Shapes.Contains(polygon, new Vector2(17, 0))), "齒");
            Assert.IsFalse(gear.Any(polygon => Shapes.Contains(polygon, Direction(22.5f) * 17)), "齒與齒之間是空的");
        }
    }
}

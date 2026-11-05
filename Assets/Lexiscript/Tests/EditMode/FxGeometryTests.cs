using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Lexiscript
{
    public sealed class FxGeometryTests
    {
        static float Area(List<Vector2> polygon)
        {
            float twice = 0;
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Count];
                twice += a.x * b.y - b.x * a.y;
            }
            return twice / 2;
        }

        /// <summary>點在逆時針凸多邊形內；邊上的點容許一點浮點誤差。</summary>
        static bool Inside(List<Vector2> convex, Vector2 point)
        {
            for (int i = 0; i < convex.Count; i++)
            {
                Vector2 a = convex[i], b = convex[(i + 1) % convex.Count];
                if ((b.x - a.x) * (point.y - a.y) - (b.y - a.y) * (point.x - a.x) < -1e-5f) return false;
            }
            return true;
        }

        [TestCase(BlockShape.Plain)]
        [TestCase(BlockShape.Beveled)]
        public void GoldSweepsAcrossTheAnswerBoxFromTheLeft(BlockShape shape)
        {
            var box = Art.AnswerBox(shape);
            Assert.Less(SolveEffect.Sweep(box, 0).Count, 3, "還沒開始時什麼都不畫");
            var early = SolveEffect.Sweep(box, 0.25f);
            Assert.AreEqual(box.Min(point => point.x), early.Min(point => point.x), 1e-5f, "從最左邊開始刷");
            Assert.Less(early.Max(point => point.x), box.Max(point => point.x), "右邊還沒刷到");

            float previous = 0;
            for (int i = 1; i <= 20; i++)
            {
                var fill = SolveEffect.Sweep(box, i / 20f);
                Assert.IsTrue(fill.All(point => Inside(box, point)), "不超出答案欄，斜角方塊的切角也一樣");
                float area = Area(fill);
                Assert.Greater(area, previous, "越刷越多");
                previous = area;
            }
            Assert.AreEqual(Area(box), previous, 1e-6f, "刷到底剛好是整個答案欄");
        }
    }
}

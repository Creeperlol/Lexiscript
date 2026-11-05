using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Lexiscript
{
    public sealed class ShapesTests
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

        [Test]
        public void ClipKeepsTheInnerHalfPlane()
        {
            var square = Shapes.Octagon(1, 0);
            var half = Shapes.Clip(square, new Vector2(1, 1).normalized, 0);
            Assert.IsTrue(half.All(p => p.x + p.y <= 1e-5f));
            Assert.AreEqual(2f, Area(half), 1e-5f);
        }

        [Test]
        public void ClipOutsideReturnsNothingToDraw()
        {
            var square = Shapes.Octagon(1, 0);
            Assert.Less(Shapes.Clip(square, Vector2.right, -2).Count, 3);
        }

        [Test]
        public void ContainsMatchesTheBeveledOutline()
        {
            var outline = Shapes.Octagon(1, 0.3f);
            Assert.IsTrue(Shapes.Contains(outline, Vector2.zero));
            Assert.IsTrue(Shapes.Contains(outline, new Vector2(0.95f, 0)));
            Assert.IsFalse(Shapes.Contains(outline, new Vector2(0.95f, 0.95f)), "斜切掉的角不算在格子裡");
            Assert.IsFalse(Shapes.Contains(outline, new Vector2(1.1f, 0)));
        }

        [Test]
        public void PolygonIsCounterClockwiseAndRegular()
        {
            var triangle = Shapes.Polygon(Vector2.zero, 1, 3, 90);
            Assert.AreEqual(3, triangle.Count);
            Assert.Greater(Area(triangle), 0);
            Assert.AreEqual(new Vector2(0, 1).x, triangle[0].x, 1e-5f);
            Assert.AreEqual(1, triangle[0].y, 1e-5f);
        }

        [Test]
        public void BevelBandHasUniformWidth()
        {
            const float chamfer = 0.3f, inset = 0.04f;
            var outer = Shapes.Octagon(1, chamfer);
            var inner = Shapes.Octagon(1 - inset, Shapes.InsetChamfer(chamfer, inset));
            // 第一象限的斜邊在 x + y = 常數 的直線上，兩條線的距離就是色帶寬度
            float gap = (outer[2].x + outer[2].y - (inner[2].x + inner[2].y)) / Mathf.Sqrt(2);
            Assert.AreEqual(inset, gap, 1e-5f);
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Lexiscript
{
    /// <summary>產生凸多邊形頂點（逆時針）的幾何工具。</summary>
    public static class Shapes
    {
        /// <summary>以原點為中心的正方形；chamfer 大於 0 時四角切成斜角。</summary>
        public static List<Vector2> Octagon(float half, float chamfer)
        {
            // 網格從第一個頂點扇形展開。正方形從角展開的話內部那條邊是 45° 對角線，開 MSAA 時鏡頭停在某些次像素位置，
            // 這條邊上會漏出斷斷續續的細縫；從底邊中點開始，內部的邊就不是 45°
            if (chamfer <= 0)
                return new List<Vector2>
                {
                    new Vector2(0, -half), new Vector2(half, -half), new Vector2(half, half),
                    new Vector2(-half, half), new Vector2(-half, -half),
                };
            float h = half, c = chamfer;
            return new List<Vector2>
            {
                new Vector2(h - c, -h), new Vector2(h, -h + c),
                new Vector2(h, h - c), new Vector2(h - c, h),
                new Vector2(-h + c, h), new Vector2(-h, h - c),
                new Vector2(-h, -h + c), new Vector2(-h + c, -h),
            };
        }

        /// <summary>
        /// 斜切八邊形內縮 inset 後應有的斜角長度。
        /// 直接沿用原斜角的話，斜邊處的色帶會比直邊寬 √2 倍。
        /// </summary>
        public static float InsetChamfer(float chamfer, float inset) =>
            Mathf.Max(0, chamfer - inset * (2 - Mathf.Sqrt(2)));

        /// <summary>只保留 dot(p, normal) ≤ max 的部分（單一半平面的 Sutherland–Hodgman）。</summary>
        public static List<Vector2> Clip(List<Vector2> polygon, Vector2 normal, float max)
        {
            var result = new List<Vector2>();
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Count];
                float da = Vector2.Dot(a, normal) - max, db = Vector2.Dot(b, normal) - max;
                if (da <= 0) result.Add(a);
                if ((da < 0 && db > 0) || (da > 0 && db < 0)) result.Add(Vector2.Lerp(a, b, da / (da - db)));
            }
            return result;
        }

        public static List<Vector2> Rect(Vector2 min, Vector2 max) => new List<Vector2>
        {
            min, new Vector2(max.x, min.y), max, new Vector2(min.x, max.y),
        };

        public static List<Vector2> Diamond(Vector2 center, float radius) => new List<Vector2>
        {
            center + new Vector2(0, -radius), center + new Vector2(radius, 0),
            center + new Vector2(0, radius), center + new Vector2(-radius, 0),
        };

        /// <summary>正多邊形；邊數夠多就當圓用。degrees 是第一個頂點的角度。</summary>
        public static List<Vector2> Polygon(Vector2 center, float radius, int sides, float degrees)
        {
            var points = new List<Vector2>();
            for (int i = 0; i < sides; i++)
            {
                float angle = degrees * Mathf.Deg2Rad + i * 2 * Mathf.PI / sides;
                points.Add(center + radius * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
            }
            return points;
        }

        /// <summary>點是否在逆時針凸多邊形內（含邊上）。</summary>
        public static bool Contains(List<Vector2> convexPolygon, Vector2 point)
        {
            for (int i = 0; i < convexPolygon.Count; i++)
            {
                Vector2 a = convexPolygon[i], b = convexPolygon[(i + 1) % convexPolygon.Count];
                if ((b.x - a.x) * (point.y - a.y) - (b.y - a.y) * (point.x - a.x) < 0) return false;
            }
            return true;
        }

        /// <summary>從 a 到 b 的粗線段，兩端各延伸半個線寬（方形端點）。</summary>
        public static List<Vector2> Segment(Vector2 a, Vector2 b, float width)
        {
            Vector2 along = (b - a).normalized * (width / 2);
            Vector2 side = new Vector2(-along.y, along.x);
            a -= along;
            b += along;
            return new List<Vector2> { a - side, b - side, b + side, a + side };
        }

        /// <summary>rect 扣掉 holes 之後剩下的幾塊長方形。</summary>
        public static IEnumerable<Rect> Subtract(Rect rect, IEnumerable<Rect> holes)
        {
            var pieces = new List<Rect> { rect };
            foreach (var hole in holes)
            {
                var next = new List<Rect>();
                foreach (var piece in pieces)
                {
                    if (!piece.Overlaps(hole))
                    {
                        next.Add(piece);
                        continue;
                    }
                    float yMin = Mathf.Max(piece.yMin, hole.yMin), yMax = Mathf.Min(piece.yMax, hole.yMax);
                    if (hole.yMax < piece.yMax) next.Add(UnityEngine.Rect.MinMaxRect(piece.xMin, hole.yMax, piece.xMax, piece.yMax));
                    if (hole.yMin > piece.yMin) next.Add(UnityEngine.Rect.MinMaxRect(piece.xMin, piece.yMin, piece.xMax, hole.yMin));
                    if (hole.xMin > piece.xMin) next.Add(UnityEngine.Rect.MinMaxRect(piece.xMin, yMin, hole.xMin, yMax));
                    if (hole.xMax < piece.xMax) next.Add(UnityEngine.Rect.MinMaxRect(hole.xMax, yMin, piece.xMax, yMax));
                }
                pieces = next;
            }
            return pieces.Where(piece => piece.width > 1e-4f && piece.height > 1e-4f);
        }
    }
}

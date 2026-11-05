using System.Collections.Generic;
using UnityEngine;

namespace Lexiscript
{
    /// <summary>
    /// 總答題數與設定介面共用的平面設計配色與圖形（參考鷹角網路／明日方舟）：深色硬邊面板、45° 斜切角、細線、
    /// 條碼、斜紋、角落括號，強調色是淺金（<see cref="Art.Gold"/>）、危險操作用玫瑰紅。
    /// 座標是 <see cref="UiShape"/> 的介面像素（y 往上）。
    /// </summary>
    public static class HudStyle
    {
        /// <summary>危險操作（重置進度）的玫瑰紅。</summary>
        public static readonly Color Danger = Hex(0xD7263D);

        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        public static void AddAll(this UiShape shape, IEnumerable<List<Vector2>> polygons, Color color)
        {
            foreach (var polygon in polygons) shape.Add(polygon, color);
        }

        /// <summary>
        /// 四角依序（左上、右上、右下、左下）切掉 45° 斜角的矩形，切角是沿邊量的長度。
        /// 沒切的角也輸出兩個重合的點，內外框的頂點才能一一對應（見 <see cref="Frame"/>）。
        /// </summary>
        public static List<Vector2> Chamfered(Rect rect, float topLeft, float topRight, float bottomRight, float bottomLeft)
        {
            float x0 = rect.xMin, y0 = rect.yMin, x1 = rect.xMax, y1 = rect.yMax;
            return new List<Vector2>
            {
                new Vector2(x0, y0 + bottomLeft), new Vector2(x0 + bottomLeft, y0),
                new Vector2(x1 - bottomRight, y0), new Vector2(x1, y0 + bottomRight),
                new Vector2(x1, y1 - topRight), new Vector2(x1 - topRight, y1),
                new Vector2(x0 + topLeft, y1), new Vector2(x0, y1 - topLeft),
            };
        }

        /// <summary>
        /// <see cref="Chamfered"/> 外緣往內 thickness 的細框，每邊各一塊梯形。
        /// 用線段疊出來的話轉角會重疊，半透明時轉角會多一個亮點。
        /// </summary>
        public static IEnumerable<List<Vector2>> Frame(Rect rect, float topLeft, float topRight, float bottomRight, float bottomLeft, float thickness)
        {
            var outer = Chamfered(rect, topLeft, topRight, bottomRight, bottomLeft);
            var inset = new Rect(rect.x + thickness, rect.y + thickness, rect.width - 2 * thickness, rect.height - 2 * thickness);
            float Cut(float chamfer) => Shapes.InsetChamfer(chamfer, thickness);
            var inner = Chamfered(inset, Cut(topLeft), Cut(topRight), Cut(bottomRight), Cut(bottomLeft));
            for (int i = 0; i < outer.Count; i++)
            {
                int next = (i + 1) % outer.Count;
                yield return new List<Vector2> { outer[i], outer[next], inner[next], inner[i] };
            }
        }

        /// <summary>裁在凸多邊形 area 內的 45° 斜紋（／）。</summary>
        public static IEnumerable<List<Vector2>> Hatch(List<Vector2> area, float pitch, float width)
        {
            var across = new Vector2(1, -1).normalized;
            float from = float.MaxValue, to = float.MinValue;
            foreach (var corner in area)
            {
                from = Mathf.Min(from, Vector2.Dot(corner, across));
                to = Mathf.Max(to, Vector2.Dot(corner, across));
            }
            for (int k = Mathf.FloorToInt(from / pitch) - 1; k * pitch <= to; k++)
            {
                var stripe = Shapes.Clip(Shapes.Clip(area, across, k * pitch + width), -across, -k * pitch);
                if (stripe.Count >= 3) yield return stripe;
            }
        }

        /// <summary>條碼：pattern 的數字依序是黑條、空白、黑條……的寬度（以 unit 為單位）。</summary>
        public static IEnumerable<List<Vector2>> Barcode(Vector2 min, float height, string pattern, float unit)
        {
            float x = min.x;
            for (int i = 0; i < pattern.Length; i++)
            {
                float width = (pattern[i] - '0') * unit;
                if (i % 2 == 0) yield return Shapes.Rect(new Vector2(x, min.y), new Vector2(x + width, min.y + height));
                x += width;
            }
        }

        /// <summary>水平虛線，from 是左端線條中心。</summary>
        public static IEnumerable<List<Vector2>> Dashes(Vector2 from, float length, float thickness, float dash, float gap)
        {
            for (float x = 0; x < length; x += dash + gap)
                yield return Shapes.Rect(from + new Vector2(x, -thickness / 2), from + new Vector2(Mathf.Min(x + dash, length), thickness / 2));
        }

        /// <summary>直角括號：從轉角 corner 往兩個方向各伸出一臂。</summary>
        public static IEnumerable<List<Vector2>> Bracket(Vector2 corner, Vector2 armA, Vector2 armB, float thickness)
        {
            yield return Shapes.Segment(corner, corner + armA, thickness);
            yield return Shapes.Segment(corner, corner + armB, thickness);
        }

        /// <summary>✕ 形的關閉記號。</summary>
        public static IEnumerable<List<Vector2>> Cross(Vector2 center, float half, float thickness)
        {
            yield return Shapes.Segment(center + new Vector2(-half, -half), center + new Vector2(half, half), thickness);
            yield return Shapes.Segment(center + new Vector2(-half, half), center + new Vector2(half, -half), thickness);
        }

        /// <summary>
        /// 以原點為中心的齒輪：中間鏤空的圓環加一圈梯形齒。圓環拆成一圈小梯形，孔才是真的透明，
        /// 不用拿背景色去蓋（背景是半透明的）。
        /// </summary>
        public static IEnumerable<List<Vector2>> Gear(int teeth, float tipRadius, float bodyRadius, float holeRadius)
        {
            const int Segments = 40;
            for (int i = 0; i < Segments; i++)
            {
                var a = Direction(i * 360f / Segments);
                var b = Direction((i + 1) * 360f / Segments);
                yield return new List<Vector2> { a * holeRadius, a * bodyRadius, b * bodyRadius, b * holeRadius };
            }
            float pitch = 2 * Mathf.PI * bodyRadius / teeth;
            float baseHalf = pitch * 0.32f, tipHalf = pitch * 0.22f, root = bodyRadius - 1.5f;
            for (int k = 0; k < teeth; k++)
            {
                var along = Direction(k * 360f / teeth);
                var side = new Vector2(-along.y, along.x);
                yield return new List<Vector2>
                {
                    along * root - side * baseHalf, along * tipRadius - side * tipHalf,
                    along * tipRadius + side * tipHalf, along * root + side * baseHalf,
                };
            }
        }

        static Vector2 Direction(float degrees) =>
            new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));

        static Color Hex(int rgb) =>
            new Color((rgb >> 16 & 0xFF) / 255f, (rgb >> 8 & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }
}

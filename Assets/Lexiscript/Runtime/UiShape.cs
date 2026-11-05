using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Lexiscript
{
    /// <summary>
    /// uGUI 版的 <see cref="MeshBuilder"/>：在 RectTransform 的本地座標（介面像素，原點是 pivot）裡疊凸多邊形，
    /// 畫齒輪、箭頭、條碼線、斜紋這類平面設計的圖形。後加的蓋在前面。
    /// 頂點色會乘上 <see cref="Graphic.color"/>，整組淡入淡出只要改 color.a。
    /// </summary>
    public sealed class UiShape : MaskableGraphic
    {
        readonly List<List<Vector2>> polygons = new List<List<Vector2>>();
        readonly List<Color> colors = new List<Color>();

        public int Count => polygons.Count;

        public void Clear()
        {
            polygons.Clear();
            colors.Clear();
            SetVerticesDirty();
        }

        public void Add(List<Vector2> convexPolygon, Color color)
        {
            polygons.Add(convexPolygon);
            colors.Add(color);
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            for (int p = 0; p < polygons.Count; p++)
            {
                var polygon = polygons[p];
                if (polygon.Count < 3) continue;
                Color32 tint = colors[p] * color;
                int start = vh.currentVertCount;
                foreach (var point in polygon) vh.AddVert(point, tint, Vector4.zero);
                for (int i = 1; i < polygon.Count - 1; i++) vh.AddTriangle(start, start + i, start + i + 1);
            }
        }
    }
}

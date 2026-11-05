using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lexiscript
{
    /// <summary>把多個凸多邊形依加入順序疊成一個頂點色網格，後加的蓋在前面。</summary>
    public sealed class MeshBuilder
    {
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> triangles = new List<int>();

        public void Add(List<Vector2> convexPolygon, Color srgb)
        {
            if (convexPolygon.Count < 3) return;
            // 專案是 Linear 色彩空間，頂點色不會被自動從 sRGB 轉換
            Color color = srgb.linear;
            int start = vertices.Count;
            foreach (var point in convexPolygon)
            {
                vertices.Add(point);
                colors.Add(color);
            }
            for (int i = 1; i < convexPolygon.Count - 1; i++)
            {
                triangles.Add(start);
                triangles.Add(start + i);
                triangles.Add(start + i + 1);
            }
        }

        public Mesh Apply(Mesh mesh)
        {
            mesh.Clear();
            // 房間網格含大量裝飾圖形，可能超過 16-bit 索引的 65535 個頂點
            mesh.indexFormat = vertices.Count > ushort.MaxValue ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(vertices);
            // 用 float 顏色：暗色轉成 linear 後數值很小，8-bit 會出現色階斷層
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            return mesh;
        }
    }
}

using UnityEngine;

namespace Lexiscript
{
    /// <summary>場景裡各房間的地板範圍與房間之間的門口（世界座標），牆的網格與碰撞由建場景的腳本依此產生。</summary>
    public sealed class Rooms : MonoBehaviour
    {
        public Rect[] floors = new Rect[0];
        public Rect[] doorways = new Rect[0];

        public bool Contains(Vector2 point)
        {
            foreach (var floor in floors)
                if (floor.Contains(point)) return true;
            foreach (var doorway in doorways)
                if (doorway.Contains(point)) return true;
            return false;
        }
    }
}

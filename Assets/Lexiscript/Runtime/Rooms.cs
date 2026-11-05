using UnityEngine;

namespace Lexiscript
{
    /// <summary>正式關卡裡每塊地板的用途。</summary>
    public enum RoomKind
    {
        /// <summary>教學房間，由左而右一間接一間。</summary>
        Tutorial,
        /// <summary>教學之後的開放式大地圖。</summary>
        Field,
        /// <summary>大地圖邊緣、要先解開門前題目才進得去的房間。</summary>
        Gated,
    }

    /// <summary>場景裡各房間的地板範圍與房間之間的門口（世界座標），牆的網格與碰撞由建場景的腳本依此產生。</summary>
    public sealed class Rooms : MonoBehaviour
    {
        public Rect[] floors = new Rect[0];
        public Rect[] doorways = new Rect[0];
        /// <summary>與 <see cref="floors"/> 一一對應；Demo 場景只有一塊地板，沒有分類。</summary>
        public RoomKind[] kinds = new RoomKind[0];

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

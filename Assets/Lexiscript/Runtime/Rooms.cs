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
        /// <summary>大地圖上要開門才進得去的房間。</summary>
        Gated,
        /// <summary>教學出口通往大廳的橋，算大廳的一部分。</summary>
        Bridge,
    }

    /// <summary>場景裡各房間的地板範圍與房間之間的門口（世界座標），牆的網格與碰撞由建場景的腳本依此產生。</summary>
    public sealed class Rooms : MonoBehaviour
    {
        public Rect[] floors = new Rect[0];
        public Rect[] doorways = new Rect[0];
        /// <summary>與 <see cref="floors"/> 一一對應；Demo 場景只有一塊地板，沒有分類。</summary>
        public RoomKind[] kinds = new RoomKind[0];
        /// <summary>
        /// 與 <see cref="floors"/> 一一對應：這塊地板屬於哪一間房（那間房主要那塊地板的索引）。
        /// 走道、側間與橋都是某間房的一部分；空陣列代表每塊地板各自是一間。
        /// </summary>
        public int[] owners = new int[0];
        /// <summary>會擋路的擺設（柱子、盆栽）的範圍。</summary>
        public Rect[] obstacles = new Rect[0];

        /// <summary>這塊地板所屬那間房的主要地板。</summary>
        public int Owner(int floor) => floor >= 0 && floor < owners.Length ? owners[floor] : floor;

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

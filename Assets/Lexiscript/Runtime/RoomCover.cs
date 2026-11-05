using System.Collections.Generic;
using UnityEngine;

namespace Lexiscript
{
    /// <summary>
    /// 門還沒開的房間上方的遮罩：看不到裡面，裡面的方塊也不能點、不顯示顏色名稱。
    /// 門打開時遮罩收起來。
    /// </summary>
    public sealed class RoomCover : MonoBehaviour
    {
        static readonly List<RoomCover> covers = new List<RoomCover>();

        /// <summary>遮住的範圍（世界座標）。</summary>
        public Rect area;

        /// <summary>還遮著（門還沒開）。</summary>
        public bool Covering { get; private set; } = true;

        /// <summary>這個世界座標被某個還沒打開的房間遮住了。</summary>
        public static bool Hides(Vector2 world)
        {
            foreach (var cover in covers)
                if (cover.Covering && cover.area.Contains(world)) return true;
            return false;
        }

        void OnEnable() => covers.Add(this);

        void OnDisable() => covers.Remove(this);
    }
}

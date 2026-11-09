using System.Linq;
using UnityEngine;

namespace Lexiscript
{
    /// <summary>傳送介面裡的一個地點。所在的地板掀開遮罩（解鎖那一區）之後才能傳送過去。</summary>
    public sealed class TeleportPoint : MonoBehaviour
    {
        public string english, chinese;
        /// <summary>在列表裡的順序，小的排前面。</summary>
        public int order;

        public string DisplayName => Localization.Pick(english, chinese);

        public bool Unlocked => !RoomCover.Hides(transform.position);

        public static TeleportPoint[] All() =>
            FindObjectsByType<TeleportPoint>(FindObjectsSortMode.None).OrderBy(point => point.order).ToArray();

        /// <summary>把玩家直接搬到這裡；鏡頭在 <see cref="Player"/> 的 LateUpdate 跟過去。</summary>
        public void Send(Player player)
        {
            var body = player.GetComponent<Rigidbody2D>();
            Vector2 target = transform.position;
            body.position = target;
            body.linearVelocity = Vector2.zero;
            player.transform.position = target;
        }
    }
}

using UnityEngine;

namespace Lexiscript
{
    /// <summary>正式關卡的遊玩進度存檔（progress.json）：重新進入遊戲時接續上次的進度。</summary>
    public sealed class GameProgress : MonoBehaviour
    {
        /// <summary>存檔位置；null 代表 persistentDataPath/progress.json。測試改到暫存檔，以免蓋掉玩家的進度。</summary>
        public static string StoragePath;

        /// <summary>清掉遊戲進度與筆記（設定保留），回到標題畫面。</summary>
        public static void ResetAll()
        {
        }
    }
}

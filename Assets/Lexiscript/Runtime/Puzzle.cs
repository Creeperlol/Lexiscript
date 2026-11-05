using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lexiscript
{
    /// <summary>
    /// 每格的輸入與解題狀態。不依賴場景，也不處理連結。
    /// </summary>
    public sealed class Puzzle
    {
        public const int MaxInputLength = 16;

        readonly Dictionary<string, BlockDef> defs = new Dictionary<string, BlockDef>();
        readonly Dictionary<string, string> inputs = new Dictionary<string, string>();
        readonly HashSet<string> solved = new HashSet<string>();

        public Puzzle(IEnumerable<BlockDef> blocks)
        {
            foreach (var block in blocks)
            {
                defs.Add(block.Id, block);
                inputs.Add(block.Id, "");
            }
        }

        public bool IsSolved(string id) => solved.Contains(id);

        /// <summary>目前輸入，保留玩家打的大小寫；解開後是答案的標準大小寫。</summary>
        public string Input(string id) => inputs[id];

        public string Answer(string id) => defs[id].Answer;

        /// <summary>有答案且還沒解開；純色格永遠不能作答。</summary>
        public bool IsAnswerable(string id) => defs[id].Answer.Length > 0 && !IsSolved(id);

        /// <summary>目前輸入完整等於某個提示答案（不分大小寫）。</summary>
        public bool IsHint(string id) => !IsSolved(id) && defs[id].Hints.Any(hint => Same(hint, inputs[id]));

        /// <summary>整段取代輸入；非英文字母一律濾掉，超過上限的部分截掉。</summary>
        /// <returns>只有這次輸入解開題目時回傳 true；解開後輸入換成答案的標準大小寫。</returns>
        public bool SetInput(string id, string text)
        {
            if (!IsAnswerable(id)) return false;
            inputs[id] = Letters(text);
            if (!Same(inputs[id], defs[id].Answer)) return false;
            inputs[id] = defs[id].Answer;
            solved.Add(id);
            return true;
        }

        /// <summary>清掉沒答對的輸入；已解開的格子不受影響。</summary>
        public void Clear(string id)
        {
            if (!IsSolved(id)) inputs[id] = "";
        }

        /// <summary>
        /// 只認 ASCII 英文字母：char.IsLetter 會放行 é、全形字母，
        /// char.ToUpperInvariant 也會把土耳其文的 ı 轉成 I，跟答案比對會誤判。
        /// </summary>
        public static bool IsLetter(char c) => (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');

        public static string Letters(string text)
        {
            var letters = new StringBuilder(MaxInputLength);
            foreach (var c in text)
            {
                if (letters.Length == MaxInputLength) break;
                if (IsLetter(c)) letters.Append(c);
            }
            return letters.ToString();
        }

        // 兩邊都只剩 ASCII 字母，OrdinalIgnoreCase 不會碰到語系相關的大小寫規則
        static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }
}

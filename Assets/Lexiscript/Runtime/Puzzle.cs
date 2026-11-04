using System.Collections.Generic;
using System.Linq;

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

        public string Input(string id) => inputs[id];

        /// <summary>有答案且還沒解開；純色格永遠不能作答。</summary>
        public bool IsAnswerable(string id) => defs[id].Answers.Length > 0 && !IsSolved(id);

        /// <summary>目前輸入完整等於某個提示答案。</summary>
        public bool IsHint(string id) => !IsSolved(id) && defs[id].Hints.Contains(inputs[id]);

        /// <summary>輸入一個字元；只接受英文字母，大小寫不拘，其他字元一律忽略。</summary>
        /// <returns>只有打出解答的那一下回傳 true。</returns>
        public bool Type(string id, char c)
        {
            if (!IsAnswerable(id) || inputs[id].Length >= MaxInputLength) return false;
            // 用 ASCII 運算轉大寫：char.ToUpperInvariant 會把土耳其文的 ı 轉成 I
            if (c >= 'a' && c <= 'z') c = (char)(c - 'a' + 'A');
            if (c < 'A' || c > 'Z') return false;
            inputs[id] += c;
            if (!defs[id].Answers.Contains(inputs[id])) return false;
            solved.Add(id);
            return true;
        }

        public void Backspace(string id)
        {
            var input = inputs[id];
            if (IsSolved(id) || input.Length == 0) return;
            inputs[id] = input.Substring(0, input.Length - 1);
        }
    }
}

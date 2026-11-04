using System.Collections.Generic;

namespace Lexiscript
{
    /// <summary>各顏色兩種變化的規則說明。M1 只是資料，遊戲內不顯示。</summary>
    public static class ColorCodex
    {
        static readonly Dictionary<BlockColor, string[]> Rules = new Dictionary<BlockColor, string[]>
        {
            { BlockColor.PureWhite, new[] { "相同", "近義詞" } },
            { BlockColor.PureBlack, new[] { "顛倒過來寫", "反義詞" } },
            { BlockColor.LightGray, new[] { "同音", "押韻" } },
            { BlockColor.BrightYellow, new[] { "將字重新排列組合", "添加新的字母進去" } },
            { BlockColor.RoyalBlue, new[] { "換一個字母", "減少字母" } },
            { BlockColor.Colorless, new[] { "由答案反推該格顏色", "由答案反推該格顏色" } },
            { BlockColor.RoseRed, new[] { "上下位詞・往上猜", "上下位詞・往下猜" } },
            { BlockColor.EmeraldGreen, new[] { "同類詞", "搭配詞" } },
            { BlockColor.DeepPurple, new[] { "過去式", "進行式" } },
            { BlockColor.Magenta, new[] { "單轉複數", "寫出該詞詞性" } },
            { BlockColor.SkyCyan, new[] { "名詞↔動詞", "形容詞↔名詞" } },
            { BlockColor.SunsetOrange, new[] { "複合詞", null } },
            { BlockColor.GoldenBrown, new[] { "時間狀態（上＝現在、下＝以前）", "這詞的文言文" } },
        };

        /// <returns>規則文字；尚未定義的變化回傳 null。</returns>
        public static string Rule(BlockColor color, BlockShape shape) => Rules[color][(int)shape];
    }
}

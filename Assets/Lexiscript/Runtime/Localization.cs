using System.Collections.Generic;
using TMPro;

namespace Lexiscript
{
    /// <summary>
    /// 只有設定介面與教學地板上的說明會換語言；題目本身是英文謎題，其他文字都不翻。
    /// 地板說明建場景時寫成英文，執行時依 <see cref="GameSettings.Language"/> 換成這裡的中文。
    /// 新增地板說明時記得在 <see cref="Chinese"/> 補上翻譯（有測試檢查）。
    /// </summary>
    public static class Localization
    {
        static readonly Dictionary<string, string> Chinese = new Dictionary<string, string>
        {
            { "WASD to move · hold Shift to sprint · scroll to zoom", "WASD 移動 · 按住 Shift 衝刺 · 滾輪縮放" },
            { "Click the lower half of a block to type an answer", "點方塊的下半部就能輸入答案" },
            { "Left / right-click the upper half to show the first / last letter", "左鍵／右鍵點上半部，顯示答案的第一個／最後一個字母" },
            { "Try writing the same word?", "試著寫出一模一樣的字？" },
            { "Cut corners? Try a word that means the same.", "切角的方塊？試試意思相同的字。" },
            { "Many words fit? Show the first or last letter.", "很多字都符合？顯示第一個或最後一個字母看看。" },
            { "Black turns words around.", "黑色會把字倒過來。" },
            { "Stuck? Show the answer's first letter.", "卡住了？顯示答案的第一個字母。" },
            { "Notes (bottom-right): write down what each color does!", "右下角的筆記：把每種顏色的作用記下來！" },
            { "Square black flips the spelling. What does cut black flip?", "方角的黑色把拼字倒過來，那切角的黑色倒過來的是什麼？" },
            { "Opposites attract!", "相反的東西會互相吸引！" },
            { "Arrows pass a block's answer on as the next block's clue.", "箭頭會把這格的答案，傳給下一格當題目。" },
            { "Empty clue? Look where its arrow comes from.", "沒有題目？看看箭頭是從哪裡來的。" },
            { "White and black mix now. Check color and corners!", "白色和黑色混在一起了，注意顏色和邊角！" },
            { "Arrows can chain, too.", "箭頭也可以一個接一個。" },
            { "Bridged blocks share the same answer.", "用橋連起來的方塊，答案相同。" },
            { "Solve the side you know, then cross the bridge.", "先解你會的那一邊，再過橋。" },
            { "What does gray do to a word? Say it out loud!", "灰色對字做了什麼？唸出來聽聽！" },
            { "One gray block has no bridge. You've got this!", "有一格灰色沒有橋，你一定可以的！" },
            { "Gray with cut corners. Say each pair: where do they match?", "切角的灰色。把每一組唸出來：哪裡一樣？" },
            { "Bridges still mean: same answer on both sides.", "橋的意思不變：兩邊的答案相同。" },
            { "One gray block has no bridge. Trust your ears!", "有一格灰色沒有橋，相信你的耳朵！" },
            { "From here on, you're on your own.", "接下來，就靠你自己了。" },
            { "A solid block has no blank to fill.", "純色方塊沒有空格可以填。" },
            { "Its answer goes back to where the arrows began.", "它的答案會回到箭頭出發的那一格。" },
            { "Click to reveal the answer.", "點一下顯示答案。" },
        };

        public static bool IsChinese => GameSettings.Language == Language.Chinese;

        /// <summary>設定介面這類兩種語言都直接寫在程式裡的文字。</summary>
        public static string Pick(string english, string chinese) => IsChinese ? chinese : english;

        /// <summary>地板說明目前要顯示的文字；英文模式或沒有翻譯時回傳原文。</summary>
        public static string Floor(string english) =>
            IsChinese && Chinese.TryGetValue(english, out var chinese) ? chinese : english;

        public static bool HasTranslation(string english) => Chinese.ContainsKey(english);

        /// <summary>依目前語言設定文字與字型；遊戲內建的英文字型沒有中文字，中文要換成介面字型。</summary>
        public static void Apply(TMP_Text text, string english, TMP_FontAsset englishFont)
        {
            var shown = Floor(english);
            var font = shown == english ? englishFont : Hud.UiFont;
            if (text.font != font) text.font = font;
            text.text = shown;
        }
    }
}

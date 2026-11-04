namespace Lexiscript
{
    /// <summary>
    /// 13 種方塊顏色，順序同規格。
    /// <c>ToString().ToUpperInvariant()</c> 就是 Colorless 格要輸入的顏色全名。
    /// </summary>
    public enum BlockColor
    {
        PureWhite,
        PureBlack,
        LightGray,
        BrightYellow,
        RoyalBlue,
        Colorless,
        RoseRed,
        EmeraldGreen,
        DeepPurple,
        Magenta,
        SkyCyan,
        SunsetOrange,
        GoldenBrown,
    }

    /// <summary>純方塊是該顏色的第一類變化，斜切角是第二類變化。</summary>
    public enum BlockShape
    {
        Plain,
        Beveled,
    }

    /// <summary>連結只是畫給玩家看的線索，程式不會沿著連結傳遞任何內容。</summary>
    public enum LinkKind
    {
        /// <summary>這格的答案就是被指那格的題目。</summary>
        Pointer,
        /// <summary>兩格答案相同，但仍要各自輸入。</summary>
        Bridge,
        /// <summary>指向與連接橋兩條規則同時成立。</summary>
        BridgePointer,
    }

    /// <summary><see cref="Puzzle"/> 需要的單格資料；答案與提示答案都是大寫 A–Z。</summary>
    public sealed class BlockDef
    {
        public readonly string Id;
        public readonly string[] Answers;
        /// <summary>依規則也成立、但不是指定答案的詞；打出來只跳提示，不會解開。</summary>
        public readonly string[] Hints;

        public BlockDef(string id, string[] answers, string[] hints)
        {
            Id = id;
            Answers = answers;
            Hints = hints;
        }
    }
}

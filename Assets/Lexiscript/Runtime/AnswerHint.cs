using System.Text;

namespace Lexiscript
{
    /// <summary>答案欄沒聚焦時的提示字：底線表示字數，可以露出字首或字尾。</summary>
    public static class AnswerHint
    {
        /// <summary>apple → <c>_____(5)</c>、<c>a____(5)</c>、<c>____e(5)</c>。</summary>
        public static string Placeholder(string answer, Reveal reveal)
        {
            int length = answer.Length;
            var blanks = new StringBuilder(new string('_', length));
            if (length > 0 && reveal == Reveal.First) blanks[0] = answer[0];
            if (length > 0 && reveal == Reveal.Last) blanks[length - 1] = answer[length - 1];
            return $"{blanks}({length})";
        }

        /// <summary>再按一次同一種就收起來，按另一種就直接切換，所以字首字尾不會同時出現。</summary>
        public static Reveal Toggle(Reveal current, Reveal pressed) => current == pressed ? Reveal.None : pressed;
    }
}

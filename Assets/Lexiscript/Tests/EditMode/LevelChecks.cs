using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Lexiscript
{
    /// <summary>Demo 與正式關卡共用的題目與擺放檢查。</summary>
    static class LevelChecks
    {
        static readonly Regex Word = new Regex("^[A-Za-z]{1," + Puzzle.MaxInputLength + "}$");

        public static string[] ColorNames => Enum.GetValues(typeof(BlockColor)).Cast<BlockColor>()
            .Where(c => c != BlockColor.Colorless)
            .Select(c => c.ToString())
            .ToArray();

        public static Vector2Int Delta(Block from, Block to)
        {
            Vector2 delta = to.transform.position - from.transform.position;
            var cells = Vector2Int.RoundToInt(delta / Art.Pitch);
            Assert.Less((delta - (Vector2)cells * Art.Pitch).magnitude, 1e-3f, $"{from.name}->{to.name} 不在格點上");
            return cells;
        }

        public static IEnumerable<(Block from, Block.Link link)> Links(IEnumerable<Block> blocks) =>
            blocks.SelectMany(block => block.links.Select(link => (block, link)));

        /// <summary>一般詞全小寫、專有名詞只有字首大寫、顏色名稱照列舉名稱。</summary>
        public static bool CanonicalCase(string word) =>
            word == word.ToLowerInvariant()
            || (char.IsUpper(word[0]) && word.Substring(1) == word.Substring(1).ToLowerInvariant())
            || ColorNames.Contains(word);

        public static void WordsAreWellFormed(IEnumerable<Block> blocks)
        {
            foreach (var block in blocks)
            {
                if (!string.IsNullOrEmpty(block.clue))
                {
                    Assert.IsTrue(Word.IsMatch(block.clue), $"{block.name} clue: {block.clue}");
                    Assert.IsTrue(CanonicalCase(block.clue), $"{block.name} clue: {block.clue}");
                }
                var words = new[] { block.answer }.Where(word => !string.IsNullOrEmpty(word)).Concat(block.hintAnswers).ToArray();
                foreach (var word in words)
                {
                    Assert.IsTrue(Word.IsMatch(word), $"{block.name}: {word}");
                    Assert.IsTrue(CanonicalCase(word), $"{block.name}: {word}");
                }
                // 打字時一對上答案就解開，所以任何詞都不能是另一個詞的開頭
                foreach (var a in words)
                    foreach (var b in words)
                        Assert.IsFalse(a != b && b.StartsWith(a, StringComparison.OrdinalIgnoreCase), $"{block.name}: {a} / {b}");
                CollectionAssert.AllItemsAreUnique(words.Select(word => word.ToLowerInvariant()), block.name);
            }
        }

        public static void LinksJoinNeighbours(IEnumerable<Block> blocks)
        {
            foreach (var (from, link) in Links(blocks))
            {
                Assert.IsNotNull(link.target, from.name);
                var d = Delta(from, link.target);
                if (link.kind == LinkKind.Bridge)
                    Assert.AreEqual(1, Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y)), from.name);
                else
                    Assert.AreEqual(1, Mathf.Abs(d.x) + Mathf.Abs(d.y), from.name);
            }
        }

        public static void BridgedBlocksShareTheirAnswer(IEnumerable<Block> blocks)
        {
            foreach (var (from, link) in Links(blocks).Where(x => x.link.kind != LinkKind.Pointer))
            {
                Assert.IsNotEmpty(from.answer, from.name);
                Assert.AreEqual(from.answer.ToLowerInvariant(), link.target.answer.ToLowerInvariant(), from.name);
                if (link.kind == LinkKind.BridgePointer)
                    Assert.AreEqual((BlockColor.PureWhite, BlockShape.Plain), (link.target.color, link.target.shape));
            }
        }

        public static void CellKindsHaveTheirIncomingLinks(Block[] blocks)
        {
            var links = Links(blocks).ToList();
            IEnumerable<Block> PointedAt(Block target) => links
                .Where(x => x.link.target == target && x.link.kind != LinkKind.Bridge)
                .Select(x => x.from);
            foreach (var block in blocks)
            {
                bool hasClue = !string.IsNullOrEmpty(block.clue);
                if (!hasClue && !string.IsNullOrEmpty(block.answer))
                    Assert.IsTrue(links.Any(x => x.link.target == block || (x.from == block && x.link.kind == LinkKind.Bridge)), block.name);
                if (block.IsPure)
                    Assert.IsNotEmpty(PointedAt(block), block.name);
                if (block.color == BlockColor.Colorless)
                {
                    Assert.IsTrue(hasClue, block.name);
                    Assert.IsNotEmpty(PointedAt(block), block.name);
                    CollectionAssert.IsSubsetOf(new[] { block.answer }.Concat(block.hintAnswers), ColorNames, block.name);
                }
            }
        }

        public static void EverythingIsInsideTheRooms(Rooms rooms, Block[] blocks, Player player)
        {
            CollectionAssert.AllItemsAreUnique(blocks.Select(block => block.name));
            foreach (var block in blocks)
            {
                Vector2 c = block.transform.position;
                Assert.IsTrue(rooms.floors.Any(floor => c.x - Art.Half >= floor.xMin && c.x + Art.Half <= floor.xMax
                    && c.y - Art.Half >= floor.yMin && c.y + Art.Half <= floor.yMax), block.name);
            }
            foreach (var a in blocks)
                foreach (var b in blocks)
                    if (a != b)
                    {
                        // 方塊是正方形：只要某一軸的距離超過邊長就不重疊；看中心距離的話斜向錯開半格會漏掉
                        Vector2 d = a.transform.position - b.transform.position;
                        Assert.IsTrue(Mathf.Abs(d.x) > 2 * Art.Half || Mathf.Abs(d.y) > 2 * Art.Half, $"{a.name} / {b.name} 重疊");
                    }
            Vector2 spawn = player.transform.position;
            Assert.IsTrue(rooms.Contains(spawn));
            float clearance = Art.Half + Art.PlayerHalf;
            foreach (var block in blocks)
            {
                Vector2 c = block.transform.position;
                Assert.IsTrue(Mathf.Abs(spawn.x - c.x) > clearance || Mathf.Abs(spawn.y - c.y) > clearance, block.name);
            }
        }

        public static void BlocksAreVisibleWithoutPlaying(Board board)
        {
            foreach (var block in board.Blocks)
            {
                var mesh = block.GetComponent<MeshFilter>().sharedMesh;
                Assert.IsTrue(mesh != null && mesh.vertexCount > 0, block.name);
                Assert.AreEqual(block.clue, block.clueText.text, block.name);
                // 建場景時 TMP 的 Awake 若延後執行，會用預設值蓋掉這些設定，字會變成超大且一字一行
                Assert.AreEqual(TMPro.TextWrappingModes.NoWrap, block.clueText.textWrappingMode, block.name);
                Assert.AreEqual(Art.ClueFontMax, block.clueText.fontSizeMax, 1e-4f, block.name);
                Assert.AreEqual(Art.AnswerFontMax, block.answerText.fontSizeMax, 1e-4f, block.name);
                if (!string.IsNullOrEmpty(block.answer))
                    StringAssert.Contains($"({block.answer.Length})", block.answerText.text, block.name);
            }
        }
    }
}

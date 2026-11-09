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

        /// <summary>
        /// 鏈尾是純色格時，最後的結果回到鏈頭作答，和題目字面上無關；鏈頭若把中途的詞列為提示答案，
        /// 玩家打出中途的詞會跳「方向對了」，被引到錯的方向。
        /// </summary>
        public static void ChainsIntoSolidBlocksDoNotHintTheirMiddleWords(Block[] blocks)
        {
            var pointers = Links(blocks).Where(x => x.link.kind != LinkKind.Bridge).ToList();
            foreach (var end in blocks.Where(block => block.IsPure))
            {
                var chain = new HashSet<Block>();
                var frontier = new Queue<Block>(new[] { end });
                while (frontier.Count > 0)
                {
                    var current = frontier.Dequeue();
                    foreach (var from in pointers.Where(x => x.link.target == current).Select(x => x.from))
                        if (chain.Add(from)) frontier.Enqueue(from);
                }
                var heads = chain.Where(block => !string.IsNullOrEmpty(block.clue)).ToList();
                Assert.IsNotEmpty(heads, $"{end.name} 的鏈沒有起點");
                var middle = chain.Concat(blocks.Where(block => heads.Any(head => head.links.Any(link => link.target == block))))
                    .Where(block => !heads.Contains(block) && !string.IsNullOrEmpty(block.answer))
                    .Select(block => block.answer.ToLowerInvariant()).ToList();
                foreach (var head in heads)
                    Assert.IsFalse(head.hintAnswers.Any(hint => middle.Contains(hint.ToLowerInvariant())), $"{head.name} 把中途的詞列成提示答案");
            }
        }

        public static void EverythingIsInsideTheRooms(Rooms rooms, Block[] blocks, Player player)
        {
            CollectionAssert.AllItemsAreUnique(blocks.Select(block => block.name));
            foreach (var block in blocks)
            {
                // 房間不全是長方形，地板拆成好幾塊；方塊可以跨在同一間房相鄰的兩塊上
                var square = new Rect((Vector2)block.transform.position - Vector2.one * Art.Half, Vector2.one * 2 * Art.Half);
                Assert.IsFalse(Shapes.Subtract(square, rooms.floors).Any(), block.name);
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

        public static Rect Bounds(Block block) =>
            new Rect((Vector2)block.transform.position - Vector2.one * Art.Half, Vector2.one * (2 * Art.Half));

        /// <summary>門口兩側各一段通道裡沒有方塊，進出門口不必繞路。</summary>
        public static void DoorwaysAreClear(Rooms rooms, IEnumerable<Block> blocks)
        {
            const float depth = 2.5f, margin = 0.3f;
            var bounds = blocks.Select(block => (block.name, rect: Bounds(block))).Concat(rooms.obstacles.Select(rect => ("擺設", rect))).ToList();
            foreach (var d in rooms.doorways)
            {
                // 直的門口開在左右兩間之間，橫的門口開在上下兩間之間
                var sides = d.width < d.height
                    ? new[] { Rect.MinMaxRect(d.xMin - depth, d.yMin - margin, d.xMin, d.yMax + margin), Rect.MinMaxRect(d.xMax, d.yMin - margin, d.xMax + depth, d.yMax + margin) }
                    : new[] { Rect.MinMaxRect(d.xMin - margin, d.yMin - depth, d.xMax + margin, d.yMin), Rect.MinMaxRect(d.xMin - margin, d.yMax, d.xMax + margin, d.yMax + depth) };
                foreach (var side in sides)
                    foreach (var (name, rect) in bounds)
                        Assert.IsFalse(side.Overlaps(rect), $"{name} 擋在門口 {d} 前面");
            }
        }

        /// <summary>
        /// 從出生點以角色碰撞體的大小走（門都當作開著，以 0.25 的格點近似）：每個門口都走得到，
        /// 每格方塊附近都站得到——鏡頭跟著角色，站得到才看得到、點得到。
        /// </summary>
        public static void EverythingCanBeReachedOnFoot(Rooms rooms, Block[] blocks, Vector2 spawn)
        {
            const float step = 0.25f, near = 4f;
            var areas = rooms.floors.Concat(rooms.doorways).ToArray();
            var origin = new Vector2(areas.Min(a => a.xMin), areas.Min(a => a.yMin));
            int width = Mathf.CeilToInt((areas.Max(a => a.xMax) - origin.x) / step) + 1;
            int height = Mathf.CeilToInt((areas.Max(a => a.yMax) - origin.y) / step) + 1;
            Vector2 At(int x, int y) => origin + new Vector2(x, y) * step;

            var inside = new bool[width, height];
            foreach (var area in areas)
                for (int x = Mathf.Max(0, Mathf.FloorToInt((area.xMin - origin.x) / step)); x < width && At(x, 0).x <= area.xMax; x++)
                    for (int y = Mathf.Max(0, Mathf.FloorToInt((area.yMin - origin.y) / step)); y < height && At(0, y).y <= area.yMax; y++)
                        if (area.Contains(At(x, y))) inside[x, y] = true;
            var blocked = new bool[width, height];
            // 方塊與擋路的擺設都往外擴一個角色半徑，角色中心不能進到裡面
            var solids = blocks.Select(Bounds).Concat(rooms.obstacles);
            foreach (var solid in solids)
            {
                var grown = new Rect(solid.position - Vector2.one * Art.PlayerHalf, solid.size + Vector2.one * 2 * Art.PlayerHalf);
                for (int x = Mathf.Max(0, Mathf.FloorToInt((grown.xMin - origin.x) / step)); x < width && At(x, 0).x <= grown.xMax; x++)
                    for (int y = Mathf.Max(0, Mathf.FloorToInt((grown.yMin - origin.y) / step)); y < height && At(0, y).y <= grown.yMax; y++)
                        if (grown.xMin < At(x, y).x && At(x, y).x < grown.xMax && grown.yMin < At(x, y).y && At(x, y).y < grown.yMax) blocked[x, y] = true;
            }
            // 角色的四個角都要在地板或門口裡
            int k = Mathf.CeilToInt(Art.PlayerHalf / step);
            bool Free(int x, int y) =>
                x - k >= 0 && y - k >= 0 && x + k < width && y + k < height && !blocked[x, y]
                && inside[x - k, y - k] && inside[x + k, y - k] && inside[x - k, y + k] && inside[x + k, y + k];

            var reached = new bool[width, height];
            var queue = new Queue<Vector2Int>();
            var start = Vector2Int.RoundToInt((spawn - origin) / step);
            Assert.IsTrue(Free(start.x, start.y), "出生點站不住");
            reached[start.x, start.y] = true;
            queue.Enqueue(start);
            var neighbours = new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down };
            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();
                foreach (var n in neighbours.Select(offset => cell + offset))
                {
                    if (!Free(n.x, n.y) || reached[n.x, n.y]) continue;
                    reached[n.x, n.y] = true;
                    queue.Enqueue(n);
                }
            }

            foreach (var doorway in rooms.doorways)
            {
                var cell = Vector2Int.RoundToInt((doorway.center - origin) / step);
                Assert.IsTrue(reached[cell.x, cell.y], $"走不到門口 {doorway}");
            }
            int radius = Mathf.CeilToInt(near / step);
            foreach (var block in blocks)
            {
                var cell = Vector2Int.RoundToInt(((Vector2)block.transform.position - origin) / step);
                bool found = false;
                for (int x = Mathf.Max(0, cell.x - radius); x <= Mathf.Min(width - 1, cell.x + radius) && !found; x++)
                    for (int y = Mathf.Max(0, cell.y - radius); y <= Mathf.Min(height - 1, cell.y + radius) && !found; y++)
                        found = reached[x, y] && (At(x, y) - (Vector2)block.transform.position).magnitude <= near;
                Assert.IsTrue(found, $"{block.name} 附近走不到");
            }
        }
    }
}

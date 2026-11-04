using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Lexiscript
{
    public sealed class DemoSceneTests
    {
        Board board;
        Block[] blocks;

        [OneTimeSetUp]
        public void OpenDemo()
        {
            EditorSceneManager.OpenScene("Assets/Lexiscript/Demo.unity");
            board = Object.FindAnyObjectByType<Board>();
            blocks = board.Blocks;
        }

        Block Find(string id) => blocks.Single(block => block.name == id);

        static Vector2Int Delta(Block from, Block to)
        {
            Vector2 delta = to.transform.position - from.transform.position;
            var cells = Vector2Int.RoundToInt(delta / Art.Pitch);
            Assert.Less((delta - (Vector2)cells * Art.Pitch).magnitude, 1e-3f, $"{from.name}->{to.name} 不在格點上");
            return cells;
        }

        IEnumerable<(Block from, Block.Link link)> Links() =>
            blocks.SelectMany(block => block.links.Select(link => (block, link)));

        IEnumerable<Block> PointedAt(Block target) => Links()
            .Where(x => x.link.target == target && x.link.kind != LinkKind.Bridge)
            .Select(x => x.from);

        static string[] ColorNames => Enum.GetValues(typeof(BlockColor)).Cast<BlockColor>()
            .Where(c => c != BlockColor.Colorless)
            .Select(c => c.ToString().ToUpperInvariant())
            .ToArray();

        [Test]
        public void HasTwentyNineBlocks()
        {
            Assert.AreEqual(29, blocks.Length);
        }

        [Test]
        public void EveryDefinedColorAndShapeAppears()
        {
            var expected = Enum.GetValues(typeof(BlockColor)).Cast<BlockColor>()
                .SelectMany(c => new[] { (c, BlockShape.Plain), (c, BlockShape.Beveled) })
                .Where(pair => pair != (BlockColor.SunsetOrange, BlockShape.Beveled));
            var actual = blocks.Where(block => !block.IsPure).Select(block => (block.color, block.shape)).Distinct();
            CollectionAssert.AreEquivalent(expected, actual);
        }

        [Test]
        public void NamesAreUniqueAndEverythingIsInsideTheRoom()
        {
            CollectionAssert.AllItemsAreUnique(blocks.Select(block => block.name));
            var floor = Art.Floor;
            foreach (var block in blocks)
            {
                Vector2 c = block.transform.position;
                Assert.IsTrue(c.x - Art.Half >= floor.xMin && c.x + Art.Half <= floor.xMax
                    && c.y - Art.Half >= floor.yMin && c.y + Art.Half <= floor.yMax, block.name);
            }
            Vector2 player = Object.FindAnyObjectByType<Player>().transform.position;
            Assert.IsTrue(floor.Contains(player));
            float clearance = Art.Half + Art.PlayerRadius;
            foreach (var block in blocks)
            {
                Vector2 c = block.transform.position;
                Assert.IsTrue(Mathf.Abs(player.x - c.x) > clearance || Mathf.Abs(player.y - c.y) > clearance, block.name);
            }
        }

        [Test]
        public void AnswersAreUppercaseLettersWithoutPrefixClashes()
        {
            var format = new Regex("^[A-Z]{1," + Puzzle.MaxInputLength + "}$");
            foreach (var block in blocks)
            {
                var words = block.answers.Concat(block.hintAnswers).ToArray();
                foreach (var word in words) Assert.IsTrue(format.IsMatch(word), $"{block.name}: {word}");
                CollectionAssert.AllItemsAreUnique(words, block.name);
                foreach (var a in words)
                    foreach (var b in words)
                        Assert.IsFalse(a != b && b.StartsWith(a, StringComparison.Ordinal), $"{block.name}: {a} / {b}");
            }
        }

        [Test]
        public void LinksJoinNeighbours()
        {
            foreach (var (from, link) in Links())
            {
                Assert.IsNotNull(link.target, from.name);
                var d = Delta(from, link.target);
                if (link.kind == LinkKind.Bridge)
                    Assert.AreEqual(1, Mathf.Max(Mathf.Abs(d.x), Mathf.Abs(d.y)), from.name);
                else
                    Assert.AreEqual(1, Mathf.Abs(d.x) + Mathf.Abs(d.y), from.name);
            }
        }

        [Test]
        public void BridgedBlocksShareTheirSingleAnswer()
        {
            foreach (var (from, link) in Links().Where(x => x.link.kind != LinkKind.Pointer))
            {
                Assert.AreEqual(1, from.answers.Length, from.name);
                CollectionAssert.AreEqual(from.answers, link.target.answers, from.name);
                if (link.kind == LinkKind.BridgePointer)
                    Assert.AreEqual((BlockColor.PureWhite, BlockShape.Plain), (link.target.color, link.target.shape));
            }
        }

        [Test]
        public void CellKindsHaveTheirIncomingLinks()
        {
            foreach (var block in blocks)
            {
                bool hasClue = !string.IsNullOrEmpty(block.clue);
                if (!hasClue && block.answers.Length > 0)
                    Assert.IsTrue(Links().Any(x => x.link.target == block || (x.from == block && x.link.kind == LinkKind.Bridge)), block.name);
                if (block.IsPure)
                    Assert.IsNotEmpty(PointedAt(block), block.name);
                if (block.color == BlockColor.Colorless)
                {
                    Assert.IsTrue(hasClue, block.name);
                    Assert.IsNotEmpty(PointedAt(block), block.name);
                    CollectionAssert.IsSubsetOf(block.answers.Concat(block.hintAnswers), ColorNames, block.name);
                }
            }
        }

        [Test]
        public void SpecExamplesAreBuiltAsWritten()
        {
            var p = Find("P");
            Assert.AreEqual((BlockColor.PureWhite, BlockShape.Beveled), (p.color, p.shape));
            Assert.AreEqual("PERPETUAL", p.clue);
            CollectionAssert.AreEqual(new[] { "BYZANTIUM" }, p.answers);
            CollectionAssert.Contains(p.hintAnswers, "CONSTANT");

            foreach (var id in new[] { "L", "Y" })
                Assert.IsTrue(string.IsNullOrEmpty(Find(id).clue) && Find(id).answers.Length > 0, id);

            var g = Find("G");
            Assert.AreEqual((BlockColor.GoldenBrown, BlockShape.Plain), (g.color, g.shape));
            Assert.IsTrue(g.IsPure);
            Assert.IsEmpty(g.links);

            Assert.AreEqual(new Vector2Int(-1, 0), Delta(p, Find("L")));
            Assert.AreEqual(new Vector2Int(1, 0), Delta(p, Find("Y")));
            Assert.AreEqual(new Vector2Int(1, 0), Delta(Find("Y"), g));
            CollectionAssert.AreEquivalent(new[] { "L", "Y" }, p.links.Select(link => link.target.name));
            CollectionAssert.AreEqual(new[] { "G" }, Find("Y").links.Select(link => link.target.name));
            Assert.IsTrue(p.links.Concat(Find("Y").links).All(link => link.kind == LinkKind.Pointer));

            CollectionAssert.Contains(Find("DESSERTS").hintAnswers, "BRIGHTYELLOW");
        }

        [Test]
        public void CodexHasEveryDefinedRule()
        {
            foreach (BlockColor color in Enum.GetValues(typeof(BlockColor)))
                foreach (BlockShape shape in Enum.GetValues(typeof(BlockShape)))
                {
                    var rule = ColorCodex.Rule(color, shape);
                    if (color == BlockColor.SunsetOrange && shape == BlockShape.Beveled) Assert.IsNull(rule);
                    else Assert.IsNotEmpty(rule, $"{color} {shape}");
                }
            // 用碼位比對，原始碼存檔編碼若出錯會在這裡抓到
            Assert.AreEqual("\u76F8\u540C", ColorCodex.Rule(BlockColor.PureWhite, BlockShape.Plain));
        }

        [Test]
        public void HintPhrasesAreEightAsciiLines()
        {
            Assert.AreEqual(8, Board.HintPhrases.Length);
            CollectionAssert.AllItemsAreUnique(Board.HintPhrases);
            foreach (var phrase in Board.HintPhrases)
                Assert.IsTrue(phrase.All(c => c >= ' ' && c <= '~'), phrase);
        }

        [Test]
        public void BlocksAreVisibleWithoutPlaying()
        {
            foreach (var block in blocks)
            {
                var mesh = block.GetComponent<MeshFilter>().sharedMesh;
                Assert.IsTrue(mesh != null && mesh.vertexCount > 0, block.name);
                Assert.AreEqual(block.clue, block.clueText.text, block.name);
            }
            Assert.Greater(board.bridges.sharedMesh.vertexCount, 0);
        }
    }
}

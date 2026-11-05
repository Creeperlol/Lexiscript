using System;
using System.Linq;
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
        public void EverythingIsInsideTheRoom()
        {
            LevelChecks.EverythingIsInsideTheRooms(Object.FindAnyObjectByType<Rooms>(), blocks, Object.FindAnyObjectByType<Player>());
        }

        [Test]
        public void WordsAreLettersInCanonicalCaseWithoutPrefixClashes()
        {
            LevelChecks.WordsAreWellFormed(blocks);
        }

        [Test]
        public void LinksJoinNeighbours()
        {
            LevelChecks.LinksJoinNeighbours(blocks);
        }

        [Test]
        public void BridgedBlocksShareTheirAnswer()
        {
            LevelChecks.BridgedBlocksShareTheirAnswer(blocks);
        }

        [Test]
        public void CellKindsHaveTheirIncomingLinks()
        {
            LevelChecks.CellKindsHaveTheirIncomingLinks(blocks);
        }

        [Test]
        public void SpecExamplesAreBuiltAsWritten()
        {
            var p = Find("P");
            Assert.AreEqual((BlockColor.PureWhite, BlockShape.Beveled), (p.color, p.shape));
            Assert.AreEqual("perpetual", p.clue);
            Assert.AreEqual("Byzantium", p.answer);
            CollectionAssert.Contains(p.hintAnswers, "constant");

            foreach (var id in new[] { "L", "Y" })
                Assert.IsTrue(string.IsNullOrEmpty(Find(id).clue) && Find(id).answer.Length > 0, id);

            var g = Find("G");
            Assert.AreEqual((BlockColor.GoldenBrown, BlockShape.Plain), (g.color, g.shape));
            Assert.IsTrue(g.IsPure);
            Assert.IsEmpty(g.links);

            Assert.AreEqual(new Vector2Int(-1, 0), LevelChecks.Delta(p, Find("L")));
            Assert.AreEqual(new Vector2Int(1, 0), LevelChecks.Delta(p, Find("Y")));
            Assert.AreEqual(new Vector2Int(1, 0), LevelChecks.Delta(Find("Y"), g));
            CollectionAssert.AreEquivalent(new[] { "L", "Y" }, p.links.Select(link => link.target.name));
            CollectionAssert.AreEqual(new[] { "G" }, Find("Y").links.Select(link => link.target.name));
            Assert.IsTrue(p.links.Concat(Find("Y").links).All(link => link.kind == LinkKind.Pointer));

            CollectionAssert.Contains(Find("DESSERTS").hintAnswers, "BrightYellow");
            Assert.AreEqual("Monday", Find("MONDAY").clue);
            Assert.AreEqual("Tuesday", Find("MONDAY").answer);
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
            Assert.AreEqual("相同", ColorCodex.Rule(BlockColor.PureWhite, BlockShape.Plain));
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
            LevelChecks.BlocksAreVisibleWithoutPlaying(board);
            Assert.Greater(board.bridges.sharedMesh.vertexCount, 0);
        }

        [Test]
        public void SceneHasTheHudAndTheZoomableCamera()
        {
            Assert.IsNotNull(Object.FindAnyObjectByType<Hud>());
            Assert.IsNotNull(Object.FindAnyObjectByType<TextInputHub>());
            Assert.IsNotNull(Camera.main.GetComponent<CameraZoom>());
        }
    }
}

using NUnit.Framework;

namespace Lexiscript
{
    public sealed class PuzzleTests
    {
        Puzzle puzzle;

        [SetUp]
        public void SetUp()
        {
            puzzle = new Puzzle(new[]
            {
                new BlockDef("A", new[] { "CAT", "LION" }, new[] { "DOG" }),
                new BlockDef("Pure", new string[0], new string[0]),
            });
        }

        void Type(string id, string text)
        {
            foreach (var c in text) puzzle.Type(id, c);
        }

        [Test]
        public void WrongInputIsKeptWithoutReaction()
        {
            Type("A", "cow");
            Assert.IsFalse(puzzle.IsSolved("A"));
            Assert.AreEqual("COW", puzzle.Input("A"));
        }

        [Test]
        public void SolvesOnTheLastLetterRegardlessOfCase()
        {
            Assert.IsFalse(puzzle.Type("A", 'c'));
            Assert.IsFalse(puzzle.Type("A", 'A'));
            Assert.IsFalse(puzzle.IsSolved("A"));
            Assert.IsTrue(puzzle.Type("A", 't'));
            Assert.IsTrue(puzzle.IsSolved("A"));
            Assert.AreEqual("CAT", puzzle.Input("A"));
        }

        [Test]
        public void AnyAcceptedAnswerSolves()
        {
            Type("A", "Lion");
            Assert.IsTrue(puzzle.IsSolved("A"));
        }

        [Test]
        public void IgnoresEverythingButLetters()
        {
            Type("A", "c");
            foreach (var c in new[] { '1', ' ', '-', 'é', 'ı', 'Ａ', 'ａ', '貓', '\b', '\r' })
                Assert.IsFalse(puzzle.Type("A", c), $"U+{(int)c:X4}");
            Assert.AreEqual("C", puzzle.Input("A"));
        }

        [Test]
        public void BackspaceRemovesTheLastLetter()
        {
            puzzle.Backspace("A");
            Assert.AreEqual("", puzzle.Input("A"));
            Type("A", "co");
            puzzle.Backspace("A");
            Assert.AreEqual("C", puzzle.Input("A"));
        }

        [Test]
        public void SolvedBlockIsLocked()
        {
            Type("A", "cat");
            Assert.IsFalse(puzzle.Type("A", 's'));
            puzzle.Backspace("A");
            Assert.AreEqual("CAT", puzzle.Input("A"));
            Assert.IsFalse(puzzle.IsAnswerable("A"));
        }

        [Test]
        public void InputIsCappedAt16Letters()
        {
            Type("A", new string('x', 20));
            Assert.AreEqual(Puzzle.MaxInputLength, puzzle.Input("A").Length);
        }

        [Test]
        public void HintOnlyWhileInputEqualsAHintAnswer()
        {
            Type("A", "do");
            Assert.IsFalse(puzzle.IsHint("A"));
            Assert.IsFalse(puzzle.Type("A", 'g'));
            Assert.IsTrue(puzzle.IsHint("A"));
            Assert.IsFalse(puzzle.IsSolved("A"));
            puzzle.Type("A", 's');
            Assert.IsFalse(puzzle.IsHint("A"));
            puzzle.Backspace("A");
            Assert.IsTrue(puzzle.IsHint("A"));
            puzzle.Backspace("A");
            Assert.IsFalse(puzzle.IsHint("A"));
        }

        [Test]
        public void PureColorBlockCannotBeAnswered()
        {
            Assert.IsFalse(puzzle.IsAnswerable("Pure"));
            Assert.IsFalse(puzzle.Type("Pure", 'a'));
            Assert.AreEqual("", puzzle.Input("Pure"));
        }
    }
}

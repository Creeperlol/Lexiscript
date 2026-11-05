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
                new BlockDef("A", "Cat", new[] { "dog" }),
                new BlockDef("Pure", "", new string[0]),
            });
        }

        [Test]
        public void WrongInputKeepsThePlayersCase()
        {
            Assert.IsFalse(puzzle.SetInput("A", "cOw"));
            Assert.IsFalse(puzzle.IsSolved("A"));
            Assert.AreEqual("cOw", puzzle.Input("A"));
        }

        [Test]
        public void SolvesRegardlessOfCaseAndSwitchesToTheAnswersCase()
        {
            Assert.IsFalse(puzzle.SetInput("A", "cA"));
            Assert.IsTrue(puzzle.SetInput("A", "cAT"));
            Assert.IsTrue(puzzle.IsSolved("A"));
            Assert.AreEqual("Cat", puzzle.Input("A"));
        }

        [Test]
        public void IgnoresEverythingButAsciiLetters()
        {
            Assert.IsFalse(puzzle.SetInput("A", "c1 -éıＡａ貓\b\r"));
            Assert.AreEqual("c", puzzle.Input("A"));
        }

        [Test]
        public void InputIsCappedAt16Letters()
        {
            puzzle.SetInput("A", new string('x', 20));
            Assert.AreEqual(Puzzle.MaxInputLength, puzzle.Input("A").Length);
        }

        [Test]
        public void ClearOnlyAffectsUnsolvedBlocks()
        {
            puzzle.SetInput("A", "co");
            puzzle.Clear("A");
            Assert.AreEqual("", puzzle.Input("A"));
            puzzle.SetInput("A", "cat");
            puzzle.Clear("A");
            Assert.AreEqual("Cat", puzzle.Input("A"));
        }

        [Test]
        public void SolvedBlockIsLocked()
        {
            puzzle.SetInput("A", "cat");
            Assert.IsFalse(puzzle.SetInput("A", "dog"));
            Assert.AreEqual("Cat", puzzle.Input("A"));
            Assert.IsFalse(puzzle.IsAnswerable("A"));
            Assert.AreEqual("Cat", puzzle.Answer("A"));
        }

        [Test]
        public void HintOnlyWhileInputEqualsAHintAnswer()
        {
            puzzle.SetInput("A", "do");
            Assert.IsFalse(puzzle.IsHint("A"));
            puzzle.SetInput("A", "DOG");
            Assert.IsTrue(puzzle.IsHint("A"));
            Assert.IsFalse(puzzle.IsSolved("A"));
            puzzle.SetInput("A", "dogs");
            Assert.IsFalse(puzzle.IsHint("A"));
        }

        [Test]
        public void PureColorBlockCannotBeAnswered()
        {
            Assert.IsFalse(puzzle.IsAnswerable("Pure"));
            Assert.IsFalse(puzzle.SetInput("Pure", "a"));
            Assert.AreEqual("", puzzle.Input("Pure"));
        }
    }

    public sealed class AnswerHintTests
    {
        [Test]
        public void OneBlankPerLetterFollowedByTheCount()
        {
            Assert.AreEqual("_____(5)", AnswerHint.Placeholder("apple", Reveal.None));
        }

        [Test]
        public void RevealsTheFirstOrTheLastLetter()
        {
            Assert.AreEqual("a____(5)", AnswerHint.Placeholder("apple", Reveal.First));
            Assert.AreEqual("____e(5)", AnswerHint.Placeholder("apple", Reveal.Last));
            Assert.AreEqual("B________(9)", AnswerHint.Placeholder("Byzantium", Reveal.First));
            Assert.AreEqual("a(1)", AnswerHint.Placeholder("a", Reveal.Last));
        }

        [Test]
        public void ClicksToggleAndNeverShowBothEnds()
        {
            Assert.AreEqual(Reveal.First, AnswerHint.Toggle(Reveal.None, Reveal.First));
            Assert.AreEqual(Reveal.None, AnswerHint.Toggle(Reveal.First, Reveal.First));
            Assert.AreEqual(Reveal.Last, AnswerHint.Toggle(Reveal.First, Reveal.Last));
            Assert.AreEqual(Reveal.First, AnswerHint.Toggle(Reveal.Last, Reveal.First));
            Assert.AreEqual(Reveal.None, AnswerHint.Toggle(Reveal.Last, Reveal.Last));
        }
    }
}

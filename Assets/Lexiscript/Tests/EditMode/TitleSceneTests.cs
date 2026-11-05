using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Lexiscript
{
    /// <summary>標題場景（TitleSetup 產生）的內容。</summary>
    public sealed class TitleSceneTests
    {
        const string ScenePath = "Assets/Lexiscript/Title.unity";

        Board board;

        [OneTimeSetUp]
        public void OpenTitle()
        {
            EditorSceneManager.OpenScene(ScenePath);
            board = Object.FindAnyObjectByType<Board>();
        }

        [Test]
        public void IsTheFirstSceneOfTheBuild()
        {
            var scenes = EditorBuildSettings.scenes;
            Assert.AreEqual(ScenePath, scenes[0].path);
            Assert.IsTrue(scenes[0].enabled);
            CollectionAssert.Contains(scenes.Select(scene => scene.path), "Assets/Lexiscript/Game.unity");
        }

        [Test]
        public void HasASingleWhiteSquareStartBlock()
        {
            var block = board.Blocks.Single();
            Assert.AreEqual((BlockColor.PureWhite, BlockShape.Plain), (block.color, block.shape));
            Assert.AreEqual(TitleScreen.Word, block.clue);
            Assert.AreEqual(TitleScreen.Word, block.answer, "白色方角：寫出一樣的字");
            Assert.AreEqual("type \"start\" to start!!!", TitleScreen.Instruction, "使用者指定的說明文字");
            StringAssert.Contains($"\"{block.answer.ToLowerInvariant()}\"", TitleScreen.Instruction, "說明叫玩家打的就是答案");
            Assert.IsEmpty(block.hintAnswers);
            Assert.IsEmpty(block.links);
            Assert.Less(((Vector2)block.transform.position).magnitude, 1e-4f, "鏡頭位置是照方塊在原點算的");
        }

        [Test]
        public void BlockIsVisibleWithoutPlaying()
        {
            LevelChecks.BlocksAreVisibleWithoutPlaying(board);
        }

        [Test]
        public void HasTheTitleScreenAndAFixedCamera()
        {
            Assert.IsNotNull(Object.FindAnyObjectByType<TitleScreen>());
            Assert.IsNotNull(Object.FindAnyObjectByType<TextInputHub>(), "鍵盤要派送到答案欄");
            Assert.IsNull(Object.FindAnyObjectByType<Hud>(), "標題沒有筆記與聊天");
            Assert.IsNull(Object.FindAnyObjectByType<Player>());
            var camera = Camera.main;
            Assert.IsNull(camera.GetComponent<CameraZoom>(), "鏡頭固定，不能用滾輪縮放");
            Assert.AreEqual(TitleScreen.CameraSize, camera.orthographicSize, 1e-4f);
            Assert.Less(((Vector2)camera.transform.position - TitleScreen.CameraPosition).magnitude, 1e-3f);
            Assert.Less(((Vector4)(camera.backgroundColor - TitleScreen.Background)).magnitude, 1e-3f);
        }
    }

    /// <summary>標題轉場的斜切色塊幾何。</summary>
    public sealed class TitleTransitionTests
    {
        static readonly Vector2[] Sizes = { new Vector2(1920, 1080), new Vector2(1920, 1440), new Vector2(2520, 1080) };

        static IEnumerable<Vector2> Probes(Vector2 size)
        {
            var half = size / 2 - Vector2.one;
            yield return Vector2.zero;
            yield return new Vector2(-half.x, -half.y);
            yield return new Vector2(half.x, -half.y);
            yield return new Vector2(half.x, half.y);
            yield return new Vector2(-half.x, half.y);
        }

        static bool Covered(float cover, float reveal, Vector2 size, Vector2 point) =>
            TitleTransition.Slabs(cover, reveal, size).Any(slab => Shapes.Contains(slab, point));

        [Test]
        public void SlabsStartOffScreen()
        {
            foreach (var size in Sizes)
                foreach (var point in Probes(size))
                    Assert.IsFalse(Covered(0, 0, size, point), $"{size} {point}");
        }

        [Test]
        public void TopSlabCoversTheWholeScreenWhileTheSceneLoads()
        {
            foreach (var size in Sizes)
            {
                var top = TitleTransition.Slabs(TitleTransition.SweepTotal, 0, size).Last();
                foreach (var point in Probes(size)) Assert.IsTrue(Shapes.Contains(top, point), $"{size} {point}");
            }
        }

        [Test]
        public void SlabsLeaveTheScreenAfterTheReveal()
        {
            foreach (var size in Sizes)
                foreach (var point in Probes(size))
                    Assert.IsFalse(Covered(TitleTransition.SweepTotal, TitleTransition.SweepTotal, size, point), $"{size} {point}");
        }

        [Test]
        public void SlabsSweepInLayers()
        {
            var size = Sizes[0];
            // 蓋到一半時，先出發的淡金在最右邊、墨色殿後；掃出時墨色先走
            var covering = TitleTransition.Slabs(TitleTransition.SweepSeconds / 2 + TitleTransition.Stagger, 0, size);
            Assert.Greater(covering[0][1].x, covering[1][1].x);
            Assert.Greater(covering[1][1].x, covering[2][1].x);
            var revealing = TitleTransition.Slabs(TitleTransition.SweepTotal, TitleTransition.SweepSeconds / 2 + TitleTransition.Stagger, size);
            Assert.Greater(revealing[2][0].x, revealing[1][0].x);
            Assert.Greater(revealing[1][0].x, revealing[0][0].x);
        }
    }
}

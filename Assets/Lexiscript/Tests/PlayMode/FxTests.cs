using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lexiscript
{
    public sealed class FxTests
    {
        readonly InputHarness input = new InputHarness();

        [SetUp]
        public void SetUp() => input.SetUp();

        [UnityTearDown]
        public IEnumerator TearDown() => input.TearDown();

        static float Area(List<Vector2> polygon)
        {
            float twice = 0;
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Count];
                twice += a.x * b.y - b.x * a.y;
            }
            return twice / 2;
        }

        /// <summary>指著方塊上緣那段色框（寬 0.24）的正中間，等到標籤換成它的顏色那一幀。</summary>
        IEnumerator Hover(Block block)
        {
            input.Point(InputHarness.Screen((Vector2)block.transform.position + new Vector2(0, 0.88f)));
            var label = Hud.Instance.ColorLabel;
            for (int i = 0; i < 5 && label.ColorName != block.color.ToString(); i++) yield return null;
        }

        [UnityTest]
        public IEnumerator ColorLabelIsLargerAndFlankedByBarsInTheBlockColor()
        {
            SceneManager.LoadScene("Demo");
            yield return null;
            Hud.Instance.Notes.SetMinimized(true);
            var board = Object.FindAnyObjectByType<Board>();
            var label = Hud.Instance.ColorLabel;
            var rect = (RectTransform)label.transform;
            var text = label.GetComponentInChildren<TextMeshProUGUI>();
            Block Find(string id) => board.Blocks.Single(block => block.name == id);

            // 拉到最遠，Demo 的每一格都在畫面裡才指得到
            input.Point(InputHarness.Screen((Vector2)Camera.main.transform.position));
            for (int i = 0; i < 3; i++) yield return input.Scroll(-3);
            yield return new WaitForSeconds(0.6f);

            var p = Find("P");
            yield return Hover(p);
            Assert.AreEqual("PureWhite", label.ColorName);
            Assert.Less(label.LeftBars.color.a, 1, "直條剛開始滑入");
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(1, label.LeftBars.color.a, 1e-4f, "滑入很快就結束");
            Assert.AreEqual(Art.Swatch(BlockColor.PureWhite), label.BarColor, "直條是方塊的顏色");
            Assert.IsFalse(label.BarsOnPlate);
            Assert.AreEqual(3, label.LeftBars.Count, "粗、粗、細三條");
            Assert.AreEqual(3, label.RightBars.Count);

            Canvas.ForceUpdateCanvases();
            Assert.Greater(rect.rect.height, 56, "標籤比原本的高");
            Assert.Greater(text.fontSize, 32, "字比原本的大");
            float middle = InputHarness.Screen(rect).x;
            Assert.AreEqual(middle - InputHarness.Screen(label.LeftBars.rectTransform).x,
                InputHarness.Screen(label.RightBars.rectTransform).x - middle, 0.5f, "兩組直條左右對稱");
            text.ForceMeshUpdate();
            var corners = new Vector3[4];
            label.LeftBars.rectTransform.GetWorldCorners(corners);
            Assert.Less(corners[2].x, text.rectTransform.TransformPoint(text.textBounds.min).x, "左邊的直條不壓到字");
            label.RightBars.rectTransform.GetWorldCorners(corners);
            Assert.Greater(corners[0].x, text.rectTransform.TransformPoint(text.textBounds.max).x, "右邊的直條不壓到字");
            var labelCorners = new Vector3[4];
            rect.GetWorldCorners(labelCorners);
            ((RectTransform)Hud.Instance.Hint.transform).GetWorldCorners(corners);
            Assert.LessOrEqual(corners[1].y, labelCorners[0].y, "提示句往下挪，不會被變大的標籤蓋住");
            InputHarness.Shot("31_fx_label_purewhite", p.transform.position, 3.4f, hud: true);

            var drawer = Find("DRAWER");
            yield return Hover(drawer);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual("PureBlack", label.ColorName);
            Assert.AreEqual(Art.Swatch(BlockColor.PureBlack), label.BarColor);
            Assert.IsTrue(label.BarsOnPlate, "純黑的直條墊淺色底板，深色標籤上才看得見");
            Assert.AreEqual(4, label.LeftBars.Count, "底板加三條直條");
            InputHarness.Shot("32_fx_label_pureblack", drawer.transform.position, 3.4f, hud: true);

            var desserts = Find("DESSERTS");
            yield return Hover(desserts);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual("Colorless", label.ColorName);
            Assert.AreEqual(Art.Hatch, label.BarColor, "無色用無色方塊的斜紋灰");
            Assert.Greater(label.LeftBars.Count, 3, "無色的直條是一段段斜紋");
            InputHarness.Shot("33_fx_label_colorless", desserts.transform.position, 3.4f, hud: true);

            var testy = Find("TESTY");
            yield return Hover(testy);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual("RoyalBlue", label.ColorName);
            Assert.AreEqual(Art.Swatch(BlockColor.RoyalBlue), label.BarColor);
            InputHarness.Shot("34_fx_label_royalblue", testy.transform.position, 3.4f, hud: true);
        }

        [UnityTest]
        public IEnumerator SolvingSweepsGoldAcrossTheAnswerBoxAndKeepsIt()
        {
            Assert.That(SolveEffect.Seconds, Is.InRange(0.6f, 0.8f), "動畫不能拖太久");
            SceneManager.LoadScene("Demo");
            yield return null;
            Hud.Instance.Notes.SetMinimized(true);
            var board = Object.FindAnyObjectByType<Board>();
            var effect = board.GetComponent<SolveEffect>();
            Assert.IsNotNull(effect, "盤面在 Play 時自動加上解題特效");
            var fill = effect.transform.Find("SolveFill").GetComponent<MeshFilter>().sharedMesh;
            var burst = effect.transform.Find("SolveBurst").GetComponent<MeshFilter>().sharedMesh;
            Assert.AreEqual(Art.SelectionOrder, effect.transform.Find("SolveFill").GetComponent<MeshRenderer>().sortingOrder, "金底在方塊之上、文字之下");
            Assert.AreEqual(Art.EffectOrder, effect.transform.Find("SolveBurst").GetComponent<MeshRenderer>().sortingOrder);

            var p = board.Blocks.Single(block => block.name == "P");
            var box = Art.AnswerBox(p.shape);
            float full = Area(box);
            Assert.Less(full, Area(Art.AnswerBox(BlockShape.Plain)), "P 是斜角方塊，答案欄下緣有切角");
            Assert.IsFalse(effect.IsGilded(p));

            yield return input.Click(p);
            input.Type("byzantium");
            float start = Time.time;
            Assert.IsTrue(board.Puzzle.IsSolved("P"));
            Assert.IsTrue(effect.IsPlaying(p), "答對就開始播");
            Assert.AreEqual(0, Area(effect.FillOf(p)), 1e-6f, "金色從零開始刷");

            float previous = 0;
            bool popped = false, shards = false, sweepShot = false, burstShot = false;
            while (effect.IsPlaying(p))
            {
                yield return null;
                float area = Area(effect.FillOf(p));
                Assert.GreaterOrEqual(area, previous - 1e-6f, "金色只會越刷越多");
                previous = area;
                popped |= Mathf.Abs(p.answerText.transform.localScale.x - 1) > 0.05f;
                shards |= burst.vertexCount > 0;
                Assert.AreEqual(Vector3.one, p.transform.localScale, "方塊本身不縮放，碰撞體才不會變大");
                float t = Time.time - start;
                Assert.Less(t, 2f, "動畫要會結束");
                if (!sweepShot && t >= 0.05f)
                {
                    sweepShot = true;
                    InputHarness.Shot("35_fx_solve_sweep", p.transform.position, 1.5f);
                }
                else if (!burstShot && t >= 0.22f)
                {
                    burstShot = true;
                    InputHarness.Shot("36_fx_solve_burst", p.transform.position, 1.5f);
                }
            }
            Assert.IsTrue(popped, "綠字彈了一下");
            Assert.IsTrue(shards, "有碎片從答案欄爆開");
            Assert.IsTrue(effect.IsGilded(p), "播完之後維持金底");
            Assert.AreEqual(full, Area(effect.FillOf(p)), 1e-5f, "最後蓋滿整個答案欄，切角也貼合");
            Assert.AreEqual(Vector3.one, p.answerText.transform.localScale, "綠字回到原本大小");
            Assert.AreEqual(0, burst.vertexCount, "碎片都消失了");
            Vector2 at = p.transform.localPosition;
            var bounds = fill.bounds;
            Assert.AreEqual(at.x + box.Min(point => point.x), bounds.min.x, 1e-4f, "金底畫在這格的答案欄上");
            Assert.AreEqual(at.x + box.Max(point => point.x), bounds.max.x, 1e-4f);
            Assert.AreEqual(at.y + box.Min(point => point.y), bounds.min.y, 1e-4f);
            Assert.AreEqual(at.y + box.Max(point => point.y), bounds.max.y, 1e-4f);
            InputHarness.Shot("37_fx_solve_end", p.transform.position, 1.5f);

            // 讀檔還原只改 Puzzle、不經過 Board.Solved
            var moon = board.Blocks.Single(block => block.name == "MOON");
            board.Puzzle.SetInput("MOON", "moon");
            yield return null;
            Assert.IsTrue(effect.IsGilded(moon), "讀檔還原的已解題格直接是金底");
            Assert.IsFalse(effect.IsPlaying(moon), "不播動畫");
            Assert.AreEqual(Area(Art.AnswerBox(moon.shape)), Area(effect.FillOf(moon)), 1e-5f);
            Assert.AreEqual(Vector3.one, moon.answerText.transform.localScale);
            Assert.AreEqual(0, burst.vertexCount);
        }

        [UnityTest]
        public IEnumerator EveryBoardGetsTheSolveEffect()
        {
            foreach (var scene in new[] { "Demo", "Game", "Title" }.Where(Application.CanStreamedLevelBeLoaded))
            {
                SceneManager.LoadScene(scene);
                yield return null;
                var boards = Object.FindObjectsByType<Board>(FindObjectsSortMode.None);
                Assert.IsNotEmpty(boards, scene);
                foreach (var board in boards) Assert.IsNotNull(board.GetComponent<SolveEffect>(), scene);
            }
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lexiscript
{
    public sealed class PlayerTests
    {
        readonly InputHarness input = new InputHarness();
        Board board;
        Rigidbody2D body;
        PlayerAvatar avatar;
        bool crossed;
        int steps;

        [SetUp]
        public void SetUp() => input.SetUp();

        [UnityTearDown]
        public IEnumerator TearDown() => input.TearDown();

        IEnumerator LoadDemo()
        {
            SceneManager.LoadScene("Demo");
            yield return null;
            yield return null;
            board = Object.FindAnyObjectByType<Board>();
            body = Object.FindAnyObjectByType<Player>().GetComponent<Rigidbody2D>();
            avatar = body.GetComponentInChildren<PlayerAvatar>();
        }

        Vector2 Cell(string id) => board.Blocks.Single(block => block.name == id).transform.position;

        /// <summary>瞬間移動玩家；角色有開插值，畫面上的位置要等兩次物理更新後才會停在新位置。</summary>
        IEnumerator Teleport(Vector2 position)
        {
            body.position = position;
            body.linearVelocity = Vector2.zero;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            yield return null;
        }

        /// <summary>
        /// 把玩家放到 start，按住 key，直到沿 direction 越過 goal 那條線或過了一秒；越過了 <see cref="crossed"/> 就是 true。
        /// </summary>
        IEnumerator Push(Vector2 start, Key key, Vector2 direction, float goal)
        {
            body.position = start;
            body.linearVelocity = Vector2.zero;
            yield return new WaitForFixedUpdate();
            input.Hold(key);
            float until = Time.time + 1f;
            while (Time.time < until && Vector2.Dot(body.position, direction) < goal) yield return new WaitForFixedUpdate();
            crossed = Vector2.Dot(body.position, direction) >= goal;
            input.Release();
            yield return null;
            yield return new WaitForFixedUpdate();
        }

        /// <summary>按住這些鍵 seconds 秒，數走路影格換了幾次，結果放在 <see cref="steps"/>。</summary>
        IEnumerator CountSteps(float seconds, params Key[] keys)
        {
            input.Hold(keys);
            yield return new WaitForSeconds(0.1f);
            steps = 0;
            int last = avatar.Frame;
            for (float until = Time.time + seconds; Time.time < until;)
            {
                yield return null;
                if (avatar.Frame != last) steps++;
                last = avatar.Frame;
            }
            input.Release();
            yield return new WaitForSeconds(0.1f);
        }

        /// <summary>貼近角色拍一張：畫面中心在小人身體中段。</summary>
        void CloseUp(string name) => InputHarness.Shot(name, (Vector2)avatar.transform.position + new Vector2(0, 0.45f), 0.8f);

        [UnityTest]
        public IEnumerator SlipsIntoTheGapBetweenBlocksEvenWhenSlightlyOffCentre()
        {
            yield return LoadDemo();
            Vector2 left = Cell("NIGHT"), right = Cell("WIZARD");
            Assert.AreEqual(Art.Pitch, right.x - left.x, 1e-4f, "NIGHT 與 WIZARD 左右相鄰");
            float gapX = (left.x + right.x) / 2;
            float above = left.y + Art.Half + Art.PlayerHalf + 0.3f;
            float below = left.y - Art.Half - Art.PlayerHalf - 0.05f;

            yield return Teleport(new Vector2(gapX, left.y));
            Assert.AreEqual(gapX, body.position.x, 1e-3f, "站在縫的正中間不會被擠出去");
            Assert.AreEqual(left.y, body.position.y, 1e-3f);
            InputHarness.Shot("63_player_in_gap", new Vector2(gapX, left.y + 0.3f), 2.4f);

            foreach (float offset in new[] { -0.2f, -0.15f, 0f, 0.15f, 0.2f })
            {
                yield return Push(new Vector2(gapX + offset, above), Key.S, Vector2.down, -below);
                Assert.IsTrue(crossed, $"離縫中心 {offset}，往下按住一秒內要穿過兩格之間的縫（停在 {body.position}）");
                Assert.AreEqual(gapX, body.position.x, 0.06f, "順著方塊角滑到縫的中間");
            }

            Vector2 top = Cell("DRAWER"), bottom = Cell("SCRAMBLED");
            Assert.AreEqual(Art.Pitch, top.y - bottom.y, 1e-4f, "DRAWER 在 SCRAMBLED 正上方");
            float gapY = (top.y + bottom.y) / 2;
            float before = top.x - Art.Half - Art.PlayerHalf - 0.6f;
            float after = top.x + Art.Half + Art.PlayerHalf + 0.05f;
            foreach (float offset in new[] { -0.2f, -0.15f, 0f, 0.15f, 0.2f })
            {
                yield return Push(new Vector2(before, gapY + offset), Key.D, Vector2.right, after);
                Assert.IsTrue(crossed, $"離縫中心 {offset}，往右按住一秒內要穿過上下兩格之間的縫（停在 {body.position}）");
                Assert.AreEqual(gapY, body.position.y, 0.06f, "順著方塊角滑到縫的中間");
            }
        }

        [UnityTest]
        public IEnumerator BridgesAndPointerSymbolsDoNotBlockTheGaps()
        {
            yield return LoadDemo();
            Vector2 l = Cell("L"), p = Cell("P");
            yield return Push(new Vector2((l.x + p.x) / 2, p.y - 2.4f), Key.W, Vector2.up, p.y + Art.Half + Art.PlayerHalf + 0.05f);
            Assert.IsTrue(crossed, $"P 的指向符號凸進 L 與 P 之間的縫，仍然走得過去（停在 {body.position}）");

            Vector2 stress = Cell("STRESS"), desserts = Cell("DESSERTS");
            yield return Push(new Vector2(stress.x - 2.4f, (stress.y + desserts.y) / 2), Key.D, Vector2.right, stress.x + Art.Half + Art.PlayerHalf + 0.05f);
            Assert.IsTrue(crossed, $"STRESS 往下指的符號凸進橫縫，仍然走得過去（停在 {body.position}）");

            Vector2 flower = Cell("FLOWER"), flour = Cell("FLOUR");
            float between = (flower.x + flour.x) / 2;
            yield return Push(new Vector2(between, flour.y - 2.1f), Key.W, Vector2.up, flour.y + Art.Half + Art.PlayerHalf + 0.05f);
            Assert.IsTrue(crossed, $"FLOUR 與 FLOWER 之間的橋與箭頭不擋路（停在 {body.position}）");

            yield return Teleport(new Vector2(between, flour.y - 0.3f));
            InputHarness.Shot("65_player_over_bridge", new Vector2(between, flour.y), 2.4f);

            Vector2 moon = Cell("MOON"), spoon = Cell("SPOON");
            yield return Push(new Vector2(moon.x - 2.2f, (moon.y + spoon.y) / 2), Key.D, Vector2.right, (moon.x + spoon.x) / 2 + 0.5f);
            Assert.IsTrue(crossed, $"MOON 與 SPOON 之間的斜橋不擋路（停在 {body.position}）");
        }

        [UnityTest]
        public IEnumerator IsDrawnAboveEverythingInTheWorld()
        {
            foreach (var scene in new[] { "Demo", "Game" })
            {
                SceneManager.LoadScene(scene);
                yield return null;
                yield return null;
                var drawn = Object.FindAnyObjectByType<PlayerAvatar>().GetComponent<MeshRenderer>();
                Assert.AreEqual(Art.PlayerOrder, drawn.sortingOrder);
                foreach (var other in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    if (other == drawn) continue;
                    Assert.AreEqual(drawn.sortingLayerID, other.sortingLayerID, $"{scene} {other.name}");
                    Assert.Less(other.sortingOrder, drawn.sortingOrder, $"{scene}：{other.name} 會蓋住角色");
                }
            }
        }

        [UnityTest]
        public IEnumerator SettingSwitchesBetweenThePixelGirlAndThePlainSquare()
        {
            yield return LoadDemo();
            var filter = avatar.GetComponent<MeshFilter>();
            var circle = body.GetComponent<CircleCollider2D>();
            Assert.AreEqual(PlayerStyle.Pixel, avatar.Style, "預設是像素小人");
            StringAssert.StartsWith("PixelHero", filter.sharedMesh.name);
            Assert.Greater(filter.sharedMesh.bounds.size.y, 4 * Art.PlayerHalf, "小人比碰撞體高，頭露在碰撞體上面");

            GameSettings.PlayerStyle = PlayerStyle.Square;
            Assert.AreEqual(PlayerStyle.Square, avatar.Style, "改設定當下就換外觀");
            var square = filter.sharedMesh;
            Assert.AreEqual(2 * Art.PlayerHalf, square.bounds.size.x, 1e-4f, "方塊跟碰撞體一樣大");
            Assert.AreEqual(2 * Art.PlayerHalf, square.bounds.size.y, 1e-4f);
            Vector2 center = avatar.transform.TransformPoint(square.bounds.center);
            Assert.Less(Vector2.Distance(body.transform.position, center), 1e-3f, "方塊正好蓋在碰撞體上");

            input.Hold(Key.A);
            yield return new WaitForSeconds(0.3f);
            input.Release();
            yield return new WaitForSeconds(0.2f);
            Assert.AreSame(square, filter.sharedMesh, "走路轉身也維持方塊");
            Assert.AreEqual(Vector3.one, avatar.transform.localScale, "方塊不做擠壓");
            InputHarness.Shot("64_player_square", body.position + new Vector2(0, 1.2f), 2.4f);

            GameSettings.PlayerStyle = PlayerStyle.Pixel;
            Assert.AreEqual(PlayerStyle.Pixel, avatar.Style);
            StringAssert.StartsWith("PixelHero", filter.sharedMesh.name);
            Assert.AreEqual(Art.PlayerHalf, circle.radius, 1e-6f, "換外觀不會改到碰撞體");
            Assert.AreEqual(1, body.GetComponentsInChildren<Collider2D>().Length);
        }

        [UnityTest]
        public IEnumerator FacesWhereItWalksWithASquashOnEveryTurn()
        {
            yield return LoadDemo();
            Assert.AreEqual(Facing.Down, avatar.Facing, "一開始面向鏡頭");
            var collider = body.GetComponent<Collider2D>();

            // 出生點上方 1.5 格內沒有方塊，左右與下方都很空，每個方向走半秒都不會撞到東西
            foreach (var (key, facing) in new[] { (Key.A, Facing.Left), (Key.D, Facing.Right), (Key.S, Facing.Down), (Key.W, Facing.Up) })
            {
                input.Hold(key);
                var frames = new HashSet<int>();
                bool squashed = false;
                float turnedBy = float.PositiveInfinity;
                for (float until = Time.time + 0.5f; Time.time < until;)
                {
                    yield return null;
                    frames.Add(avatar.Frame);
                    var scale = avatar.transform.localScale;
                    if (avatar.Facing == facing && float.IsPositiveInfinity(turnedBy)) turnedBy = Time.time;
                    squashed |= scale.x > 1.05f && scale.y < 0.95f;
                    if (Time.time > turnedBy + PlayerAvatar.TurnSeconds + 0.05f)
                        Assert.AreEqual(Vector3.one, scale, "擠壓很快就彈回原狀");
                    Assert.AreEqual(2 * Art.PlayerHalf, collider.bounds.size.x, 1e-3f, "外觀擠壓不會改到碰撞體");
                    Assert.AreEqual(2 * Art.PlayerHalf, collider.bounds.size.y, 1e-3f, "外觀擠壓不會改到碰撞體");
                }
                Assert.AreEqual(facing, avatar.Facing, $"按 {key} 轉向 {facing}");
                Assert.IsTrue(squashed, $"轉向 {facing} 時先壓扁再彈回");
                CollectionAssert.IsSubsetOf(new[] { 2, 3, 4, 5 }, frames, $"往 {facing} 走時四個走路影格都會出現");
                Assert.AreEqual(-Art.PlayerHalf, avatar.transform.localPosition.y, 1e-6f, "外觀一直錨在腳底");
                if (facing == Facing.Left) CloseUp("61_player_walk_left");
                input.Release();
                yield return new WaitForSeconds(0.1f);
            }

            Assert.AreEqual(Facing.Up, avatar.Facing, "放開方向鍵後維持最後的朝向");
            input.Hold(Key.W, Key.D);
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(Facing.Up, avatar.Facing, "斜走時原本的方向還按著就不轉身");
            input.Hold(Key.D);
            yield return new WaitForSeconds(0.2f);
            Assert.AreEqual(Facing.Right, avatar.Facing);
            input.Release();
            yield return new WaitForSeconds(0.3f);
            CloseUp("62_player_idle_right");
        }

        [UnityTest]
        public IEnumerator BreathesAndBlinksWhenIdleAndStepsFasterWhenSprinting()
        {
            yield return LoadDemo();
            var idle = new HashSet<int>();
            bool blinked = false;
            for (float until = Time.time + 5.5f; Time.time < until && !(blinked && idle.Count == 2);)
            {
                yield return null;
                idle.Add(avatar.Frame);
                if (!avatar.Blinking) continue;
                blinked = true;
                StringAssert.Contains("blink", avatar.GetComponent<MeshFilter>().sharedMesh.name, "眨眼時換成閉眼的影格");
            }
            CollectionAssert.AreEquivalent(new[] { 0, 1 }, idle, "站著時在吸氣、吐氣兩個影格之間呼吸");
            Assert.IsTrue(blinked, "站著不動幾秒內會眨眼");

            // 最下面一排方塊底下是一長條空地，往右走與疾跑都有足夠的距離
            yield return Teleport(new Vector2(-0.5f, 0.25f));
            yield return CountSteps(0.8f, Key.D);
            int walking = steps;
            Assert.GreaterOrEqual(walking, 5, "走路時影格一直在換");
            yield return CountSteps(0.8f, Key.D, Key.LeftShift);
            Assert.Greater(steps, walking * 1.4f, "疾跑時腳步比較快");
            Assert.AreEqual(Facing.Right, avatar.Facing);
        }

        [UnityTest]
        public IEnumerator RendersEveryFrameOfThePixelGirlForReview()
        {
            yield return LoadDemo();
            var material = avatar.GetComponent<MeshRenderer>().sharedMaterial;
            // 放在房間外面，背景乾淨；一列一個方向，最後一欄是閉眼
            var origin = new Vector2(60, 0);
            const float across = 0.8f, down = 1.15f;
            var facings = new[] { Facing.Down, Facing.Up, Facing.Left, Facing.Right };
            var shown = new List<Object>();
            for (int row = 0; row < facings.Length; row++)
                for (int column = 0; column <= PixelHero.FrameCount; column++)
                {
                    bool blink = column == PixelHero.FrameCount;
                    var mesh = PixelHero.Build(facings[row], blink ? 0 : column, blink);
                    var go = new GameObject($"Frame {facings[row]} {column}", typeof(MeshFilter), typeof(MeshRenderer));
                    go.transform.position = origin + new Vector2(column * across, -row * down);
                    go.GetComponent<MeshFilter>().sharedMesh = mesh;
                    var renderer = go.GetComponent<MeshRenderer>();
                    renderer.sharedMaterial = material;
                    renderer.sortingOrder = Art.PlayerOrder;
                    shown.Add(go);
                    shown.Add(mesh);
                }
            var middle = origin + new Vector2(PixelHero.FrameCount * across / 2, -(facings.Length - 1) * down / 2 + 0.45f);
            InputHarness.Shot("60_player_all_frames", middle, 2.6f);
            foreach (var thing in shown) Object.Destroy(thing);
        }
    }
}

using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lexiscript
{
    /// <summary>建好的場景裡角色的碰撞體、外觀與圖層，以及盤面上哪些東西有碰撞體。</summary>
    [TestFixture("Assets/Lexiscript/Demo.unity")]
    [TestFixture("Assets/Lexiscript/Game.unity")]
    public sealed class PlayerSceneTests
    {
        readonly string scenePath;
        Player player;
        PlayerAvatar avatar;

        public PlayerSceneTests(string scenePath) => this.scenePath = scenePath;

        [OneTimeSetUp]
        public void OpenScene()
        {
            EditorSceneManager.OpenScene(scenePath);
            player = Object.FindAnyObjectByType<Player>();
            avatar = player.GetComponentInChildren<PlayerAvatar>();
        }

        [Test]
        public void ColliderIsASmallCircleThatFitsBetweenBlocks()
        {
            var colliders = player.GetComponentsInChildren<Collider2D>();
            Assert.AreEqual(1, colliders.Length, "外觀的子物件沒有碰撞體");
            var circle = colliders[0] as CircleCollider2D;
            Assert.IsNotNull(circle, "圓形碰撞體推著方塊角時才會順著角滑進縫裡");
            Assert.AreSame(player.gameObject, circle.gameObject);
            Assert.AreEqual(Art.PlayerHalf, circle.radius, 1e-6f);
            Assert.AreEqual(Vector2.zero, circle.offset);
            Assert.Less(2 * circle.radius, Art.Pitch - 2 * Art.Half - 0.05f, "比方塊之間的縫窄，還留一點餘裕");
        }

        [Test]
        public void AvatarIsVisibleWithoutPlayingAndStandsOnTheBottomOfTheCollider()
        {
            Assert.IsNotNull(avatar, "角色底下要有外觀子物件");
            Assert.AreNotSame(player.gameObject, avatar.gameObject, "外觀放在子物件，擠壓縮放不會影響碰撞體");
            var mesh = avatar.GetComponent<MeshFilter>().sharedMesh;
            Assert.IsTrue(mesh != null && mesh.vertexCount > 0, "不按 Play 也看得到角色");
            Assert.AreEqual(0, avatar.transform.localPosition.x, 1e-6f);
            Assert.AreEqual(-Art.PlayerHalf, avatar.transform.localPosition.y, 1e-6f, "錨在碰撞體底部，腳踩在那裡");
            Assert.AreEqual(Vector3.one, avatar.transform.localScale);
        }

        [Test]
        public void PlayerIsDrawnAboveEverythingElse()
        {
            var drawn = avatar.GetComponent<MeshRenderer>();
            Assert.AreEqual(Art.PlayerOrder, drawn.sortingOrder);
            foreach (var other in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (other == drawn) continue;
                Assert.AreEqual(drawn.sortingLayerID, other.sortingLayerID, other.name);
                Assert.Less(other.sortingOrder, drawn.sortingOrder, $"{other.name} 會蓋住角色");
            }
        }

        [Test]
        public void BridgesAndPointersHaveNoColliders()
        {
            var board = Object.FindAnyObjectByType<Board>();
            Assert.IsEmpty(board.bridges.GetComponentsInChildren<Collider2D>(), "連接橋沒有碰撞體");
            foreach (var collider in board.GetComponentsInChildren<Collider2D>())
            {
                var block = collider.GetComponent<Block>();
                Assert.IsNotNull(block, $"{collider.name}：盤面上只有方塊本身有碰撞體");
                var box = collider as BoxCollider2D;
                Assert.IsNotNull(box, block.name);
                // 指向符號畫在方塊邊上、一半凸到縫裡；碰撞範圍只到方塊邊緣，凸出去的那半走得過去
                Assert.AreEqual(Vector2.one * (2 * Art.Half), box.size, block.name);
                Assert.AreEqual(Vector2.zero, box.offset, block.name);
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Lexiscript
{
    /// <summary>
    /// 門還沒開的房間上方的遮罩（平面設計的屋頂）：看不到裡面，裡面的方塊也不能點、不顯示顏色名稱。
    /// <see cref="door"/> 打開時從門那一側往內擦除；讀檔時直接開門（<see cref="Door.Opened"/> 帶 true）就立刻消失。
    /// 遮罩只在執行時畫，編輯模式的場景裡看得到房間內容。
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class RoomCover : MonoBehaviour
    {
        /// <summary>開門後遮罩從門那一側擦除到完全收起的秒數。</summary>
        public const float RevealSeconds = 0.7f;

        static readonly List<RoomCover> covers = new List<RoomCover>();

        /// <summary>遮住的範圍（世界座標），物件本身擺在它的中心。只蓋地板，牆和門留在外面看得見。</summary>
        public Rect area;

        /// <summary>打開後遮罩就收起來的那扇門。</summary>
        public Door door;

        Mesh mesh;
        Vector2 inward, emblem;
        bool revealing;
        float revealStart, wipe;

        /// <summary>收起的進度：0 是完全蓋住，1 是完全收起。</summary>
        public float Progress { get; private set; }

        /// <summary>還遮著（門還沒開，或還在收起來的動畫中）。</summary>
        public bool Covering => Progress < 1;

        /// <summary>這個世界座標被某個還沒打開的房間遮住了；收起動畫中只算還沒擦掉的那一塊。</summary>
        public static bool Hides(Vector2 world)
        {
            foreach (var cover in covers)
                if (cover.Conceals(world)) return true;
            return false;
        }

        bool Conceals(Vector2 world) =>
            Covering && area.Contains(world) && Art.CoverRemains(area.size, inward, wipe, world - area.center);

        void OnEnable()
        {
            covers.Add(this);
            if (mesh == null)
            {
                mesh = new Mesh { hideFlags = HideFlags.DontSave };
                GetComponent<MeshFilter>().sharedMesh = mesh;
            }
            var doorway = door != null ? (Vector2)door.transform.position - area.center : Vector2.left * area.width;
            inward = Art.CoverInward(area.size, doorway);
            emblem = Art.CoverEmblem(area.size, inward, doorway);
            if (door != null)
            {
                door.Opened += OnOpened;
                if (door.IsOpen) Progress = 1;
            }
            Draw();
        }

        void OnDisable()
        {
            covers.Remove(this);
            if (door != null) door.Opened -= OnOpened;
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }

        void OnOpened(bool instant)
        {
            if (instant)
            {
                Progress = 1;
                Draw();
                return;
            }
            revealing = true;
            revealStart = Time.time;
        }

        void Update()
        {
            if (!revealing || !Covering) return;
            Progress = Mathf.Clamp01((Time.time - revealStart) / RevealSeconds);
            Draw();
        }

        void Draw()
        {
            wipe = Ease.InOutCubic(Progress);
            var builder = new MeshBuilder();
            if (Covering) Art.Cover(builder, area.size, inward, emblem, wipe);
            builder.Apply(mesh);
        }
    }
}

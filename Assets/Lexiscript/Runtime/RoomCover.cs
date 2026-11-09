using System;
using System.Collections.Generic;
using UnityEngine;

namespace Lexiscript
{
    /// <summary>
    /// 門還沒開的房間上方的遮罩（平面設計的屋頂）：看不到裡面，裡面的方塊也不能點、不顯示顏色名稱。
    /// <see cref="doors"/> 任何一扇打開時，從那扇門的一側往內擦除；讀檔時直接開門（<see cref="Door.Opened"/> 帶 true）就立刻消失。
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

        /// <summary>通往這個房間的門，任何一扇打開遮罩就收起來；第一扇是主要入口，標誌畫在靠它的那一側。</summary>
        public Door[] doors = new Door[0];

        /// <summary>畫鎖；同一間房的走道、側間、橋只蓋屋頂，鎖畫在主要那塊地板上就好。</summary>
        public bool locked = true;

        /// <summary>
        /// 同一間房所有遮罩合起來的範圍（世界座標）。擦除的方向與進度依它來算，房間分成好幾塊時才會一路連貫地掀開；
        /// 寬度是 0 時就用 <see cref="area"/>。
        /// </summary>
        public Rect frame;

        Rect Frame => frame.width > 0 ? frame : area;

        readonly List<(Door door, Action<bool> opened)> subscriptions = new List<(Door, Action<bool>)>();
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
            Covering && area.Contains(world) && Art.CoverRemains(Frame.size, inward, wipe, world - Frame.center);

        void OnEnable()
        {
            covers.Add(this);
            if (mesh == null)
            {
                mesh = new Mesh { hideFlags = HideFlags.DontSave };
                GetComponent<MeshFilter>().sharedMesh = mesh;
            }
            var main = doors.Length > 0 ? doors[0] : null;
            Aim(main);
            var doorway = main != null ? (Vector2)main.transform.position - area.center : Vector2.left * area.width;
            emblem = Art.CoverEmblem(area.size, inward, doorway);
            foreach (var door in doors)
            {
                if (door == null) continue;
                Action<bool> opened = instant => OnOpened(door, instant);
                door.Opened += opened;
                subscriptions.Add((door, opened));
                if (door.IsOpen) Progress = 1;
            }
            Draw();
        }

        void OnDisable()
        {
            covers.Remove(this);
            foreach (var (door, opened) in subscriptions) door.Opened -= opened;
            subscriptions.Clear();
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }

        /// <summary>從這扇門那一側往內收；沒有門時當作在左邊。</summary>
        void Aim(Door door)
        {
            var frame = Frame;
            var doorway = door != null ? (Vector2)door.transform.position - frame.center : Vector2.left * frame.width;
            inward = Art.CoverInward(frame.size, doorway);
        }

        void OnOpened(Door door, bool instant)
        {
            // 另一扇門已經開始掀開了，就照原本的方向收完
            if (revealing || !Covering) return;
            Aim(door);
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
            if (Covering) Art.Cover(builder, area.size, inward, emblem, wipe, locked, area.center, Frame);
            builder.Apply(mesh);
        }
    }
}

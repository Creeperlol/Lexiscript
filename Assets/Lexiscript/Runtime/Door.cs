using System;
using System.Linq;
using TMPro;
using UnityEngine;

namespace Lexiscript
{
    /// <summary>
    /// 教學房間的出口門與門後房間的門：<see cref="blocks"/> 裡答對 <see cref="required"/> 題就打開，門上顯示目前進度。
    /// 正式關卡一律要求全部答對（教學門要確定玩家真的學會，門後房間的門只有門前那一道題）。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider2D))]
    public sealed class Door : MonoBehaviour
    {
        const float OpenSeconds = 0.25f;

        public Board board;
        public Block[] blocks = new Block[0];
        public int required;
        public TextMeshPro counter;

        Mesh mesh;
        float openedAt = float.NegativeInfinity;

        public bool IsOpen { get; private set; }

        /// <summary>門打開的那一刻；參數為 true 表示是載入存檔時直接打開（沒有動畫）。</summary>
        public event Action<bool> Opened;

        public int SolvedCount =>
            board == null || board.Puzzle == null ? 0 : blocks.Count(block => block != null && board.Puzzle.IsSolved(block.name));

        /// <summary>載入存檔、還原解題狀態後呼叫：已經達標的門直接打開，不播收起來的動畫。</summary>
        public void SyncInstantly() => Refresh(true);

        void OnEnable()
        {
            if (mesh == null)
            {
                mesh = new Mesh { hideFlags = HideFlags.DontSave };
                GetComponent<MeshFilter>().sharedMesh = mesh;
            }
            var builder = new MeshBuilder();
            Art.Panel(builder, GetComponent<BoxCollider2D>().size, Art.DoorColor);
            builder.Apply(mesh);
            Refresh();
        }

        void OnDisable()
        {
            if (mesh != null) DestroyImmediate(mesh);
        }

        void Update()
        {
            if (!IsOpen) Refresh();
            if (!IsOpen || !Application.isPlaying) return;
            // 門沿著短邊收起來
            float t = Ease.InCubic((Time.time - openedAt) / OpenSeconds);
            var size = GetComponent<BoxCollider2D>().size;
            transform.localScale = size.x < size.y ? new Vector3(1, 1 - t, 1) : new Vector3(1 - t, 1, 1);
        }

        void Refresh(bool instant = false)
        {
            if (IsOpen) return;
            int solved = SolvedCount;
            if (counter != null) counter.text = $"{Mathf.Min(solved, required)}/{required}";
            if (!Application.isPlaying || solved < required) return;
            IsOpen = true;
            openedAt = instant ? float.NegativeInfinity : Time.time;
            GetComponent<BoxCollider2D>().enabled = false;
            if (counter != null) counter.text = "";
            Opened?.Invoke(instant);
        }
    }
}

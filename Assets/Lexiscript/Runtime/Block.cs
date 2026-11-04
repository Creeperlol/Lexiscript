using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace Lexiscript
{
    /// <summary>
    /// 一格方塊，放在 <see cref="Board"/> 底下。格子種類由欄位推得：
    /// 有題目＝題目格；沒有題目、有答案＝答案格；兩者都沒有＝純色格。
    /// 新增方塊請直接複製（Ctrl+D）既有的方塊。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class Block : MonoBehaviour
    {
        [Serializable]
        public struct Link
        {
            public Block target;
            public LinkKind kind;

            public Link(Block target, LinkKind kind)
            {
                this.target = target;
                this.kind = kind;
            }
        }

        public BlockColor color;
        public BlockShape shape;
        public string clue = "";
        public string[] answers = new string[0];
        public string[] hintAnswers = new string[0];
        public List<Link> links = new List<Link>();
        public TextMeshPro clueText;
        public TextMeshPro answerText;

        Mesh mesh;

        public bool IsPure => string.IsNullOrEmpty(clue) && answers.Length == 0;

        public BlockDef ToDef() => new BlockDef(name, Upper(answers), Upper(hintAnswers));

        /// <summary>依目前欄位與連結目標的位置重畫這格的網格。</summary>
        public void Redraw()
        {
            // 停用的元件不會再收到 OnDisable，這時建的網格就沒人銷毀
            if (!enabled) return;
            if (mesh == null)
            {
                // 網格由欄位推得，不存進場景；複製出來的方塊也會因此各自建立自己的網格
                mesh = new Mesh { hideFlags = HideFlags.DontSave };
                GetComponent<MeshFilter>().sharedMesh = mesh;
            }
            var pointers = links
                .Where(link => link.target != null && link.kind == LinkKind.Pointer)
                .Select(link => Art.Direction(link.target.transform.position - transform.position));
            var builder = new MeshBuilder();
            Art.Block(builder, color, shape, IsPure, pointers);
            builder.Apply(mesh);
        }

        void OnDisable()
        {
            if (mesh != null) DestroyImmediate(mesh);
        }

#if UNITY_EDITOR
        // Edit 模式的 Update 只在場景變動時才執行，Inspector 改欄位後要自己排一次，Board 才會重畫
        void OnValidate() => UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif

        static string[] Upper(string[] words) => words.Select(word => word.ToUpperInvariant()).ToArray();
    }
}

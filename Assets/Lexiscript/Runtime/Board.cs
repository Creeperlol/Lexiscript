using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lexiscript
{
    /// <summary>
    /// 底下所有 <see cref="Block"/> 組成的盤面。
    /// Edit 模式依場景內容重建網格與文字，所以不按 Play 也看得到方塊；
    /// Play 模式負責點擊聚焦、打字判定、提示句與移動鎖。
    /// </summary>
    [ExecuteAlways]
    public sealed class Board : MonoBehaviour
    {
        public static readonly string[] HintPhrases =
        {
            "correct, but not correct, try another answer?",
            "technically right... but not the one.",
            "that fits the rule, but it's not the answer.",
            "good thinking, wrong word.",
            "so close! try another word.",
            "valid, but this cell wants something else.",
            "nice try, there's a better fit.",
            "right idea, different word.",
        };

        public MeshFilter bridges;
        public TextMeshPro hint;

        Mesh bridgeMesh;
        Keyboard keyboard;
        bool awaitingRelease;
        int lastHint = -1;

        public Block[] Blocks { get; private set; }
        public Puzzle Puzzle { get; private set; }
        public Block Focused { get; private set; }
        public bool CanMove => Focused == null && !awaitingRelease;

        void OnEnable()
        {
            Rebuild();
            if (Application.isPlaying)
            {
                Puzzle = new Puzzle(Blocks.Select(block => block.ToDef()));
                keyboard = Keyboard.current;
                keyboard.onTextInput += OnText;
            }
            Refresh();
        }

        void OnDisable()
        {
            if (keyboard != null) keyboard.onTextInput -= OnText;
            if (bridgeMesh != null) DestroyImmediate(bridgeMesh);
        }

        void Update()
        {
            if (!Application.isPlaying)
            {
                Rebuild();
                Refresh();
                return;
            }

            if (awaitingRelease && !MoveKeyHeld()) awaitingRelease = false;

            var mouse = Mouse.current;
            if (mouse.leftButton.wasPressedThisFrame)
                ClickAt(Camera.main.ScreenToWorldPoint(mouse.position.ReadValue()));

            if (Focused != null && !awaitingRelease && keyboard.backspaceKey.wasPressedThisFrame)
            {
                var before = Puzzle.Input(Focused.name);
                Puzzle.Backspace(Focused.name);
                InputChanged(Focused, before);
            }
        }

        /// <summary>點在未解格子的答案半格就聚焦那格，點其他地方一律取消聚焦。</summary>
        void ClickAt(Vector2 world)
        {
            Focused = Blocks.FirstOrDefault(block => Puzzle.IsAnswerable(block.name) && InAnswerHalf(block, world));
            if (Focused != null)
            {
                // 按著移動鍵時點擊，OS 的按鍵連發會把 WASD 打進答案欄
                awaitingRelease = MoveKeyHeld();
                // 注音等輸入法會先組字、吞掉英文字母
                keyboard.SetIMEEnabled(false);
            }
            Refresh();
        }

        void OnText(char c)
        {
            if (Focused == null || awaitingRelease) return;
            var block = Focused;
            var before = Puzzle.Input(block.name);
            if (Puzzle.Type(block.name, c))
            {
                Focused = null;
                // 解題的最後一個字母可能是 WASD 且還按著，放開前不能讓它變成移動
                awaitingRelease = true;
            }
            InputChanged(block, before);
        }

        void InputChanged(Block block, string before)
        {
            if (Puzzle.Input(block.name) == before) return;
            hint.text = Puzzle.IsHint(block.name) ? NextHint() : "";
            hint.transform.position = block.transform.position + Vector3.up * Art.HintOffset;
            Refresh();
        }

        string NextHint()
        {
            int index;
            do index = Random.Range(0, HintPhrases.Length);
            while (index == lastHint);
            lastHint = index;
            return HintPhrases[index];
        }

        void Rebuild()
        {
            Blocks = GetComponentsInChildren<Block>();
            var builder = new MeshBuilder();
            foreach (var block in Blocks)
            {
                block.Redraw();
                foreach (var link in block.links)
                    if (link.target != null && link.kind != LinkKind.Pointer)
                        Art.Bridge(builder, Local(block), Local(link.target), link.kind == LinkKind.BridgePointer);
            }
            if (bridgeMesh == null)
            {
                bridgeMesh = new Mesh { hideFlags = HideFlags.DontSave };
                bridges.sharedMesh = bridgeMesh;
            }
            builder.Apply(bridgeMesh);
        }

        void Refresh()
        {
            foreach (var block in Blocks)
            {
                block.clueText.text = block.clue;
                bool solved = Puzzle != null && Puzzle.IsSolved(block.name);
                block.answerText.text = Puzzle == null ? "" : Puzzle.Input(block.name) + (block == Focused ? "_" : "");
                block.answerText.fontStyle = solved ? FontStyles.Bold : FontStyles.Normal;
                block.answerText.color = solved ? Art.Ink : Art.Typing;
            }
        }

        Vector2 Local(Block block) => bridges.transform.InverseTransformPoint(block.transform.position);

        static bool InAnswerHalf(Block block, Vector2 point)
        {
            Vector2 center = block.transform.position;
            return Mathf.Abs(point.x - center.x) <= Art.Half && point.y <= center.y && point.y >= center.y - Art.Half;
        }

        bool MoveKeyHeld() =>
            keyboard.wKey.isPressed || keyboard.aKey.isPressed || keyboard.sKey.isPressed || keyboard.dKey.isPressed;
    }
}

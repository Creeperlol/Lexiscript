using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace Lexiscript
{
    /// <summary>
    /// 底下所有 <see cref="Block"/> 組成的盤面。
    /// Edit 模式依場景內容重建網格與文字，所以不按 Play 也看得到方塊；
    /// Play 模式負責點擊聚焦、字首字尾提示、答案欄編輯、提示句與解題判定。
    /// </summary>
    [ExecuteAlways]
    public sealed class Board : MonoBehaviour, ITextTarget
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

        const float BlinkHalfPeriod = 0.53f;
        const float DoubleClickTime = 0.35f;
        /// <summary>打完提示答案直接按 Enter 離開時，提示句再留這麼久，否則根本來不及看到。</summary>
        public const float HintLingerSeconds = 2f;

        public MeshFilter bridges;

        readonly TextEditBuffer field = new TextEditBuffer(Puzzle.IsLetter, Puzzle.MaxInputLength);
        readonly Dictionary<Block, Reveal> reveals = new Dictionary<Block, Reveal>();
        Mesh bridgeMesh, selectionMesh;
        int drawnSelection = -1;
        float blinkStart, lastClickTime = float.NegativeInfinity, hintHideAt = float.PositiveInfinity;
        bool dragging;
        int lastHint = -1;

        public Block[] Blocks { get; private set; }
        public Puzzle Puzzle { get; private set; }
        public Block Focused { get; private set; }
        /// <summary>聚焦中的答案欄內容（含游標與選取）。</summary>
        public TextEditBuffer Field => field;
        /// <summary>目前要顯示的提示句（由介面的 <see cref="HintLabel"/> 顯示）；沒有時是空字串。</summary>
        public string HintPhrase { get; private set; } = "";
        public bool WantsIme => false;

        public event Action<Block> Solved;
        /// <summary>玩家點題目那半格，露出或收起了字首字尾。</summary>
        public event Action<Block> RevealChanged;

        public Reveal RevealOf(Block block) => reveals.TryGetValue(block, out var reveal) ? reveal : Reveal.None;

        /// <summary>滑鼠指在方塊色框上（純色格是整格）時回傳那格，指在內面或空地上回傳 null。</summary>
        public Block BandAt(Vector2 world) => Blocks.FirstOrDefault(block => block.InBand(world));

        /// <summary>讀檔用：直接把這格設成已解開，不觸發 <see cref="Solved"/>，所以不會播解題特效。</summary>
        public void RestoreSolved(Block block)
        {
            if (Puzzle.SetInput(block.name, Puzzle.Answer(block.name))) Refresh(block);
        }

        /// <summary>讀檔用：還原這格露出的字首或字尾，不觸發 <see cref="RevealChanged"/>。</summary>
        public void RestoreReveal(Block block, Reveal reveal)
        {
            reveals[block] = reveal;
            Refresh(block);
        }

        void OnEnable()
        {
            Rebuild();
            if (Application.isPlaying)
            {
                Puzzle = new Puzzle(Blocks.Select(block => block.ToDef()));
                field.Changed += OnFieldChanged;
                // TMP 的 <mark> 在世界座標文字上只剩很淡的底色（SDF 邊緣抗鋸齒），選取範圍改用自己的網格
                selectionMesh = Overlay("Selection", transform, Art.SelectionOrder);
                if (!TryGetComponent<SolveEffect>(out _)) gameObject.AddComponent<SolveEffect>();
            }
            Refresh();
        }

        void OnDisable()
        {
            field.Changed -= OnFieldChanged;
            TextFocus.Release(this);
            if (bridgeMesh != null) DestroyImmediate(bridgeMesh);
            if (selectionMesh != null) Destroy(selectionMesh);
        }

        Mesh Overlay(string name, Transform parent, int sortingOrder)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(parent, false);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = bridges.GetComponent<MeshRenderer>().sharedMaterial;
            renderer.sortingOrder = sortingOrder;
            var mesh = new Mesh { hideFlags = HideFlags.DontSave };
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            return mesh;
        }

        void Update()
        {
            if (!Application.isPlaying)
            {
                Rebuild();
                Refresh();
                return;
            }
            HandleMouse();
            if (Time.unscaledTime >= hintHideAt) HideHint();
            if (Focused != null) Refresh(Focused);
            DrawSelection();
        }

        void DrawSelection()
        {
            int version = Focused == null || !field.HasSelection ? -1 : field.Version;
            if (version == drawnSelection) return;
            drawnSelection = version;
            var builder = new MeshBuilder();
            if (version >= 0)
            {
                var text = Focused.answerText;
                text.ForceMeshUpdate();
                var info = text.textInfo;
                var local = transform.worldToLocalMatrix * text.transform.localToWorldMatrix;
                for (int i = field.SelectionStart; i < field.SelectionEnd && i < info.characterCount; i++)
                {
                    var character = info.characterInfo[i];
                    var line = info.lineInfo[character.lineNumber];
                    Vector2 min = local.MultiplyPoint3x4(new Vector3(character.origin, line.descender));
                    Vector2 max = local.MultiplyPoint3x4(new Vector3(character.xAdvance, line.ascender));
                    builder.Add(Shapes.Rect(min, max), Art.Selection);
                }
            }
            builder.Apply(selectionMesh);
        }

        void HandleMouse()
        {
            var mouse = Mouse.current;
            var view = Camera.main;
            if (mouse == null || view == null) return;
            Vector2 screen = mouse.position.ReadValue();
            if (dragging)
            {
                if (Focused != null && mouse.leftButton.isPressed) field.MoveTo(CaretIndexAt(screen), true);
                else dragging = false;
            }

            bool left = mouse.leftButton.wasPressedThisFrame, right = mouse.rightButton.wasPressedThisFrame;
            if (!left && !right) return;
            // 點在介面上也算離開答案欄；介面要不要拿走焦點由它自己決定
            if (Hud.IsPointerOverUi(screen))
            {
                Unfocus();
                return;
            }
            Vector2 world = view.ScreenToWorldPoint(screen);
            var block = RoomCover.Hides(world) ? null : Blocks.FirstOrDefault(candidate => Puzzle.IsAnswerable(candidate.name) && candidate.Contains(world));
            if (block == null)
            {
                Unfocus();
                return;
            }
            if (!block.InAnswerHalf(world))
            {
                Unfocus();
                reveals[block] = AnswerHint.Toggle(RevealOf(block), left ? Reveal.First : Reveal.Last);
                Refresh(block);
                RevealChanged?.Invoke(block);
                return;
            }
            if (right)
            {
                if (block != Focused) Unfocus();
            }
            else if (block != Focused)
            {
                Focus(block);
            }
            else
            {
                ClickInField(screen);
            }
        }

        void ClickInField(Vector2 screen)
        {
            float now = Time.unscaledTime;
            if (now - lastClickTime <= DoubleClickTime)
            {
                field.SelectAll();
                lastClickTime = float.NegativeInfinity;
            }
            else
            {
                var keyboard = Keyboard.current;
                field.MoveTo(CaretIndexAt(screen), keyboard != null && keyboard.shiftKey.isPressed);
                dragging = true;
                lastClickTime = now;
            }
            blinkStart = now;
        }

        int CaretIndexAt(Vector2 screen)
        {
            var text = Focused.answerText;
            text.ForceMeshUpdate();
            return Mathf.Clamp(TMP_TextUtilities.GetCursorIndexFromPosition(text, screen, Camera.main), 0, field.Length);
        }

        /// <summary>聚焦到這格的答案欄（等同點它的下半部）。</summary>
        public void Focus(Block block)
        {
            Unfocus();
            Focused = block;
            field.Reset("");
            blinkStart = lastClickTime = Time.unscaledTime;
            TextFocus.Set(this);
            Refresh(block);
        }

        /// <summary>沒答對就離開：清掉輸入，答案欄回到聚焦前的提示（包含字首字尾）。</summary>
        void Unfocus()
        {
            var block = Focused;
            if (block == null) return;
            bool hinted = Puzzle.IsHint(block.name);
            Focused = null;
            dragging = false;
            TextFocus.Release(this);
            Puzzle.Clear(block.name);
            if (hinted) hintHideAt = Time.unscaledTime + HintLingerSeconds;
            else HideHint();
            Refresh(block);
        }

        void OnFieldChanged()
        {
            var block = Focused;
            if (block == null) return;
            if (Puzzle.SetInput(block.name, field.Text))
            {
                Focused = null;
                dragging = false;
                TextFocus.Release(this);
                HideHint();
                Refresh(block);
                Solved?.Invoke(block);
                return;
            }
            if (Puzzle.IsHint(block.name)) ShowHint();
            else HideHint();
        }

        public void OnText(char c)
        {
            if (Focused == null) return;
            field.Type(c);
            blinkStart = Time.unscaledTime;
        }

        public void OnComposition(string composition) { }

        public void OnKey(Key key, bool shift, bool ctrl)
        {
            if (Focused == null) return;
            if (key == Key.Enter || key == Key.NumpadEnter || key == Key.Escape) Unfocus();
            else if (TextEditKeys.Apply(field, key, shift, ctrl)) blinkStart = Time.unscaledTime;
        }

        public void OnFocusChanged(bool focused)
        {
            if (!focused) Unfocus();
        }

        void ShowHint()
        {
            HintPhrase = NextHint();
            hintHideAt = float.PositiveInfinity;
        }

        void HideHint()
        {
            HintPhrase = "";
            hintHideAt = float.PositiveInfinity;
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
                        Art.Bridge(builder, Local(block), Local(link.target));
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
            foreach (var block in Blocks) Refresh(block);
        }

        void Refresh(Block block)
        {
            if (block.clueText != null) block.clueText.text = block.clue;
            if (block.answerText == null) return;
            if (Puzzle == null)
            {
                Show(block, Placeholder(block.answer ?? "", Reveal.None), Art.PlaceholderInk, false);
            }
            else if (Puzzle.IsSolved(block.name))
            {
                Show(block, Puzzle.Input(block.name), Art.Correct, true);
            }
            else if (block == Focused)
            {
                Show(block, FieldMarkup(), Art.Ink, false);
            }
            else
            {
                Show(block, Placeholder(Puzzle.Answer(block.name), RevealOf(block)), Art.PlaceholderInk, false);
            }
        }

        static void Show(Block block, string text, Color color, bool bold)
        {
            var answerText = block.answerText;
            answerText.text = text;
            answerText.color = color;
            answerText.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        }

        /// <summary>底線之間留點字距，連在一起的底線數不出字數。</summary>
        static string Placeholder(string answer, Reveal reveal)
        {
            if (answer.Length == 0) return "";
            var text = AnswerHint.Placeholder(answer, reveal);
            int count = text.LastIndexOf('(');
            return $"<cspace=0.12em>{text.Substring(0, count)}</cspace>{text.Substring(count)}";
        }

        /// <summary>
        /// 聚焦中的答案欄：游標是閃爍的底線——在字中間時畫在游標後那個字底下，在最後時是一個底線字元。
        /// 最後那個底線不閃時也保留成透明的，置中的文字才不會跟著閃爍左右跳。選取範圍的底色由 <see cref="DrawSelection"/> 畫。
        /// </summary>
        string FieldMarkup()
        {
            bool caretOn = (Time.unscaledTime - blinkStart) % (2 * BlinkHalfPeriod) < BlinkHalfPeriod;
            var text = new StringBuilder();
            for (int i = 0; i < field.Length; i++)
            {
                bool underline = caretOn && i == field.Caret;
                if (underline) text.Append("<u>");
                text.Append(field[i]);
                if (underline) text.Append("</u>");
            }
            text.Append(caretOn && field.Caret == field.Length ? "_" : "<alpha=#00>_");
            return text.ToString();
        }

        Vector2 Local(Block block) => bridges.transform.InverseTransformPoint(block.transform.position);
    }
}

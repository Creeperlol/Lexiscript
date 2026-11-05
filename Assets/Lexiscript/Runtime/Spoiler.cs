using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lexiscript
{
    /// <summary>
    /// 教學關方塊旁的暴雷條：黑條寫著 Click to reveal the answer.，
    /// 點一下就顯示該格答案，像 Discord 的暴雷內容。只在教學關使用，避免玩家卡住。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class Spoiler : MonoBehaviour
    {
        public const string Prompt = "Click to reveal the answer.";
        public static readonly Vector2 Size = new Vector2(2.2f, 0.42f);

        public Block block;
        public TextMeshPro label;

        Mesh mesh;
        TMP_FontAsset englishFont;

        public bool Revealed { get; private set; }

        void OnEnable()
        {
            if (mesh == null)
            {
                mesh = new Mesh { hideFlags = HideFlags.DontSave };
                GetComponent<MeshFilter>().sharedMesh = mesh;
            }
            if (label != null && englishFont == null) englishFont = label.font;
            if (Application.isPlaying) GameSettings.Changed += Paint;
            Paint();
        }

        void OnDisable()
        {
            GameSettings.Changed -= Paint;
            if (mesh != null) DestroyImmediate(mesh);
        }

        void Update()
        {
            if (!Application.isPlaying || Revealed) return;
            var mouse = Mouse.current;
            var view = Camera.main;
            if (mouse == null || view == null || !mouse.leftButton.wasPressedThisFrame) return;
            Vector2 screen = mouse.position.ReadValue();
            Vector2 world = view.ScreenToWorldPoint(screen);
            if (Hud.IsPointerOverUi(screen) || RoomCover.Hides(world)) return;
            Vector2 local = world - (Vector2)transform.position;
            if (Mathf.Abs(local.x) <= Size.x / 2 && Mathf.Abs(local.y) <= Size.y / 2) Reveal();
        }

        public void Reveal()
        {
            Revealed = true;
            Paint();
        }

        void Paint()
        {
            var builder = new MeshBuilder();
            Art.Panel(builder, Size, Revealed ? Art.SpoilerOpen : Art.SpoilerBar);
            builder.Apply(mesh);
            if (label == null) return;
            if (Revealed && block != null)
            {
                if (englishFont != null) label.font = englishFont;
                label.text = block.answer;
            }
            // 編輯模式維持英文：場景存檔與編輯模式的測試都以英文為準
            else if (Application.isPlaying && englishFont != null) Localization.Apply(label, Prompt, englishFont);
            else label.text = Prompt;
            label.color = Revealed ? Art.Ink : Art.Paper;
        }
    }
}

using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lexiscript
{
    /// <summary>滑鼠移到方塊的色框上時，在畫面正上方顯示該顏色的名稱。</summary>
    public sealed class ColorNameLabel : MonoBehaviour
    {
        const float FadeSpeed = 10f, PaddingX = 28, Height = 56;

        CanvasGroup group;
        TextMeshProUGUI label;
        RectTransform rect;
        Board board;

        /// <summary>目前指著的顏色名稱；沒指著色框時是 null。</summary>
        public string ColorName { get; private set; }

        void Awake()
        {
            rect = (RectTransform)transform;
            Ui.Place(rect, new Vector2(0.5f, 1), new Vector2(0, -28), new Vector2(200, Height));
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0;
            group.blocksRaycasts = false;
            Ui.Fill(Ui.Image("Background", transform, Art.LabelBackground).rectTransform);
            label = Ui.Text("Label", transform, 32, Color.white);
            label.alignment = TextAlignmentOptions.Center;
            Ui.Fill(label.rectTransform);
        }

        void Update()
        {
            ColorName = Hovered()?.color.ToString();
            if (ColorName != null && label.text != ColorName)
            {
                label.text = ColorName;
                rect.sizeDelta = new Vector2(label.GetPreferredValues(ColorName).x + 2 * PaddingX, Height);
            }
            group.alpha = Mathf.MoveTowards(group.alpha, ColorName != null ? 1 : 0, FadeSpeed * Time.unscaledDeltaTime);
        }

        Block Hovered()
        {
            if (board == null) board = FindAnyObjectByType<Board>();
            var mouse = Mouse.current;
            var view = Camera.main;
            if (board == null || board.Blocks == null || mouse == null || view == null) return null;
            Vector2 screen = mouse.position.ReadValue();
            Vector2 world = view.ScreenToWorldPoint(screen);
            return Hud.IsPointerOverUi(screen) || RoomCover.Hides(world) ? null : board.BandAt(world);
        }
    }
}

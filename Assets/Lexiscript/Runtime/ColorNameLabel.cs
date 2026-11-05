using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lexiscript
{
    /// <summary>
    /// 滑鼠移到方塊的色框上時，在畫面正上方顯示該顏色的名稱。
    /// 名稱左右各有一組該顏色的直條（由外往內粗、粗、細，左右鏡像），不讀字也認得出顏色。
    /// </summary>
    public sealed class ColorNameLabel : MonoBehaviour
    {
        public const float Top = -28, Height = 72;
        const float FadeSpeed = 10f, FontSize = 40;
        const float BarHeight = 50, Thick = 7, Thin = 3, BarGap = 4, Inset = 18, TextGap = 16, PlatePadding = 3;
        const float GroupWidth = 2 * Thick + Thin + 2 * BarGap, PaddingX = Inset + GroupWidth + TextGap;
        const float HatchPitch = 6, HatchWidth = 2.5f;
        const float SlideSeconds = 0.18f, SlideDistance = 10;
        // 純黑、深紫在深色底上幾乎看不見，這類顏色的直條墊一塊淺色底板
        const float DarkGrayscale = 0.33f;

        static readonly float[] BarWidths = { Thick, Thick, Thin };

        CanvasGroup group;
        TextMeshProUGUI label;
        RectTransform rect;
        UiShape left, right;
        Board board;
        BlockColor shown;
        float slideStart = float.NegativeInfinity;

        /// <summary>目前指著的顏色名稱；沒指著色框時是 null。</summary>
        public string ColorName { get; private set; }
        /// <summary>兩側直條的顏色；無色是斜紋的灰。</summary>
        public Color BarColor { get; private set; }
        /// <summary>直條底下墊了淺色底板（深色在深底上才看得見）。</summary>
        public bool BarsOnPlate { get; private set; }
        /// <summary>名稱左邊那組直條。</summary>
        public UiShape LeftBars => left;
        /// <summary>名稱右邊那組直條，與左邊鏡像。</summary>
        public UiShape RightBars => right;

        void Awake()
        {
            rect = (RectTransform)transform;
            Ui.Place(rect, new Vector2(0.5f, 1), new Vector2(0, Top), new Vector2(200, Height));
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0;
            group.blocksRaycasts = false;
            Ui.Fill(Ui.Image("Background", transform, Art.LabelBackground).rectTransform);
            left = Bars("LeftBars", 0);
            right = Bars("RightBars", 1);
            label = Ui.Text("Label", transform, FontSize, Color.white);
            label.alignment = TextAlignmentOptions.Center;
            Ui.Fill(label.rectTransform);
        }

        /// <summary>貼著標籤左緣或右緣、垂直置中；圖形的原點在貼著的那一邊。</summary>
        UiShape Bars(string name, float side)
        {
            var shape = Ui.Shape(name, transform);
            Ui.Place(shape.rectTransform, new Vector2(side, 0.5f), Vector2.zero, new Vector2(GroupWidth, BarHeight));
            return shape;
        }

        void Update()
        {
            var block = Hovered();
            if (block != null && (ColorName == null || block.color != shown)) Show(block.color);
            ColorName = block?.color.ToString();
            group.alpha = Mathf.MoveTowards(group.alpha, ColorName != null ? 1 : 0, FadeSpeed * Time.unscaledDeltaTime);
            Slide();
        }

        void Show(BlockColor color)
        {
            shown = color;
            var name = color.ToString();
            label.text = name;
            rect.sizeDelta = new Vector2(label.GetPreferredValues(name).x + 2 * PaddingX, Height);

            bool colorless = color == BlockColor.Colorless;
            BarColor = colorless ? Art.Hatch : Art.Swatch(color);
            BarsOnPlate = !colorless && BarColor.grayscale < DarkGrayscale;
            left.Clear();
            right.Clear();
            if (BarsOnPlate)
                AddMirrored(Shapes.Rect(new Vector2(-PlatePadding, -BarHeight / 2 - PlatePadding), new Vector2(GroupWidth + PlatePadding, BarHeight / 2 + PlatePadding)), Art.FloorColor);
            float x = 0;
            foreach (var width in BarWidths)
            {
                var bar = Shapes.Rect(new Vector2(x, -BarHeight / 2), new Vector2(x + width, BarHeight / 2));
                if (colorless)
                    foreach (var stripe in Hatch(bar)) AddMirrored(stripe, BarColor);
                else
                    AddMirrored(bar, BarColor);
                x += width + BarGap;
            }
            slideStart = Time.unscaledTime;
        }

        /// <summary>左邊那組照畫；右邊那組左右翻過來，最粗的一樣在最外側。</summary>
        void AddMirrored(List<Vector2> polygon, Color color)
        {
            left.Add(polygon, color);
            right.Add(polygon.Select(point => new Vector2(-point.x, point.y)).ToList(), color);
        }

        /// <summary>無色的直條只畫 45° 斜紋、不填底，跟世界裡的無色方塊一樣透出底色。</summary>
        static IEnumerable<List<Vector2>> Hatch(List<Vector2> bar)
        {
            var across = new Vector2(1, -1).normalized;
            float min = bar.Min(corner => Vector2.Dot(corner, across));
            float max = bar.Max(corner => Vector2.Dot(corner, across));
            // 斜紋對齊整組的座標，相鄰直條的斜紋才連得起來
            for (float offset = Mathf.Floor(min / HatchPitch) * HatchPitch; offset < max; offset += HatchPitch)
            {
                var stripe = Shapes.Clip(Shapes.Clip(bar, across, offset + HatchWidth), -across, -offset);
                if (stripe.Count >= 3) yield return stripe;
            }
        }

        /// <summary>換顏色時直條從兩側往內滑入並淡入。</summary>
        void Slide()
        {
            float t = Ease.OutCubic((Time.unscaledTime - slideStart) / SlideSeconds);
            float offset = (1 - t) * SlideDistance;
            left.rectTransform.anchoredPosition = new Vector2(Inset - offset, 0);
            right.rectTransform.anchoredPosition = new Vector2(offset - Inset, 0);
            left.color = right.color = new Color(1, 1, 1, t);
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

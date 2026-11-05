using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lexiscript
{
    /// <summary>
    /// 畫面上的介面：左上角的總答題數、正上方的顏色名稱與提示句、右上角的設定、右下角的筆記、左下角的聊天列，
    /// 全部在執行時用程式建立。鍵盤則由同物件上的 <see cref="TextInputHub"/> 分派給目前的打字焦點。
    /// </summary>
    [RequireComponent(typeof(TextInputHub))]
    public sealed class Hud : MonoBehaviour
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);

        // 筆記與聊天要能顯示中文，遊戲內建的 LiberationSans 沒有中文字，所以借用系統字型
        static readonly string[] CjkFamilies =
        {
            "Microsoft JhengHei UI", "Microsoft JhengHei", "PingFang TC", "Noto Sans CJK TC", "Noto Sans TC",
            "Microsoft YaHei UI", "Microsoft YaHei",
        };

        static TMP_FontAsset uiFont;

        public static Hud Instance { get; private set; }

        public RectTransform Root { get; private set; }
        public SolvedCounter Counter { get; private set; }
        public ColorNameLabel ColorLabel { get; private set; }
        public HintLabel Hint { get; private set; }
        public NotesPanel Notes { get; private set; }
        public ChatConsole Chat { get; private set; }
        public SettingsMenu Settings { get; private set; }

        /// <summary>介面用的字型：有系統中文字型就用它，否則退回 TMP 預設字型。</summary>
        public static TMP_FontAsset UiFont
        {
            get
            {
                if (uiFont == null) uiFont = LoadUiFont();
                return uiFont;
            }
        }

        void Awake()
        {
            Instance = this;
            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = 0.5f;
            Root = (RectTransform)canvasObject.transform;
            Counter = Ui.Add<SolvedCounter>("SolvedCounter", Root);
            ColorLabel = Ui.Add<ColorNameLabel>("ColorName", Root);
            Hint = Ui.Add<HintLabel>("Hint", Root);
            Chat = Ui.Add<ChatConsole>("Chat", Root);
            Notes = Ui.Add<NotesPanel>("Notes", Root);
            // 設定視窗要蓋住其他所有介面，所以最後建
            Settings = Ui.Add<SettingsMenu>("Settings", Root);
        }

        void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            ModalOpen = false;
        }

        /// <summary>
        /// 設定這類蓋住整個畫面的視窗開著：滑鼠點哪裡都算點在介面上，筆記等其他介面也不處理點擊。
        /// </summary>
        public static bool ModalOpen { get; set; }

        /// <summary>滑鼠在介面上：世界的點擊、顏色名稱與滾輪縮放都要讓給介面。</summary>
        public static bool IsPointerOverUi(Vector2 screen) =>
            ModalOpen || (Instance != null && (Instance.Notes.Covers(screen) || Instance.Chat.Covers(screen) || Instance.Settings.Covers(screen)));

        public static bool ConsumesScroll(Vector2 screen) =>
            ModalOpen || (Instance != null && (Instance.Notes.WantsScroll(screen) || Instance.Chat.WantsScroll(screen)));

        static TMP_FontAsset LoadUiFont()
        {
            foreach (var family in CjkFamilies)
            {
                var font = TMP_FontAsset.CreateFontAsset(family, "Regular");
                if (font == null) continue;
                font.name = family;
                return font;
            }
            return TMP_Settings.defaultFontAsset;
        }
    }

    /// <summary>用程式建 uGUI 物件的小工具。</summary>
    public static class Ui
    {
        public static T Add<T>(string name, Transform parent) where T : Component
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.AddComponent<T>();
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>畫平面圖形用的 <see cref="UiShape"/>；介面的點擊都自己判斷，所以不接收 raycast。</summary>
        public static UiShape Shape(string name, Transform parent)
        {
            var shape = Add<UiShape>(name, parent);
            shape.raycastTarget = false;
            return shape;
        }

        public static Image Image(string name, Transform parent, Color color)
        {
            var image = Add<Image>(name, parent);
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static TextMeshProUGUI Text(string name, Transform parent, float fontSize, Color color)
        {
            var text = Add<TextMeshProUGUI>(name, parent);
            text.font = Hud.UiFont;
            text.fontSize = fontSize;
            text.color = color;
            text.raycastTarget = false;
            // 玩家打的反斜線 n 要原樣顯示，不能被當成換行
            text.parseCtrlCharacters = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.margin = Vector4.zero;
            return text;
        }

        /// <summary>依父物件四邊內縮擺放。</summary>
        public static void Fill(RectTransform rect, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>以 anchor 為基準擺固定大小的矩形，pivot 與 anchor 相同。</summary>
        public static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        public static bool Contains(RectTransform rect, Vector2 screen) =>
            rect != null && rect.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(rect, screen, null);

        /// <summary>TMP rich text 跳脫：玩家打的 &lt; 不能被當成標籤。</summary>
        public static string Escape(char c) => c == '<' ? "<noparse><</noparse>" : c.ToString();

        public static string Escape(string text) => text.Replace("<", "<noparse><</noparse>");
    }
}

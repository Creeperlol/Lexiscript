using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Lexiscript
{
    /// <summary>
    /// 從標題進入遊戲的轉場：淡金、淺金、墨色三片斜切色塊依序由左往右掃過、蓋滿畫面，
    /// 載入下一個場景後再依序往右掃出去。物件跨場景保留，掃完就銷毀自己。
    /// </summary>
    public sealed class TitleTransition : MonoBehaviour
    {
        public const float SweepSeconds = 0.5f, Stagger = 0.08f, HoldSeconds = 0.15f;
        /// <summary>斜邊上下兩端的水平差，以畫面高度為單位。</summary>
        const float Slant = 0.38f;
        /// <summary>由下往上疊：最後蓋上來的墨色在最上面，掃出時最先離開。</summary>
        static readonly Color[] Layers = { Art.PaleGold, Art.Gold, Art.Ink };

        string scene;
        RectTransform root;
        UiShape slabs, mark;
        TextMeshProUGUI label;
        float started, revealStarted = -1;
        bool loading, revealPending;

        /// <summary>正在播的轉場；沒有時是 null。</summary>
        public static TitleTransition Current { get; private set; }

        /// <summary>三片色塊全部掃過（蓋滿，或完全露出）所需的秒數。</summary>
        public static float SweepTotal => (Layers.Length - 1) * Stagger + SweepSeconds;

        public Canvas Canvas { get; private set; }

        /// <summary>開始轉場；畫面蓋滿後載入 scene。</summary>
        public static TitleTransition Play(string scene)
        {
            var go = new GameObject("Transition");
            DontDestroyOnLoad(go);
            var transition = go.AddComponent<TitleTransition>();
            transition.scene = scene;
            return transition;
        }

        /// <summary>
        /// 各層色塊（由下往上）的多邊形。cover、reveal 是開始蓋上、開始掃出後經過的秒數，
        /// size 是 Canvas 的大小（座標以畫面中心為原點）。
        /// </summary>
        public static List<List<Vector2>> Slabs(float cover, float reveal, Vector2 size)
        {
            float left = -size.x / 2, right = size.x / 2, bottom = -size.y / 2, top = size.y / 2;
            float slant = Slant * size.y;
            // 斜邊上端比下端多往右 slant，下端從 left - slant 出發，整片才會從畫面外進來
            float from = left - slant;
            var slabs = new List<List<Vector2>>();
            for (int i = 0; i < Layers.Length; i++)
            {
                float lead = Mathf.Lerp(from, right, Ease.InOutCubic((cover - i * Stagger) / SweepSeconds));
                float trail = Mathf.Lerp(from, right, Ease.InOutCubic((reveal - (Layers.Length - 1 - i) * Stagger) / SweepSeconds));
                slabs.Add(new List<Vector2>
                {
                    new Vector2(trail, bottom), new Vector2(lead, bottom), new Vector2(lead + slant, top), new Vector2(trail + slant, top),
                });
            }
            return slabs;
        }

        void Awake()
        {
            Current = this;
            started = Time.unscaledTime;
            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            Canvas = canvasObject.GetComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // 要蓋過下一個場景的介面（Hud 是 100）
            Canvas.sortingOrder = 1000;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = TitleScreen.DesignSize;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            root = (RectTransform)canvasObject.transform;
            slabs = Ui.Shape("Slabs", root);
            label = Ui.Text("Label", root, 22, Art.Paper);
            label.font = Fonts.Display;
            label.characterSpacing = 8f / 22 * 100;
            label.alignment = TextAlignmentOptions.Baseline;
            label.text = "NOW LOADING";
            label.rectTransform.sizeDelta = new Vector2(600, 40);
            label.rectTransform.anchoredPosition = new Vector2(0, -8);
            label.alpha = 0;
            mark = Ui.Shape("Mark", root);
        }

        void Update()
        {
            float now = Time.unscaledTime;
            if (revealPending)
            {
                // 從載入完的第一幀開始計時，載入花掉的時間不會讓掃出動畫一開始就跳掉一段
                revealPending = false;
                revealStarted = now;
            }
            float cover = now - started, reveal = revealStarted < 0 ? 0 : now - revealStarted;
            Draw(cover, reveal);
            if (!loading && cover >= SweepTotal + HoldSeconds)
            {
                loading = true;
                SceneManager.sceneLoaded += OnSceneLoaded;
                SceneManager.LoadScene(scene);
            }
            else if (revealStarted >= 0 && reveal >= SweepTotal)
            {
                Destroy(gameObject);
            }
        }

        void Draw(float cover, float reveal)
        {
            var polygons = Slabs(cover, reveal, root.rect.size);
            slabs.Clear();
            for (int i = 0; i < polygons.Count; i++) slabs.Add(polygons[i], Layers[i]);
            // 蓋滿前一刻才出現的載入字樣，一開始掃出就收掉
            float shown = Ease.OutCubic((cover - SweepTotal + 0.1f) / 0.15f) * (1 - Ease.OutCubic(reveal / 0.12f));
            label.alpha = shown;
            mark.Clear();
            mark.Add(Shapes.Rect(new Vector2(-48 * shown, -34), new Vector2(48 * shown, -30)), Art.Gold);
        }

        void OnSceneLoaded(Scene loaded, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            revealPending = true;
        }

        void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Current == this) Current = null;
        }
    }
}

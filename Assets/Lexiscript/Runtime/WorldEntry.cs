using UnityEngine;
using UnityEngine.UI;

namespace Lexiscript
{
    /// <summary>
    /// 教學出口到大廳之間那座橋的演出，只在第一次走過時播：越往橋中間走，畫面四周越暗、鏡頭越拉近；
    /// 一踏進大廳，暗角退掉、鏡頭先拉得比平常遠一點再回到原本的大小，有豁然開朗的感覺。
    /// 播過之後記在存檔裡；/refreshanimation 會清掉這個記錄。走進大廳也會記下「到過大地圖」，傳送鈕從此出現。
    /// </summary>
    public sealed class WorldEntry : MonoBehaviour
    {
        // 走到橋的這個比例時暗到最深、拉到最近
        const float FullDepth = 0.65f, Darkest = 0.55f, Closest = 0.66f;
        const float ReleaseSeconds = 2.2f, Overshoot = 1.28f, FlashAlpha = 0.22f, SettleSeconds = 0.6f;

        public Rect bridge, hall;

        static WorldEntry instance;
        Transform player;
        CameraZoom zoom;
        Image shade, flash;
        UiShape vignette;
        float intensity, releasedAt = float.NegativeInfinity;
        bool crossing;

        /// <summary>目前的暗度，0 是正常、1 是最暗。</summary>
        public float Intensity => intensity;
        /// <summary>正在播踏進大廳的那一段。</summary>
        public bool Releasing => Time.time - releasedAt < ReleaseSeconds;

        /// <summary>讓下次過橋時再播一次演出；回傳場景裡有沒有這座橋。</summary>
        public static bool Refresh()
        {
            if (instance == null) return false;
            if (GameProgress.Current != null) GameProgress.Current.BridgeIntroPlayed = false;
            instance.crossing = false;
            return true;
        }

        void Awake() => instance = this;

        void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        void Start()
        {
            player = FindAnyObjectByType<Player>()?.transform;
            zoom = Camera.main != null ? Camera.main.GetComponent<CameraZoom>() : null;
            BuildOverlay();
        }

        bool Played => GameProgress.Current == null || GameProgress.Current.BridgeIntroPlayed;

        void Update()
        {
            if (player == null) return;
            Vector2 at = player.position;
            if (hall.Contains(at) && GameProgress.Current != null) GameProgress.Current.WorldReached = true;

            float target = 0;
            if (!Played && bridge.Contains(at))
            {
                crossing = true;
                target = Mathf.Clamp01((at.x - bridge.xMin) / (bridge.width * FullDepth));
            }
            if (crossing && hall.Contains(at))
            {
                crossing = false;
                releasedAt = Time.time;
                if (GameProgress.Current != null) GameProgress.Current.BridgeIntroPlayed = true;
            }
            // 走回教學那一頭就算沒走完，下次過橋重新開始
            if (crossing && !bridge.Contains(at) && !hall.Contains(at)) crossing = false;

            float released = (Time.time - releasedAt) / ReleaseSeconds;
            if (released < 1)
            {
                // 暗角很快退掉，鏡頭先拉遠超過原本的大小，再慢慢回來
                intensity = Mathf.MoveTowards(intensity, 0, Time.deltaTime / 0.35f);
                float open = Ease.OutCubic(Mathf.Clamp01(released * ReleaseSeconds / SettleSeconds));
                float back = Ease.InOutCubic(Mathf.Clamp01((released * ReleaseSeconds - SettleSeconds) / (ReleaseSeconds - SettleSeconds)));
                if (zoom != null) zoom.Multiplier = Mathf.Lerp(Mathf.Lerp(Closest, Overshoot, open), 1, back);
                flash.color = new Color(1, 1, 1, FlashAlpha * (1 - Ease.OutCubic(Mathf.Clamp01(released * 3))));
            }
            else
            {
                intensity = Mathf.MoveTowards(intensity, target, Time.deltaTime / 0.5f);
                if (zoom != null) zoom.Multiplier = Mathf.Lerp(1, Closest, Ease.InOutCubic(intensity));
                flash.color = Color.clear;
            }
            shade.color = new Color(Art.Ink.r, Art.Ink.g, Art.Ink.b, Darkest * 0.45f * Ease.InOutCubic(intensity));
            vignette.color = new Color(1, 1, 1, Ease.InOutCubic(intensity));
        }

        /// <summary>蓋在世界上、介面底下的暗幕：整片淡淡的墨色，加上四周更深的暗角。</summary>
        void BuildOverlay()
        {
            var canvasObject = new GameObject("Entry Overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // 介面（100）之下，答題數、筆記照常看得清楚
            canvas.sortingOrder = 90;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = Hud.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var root = (RectTransform)canvasObject.transform;
            shade = Ui.Image("Shade", root, Color.clear);
            Ui.Fill(shade.rectTransform);
            vignette = Ui.Shape("Vignette", root);
            Ui.Fill(vignette.rectTransform);
            // 由外往內一圈圈疊上去，越外圈疊得越多層，邊緣最暗
            var screen = Hud.ReferenceResolution;
            for (int ring = 0; ring < 6; ring++)
            {
                float inset = 40 + ring * 70;
                var outer = new Rect(-screen, screen * 2);
                var inner = new Rect(-screen / 2 + Vector2.one * inset, screen - Vector2.one * 2 * inset);
                var color = new Color(Art.Ink.r, Art.Ink.g, Art.Ink.b, Darkest * 0.18f);
                vignette.Add(Shapes.Rect(outer.min, new Vector2(outer.xMax, inner.yMin)), color);
                vignette.Add(Shapes.Rect(new Vector2(outer.xMin, inner.yMax), outer.max), color);
                vignette.Add(Shapes.Rect(new Vector2(outer.xMin, inner.yMin), new Vector2(inner.xMin, inner.yMax)), color);
                vignette.Add(Shapes.Rect(new Vector2(inner.xMax, inner.yMin), new Vector2(outer.xMax, inner.yMax)), color);
            }
            vignette.color = Color.clear;
            flash = Ui.Image("Flash", root, Color.clear);
            Ui.Fill(flash.rectTransform);
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lexiscript
{
    /// <summary>
    /// 右下角筆記旁的傳送鈕與傳送視窗。第一次走進大地圖之後才出現。視窗列出所有傳送點，
    /// 已解鎖（所在區域的遮罩掀開了）的點一下就傳送；上方的開關決定傳送前要不要再確認一次。
    /// 開著時跟設定視窗一樣是蓋住整個畫面的 <see cref="Hud.ModalOpen"/>，拿走打字焦點擋住移動，Esc、點視窗外或關閉鈕關閉。
    /// 外觀照設定視窗的平面設計：深色斜切面板、淺金強調、條碼與斜紋。
    /// </summary>
    // 跟設定視窗同一套點擊處理：開關鈕在 Update，視窗裡的點擊留到 LateUpdate，同一下點擊才不會穿透到後面
    [DefaultExecutionOrder(50)]
    public sealed class TeleportMenu : MonoBehaviour, ITextTarget
    {
        sealed class Label
        {
            public TextMeshProUGUI Text;
            public string English, Chinese;
            public float Spacing;

            public void Apply()
            {
                var shown = Localization.Pick(English, Chinese);
                bool cjk = shown.Any(c => c >= 0x2E80);
                Text.font = cjk ? Hud.UiFont : Fonts.Display;
                Text.fontStyle = cjk ? FontStyles.Bold : FontStyles.Normal;
                Text.characterSpacing = cjk ? Spacing / 3 : Spacing;
                Text.text = shown;
            }
        }

        sealed class Row
        {
            public TeleportPoint Point;
            public RectTransform Rect;
            public CanvasGroup Group;
            public UiShape Hover, Locked, Arrow;
            public Label Name, Status;
            public float Glow;
        }

        public static readonly Vector2 ButtonSize = new Vector2(168, 52);
        const float Margin = 24, ButtonGap = 12, Width = 760, Header = 196, RowHeight = 72, Footer = 40, PadX = 44;
        const float FadeSeconds = 0.25f, OpenSeconds = 0.32f, CloseSeconds = 0.16f, HoverSeconds = 0.12f;
        const float TravelOutSeconds = 0.16f, TravelInSeconds = 0.36f, ShiftSeconds = 0.2f;
        static readonly Vector2 DialogSize = new Vector2(640, 280);
        static readonly Color Body = new Color(0.118f, 0.129f, 0.157f, 0.97f);
        static readonly Color Muted = HudStyle.WithAlpha(Art.Paper, 0.55f), Line = HudStyle.WithAlpha(Art.Paper, 0.16f);

        readonly List<Label> labels = new List<Label>();
        readonly List<Row> rows = new List<Row>();
        RectTransform button, buttonVisual, window, panel, dialog, confirm, close, toggle, cancel, go;
        CanvasGroup buttonGroup, windowGroup, confirmGroup;
        UiShape buttonLine, buttonIcon, toggleKnob, travelShade;
        Label toggleState, message;
        TeleportPoint pending, travelling;
        Vector2 pointer;
        bool pointing, clicked;
        float shown, buttonHover, openness, confirmness, travelAt = float.NegativeInfinity, shift;

        public bool IsOpen { get; private set; }
        public bool Confirming { get; private set; }
        public bool Available => GameProgress.Current != null && GameProgress.Current.WorldReached && rows.Count > 0;
        public RectTransform Button => button;
        public RectTransform Toggle => toggle;
        public RectTransform ConfirmButton => go;
        public RectTransform CancelButton => cancel;
        public bool WantsIme => false;

        /// <summary>列表裡第 index 個地點那一列。</summary>
        public RectTransform RowOf(int index) => rows[index].Rect;

        public bool Covers(Vector2 screen) => buttonGroup.alpha > 0.5f && Ui.Contains(button, screen);

        void Awake()
        {
            Ui.Fill((RectTransform)transform);
            BuildButton();
            BuildShade();
        }

        void Start()
        {
            // 傳送點是場景裡的物件，Hud 在 Awake 時不一定都已經建好
            var points = TeleportPoint.All();
            BuildWindow(points);
            BuildConfirm();
            ApplyLabels();
            window.gameObject.SetActive(false);
            confirm.gameObject.SetActive(false);
        }

        void OnEnable() => GameSettings.Changed += ApplyLabels;

        void OnDisable() => GameSettings.Changed -= ApplyLabels;

        void OnDestroy() => TextFocus.Release(this);

        public void Open()
        {
            if (IsOpen || !Available) return;
            IsOpen = true;
            Confirming = false;
            window.gameObject.SetActive(true);
            Hud.ModalOpen = true;
            TextFocus.Set(this);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            Confirming = false;
            Hud.ModalOpen = false;
            TextFocus.Release(this);
        }

        /// <summary>點了某個地點：要確認就先跳確認視窗，否則直接出發。</summary>
        public void Choose(TeleportPoint point)
        {
            if (point == null || !point.Unlocked) return;
            if (GameSettings.ConfirmTeleport)
            {
                pending = point;
                message.English = $"Teleport to {point.english}?";
                message.Chinese = $"即將前往{point.chinese}，是否確定？";
                message.Apply();
                Confirming = true;
                confirm.gameObject.SetActive(true);
                return;
            }
            Travel(point);
        }

        void Travel(TeleportPoint point)
        {
            Close();
            travelling = point;
            travelAt = Time.unscaledTime;
        }

        public void OnText(char c) { }

        public void OnComposition(string composition) { }

        public void OnKey(Key key, bool shift, bool ctrl)
        {
            if (key != Key.Escape) return;
            if (Confirming) Confirming = false;
            else Close();
        }

        public void OnFocusChanged(bool focused)
        {
            if (!focused) Close();
        }

        void Update()
        {
            var mouse = Mouse.current;
            pointing = mouse != null;
            clicked = pointing && mouse.leftButton.wasPressedThisFrame;
            if (pointing) pointer = mouse.position.ReadValue();
            if (clicked && !IsOpen && !Hud.ModalOpen && Covers(pointer))
            {
                Open();
                clicked = false;
            }
        }

        void LateUpdate()
        {
            if (clicked && IsOpen) Click(pointer);
            clicked = false;
            AnimateButton();
            if (window != null && window.gameObject.activeSelf) AnimateWindow();
            AnimateTravel();
        }

        void Click(Vector2 screen)
        {
            if (Confirming)
            {
                if (Ui.Contains(go, screen)) Travel(pending);
                else if (Ui.Contains(cancel, screen) || !Ui.Contains(dialog, screen)) Confirming = false;
                return;
            }
            if (Ui.Contains(close, screen) || !Ui.Contains(panel, screen)) Close();
            else if (Ui.Contains(toggle, screen)) GameSettings.ConfirmTeleport = !GameSettings.ConfirmTeleport;
            else
                foreach (var row in rows)
                    if (Ui.Contains(row.Rect, screen)) Choose(row.Point);
        }

        static float Step(float value, bool on, float inSeconds, float outSeconds) =>
            Mathf.MoveTowards(value, on ? 1 : 0, Time.unscaledDeltaTime / (on ? inSeconds : outSeconds));

        bool Hovered(RectTransform rect) => pointing && Ui.Contains(rect, pointer);

        void AnimateButton()
        {
            shown = Step(shown, Available, FadeSeconds * 2, FadeSeconds);
            float eased = Ease.OutCubic(shown);
            buttonGroup.alpha = eased;
            buttonVisual.anchoredPosition = new Vector2(0, -24 * (1 - eased));

            // 筆記展開時會蓋到傳送鈕原本的位置，傳送鈕就讓到筆記左邊
            var notes = Hud.Instance != null ? (RectTransform)Hud.Instance.Notes.transform : null;
            float x = -(Margin + NotesPanel.MinimizedWidth + ButtonGap);
            if (notes != null && !Hud.Instance.Notes.Minimized)
            {
                float notesLeft = notes.anchoredPosition.x - notes.sizeDelta.x, notesBottom = notes.anchoredPosition.y;
                if (notesBottom < Margin + ButtonSize.y && notesBottom + notes.sizeDelta.y > Margin) x = Mathf.Min(x, notesLeft - ButtonGap);
            }
            shift = Mathf.Lerp(shift == 0 ? x : shift, x, 1 - Mathf.Exp(-Time.unscaledDeltaTime / ShiftSeconds * 4));
            button.anchoredPosition = new Vector2(shift, Margin);

            buttonHover = Step(buttonHover, Available && !IsOpen && Hovered(button), HoverSeconds, HoverSeconds);
            float hover = Ease.OutCubic(buttonHover);
            buttonLine.rectTransform.localScale = new Vector3(Mathf.Max(0.25f, hover), 1, 1);
            buttonIcon.color = Color.Lerp(Art.Paper, Art.Gold, hover);
        }

        void AnimateWindow()
        {
            openness = Step(openness, IsOpen, OpenSeconds, CloseSeconds);
            confirmness = Step(confirmness, Confirming, OpenSeconds, CloseSeconds);
            windowGroup.alpha = Ease.OutCubic(openness);
            panel.anchoredPosition = new Vector2(0, -28 * (1 - Ease.OutQuint(openness)));
            bool interactive = IsOpen && !Confirming;
            for (int k = 0; k < rows.Count; k++)
            {
                var row = rows[k];
                bool unlocked = row.Point != null && row.Point.Unlocked;
                float local = Mathf.Clamp01((openness - 0.08f * k) / 0.6f);
                row.Group.alpha = Ease.OutCubic(local) * (unlocked ? 1 : 0.42f);
                row.Rect.anchoredPosition = new Vector2(PadX - 30 * (1 - Ease.OutCubic(local)), row.Rect.anchoredPosition.y);
                row.Locked.color = unlocked ? Color.clear : Color.white;
                row.Arrow.color = unlocked ? Color.white : Color.clear;
                row.Glow = Step(row.Glow, interactive && unlocked && Hovered(row.Rect), HoverSeconds, HoverSeconds);
                row.Hover.color = new Color(1, 1, 1, Ease.OutCubic(row.Glow));
                string english = unlocked ? $"UNLOCKED  //  NAV-{k:00}" : "LOCKED", chinese = unlocked ? $"已解鎖  //  NAV-{k:00}" : "未解鎖";
                if (row.Status.English != english)
                {
                    row.Status.English = english;
                    row.Status.Chinese = chinese;
                    row.Status.Apply();
                }
            }
            bool on = GameSettings.ConfirmTeleport;
            float follow = 1 - Mathf.Exp(-Time.unscaledDeltaTime * 20);
            var knob = toggleKnob.rectTransform;
            knob.anchoredPosition = Vector2.Lerp(knob.anchoredPosition, new Vector2(on ? 52 : 4, -4), follow);
            toggleKnob.color = on ? Art.Gold : HudStyle.WithAlpha(Art.Paper, 0.5f);
            var state = toggleState.Text.rectTransform;
            state.anchoredPosition = Vector2.Lerp(state.anchoredPosition, new Vector2(on ? 0 : 48, 0), follow);
            if (toggleState.English != (on ? "ON" : "OFF"))
            {
                toggleState.English = on ? "ON" : "OFF";
                toggleState.Chinese = on ? "開" : "關";
                toggleState.Apply();
            }

            if (confirm.gameObject.activeSelf)
            {
                confirmGroup.alpha = Ease.OutCubic(confirmness);
                float pop = Mathf.LerpUnclamped(0.92f, 1, Ease.OutBack(confirmness));
                dialog.localScale = new Vector3(pop, pop, 1);
                if (!Confirming && confirmness <= 0) confirm.gameObject.SetActive(false);
            }
            if (!IsOpen && openness <= 0) window.gameObject.SetActive(false);
        }

        /// <summary>傳送：畫面很快暗下來，玩家換到目的地，再亮回來。</summary>
        void AnimateTravel()
        {
            float t = Time.unscaledTime - travelAt;
            if (travelling != null && t >= TravelOutSeconds)
            {
                var player = FindAnyObjectByType<Player>();
                if (player != null) travelling.Send(player);
                travelling = null;
            }
            float dark = t < TravelOutSeconds ? Ease.InCubic(t / TravelOutSeconds) : 1 - Ease.OutCubic((t - TravelOutSeconds) / TravelInSeconds);
            travelShade.color = new Color(1, 1, 1, Mathf.Clamp01(dark));
        }

        void ApplyLabels()
        {
            foreach (var label in labels) label.Apply();
        }

        void BuildButton()
        {
            button = Ui.Rect("Button", transform);
            Ui.Place(button, new Vector2(1, 0), new Vector2(-(Margin + NotesPanel.MinimizedWidth + ButtonGap), Margin), ButtonSize);
            buttonVisual = Ui.Rect("Visual", button);
            Ui.Fill(buttonVisual);
            buttonGroup = buttonVisual.gameObject.AddComponent<CanvasGroup>();
            buttonGroup.blocksRaycasts = false;
            buttonGroup.alpha = 0;
            float w = ButtonSize.x, h = ButtonSize.y;

            var body = Shape("Body", buttonVisual, 0, 0, w, h);
            body.Add(HudStyle.Chamfered(Box(0, 0, w, h), 12, 0, 0, 0), HudStyle.WithAlpha(Art.Ink, 0.9f));
            body.Add(Shapes.Rect(P(0, h), P(4, 0)), Art.Gold);
            body.AddAll(HudStyle.Barcode(P(w - 40, h - 14), 8, "21312", 2), HudStyle.WithAlpha(Art.Paper, 0.35f));
            buttonLine = Shape("Line", buttonVisual, 16, h - 6, w - 32, 2);
            buttonLine.rectTransform.pivot = new Vector2(0, 0.5f);
            buttonLine.Add(Shapes.Rect(P(0, 2), P(w - 32, 0)), Art.Gold);
            buttonIcon = Shape("Icon", buttonVisual, 0, 0, w, h);
            // 傳送台的樣子：外圈與中間的菱形
            var center = P(34, h / 2);
            buttonIcon.AddAll(Ring(center, 13, 2.5f), Color.white);
            buttonIcon.Add(Shapes.Diamond(center, 6), Color.white);
            var label = AddLabel(buttonVisual, 56, 0, w - 64, h, 20, Art.Paper, TextAlignmentOptions.MidlineLeft, "WARP", "傳送", 14);
            label.Text.raycastTarget = false;
        }

        void BuildShade()
        {
            travelShade = Ui.Shape("TravelShade", transform);
            Ui.Fill(travelShade.rectTransform);
            travelShade.Add(Shapes.Rect(new Vector2(-4000, -4000), new Vector2(4000, 4000)), Art.Ink);
            travelShade.color = Color.clear;
        }

        void BuildWindow(TeleportPoint[] points)
        {
            window = Ui.Rect("Window", transform);
            Ui.Fill(window);
            windowGroup = window.gameObject.AddComponent<CanvasGroup>();
            windowGroup.blocksRaycasts = false;
            Ui.Fill(Ui.Image("Overlay", window, HudStyle.WithAlpha(Art.Ink, 0.62f)).rectTransform);
            float h = Header + points.Length * RowHeight + Footer, w = Width;
            panel = Ui.Rect("Panel", window);
            Ui.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h));
            var content = At(Ui.Rect("Content", panel), 0, 0, w, h);

            var body = Shape("Body", content, 0, 0, w, h);
            body.Add(HudStyle.Chamfered(Box(10, 10, w, h), 32, 0, 32, 0), HudStyle.WithAlpha(Art.Ink, 0.45f));
            body.Add(HudStyle.Chamfered(Box(0, 0, w, h), 32, 0, 32, 0), Body);
            body.AddAll(HudStyle.Frame(Box(10, 10, w - 20, h - 20), 26, 0, 26, 0, 1), HudStyle.WithAlpha(Art.Paper, 0.07f));
            body.Add(Shapes.Rect(P(0, 164), P(6, 40)), Art.Gold);
            body.Add(Shapes.Rect(P(PadX, Header - 22), P(w - PadX, Header - 24)), Line);
            body.Add(Shapes.Rect(P(PadX, Header - 21), P(PadX + 72, Header - 25)), Art.Gold);
            body.AddAll(HudStyle.Hatch(Shapes.Rect(P(w - 330, 44), P(w - 170, 30)), 10, 4), HudStyle.WithAlpha(Art.Paper, 0.12f));
            body.AddAll(HudStyle.Bracket(P(w + 14, -14), new Vector2(-36, 0), new Vector2(0, -36), 3), Art.Gold);
            body.AddAll(HudStyle.Bracket(P(-14, h + 14), new Vector2(36, 0), new Vector2(0, 36), 3), Art.Gold);

            Tag(content, PadX, 16, 400, 22, 13, HudStyle.WithAlpha(Art.Paper, 0.45f), "LEXISCRIPT // NAV.LINK", 30);
            AddLabel(content, PadX, 56, 420, 62, 46, Art.Paper, TextAlignmentOptions.Left, "TELEPORT", "傳送", 10);
            AddLabel(content, PadX, 120, 420, 26, 17, Muted, TextAlignmentOptions.Left, "Travel to any area you have unlocked.", "前往任何已經解鎖的區域。", 1);

            close = At(Ui.Rect("Close", content), w - 24 - 112, 14, 112, 40);
            Tag(close, 16, 0, 56, 40, 13, Muted, "ESC", 24);
            var cross = Shape("Icon", close, 0, 0, 112, 40);
            cross.AddAll(HudStyle.Cross(P(86, 20), 8, 2.5f), Art.Paper);

            // 二次確認的開關：說明字在左，右邊一顆滑動的方塊
            AddLabel(content, w - PadX - 330, 104, 220, 32, 16, Muted, TextAlignmentOptions.Right, "CONFIRM FIRST", "傳送前確認", 6);
            toggle = At(Ui.Rect("Toggle", content), w - PadX - 100, 100, 100, 40);
            var track = Shape("Track", toggle, 0, 0, 100, 40);
            track.AddAll(HudStyle.Frame(Box(0, 0, 100, 40), 8, 0, 8, 0, 2), HudStyle.WithAlpha(Art.Paper, 0.3f));
            toggleKnob = Shape("Knob", toggle, 4, 4, 44, 32);
            toggleKnob.Add(HudStyle.Chamfered(Box(0, 0, 44, 32), 6, 0, 6, 0), Color.white);
            toggleState = AddLabel(toggle, 0, 0, 52, 40, 15, Art.Paper, TextAlignmentOptions.Center, "ON", "開", 6);

            for (int i = 0; i < points.Length; i++) rows.Add(BuildRow(content, points[i], i, Header + i * RowHeight));
        }

        Row BuildRow(RectTransform content, TeleportPoint point, int index, float top)
        {
            float w = Width - 2 * PadX, h = RowHeight - 10;
            var rect = At(Ui.Rect("Row", content), PadX, top, w, h);
            var row = new Row { Point = point, Rect = rect };
            row.Group = rect.gameObject.AddComponent<CanvasGroup>();
            row.Group.blocksRaycasts = false;
            var frame = Shape("Frame", rect, 0, 0, w, h);
            frame.AddAll(HudStyle.Frame(Box(0, 0, w, h), 10, 0, 10, 0, 1), HudStyle.WithAlpha(Art.Paper, 0.18f));
            row.Hover = Shape("Hover", rect, 0, 0, w, h);
            row.Hover.Add(HudStyle.Chamfered(Box(0, 0, w, h), 10, 0, 10, 0), HudStyle.WithAlpha(Art.Paper, 0.07f));
            row.Hover.Add(Shapes.Rect(P(0, h), P(4, 0)), Art.Gold);
            row.Hover.color = Color.clear;
            row.Locked = Shape("Locked", rect, 0, 0, w, h);
            row.Locked.AddAll(HudStyle.Hatch(HudStyle.Chamfered(Box(2, 2, w - 4, h - 4), 9, 0, 9, 0), 12, 3), HudStyle.WithAlpha(Art.Paper, 0.08f));
            Lock(row.Locked, P(w - 40, h / 2));
            Tag(rect, 20, 0, 56, h, 24, Art.Gold, (index + 1).ToString("00"), 4);
            var tick = Shape("Tick", rect, 0, 0, w, h);
            tick.Add(Shapes.Rect(P(70, h - 14), P(72, 14)), HudStyle.WithAlpha(Art.Paper, 0.18f));
            // 已解鎖的列右邊是往右的箭頭，沒解鎖的換成鎖頭
            row.Arrow = Shape("Arrow", rect, 0, 0, w, h);
            row.Arrow.Add(new List<Vector2> { P(w - 52, h / 2 - 8), P(w - 40, h / 2), P(w - 52, h / 2 + 8) }, HudStyle.WithAlpha(Art.Gold, 0.9f));
            row.Name = AddLabel(rect, 90, 6, w - 180, 32, 24, Art.Paper, TextAlignmentOptions.Left, point.english.ToUpperInvariant(), point.chinese, 6);
            row.Status = AddLabel(rect, 90, 38, w - 180, 20, 13, Muted, TextAlignmentOptions.Left, "", "", 4);
            return row;
        }

        void BuildConfirm()
        {
            confirm = Ui.Rect("Confirm", window);
            Ui.Fill(confirm);
            confirmGroup = confirm.gameObject.AddComponent<CanvasGroup>();
            confirmGroup.blocksRaycasts = false;
            Ui.Fill(Ui.Image("Overlay", confirm, HudStyle.WithAlpha(Art.Ink, 0.5f)).rectTransform);
            dialog = Ui.Rect("Dialog", confirm);
            Ui.Place(dialog, new Vector2(0.5f, 0.5f), Vector2.zero, DialogSize);
            float w = DialogSize.x, h = DialogSize.y;
            var content = At(Ui.Rect("Content", dialog), 0, 0, w, h);
            var body = Shape("Body", content, 0, 0, w, h);
            body.Add(HudStyle.Chamfered(Box(10, 10, w, h), 24, 0, 24, 0), HudStyle.WithAlpha(Art.Ink, 0.45f));
            body.Add(HudStyle.Chamfered(Box(0, 0, w, h), 24, 0, 24, 0), new Color(0.137f, 0.149f, 0.18f, 0.99f));
            body.Add(HudStyle.Chamfered(Box(0, 0, w, 56), 24, 0, 0, 0), Art.Gold);
            body.AddAll(HudStyle.Hatch(Shapes.Rect(P(w - 240, 56), P(w, 0)), 14, 6), HudStyle.WithAlpha(Art.Ink, 0.18f));
            var icon = P(40, 28);
            body.AddAll(Ring(icon, 14, 3), Art.Ink);
            body.Add(Shapes.Diamond(icon, 6), Art.Ink);
            AddLabel(content, 70, 12, w - 120, 32, 22, Art.Ink, TextAlignmentOptions.Left, "TELEPORT", "傳送", 8);
            message = AddLabel(content, PadX, 84, w - 2 * PadX, 80, 24, Art.Paper, TextAlignmentOptions.TopLeft, "", "", 0);
            message.Text.textWrappingMode = TextWrappingModes.Normal;
            go = DialogButton(content, "Go", w - PadX - 200, 200, true, "GO", "出發");
            cancel = DialogButton(content, "Cancel", w - PadX - 200 - 16 - 170, 170, false, "CANCEL", "取消");
        }

        RectTransform DialogButton(RectTransform content, string name, float x, float width, bool primary, string english, string chinese)
        {
            const float Height = 52;
            var rect = At(Ui.Rect(name, content), x, DialogSize.y - 36 - Height, width, Height);
            var frame = Shape("Frame", rect, 0, 0, width, Height);
            if (primary) frame.Add(HudStyle.Chamfered(Box(0, 0, width, Height), 10, 0, 10, 0), Art.Gold);
            else frame.AddAll(HudStyle.Frame(Box(0, 0, width, Height), 10, 0, 10, 0, 2), HudStyle.WithAlpha(Art.Paper, 0.4f));
            AddLabel(rect, 0, 0, width, Height, 20, primary ? Art.Ink : Art.Paper, TextAlignmentOptions.Center, english, chinese, 10);
            return rect;
        }

        /// <summary>小鎖頭：鎖環與鎖身。</summary>
        static void Lock(UiShape shape, Vector2 center)
        {
            var color = HudStyle.WithAlpha(Art.Paper, 0.7f);
            shape.Add(Shapes.Rect(center + new Vector2(-9, -10), center + new Vector2(9, 3)), color);
            shape.Add(Shapes.Rect(center + new Vector2(-7, 3), center + new Vector2(-4, 11)), color);
            shape.Add(Shapes.Rect(center + new Vector2(4, 3), center + new Vector2(7, 11)), color);
            shape.Add(Shapes.Rect(center + new Vector2(-7, 9), center + new Vector2(7, 12)), color);
        }

        /// <summary>圓環；多邊形要是凸的，所以拆成一圈小四邊形。</summary>
        static IEnumerable<List<Vector2>> Ring(Vector2 center, float radius, float thickness)
        {
            const int steps = 28;
            for (int i = 0; i < steps; i++)
            {
                float from = Mathf.PI * 2 * i / steps, to = Mathf.PI * 2 * (i + 1) / steps;
                Vector2 a = new Vector2(Mathf.Cos(from), Mathf.Sin(from)), b = new Vector2(Mathf.Cos(to), Mathf.Sin(to));
                yield return new List<Vector2> { center + a * (radius - thickness), center + a * radius, center + b * radius, center + b * (radius - thickness) };
            }
        }

        Label AddLabel(RectTransform parent, float x, float y, float width, float height, float size, Color color, TextAlignmentOptions alignment,
            string english, string chinese, float spacing)
        {
            var text = Ui.Text("Text", parent, size, color);
            text.alignment = alignment;
            At(text.rectTransform, x, y, width, height);
            var label = new Label { Text = text, English = english, Chinese = chinese, Spacing = spacing };
            labels.Add(label);
            label.Apply();
            return label;
        }

        static void Tag(RectTransform parent, float x, float y, float width, float height, float size, Color color, string text, float spacing)
        {
            var label = Ui.Text("Tag", parent, size, color);
            label.font = Fonts.Display;
            label.characterSpacing = spacing;
            label.alignment = TextAlignmentOptions.Left;
            label.text = text;
            At(label.rectTransform, x, y, width, height);
        }

        static UiShape Shape(string name, RectTransform parent, float x, float y, float width, float height)
        {
            var shape = Ui.Shape(name, parent);
            At(shape.rectTransform, x, y, width, height);
            return shape;
        }

        /// <summary>以父物件左上角為原點擺放，y 是往下的距離。</summary>
        static RectTransform At(RectTransform rect, float x, float y, float width, float height)
        {
            Ui.Place(rect, new Vector2(0, 1), new Vector2(x, -y), new Vector2(width, height));
            return rect;
        }

        static Rect Box(float x, float y, float width, float height) => new Rect(x, -y - height, width, height);

        static Vector2 P(float x, float y) => new Vector2(x, -y);
    }
}

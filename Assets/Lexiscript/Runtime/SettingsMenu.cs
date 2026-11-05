using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lexiscript
{
    /// <summary>
    /// 右上角的設定鈕與設定視窗。設定鈕平常看不到，滑鼠靠近右上角才淡入，滑過時齒輪轉一格；
    /// 點開後是蓋住整個畫面的視窗：開著時 <see cref="Hud.ModalOpen"/>，並拿走打字焦點擋住打字與移動，
    /// Esc、點視窗外或關閉鈕關閉。內容依序是語言、總答題數顯示、玩家外觀，最下面是要再確認一次的重置進度。
    /// 設定介面跟教學地板說明是遊戲裡唯二會換語言的文字。
    /// </summary>
    // 同一下點擊不能同時按到後面的東西，所以點擊分兩段處理。按設定鈕在 Update，排在筆記（100）之前：
    // 筆記可以拖到右上角墊在設定鈕底下，視窗先打開，筆記這一幀就會看到 ModalOpen 而不理這下點擊。
    // 視窗裡的點擊留到 LateUpdate：方塊、暴雷條與筆記這一幀看到的視窗都還開著，關掉視窗的那一下才不會穿透到後面
    [DefaultExecutionOrder(50)]
    public sealed class SettingsMenu : MonoBehaviour, ITextTarget
    {
        public const int LanguageRow = 0, CounterRow = 1, StyleRow = 2;

        /// <summary>依語言換字；英文用顯示字型，含中文字時換成介面字型（顯示字型沒有中文字）。</summary>
        sealed class Label
        {
            public TextMeshProUGUI Text;
            public string English, Chinese;
            public float EnglishSpacing, ChineseSpacing;
            public bool Heavy;

            public void Apply()
            {
                var shown = Localization.Pick(English, Chinese);
                bool cjk = HasCjk(shown);
                Text.font = cjk ? Hud.UiFont : Fonts.Display;
                // 顯示字型本身是半粗體，中文的介面字型只有一般粗細，標題不加粗會比英文單薄
                Text.fontStyle = Heavy && cjk ? FontStyles.Bold : FontStyles.Normal;
                Text.characterSpacing = cjk ? ChineseSpacing : EnglishSpacing;
                Text.text = shown;
            }
        }

        sealed class Button
        {
            public RectTransform Rect;
            /// <summary>滑鼠移上去時淡入的那一層。</summary>
            public UiShape Fill;
            public Label Label;
            public float Hover;
        }

        /// <summary>二選一的設定列，金色選取塊在兩個選項之間滑動。</summary>
        sealed class Choice
        {
            public Button[] Options;
            public RectTransform Indicator;
            public Func<int> Get;
            public Action<int> Set;
            public float Position;
        }

        sealed class Row
        {
            public RectTransform Rect;
            public CanvasGroup Group;
            public float Top;
        }

        static readonly Vector2 PanelSize = new Vector2(820, 644), DialogSize = new Vector2(700, 300);
        static readonly Vector2 GearSize = new Vector2(60, 60), GearZoneSize = new Vector2(320, 240);
        const float GearMargin = 22, GearTurn = 45, GearSlide = 16;
        const float PadX = 44, NameX = PadX + 64, FirstRowTop = 184, RowHeight = 96, ResetRowTop = 516;
        const float ControlWidth = 300, ControlHeight = 48, ResetWidth = 220, IndicatorInset = 4;
        const float GearInSeconds = 0.18f, GearOutSeconds = 0.3f, TurnSeconds = 0.35f, HoverSeconds = 0.12f, IndicatorSeconds = 0.24f;
        const float OpenSeconds = 0.36f, CloseSeconds = 0.18f, ConfirmInSeconds = 0.28f, ConfirmOutSeconds = 0.14f;
        const float PanelRise = 30, RowSlide = 40, RowStagger = 0.1f, DecorDrift = 64;

        static readonly Color Body = new Color(0.118f, 0.129f, 0.157f, 0.97f);
        static readonly Color DialogBody = new Color(0.137f, 0.149f, 0.18f, 0.99f);
        static readonly Color Shadow = HudStyle.WithAlpha(Art.Ink, 0.45f);
        static readonly Color Line = HudStyle.WithAlpha(Art.Paper, 0.16f);
        static readonly Color Muted = HudStyle.WithAlpha(Art.Paper, 0.55f);
        static readonly Color Idle = HudStyle.WithAlpha(Art.Paper, 0.78f);

        readonly List<Label> labels = new List<Label>();
        readonly List<Row> rows = new List<Row>();
        readonly List<Choice> choices = new List<Choice>();
        RectTransform gearZone, gear, gearVisual, gearIcon, gearLine;
        RectTransform window, decor, panel, accent, confirm, dialog;
        CanvasGroup gearGroup, windowGroup, confirmGroup;
        UiShape gearIconShape, closeIcon;
        Button close, reset, cancel, confirmButton;
        Label title;
        Vector2 pointer;
        bool pointing;
        /// <summary>這一幀按下了左鍵，而且沒被設定鈕用掉；留給 LateUpdate 處理視窗裡的點擊。</summary>
        bool clicked;
        float gearShown, gearHover, openness, confirmness;

        public bool IsOpen { get; private set; }
        /// <summary>重置進度的確認視窗開著。</summary>
        public bool Confirming { get; private set; }
        public float GearAlpha => gearGroup.alpha;
        public RectTransform Gear => gear;
        public RectTransform Panel => panel;
        public RectTransform CloseButton => close.Rect;
        public RectTransform ResetButton => reset.Rect;
        public RectTransform CancelButton => cancel.Rect;
        public RectTransform ConfirmButton => confirmButton.Rect;
        public TMP_Text Title => title.Text;
        public bool WantsIme => false;

        /// <summary>確認重置後執行的動作；測試換掉它，以免真的清掉進度、切換場景。</summary>
        public Action ResetProgress { get; set; } = GameProgress.ResetAll;

        /// <summary>二選一設定列的選項（row 用 <see cref="LanguageRow"/> 等常數，option 是左 0 右 1）。</summary>
        public RectTransform Option(int row, int option) => choices[row].Options[option].Rect;

        bool GearVisible => gearGroup.alpha > 0.5f;

        /// <summary>滑鼠在看得到的設定鈕上；視窗開著時整個畫面都算介面（<see cref="Hud.ModalOpen"/>）。</summary>
        public bool Covers(Vector2 screen) => GearVisible && Ui.Contains(gear, screen);

        void Awake()
        {
            Ui.Fill((RectTransform)transform);
            BuildGear();
            BuildWindow();
            BuildConfirm();
            ApplyLabels();
            confirm.gameObject.SetActive(false);
            window.gameObject.SetActive(false);
        }

        void OnEnable() => GameSettings.Changed += ApplyLabels;

        void OnDisable() => GameSettings.Changed -= ApplyLabels;

        void OnDestroy() => TextFocus.Release(this);

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            Confirming = false;
            // GameSettings 是全域的，視窗關著時也可能被別處改掉；打開時選取塊直接停在目前的選項，不從舊位置滑過去
            foreach (var choice in choices) choice.Position = choice.Get();
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

        void SetConfirming(bool value)
        {
            Confirming = value;
            if (value) confirm.gameObject.SetActive(true);
        }

        void ConfirmReset()
        {
            Close();
            ResetProgress();
        }

        // 拿著焦點只是為了擋住打字與移動，打的字直接丟掉
        public void OnText(char c) { }

        public void OnComposition(string composition) { }

        public void OnKey(Key key, bool shift, bool ctrl)
        {
            if (key != Key.Escape) return;
            if (Confirming) SetConfirming(false);
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
            if (clicked && !IsOpen && Covers(pointer))
            {
                Open();
                clicked = false;
            }
        }

        void LateUpdate()
        {
            if (clicked && IsOpen) Click(pointer);
            clicked = false;
            Animate();
        }

        void Click(Vector2 screen)
        {
            if (Confirming)
            {
                if (Ui.Contains(confirmButton.Rect, screen)) ConfirmReset();
                else if (Ui.Contains(cancel.Rect, screen) || !Ui.Contains(dialog, screen)) SetConfirming(false);
            }
            else if (Ui.Contains(close.Rect, screen) || !Ui.Contains(panel, screen))
            {
                Close();
            }
            else if (Ui.Contains(reset.Rect, screen))
            {
                SetConfirming(true);
            }
            else
            {
                foreach (var choice in choices)
                    for (int i = 0; i < choice.Options.Length; i++)
                        if (Ui.Contains(choice.Options[i].Rect, screen)) choice.Set(i);
            }
        }

        bool Hovered(RectTransform rect) => pointing && Ui.Contains(rect, pointer);

        static float Step(float value, bool on, float inSeconds, float outSeconds) =>
            Mathf.MoveTowards(value, on ? 1 : 0, Time.unscaledDeltaTime / (on ? inSeconds : outSeconds));

        void Animate()
        {
            gearShown = Step(gearShown, !IsOpen && Hovered(gearZone), GearInSeconds, GearOutSeconds);
            gearHover = Step(gearHover, !IsOpen && GearVisible && Hovered(gear), TurnSeconds, TurnSeconds);
            float shown = Ease.OutCubic(gearShown), hover = Ease.OutCubic(gearHover);
            gearGroup.alpha = shown;
            gearVisual.anchoredPosition = new Vector2(GearSlide * (1 - shown), 0);
            // 移開時倒著走 OutBack，會先多轉一點再回正，像預備動作
            gearIcon.localEulerAngles = new Vector3(0, 0, -GearTurn * Ease.OutBack(gearHover));
            gearIconShape.color = Color.Lerp(Art.Paper, Art.Gold, hover);
            gearLine.localScale = new Vector3(hover, 1, 1);

            openness = Step(openness, IsOpen, OpenSeconds, CloseSeconds);
            confirmness = Step(confirmness, Confirming, ConfirmInSeconds, ConfirmOutSeconds);
            if (window.gameObject.activeSelf) AnimateWindow();
        }

        void AnimateWindow()
        {
            float rise = Ease.OutQuint(openness);
            windowGroup.alpha = Ease.OutCubic(openness);
            panel.anchoredPosition = new Vector2(0, -PanelRise * (1 - rise));
            accent.localScale = new Vector3(1, rise, 1);
            decor.anchoredPosition = new Vector2(-DecorDrift, -DecorDrift) * (1 - rise);
            for (int k = 0; k < rows.Count; k++)
            {
                float local = Mathf.Clamp01((openness - RowStagger * k) / (1 - RowStagger * (rows.Count - 1)));
                rows[k].Group.alpha = Ease.OutCubic(local);
                rows[k].Rect.anchoredPosition = new Vector2(-RowSlide * (1 - Ease.OutCubic(local)), -rows[k].Top);
            }

            bool interactive = IsOpen && !Confirming;
            foreach (var choice in choices)
            {
                int selected = choice.Get();
                choice.Position = Mathf.MoveTowards(choice.Position, selected, Time.unscaledDeltaTime / IndicatorSeconds);
                float at = Ease.InOutCubic(choice.Position);
                choice.Indicator.anchoredPosition = new Vector2(IndicatorInset + at * ControlWidth / 2, -IndicatorInset);
                for (int i = 0; i < choice.Options.Length; i++)
                {
                    var option = choice.Options[i];
                    Hover(option, interactive && i != selected);
                    option.Label.Text.color = Color.Lerp(Idle, Art.Ink, i == 0 ? 1 - at : at);
                }
            }
            Hover(close, interactive);
            closeIcon.color = Color.Lerp(Art.Paper, Art.Gold, Ease.OutCubic(close.Hover));
            Hover(reset, interactive);
            reset.Label.Text.color = Color.Lerp(HudStyle.Danger, Art.Paper, Ease.OutCubic(reset.Hover));

            if (confirm.gameObject.activeSelf)
            {
                confirmGroup.alpha = Ease.OutCubic(confirmness);
                float pop = Mathf.LerpUnclamped(0.92f, 1, Ease.OutBack(confirmness));
                dialog.localScale = new Vector3(pop, pop, 1);
                Hover(cancel, Confirming);
                Hover(confirmButton, Confirming);
                if (!Confirming && confirmness <= 0) confirm.gameObject.SetActive(false);
            }
            if (!IsOpen && openness <= 0) window.gameObject.SetActive(false);
        }

        void Hover(Button button, bool enabled)
        {
            button.Hover = Step(button.Hover, enabled && Hovered(button.Rect), HoverSeconds, HoverSeconds);
            button.Fill.color = new Color(1, 1, 1, Ease.OutCubic(button.Hover));
        }

        void ApplyLabels()
        {
            foreach (var label in labels) label.Apply();
        }

        static bool HasCjk(string text)
        {
            foreach (var c in text)
                if (c >= 0x2E80) return true;
            return false;
        }

        void BuildGear()
        {
            gearZone = Ui.Rect("GearZone", transform);
            Ui.Place(gearZone, Vector2.one, Vector2.zero, GearZoneSize);
            gear = Ui.Rect("Gear", transform);
            Ui.Place(gear, Vector2.one, new Vector2(-GearMargin, -GearMargin), GearSize);
            gearVisual = Ui.Rect("Visual", gear);
            Ui.Fill(gearVisual);
            gearGroup = gearVisual.gameObject.AddComponent<CanvasGroup>();
            gearGroup.blocksRaycasts = false;
            gearGroup.alpha = 0;

            var backdrop = Ui.Shape("Backdrop", gearVisual);
            Ui.Fill(backdrop.rectTransform);
            var box = new Rect(-GearSize / 2, GearSize);
            backdrop.Add(HudStyle.Chamfered(box, 12, 0, 12, 0), HudStyle.WithAlpha(Art.Ink, 0.88f));
            backdrop.AddAll(HudStyle.Bracket(box.max + new Vector2(5, 5), new Vector2(-12, 0), new Vector2(0, -12), 2), Art.Gold);

            var line = Ui.Shape("Line", gearVisual);
            gearLine = line.rectTransform;
            Ui.Place(gearLine, Vector2.zero, new Vector2(0, -6), new Vector2(GearSize.x, 2));
            line.Add(Shapes.Rect(Vector2.zero, new Vector2(GearSize.x, 2)), Art.Gold);

            gearIconShape = Ui.Shape("Icon", gearVisual);
            gearIcon = gearIconShape.rectTransform;
            Ui.Fill(gearIcon);
            gearIconShape.AddAll(HudStyle.Gear(8, 19, 14, 6), Color.white);
        }

        void BuildWindow()
        {
            window = Ui.Rect("Window", transform);
            Ui.Fill(window);
            windowGroup = window.gameObject.AddComponent<CanvasGroup>();
            windowGroup.blocksRaycasts = false;
            Ui.Fill(Ui.Image("Overlay", window, HudStyle.WithAlpha(Art.Ink, 0.62f)).rectTransform);

            var decorShape = Ui.Shape("Decor", window);
            decor = decorShape.rectTransform;
            Ui.Fill(decor);
            var along = new Vector2(1, 1).normalized * 3000;
            var normal = new Vector2(-1, 1).normalized;
            decorShape.Add(Shapes.Segment(-along, along, 460), HudStyle.WithAlpha(Art.Paper, 0.035f));
            decorShape.Add(Shapes.Segment(normal * 320 - along, normal * 320 + along, 2), HudStyle.WithAlpha(Art.Gold, 0.5f));
            decorShape.Add(Shapes.Segment(-normal * 300 - along, -normal * 300 + along, 1), HudStyle.WithAlpha(Art.Paper, 0.14f));

            panel = Ui.Rect("Panel", window);
            Ui.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, PanelSize);
            var content = At(Ui.Rect("Content", panel), 0, 0, PanelSize.x, PanelSize.y);
            float w = PanelSize.x, h = PanelSize.y;

            var body = Shape("Body", content, 0, 0, w, h);
            body.Add(HudStyle.Chamfered(Box(10, 10, w, h), 32, 0, 32, 0), Shadow);
            body.Add(HudStyle.Chamfered(Box(0, 0, w, h), 32, 0, 32, 0), Body);
            body.AddAll(HudStyle.Frame(Box(10, 10, w - 20, h - 20), 26, 0, 26, 0, 1), HudStyle.WithAlpha(Art.Paper, 0.07f));
            body.AddAll(HudStyle.Barcode(P(w - 330, 34), 12, "21312113121231", 3), HudStyle.WithAlpha(Art.Paper, 0.3f));
            body.Add(Shapes.Rect(P(PadX, 165), P(w - PadX, 163)), Line);
            body.Add(Shapes.Rect(P(PadX, 166), P(PadX + 72, 162)), Art.Gold);
            for (int k = 1; k < 3; k++)
                body.AddAll(HudStyle.Dashes(P(PadX, FirstRowTop + k * RowHeight), w - 2 * PadX, 1, 8, 6), HudStyle.WithAlpha(Art.Paper, 0.12f));
            float band = ResetRowTop - 30;
            body.AddAll(HudStyle.Hatch(Shapes.Rect(P(PadX, band + 12), P(w - PadX, band)), 12, 5), HudStyle.WithAlpha(HudStyle.Danger, 0.55f));
            body.Add(Shapes.Rect(P(PadX, band + 14), P(w - PadX, band + 13)), HudStyle.WithAlpha(HudStyle.Danger, 0.4f));
            body.AddAll(HudStyle.Bracket(P(w + 14, -14), new Vector2(-36, 0), new Vector2(0, -36), 3), Art.Gold);
            body.AddAll(HudStyle.Bracket(P(-14, h + 14), new Vector2(36, 0), new Vector2(0, 36), 3), Art.Gold);

            var accentShape = Shape("Accent", content, 0, 40, 6, 124);
            accent = accentShape.rectTransform;
            accentShape.Add(Shapes.Rect(P(0, 124), P(6, 0)), Art.Gold);

            Tag(content, PadX, 16, 400, 22, 13, HudStyle.WithAlpha(Art.Paper, 0.45f), TextAlignmentOptions.Left, "LEXISCRIPT // SYS.CONFIG", 30);
            title = AddLabel(content, PadX, 56, 520, 62, 46, Art.Paper, TextAlignmentOptions.Left, "SETTINGS", "設定", 10, 12, true);
            AddLabel(content, PadX, 120, w - 2 * PadX, 26, 17, Muted, TextAlignmentOptions.Left, "Changes are saved automatically.", "變更會自動儲存。", 1, 1);

            close = new Button { Rect = At(Ui.Rect("Close", content), w - 24 - 112, 14, 112, 40) };
            close.Fill = Shape("Hover", close.Rect, 0, 0, 112, 40);
            close.Fill.Add(HudStyle.Chamfered(Box(0, 0, 112, 40), 8, 0, 8, 0), HudStyle.WithAlpha(Art.Paper, 0.08f));
            Tag(close.Rect, 16, 0, 56, 40, 13, Muted, TextAlignmentOptions.Left, "ESC", 24);
            closeIcon = Shape("Icon", close.Rect, 0, 0, 112, 40);
            closeIcon.AddAll(HudStyle.Cross(P(86, 20), 8, 2.5f), Color.white);

            AddChoice(content, LanguageRow, "LANGUAGE", "語言",
                "Applies to this menu and the tutorial floor hints.", "套用在設定介面與教學地板上的說明。",
                new[] { ("English", "English"), ("中文", "中文") },
                () => GameSettings.Language == Language.Chinese ? 1 : 0,
                option => GameSettings.Language = option == 1 ? Language.Chinese : Language.English);
            AddChoice(content, CounterRow, "SOLVED COUNTER", "總答題數顯示",
                "Show how many puzzles you have solved, top left.", "在畫面左上角顯示已解開的題數。",
                new[] { ("ON", "開啟"), ("OFF", "關閉") },
                () => GameSettings.ShowSolvedCounter ? 0 : 1,
                option => GameSettings.ShowSolvedCounter = option == 0);
            AddChoice(content, StyleRow, "PLAYER STYLE", "玩家外觀",
                "A pixel character, or the original little square.", "像素小人，或原本的小方塊。",
                new[] { ("PIXEL", "像素小人"), ("SQUARE", "小方塊") },
                () => GameSettings.PlayerStyle == PlayerStyle.Square ? 1 : 0,
                option => GameSettings.PlayerStyle = option == 1 ? PlayerStyle.Square : PlayerStyle.Pixel);

            var resetRow = AddRow(content, ResetRowTop);
            RowHeader(resetRow, 4, HudStyle.Danger, "RESET PROGRESS", "重置進度",
                "Erase all game progress and notes. Settings are kept.", "清除所有遊戲進度與筆記，設定會保留。", w - NameX - PadX - ResetWidth - 24);
            reset = new Button { Rect = At(Ui.Rect("Reset", resetRow), w - PadX - ResetWidth, (RowHeight - ControlHeight) / 2, ResetWidth, ControlHeight) };
            var outline = Box(0, 0, ResetWidth, ControlHeight);
            var resetFrame = Shape("Frame", reset.Rect, 0, 0, ResetWidth, ControlHeight);
            // 斜紋只是底紋：太濃的話同樣是紅色的字會跟它糊在一起看不清
            resetFrame.AddAll(HudStyle.Hatch(HudStyle.Chamfered(Box(2, 2, ResetWidth - 4, ControlHeight - 4), 9, 0, 9, 0), 10, 3), HudStyle.WithAlpha(HudStyle.Danger, 0.12f));
            resetFrame.AddAll(HudStyle.Frame(outline, 10, 0, 10, 0, 2), HudStyle.Danger);
            reset.Fill = Shape("Hover", reset.Rect, 0, 0, ResetWidth, ControlHeight);
            reset.Fill.Add(HudStyle.Chamfered(outline, 10, 0, 10, 0), HudStyle.Danger);
            reset.Label = AddLabel(reset.Rect, 0, 0, ResetWidth, ControlHeight, 19, HudStyle.Danger, TextAlignmentOptions.Center, "RESET", "重置", 10, 4, true);
        }

        void AddChoice(RectTransform content, int index, string name, string chineseName, string detail, string chineseDetail,
            (string english, string chinese)[] options, Func<int> get, Action<int> set)
        {
            var row = AddRow(content, FirstRowTop + index * RowHeight);
            RowHeader(row, index + 1, Art.Gold, name, chineseName, detail, chineseDetail, PanelSize.x - NameX - PadX - ControlWidth - 24);
            var control = At(Ui.Rect("Control", row), PanelSize.x - PadX - ControlWidth, (RowHeight - ControlHeight) / 2, ControlWidth, ControlHeight);
            float half = ControlWidth / 2, inner = ControlHeight - 2 * IndicatorInset;
            var frame = Shape("Frame", control, 0, 0, ControlWidth, ControlHeight);
            frame.AddAll(HudStyle.Frame(Box(0, 0, ControlWidth, ControlHeight), 10, 0, 10, 0, 2), HudStyle.WithAlpha(Art.Paper, 0.3f));
            frame.Add(Shapes.Rect(P(half - 1, ControlHeight - 14), P(half + 1, 14)), HudStyle.WithAlpha(Art.Paper, 0.18f));

            var choice = new Choice { Get = get, Set = set, Position = get(), Options = new Button[options.Length] };
            for (int i = 0; i < options.Length; i++)
            {
                var hover = Shape("Hover", control, half * i + IndicatorInset, IndicatorInset, half - 2 * IndicatorInset, inner);
                hover.Add(Shapes.Rect(P(0, inner), P(half - 2 * IndicatorInset, 0)), HudStyle.WithAlpha(Art.Paper, 0.1f));
                choice.Options[i] = new Button { Fill = hover };
            }
            var indicator = Shape("Indicator", control, IndicatorInset, IndicatorInset, half - 2 * IndicatorInset, inner);
            indicator.Add(HudStyle.Chamfered(Box(0, 0, half - 2 * IndicatorInset, inner), 8, 0, 8, 0), Art.Gold);
            choice.Indicator = indicator.rectTransform;
            // 字畫在金色選取塊上面，所以最後才建
            for (int i = 0; i < options.Length; i++)
            {
                // 中文細筆畫壓在金色選取塊上會糊成灰色，跟標題一樣加粗
                var label = AddLabel(control, half * i, 0, half, ControlHeight, 19, Idle, TextAlignmentOptions.Center, options[i].english, options[i].chinese, 8, 2, true);
                choice.Options[i].Label = label;
                choice.Options[i].Rect = label.Text.rectTransform;
            }
            choices.Add(choice);
        }

        RectTransform AddRow(RectTransform content, float top)
        {
            var rect = At(Ui.Rect("Row", content), 0, top, PanelSize.x, RowHeight);
            var group = rect.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            rows.Add(new Row { Rect = rect, Group = group, Top = top });
            return rect;
        }

        void RowHeader(RectTransform row, int number, Color accentColor, string name, string chineseName, string detail, string chineseDetail, float detailWidth)
        {
            Tag(row, PadX, 16, 44, 32, 24, accentColor, TextAlignmentOptions.Left, number.ToString("00"), 4);
            var tick = Shape("Tick", row, 0, 0, PanelSize.x, RowHeight);
            tick.Add(Shapes.Rect(P(PadX + 46, 76), P(PadX + 48, 22)), HudStyle.WithAlpha(Art.Paper, 0.18f));
            AddLabel(row, NameX, 14, 380, 34, 24, Art.Paper, TextAlignmentOptions.Left, name, chineseName, 6, 2, true);
            var description = AddLabel(row, NameX, 50, detailWidth, 42, 16, Muted, TextAlignmentOptions.TopLeft, detail, chineseDetail, 0, 0);
            description.Text.textWrappingMode = TextWrappingModes.Normal;
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
            var content = At(Ui.Rect("Content", dialog), 0, 0, DialogSize.x, DialogSize.y);
            float w = DialogSize.x, h = DialogSize.y;

            var body = Shape("Body", content, 0, 0, w, h);
            body.Add(HudStyle.Chamfered(Box(10, 10, w, h), 24, 0, 24, 0), Shadow);
            body.Add(HudStyle.Chamfered(Box(0, 0, w, h), 24, 0, 24, 0), DialogBody);
            body.Add(HudStyle.Chamfered(Box(0, 0, w, 56), 24, 0, 0, 0), HudStyle.Danger);
            body.AddAll(HudStyle.Hatch(Shapes.Rect(P(w - 260, 56), P(w, 0)), 14, 6), HudStyle.WithAlpha(Art.Ink, 0.22f));
            var icon = P(42, 30);
            body.Add(Shapes.Polygon(icon, 17, 3, 90), Art.Paper);
            body.Add(Shapes.Rect(icon + new Vector2(-1.6f, -3), icon + new Vector2(1.6f, 7)), HudStyle.Danger);
            body.Add(Shapes.Rect(icon + new Vector2(-1.6f, -8), icon + new Vector2(1.6f, -4.8f)), HudStyle.Danger);
            body.AddAll(HudStyle.Dashes(P(44, h - 24), 120, 2, 6, 5), HudStyle.WithAlpha(HudStyle.Danger, 0.6f));

            AddLabel(content, 74, 12, w - 120, 32, 22, Art.Paper, TextAlignmentOptions.Left, "RESET PROGRESS", "重置進度", 8, 4, true);
            var message = AddLabel(content, PadX, 86, w - 2 * PadX, 90, 24, Art.Paper, TextAlignmentOptions.TopLeft,
                "This will reset all game progress. Are you sure?", "此操作會重置所有遊戲進度，是否確認？", 0, 1);
            message.Text.textWrappingMode = TextWrappingModes.Normal;

            confirmButton = DialogButton(content, "Confirm", w - PadX - 220, 220, true, "RESET", "確認重置");
            cancel = DialogButton(content, "Cancel", w - PadX - 220 - 16 - 180, 180, false, "CANCEL", "取消");
        }

        Button DialogButton(RectTransform content, string name, float x, float width, bool danger, string english, string chinese)
        {
            const float Height = 52;
            var button = new Button { Rect = At(Ui.Rect(name, content), x, DialogSize.y - 36 - Height, width, Height) };
            var outline = Box(0, 0, width, Height);
            var frame = Shape("Frame", button.Rect, 0, 0, width, Height);
            if (danger) frame.Add(HudStyle.Chamfered(outline, 10, 0, 10, 0), HudStyle.Danger);
            else frame.AddAll(HudStyle.Frame(outline, 10, 0, 10, 0, 2), HudStyle.WithAlpha(Art.Paper, 0.4f));
            button.Fill = Shape("Hover", button.Rect, 0, 0, width, Height);
            button.Fill.Add(HudStyle.Chamfered(outline, 10, 0, 10, 0), HudStyle.WithAlpha(Art.Paper, danger ? 0.18f : 0.1f));
            button.Label = AddLabel(button.Rect, 0, 0, width, Height, 20, Art.Paper, TextAlignmentOptions.Center, english, chinese, 10, 4, true);
            return button;
        }

        Label AddLabel(RectTransform parent, float x, float y, float width, float height, float size, Color color, TextAlignmentOptions alignment,
            string english, string chinese, float englishSpacing, float chineseSpacing, bool heavy = false)
        {
            var label = new Label
            {
                Text = Text(parent, x, y, width, height, size, color, alignment),
                English = english,
                Chinese = chinese,
                EnglishSpacing = englishSpacing,
                ChineseSpacing = chineseSpacing,
                Heavy = heavy,
            };
            labels.Add(label);
            return label;
        }

        /// <summary>不換語言的裝飾字（技術標籤、編號），一律用顯示字型。</summary>
        static void Tag(RectTransform parent, float x, float y, float width, float height, float size, Color color, TextAlignmentOptions alignment, string text, float spacing)
        {
            var label = Text(parent, x, y, width, height, size, color, alignment);
            label.font = Fonts.Display;
            label.characterSpacing = spacing;
            label.text = text;
        }

        static TextMeshProUGUI Text(RectTransform parent, float x, float y, float width, float height, float size, Color color, TextAlignmentOptions alignment)
        {
            var text = Ui.Text("Text", parent, size, color);
            text.alignment = alignment;
            At(text.rectTransform, x, y, width, height);
            return text;
        }

        /// <summary>畫在父物件左上角座標系的圖形（見 <see cref="Box"/>、<see cref="P"/>）。</summary>
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

        /// <summary>版面座標（左上角為原點、y 往下）的矩形換成圖形的本地座標（y 往上）。</summary>
        static Rect Box(float x, float y, float width, float height) => new Rect(x, -y - height, width, height);

        static Vector2 P(float x, float y) => new Vector2(x, -y);
    }
}

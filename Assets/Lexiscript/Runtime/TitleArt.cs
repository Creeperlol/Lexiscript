using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Lexiscript
{
    /// <summary>
    /// 標題畫面的平面設計構圖，參考明日方舟的介面：大字標、細線、45° 斜紋、條碼、角落括號、技術小字與淺金強調色。
    /// 全部畫在一張 Overlay Canvas 上，座標是 Canvas 單位、以畫面中心為原點、y 朝上。
    /// 邊框類的元素貼著畫面四邊，字標等內容固定在 1920×1080 的構圖裡，
    /// 箭頭、括號與標註則每次都從方塊實際的螢幕位置算，解析度改變也對得上。
    /// </summary>
    sealed class TitleArt
    {
        static readonly Color Ink = Art.Ink, Paper = Art.Paper, Gold = Art.Gold;
        // 底色是墨色，半透明的細線改用事先混好的不透明色，線條交疊處才不會變亮
        static readonly Color Rule = Color.Lerp(Art.Ink, Art.Paper, 0.28f), Tick = Color.Lerp(Art.Ink, Art.Paper, 0.45f);
        static readonly Color Leader = Color.Lerp(Art.Ink, Art.Paper, 0.7f), Dot = Color.Lerp(Art.Ink, Art.Paper, 0.22f);
        static readonly Color Backdrop = new Color32(0x1D, 0x20, 0x26, 0xFF);
        // 條碼的粗細（每單位 2px），偶數格是條、奇數格是空白
        static readonly int[] Bars = { 3, 1, 2, 1, 4, 1, 1, 3, 2, 1, 1, 2, 4, 1, 2, 1, 3, 1, 1, 2, 1, 4, 2, 1, 3, 1, 2, 2, 1, 1, 3, 1, 4, 1, 2 };

        const string Waiting = "SYS  //  AWAITING INPUT", Granted = "SYS  //  ACCESS GRANTED";
        const float SolvedSeconds = 0.5f, BlinkHalfPeriod = 0.53f, NudgePeriod = 1.4f, NudgeDistance = 12;
        const float Spine = 56, Left = 96, Right = 56;
        const float WordmarkSize = 164, WordmarkCap = 0.72f;
        static readonly Vector2 WordmarkAt = new Vector2(-874, 210);
        const float BracketGap = 24, BracketLength = 30, BracketWidth = 4;
        const float ArrowGap = 18, ArrowRise = 150, ArrowRun = 120, LineWidth = 5;
        const float TagGap = 340, TagHeight = 64, TagPadding = 28, TagCut = 14;

        readonly RectTransform root, wordmarkMask;
        readonly UiShape decor, pointer, overlay;
        readonly TextMeshProUGUI backdrop, spine, terminal, serial, sector, kicker, wordmark, subtitle;
        readonly TextMeshProUGUI cell, kind, length, instruction, status, copyright;
        readonly float wordmarkWidth, instructionWidth, chipStart, chipEnd;
        float statusWidth;
        (Vector2 size, Vector2 block, float unit) drawn;
        bool settled;
        Vector2 tip;

        public Canvas Canvas { get; }

        /// <summary>箭頭尖端目前的螢幕座標。</summary>
        public Vector2 TipScreen => RectTransformUtility.WorldToScreenPoint(UiCamera, root.TransformPoint(tip));

        Camera UiCamera => Canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Canvas.worldCamera;

        public TitleArt(Transform parent, Block block)
        {
            var canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(parent, false);
            Canvas = canvasObject.GetComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = TitleScreen.DesignSize;
            // 任何畫面比例都完整露出 1920×1080 的構圖、只多出邊緣；鏡頭也照同一套規則縮放，方塊才會落在構圖上的同一點
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            root = (RectTransform)canvasObject.transform;

            backdrop = Label("Backdrop", "LEXISCRIPT", 410, 4, TextAlignmentOptions.BaselineLeft, Backdrop);
            decor = Ui.Shape("Decor", root);
            pointer = Ui.Shape("Pointer", root);
            spine = Label("Spine", "LEXISCRIPT  //  WORD PUZZLE TERMINAL  //  BUILD 2026.11", 17, 6, TextAlignmentOptions.BaselineLeft, Ink);
            // 轉 90° 後由下往上讀，字身在基線左側，基線放在色條中線偏右，字才會置中在色條上
            spine.rectTransform.localEulerAngles = new Vector3(0, 0, 90);
            terminal = Label("Terminal", "LEXISCRIPT.TERMINAL", 19, 5, TextAlignmentOptions.BaselineLeft, Paper);
            serial = Label("Serial", "NO.0001  ·  LX-01", 19, 4, TextAlignmentOptions.BaselineRight, Paper);
            sector = Label("Sector", "SECTOR  //  00", 15, 4, TextAlignmentOptions.BaselineRight, Paper);
            kicker = Label("Kicker", "WORD PUZZLE  //  VER 0.3", 21, 7, TextAlignmentOptions.BaselineLeft, Paper);
            wordmarkMask = Ui.Rect("Wordmark", root);
            wordmarkMask.pivot = new Vector2(0, 0.5f);
            wordmarkMask.gameObject.AddComponent<RectMask2D>();
            wordmark = Label("Text", "LEXISCRIPT", WordmarkSize, 10, TextAlignmentOptions.BaselineLeft, Paper, wordmarkMask);
            subtitle = Label("Subtitle", "A PUZZLE OF WORDS, COLORS AND CORNERS", 18, 5, TextAlignmentOptions.BaselineLeft, Paper);
            cell = Label("Cell", "CELL.00", 18, 5, TextAlignmentOptions.BaselineLeft, Paper);
            var shape = block.shape == BlockShape.Plain ? "SQUARE" : "BEVELED";
            kind = Label("Kind", $"{block.color.ToString().ToUpperInvariant()}  ·  {shape}", 15, 4, TextAlignmentOptions.BaselineLeft, Paper);
            length = Label("Length", $"LENGTH  {block.answer.Length:00}", 15, 4, TextAlignmentOptions.BaselineLeft, Paper);
            instruction = Label("Instruction", TitleScreen.Instruction, 40, 1, TextAlignmentOptions.BaselineLeft, Ink);
            status = Label("Status", Waiting, 19, 5, TextAlignmentOptions.BaselineLeft, Paper);
            copyright = Label("Copyright", "© 2026  LEXISCRIPT", 19, 4, TextAlignmentOptions.BaselineRight, Paper);
            overlay = Ui.Shape("Overlay", root);

            wordmarkWidth = Advance(wordmark, wordmark.text.Length - 1);
            instructionWidth = Advance(instruction, TitleScreen.Instruction.Length - 1);
            var characters = instruction.textInfo.characterInfo;
            chipStart = characters[TitleScreen.Instruction.IndexOf('"')].origin;
            chipEnd = characters[TitleScreen.Instruction.LastIndexOf('"')].xAdvance;
            statusWidth = Advance(status, Waiting.Length - 1);
        }

        public Vector2 ToCanvas(Vector2 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, UiCamera, out var local);
            return local;
        }

        public void Solve()
        {
            status.text = Granted;
            statusWidth = Advance(status, Granted.Length - 1);
        }

        /// <param name="t">進場開始後的秒數。</param>
        /// <param name="block">方塊中心的 Canvas 座標。</param>
        /// <param name="unit">一個世界單位在 Canvas 上的長度。</param>
        /// <param name="solved">解開後的秒數；還沒解開時是負的。</param>
        public void Draw(float t, Vector2 block, float unit, float solved)
        {
            var size = root.rect.size;
            bool animating = t < TitleScreen.IntroSeconds || (solved >= 0 && solved < SolvedSeconds);
            // 進場與解開的動畫結束後畫面是靜止的，只在版面改變時重畫，不必每幀重建上百個多邊形
            if (animating || !settled || drawn != (size, block, unit))
            {
                DrawDecor(t, size, block, unit, solved);
                drawn = (size, block, unit);
                settled = !animating;
            }
            DrawPointer(t, block, unit, solved);
            DrawOverlay(t, size, solved);
        }

        void DrawDecor(float t, Vector2 size, Vector2 block, float unit, float solved)
        {
            float l = -size.x / 2, r = size.x / 2, top = size.y / 2, bottom = -size.y / 2;
            decor.Clear();

            float spineIn = Ease.OutQuint(Progress(t, 0, 0.5f));
            decor.Add(Box(l + Spine * (spineIn - 1), bottom, l + Spine * spineIn, top), Gold);
            decor.Add(Square(new Vector2(l + Spine / 2, top - 52), 12 * Ease.OutBack(Progress(t, 0.35f, 0.35f))), Ink);
            float spineText = Ease.OutCubic(Progress(t, 0.3f, 0.5f));
            Show(spine, new Vector2(l + 35, bottom + 56 - 40 * (1 - spineText)), spineText);

            float terminalIn = Ease.OutQuint(Progress(t, 0.15f, 0.5f));
            decor.Add(Box(l + 82, top - 70, l + 88, top - 52), Alpha(Gold, terminalIn));
            Show(terminal, new Vector2(l + Left - 20 * (1 - terminalIn), top - 66), terminalIn);
            float serialIn = Ease.OutQuint(Progress(t, 0.3f, 0.5f));
            Show(serial, new Vector2(r - Right + 20 * (1 - serialIn), top - 66), serialIn);
            Barcode(r - 516, top - 70, r - 316, top - 48, Mathf.Lerp(r - 516, r - 316, Ease.OutCubic(Progress(t, 0.4f, 0.5f))));
            float rule = Mathf.Lerp(l + Left, r - Right, Ease.OutQuint(Progress(t, 0.1f, 0.8f)));
            decor.Add(Box(l + Left, top - 94, rule, top - 92), Rule);
            for (float x = l + Left; x < rule; x += 120) decor.Add(Box(x, top - 94, x + 2, top - 86), Tick);
            Hatch(r - 236, top - 140, r - Right, top - 124, 12, 5, Mathf.Lerp(r - 236, r - Right, Ease.OutQuint(Progress(t, 0.45f, 0.5f))));
            Show(sector, new Vector2(r - Right, top - 170), 0.5f * Ease.OutCubic(Progress(t, 0.6f, 0.4f)));

            Hatch(l + Left, bottom + 98, l + 716, bottom + 122, 20, 10, Mathf.Lerp(l + Left, l + 716, Ease.OutQuint(Progress(t, 0.5f, 0.6f))));
            decor.Add(Square(new Vector2(l + 748, bottom + 110), 12 * Ease.OutBack(Progress(t, 0.9f, 0.3f))), Gold);
            Show(status, new Vector2(l + Left, bottom + 58), Ease.OutCubic(Progress(t, 0.6f, 0.4f)));
            Show(copyright, new Vector2(r - Right, bottom + 58), 0.55f * Ease.OutCubic(Progress(t, 0.7f, 0.4f)));
            // 超大的淡字標壓在最底層，基線在畫面外，只露出上半截
            float rise = Ease.OutQuint(Progress(t, 0.3f, 0.9f));
            Show(backdrop, new Vector2(l + 40, bottom - 96 - 80 * (1 - rise)), rise);

            float kickerIn = Ease.OutQuint(Progress(t, 0.15f, 0.5f));
            Show(kicker, new Vector2(-864 - 24 * (1 - kickerIn), 362), 0.6f * kickerIn);
            wordmarkMask.anchoredPosition = WordmarkAt;
            wordmarkMask.sizeDelta = new Vector2((wordmarkWidth + 24) * Ease.OutQuint(Progress(t, 0.2f, 0.65f)), 2 * WordmarkSize);
            decor.Add(Box(-864, 168, -864 + 320 * Ease.OutQuint(Progress(t, 0.55f, 0.45f)), 182), Gold);
            decor.Add(Box(-544, 174, -544 + 584 * Ease.OutCubic(Progress(t, 0.62f, 0.5f)), 176), Leader);
            decor.Add(Square(new Vector2(44, 175), 4 * Ease.OutBack(Progress(t, 1.0f, 0.25f))), Paper);
            float subtitleIn = Ease.OutQuint(Progress(t, 0.7f, 0.5f));
            Show(subtitle, new Vector2(-864 - 24 * (1 - subtitleIn), 116), 0.6f * subtitleIn);
            float marks = Ease.OutCubic(Progress(t, 0.8f, 0.4f));
            Plus(new Vector2(150, 354), marks);
            Plus(new Vector2(860, -330), marks);
            // 跟地板裝飾一樣的點陣
            for (int i = 0; i < 7; i++)
                for (int j = 0; j < 4; j++)
                    decor.Add(Square(new Vector2(560 + 28 * i, 330 - 28 * j), 3), Alpha(Dot, Ease.OutCubic(Progress(t, 0.5f + 0.03f * (i + j), 0.2f))));

            Brackets(t, block, unit, solved);
            Callout(t, block, unit);
        }

        /// <summary>方塊四角的括號：進場時從外面收攏鎖定方塊，解開時鼓一下並變成金色。</summary>
        void Brackets(float t, Vector2 block, float unit, float solved)
        {
            float lockOn = Ease.OutQuint(Progress(t, 0.35f, 0.55f));
            float reach = Art.Half * unit + BracketGap + 70 * (1 - lockOn) + 12 * Ease.Pulse(Progress(solved, 0, 0.4f));
            var color = Alpha(Color.Lerp(Paper, Gold, Ease.OutCubic(Progress(solved, 0, 0.25f))), Ease.OutCubic(Progress(t, 0.35f, 0.3f)));
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                {
                    var corner = block + new Vector2(sx, sy) * reach;
                    decor.Add(Box(corner.x, corner.y, corner.x - sx * BracketLength, corner.y - sy * BracketWidth), color);
                    decor.Add(Box(corner.x, corner.y - sy * BracketWidth, corner.x - sx * BracketWidth, corner.y - sy * BracketLength), color);
                }
        }

        /// <summary>從右下角括號拉出的標註線與方塊資料。</summary>
        void Callout(float t, Vector2 block, float unit)
        {
            float reach = Art.Half * unit + BracketGap;
            var from = block + new Vector2(reach, -reach);
            var bend = from + new Vector2(56, -56);
            var end = bend + new Vector2(250, 0);
            Polyline(decor, new[] { from, bend, end }, Ease.OutCubic(Progress(t, 0.65f, 0.45f)), 2, Leader);
            decor.Add(Square(end, 4 * Ease.OutBack(Progress(t, 1.05f, 0.25f))), Gold);
            Show(cell, bend + new Vector2(6, 12), Ease.OutCubic(Progress(t, 0.8f, 0.35f)));
            Show(kind, bend + new Vector2(6, -30), 0.6f * Ease.OutCubic(Progress(t, 0.86f, 0.35f)));
            Show(length, bend + new Vector2(6, -54), 0.6f * Ease.OutCubic(Progress(t, 0.92f, 0.35f)));
        }

        /// <summary>
        /// 左下的說明標籤與拉向答案欄的箭頭：線從標籤延伸出去、45° 轉上答案欄的高度，最後水平指進答案欄。
        /// 進場後箭頭來回點著答案欄；解開後停住並變成金色。
        /// </summary>
        void DrawPointer(float t, Vector2 block, float unit, float solved)
        {
            pointer.Clear();
            float gold = Ease.OutCubic(Progress(solved, 0, 0.25f));
            var color = Color.Lerp(Paper, Gold, gold);
            var rest = block + new Vector2(-Art.Half * unit - ArrowGap, Art.AnswerPosition.y * unit);
            float swing = t < TitleScreen.IntroSeconds ? 0 : Mathf.PingPong(2 * (t - TitleScreen.IntroSeconds) / NudgePeriod, 1);
            tip = rest + new Vector2(-NudgeDistance * Ease.InOutCubic(swing) * (1 - gold), 0);

            float slide = Ease.OutQuint(Progress(t, 0.45f, 0.55f)), shown = Ease.OutCubic(Progress(t, 0.45f, 0.3f));
            float right = rest.x - TagGap - 60 * (1 - slide), y = rest.y - ArrowRise;
            float left = right - instructionWidth - 2 * TagPadding;
            float head = Ease.OutBack(Progress(t, 1.05f, 0.3f));
            var path = new[]
            {
                new Vector2(right, y), new Vector2(rest.x - ArrowRun - ArrowRise, y), new Vector2(rest.x - ArrowRun, rest.y),
                // 線收在箭頭凹口裡，箭頭還沒彈出來時線直接畫到尖端
                new Vector2(tip.x - 14 * Mathf.Clamp01(head), tip.y),
            };
            Polyline(pointer, path, Ease.OutCubic(Progress(t, 0.65f, 0.5f)), LineWidth, color);
            if (head > 0)
            {
                pointer.Add(new List<Vector2> { tip, tip + new Vector2(-30, -18) * head, tip + new Vector2(-22, 0) * head }, color);
                pointer.Add(new List<Vector2> { tip, tip + new Vector2(-22, 0) * head, tip + new Vector2(-30, 18) * head }, color);
            }

            pointer.Add(Tag(left, right, y), Alpha(Paper, shown));
            pointer.Add(Box(left, y + TagHeight / 2 + 8, left + 120, y + TagHeight / 2 + 12), Alpha(Gold, shown));
            float textLeft = left + TagPadding;
            pointer.Add(Box(textLeft + chipStart - 6, y - 24, textLeft + chipEnd + 6, y + 24), Alpha(Gold, shown));
            pointer.Add(Square(new Vector2(right, y), 6 * Ease.OutBack(Progress(t, 0.7f, 0.3f))), Gold);
            Show(instruction, new Vector2(textLeft, y - 14), shown);
        }

        /// <summary>字標進場時跟著遮罩邊緣掃過的金色游標，以及狀態列閃爍的游標。</summary>
        void DrawOverlay(float t, Vector2 size, float solved)
        {
            overlay.Clear();
            float wipe = Progress(t, 0.2f, 0.65f), fade = 1 - Ease.InCubic(Progress(t, 0.85f, 0.25f));
            if (wipe > 0 && fade > 0)
            {
                float x = WordmarkAt.x + (wordmarkWidth + 24) * Ease.OutQuint(wipe);
                overlay.Add(Box(x, WordmarkAt.y - 6, x + 18, WordmarkAt.y + WordmarkCap * WordmarkSize + 6), Alpha(Gold, fade));
            }
            bool on = solved >= 0 || (t >= TitleScreen.IntroSeconds && Mathf.Repeat(t, 2 * BlinkHalfPeriod) < BlinkHalfPeriod);
            if (on)
            {
                float x = -size.x / 2 + Left + statusWidth + 10, bottom = -size.y / 2;
                overlay.Add(Box(x, bottom + 56, x + 12, bottom + 74), Gold);
            }
        }

        void Hatch(float x0, float y0, float x1, float y1, float pitch, float width, float reveal)
        {
            float height = y1 - y0, end = Mathf.Min(x1, reveal);
            if (end <= x0) return;
            for (float x = x0 - height; x < end; x += pitch)
            {
                var stripe = new List<Vector2> { new Vector2(x, y0), new Vector2(x + width, y0), new Vector2(x + width + height, y1), new Vector2(x + height, y1) };
                decor.Add(Shapes.Clip(Shapes.Clip(stripe, Vector2.left, -x0), Vector2.right, end), Paper);
            }
        }

        void Barcode(float x0, float y0, float x1, float y1, float reveal)
        {
            float end = Mathf.Min(x1, reveal), x = x0;
            for (int i = 0; x < end; i++)
            {
                float width = 2 * Bars[i % Bars.Length];
                if (i % 2 == 0) decor.Add(Box(x, y0, Mathf.Min(x + width, end), y1), Paper);
                x += width;
            }
        }

        void Plus(Vector2 center, float alpha)
        {
            var color = Alpha(Tick, alpha);
            decor.Add(Box(center.x - 9, center.y - 1, center.x + 9, center.y + 1), color);
            decor.Add(Box(center.x - 1, center.y + 1, center.x + 1, center.y + 9), color);
            decor.Add(Box(center.x - 1, center.y - 9, center.x + 1, center.y - 1), color);
        }

        /// <summary>沿著折線畫出前 fraction 的長度（線條「畫出來」的進場）。</summary>
        static void Polyline(UiShape shape, Vector2[] points, float fraction, float width, Color color)
        {
            float total = 0;
            for (int i = 1; i < points.Length; i++) total += Vector2.Distance(points[i - 1], points[i]);
            float remaining = total * fraction;
            for (int i = 1; i < points.Length && remaining > 0; i++)
            {
                shape.Add(Shapes.Segment(points[i - 1], Vector2.MoveTowards(points[i - 1], points[i], remaining), width), color);
                remaining -= Vector2.Distance(points[i - 1], points[i]);
            }
        }

        /// <summary>右下角切成斜角的標籤底。</summary>
        static List<Vector2> Tag(float left, float right, float y)
        {
            float half = TagHeight / 2;
            return new List<Vector2>
            {
                new Vector2(left, y - half), new Vector2(right - TagCut, y - half), new Vector2(right, y - half + TagCut),
                new Vector2(right, y + half), new Vector2(left, y + half),
            };
        }

        TextMeshProUGUI Label(string name, string content, float size, float tracking, TextAlignmentOptions alignment, Color color, RectTransform parent = null)
        {
            var text = Ui.Text(name, parent != null ? parent : root, size, color);
            text.font = Fonts.Display;
            // 字距照設計稿的像素寫，TMP 的 characterSpacing 單位是 1/100 em
            text.characterSpacing = tracking / size * 100;
            text.alignment = alignment;
            text.text = content;
            var rect = text.rectTransform;
            rect.anchorMin = rect.anchorMax = parent != null ? new Vector2(0, 0.5f) : new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(alignment == TextAlignmentOptions.BaselineRight ? 1 : 0, 0.5f);
            rect.sizeDelta = new Vector2(2400, size * 1.5f);
            return text;
        }

        static float Advance(TMP_Text text, int index)
        {
            text.ForceMeshUpdate(true);
            return text.textInfo.characterInfo[index].xAdvance;
        }

        static float Progress(float t, float delay, float duration) => Mathf.Clamp01((t - delay) / duration);

        static Color Alpha(Color color, float alpha)
        {
            color.a *= alpha;
            return color;
        }

        static List<Vector2> Box(float x0, float y0, float x1, float y1) =>
            Shapes.Rect(new Vector2(Mathf.Min(x0, x1), Mathf.Min(y0, y1)), new Vector2(Mathf.Max(x0, x1), Mathf.Max(y0, y1)));

        static List<Vector2> Square(Vector2 center, float half) => Box(center.x - half, center.y - half, center.x + half, center.y + half);

        static void Show(TMP_Text text, Vector2 position, float alpha)
        {
            text.rectTransform.anchoredPosition = position;
            text.alpha = alpha;
        }
    }
}

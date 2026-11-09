using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lexiscript
{
    /// <summary>
    /// 標題畫面。主角是一格純白方角方塊（題目 START），打出 start 解開就進入遊戲：開始遊戲本身就是一道「寫出一樣的字」的題目。
    /// 一進場就聚焦答案欄；沒有焦點時直接打字也會聚焦並把字送進去。解開後先讓解題特效播完，再以 <see cref="TitleTransition"/> 轉場。
    /// 方塊與鏡頭由 TitleSetup 建在場景裡，介面（<see cref="TitleArt"/>）在執行時用程式建立。
    /// </summary>
    [RequireComponent(typeof(TextInputHub))]
    public sealed class TitleScreen : MonoBehaviour
    {
        public const string NextScene = "Game";
        /// <summary>方塊的題目，也是答案。</summary>
        public const string Word = "START";
        public const string Instruction = "type \"start\" to start!!!";
        /// <summary>解開後等解題特效（<see cref="SolveEffect"/>）播完、再停一拍才轉場。</summary>
        public const float SolveDelay = SolveEffect.Seconds + 0.05f;
        public const float IntroSeconds = 1.4f;
        public const float CameraSize = 3.25f;
        /// <summary>構圖的參考解析度；任何畫面比例都會完整看到這個範圍。</summary>
        public static readonly Vector2 DesignSize = new Vector2(1920, 1080);
        /// <summary>方塊中心在構圖上的位置（以畫面中心為原點、y 朝上）。偏右下，左邊留給字標與拉出箭頭的說明標籤。</summary>
        public static readonly Vector2 BlockOnScreen = new Vector2(300, -70);
        public static readonly Color Background = Art.Ink;

        // 開場建立字型時會卡住幾幀；限制每幀前進的時間，進場動畫才不會一開始就跳掉一大段
        const float MaxStep = 0.1f;

        Camera view;
        TitleArt art;
        float clock, solvedAt = float.PositiveInfinity;

        /// <summary>方塊放在世界原點時鏡頭的位置，方塊才會落在構圖的 <see cref="BlockOnScreen"/>。</summary>
        public static Vector2 CameraPosition => -BlockOnScreen * (2 * CameraSize / DesignSize.y);

        public Board Board { get; private set; }
        public Block Block { get; private set; }
        /// <summary>已經解開，正在等待或播放轉場。</summary>
        public bool Starting { get; private set; }
        public bool IntroFinished => clock >= IntroSeconds;
        /// <summary>標題介面的 Canvas；截圖時要暫時改成跟著鏡頭。</summary>
        public Canvas Canvas => art.Canvas;
        /// <summary>箭頭尖端目前的螢幕座標。</summary>
        public Vector2 ArrowTipScreen => art.TipScreen;

        void Start()
        {
            view = Camera.main;
            Board = FindAnyObjectByType<Board>();
            Block = Board.Blocks.First(block => Board.Puzzle.IsAnswerable(block.name));
            art = new TitleArt(transform, Block);
            Board.Solved += OnSolved;
            TextFocus.Unfocused += OnUnfocusedText;
            Board.Focus(Block);
        }

        void OnDestroy()
        {
            if (Board != null) Board.Solved -= OnSolved;
            TextFocus.Unfocused -= OnUnfocusedText;
        }

        /// <summary>按下離開按鈕時執行；測試換掉它，以免真的關掉遊戲。</summary>
        public static System.Action QuitGame { get; set; } = Quit;

        public RectTransform QuitButton => art.QuitButton;

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void Update()
        {
            var mouse = Mouse.current;
            if (art == null || Starting || !IntroFinished || mouse == null || !mouse.leftButton.wasPressedThisFrame) return;
            if (Ui.Contains(art.QuitButton, mouse.position.ReadValue())) QuitGame();
        }

        void LateUpdate()
        {
            clock += Mathf.Min(Time.unscaledDeltaTime, MaxStep);
            FitCamera();
            Vector3 world = Block.transform.position;
            var center = art.ToCanvas(view.WorldToScreenPoint(world));
            var right = art.ToCanvas(view.WorldToScreenPoint(world + Vector3.right));
            art.Draw(clock, center, right.x - center.x, clock - solvedAt);
        }

        /// <summary>
        /// 畫面比 16:9 窄時把鏡頭拉遠，讓構圖的整個寬度都看得到。跟介面 CanvasScaler 的 Expand 是同一套規則，
        /// 世界與介面的比例才會一致，方塊永遠落在構圖上的同一點。
        /// </summary>
        void FitCamera() => view.orthographicSize = CameraSize * Mathf.Max(1, DesignSize.x / DesignSize.y / view.aspect);

        void OnUnfocusedText(char c)
        {
            if (Starting) return;
            Board.Focus(Block);
            Board.OnText(c);
        }

        void OnSolved(Block block)
        {
            Starting = true;
            solvedAt = clock;
            art.Solve();
            StartCoroutine(LoadNext());
        }

        IEnumerator LoadNext()
        {
            yield return new WaitForSecondsRealtime(SolveDelay);
            TitleTransition.Play(NextScene);
        }
    }
}

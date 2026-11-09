using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lexiscript
{
    /// <summary>
    /// 正式關卡的遊玩進度存檔（progress.json）：重新進入遊戲時接續上次的進度。
    /// 記錄已解開的題目、露出的字首字尾、打開的暴雷條與玩家位置；有變動就稍後存檔，離開遊戲或換場景時也會存。
    /// </summary>
    // 要在其他元件的 Start 之前還原：答題數這類讀目前狀態的介面，第一幀就要拿到還原後的數字，不能當成剛解開而播動畫
    [DefaultExecutionOrder(-100)]
    public sealed class GameProgress : MonoBehaviour
    {
        // 方塊一律用 id（物件名稱）記，關卡改版後找不到的 id 直接略過，舊存檔仍可用
        [Serializable]
        sealed class SaveData
        {
            /// <summary>存檔格式版本，之後格式改變時用來轉換舊存檔。</summary>
            public int version = Version;
            public string[] solved = new string[0];
            public string[] firstLetter = new string[0];
            public string[] lastLetter = new string[0];
            /// <summary>打開的暴雷條所屬方塊的 id。</summary>
            public string[] spoilers = new string[0];
            public Vector2 player;
            /// <summary>走到過大地圖（右下角的傳送鈕從這之後才出現）。</summary>
            public bool worldReached;
            /// <summary>第一次過橋的演出已經播過。</summary>
            public bool bridgeIntroPlayed;
        }

        const int Version = 1;
        const string TitleScene = "Title";
        // 走動時位置每幀都在變：每次變動都往後延的話，一路走著就永遠存不到檔，所以從第一次變動起算
        const float SaveDelay = 1f;
        // 站著不動時物理的微小誤差不算移動，免得一直寫檔
        const float MoveThreshold = 0.05f;

        /// <summary>存檔位置；null 代表 persistentDataPath/progress.json。測試改到暫存檔，以免蓋掉玩家的進度。</summary>
        public static string StoragePath;

        Board board;
        Spoiler[] spoilers;
        Rigidbody2D player;
        // 場景卸載時其他物件可能已經先被銷毀，存檔時不能再去讀它們，所以一有變動就先記下來
        SaveData state;
        float saveAt = float.PositiveInfinity;
        bool discarded;

        static string FilePath => StoragePath ?? Path.Combine(Application.persistentDataPath, "progress.json");

        /// <summary>目前場景的進度；Demo 這類沒有存檔的場景是 null。</summary>
        public static GameProgress Current { get; private set; }

        public bool WorldReached
        {
            get => state != null && state.worldReached;
            set => SetFlag(value, state == null || state.worldReached == value, () => state.worldReached = value);
        }

        public bool BridgeIntroPlayed
        {
            get => state != null && state.bridgeIntroPlayed;
            set => SetFlag(value, state == null || state.bridgeIntroPlayed == value, () => state.bridgeIntroPlayed = value);
        }

        void SetFlag(bool value, bool unchanged, Action apply)
        {
            if (unchanged) return;
            apply();
            ScheduleSave();
        }

        void Awake() => Current = this;

        /// <summary>清掉遊戲進度與筆記（設定保留），回到標題畫面；沒有標題畫面時重新載入目前的場景。</summary>
        public static void ResetAll()
        {
            foreach (var progress in FindObjectsByType<GameProgress>(FindObjectsSortMode.None)) progress.discarded = true;
            try
            {
                if (File.Exists(FilePath)) File.Delete(FilePath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not delete progress at {FilePath}: {exception.Message}");
            }
            if (Hud.Instance != null) Hud.Instance.Notes.Discard();
            else NotesPanel.DeleteSave();
            SceneManager.LoadScene(Application.CanStreamedLevelBeLoaded(TitleScene) ? TitleScene : SceneManager.GetActiveScene().name);
        }

        void Start()
        {
            board = FindAnyObjectByType<Board>();
            spoilers = FindObjectsByType<Spoiler>(FindObjectsSortMode.None);
            player = FindAnyObjectByType<Player>().GetComponent<Rigidbody2D>();
            var saved = Load();
            if (saved != null) Restore(saved);
            state = Capture();
            if (saved != null)
            {
                state.worldReached = saved.worldReached;
                state.bridgeIntroPlayed = saved.bridgeIntroPlayed;
            }
            board.Solved += OnBlockChanged;
            board.RevealChanged += OnBlockChanged;
            foreach (var spoiler in spoilers) spoiler.Opened += OnChanged;
        }

        void Update()
        {
            if ((player.position - state.player).sqrMagnitude > MoveThreshold * MoveThreshold)
            {
                state.player = player.position;
                ScheduleSave();
            }
            if (Time.unscaledTime >= saveAt) Save();
        }

        void OnApplicationQuit() => SavePending();

        void OnDestroy()
        {
            SavePending();
            if (Current == this) Current = null;
        }

        void Restore(SaveData saved)
        {
            var blocks = board.Blocks.ToDictionary(block => block.name);
            foreach (var id in saved.solved)
                if (blocks.TryGetValue(id, out var block)) board.RestoreSolved(block);
            foreach (var id in saved.firstLetter)
                if (blocks.TryGetValue(id, out var block)) board.RestoreReveal(block, Reveal.First);
            foreach (var id in saved.lastLetter)
                if (blocks.TryGetValue(id, out var block)) board.RestoreReveal(block, Reveal.Last);
            foreach (var spoiler in spoilers)
                if (saved.spoilers.Contains(spoiler.block.name)) spoiler.Reveal();
            foreach (var door in FindObjectsByType<Door>(FindObjectsSortMode.None)) door.SyncInstantly();
            if (!CanStandAt(saved.player)) return;
            player.position = saved.player;
            player.transform.position = saved.player;
        }

        /// <summary>關卡改版後，舊存檔的位置可能已經在牆裡、方塊裡、房間外或還沒打開的房間裡，這時寧可留在出生點。</summary>
        bool CanStandAt(Vector2 position)
        {
            var rooms = FindAnyObjectByType<Rooms>();
            if ((rooms != null && !rooms.Contains(position)) || RoomCover.Hides(position)) return false;
            // 貼著牆或方塊站時，兩邊的碰撞體在物理引擎裡算是相交的；只用一半大小檢查，合理的位置才不會被當成卡住
            var size = player.GetComponent<Collider2D>().bounds.size / 2;
            return Physics2D.OverlapBoxAll(position, size, 0).All(hit => hit.isTrigger || hit.attachedRigidbody == player);
        }

        SaveData Capture() => new SaveData
        {
            solved = board.Puzzle.SolvedIds.ToArray(),
            firstLetter = Revealed(Reveal.First),
            lastLetter = Revealed(Reveal.Last),
            spoilers = spoilers.Where(spoiler => spoiler.Revealed).Select(spoiler => spoiler.block.name).ToArray(),
            player = player.position,
        };

        string[] Revealed(Reveal reveal) => board.Blocks.Where(block => board.RevealOf(block) == reveal).Select(block => block.name).ToArray();

        void OnBlockChanged(Block block) => OnChanged();

        void OnChanged()
        {
            bool reached = state.worldReached, played = state.bridgeIntroPlayed;
            state = Capture();
            state.worldReached = reached;
            state.bridgeIntroPlayed = played;
            ScheduleSave();
        }

        void ScheduleSave() => saveAt = Mathf.Min(saveAt, Time.unscaledTime + SaveDelay);

        void SavePending()
        {
            if (saveAt < float.PositiveInfinity) Save();
        }

        static SaveData Load()
        {
            try
            {
                if (File.Exists(FilePath)) return JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not read progress from {FilePath}: {exception.Message}");
            }
            return null;
        }

        void Save()
        {
            saveAt = float.PositiveInfinity;
            if (discarded) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, JsonUtility.ToJson(state));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not save progress to {FilePath}: {exception.Message}");
            }
        }
    }
}

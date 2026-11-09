using System;
using System.IO;
using UnityEngine;

namespace Lexiscript
{
    public enum Language
    {
        English,
        Chinese,
    }

    public enum PlayerStyle
    {
        /// <summary>二次元像素小人。</summary>
        Pixel,
        /// <summary>原本的白色小方塊。</summary>
        Square,
    }

    /// <summary>
    /// 玩家偏好：語言、是否顯示總答題數、角色外觀。存在 settings.json，
    /// 跟遊戲進度分開存，所以重置進度不會清掉。改任何一項都會立刻存檔並觸發 <see cref="Changed"/>。
    /// </summary>
    public static class GameSettings
    {
        // 列舉存成字串：之後增減列舉值時舊存檔不會對錯位
        [Serializable]
        sealed class Data
        {
            public string language = nameof(Lexiscript.Language.English);
            public bool showSolvedCounter = true;
            public string playerStyle = nameof(Lexiscript.PlayerStyle.Pixel);
            public bool confirmTeleport = true;
        }

        /// <summary>存檔位置；null 代表 persistentDataPath/settings.json。測試改到暫存檔，以免蓋掉玩家的設定。</summary>
        public static string StoragePath;

        static Data data;

        public static event Action Changed;

        static string FilePath => StoragePath ?? Path.Combine(Application.persistentDataPath, "settings.json");

        static Data Current
        {
            get
            {
                if (data == null) data = Load();
                return data;
            }
        }

        public static Language Language
        {
            get => Parse(Current.language, Lexiscript.Language.English);
            set => Update(d => d.language = value.ToString());
        }

        public static bool ShowSolvedCounter
        {
            get => Current.showSolvedCounter;
            set => Update(d => d.showSolvedCounter = value);
        }

        public static PlayerStyle PlayerStyle
        {
            get => Parse(Current.playerStyle, Lexiscript.PlayerStyle.Pixel);
            set => Update(d => d.playerStyle = value.ToString());
        }

        /// <summary>傳送前先跳出「即將前往……是否確定」。</summary>
        public static bool ConfirmTeleport
        {
            get => Current.confirmTeleport;
            set => Update(d => d.confirmTeleport = value);
        }

        /// <summary>丟掉記憶體裡的設定，下次讀取時重新從檔案載入（換了 <see cref="StoragePath"/> 之後呼叫）。</summary>
        public static void Reload()
        {
            data = null;
            Changed?.Invoke();
        }

        static void Update(Action<Data> change)
        {
            var before = JsonUtility.ToJson(Current);
            change(Current);
            if (JsonUtility.ToJson(Current) == before) return;
            Save();
            Changed?.Invoke();
        }

        static T Parse<T>(string value, T fallback) where T : struct =>
            Enum.TryParse(value, out T parsed) && Enum.IsDefined(typeof(T), parsed) ? parsed : fallback;

        static Data Load()
        {
            try
            {
                if (File.Exists(FilePath)) return JsonUtility.FromJson<Data>(File.ReadAllText(FilePath)) ?? new Data();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not read settings from {FilePath}: {exception.Message}");
            }
            return new Data();
        }

        static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, JsonUtility.ToJson(Current));
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not save settings to {FilePath}: {exception.Message}");
            }
        }
    }
}

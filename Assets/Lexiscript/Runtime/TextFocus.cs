using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Lexiscript
{
    /// <summary>能接收鍵盤打字的東西：答案欄、聊天列、筆記、筆記的透明度欄。</summary>
    public interface ITextTarget
    {
        /// <summary>要不要開輸入法；答案欄只收英文字母，開著注音反而會吃掉字母。</summary>
        bool WantsIme { get; }

        void OnText(char c);

        /// <summary>輸入法組字中的字串；選定後的字會再從 <see cref="OnText"/> 進來。</summary>
        void OnComposition(string composition);

        /// <summary>方向鍵、Backspace、Enter、Ctrl 快捷鍵等；按住時依按鍵連發的節奏重複呼叫。</summary>
        void OnKey(Key key, bool shift, bool ctrl);

        void OnFocusChanged(bool focused);
    }

    /// <summary>全場同時只有一個打字焦點；有焦點時角色不能移動。</summary>
    public static class TextFocus
    {
        static readonly Key[] MoveKeys = { Key.W, Key.A, Key.S, Key.D };
        /// <summary>取得焦點當下按著的移動鍵，放開前系統連發打出的那個字母不能進欄位。</summary>
        static readonly List<Key> heldAtFocus = new List<Key>();
        static bool holdMovement;

        public static ITextTarget Current { get; private set; }

        /// <summary>沒有任何焦點時打出來的字，例如按 / 打開聊天列。</summary>
        public static event Action<char> Unfocused;

        /// <summary>
        /// 失去焦點時若還按著移動鍵（例如解題的最後一個字母是 D），放開之前不能讓它變成移動。
        /// </summary>
        public static bool MovementAllowed
        {
            get
            {
                if (Current != null) return false;
                if (holdMovement && !MoveKeyHeld()) holdMovement = false;
                return !holdMovement;
            }
        }

        public static void Set(ITextTarget target)
        {
            if (Current == target) return;
            var previous = Current;
            Current = target;
            bool moving = MoveKeyHeld();
            var keyboard = Keyboard.current;
            // 按著移動鍵時點進欄位，系統的按鍵連發會把 WASD 打進去
            heldAtFocus.Clear();
            if (target != null && keyboard != null)
                foreach (var key in MoveKeys)
                    if (keyboard[key].isPressed) heldAtFocus.Add(key);
            holdMovement = target == null && moving;
            if (keyboard != null) keyboard.SetIMEEnabled(target != null && target.WantsIme);
            previous?.OnFocusChanged(false);
            // 前一個目標失焦時可能又把焦點交給別人
            if (Current == target) target?.OnFocusChanged(true);
        }

        public static void Release(ITextTarget target)
        {
            if (Current == target) Set(null);
        }

        /// <summary>這個字是剛拿到焦點時就按著、還沒放開的移動鍵連發出來的。</summary>
        public static bool TypingSuppressed(char c)
        {
            ForgetReleasedKeys();
            var keyboard = Keyboard.current;
            foreach (var key in heldAtFocus)
                if (string.Equals(keyboard[key].displayName, c.ToString(), StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>
        /// 每幀呼叫：放開過的鍵就不再壓制。只在打字時才檢查的話，放開後重新按下同一個鍵，
        /// 它的按下狀態會比文字事件先到，那個字就被誤吞。
        /// </summary>
        internal static void ForgetReleasedKeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) heldAtFocus.Clear();
            else heldAtFocus.RemoveAll(key => !keyboard[key].isPressed);
        }

        public static bool MoveKeyHeld()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && (keyboard.wKey.isPressed || keyboard.aKey.isPressed || keyboard.sKey.isPressed || keyboard.dKey.isPressed);
        }

        internal static void RaiseUnfocused(char c) => Unfocused?.Invoke(c);

        /// <summary>換場景時清掉指向已銷毀物件的焦點。</summary>
        internal static void Reset()
        {
            Current = null;
            holdMovement = false;
            heldAtFocus.Clear();
        }
    }

    /// <summary>系統剪貼簿；測試改用程式內的字串，以免動到使用者的剪貼簿。</summary>
    public static class TextClipboard
    {
        static string local = "";

        public static bool UseSystem { get; set; } = true;

        public static string Text
        {
            get => (UseSystem ? GUIUtility.systemCopyBuffer : local) ?? "";
            set
            {
                if (UseSystem) GUIUtility.systemCopyBuffer = value;
                else local = value;
            }
        }
    }
}

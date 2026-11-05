using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Lexiscript
{
    /// <summary>
    /// 把鍵盤送到目前的 <see cref="TextFocus"/>。文字走 onTextInput，系統的按鍵連發與輸入法都在裡面；
    /// 方向鍵這類編輯鍵沒有文字事件，這裡自己做按住連發。
    /// </summary>
    public sealed class TextInputHub : MonoBehaviour
    {
        const float RepeatDelay = 0.45f, RepeatInterval = 0.035f;

        static readonly Key[] EditKeys =
        {
            Key.LeftArrow, Key.RightArrow, Key.UpArrow, Key.DownArrow, Key.Home, Key.End, Key.PageUp, Key.PageDown,
            Key.Backspace, Key.Delete, Key.Enter, Key.NumpadEnter, Key.Tab, Key.Escape,
        };

        static readonly Key[] ShortcutKeys = { Key.A, Key.B, Key.C, Key.I, Key.U, Key.V, Key.X, Key.Y, Key.Z };

        readonly Dictionary<Key, float> repeatAt = new Dictionary<Key, float>();
        /// <summary>這一幀已經派送過「按下」的鍵：文字事件前先送過的，Update 輪詢時不再重送，反之亦然。</summary>
        readonly HashSet<Key> pressedSent = new HashSet<Key>();
        int pressedSentFrame = -1;
        Keyboard keyboard;
        ITextTarget lastTarget;
        bool composing;
        int compositionEndFrame = -10;

        void OnEnable()
        {
            TextFocus.Reset();
            Bind(Keyboard.current);
        }

        void OnDisable()
        {
            Bind(null);
            TextFocus.Reset();
        }

        void Update()
        {
            Bind(Keyboard.current);
            TextFocus.ForgetReleasedKeys();
            if (keyboard != null) PollKeys();
        }

        void PollKeys()
        {
            if (TextFocus.Current != lastTarget)
            {
                // 關掉輸入法的欄位不會再收到「組字結束」，換焦點時直接當作沒在組字
                lastTarget = TextFocus.Current;
                composing = false;
            }
            bool shift = keyboard.shiftKey.isPressed, ctrl = keyboard.ctrlKey.isPressed;
            foreach (var key in EditKeys) Poll(key, shift, ctrl);
            foreach (var key in ShortcutKeys)
            {
                // 字母鍵沒按 Ctrl 時是打字，不能留著連發計時，否則之後按下 Ctrl 會立刻觸發快捷鍵
                if (ctrl) Poll(key, shift, true);
                else repeatAt.Remove(key);
            }
        }

        void Poll(Key key, bool shift, bool ctrl)
        {
            var control = keyboard[key];
            float now = Time.unscaledTime;
            if (control.wasPressedThisFrame)
            {
                repeatAt[key] = now + RepeatDelay;
                if (!MarkPressSent(key)) return;
            }
            else if (control.isPressed && repeatAt.TryGetValue(key, out float at) && now >= at)
            {
                repeatAt[key] = now + RepeatInterval;
            }
            else
            {
                if (!control.isPressed) repeatAt.Remove(key);
                return;
            }
            Dispatch(key, shift, ctrl);
        }

        void Dispatch(Key key, bool shift, bool ctrl)
        {
            // 組字中與組字剛結束的那一兩幀，Enter、Backspace、方向鍵是在操作輸入法（選字、刪注音），不是在編輯文字
            if (composing || Time.frameCount - compositionEndFrame <= 1) return;
            TextFocus.Current?.OnKey(key, shift, ctrl);
        }

        /// <summary>
        /// 文字在 input update 裡就送來，編輯鍵要等到 Update 才輪詢；卡頓時同一幀先按 Backspace 再打字，
        /// 順序會顛倒。打字前先把這一幀在它之前按下的編輯鍵送出去。
        /// </summary>
        void DispatchKeysPressedBefore()
        {
            bool shift = keyboard.shiftKey.isPressed, ctrl = keyboard.ctrlKey.isPressed;
            foreach (var key in EditKeys) DispatchEarly(key, shift, ctrl);
            if (ctrl)
                foreach (var key in ShortcutKeys) DispatchEarly(key, shift, true);
        }

        void DispatchEarly(Key key, bool shift, bool ctrl)
        {
            if (keyboard[key].wasPressedThisFrame && MarkPressSent(key)) Dispatch(key, shift, ctrl);
        }

        /// <returns>這一幀還沒送過這個鍵的按下。</returns>
        bool MarkPressSent(Key key)
        {
            if (pressedSentFrame != Time.frameCount)
            {
                pressedSent.Clear();
                pressedSentFrame = Time.frameCount;
            }
            return pressedSent.Add(key);
        }

        void EndComposition()
        {
            composing = false;
            compositionEndFrame = Time.frameCount;
        }

        void Bind(Keyboard next)
        {
            if (keyboard == next) return;
            if (keyboard != null)
            {
                keyboard.onTextInput -= OnText;
                keyboard.onIMECompositionChange -= OnComposition;
            }
            keyboard = next;
            repeatAt.Clear();
            if (keyboard != null)
            {
                keyboard.onTextInput += OnText;
                keyboard.onIMECompositionChange += OnComposition;
            }
        }

        void OnText(char c)
        {
            // Backspace、Enter、Tab 與 Ctrl+字母也會送出控制字元，這些鍵已在 Update 處理
            if (char.IsControl(c)) return;
            if (composing)
            {
                // 微軟輸入法選字送出時不一定會再送空的組字字串（Input System 文件也說不能依賴），
                // 收到送出的字就當作組字結束，否則編輯鍵會一直被當成在操作輸入法
                EndComposition();
                TextFocus.Current?.OnComposition("");
            }
            // 先送出的 Enter、Esc 可能已經換掉焦點，所以之後才讀目前的焦點
            DispatchKeysPressedBefore();
            var target = TextFocus.Current;
            if (target == null) TextFocus.RaiseUnfocused(c);
            else if (!TextFocus.TypingSuppressed(c)) target.OnText(c);
        }

        void OnComposition(IMECompositionString composition)
        {
            var text = composition.ToString();
            if (text.Length > 0) composing = true;
            else if (composing) EndComposition();
            TextFocus.Current?.OnComposition(text);
        }
    }

    /// <summary>單行欄位共用的標準編輯鍵，行為跟一般聊天輸入框相同。</summary>
    public static class TextEditKeys
    {
        /// <returns>這個鍵是不是標準編輯鍵（是的話已經套用）。</returns>
        public static bool Apply(TextEditBuffer buffer, Key key, bool shift, bool ctrl)
        {
            switch (key)
            {
                case Key.LeftArrow: buffer.MoveLeft(shift, ctrl); return true;
                case Key.RightArrow: buffer.MoveRight(shift, ctrl); return true;
                case Key.Home: buffer.MoveTo(ctrl ? 0 : buffer.LineStart(buffer.Caret), shift); return true;
                case Key.End: buffer.MoveTo(ctrl ? buffer.Length : buffer.LineEnd(buffer.Caret), shift); return true;
                case Key.Backspace: buffer.Backspace(ctrl); return true;
                case Key.Delete: buffer.Delete(ctrl); return true;
            }
            if (!ctrl) return false;
            switch (key)
            {
                case Key.A: buffer.SelectAll(); return true;
                case Key.C:
                    if (buffer.HasSelection) TextClipboard.Text = buffer.SelectedText;
                    return true;
                case Key.X:
                    if (buffer.HasSelection) TextClipboard.Text = buffer.Cut();
                    return true;
                case Key.V: buffer.Insert(TextClipboard.Text); return true;
                case Key.Z:
                    if (shift) buffer.Redo();
                    else buffer.Undo();
                    return true;
                case Key.Y: buffer.Redo(); return true;
            }
            return false;
        }
    }
}

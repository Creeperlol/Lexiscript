using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Lexiscript
{
    /// <summary>顯示用字型（標題、計數器、設定介面的英文標題）。</summary>
    public static class Fonts
    {
        // Bahnschrift 是 Windows 內建的 DIN 風格幾何字，最接近想要的平面設計感；專案沒有附字型檔，所以借系統字型
        static readonly (string family, string style)[] DisplayCandidates =
        {
            ("Bahnschrift", "SemiBold"), ("Bahnschrift", "Bold"), ("Bahnschrift", "Regular"),
            ("Segoe UI", "Semibold"), ("Segoe UI", "Bold"), ("Arial", "Bold"),
        };

        static TMP_FontAsset display;

        /// <summary>找不到候選字型時退回 TMP 預設字型；中文字形一律由 <see cref="Hud.UiFont"/> 補上。</summary>
        public static TMP_FontAsset Display
        {
            get
            {
                if (display == null) display = LoadDisplay();
                return display;
            }
        }

        static TMP_FontAsset LoadDisplay()
        {
            foreach (var (family, style) in DisplayCandidates)
            {
                var font = TMP_FontAsset.CreateFontAsset(family, style);
                if (font == null) continue;
                font.name = $"{family} {style}";
                font.fallbackFontAssetTable = new List<TMP_FontAsset> { Hud.UiFont };
                return font;
            }
            Debug.LogWarning("No display font found, using the TMP default font.");
            return Hud.UiFont;
        }
    }
}

using UnityEngine;

namespace Lexiscript
{
    /// <summary>
    /// 介面與特效共用的緩動曲線，輸入輸出都是 0 到 1（OutBack 會短暫超過 1）。
    /// 動畫一律不用等速：進場用 Out、退場用 In、位移來回用 InOut、彈出用 OutBack。
    /// </summary>
    public static class Ease
    {
        public static float InCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t;
        }

        public static float OutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return 1 - (1 - t) * (1 - t) * (1 - t);
        }

        public static float InOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 4 * t * t * t : 1 - Mathf.Pow(-2 * t + 2, 3) / 2;
        }

        public static float OutQuint(float t)
        {
            t = Mathf.Clamp01(t);
            return 1 - Mathf.Pow(1 - t, 5);
        }

        /// <param name="overshoot">越大回彈越明顯；1.70158 約超出 10%。</param>
        public static float OutBack(float t, float overshoot = 1.70158f)
        {
            t = Mathf.Clamp01(t) - 1;
            return 1 + (overshoot + 1) * t * t * t + overshoot * t * t;
        }

        /// <summary>0→1→0 的單峰曲線（先快後慢地鼓起再收回），做脈衝、閃光用。</summary>
        public static float Pulse(float t)
        {
            t = Mathf.Clamp01(t);
            return Mathf.Sin(Mathf.PI * OutCubic(t));
        }
    }
}

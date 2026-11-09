using UnityEngine;
using UnityEngine.InputSystem;

namespace Lexiscript
{
    /// <summary>滑鼠滾輪平滑縮放鏡頭；滑鼠在筆記等介面上時，滾輪交給介面。</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraZoom : MonoBehaviour
    {
        const float StepPerNotch = 1.15f, Smoothing = 14f;

        Camera view;

        /// <summary>滾輪設定的目標大小；實際的 orthographicSize 會平滑追上它。</summary>
        public float TargetSize { get; private set; }

        /// <summary>演出用的額外倍率（過橋時拉近），乘在 <see cref="TargetSize"/> 上，不改玩家自己調的大小。</summary>
        public float Multiplier { get; set; } = 1;

        void Awake()
        {
            view = GetComponent<Camera>();
            TargetSize = view.orthographicSize;
        }

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse != null)
            {
                // 輸入系統預設把各平台的滾輪量正規化成每格約 ±1；觸控板會送出零碎的小數
                float notches = Mathf.Clamp(mouse.scroll.ReadValue().y, -3, 3);
                if (notches != 0 && !Hud.ConsumesScroll(mouse.position.ReadValue()))
                    TargetSize = Mathf.Clamp(TargetSize * Mathf.Pow(StepPerNotch, -notches), Art.MinCameraSize, Art.MaxCameraSize);
            }
            view.orthographicSize = Mathf.Lerp(view.orthographicSize, TargetSize * Multiplier, 1 - Mathf.Exp(-Smoothing * Time.unscaledDeltaTime));
        }
    }
}

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Lexiscript
{
    /// <summary>WASD 移動、按住 Shift 疾跑的角色；鏡頭直接跟著它。外觀在子物件的 <see cref="PlayerAvatar"/>。</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class Player : MonoBehaviour
    {
        public const float Speed = 6f;
        public const float SprintMultiplier = 1.8f;

        Rigidbody2D body;
        PhysicsMaterial2D frictionless;

        /// <summary>正在按的方向，各軸是 -1、0 或 1；不能移動（例如正在打字）時是零。</summary>
        public Vector2 Heading { get; private set; }
        public bool Sprinting { get; private set; }

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            // 沒有摩擦，圓形碰撞體推著方塊角時才會順著角滑進方塊之間的縫，不會被摩擦力卡在角上；
            // 組合方式取 Minimum：碰到牆與方塊的預設材質（摩擦 0.4）時取兩者較小的 0
            frictionless = new PhysicsMaterial2D("Frictionless") { friction = 0, frictionCombine = PhysicsMaterialCombine2D.Minimum };
            body.sharedMaterial = frictionless;
        }

        void OnDestroy() => Destroy(frictionless);

        void FixedUpdate()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !TextFocus.MovementAllowed)
            {
                Heading = Vector2.zero;
                Sprinting = false;
                body.linearVelocity = Vector2.zero;
                return;
            }
            Heading = new Vector2(Axis(keyboard.aKey, keyboard.dKey), Axis(keyboard.sKey, keyboard.wKey));
            Sprinting = keyboard.shiftKey.isPressed;
            body.linearVelocity = Heading.normalized * (Sprinting ? Speed * SprintMultiplier : Speed);
        }

        void LateUpdate()
        {
            var camera = Camera.main.transform;
            var position = transform.position;
            camera.position = new Vector3(position.x, position.y, camera.position.z);
        }

        static float Axis(KeyControl negative, KeyControl positive) =>
            (positive.isPressed ? 1 : 0) - (negative.isPressed ? 1 : 0);
    }
}

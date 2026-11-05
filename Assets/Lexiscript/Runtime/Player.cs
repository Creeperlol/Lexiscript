using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Lexiscript
{
    /// <summary>WASD 移動、按住 Shift 疾跑的方形角色；鏡頭直接跟著它。</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class Player : MonoBehaviour
    {
        public const float Speed = 6f;
        public const float SprintMultiplier = 1.8f;

        Rigidbody2D body;

        void Awake() => body = GetComponent<Rigidbody2D>();

        void FixedUpdate()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !TextFocus.MovementAllowed)
            {
                body.linearVelocity = Vector2.zero;
                return;
            }
            var move = new Vector2(Axis(keyboard.aKey, keyboard.dKey), Axis(keyboard.sKey, keyboard.wKey));
            float speed = keyboard.shiftKey.isPressed ? Speed * SprintMultiplier : Speed;
            body.linearVelocity = move.normalized * speed;
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

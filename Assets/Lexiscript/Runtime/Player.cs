using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Lexiscript
{
    /// <summary>WASD 移動的圓盤角色；鏡頭直接跟著它。</summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class Player : MonoBehaviour
    {
        public const float Speed = 6f;

        public Board board;

        Rigidbody2D body;

        void Awake() => body = GetComponent<Rigidbody2D>();

        void FixedUpdate()
        {
            var keyboard = Keyboard.current;
            var move = new Vector2(Axis(keyboard.aKey, keyboard.dKey), Axis(keyboard.sKey, keyboard.wKey));
            body.linearVelocity = board.CanMove ? move.normalized * Speed : Vector2.zero;
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

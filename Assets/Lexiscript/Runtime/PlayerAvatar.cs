using UnityEngine;

namespace Lexiscript
{
    /// <summary>
    /// 角色的外觀，掛在 <see cref="Player"/> 底下、錨在碰撞體底部（腳底）的子物件：依 <see cref="GameSettings.PlayerStyle"/>
    /// 顯示像素小人或原本的小方塊，遊戲中改設定立刻切換。像素小人照 <see cref="Player.Heading"/> 轉向、走路（疾跑時腳步加快），
    /// 停下來會呼吸、隨機眨眼，轉身時先壓扁再彈回。這裡只換網格與縮放，碰撞體不受外觀影響。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class PlayerAvatar : MonoBehaviour
    {
        /// <summary>走路時每個影格的秒數；疾跑時再除以 <see cref="Player.SprintMultiplier"/>。</summary>
        public const float StepSeconds = 0.11f;
        /// <summary>轉身時擠壓伸展的時間。</summary>
        public const float TurnSeconds = 0.2f;
        const float TurnSquash = 0.1f, TurnOvershoot = 3f;
        const float BreathSeconds = 1.3f, BreathOutAt = 0.75f;
        const float BlinkSeconds = 0.12f, MinBlinkGap = 2f, MaxBlinkGap = 5f;
        const int Facings = 4;

        readonly Mesh[] frames = new Mesh[Facings * PixelHero.FrameCount * 2];
        Mesh square;
        MeshFilter filter;
        Player player;
        float walkClock, idleClock, turnedAt = float.NegativeInfinity, blinkAt, blinkUntil;

        public PlayerStyle Style { get; private set; }
        public Facing Facing { get; private set; } = Facing.Down;
        /// <summary>目前顯示的影格：0–1 待機，2–5 走路（見 <see cref="PixelHero"/>）。</summary>
        public int Frame { get; private set; }
        public bool Blinking { get; private set; }

        void OnEnable()
        {
            filter = GetComponent<MeshFilter>();
            player = GetComponentInParent<Player>();
            for (int facing = 0; facing < Facings; facing++)
                for (int frame = 0; frame < PixelHero.FrameCount; frame++)
                    foreach (bool blink in new[] { false, true })
                        frames[Index((Facing)facing, frame, blink)] = PixelHero.Build((Facing)facing, frame, blink);
            square = Art.PlayerSquare();
            // 網格由程式產生，不存進場景（同 Block）
            square.hideFlags = HideFlags.DontSave;
            blinkAt = Time.time + Random.Range(MinBlinkGap, MaxBlinkGap);
            if (Application.isPlaying) GameSettings.Changed += ApplyStyle;
            ApplyStyle();
        }

        void OnDisable()
        {
            GameSettings.Changed -= ApplyStyle;
            for (int i = 0; i < frames.Length; i++)
            {
                if (frames[i] != null) DestroyImmediate(frames[i]);
                frames[i] = null;
            }
            if (square != null) DestroyImmediate(square);
        }

        void ApplyStyle()
        {
            Style = GameSettings.PlayerStyle;
            // 只在擠壓到一半換成方塊時才需要復原；編輯模式不去寫 Transform，免得開場景就被標成有修改
            if (transform.localScale != Vector3.one) transform.localScale = Vector3.one;
            filter.sharedMesh = Style == PlayerStyle.Pixel ? frames[Index(Facing, Frame, Blinking)] : square;
        }

        void Update()
        {
            if (!Application.isPlaying || Style != PlayerStyle.Pixel || player == null) return;
            var heading = player.Heading;
            if (heading == Vector2.zero)
            {
                walkClock = 0;
                idleClock += Time.deltaTime;
                Frame = idleClock % BreathSeconds < BreathOutAt ? 0 : 1;
            }
            else
            {
                Turn(Face(heading, Facing));
                idleClock = 0;
                // 先取影格再累加：起步的第一格就抬腳，一按方向鍵就看得出在走
                Frame = PixelHero.IdleFrames + (int)(walkClock / StepSeconds) % PixelHero.WalkFrames;
                walkClock += Time.deltaTime * (player.Sprinting ? Player.SprintMultiplier : 1);
            }

            if (Time.time >= blinkAt)
            {
                blinkUntil = Time.time + BlinkSeconds;
                blinkAt = blinkUntil + Random.Range(MinBlinkGap, MaxBlinkGap);
            }
            Blinking = Time.time < blinkUntil;

            // 剛轉身時壓扁（變寬變矮），再用 OutBack 彈回：回彈時會先拉長一點才停。子物件錨在腳底，所以腳不會離地
            float t = (Time.time - turnedAt) / TurnSeconds;
            float squash = t < 1 ? TurnSquash * (1 - Ease.OutBack(t, TurnOvershoot)) : 0;
            transform.localScale = new Vector3(1 + squash, 1 - squash, 1);
            filter.sharedMesh = frames[Index(Facing, Frame, Blinking)];
        }

        void Turn(Facing facing)
        {
            if (facing == Facing) return;
            Facing = facing;
            turnedAt = Time.time;
        }

        /// <summary>斜著走時，只要原本的朝向還是按著的方向之一就不轉身，否則每換一次按鍵組合就會轉來轉去。</summary>
        static Facing Face(Vector2 heading, Facing current)
        {
            if (Vector2.Dot(heading, Direction(current)) > 0) return current;
            if (heading.x != 0) return heading.x < 0 ? Facing.Left : Facing.Right;
            return heading.y > 0 ? Facing.Up : Facing.Down;
        }

        static Vector2 Direction(Facing facing) => facing switch
        {
            Facing.Up => Vector2.up,
            Facing.Left => Vector2.left,
            Facing.Right => Vector2.right,
            _ => Vector2.down,
        };

        static int Index(Facing facing, int frame, bool blink) => ((int)facing * PixelHero.FrameCount + frame) * 2 + (blink ? 1 : 0);
    }
}

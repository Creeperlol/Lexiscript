using System.Collections.Generic;
using UnityEngine;

namespace Lexiscript
{
    /// <summary>
    /// 答對的特效：答案欄由左至右刷上淺金、綠字彈一下、一道斜向反光掃過、幾片金白墨的碎片從答案欄爆開後減速縮小，
    /// 最後底色沉澱成淡金並一直保留。沒經過 <see cref="Board.Solved"/> 的解題（讀檔還原）直接補上淡金底，不播動畫。
    /// <see cref="Board"/> 在 Play 時自動加上這個元件，所以每個有盤面的場景都有。
    /// </summary>
    public sealed class SolveEffect : MonoBehaviour
    {
        /// <summary>整段動畫的秒數。</summary>
        public const float Seconds = 0.75f;

        const float SweepSeconds = 0.3f;
        const float PopSeconds = 0.4f, PopFrom = 0.7f, PopOvershoot = 3f;
        const float SheenStart = 0.2f, SheenSeconds = 0.3f, SheenWidth = 0.14f;
        const float SettleStart = 0.35f;
        const float BurstStart = 0.08f, BurstStagger = 0.012f;
        const int ShardCount = 9;
        const float DashLength = 0.16f, DashWidth = 0.028f;

        // 投影到這個方向相同的點落在同一條「/」斜線上；金色前緣與反光都沿著它走
        static readonly Vector2 Across = new Vector2(1, -1);
        static readonly Color Sheen = new Color(1, 1, 1, 0.55f);

        sealed class Play
        {
            public float start;
            public Shard[] shards;
        }

        struct Shard
        {
            public Vector2 from, to;
            public float delay, seconds, size, angle, turn;
            public int kind;
            public Color color;
        }

        readonly HashSet<Block> gilded = new HashSet<Block>();
        readonly Dictionary<Block, Play> playing = new Dictionary<Block, Play>();
        readonly List<Block> finished = new List<Block>();
        Board board;
        Mesh fill, burst;
        bool dirty;

        /// <summary>這格的答案欄已經是金底（解開了）。</summary>
        public bool IsGilded(Block block) => gilded.Contains(block);

        /// <summary>這格正在播答對動畫。</summary>
        public bool IsPlaying(Block block) => playing.ContainsKey(block);

        /// <summary>這格目前被金色蓋住的範圍（方塊本地座標）；還沒解開是空的。</summary>
        public List<Vector2> FillOf(Block block) => gilded.Contains(block) ? Fill(block.shape, Elapsed(block)) : new List<Vector2>();

        /// <summary>答案欄刷到 progress（0～1）時的金色範圍：從左邊開始，前緣是「/」斜邊，1 就是整個答案欄。</summary>
        public static List<Vector2> Sweep(List<Vector2> box, float progress)
        {
            if (progress >= 1) return new List<Vector2>(box);
            var (min, max) = Span(box);
            return Shapes.Clip(box, Across, Mathf.Lerp(min, max, progress));
        }

        void Awake()
        {
            board = GetComponent<Board>();
            var material = board.bridges.GetComponent<MeshRenderer>().sharedMaterial;
            fill = Overlay("SolveFill", material, Art.SelectionOrder);
            burst = Overlay("SolveBurst", material, Art.EffectOrder);
        }

        void OnEnable() => board.Solved += OnSolved;

        void OnDisable() => board.Solved -= OnSolved;

        void OnDestroy()
        {
            Destroy(fill);
            Destroy(burst);
        }

        Mesh Overlay(string name, Material material, int sortingOrder)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = sortingOrder;
            var mesh = new Mesh();
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            return mesh;
        }

        void OnSolved(Block block)
        {
            gilded.Add(block);
            playing[block] = new Play { start = Time.time, shards = Shards(Art.AnswerBox(block.shape), Seed(block.name)) };
        }

        void Update()
        {
            var puzzle = board.Puzzle;
            if (puzzle.SolvedCount != gilded.Count)
            {
                gilded.Clear();
                foreach (var block in board.Blocks)
                    if (puzzle.IsSolved(block.name)) gilded.Add(block);
                dirty = true;
            }
            if (!dirty && playing.Count == 0) return;
            dirty = false;
            Draw();
            finished.Clear();
            foreach (var pair in playing)
            {
                float t = Time.time - pair.Value.start;
                Pop(pair.Key, t);
                if (t >= Seconds) finished.Add(pair.Key);
            }
            foreach (var block in finished) playing.Remove(block);
        }

        /// <summary>沒在播的格子當作已經播完，畫成最後的淡金底。</summary>
        float Elapsed(Block block) => playing.TryGetValue(block, out var play) ? Time.time - play.start : Seconds;

        void Draw()
        {
            var fills = new MeshBuilder();
            var pieces = new MeshBuilder();
            foreach (var block in gilded)
            {
                var toBoard = transform.worldToLocalMatrix * block.transform.localToWorldMatrix;
                float t = Elapsed(block);
                float settle = Ease.OutCubic((t - SettleStart) / (Seconds - SettleStart));
                fills.Add(Place(Fill(block.shape, t), toBoard), Color.Lerp(Art.Gold, Art.PaleGold, settle));
                fills.Add(Place(Band(Art.AnswerBox(block.shape), Ease.InOutCubic((t - SheenStart) / SheenSeconds)), toBoard), Sheen);
                if (playing.TryGetValue(block, out var play))
                    foreach (var shard in play.shards) DrawShard(pieces, shard, t, toBoard);
            }
            fills.Apply(fill);
            pieces.Apply(burst);
        }

        static List<Vector2> Fill(BlockShape shape, float t) => Sweep(Art.AnswerBox(shape), Ease.OutQuint(t / SweepSeconds));

        /// <summary>斜向反光：progress 0 時還在答案欄左邊外面，1 時已經掃出右邊。</summary>
        static List<Vector2> Band(List<Vector2> box, float progress)
        {
            if (progress <= 0 || progress >= 1) return new List<Vector2>();
            var (min, max) = Span(box);
            float from = Mathf.Lerp(min - SheenWidth, max, progress);
            return Shapes.Clip(Shapes.Clip(box, Across, from + SheenWidth), -Across, -from);
        }

        static (float min, float max) Span(List<Vector2> polygon)
        {
            float min = float.MaxValue, max = float.MinValue;
            foreach (var point in polygon)
            {
                float along = Vector2.Dot(point, Across);
                min = Mathf.Min(min, along);
                max = Mathf.Max(max, along);
            }
            return (min, max);
        }

        /// <summary>綠字先縮小，再彈過頭一點回到原本大小。只縮放文字，方塊本身縮放的話碰撞體也會跟著變大。</summary>
        static void Pop(Block block, float t)
        {
            if (block.answerText == null) return;
            float scale = t >= PopSeconds ? 1 : Mathf.LerpUnclamped(PopFrom, 1, Ease.OutBack(t / PopSeconds, PopOvershoot));
            block.answerText.transform.localScale = new Vector3(scale, scale, 1);
        }

        /// <summary>碎片從答案欄外框往四周飛出，一片接一片稍微錯開。</summary>
        static Shard[] Shards(List<Vector2> box, int seed)
        {
            var random = new System.Random(seed);
            float Range(float min, float max) => min + (float)random.NextDouble() * (max - min);
            Vector2 low = box[0], high = box[0];
            foreach (var point in box)
            {
                low = Vector2.Min(low, point);
                high = Vector2.Max(high, point);
            }
            Vector2 center = (low + high) / 2, extent = (high - low) / 2;
            float spin = Range(0, 2 * Mathf.PI);
            var shards = new Shard[ShardCount];
            for (int i = 0; i < shards.Length; i++)
            {
                float angle = spin + (i + Range(-0.3f, 0.3f)) * 2 * Mathf.PI / shards.Length;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                var from = center + direction * Mathf.Min(extent.x / Mathf.Abs(direction.x), extent.y / Mathf.Abs(direction.y));
                double roll = random.NextDouble();
                shards[i] = new Shard
                {
                    from = from,
                    to = from + direction * Range(0.22f, 0.5f),
                    delay = BurstStart + i * BurstStagger,
                    seconds = Range(0.4f, 0.5f),
                    size = Range(0.05f, 0.085f),
                    angle = Range(0, 90),
                    turn = Range(-200, 200),
                    kind = i % 3,
                    color = roll < 0.5 ? Art.Gold : roll < 0.8 ? Art.Paper : Art.Ink,
                };
            }
            return shards;
        }

        /// <summary>方塊、往外飛的細線或三角形；移動先快後慢，最後縮小消失。</summary>
        static void DrawShard(MeshBuilder mesh, Shard shard, float t, Matrix4x4 toBoard)
        {
            float progress = (t - shard.delay) / shard.seconds;
            if (progress <= 0 || progress >= 1) return;
            float move = Ease.OutCubic(progress), shrink = 1 - Ease.InCubic(progress);
            var center = Vector2.Lerp(shard.from, shard.to, move);
            float degrees = shard.angle + shard.turn * move;
            List<Vector2> shape;
            switch (shard.kind)
            {
                case 0:
                    shape = Shapes.Polygon(center, shard.size * shrink, 4, degrees);
                    break;
                case 1:
                    var tail = (shard.to - shard.from).normalized * (DashLength * shrink);
                    shape = Shapes.Segment(center - tail, center, DashWidth * shrink);
                    break;
                default:
                    shape = Shapes.Polygon(center, shard.size * shrink * 1.2f, 3, degrees);
                    break;
            }
            mesh.Add(Place(shape, toBoard), shard.color);
        }

        static List<Vector2> Place(List<Vector2> polygon, Matrix4x4 toBoard)
        {
            var placed = new List<Vector2>(polygon.Count);
            foreach (var point in polygon) placed.Add(toBoard.MultiplyPoint3x4(point));
            return placed;
        }

        // string.GetHashCode 在不同執行環境可能不同；自己算，同一格每次爆開的樣子都一樣，截圖才能比對
        static int Seed(string id)
        {
            int seed = 17;
            foreach (var c in id) seed = seed * 31 + c;
            return seed;
        }
    }
}

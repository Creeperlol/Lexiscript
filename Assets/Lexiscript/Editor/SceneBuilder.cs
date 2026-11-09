using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Lexiscript
{
    /// <summary><see cref="DemoSetup"/> 與 <see cref="GameSetup"/> 共用的場景建構工具。</summary>
    static class SceneBuilder
    {
        public const string Folder = "Assets/Lexiscript/";
        const int RoomOrder = Art.RoomOrder, FloorTextOrder = Art.FloorTextOrder, DoorOrder = Art.DoorOrder, BridgeOrder = Art.BridgeOrder, BlockOrder = Art.BlockOrder;
        const int BlockTextOrder = Art.BlockTextOrder, SpoilerOrder = Art.SpoilerOrder, PlayerOrder = Art.PlayerOrder;

        /// <summary>已存在就沿用，換掉資產會改變 GUID，讓其他場景的參照斷掉。</summary>
        public static Material Material()
        {
            var path = Folder + "Lexiscript.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(AssetDatabase.LoadAssetAtPath<Shader>(Folder + "Runtime/VertexColor.shader"));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>存成資產；已存在就覆寫內容但保留 GUID，兩個場景共用的網格才不會斷。</summary>
        public static T Save<T>(T asset, string name) where T : Object
        {
            var path = Folder + name;
            // CopySerialized 連名稱一起複製，沒命名的話既有資產會變成空名稱，匯入時一直警告
            asset.name = System.IO.Path.GetFileNameWithoutExtension(path);
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(asset, path);
                return asset;
            }
            EditorUtility.CopySerialized(asset, existing);
            Object.DestroyImmediate(asset);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        public static Camera Camera(Vector2 position)
        {
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(UniversalAdditionalCameraData), typeof(AudioListener), typeof(CameraZoom)) { tag = "MainCamera" }.GetComponent<Camera>();
            camera.transform.position = new Vector3(position.x, position.y, -10);
            camera.orthographic = true;
            camera.orthographicSize = Art.CameraSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Art.Background;
            camera.allowMSAA = true;
            return camera;
        }

        public static void Hud() => new GameObject("Hud", typeof(Hud));

        /// <param name="kinds">與 floors 一一對應的用途；Demo 這種只有一塊地板的場景不用給。</param>
        /// <param name="plots">大地圖分區與門後房間的地面標記（見 <see cref="Art.Plot"/>）。</param>
        /// <param name="decor">地板花紋與擺設；會擋路的擺設也加上碰撞，範圍記在 <see cref="Lexiscript.Rooms.obstacles"/>。</param>
        public static Rooms Rooms(Material material, IList<Rect> floors, IList<Rect> doorways, string meshName, int seed,
            IList<RoomKind> kinds = null, IList<(Rect area, BlockColor? accent, bool flipped)> plots = null, RoomsDecor decor = null)
        {
            var mesh = Save(Art.Rooms(floors, doorways, seed, plots, decor), meshName);
            var go = Visual("Rooms", null, mesh, material, RoomOrder);
            var rooms = go.AddComponent<Rooms>();
            rooms.floors = floors.ToArray();
            rooms.doorways = doorways.ToArray();
            rooms.kinds = kinds?.ToArray() ?? new RoomKind[0];
            var obstacles = decor?.Props == null ? new List<Rect>() : decor.Props.Where(prop => prop.Blocking).Select(prop => new Rect(prop.Center - prop.Size / 2, prop.Size)).ToList();
            rooms.obstacles = obstacles.ToArray();
            foreach (var wall in Walls(floors, doorways).Concat(obstacles))
            {
                var collider = go.AddComponent<BoxCollider2D>();
                collider.offset = wall.center;
                collider.size = wall.size;
            }
            return rooms;
        }

        /// <summary>每塊地板四面的牆，扣掉門口與相鄰的地板（走道、側間、橋跟房間之間沒有牆）。</summary>
        static IEnumerable<Rect> Walls(IList<Rect> floors, IList<Rect> doorways)
        {
            float t = Art.WallThickness;
            for (int i = 0; i < floors.Count; i++)
            {
                var f = floors[i];
                var holes = doorways.Concat(floors.Where((_, j) => j != i)).ToList();
                var sides = new[]
                {
                    Rect.MinMaxRect(f.xMin - t, f.yMin - t, f.xMin, f.yMax + t),
                    Rect.MinMaxRect(f.xMax, f.yMin - t, f.xMax + t, f.yMax + t),
                    Rect.MinMaxRect(f.xMin, f.yMin - t, f.xMax, f.yMin),
                    Rect.MinMaxRect(f.xMin, f.yMax, f.xMax, f.yMax + t),
                };
                foreach (var side in sides)
                    foreach (var piece in Shapes.Subtract(side, holes))
                        yield return piece;
            }
        }

        public static GameObject BeginBoard() => new GameObject("Board");

        public static Board EndBoard(GameObject boardObject, Material material)
        {
            var bridges = Visual("Bridges", boardObject.transform, null, material, BridgeOrder).GetComponent<MeshFilter>();
            // Board 是 ExecuteAlways，AddComponent 當下就會 OnEnable；先停用，欄位接好再啟用
            boardObject.SetActive(false);
            var board = boardObject.AddComponent<Board>();
            board.bridges = bridges;
            boardObject.SetActive(true);
            return board;
        }

        public static Block Block(Transform board, Material material, string id, Vector2 position, BlockColor color, BlockShape shape, string clue, string answer, string[] hints)
        {
            var go = Visual(id, board, null, material, BlockOrder);
            go.transform.localPosition = position;
            go.AddComponent<BoxCollider2D>().size = Vector2.one * (2 * Art.Half);
            var block = go.AddComponent<Block>();
            block.color = color;
            block.shape = shape;
            block.clue = clue;
            block.answer = answer;
            block.hintAnswers = hints;
            block.clueText = BlockText("Clue", go.transform, Art.CluePosition, Art.ClueSize, Art.ClueFontMin, Art.ClueFontMax, Art.Paper);
            block.answerText = BlockText("Answer", go.transform, Art.AnswerPosition, Art.AnswerSize, Art.AnswerFontMin, Art.AnswerFontMax, Art.PlaceholderInk);
            return block;
        }

        public static void Link(Block from, Block to, LinkKind kind) => from.links.Add(new Block.Link(to, kind));

        public static Player Player(Material material, Vector2 spawn)
        {
            var go = new GameObject("Player");
            go.transform.position = spawn;
            var body = go.AddComponent<Rigidbody2D>();
            // 模板的 2D 重力是 -9.81，俯視角不需要
            body.gravityScale = 0;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            // 圓形碰撞體推著方塊角時會順著角滑開，玩家稍微沒對準也能滑進方塊之間的縫
            go.AddComponent<CircleCollider2D>().radius = Art.PlayerHalf;
            var player = go.AddComponent<Player>();
            // 外觀放在錨在碰撞體底部的子物件：轉身擠壓、換外觀都不會改到碰撞體，擠壓時腳也不會離地
            var avatar = Visual("Avatar", go.transform, null, material, PlayerOrder);
            avatar.transform.localPosition = new Vector3(0, -Art.PlayerHalf, 0);
            avatar.AddComponent<PlayerAvatar>();
            return player;
        }

        public static TextMeshPro FloorText(Transform parent, string name, string text, Vector2 position, float width, float fontSize)
        {
            var label = Text(name, parent, position, new Vector2(width, 1.2f), FloorTextOrder);
            label.text = text;
            label.fontSize = fontSize;
            label.color = Art.FloorInk;
            label.gameObject.AddComponent<LocalizedText>().english = text;
            return label;
        }

        /// <summary>門與暴雷條都是 ExecuteAlways：先停用、接好欄位再啟用，OnEnable 才拿得到文字元件。</summary>
        public static Door Door(Material material, Board board, Rect doorway, IEnumerable<Block> blocks, int required)
        {
            var go = Visual("Door", null, null, material, DoorOrder);
            go.SetActive(false);
            go.transform.position = doorway.center;
            go.AddComponent<BoxCollider2D>().size = doorway.size;
            var counter = Text("Counter", go.transform, Vector2.zero, new Vector2(doorway.width + 1, 0.8f), DoorOrder + 1);
            counter.fontSize = 3.2f;
            counter.fontStyle = FontStyles.Bold;
            counter.color = Art.Paper;
            var door = go.AddComponent<Door>();
            door.board = board;
            door.blocks = blocks.ToArray();
            door.required = required;
            door.counter = counter;
            go.SetActive(true);
            return door;
        }

        /// <summary>蓋住 doors 都還沒打開前看不到的地板；遮罩在執行時才畫，所以編輯模式看得到裡面。</summary>
        /// <param name="frame">同一間房所有遮罩合起來的範圍，見 <see cref="RoomCover.frame"/>。</param>
        public static RoomCover Cover(Material material, string name, Rect area, Door[] doors, bool locked = true, Rect frame = default)
        {
            var go = Visual(name, null, null, material, Art.CoverOrder);
            go.transform.position = area.center;
            var cover = go.AddComponent<RoomCover>();
            cover.area = area;
            cover.doors = doors;
            cover.locked = locked;
            cover.frame = frame;
            return cover;
        }

        /// <summary>傳送介面列出的地點；order 是列表裡的順序。</summary>
        public static TeleportPoint Teleport(string english, string chinese, Vector2 position, int order)
        {
            var go = new GameObject("Teleport " + english);
            go.transform.position = position;
            var point = go.AddComponent<TeleportPoint>();
            point.english = english;
            point.chinese = chinese;
            point.order = order;
            return point;
        }

        /// <summary>第一次走過教學出口那座橋時的演出；hall 是橋另一頭的大廳。</summary>
        public static WorldEntry WorldEntry(Rect bridge, Rect hall)
        {
            var entry = new GameObject("World Entry").AddComponent<WorldEntry>();
            entry.bridge = bridge;
            entry.hall = hall;
            return entry;
        }

        public static Spoiler Spoiler(Transform parent, Material material, Block block, Vector2 position)
        {
            var go = Visual("Spoiler " + block.name, parent, null, material, SpoilerOrder);
            go.SetActive(false);
            go.transform.position = position;
            var label = Text("Label", go.transform, Vector2.zero, global::Lexiscript.Spoiler.Size - new Vector2(0.2f, 0.06f), SpoilerOrder + 1);
            label.enableAutoSizing = true;
            label.fontSizeMin = 1f;
            label.fontSizeMax = 2.2f;
            var spoiler = go.AddComponent<Spoiler>();
            spoiler.block = block;
            spoiler.label = label;
            go.SetActive(true);
            return spoiler;
        }

        /// <summary>標題畫面排第一，建置後從標題進入教學。</summary>
        public static void RegisterScenes()
        {
            var scenes = new[] { TitleSetup.ScenePath, GameSetup.ScenePath, DemoSetup.ScenePath }
                .Where(path => AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                .Select(path => new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        public static void ConfigurePipeline()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/UniversalRP.asset");
            // 斜切角、菱形與斜線都是斜邊，不開 MSAA 會有明顯鋸齒
            pipeline.msaaSampleCount = 4;
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
        }

        static TextMeshPro BlockText(string name, Transform parent, Vector2 position, Vector2 size, float min, float max, Color color)
        {
            var text = Text(name, parent, position, size, BlockTextOrder);
            text.enableAutoSizing = true;
            text.fontSizeMin = min;
            text.fontSizeMax = max;
            text.color = color;
            return text;
        }

        public static TextMeshPro Text(string name, Transform parent, Vector2 position, Vector2 size, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshPro>();
            // 父物件停用時 TMP 的 Awake 會延到啟用才跑，字級若還是未設定（-99），換行、字級範圍等設定會被預設值蓋掉
            text.fontSize = size.y * 4;
            text.rectTransform.sizeDelta = size;
            // 不設 localPosition：批次模式建場景時 RectTransform 存檔用的 anchoredPosition 不會跟著同步，存出來會變回原點
            text.rectTransform.anchoredPosition = position;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.sortingOrder = sortingOrder;
            return text;
        }

        public static GameObject Visual(string name, Transform parent, Mesh mesh, Material material, int sortingOrder)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = sortingOrder;
            return go;
        }
    }
}

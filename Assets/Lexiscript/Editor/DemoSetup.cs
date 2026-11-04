using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using static Lexiscript.BlockColor;
using static Lexiscript.BlockShape;
using static Lexiscript.LinkKind;

namespace Lexiscript
{
    /// <summary>
    /// 以 <c>-executeMethod Lexiscript.DemoSetup.Run</c> 整個重建示範場景與它用到的資產。
    /// 手動改過 Demo.unity 後就不要再執行，否則會被覆蓋。
    /// </summary>
    public static class DemoSetup
    {
        const string Folder = "Assets/Lexiscript/";
        public const string ScenePath = Folder + "Demo.unity";
        static readonly Vector2Int PlayerCell = new Vector2Int(2, 7);

        public static void Run()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var material = new Material(AssetDatabase.LoadAssetAtPath<Shader>(Folder + "Runtime/VertexColor.shader"));
            AssetDatabase.CreateAsset(material, Folder + "Lexiscript.mat");
            var roomMesh = Art.Room(Art.Floor);
            AssetDatabase.CreateAsset(roomMesh, Folder + "Room.asset");
            var playerMesh = Art.PlayerDisc();
            AssetDatabase.CreateAsset(playerMesh, Folder + "Player.asset");

            Vector2 spawn = (Vector2)PlayerCell * Art.Pitch;
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(UniversalAdditionalCameraData), typeof(AudioListener)) { tag = "MainCamera" }.GetComponent<Camera>();
            camera.transform.position = new Vector3(spawn.x, spawn.y, -10);
            camera.orthographic = true;
            camera.orthographicSize = Art.CameraSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Art.Background;
            camera.allowMSAA = true;

            var room = Visual("Room", null, roomMesh, material, -20);
            var floor = Art.Floor;
            float wall = Art.WallThickness;
            Wall(room, new Vector2(floor.xMin - wall / 2, floor.center.y), new Vector2(wall, floor.height + 2 * wall));
            Wall(room, new Vector2(floor.xMax + wall / 2, floor.center.y), new Vector2(wall, floor.height + 2 * wall));
            Wall(room, new Vector2(floor.center.x, floor.yMin - wall / 2), new Vector2(floor.width + 2 * wall, wall));
            Wall(room, new Vector2(floor.center.x, floor.yMax + wall / 2), new Vector2(floor.width + 2 * wall, wall));

            var boardObject = new GameObject("Board");
            var board = boardObject.transform;
            var bridges = Visual("Bridges", board, null, material, -10).GetComponent<MeshFilter>();
            var hint = Text("Hint", board, Vector2.zero, Art.HintSize, 5);
            hint.fontSize = Art.HintFontSize;
            hint.color = Art.Paper;

            var p = Add(board, material, "P", 2, 8, PureWhite, Beveled, "PERPETUAL", A("BYZANTIUM"), "CONSTANT", "ETERNAL", "ENDLESS", "EVERLASTING", "PERMANENT");
            var l = Add(board, material, "L", 1, 8, PureWhite, Plain, "", A("CONSTANT"));
            var y = Add(board, material, "Y", 3, 8, BrightYellow, Beveled, "", A("CONSTANTINOPLE"), "CONSTANTLY");
            var g = Add(board, material, "G", 4, 8, GoldenBrown, Plain, "", A());
            var stress = Add(board, material, "STRESS", 6, 8, DeepPurple, Plain, "STRESS", A("STRESSED"));
            var desserts = Add(board, material, "DESSERTS", 6, 7, Colorless, Plain, "DESSERTS", A("PUREBLACK"), "BRIGHTYELLOW");
            var testy = Add(board, material, "TESTY", 8, 7, RoyalBlue, Beveled, "TESTY", A("TEST"));
            var best = Add(board, material, "BEST", 8, 8, Colorless, Beveled, "BEST", A("LIGHTGRAY"));
            var son = Add(board, material, "SON", 1, 5, LightGray, Plain, "SON", A("SUN"));
            var sunflower = Add(board, material, "SUNFLOWER", 2, 5, SunsetOrange, Plain, "", A("SUNFLOWER"));
            var flower = Add(board, material, "FLOWER", 3, 5, PureWhite, Plain, "", A("FLOWER"));
            var flour = Add(board, material, "FLOUR", 4, 5, LightGray, Plain, "FLOUR", A("FLOWER"));
            var moon = Add(board, material, "MOON", 6, 4, PureWhite, Plain, "MOON", A("MOON"));
            var spoon = Add(board, material, "SPOON", 7, 5, LightGray, Beveled, "SPOON", A("MOON"), "SOON", "NOON", "JUNE", "TUNE");
            Add(board, material, "DRAWER", 1, 2, PureBlack, Plain, "DRAWER", A("REWARD"));
            Add(board, material, "MAXIMUM", 2, 2, PureBlack, Beveled, "MAXIMUM", A("MINIMUM"));
            Add(board, material, "NIGHT", 3, 2, BrightYellow, Plain, "NIGHT", A("THING"));
            Add(board, material, "WIZARD", 4, 2, RoyalBlue, Plain, "WIZARD", A("LIZARD"));
            Add(board, material, "SPARROW", 5, 2, RoseRed, Plain, "SPARROW", A("BIRD", "SONGBIRD", "ANIMAL"));
            Add(board, material, "SEASON", 6, 2, RoseRed, Beveled, "SEASON", A("SPRING", "SUMMER", "AUTUMN", "FALL", "WINTER"));
            Add(board, material, "MONDAY", 7, 2, EmeraldGreen, Plain, "MONDAY", A("TUESDAY", "WEDNESDAY", "THURSDAY", "FRIDAY", "SATURDAY", "SUNDAY"));
            Add(board, material, "FROG", 8, 2, GoldenBrown, Plain, "FROG", A("TADPOLE", "POLLIWOG"), "EGG");
            Add(board, material, "SCRAMBLED", 1, 1, EmeraldGreen, Beveled, "SCRAMBLED", A("EGGS"));
            Add(board, material, "RUN", 2, 1, DeepPurple, Beveled, "RUN", A("RUNNING"));
            Add(board, material, "MOUSE", 3, 1, Magenta, Plain, "MOUSE", A("MICE"));
            Add(board, material, "QUICKLY", 4, 1, Magenta, Beveled, "QUICKLY", A("ADVERB"));
            Add(board, material, "DECISION", 5, 1, SkyCyan, Plain, "DECISION", A("DECIDE"));
            Add(board, material, "STRONG", 6, 1, SkyCyan, Beveled, "STRONG", A("STRENGTH"));
            Add(board, material, "YOU", 7, 1, GoldenBrown, Beveled, "YOU", A("THOU", "THEE", "YE"));

            Link(p, l, Pointer);
            Link(p, y, Pointer);
            Link(y, g, Pointer);
            Link(stress, desserts, Pointer);
            Link(testy, best, Pointer);
            Link(son, sunflower, Pointer);
            Link(flower, sunflower, Pointer);
            Link(flour, flower, BridgePointer);
            Link(moon, spoon, Bridge);

            // Board 是 ExecuteAlways，AddComponent 當下就會 OnEnable；先停用，欄位接好再啟用
            boardObject.SetActive(false);
            var boardComponent = boardObject.AddComponent<Board>();
            boardComponent.bridges = bridges;
            boardComponent.hint = hint;
            boardObject.SetActive(true);

            var player = Visual("Player", null, playerMesh, material, 10);
            player.transform.position = spawn;
            var body = player.AddComponent<Rigidbody2D>();
            // 模板的 2D 重力是 -9.81，俯視角不需要
            body.gravityScale = 0;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            player.AddComponent<CircleCollider2D>().radius = Art.PlayerRadius;
            player.AddComponent<Player>().board = boardComponent;

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/UniversalRP.asset");
            // 斜切角、菱形與斜線都是斜邊，不開 MSAA 會有明顯鋸齒
            pipeline.msaaSampleCount = 4;
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
        }

        static string[] A(params string[] answers) => answers;

        static Block Add(Transform board, Material material, string id, int x, int y, BlockColor color, BlockShape shape, string clue, string[] answers, params string[] hints)
        {
            var go = Visual(id, board, null, material, 0);
            go.transform.localPosition = new Vector3(x * Art.Pitch, y * Art.Pitch, 0);
            go.AddComponent<BoxCollider2D>().size = Vector2.one * (2 * Art.Half);
            var block = go.AddComponent<Block>();
            block.color = color;
            block.shape = shape;
            block.clue = clue;
            block.answers = answers;
            block.hintAnswers = hints;
            block.clueText = BlockText("Clue", go.transform, Art.TextOffset);
            block.answerText = BlockText("Answer", go.transform, -Art.TextOffset);
            return block;
        }

        static void Link(Block from, Block to, LinkKind kind) => from.links.Add(new Block.Link(to, kind));

        static TextMeshPro BlockText(string name, Transform parent, float y)
        {
            var text = Text(name, parent, new Vector2(0, y), Art.TextSize, 1);
            text.enableAutoSizing = true;
            text.fontSizeMin = Art.FontMin;
            text.fontSizeMax = Art.FontMax;
            text.color = Art.Ink;
            return text;
        }

        static TextMeshPro Text(string name, Transform parent, Vector2 position, Vector2 size, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshPro>();
            text.rectTransform.sizeDelta = size;
            text.rectTransform.localPosition = position;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.sortingOrder = sortingOrder;
            return text;
        }

        static GameObject Visual(string name, Transform parent, Mesh mesh, Material material, int sortingOrder)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingOrder = sortingOrder;
            return go;
        }

        static void Wall(GameObject room, Vector2 center, Vector2 size)
        {
            var collider = room.AddComponent<BoxCollider2D>();
            collider.offset = center;
            collider.size = size;
        }
    }
}

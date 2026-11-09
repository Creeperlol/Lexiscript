using UnityEditor.SceneManagement;
using UnityEngine;
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
        public const string ScenePath = SceneBuilder.Folder + "Demo.unity";
        static readonly Vector2Int PlayerCell = new Vector2Int(2, 7);
        public static readonly Rect Floor = new Rect(-1, -1, 23.5f, 23.5f);

        public static void Run()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var material = SceneBuilder.Material();
            Vector2 spawn = (Vector2)PlayerCell * Art.Pitch;
            SceneBuilder.Camera(spawn);
            SceneBuilder.Rooms(material, new[] { Floor }, new Rect[0], "Room.asset", 1);

            var boardObject = SceneBuilder.BeginBoard();
            var board = boardObject.transform;
            var p = Add(board, material, "P", 2, 8, PureWhite, Beveled, "perpetual", "Byzantium");
            var l = Add(board, material, "L", 1, 8, PureWhite, Plain, "", "constant");
            var y = Add(board, material, "Y", 3, 8, BrightYellow, Beveled, "", "Constantinople", "constantly");
            var g = Add(board, material, "G", 4, 8, GoldenBrown, Plain, "", "");
            var stress = Add(board, material, "STRESS", 6, 8, DeepPurple, Plain, "stress", "stressed");
            var desserts = Add(board, material, "DESSERTS", 6, 7, Colorless, Plain, "desserts", "PureBlack", "BrightYellow");
            var testy = Add(board, material, "TESTY", 8, 7, RoyalBlue, Beveled, "testy", "test");
            var best = Add(board, material, "BEST", 8, 8, Colorless, Beveled, "best", "LightGray");
            var son = Add(board, material, "SON", 1, 5, LightGray, Plain, "son", "sun");
            var sunflower = Add(board, material, "SUNFLOWER", 2, 5, SunsetOrange, Plain, "", "sunflower");
            var flower = Add(board, material, "FLOWER", 3, 5, PureWhite, Plain, "", "flower");
            var flour = Add(board, material, "FLOUR", 4, 5, LightGray, Plain, "flour", "flower");
            var moon = Add(board, material, "MOON", 6, 4, PureWhite, Plain, "moon", "moon");
            var spoon = Add(board, material, "SPOON", 7, 5, LightGray, Beveled, "spoon", "moon", "soon", "noon", "June", "tune");
            Add(board, material, "DRAWER", 1, 2, PureBlack, Plain, "drawer", "reward");
            Add(board, material, "MAXIMUM", 2, 2, PureBlack, Beveled, "maximum", "minimum");
            Add(board, material, "NIGHT", 3, 2, BrightYellow, Plain, "night", "thing");
            Add(board, material, "WIZARD", 4, 2, RoyalBlue, Plain, "wizard", "lizard");
            Add(board, material, "SPARROW", 5, 2, RoseRed, Plain, "sparrow", "bird", "songbird", "animal");
            Add(board, material, "SEASON", 6, 2, RoseRed, Beveled, "season", "spring", "summer", "autumn", "fall", "winter");
            Add(board, material, "MONDAY", 7, 2, EmeraldGreen, Plain, "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday");
            Add(board, material, "FROG", 8, 2, GoldenBrown, Plain, "frog", "tadpole", "polliwog", "egg");
            Add(board, material, "SCRAMBLED", 1, 1, EmeraldGreen, Beveled, "scrambled", "eggs");
            Add(board, material, "RUN", 2, 1, DeepPurple, Beveled, "run", "running");
            Add(board, material, "MOUSE", 3, 1, Magenta, Plain, "mouse", "mice");
            Add(board, material, "QUICKLY", 4, 1, Magenta, Beveled, "quickly", "adverb");
            Add(board, material, "DECISION", 5, 1, SkyCyan, Plain, "decision", "decide");
            Add(board, material, "STRONG", 6, 1, SkyCyan, Beveled, "strong", "strength");
            Add(board, material, "YOU", 7, 1, GoldenBrown, Beveled, "you", "thou", "thee", "ye");

            SceneBuilder.Link(p, l, Pointer);
            SceneBuilder.Link(p, y, Pointer);
            SceneBuilder.Link(y, g, Pointer);
            SceneBuilder.Link(stress, desserts, Pointer);
            SceneBuilder.Link(testy, best, Pointer);
            SceneBuilder.Link(son, sunflower, Pointer);
            SceneBuilder.Link(flower, sunflower, Pointer);
            SceneBuilder.Link(flour, flower, BridgePointer);
            SceneBuilder.Link(moon, spoon, Bridge);
            SceneBuilder.EndBoard(boardObject, material);

            SceneBuilder.Player(material, spawn);
            SceneBuilder.Hud();

            EditorSceneManager.SaveScene(scene, ScenePath);
            SceneBuilder.RegisterScenes();
            SceneBuilder.ConfigurePipeline();
        }

        static Block Add(Transform board, Material material, string id, int x, int y, BlockColor color, BlockShape shape, string clue, string answer, params string[] hints) =>
            SceneBuilder.Block(board, material, id, new Vector2(x, y) * Art.Pitch, color, shape, clue, answer, hints);
    }
}

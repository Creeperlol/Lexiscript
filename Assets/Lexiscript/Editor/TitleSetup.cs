using UnityEditor.SceneManagement;
using UnityEngine;

namespace Lexiscript
{
    /// <summary>
    /// 以 <c>-executeMethod Lexiscript.TitleSetup.Run</c> 重建標題場景：固定的鏡頭、一格純白方角的 START 方塊與 <see cref="TitleScreen"/>。
    /// 標題介面在執行時由 TitleScreen 用程式建立，這裡只放世界裡的東西。
    /// </summary>
    public static class TitleSetup
    {
        public const string ScenePath = SceneBuilder.Folder + "Title.unity";

        public static void Run()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var material = SceneBuilder.Material();
            var camera = SceneBuilder.Camera(TitleScreen.CameraPosition);
            // 標題的構圖是固定的，不讓滾輪縮放
            Object.DestroyImmediate(camera.GetComponent<CameraZoom>());
            camera.orthographicSize = TitleScreen.CameraSize;
            camera.backgroundColor = TitleScreen.Background;

            var boardObject = SceneBuilder.BeginBoard();
            SceneBuilder.Block(boardObject.transform, material, TitleScreen.Word, Vector2.zero, BlockColor.PureWhite, BlockShape.Plain,
                TitleScreen.Word, TitleScreen.Word, new string[0]);
            SceneBuilder.EndBoard(boardObject, material);
            new GameObject("Title", typeof(TitleScreen));

            EditorSceneManager.SaveScene(scene, ScenePath);
            SceneBuilder.RegisterScenes();
            SceneBuilder.ConfigurePipeline();
        }
    }
}

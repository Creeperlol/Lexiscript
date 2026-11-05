using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Lexiscript
{
    /// <summary>方角方塊在 MSAA 下不能沿著網格內部的邊漏出細縫（標題畫面鏡頭固定，漏縫會一直看得到）。</summary>
    public sealed class BlockSeamTests
    {
        const int Width = 1920, Height = 1080;
        const float CameraSize = 1.6f;

        readonly InputHarness input = new InputHarness();

        [SetUp]
        public void SetUp() => input.SetUp();

        [UnityTearDown]
        public IEnumerator TearDown() => input.TearDown();

        [UnityTest]
        public IEnumerator PlainBlockFaceHasNoSeamsAtAnySubpixelCameraPosition()
        {
            SceneManager.LoadScene("Demo");
            yield return null;
            yield return null;
            var material = Object.FindAnyObjectByType<Board>().bridges.GetComponent<MeshRenderer>().sharedMaterial;
            // 放在場景外的空地，只照得到這一格
            var at = new Vector2(2.5f, -20f);
            var builder = new MeshBuilder();
            Art.Block(builder, BlockColor.PureWhite, BlockShape.Plain, false, new Vector2[0]);
            var block = new GameObject("SeamProbe", typeof(MeshFilter), typeof(MeshRenderer));
            block.transform.position = at;
            var mesh = new Mesh();
            builder.Apply(mesh);
            block.GetComponent<MeshFilter>().sharedMesh = mesh;
            block.GetComponent<MeshRenderer>().sharedMaterial = material;
            yield return null;

            // 漏縫只在某些次像素位置出現，所以鏡頭在一個像素內走幾個不同的位置
            for (int k = 0; k < 12; k++)
                Assert.AreEqual(0, SeamPixels(at + new Vector2(k * 0.0007f, k * 0.0011f)), $"鏡頭偏移第 {k} 個位置時，深灰內面上出現了細縫");
        }

        /// <summary>深灰內面上半部（沒有虛線與答案欄的地方）顏色不對的像素數。</summary>
        static int SeamPixels(Vector2 center)
        {
            var camera = Camera.main;
            var savedPosition = camera.transform.position;
            var savedSize = camera.orthographicSize;
            camera.transform.position = new Vector3(center.x, center.y, savedPosition.z);
            camera.orthographicSize = CameraSize;
            var target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
            RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
            var image = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            var active = RenderTexture.active;
            RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            image.Apply();
            RenderTexture.active = active;
            camera.transform.position = savedPosition;
            camera.orthographicSize = savedSize;
            target.Release();

            float pixelsPerUnit = Height / (2 * CameraSize);
            var face = image.GetPixel(Width / 2, (int)(Height / 2 + 0.5f * pixelsPerUnit));
            int seams = 0;
            for (int y = (int)(Height / 2 + 0.1f * pixelsPerUnit); y < (int)(Height / 2 + 0.7f * pixelsPerUnit); y++)
                for (int x = (int)(Width / 2 - 0.65f * pixelsPerUnit); x < (int)(Width / 2 + 0.65f * pixelsPerUnit); x++)
                {
                    var c = image.GetPixel(x, y);
                    if (Mathf.Abs(c.r - face.r) + Mathf.Abs(c.g - face.g) + Mathf.Abs(c.b - face.b) > 0.03f) seams++;
                }
            Object.Destroy(image);
            return seams;
        }
    }
}

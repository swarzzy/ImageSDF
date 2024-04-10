using System.IO;
using Unity.Collections;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace ImgSDF.Editor
{
    [ScriptedImporter(1, new string[] { }, new[] { "png", "PNG" })]
    public class PngDistanceFieldImporter : DistanceFieldImporterBase
    {
        protected override (NativeArray<byte>? Bitmap, int Width, int Height) GetSourceAlphaBitmap(AssetImportContext ctx)
        {
            var bytes = File.ReadAllBytes(ctx.assetPath);
            var srcTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);
            srcTexture.LoadImage(bytes);

            var pixels = srcTexture.GetPixels32();

            var data = new NativeArray<byte>(srcTexture.width * srcTexture.height, Allocator.TempJob);

            for (int i = 0; i < pixels.Length; i++)
            {
                data[i] = (byte)(pixels[i].a > 0 ? 255 : 0);
            }

            return (data, srcTexture.width, srcTexture.height);
        }
    }
}


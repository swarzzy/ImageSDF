using System;
using System.Diagnostics;
using System.IO;
using Unity.Collections;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace ImgSDF.Editor
{
    [ScriptedImporter(1, new string[] { "svg", "SVG" })]
    public class SvgDistanceFieldImporter : DistanceFieldImporterBase
    {
        [SerializeField]
        internal int rasterizationResolution = 2048;

        [SerializeField]
        internal string customCommandLineArgs = string.Empty;

        internal readonly static int[] MaxDistances = new[] { 127, 127, 127, 63, 31 };
        internal readonly static int[] RasterResolutions = new[] { 256, 512, 1024, 2048, 4096 };

#if UNITY_EDITOR_WIN
        private const string ResvgAssetLabel = "l:ImageSDF.Windows.Resvg";
#elif UNITY_EDITOR_OSX
    private const string ResvgAssetLabel = "l:ImageSDF.Macos.Resvg";

    private void MacExecuteCommand(AssetImportContext ctx, string command)
    {
        Process proc = new System.Diagnostics.Process();
        proc.StartInfo.FileName = "/bin/bash";
        proc.StartInfo.Arguments = $"-c \"{command}\"";
        proc.StartInfo.UseShellExecute = false;
        proc.StartInfo.RedirectStandardOutput = true;
        proc.StartInfo.RedirectStandardError = true;
        proc.Start();

        var stderr = proc.StandardError.ReadToEnd();
        var stdout = proc.StandardOutput.ReadToEnd();

        proc.WaitForExit();

        if (proc.ExitCode != 0)
        {
            ctx.LogImportError($"Macos failed. Stderr: {stderr}. Stdout: {stdout}", this);
        }
    }
    // TODO: Linux support
#endif

        private byte[] RenderSvg(AssetImportContext ctx, string assetPath, int resolution)
        {
            var exeAsset = AssetDatabase.FindAssets(ResvgAssetLabel);
            var exePath = AssetDatabase.GUIDToAssetPath(exeAsset[0]);

#if UNITY_EDITOR_OSX
        // Workaround for idiotic Apple "protection" which is trying to force us to pay money
        // for executing code on their computes.
        MacExecuteCommand(ctx, $"xattr -dr com.apple.quarantine {exePath}");
#endif
            var assetsFolder = "Assets/";
            assetPath = assetPath.StartsWith(assetsFolder) ? assetPath.Substring(assetsFolder.Length) : assetPath;

            var startInfo = new ProcessStartInfo(exePath);
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.RedirectStandardError = true;
            startInfo.CreateNoWindow = true;
            startInfo.WorkingDirectory = Application.dataPath;

            var pngOutPath = Path.Combine(Application.temporaryCachePath, "out.png");
            try
            {
                startInfo.Arguments = $"-w {resolution} -h {resolution} {customCommandLineArgs} \"{assetPath}\" \"{pngOutPath}\"";
                var process = Process.Start(startInfo);

                var stderr = process.StandardError.ReadToEnd();

                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    ctx.LogImportError($"An error occurred while rendering SVG file \"{assetPath}\". See details below... {Environment.NewLine} {stderr}", this);
                    return null;
                }

                var bytes = File.ReadAllBytes(pngOutPath);
                return bytes;
            }
            finally
            {
                File.Delete(pngOutPath);
            }
        }

        protected override (NativeArray<byte>? Bitmap, int Width, int Height) GetSourceAlphaBitmap(AssetImportContext ctx)
        {
            var srcTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false, true);

            try
            {
                var attemptResolution = rasterizationResolution;

                bool loadedWithOriginalRes = true;

                while (true)
                {
                    var bytes = RenderSvg(ctx, ctx.assetPath, attemptResolution);

                    if (bytes == null)
                    {
                        return default;
                    }

                    if (srcTexture.LoadImage(bytes))
                    {
                        if (!loadedWithOriginalRes)
                        {
                            ctx.LogImportWarning($"Source file is too big to rasterize with {rasterizationResolution} resolution. Switched to {attemptResolution}", this);
                            rasterizationResolution = attemptResolution;
                            maxDistance = MaxDistances[Array.IndexOf(RasterResolutions, rasterizationResolution)];
                        }

                        break;
                    }

                    loadedWithOriginalRes = false;

                    if (attemptResolution <= 256)
                    {
                        ctx.LogImportError($"Failed to process SVG file. Source file resolution if too big.", this);
                        break;
                    }

                    attemptResolution /= 2;
                }

                using var pixels = srcTexture.GetPixelData<Color32>(0);

                var alphaData = new NativeArray<byte>(srcTexture.width * srcTexture.height, Allocator.TempJob);

                for (int i = 0; i < pixels.Length; i++)
                {
                    alphaData[i] = (byte)(pixels[i].a > 0 ? 255 : 0);
                }

                return (alphaData, srcTexture.width, srcTexture.height);
            }
            finally
            {
                DestroyImmediate(srcTexture);
            }
        }
    }
}
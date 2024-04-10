using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;

public abstract class DistanceFieldImporterBase : ScriptedImporter
{
    [SerializeField]
    internal int targetResolution = 128;

    [SerializeField]
    [Min(0.001f)]
    internal float spritePixelsToUnits = 1;

    [SerializeField]
    internal SpriteMeshType spriteMeshType = SpriteMeshType.Tight;

    [SerializeField]
    internal int spriteExtrude = 1;

    [SerializeField]
    internal SpriteAlignment spriteAlignment;

    [SerializeField]
    internal TextureWrapMode wrapU;

    [SerializeField]
    internal TextureWrapMode wrapV;

    [SerializeField]
    internal TextureWrapMode wrapW;

    [SerializeField]
    internal Vector2 spritePivot = new Vector2(0.5f, 0.5f);

    [SerializeField]
    internal int maxDistance = 31;

    internal readonly static int[] SpriteResolutions = new[] { 64, 128, 256, 512, 1024, 2048 };

    private const int MaxDistanceMaxNormalizedValue = 127;

    private const int MaxSpriteExtrude = 32;

    // Spread values are defined relative to this resolution and will be rescaled
    // for other resolution to match coverage.
    // Example: spread 32 will have length of 32 pixels for 1024x texture and, 64 for 2048x, 128 for 4096x...
    private const int SpreadReferenceResolution = 1024;

    public override void OnImportAsset(AssetImportContext ctx)
    {
        try
        {
            OnImportAssetInternal(ctx);
        }
        catch (Exception e)
        {
            ctx.LogImportError($"An {e.GetType()} exception occurred while importing SVG asset ({e.Message})", this);
        }
    }

    protected abstract (NativeArray<byte>? Bitmap, int Width, int Height) GetSourceAlphaBitmap(AssetImportContext ctx);

    private  void OnImportAssetInternal(AssetImportContext ctx)
    {
        NativeArray<byte> distanceMap = default;
        NativeArray<byte> alphaData = default;
        NativeArray<Color32> result = default;

        try
        {
            var source = GetSourceAlphaBitmap(ctx);
            if (!source.Bitmap.HasValue)
            {
                return;
            }

            alphaData = source.Bitmap.Value;

            var normalizedMaxDistance = Math.Max(source.Width, source.Height) / (SpreadReferenceResolution / (float)maxDistance);
            if (normalizedMaxDistance > 127)
            {
                ctx.LogImportWarning($"Computed SDF max distance value is greater than 127. It will be clamped to 127", this);
            }

            normalizedMaxDistance = Math.Clamp(normalizedMaxDistance, 0, MaxDistanceMaxNormalizedValue);
            spriteExtrude = Math.Clamp(spriteExtrude, 0, MaxSpriteExtrude);

            var startTime = Stopwatch.GetTimestamp();

            targetResolution = Math.Clamp(targetResolution, SpriteResolutions.First(), SpriteResolutions.Last());

            var downscale = Math.Max(source.Width, source.Height) / targetResolution;

            distanceMap = SdfGenerator.GenerateDistanceField(alphaData, source.Width, source.Height, normalizedMaxDistance, downscale, out var outWidth, out var outHeight);

            var elapsedMs = (Stopwatch.GetTimestamp() - startTime) / (Stopwatch.Frequency / 1000);
            //UnityEngine.Debug.Log($"SDF convolution took {elapsedMs} ms");

            result = new NativeArray<Color32>(outWidth * outHeight, Allocator.Temp);

            for (int i = 0; i < distanceMap.Length; i++)
            {
                var c = new Color32(255, 255, 255, distanceMap[i]);
                result[i] = c;
            }

            spritePixelsToUnits = Mathf.Clamp(spritePixelsToUnits, 0.001f, float.MaxValue);

            var settings = new TextureGenerationSettings(TextureImporterType.Sprite);
            settings.enablePostProcessor = true;
            settings.qualifyForSpritePacking = true;

            settings.textureImporterSettings.mipmapEnabled = false;
            settings.textureImporterSettings.spritePixelsPerUnit = spritePixelsToUnits;
            settings.textureImporterSettings.filterMode = FilterMode.Bilinear;
            settings.textureImporterSettings.sRGBTexture = false;
            settings.textureImporterSettings.alphaSource = TextureImporterAlphaSource.FromInput;
            settings.textureImporterSettings.spriteMeshType = spriteMeshType;
            settings.textureImporterSettings.spriteExtrude = (uint)spriteExtrude;
            settings.textureImporterSettings.wrapModeU = wrapU;
            settings.textureImporterSettings.wrapModeV = wrapV;
            settings.textureImporterSettings.wrapModeW = wrapW;

            settings.sourceTextureInformation.containsAlpha = true;
            settings.sourceTextureInformation.hdr = false;
            settings.sourceTextureInformation.width = outWidth;
            settings.sourceTextureInformation.height = outHeight;

            settings.platformSettings.textureCompression = TextureImporterCompression.Uncompressed;

            var assetName = Path.ChangeExtension(ctx.assetPath.Split('/').Last(), string.Empty)
                   .Replace(".", string.Empty);

            settings.spriteImportData = new SpriteImportData[1];
            settings.spriteImportData[0].alignment = spriteAlignment;
            settings.spriteImportData[0].pivot = spritePivot;
            settings.spriteImportData[0].border = Vector4.zero;
            settings.spriteImportData[0].name = assetName;
            settings.spriteImportData[0].rect = new Rect(0.0f, 0.0f, outWidth, outHeight);
            settings.spriteImportData[0].spriteID = assetName;

            var r = TextureGenerator.GenerateTexture(settings, result);

            ctx.AddObjectToAsset(assetName, r.texture, r.thumbNail);
            ctx.SetMainObject(r.texture);
            ctx.AddObjectToAsset(assetName, r.sprites[0]);
        }
        finally
        {
            distanceMap.Dispose();
            result.Dispose();
            alphaData.Dispose();
        }
    }
}

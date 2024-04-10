using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

public abstract class DistanceFieldImporterEditorBase : ScriptedImporterEditor
{
    protected SerializedProperty maxDistanceProp;
    protected SerializedProperty targetResolutionProp;
    protected SerializedProperty spritePixelsPerUnitProp;
    protected SerializedProperty spriteMeshTypeProp;
    protected SerializedProperty spriteExtrudeProp;
    protected SerializedProperty spriteAlignmentProp;
    protected SerializedProperty spritePivotProp;
    protected SerializedProperty wrapUProp;
    protected SerializedProperty wrapVProp;
    protected SerializedProperty wrapWProp;

    protected override bool needsApplyRevert => true;

    protected override bool useAssetDrawPreview => true;

    protected bool showPerAxisWrapModes = false;

    protected static GUIContent[] SpriteResolutionsNames = new[] 
    { 
        new GUIContent("64"),
        new GUIContent("128"),
        new GUIContent("256"),
        new GUIContent("512"),
        new GUIContent("1024"),
        new GUIContent("2048")
    };

    protected static readonly GUIContent[] spriteMeshTypeOptions =
    {
        EditorGUIUtility.TrTextContent("Full Rect"),
        EditorGUIUtility.TrTextContent("Tight"),
    };

    protected static readonly GUIContent spritePixelsPerUnit = EditorGUIUtility.TrTextContent("Pixels Per Unit", "How many pixels in the sprite correspond to one unit in the world.");
    protected static readonly GUIContent spriteExtrude = EditorGUIUtility.TrTextContent("Extrude Edges", "How much empty area to leave around the sprite in the generated mesh.");
    protected static readonly GUIContent spriteMeshType = EditorGUIUtility.TrTextContent("Mesh Type", "Type of sprite mesh to generate.");
    protected static readonly GUIContent spriteAlignment = EditorGUIUtility.TrTextContent("Pivot", "Sprite pivot point in its localspace. May be used for syncing animation frames of different sizes.");
    protected static readonly GUIContent sdfSettingsLabel = EditorGUIUtility.TrTextContent("Distance Field Generation Settings");
    protected static readonly GUIContent maxDistanceLabel = EditorGUIUtility.TrTextContent("Max Distance", "Maximum distance for SDF computation");
    protected static readonly GUIContent maxSpriteResolution = EditorGUIUtility.TrTextContent("Max Sprite Resolution", "Maximum resolution of output sprite");
    protected static readonly GUIContent spriteSettingsLabel = EditorGUIUtility.TrTextContent("Sprite Settings");
    protected static readonly GUIContent textureSettingsLabel = EditorGUIUtility.TrTextContent("Texture Settings");
    protected static readonly GUIContent wrapModeLabel = EditorGUIUtility.TrTextContent("Wrap Mode");
    protected static readonly GUIContent wrapULabel = EditorGUIUtility.TrTextContent("U axis");
    protected static readonly GUIContent wrapVLabel = EditorGUIUtility.TrTextContent("V axis");
    protected static readonly GUIContent wrapWLabel = EditorGUIUtility.TrTextContent("W axis");

    protected static readonly GUIContent[] wrapModeContents =
    {
        EditorGUIUtility.TrTextContent("Repeat"),
        EditorGUIUtility.TrTextContent("Clamp"),
        EditorGUIUtility.TrTextContent("Mirror"),
        EditorGUIUtility.TrTextContent("Mirror Once"),
        EditorGUIUtility.TrTextContent("Per-axis")
    };

    protected static readonly int[] wrapModeValues =
    {
        (int)TextureWrapMode.Repeat,
        (int)TextureWrapMode.Clamp,
        (int)TextureWrapMode.Mirror,
        (int)TextureWrapMode.MirrorOnce,
        -1
    };

    public override void OnEnable()
    {
        base.OnEnable();
        maxDistanceProp = serializedObject.FindProperty(nameof(DistanceFieldImporterBase.maxDistance));
        targetResolutionProp = serializedObject.FindProperty(nameof(DistanceFieldImporterBase.targetResolution));
        spritePixelsPerUnitProp = serializedObject.FindProperty(nameof(DistanceFieldImporterBase.spritePixelsToUnits));
        spriteMeshTypeProp = serializedObject.FindProperty(nameof(DistanceFieldImporterBase.spriteMeshType));
        spriteExtrudeProp = serializedObject.FindProperty(nameof(DistanceFieldImporterBase.spriteExtrude));
        spriteAlignmentProp = serializedObject.FindProperty(nameof(DistanceFieldImporterBase.spriteAlignment));
        spritePivotProp = serializedObject.FindProperty(nameof(DistanceFieldImporterBase.spritePivot));
        wrapUProp = serializedObject.FindProperty(nameof(DistanceFieldImporterBase.wrapU));
        wrapVProp = serializedObject.FindProperty(nameof(DistanceFieldImporterBase.wrapV));
        wrapWProp = serializedObject.FindProperty(nameof(DistanceFieldImporterBase.wrapW));
    }

    protected virtual void DrawBurstInstallationWarning()
    {
#if !IMAGE_SDF_MATHEMATICS_INSTALLED
        EditorGUILayout.HelpBox("Install com.unity.mathematics package to speedup SVG import times.", MessageType.Warning);
#endif

#if !IMAGE_SDF_BURST_INSTALLED
        EditorGUILayout.HelpBox("Install com.unity.burst package to speedup SVG import times.", MessageType.Warning);
#endif
    }

    protected virtual void SpriteGUI()
    {
        EditorGUILayout.LabelField(spriteSettingsLabel);
        EditorGUI.indentLevel++;

        EditorGUILayout.PropertyField(spritePixelsPerUnitProp, spritePixelsPerUnit);
        EditorGUILayout.PropertyField(spriteMeshTypeProp, spriteMeshType);

        EditorGUILayout.IntSlider(spriteExtrudeProp, 0, 32, spriteExtrude);

        EditorGUILayout.PropertyField(spriteAlignmentProp, spriteAlignment);

        if (spriteAlignmentProp.intValue == (int)SpriteAlignment.Custom)
        {
            GUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(spritePivotProp);
            GUILayout.EndHorizontal();
        }

        EditorGUI.indentLevel--;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(textureSettingsLabel);
        EditorGUI.indentLevel++;
        WrapModePopup(wrapUProp, wrapVProp, wrapWProp, false, ref showPerAxisWrapModes, assetTarget == null);
        EditorGUI.indentLevel--;
    }

    private static void WrapModePopup(SerializedProperty wrapU, SerializedProperty wrapV, SerializedProperty wrapW, bool isVolumeTexture, ref bool showPerAxisWrapModes, bool enforcePerAxis)
    {
        // In texture importer settings, serialized properties for things like wrap modes can contain -1;
        // that seems to indicate "use defaults, user has not changed them to anything" but not totally sure.
        // Show them as Repeat wrap modes in the popups.
        var wu = (TextureWrapMode)Mathf.Max(wrapU.intValue, 0);
        var wv = (TextureWrapMode)Mathf.Max(wrapV.intValue, 0);
        var ww = (TextureWrapMode)Mathf.Max(wrapW.intValue, 0);

        // automatically go into per-axis mode if values are already different
        if (wu != wv) showPerAxisWrapModes = true;
        if (isVolumeTexture)
        {
            if (wu != ww || wv != ww) showPerAxisWrapModes = true;
        }

        // It's not possible to determine whether any single texture in the whole selection is using per-axis wrap modes
        // just from SerializedProperty values. They can only tell if "some values in whole selection are different" (e.g.
        // wrap value on U axis is not the same among all textures), and can return value of "some" object in the selection
        // (typically based on object loading order). So in order for more intuitive behavior with multi-selection,
        // we go over the actual objects when there's >1 object selected and some wrap modes are different.
        if (!showPerAxisWrapModes)
        {
            if (wrapU.hasMultipleDifferentValues || wrapV.hasMultipleDifferentValues || (isVolumeTexture && wrapW.hasMultipleDifferentValues))
            {
                if (IsAnyTextureObjectUsingPerAxisWrapMode(wrapU.serializedObject.targetObjects, isVolumeTexture))
                {
                    showPerAxisWrapModes = true;
                }
            }
        }

        int value = showPerAxisWrapModes || enforcePerAxis ? -1 : (int)wu;

        // main wrap mode popup
        if (enforcePerAxis)
        {
            EditorGUILayout.LabelField(wrapModeLabel);
        }
        else
        {
            EditorGUI.BeginChangeCheck();
            EditorGUI.showMixedValue = !showPerAxisWrapModes && (wrapU.hasMultipleDifferentValues || wrapV.hasMultipleDifferentValues || (isVolumeTexture && wrapW.hasMultipleDifferentValues));
            value = EditorGUILayout.IntPopup(wrapModeLabel, value, wrapModeContents, wrapModeValues);
            if (EditorGUI.EndChangeCheck() && value != -1)
            {
                // assign the same wrap mode to all axes, and hide per-axis popups
                wrapU.intValue = value;
                wrapV.intValue = value;
                wrapW.intValue = value;
                showPerAxisWrapModes = false;
            }
            EditorGUI.showMixedValue = false;
        }

        // show per-axis popups if needed
        if (value == -1)
        {
            showPerAxisWrapModes = true;
            EditorGUI.indentLevel++;
            WrapModeAxisPopup(wrapULabel, wrapU);
            WrapModeAxisPopup(wrapVLabel, wrapV);
            if (isVolumeTexture)
            {
                WrapModeAxisPopup(wrapWLabel, wrapW);
            }
            EditorGUI.indentLevel--;
        }
    }

    private static void WrapModeAxisPopup(GUIContent label, SerializedProperty wrapProperty)
    {
        // In texture importer settings, serialized properties for wrap modes can contain -1, which means "use default".
        var wrap = (TextureWrapMode)Mathf.Max(wrapProperty.intValue, 0);
        Rect rect = EditorGUILayout.GetControlRect();
        EditorGUI.BeginChangeCheck();
        EditorGUI.BeginProperty(rect, label, wrapProperty);
        wrap = (TextureWrapMode)EditorGUI.EnumPopup(rect, label, wrap);
        EditorGUI.EndProperty();
        if (EditorGUI.EndChangeCheck())
        {
            wrapProperty.intValue = (int)wrap;
        }
    }

    private static bool IsAnyTextureObjectUsingPerAxisWrapMode(UnityEngine.Object[] objects, bool isVolumeTexture)
    {
        foreach (var o in objects)
        {
            int u = 0, v = 0, w = 0;
            // the objects can be Textures themselves, or texture-related importers
            if (o is Texture)
            {
                var ti = (Texture)o;
                u = (int)ti.wrapModeU;
                v = (int)ti.wrapModeV;
                w = (int)ti.wrapModeW;
            }
            if (o is TextureImporter)
            {
                var ti = (TextureImporter)o;
                u = (int)ti.wrapModeU;
                v = (int)ti.wrapModeV;
                w = (int)ti.wrapModeW;
            }
            if (o is IHVImageFormatImporter)
            {
                var ti = (IHVImageFormatImporter)o;
                u = (int)ti.wrapModeU;
                v = (int)ti.wrapModeV;
                w = (int)ti.wrapModeW;
            }
            u = Mathf.Max(0, u);
            v = Mathf.Max(0, v);
            w = Mathf.Max(0, w);
            if (u != v)
            {
                return true;
            }
            if (isVolumeTexture)
            {
                if (u != w || v != w)
                {
                    return true;
                }
            }
        }
        return false;
    }
}
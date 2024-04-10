using System;
using UnityEditor;
using UnityEngine;

namespace ImgSDF.Editor
{
    [CustomEditor(typeof(SvgDistanceFieldImporter))]
    [CanEditMultipleObjects]
    public sealed class SvgDistanceFieldImporterEditor : DistanceFieldImporterEditorBase
    {
        private SerializedProperty rasterizationResolutionProp;
        private SerializedProperty customCommandLineArgsProp;

        private bool advancedSettingsEnabled;

        private static GUIContent[] RasterResolutionsNames = new[]
        {
        new GUIContent("256"),
        new GUIContent("512"),
        new GUIContent("1024"),
        new GUIContent("2048"),
        new GUIContent("4096")
    };

        private static readonly GUIContent rasterResolutionLabel = EditorGUIUtility.TrTextContent("Rasterization Resolution", "A resolution of bitmap to which SVG content will be rendered for computing SDF.");
        private static readonly GUIContent advancedSettingsLabel = EditorGUIUtility.TrTextContent("Advanced Settings");

        public override void OnEnable()
        {
            base.OnEnable();
            rasterizationResolutionProp = serializedObject.FindProperty(nameof(SvgDistanceFieldImporter.rasterizationResolution));
            customCommandLineArgsProp = serializedObject.FindProperty(nameof(SvgDistanceFieldImporter.customCommandLineArgs));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField(sdfSettingsLabel);
            EditorGUI.indentLevel++;

            var rasterResolution = rasterizationResolutionProp.intValue;
            if (Array.IndexOf(SvgDistanceFieldImporter.RasterResolutions, rasterResolution) == -1)
            {
                rasterResolution = 2048;
            }

            EditorGUI.BeginChangeCheck();
            var newRasterResolution = EditorGUILayout.IntPopup(rasterResolutionLabel, rasterResolution, RasterResolutionsNames, SvgDistanceFieldImporter.RasterResolutions);
            if (EditorGUI.EndChangeCheck())
            {
                rasterizationResolutionProp.intValue = newRasterResolution;
                var newMaxDist = SvgDistanceFieldImporter.MaxDistances[Array.IndexOf(SvgDistanceFieldImporter.RasterResolutions, rasterizationResolutionProp.intValue)];
                maxDistanceProp.intValue = Math.Min(maxDistanceProp.intValue, newMaxDist);
            }

            EditorGUI.BeginChangeCheck();
            var newDistance = EditorGUILayout.IntSlider(maxDistanceLabel, maxDistanceProp.intValue, 0, SvgDistanceFieldImporter.MaxDistances[Array.IndexOf(SvgDistanceFieldImporter.RasterResolutions, rasterizationResolutionProp.intValue)]);
            if (EditorGUI.EndChangeCheck())
            {
                maxDistanceProp.intValue = newDistance;
            }

            EditorGUI.BeginChangeCheck();
            var newSpriteResolution = EditorGUILayout.IntPopup(maxSpriteResolution, targetResolutionProp.intValue, SpriteResolutionsNames, SvgDistanceFieldImporter.SpriteResolutions);
            if (EditorGUI.EndChangeCheck())
            {
                targetResolutionProp.intValue = newSpriteResolution;
            }

            EditorGUI.indentLevel--;

            EditorGUILayout.Space();

            SpriteGUI();

            advancedSettingsEnabled = EditorGUILayout.BeginFoldoutHeaderGroup(advancedSettingsEnabled, advancedSettingsLabel);
            if (advancedSettingsEnabled)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(customCommandLineArgsProp);
                EditorGUI.indentLevel--;
            }

            DrawBurstInstallationWarning();

            serializedObject.ApplyModifiedProperties();

            ApplyRevertGUI();
        }
    }
}
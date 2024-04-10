using UnityEditor;

[CustomEditor(typeof(PngDistanceFieldImporter))]
[CanEditMultipleObjects]
public sealed class PngDistanceFieldImporterEditor : DistanceFieldImporterEditorBase
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField(sdfSettingsLabel);
        EditorGUI.indentLevel++;

        EditorGUI.BeginChangeCheck();
        var newDistance = EditorGUILayout.IntSlider(maxDistanceLabel,  maxDistanceProp.intValue, 0, 127);
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

        DrawBurstInstallationWarning();

        serializedObject.ApplyModifiedProperties();

        ApplyRevertGUI();
    }
}
using UnityEditor;
using UnityEditor.UI;
using UnityEngine;

namespace Saritasa.SDF
{
    [CustomEditor(typeof(ImageSDF), true)]
    [CanEditMultipleObjects]
    public class ImageSDFInspector : ImageEditor
    {
        public override void OnInspectorGUI()
        {
            var targetImage = (ImageSDF)target;
            var parentCanvas = targetImage.GetComponentInParent<Canvas>();

            if (parentCanvas != null && (parentCanvas.additionalShaderChannels & AdditionalCanvasShaderChannels.TexCoord1) == 0)
            {
                EditorGUILayout.HelpBox("Image SDF requires TexCoord1 additional shader channel to be enabled on parent canvas.", MessageType.Error);
            }

            base.OnInspectorGUI();
        }
    }
}
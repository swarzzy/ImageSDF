using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Saritasa.SDF
{
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(CanvasRenderer))]
    [ExecuteAlways]
    [AddComponentMenu("UI/Image SDF", 12)]
    public class ImageSDF : Image
    {
        private Mesh tempBufferMesh;

        protected override void OnPopulateMesh(VertexHelper toFill)
        {
            base.OnPopulateMesh(toFill);

            if (tempBufferMesh == null)
            {
                tempBufferMesh = new();
            }

            toFill.FillMesh(tempBufferMesh);

            var vertices = tempBufferMesh.vertices;
            var colors = tempBufferMesh.colors32;
            var normals = tempBufferMesh.normals;
            var tangents = tempBufferMesh.tangents;
            var indices = tempBufferMesh.triangles;

            var uv0 = new List<Vector4>();
            var uv1 = new List<Vector4>();
            var uv2 = new List<Vector4>();
            var uv3 = new List<Vector4>();
            tempBufferMesh.GetUVs(0, uv0);
            tempBufferMesh.GetUVs(1, uv1);
            tempBufferMesh.GetUVs(2, uv2);
            tempBufferMesh.GetUVs(3, uv3);

            toFill.Clear();

            var scale = GetSdfScale();
            var uv2sdf = new Vector2(0.0f, scale);

            for (int i = 0; i < vertices.Length; i++)
            {
                toFill.AddVert(vertices[i], colors[i], uv0[i], uv2sdf, uv2[i], uv3[i], normals[i], tangents[i]);
            }

            for (int i = 0; i < indices.Length; i += 3)
            {
                toFill.AddTriangle(indices[i], indices[i + 1], indices[i + 2]);
            }

            tempBufferMesh.Clear();
        }

        private float previousLossyScaleY = -1; // Used for Tracking lossy scale changes in the transform;

        private float GetSdfScale()
        {
            var xScale = 1.0f;//characterInfos[i].scale * (1 - m_charWidthAdjDelta);

            switch (canvas.renderMode)
            {
                case RenderMode.ScreenSpaceOverlay:
                    xScale *= Mathf.Abs(transform.lossyScale.y) / canvas.scaleFactor;
                    break;
                case RenderMode.ScreenSpaceCamera:
                    xScale *= Mathf.Abs(transform.lossyScale.y);
                    break;
                case RenderMode.WorldSpace:
                    xScale *= Mathf.Abs(transform.lossyScale.y);
                    break;
            }

            return xScale;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            var parentCanvas = GetComponentInParent<Canvas>();

            if (parentCanvas == null)
            {
                Debug.LogError("Parent canvas not found!");
            }

            if ((parentCanvas.additionalShaderChannels & AdditionalCanvasShaderChannels.TexCoord1) == 0)
            {
                Debug.LogWarning("Image SDF requires TexCoord1 additional shader channel to be enabled on parent canvas.");
            }
        }
#endif

        protected override void OnEnable()
        {
            ImageSdfUpdateManager.RegisterObject(this);
            base.OnEnable();
        }

        protected override void OnDisable()
        {
            ImageSdfUpdateManager.UnregisterObject(this);
            base.OnDisable();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            //Destroy(tempBufferMesh);
            //tempBufferMesh = null;
        }

        internal void DoUpdate()
        {
            // We need to update the SDF scale or possibly regenerate the text object if lossy scale has changed.
            float lossyScaleY = transform.lossyScale.y;

            // Ignore very small lossy scale changes as their effect on SDF Scale would not be visually noticeable.
            // Do not update SDF Scale if the text is null or empty
            if (Mathf.Abs(lossyScaleY - previousLossyScaleY) > 0.0001f)
            {
                float scaleDelta = lossyScaleY / previousLossyScaleY;
                UpdateGeometry();
                previousLossyScaleY = lossyScaleY;
            }
        }
    }
}
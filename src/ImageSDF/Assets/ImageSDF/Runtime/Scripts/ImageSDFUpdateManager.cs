using System;
using System.Collections.Generic;
using UnityEngine;

namespace Saritasa.SDF
{
    public static class ImageSdfUpdateManager
    {
        private static readonly HashSet<int> updateLookup = new HashSet<int>();
        private static readonly List<ImageSDF> updateQueue = new List<ImageSDF>();

        static ImageSdfUpdateManager()
        {
            Canvas.willRenderCanvases += DoUpdate;
        }

        public static void RegisterObject(ImageSDF image)
        {
            int id = image.GetInstanceID();

            if (updateLookup.Contains(id))
            {
                return;
            }

            updateLookup.Add(id);
            updateQueue.Add(image);
        }

        public static void UnregisterObject(ImageSDF image)
        {
            int id = image.GetInstanceID();

            if (!updateLookup.Contains(id))
            {
                return;
            }

            updateLookup.Remove(id);
            updateQueue.Remove(image);
        }

        private static void DoUpdate()
        {
            for (int i = 0; i < updateQueue.Count; i++)
            {
                var entry = updateQueue[i];
                try
                {
                    entry.DoUpdate();
                }
                catch (Exception e)
                {
                    Debug.LogError($"An exception occured while updating ImageSDF component: {e.Message}");
                }
            }
        }
    }
}


using UnityEditor;
using UnityEngine;

namespace UnityTools.Editor.AssetSyncTool
{
    public static class AssetSyncMenuItems
    {
        [MenuItem("Assets/Asset Sync/Mark for Sync", false, 20)]
        private static void MarkForSync()
        {
            var selectedGuids = Selection.assetGUIDs;
            foreach (var guid in selectedGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path))
                {
                    AssetSyncManager.MarkAsset(guid, path);
                }
            }
        }

        [MenuItem("Assets/Asset Sync/Mark for Sync", true)]
        private static bool MarkForSyncValidate()
        {
            // Only show if items are selected
            return Selection.assetGUIDs.Length > 0;
        }

        [MenuItem("Assets/Asset Sync/Unmark from Sync", false, 21)]
        private static void UnmarkFromSync()
        {
            var selectedGuids = Selection.assetGUIDs;
            foreach (var guid in selectedGuids)
            {
                AssetSyncManager.UnmarkAsset(guid);
            }
        }

        [MenuItem("Assets/Asset Sync/Unmark from Sync", true)]
        private static bool UnmarkFromSyncValidate()
        {
            // Only show if items are selected and at least one is marked?
            // For simplicity, just check selection.
            if (Selection.assetGUIDs.Length == 0) return false;
            
            // Optional: Check if any is actually marked to gray it out
            foreach (var guid in Selection.assetGUIDs)
            {
                if (AssetSyncManager.IsMarked(guid)) return true;
            }
            return false;
        }
    }
}

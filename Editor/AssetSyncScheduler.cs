using System;
using UnityEditor;
using UnityEngine;

namespace UnityTools.Editor.AssetSyncTool
{
    [InitializeOnLoad]
    public static class AssetSyncScheduler
    {
        private static double _lastCheckTime;

        static AssetSyncScheduler()
        {
            EditorApplication.update += OnUpdate;
        }

        private static void OnUpdate()
        {
            // Check every 10 seconds to avoid spamming
            if (EditorApplication.timeSinceStartup - _lastCheckTime < 10.0) return;
            _lastCheckTime = EditorApplication.timeSinceStartup;

            var storage = AssetSyncManager.Storage;
            // Global Sync
            if (storage.IsAutoSyncEnabled)
            {
                TimeSpan timeSinceLastSync = storage.LastAutoSyncTime > 0 ? DateTime.Now - new DateTime(storage.LastAutoSyncTime) : TimeSpan.FromDays(1);
                if (timeSinceLastSync.TotalMinutes >= storage.AutoSyncIntervalMinutes)
                {
                    Debug.Log("[Asset Sync] Global auto-sync triggered.");
                    AssetSyncManager.SyncAll(force: false, silent: true);
                    storage.LastAutoSyncTime = DateTime.Now.Ticks;
                    AssetSyncManager.Save();
                }
            }

            // Group Syncs
            foreach (var groupSchedule in storage.GroupSchedules)
            {
                if (!groupSchedule.IsEnabled) continue;

                TimeSpan groupSinceLastSync = groupSchedule.LastSyncTime > 0 ? DateTime.Now - new DateTime(groupSchedule.LastSyncTime) : TimeSpan.FromDays(1);
                if (groupSinceLastSync.TotalMinutes >= groupSchedule.IntervalMinutes)
                {
                    Debug.Log($"[Asset Sync] Auto-sync triggered for group: {groupSchedule.GroupKey} ({groupSchedule.Mode})");
                    AssetSyncManager.SyncGroup(groupSchedule.GroupKey, groupSchedule.Mode, force: false, silent: true);
                    groupSchedule.LastSyncTime = DateTime.Now.Ticks;
                    AssetSyncManager.Save();
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityTools.Editor.AssetSyncTool
{
    [Serializable]
    public class SyncItem
    {
        public string AssetPath; // Relative path in Unity Project (e.g. "Assets/Textures/...")
        public string Guid;
        public bool IsFolder;
        public long LastSyncTime;
        public bool IsEnabled = true;
        public string Category = "General";
        public string LastChecksum;

        public SyncItem(string guid, string path, bool isFolder)
        {
            Guid = guid;
            AssetPath = path;
            IsFolder = isFolder;
            LastSyncTime = 0;
            IsEnabled = true;
            Category = "General";
            LastChecksum = "";
        }
    }

    public enum LogType { Info, Success, Warning, Error }

    [Serializable]
    public class SyncLog
    {
        public string Message;
        public string Timestamp;
        public LogType Type;

        public SyncLog(string message, LogType type)
        {
            Message = message;
            Timestamp = DateTime.Now.ToString("HH:mm:ss");
            Type = type;
        }
    }

    [Serializable]
    public class GroupSchedule
    {
        public string GroupKey;
        public string Mode; // "Directory", "Type", "Custom"
        public bool IsEnabled = false;
        public int IntervalMinutes = 60;
        public long LastSyncTime;
        public string DestinationPath = "";

        public GroupSchedule(string key, string mode)
        {
            GroupKey = key;
            Mode = mode;
        }
    }

    [Serializable]
    public class AssetSyncStorage
    {
        public List<SyncItem> Items = new List<SyncItem>();
        public string DestinationPath = "";
        public List<SyncLog> History = new List<SyncLog>();
        
        // Scheduling
        public bool IsAutoSyncEnabled = false;
        public int AutoSyncIntervalMinutes = 60;
        public long LastAutoSyncTime;
        public List<GroupSchedule> GroupSchedules = new List<GroupSchedule>();
    }

    public static class AssetSyncManager
    {
        private const string PREFS_KEY = "AssetSyncTool_Data";
        private static AssetSyncStorage _storage;

        // Events
        public static event Action OnPreSync;
        public static event Action OnPostSync;
        public static event Action<float, string> OnSyncProgress;

        public static AssetSyncStorage Storage
        {
            get
            {
                if (_storage == null) Load();
                return _storage;
            }
        }

        public static void Load()
        {
            string json = EditorPrefs.GetString(PREFS_KEY, "");
            if (!string.IsNullOrEmpty(json))
            {
                _storage = JsonUtility.FromJson<AssetSyncStorage>(json);
            }
            
            if (_storage == null)
            {
                _storage = new AssetSyncStorage();
            }
        }

        public static void Save()
        {
            if (_storage == null) return;
            string json = JsonUtility.ToJson(_storage);
            EditorPrefs.SetString(PREFS_KEY, json);
        }

        public static bool IsMarked(string guid)
        {
            return Storage.Items.Any(i => i.Guid == guid);
        }

        public static void MarkAsset(string guid, string path)
        {
            if (IsMarked(guid)) return;

            bool isFolder = AssetDatabase.IsValidFolder(path);
            _storage.Items.Add(new SyncItem(guid, path, isFolder));
            Save();
        }

        public static void UnmarkAsset(string guid)
        {
            _storage.Items.RemoveAll(i => i.Guid == guid);
            Save();
        }

        public static void SetDestination(string path)
        {
            _storage.DestinationPath = path;
            Save();
        }

        public static void AddHistory(string message, LogType type = LogType.Info)
        {
            _storage.History.Add(new SyncLog(message, type));
            if (_storage.History.Count > 100) // Increased limit
            {
                _storage.History.RemoveAt(0);
            }
            Save();
        }

        public static void ClearHistory()
        {
            _storage.History.Clear();
            Save();
        }

        public static void TriggerPostSync()
        {
            OnPostSync?.Invoke();
            EditorUtility.ClearProgressBar();
            Debug.Log($"[Asset Sync] Sync complete.");
        }

        public static void SyncAll(bool force = false, bool silent = false, string categoryFilter = null)
        {
             var itemsToSync = _storage.Items.Where(i => i.IsEnabled).ToList();
             if (!string.IsNullOrEmpty(categoryFilter))
             {
                 itemsToSync = itemsToSync.Where(i => i.Category == categoryFilter).ToList();
             }
             
             SyncItems(items: itemsToSync, force: force, silent: silent);
        }

        public static void SyncGroup(string groupKey, string mode, bool force = false, bool silent = false)
        {
             var itemsToSync = new List<SyncItem>();
             foreach(var item in _storage.Items)
             {
                 if (!item.IsEnabled) continue;
                 
                 string key = "";
                 if (mode == "Directory")
                 {
                      var parts = item.AssetPath.Split('/');
                      key = parts.Length > 1 ? parts[1] : "Root";
                 }
                 else if (mode == "Custom")
                 {
                      key = string.IsNullOrEmpty(item.Category) ? "General" : item.Category;
                 }
                 
                 if (key == groupKey) itemsToSync.Add(item);
             }
             
             if (itemsToSync.Count > 0)
             {
                 var schedule = GetGroupSchedule(groupKey, mode);
                 SyncItems(items: itemsToSync, overrideDestination: schedule?.DestinationPath, force: force, silent: silent);
             }
        }

        public static GroupSchedule GetGroupSchedule(string key, string mode)
        {
            var schedule = _storage.GroupSchedules.Find(s => s.GroupKey == key && s.Mode == mode);
            if (schedule == null)
            {
                schedule = new GroupSchedule(key, mode);
                _storage.GroupSchedules.Add(schedule);
                Save();
            }
            return schedule;
        }

        public static void SyncItems(List<SyncItem> items, string overrideDestination = null, bool force = false, bool silent = false)
        {
            string targetPath = !string.IsNullOrEmpty(overrideDestination) ? overrideDestination : _storage.DestinationPath;

             if (string.IsNullOrEmpty(targetPath))
            {
                Debug.LogError("[Asset Sync] No destination path selected!");
                return;
            }

            if (!Directory.Exists(targetPath))
            {
                try
                {
                    Directory.CreateDirectory(targetPath);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Asset Sync] Failed to create destination directory: {e.Message}");
                    return;
                }
            }

            OnPreSync?.Invoke();

            // Only cancel if we are starting a fresh sync that might conflict? 
            // Or maybe append? For now, simplistic approach: Cancel previous.
            AssetSyncQueue.CancelAll(); 

            int totalItems = items.Count;
            int count = 0;

            try
            {
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    // item.IsEnabled check is done by caller usually, but safe to ignore here if passed explicitly
                    
                    var currentItem = item;
                    int currentIndex = count;
                    
                    AssetSyncQueue.Enqueue(() => 
                    {
                        if (!silent)
                        {
                            float progress = (float)currentIndex / totalItems;
                            EditorUtility.DisplayProgressBar("Asset Sync", $"Syncing {currentItem.AssetPath}...", progress);
                            OnSyncProgress?.Invoke(progress, currentItem.AssetPath);
                        }

                        if (SyncItemLogic(currentItem, targetPath, force))
                        {
                             // Only log individual successes if not silent/bulk to avoid spam? 
                             // No, user wants more info.
                             AddHistory($"Synced: {currentItem.AssetPath}", LogType.Success);
                        }
                    });
                    
                    count++;
                }

                if (count == 0 && !silent)
                {
                     EditorUtility.DisplayDialog("Asset Sync", "No items to sync.", "OK");
                }
                else
                {
                    AddHistory($"Started sync for {count} items", LogType.Info);
                }
            }
            catch (Exception ex)
            {
                string error = $"Error during sync queueing: {ex.Message}";
                Debug.LogError($"[Asset Sync] {error}");
                AddHistory(error, LogType.Error);
            }
        }

        private static bool SyncItemLogic(SyncItem item, string rootDestination, bool force)
        {
             // Refresh path from GUID in case it moved
            string currentPath = AssetDatabase.GUIDToAssetPath(item.Guid);
            if (string.IsNullOrEmpty(currentPath))
            {
                Debug.LogWarning($"[Asset Sync] Asset with GUID {item.Guid} not found. Skipping.");
                AddHistory($"Missing Asset: {item.AssetPath}", LogType.Warning);
                return false;
            }
            
            // Update stored path if changed
            item.AssetPath = currentPath; 
            
            bool changed = false;
            if (item.IsFolder)
            {
                changed = SyncFolder(currentPath, rootDestination, force);
            }
            else
            {
                changed = SyncFile(currentPath, rootDestination, force, item);
            }
            item.LastSyncTime = DateTime.Now.Ticks;
            return changed;
        }

        private static bool SyncFile(string assetPath, string rootDestination, bool force, SyncItem item = null)
        {
            string sourceAbsPath = Path.GetFullPath(assetPath);
            string relPath = assetPath; 
            string destAbsPath = Path.Combine(rootDestination, relPath);

            if (!File.Exists(sourceAbsPath)) return false;

            // Incremental Check
            string currentChecksum = CalculateChecksum(sourceAbsPath);
            if (!force && item != null && File.Exists(destAbsPath))
            {
                 if (currentChecksum == item.LastChecksum)
                 {
                     // No changes
                     return false;
                 }
            }

            string destDir = Path.GetDirectoryName(destAbsPath);
            if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

            File.Copy(sourceAbsPath, destAbsPath, true);
            
            if (item != null)
            {
                item.LastChecksum = currentChecksum;
            }
            return true;
        }

        private static bool SyncFolder(string folderPath, string rootDestination, bool force)
        {
            string sourceAbsPath = Path.GetFullPath(folderPath);
            string[] files = Directory.GetFiles(sourceAbsPath, "*", SearchOption.AllDirectories);
            bool anyChanged = false;
            
            foreach (string file in files)
            {
                if (file.EndsWith(".meta")) continue; 
                
                string relPath = GetRelativePath(file, Path.GetFullPath("."));
                string destPath = Path.Combine(rootDestination, relPath);

                bool shouldCopy = true;
                if (!force && File.Exists(destPath))
                {
                    string srcHash = CalculateChecksum(file);
                    string destHash = CalculateChecksum(destPath);
                    if (srcHash == destHash) shouldCopy = false;
                }

                if (shouldCopy)
                {
                    string destDir = Path.GetDirectoryName(destPath);
                    if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
                    File.Copy(file, destPath, true);
                    anyChanged = true;
                }
            }
            return anyChanged;
        }
        
        public static string CalculateChecksum(string filePath)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    var hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }
        
        private static string GetRelativePath(string fullPath, string basePath)
        {
            // Ensure trailing slash
            if (!basePath.EndsWith(Path.DirectorySeparatorChar.ToString()) && !basePath.EndsWith(Path.AltDirectorySeparatorChar.ToString()))
                basePath += Path.DirectorySeparatorChar;
                
            Uri baseUri = new Uri(basePath);
            Uri fullUri = new Uri(fullPath);
            return Uri.UnescapeDataString(baseUri.MakeRelativeUri(fullUri).ToString().Replace('/', Path.DirectorySeparatorChar));
        }
    }
}

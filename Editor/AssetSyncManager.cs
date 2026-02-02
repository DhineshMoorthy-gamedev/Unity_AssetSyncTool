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

        public SyncItem(string guid, string path, bool isFolder)
        {
            Guid = guid;
            AssetPath = path;
            IsFolder = isFolder;
            LastSyncTime = 0;
            IsEnabled = true;
        }
    }

    [Serializable]
    public class SyncLog
    {
        public string Message;
        public string Timestamp;
        public bool IsError;

        public SyncLog(string message, bool isError)
        {
            Message = message;
            Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            IsError = isError;
        }
    }

    [Serializable]
    public class AssetSyncStorage
    {
        public List<SyncItem> Items = new List<SyncItem>();
        public string DestinationPath = "";
        public List<SyncLog> History = new List<SyncLog>();
    }

    public static class AssetSyncManager
    {
        private const string PREFS_KEY = "AssetSyncTool_Data";
        private static AssetSyncStorage _storage;

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

        public static void AddHistory(string message, bool isError = false)
        {
            _storage.History.Add(new SyncLog(message, isError));
            if (_storage.History.Count > 50) // Limit history
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

        public static void SyncAll()
        {
            if (string.IsNullOrEmpty(_storage.DestinationPath))
            {
                Debug.LogError("[Asset Sync] No destination path selected!");
                return;
            }

            if (!Directory.Exists(_storage.DestinationPath))
            {
                try
                {
                    Directory.CreateDirectory(_storage.DestinationPath);
                }
                catch (Exception e)
                {
                    Debug.LogError($"[Asset Sync] Failed to create destination directory: {e.Message}");
                    return;
                }
            }

            int count = 0;
            try
            {
                for (int i = 0; i < _storage.Items.Count; i++)
                {
                    var item = _storage.Items[i];
                    if (!item.IsEnabled) continue;

                    EditorUtility.DisplayProgressBar("Asset Sync", $"Syncing {item.AssetPath}...", (float)i / _storage.Items.Count);

                    // Refresh path from GUID in case it moved
                    string currentPath = AssetDatabase.GUIDToAssetPath(item.Guid);
                    if (string.IsNullOrEmpty(currentPath))
                    {
                        Debug.LogWarning($"[Asset Sync] Asset with GUID {item.Guid} not found. Skipping.");
                        continue;
                    }
                    
                    // Update stored path if changed
                    item.AssetPath = currentPath; 
                    
                    if (item.IsFolder)
                    {
                        SyncFolder(currentPath, _storage.DestinationPath);
                    }
                    else
                    {
                        SyncFile(currentPath, _storage.DestinationPath);
                    }
                    item.LastSyncTime = DateTime.Now.Ticks;
                    count++;
                }
                AddHistory($"Successfully synced {count} items to {_storage.DestinationPath}");
            }
            catch (Exception ex)
            {
                string error = $"Error during sync: {ex.Message}";
                Debug.LogError($"[Asset Sync] {error}");
                AddHistory(error, true);
            }
            finally
            {
                Save(); // Save updated paths/times
                EditorUtility.ClearProgressBar();
            }

            Debug.Log($"[Asset Sync] Synced {count} root items to {_storage.DestinationPath}");
        }

        private static void SyncFile(string assetPath, string rootDestination)
        {
            // source: Assets/Foo/Bar.png
            // dest: D:/Backup/Assets/Foo/Bar.png (?) or D:/Backup/Bar.png?
            // User requirement: "Preserve folder structure". 
            // Usually this means mirroring the Assets folder structure relative to the project root or Assets root.
            // Let's assume we mirror relative to project root? Or just relative to 'Assets'?
            // Usually 'Assets' is the root context.

            string sourceAbsPath = Path.GetFullPath(assetPath);
            string relPath = assetPath; // e.g. "Assets/Folder/File.png"
            string destAbsPath = Path.Combine(rootDestination, relPath);

            string destDir = Path.GetDirectoryName(destAbsPath);
            if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);

            File.Copy(sourceAbsPath, destAbsPath, true);
        }

        private static void SyncFolder(string folderPath, string rootDestination)
        {
            // Sync all files recursively
            string sourceAbsPath = Path.GetFullPath(folderPath);
            
            // Get all files
            string[] files = Directory.GetFiles(sourceAbsPath, "*", SearchOption.AllDirectories);
            
            foreach (string file in files)
            {
                if (file.EndsWith(".meta")) continue; // basic exclusion
                
                // Reconstruct relative path
                // file = C:/Project/Assets/Folder/Sub/File.png
                // projectPath = C:/Project/
                // relative = Assets/Folder/Sub/File.png
                
                // A simpler way: use Unity's paths.
                // We are inside a loop of system files.
                // Alternatively, use AssetDatabase to find assets inside, but that might be slow?
                // Standard IO is faster for bulk copy.
                
                string relPath = GetRelativePath(file, Path.GetFullPath(".")); // Relative to project root
                string destPath = Path.Combine(rootDestination, relPath);
                
                string destDir = Path.GetDirectoryName(destPath);
                if (!Directory.Exists(destDir)) Directory.CreateDirectory(destDir);
                
                File.Copy(file, destPath, true);
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

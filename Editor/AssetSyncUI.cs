using UnityEditor;
using UnityEngine;
using System.Linq;
using System.Collections.Generic;

namespace UnityTools.Editor.AssetSyncTool
{
    [System.Serializable]
    public class AssetSyncUI
    {
        private Vector2 _scrollPos;
        private Vector2 _historyScrollPos;
        private int _selectedTab = 0;
        private readonly string[] _tabs = { "Sync", "History" };
        
        // Grouping & Filtering
        private enum GroupingMode { None, Directory, Custom }
        private GroupingMode _groupingMode = GroupingMode.None;
        private string _searchFilter = "";
        private System.Collections.Generic.Dictionary<string, bool> _groupFoldouts = new System.Collections.Generic.Dictionary<string, bool>();
        private System.Collections.Generic.Dictionary<string, bool> _groupScheduleFoldouts = new System.Collections.Generic.Dictionary<string, bool>();
        
        // Renaming State
        private string _renamingGroupKey = "";
        private string _renamingGroupTempName = "";

        public void Draw()
        {
            DrawHeader();
            EditorGUILayout.Space();
            
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabs);
            EditorGUILayout.Space();

            if (_selectedTab == 0)
            {
                DrawSyncTab();
            }
            else
            {
                DrawHistoryTab();
            }
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Asset Sync Manager V1", EditorStyles.boldLabel);
            var storage = AssetSyncManager.Storage;
            EditorGUILayout.LabelField($"Marked Items: {storage.Items.Count}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawSyncTab()
        {
            DrawDestination();
            EditorGUILayout.Space();
            DrawScheduling();
            EditorGUILayout.Space();
            DrawSyncQueue();
            EditorGUILayout.Space();
            DrawMarkedAssets();
            EditorGUILayout.Space();
            DrawActions();
        }

        private void DrawScheduling()
        {
            var storage = AssetSyncManager.Storage;
            EditorGUILayout.LabelField("Scheduling", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            storage.IsAutoSyncEnabled = EditorGUILayout.Toggle("Enable Auto Sync", storage.IsAutoSyncEnabled);
            if (storage.IsAutoSyncEnabled)
            {
                storage.AutoSyncIntervalMinutes = EditorGUILayout.IntSlider("Interval (Minutes)", storage.AutoSyncIntervalMinutes, 1, 1440);
                string lastSync = storage.LastAutoSyncTime > 0 ? new System.DateTime(storage.LastAutoSyncTime).ToString("HH:mm:ss") : "Never";
                EditorGUILayout.LabelField($"Last Auto Sync: {lastSync}", EditorStyles.miniLabel);
            }
            
            EditorGUILayout.EndVertical();
        }

        private void DrawSyncQueue()
        {
            // Only draw if active or paused
            if (!AssetSyncQueue.IsRunning && !AssetSyncQueue.IsPaused) return;

            EditorGUILayout.LabelField("Sync Progress", EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            Rect r = EditorGUILayout.GetControlRect(false, 20);
            EditorGUI.ProgressBar(r, AssetSyncQueue.Progress, AssetSyncQueue.IsPaused ? "Paused" : "Syncing...");

            EditorGUILayout.BeginHorizontal();
            if (AssetSyncQueue.IsPaused)
            {
                if (GUILayout.Button("Resume")) AssetSyncQueue.Resume();
            }
            else
            {
                if (GUILayout.Button("Pause")) AssetSyncQueue.Pause();
            }
            
            if (GUILayout.Button("Cancel")) AssetSyncQueue.CancelAll();
            
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            
            // Force repaint to animate progress bar
            if (AssetSyncQueue.IsRunning) 
            {
                // We need to find the window instance to repaint it, but since this is a class used by the window,
                // we can try to find the open window.
                var window = Resources.FindObjectsOfTypeAll<AssetSyncWindow>();
                if (window.Length > 0) window[0].Repaint();
            }
        }

        private void DrawDestination()
        {
            EditorGUILayout.LabelField("Sync Destination", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            
            var storage = AssetSyncManager.Storage;
            string newPath = EditorGUILayout.TextField(storage.DestinationPath);
            if (newPath != storage.DestinationPath)
            {
                AssetSyncManager.SetDestination(newPath);
            }

            if (GUILayout.Button(EditorGUIUtility.IconContent("Folder Icon"), GUILayout.Width(30), GUILayout.Height(18)))
            {
                if (!string.IsNullOrEmpty(storage.DestinationPath) && System.IO.Directory.Exists(storage.DestinationPath))
                {
                    EditorUtility.RevealInFinder(storage.DestinationPath);
                }
                else
                {
                    EditorUtility.DisplayDialog("Error", "Destination folder does not exist.", "OK");
                }
            }

            if (GUILayout.Button("Browse", GUILayout.Width(60)))
            {
                string path = EditorUtility.OpenFolderPanel("Select Sync Destination", storage.DestinationPath, "");
                if (!string.IsNullOrEmpty(path))
                {
                    AssetSyncManager.SetDestination(path);
                }
            }
            EditorGUILayout.EndHorizontal();

            if (string.IsNullOrEmpty(storage.DestinationPath))
            {
                EditorGUILayout.HelpBox("Please select a destination folder.", MessageType.Warning);
            }
            else if (!System.IO.Directory.Exists(storage.DestinationPath))
            {
                EditorGUILayout.HelpBox("Destination directory does not exist. It will be created.", MessageType.Info);
            }
        }

        private void DrawMarkedAssets()
        {
            EditorGUILayout.LabelField("Marked Assets", EditorStyles.boldLabel);
            
            var storage = AssetSyncManager.Storage;

            // Selection buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("All", EditorStyles.miniButtonLeft, GUILayout.Width(40)))
            {
                storage.Items.ForEach(i => i.IsEnabled = true);
                AssetSyncManager.Save();
            }
            if (GUILayout.Button("None", EditorStyles.miniButtonRight, GUILayout.Width(40)))
            {
                storage.Items.ForEach(i => i.IsEnabled = false);
                AssetSyncManager.Save();
            }
            
            GUILayout.FlexibleSpace();
            
            GUILayout.Label("Group By:", GUILayout.Width(60));
            _groupingMode = (GroupingMode)EditorGUILayout.EnumPopup(_groupingMode, GUILayout.Width(100));
            EditorGUILayout.EndHorizontal();
            
            // Bulk Actions (Custom Grouping Only)
            // Bulk Actions removed in v1.6.0 in favor of inline editing

            // Drag and drop area
            var dropArea = GUILayoutUtility.GetRect(0.0f, 50.0f, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "Drag & Drop Assets Here", EditorStyles.helpBox);
            
            HandleDragAndDrop(dropArea);
            
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, EditorStyles.helpBox);
            EditorGUI.BeginChangeCheck();

            if (storage.Items.Count == 0)
            {
                EditorGUILayout.LabelField("No assets marked. Right-click assets in Project view to mark them.", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                DrawBookmarkedItemsRaw(storage.Items);
            }

            if (EditorGUI.EndChangeCheck())
            {
                AssetSyncManager.Save();
            }

            EditorGUILayout.EndScrollView();

            // "Create New Group" Drop Area (Only in Custom Mode)
            if (_groupingMode == GroupingMode.Custom)
            {
                var newGroupArea = GUILayoutUtility.GetRect(0.0f, 30.0f, GUILayout.ExpandWidth(true));
                GUI.Box(newGroupArea, "Drag Here to Create New Group", EditorStyles.helpBox);
                HandleGroupDrop(newGroupArea, "New Group");
            }
        }

        private void DrawBookmarkedItemsRaw(System.Collections.Generic.List<SyncItem> items)
        {
            // 1. Filter items first
            var filteredItems = new System.Collections.Generic.List<SyncItem>();
            foreach (var item in items)
            {
                if (!string.IsNullOrEmpty(_searchFilter))
                {
                    if (!item.AssetPath.ToLower().Contains(_searchFilter.ToLower())) continue;
                }
                filteredItems.Add(item);
            }

            if (_groupingMode == GroupingMode.None)
            {
                DrawItemList(filteredItems);
            }
            else
            {
                // Group items
                var groups = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<SyncItem>>();

                foreach (var item in filteredItems)
                {
                    string key = "Other";
                    if (_groupingMode == GroupingMode.Directory)
                    {
                        var parts = item.AssetPath.Split('/');
                        key = parts.Length > 1 ? parts[1] : "Root";
                    }
                    else if (_groupingMode == GroupingMode.Custom)
                    {
                        key = string.IsNullOrEmpty(item.Category) ? "General" : item.Category;
                    }

                    if (!groups.ContainsKey(key)) groups[key] = new System.Collections.Generic.List<SyncItem>();
                    groups[key].Add(item);
                }

                // Draw Groups
                foreach (var group in groups)
                {
                    if (!_groupFoldouts.ContainsKey(group.Key)) _groupFoldouts[group.Key] = true;

                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

                    if (_renamingGroupKey == group.Key)
                    {
                        GUI.SetNextControlName("GroupRenameField");
                        _renamingGroupTempName = EditorGUILayout.TextField(_renamingGroupTempName, EditorStyles.toolbarTextField, GUILayout.Width(200));
                        
                        if (Event.current.isKey && Event.current.keyCode == KeyCode.Return)
                        {
                            RenameGroup(group.Key, _renamingGroupTempName);
                            _renamingGroupKey = "";
                            Event.current.Use();
                        }
                        // Focus
                        if (Event.current.type == EventType.Repaint && GUI.GetNameOfFocusedControl() != "GroupRenameField")
                        {
                            EditorGUI.FocusTextInControl("GroupRenameField");
                        }
                        // Cancel on Escape
                        if (Event.current.isKey && Event.current.keyCode == KeyCode.Escape)
                        {
                            _renamingGroupKey = "";
                            Event.current.Use();
                        }
                    }
                    else
                    {
                        _groupFoldouts[group.Key] = EditorGUILayout.Foldout(_groupFoldouts[group.Key], $"{group.Key} ({group.Value.Count})", true);
                    }

                     GUILayout.FlexibleSpace();

                     if (GUILayout.Button("Schedule", EditorStyles.toolbarButton, GUILayout.Width(70)))
                     {
                         if (!_groupScheduleFoldouts.ContainsKey(group.Key)) _groupScheduleFoldouts[group.Key] = false;
                         _groupScheduleFoldouts[group.Key] = !_groupScheduleFoldouts[group.Key];
                     }

                     // Rename button moved here, next to Schedule
                     if (_groupingMode == GroupingMode.Custom && GUILayout.Button("Rename", EditorStyles.toolbarButton, GUILayout.Width(60)))
                     {
                         _renamingGroupKey = group.Key;
                         _renamingGroupTempName = group.Key;
                     }

                     if (GUILayout.Button("Sync Group", EditorStyles.toolbarButton, GUILayout.Width(80)))
                     {
                         var itemsToSync = group.Value.Where(x => x.IsEnabled).ToList();
                         if (itemsToSync.Count > 0)
                         {
                             var schedule = AssetSyncManager.GetGroupSchedule(group.Key, _groupingMode.ToString());
                             AssetSyncManager.SyncItems(items: itemsToSync, overrideDestination: schedule?.DestinationPath);
                         }
                         else
                         {
                             EditorUtility.DisplayDialog("Sync Group", "No items enabled in this group to sync.", "OK");
                         }
                     }

                     if (GUILayout.Button("Select All", EditorStyles.toolbarButton, GUILayout.Width(70)))
                     {
                         group.Value.ForEach(x => x.IsEnabled = true);
                     }
                     if (GUILayout.Button("None", EditorStyles.toolbarButton, GUILayout.Width(50)))
                     {
                         group.Value.ForEach(x => x.IsEnabled = false);
                     }

                    EditorGUILayout.EndHorizontal();

                     // Handle Drag & Drop on Group Header
                     if (_groupingMode == GroupingMode.Custom)
                     {
                         HandleGroupDrop(GUILayoutUtility.GetLastRect(), group.Key);
                     }

                     // Draw Group Schedule Settings
                     if (_groupScheduleFoldouts.ContainsKey(group.Key) && _groupScheduleFoldouts[group.Key])
                     {
                         var schedule = AssetSyncManager.GetGroupSchedule(group.Key, _groupingMode.ToString());
                         EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                         schedule.IsEnabled = EditorGUILayout.Toggle("Auto Sync", schedule.IsEnabled);
                         if (schedule.IsEnabled)
                         {
                             schedule.IntervalMinutes = EditorGUILayout.IntSlider("Interval (Min)", schedule.IntervalMinutes, 1, 1440);
                             string lastSync = schedule.LastSyncTime > 0 ? new System.DateTime(schedule.LastSyncTime).ToString("HH:mm:ss") : "Never";
                             EditorGUILayout.LabelField($"Last Sync: {lastSync}", EditorStyles.miniLabel);
                         }
                         
                         EditorGUILayout.Space(2);
                         EditorGUILayout.LabelField("Sync Destination", EditorStyles.boldLabel);
                         EditorGUILayout.BeginHorizontal();
                         string displayPath = string.IsNullOrEmpty(schedule.DestinationPath) ? "Using Global Destination" : schedule.DestinationPath;
                         EditorGUILayout.TextField(displayPath, EditorStyles.miniTextField);
                         
                         if (GUILayout.Button("Select", GUILayout.Width(50)))
                         {
                             string path = EditorUtility.OpenFolderPanel("Select Group Sync Destination", schedule.DestinationPath, "");
                             if (!string.IsNullOrEmpty(path))
                             {
                                 schedule.DestinationPath = path;
                                 AssetSyncManager.Save();
                             }
                         }
                         
                         if (!string.IsNullOrEmpty(schedule.DestinationPath) && GUILayout.Button("Clear", GUILayout.Width(45)))
                         {
                             schedule.DestinationPath = "";
                             AssetSyncManager.Save();
                         }
                         EditorGUILayout.EndHorizontal();
                         
                         EditorGUILayout.EndVertical();
                     }

                    if (_groupFoldouts.ContainsKey(group.Key) && _groupFoldouts[group.Key])
                    {
                        EditorGUI.indentLevel++;
                        DrawItemList(group.Value);
                        EditorGUI.indentLevel--;
                    }
                    EditorGUILayout.EndVertical();
                }
            }
        }

        private void DrawItemList(System.Collections.Generic.List<SyncItem> items)
        {
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                var rect = EditorGUILayout.BeginHorizontal();

                if (Event.current.type == EventType.Repaint && rect.Contains(Event.current.mousePosition))
                {
                    EditorGUI.DrawRect(rect, new Color(1f, 1f, 1f, 0.05f));
                }

                item.IsEnabled = EditorGUILayout.Toggle(item.IsEnabled, GUILayout.Width(20));

                Texture icon = AssetDatabase.GetCachedIcon(item.AssetPath);
                if (icon != null)
                {
                    GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(20));
                }

                if (GUILayout.Button(item.AssetPath, EditorStyles.label))
                {
                    // Context Menu
                    if (Event.current.button == 1)
                    {
                        GenericMenu menu = new GenericMenu();
                        if (_groupingMode == GroupingMode.Custom)
                        {
                            // Add existing groups
                            var existingGroups = items.Select(x => x.Category).Distinct().ToList();
                            foreach (var grp in existingGroups)
                            {
                                menu.AddItem(new GUIContent($"Move to Group/{grp}"), false, () => {
                                    item.Category = grp;
                                    AssetSyncManager.Save();
                                });
                            }
                            menu.AddSeparator("Move to Group/");
                            menu.AddItem(new GUIContent("Move to Group/New Group..."), false, () => {
                                // Simple way: assign a temporary name, user can rename it inline
                                item.Category = "New Group";
                                AssetSyncManager.Save();
                            });
                        }
                        menu.ShowAsContext();
                        Event.current.Use();
                    }
                    else
                    {
                        UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(item.AssetPath);
                        if (obj != null) EditorGUIUtility.PingObject(obj);
                    }
                }

                if (GUILayout.Button("X", GUILayout.Width(20)))
                {
                    AssetSyncManager.UnmarkAsset(item.Guid);
                }

                EditorGUILayout.EndHorizontal();
            }
        }

        private void HandleGroupDrop(Rect dropArea, string targetGroup)
        {
            Event currentEvent = Event.current;
            if (!dropArea.Contains(currentEvent.mousePosition)) return;
            
            switch (currentEvent.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    
                    if (currentEvent.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        
                        string finalGroupName = targetGroup;
                        if (targetGroup == "New Group")
                        {
                            // In a real app we might ask for a name, but for now let's just use "New Group" 
                            // and let the user rename it inline.
                            finalGroupName = "New Group"; 
                        }

                        int count = 0;
                        foreach (var obj in DragAndDrop.objectReferences)
                        {
                            string path = AssetDatabase.GetAssetPath(obj);
                            if (string.IsNullOrEmpty(path)) continue;

                            string guid = AssetDatabase.AssetPathToGUID(path);
                            // 1. Check if already tracked
                            var item = AssetSyncManager.Storage.Items.Find(x => x.Guid == guid);
                            if (item == null)
                            {
                                // Add new
                                AssetSyncManager.MarkAsset(guid, path);
                                item = AssetSyncManager.Storage.Items.Find(x => x.Guid == guid);
                            }
                            
                            // 2. Assign Group
                            if (item != null)
                            {
                                item.Category = finalGroupName;
                                count++;
                            }
                        }
                        
                        if (count > 0)
                        {
                            AssetSyncManager.Save();
                            AssetSyncManager.AddHistory($"Moved/Added {count} items to group: {finalGroupName}", LogType.Info);
                        }
                        currentEvent.Use();
                    }
                    break;
            }
        }
        
        private void HandleDragAndDrop(Rect dropArea)
        {
            Event currentEvent = Event.current;
            
            switch (currentEvent.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!dropArea.Contains(currentEvent.mousePosition))
                        return;

                    DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                    
                    if (currentEvent.type == EventType.DragPerform)
                    {
                        DragAndDrop.AcceptDrag();
                        
                        foreach (string draggedObject in DragAndDrop.paths)
                        {
                            if (draggedObject.StartsWith("Assets/"))
                            {
                                string guid = AssetDatabase.AssetPathToGUID(draggedObject);
                                if (!string.IsNullOrEmpty(guid))
                                {
                                    AssetSyncManager.MarkAsset(guid, draggedObject);
                                }
                            }
                        }
                        
                        AssetSyncManager.Save();
                        Event.current.Use();
                    }
                    break;
            }
        }

        private void DrawActions()
        {
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Clear All", GUILayout.Width(100)))
            {
                if (EditorUtility.DisplayDialog("Clear All", "Are you sure you want to unmark all assets?", "Yes", "No"))
                {
                    AssetSyncManager.Storage.Items.Clear();
                    AssetSyncManager.Save();
                }
            }
            
            GUILayout.FlexibleSpace();

            GUI.enabled = !string.IsNullOrEmpty(AssetSyncManager.Storage.DestinationPath) && AssetSyncManager.Storage.Items.Count > 0;
            
            // Sync Options
            // Sync Options
            if (GUILayout.Button("Sync Now (Smart)", GUILayout.Height(30), GUILayout.Width(120)))
            {
                AssetSyncManager.SyncAll(force: false);
            }
            
            if (GUILayout.Button("Force Sync All", GUILayout.Height(30), GUILayout.Width(120)))
            {
                AssetSyncManager.SyncAll(force: true);
            }
            
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }

        private void DrawHistoryTab()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Sync Logs", EditorStyles.boldLabel);
            if (GUILayout.Button("Clear History", GUILayout.Width(100)))
            {
                AssetSyncManager.ClearHistory();
            }
            EditorGUILayout.EndHorizontal();

            var history = AssetSyncManager.Storage.History;
            _historyScrollPos = EditorGUILayout.BeginScrollView(_historyScrollPos, EditorStyles.helpBox);

            if (history.Count == 0)
            {
                EditorGUILayout.LabelField("No history yet.", EditorStyles.centeredGreyMiniLabel);
            }
            else
            {
                // Iterate in reverse to show newest first
                for (int i = history.Count - 1; i >= 0; i--)
                {
                    var log = history[i];
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.BeginHorizontal();
                    
                    // Time
                    EditorGUILayout.LabelField(log.Timestamp, EditorStyles.miniLabel, GUILayout.Width(60));
                    
                    // Color based on type
                    GUIStyle style = new GUIStyle(EditorStyles.label);
                    switch(log.Type)
                    {
                        case LogType.Success: style.normal.textColor = new Color(0.2f, 0.8f, 0.2f); break;
                        case LogType.Warning: style.normal.textColor = new Color(1f, 0.6f, 0f); break;
                        case LogType.Error: style.normal.textColor = Color.red; break;
                        default: style.normal.textColor = Color.white; break;
                    }

                    EditorGUILayout.LabelField(log.Message, style);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                }
            }

            EditorGUILayout.EndScrollView();
        }
        private void RenameGroup(string oldName, string newName)
        {
            if (string.IsNullOrEmpty(newName) || oldName == newName) return;
            
            var storage = AssetSyncManager.Storage;
            int count = 0;
            foreach (var item in storage.Items)
            {
                if (item.Category == oldName)
                {
                    item.Category = newName;
                    count++;
                }
            }
            
            if (count > 0)
            {
                AssetSyncManager.Save();
                AssetSyncManager.AddHistory($"Renamed group '{oldName}' to '{newName}' ({count} items updated)", LogType.Info);
                
                // Update foldouts dictionary key to prevent closing
                if (_groupFoldouts.ContainsKey(oldName))
                {
                    bool isOpen = _groupFoldouts[oldName];
                    _groupFoldouts.Remove(oldName);
                    _groupFoldouts[newName] = isOpen;
                }
            }
        }
    }
}

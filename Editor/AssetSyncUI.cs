using UnityEditor;
using UnityEngine;

namespace UnityTools.Editor.AssetSyncTool
{
    [System.Serializable]
    public class AssetSyncUI
    {
        private Vector2 _scrollPos;
        private Vector2 _historyScrollPos;
        private int _selectedTab = 0;
        private readonly string[] _tabs = { "Sync", "History" };

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
            EditorGUILayout.LabelField("Asset Sync Manager", EditorStyles.boldLabel);
            var storage = AssetSyncManager.Storage;
            EditorGUILayout.LabelField($"Marked Items: {storage.Items.Count}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
        }

        private void DrawSyncTab()
        {
            DrawDestination();
            EditorGUILayout.Space();
            DrawMarkedAssets();
            EditorGUILayout.Space();
            DrawActions();
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
            EditorGUILayout.EndHorizontal();
            
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

            for (int i = 0; i < storage.Items.Count; i++)
            {
                var item = storage.Items[i];
                var rect = EditorGUILayout.BeginHorizontal();
                
                // Hover highlight
                if (Event.current.type == EventType.Repaint && rect.Contains(Event.current.mousePosition))
                {
                    EditorGUI.DrawRect(rect, new Color(0.3f, 0.5f, 0.7f, 0.2f));
                }

                // Checkbox
                item.IsEnabled = EditorGUILayout.Toggle(item.IsEnabled, GUILayout.Width(20));
                
                // Icon
                Texture icon = AssetDatabase.GetCachedIcon(item.AssetPath);
                if (icon != null)
                {
                    GUILayout.Label(icon, GUILayout.Width(20), GUILayout.Height(20));
                }

                // Path
                if (GUILayout.Button(item.AssetPath, EditorStyles.label))
                {
                    // Ping object
                    UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(item.AssetPath);
                    if (obj != null) EditorGUIUtility.PingObject(obj);
                }

                // Remove
                if (GUILayout.Button("X", GUILayout.Width(25)))
                {
                    AssetSyncManager.UnmarkAsset(item.Guid);
                    i--; // adjust index
                }

                EditorGUILayout.EndHorizontal();
            }

            if (EditorGUI.EndChangeCheck())
            {
                AssetSyncManager.Save();
            }

            EditorGUILayout.EndScrollView();
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
            if (GUILayout.Button("Sync Now", GUILayout.Height(30), GUILayout.Width(150)))
            {
                AssetSyncManager.SyncAll();
                EditorUtility.DisplayDialog("Sync Complete", "Files have been synced to the destination.", "OK");
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
                    EditorGUILayout.LabelField(log.Timestamp, EditorStyles.miniLabel, GUILayout.Width(120));
                    GUIStyle style = new GUIStyle(EditorStyles.label);
                    if (log.IsError) style.normal.textColor = Color.red;
                    EditorGUILayout.LabelField(log.Message, style);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace UnityTools.Editor.AssetSyncTool
{
    public class AssetSyncQueue
    {
        private static Queue<Action> _taskQueue = new Queue<Action>();
        private static bool _isPaused = false;
        private static bool _isRunning = false;
        private static int _totalTasks = 0;
        private static int _completedTasks = 0;

        public static bool IsRunning => _isRunning;
        public static bool IsPaused => _isPaused;
        public static float Progress => _totalTasks > 0 ? (float)_completedTasks / _totalTasks : 0f;

        public static void Enqueue(Action task)
        {
            _taskQueue.Enqueue(task);
            _totalTasks++;
            if (!_isRunning && !_isPaused)
            {
                ProcessQueue();
            }
        }

        public static void ProcessQueue()
        {
            if (_isPaused) return;

            _isRunning = true;
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
        }

        private static void OnUpdate()
        {
            if (_isPaused || _taskQueue.Count == 0)
            {
                _isRunning = false;
                EditorApplication.update -= OnUpdate;
                return;
            }

            // Process one item per frame to keep UI responsive
            // Or maybe a few items? Let's stick to one for now to avoid freezing 
            // if the tasks are heavy file copies
            try
            {
                var task = _taskQueue.Dequeue();
                task?.Invoke();
                _completedTasks++;
                
                // Force repaint of the window to show progress
                var windows = Resources.FindObjectsOfTypeAll<AssetSyncWindow>();
                if (windows.Length > 0) windows[0].Repaint();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Asset Sync Queue] Error processing task: {ex.Message}");
            }
            
            if (_taskQueue.Count == 0)
            {
                _isRunning = false;
                _totalTasks = 0;
                _completedTasks = 0;
                EditorApplication.update -= OnUpdate;
                AssetSyncManager.TriggerPostSync();
            }
        }

        public static void Pause()
        {
            _isPaused = true;
            _isRunning = false;
            EditorApplication.update -= OnUpdate;
        }

        public static void Resume()
        {
            _isPaused = false;
            ProcessQueue();
        }

        public static void CancelAll()
        {
            _taskQueue.Clear();
            _isPaused = false;
            _isRunning = false;
            _totalTasks = 0;
            _completedTasks = 0;
            EditorApplication.update -= OnUpdate;
            EditorUtility.ClearProgressBar();
        }
    }
}

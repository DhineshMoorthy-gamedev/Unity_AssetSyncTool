using UnityEditor;
using UnityEngine;

namespace UnityTools.Editor.AssetSyncTool
{
    public class AssetSyncWindow : EditorWindow
    {
        [SerializeField] private AssetSyncUI ui = new AssetSyncUI();

        [MenuItem("Tools/GameDevTools/Asset Sync/Manager Window", false, 110)]
        public static void ShowWindow()
        {
            GetWindow<AssetSyncWindow>("Asset Sync");
        }

        private void OnGUI()
        {
            ui.Draw();
        }
    }
}

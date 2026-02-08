# Asset Sync Tool

A Unity Editor extension for synchronizing assets between projects and external sources.

## Features

- **Sync Assets**: Easily sync assets between multiple Unity projects or external folders.
- **Smart Incremental Sync**: Only changes are transferred based on file checksums, savings time.
- **Automated Scheduling**: Configure automatic syncs at set intervals (e.g., every 60 minutes) **per group** or globally.
- **Advanced Grouping**:
    - **Drag & Drop**: Drag assets onto group headers to organize them.
    - **Header Drops**: Drag files from the Project View directly onto a Custom Group Header to add/move them.
    - **Context Menus**: Right-click items to "Move to Group" or "Rename Group".
    - **Inline Renaming**: Rename groups directly in the list view.
- **Queue Management**: Pause, resume, and cancel sync operations securely.
- **Rich History**: View detailed sync logs with color-coded status (Success, Warning, Error).

## Installation

1. Open Unity Package Manager
2. Click the "+" button
3. Select "Add package from git URL"
4. Enter the repository URL or local path

## Usage

### General Usage
- Open **Tools > GameDevTools > Asset Sync Manager**.
- Right-click assets in the Project view to "Mark for Sync".
- Select a "Sync Destination" folder in the tool window.
- Click "Sync Now (Smart)" for incremental sync or "Force Sync All" to overwrite everything.

### Grouping & Organization (v2.0+)
- **Per-Group Destinations**: You can set a unique destination path for each group in the **Schedule** settings. This overrides the global destination.
- **Create Group**: Drag assets to the "New Group" box at the bottom of the list.
- **Move Assets**: Drag selected assets onto any Group Header to move them there.
- **Rename Group**: Click the "Rename" button on the right of any group header.
- **Context Menus**: Use the Context Menu (Right-Click > Sync Item) to quickly move items between groups.

### Scheduling
- **Global**: Enable "Enable Auto Sync" in the main Scheduling section.
- **Per-Group**: Click the "Schedule" button on any group header to set independent intervals for that specific group.

### API / Hooks Example

```csharp
using UnityTools.Editor.AssetSyncTool;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public class MySyncHooks
{
    static MySyncHooks()
    {
        AssetSyncManager.OnPreSync += () => Debug.Log("Starting Sync...");
        AssetSyncManager.OnPostSync += () => Debug.Log("Sync Finished!");
        AssetSyncManager.OnSyncProgress += (progress, item) => 
        {
            // Update custom status bar
        };
    }
}
```

## Requirements

- Unity 2020.3 or higher
- No additional dependencies

## License

MIT License

---

[View Changelog](CHANGELOG.md)
# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [V1] - 2026-02-08

### Added
- **Per-Group Sync Destinations**: Independent destination paths for any group, overriding global settings.
- **Group Management UI**: 
    - Dedicated **Rename** and **Schedule** buttons in group headers.
    - Drag-and-drop assets directly onto group headers for quick organization.
    - "Drag Here to Create New Group" drop area.
- **Improved Organization**:
    - **Custom Grouping**: Manually assign and rename groups.
    - **Automatic Grouping**: Group by Directory or File Type.
    - **Sync Group Button**: Sync only specific groups of assets.
- **Rich History & Monitoring**: Color-coded logs (Success, Warning, Error) with detailed file reports.
- **Per-Group Scheduling**: Set independent auto-sync intervals for different asset groups.
- **Context Menus**: Right-click to move assets between groups or rename custom groups.

### Changed
- **UI Polish**: Removed all per-item renaming clutter. The asset list is now clean and focused on status.
- **Improved Performance**: Incremental sync logic using MD5 checksums and high-precision timestamps.
- **Stable UI**: Refactored layout logic to prevent GUI errors and ensure dropdown responsiveness.

### Fixed
- Resolved `KeyNotFoundException` and GUI Layout mismatch errors during renaming.
- Optimized scheduler to handle multiple concurrent group tasks.

## [1.0.0] - 2026-02-08

### Added
- **Core Sync Engine**: 
    - Incremental sync between Unity projects.
    - Sync Queue system with progress tracking.
- **Automation**: Global scheduling and auto-sync intervals.
- **Standard UI**: Centralized manager window for marking and syncing assets.

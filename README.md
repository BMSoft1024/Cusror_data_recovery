# Cursor Backup - Settings Manager

## Overview

**Cursor Backup** is a Windows WPF application (.NET 8) designed to help manage Cursor AI settings and data. The application enables backing up, restoring, and merging Cursor configurations.

## Key Features

### 1. Load Current Settings
- **Global Settings**: settings.json, keybindings.json, state.vscdb, etc.
- **Chat Histories**: Grouped by project with chat names and checkboxes
- **Dynamic List**: Only available settings appear with active checkboxes

### 2. Load Settings from Folder
- Load settings from backup folders
- Automatic detection of different backup structures

### 3. Save Selected to Location
- Save selected settings to any folder
- Preserve complete structure

### 4. Merge Function
- Merge settings without duplication or overwriting
- Automatic conflict detection and notification
- Only new items are added

## User Interface

### Full Screen Mode
- Application starts in maximized window
- Modern, clean interface

### Dual Panel System
- **Left Panel**: Current Cursor settings
- **Right Panel**: Settings loaded from backup

### Checkbox System
- Every setting has a checkbox
- Only available settings are active
- Color-coded status (green = available, red = unavailable)

### Chat Histories
- Grouped by project
- Expandable/collapsible with expanders
- Only chat names displayed (not content)

## Installation and Running

### Prerequisites
- .NET 8.0 Runtime (or newer)
- Windows 10/11

### Build
```bash
cd Cursor_Backup/CursorBackup
dotnet build
```

### Run
```bash
dotnet run
```

Or directly from the built exe file:
```
bin\Debug\net8.0-windows\CursorBackup.exe
```

## Usage

### 1. Load Current Settings
1. Click the **"Load Current Settings"** button
2. The application automatically discovers all available settings
3. Chat histories appear in the "Chat Histories" expander

### 2. Load Settings from Backup
1. Click the **"Load from Backup Folder"** button
2. Select the backup folder
3. Settings appear in the right panel

### 3. Save
1. Mark desired settings (checkboxes)
2. Click the **"Save Selected to..."** button
3. Select destination folder

### 4. Merge
1. Load settings from backup
2. Mark items to merge
3. Click the **"Merge Selected to..."** button
4. Select destination folder
5. Application reports conflicts (if any)

### 5. Export Chat Histories
1. Select chat histories from either panel
2. Click **"Export Chats to Markdown"** button
3. Select destination folder
4. Chats are exported in Markdown format organized by project

## Supported Settings

- **Global Settings** (settings.json)
- **Keybindings** (keybindings.json)
- **State Database** (state.vscdb, state.vscdb.backup)
- **Global Storage** (global extension storage)
- **Language Packs** (languagepacks.json)
- **Workspace Storage** (project-specific settings)
- **Chat Histories** (by project)
- **Extensions List**
- **Documentation Groups** (Cursor documentation)

## Technical Details

### Architecture
- **MVVM Pattern**: ViewModel-View separation
- **Service Layer**: CursorDataService, BackupService, ChatExportService
- **WPF Data Binding**: Full binding support
- **SQLite Integration**: Read state.vscdb databases

### Dependencies
- Newtonsoft.Json (JSON handling)
- System.Data.SQLite.Core (state.vscdb reading)

### Project Structure
```
CursorBackup/
├── Models/          # Data model classes
├── ViewModels/      # ViewModel classes
├── Services/        # Business logic
├── Dialogs/         # Dialog windows
├── Controls/        # Custom controls
├── Converters/      # Value converters
└── MainWindow.xaml  # Main window
```

### Key Components
- **MainViewModel**: Main application logic and coordination
- **CursorDataService**: Discovers and reads Cursor settings
- **BackupService**: Handles backup and merge operations
- **ChatExportService**: Exports chat histories to Markdown
- **SettingsStorage**: Persists application settings

## Advanced Features

### Chat History Export
- Export chat histories to Markdown format
- Automatic organization by project
- Support for both regular JSON chat files and state.vscdb entries
- Thread and session detection

### State Database Integration
- Read chat histories directly from state.vscdb
- Automatic thread and session grouping
- Support for combined prompts extraction

### Conflict Resolution
- Smart merge without overwriting
- Automatic conflict detection
- Detailed conflict reporting

## Troubleshooting

### "No settings found"
- Check if Cursor is installed
- Verify the `%APPDATA%\Cursor` folder exists

### "Cannot access file"
- Close all Cursor windows
- Check file permissions

### Chat export issues
- Ensure chat files are not corrupted
- Check state.vscdb database integrity
- Verify export folder permissions

## Development

### Adding New Settings Types
1. Extend the `SettingType` enum in `CursorSettingItem.cs`
2. Add discovery logic in `CursorDataService.cs`
3. Update UI templates as needed

### Extending Export Formats
1. Implement new export service
2. Add command to MainViewModel
3. Update UI with new export button

## License

This project is for personal use.

## Contributing

Feel free to submit issues and enhancement requests.

## Version History

- **v1.0**: Initial release with basic backup/restore functionality
- **v1.1**: Added chat history export to Markdown
- **v1.2**: Enhanced state.vscdb support and thread detection
- **v1.3**: Improved UI with documentation groups support

# 📱 ADB File Manager

A lightweight, standalone Windows application for exploring, managing, and transferring files on Android devices via **ADB (Android Debug Bridge)**.

Built with **C# (.NET Framework / WinForms)**, with zero external dependencies.

---

## ✨ Features

- **🔌 Device Management**: Automatically discovers connected ADB devices and enables seamless device switching.
- **📂 File Navigation**: Full path address bar with history (Back, Up, Refresh), quick location shortcuts, and real-time search filtering.
- **📊 Storage Capacity Monitor**: Displays total, used, and free space for the active partition.
- **📋 Clipboard & Actions**: Full Copy (`Ctrl+C`), Cut (`Ctrl+X`), Paste (`Ctrl+V`), and Select All (`Ctrl+A`) with automatic duplicate renaming.
- **📦 Package Installation**: Prompts on dropping or uploading Android packages (`.apk`, `.xapk`, `.apks`) to either install them directly or upload them to the device storage. Includes zip extraction for split APKs and OBB expansion files.
- **🚀 Drag-and-Drop & Queue**: Drag-and-drop file transfers with sequential upload queue, progress tracking, and conflict dialogs (Overwrite/Skip).
- **👁️ Text Viewer & Context Menu**: Full right-click context menu and built-in monospaced viewer for remote text files and logs.
- **📜 C# Script Addon Engine**: Write custom C# scripts placed in `scripts/` to create custom file context menu actions, tools menu entries, automate ADB commands, and extend application behavior. Live reload scripts without restarting.

---

## 📜 C# Scripting System Guide

Create `.cs` script files in the `scripts/` directory to create addons. Scripts are compiled dynamically at startup or via **Scripts -> Reload Scripts**.

### Writing a Script Addon
Implement the `IScript` interface:

```csharp
using System;
using System.Collections.Generic;
using ADBFileManager;
using ADBFileManager.Scripting;

public class MyCustomAddon : IScript
{
    public string Name { get { return "My Custom Addon"; } }
    public string Description { get { return "Adds custom ADB automation actions."; } }
    public string Author { get { return "Developer"; } }
    public string Version { get { return "1.0"; } }

    public void Initialize(IScriptContext context)
    {
        // 1. Add item to "📜 Scripts" tools dropdown menu
        context.RegisterToolsMenuItem("🚀 Run Custom ADB Command", () => {
            var result = context.RunAdbShell("pm list packages -3");
            context.ShowTextPreview("Third Party Packages", result.Output);
        });

        // 2. Add context menu item for specific files (supports extension, fileNameFilter wildcard, pathFilter wildcard)
        context.RegisterContextMenu("📦 Dump Package Info", (selectedFiles) => {
            foreach (var file in selectedFiles) {
                var res = context.RunAdbShell("pm dump " + file.Name);
                context.ShowTextPreview("Dump: " + file.Name, res.Output);
            }
        }, fileExtensionFilter: ".apk", fileNameFilter: "example_*.apk", pathFilter: "/sdcard/Download/*");
    }
}
```

### Available `IScriptContext` APIs

| Category | Method | Description |
| :--- | :--- | :--- |
| **ADB** | `RunAdbCommand(string args)` | Executes an ADB command (automatically appends `-s DEVICEID`). |
| **ADB** | `RunAdbShell(string cmd)` | Runs an `adb shell <cmd>` command on the active device. |
| **ADB** | `GetStorageInfo(path)` | Queries partition storage stats (Total, Used, Free). |
| **Files** | `GetFileList(path)` | Returns remote directory listing (`List<AdbFileInfo>`). |
| **Files** | `PushFile(local, remote)` | Pushes a file to the remote device. |
| **Files** | `PullFile(remote, local)` | Pulls a remote file to the local machine. |
| **Files** | `CreateDirectory(path)` | Creates a directory on the remote device. |
| **Files** | `DeleteItem(path)` | Deletes a file or directory recursively on the remote device. |
| **Files** | `RenameItem(oldPath, newPath)` | Renames or moves a remote file/folder. |
| **UI** | `GetSelectedFiles()` | Gets currently selected items in the main ListView. |
| **UI** | `RefreshFileList()` | Triggers a refresh of the current file listing. |
| **UI** | `RegisterContextMenu(...)` | Registers context menu item (supports `fileExtensionFilter`, `fileNameFilter` wildcards like `example_*.apk`, `pathFilter` wildcards like `/sdcard/*`, and `foldersOnly`). |
| **UI** | `RegisterToolsMenuItem(...)` | Registers a menu item under **📜 Scripts**. |
| **UI** | `ShowMessage(msg, title)` | Displays an information dialog. |
| **UI** | `ShowConfirmation(msg, title)` | Displays a Yes/No confirmation dialog. |
| **UI** | `PromptInput(prompt, title, default)` | Prompts the user for text input. |
| **UI** | `ShowTextPreview(title, content)` | Displays text in a monospaced preview window. |

### Included Sample Scripts
- `DeviceScreenshotAddon.cs`: Takes device screenshot and pulls it locally to `Screenshots/`.
- `PackageDumperAddon.cs`: Context menu item for `.apk` files to inspect package dumps via `pm dump`.


---

## 🛠️ Requirements

- **OS**: Windows 7 / 8 / 10 / 11
- **ADB**: Android SDK Platform-Tools (`adb.exe` in PATH or working directory)

---

## 🏗️ Building

### Command Line (Built-in C# Compiler)
```cmd
build.bat
```

### Visual Studio / MSBuild
```cmd
msbuild ADBFileManager.csproj /p:Configuration=Release
```

---

## 📄 License

Licensed under the [MIT License](LICENSE).

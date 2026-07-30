# 📱 ADB File Manager

A lightweight, standalone Windows desktop application for seamlessly exploring, managing, and transferring files on Android devices via **ADB (Android Debug Bridge)**.

Built with **C# (.NET Framework / WinForms)**, it compiles using either standard Visual Studio tools or Windows' built-in C# compiler (`csc.exe`) without requiring external dependencies.

---

## ✨ Features

- **🔌 Automatic Device Management**: Discovers connected ADB devices (`adb devices -l`), displays device model details, and allows switching between active devices.
- **📂 File Explorer & Quick Locations**:
  - Full path address bar with navigation history (**Back**, **Up**, **Home**, **Refresh**).
  - Quick sidebar shortcuts (`/sdcard/`, `/sdcard/Download`, `/sdcard/DCIM`, `/sdcard/Pictures`, `/sdcard/Documents`, `/data/local/tmp/`, `/system/`, `/`).
  - Search filter to quickly locate files in the current view.
- **📊 Storage Capacity Monitor**: Displays total, used, and remaining free space for the active directory/storage partition with live percentage calculations.
- **📋 Copy, Cut & Paste (Remote & Keyboard Shortcuts)**:
  - Copy (<kbd>Ctrl+C</kbd>) and Cut (<kbd>Ctrl+X</kbd>) files or directories internally within the ADB file manager.
  - Paste (<kbd>Ctrl+V</kbd>) into target directories with automatic duplicate renaming (`filename_copy.ext`).
  - Select all items using <kbd>Ctrl+A</kbd>.
- **⌨️ Keyboard & Context Menu Actions**:
  - Direct deletion with the <kbd>Delete</kbd> key or context menu.
  - Full right-click context menu (Navigate, Download to PC, Upload File/Folder, Preview Text, New Folder, Rename, Delete, Copy Remote Path).
- **🚀 File Transfers & Drag-and-Drop**:
  - Drag and drop files/folders from Windows Explorer straight into the device view.
  - Progress bar and progress percentage indicator for `adb push` and `adb pull`.
  - Smart collision handling modal dialog (**Overwrite**, **Overwrite All**, **Skip**, **Skip All**, **Cancel**).
- **👁️ Text File Preview**: Modal viewer with monospaced font for inspecting configuration files or logs directly on the device.

---

## 🛠️ Requirements

- **OS**: Windows 7 / 8 / 10 / 11
- **ADB**: Android SDK Platform-Tools (`adb.exe` in PATH or working directory).
- **Compiler** *(optional for source build)*: Built-in Windows C# compiler (`csc.exe`) included with Windows .NET Framework 4.0+.

---

## 🏗️ Building the Project

### Option 1: Using Built-in Windows Compiler (No IDE Required)
Simply run the included `build.bat` script:
```cmd
build.bat
```
This compiles `Program.cs`, `AdbService.cs`, `MainForm.cs`, `FileViewerForm.cs`, and `ConflictDialog.cs` into a standalone `ADBFileManager.exe`.

### Option 2: Using Visual Studio / MSBuild
Open `ADBFileManager.csproj` in Visual Studio and click **Build** or run:
```cmd
msbuild ADBFileManager.csproj /p:Configuration=Release
```

---

## 📄 License

This project is licensed under the [MIT License](LICENSE).

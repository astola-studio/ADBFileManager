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


@echo off
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" (
    set CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe
)

echo Compiling ADB File Manager UI using built-in Windows C# compiler...
"%CSC%" /nologo /win32icon:icon.ico /target:winexe /out:ADBFileManager.exe /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Core.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll /r:Microsoft.CSharp.dll Program.cs AdbService.cs ScriptEngine.cs PromptDialog.cs ScriptManagerForm.cs MainForm.cs FileViewerForm.cs ConflictDialog.cs ApkActionDialog.cs

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================================
    echo  SUCCESS: ADBFileManager.exe successfully built!
    echo ========================================================
) else (
    echo.
    echo BUILD FAILED. Check compiler output above.
)

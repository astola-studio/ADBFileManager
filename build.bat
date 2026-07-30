@echo off
set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" (
    set CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe
)

echo Compiling ADB File Manager UI using built-in Windows C# compiler...
"%CSC%" /nologo /win32icon:icon.ico /target:winexe /out:ADBFileManager.exe /r:System.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.Core.dll Program.cs AdbService.cs MainForm.cs FileViewerForm.cs ConflictDialog.cs

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================================
    echo  SUCCESS: ADBFileManager.exe successfully built!
    echo ========================================================
) else (
    echo.
    echo BUILD FAILED. Check compiler output above.
)

using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.CSharp;

namespace ADBFileManager.Scripting
{
    /// <summary>
    /// Contract that all C# script addons must implement.
    /// </summary>
    public interface IScript
    {
        string Name { get; }
        string Description { get; }
        string Author { get; }
        string Version { get; }

        void Initialize(IScriptContext context);
    }

    /// <summary>
    /// API interface exposed to scripts to interact with ADB, file system, and UI.
    /// </summary>
    public interface IScriptContext
    {
        // Active State
        string CurrentDeviceSerial { get; }
        string CurrentPath { get; }

        // ADB Commands
        ExecutionResult RunAdbCommand(string args);
        ExecutionResult RunAdbShell(string shellCommand);
        StorageInfo GetStorageInfo(string remotePath = null);

        // Remote File Operations
        List<AdbFileInfo> GetFileList(string remotePath = null);
        ExecutionResult PushFile(string localPath, string remotePath);
        ExecutionResult PullFile(string remotePath, string localPath);
        ExecutionResult CreateDirectory(string remotePath);
        ExecutionResult DeleteItem(string remotePath);
        ExecutionResult RenameItem(string oldPath, string newPath);

        // UI & Selection Access
        List<AdbFileInfo> GetSelectedFiles();
        void RefreshFileList();

        // UI Extensibility & Dialogs
        void RegisterContextMenu(string title, Action<List<AdbFileInfo>> action, string fileExtensionFilter = null, bool foldersOnly = false);
        void RegisterContextMenu(string title, Action<List<AdbFileInfo>> action, string fileExtensionFilter, string fileNameFilter, string pathFilter, bool foldersOnly = false);
        void RegisterToolsMenuItem(string title, Action action);
        void ShowMessage(string message, string caption = "Script Notice");
        bool ShowConfirmation(string message, string caption = "Script Question");
        string PromptInput(string prompt, string title = "Script Input", string defaultValue = "");
        void ShowTextPreview(string title, string content);
    }

    public class RegisteredContextMenu
    {
        public string Title { get; set; }
        public Action<List<AdbFileInfo>> Action { get; set; }
        public string FileExtensionFilter { get; set; }
        public string FileNameFilter { get; set; }
        public string PathFilter { get; set; }
        public bool FoldersOnly { get; set; }
        public IScript SourceScript { get; set; }
    }

    public class RegisteredMenuItem
    {
        public string Title { get; set; }
        public Action Action { get; set; }
        public IScript SourceScript { get; set; }
    }

    public class LoadedScriptInfo
    {
        public IScript ScriptInstance { get; set; }
        public string FilePath { get; set; }
        public string ScriptName { get { return ScriptInstance != null ? ScriptInstance.Name : Path.GetFileName(FilePath); } }
        public string Description { get { return ScriptInstance != null ? ScriptInstance.Description : ""; } }
        public string Author { get { return ScriptInstance != null ? ScriptInstance.Author : ""; } }
        public string Version { get { return ScriptInstance != null ? ScriptInstance.Version : ""; } }
        public List<string> CompilationErrors { get; set; }
        public bool HasErrors { get { return CompilationErrors != null && CompilationErrors.Count > 0; } }

        public LoadedScriptInfo()
        {
            CompilationErrors = new List<string>();
        }
    }

    public class ScriptManager : IScriptContext
    {
        private readonly AdbService adbService;
        private readonly Func<string> getDeviceSerialFunc;
        private readonly Func<string> getCurrentPathFunc;
        private readonly Func<List<AdbFileInfo>> getSelectedFilesFunc;
        private readonly Action refreshFileListAction;

        public List<LoadedScriptInfo> LoadedScripts { get; private set; }
        public List<RegisteredContextMenu> ContextMenuItems { get; private set; }
        public List<RegisteredMenuItem> ToolsMenuItems { get; private set; }

        public ScriptManager(
            AdbService adbService,
            Func<string> getDeviceSerialFunc,
            Func<string> getCurrentPathFunc,
            Func<List<AdbFileInfo>> getSelectedFilesFunc,
            Action refreshFileListAction)
        {
            this.adbService = adbService;
            this.getDeviceSerialFunc = getDeviceSerialFunc;
            this.getCurrentPathFunc = getCurrentPathFunc;
            this.getSelectedFilesFunc = getSelectedFilesFunc;
            this.refreshFileListAction = refreshFileListAction;

            LoadedScripts = new List<LoadedScriptInfo>();
            ContextMenuItems = new List<RegisteredContextMenu>();
            ToolsMenuItems = new List<RegisteredMenuItem>();
        }

        public string CurrentDeviceSerial { get { return getDeviceSerialFunc != null ? getDeviceSerialFunc() : null; } }
        public string CurrentPath { get { return getCurrentPathFunc != null ? getCurrentPathFunc() : "/sdcard/"; } }

        public void LoadScripts(string scriptsFolder)
        {
            LoadedScripts.Clear();
            ContextMenuItems.Clear();
            ToolsMenuItems.Clear();

            if (!Directory.Exists(scriptsFolder))
            {
                try
                {
                    Directory.CreateDirectory(scriptsFolder);
                }
                catch { }
                return;
            }

            string[] files = Directory.GetFiles(scriptsFolder, "*.cs", SearchOption.TopDirectoryOnly);
            foreach (string file in files)
            {
                var scriptInfo = CompileAndInstantiateScript(file);
                LoadedScripts.Add(scriptInfo);

                if (scriptInfo.ScriptInstance != null)
                {
                    try
                    {
                        scriptInfo.ScriptInstance.Initialize(this);
                    }
                    catch (Exception ex)
                    {
                        scriptInfo.CompilationErrors.Add("Runtime initialization error: " + ex.Message);
                    }
                }
            }
        }

        private LoadedScriptInfo CompileAndInstantiateScript(string filePath)
        {
            var info = new LoadedScriptInfo { FilePath = filePath };

            try
            {
                using (var provider = new CSharpCodeProvider())
                {
                    var parameters = new CompilerParameters
                    {
                        GenerateInMemory = true,
                        GenerateExecutable = false,
                        TreatWarningsAsErrors = false
                    };

                    // Add framework assembly references
                    parameters.ReferencedAssemblies.Add("System.dll");
                    parameters.ReferencedAssemblies.Add("System.Core.dll");
                    parameters.ReferencedAssemblies.Add("System.Drawing.dll");
                    parameters.ReferencedAssemblies.Add("System.Windows.Forms.dll");
                    parameters.ReferencedAssemblies.Add("System.IO.Compression.dll");
                    parameters.ReferencedAssemblies.Add("System.IO.Compression.FileSystem.dll");

                    // Add current running assembly (ADBFileManager.exe)
                    string currentAssemblyLocation = Assembly.GetExecutingAssembly().Location;
                    if (!string.IsNullOrEmpty(currentAssemblyLocation) && File.Exists(currentAssemblyLocation))
                    {
                        parameters.ReferencedAssemblies.Add(currentAssemblyLocation);
                    }

                    CompilerResults results = provider.CompileAssemblyFromFile(parameters, filePath);

                    if (results.Errors.HasErrors)
                    {
                        foreach (CompilerError err in results.Errors)
                        {
                            info.CompilationErrors.Add(string.Format("Line {0}, Col {1}: {2}", err.Line, err.Column, err.ErrorText));
                        }
                        return info;
                    }

                    Assembly asm = results.CompiledAssembly;
                    Type scriptInterfaceType = typeof(IScript);

                    foreach (Type type in asm.GetTypes())
                    {
                        if (scriptInterfaceType.IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract)
                        {
                            try
                            {
                                var instance = (IScript)Activator.CreateInstance(type);
                                info.ScriptInstance = instance;
                                break;
                            }
                            catch (Exception ex)
                            {
                                info.CompilationErrors.Add("Failed to create script instance: " + ex.Message);
                            }
                        }
                    }

                    if (info.ScriptInstance == null && !info.HasErrors)
                    {
                        info.CompilationErrors.Add("No class implementing IScript was found in file.");
                    }
                }
            }
            catch (Exception ex)
            {
                info.CompilationErrors.Add("Compilation exception: " + ex.Message);
            }

            return info;
        }

        #region IScriptContext Implementation

        public ExecutionResult RunAdbCommand(string args)
        {
            string serial = CurrentDeviceSerial;
            string formattedArgs = args;

            // If a device is selected and command doesn't explicitly start with -s, prepend -s DEVICEID
            if (!string.IsNullOrEmpty(serial) && !args.TrimStart().StartsWith("-s "))
            {
                formattedArgs = string.Format("-s \"{0}\" {1}", serial, args);
            }

            return adbService.RunAdbCommand(formattedArgs);
        }

        public ExecutionResult RunAdbShell(string shellCommand)
        {
            string serial = CurrentDeviceSerial;
            if (string.IsNullOrEmpty(serial))
            {
                return new ExecutionResult { ExitCode = -1, Error = "No ADB device selected." };
            }

            string cleanCmd = shellCommand.Replace("'", "'\\''");
            return adbService.RunAdbCommand(string.Format("-s \"{0}\" shell \"{1}\"", serial, cleanCmd));
        }

        public StorageInfo GetStorageInfo(string remotePath = null)
        {
            string serial = CurrentDeviceSerial;
            if (string.IsNullOrEmpty(serial)) return null;
            string targetPath = string.IsNullOrEmpty(remotePath) ? CurrentPath : remotePath;
            return adbService.GetStorageInfo(serial, targetPath);
        }

        public List<AdbFileInfo> GetFileList(string remotePath = null)
        {
            string serial = CurrentDeviceSerial;
            if (string.IsNullOrEmpty(serial)) return new List<AdbFileInfo>();
            string targetPath = string.IsNullOrEmpty(remotePath) ? CurrentPath : remotePath;
            return adbService.ListDirectory(serial, targetPath);
        }

        public ExecutionResult PushFile(string localPath, string remotePath)
        {
            string serial = CurrentDeviceSerial;
            if (string.IsNullOrEmpty(serial)) return new ExecutionResult { ExitCode = -1, Error = "No ADB device selected." };
            return adbService.Push(serial, localPath, remotePath);
        }

        public ExecutionResult PullFile(string remotePath, string localPath)
        {
            string serial = CurrentDeviceSerial;
            if (string.IsNullOrEmpty(serial)) return new ExecutionResult { ExitCode = -1, Error = "No ADB device selected." };
            return adbService.Pull(serial, remotePath, localPath);
        }

        public ExecutionResult CreateDirectory(string remotePath)
        {
            string serial = CurrentDeviceSerial;
            if (string.IsNullOrEmpty(serial)) return new ExecutionResult { ExitCode = -1, Error = "No ADB device selected." };
            return adbService.CreateFolder(serial, remotePath);
        }

        public ExecutionResult DeleteItem(string remotePath)
        {
            string serial = CurrentDeviceSerial;
            if (string.IsNullOrEmpty(serial)) return new ExecutionResult { ExitCode = -1, Error = "No ADB device selected." };
            return adbService.DeleteItem(serial, remotePath);
        }

        public ExecutionResult RenameItem(string oldPath, string newPath)
        {
            string serial = CurrentDeviceSerial;
            if (string.IsNullOrEmpty(serial)) return new ExecutionResult { ExitCode = -1, Error = "No ADB device selected." };
            return adbService.RenameItem(serial, oldPath, newPath);
        }

        public List<AdbFileInfo> GetSelectedFiles()
        {
            return getSelectedFilesFunc != null ? getSelectedFilesFunc() : new List<AdbFileInfo>();
        }

        public void RefreshFileList()
        {
            if (refreshFileListAction != null)
            {
                if (Form.ActiveForm != null && Form.ActiveForm.InvokeRequired)
                {
                    Form.ActiveForm.BeginInvoke(refreshFileListAction);
                }
                else
                {
                    refreshFileListAction();
                }
            }
        }

        public static bool MatchesWildcard(string text, string pattern)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(pattern)) return false;
            pattern = pattern.Trim();
            if (pattern == "*") return true;

            string regexPattern = "^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
            return Regex.IsMatch(text, regexPattern, RegexOptions.IgnoreCase);
        }

        public void RegisterContextMenu(string title, Action<List<AdbFileInfo>> action, string fileExtensionFilter = null, bool foldersOnly = false)
        {
            RegisterContextMenu(title, action, fileExtensionFilter, null, null, foldersOnly);
        }

        public void RegisterContextMenu(string title, Action<List<AdbFileInfo>> action, string fileExtensionFilter, string fileNameFilter, string pathFilter, bool foldersOnly = false)
        {
            ContextMenuItems.Add(new RegisteredContextMenu
            {
                Title = title,
                Action = action,
                FileExtensionFilter = fileExtensionFilter,
                FileNameFilter = fileNameFilter,
                PathFilter = pathFilter,
                FoldersOnly = foldersOnly
            });
        }

        public void RegisterToolsMenuItem(string title, Action action)
        {
            ToolsMenuItems.Add(new RegisteredMenuItem
            {
                Title = title,
                Action = action
            });
        }

        public void ShowMessage(string message, string caption = "Script Notice")
        {
            MessageBox.Show(message, caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public bool ShowConfirmation(string message, string caption = "Script Question")
        {
            return MessageBox.Show(message, caption, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
        }

        public string PromptInput(string prompt, string title = "Script Input", string defaultValue = "")
        {
            using (var dlg = new PromptDialog(prompt, title, defaultValue))
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                {
                    return dlg.InputText;
                }
            }
            return null;
        }

        public void ShowTextPreview(string title, string content)
        {
            using (var viewer = new FileViewerForm(title, content))
            {
                viewer.ShowDialog();
            }
        }

        #endregion
    }
}

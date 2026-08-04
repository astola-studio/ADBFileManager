using System;
using System.Collections.Generic;
using ADBFileManager;
using ADBFileManager.Scripting;

namespace ADBFileManager.Scripts
{
    public class PackageDumperAddon : IScript
    {
        public string Name { get { return "APK Package Inspector"; } }
        public string Description { get { return "Adds a context menu item to view package dump info for remote .apk files."; } }
        public string Author { get { return "ADB File Manager Team"; } }
        public string Version { get { return "1.0"; } }

        public void Initialize(IScriptContext context)
        {
            context.RegisterContextMenu("📦 Inspect APK Package Info", (selectedFiles) =>
            {
                if (selectedFiles == null || selectedFiles.Count == 0) return;

                foreach (var file in selectedFiles)
                {
                    string pkgName = file.Name;
                    if (pkgName.EndsWith(".apk", StringComparison.OrdinalIgnoreCase))
                    {
                        pkgName = pkgName.Substring(0, pkgName.Length - 4);
                    }

                    var res = context.RunAdbShell("pm dump " + pkgName);
                    if (string.IsNullOrEmpty(res.Output) || res.Output.Contains("Unable to find package"))
                    {
                        res = context.RunAdbShell("dumpsys package " + pkgName);
                    }

                    string outputText = !string.IsNullOrEmpty(res.Output) ? res.Output : (res.Error ?? "No output returned.");
                    context.ShowTextPreview("Package Dump: " + file.Name, outputText);
                }
            }, fileExtensionFilter: ".apk");
        }
    }
}

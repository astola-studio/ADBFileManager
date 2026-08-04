using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using ADBFileManager.Scripting;

namespace ADBFileManager.Scripts
{
    public class DeviceScreenshotAddon : IScript
    {
        public string Name { get { return "Device Screenshot Capture"; } }
        public string Description { get { return "Captures a screenshot from the connected Android device and pulls it to your PC."; } }
        public string Author { get { return "ADB File Manager Team"; } }
        public string Version { get { return "1.0"; } }

        public void Initialize(IScriptContext context)
        {
            context.RegisterToolsMenuItem("📸 Take Device Screenshot", () =>
            {
                if (string.IsNullOrEmpty(context.CurrentDeviceSerial))
                {
                    context.ShowMessage("Please select an active ADB device first.", "No Device Selected");
                    return;
                }

                string remoteTempPath = "/sdcard/screen_cap_tmp.png";
                var capResult = context.RunAdbShell("screencap -p " + remoteTempPath);

                if (capResult.ExitCode != 0)
                {
                    context.ShowMessage("Failed to capture screenshot:\n" + capResult.Error, "Screenshot Error");
                    return;
                }

                string localFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Screenshots");
                if (!Directory.Exists(localFolder))
                {
                    Directory.CreateDirectory(localFolder);
                }

                string filename = string.Format("screenshot_{0:yyyyMMdd_HHmmss}.png", DateTime.Now);
                string localFilePath = Path.Combine(localFolder, filename);

                var pullResult = context.PullFile(remoteTempPath, localFilePath);
                context.RunAdbShell("rm -f " + remoteTempPath);

                if (pullResult.ExitCode == 0 && File.Exists(localFilePath))
                {
                    if (context.ShowConfirmation("Screenshot saved to:\n" + localFilePath + "\n\nDo you want to open the image now?", "Screenshot Captured"))
                    {
                        try
                        {
                            Process.Start(localFilePath);
                        }
                        catch { }
                    }
                }
                else
                {
                    context.ShowMessage("Failed to pull screenshot file to local machine.", "Pull Error");
                }
            });
        }
    }
}

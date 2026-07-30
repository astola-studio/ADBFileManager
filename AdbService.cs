using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ADBFileManager
{
    public class AdbDevice
    {
        public string Serial { get; set; }
        public string Model { get; set; }
        public string State { get; set; }

        public override string ToString()
        {
            if (!string.IsNullOrEmpty(Model))
                return string.Format("{0} ({1}) - {2}", Model, Serial, State);
            return string.Format("{0} - {1}", Serial, State);
        }
    }

    public class AdbFileInfo
    {
        public string Name { get; set; }
        public string FullPath { get; set; }
        public bool IsDirectory { get; set; }
        public bool IsSymlink { get; set; }
        public string SymlinkTarget { get; set; }
        public long Size { get; set; }
        public string Permissions { get; set; }
        public string Owner { get; set; }
        public string Group { get; set; }
        public string ModifiedTime { get; set; }

        public string FormattedSize
        {
            get
            {
                if (IsDirectory) return "<DIR>";
                if (IsSymlink) return "<LINK>";
                
                string[] suf = { "B", "KB", "MB", "GB", "TB" };
                if (Size == 0) return "0 B";
                long bytes = Math.Abs(Size);
                int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
                double num = Math.Round(bytes / Math.Pow(1024, place), 1);
                return (Math.Sign(Size) * num).ToString() + " " + suf[place];
            }
        }
    }

    public class AdbService
    {
        public string AdbPath { get; set; }
        private static readonly Regex ProgressRegex = new Regex(@"\[\s*(\d{1,3})%\s*\]", RegexOptions.Compiled);

        public AdbService(string adbPath = "adb")
        {
            AdbPath = string.IsNullOrEmpty(adbPath) ? "adb" : adbPath;
        }

        private ExecutionResult RunAdbCommand(string args, Action<int, string> progressCallback = null, int timeoutMs = 300000)
        {
            var result = new ExecutionResult();
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = AdbPath,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };

                using (var proc = new Process())
                {
                    proc.StartInfo = psi;
                    var sbOut = new StringBuilder();
                    var sbErr = new StringBuilder();

                    Action<string> parseLine = delegate(string line)
                    {
                        if (string.IsNullOrEmpty(line)) return;
                        if (progressCallback != null)
                        {
                            var match = ProgressRegex.Match(line);
                            if (match.Success)
                            {
                                int pct;
                                if (int.TryParse(match.Groups[1].Value, out pct))
                                {
                                    progressCallback(pct, line);
                                }
                            }
                        }
                    };

                    proc.OutputDataReceived += (s, e) =>
                    {
                        if (e.Data != null)
                        {
                            sbOut.AppendLine(e.Data);
                            parseLine(e.Data);
                        }
                    };
                    proc.ErrorDataReceived += (s, e) =>
                    {
                        if (e.Data != null)
                        {
                            sbErr.AppendLine(e.Data);
                            parseLine(e.Data);
                        }
                    };

                    proc.Start();
                    proc.BeginOutputReadLine();
                    proc.BeginErrorReadLine();

                    if (proc.WaitForExit(timeoutMs))
                    {
                        result.ExitCode = proc.ExitCode;
                        result.Output = sbOut.ToString();
                        result.Error = sbErr.ToString();
                    }
                    else
                    {
                        try { proc.Kill(); } catch { }
                        result.ExitCode = -1;
                        result.Error = "Command timed out.";
                    }
                }
            }
            catch (Exception ex)
            {
                result.ExitCode = -1;
                result.Error = ex.Message;
            }
            return result;
        }

        public List<AdbDevice> GetDevices()
        {
            var list = new List<AdbDevice>();
            var res = RunAdbCommand("devices -l");
            if (res.ExitCode != 0 || string.IsNullOrEmpty(res.Output))
                return list;

            var lines = res.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.StartsWith("List of devices attached") || line.StartsWith("* daemon"))
                    continue;

                var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    var dev = new AdbDevice
                    {
                        Serial = parts[0],
                        State = parts[1]
                    };

                    for (int i = 2; i < parts.Length; i++)
                    {
                        if (parts[i].StartsWith("model:"))
                        {
                            dev.Model = parts[i].Substring(6).Replace('_', ' ');
                        }
                    }
                    list.Add(dev);
                }
            }
            return list;
        }

        public List<AdbFileInfo> ListDirectory(string serial, string remotePath)
        {
            var list = new List<AdbFileInfo>();
            string cleanPath = NormalizePath(remotePath);
            
            string cmdPath = cleanPath.Replace("'", "'\\''");
            var res = RunAdbCommand(string.Format("-s \"{0}\" shell \"ls -l -a '{1}'\"", serial, cmdPath));

            if (res.ExitCode != 0 || string.IsNullOrEmpty(res.Output))
            {
                res = RunAdbCommand(string.Format("-s \"{0}\" shell \"ls -a '{1}'\"", serial, cleanPath));
                if (!string.IsNullOrEmpty(res.Output))
                {
                    var simpleNames = res.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var name in simpleNames)
                    {
                        var trimmed = name.Trim();
                        if (trimmed == "." || trimmed == "..") continue;
                        list.Add(new AdbFileInfo
                        {
                            Name = trimmed,
                            FullPath = CombinePath(cleanPath, trimmed),
                            IsDirectory = false,
                            Permissions = "????",
                            ModifiedTime = ""
                        });
                    }
                }
                return list;
            }

            var lines = res.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                if (line.StartsWith("total ") || line.Trim().Length == 0)
                    continue;

                var item = ParseLsLine(line, cleanPath);
                if (item != null && item.Name != "." && item.Name != "..")
                {
                    list.Add(item);
                }
            }

            return list;
        }

        private AdbFileInfo ParseLsLine(string line, string parentPath)
        {
            try
            {
                string trimmed = line.Trim();
                if (trimmed.Length < 10) return null;

                char typeChar = trimmed[0];
                bool isDir = (typeChar == 'd');
                bool isLink = (typeChar == 'l');

                var tokens = Regex.Split(trimmed, @"\s+");
                if (tokens.Length < 6) return null;

                string perms = tokens[0];
                int idx = 1;

                int dummy;
                if (idx < tokens.Length && int.TryParse(tokens[idx], out dummy))
                {
                    idx++;
                }

                string owner = (idx < tokens.Length) ? tokens[idx++] : "";
                string group = (idx < tokens.Length) ? tokens[idx++] : "";

                long size = 0;
                long parsedSize;
                if (idx < tokens.Length && long.TryParse(tokens[idx], out parsedSize))
                {
                    size = parsedSize;
                    idx++;
                }

                string dateStr = "";
                string timeStr = "";

                if (idx < tokens.Length && Regex.IsMatch(tokens[idx], @"^\d{4}-\d{2}-\d{2}$"))
                {
                    dateStr = tokens[idx++];
                }
                if (idx < tokens.Length && Regex.IsMatch(tokens[idx], @"^\d{2}:\d{2}(:\d{2})?$"))
                {
                    timeStr = tokens[idx++];
                }

                string modified = (dateStr + " " + timeStr).Trim();

                if (idx >= tokens.Length) return null;

                string rawName = string.Join(" ", tokens, idx, tokens.Length - idx);
                string name = rawName;
                string target = "";

                if (isLink && rawName.Contains(" -> "))
                {
                    var parts = rawName.Split(new[] { " -> " }, StringSplitOptions.None);
                    name = parts[0];
                    target = parts.Length > 1 ? parts[1] : "";
                }

                name = name.Trim();
                if (string.IsNullOrEmpty(name)) return null;

                return new AdbFileInfo
                {
                    Name = name,
                    FullPath = CombinePath(parentPath, name),
                    IsDirectory = isDir,
                    IsSymlink = isLink,
                    SymlinkTarget = target,
                    Size = size,
                    Permissions = perms,
                    Owner = owner,
                    Group = group,
                    ModifiedTime = modified
                };
            }
            catch
            {
                return null;
            }
        }

        public bool FileExistsRemote(string serial, string remotePath)
        {
            string clean = NormalizePath(remotePath).Replace("'", "'\\''");
            var res = RunAdbCommand(string.Format("-s \"{0}\" shell \"test -e '{1}' && echo EXISTS\"", serial, clean));
            return res.ExitCode == 0 && res.Output != null && res.Output.Contains("EXISTS");
        }

        public ExecutionResult Pull(string serial, string remotePath, string localPath, Action<int, string> progressCallback = null)
        {
            return RunAdbCommand(string.Format("-s \"{0}\" pull \"{1}\" \"{2}\"", serial, remotePath, localPath), progressCallback, 600000);
        }

        public ExecutionResult Push(string serial, string localPath, string remotePath, Action<int, string> progressCallback = null)
        {
            return RunAdbCommand(string.Format("-s \"{0}\" push \"{1}\" \"{2}\"", serial, localPath, remotePath), progressCallback, 600000);
        }

        public ExecutionResult CreateFolder(string serial, string remotePath)
        {
            string clean = NormalizePath(remotePath).Replace("'", "'\\''");
            return RunAdbCommand(string.Format("-s \"{0}\" shell \"mkdir -p '{1}'\"", serial, clean));
        }

        public ExecutionResult DeleteItem(string serial, string remotePath)
        {
            string clean = NormalizePath(remotePath).Replace("'", "'\\''");
            return RunAdbCommand(string.Format("-s \"{0}\" shell \"rm -rf '{1}'\"", serial, clean));
        }

        public ExecutionResult RenameItem(string serial, string oldPath, string newPath)
        {
            string cleanOld = NormalizePath(oldPath).Replace("'", "'\\''");
            string cleanNew = NormalizePath(newPath).Replace("'", "'\\''");
            return RunAdbCommand(string.Format("-s \"{0}\" shell \"mv '{1}' '{2}'\"", serial, cleanOld, cleanNew));
        }

        public ExecutionResult CopyItem(string serial, string sourcePath, string targetPath)
        {
            string cleanSrc = NormalizePath(sourcePath).Replace("'", "'\\''");
            string cleanDest = NormalizePath(targetPath).Replace("'", "'\\''");
            return RunAdbCommand(string.Format("-s \"{0}\" shell \"cp -r '{1}' '{2}'\"", serial, cleanSrc, cleanDest));
        }

        public string ReadTextFile(string serial, string remotePath, int maxLines = 1000)
        {
            string clean = NormalizePath(remotePath).Replace("'", "'\\''");
            var res = RunAdbCommand(string.Format("-s \"{0}\" shell \"head -n {1} '{2}'\"", serial, maxLines, clean));
            if (res.ExitCode == 0 && !string.IsNullOrEmpty(res.Output))
            {
                return res.Output;
            }
            return res.Error ?? "Failed to read file.";
        }

        public StorageInfo GetStorageInfo(string serial, string remotePath)
        {
            string clean = NormalizePath(remotePath).Replace("'", "'\\''");
            var res = RunAdbCommand(string.Format("-s \"{0}\" shell \"df -k '{1}'\"", serial, clean));
            if (res.ExitCode != 0 || string.IsNullOrEmpty(res.Output))
            {
                res = RunAdbCommand(string.Format("-s \"{0}\" shell \"df '{1}'\"", serial, clean));
                if (res.ExitCode != 0 || string.IsNullOrEmpty(res.Output)) return null;
            }

            var lines = res.Output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var tokens = new List<string>();

            foreach (var rawLine in lines)
            {
                string line = rawLine.Trim();
                if (line.StartsWith("Filesystem") || line.StartsWith("1K-blocks") || line.StartsWith("Size"))
                    continue;

                var parts = Regex.Split(line, @"\s+");
                foreach (var p in parts)
                {
                    if (!string.IsNullOrEmpty(p)) tokens.Add(p);
                }
            }

            if (tokens.Count < 3) return null;

            long total = 0, used = 0, avail = 0;
            string mount = "";

            for (int i = 0; i < tokens.Count - 2; i++)
            {
                long tVal, uVal, aVal;
                if (TryParseSizeToBytes(tokens[i], out tVal) &&
                    TryParseSizeToBytes(tokens[i + 1], out uVal) &&
                    TryParseSizeToBytes(tokens[i + 2], out aVal))
                {
                    total = tVal;
                    used = uVal;
                    avail = aVal;
                    if (i + 4 < tokens.Count) mount = tokens[i + 4];
                    else if (i + 3 < tokens.Count && tokens[i + 3].StartsWith("/")) mount = tokens[i + 3];
                    break;
                }
            }

            if (total <= 0) return null;

            return new StorageInfo
            {
                TotalBytes = total,
                UsedBytes = used,
                AvailableBytes = avail,
                MountedOn = mount
            };
        }

        private static bool TryParseSizeToBytes(string token, out long bytes)
        {
            bytes = 0;
            if (string.IsNullOrEmpty(token)) return false;
            token = token.Trim().ToUpper();

            long rawNum;
            if (long.TryParse(token, out rawNum))
            {
                bytes = rawNum * 1024;
                return true;
            }

            var match = Regex.Match(token, @"^([\d\.]+)([KMGTB])?$");
            if (match.Success)
            {
                double val;
                if (double.TryParse(match.Groups[1].Value, out val))
                {
                    string unit = match.Groups[2].Value;
                    double mult = 1024;
                    if (unit == "B") mult = 1;
                    else if (unit == "K") mult = 1024;
                    else if (unit == "M") mult = 1024 * 1024;
                    else if (unit == "G") mult = 1024 * 1024 * 1024;
                    else if (unit == "T") mult = 1024L * 1024 * 1024 * 1024;

                    bytes = (long)(val * mult);
                    return true;
                }
            }

            return false;
        }

        public static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "/";
            path = path.Replace('\\', '/');
            if (!path.StartsWith("/")) path = "/" + path;
            while (path.Contains("//")) path = path.Replace("//", "/");
            return path;
        }

        public static string CombinePath(string parent, string child)
        {
            parent = NormalizePath(parent);
            if (parent == "/") return "/" + child;
            return parent + "/" + child;
        }
    }

    public class StorageInfo
    {
        public long TotalBytes { get; set; }
        public long UsedBytes { get; set; }
        public long AvailableBytes { get; set; }
        public string MountedOn { get; set; }

        public string FormattedTotal
        {
            get { return FormatBytes(TotalBytes); }
        }

        public string FormattedUsed
        {
            get { return FormatBytes(UsedBytes); }
        }

        public string FormattedAvailable
        {
            get { return FormatBytes(AvailableBytes); }
        }

        public int PercentUsed
        {
            get
            {
                if (TotalBytes <= 0) return 0;
                return (int)Math.Min(100, Math.Max(0, (UsedBytes * 100) / TotalBytes));
            }
        }

        public static string FormatBytes(long bytes)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB" };
            if (bytes == 0) return "0 B";
            long absBytes = Math.Abs(bytes);
            int place = Convert.ToInt32(Math.Floor(Math.Log(absBytes, 1024)));
            double num = Math.Round(absBytes / Math.Pow(1024, place), 1);
            return (Math.Sign(bytes) * num).ToString() + " " + suf[place];
        }
    }

    public class ExecutionResult
    {
        public int ExitCode { get; set; }
        public string Output { get; set; }
        public string Error { get; set; }
    }
}

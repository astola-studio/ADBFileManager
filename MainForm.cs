using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ADBFileManager
{
    public class MainForm : Form
    {
        private AdbService adbService;
        private string currentDeviceSerial = null;
        private string currentPath = "/sdcard/";
        private List<string> pathHistory = new List<string>();
        private int historyIndex = -1;

        private List<AdbFileInfo> currentFiles = new List<AdbFileInfo>();

        // UI Controls
        private ComboBox cmbDevices;
        private Button btnRefreshDevices;
        private Label lblDeviceStatus;
        
        private Button btnBack;
        private Button btnUp;
        private Button btnHome;
        private Button btnRefresh;
        private TextBox txtAddressBar;
        private Button btnGo;

        private ToolStrip toolStripActions;
        private ToolStripButton btnUploadFile;
        private ToolStripButton btnUploadFolder;
        private ToolStripButton btnDownload;
        private ToolStripButton btnCopy;
        private ToolStripButton btnCut;
        private ToolStripButton btnPaste;
        private ToolStripButton btnNewFolder;
        private ToolStripButton btnDelete;
        private ToolStripButton btnViewFile;
        private ToolStripTextBox txtFilter;

        private ToolStripMenuItem menuCopy;
        private ToolStripMenuItem menuCut;
        private ToolStripMenuItem menuPaste;

        private class AdbClipboardData
        {
            public List<AdbFileInfo> Items { get; set; }
            public bool IsCut { get; set; }
        }
        private AdbClipboardData internalClipboard = null;

        private class TransferItem
        {
            public string LocalPath { get; set; }
            public string TargetDir { get; set; }
            public string Serial { get; set; }
            public string ItemName { get; set; }
        }

        private readonly ConcurrentQueue<TransferItem> uploadQueue = new ConcurrentQueue<TransferItem>();
        private readonly SemaphoreSlim queueSemaphore = new SemaphoreSlim(1, 1);
        private int queuedTotalCount = 0;
        private int queuedProcessedCount = 0;
        private ConflictResult queueConflictState = ConflictResult.Overwrite;

        private SplitContainer splitContainer;
        private ListBox lstQuickLinks;
        private ListView lstFiles;

        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusLabelInfo;
        private ToolStripStatusLabel statusLabelProgress;
        private ToolStripStatusLabel statusLabelStorage;
        private ToolStripProgressBar progressBar;

        private ContextMenuStrip contextMenuFiles;
        private ImageList imageListSmall;

        public MainForm()
        {
            adbService = new AdbService();
            InitializeComponent();
            SetupIcons();
            
            this.Load += MainForm_Load;
        }

        private void InitializeComponent()
        {
            this.Text = "ADB File Manager - Astola Studio";
            this.ClientSize = new Size(1000, 650);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            // 1. Device Selection Panel (Top)
            var pnlTop = new Panel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(8, 6, 8, 4), BackColor = Color.FromArgb(242, 244, 247) };
            
            var lblDev = new Label { Text = "Target Device:", AutoSize = true, Location = new Point(10, 10), Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
            cmbDevices = new ComboBox { Location = new Point(105, 7), Width = 320, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbDevices.SelectedIndexChanged += CmbDevices_SelectedIndexChanged;

            btnRefreshDevices = new Button { Text = "Refresh Devices", Location = new Point(435, 6), Width = 110, Height = 27 };
            btnRefreshDevices.Click += async (s, e) => await RefreshDevicesAsync();

            lblDeviceStatus = new Label { Text = "No device selected", AutoSize = true, Location = new Point(555, 10), ForeColor = Color.DarkSlateGray };

            pnlTop.Controls.Add(lblDev);
            pnlTop.Controls.Add(cmbDevices);
            pnlTop.Controls.Add(btnRefreshDevices);
            pnlTop.Controls.Add(lblDeviceStatus);

            // 2. Navigation Panel
            var pnlNav = new Panel { Dock = DockStyle.Top, Height = 38, Padding = new Padding(8, 4, 8, 4), BackColor = Color.FromArgb(248, 249, 250) };
            
            btnBack = new Button { Text = "◄ Back", Location = new Point(8, 5), Width = 65, Height = 28, Enabled = false };
            btnBack.Click += BtnBack_Click;

            btnUp = new Button { Text = "▲ Up", Location = new Point(78, 5), Width = 60, Height = 28 };
            btnUp.Click += async (s, e) => await NavigateUpAsync();

            btnHome = new Button { Text = "⌂ Home", Location = new Point(143, 5), Width = 65, Height = 28 };
            btnHome.Click += async (s, e) => await NavigateToAsync("/sdcard/");

            btnRefresh = new Button { Text = "↻", Location = new Point(213, 5), Width = 35, Height = 28, Font = new Font("Segoe UI", 10F, FontStyle.Bold) };
            btnRefresh.Click += async (s, e) => await LoadCurrentDirectoryAsync();

            txtAddressBar = new TextBox { Location = new Point(253, 7), Width = 660, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top };
            txtAddressBar.KeyDown += async (s, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; await NavigateToAsync(txtAddressBar.Text); } };

            btnGo = new Button { Text = "Go", Location = new Point(918, 5), Width = 45, Height = 28, Anchor = AnchorStyles.Right | AnchorStyles.Top };
            btnGo.Click += async (s, e) => await NavigateToAsync(txtAddressBar.Text);

            pnlNav.Controls.Add(btnBack);
            pnlNav.Controls.Add(btnUp);
            pnlNav.Controls.Add(btnHome);
            pnlNav.Controls.Add(btnRefresh);
            pnlNav.Controls.Add(txtAddressBar);
            pnlNav.Controls.Add(btnGo);

            // 3. Action Toolbar
            toolStripActions = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Padding = new Padding(6, 2, 6, 2), BackColor = Color.White };
            
            btnUploadFile = new ToolStripButton("Upload File", null, async (s, e) => await UploadFileAsync()) { DisplayStyle = ToolStripItemDisplayStyle.ImageAndText };
            btnUploadFolder = new ToolStripButton("Upload Folder", null, async (s, e) => await UploadFolderAsync()) { DisplayStyle = ToolStripItemDisplayStyle.ImageAndText };
            btnDownload = new ToolStripButton("Download", null, async (s, e) => await DownloadSelectedAsync()) { DisplayStyle = ToolStripItemDisplayStyle.ImageAndText };
            btnCopy = new ToolStripButton("Copy", null, (s, e) => CopySelectedToClipboard(false)) { DisplayStyle = ToolStripItemDisplayStyle.ImageAndText };
            btnCut = new ToolStripButton("Cut", null, (s, e) => CopySelectedToClipboard(true)) { DisplayStyle = ToolStripItemDisplayStyle.ImageAndText };
            btnPaste = new ToolStripButton("Paste", null, async (s, e) => await PasteClipboardAsync()) { DisplayStyle = ToolStripItemDisplayStyle.ImageAndText, Enabled = false };
            btnNewFolder = new ToolStripButton("New Folder", null, async (s, e) => await CreateNewFolderAsync()) { DisplayStyle = ToolStripItemDisplayStyle.ImageAndText };
            btnDelete = new ToolStripButton("Delete", null, async (s, e) => await DeleteSelectedAsync()) { DisplayStyle = ToolStripItemDisplayStyle.ImageAndText };
            btnViewFile = new ToolStripButton("Preview File", null, async (s, e) => await PreviewSelectedFileAsync()) { DisplayStyle = ToolStripItemDisplayStyle.ImageAndText };

            var lblFilter = new ToolStripLabel(" Filter: ") { Alignment = ToolStripItemAlignment.Right };
            txtFilter = new ToolStripTextBox { Width = 130, Alignment = ToolStripItemAlignment.Right };
            txtFilter.TextChanged += (s, e) => ApplyFilter();

            toolStripActions.Items.Add(btnUploadFile);
            toolStripActions.Items.Add(btnUploadFolder);
            toolStripActions.Items.Add(new ToolStripSeparator());
            toolStripActions.Items.Add(btnDownload);
            toolStripActions.Items.Add(btnViewFile);
            toolStripActions.Items.Add(new ToolStripSeparator());
            toolStripActions.Items.Add(btnCopy);
            toolStripActions.Items.Add(btnCut);
            toolStripActions.Items.Add(btnPaste);
            toolStripActions.Items.Add(new ToolStripSeparator());
            toolStripActions.Items.Add(btnNewFolder);
            toolStripActions.Items.Add(btnDelete);
            toolStripActions.Items.Add(txtFilter);
            toolStripActions.Items.Add(lblFilter);

            // 4. Main Split Container (Sidebar + File List)
            splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 180,
                FixedPanel = FixedPanel.Panel1
            };

            // Quick Links Sidebar
            var lblQuick = new Label { Text = "Quick Locations", Dock = DockStyle.Top, Height = 28, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 0, 0, 0), Font = new Font("Segoe UI", 9F, FontStyle.Bold), BackColor = Color.FromArgb(235, 238, 242) };
            lstQuickLinks = new ListBox { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, ItemHeight = 22 };
            lstQuickLinks.Items.AddRange(new object[] {
                "/sdcard/",
                "/storage/emulated/0/",
                "/sdcard/Download",
                "/sdcard/DCIM",
                "/sdcard/Pictures",
                "/sdcard/Documents",
                "/data/local/tmp/",
                "/system/",
                "/"
            });
            lstQuickLinks.SelectedIndexChanged += async (s, e) =>
            {
                if (lstQuickLinks.SelectedItem != null)
                {
                    await NavigateToAsync(lstQuickLinks.SelectedItem.ToString());
                }
            };

            splitContainer.Panel1.Controls.Add(lstQuickLinks);
            splitContainer.Panel1.Controls.Add(lblQuick);

            // Main ListView
            lstFiles = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = true,
                BorderStyle = BorderStyle.None
            };

            lstFiles.Columns.Add("Name", 280);
            lstFiles.Columns.Add("Size", 90, HorizontalAlignment.Right);
            lstFiles.Columns.Add("Type", 80);
            lstFiles.Columns.Add("Date Modified", 140);
            lstFiles.Columns.Add("Permissions", 100);
            lstFiles.Columns.Add("Owner/Group", 120);

            lstFiles.AllowDrop = true;
            lstFiles.DragEnter += LstFiles_DragEnter;
            lstFiles.DragDrop += LstFiles_DragDrop;

            lstFiles.DoubleClick += async (s, e) => await OnItemDoubleClickAsync();
            lstFiles.ColumnClick += LstFiles_ColumnClick;

            // Keyboard Shortcuts (Delete, Ctrl+C, Ctrl+X, Ctrl+V, Ctrl+A)
            lstFiles.KeyDown += async (s, e) =>
            {
                if (e.KeyCode == Keys.Delete)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    await DeleteSelectedAsync();
                }
                else if (e.Control && e.KeyCode == Keys.C)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    CopySelectedToClipboard(false);
                }
                else if (e.Control && e.KeyCode == Keys.X)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    CopySelectedToClipboard(true);
                }
                else if (e.Control && e.KeyCode == Keys.V)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    await PasteClipboardAsync();
                }
                else if (e.Control && e.KeyCode == Keys.A)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    foreach (ListViewItem lvi in lstFiles.Items)
                    {
                        lvi.Selected = true;
                    }
                }
            };

            // Context Menu for Files
            contextMenuFiles = new ContextMenuStrip();
            contextMenuFiles.Items.Add("Open / Navigate", null, async (s, e) => await OnItemDoubleClickAsync());
            contextMenuFiles.Items.Add(new ToolStripSeparator());
            
            menuCut = new ToolStripMenuItem("Cut (Ctrl+X)", null, (s, e) => CopySelectedToClipboard(true));
            menuCopy = new ToolStripMenuItem("Copy (Ctrl+C)", null, (s, e) => CopySelectedToClipboard(false));
            menuPaste = new ToolStripMenuItem("Paste (Ctrl+V)", null, async (s, e) => await PasteClipboardAsync()) { Enabled = false };

            contextMenuFiles.Items.Add(menuCut);
            contextMenuFiles.Items.Add(menuCopy);
            contextMenuFiles.Items.Add(menuPaste);
            contextMenuFiles.Items.Add(new ToolStripSeparator());
            contextMenuFiles.Items.Add("Download to PC...", null, async (s, e) => await DownloadSelectedAsync());
            contextMenuFiles.Items.Add("Upload File Here...", null, async (s, e) => await UploadFileAsync());
            contextMenuFiles.Items.Add("Upload Folder Here...", null, async (s, e) => await UploadFolderAsync());
            contextMenuFiles.Items.Add(new ToolStripSeparator());
            contextMenuFiles.Items.Add("Preview Text", null, async (s, e) => await PreviewSelectedFileAsync());
            contextMenuFiles.Items.Add("New Folder...", null, async (s, e) => await CreateNewFolderAsync());
            contextMenuFiles.Items.Add("Rename...", null, async (s, e) => await RenameSelectedAsync());
            contextMenuFiles.Items.Add("Delete", null, async (s, e) => await DeleteSelectedAsync());
            contextMenuFiles.Items.Add(new ToolStripSeparator());
            contextMenuFiles.Items.Add("Copy Remote Path", null, (s, e) => CopySelectedPathToClipboard());

            contextMenuFiles.Opening += (s, e) =>
            {
                bool hasSelection = lstFiles.SelectedItems.Count > 0;
                bool hasClipboard = internalClipboard != null && internalClipboard.Items != null && internalClipboard.Items.Count > 0;
                menuCut.Enabled = hasSelection;
                menuCopy.Enabled = hasSelection;
                menuPaste.Enabled = hasClipboard;
                btnCopy.Enabled = hasSelection;
                btnCut.Enabled = hasSelection;
            };

            lstFiles.ContextMenuStrip = contextMenuFiles;

            splitContainer.Panel2.Controls.Add(lstFiles);

            // 5. Bottom Status Strip
            statusStrip = new StatusStrip();
            statusLabelInfo = new ToolStripStatusLabel("Ready") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            statusLabelProgress = new ToolStripStatusLabel("") { Alignment = ToolStripItemAlignment.Right };
            statusLabelStorage = new ToolStripStatusLabel("Storage: --")
            {
                Alignment = ToolStripItemAlignment.Right,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.DarkSlateGray
            };
            progressBar = new ToolStripProgressBar
            {
                Width = 140,
                Visible = false,
                Style = ProgressBarStyle.Blocks,
                Minimum = 0,
                Maximum = 100,
                Alignment = ToolStripItemAlignment.Right
            };

            statusStrip.Items.Add(statusLabelInfo);
            statusStrip.Items.Add(progressBar);
            statusStrip.Items.Add(statusLabelProgress);
            statusStrip.Items.Add(statusLabelStorage);

            try
            {
                this.Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
                if (System.IO.File.Exists("icon.ico"))
                {
                    try { this.Icon = new Icon("icon.ico"); } catch { }
                }
            }

            // Assembly layout
            this.Controls.Add(splitContainer);
            this.Controls.Add(toolStripActions);
            this.Controls.Add(pnlNav);
            this.Controls.Add(pnlTop);
            this.Controls.Add(statusStrip);
        }

        private void SetupIcons()
        {
            imageListSmall = new ImageList { ImageSize = new Size(16, 16), ColorDepth = ColorDepth.Depth32Bit };
            
            // Generate basic procedural icons for Folder, File, and Symlink
            Bitmap bmpFolder = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(bmpFolder))
            {
                g.Clear(Color.Transparent);
                g.FillRectangle(Brushes.Goldenrod, 1, 3, 14, 11);
                g.FillRectangle(Brushes.Gold, 1, 5, 14, 9);
                g.DrawRectangle(Pens.DarkGoldenrod, 1, 3, 14, 11);
            }

            Bitmap bmpFile = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(bmpFile))
            {
                g.Clear(Color.Transparent);
                g.FillRectangle(Brushes.White, 3, 1, 10, 14);
                g.DrawRectangle(Pens.Gray, 3, 1, 10, 14);
                g.DrawLine(Pens.SteelBlue, 5, 4, 11, 4);
                g.DrawLine(Pens.SteelBlue, 5, 7, 11, 7);
                g.DrawLine(Pens.SteelBlue, 5, 10, 9, 10);
            }

            Bitmap bmpLink = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(bmpLink))
            {
                g.Clear(Color.Transparent);
                g.FillRectangle(Brushes.LightSkyBlue, 2, 2, 12, 12);
                g.DrawRectangle(Pens.DeepSkyBlue, 2, 2, 12, 12);
                g.DrawString("L", new Font("Segoe UI", 7F, FontStyle.Bold), Brushes.DarkBlue, 3, 1);
            }

            imageListSmall.Images.Add("folder", bmpFolder);
            imageListSmall.Images.Add("file", bmpFile);
            imageListSmall.Images.Add("link", bmpLink);

            lstFiles.SmallImageList = imageListSmall;
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            await RefreshDevicesAsync();
        }

        private async Task RefreshDevicesAsync()
        {
            statusLabelInfo.Text = "Scanning ADB devices...";
            cmbDevices.Items.Clear();

            var devices = await Task.Run(() => adbService.GetDevices());

            if (devices.Count == 0)
            {
                lblDeviceStatus.Text = "No ADB devices connected.";
                lblDeviceStatus.ForeColor = Color.Red;
                currentDeviceSerial = null;
                lstFiles.Items.Clear();
                statusLabelInfo.Text = "No ADB device detected. Ensure USB Debugging is enabled and 'adb devices' works.";
            }
            else
            {
                foreach (var d in devices)
                {
                    cmbDevices.Items.Add(d);
                }
                cmbDevices.SelectedIndex = 0;
                lblDeviceStatus.Text = string.Format("Found {0} device(s)", devices.Count);
                lblDeviceStatus.ForeColor = Color.DarkGreen;
            }
        }

        private async void CmbDevices_SelectedIndexChanged(object sender, EventArgs e)
        {
            var dev = cmbDevices.SelectedItem as AdbDevice;
            if (dev != null)
            {
                currentDeviceSerial = dev.Serial;
                await NavigateToAsync("/sdcard/");
            }
        }

        private async Task NavigateToAsync(string targetPath, bool recordHistory = true)
        {
            if (string.IsNullOrEmpty(currentDeviceSerial))
            {
                MessageBox.Show("Please select a valid connected ADB device first.", "No Device", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string clean = AdbService.NormalizePath(targetPath);
            currentPath = clean;
            txtAddressBar.Text = clean;

            if (recordHistory)
            {
                if (historyIndex < pathHistory.Count - 1)
                {
                    pathHistory.RemoveRange(historyIndex + 1, pathHistory.Count - historyIndex - 1);
                }
                pathHistory.Add(clean);
                historyIndex = pathHistory.Count - 1;
                btnBack.Enabled = (historyIndex > 0);
            }

            await LoadCurrentDirectoryAsync();
        }

        private async Task NavigateUpAsync()
        {
            if (currentPath == "/") return;
            int lastSlash = currentPath.TrimEnd('/').LastIndexOf('/');
            string parent = (lastSlash <= 0) ? "/" : currentPath.Substring(0, lastSlash);
            await NavigateToAsync(parent);
        }

        private async void BtnBack_Click(object sender, EventArgs e)
        {
            if (historyIndex > 0)
            {
                historyIndex--;
                btnBack.Enabled = (historyIndex > 0);
                await NavigateToAsync(pathHistory[historyIndex], false);
            }
        }

        private async Task LoadCurrentDirectoryAsync()
        {
            if (string.IsNullOrEmpty(currentDeviceSerial)) return;

            statusLabelInfo.Text = "Loading: " + currentPath + "...";
            lstFiles.BeginUpdate();
            lstFiles.Items.Clear();

            string serial = currentDeviceSerial;
            string path = currentPath;

            var items = await Task.Run(() => adbService.ListDirectory(serial, path));
            currentFiles = items;

            DisplayFiles(currentFiles);

            lstFiles.EndUpdate();
            int dirCount = items.FindAll(delegate(AdbFileInfo i) { return i.IsDirectory; }).Count;
            int fileCount = items.FindAll(delegate(AdbFileInfo i) { return !i.IsDirectory && !i.IsSymlink; }).Count;

            statusLabelInfo.Text = string.Format("Path: {0} | Items: {1} ({2} folders, {3} files)",
                currentPath, items.Count, dirCount, fileCount);

            await UpdateStorageInfoAsync();
        }

        private async Task UpdateStorageInfoAsync()
        {
            if (string.IsNullOrEmpty(currentDeviceSerial))
            {
                statusLabelStorage.Text = "Storage: N/A";
                return;
            }

            string serial = currentDeviceSerial;
            string path = currentPath;

            var info = await Task.Run(() => adbService.GetStorageInfo(serial, path));
            if (info != null)
            {
                statusLabelStorage.Text = string.Format("Storage: {0} free of {1} ({2}% used)",
                    info.FormattedAvailable, info.FormattedTotal, info.PercentUsed);
                statusLabelStorage.ToolTipText = string.Format("Used: {0} / Total: {1} (Mount: {2})",
                    info.FormattedUsed, info.FormattedTotal, info.MountedOn);
            }
            else
            {
                statusLabelStorage.Text = "Storage: --";
            }
        }

        private void DisplayFiles(List<AdbFileInfo> files)
        {
            lstFiles.Items.Clear();

            // Sort directories first, then files
            var ordered = files.OrderByDescending(f => f.IsDirectory)
                               .ThenBy(f => f.Name)
                               .ToList();

            foreach (var item in ordered)
            {
                string imgKey = item.IsDirectory ? "folder" : (item.IsSymlink ? "link" : "file");
                string typeStr = item.IsDirectory ? "Folder" : (item.IsSymlink ? "Symlink" : "File");

                var lvi = new ListViewItem(item.Name, imgKey)
                {
                    Tag = item
                };
                lvi.SubItems.Add(item.FormattedSize);
                lvi.SubItems.Add(typeStr);
                lvi.SubItems.Add(item.ModifiedTime);
                lvi.SubItems.Add(item.Permissions);
                lvi.SubItems.Add(string.Format("{0}/{1}", item.Owner, item.Group));

                lstFiles.Items.Add(lvi);
            }
        }

        private void ApplyFilter()
        {
            if (currentFiles == null) return;
            string filter = txtFilter.Text.Trim().ToLower();
            if (string.IsNullOrEmpty(filter))
            {
                DisplayFiles(currentFiles);
            }
            else
            {
                var filtered = currentFiles.Where(f => f.Name.ToLower().Contains(filter)).ToList();
                DisplayFiles(filtered);
            }
        }

        private async Task OnItemDoubleClickAsync()
        {
            if (lstFiles.SelectedItems.Count == 0) return;
            var item = lstFiles.SelectedItems[0].Tag as AdbFileInfo;
            if (item == null) return;

            if (item.IsDirectory)
            {
                await NavigateToAsync(item.FullPath);
            }
            else if (item.IsSymlink && !string.IsNullOrEmpty(item.SymlinkTarget))
            {
                await NavigateToAsync(item.SymlinkTarget);
            }
            else
            {
                // Offer preview or download
                var res = MessageBox.Show(string.Format("Preview file '{0}'?", item.Name), "View File", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (res == DialogResult.Yes)
                {
                    await PreviewSelectedFileAsync();
                }
                else if (res == DialogResult.No)
                {
                    await DownloadSelectedAsync();
                }
            }
        }

        private async Task PreviewSelectedFileAsync()
        {
            if (lstFiles.SelectedItems.Count == 0) return;
            var item = lstFiles.SelectedItems[0].Tag as AdbFileInfo;
            if (item == null || item.IsDirectory) return;

            statusLabelInfo.Text = "Reading file: " + item.Name + "...";
            string serial = currentDeviceSerial;
            string fullPath = item.FullPath;

            string text = await Task.Run(() => adbService.ReadTextFile(serial, fullPath, 1500));
            statusLabelInfo.Text = "Ready";

            using (var dlg = new FileViewerForm(item.Name, text))
            {
                dlg.ShowDialog(this);
            }
        }

        private void SetProgress(int percent, string message = null)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action<int, string>(SetProgress), percent, message);
                return;
            }

            if (percent < 0)
            {
                progressBar.Visible = false;
            }
            else
            {
                progressBar.Visible = true;
                progressBar.Value = Math.Max(0, Math.Min(100, percent));
            }

            if (message != null)
            {
                statusLabelInfo.Text = message;
            }
        }

        private async Task DownloadSelectedAsync()
        {
            if (lstFiles.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select one or more files to download.", "No Selection", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (var fbd = new FolderBrowserDialog { Description = "Select PC folder to save downloaded items" })
            {
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    string targetFolder = fbd.SelectedPath;
                    int successCount = 0;
                    int totalItems = lstFiles.SelectedItems.Count;
                    ConflictResult conflictState = ConflictResult.Overwrite;

                    for (int i = 0; i < totalItems; i++)
                    {
                        var info = lstFiles.SelectedItems[i].Tag as AdbFileInfo;
                        if (info == null) continue;

                        string local = Path.Combine(targetFolder, info.Name);
                        bool existsLocally = File.Exists(local) || Directory.Exists(local);

                        if (existsLocally)
                        {
                            if (conflictState == ConflictResult.SkipAll) continue;
                            if (conflictState != ConflictResult.OverwriteAll)
                            {
                                using (var dlg = new ConflictDialog(info.Name, targetFolder, false))
                                {
                                    if (dlg.ShowDialog(this) != DialogResult.OK || dlg.SelectedResult == ConflictResult.Cancel)
                                    {
                                        break;
                                    }
                                    if (dlg.SelectedResult == ConflictResult.Skip) continue;
                                    if (dlg.SelectedResult == ConflictResult.SkipAll)
                                    {
                                        conflictState = ConflictResult.SkipAll;
                                        continue;
                                    }
                                    if (dlg.SelectedResult == ConflictResult.OverwriteAll)
                                    {
                                        conflictState = ConflictResult.OverwriteAll;
                                    }
                                }
                            }
                        }

                        int itemIdx = i;
                        string serial = currentDeviceSerial;
                        string remote = info.FullPath;

                        SetProgress((itemIdx * 100) / totalItems, string.Format("Downloading ({0}/{1}): {2}...", itemIdx + 1, totalItems, info.Name));

                        var res = await Task.Run(delegate()
                        {
                            return adbService.Pull(serial, remote, local, delegate(int pct, string status)
                            {
                                int totalPct = ((itemIdx * 100) + pct) / totalItems;
                                SetProgress(totalPct, string.Format("Downloading ({0}/{1}): {2} [{3}%]", itemIdx + 1, totalItems, info.Name, pct));
                            });
                        });

                        if (res.ExitCode == 0)
                        {
                            successCount++;
                        }
                        else
                        {
                            MessageBox.Show(string.Format("Failed to pull '{0}':\n{1}", info.Name, res.Error), "Pull Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }

                    SetProgress(-1, string.Format("Successfully downloaded {0} item(s) to {1}", successCount, targetFolder));
                }
            }
        }

        private async Task UploadFileAsync()
        {
            if (string.IsNullOrEmpty(currentDeviceSerial)) return;

            using (var ofd = new OpenFileDialog { Title = "Select File to Upload to ADB Device", Multiselect = true })
            {
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    await UploadItemsInternalAsync(ofd.FileNames, currentPath);
                }
            }
        }

        private async Task UploadFolderAsync()
        {
            if (string.IsNullOrEmpty(currentDeviceSerial)) return;

            using (var fbd = new FolderBrowserDialog { Description = "Select PC Folder to Upload to ADB Device" })
            {
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    await UploadItemsInternalAsync(new string[] { fbd.SelectedPath }, currentPath);
                }
            }
        }

        private void LstFiles_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private async void LstFiles_DragDrop(object sender, DragEventArgs e)
        {
            if (string.IsNullOrEmpty(currentDeviceSerial))
            {
                MessageBox.Show("Please select a valid connected ADB device first.", "No Device", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] droppedPaths = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (droppedPaths != null && droppedPaths.Length > 0)
                {
                    await UploadItemsInternalAsync(droppedPaths, currentPath);
                }
            }
        }

        private async Task UploadItemsInternalAsync(IEnumerable<string> localPaths, string targetDir)
        {
            if (string.IsNullOrEmpty(currentDeviceSerial)) return;

            var pathList = localPaths.Where(p => File.Exists(p) || Directory.Exists(p)).ToList();
            if (pathList.Count == 0) return;

            string serial = currentDeviceSerial;

            foreach (string localPath in pathList)
            {
                string itemName = Path.GetFileName(localPath);
                if (string.IsNullOrEmpty(itemName)) itemName = localPath;

                uploadQueue.Enqueue(new TransferItem
                {
                    LocalPath = localPath,
                    TargetDir = targetDir,
                    Serial = serial,
                    ItemName = itemName
                });
            }

            queuedTotalCount += pathList.Count;
            SetProgress(-1, string.Format("Queued {0} item(s) for upload... (Queue: {1})", pathList.Count, uploadQueue.Count));

            await ProcessUploadQueueAsync();
        }

        private async Task ProcessUploadQueueAsync()
        {
            if (!await queueSemaphore.WaitAsync(0))
            {
                return;
            }

            int sessionSuccessCount = 0;
            TransferItem item;

            try
            {
                while (uploadQueue.TryDequeue(out item))
                {
                    if (string.IsNullOrEmpty(currentDeviceSerial) || item.Serial != currentDeviceSerial)
                    {
                        continue;
                    }

                    queuedProcessedCount++;
                    int currentItemIdx = queuedProcessedCount;

                    string remoteTarget = AdbService.CombinePath(item.TargetDir, item.ItemName);

                    string serial = item.Serial;
                    bool existsRemote = await Task.Run(delegate() { return adbService.FileExistsRemote(serial, remoteTarget); });

                    if (existsRemote)
                    {
                        if (queueConflictState == ConflictResult.SkipAll) continue;
                        if (queueConflictState != ConflictResult.OverwriteAll)
                        {
                            using (var dlg = new ConflictDialog(item.ItemName, item.TargetDir, true))
                            {
                                if (dlg.ShowDialog(this) != DialogResult.OK || dlg.SelectedResult == ConflictResult.Cancel)
                                {
                                    TransferItem discardItem;
                                    while (uploadQueue.TryDequeue(out discardItem)) { }
                                    break;
                                }
                                if (dlg.SelectedResult == ConflictResult.Skip) continue;
                                if (dlg.SelectedResult == ConflictResult.SkipAll)
                                {
                                    queueConflictState = ConflictResult.SkipAll;
                                    continue;
                                }
                                if (dlg.SelectedResult == ConflictResult.OverwriteAll)
                                {
                                    queueConflictState = ConflictResult.OverwriteAll;
                                }
                            }
                        }
                    }

                    int totalNow = queuedTotalCount > 0 ? queuedTotalCount : 1;
                    SetProgress(((currentItemIdx - 1) * 100) / totalNow, string.Format("Uploading ({0}/{1}): {2}...", currentItemIdx, totalNow, item.ItemName));

                    var res = await Task.Run(delegate()
                    {
                        return adbService.Push(serial, item.LocalPath, item.TargetDir, delegate(int pct, string line)
                        {
                            int currentTotal = queuedTotalCount > 0 ? queuedTotalCount : 1;
                            int totalPct = ((((currentItemIdx - 1) * 100) + pct)) / currentTotal;
                            SetProgress(totalPct, string.Format("Uploading ({0}/{1}): {2} [{3}%]", currentItemIdx, currentTotal, item.ItemName, pct));
                        });
                    });

                    if (res.ExitCode == 0)
                    {
                        sessionSuccessCount++;
                    }
                    else
                    {
                        MessageBox.Show(string.Format("Failed to upload '{0}':\n{1}", item.ItemName, res.Error), "Upload Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }

                SetProgress(-1, string.Format("Successfully uploaded {0} item(s)", sessionSuccessCount));
                await LoadCurrentDirectoryAsync();
            }
            finally
            {
                if (uploadQueue.IsEmpty)
                {
                    queuedTotalCount = 0;
                    queuedProcessedCount = 0;
                    queueConflictState = ConflictResult.Overwrite;
                }

                queueSemaphore.Release();
            }
        }

        private async Task CreateNewFolderAsync()
        {
            if (string.IsNullOrEmpty(currentDeviceSerial)) return;

            string folderName = PromptInputDialog("Enter New Folder Name:", "New Folder", "NewFolder");
            if (!string.IsNullOrEmpty(folderName))
            {
                string newRemote = AdbService.CombinePath(currentPath, folderName);
                statusLabelInfo.Text = "Creating folder: " + folderName;

                string serial = currentDeviceSerial;
                var res = await Task.Run(() => adbService.CreateFolder(serial, newRemote));
                if (res.ExitCode != 0)
                {
                    MessageBox.Show(string.Format("Failed to create folder:\n{0}", res.Error), "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                await LoadCurrentDirectoryAsync();
            }
        }

        private async Task DeleteSelectedAsync()
        {
            if (lstFiles.SelectedItems.Count == 0) return;

            var confirm = MessageBox.Show(string.Format("Are you sure you want to delete {0} selected item(s) from the ADB device?", lstFiles.SelectedItems.Count),
                                          "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

            if (confirm == DialogResult.Yes)
            {
                foreach (ListViewItem lvi in lstFiles.SelectedItems)
                {
                    var info = lvi.Tag as AdbFileInfo;
                    if (info == null) continue;

                    statusLabelInfo.Text = "Deleting: " + info.Name + "...";
                    string serial = currentDeviceSerial;
                    string remote = info.FullPath;

                    var res = await Task.Run(() => adbService.DeleteItem(serial, remote));
                    if (res.ExitCode != 0)
                    {
                        MessageBox.Show(string.Format("Failed to delete '{0}':\n{1}", info.Name, res.Error), "Delete Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                await LoadCurrentDirectoryAsync();
            }
        }

        private async Task RenameSelectedAsync()
        {
            if (lstFiles.SelectedItems.Count == 0) return;
            var item = lstFiles.SelectedItems[0].Tag as AdbFileInfo;
            if (item == null) return;

            string newName = PromptInputDialog("Enter new name:", "Rename Item", item.Name);
            if (!string.IsNullOrEmpty(newName) && newName != item.Name)
            {
                string oldPath = item.FullPath;
                string newPath = AdbService.CombinePath(currentPath, newName);

                string serial = currentDeviceSerial;
                var res = await Task.Run(() => adbService.RenameItem(serial, oldPath, newPath));
                if (res.ExitCode != 0)
                {
                    MessageBox.Show(string.Format("Failed to rename:\n{0}", res.Error), "Rename Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                await LoadCurrentDirectoryAsync();
            }
        }

        private void CopySelectedPathToClipboard()
        {
            if (lstFiles.SelectedItems.Count == 0) return;
            var item = lstFiles.SelectedItems[0].Tag as AdbFileInfo;
            if (item != null)
            {
                Clipboard.SetText(item.FullPath);
                statusLabelInfo.Text = "Copied to clipboard: " + item.FullPath;
            }
        }

        private void CopySelectedToClipboard(bool isCut)
        {
            if (lstFiles.SelectedItems.Count == 0) return;

            var selectedItems = new List<AdbFileInfo>();
            foreach (ListViewItem item in lstFiles.SelectedItems)
            {
                var info = item.Tag as AdbFileInfo;
                if (info != null)
                {
                    selectedItems.Add(info);
                }
            }

            if (selectedItems.Count == 0) return;

            internalClipboard = new AdbClipboardData
            {
                Items = selectedItems,
                IsCut = isCut
            };

            btnPaste.Enabled = true;
            if (menuPaste != null) menuPaste.Enabled = true;

            if (selectedItems.Count == 1)
            {
                try { Clipboard.SetText(selectedItems[0].FullPath); } catch { }
            }

            string actionStr = isCut ? "Cut" : "Copied";
            statusLabelInfo.Text = string.Format("{0} {1} item(s) to internal clipboard", actionStr, selectedItems.Count);
        }

        private async Task PasteClipboardAsync()
        {
            if (string.IsNullOrEmpty(currentDeviceSerial))
            {
                MessageBox.Show("Please select a valid connected ADB device first.", "No Device", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (internalClipboard == null || internalClipboard.Items == null || internalClipboard.Items.Count == 0)
            {
                MessageBox.Show("Clipboard is empty.", "Nothing to Paste", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string serial = currentDeviceSerial;
            var itemsToPaste = new List<AdbFileInfo>(internalClipboard.Items);
            bool isCut = internalClipboard.IsCut;

            int totalItems = itemsToPaste.Count;
            int successCount = 0;
            ConflictResult conflictState = ConflictResult.Overwrite;

            statusLabelInfo.Text = string.Format("Pasting {0} item(s)...", totalItems);

            for (int i = 0; i < totalItems; i++)
            {
                var info = itemsToPaste[i];
                string sourcePath = info.FullPath;
                string itemName = info.Name;

                string destPath = AdbService.CombinePath(currentPath, itemName);

                if (isCut && AdbService.NormalizePath(sourcePath) == AdbService.NormalizePath(destPath))
                {
                    continue;
                }

                if (!isCut && AdbService.NormalizePath(sourcePath) == AdbService.NormalizePath(destPath))
                {
                    string ext = Path.GetExtension(itemName);
                    string nameWithoutExt = Path.GetFileNameWithoutExtension(itemName);
                    if (info.IsDirectory)
                    {
                        itemName = itemName + "_copy";
                    }
                    else
                    {
                        itemName = string.Format("{0}_copy{1}", nameWithoutExt, ext);
                    }
                    destPath = AdbService.CombinePath(currentPath, itemName);
                }

                bool existsRemote = await Task.Run(() => adbService.FileExistsRemote(serial, destPath));
                if (existsRemote)
                {
                    if (conflictState == ConflictResult.SkipAll) continue;
                    if (conflictState != ConflictResult.OverwriteAll)
                    {
                        using (var dlg = new ConflictDialog(itemName, currentPath, true))
                        {
                            if (dlg.ShowDialog(this) != DialogResult.OK || dlg.SelectedResult == ConflictResult.Cancel)
                            {
                                break;
                            }
                            if (dlg.SelectedResult == ConflictResult.Skip) continue;
                            if (dlg.SelectedResult == ConflictResult.SkipAll)
                            {
                                conflictState = ConflictResult.SkipAll;
                                continue;
                            }
                            if (dlg.SelectedResult == ConflictResult.OverwriteAll)
                            {
                                conflictState = ConflictResult.OverwriteAll;
                            }
                        }
                    }
                }

                int itemIdx = i;
                string actionVerb = isCut ? "Moving" : "Copying";
                SetProgress((itemIdx * 100) / totalItems, string.Format("{0} ({1}/{2}): {3}...", actionVerb, itemIdx + 1, totalItems, itemName));

                var res = await Task.Run(() =>
                {
                    if (isCut)
                    {
                        return adbService.RenameItem(serial, sourcePath, destPath);
                    }
                    else
                    {
                        return adbService.CopyItem(serial, sourcePath, destPath);
                    }
                });

                if (res.ExitCode == 0)
                {
                    successCount++;
                }
                else
                {
                    MessageBox.Show(string.Format("Failed to {0} '{1}':\n{2}", actionVerb.ToLower(), itemName, res.Error), "Paste Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }

            if (isCut && successCount > 0)
            {
                internalClipboard = null;
                btnPaste.Enabled = false;
                if (menuPaste != null) menuPaste.Enabled = false;
            }

            SetProgress(-1, string.Format("Pasted {0} item(s) to {1}", successCount, currentPath));
            await LoadCurrentDirectoryAsync();
        }

        private int sortColumn = -1;
        private bool sortAscending = true;

        private void LstFiles_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (e.Column == sortColumn)
            {
                sortAscending = !sortAscending;
            }
            else
            {
                sortColumn = e.Column;
                sortAscending = true;
            }

            currentFiles.Sort((x, y) =>
            {
                int result = 0;
                switch (sortColumn)
                {
                    case 0: // Name
                        result = string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase);
                        break;
                    case 1: // Size
                        result = x.Size.CompareTo(y.Size);
                        break;
                    case 2: // Type
                        result = x.IsDirectory.CompareTo(y.IsDirectory);
                        break;
                    case 3: // Modified
                        result = string.Compare(x.ModifiedTime, y.ModifiedTime, StringComparison.OrdinalIgnoreCase);
                        break;
                    case 4: // Permissions
                        result = string.Compare(x.Permissions, y.Permissions, StringComparison.OrdinalIgnoreCase);
                        break;
                }
                return sortAscending ? result : -result;
            });

            DisplayFiles(currentFiles);
        }

        private string PromptInputDialog(string text, string caption, string defaultValue = "")
        {
            Form prompt = new Form()
            {
                Width = 420,
                Height = 170,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = caption,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false
            };
            Label textLabel = new Label() { Left = 20, Top = 15, Text = text, AutoSize = true, Font = new Font("Segoe UI", 9F) };
            TextBox textBox = new TextBox() { Left = 20, Top = 40, Width = 360, Text = defaultValue, Font = new Font("Segoe UI", 9F) };
            Button confirmation = new Button() { Text = "OK", Left = 210, Width = 80, Top = 80, DialogResult = DialogResult.OK };
            Button cancel = new Button() { Text = "Cancel", Left = 300, Width = 80, Top = 80, DialogResult = DialogResult.Cancel };
            
            prompt.Controls.Add(textBox);
            prompt.Controls.Add(confirmation);
            prompt.Controls.Add(cancel);
            prompt.Controls.Add(textLabel);
            prompt.AcceptButton = confirmation;
            prompt.CancelButton = cancel;

            return prompt.ShowDialog() == DialogResult.OK ? textBox.Text.Trim() : null;
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ADBFileManager.Scripting;

namespace ADBFileManager
{
    public class ScriptManagerForm : Form
    {
        private readonly ScriptManager scriptManager;
        private readonly string scriptsDirectory;
        private readonly Action reloadAction;

        private ListView lstScripts;
        private TextBox txtDetails;
        private Button btnReload;
        private Button btnOpenFolder;
        private Button btnClose;

        public ScriptManagerForm(ScriptManager scriptManager, string scriptsDirectory, Action reloadAction)
        {
            this.scriptManager = scriptManager;
            this.scriptsDirectory = scriptsDirectory;
            this.reloadAction = reloadAction;

            InitializeComponent();
            PopulateScriptList();
        }

        private void InitializeComponent()
        {
            this.Text = "Script Addon Manager";
            this.ClientSize = new Size(720, 460);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = new Font("Segoe UI", 9F);

            var pnlTop = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(10, 8, 10, 0)
            };

            var lblHeader = new Label
            {
                Text = "Loaded C# Script Addons",
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(10, 10)
            };
            pnlTop.Controls.Add(lblHeader);

            var splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 240
            };

            lstScripts = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true,
                MultiSelect = false
            };
            lstScripts.Columns.Add("Script Name", 180);
            lstScripts.Columns.Add("Version", 70);
            lstScripts.Columns.Add("Author", 110);
            lstScripts.Columns.Add("Status", 100);
            lstScripts.Columns.Add("File Path", 230);
            lstScripts.SelectedIndexChanged += LstScripts_SelectedIndexChanged;

            txtDetails = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                ReadOnly = true,
                Font = new Font("Consolas", 9.5F),
                BackColor = Color.FromArgb(248, 249, 250)
            };

            splitContainer.Panel1.Controls.Add(lstScripts);
            splitContainer.Panel2.Controls.Add(txtDetails);

            var pnlBottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50,
                Padding = new Padding(10)
            };

            btnReload = new Button
            {
                Text = "↻ Reload Scripts",
                Location = new Point(12, 10),
                Size = new Size(130, 30)
            };
            btnReload.Click += (s, e) =>
            {
                if (reloadAction != null)
                {
                    reloadAction();
                    PopulateScriptList();
                }
            };

            btnOpenFolder = new Button
            {
                Text = "📁 Open Scripts Directory",
                Location = new Point(148, 10),
                Size = new Size(170, 30)
            };
            btnOpenFolder.Click += (s, e) =>
            {
                try
                {
                    if (!Directory.Exists(scriptsDirectory))
                    {
                        Directory.CreateDirectory(scriptsDirectory);
                    }
                    Process.Start("explorer.exe", scriptsDirectory);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Unable to open folder: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };

            btnClose = new Button
            {
                Text = "Close",
                Location = new Point(610, 10),
                Size = new Size(95, 30),
                DialogResult = DialogResult.OK
            };

            pnlBottom.Controls.Add(btnReload);
            pnlBottom.Controls.Add(btnOpenFolder);
            pnlBottom.Controls.Add(btnClose);

            this.Controls.Add(splitContainer);
            this.Controls.Add(pnlTop);
            this.Controls.Add(pnlBottom);

            this.AcceptButton = btnClose;
        }

        private void PopulateScriptList()
        {
            lstScripts.Items.Clear();
            txtDetails.Clear();

            if (scriptManager == null || scriptManager.LoadedScripts.Count == 0)
            {
                txtDetails.Text = "No C# script addons loaded. Place '.cs' script files in the scripts directory to extend functionality.";
                return;
            }

            foreach (var info in scriptManager.LoadedScripts)
            {
                string status = info.HasErrors ? "❌ Error" : "✅ Active";
                var item = new ListViewItem(new[]
                {
                    info.ScriptName,
                    info.Version,
                    info.Author,
                    status,
                    Path.GetFileName(info.FilePath)
                });
                item.Tag = info;
                if (info.HasErrors)
                {
                    item.ForeColor = Color.Red;
                }
                lstScripts.Items.Add(item);
            }

            if (lstScripts.Items.Count > 0)
            {
                lstScripts.Items[0].Selected = true;
            }
        }

        private void LstScripts_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstScripts.SelectedItems.Count == 0)
            {
                txtDetails.Clear();
                return;
            }

            var info = lstScripts.SelectedItems[0].Tag as LoadedScriptInfo;
            if (info == null) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Script Name: " + info.ScriptName);
            sb.AppendLine("Author:      " + (string.IsNullOrEmpty(info.Author) ? "Unknown" : info.Author));
            sb.AppendLine("Version:     " + (string.IsNullOrEmpty(info.Version) ? "1.0" : info.Version));
            sb.AppendLine("File:        " + info.FilePath);
            sb.AppendLine(new string('-', 60));

            if (!string.IsNullOrEmpty(info.Description))
            {
                sb.AppendLine("Description:");
                sb.AppendLine(info.Description);
                sb.AppendLine();
            }

            if (info.HasErrors)
            {
                sb.AppendLine("COMPILATION / INITIALIZATION ERRORS:");
                foreach (var err in info.CompilationErrors)
                {
                    sb.AppendLine("  • " + err);
                }
            }
            else
            {
                sb.AppendLine("Status: Script compiled and loaded successfully.");
            }

            txtDetails.Text = sb.ToString();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace ADBFileManager
{
    public enum ApkDropChoice
    {
        Install,
        Drop,
        Cancel
    }

    public class ApkActionDialog : Form
    {
        public ApkDropChoice Choice { get; private set; }

        public ApkActionDialog(List<string> apkFileNames)
        {
            Choice = ApkDropChoice.Cancel;

            this.Text = "Android Package Detected";
            this.ClientSize = new Size(480, 220);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = new Font("Segoe UI", 9F);

            var picIcon = new PictureBox
            {
                Location = new Point(16, 18),
                Size = new Size(36, 36),
                Image = SystemIcons.Question.ToBitmap(),
                SizeMode = PictureBoxSizeMode.StretchImage
            };

            string fileListText;
            if (apkFileNames.Count == 1)
            {
                fileListText = string.Format("Android package file detected:\n• {0}", apkFileNames[0]);
            }
            else
            {
                fileListText = string.Format("{0} Android package files detected:\n• {1}", apkFileNames.Count, string.Join("\n• ", apkFileNames.ToArray()));
            }

            var lblTitle = new Label
            {
                Text = fileListText,
                Location = new Point(62, 16),
                Size = new Size(400, 60),
                ForeColor = Color.FromArgb(30, 30, 30)
            };

            var lblPrompt = new Label
            {
                Text = "Do you want to INSTALL the package(s) directly onto your connected Android device, or UPLOAD/DROP them as regular file(s)?",
                Location = new Point(62, 85),
                Size = new Size(400, 55),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(50, 50, 50)
            };

            var btnInstall = new Button
            {
                Text = "📦 Install App",
                Location = new Point(40, 155),
                Size = new Size(130, 38),
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                FlatStyle = FlatStyle.System
            };
            btnInstall.Click += (s, e) =>
            {
                Choice = ApkDropChoice.Install;
                this.DialogResult = DialogResult.OK;
            };

            var btnDrop = new Button
            {
                Text = "📂 Drop / Upload",
                Location = new Point(180, 155),
                Size = new Size(130, 38),
                Font = new Font("Segoe UI", 9F)
            };
            btnDrop.Click += (s, e) =>
            {
                Choice = ApkDropChoice.Drop;
                this.DialogResult = DialogResult.OK;
            };

            var btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(320, 155),
                Size = new Size(110, 38),
                Font = new Font("Segoe UI", 9F)
            };
            btnCancel.Click += (s, e) =>
            {
                Choice = ApkDropChoice.Cancel;
                this.DialogResult = DialogResult.Cancel;
            };

            this.Controls.Add(picIcon);
            this.Controls.Add(lblTitle);
            this.Controls.Add(lblPrompt);
            this.Controls.Add(btnInstall);
            this.Controls.Add(btnDrop);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnInstall;
            this.CancelButton = btnCancel;
        }
    }
}

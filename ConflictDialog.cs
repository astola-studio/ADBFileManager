using System;
using System.Drawing;
using System.Windows.Forms;

namespace ADBFileManager
{
    public enum ConflictResult
    {
        Overwrite,
        OverwriteAll,
        Skip,
        SkipAll,
        Cancel
    }

    public class ConflictDialog : Form
    {
        public ConflictResult SelectedResult { get; private set; }

        private Label lblMessage;
        private Label lblItemName;
        private CheckBox chkApplyToAll;
        private Button btnOverwrite;
        private Button btnSkip;
        private Button btnCancel;

        public ConflictDialog(string fileName, string targetLocation, bool isUpload)
        {
            InitializeComponent();

            string direction = isUpload ? "remote target location" : "local folder";
            lblItemName.Text = fileName;
            lblMessage.Text = string.Format("The item '{0}' already exists in the {1}:\n{2}\n\nWhat would you like to do?", fileName, direction, targetLocation);
            SelectedResult = ConflictResult.Skip;
        }

        private void InitializeComponent()
        {
            this.lblMessage = new Label();
            this.lblItemName = new Label();
            this.chkApplyToAll = new CheckBox();
            this.btnOverwrite = new Button();
            this.btnSkip = new Button();
            this.btnCancel = new Button();

            this.SuspendLayout();

            this.Text = "File Conflict Detected";
            this.ClientSize = new Size(460, 195);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = new Font("Segoe UI", 9F);

            // Icon / Header Warning Picture
            var picWarning = new PictureBox
            {
                Location = new Point(16, 18),
                Size = new Size(32, 32),
                Image = SystemIcons.Warning.ToBitmap(),
                SizeMode = PictureBoxSizeMode.StretchImage
            };

            lblMessage.Location = new Point(60, 16);
            lblMessage.Size = new Size(380, 70);
            lblMessage.ForeColor = Color.FromArgb(30, 30, 30);

            chkApplyToAll.Text = "Apply choice to all remaining conflicts in this transfer";
            chkApplyToAll.Location = new Point(60, 95);
            chkApplyToAll.Size = new Size(380, 24);
            chkApplyToAll.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

            btnOverwrite.Text = "Overwrite";
            btnOverwrite.Location = new Point(135, 140);
            btnOverwrite.Size = new Size(95, 32);
            btnOverwrite.Click += (s, e) =>
            {
                SelectedResult = chkApplyToAll.Checked ? ConflictResult.OverwriteAll : ConflictResult.Overwrite;
                this.DialogResult = DialogResult.OK;
            };

            btnSkip.Text = "Skip";
            btnSkip.Location = new Point(240, 140);
            btnSkip.Size = new Size(95, 32);
            btnSkip.Click += (s, e) =>
            {
                SelectedResult = chkApplyToAll.Checked ? ConflictResult.SkipAll : ConflictResult.Skip;
                this.DialogResult = DialogResult.OK;
            };

            btnCancel.Text = "Cancel";
            btnCancel.Location = new Point(345, 140);
            btnCancel.Size = new Size(95, 32);
            btnCancel.Click += (s, e) =>
            {
                SelectedResult = ConflictResult.Cancel;
                this.DialogResult = DialogResult.Cancel;
            };

            this.Controls.Add(picWarning);
            this.Controls.Add(lblMessage);
            this.Controls.Add(chkApplyToAll);
            this.Controls.Add(btnOverwrite);
            this.Controls.Add(btnSkip);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnOverwrite;
            this.CancelButton = btnCancel;

            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}

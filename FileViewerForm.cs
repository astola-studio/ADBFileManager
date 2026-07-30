using System;
using System.Drawing;
using System.Windows.Forms;

namespace ADBFileManager
{
    public class FileViewerForm : Form
    {
        private TextBox txtContent;
        private Label lblHeader;
        private Button btnClose;

        public FileViewerForm(string fileName, string content)
        {
            InitializeComponent();
            this.Text = "File Preview - " + fileName;
            lblHeader.Text = "Preview: " + fileName;
            txtContent.Text = content;
            txtContent.SelectionStart = 0;
            txtContent.SelectionLength = 0;
        }

        private void InitializeComponent()
        {
            this.lblHeader = new Label();
            this.txtContent = new TextBox();
            this.btnClose = new Button();

            this.SuspendLayout();

            // Header Label
            this.lblHeader.Dock = DockStyle.Top;
            this.lblHeader.Height = 35;
            this.lblHeader.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            this.lblHeader.TextAlign = ContentAlignment.MiddleLeft;
            this.lblHeader.Padding = new Padding(10, 0, 0, 0);

            // Close Button Panel at Bottom
            var pnlBottom = new Panel();
            pnlBottom.Dock = DockStyle.Bottom;
            pnlBottom.Height = 45;

            this.btnClose.Text = "Close";
            this.btnClose.DialogResult = DialogResult.OK;
            this.btnClose.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            this.btnClose.Location = new Point(590, 8);
            this.btnClose.Size = new Size(85, 30);
            this.btnClose.Font = new Font("Segoe UI", 9F);
            pnlBottom.Controls.Add(this.btnClose);

            // TextBox for file contents
            this.txtContent.Dock = DockStyle.Fill;
            this.txtContent.Multiline = true;
            this.txtContent.ScrollBars = ScrollBars.Both;
            this.txtContent.WordWrap = false;
            this.txtContent.Font = new Font("Consolas", 10F);
            this.txtContent.ReadOnly = true;
            this.txtContent.BackColor = Color.FromArgb(248, 249, 250);

            // Form Layout
            this.ClientSize = new Size(690, 500);
            this.Controls.Add(this.txtContent);
            this.Controls.Add(pnlBottom);
            this.Controls.Add(this.lblHeader);
            this.StartPosition = FormStartPosition.CenterParent;
            
            if (System.IO.File.Exists("icon.ico"))
            {
                try { this.Icon = new Icon("icon.ico"); } catch { }
            }
            this.AcceptButton = this.btnClose;

            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}

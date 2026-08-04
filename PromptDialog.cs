using System;
using System.Drawing;
using System.Windows.Forms;

namespace ADBFileManager
{
    public class PromptDialog : Form
    {
        private Label lblPrompt;
        private TextBox txtInput;
        private Button btnOk;
        private Button btnCancel;

        public string InputText { get { return txtInput.Text; } }

        public PromptDialog(string promptText, string titleText = "Input Required", string defaultValue = "")
        {
            InitializeComponent();

            this.Text = titleText;
            lblPrompt.Text = promptText;
            txtInput.Text = defaultValue ?? "";
            if (!string.IsNullOrEmpty(txtInput.Text))
            {
                txtInput.SelectAll();
            }
        }

        private void InitializeComponent()
        {
            this.lblPrompt = new Label();
            this.txtInput = new TextBox();
            this.btnOk = new Button();
            this.btnCancel = new Button();

            this.SuspendLayout();

            this.ClientSize = new Size(420, 150);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Font = new Font("Segoe UI", 9F);

            lblPrompt.Location = new Point(16, 15);
            lblPrompt.Size = new Size(388, 40);
            lblPrompt.ForeColor = Color.FromArgb(30, 30, 30);

            txtInput.Location = new Point(16, 60);
            txtInput.Size = new Size(388, 23);
            txtInput.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
            };

            btnOk.Text = "OK";
            btnOk.Location = new Point(220, 102);
            btnOk.Size = new Size(88, 30);
            btnOk.DialogResult = DialogResult.OK;

            btnCancel.Text = "Cancel";
            btnCancel.Location = new Point(316, 102);
            btnCancel.Size = new Size(88, 30);
            btnCancel.DialogResult = DialogResult.Cancel;

            this.Controls.Add(lblPrompt);
            this.Controls.Add(txtInput);
            this.Controls.Add(btnOk);
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnOk;
            this.CancelButton = btnCancel;

            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}

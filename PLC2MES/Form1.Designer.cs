using System.Windows.Forms;
using System.Drawing;

namespace PLC2MES
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        // UI controls (keep original names)
        private TextBox txtBaseUrl;
        private TextBox txtRequestTemplate;
        private TextBox txtResponseTemplate;
        private TextBox txtSuccessCriteria;
        private Button btnParseTemplates;
        private Button btnSendRequest;
        private DataGridView dgvRequestVariables;
        private DataGridView dgvResponseVariables;
        private RichTextBox rtbRequest;
        private RichTextBox rtbResponse;
        private Label lblStatus;
        private Label lblDuration;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        ///  This layout uses TableLayoutPanel so controls resize responsively.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();

            // Create controls
            txtBaseUrl = new TextBox();
            txtRequestTemplate = new TextBox();
            txtResponseTemplate = new TextBox();
            txtSuccessCriteria = new TextBox();
            btnParseTemplates = new Button();
            btnSendRequest = new Button();
            dgvRequestVariables = new DataGridView();
            dgvResponseVariables = new DataGridView();
            rtbRequest = new RichTextBox();
            rtbResponse = new RichTextBox();
            lblStatus = new Label();
            lblDuration = new Label();

            // Form
            this.SuspendLayout();
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new Size(1000, 700);
            this.Text = "PLC-MES HTTP 测试工具";

            // Main table with 2 columns
            var mainLayout = new TableLayoutPanel();
            mainLayout.Dock = DockStyle.Fill;
            mainLayout.ColumnCount = 2;
            mainLayout.RowCount = 1;
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));

            // Left layout: 3 rows (Request, Response, SuccessCriteria)
            var leftLayout = new TableLayoutPanel();
            leftLayout.Dock = DockStyle.Fill;
            leftLayout.ColumnCount = 1;
            leftLayout.RowCount = 3;
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F)); // request
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 35F)); // response
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 15F)); // criteria

            // Request template textbox
            txtRequestTemplate.Multiline = true;
            txtRequestTemplate.ScrollBars = ScrollBars.Both;
            txtRequestTemplate.Font = new Font("Consolas", 10F);
            txtRequestTemplate.Dock = DockStyle.Fill;
            txtRequestTemplate.Name = "txtRequestTemplate";

            // Response template textbox
            txtResponseTemplate.Multiline = true;
            txtResponseTemplate.ScrollBars = ScrollBars.Both;
            txtResponseTemplate.Font = new Font("Consolas", 10F);
            txtResponseTemplate.Dock = DockStyle.Fill;
            txtResponseTemplate.Name = "txtResponseTemplate";

            // Success criteria textbox
            txtSuccessCriteria.Multiline = true;
            txtSuccessCriteria.ScrollBars = ScrollBars.Horizontal;
            txtSuccessCriteria.Font = new Font("Consolas", 10F);
            txtSuccessCriteria.Dock = DockStyle.Fill;
            txtSuccessCriteria.Name = "txtSuccessCriteria";

            leftLayout.Controls.Add(txtRequestTemplate, 0, 0);
            leftLayout.Controls.Add(txtResponseTemplate, 0, 1);
            leftLayout.Controls.Add(txtSuccessCriteria, 0, 2);

            // Right layout: 4 rows (top: baseUrl+buttons, request vars, response vars, bottom: request/response preview)
            var rightLayout = new TableLayoutPanel();
            rightLayout.Dock = DockStyle.Fill;
            rightLayout.ColumnCount = 1;
            rightLayout.RowCount = 4;
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40F));
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 30F));
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));

            // Top panel (baseUrl + buttons)
            var topPanel = new FlowLayoutPanel();
            topPanel.Dock = DockStyle.Fill;
            topPanel.FlowDirection = FlowDirection.LeftToRight;
            topPanel.Padding = new Padding(4);

            var lblBase = new Label();
            lblBase.Text = "Base URL:";
            lblBase.AutoSize = true;
            lblBase.TextAlign = ContentAlignment.MiddleLeft;
            lblBase.Padding = new Padding(6, 8, 6, 4);

            txtBaseUrl.Dock = DockStyle.Fill;
            txtBaseUrl.Width = 300;
            txtBaseUrl.Name = "txtBaseUrl";
            txtBaseUrl.Text = "http://localhost:8080";

            btnParseTemplates.Text = "解析模板 (F5)";
            btnParseTemplates.AutoSize = true;
            btnParseTemplates.Name = "btnParseTemplates";

            btnSendRequest.Text = "发送请求 (F6)";
            btnSendRequest.AutoSize = true;
            btnSendRequest.Name = "btnSendRequest";

            topPanel.Controls.Add(lblBase);
            topPanel.Controls.Add(txtBaseUrl);
            topPanel.Controls.Add(btnParseTemplates);
            topPanel.Controls.Add(btnSendRequest);

            // Request variables grid
            dgvRequestVariables.Dock = DockStyle.Fill;
            dgvRequestVariables.Name = "dgvRequestVariables";
            dgvRequestVariables.AllowUserToAddRows = false;
            dgvRequestVariables.AllowUserToDeleteRows = false;

            // Response variables grid
            dgvResponseVariables.Dock = DockStyle.Fill;
            dgvResponseVariables.Name = "dgvResponseVariables";
            dgvResponseVariables.AllowUserToAddRows = false;
            dgvResponseVariables.AllowUserToDeleteRows = false;
            dgvResponseVariables.ReadOnly = true;

            // Bottom preview panel with two RichTextBoxes side by side
            var previewSplit = new TableLayoutPanel();
            previewSplit.Dock = DockStyle.Fill;
            previewSplit.ColumnCount = 2;
            previewSplit.RowCount = 1;
            previewSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            previewSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

            rtbRequest.Dock = DockStyle.Fill;
            rtbRequest.ReadOnly = true;
            rtbRequest.Font = new Font("Consolas", 9F);
            rtbRequest.Name = "rtbRequest";

            rtbResponse.Dock = DockStyle.Fill;
            rtbResponse.ReadOnly = true;
            rtbResponse.Font = new Font("Consolas", 9F);
            rtbResponse.Name = "rtbResponse";

            previewSplit.Controls.Add(rtbRequest, 0, 0);
            previewSplit.Controls.Add(rtbResponse, 1, 0);

            // Status bar (below everything) - placed inside rightLayout first row? we'll add labels under preview
            var statusPanel = new FlowLayoutPanel();
            statusPanel.Dock = DockStyle.Bottom;
            statusPanel.Height = 24;
            statusPanel.FlowDirection = FlowDirection.LeftToRight;

            lblStatus.AutoSize = true;
            lblStatus.Text = "状态: 未执行";
            lblStatus.Name = "lblStatus";

            lblDuration.AutoSize = true;
            lblDuration.Text = "耗时: -";
            lblDuration.Name = "lblDuration";

            statusPanel.Controls.Add(lblStatus);
            statusPanel.Controls.Add(lblDuration);

            // Assemble right layout
            rightLayout.Controls.Add(topPanel, 0, 0);
            rightLayout.Controls.Add(dgvRequestVariables, 0, 1);
            rightLayout.Controls.Add(dgvResponseVariables, 0, 2);
            rightLayout.Controls.Add(previewSplit, 0, 3);

            // Add left and right to main layout
            mainLayout.Controls.Add(leftLayout, 0, 0);
            mainLayout.Controls.Add(rightLayout, 1, 0);

            // Add mainLayout and statusPanel to form
            this.Controls.Add(mainLayout);
            this.Controls.Add(statusPanel);

            this.ResumeLayout(false);
            this.PerformLayout();
        }

        #endregion
    }
}

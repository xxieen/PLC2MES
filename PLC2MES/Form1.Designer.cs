namespace PLC2MES
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        // UI controls
        private System.Windows.Forms.TextBox txtBaseUrl;
        private System.Windows.Forms.TextBox txtRequestTemplate;
        private System.Windows.Forms.TextBox txtResponseTemplate;
        private System.Windows.Forms.TextBox txtSuccessCriteria;
        private System.Windows.Forms.Button btnParseTemplates;
        private System.Windows.Forms.Button btnSendRequest;
        private System.Windows.Forms.DataGridView dgvRequestVariables;
        private System.Windows.Forms.DataGridView dgvResponseVariables;
        private System.Windows.Forms.RichTextBox rtbRequest;
        private System.Windows.Forms.RichTextBox rtbResponse;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Label lblDuration;

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
        /// </summary>
        private void InitializeComponent()
        {
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
            ((System.ComponentModel.ISupportInitialize)dgvRequestVariables).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dgvResponseVariables).BeginInit();
            SuspendLayout();
            // 
            // txtBaseUrl
            // 
            txtBaseUrl.Location = new Point(12, 12);
            txtBaseUrl.Name = "txtBaseUrl";
            txtBaseUrl.Size = new Size(600, 30);
            txtBaseUrl.TabIndex = 0;
            txtBaseUrl.Text = "http://localhost:8080";
            // 
            // txtRequestTemplate
            // 
            txtRequestTemplate.Font = new Font("Consolas", 10F);
            txtRequestTemplate.Location = new Point(12, 45);
            txtRequestTemplate.Multiline = true;
            txtRequestTemplate.Name = "txtRequestTemplate";
            txtRequestTemplate.ScrollBars = ScrollBars.Both;
            txtRequestTemplate.Size = new Size(480, 240);
            txtRequestTemplate.TabIndex = 3;
            // 
            // txtResponseTemplate
            // 
            txtResponseTemplate.Font = new Font("Consolas", 10F);
            txtResponseTemplate.Location = new Point(12, 295);
            txtResponseTemplate.Multiline = true;
            txtResponseTemplate.Name = "txtResponseTemplate";
            txtResponseTemplate.ScrollBars = ScrollBars.Both;
            txtResponseTemplate.Size = new Size(480, 200);
            txtResponseTemplate.TabIndex = 4;
            // 
            // txtSuccessCriteria
            // 
            txtSuccessCriteria.Font = new Font("Consolas", 10F);
            txtSuccessCriteria.Location = new Point(12, 505);
            txtSuccessCriteria.Multiline = true;
            txtSuccessCriteria.Name = "txtSuccessCriteria";
            txtSuccessCriteria.ScrollBars = ScrollBars.Horizontal;
            txtSuccessCriteria.Size = new Size(480, 80);
            txtSuccessCriteria.TabIndex = 5;
            // 
            // btnParseTemplates
            // 
            btnParseTemplates.Location = new Point(620, 10);
            btnParseTemplates.Name = "btnParseTemplates";
            btnParseTemplates.Size = new Size(124, 32);
            btnParseTemplates.TabIndex = 1;
            btnParseTemplates.Text = "解析模板 (F5)";
            // 
            // btnSendRequest
            // 
            btnSendRequest.Location = new Point(750, 10);
            btnSendRequest.Name = "btnSendRequest";
            btnSendRequest.Size = new Size(124, 32);
            btnSendRequest.TabIndex = 2;
            btnSendRequest.Text = "发送请求 (F6)";
            // 
            // dgvRequestVariables
            // 
            dgvRequestVariables.AllowUserToAddRows = false;
            dgvRequestVariables.AllowUserToDeleteRows = false;
            dgvRequestVariables.ColumnHeadersHeight = 34;
            dgvRequestVariables.Location = new Point(500, 45);
            dgvRequestVariables.Name = "dgvRequestVariables";
            dgvRequestVariables.RowHeadersWidth = 62;
            dgvRequestVariables.Size = new Size(480, 220);
            dgvRequestVariables.TabIndex = 6;
            // 
            // dgvResponseVariables
            // 
            dgvResponseVariables.AllowUserToAddRows = false;
            dgvResponseVariables.AllowUserToDeleteRows = false;
            dgvResponseVariables.ColumnHeadersHeight = 34;
            dgvResponseVariables.Location = new Point(500, 295);
            dgvResponseVariables.Name = "dgvResponseVariables";
            dgvResponseVariables.ReadOnly = true;
            dgvResponseVariables.RowHeadersWidth = 62;
            dgvResponseVariables.Size = new Size(480, 200);
            dgvResponseVariables.TabIndex = 7;
            // 
            // rtbRequest
            // 
            rtbRequest.Location = new Point(12, 595);
            rtbRequest.Name = "rtbRequest";
            rtbRequest.ReadOnly = true;
            rtbRequest.Size = new Size(480, 90);
            rtbRequest.TabIndex = 8;
            rtbRequest.Text = "";
            // 
            // rtbResponse
            // 
            rtbResponse.Location = new Point(500, 505);
            rtbResponse.Name = "rtbResponse";
            rtbResponse.ReadOnly = true;
            rtbResponse.Size = new Size(480, 180);
            rtbResponse.TabIndex = 9;
            rtbResponse.Text = "";
            // 
            // lblStatus
            // 
            lblStatus.Location = new Point(12, 688);
            lblStatus.Name = "lblStatus";
            lblStatus.Size = new Size(400, 20);
            lblStatus.TabIndex = 10;
            lblStatus.Text = "状态: 未执行";
            // 
            // lblDuration
            // 
            lblDuration.Location = new Point(420, 688);
            lblDuration.Name = "lblDuration";
            lblDuration.Size = new Size(200, 20);
            lblDuration.TabIndex = 11;
            lblDuration.Text = "耗时: -";
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(11F, 24F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1000, 700);
            Controls.Add(txtBaseUrl);
            Controls.Add(btnParseTemplates);
            Controls.Add(btnSendRequest);
            Controls.Add(txtRequestTemplate);
            Controls.Add(txtResponseTemplate);
            Controls.Add(txtSuccessCriteria);
            Controls.Add(dgvRequestVariables);
            Controls.Add(dgvResponseVariables);
            Controls.Add(rtbRequest);
            Controls.Add(rtbResponse);
            Controls.Add(lblStatus);
            Controls.Add(lblDuration);
            Name = "Form1";
            Text = "PLC-MES HTTP 测试工具";
            ((System.ComponentModel.ISupportInitialize)dgvRequestVariables).EndInit();
            ((System.ComponentModel.ISupportInitialize)dgvResponseVariables).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
    }
}

namespace fileVault
{
    partial class Client
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
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
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            lblClient = new Label();
            dgvFiles = new DataGridView();
            dgvAccessLog = new DataGridView();
            lblAccessLogTable = new Label();
            lblUsersTable = new Label();
            btnUpload = new Button();
            btnDownload = new Button();
            btnShare = new Button();
            btnRefresh = new Button();
            btnDelete = new Button();
            btnUnshare = new Button();
            lblTimeStamp = new Label();
            ((System.ComponentModel.ISupportInitialize)dgvFiles).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dgvAccessLog).BeginInit();
            SuspendLayout();
            // 
            // lblClient
            // 
            lblClient.AutoSize = true;
            lblClient.Font = new Font("Verdana", 14.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblClient.ForeColor = Color.White;
            lblClient.Location = new Point(12, 9);
            lblClient.Name = "lblClient";
            lblClient.Size = new Size(0, 23);
            lblClient.TabIndex = 0;
            // 
            // dgvFiles
            // 
            dgvFiles.BackgroundColor = Color.DarkGray;
            dgvFiles.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvFiles.Location = new Point(12, 115);
            dgvFiles.Name = "dgvFiles";
            dgvFiles.Size = new Size(776, 197);
            dgvFiles.TabIndex = 6;
            // 
            // dgvAccessLog
            // 
            dgvAccessLog.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvAccessLog.Location = new Point(12, 343);
            dgvAccessLog.Name = "dgvAccessLog";
            dgvAccessLog.Size = new Size(776, 95);
            dgvAccessLog.TabIndex = 9;
            // 
            // lblAccessLogTable
            // 
            lblAccessLogTable.AutoSize = true;
            lblAccessLogTable.Font = new Font("Verdana", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblAccessLogTable.ForeColor = Color.White;
            lblAccessLogTable.Location = new Point(12, 322);
            lblAccessLogTable.Name = "lblAccessLogTable";
            lblAccessLogTable.Size = new Size(98, 18);
            lblAccessLogTable.TabIndex = 10;
            lblAccessLogTable.Text = "Access Log";
            // 
            // lblUsersTable
            // 
            lblUsersTable.AutoSize = true;
            lblUsersTable.Font = new Font("Verdana", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblUsersTable.ForeColor = Color.White;
            lblUsersTable.Location = new Point(12, 94);
            lblUsersTable.Name = "lblUsersTable";
            lblUsersTable.Size = new Size(54, 18);
            lblUsersTable.TabIndex = 11;
            lblUsersTable.Text = "Users";
            // 
            // btnUpload
            // 
            btnUpload.BackColor = Color.FromArgb(230, 210, 160);
            btnUpload.Font = new Font("Verdana", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnUpload.ForeColor = Color.White;
            btnUpload.Location = new Point(16, 43);
            btnUpload.Name = "btnUpload";
            btnUpload.Size = new Size(94, 38);
            btnUpload.TabIndex = 12;
            btnUpload.Text = "Upload";
            btnUpload.UseVisualStyleBackColor = false;
            btnUpload.Click += btnUpload_Click;
            // 
            // btnDownload
            // 
            btnDownload.BackColor = Color.FromArgb(230, 210, 160);
            btnDownload.Font = new Font("Verdana", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnDownload.ForeColor = Color.White;
            btnDownload.Location = new Point(142, 43);
            btnDownload.Name = "btnDownload";
            btnDownload.Size = new Size(94, 38);
            btnDownload.TabIndex = 13;
            btnDownload.Text = "Download";
            btnDownload.UseVisualStyleBackColor = false;
            btnDownload.Click += btnDownload_Click;
            // 
            // btnShare
            // 
            btnShare.BackColor = Color.FromArgb(230, 210, 160);
            btnShare.Font = new Font("Verdana", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnShare.ForeColor = Color.White;
            btnShare.Location = new Point(272, 43);
            btnShare.Name = "btnShare";
            btnShare.Size = new Size(94, 38);
            btnShare.TabIndex = 14;
            btnShare.Text = "Share";
            btnShare.UseVisualStyleBackColor = false;
            btnShare.Click += btnShare_Click;
            // 
            // btnRefresh
            // 
            btnRefresh.BackColor = Color.FromArgb(230, 210, 160);
            btnRefresh.Font = new Font("Verdana", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnRefresh.ForeColor = Color.White;
            btnRefresh.Location = new Point(664, 43);
            btnRefresh.Name = "btnRefresh";
            btnRefresh.Size = new Size(94, 38);
            btnRefresh.TabIndex = 15;
            btnRefresh.Text = "Refresh";
            btnRefresh.UseVisualStyleBackColor = false;
            btnRefresh.Click += btnRefresh_Click;
            // 
            // btnDelete
            // 
            btnDelete.BackColor = Color.FromArgb(230, 210, 160);
            btnDelete.Font = new Font("Verdana", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnDelete.ForeColor = Color.White;
            btnDelete.Location = new Point(533, 43);
            btnDelete.Name = "btnDelete";
            btnDelete.Size = new Size(94, 38);
            btnDelete.TabIndex = 16;
            btnDelete.Text = "Delete";
            btnDelete.UseVisualStyleBackColor = false;
            btnDelete.Click += btnDelete_Click;
            // 
            // btnUnshare
            // 
            btnUnshare.BackColor = Color.FromArgb(230, 210, 160);
            btnUnshare.Font = new Font("Verdana", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnUnshare.ForeColor = Color.White;
            btnUnshare.Location = new Point(402, 43);
            btnUnshare.Name = "btnUnshare";
            btnUnshare.Size = new Size(94, 38);
            btnUnshare.TabIndex = 17;
            btnUnshare.Text = "Unshare";
            btnUnshare.UseVisualStyleBackColor = false;
            btnUnshare.Click += btnUnshare_Click;
            // 
            // lblTimeStamp
            // 
            lblTimeStamp.AutoSize = true;
            lblTimeStamp.Font = new Font("Verdana", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblTimeStamp.ForeColor = Color.White;
            lblTimeStamp.Location = new Point(627, 13);
            lblTimeStamp.Name = "lblTimeStamp";
            lblTimeStamp.Size = new Size(0, 18);
            lblTimeStamp.TabIndex = 18;
            // 
            // Client
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(43, 87, 72);
            ClientSize = new Size(800, 450);
            Controls.Add(lblTimeStamp);
            Controls.Add(btnUnshare);
            Controls.Add(btnDelete);
            Controls.Add(btnRefresh);
            Controls.Add(btnShare);
            Controls.Add(btnDownload);
            Controls.Add(btnUpload);
            Controls.Add(lblUsersTable);
            Controls.Add(lblAccessLogTable);
            Controls.Add(dgvAccessLog);
            Controls.Add(dgvFiles);
            Controls.Add(lblClient);
            Name = "Client";
            Text = "Form1";
            ((System.ComponentModel.ISupportInitialize)dgvFiles).EndInit();
            ((System.ComponentModel.ISupportInitialize)dgvAccessLog).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label lblClient;
        private DataGridView dgvFiles;
        private DataGridView dgvAccessLog;
        private Label lblAccessLogTable;
        private Label lblUsersTable;
        private Button btnUpload;
        private Button btnDownload;
        private Button btnShare;
        private Button btnRefresh;
        private Button btnDelete;
        private Button btnUnshare;
        private Label lblTimeStamp;
    }
}
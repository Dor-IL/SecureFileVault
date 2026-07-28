namespace fileVault
{
    partial class Admin
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
            ProjectName = new Label();
            btnLock = new Button();
            btnDelete = new Button();
            btnUnlock = new Button();
            btnViewFiles = new Button();
            dgvData = new DataGridView();
            btnRefresh = new Button();
            lblDataTable = new Label();
            dgvAccessLog = new DataGridView();
            lblAccessLogTable = new Label();
            cmbAccessLogUser = new ComboBox(); //
            btnBack = new Button();
            ((System.ComponentModel.ISupportInitialize)dgvData).BeginInit();
            ((System.ComponentModel.ISupportInitialize)dgvAccessLog).BeginInit();
            SuspendLayout();
            // 
            // ProjectName
            // 
            ProjectName.AutoSize = true;
            ProjectName.Font = new Font("Verdana", 14.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            ProjectName.ForeColor = Color.White;
            ProjectName.Location = new Point(24, 9);
            ProjectName.Name = "ProjectName";
            ProjectName.Size = new Size(260, 23);
            ProjectName.TabIndex = 0;
            ProjectName.Text = "Secure files vault - Admin";
            // 
            // btnLock
            // 
            btnLock.BackColor = Color.FromArgb(212, 160, 66);
            btnLock.Font = new Font("Verdana", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnLock.ForeColor = Color.White;
            btnLock.Location = new Point(411, 48);
            btnLock.Name = "btnLock";
            btnLock.Size = new Size(94, 38);
            btnLock.TabIndex = 1;
            btnLock.Text = "Lock";
            btnLock.UseVisualStyleBackColor = false;
            btnLock.Click += btnLock_Click;
            // 
            // btnDelete
            // 
            btnDelete.BackColor = Color.FromArgb(212, 160, 66);
            btnDelete.Font = new Font("Verdana", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnDelete.ForeColor = Color.White;
            btnDelete.Location = new Point(24, 48);
            btnDelete.Name = "btnDelete";
            btnDelete.Size = new Size(94, 38);
            btnDelete.TabIndex = 2;
            btnDelete.Text = "Delete";
            btnDelete.UseVisualStyleBackColor = false;
            btnDelete.Click += btnDelete_Click;
            // 
            // btnUnlock
            // 
            btnUnlock.BackColor = Color.FromArgb(212, 160, 66);
            btnUnlock.Font = new Font("Verdana", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnUnlock.ForeColor = Color.White;
            btnUnlock.Location = new Point(538, 48);
            btnUnlock.Name = "btnUnlock";
            btnUnlock.Size = new Size(94, 38);
            btnUnlock.TabIndex = 3;
            btnUnlock.Text = "Unlock";
            btnUnlock.UseVisualStyleBackColor = false;
            btnUnlock.Click += btnUnlock_Click;
            // 
            // btnViewFiles
            // 
            btnViewFiles.BackColor = Color.FromArgb(212, 160, 66);
            btnViewFiles.Font = new Font("Verdana", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnViewFiles.ForeColor = Color.White;
            btnViewFiles.Location = new Point(148, 48);
            btnViewFiles.Name = "btnViewFiles";
            btnViewFiles.Size = new Size(98, 38);
            btnViewFiles.TabIndex = 4;
            btnViewFiles.Text = "ViewFiles";
            btnViewFiles.UseVisualStyleBackColor = false;
            btnViewFiles.Click += btnViewFiles_Click;
            // 
            // dgvData
            // 
            dgvData.BackgroundColor = Color.DarkGray;
            dgvData.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvData.Location = new Point(12, 113);
            dgvData.Name = "dgvData";
            dgvData.Size = new Size(776, 197);
            dgvData.TabIndex = 5;
            // 
            // btnRefresh
            // 
            btnRefresh.BackColor = Color.FromArgb(212, 160, 66);
            btnRefresh.Font = new Font("Verdana", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnRefresh.ForeColor = Color.White;
            btnRefresh.Location = new Point(280, 48);
            btnRefresh.Name = "btnRefresh";
            btnRefresh.Size = new Size(98, 38);
            btnRefresh.TabIndex = 6;
            btnRefresh.Text = "Refresh";
            btnRefresh.UseVisualStyleBackColor = false;
            btnRefresh.Click += btnRefresh_Click;
            // 
            // lblDataTable
            // 
            lblDataTable.AutoSize = true;
            lblDataTable.Font = new Font("Verdana", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblDataTable.ForeColor = Color.White;
            lblDataTable.Location = new Point(12, 92);
            lblDataTable.Name = "lblDataTable";
            lblDataTable.Size = new Size(54, 18);
            lblDataTable.TabIndex = 7;
            lblDataTable.Text = "Users";
            // 
            // dgvAccessLog
            // 
            dgvAccessLog.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dgvAccessLog.Location = new Point(12, 343);
            dgvAccessLog.Name = "dgvAccessLog";
            dgvAccessLog.Size = new Size(776, 95);
            dgvAccessLog.TabIndex = 8;
            // 
            // lblAccessLogTable
            // 
            lblAccessLogTable.AutoSize = true;
            lblAccessLogTable.Font = new Font("Verdana", 12F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblAccessLogTable.ForeColor = Color.White;
            lblAccessLogTable.Location = new Point(12, 322);
            lblAccessLogTable.Name = "lblAccessLogTable";
            lblAccessLogTable.Size = new Size(98, 18);
            lblAccessLogTable.TabIndex = 9;
            lblAccessLogTable.Text = "Access Log";
            //
            // cmbAccessLogUser //
            //
            cmbAccessLogUser.DropDownStyle = ComboBoxStyle.DropDownList; //
            cmbAccessLogUser.Font = new Font("Verdana", 9F, FontStyle.Regular, GraphicsUnit.Point, 0); //
            cmbAccessLogUser.FormattingEnabled = true; //
            cmbAccessLogUser.Location = new Point(608, 319); //
            cmbAccessLogUser.Name = "cmbAccessLogUser"; //
            cmbAccessLogUser.Size = new Size(180, 23); //
            cmbAccessLogUser.TabIndex = 11; //
            cmbAccessLogUser.SelectedIndexChanged += cmbAccessLogUser_SelectedIndexChanged; //
            //
            // btnBack
            // 
            btnBack.BackColor = Color.FromArgb(212, 160, 66);
            btnBack.Font = new Font("Verdana", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnBack.ForeColor = Color.White;
            btnBack.Location = new Point(148, 48);
            btnBack.Name = "btnBack";
            btnBack.Size = new Size(98, 38);
            btnBack.TabIndex = 10;
            btnBack.Text = "Back";
            btnBack.UseVisualStyleBackColor = false;
            btnBack.Visible = false;
            btnBack.Click += btnBack_Click;
            // 
            // Admin
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.FromArgb(110, 26, 55);
            ClientSize = new Size(800, 450);
            Controls.Add(lblAccessLogTable);
            Controls.Add(cmbAccessLogUser); //
            Controls.Add(dgvAccessLog);
            Controls.Add(lblDataTable);
            Controls.Add(btnRefresh);
            Controls.Add(dgvData);
            Controls.Add(btnViewFiles);
            Controls.Add(btnUnlock);
            Controls.Add(btnDelete);
            Controls.Add(btnLock);
            Controls.Add(ProjectName);
            Controls.Add(btnBack);
            Name = "Admin";
            Text = "Form1";
            ((System.ComponentModel.ISupportInitialize)dgvData).EndInit();
            ((System.ComponentModel.ISupportInitialize)dgvAccessLog).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private Label ProjectName;
        private Button btnLock;
        private Button btnDelete;
        private Button btnUnlock;
        private Button btnViewFiles;
        private DataGridView dgvData;
        private Button btnRefresh;
        private Label lblDataTable;
        private DataGridView dgvAccessLog;
        private Label lblAccessLogTable;
        private ComboBox cmbAccessLogUser; //
        private Button btnBack;
    }
}
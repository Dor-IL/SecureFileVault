namespace fileVault
{
    partial class LoginRegister
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

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
            panel1 = new Panel();
            lblRegister = new Label();
            btnAdmin = new Button();
            lblError = new Label();
            ProjectName = new Label();
            btnRegister = new Button();
            btnLogin = new Button();
            lblPassword = new Label();
            lblName = new Label();
            txtPassword = new TextBox();
            txtName = new TextBox();
            panel1.SuspendLayout();
            SuspendLayout();
            // 
            // panel1
            // 
            panel1.BackColor = Color.FromArgb(64, 64, 64);
            panel1.Controls.Add(lblRegister);
            panel1.Controls.Add(btnAdmin);
            panel1.Controls.Add(lblError);
            panel1.Controls.Add(ProjectName);
            panel1.Controls.Add(btnRegister);
            panel1.Controls.Add(btnLogin);
            panel1.Controls.Add(lblPassword);
            panel1.Controls.Add(lblName);
            panel1.Controls.Add(txtPassword);
            panel1.Controls.Add(txtName);
            panel1.Location = new Point(252, 54);
            panel1.Name = "panel1";
            panel1.Size = new Size(295, 314);
            panel1.TabIndex = 0;
            // 
            // lblRegister
            // 
            lblRegister.AutoSize = true;
            lblRegister.BackColor = Color.FromArgb(128, 255, 128);
            lblRegister.Font = new Font("Verdana", 6.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblRegister.Location = new Point(44, 247);
            lblRegister.Name = "lblRegister";
            lblRegister.Size = new Size(0, 12);
            lblRegister.TabIndex = 8;
            // 
            // btnAdmin
            // 
            btnAdmin.Font = new Font("Verdana", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnAdmin.Location = new Point(44, 221);
            btnAdmin.Name = "btnAdmin";
            btnAdmin.Size = new Size(208, 23);
            btnAdmin.TabIndex = 1;
            btnAdmin.Text = "Admin";
            btnAdmin.UseVisualStyleBackColor = true;
            btnAdmin.Click += btnAdmin_Click;
            // 
            // lblError
            //
            lblError.AutoSize = true;
            lblError.BackColor = Color.FromArgb(255, 128, 128);
            lblError.Font = new Font("Verdana", 6.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblError.Location = new Point(44, 247);
            lblError.MaximumSize = new Size(208, 0);
            lblError.Name = "lblError";
            lblError.Size = new Size(0, 12);
            lblError.TabIndex = 7;
            // 
            // ProjectName
            // 
            ProjectName.AutoSize = true;
            ProjectName.Font = new Font("Verdana", 14.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            ProjectName.ForeColor = Color.White;
            ProjectName.Location = new Point(56, 14);
            ProjectName.Name = "ProjectName";
            ProjectName.Size = new Size(166, 23);
            ProjectName.TabIndex = 6;
            ProjectName.Text = "Secure file vault";
            // 
            // btnRegister
            // 
            btnRegister.Font = new Font("Verdana", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnRegister.ForeColor = Color.Black;
            btnRegister.Location = new Point(152, 191);
            btnRegister.Name = "btnRegister";
            btnRegister.Size = new Size(100, 24);
            btnRegister.TabIndex = 5;
            btnRegister.Text = "Register";
            btnRegister.UseVisualStyleBackColor = true;
            btnRegister.Click += btnRegister_Click;
            // 
            // btnLogin
            // 
            btnLogin.Font = new Font("Verdana", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 0);
            btnLogin.ForeColor = Color.Black;
            btnLogin.Location = new Point(44, 191);
            btnLogin.Name = "btnLogin";
            btnLogin.Size = new Size(102, 24);
            btnLogin.TabIndex = 4;
            btnLogin.Text = "Login";
            btnLogin.UseVisualStyleBackColor = true;
            btnLogin.Click += btnLogin_Click;
            // 
            // lblPassword
            // 
            lblPassword.AutoSize = true;
            lblPassword.Font = new Font("Verdana", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblPassword.ForeColor = Color.White;
            lblPassword.Location = new Point(44, 127);
            lblPassword.Name = "lblPassword";
            lblPassword.Size = new Size(80, 18);
            lblPassword.TabIndex = 3;
            lblPassword.Text = "Password";
            // 
            // lblName
            // 
            lblName.AutoSize = true;
            lblName.Font = new Font("Verdana", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 0);
            lblName.ForeColor = Color.White;
            lblName.Location = new Point(44, 67);
            lblName.Name = "lblName";
            lblName.Size = new Size(52, 18);
            lblName.TabIndex = 2;
            lblName.Text = "Name";
            // 
            // txtPassword
            // 
            txtPassword.Location = new Point(44, 148);
            txtPassword.MaxLength = UserService.MaxPasswordLength;
            txtPassword.Name = "txtPassword";
            txtPassword.Size = new Size(208, 23);
            txtPassword.TabIndex = 1;
            //
            // txtName
            //
            txtName.Location = new Point(44, 88);
            txtName.MaxLength = UserService.MaxUsernameLength;
            txtName.Name = "txtName";
            txtName.Size = new Size(208, 23);
            txtName.TabIndex = 0;
            // 
            // LoginRegister
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            BackColor = Color.Black;
            ClientSize = new Size(800, 450);
            Controls.Add(panel1);
            Name = "LoginRegister";
            Text = "Form1";
            panel1.ResumeLayout(false);
            panel1.PerformLayout();
            ResumeLayout(false);
        }

        #endregion

        private Panel panel1;
        private TextBox txtPassword;
        private TextBox txtName;
        private Label lblPassword;
        private Label lblName;
        private Label ProjectName;
        private Button btnRegister;
        private Button btnLogin;
        private Label lblError;
        private Button btnAdmin;
        private Label lblRegister;
    }
}

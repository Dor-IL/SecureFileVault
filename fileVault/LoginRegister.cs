namespace fileVault
{
    public partial class LoginRegister : Form
    {
        private VaultClient _vaultClient;

        const string DatabaseFileName = "database.db";

        public static readonly string ConnectionString =
           $"Data Source={Path.Combine(Application.StartupPath, DatabaseFileName)};Version=3;Pooling=True;";

        public LoginRegister()
        {
            InitializeComponent();

            try
            {
                Db.Initialize(ConnectionString, DatabaseFileName);
                var server = new VaultServer(9000);
                server.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Startup error: {ex.Message}");
            }
        }

        private async Task EnsureConnectedAsync()
        {
            if (_vaultClient == null)
            {
                _vaultClient = new VaultClient();
                await _vaultClient.ConnectAsync("127.0.0.1", 9000);
            }
        }

        private async void btnLogin_Click(object sender, EventArgs e)
        {
            btnLogin.Enabled = false;
            btnRegister.Enabled = false;

            await EnsureConnectedAsync();

            string username = txtName.Text.Trim();
            string password = txtPassword.Text.Trim();

            var (success, message) = await _vaultClient.LoginAsync(username, password);

            btnLogin.Enabled = true;
            btnRegister.Enabled = true;

            if (success)
            {
                int userId = int.Parse(message);

                Client clientForm = new Client(userId, username, _vaultClient);
                clientForm.FormClosed += (s, args) =>
                {
                    if (clientForm.AccountWasDeleted || clientForm.AccountWasLocked)
                    {
                        txtName.Clear();
                        txtPassword.Clear();
                        lblRegister.Text = null;
                        lblError.Text = null;
                        this.Show();
                    }
                    else
                    {
                        this.Close();
                    }
                };
                clientForm.Show();
                this.Hide();
            }
            else
            {
                lblRegister.Text = null;
                lblError.Text = message;
            }
        }

        private async void btnRegister_Click(object sender, EventArgs e)
        {
            lblRegister.Text = null;
            lblError.Text = null;

            string username = txtName.Text.Trim();
            string password = txtPassword.Text.Trim();

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                lblError.Text = "Username and password cannot be empty.";
                return;
            }

            btnLogin.Enabled = false;
            btnRegister.Enabled = false;

            await EnsureConnectedAsync();

            var (success, message) = await _vaultClient.RegisterAsync(username, password);

            btnLogin.Enabled = true;
            btnRegister.Enabled = true;

            if (success)
                lblRegister.Text = message;
            else
                lblError.Text = message;
        }

        private void btnAdmin_Click(object sender, EventArgs e)
        {
            Admin adminForm = new Admin();
            adminForm.FormClosed += (s, args) => this.Close();
            adminForm.Show();
            this.Hide();
        }
    }
}

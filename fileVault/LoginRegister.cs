namespace fileVault
{
    public partial class LoginRegister : Form
    {
        private VaultClient _vaultClient;

        const string DatabaseFileName = "database.db";

        // Perf: build the connection string once instead of re-running Path.Combine/string-interpolation on
        // every call (this is read on nearly every DB operation in the app). Pooling=True lets the many
        // short-lived SQLiteConnections used throughout the app reuse an underlying connection instead of
        // paying file-open overhead each time.
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
            await EnsureConnectedAsync();

            string username = txtName.Text.Trim();
            string password = txtPassword.Text.Trim();

            var (success, message) = await _vaultClient.LoginAsync(username, password);

            if (success)
            {
                int userId = int.Parse(message);

                Client clientForm = new Client(userId, username, _vaultClient);
                clientForm.FormClosed += (s, args) => this.Close();
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

            await EnsureConnectedAsync();

            var (success, message) = await _vaultClient.RegisterAsync(username, password);
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

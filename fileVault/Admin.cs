using System.Data;
using System.Data.SQLite;

namespace fileVault
{
    public partial class Admin : Form
    {
        private List<(DataGridView Grid, string Sql)> tables;

        private bool viewingUserFiles = false;
        private object selectedUserId = null;

        public Admin()
        {
            InitializeComponent();

            StyleButton(btnDelete);
            StyleButton(btnLock);
            StyleButton(btnUnlock);
            StyleButton(btnViewFiles);
            StyleButton(btnBack);
            StyleButton(btnRefresh);

            StyleGrid(dgvData);
            StyleGrid(dgvAccessLog, false);

            tables = new List<(DataGridView, string)>
            {
                (dgvData,       "SELECT * FROM users"),
            };

            PopulateAccessLogUserFilter();

            LoadAllTables();

            dgvData.SelectionChanged += (s, e) => UpdateLockButtonStates();
        }

        private void StyleButton(Button btn)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderColor = Color.FromArgb(212, 160, 66);
            btn.FlatAppearance.BorderSize = 1;
        }

        private void StyleGrid(DataGridView grid, bool allowHighlight = true)
        {
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.RowHeadersVisible = false;
            grid.AllowUserToResizeColumns = false;
            grid.AllowUserToResizeRows = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;

            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;

            Color normalBack = Color.FromArgb(110, 26, 55);
            Color normalFore = Color.White;

            Color selectedBack = allowHighlight ? Color.FromArgb(160, 50, 85) : normalBack;
            Color selectedFore = Color.White;

            grid.DefaultCellStyle.SelectionBackColor = selectedBack;
            grid.DefaultCellStyle.SelectionForeColor = selectedFore;
            grid.DefaultCellStyle.BackColor = normalBack;
            grid.DefaultCellStyle.ForeColor = normalFore;

            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = normalBack;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = normalFore;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font(grid.Font, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = normalBack;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = normalFore;

            grid.GridColor = Color.FromArgb(75, 16, 38);
            grid.BackgroundColor = Color.FromArgb(75, 16, 38);

            if (!allowHighlight)
            {
                grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            }
        }

        private void LoadTableIntoGrid(string sql, DataGridView grid, params SQLiteParameter[] parameters)
        {
            using var connection = new SQLiteConnection(LoginRegister.ConnectionString);
            using var command = new SQLiteCommand(sql, connection);
            if (parameters != null) command.Parameters.AddRange(parameters);

            using var adapter = new SQLiteDataAdapter(command);
            var table = new DataTable();
            adapter.Fill(table);

            grid.DataSource = table;
        }

        private void LoadAllTables()
        {
            foreach (var (grid, sql) in tables)
            {
                LoadTableIntoGrid(sql, grid);
            }

            LoadAccessLog();

            UpdateLockButtonStates();
        }

        private List<string> GetAllUsernames()
        {
            var usernames = new List<string>();

            using var connection = new SQLiteConnection(LoginRegister.ConnectionString);
            connection.Open();
            using var command = new SQLiteCommand("SELECT username FROM users ORDER BY username", connection);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                usernames.Add(reader.GetString(0));
            }

            return usernames;
        }

        private void PopulateAccessLogUserFilter()
        {
            cmbAccessLogUser.Items.Clear();
            cmbAccessLogUser.Items.Add("View All");
            cmbAccessLogUser.Items.AddRange(GetAllUsernames().ToArray());
            cmbAccessLogUser.SelectedIndex = 0;
        }

        private void LoadAccessLog()
        {
            string selected = cmbAccessLogUser.SelectedItem as string;

            if (string.IsNullOrEmpty(selected) || selected == "View All")
            {
                LoadTableIntoGrid("SELECT * FROM access_log ORDER BY Timestamp DESC", dgvAccessLog);
            }
            else
            {
                LoadTableIntoGrid(
                    "SELECT * FROM access_log WHERE username = @username ORDER BY Timestamp DESC",
                    dgvAccessLog,
                    new SQLiteParameter("@username", selected));
            }
        }

        private void cmbAccessLogUser_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadAccessLog();
        }

        private void UpdateLockButtonStates()
        {
            if (dgvData.CurrentRow == null || viewingUserFiles)
            {
                return;
            }

            int lockValue = Convert.ToInt32(dgvData.CurrentRow.Cells["is_locked"].Value);
            bool isLocked = lockValue == 1;

            btnLock.Enabled = !isLocked;
            btnUnlock.Enabled = isLocked;
        }

        private void btnRefresh_Click(object sender, EventArgs e)
        {
            if (viewingUserFiles && selectedUserId != null)
            {
                LoadTableIntoGrid(
                    "SELECT * FROM files WHERE owner_id = @uid ORDER BY uploaded_at DESC",
                    dgvData,
                    new SQLiteParameter("@uid", selectedUserId)
                );

                LoadAccessLog();
            }
            else
            {
                LoadAllTables();
            }
        }

        private void btnViewFiles_Click(object sender, EventArgs e)
        {
            if (dgvData.CurrentRow == null)
            {
                MessageBox.Show("Please select a user first.");
                return;
            }

            selectedUserId = dgvData.CurrentRow.Cells["user_id"].Value;
            string selectedUsername = dgvData.CurrentRow.Cells["username"].Value.ToString();
            viewingUserFiles = true;

            LoadTableIntoGrid(
                "SELECT * FROM files WHERE owner_id = @uid ORDER BY uploaded_at DESC",
                dgvData,
                new SQLiteParameter("@uid", selectedUserId)
            );

            lblDataTable.Text = $"Files - {selectedUsername}";

            btnBack.Visible = true;
            btnViewFiles.Visible = false;
            btnLock.Visible = false;
            btnUnlock.Visible = false;
        }

        private void btnBack_Click(object sender, EventArgs e)
        {
            viewingUserFiles = false;
            selectedUserId = null;

            lblDataTable.Text = "Users";

            btnBack.Visible = false;
            btnViewFiles.Visible = true;
            btnLock.Visible = true;
            btnUnlock.Visible = true;
            LoadAllTables();
        }

        private void btnLock_Click(object sender, EventArgs e)
        {
            if (dgvData.CurrentRow == null)
            {
                MessageBox.Show("Please select a user first.");
                return;
            }

            int userId = Convert.ToInt32(dgvData.CurrentRow.Cells["user_id"].Value);
            UserService.LockUserIndefinitely(LoginRegister.ConnectionString, userId);
            LoadAllTables();
        }

        private void btnUnlock_Click(object sender, EventArgs e)
        {
            if (dgvData.CurrentRow == null)
            {
                MessageBox.Show("Please select a user first.");
                return;
            }

            int userId = Convert.ToInt32(dgvData.CurrentRow.Cells["user_id"].Value);
            UserService.UnlockUser(LoginRegister.ConnectionString, userId);
            LoadAllTables();
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (dgvData.CurrentRow == null)
            {
                MessageBox.Show("Please select a row first.");
                return;
            }

            if (viewingUserFiles)
            {
                int fileId = Convert.ToInt32(dgvData.CurrentRow.Cells["file_id"].Value);
                string fileName = dgvData.CurrentRow.Cells["file_name"].Value.ToString();

                var confirm = MessageBox.Show($"Delete file '{fileName}'?", "Confirm",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (confirm != DialogResult.Yes) return;

                UserService.DeleteFile(LoginRegister.ConnectionString, fileId);

                LoadTableIntoGrid(
                    "SELECT * FROM files WHERE owner_id = @uid ORDER BY uploaded_at DESC",
                    dgvData, new SQLiteParameter("@uid", selectedUserId));
                LoadAccessLog();
            }
            else
            {
                int userId = Convert.ToInt32(dgvData.CurrentRow.Cells["user_id"].Value);
                string username = dgvData.CurrentRow.Cells["username"].Value.ToString();

                var confirm = MessageBox.Show(
                    $"Delete user '{username}' and ALL their files? This cannot be undone.",
                    "Confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (confirm != DialogResult.Yes) return;

                UserService.DeleteUser(LoginRegister.ConnectionString, userId);

                PopulateAccessLogUserFilter();
                LoadAllTables();
            }
        }
    }
}

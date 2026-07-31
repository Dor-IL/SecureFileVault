using System.Data.SQLite;
using System.Globalization;

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

            GridStyler.Style(dgvData, Color.FromArgb(110, 26, 55), Color.FromArgb(160, 50, 85), Color.FromArgb(75, 16, 38));
            GridStyler.Style(dgvAccessLog, Color.FromArgb(110, 26, 55), Color.FromArgb(160, 50, 85), Color.FromArgb(75, 16, 38), false);

            tables = new List<(DataGridView, string)>
            {
                (dgvData,       UsersQuery),
            };

            PopulateAccessLogUserFilter();

            LoadAllTables();

            dgvData.SelectionChanged += (s, e) => UpdateLockButtonStates();
            dgvData.CellFormatting += dgvData_CellFormatting;
        }

        private const string UsersQuery =
            "SELECT user_id, username, failed_attempts, locked_until, is_locked, created_at FROM users";

        private const string UserFilesQuery = @"
            SELECT f.file_id, f.owner_id, f.file_name, f.file_size, f.uploaded_at, 'Owned' AS access
            FROM files f
            WHERE f.owner_id = @uid1

            UNION

            SELECT f.file_id, f.owner_id, f.file_name, f.file_size, f.uploaded_at, 'Shared' AS access
            FROM files f
            JOIN permissions p ON p.file_id = f.file_id
            WHERE p.user_id = @uid2

            ORDER BY uploaded_at DESC";

        private static SQLiteParameter[] UserFilesParams(object userId) =>
            new[] { new SQLiteParameter("@uid1", userId), new SQLiteParameter("@uid2", userId) };

        private void dgvData_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.Value == null || e.Value == DBNull.Value)
            {
                return;
            }

            string columnName = dgvData.Columns[e.ColumnIndex].Name;

            if (columnName == "locked_until")
            {
                if (DateTime.TryParse(e.Value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime lockedUntilUtc))
                {
                    e.Value = lockedUntilUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                    e.FormattingApplied = true;
                }
            }
            else if (columnName == "file_size")
            {
                e.Value = FormatFileSize(Convert.ToInt64(e.Value));
                e.FormattingApplied = true;
            }
        }

        private static string FormatFileSize(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB" };
            double size = bytes;
            int unitIndex = 0;

            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            return unitIndex == 0
                ? $"{bytes} {units[unitIndex]}"
                : $"{size:0.##} {units[unitIndex]}";
        }

        private void StyleButton(Button btn)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderColor = Color.FromArgb(212, 160, 66);
            btn.FlatAppearance.BorderSize = 1;
        }

        private void LoadAllTables()
        {
            foreach (var (grid, sql) in tables)
            {
                GridDataLoader.Load(grid, sql);
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
            string previouslySelected = cmbAccessLogUser.SelectedItem as string;

            cmbAccessLogUser.Items.Clear();
            cmbAccessLogUser.Items.Add("View All");
            cmbAccessLogUser.Items.AddRange(GetAllUsernames().ToArray());

            int restoredIndex = previouslySelected != null ? cmbAccessLogUser.Items.IndexOf(previouslySelected) : -1;
            cmbAccessLogUser.SelectedIndex = restoredIndex >= 0 ? restoredIndex : 0;
        }

        private void LoadAccessLog()
        {
            string selected = cmbAccessLogUser.SelectedItem as string;

            if (string.IsNullOrEmpty(selected) || selected == "View All")
            {
                GridDataLoader.Load(dgvAccessLog, "SELECT * FROM access_log ORDER BY Timestamp DESC");
            }
            else
            {
                GridDataLoader.Load(
                    dgvAccessLog,
                    "SELECT * FROM access_log WHERE username = @username ORDER BY Timestamp DESC",
                    new SQLiteParameter("@username", selected));
            }
        }

        private void cmbAccessLogUser_SelectedIndexChanged(object sender, EventArgs e)
        {
            LoadAccessLog();
        }

        private void UpdateLockButtonStates()
        {
            if (dgvData.CurrentRow == null || viewingUserFiles || !dgvData.Columns.Contains("is_locked"))
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
            PopulateAccessLogUserFilter();

            if (viewingUserFiles && selectedUserId != null)
            {
                GridDataLoader.Load(dgvData, UserFilesQuery, UserFilesParams(selectedUserId));

                LoadAccessLog();
            }
            else
            {
                ReloadUsersPreservingSelection();
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

            GridDataLoader.Load(dgvData, UserFilesQuery, UserFilesParams(selectedUserId));

            lblDataTable.Text = $"Files - {selectedUsername}";

            btnBack.Visible = true;
            btnViewFiles.Visible = false;
            btnLock.Visible = false;
            btnUnlock.Visible = false;
        }

        private void btnBack_Click(object sender, EventArgs e)
        {
            lblDataTable.Text = "Users";

            btnBack.Visible = false;
            btnViewFiles.Visible = true;
            btnLock.Visible = true;
            btnUnlock.Visible = true;

            ReloadUsersPreservingSelection();
        }

        private void ReloadUsersPreservingSelection()
        {
            object userIdToReselect = viewingUserFiles
                ? selectedUserId
                : dgvData.CurrentRow?.Cells["user_id"].Value;

            viewingUserFiles = false;
            selectedUserId = null;

            LoadAllTables();

            ReselectUserRow(userIdToReselect);
        }

        private void ReselectUserRow(object userId)
        {
            if (userId == null)
            {
                return;
            }

            foreach (DataGridViewRow row in dgvData.Rows)
            {
                if (userId.Equals(row.Cells["user_id"].Value))
                {
                    dgvData.CurrentCell = row.Cells[0];
                    break;
                }
            }
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
            ReloadUsersPreservingSelection();
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
            ReloadUsersPreservingSelection();
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
                string access = dgvData.CurrentRow.Cells["access"].Value.ToString();

                if (access == "Shared")
                {
                    MessageBox.Show("This file belongs to another user. Delete it from that user's own file list instead.");
                    return;
                }

                var confirm = MessageBox.Show($"Delete file '{fileName}'?", "Confirm",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (confirm != DialogResult.Yes) return;

                UserService.DeleteFile(LoginRegister.ConnectionString, fileId);

                GridDataLoader.Load(dgvData, UserFilesQuery, UserFilesParams(selectedUserId));
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
                ReloadUsersPreservingSelection();
            }
        }
    }
}

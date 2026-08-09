using System.Data.SQLite;
using System.Globalization;

namespace fileVault
{
    public partial class Client : Form
    {
        private readonly int _userId;
        private readonly string _username;
        private readonly VaultClient _vaultClient;

        private readonly System.Windows.Forms.Timer _accountCheckTimer;
        private readonly System.Windows.Forms.Timer _clockTimer;

        public bool AccountWasDeleted { get; private set; }
        public bool AccountWasLocked { get; private set; }

        public Client(int userId, string username, VaultClient vaultClient)
        {
            InitializeComponent();

            _userId = userId;
            _username = username;
            _vaultClient = vaultClient;

            lblClient.Text = $"Secure files vault - logged in as {_username}";

            GridStyler.Style(dgvFiles, Color.FromArgb(43, 87, 72), Color.FromArgb(70, 130, 110), Color.FromArgb(30, 60, 50));
            GridStyler.Style(dgvAccessLog, Color.FromArgb(43, 87, 72), Color.FromArgb(70, 130, 110), Color.FromArgb(30, 60, 50), false);

            dgvFiles.CellFormatting += dgvFiles_CellFormatting;

            LoadUserData();

            _accountCheckTimer = new System.Windows.Forms.Timer { Interval = 5000 };
            _accountCheckTimer.Tick += AccountCheckTimer_Tick;
            _accountCheckTimer.Start();

            UpdateClock();
            _clockTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            _clockTimer.Tick += (s, e) => UpdateClock();
            _clockTimer.Start();

            FormClosed += (s, e) => _accountCheckTimer.Dispose();
            FormClosed += (s, e) => _clockTimer.Dispose();
        }

        private void UpdateClock()
        {
            lblTimeStamp.Text = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        private async void AccountCheckTimer_Tick(object sender, EventArgs e)
        {
            _accountCheckTimer.Stop();

            bool ok;
            bool locked;
            string message;
            try
            {
                (ok, locked, message) = await _vaultClient.CheckAccountStatusAsync(_userId);
            }
            catch (Exception)
            {
                if (!IsDisposed)
                {
                    _accountCheckTimer.Start();
                }
                return;
            }

            if (IsDisposed)
            {
                return;
            }

            if (!ok)
            {
                CloseDueToAccountState(locked, message);
                return;
            }

            _accountCheckTimer.Start();
        }

        private void CloseDueToAccountState(bool locked, string message)
        {
            if (locked)
                AccountWasLocked = true;
            else
                AccountWasDeleted = true;

            MessageBox.Show(message, locked ? "Account locked" : "Account removed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Close();
        }

        private void dgvFiles_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (dgvFiles.Columns[e.ColumnIndex].Name != "file_size" || e.Value == null || e.Value == DBNull.Value)
            {
                return;
            }

            e.Value = FileSizeFormatter.Format(Convert.ToInt64(e.Value));
            e.FormattingApplied = true;
        }

        private void LoadUserData()
        {
            GridDataLoader.LoadPreservingSelection(
                dgvFiles,
                "file_id",
                @"SELECT file_id, file_name, file_size, uploaded_at, 'Owned' AS access
                  FROM files
                  WHERE owner_id = @uid1
                  UNION
                  SELECT f.file_id, f.file_name, f.file_size, f.uploaded_at, 'Shared' AS access
                  FROM files f
                  JOIN permissions p ON p.file_id = f.file_id
                  WHERE p.user_id = @uid2
                  ORDER BY uploaded_at DESC",
                new SQLiteParameter("@uid1", _userId),
                new SQLiteParameter("@uid2", _userId)
            );

            GridDataLoader.Load(
                dgvAccessLog,
                @"SELECT log_id, action, file_id, timestamp
                  FROM access_log
                  WHERE user_id = @uid
                  ORDER BY timestamp DESC",
                new SQLiteParameter("@uid", _userId)
            );
        }

        private async void btnRefresh_Click(object sender, EventArgs e)
        {
            LoadUserData();
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            var state = UserService.GetAccountState(LoginRegister.ConnectionString, _userId);
            if (state != AccountState.Active)
            {
                var (_, message) = state.Describe();
                MessageBox.Show($"Can't delete or unshare files: {message}", "Action blocked", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (dgvFiles.CurrentRow == null)
            {
                MessageBox.Show("Please select a file first.");
                return;
            }

            int fileId = Convert.ToInt32(dgvFiles.CurrentRow.Cells["file_id"].Value);
            string fileName = dgvFiles.CurrentRow.Cells["file_name"].Value.ToString();
            string access = dgvFiles.CurrentRow.Cells["access"].Value.ToString();

            string confirmMessage = access == "Shared"
                ? $"'{fileName}' was shared with you. Remove it from your list?"
                : $"Delete '{fileName}'? This cannot be undone.";

            var confirm = MessageBox.Show(
                confirmMessage,
                "Confirm Delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes) return;

            bool success = UserService.DeleteFile(LoginRegister.ConnectionString, fileId, _userId);

            if (!success)
                MessageBox.Show("Could not delete that file.");

            LoadUserData();
        }

        private async void btnUpload_Click(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog();
            if (ofd.ShowDialog() != DialogResult.OK) return;

            btnUpload.Enabled = false;
            try
            {
                var (success, message) = await _vaultClient.UploadFileAsync(_userId, ofd.FileName);

                if (!success)
                    MessageBox.Show($"Upload failed: {message}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Upload failed: {ex.Message}");
            }
            finally
            {
                btnUpload.Enabled = true;
            }

            LoadUserData();
        }

        private async void btnDownload_Click(object sender, EventArgs e)
        {
            if (dgvFiles.CurrentRow == null)
            {
                MessageBox.Show("Please select a file first.");
                return;
            }

            int fileId = Convert.ToInt32(dgvFiles.CurrentRow.Cells["file_id"].Value);

            string downloadsFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            Directory.CreateDirectory(downloadsFolder);

            btnDownload.Enabled = false;
            try
            {
                var (success, message) = await _vaultClient.DownloadFileAsync(_userId, fileId, downloadsFolder);
                MessageBox.Show(success ? $"Downloaded to: {message}" : $"Download failed: {message}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Download failed: {ex.Message}");
            }
            finally
            {
                btnDownload.Enabled = true;
            }

            LoadUserData();
        }

        private async void btnShare_Click(object sender, EventArgs e)
        {
            if (dgvFiles.CurrentRow == null)
            {
                MessageBox.Show("Please select a file first.");
                return;
            }

            int fileId = Convert.ToInt32(dgvFiles.CurrentRow.Cells["file_id"].Value);

            string targetUsername = PromptForUsername("Share File", "Enter the username to share this file with:");
            if (string.IsNullOrWhiteSpace(targetUsername)) return;

            btnShare.Enabled = false;
            try
            {
                var (success, message) = await _vaultClient.ShareFileAsync(fileId, _userId, targetUsername.Trim());
                MessageBox.Show(success ? message : $"Share failed: {message}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Share failed: {ex.Message}");
            }
            finally
            {
                btnShare.Enabled = true;
            }
        }

        private async void btnUnshare_Click(object sender, EventArgs e)
        {
            if (dgvFiles.CurrentRow == null)
            {
                MessageBox.Show("Please select a file first.");
                return;
            }

            int fileId = Convert.ToInt32(dgvFiles.CurrentRow.Cells["file_id"].Value);

            List<string> sharedWith = GetSharedUsernames(fileId);
            if (sharedWith.Count == 0)
            {
                MessageBox.Show("This file hasn't been shared with anyone.");
                return;
            }

            string targetUsername = PromptForSelection("Unshare File", "Choose a user to revoke access from:", sharedWith);
            if (string.IsNullOrWhiteSpace(targetUsername)) return;

            btnUnshare.Enabled = false;
            try
            {
                var (success, message) = await _vaultClient.UnshareFileAsync(fileId, _userId, targetUsername);
                MessageBox.Show(success ? message : $"Unshare failed: {message}");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unshare failed: {ex.Message}");
            }
            finally
            {
                btnUnshare.Enabled = true;
            }

            LoadUserData();
        }

        private List<string> GetSharedUsernames(int fileId)
        {
            var usernames = new List<string>();

            using var connection = new SQLiteConnection(LoginRegister.ConnectionString);
            connection.Open();
            using var command = new SQLiteCommand(
                @"SELECT u.username FROM permissions p
                  JOIN users u ON u.user_id = p.user_id
                  WHERE p.file_id = @fid
                  ORDER BY u.username",
                connection);
            command.Parameters.AddWithValue("@fid", fileId);

            using var reader = command.ExecuteReader();
            while (reader.Read())
                usernames.Add(reader.GetString(0));

            return usernames;
        }

        private static string PromptForSelection(string title, string prompt, List<string> options)
        {
            using var dialog = new Form
            {
                Text = title,
                Width = 360,
                Height = 160,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var lbl = new Label { Left = 12, Top = 12, Width = 320, Text = prompt };
            var combo = new ComboBox { Left = 12, Top = 40, Width = 320, DropDownStyle = ComboBoxStyle.DropDownList };
            combo.Items.AddRange(options.ToArray());
            combo.SelectedIndex = 0;
            var btnOk = new Button { Text = "OK", Left = 175, Width = 75, Top = 75, DialogResult = DialogResult.OK };
            var btnCancel = new Button { Text = "Cancel", Left = 257, Width = 75, Top = 75, DialogResult = DialogResult.Cancel };

            dialog.Controls.AddRange(new Control[] { lbl, combo, btnOk, btnCancel });
            dialog.AcceptButton = btnOk;
            dialog.CancelButton = btnCancel;

            return dialog.ShowDialog() == DialogResult.OK ? combo.SelectedItem as string : null;
        }

        private string PromptForUsername(string title, string prompt)
        {
            using var dialog = new Form
            {
                Text = title,
                Width = 360,
                Height = 230,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false
            };

            var lbl = new Label { Left = 12, Top = 12, Width = 320, Text = prompt };
            var txt = new TextBox { Left = 12, Top = 40, Width = 320 };
            var lst = new ListBox { Left = 12, Top = 64, Width = 320, Height = 92, Visible = false };
            var btnOk = new Button { Text = "OK", Left = 175, Width = 75, Top = 164, DialogResult = DialogResult.OK };
            var btnCancel = new Button { Text = "Cancel", Left = 257, Width = 75, Top = 164, DialogResult = DialogResult.Cancel };

            void AcceptSuggestion(int index)
            {
                if (index < 0 || index >= lst.Items.Count) return;
                txt.Text = (string)lst.Items[index];
                txt.SelectionStart = txt.Text.Length;
                lst.Visible = false;
                txt.Focus();
            }

            txt.TextChanged += (s, e) =>
            {
                string query = txt.Text.Trim();
                List<string> matches = query.Length == 0
                    ? new List<string>()
                    : UserService.SearchUsernames(LoginRegister.ConnectionString, query, _userId);

                lst.Items.Clear();
                if (matches.Count > 0)
                {
                    lst.Items.AddRange(matches.ToArray());
                    lst.Visible = true;
                }
                else
                {
                    lst.Visible = false;
                }
            };

            txt.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Down && lst.Visible && lst.Items.Count > 0)
                {
                    lst.Focus();
                    lst.SelectedIndex = 0;
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };

            lst.Click += (s, e) => AcceptSuggestion(lst.SelectedIndex);

            lst.DoubleClick += (s, e) =>
            {
                AcceptSuggestion(lst.SelectedIndex);
                dialog.DialogResult = DialogResult.OK;
                dialog.Close();
            };

            lst.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    AcceptSuggestion(lst.SelectedIndex);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
                else if (e.KeyCode == Keys.Escape)
                {
                    lst.Visible = false;
                    txt.Focus();
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }
            };

            dialog.Controls.AddRange(new Control[] { lbl, txt, lst, btnOk, btnCancel });
            dialog.AcceptButton = btnOk;
            dialog.CancelButton = btnCancel;

            return dialog.ShowDialog() == DialogResult.OK ? txt.Text : null;
        }
    }
}

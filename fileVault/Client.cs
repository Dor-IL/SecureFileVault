using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Security.Cryptography;
namespace fileVault
{
    public partial class Client : Form
    {
        private readonly int _userId;
        private readonly string _username;
        private readonly VaultClient _vaultClient;

        public Client(int userId, string username, VaultClient vaultClient)
        {
            InitializeComponent();

            _userId = userId;
            _username = username;
            _vaultClient = vaultClient;

            lblClient.Text = $"Secure files vault - logged in as {_username}";

            StyleGrid(dgvFiles);
            StyleGrid(dgvAccessLog, false);

            LoadUserData();
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

            Color normalBack = Color.FromArgb(43, 87, 72);
            Color normalFore = Color.White;

            // A lighter tint of the same green for the selected row, so it still reads clearly
            Color selectedBack = allowHighlight ? Color.FromArgb(70, 130, 110) : normalBack;
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

            grid.GridColor = Color.FromArgb(30, 60, 50); // slightly darker green for grid lines
            grid.BackgroundColor = Color.FromArgb(30, 60, 50);

            if (!allowHighlight)
            {
                grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            }
        }

        private void LoadUserData()
        {

            LoadIntoGrid(
                dgvFiles,
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

            LoadIntoGrid(
                dgvAccessLog,
                @"SELECT log_id, action, file_id, timestamp
                  FROM access_log
                  WHERE user_id = @uid
                  ORDER BY timestamp DESC",
                new SQLiteParameter("@uid", _userId)
            );
        }

        private void LoadIntoGrid(DataGridView grid, string sql, params SQLiteParameter[] parameters)
        {
            using var connection = new SQLiteConnection(LoginRegister.ConnectionString);
            using var command = new SQLiteCommand(sql, connection);
            if (parameters != null) command.Parameters.AddRange(parameters);

            using var adapter = new SQLiteDataAdapter(command);
            var table = new DataTable();
            adapter.Fill(table);

            grid.DataSource = table;
        }

        private async void btnRefresh_Click(object sender, EventArgs e)
        {
            LoadUserData();
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (dgvFiles.CurrentRow == null)
            {
                MessageBox.Show("Please select a file first.");
                return;
            }

            int fileId = Convert.ToInt32(dgvFiles.CurrentRow.Cells["file_id"].Value);
            string fileName = dgvFiles.CurrentRow.Cells["file_name"].Value.ToString();

            var confirm = MessageBox.Show(
                $"Delete '{fileName}'? This cannot be undone.",
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
            var (success, message) = await _vaultClient.UploadFileAsync(_userId, ofd.FileName);
            btnUpload.Enabled = true;

            if (!success)
                MessageBox.Show($"Upload failed: {message}");

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

            using var fbd = new FolderBrowserDialog();
            if (fbd.ShowDialog() != DialogResult.OK) return;

            btnDownload.Enabled = false;
            var (success, message) = await _vaultClient.DownloadFileAsync(_userId, fileId, fbd.SelectedPath);
            btnDownload.Enabled = true;

            MessageBox.Show(success ? $"Downloaded to: {message}" : $"Download failed: {message}");

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
            var (success, message) = await _vaultClient.ShareFileAsync(fileId, _userId, targetUsername.Trim()); 
            btnShare.Enabled = true; 

            MessageBox.Show(success ? message : $"Share failed: {message}");
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
            var (success, message) = await _vaultClient.UnshareFileAsync(fileId, _userId, targetUsername); 
            btnUnshare.Enabled = true; 

            MessageBox.Show(success ? message : $"Unshare failed: {message}"); 

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
        }//

        private static string PromptForUsername(string title, string prompt)
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
            var txt = new TextBox { Left = 12, Top = 40, Width = 320 }; 
            var btnOk = new Button { Text = "OK", Left = 175, Width = 75, Top = 75, DialogResult = DialogResult.OK }; 
            var btnCancel = new Button { Text = "Cancel", Left = 257, Width = 75, Top = 75, DialogResult = DialogResult.Cancel }; 

            dialog.Controls.AddRange(new Control[] { lbl, txt, btnOk, btnCancel }); 
            dialog.AcceptButton = btnOk; 
            dialog.CancelButton = btnCancel; 

            return dialog.ShowDialog() == DialogResult.OK ? txt.Text : null; 
        }//
    }

}


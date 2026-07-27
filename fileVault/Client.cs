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
            // Files owned by this user
            LoadIntoGrid(
                dgvFiles,
                @"SELECT file_id, file_name, file_size, uploaded_at
                  FROM files
                  WHERE owner_id = @uid
                  ORDER BY uploaded_at DESC",
                new SQLiteParameter("@uid", _userId)
            );

            // Access log entries relevant to this user (their own actions)
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

        private void btnShare_Click(object sender, EventArgs e)
        {

        }
    }

}


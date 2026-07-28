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

    public partial class LoginRegister : Form
    {
        private VaultClient _vaultClient;

        const string DatabaseFileName = "database.db";
        public static string ConnectionString =>
           $"Data Source={Path.Combine(Application.StartupPath, DatabaseFileName)};Version=3;";

        public LoginRegister()
        {
            InitializeComponent();

            try
            {
                Db.initialize(ConnectionString, DatabaseFileName);
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

    class VaultServer
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public VaultServer(int port)
        {
            _listener = new TcpListener(IPAddress.Loopback, port);
        }

        public void Start()
        {
            _listener.Start();
            Console.WriteLine($"[Server] Listening on port {((IPEndPoint)_listener.LocalEndpoint).Port}...");

            Task.Run(() => AcceptLoopAsync(_cts.Token));
        }

        private async Task AcceptLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    Console.WriteLine($"[Server] Client connected: {client.Client.RemoteEndPoint}");

                    _ = Task.Run(() => HandleClientAsync(client));
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
            }
        }

        private async Task HandleClientAsync(TcpClient client) //
        {
            using (client)
            using (var stream = client.GetStream())
            {
                try
                {
                    while (client.Connected)
                    {
                        string request = await NetworkHelper.ReceiveTextAsync(stream);
                        int commandEnd = request.IndexOf('|');
                        string command = commandEnd >= 0 ? request.Substring(0, commandEnd) : request;

                        if (command == "UPLOAD")
                        {
                            await HandleUploadAsync(stream, request);
                        }
                        else if (command == "DOWNLOAD")
                        {
                            await HandleDownloadAsync(stream, request);
                        }
                        else
                        {
                            string response = await ProcessCommandAsync(request);
                            await NetworkHelper.SendTextAsync(stream, response);
                        }
                    }
                }
                catch (IOException)
                {
                    Console.WriteLine("[Server] Client disconnected.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Server] Error handling client: {ex.Message}");
                }
            }
        }

        private async Task HandleUploadAsync(NetworkStream stream, string request) //
        {
            try
            {
                string[] parts = request.Split('|', 3);
                int userId = int.Parse(parts[1]);
                string fileName = parts[2];

                byte[] fileData = await NetworkHelper.ReceiveMessageAsync(stream);

                int fileId = FileService.SaveNewFile(LoginRegister.ConnectionString, userId, fileName, fileData);
                await NetworkHelper.SendTextAsync(stream, $"OK|{fileId}");
            }
            catch (Exception ex)
            {
                await NetworkHelper.SendTextAsync(stream, $"FAIL|{ex.Message}");
            }
        }

        private async Task HandleDownloadAsync(NetworkStream stream, string request) //
        {
            string[] parts = request.Split('|');
            int userId = int.Parse(parts[1]);
            int fileId = int.Parse(parts[2]);

            var result = FileService.GetFileForDownload(LoginRegister.ConnectionString, fileId, userId);

            if (!result.allowed)
            {
                await NetworkHelper.SendTextAsync(stream, $"FAIL|{result.message}");
                return;
            }

            await NetworkHelper.SendTextAsync(stream, $"OK|{result.fileName}");
            await NetworkHelper.SendMessageAsync(stream, result.data);
        }

        private async Task<string> ProcessCommandAsync(string request) //
        {
            int commandEnd = request.IndexOf('|');
            string command = commandEnd >= 0 ? request.Substring(0, commandEnd) : request;

            switch (command)
            {
                case "LOGIN":
                    {
                        string[] parts = request.Split('|');
                        if (parts.Length != 3)
                            return "FAIL|Invalid username or password.";
                        string username = parts[1];
                        string password = parts[2];

                        var result = UserService.Login(LoginRegister.ConnectionString, username, password, out int userId);

                        switch (result)
                        {
                            case LoginResult.Success:
                                return $"OK|{userId}";
                            case LoginResult.AccountLocked:
                                return "FAIL|Account locked. Try again later.";
                            default:
                                return "FAIL|Invalid username or password.";
                        }
                    }

                case "REGISTER":
                    {
                        string[] parts = request.Split('|');
                        if (parts.Length != 3)
                            return "FAIL|Username and password cannot contain the '|' character.";
                        string username = parts[1];
                        string password = parts[2];

                        bool success = UserService.RegisterUser(LoginRegister.ConnectionString, username, password);
                        return success ? "OK" : "FAIL|Username already taken.";
                    }

                case "SHARE": // Handles a client's request to grant another user access to one of its files
                    {
                        string[] parts = request.Split('|', 4);
                        int fileId = int.Parse(parts[1]); // Which file is being shared
                        int ownerId = int.Parse(parts[2]); // Who is requesting the share (must be the owner)
                        string targetUsername = parts[3]; // Username of the person to share with

                        // Delegate the ownership check and permissions-table insert to FileService
                        var (success, message) = FileService.ShareFile(LoginRegister.ConnectionString, fileId, ownerId, targetUsername);
                        return success ? $"OK|{message}" : $"FAIL|{message}"; // Mirror the OK/FAIL protocol used by other commands
                    }

                case "UNSHARE": // Handles a client's request to revoke another user's access to one of its files
                    {
                        string[] parts = request.Split('|', 4);
                        int fileId = int.Parse(parts[1]); // Which file access is being revoked from
                        int ownerId = int.Parse(parts[2]); // Who is requesting the unshare (must be the owner)
                        string targetUsername = parts[3]; // Username of the person to revoke access from

                        // Delegate the ownership check and permissions-table delete to FileService
                        var (success, message) = FileService.UnshareFile(LoginRegister.ConnectionString, fileId, ownerId, targetUsername);
                        return success ? $"OK|{message}" : $"FAIL|{message}"; // Mirror the OK/FAIL protocol used by other commands
                    }

                default:
                    return "FAIL|Unknown command.";
            }
        }

        public void Stop()
        {
            _cts.Cancel();
            _listener.Stop();
        }
    } 

    public class VaultClient
    {
        private TcpClient _client;
        private NetworkStream _stream;

        public async Task ConnectAsync(string host, int port)
        {
            _client = new TcpClient();
            await _client.ConnectAsync(host, port); 
            _stream = _client.GetStream();
            Console.WriteLine("[Client] Connected to server.");
        }

        public async Task<(bool success, string message)> LoginAsync(string username, string password)
        {
            await NetworkHelper.SendTextAsync(_stream, $"LOGIN|{username}|{password}");
            string response = await NetworkHelper.ReceiveTextAsync(_stream);

            string[] parts = response.Split('|');
            if (parts[0] == "OK")
                return (true, parts.Length > 1 ? parts[1] : ""); 
            else
                return (false, parts.Length > 1 ? parts[1] : "Login failed.");
        }

        public async Task<(bool success, string message)> RegisterAsync(string username, string password)
        {
            await NetworkHelper.SendTextAsync(_stream, $"REGISTER|{username}|{password}");
            string response = await NetworkHelper.ReceiveTextAsync(_stream);

            string[] parts = response.Split('|');
            return parts[0] == "OK"
                ? (true, "Registered successfully.")
                : (false, parts.Length > 1 ? parts[1] : "Registration failed.");
        }

        public async Task<(bool success, string message)> UploadFileAsync(int userId, string filePath)
        {
            string fileName = Path.GetFileName(filePath);
            byte[] fileBytes = await File.ReadAllBytesAsync(filePath);

            await NetworkHelper.SendTextAsync(_stream, $"UPLOAD|{userId}|{fileName}");
            await NetworkHelper.SendMessageAsync(_stream, fileBytes);

            string response = await NetworkHelper.ReceiveTextAsync(_stream);
            string[] parts = response.Split('|');
            return parts[0] == "OK"
                ? (true, "Upload successful.")
                : (false, parts.Length > 1 ? parts[1] : "Upload failed.");
        }

        public async Task<(bool success, string message)> DownloadFileAsync(int userId, int fileId, string saveDirectory)
        {
            await NetworkHelper.SendTextAsync(_stream, $"DOWNLOAD|{userId}|{fileId}");
            string response = await NetworkHelper.ReceiveTextAsync(_stream);
            string[] parts = response.Split('|');

            if (parts[0] != "OK")
                return (false, parts.Length > 1 ? parts[1] : "Download failed.");

            string fileName = parts[1];
            byte[] fileBytes = await NetworkHelper.ReceiveMessageAsync(_stream);

            string savePath = Path.Combine(saveDirectory, fileName);
            await File.WriteAllBytesAsync(savePath, fileBytes);

            return (true, savePath);
        }

        public async Task<(bool success, string message)> ShareFileAsync(int fileId, int ownerId, string targetUsername)
        {
            // Send the share request over the wire: command|fileId|ownerId|targetUsername
            await NetworkHelper.SendTextAsync(_stream, $"SHARE|{fileId}|{ownerId}|{targetUsername}");
            string response = await NetworkHelper.ReceiveTextAsync(_stream); // Wait for the server's OK/FAIL reply

            string[] parts = response.Split('|'); // Split the "OK|message" or "FAIL|message" response
            return parts[0] == "OK"
                ? (true, parts.Length > 1 ? parts[1] : "Shared successfully.") // Success: pass along the server's message
                : (false, parts.Length > 1 ? parts[1] : "Share failed."); // Failure: pass along the reason, or a default
        }

        public async Task<(bool success, string message)> UnshareFileAsync(int fileId, int ownerId, string targetUsername)
        {
            // Send the unshare request over the wire: command|fileId|ownerId|targetUsername
            await NetworkHelper.SendTextAsync(_stream, $"UNSHARE|{fileId}|{ownerId}|{targetUsername}");
            string response = await NetworkHelper.ReceiveTextAsync(_stream); // Wait for the server's OK/FAIL reply

            string[] parts = response.Split('|'); // Split the "OK|message" or "FAIL|message" response
            return parts[0] == "OK"
                ? (true, parts.Length > 1 ? parts[1] : "Unshared successfully.") // Success: pass along the server's message
                : (false, parts.Length > 1 ? parts[1] : "Unshare failed."); // Failure: pass along the reason, or a default
        }//

        public NetworkStream GetStream() => _stream;

        public void Close() => _client?.Close();
    }

    static class PasswordHasher
    {
        private const int SaltSize = 16;   
        private const int HashSize = 32;     
        private const int Iterations = 100_000;

        public static string GenerateSalt()
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            return Convert.ToBase64String(salt);
        }
        public static string HashPassword(string password, string saltBase64)
        {
            byte[] salt = Convert.FromBase64String(saltBase64);

            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                Iterations,
                HashAlgorithmName.SHA256,
                HashSize);

            return Convert.ToBase64String(hash);
        }

        public static bool VerifyPassword(string password, string saltBase64, string storedHashBase64)
        {
            string attemptHash = HashPassword(password, saltBase64);

            byte[] a = Convert.FromBase64String(attemptHash);
            byte[] b = Convert.FromBase64String(storedHashBase64);
            return CryptographicOperations.FixedTimeEquals(a, b);
        }
    } 

    enum LoginResult { Success, InvalidCredentials, AccountLocked }

    static class UserService
    {
        private const int MaxFailedAttempts = 5;
        private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);

        public static bool RegisterUser(string connectionString, string username, string password)
        {
            string salt = PasswordHasher.GenerateSalt();
            string hash = PasswordHasher.HashPassword(password, salt);

            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();

                const string sql = @"
                    INSERT INTO users (username, hashedPassword, salt)
                    VALUES (@username, @hash, @salt);";

                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@username", username);
                    cmd.Parameters.AddWithValue("@hash", hash);
                    cmd.Parameters.AddWithValue("@salt", salt);

                    try
                    {
                        cmd.ExecuteNonQuery();
                        return true;
                    }
                    catch (SQLiteException ex) when (ex.ResultCode == SQLiteErrorCode.Constraint)
                    {
                        return false;
                    }
                }
            }
        }

        public static LoginResult Login(string connectionString, string username, string password, out int userId)
        {
            userId = -1;

            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();

                const string sql = @"
                    SELECT user_id, hashedPassword, salt, failed_attempts, locked_until, is_locked
                    FROM users WHERE username = @username;";

                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@username", username);

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                        {
                            LogEvent(conn, null, username, "LOGIN_FAILED", null);
                            return LoginResult.InvalidCredentials;
                        }

                        int id = reader.GetInt32(0);
                        string storedHash = reader.GetString(1);
                        string salt = reader.GetString(2);
                        int failedAttempts = reader.GetInt32(3);
                        object lockedUntilObj = reader["locked_until"];
                        bool isLocked = reader.GetInt32(reader.GetOrdinal("is_locked")) == 1;

                        if (isLocked)
                        {
                            reader.Close();
                            LogEvent(conn, id, username, "LOGIN_FAILED", null);
                            return LoginResult.AccountLocked;
                        }

                        if (lockedUntilObj != DBNull.Value)
                        {
                            DateTime lockedUntil = DateTime.Parse(lockedUntilObj.ToString());
                            if (DateTime.UtcNow < lockedUntil)
                            {
                                reader.Close();
                                LogEvent(conn, id, username, "LOGIN_FAILED", null);
                                return LoginResult.AccountLocked;
                            }
                        }

                        reader.Close();

                        bool ok = PasswordHasher.VerifyPassword(password, salt, storedHash);

                        if (ok)
                        {
                            ResetFailedAttempts(conn, id);
                            LogEvent(conn, id, username, "LOGIN_SUCCESS", null);
                            userId = id;
                            return LoginResult.Success;
                        }
                        else
                        {
                            RegisterFailedAttempt(conn, id, failedAttempts + 1);
                            LogEvent(conn, id, username, "LOGIN_FAILED", null);
                            return LoginResult.InvalidCredentials;
                        }
                    }
                }
            }
        }

        private static void RegisterFailedAttempt(SQLiteConnection conn, int userId, int newCount)
        {
            string sql;
            if (newCount >= MaxFailedAttempts)
            {
                sql = @"UPDATE users
                        SET failed_attempts = @count,
                            locked_until = @lockedUntil
                        WHERE user_id = @id;";
            }
            else
            {
                sql = @"UPDATE users SET failed_attempts = @count WHERE user_id = @id;";
            }

            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@count", newCount);
                cmd.Parameters.AddWithValue("@id", userId);
                if (newCount >= MaxFailedAttempts)
                {
                    cmd.Parameters.AddWithValue("@lockedUntil",
                    DateTime.UtcNow.Add(LockoutDuration).ToString("o"));
                }
                cmd.ExecuteNonQuery();
            }
        }

        private static void ResetFailedAttempts(SQLiteConnection conn, int userId)
        {
            const string sql = @"UPDATE users
                                  SET failed_attempts = 0, locked_until = NULL
                                  WHERE user_id = @id;";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@id", userId);
                cmd.ExecuteNonQuery();
            }
        }

        public static void LockUserIndefinitely(string connectionString, int userId)
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();

                const string sql = @"UPDATE users
                              SET is_locked = 1
                              WHERE user_id = @id;";

                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", userId);
                    cmd.ExecuteNonQuery();
                }

                LogEvent(conn, userId, GetUsername(conn, userId), "ADMIN_LOCK", null);
            }
        }

        public static void UnlockUser(string connectionString, int userId)
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();

                const string sql = @"UPDATE users
                              SET is_locked = 0, locked_until = NULL, failed_attempts = 0
                              WHERE user_id = @id;";

                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", userId);
                    cmd.ExecuteNonQuery();
                }

                LogEvent(conn, userId, GetUsername(conn, userId), "ADMIN_UNLOCK", null);
            }
        }

        public static void DeleteUser(string connectionString, int userId)
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();
                using (var pragma = new SQLiteCommand("PRAGMA foreign_keys = ON;", conn))
                    pragma.ExecuteNonQuery();

                string deletedUsername = GetUsername(conn, userId);

                using (var tx = conn.BeginTransaction())
                {
                    try
                    {
                        var filePaths = new List<string>();
                        using (var cmd = new SQLiteCommand(
                            "SELECT stored_path FROM files WHERE owner_id = @id", conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@id", userId);
                            using (var reader = cmd.ExecuteReader())
                                while (reader.Read()) filePaths.Add(reader.GetString(0));
                        }

                        using (var cmd = new SQLiteCommand(
                            "UPDATE access_log SET file_id = NULL WHERE file_id IN " +
                            "(SELECT file_id FROM files WHERE owner_id = @id)", conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@id", userId);
                            cmd.ExecuteNonQuery();
                        }
                        using (var cmd = new SQLiteCommand(
                            "UPDATE access_log SET user_id = NULL WHERE user_id = @id", conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@id", userId);
                            cmd.ExecuteNonQuery();
                        }

                        using (var cmd = new SQLiteCommand(
                            "DELETE FROM users WHERE user_id = @id", conn, tx))
                        {
                            cmd.Parameters.AddWithValue("@id", userId);
                            cmd.ExecuteNonQuery();
                        }

                        tx.Commit();

                        LogEvent(conn, null, deletedUsername, "USER_DELETED", null);

                        foreach (var path in filePaths)
                        {
                            try { if (File.Exists(path)) File.Delete(path); } catch { }
                        }
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        public static bool DeleteFile(string connectionString, int fileId, int? requestingUserId = null)
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();
                using (var pragma = new SQLiteCommand("PRAGMA foreign_keys = ON;", conn))
                    pragma.ExecuteNonQuery();

                string storedPath = null;
                int ownerId = -1;

                using (var cmd = new SQLiteCommand(
                    "SELECT stored_path, owner_id FROM files WHERE file_id = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", fileId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read()) return false; 
                        storedPath = reader.GetString(0);
                        ownerId = reader.GetInt32(1);
                    }
                }

                if (requestingUserId.HasValue && ownerId != requestingUserId.Value)
                {
                    LogEvent(conn, requestingUserId, GetUsername(conn, requestingUserId.Value), "DELETE_DENIED", fileId);
                    return false;
                }

                using (var cmd = new SQLiteCommand(
                    "UPDATE access_log SET file_id = NULL WHERE file_id = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", fileId);
                    cmd.ExecuteNonQuery();
                }

                using (var cmd = new SQLiteCommand("DELETE FROM files WHERE file_id = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", fileId);
                    cmd.ExecuteNonQuery();
                }

                LogEvent(conn, ownerId, GetUsername(conn, ownerId), "FILE_DELETED", null);

                try { if (File.Exists(storedPath)) File.Delete(storedPath); } catch { }

                return true;
            }
        }

        private static void LogEvent(SQLiteConnection conn, int? userId, string username, string action, int? fileId)
        {
            const string sql = @"INSERT INTO access_log (user_id, username, action, file_id)
                                        VALUES (@userId, @username, @action, @fileId);";
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.Parameters.AddWithValue("@userId", (object)userId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@username", (object)username ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@action", action);
                cmd.Parameters.AddWithValue("@fileId", (object)fileId ?? DBNull.Value);

                cmd.ExecuteNonQuery();
            }
        }

        private static string GetUsername(SQLiteConnection conn, int userId)
        {
            using (var cmd = new SQLiteCommand("SELECT username FROM users WHERE user_id = @id", conn))
            {
                cmd.Parameters.AddWithValue("@id", userId);
                var result = cmd.ExecuteScalar();
                return result?.ToString();
            }
        }
    }

    static class FileService
    {
        public static string StorageDirectory =>
            Path.Combine(Application.StartupPath, "VaultStorage");

        public static int SaveNewFile(string connectionString, int ownerId, string fileName, byte[] plainData)
        {
            Directory.CreateDirectory(StorageDirectory);

            byte[] key = RandomNumberGenerator.GetBytes(32); 
            byte[] iv = RandomNumberGenerator.GetBytes(16);

            byte[] encrypted = Encrypt(plainData, key, iv);

            string storedFileName = $"{Guid.NewGuid()}.enc";
            string storedPath = Path.Combine(StorageDirectory, storedFileName);
            File.WriteAllBytes(storedPath, encrypted);

            string keyStore = $"{Convert.ToBase64String(iv)}:{Convert.ToBase64String(key)}";

            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();
                const string sql = @"INSERT INTO files (owner_id, file_name, stored_path, encryption_key, file_size)
                                  VALUES (@owner, @name, @path, @key, @size);
                                  SELECT last_insert_rowid();";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@owner", ownerId);
                    cmd.Parameters.AddWithValue("@name", fileName);
                    cmd.Parameters.AddWithValue("@path", storedPath);
                    cmd.Parameters.AddWithValue("@key", keyStore);
                    cmd.Parameters.AddWithValue("@size", plainData.Length);

                    int fileId = Convert.ToInt32(cmd.ExecuteScalar());
                    LogEvent(conn, ownerId, GetUsername(conn, ownerId), "FILE_UPLOADED", fileId);
                    return fileId;
                }
            }
        }

        public static (bool allowed, string message, string fileName, byte[] data) GetFileForDownload(
            string connectionString, int fileId, int requestingUserId)
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();

                string fileName, storedPath, keyStore;
                int ownerId;

                using (var cmd = new SQLiteCommand(
                    "SELECT file_name, stored_path, encryption_key, owner_id FROM files WHERE file_id = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", fileId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read())
                            return (false, "File not found.", null, null);

                        fileName = reader.GetString(0);
                        storedPath = reader.GetString(1);
                        keyStore = reader.GetString(2);
                        ownerId = reader.GetInt32(3);
                    }
                }

                bool allowed = ownerId == requestingUserId || HasPermission(conn, fileId, requestingUserId);

                if (!allowed)
                {
                    LogEvent(conn, requestingUserId, GetUsername(conn, requestingUserId), "DOWNLOAD_DENIED", fileId);
                    return (false, "Access denied.", null, null);
                }

                string[] parts = keyStore.Split(':');
                byte[] iv = Convert.FromBase64String(parts[0]);
                byte[] key = Convert.FromBase64String(parts[1]);

                byte[] encrypted = File.ReadAllBytes(storedPath);
                byte[] decrypted = Decrypt(encrypted, key, iv);

                LogEvent(conn, requestingUserId, GetUsername(conn, requestingUserId), "DOWNLOAD_SUCCESS", fileId);
                return (true, "OK", fileName, decrypted);
            }
        }

        public static (bool success, string message) ShareFile(
            string connectionString, int fileId, int ownerId, string targetUsername)
        {
            using (var conn = new SQLiteConnection(connectionString)) // Open a fresh connection for this request
            {
                conn.Open(); // Actually connect to the SQLite database
                using (var pragma = new SQLiteCommand("PRAGMA foreign_keys = ON;", conn)) // Enforce FK constraints on this connection
                    pragma.ExecuteNonQuery(); // Run the pragma

                int actualOwnerId; // Will hold the file's real owner_id from the database

                using (var cmd = new SQLiteCommand("SELECT owner_id FROM files WHERE file_id = @fid", conn)) // Look up who owns the file
                {
                    cmd.Parameters.AddWithValue("@fid", fileId); // Bind the file id parameter
                    var result = cmd.ExecuteScalar(); // Run the query and fetch the single owner_id value
                    if (result == null) return (false, "File not found."); // No such file, so nothing to share
                    actualOwnerId = Convert.ToInt32(result); // Convert the DB value to an int
                }

                if (actualOwnerId != ownerId) // Only the real owner is allowed to grant access
                {
                    LogEvent(conn, ownerId, GetUsername(conn, ownerId), "SHARE_DENIED", fileId); // Record the denied attempt
                    return (false, "Only the file owner can share this file."); // Reject the request
                }

                int targetUserId; // Will hold the id of the user we're sharing with

                using (var cmd = new SQLiteCommand("SELECT user_id FROM users WHERE username = @uname", conn)) // Resolve username to a user id
                {
                    cmd.Parameters.AddWithValue("@uname", targetUsername); // Bind the target username
                    var result = cmd.ExecuteScalar(); // Run the lookup
                    if (result == null) return (false, "No user with that username."); // Unknown recipient
                    targetUserId = Convert.ToInt32(result); // Convert the DB value to an int
                }

                if (targetUserId == ownerId) // Sharing a file with its own owner is meaningless
                    return (false, "You already own this file."); // Reject that case

                using (var cmd = new SQLiteCommand( // Grant access by inserting into the permissions table
                    "INSERT OR IGNORE INTO permissions (file_id, user_id) VALUES (@fid, @uid)", conn)) // Ignore if already shared (UNIQUE constraint)
                {
                    cmd.Parameters.AddWithValue("@fid", fileId); // Bind the file id
                    cmd.Parameters.AddWithValue("@uid", targetUserId); // Bind the recipient's user id
                    cmd.ExecuteNonQuery(); // Perform the insert
                }

                LogEvent(conn, ownerId, GetUsername(conn, ownerId), $"FILE_SHARED_WITH:{targetUsername}", fileId); // Audit-log the share
                return (true, $"Shared with {targetUsername}."); // Report success back to the caller
            }
        }

        public static (bool success, string message) UnshareFile(
            string connectionString, int fileId, int ownerId, string targetUsername)
        {
            using (var conn = new SQLiteConnection(connectionString)) // Open a fresh connection for this request
            {
                conn.Open(); // Actually connect to the SQLite database
                using (var pragma = new SQLiteCommand("PRAGMA foreign_keys = ON;", conn)) // Enforce FK constraints on this connection
                    pragma.ExecuteNonQuery(); // Run the pragma

                int actualOwnerId; // Will hold the file's real owner_id from the database

                using (var cmd = new SQLiteCommand("SELECT owner_id FROM files WHERE file_id = @fid", conn)) // Look up who owns the file
                {
                    cmd.Parameters.AddWithValue("@fid", fileId); // Bind the file id parameter
                    var result = cmd.ExecuteScalar(); // Run the query and fetch the single owner_id value
                    if (result == null) return (false, "File not found."); // No such file, so nothing to unshare
                    actualOwnerId = Convert.ToInt32(result); // Convert the DB value to an int
                }

                if (actualOwnerId != ownerId) // Only the real owner is allowed to revoke access
                {
                    LogEvent(conn, ownerId, GetUsername(conn, ownerId), "UNSHARE_DENIED", fileId); // Record the denied attempt
                    return (false, "Only the file owner can unshare this file."); // Reject the request
                }

                int targetUserId; // Will hold the id of the user we're revoking access from

                using (var cmd = new SQLiteCommand("SELECT user_id FROM users WHERE username = @uname", conn)) // Resolve username to a user id
                {
                    cmd.Parameters.AddWithValue("@uname", targetUsername); // Bind the target username
                    var result = cmd.ExecuteScalar(); // Run the lookup
                    if (result == null) return (false, "No user with that username."); // Unknown recipient
                    targetUserId = Convert.ToInt32(result); // Convert the DB value to an int
                }

                if (!HasPermission(conn, fileId, targetUserId)) // Nothing to revoke if they were never shared with
                    return (false, "That user doesn't have access to this file.");

                using (var cmd = new SQLiteCommand( // Revoke access by deleting the permissions row
                    "DELETE FROM permissions WHERE file_id=@fid AND user_id=@uid", conn))
                {
                    cmd.Parameters.AddWithValue("@fid", fileId); // Bind the file id
                    cmd.Parameters.AddWithValue("@uid", targetUserId); // Bind the recipient's user id
                    cmd.ExecuteNonQuery(); // Perform the delete
                }

                LogEvent(conn, ownerId, GetUsername(conn, ownerId), $"FILE_UNSHARED_WITH:{targetUsername}", fileId); // Audit-log the unshare
                return (true, $"Unshared with {targetUsername}."); // Report success back to the caller
            }
        }//

        private static bool HasPermission(SQLiteConnection conn, int fileId, int userId)
        {
            using (var cmd = new SQLiteCommand(
                "SELECT COUNT(*) FROM permissions WHERE file_id=@fid AND user_id=@uid", conn))
            {
                cmd.Parameters.AddWithValue("@fid", fileId);
                cmd.Parameters.AddWithValue("@uid", userId);
                return Convert.ToInt64(cmd.ExecuteScalar()) > 0;
            }
        }

        private static byte[] Encrypt(byte[] data, byte[] key, byte[] iv)
        {
            using var aes = Aes.Create();
            aes.Key = key; aes.IV = iv;
            using var encryptor = aes.CreateEncryptor();
            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                cs.Write(data, 0, data.Length);
            return ms.ToArray();
        }

        private static byte[] Decrypt(byte[] data, byte[] key, byte[] iv)
        {
            using var aes = Aes.Create();
            aes.Key = key; aes.IV = iv;
            using var decryptor = aes.CreateDecryptor();
            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Write))
                cs.Write(data, 0, data.Length);
            return ms.ToArray();
        }

        private static void LogEvent(SQLiteConnection conn, int? userId, string username, string action, int? fileId)
        {
            const string sql = @"INSERT INTO access_log (user_id, username, action, file_id)
                              VALUES (@userId, @username, @action, @fileId);";
            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@userId", (object)userId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@username", (object)username ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@action", action);
            cmd.Parameters.AddWithValue("@fileId", (object)fileId ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        private static string GetUsername(SQLiteConnection conn, int userId)
        {
            using var cmd = new SQLiteCommand("SELECT username FROM users WHERE user_id=@id", conn);
            cmd.Parameters.AddWithValue("@id", userId);
            return cmd.ExecuteScalar()?.ToString();
        }
    }

    static class NetworkHelper
    {
        public static async Task SendMessageAsync(NetworkStream stream, byte[] payload)
        {
            byte[] lengthPrefix = BitConverter.GetBytes(payload.Length);
            await stream.WriteAsync(lengthPrefix, 0, 4);
            await stream.WriteAsync(payload, 0, payload.Length);
        }

        public static Task SendTextAsync(NetworkStream stream, string text)
        {
            byte[] payload = Encoding.UTF8.GetBytes(text);
            return SendMessageAsync(stream, payload);
        }

        public static async Task<byte[]> ReceiveMessageAsync(NetworkStream stream)
        {
            byte[] lengthBuffer = await ReadExactAsync(stream, 4);
            int length = BitConverter.ToInt32(lengthBuffer, 0);

            if (length < 0 || length > 100_000_000) 
                throw new InvalidDataException($"Invalid frame length: {length}");

            return await ReadExactAsync(stream, length);
        }

        public static async Task<string> ReceiveTextAsync(NetworkStream stream)
        {
            byte[] payload = await ReceiveMessageAsync(stream);
            return Encoding.UTF8.GetString(payload);
        }

        private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int count)
        {
            byte[] buffer = new byte[count];
            int totalRead = 0;

            while (totalRead < count)
            {
                int bytesRead = await stream.ReadAsync(buffer, totalRead, count - totalRead);
                if (bytesRead == 0)
                    throw new IOException("Connection closed before expected data was received.");

                totalRead += bytesRead;
            }

            return buffer;
        }
    }

    static class Db
    {
        public static void initialize(string connectionString, string dbFileName)
        {
            string dbPath = Path.Combine(Application.StartupPath, dbFileName);

            if (!File.Exists(dbPath))
            {
                SQLiteConnection.CreateFile(dbPath);
            }

            const string createTablesQuery = @"
                CREATE TABLE IF NOT EXISTS users (
                    user_id INTEGER PRIMARY KEY AUTOINCREMENT,
                    username TEXT NOT NULL UNIQUE,
                    hashedPassword TEXT NOT NULL,
                    salt TEXT NOT NULL,
                    failed_attempts INTEGER NOT NULL DEFAULT 0,
                    locked_until    TEXT, 
                    is_locked       INTEGER NOT NULL DEFAULT 0,
                    created_at      TEXT NOT NULL DEFAULT (datetime('now'))
                );

                CREATE TABLE IF NOT EXISTS files (
                     file_id         INTEGER PRIMARY KEY AUTOINCREMENT,
                     owner_id        INTEGER NOT NULL,
                     file_name       TEXT NOT NULL,  
                     stored_path     TEXT NOT NULL,    
                     encryption_key  TEXT NOT NULL,    
                     file_size       INTEGER NOT NULL, 
                     uploaded_at     TEXT NOT NULL DEFAULT (datetime('now')),
                     FOREIGN KEY (owner_id) REFERENCES users(user_id) ON DELETE CASCADE
                );

                CREATE TABLE  IF NOT EXISTS permissions (
                permission_id   INTEGER PRIMARY KEY AUTOINCREMENT,
                file_id         INTEGER NOT NULL,
                user_id         INTEGER NOT NULL,  
                granted_at      TEXT NOT NULL DEFAULT (datetime('now')),
                FOREIGN KEY (file_id) REFERENCES files(file_id) ON DELETE CASCADE,
                FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE,
                UNIQUE (file_id, user_id)         
                );

                CREATE TABLE IF NOT EXISTS access_log (
                    log_id          INTEGER PRIMARY KEY AUTOINCREMENT,
                    user_id         INTEGER,   
                    username Text NOT NULL,
                    action          TEXT NOT NULL,  
                    file_id         INTEGER,        
                    timestamp       TEXT NOT NULL DEFAULT (datetime('now')),
                    FOREIGN KEY (user_id) REFERENCES users(user_id),
                    FOREIGN KEY (file_id) REFERENCES files(file_id)
                );";

            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();

                using (var pragmaCommand = new SQLiteCommand("PRAGMA foreign_keys = ON;", connection))
                    pragmaCommand.ExecuteNonQuery();

                using (var command = new SQLiteCommand(createTablesQuery, connection))
                    command.ExecuteNonQuery();
            }
        }
    }
}

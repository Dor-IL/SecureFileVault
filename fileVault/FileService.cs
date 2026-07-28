using System.Data.SQLite;
using System.Security.Cryptography;

namespace fileVault
{
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
            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();
                using (var pragma = new SQLiteCommand("PRAGMA foreign_keys = ON;", conn))
                    pragma.ExecuteNonQuery();

                int actualOwnerId;

                using (var cmd = new SQLiteCommand("SELECT owner_id FROM files WHERE file_id = @fid", conn))
                {
                    cmd.Parameters.AddWithValue("@fid", fileId);
                    var result = cmd.ExecuteScalar();
                    if (result == null) return (false, "File not found.");
                    actualOwnerId = Convert.ToInt32(result);
                }

                if (actualOwnerId != ownerId)
                {
                    LogEvent(conn, ownerId, GetUsername(conn, ownerId), "SHARE_DENIED", fileId);
                    return (false, "Only the file owner can share this file.");
                }

                int targetUserId;

                using (var cmd = new SQLiteCommand("SELECT user_id FROM users WHERE username = @uname", conn))
                {
                    cmd.Parameters.AddWithValue("@uname", targetUsername);
                    var result = cmd.ExecuteScalar();
                    if (result == null) return (false, "No user with that username.");
                    targetUserId = Convert.ToInt32(result);
                }

                if (targetUserId == ownerId)
                    return (false, "You already own this file.");

                using (var cmd = new SQLiteCommand(
                    "INSERT OR IGNORE INTO permissions (file_id, user_id) VALUES (@fid, @uid)", conn))
                {
                    cmd.Parameters.AddWithValue("@fid", fileId);
                    cmd.Parameters.AddWithValue("@uid", targetUserId);
                    cmd.ExecuteNonQuery();
                }

                LogEvent(conn, ownerId, GetUsername(conn, ownerId), $"FILE_SHARED_WITH:{targetUsername}", fileId);
                return (true, $"Shared with {targetUsername}.");
            }
        }

        public static (bool success, string message) UnshareFile(
            string connectionString, int fileId, int ownerId, string targetUsername)
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();
                using (var pragma = new SQLiteCommand("PRAGMA foreign_keys = ON;", conn))
                    pragma.ExecuteNonQuery();

                int actualOwnerId;

                using (var cmd = new SQLiteCommand("SELECT owner_id FROM files WHERE file_id = @fid", conn))
                {
                    cmd.Parameters.AddWithValue("@fid", fileId);
                    var result = cmd.ExecuteScalar();
                    if (result == null) return (false, "File not found.");
                    actualOwnerId = Convert.ToInt32(result);
                }

                if (actualOwnerId != ownerId)
                {
                    LogEvent(conn, ownerId, GetUsername(conn, ownerId), "UNSHARE_DENIED", fileId);
                    return (false, "Only the file owner can unshare this file.");
                }

                int targetUserId;

                using (var cmd = new SQLiteCommand("SELECT user_id FROM users WHERE username = @uname", conn))
                {
                    cmd.Parameters.AddWithValue("@uname", targetUsername);
                    var result = cmd.ExecuteScalar();
                    if (result == null) return (false, "No user with that username.");
                    targetUserId = Convert.ToInt32(result);
                }

                if (!HasPermission(conn, fileId, targetUserId))
                    return (false, "That user doesn't have access to this file.");

                using (var cmd = new SQLiteCommand(
                    "DELETE FROM permissions WHERE file_id=@fid AND user_id=@uid", conn))
                {
                    cmd.Parameters.AddWithValue("@fid", fileId);
                    cmd.Parameters.AddWithValue("@uid", targetUserId);
                    cmd.ExecuteNonQuery();
                }

                LogEvent(conn, ownerId, GetUsername(conn, ownerId), $"FILE_UNSHARED_WITH:{targetUsername}", fileId);
                return (true, $"Unshared with {targetUsername}.");
            }
        }

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
            aes.Key = key;
            aes.IV = iv;
            using var encryptor = aes.CreateEncryptor();
            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                cs.Write(data, 0, data.Length);
            return ms.ToArray();
        }

        private static byte[] Decrypt(byte[] data, byte[] key, byte[] iv)
        {
            using var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
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
}

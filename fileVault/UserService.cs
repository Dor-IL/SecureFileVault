using System.Data.SQLite;
using System.Globalization;

namespace fileVault
{
    static class UserService
    {
        private const int MaxFailedAttempts = 5;
        private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);

        public const int MaxUsernameLength = 32;
        public const int MaxPasswordLength = 64;

        public static bool RegisterUser(string connectionString, string username, string password, out string error)
        {
            error = null;

            if (username.Length > MaxUsernameLength)
            {
                error = $"Username cannot exceed {MaxUsernameLength} characters.";
                return false;
            }

            if (password.Length > MaxPasswordLength)
            {
                error = $"Password cannot exceed {MaxPasswordLength} characters.";
                return false;
            }

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
                            Db.LogEvent(conn, null, username, "LOGIN_FAILED", null, null);
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
                            Db.LogEvent(conn, id, username, "LOGIN_FAILED", null, null);
                            return LoginResult.AccountLocked;
                        }

                        if (lockedUntilObj != DBNull.Value)
                        {
                            DateTime lockedUntil = DateTime.Parse(
                                lockedUntilObj.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                            if (DateTime.UtcNow < lockedUntil)
                            {
                                reader.Close();
                                Db.LogEvent(conn, id, username, "LOGIN_FAILED", null, null);
                                return LoginResult.AccountLocked;
                            }
                        }

                        reader.Close();

                        bool ok = PasswordHasher.VerifyPassword(password, salt, storedHash);

                        if (ok)
                        {
                            ResetFailedAttempts(conn, id);
                            Db.LogEvent(conn, id, username, "LOGIN_SUCCESS", null, null);
                            userId = id;
                            return LoginResult.Success;
                        }
                        else
                        {
                            RegisterFailedAttempt(conn, id, failedAttempts + 1);
                            Db.LogEvent(conn, id, username, "LOGIN_FAILED", null, null);
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

                Db.LogEvent(conn, userId, Db.GetUsername(conn, userId), "ADMIN_LOCK", null, null, isAdminAction: true);
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

                Db.LogEvent(conn, userId, Db.GetUsername(conn, userId), "ADMIN_UNLOCK", null, null, isAdminAction: true);
            }
        }

        public static AccountState GetAccountState(string connectionString, int userId)
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();

                const string sql = "SELECT is_locked FROM users WHERE user_id = @id;";
                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@id", userId);
                    object result = cmd.ExecuteScalar();
                    if (result == null) return AccountState.NotFound;
                    return Convert.ToInt32(result) == 1 ? AccountState.Locked : AccountState.Active;
                }
            }
        }

        public static List<string> SearchUsernames(string connectionString, string prefix, int excludeUserId, int maxResults = 5)
        {
            var results = new List<string>();
            if (string.IsNullOrWhiteSpace(prefix)) return results;

            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();

                const string sql = @"
                    SELECT username FROM users
                    WHERE username LIKE @pattern ESCAPE '\'
                      AND user_id != @excludeId
                    ORDER BY username
                    LIMIT @max;";

                using (var cmd = new SQLiteCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@pattern", EscapeLikePrefix(prefix));
                    cmd.Parameters.AddWithValue("@excludeId", excludeUserId);
                    cmd.Parameters.AddWithValue("@max", maxResults);

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            results.Add(reader.GetString(0));
                    }
                }
            }

            return results;
        }

        private static string EscapeLikePrefix(string prefix)
        {
            return prefix.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_") + "%";
        }

        public static void DeleteUser(string connectionString, int userId)
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();
                using (var pragma = new SQLiteCommand("PRAGMA foreign_keys = ON;", conn))
                    pragma.ExecuteNonQuery();

                string deletedUsername = Db.GetUsername(conn, userId);

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

                        Db.LogEvent(conn, null, deletedUsername, "USER_DELETED", null, null, isAdminAction: true);

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

        public static bool DeleteFile(string connectionString, int fileId, int? requestingUserId = null, bool isAdminAction = false)
        {
            using (var conn = new SQLiteConnection(connectionString))
            {
                conn.Open();
                using (var pragma = new SQLiteCommand("PRAGMA foreign_keys = ON;", conn))
                    pragma.ExecuteNonQuery();

                string storedPath = null;
                string fileName = null;
                int ownerId = -1;

                using (var cmd = new SQLiteCommand(
                    "SELECT stored_path, owner_id, file_name FROM files WHERE file_id = @id", conn))
                {
                    cmd.Parameters.AddWithValue("@id", fileId);
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (!reader.Read()) return false;
                        storedPath = reader.GetString(0);
                        ownerId = reader.GetInt32(1);
                        fileName = reader.GetString(2);
                    }
                }

                if (requestingUserId.HasValue && ownerId != requestingUserId.Value)
                {
                    using (var cmd = new SQLiteCommand(
                        "DELETE FROM permissions WHERE file_id = @id AND user_id = @uid", conn))
                    {
                        cmd.Parameters.AddWithValue("@id", fileId);
                        cmd.Parameters.AddWithValue("@uid", requestingUserId.Value);
                        int rowsAffected = cmd.ExecuteNonQuery();

                        if (rowsAffected == 0)
                        {
                            Db.LogEvent(conn, requestingUserId, Db.GetUsername(conn, requestingUserId.Value), "DELETE_DENIED", fileId, fileName, isAdminAction);
                            return false;
                        }
                    }

                    Db.LogEvent(conn, requestingUserId, Db.GetUsername(conn, requestingUserId.Value), "SHARE_REMOVED_SELF", fileId, fileName, isAdminAction);
                    return true;
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

                Db.LogEvent(conn, ownerId, Db.GetUsername(conn, ownerId), "FILE_DELETED", null, fileName, isAdminAction);

                try { if (File.Exists(storedPath)) File.Delete(storedPath); } catch { }

                return true;
            }
        }
    }
}

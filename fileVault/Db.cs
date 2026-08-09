using System.Data.SQLite;

namespace fileVault
{
    static class Db
    {
        public static void Initialize(string connectionString, string dbFileName)
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

                CREATE TABLE IF NOT EXISTS permissions (
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
                    actor           TEXT NOT NULL DEFAULT 'USER',
                    timestamp       TEXT NOT NULL DEFAULT (datetime('now')),
                    FOREIGN KEY (user_id) REFERENCES users(user_id),
                    FOREIGN KEY (file_id) REFERENCES files(file_id)
                );";

            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();

                using (var pragmaCommand = new SQLiteCommand("PRAGMA foreign_keys = ON;", connection))
                    pragmaCommand.ExecuteNonQuery();

                using (var walCommand = new SQLiteCommand("PRAGMA journal_mode = WAL;", connection))
                    walCommand.ExecuteNonQuery();

                using (var syncCommand = new SQLiteCommand("PRAGMA synchronous = NORMAL;", connection))
                    syncCommand.ExecuteNonQuery();

                using (var command = new SQLiteCommand(createTablesQuery, connection))
                    command.ExecuteNonQuery();

                AddColumnIfMissing(connection, "access_log", "actor", "TEXT NOT NULL DEFAULT 'USER'");
            }
        }

        private static void AddColumnIfMissing(SQLiteConnection connection, string table, string column, string definition)
        {
            using (var pragma = new SQLiteCommand($"PRAGMA table_info({table});", connection))
            using (var reader = pragma.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (string.Equals(reader["name"].ToString(), column, StringComparison.OrdinalIgnoreCase))
                        return;
                }
            }

            using (var alter = new SQLiteCommand($"ALTER TABLE {table} ADD COLUMN {column} {definition};", connection))
                alter.ExecuteNonQuery();
        }

        public static void LogEvent(SQLiteConnection conn, int? userId, string username, string action, int? fileId, bool isAdminAction = false)
        {
            const string sql = @"INSERT INTO access_log (user_id, username, action, file_id, actor)
                              VALUES (@userId, @username, @action, @fileId, @actor);";
            using var cmd = new SQLiteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@userId", (object)userId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@username", (object)username ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@action", action);
            cmd.Parameters.AddWithValue("@fileId", (object)fileId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@actor", isAdminAction ? "ADMIN" : "USER");
            cmd.ExecuteNonQuery();
        }

        public static string GetUsername(SQLiteConnection conn, int userId)
        {
            using var cmd = new SQLiteCommand("SELECT username FROM users WHERE user_id = @id", conn);
            cmd.Parameters.AddWithValue("@id", userId);
            return cmd.ExecuteScalar()?.ToString();
        }
    }
}

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
                    timestamp       TEXT NOT NULL DEFAULT (datetime('now')),
                    FOREIGN KEY (user_id) REFERENCES users(user_id),
                    FOREIGN KEY (file_id) REFERENCES files(file_id)
                );";

            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();

                using (var pragmaCommand = new SQLiteCommand("PRAGMA foreign_keys = ON;", connection))
                    pragmaCommand.ExecuteNonQuery();

                // Perf: WAL lets readers (grid refreshes) run concurrently with writers (uploads/logs)
                // instead of blocking on the single database-wide lock used by the default journal mode.
                // The setting is stored in the database file itself, so it only needs to be applied once here.
                using (var walCommand = new SQLiteCommand("PRAGMA journal_mode = WAL;", connection))
                    walCommand.ExecuteNonQuery();

                // Perf: NORMAL is safe under WAL (still crash-consistent) and avoids an fsync on every
                // transaction commit, which is the dominant cost of small, frequent writes like access-log inserts.
                using (var syncCommand = new SQLiteCommand("PRAGMA synchronous = NORMAL;", connection))
                    syncCommand.ExecuteNonQuery();

                using (var command = new SQLiteCommand(createTablesQuery, connection))
                    command.ExecuteNonQuery();
            }
        }
    }
}

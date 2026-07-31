using System.Data;
using System.Data.SQLite;

namespace fileVault
{
    static class GridDataLoader
    {
        public static void Load(DataGridView grid, string sql, params SQLiteParameter[] parameters)
        {
            using var connection = new SQLiteConnection(LoginRegister.ConnectionString);
            using var command = new SQLiteCommand(sql, connection);
            if (parameters != null) command.Parameters.AddRange(parameters);

            using var adapter = new SQLiteDataAdapter(command);
            var table = new DataTable();
            adapter.Fill(table);

            grid.DataSource = table;
        }
    }
}

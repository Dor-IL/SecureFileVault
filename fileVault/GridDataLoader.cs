using System.Data;
using System.Data.SQLite;
using System.Runtime.InteropServices;

namespace fileVault
{
    static class GridDataLoader
    {
        private const int WM_SETREDRAW = 0x000B;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, bool wParam, int lParam);

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

        public static void LoadPreservingSelection(DataGridView grid, string keyColumn, string sql, params SQLiteParameter[] parameters)
        {
            object selectedKey = grid.Columns.Contains(keyColumn) && grid.CurrentRow != null
                ? grid.CurrentRow.Cells[keyColumn].Value
                : null;

            SuspendedRedraw(grid, () =>
            {
                Load(grid, sql, parameters);
                Reselect(grid, keyColumn, selectedKey);
            });
        }

        public static void Reselect(DataGridView grid, string keyColumn, object key)
        {
            if (key == null || !grid.Columns.Contains(keyColumn)) return;

            foreach (DataGridViewRow row in grid.Rows)
            {
                if (key.Equals(row.Cells[keyColumn].Value))
                {
                    grid.CurrentCell = row.Cells[0];
                    break;
                }
            }
        }

        public static void SuspendedRedraw(DataGridView grid, Action action)
        {
            bool suspend = grid.IsHandleCreated;
            if (suspend) SendMessage(grid.Handle, WM_SETREDRAW, false, 0);
            try
            {
                action();
            }
            finally
            {
                if (suspend)
                {
                    SendMessage(grid.Handle, WM_SETREDRAW, true, 0);
                    grid.Invalidate();
                }
            }
        }
    }
}

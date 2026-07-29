namespace fileVault
{
    // Extracted from Admin.cs/Client.cs, which each had an identical StyleGrid method that only
    // differed by their three theme colors (Admin = maroon, Client = green). Consolidated here to
    // remove the duplication; callers pass their own theme colors so the rendered result is unchanged.
    static class GridStyler
    {
        public static void Style(DataGridView grid, Color baseColor, Color highlightColor, Color lineColor, bool allowHighlight = true)
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

            Color normalFore = Color.White;
            Color selectedBack = allowHighlight ? highlightColor : baseColor;
            Color selectedFore = Color.White;

            grid.DefaultCellStyle.SelectionBackColor = selectedBack;
            grid.DefaultCellStyle.SelectionForeColor = selectedFore;
            grid.DefaultCellStyle.BackColor = baseColor;
            grid.DefaultCellStyle.ForeColor = normalFore;

            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = baseColor;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = normalFore;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font(grid.Font, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = baseColor;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = normalFore;

            grid.GridColor = lineColor;
            grid.BackgroundColor = lineColor;

            if (!allowHighlight)
            {
                grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            }
        }
    }
}

using Eto.Forms;

namespace DSCproto.EtoApp;

/// <summary>
/// A right-click menu on a table (2026-11-17), as the Avalonia window has: copy the cell's value, or the row with its
/// column headings as two tab-separated lines, which paste into a spreadsheet as a heading row and a value row. What
/// is copied is the text the table shows; the CSV export has full precision.
/// </summary>
internal static class GridCopy
{
    public static void Attach(GridView grid) => grid.CellClick += (_, e) =>
    {
        if (e.Buttons != MouseButtons.Alternate || e.Item is null || e.GridColumn is null) return;
        var columns = grid.Columns.Where(c => c.Visible).ToList();
        string value = TextOf(e.GridColumn.DataCell, e.Item);
        string heading = e.GridColumn.HeaderText ?? "";

        var copyValue = new ButtonMenuItem { Text = heading.Length > 0 ? $"Copy {heading}: {Shorten(value)}" : $"Copy value: {Shorten(value)}" };
        copyValue.Click += (_, _) => Copy(value);
        var copyRow = new ButtonMenuItem { Text = "Copy row, with headings" };
        object item = e.Item;
        copyRow.Click += (_, _) => Copy(
            string.Join('\t', columns.Select(c => Clean(c.HeaderText ?? ""))) + Environment.NewLine
            + string.Join('\t', columns.Select(c => Clean(TextOf(c.DataCell, item)))));
        new ContextMenu(copyValue, copyRow).Show(grid);
    };

    private static void Copy(string text)
    {
        using var clipboard = new Clipboard();
        clipboard.Text = text;
    }

    /// <summary>The text a cell shows, through the cell's own binding.</summary>
    private static string TextOf(Cell? cell, object item) => cell switch
    {
        TextBoxCell t => t.Binding?.GetValue(item) ?? "",
        CheckBoxCell c => c.Binding?.GetValue(item) == true ? "Yes" : "No",
        ComboBoxCell c => c.Binding?.GetValue(item)?.ToString() ?? "",
        _ => ""
    };

    private static string Clean(string s) => s.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');

    private static string Shorten(string s) => s.Length <= 40 ? s : s[..37] + "...";
}

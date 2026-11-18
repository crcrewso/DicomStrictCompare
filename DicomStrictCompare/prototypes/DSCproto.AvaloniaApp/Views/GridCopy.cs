using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace DSCproto.AvaloniaApp.Views;

/// <summary>
/// A right-click menu on every table in a window (2026-11-17): copy the cell's value, or the row with its column
/// headings as two tab-separated lines, which paste into a spreadsheet as a heading row and a value row. What is
/// copied is the text the table shows, so a rounded number is copied rounded; the CSV export has full precision.
/// </summary>
internal static class GridCopy
{
    /// <summary>Handles the context request for every <see cref="DataGrid"/> in <paramref name="root"/>, current and future.</summary>
    public static void Attach(Control root) =>
        root.AddHandler(Control.ContextRequestedEvent, OnContextRequested, RoutingStrategies.Bubble);

    private static void OnContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        // A cell being edited holds a TextBox, which handles the request with its own menu (cut, copy, paste) first.
        if (e.Source is not Visual source) return;
        var cell = source as DataGridCell ?? source.FindAncestorOfType<DataGridCell>();
        var row = cell?.FindAncestorOfType<DataGridRow>();
        var grid = row?.FindAncestorOfType<DataGrid>();
        if (cell is null || row is null || grid is null) return;

        var columns = grid.Columns.Where(c => c.IsVisible).OrderBy(c => c.DisplayIndex).ToList();
        var column = columns.FirstOrDefault(c => ReferenceEquals(c.GetCellContent(row), cell.Content));
        string value = TextOf(cell.Content);
        string heading = column is null ? "" : HeadingOf(column.Header);

        // Text blocks as headers, so an underscore in a file name isn't taken as an access key.
        var copyValue = new MenuItem { Header = new TextBlock { Text = heading.Length > 0 ? $"Copy {heading}: {Shorten(value)}" : $"Copy value: {Shorten(value)}" } };
        copyValue.Click += (_, _) => _ = CopyAsync(grid, value);
        var copyRow = new MenuItem { Header = new TextBlock { Text = "Copy row, with headings" } };
        copyRow.Click += (_, _) => _ = CopyAsync(grid,
            string.Join('\t', columns.Select(c => Clean(HeadingOf(c.Header)))) + Environment.NewLine
            + string.Join('\t', columns.Select(c => Clean(TextOf(c.GetCellContent(row))))));

        var menu = new ContextMenu { ItemsSource = new[] { copyValue, copyRow } };
        menu.Open(cell);
        e.Handled = true;
    }

    private static async Task CopyAsync(Visual anchor, string text)
    {
        if (TopLevel.GetTopLevel(anchor)?.Clipboard is { } clipboard) await clipboard.SetTextAsync(text);
    }

    /// <summary>The text a cell shows: a text block's text, a check box's state, else the text blocks inside it.</summary>
    internal static string TextOf(object? content) => content switch
    {
        null => "",
        string s => s,
        TextBlock t => t.Text ?? "",
        CheckBox c => c.IsChecked == true ? "Yes" : "No",
        TextBox t => t.Text ?? "",
        ContentControl c when c.Content is not Visual => c.Content?.ToString() ?? "",
        Visual v => string.Join(" ", v.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).Where(t => !string.IsNullOrEmpty(t))),
        var o => o.ToString() ?? ""
    };

    /// <summary>A column's heading: plain text, or a text block when the heading carries a hover tip.</summary>
    private static string HeadingOf(object? header) => header switch
    {
        string s => s,
        TextBlock t => t.Text ?? "",
        _ => header?.ToString() ?? ""
    };

    private static string Clean(string s) => s.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');

    private static string Shorten(string s) => s.Length <= 40 ? s : s[..37] + "...";
}

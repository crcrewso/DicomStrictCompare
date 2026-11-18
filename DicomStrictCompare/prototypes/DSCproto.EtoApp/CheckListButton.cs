using DSCprototypes.Shared.ViewModels;
using Eto.Drawing;
using Eto.Forms;

namespace DSCproto.EtoApp;

/// <summary>
/// A <see cref="CheckList"/> as a button that says what is ticked and opens a small window of check boxes with All and
/// None (crcrewso, 2026-11-17), as the Avalonia window's dropdown does. Eto has no dropdown of check boxes, and a menu of
/// check items closes at each click, so the choices open in their own window, closed by Done or Escape.
/// </summary>
internal sealed class CheckListButton : Button
{
    public CheckList List { get; }

    public CheckListButton(CheckList list)
    {
        List = list;
        Text = list.Summary;
        MinimumSize = new Size(170, -1);
        list.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(CheckList.Summary)) Text = list.Summary; };
        Click += (_, _) => Open();
    }

    private void Open()
    {
        var dialog = new Dialog { Title = "Choose", Resizable = true, MinimumSize = new Size(260, 200) };
        var boxes = new StackLayout { Spacing = 4, Padding = new Padding(4) };
        var pairs = new List<(CheckBox Box, CheckChoice Item)>();
        foreach (var item in List.Items)
        {
            var box = new CheckBox { Text = item.Label, Checked = item.IsChecked };
            box.CheckedChanged += (_, _) => item.IsChecked = box.Checked == true;
            pairs.Add((box, item));
            boxes.Items.Add(box);
        }
        void Sync() { foreach (var (box, item) in pairs) box.Checked = item.IsChecked; }
        var all = new Button { Text = "All" };
        all.Click += (_, _) => { List.AllCommand.Execute(null); Sync(); };
        var none = new Button { Text = "None" };
        none.Click += (_, _) => { List.NoneCommand.Execute(null); Sync(); };
        var done = new Button { Text = "Done" };
        done.Click += (_, _) => dialog.Close();
        dialog.DefaultButton = done;
        dialog.AbortButton = done;
        dialog.Content = new DynamicLayout(
            new TableLayout(new TableRow(all, none, null)) { Spacing = new Size(6, 6) },
            new Scrollable { Content = boxes, Height = Math.Min(480, 30 * Math.Max(1, pairs.Count) + 12), Border = BorderType.None },
            new TableLayout(new TableRow(null, done)))
        { Padding = new Padding(10), DefaultSpacing = new Size(6, 8) };
        dialog.ShowModal(this);
    }
}

using DSCprototypes.Shared.ViewModels;
using Eto.Drawing;
using Eto.Forms;

namespace DSCproto.EtoApp;

/// <summary>
/// The header-fix form in Eto's idiom, the controls written into the shared <see cref="HeaderFixViewModel"/> on Save;
/// the same fields and wording as the Avalonia window's (crcrewso, 2026-11-17: "fix the metadata per profile").
/// </summary>
internal sealed class HeaderFixDialog : Dialog<HeaderFixOutcome>
{
    public HeaderFixDialog(HeaderFixViewModel form)
    {
        Title = "Fix a scan's header";
        Resizable = false;
        Result = HeaderFixOutcome.Cancel;

        var energy = new TextBox { Text = form.Energy, Width = 80 };
        var fff = new CheckBox { Text = "FFF", Checked = form.Fff, Visible = !form.IsElectron };
        var cross = new TextBox { Text = form.FieldCrossplaneMm, Width = 80, ToolTip = "The field's size along the tank's crossplane axis, at the isocentre: 200 for a 20 cm field" };
        var inplane = new TextBox { Text = form.FieldInplaneMm, Width = 80 };
        var wedge = new DropDown { DataStore = HeaderFixViewModel.WedgeAngles, SelectedValue = form.WedgeAngle };
        var direction = new DropDown { DataStore = HeaderFixViewModel.Directions, SelectedValue = form.WedgeDirection,
            ToolTip = "As PTW writes it on the scan (WEDGE=15 out); pairing reads Eclipse's orientation 0 as IN and 180 as OUT" };
        var note = new TextArea { Text = form.Note, Height = 54, Wrap = true };
        var wholeFile = new CheckBox { Text = $"Every scan in this file ({form.ScansInFile})", Checked = form.WholeFile, Visible = form.CanFixWholeFile };
        var problem = new Label { TextColor = Color.FromArgb(0xC0, 0x39, 0x2B), Wrap = WrapMode.Word };

        var save = new Button { Text = "Save" };
        save.Click += (_, _) =>
        {
            form.Energy = energy.Text;
            form.Fff = fff.Checked == true;
            form.FieldCrossplaneMm = cross.Text;
            form.FieldInplaneMm = inplane.Text;
            form.WedgeAngle = wedge.SelectedValue as string ?? "open";
            form.WedgeDirection = direction.SelectedValue as string ?? "IN";
            form.Note = note.Text;
            form.WholeFile = wholeFile.Checked == true;
            if (form.ToFix() is null) { problem.Text = form.Problem; return; }
            Close(HeaderFixOutcome.Save);
        };
        var cancel = new Button { Text = "Cancel" };
        cancel.Click += (_, _) => Close(HeaderFixOutcome.Cancel);
        var remove = new Button { Text = "Remove fix", Visible = form.HasFix };
        remove.Click += (_, _) => Close(HeaderFixOutcome.Remove);
        DefaultButton = save;
        AbortButton = cancel;

        Label L(string text) => new() { Text = text, VerticalAlignment = VerticalAlignment.Center, Width = 170 };
        var layout = new DynamicLayout { Padding = new Padding(16), DefaultSpacing = new Size(8, 10), Width = 560 };
        layout.AddRow(new Label { Text = $"{form.File}, scan {form.ScanNumber}", Font = SystemFonts.Bold(), Wrap = WrapMode.Word });
        layout.AddRow(new Label { Text = form.FileSays, Wrap = WrapMode.Word });
        if (form.HasCheck) layout.AddRow(new Label { Text = form.CheckSays, Wrap = WrapMode.Word, TextColor = Color.FromArgb(0xC0, 0x39, 0x2B) });
        layout.AddRow(new TableLayout(
            new TableRow(L("Energy (MV or MeV)"), new TableLayout(new TableRow(energy, fff, null)) { Spacing = new Size(12, 0) }),
            new TableRow(L("Field crossplane (mm)"), new TableLayout(new TableRow(cross, null))),
            new TableRow(L("Field inplane (mm)"), new TableLayout(new TableRow(inplane, null))),
            new TableRow(L("Wedge"), new TableLayout(new TableRow(wedge, direction, null)) { Spacing = new Size(8, 0) }),
            new TableRow(L("Why (kept with the fix)"), note),
            new TableRow(null, wholeFile)) { Spacing = new Size(8, 8) });
        layout.AddRow(problem);
        layout.AddRow(new Label { Text = $"Saved in {form.FixesPath}. The scan file isn't changed. Run again to apply.", Wrap = WrapMode.Word, TextColor = Colors.Gray });
        layout.AddRow(new TableLayout(new TableRow(null, remove, cancel, save)) { Spacing = new Size(8, 0) });
        Content = layout;
    }
}

using Eto.Forms;

namespace DSCproto.EtoApp;

internal static class Program
{
    // Eto picks the platform at run time from whichever Eto.Platform.* assembly is present:
    // WPF here on Windows, GTK on Linux, Cocoa on macOS. The form code is identical on all three.
    [STAThread]
    public static void Main(string[] args)
    {
        var app = new Application(Eto.Platform.Detect);
        // --viewer SOURCE TARGET: the difference viewer alone, opened on that pair.
        int at = Array.FindIndex(args, a => string.Equals(a, "--viewer", StringComparison.OrdinalIgnoreCase));
        if (at >= 0 && at + 2 < args.Length)
        {
            var page = new ViewerPage();
            var form = new Form { Title = "DicomStrictCompare: difference viewer", ClientSize = new Eto.Drawing.Size(1400, 950), Content = page };
            form.Shown += (_, _) => _ = page.ShowAsync(args[at + 1], args[at + 2]);
            app.Run(form);
            return;
        }
        app.Run(new MainForm());
    }
}

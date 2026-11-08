using Eto.Forms;

namespace DSCproto.EtoApp;

internal static class Program
{
    // Eto picks the platform at run time from whichever Eto.Platform.* assembly is present:
    // WPF here on Windows, GTK on Linux, Cocoa on macOS. The form code is identical on all three.
    [STAThread]
    public static void Main(string[] args) =>
        new Application(Eto.Platform.Detect).Run(new MainForm());
}

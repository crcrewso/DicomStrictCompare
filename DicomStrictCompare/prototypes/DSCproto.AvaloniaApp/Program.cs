using Avalonia;

namespace DSCproto.AvaloniaApp;

internal static class Program
{
    // Avalonia's entry point is deliberately small: the framework owns the main loop, and the
    // AppBuilder is where platform detection, fonts and logging are configured.
    [STAThread]
    public static void Main(string[] args)
    {
        // The viewer's web view is WebKitGTK on Linux, which embeds only under GTK's X11 backend; Avalonia itself
        // runs on X11 (XWayland under a Wayland session), so GTK must not pick Wayland.
        if (OperatingSystem.IsLinux()) Environment.SetEnvironmentVariable("GDK_BACKEND", "x11");
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
}

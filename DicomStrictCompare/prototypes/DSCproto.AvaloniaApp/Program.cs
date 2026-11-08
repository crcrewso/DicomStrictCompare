using Avalonia;

namespace DSCproto.AvaloniaApp;

internal static class Program
{
    // Avalonia's entry point is deliberately small: the framework owns the main loop, and the
    // AppBuilder is where platform detection, fonts and logging are configured.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .WithInterFont()
        .LogToTrace();
}

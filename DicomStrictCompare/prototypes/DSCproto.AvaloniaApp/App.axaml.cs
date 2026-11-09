using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using DSCproto.AvaloniaApp.Views;
using DSCprototypes.Shared;
using DSCprototypes.Shared.ViewModels;

namespace DSCproto.AvaloniaApp;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // The view owns the view model's lifetime. The folder picker is a platform service, so the
            // view hands the view model a delegate rather than the view model reaching for the window.
            var window = new MainWindow();
            var viewModel = new MainViewModel(window.PickFolderAsync);
            var options = LaunchOptions.FromCommandLine();
            viewModel.Apply(options);
            window.DataContext = viewModel;
            window.Opened += async (_, _) =>
            {
                await viewModel.AutoRunIfRequestedAsync(options);
                if (options.ShowGraphs) window.ShowGraphs();
            };
            desktop.MainWindow = window;
        }
        base.OnFrameworkInitializationCompleted();
    }
}

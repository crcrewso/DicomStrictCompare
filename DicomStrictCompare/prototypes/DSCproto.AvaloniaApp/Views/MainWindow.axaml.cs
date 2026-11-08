using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace DSCproto.AvaloniaApp.Views;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    /// <summary>
    /// The one platform service the view model needs. Avalonia reaches native pickers through the
    /// TopLevel's StorageProvider, which is why this lives in the view and is handed down as a delegate.
    /// </summary>
    public async Task<string?> PickFolderAsync(string title, string? startFolder)
    {
        // The start folder is handed over as a storage folder, which is how Avalonia's abstraction
        // stays portable to platforms where a path is not a thing a picker can be told.
        IStorageFolder? start = startFolder is null ? null : await StorageProvider.TryGetFolderFromPathAsync(startFolder);
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = start,
        });
        return folders.Count == 1 ? folders[0].TryGetLocalPath() : null;
    }
}

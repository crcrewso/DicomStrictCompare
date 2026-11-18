using Avalonia.Controls;
using Avalonia.Interactivity;
using DSCprototypes.Shared.ViewModels;

namespace DSCproto.AvaloniaApp.Views;

/// <summary>The header-fix form; closes with what the person chose, and stays open on Save while a value can't be used.</summary>
public partial class HeaderFixWindow : Window
{
    public HeaderFixWindow() => InitializeComponent();

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        if (DataContext is HeaderFixViewModel form && form.ToFix() is null) return;   // Problem says why
        Close(HeaderFixOutcome.Save);
    }

    private void OnRemove(object? sender, RoutedEventArgs e) => Close(HeaderFixOutcome.Remove);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(HeaderFixOutcome.Cancel);
}

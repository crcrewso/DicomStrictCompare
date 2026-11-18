using Avalonia;
using Avalonia.Controls;

namespace DSCproto.AvaloniaApp.Views;

/// <summary>The criteria list and Max, bound to the window's <c>MainViewModel</c>; shown in each tab that compares doses.</summary>
public partial class CriteriaPanel : UserControl
{
    /// <summary>The panel's heading, so each tab can say where else the list is used.</summary>
    public static readonly StyledProperty<string> TitleProperty = AvaloniaProperty.Register<CriteriaPanel, string>(nameof(Title), "Criteria");

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public CriteriaPanel() => InitializeComponent();
}

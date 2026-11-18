using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DSCprototypes.Shared.ViewModels;

/// <summary>One choice in a <see cref="CheckList"/>: its label and whether it is ticked.</summary>
public sealed partial class CheckChoice : ObservableObject
{
    public CheckChoice(string label, bool isChecked)
    {
        Label = label;
        _isChecked = isChecked;
    }

    public string Label { get; }
    [ObservableProperty] private bool _isChecked;
}

/// <summary>
/// A filter of several choices, any of them ticked, shown as a dropdown of check boxes with All and None (crcrewso,
/// 2026-11-17: "dropdown list of checkboxes with select all or select none"): the scans tab's energies and field sizes.
/// <see cref="Checked"/> is null while every choice is ticked, so "all" keeps rows whatever their label.
/// </summary>
public sealed partial class CheckList : ObservableObject
{
    private readonly string _noun;
    private bool _quiet;

    /// <param name="noun">What the choices are, plural: "energies", "field sizes".</param>
    public CheckList(string noun)
    {
        _noun = noun;
        _summary = $"All {noun}";
    }

    public ObservableCollection<CheckChoice> Items { get; } = new();

    /// <summary>The dropdown's label: "All energies", "None", "6 MV, 10 MV FFF", or "3 of 9 field sizes".</summary>
    [ObservableProperty] private string _summary;

    /// <summary>Raised once per change the user makes: a tick, All or None.</summary>
    public event EventHandler? Changed;

    /// <summary>The ticked labels, or null when every one is ticked.</summary>
    public IReadOnlySet<string>? Checked =>
        Items.All(i => i.IsChecked) ? null : Items.Where(i => i.IsChecked).Select(i => i.Label).ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Replaces the choices. One that was already listed keeps its tick; a new one is ticked when every earlier choice
    /// was (or there were none), so "all" stays all as choices come and go. Doesn't raise <see cref="Changed"/>.
    /// </summary>
    public void SetChoices(IEnumerable<string> labels)
    {
        var was = Items.ToDictionary(i => i.Label, i => i.IsChecked, StringComparer.Ordinal);
        bool allWere = Items.All(i => i.IsChecked);
        foreach (var i in Items) i.PropertyChanged -= OnItemChanged;
        Items.Clear();
        foreach (string label in labels)
        {
            var item = new CheckChoice(label, was.TryGetValue(label, out bool c) ? c : allWere);
            item.PropertyChanged += OnItemChanged;
            Items.Add(item);
        }
        UpdateSummary();
    }

    [RelayCommand]
    private void All() => SetAll(true);

    [RelayCommand]
    private void None() => SetAll(false);

    private void SetAll(bool value)
    {
        _quiet = true;
        foreach (var i in Items) i.IsChecked = value;
        _quiet = false;
        UpdateSummary();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void OnItemChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_quiet || e.PropertyName != nameof(CheckChoice.IsChecked)) return;
        UpdateSummary();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateSummary()
    {
        var on = Items.Where(i => i.IsChecked).Select(i => i.Label).ToList();
        Summary = on.Count == Items.Count ? $"All {_noun}"
            : on.Count == 0 ? "None"
            : on.Count <= 2 ? string.Join(", ", on)
            : $"{on.Count} of {Items.Count} {_noun}";
    }
}

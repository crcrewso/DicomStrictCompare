# Notes for an Eto version of the Study screens

**Status:** notes kept while the Avalonia Study tab is built (crcrewso, 2026-11-14: "Avalonia is fine, carry on, but
keep notes to aide a possible Eto solution"). Nothing here is built. Companion to `comparison-studies.md`.

## What an Eto Study page can reuse unchanged

Everything below the screens is front-end free and already tested:

| Need | Type | Project |
|---|---|---|
| Sets, comparisons, depths; save and open | `Study`, `DoseSet`, `SetComparison`, `DepthSettings` | `DSClibrary/Studies` |
| Pairing rules and near misses | `PairingRules`, `LabelRule`, `PairingPreview.NearMisses` | `DSClibrary/Matching` |
| Plan check, dose warnings | `PlanCheck`, `DoseWarnings`, `DoseDescription` | `DSClibrary/Studies` |
| Preview and run | `StudyRunner.PreviewAsync` / `RunAsync`, returning `StudySession` | `DSCprototypes.Shared/Studies` |
| Results rows | `StudySession.Rows` (`StudyRow`, with `Facts`, `PassRate`, `PlanCheck`, `Warnings`) | same |
| Index tables | `IndexTable.Build(rows, comparison, criterion, rowAxis, columnAxis)` | same |
| CSV | `StudyCsv.Write(session, folder)` | same |
| Fields across sets | `StudyFieldCatalog.Build(session)` | same |
| Curves | `MultiCurves.Load` then `MultiCurves.For(doses, loaded, kind, depths, reference)` | `DSCprototypes.Shared/Graphs` |

**Text parsing is in `SetComparisonViewModel.ParseRules` and `ParseOverrides`.** Both are static and usable
without the view model: rules as `pattern -> replacement; …`, overrides as `source = target; …`.
`DepthSettings.Parse` reads "dmax, 50, 100 mm".

## Two ways to build it

1. **The Eto app's own style:** the controls are fields, the state lives in the page, and the shared types above
   do the work. That's how `MainForm.cs` and `GraphPage.cs` are written today. It means repeating, in about 300
   lines, the coordination `StudyViewModel` does: rebuild the index table when a selector changes, load curves when
   a field is chosen, and keep a load sequence so a slow load can't overwrite a newer one.
2. **Bind to `StudyViewModel`.** It's a `CommunityToolkit.Mvvm` `ObservableObject`, so it raises
   `INotifyPropertyChanged`, its lists are `ObservableCollection`s, and its commands are `IRelayCommand`s. Eto
   can bind controls to such an object. That would share the behaviour exactly, at the cost of the Eto app's
   "no view model" choice.

   *Inferred, not tried:* Eto's binding needs each property wired by hand, `Binding.Property(…)` per control, so
   the saving is in behaviour rather than in lines.

## Mapping the Avalonia tab to Eto controls

| Avalonia (MainWindow.axaml, Study tab) | Eto equivalent | Notes |
|---|---|---|
| Inner `TabControl` (Setup, Pairing, Results, Index tables, Curves, Log) | `TabControl` with `TabPage`s | |
| Sets `DataGrid`, editable | `GridView` with `TextBoxCell` columns | As the criteria grid in `MainForm.cs` does it. Seven columns: name, folder, algorithm, TPS, dose reported as, plan filter (regex), notes |
| "Dose reported as" `ComboBox` column | `ComboBoxCell` with `DataStore = Enum.GetValues<DoseReporting>()` | |
| Comparisons `DataGrid` | `GridView`; `CheckBoxCell` for "By UID" | |
| "Use the AAA / AXB label rule" button | `Button` that sets the selected row's label rules | `SetComparisonViewModel.UseAcrossAlgorithms` is one line of logic |
| Pairing `ListBox` | `ListBox`, or a read-only `TextArea` | Lines are built in `StudyViewModel.Show`; copy that loop |
| Results `DataGrid` (14 columns) | `GridView`, read-only, `TextBoxCell` with `Binding.Property((StudyRow r) => r.Facts.Energy)` and similar | |
| **Index table: columns built at run time** | `GridView` whose `Columns` are cleared and rebuilt when the table changes | The Avalonia code-behind's `BuildIndexColumns` is the model; each cell binds to `IndexRowView.Cells[i]`. Eto: `Binding.Delegate((IndexRowView r) => r.Cells[i])` |
| Two `AvaPlot`s (curves, differences) | Two `ScottPlot.Eto.EtoPlot` | Already referenced by `DSCproto.EtoApp.csproj` (ScottPlot.Eto 5.1.59). The drawing code in `MainWindow.axaml.cs` (`DrawStudy`) is ScottPlot API only and should carry over almost verbatim |
| `ProgressBar IsIndeterminate` | `ProgressBar { Indeterminate = true }` | |
| Folder picker | `SelectFolderDialog` | As `MainForm.cs` already does for source and target |

## Things to watch

- **Long work off the UI thread.** `StudyRunner` reads several 23 GB folders. In Eto, `await` the async runner
  and marshal progress the way `MainForm.cs` does: `new Progress<string>(line => Application.Instance.Invoke(...))`.
- **The Eto Windows target** (`net10.0-windows`, WPF) only builds on Windows. The `net10.0` target (Gtk) builds
  on Linux, and was checked to build after the study work (2026-11-14).
- **The Graphs tab's depths are now a setting.** `GraphViewModel.DepthsText`, with `DepthChoices` an
  `ObservableCollection` rebuilt from it. The Eto `GraphPage` still uses `BeamLines.FixedDepths` directly. To
  match, give it a text box and call `BeamLines.ProfilesAtAllDepths(lp, kind, depths)`, the overload that takes a
  depth list.

## The Max setting and the comparers (2026-11-15)

- **Max:** the Avalonia window has a "Max" `ComboBox` under the Criteria list, bound to `MainViewModel.Maximum`, with
  choices from `MaximumChoices` (`MaximumKind.GlobalMax`, `CaxMax`). An Eto form would use a `DropDown` with
  `DataStore = Enum.GetValues<MaximumKind>()`, and pass the value:
  - in `ComparisonRequest.Maximum` for a comparison;
  - as the `maximum` argument of `StudyRunner.RunAsync` for a study.
- **Max used:** both results grids have a "Max used" column (`ResultRow.MaximumUsed`, `StudyRow.MaximumUsed`),
  saying which maximum each row used and why it fell back. An Eto `GridView` needs the same column.
- **Engine, GPU float, pairs at once:** Avalonia shows these beside Max.
  - They're `MainViewModel.Engine`, `GpuFloat` and `PairsAtOnce`, and the runners build the engine with
    `DoseComparers.Create(ComparerSettings)`.
  - Eto needs a `DropDown`, a `CheckBox` (whose tooltip names the GPUs with slow double) and a `NumericStepper`.
  - A study file saves all of these (`Study.Run`), and opening one restores them through `MainViewModel.Apply`.
- **Edge check:** both results grids have an "Edge check" column (`ResultRow.EdgeCheck`, `StudyRow.EdgeCheck`).
- **Reading:** Avalonia has "Target box only", "Readers" and "Held" beside the engine, bound to
  `MainViewModel.PartialTargetReads`, `Readers` and `PairsHeld`; a study file saves them (`StudyRunSettings`). Eto
  needs a `CheckBox` and two `NumericStepper`s.
- **Applies to:** both criteria grids have it, Avalonia as a `ComboBox` and Eto as a `ComboBoxCell`, bound to
  `DtaSpec.AppliesTo` (`BeamsCompared.Both`, `Photon`, `Electron`).

## Log

- 2026-11-14: notes started. The Avalonia Study tab builds; it hasn't been tried on screen yet.
- 2026-11-14: sets gained a plan filter (`DoseSet.PlanFilter`), a regular expression on the plan label, so two sets
  can share one export folder. The Eto sets grid needs that column. `StudyRunner` reads each folder once.
- 2026-11-15: the Max dropdown and the "Max used" columns were added to the Avalonia window. Eto needs the same; see above.
- 2026-11-15: "Applies to" added to both criteria grids. The runners now load each pair once for all criteria
  (`BatchComparer.CompareMany`); rows still come out criterion by criterion.
- 2026-11-15: the reading settings (target box only, readers, held) added to the Avalonia window. Eto needs them.

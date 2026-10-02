# DicomStrictCompare

DicomStrictCompare is a tool for comparing two folders of DICOM RT Dose files in one
batch. It was written for treatment planning system (TPS) commissioning and upgrade work: you calculate
the same plans two ways (for example, an old and a new algorithm version) and want a quick, numerical
answer to "how different are these dose distributions, field by field?" without opening every pair by
hand.

While it was written for a specific use case, please don't hesitate to suggest improvements you would
like to see.

## What it does

You give it two folders:

- **Source** (reference / known good) and
- **Target** (test / unknown status).

Each folder holds RT Dose (`RD`) files **and** the RT Plan (`RP`) files they came from. The plan is
needed because a dose file on its own does not say which field it belongs to: the tool reads the plan
to turn each dose file's referenced beam number into a plan label and field name.

The tool then:

1. Finds every DICOM file in each folder (sub-folders included) and sorts them into dose and plan files.
2. Pairs each target dose with the source dose that has the same **patient ID, plan label and field
   name**. Target doses with no match are listed in an error file rather than silently skipped.
3. For each pair, and for each set of criteria you have added, counts the voxels above a dose threshold
   and how many of them fail a dose-difference / distance-to-agreement (DTA) test.
4. Writes the results to a CSV file, and optionally saves depth-dose (PDD) comparison plots as PNG.

Export doses **per field** if you want each field analysed separately.

### The comparison

Source and target are compared point by point over the region where their dose grids overlap, sampled
on the coarser of the two grids.

1. **Threshold.** A point is evaluated only if the **source** dose is at least *Threshold* % of the
   source maximum. The target dose plays no part in this, so a target that drops far below the
   reference is counted as a failure, not skipped.
2. **Dose difference.** The point passes if |target − source| is within the allowed difference:
   - **Global** (Global? ticked): *Tolerance* % of the **source maximum**. With a 100 Gy maximum and 3 %,
     a point of 12.1 Gy in the source and 15.0 Gy in the target passes (2.9 Gy ≤ 3 Gy).
   - **Local**: *Tolerance* % of the **source dose at that point**. The same point fails
     (2.9 Gy > 0.363 Gy).
3. **Distance to agreement (DTA).** If the dose test fails and a DTA distance in mm is set, the point
   still passes when the target dose lies between the lowest and highest source doses found that
   distance away along ±x, ±y and ±z. Neighbours outside the compared region are ignored. This is a
   deliberately fast approximation, not a full search of the sphere around the point.
4. Otherwise the point fails.

DTA distances are in mm. Voxel-based distances are reserved for a future feature: if a criterion is set
to voxels, the dose difference alone decides pass or fail and the DTA distance is ignored.

The result for each pair is the percentage of evaluated points that failed: 0 % means the distributions
agree everywhere under your criteria, 100 % means no point agreed.

This is the simple dose-difference / DTA approach, **not** a gamma analysis. A gamma function, which will
search the full sphere around each point, is planned. The GUI's "Gamma?" checkbox is stored but not yet
used.
<!-- TODO(Cody): add citations for the dose-difference/DTA method (the old README notes this was still needed). -->

### Criteria you can set (per row, up to 9 rows)

| Field | Meaning |
|---|---|
| Tolerance (%) | Allowed dose difference at each point |
| DTA | Distance to agreement in mm; 0 means dose difference only |
| Threshold (%) | Percentage of the source maximum below which voxels are not compared |
| Global? | Tolerance measured against the source maximum (global) instead of the local dose |
| Trim voxels | Number of voxels to remove from the edge of the overlapping volume, useful when two algorithms are known to disagree on skin dose and you want to exclude it |

Click the introduction text in the app for the same definitions.

## Assumptions and limitations

- Dose files are calculated **head first supine** and as **absolute dose**. Relative dose distributions
  have not been tested.
- The code assumes the dose volume is a phantom used in TPS commissioning. Nothing in the method should
  stop it working on patient datasets, but that is not its tested purpose.
- Source and target are compared only over the region where their dose grids overlap, at the coarser of
  the two grid resolutions.

## Requirements

- The comparison library (`DSClibrary`) and its tests run on Windows, Linux and macOS.
- The current GUI is Windows Forms, so it needs Windows 10 or later. It is due to be replaced by a
  cross-platform front end.
- [.NET 8 SDK](https://dotnet.microsoft.com/) to build, or Visual Studio 2022.
- NuGet packages restored automatically on build, including
  [EvilDICOM](http://rexcardan.github.io/Evil-DICOM/) for reading DICOM, MathNet.Numerics, and ScottPlot
  for the plots.

## Build, run and test

From the repository root:

```bash
# Library tests (any OS)
dotnet test DicomStrictCompare/DSClibraryTests/DSClibraryTests.csproj

# GUI (Windows only)
dotnet run --project DicomStrictCompare/DSCcore/DSCcore.csproj
```

`DSClibraryTests/ComparisonRulesTests.cs` checks each comparison rule above on small synthetic grids.
Several older tests are still placeholders that call `Assert.Fail()`, and three tests that compare
against Sun Nuclear results do not pass yet.

The projects are configured for the x64 platform. Opening `DicomStrictCompare/DicomStrictCompare.sln`
in Visual Studio and running `DSCcore` works too.

The tests use real RT Dose and RT Plan files of a test phantom, stored in
`DSClibraryTests/Resources/` and `DSCcoreTest/Resources/` and embedded into the test assemblies. These
make the repository large (around 1.5 GB as a clone).

## Using it

1. Click **Source** and **Target** to choose the two folders. The app shows how many dose files it
   found in each.
2. Optionally change the **Source Label** and **Target Label** used in the output, and choose a
   **Save Location** and **Save Name**.
3. Add one or more rows of criteria (Tolerance, DTA, Threshold, Global?, Trim) with **Add**.
4. Tick **Produce Dose Comparison table** and/or **Produce PDD comparison Plots**.
5. Click **Run**. Progress appears in the status line.

### Output

- `<Save Name>.csv` in the save location: one row per matched dose pair, with columns for each set of
  criteria.
- `<Save Name>-errors.txt`: target dose files that had no matching source dose.
- PNG plots of the source and target depth-dose curves, if requested.

## Repository layout

```
DicomStrictCompare/
  DicomStrictCompare.sln
  DSCcore/          Windows Forms app: GUI (View/), file matching and output (File Handling/),
                    settings and dose-pair handling (Controller/)
  DSClibrary/       Comparison maths: dose matrix, DTA criteria, comparison results, profile tools
  DSCcoreTest/      MSTest tests for the app project (with test DICOM data)
  DSClibraryTests/  MSTest tests for the library (with test DICOM data and a PTW water tank scan)
docs/               Generated DocFX API documentation (HTML)
```

## Status

<!-- TODO(Cody): state whether this is actively maintained, and whether Py142 is meant to replace it. -->
The most recent change was in October 2024. A Python, cross-platform reimplementation was started in
[crcrewso/Py142](https://github.com/crcrewso/Py142).

## Licence

DicomStrictCompare is distributed under the GNU Lesser General Public License v3.0; see
[`LICENSE`](LICENSE). It uses [EvilDICOM](https://github.com/rexcardan/Evil-DICOM), which has its own
licence.

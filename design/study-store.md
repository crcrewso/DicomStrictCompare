# Design note: a study saved as a SQLite file, with poor results kept in 3D

**Status:** proposal, 2026-11-18. Nothing is built. Extends `comparison-studies.md`: it is F12's "cached" and starts
F9 ("where it fails") and F13 (provenance).

**crcrewso, 2026-11-18:**
- *"is it something we can back with an sqlite db so it reopens cleanly and has reasonable memory usage"*;
- *"we should add the ability to keep bad results in 3D for exploring where/why the match is so poor"*;
- *"the db should allow for the reopenning of existing studies"*.

## The problem

- **A study can't be reopened with its results.** The study's JSON file holds the definition (sets, comparisons,
  run settings), not the results. `StudySession` lives only in memory, so closing the app loses the results.
  Reopening means running everything again; E15V11 vs E15V15 alone is about 3.5 minutes. CSV export is one-way.
- **A cancelled or crashed run starts over.** Adding a set or a comparison reruns every pair.
- **Results say how bad a pair is, never where.** Every engine returns counts (`SingleComparison`: count,
  compared, failed). Finding out where a 0.1 %/0 mm comparison fails (46 % of 655 million points for E15V11 vs
  E15V15) means opening the two doses elsewhere.
- **Memory:** the result rows are small (1,266 rows is a few hundred kilobytes). The costs are dose grids (review
  RV1, RV21, in aviary `projects/dicomstrictcompare/2026-11-18-gui-and-study-unification.md`) and the folder headers
  of every set, which at 8,000 doses are worth keeping on disk.

## The proposal

### SS1: one file is the study

- **A study is one SQLite file,** for example `QAE18 AAA vs AXB.dscstudy`. Open, Save and Save As work on it.
- **It holds the definition and the results together,** so reopening shows the last run's rows, index tables and
  curves at once, with no rerun.
- **Existing studies open too:**
  - Opening a `.json` study (today's format) imports its definition into a new `.dscstudy` beside it. The JSON file
    isn't changed.
  - Opening a `.dscstudy` written by an older version runs its schema migrations (SS6).
- **The definition is also kept as JSON text inside the file,** in the same serialiser's format as today's study
  file. "Export definition" writes it out, so a definition can still be diffed or shared without results.
- **The DICOM files stay where they are.** The study stores paths with each file's size and modification time,
  never dose grids. A file that moved or changed is reported, and its pairs are marked stale (SS3).

### SS2: what is stored

| Table | One row per | Holds |
|---|---|---|
| `study` | file | schema version, the definition as JSON, created/modified |
| `dose_set` | set | name, folder, algorithm, TPS version, reporting, notes |
| `dose_file` | DICOM file read | set, path, size, modification time, kind (dose/plan) |
| `dose_header` | dose | the header facts the matching, plan check (F3) and description (F4) use: plan UID/label, beam, grid geometry, units, algorithm |
| `comparison` | comparison | source and target set, strategy, rules |
| `pair` | pairing | comparison, source dose, target dose, key, candidates (ambiguous keys), plan-check notes, warnings |
| `run` | run | when, criteria, engine and precision, threads, pairs at once, commit, machine; the provenance F13 and review RV24 ask for |
| `result` | pair × criterion × run | compared, failed, % failed, max gamma, maximum used, edge check, failure text, elapsed |
| `curve` | pair × depth × kind | the sampled source and target line (a few hundred points), for F7 |
| `failure_map` | kept poor result | SS4 |

- **Index tables become queries** (`GROUP BY` over `result` joined to `pair` and `dose_header`). The open
  index-table filter (by plan, by accessories) becomes a `WHERE` clause.
- **Curves come from `curve`,** so the Study pane no longer loads whole grids to draw a line (RV21).
- **Rows are written as each pair finishes,** through the existing `BatchComparer.PairDone` hook, in short
  transactions. A run that is killed keeps everything finished before it stopped.

### SS3: runs that resume, and only compute what's new

- **A result is current when its pair, criterion and file signatures are unchanged.** The file signatures are each
  DICOM file's path, size and modification time.
- **Run** computes only pairs with no current result. That covers:
  - a cancelled or crashed run, which resumes where it stopped;
  - a new set or comparison, which computes only its own pairs;
  - a changed file, whose pairs are marked stale and rerun.
- **"Run all again"** ignores the stored results. It is for a new engine or version, and it keeps the old run's rows
  for comparison (the `run` table).
- **One writer at a time.** The app opens the file in WAL mode. Each finished pair is one short transaction, so the
  window can read rows while a run writes them.

### SS4: poor results kept in 3D

The aim is to see **where** a pair fails and **why**: in-field or penumbra, high or low dose, build-up, near an
edge.

- **Which pairs:** a pair is kept when one of its criteria fails more than a threshold set in the study, or when it
  is among the worst N of its comparison. Both settings are in the definition (SS-Q1).
- **How it's captured.** Engines today return only counts.
  - A second pass reruns only the kept pairs on the reference engine (`HandWrittenComparer`), with a capture hook in
    its per-point loop. It records each failing point's lattice index (i, j, k), the source and target dose, and the
    rule that failed (dose difference with no DTA neighbour, or gamma above 1 with the gamma value).
  - The main run then stays as fast as today, on any engine. The capture is exact, because it uses the reference
    rules.
  - It costs one more comparison per kept pair: about 2.6 s on a reference grid, more at 1 mm.
- **Two levels, because failures can be huge:**
  - **Coarse, always:** a failure-fraction volume in blocks (for example 5 mm cubes), one byte per block (percent of
    points failed), compressed. A 1 mm grid of about 100 million points is about 800,000 blocks, under 1 MB before
    compression.
  - **Exact, up to a budget:** the failing points themselves, 16 bytes each (index, two doses, metric), compressed,
    up to a limit per pair (for example 2 million points, 32 MB). Above it, the exact list keeps a deterministic
    sample, and the row says so.
  - **Sizing example:** E15V11 vs E15V15 at 0.1 %/0 mm fails 303,698,355 points over 422 pairs, about 720,000 a
    pair. Keeping exact lists for every pair would be several gigabytes. Hence the threshold and worst-N choice, and
    a study-wide budget (SS-Q2).
- **The "why" is derived when viewing, not stored.** Each failing point's context comes from the DICOM files if
  they're still present:
  - dose relative to the maximum;
  - the local dose gradient;
  - depth below the surface;
  - distance from the central axis and from the nearest 50 % edge;
  - its F9 region (in-field, penumbra, low dose, build-up).

  If the files are gone, the stored maps still open, without the dose underneath.
- **Viewing (SS5).**

### SS5: exploring a poor result

- **First, slices:** axial, coronal and sagittal through the target dose, with the failing points over it, and a
  slider per axis. Click a point for its context. This fits both GUIs (ScottPlot heatmaps).
- **Then, summaries of the kept points:** failures by F9 region, by dose level and by depth (histograms). A
  "failures within N mm of an edge" share answers most "penumbra or not?" questions at once.
- **3D:** a point cloud of the failing points, coloured by metric, as a later step. Avalonia has no 3D control
  built in, so it needs a library choice (SS-Q4).
- **From any result row:** "Explore failures" opens the kept map for that pair and criterion. A pair with no map
  offers to capture one now (SS4's second pass on that pair alone).

### SS6: schema and versions

- **The schema has a version number** in `study`, and the app carries a migration for each step. A study file
  opens in any later version.
- **crcrewso's rule of 2026-11-15** ("Study files can be considered a debug and validation tool only") still holds for
  results:
  - a migration may drop and recompute derived tables (`result`, `curve`, `failure_map`) rather than convert them;
  - it never drops the definition, the sets or the comparisons.

  So reopening an old study always keeps what the user typed. At worst, it asks to run again.
- **Package:** `Microsoft.Data.Sqlite` (MIT), which ships SQLite's native library for Linux and Windows. Nothing
  else is needed.

## Memory

- **Rows, headers and curves stay on disk,** and the window pages them. A study of 8,000 doses no longer holds every
  header in memory.
- **Grids are still loaded only to compare,** within the heap limit and grid pool (WC2). They are also loaded to
  view a kept map with its dose, one pair at a time.
- **RV1 still needs its own fix:** keep the gamma counts, drop the per-voxel array. SS4 keeps what is worth keeping
  in its place.

## Open questions

- **SS-Q1:** What makes a result "poor" enough to keep: a % failed threshold per criterion, the worst N per
  comparison, or both? Which criterion's failures: every failing criterion, or the strictest only?
- **SS-Q2:** The disk budget: per pair (exact points) and per study. Is a study file of a few gigabytes acceptable?
- **SS-Q3:** Should "Run all again" keep the previous runs' results for comparison (old engine against new), or
  replace them?
- **SS-Q4:** The 3D view: which library (Avalonia has no 3D control built in; Eto would need its own), or is a
  point cloud in an external viewer (an exported `.ply`/`.vtk`) enough at first?
- **SS-Q5:** Is the Study tab Avalonia-only for now (Eto has none; review parity gap), or does this wait for a
  shared view model both GUIs use?

## Order of work

1. SS1–SS3: the file, the tables, resume. With RV1's fix, and the curve table replacing RV21's held grids.
2. SS4: the coarse map for every kept pair; then the exact points.
3. SS5: slices and summaries; then 3D.

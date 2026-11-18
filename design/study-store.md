# Design note: a study saved as a SQLite file, with poor results kept in 3D

**Status:** proposal, 2026-11-18; SS-Q1 to SS-Q7 decided the same day (below). Nothing is built. Extends `comparison-studies.md`: it is F12's "cached" and starts
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
- **File names live only in the SQLite file** (crcrewso, 2026-11-18). The JSON definition, inside or exported, names
  folders, never files. A setting adds the matched files to an exported JSON definition when someone asks for it.
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

*Revised by crcrewso's answers the same day: see "SS4, revised" under Decided. The stored form there replaces the
exact-points list below.*

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

### SS7: a copy of the data a study used, in the study's folder

crcrewso, 2026-11-18: *"There should also be the option to save all data used in an analysis within a sub folder of
the study folders for future recall. This is mostly so that if data is stored on network drives"*.

- **What today records:** the study's JSON file holds only the definition. The study CSV names each match's source
  and target **by file name alone**, and names repeat across plan folders: E11V11 has an
  `RD.QAEclipseDcf.Dose_BEAM_1.dcm` in many of them. So today a result can't always be traced back to its file. SS2's
  `pair` and `dose_file` tables fix that: they store each set's folder plus each file's path relative to it.
- **The option:** "Keep a copy of the data with the study", per study. Off by default; the size is shown before it
  starts.
- **What is copied:** every DICOM file a run used, meaning each paired dose and its plan. Optionally the whole of each
  set's folder, for a later comparison that pairs differently.
- **Where, and under what names:** a fixed layout of short generated names, not the original folders, so the copy
  works under a Windows user's home folder (crcrewso, 2026-11-18: *"some consistent subfolder format, that can exist in
  windows under a users home folder without breaking folder count or filename compatibility"*).

  ```
  <study folder>\<study name> data\
      s01\                one folder per set, in the study's set order
          p0001\          one folder per plan
              rp.dcm      the plan
              d0001.dcm   its doses, in beam order
          noplan\         doses whose plan the set doesn't hold
      s02\ ...
  ```

  - **Names:** lowercase letters and digits with a fixed width. That rules out Windows' reserved names (`CON`, `NUL`,
    `COM1` and so on), characters it refuses, trailing dots and spaces, and two names that differ only in case.
    E11V11's `06x w15` beside `06x W15` would otherwise share a name on Windows.
  - **Length:** after the study's name, the generated part is 25 characters (` data\s01\p0001\d0001.dcm`). The
    study's name is cut to 60 characters in folder names, so a study under `C:\Users\<name>\Documents\` stays well inside the 260-character
    limit that Windows applies without long paths enabled. A location that would still pass 240 characters is
    refused, with the length shown, before anything is copied.
  - **Folder sizes:** a folder holds one set's plans or one plan's doses, a few hundred entries at most. A plan with
    more than 999 doses is split into `d0001`–`d0999`, then a `p0001-2\` folder.
  - **Where the original names are:** only in the SQLite file. `dose_file` maps each original path to its copy, as
    the first bullet of SS1 requires. Without the study file the copy still opens in any DICOM tool, because UIDs,
    labels and beam names are in the files. A set can be pointed at `s01\` and paired again.
  - **Pairing is unchanged:** it reads DICOM content, never file names (matching uses paths only in messages, and a
    dose finds its plan by UID). One exception: when two pairings of an ambiguous key tie exactly, the file path
    breaks the tie, so which file is named could differ. The counts can't.
- **When:** as each file is first read for a run. One read from the network drive both feeds the comparison and makes
  the copy, so a slow drive is read once.
- **Checked:** each copied file's size and SHA-256 are recorded in `dose_file` and checked once the copy is written.
  A file that fails is copied again, never kept. (Lesson from the T5 drive, 2026-11-17: 136 copied files came back at
  0 bytes, with the file count intact.)
- **Shared files:** a file used by two sets is stored once, by hash. On the same volume it is hard-linked rather than
  copied.
- **Reopening:**
  - Each set records both its original folder and its copy (`s01\` and so on).
  - When the original can't be reached, or a file there no longer matches its recorded size and hash, the study uses
    the copy and says so.
  - A per-set switch picks either one.
- **Budget:** the copy is its own figure, outside SS-Q2's 10 % (it *is* the source data). The study shows both.

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

## Decided, 2026-11-18 (crcrewso)

- **SS-Q1:** a percent pass threshold, *"defaulting to 97.5 % (3D) or 95 % (2D) that's beside the 'Save to Study'
  feature"*. A pair below it at a criterion is kept (SS4). "2D" is a single-plane dose such as the repository's
  `P4 E 2D` file.
  - **Measured on E15V11 vs E15V15** (the WC2 run, 422 pairs): below 97.5 %, 40 pairs at 3 %/2 mm, 163 at 1 %/1 mm,
    and all 422 at 0.1 %/0 mm. So the strict criteria keep everything; SS-Q6 asks whether that is wanted.
- **SS-Q2:** *"I wouldn't worry about any study that's < 10% of the source data"*. The budget is 10 % of the study's
  DICOM bytes. For E15V11 (20 GB) and E15V15 Prod (129 GB) that's about 15 GB.
  - A dense float32 delta for one 1 mm pair is about 0.4 GB, about the size of the target dose file. So deltas for
    every pair would pass the budget; deltas for the kept pairs, sparse, would not (SS4, revised). Since SS-Q7 a kept
    pair stores one volume per criterion it fell below, so up to three times that for three criteria.
- **SS-Q3:** *"assume replaces, pre-populates with previous comparison settings"*. "Run all again" replaces the
  stored results, and the run form starts from the last run's settings. The `run` table keeps only the latest run's
  provenance per comparison.
- **SS-Q4:** *"Profiles and a single 'Delta' 3D volume for each comparison, probably as some form of sparse array
  floats to save space"*. Revised SS4 below.
- **SS-Q5:** *"keep it Avalonia-only, once I'm happy with it then we can port it to Eto and show both implementation
  to other users"*.

### SS4, revised: profiles and one result volume per kept criterion

- **Profiles:** the source, target and delta lines at the study's depths and the PDD, from the `curve` table. Small,
  so they are kept for **every** pair, not only kept ones.
- **The result volume:** for each kept pair, one volume for each criterion it fell below, on the comparison lattice.
  It holds the value the criterion itself computes (SS-Q7):
  - **a gamma criterion:** the gamma index, capped at 3;
  - **a percent criterion:** the percent difference, (target − source) over the criterion's normalisation (the
    global maximum, or the source point for a local criterion), signed.
  - **Sparse:** only the points the comparison evaluated, those above the dose threshold. Stored as a bit mask over
    the lattice (1 bit a point) and float32 values for the set bits, compressed. A 1 mm pair of about 100 million
    points is a 12 MB mask (which compresses well) plus 4 bytes per evaluated point.
  - **Which points failed** is a second bit mask. A gamma point fails above 1, but a percent point passes either
    within its tolerance or when a point within the DTA distance agrees. So failure can't be read from the percent
    value alone, and the mask is stored.
  - The coarse failure-fraction blocks stay, for a quick overview of every kept pair.
- **The second pass on the reference engine** (unchanged) captures the values and the masks: `HandWrittenComparer`
  for percent criteria, `GammaShell` for gamma, which already returns a gamma value per point (review RV1).

### SS-Q4, existing formats to borrow

- **Nothing in common use is sparse.** The sparse dose formats in radiotherapy are dose-influence matrices (matRad's
  MATLAB sparse `dij`, column by column), which hold beamlets rather than a viewable volume. So the stored form is
  the study's own (mask plus values, above). The formats below are for **export**, so the delta opens in tools
  physicists already use.
- **DICOM RT Dose with Dose Type `ERROR`** is the standard's own form for a dose difference. *From memory, to be
  checked against PS3.3:* the RT Dose module defines `ERROR` as a difference dose and requires signed pixel data
  (Pixel Representation 1) for it.
  - It opens in DICOM-RT tools (3D Slicer with SlicerRT, dicompyler; TPS support varies).
  - It's dense, but the points outside the mask are zero, so a zipped export is small.
  - The library's `DoseGridReader` already honours signed pixel data. `RTDoseResult` doesn't (review RV10).
- **MetaImage (`.mha`, compressed)** is what Plastimatch, a radiotherapy toolkit, writes for its dose-difference
  and gamma maps. 3D Slicer and ITK read it. Writing it is a short text header plus zlib, with no dependency.
- **NRRD (`.nrrd`, gzip)** is 3D Slicer's native volume format; a similar size and effort.
- **Proposed (revised with SS-Q7):** keep the sparse form in the study. Export a kept result as `.mha`, the format
  Plastimatch uses for its own gamma and dose-difference maps, which Slicer reads, on the dose grid's geometry.
  - DICOM RT Dose `ERROR` no longer fits, because the stored values are gamma or percent, not dose.
  - DICOM's own form for a map of values that aren't dose is the Parametric Map object. *From memory:* it holds float
    pixels with a stated quantity and unit. Few radiotherapy tools read it, so it waits until someone asks.

### SS-Q6, decided 2026-11-18

crcrewso: E11V11 against E15V15 failing everywhere at the strict criteria is expected, from the surface-dose bug.
He asked for E11V11 vs E15V11 and E15V15 Prod vs Equinox, ignoring the wedges, and then agreed the rule below.

- **Measured:**
  - Pairing was by patient, plan label and field name, at 3 %/2 mm, 1 %/1 mm and 0.1 %/0 mm, with an 8 % threshold.
  - "Equinox" is `EquinoxCommissioning/DICOM/PRD-After`.
  - Pairs below 97.5 %, without wedges:
    - E11V11 vs E15V11: 2, 107 and 107 of 329;
    - E15V15 Prod vs Equinox: 12, 22 and 34 of 40.
  - In E11V11 vs E15V11, 222 of 329 pass 100 % even at 0.1 %/0 mm. At 1 %/1 mm, 80 of the 107 are the electron
    flat-field plans (`STBflatE`, `AiXflatE`, `SiXflatE`).
- **Decided:** the threshold applies to each criterion separately. A pair is kept when any criterion falls below it,
  and it keeps one result volume (gamma or percent difference, SS-Q7) with a failed-points mask for each criterion
  it fell below.

### New questions

- ~~**SS-Q6:**~~ *Decided above.* **SS-Q6 (as asked):** At the strict criteria every pair falls below 97.5 % (all 422 at 0.1 %/0 mm). Should the threshold
  apply per criterion, with some criteria exempt from saving, or should a pair be kept only by its loosest criterion?
- **SS-Q7, decided 2026-11-18 (crcrewso):** *"store the data in Gamma or gamma (capped at 3) or percent difference if
  a percent is computed"*. That replaces his first answer, Gy, the same day. So the stored value is the criterion's
  own: gamma capped at 3, or the percent difference. SS4 (revised) stores one volume per kept criterion, not one per
  pair. The question was: units of the delta, dose (Gy) or percent of the normalisation the criterion used?

## Order of work

1. SS1–SS3: the file, the tables, resume. With RV1's fix, and the curve table replacing RV21's held grids.
2. SS4: the coarse map for every kept pair; then the exact points.
3. SS5: slices and summaries; then 3D.

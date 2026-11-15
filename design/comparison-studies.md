# Design note: comparison studies across TPS versions and algorithms

**Status:** proposal, 2026-11-14. F1, F2, F3, F4, F5 and F7 are being built first (crcrewso, 2026-11-14); the rest
are recorded for later.

## The problem

A realistic commissioning question involves more than two folders:
- **Two versions of the same algorithm.** AAA 15.6 against AAA 18 asks "did the upgrade change anything?".
- **Two algorithms.** AAA against AXB asks "how different are they, and where?".
- **Both at once,** usually.

The datasets are large. The three Eclipse 18 commissioning patients built on 2026-11-13 hold about 2,080 fields:
- QAE18Photon: 1,314 fields;
- QAE18Electron: 320;
- QAE18Extra: 448.

Exported per field, that's about 2,080 doses for each calculation, so four calculations come to about 8,000 doses.

**What DicomStrictCompare does today:**
- It compares **two** folders.
- It pairs doses by patient, plan label and field name, or by plan UID and beam number.
- It reports a percentage per criterion.
- The prototypes' Graphs tab draws source over target, as a depth dose or a profile at dmax, 5, 10 or 20 cm.

**Where that stops working for AAA against AXB, measured on the Prep18 and QAtpsPTW plans:**
- **The plan labels differ.** `STBjawAAA` faces `STBjawAXB`, and in QAtpsPTW `SquareFieldsS` faces
  `SquFldsSAXB`. Identifier matching pairs nothing.
- **The beam numbers differ.** The AXB plans were reordered by energy and field size (the agreed AXB field
  order), so UID matching would pair the wrong fields, even where the plan UIDs match.
- **The field names agree.** For example `06x10x10` is in both plans. That's the thread a cross-algorithm pairing
  can follow.
- **Same fields, different phantoms.** Some plans carry the same fields on another phantom (`…Sm` on the Small
  Cube). They must never be compared voxel by voxel.

## Features

| # | Feature | Status |
|---|---|---|
| **F1** | Named dose sets | **building** |
| **F2** | Pairing rules and a pairing preview | **building** |
| **F3** | Plan check on every pair | **building** |
| **F4** | What each dose represents | **building** |
| **F5** | Results table, with index tables | **building** |
| F6 | Overview charts (pass rate against field size, heatmaps, worst N) | later |
| **F7** | Curves for every set at depths you choose | **building** |
| F8 | Curve metrics per pair (field size, penumbra, flatness, symmetry, dmax, D10, D20/D10, R50, Rp) | later |
| F9 | Where it fails (in-field, penumbra, low dose, build-up), with a failure map | later |
| F10 | Normalisation: absolute, **per MU**, or at a reference point | later |
| F11 | Measurement beside the calculations | later |
| F12 | Built for thousands of doses (header index, parallel, cached) | started: header-only reads, streamed grid loads (below) |
| F13 | Reproducible export (long CSV, reports, provenance header) | later; F5 starts it |
| F14 | Expected-pairs list from the plan generator (PG-Q7) | later |

**F10, normalisation per MU.** crcrewso asked on 2026-11-14 whether every field could be normalised by the MU in
its plan. It belongs here, beside the other normalisations.
- It has to be opt-in: the standing rule (2026-11-08, in `DoseVolume.MonitorUnits`) is that MU is recorded and
  never applied.
- So "per MU" would be a named normalisation, labelled cGy/MU on every result that uses it, and never a default.
- It answers a real case: two calculations of one field at different MU, which `resources/`'s "P5 Varied MU" plan
  exists to test.

**F10, the reference dose for the threshold and the global tolerance (crcrewso, 2026-11-15).** Today both use the
source's volume maximum. Two settings are wanted:
- **Volume maximum:** today's behaviour, and the default, so no existing number changes.
- **Central-axis (CAX) maximum.** The CAX is the centre array of the phantom along the dose axis, averaged over the
  3 × 3 grid of nearest neighbours; the reference is the maximum along it.
- **An off-axis field falls back to the volume maximum.** Off-axis has two terms (crcrewso, 2026-11-15):
  1. **Asymmetric jaws:** the field edges' distances from the CAX differ by more than 1 cm, −X against +X or −Y
     against +Y.
     - **With a plan file,** the jaw definitions are the sole test.
     - **Without one,** the same test is applied to the central profiles' penumbra distances.
  2. **Symmetric jaws, but the profiles do not intersect the CAX.** The field lies wholly to one side of the
     phantom's centre line, for example an MLC-shaped or shifted field.
- **"CAX maximum under 80 % of the volume maximum" also counts as off-axis.** It stays in the design but is **not
  yet implemented** (crcrewso, 2026-11-15). On the real data, CAX was under 80 % of the volume maximum for 11 of 26
  QAtpsPTW OtherFields, 26 of 392 QAEclipseDcf fields and 12 of 32 QASacDQA arcs.
- **A penumbra edge is the 50 % point of the profile** (crcrewso, 2026-11-15).
- **Where a plan exists, the 50 % edge and the jaw position should agree within 2 mm.** A larger difference is a
  warning on the pair, beside the F3 plan check and F4 dose warnings. It means the dose and the plan may not belong
  together, or the geometry was read wrongly.
- **Built (`DSClibrary/Studies/JawEdgeCheck.cs`), at 10 cm depth** (crcrewso, 2026-11-15):
  - the jaws are scaled from the isocentre plane by (SSD + 100 mm) / SAD;
  - the surface comes from the plan's SurfaceEntryPoint, else its SSD, else the dose. The isocentric QAtpsPTW and
    QAEclipseDcf plans state neither;
  - the 50 % level is half the dose at the jaws' field centre, which is the CAX for a symmetric field;
  - collimator angles are multiples of 45, verified at 90 and 270 on QAtpsPTW's half-beam fields.
- **Not checked:** electrons, wedged fields, MLC-shaped or closed-MLC fields, arcs, and **FFF fields for now**
  (crcrewso: "ignoring FFFs is a perfectly reasonable solution for now").
  - FFF field size is quoted as if the beam were flattened. Measured on QAtpsPTW: the inflection point agreed on
    every FFF field above 15 cm, and 50 % of the centre on every one below. That's a starting point for later.
- **On real data:** every checked field agreed within 0.62 mm. That's 78 fields: 24 QAtpsPTW AXB, 18
  SquareFieldsL, 6 OtherFields and 30 QAEclipseDcf.
- **Still open:** arcs, plan sums and doses without a plan (today's answer would be the volume maximum).
- **The one change to the reference:** `HandWrittenComparer` would read the chosen value instead of
  `source.MaxPointDose`. With the default, the equivalence tests show its counts unchanged.
- **Like every semantics change,** it needs a README update, tests and a PR note (AGENTS.md).

---

## F1: named dose sets

A **dose set** is a folder of RT Dose and RT Plan files, under a name a person chose, with what they know about
it:

| Field | Example | Required |
|---|---|---|
| Name | `AAA 15.6` | yes, unique within the study |
| Folder | `D:\Exports\PRD-15.6-AAA` | yes |
| Algorithm | `AAA 15.6.06`, `AXB 18.0` | no |
| TPS and version | `Eclipse 15.6` | no |
| Dose reported as | dose to water, dose to medium, or not stated (F4) | no, defaults to not stated |
| Notes | free text | no |
| Plan filter | a regular expression on the plan label, for example `AXB$` | no: every dose in the folder |

A **study** is a list of dose sets and a list of **set comparisons**. Each comparison names a source set, a target
set and a pairing rule (F2). One run executes every comparison. Each set is read once, however many comparisons
use it.

**One export folder often holds both algorithms,** as QAtpsPTW does: SquareFieldsS (AAA) and SquFldsSAXB (AXB) sit
side by side. So a set can name a plan filter, and two sets can share a folder. Measured on 2026-11-14 without
filters: a folder compared with itself paired every AAA plan with itself (245 pairs) and left 40 keys
ambiguous. A folder is read once, however many sets use it.

Code: `DSClibrary/Studies/Study.cs`.

**Sets are read headers only (the start of F12).** The first real-data trial failed, measured on 2026-11-14:
- a study of three QAtpsPTW folders, about 23 GB of doses each, was killed by the kernel's out-of-memory killer
  at 24 GB resident;
- the cause: `RTFolderReader` read every dose whole and kept its voxels;
- matching needs only the headers, and each pair's grids are loaded separately when it is compared.

So a study reads each dose up to its pixel data and copies the header out. A header-only dose refuses to hand
out voxels: anything that asks throws, rather than seeing an empty grid. Each file is still read from disk in
full once; reading only its start is the next F12 step.

**Grids load streamed (F12).** `DoseGridReader` reads a dose's header from the file's start and streams its voxels
straight into one array: 0.81 GB allocated for a 1 mm QAtpsPTW grid instead of EvilDICOM's 4.6 GB, and about 9×
faster. It's checked against EvilDICOM voxel for voxel, on `resources/` and on all 325 QAtpsPTW doses (2026-11-14).
The batch's fixed parallelism of 4 still needs a memory budget: four 1 mm pairs hold 6.5 GB live.

## F2: pairing rules and a pairing preview

A comparison pairs with one of the two existing strategies, plus optional **label rules** applied before keying.
Each rule is a regular expression and a replacement, applied to each side's plan label.

**The rule that pairs AAA with AXB:** replace `(AAA|AXB)` with `{algorithm}` on both sides. That turns
`STBjawAAA` and `STBjawAXB` into the same key, `STBjaw{algorithm}`. Matching is then by patient, rewritten label
and field name, so the AXB reorder doesn't matter.

**Field rules do the same for field names.** Some sites put the algorithm in the field name too. In this
repository's `resources/`, the AAA field is `X-100A-10.0-1` and its Acuros copy is `X-100X-10.0-1`. The rule
`-100[AX]-` → `-100{algorithm}-` pairs them. The Prep18 sets don't need it: their field names agree.

**An override table covers names no pattern reaches.** It maps exact source labels to target labels, for example
`SquareFieldsS = SquFldsSAXB`. An entry wins over any rule.

Rules apply only to **Identifier** matching. UID matching has no label to rewrite. With pairing across
algorithms it would pair the wrong fields, because the beam numbers differ.

**The preview runs before any voxel is compared.** It lists:
- the pairs it will compare;
- the source doses with no target, and the target doses with no source;
- ambiguous keys;
- **near misses**: for each unmatched source dose, any target dose with the same patient and field name under a
  different label. Those are the cases a label rule usually fixes, and the preview names the label pair, so the
  rule can be read straight off it.

**Phantoms stay apart.** A rule never pairs fields on different phantoms, because the patient and the rewritten
label still have to agree. The `…Sm` plans keep their `Sm`, and the Large Cube plans have none.

Code: `DSClibrary/Matching/PairingRules.cs`, and `DoseMatcher.Match` with rules.

## F3: plan check on every pair

Before trusting a difference, check the two plans asked for the same field. For each pair, compare the beams'
settings:

| Setting | Tolerance |
|---|---|
| Treatment machine, radiation type, nominal energy, FFF | exact |
| MU | 0.01 MU |
| Gantry, collimator and couch angle | 0.1° |
| Jaw positions (each jaw), and MLC positions (largest leaf difference) | 0.1 mm |
| Isocentre (each axis), and SSD where both state it | 0.1 mm |
| Applicator ID, wedge IDs, number of blocks (cutouts) | exact |

**The result is a list of differences, shown on the pair's row,** and the comparison still runs. A planning
difference doesn't make a comparison invalid. It means the difference may not be the algorithm's, and the
reader needs to know that.

**A pair with no plan on either side is "not checked"**, never "agrees".

Code: `DSClibrary/Studies/PlanCheck.cs`.

## F4: what each dose represents

Read from every RT Dose:
- `DoseUnits` (GY or RELATIVE);
- `DoseType` (PHYSICAL, EFFECTIVE, ERROR);
- `DoseSummationType` (BEAM, PLAN, …);
- grid spacing on all three axes.

**From the set (F1),** as a person declares it: dose to water or dose to medium. RT Dose has no standard place for
this, and an AXB calculation can report either.

**Per comparison, a warning (not a refusal) for each of these:**
- the units, dose types or summation types differ between paired doses;
- the two sets declare different dose reporting, or one declares it and the other doesn't, in a comparison
  involving an AXB-named set;
- the grid spacing differs. Comparisons then run on the coarser grid, as today; the warning says so.

Every warning goes on the row it concerns, and into the CSV.

Code: `DSClibrary/Studies/DoseDescription.cs`.

## F5: one results table, with index tables

**The table has one row per (comparison, matched field, criterion).** Each row carries:
- **who:** comparison name, source set, target set, patient, source and target plan labels, field name;
- **the beam:**
  - machine and energy (with FFF);
  - field size from the jaws, or the applicator for electrons;
  - jaw or MLC;
  - SSD;
  - wedge, applicator and cutout;
- **the result:** compared, failed, % failed, pass rate, max gamma (gamma rows);
- **the checks:** F3 plan differences and F4 warnings, as short text.

**Index tables are pivots over those rows,** crcrewso's example being "Field size vs Energy global pass rate". For a
chosen comparison and criterion:
- **rows** are field sizes, smallest first;
- **columns** are energies, in physical order: photons by energy with flattened before FFF, then electrons;
- **each cell is the pooled pass rate** of the fields in it: 100 × (voxels passed) / (voxels compared). Pooling
  weights a field by its voxels, the same way a single comparison already pools them.
- **Each cell also gives the field count and the worst field's pass rate,** because a pooled 99.8 % can hide a
  single field at 80 %.

"Global" here means **over all fields** in the cell. The criterion row still decides global or local dose
difference. Any two beam attributes can be the axes: field size against energy, machine against energy, SSD
against field size, and so on.

**CSV:**
- `<stem>-results.csv` in long form, one row per row above, so Excel or pandas can pivot it any other way;
- `<stem>-index-<comparison>-<criterion>.csv` for each index table;
- `<stem>-pairing.csv` for the F2 preview's unmatched doses and near misses.

Code: `prototypes/DSCprototypes.Shared/Studies/StudyRunner.cs` and `IndexTable.cs`.

## F7: curves for every set, at depths you choose

**Choosing a field shows its curves from every set that has it,** not only two. For example AAA 15.6, AAA 18,
AXB 15.6 and AXB 18 on one plot:
- depth dose along the beam axis;
- crossplane and inplane profiles at each chosen depth.

Each curve is sampled at the coarsest grid's nodes, where every grid has a value. That's the existing rule,
widened from two grids to N.

**A difference panel underneath** shows each set minus a reference set the user chooses (the first by default),
in % of the reference maximum.

**The depths are a setting**, a list in millimetres, replacing the fixed dmax, 5, 10 and 20 cm:
- the default is that same list: dmax, 50, 100, 200 mm;
- dmax can be on or off, and is found per set, on the reference set's axis;
- a depth outside a grid is skipped, and the window says so;
- the list is saved with the study, so a site can keep, say, 15, 50, 100, 200 and 300 mm.

**A field's identity across sets** is the F2 key: patient, rewritten label and field name. So a field appears
once, even when its plan label changes between sets.

Code: `prototypes/DSCprototypes.Shared/Graphs/MultiCurves.cs` and `DepthSettings.cs`.

---

## Open questions

- **CS-Q1:** should a study be saved as a file (JSON) and reopened, so a site keeps its sets, rules and depths?
  This is assumed yes, but the format is open.
- **CS-Q2:** in index tables, does "pass rate" mean the pooled rate, or the mean of the per-field rates? Pooled is
  built, and the mean is one more column if wanted.
- **CS-Q3:** F7's difference panel uses % of the reference maximum. Is local % wanted as well?
- **CS-Q4:** which front end gets the study screens first? The view models are shared; the Avalonia window is
  wired first, because it's the one that already shows Results and Graphs tabs.

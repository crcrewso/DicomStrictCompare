# DSCgpu: when does a parallel comparison beat parallel comparisons?

A benchmark prototype, not part of any front end. It asks one question: at what grid size does running **one
comparison in parallel** (CPU threads, or a GPU) beat **trivial parallelism** (whole comparisons side by side, one
thread each, as `BatchComparer` does)?

## What it runs

**Pairs:** QAtpsPTW `SquareFieldsS` (AAA, 1 mm) against `SquFldsSAXB` (AXB, 2 mm), matched by file name. They're
resampled with `GridResampler` so both sides share the chosen resolution:

| Argument | Source | Target |
|---|---|---|
| `1` | AAA at 1 mm, as exported | AXB resampled to 1 mm |
| `2` | AAA resampled to 2 mm | AXB at 2 mm, as exported |
| anything else, e.g. `3` or `5` | both resampled to that spacing | |

**Criteria:** 2 %/2 mm/10 % global, then 1 %/1 mm/10 % global.

**Backends:**
- **The engine** (`X86Mathematics`), one pair at a time on one thread. This is the reference: every other row's
  compared and failed counts are checked against it, pair by pair.
- **The engine with 2, 4, 8, … pairs at once:** trivial parallelism, up to 2 × the core count.
- **`CpuPort`:** the engine's point rules, line for line, on arrays, using 1 thread and then every core within one
  pair.
- **`IlgpuComparer`** (ILGPU 1.5.3) and **`OpenClComparer`** (Silk.NET.OpenCL 2.23.0, hand-written OpenCL C). Each
  runs in double precision (the engine's arithmetic) and in float (a possible screening pass), one pair at a time.

**Boxed rows** evaluate only the sample points inside the threshold box: the bounding box of source voxels at or
above threshold, plus the smaller of 1 cm and 2 voxels (never under 1 voxel). Outside it, a point can't reach the
threshold, because trilinear interpolation never exceeds the voxels around it. Their counts must equal the
engine's:
- "boxed" rows find the box by scanning, and the scan is timed;
- "from load-time maxima" rows read it from per-axis maxima computed beforehand (`Projections`), standing in for
  keeping them at load.

**"Zeroed" rows** set below-threshold source voxels to 0 first. They're not the same rules, so their counts are
reported, not required to match.

**Excluded from timing:** loading and resampling. GPU rows include uploading both grids for every pair. Each row is
the fastest of the repeats, after an untimed warm-up.

## Running it

```
dotnet run -c Release -- <resolution mm> <pairs> [folder or -] [repeats] [options]
dotnet run -c Release -- 2 16 "Q:\DSCReferences\...\QAtpsPTW" 3
```

| Option | Effect |
|---|---|
| `--criteria 0.01/0,0.1/0.5,3/2` | tolerance %/distance mm pairs; distance 0 is a dose test only (default `2/2,1/1`) |
| `--threshold 10` | low-dose cutoff, % of the source maximum |
| `--crop 0.4` | keep the central fraction of each axis: a smaller phantom at the same resolution |
| `--pairlist file.tsv --set name` | pairs from a tab-separated file (name, source path, target path) instead of QAtpsPTW. Resolution `0` keeps each grid's own spacing |
| `--quick` | one trivial batch size and one thread count, for sweeps |
| `--csv file` | append one row per method |

- **Memory:** about 1.6 GB per pair at 1 mm, 0.26 GB at 2 mm and 0.12 GB at 3 mm, plus float copies at half that.
- **Linux:** on 2026-11-14, ILGPU found no OpenCL device until it was pointed at `libOpenCL.so.1`, which
  `IlgpuComparer` now does. Installing `ocl-icd-opencl-dev` also fixes it.
- **Windows:** the GPU driver provides `OpenCL.dll`.

**A row reading `COUNTS DIFFER`** means that backend did not reproduce the engine. A double row that does is a
bug. A float row that does is the measured cost of float.

Results and the reasoning are in `~/Source/~Resources/DSC-notes/2026-11-14-gpu-crossing-point.md` on jackdaw.

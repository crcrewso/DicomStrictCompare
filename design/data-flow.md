# Data flow

How doses move through a comparison, from folders to CSV rows. The diagrams are [Mermaid](https://mermaid.js.org),
which GitHub draws in place. **A change to any step shown here updates its diagram in the same commit** (AGENTS.md).

## 1. A study, end to end

`StudyRunner.Run` (prototypes/DSCprototypes.Shared/Studies). Preview stops after the pairing.

```mermaid
flowchart TD
    study["Study file: sets, comparisons, Run block"] --> read
    read["RTFolderReader.Read, per folder once<br/>recursive, dose headers only, plans whole<br/>dose to plan by SOPInstanceUID"] --> filter
    filter["DoseSet.PlanFilter<br/>keeps the doses whose plan label matches"] --> match
    match["DoseMatcher.Match with PairingRules<br/>Identifier: patient / plan label / beam name<br/>PlanAndBeamUid: plan UID / beam number"] --> pairs
    match --> unmatched["Unmatched and ambiguous doses<br/>reported, never guessed"]
    pairs["Matched pairs"] --> facts["Per pair: FieldFacts, PlanCheck, DoseWarnings"]
    facts --> preview["ComparisonPreview<br/>(Preview ends here)"]
    machine["MachineSettings file: engine, threads, reading<br/>plus the study's Max and GPU float"] --> engine
    preview --> engine["DoseComparers.Create(ComparerSettings)"]
    engine --> batch["BatchComparer.CompareMany<br/>(diagram 2)"]
    batch --> rows["StudyRow per pair and criterion"]
    rows --> csv["StudyCsv.Write<br/>results, pairing, index tables"]
```

## 2. Inside `BatchComparer.CompareMany`

Each pair is read once for every criterion. With no read queue, each worker reads its own pair, then compares it.

```mermaid
flowchart TD
    pairs["Pairs and criteria"] --> budget["Thread budget: BatchComparer.Threads<br/>or CpuTopology.PhysicalCores"]
    budget --> atonce["Pairs at once: as many as half the free memory holds,<br/>up to the budget (2 for MultiThread and GPU)"]
    atonce --> mode{"Read queue set?"}
    mode -- no --> workers["Workers, each reads then compares<br/>scheduled by the OS"]
    mode -- yes --> readers["K readers, fewer if readers + held + workers<br/>wouldn't fit half the free memory"] --> channel["Bounded queue, M pairs held"] --> workers
    workers --> load
    subgraph load["Load, once per pair"]
        applies["Criteria that apply to the beam<br/>(AppliesTo: Photon, Electron, Both)"] --> src["Source grid: DoseGridReader"]
        src --> max["ReferenceMaximum<br/>GlobalMax, or CaxMax when the jaws are on axis"]
        max --> tgt{"Target box only,<br/>and no gamma criterion?"}
        tgt -- yes --> part["Partial read: union of the criteria's boxes<br/>plus the edge-check rows; the rest holds 1e300"]
        tgt -- no --> whole["Whole target grid"]
    end
    load --> evaluate
    subgraph evaluate["Evaluate"]
        edges["JawEdgeCheck, source and target"] --> dta["DTA criteria together:<br/>comparer.CompareAll (one GPU upload)"]
        dta --> gamma["Each gamma criterion: GammaShell.Compare"]
    end
    evaluate --> outcomes["ComparisonOutcome per criterion<br/>sorted by key, then criterion"]
```

## 3. One DTA comparison

`ComparisonSetup.For`, then `PointRules.Evaluate` at every sample point. These are `HandWrittenComparer`'s rules, in
the same order and in double, so every engine gives its counts exactly.

```mermaid
flowchart TD
    grids["Source and target grids"] --> region["Overlap region: the larger origin, the smaller end"]
    region --> step["Step: the coarser spacing per axis<br/>trim (default 0) shrinks the region"]
    step --> lattice["Sample lattice over the region"]
    lattice --> box["Threshold box: source voxels at or above threshold,<br/>plus min(1 cm, 2 voxels); points outside are counted, not visited"]
    box --> point["Each sample point"]
    point --> s["Source dose at the point (trilinear)"]
    s --> thr{"Below threshold x max?"}
    thr -- yes --> below["Not compared"]
    thr -- no --> diff{"Dose difference within tolerance?<br/>global: tolerance x max<br/>local: tolerance x source dose"}
    diff -- yes --> pass["Passed"]
    diff -- no --> usedta{"DTA distance set?"}
    usedta -- no --> fail["Failed"]
    usedta -- yes --> nb["Source dose at +d and -d along each axis,<br/>where that stays inside the region"]
    nb --> range{"Target dose between the lowest<br/>and highest of those?"}
    range -- yes --> pass
    range -- no --> fail
```

"Max" is the reference maximum from diagram 2. A point with no neighbour inside the region fails.

## 4. The engines

```mermaid
flowchart LR
    settings["ComparerSettings<br/>Kind, Threads, Precision, UseThresholdBox, Device"] --> create["DoseComparers.Create"]
    create --> hw["HandWritten<br/>every point, one thread: the reference"]
    create --> st["SingleThread<br/>threshold box, one thread"]
    create --> mt["MultiThread<br/>threshold box, Threads lines at once<br/>scheduled by the OS"]
    create --> gpu["ILGPU, OpenCL<br/>GpuUpload: the box only, once per pair<br/>double by default"]
    topo["CpuTopology: physical cores with an L3,<br/>no SMT siblings, capped at ProcessorCount"] -. default Threads .-> mt
    topo -. default budget .-> st
    hw === rule["Every engine must give HandWritten's counts exactly in double<br/>(DoseComparerEquivalenceTests)"]
```

Float on the GPU engines is a user's choice and may differ from the reference on a few points in millions.
Core selection: [core-selection.md](core-selection.md).

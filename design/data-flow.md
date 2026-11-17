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
    match["DoseMatcher.Match with PairingRules<br/>PlanUidAndBeamName: plan UID / beam name (the window's default)<br/>PlanLabelAndBeamName: plan label / beam name<br/>Identifier: patient / plan label / beam name<br/>PlanAndBeamUid: plan UID / beam number (older studies)<br/>+ the dose's algorithm: AlgorithmTokens on plan label and beam name,<br/>else the folder's or set's; not with {algorithm} rules"] --> pairs
    match --> unmatched["Unmatched doses reported, never guessed<br/>ambiguous keys: every source x target pairing becomes a pair"]
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
        dta --> gamma["Each gamma criterion: GammaShell.Compare<br/>points that can't reach the evaluation grid: not analysed, as pymedphys"]
    end
    evaluate --> worst{"Pairing of an ambiguous key?"}
    worst -- "yes, held until its last pairing" --> keep["Worst per criterion kept: failure, then % failed,<br/>then failed points; Candidates = pairings"]
    worst -- no --> done
    keep --> done
    done["PairDone: the pair's outcomes, as it finishes (D30)<br/>runner turns them into rows; RowBatcher adds them to the grid every 250 ms"]
    done --> outcomes["ComparisonOutcome per criterion<br/>sorted by key, then criterion"]
    outcomes --> final["Grid replaced by the final rows in this order<br/>(what the CSV holds)"]
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

## 5. Water-tank scans against doses (prototype)

`ProfileBatch.Run` (DSClibrary/Profiles), driven by `tools/DSCprofiles`. Branch `crane/prototype-profile-comparison`,
2026-11-17.

```mermaid
flowchart TD
    scans["Scans folder: every *.mcc, recursive<br/>MCCParser, MCCMeasurementMapper: a Profile per scan"] --> pair
    doses["Doses folder: RTFolderReader, headers only<br/>optional plan-label filter"] --> pair
    pair["ScanDoseMatching.Match<br/>field on the tank axes (jaws turned by the collimator, or the applicator),<br/>energy, FFF, EDW angle and IN/OUT, SSD if the plan has one"] --> unpaired["Unpaired scans, with the closest miss"]
    pair --> group["Pairs grouped by dose; doses at once:<br/>physical cores, capped by half the free memory"]
    group --> load["DoseGridReader.Read, once per dose"]
    load --> frame["PhantomFrame.For: axis = isocentre x, z;<br/>surface = entry point, else plan SSD, else scan SSD, else the dose;<br/>the dose's surface checked against it"]
    frame --> sample["DoseLineSampler: the dose at the scan's positions<br/>(trilinear, as the engine); outside the grid dropped and counted"]
    sample --> shift{"Depth dose and<br/>a shift search?"}
    shift -- yes --> best["Read the dose at depth + s, best fit beyond dmax"] --> norm
    shift -- no --> norm["Normalise both: depth dose to max;<br/>profile to CAX, or max when CAX &lt; 80 % of max"]
    norm --> test{"Test"}
    test -- gamma --> gamma["GammaShell in 1D: reference points;<br/>evaluated side on an even grid (dose nodes, or the scan interpolated)"]
    test -- strict --> strict["HandWrittenComparer's rule along the line:<br/>dose difference, else the reference's range at +-distance"]
    gamma --> extra
    strict --> extra["Constancy over the central 80 % of the reference field;<br/>FauxPID metrics or depth-dose metrics, both sides"]
    extra --> csv["DSCprofiles: results, unpaired, curves and settings files"]
    extra --> figure["DSCplots.ProfilePlot: curves above, gamma or dose difference per point below"]
    figure --> files["DSCprofiles --plots / --plots-failing:<br/>one PNG or SVG per pair, named in the results' plot column"]
    figure --> gui["Graphs tab, dose versus scans (Avalonia, Eto):<br/>one pair on screen (ScanComparison.Run); Save plot, Save plots of listed scans"]
```

The Graphs tab runs the same steps for the chosen scan and dose: `ScanCatalog.Match` (the same `ScanDoseMatching`),
the grid loaded on its own, then `ScanComparison.Run`, which is `PhantomFrame.For` and `ProfileComparison.Compare`.
What it shows and what it saves are one figure, drawn by `ProfilePlot`.
Core selection: [core-selection.md](core-selection.md).

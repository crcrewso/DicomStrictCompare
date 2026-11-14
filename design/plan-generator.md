# Design note: a test-plan generator for commissioning datasets

**Status:** proposal, 2026-11-14. Nothing is built yet. Decisions are Cody's; they're labelled **PG-Q1** onwards.

## The problem

DicomStrictCompare answers "how different are these two sets of doses?". Getting those two sets is the slow part:
- someone has to build a phantom;
- plan dozens of square fields per machine and energy;
- export every dose per field;
- then do it all again on the next system, under names that pair up.

Our own reference patient shows the cost. QAtpsPTW exists in at least twelve copies across commissioning, TST,
UAT, PRD and the annual profile tests, and **none of them includes the phantom CT or structure set it was planned
on**. Every copy references a water tank that no export carries. So the dataset can't be rebuilt on a new
system from the files alone.

**The proposal:** a generator that writes, from a short spec:
- a synthetic water phantom (CT and RT Structure Set);
- RT Plans covering machines × energies × field sizes.

All of it imports into any TPS. The TPS calculates. DicomStrictCompare compares the result against the previous
system, or the measured scans.

## What existing datasets contain

This is a header-only survey of the card on 2026-11-14. The full listing is in
`~Resources/DSC-notes/qatpsptw-survey-2026-11-14.md`.

| Dataset | Machine | Contents |
|---|---|---|
| QAtpsPTW (PRD/TST/UAT, 2026) | Equinox | 6, 6FFF, 10, 10FFF, 15 MV squares 1–40 cm (small and large sets, AAA and AXB); EDW 10–60° at 10×10 and 20×20, including half fields; electrons 6–20 MeV on applicators A06–A25 with cutouts |
| QAtpsPTW AnnualProfileTesting (2024–25) | Journey | 6–15 MV squares 3–30 cm at SSD 100, and 10–25 cm at SSD 95; electrons 6–20 MeV on A10; **one plan per field** |
| QASR2019Eclipse flat sets | Courage, TrueBeam1CC | Squares 1–40 cm plus small fields (1×5, 2×3), AAA and Acuros; electrons on every applicator |

**Common to all of them:**
- 100 MU, gantry, collimator and couch at 0;
- jaw-defined fields with no MLC;
- one machine per plan;
- SSD 100 unless the label says otherwise.

That uniformity is what makes generation realistic.

## The minimum input

A spec, plus **one exported template plan per machine, with one field per energy** (see *Energies are
defined differently in different TPSs* below):

```yaml
patient:  {id: QAtpsGEN, name: "Commissioning^WaterTank"}
phantom:  {size_cm: [50, 50, 40], hu: 0, voxel_mm: 2}
machines:
  - template: Equinox_template.dcm      # an RT Plan with one field per energy, exported from the target TPS
    photons: [6X, 6FFF, 10X, 10FFF, 15X] # abstract labels; the template supplies each one's DICOM encoding
    electrons: {energies: [6E, 9E, 12E, 16E, 20E], applicators: [A10]}
fields:   {squares_cm: [1, 2, 3, 4, 5, 6, 8, 10, 15, 20, 30, 40], ssd_cm: [100], mu: 100}
plans:    per_machine_energy            # or per_field
```

**Why a template rather than a machine description:** the template carries the details that differ by TPS,
vendor and site. A spec would have to guess them, and a wrong guess fails import or, worse, imports as something
else:
- the exact `TreatmentMachineName`;
- the FFF fluence-mode ID;
- MLC leaf boundaries;
- applicator IDs and accessory codes;
- the tolerance table;
- dose rates.

pylinac's `TrueBeamPlanGenerator` works the same way.

**Defaults, all chosen so DicomStrictCompare can pair the results:**
- **Field names** encode the energy and size: `06x10x10`, `06f10x10`, `12eA10`. This follows the QAE18 field
  naming convention: leading zeros, `_` in place of decimals.
- **Plan labels** encode the machine, the field type and the SSD.
- **Each field has a unique name** within its plan, and each plan a unique label within the patient. Matching is by
  patient ID, plan label and field name, so a duplicate is an error, not a warning.
- **The isocentre** sits at the requested SSD on the anterior surface, on the central axis.
- **The jaws define the field**, with the MLC retracted.
- **One reference point per depth requested** (for example D05, D10), as COORDINATES points.

## Energies are defined differently in different TPSs

An RT Plan doesn't name an energy the way a physicist does. "6 FFF" becomes three attributes:
- `RadiationType`: PHOTON or ELECTRON;
- `NominalBeamEnergy`: 6;
- a fluence mode: `PrimaryFluenceModeSequence`, with `FluenceMode` STANDARD or NON_STANDARD and a
  `FluenceModeID` such as `FFF`.

How a TPS fills those in varies:
- **The numbers differ.** Our own data has 15 MV on the TrueBeams and 16 MV on Varian3, for the same clinical
  role.
- **FFF can be encoded differently.** One system uses a fluence-mode ID; another may use an energy name the
  standard doesn't define. Whether a given system writes `FFF`, `SRS` or something of its own is per vendor and
  version, so check it, don't assume it.
- **Some distinctions never reach DICOM.** Two beam models for the same nominal energy, such as a standard and a
  high-resolution 6 MV, may differ only inside the TPS.
- **Electrons** carry the energy in MeV with `RadiationType` ELECTRON, plus the applicator. The applicator ID is
  per site.

**So the generator never writes an energy from the spec directly:**
1. **The spec uses abstract labels:** `6X`, `6FFF`, `10FFF`, `15X`, `12E`. They mean "the beam this site uses for
   that role", not a DICOM value.
2. **The template supplies the encoding.** Each machine's template is **an RT Plan with one field per energy**,
   exported from the TPS that will import the plans. Its first control point gives the exact `RadiationType`,
   `NominalBeamEnergy` and fluence-mode items for each label. That one export per machine is the real minimum
   input, rather than one field.
3. **An explicit map is the fallback,** where a template field can't be had:
   ```yaml
   energy_map:
     6FFF: {radiation: PHOTON, nominal: 6, fluence: {mode: NON_STANDARD, id: FFF}}
     15X:  {radiation: PHOTON, nominal: 16}     # Varian3's "15X" role is 16 MV
   ```
4. **A missing label is an error.** If a label isn't in the template or the map, the build stops and names the
   label. It doesn't fall back to a guess.

**DicomStrictCompare has to match by the abstract label too.** When the source and target TPSs encode one beam
differently, comparing `NominalBeamEnergy` would pair the wrong doses or none. The generator puts the abstract
label in every field name (`06f10x10`), and DicomStrictCompare already matches on field name. So two systems that
disagree about how to encode 6 FFF still pair, as long as both imported plans keep the generated field names. The
build report should also record each label's DICOM encoding per TPS, so a reader can see what was compared with
what.

## Outputs

- **Phantom CT:**
  - a box of the given size at the given HU, in air, with margin;
  - head-first supine;
  - its own Study, Series and Frame of Reference;
  - a slice count and spacing a TPS accepts.
- **Phantom RT Structure Set:** BODY (EXTERNAL) and, optionally, the reference points as POIs.
- **RT Plans:** unapproved, with no dose. They reference the structure set, which references the CT.
- **A build report:** every plan and field generated, every combination skipped and why (for example, an energy
  the template machine doesn't have), and every UID root used.
- **New UIDs for everything,** under a root we hold or `2.25.<uuid>`, and never reused between builds (PG-Q3).

## What it would not do

- **Calculate dose.** The TPS does that, which is the point.
- **Set calculation options.** Algorithm, grid size and heterogeneity correction are TPS settings, not plan DICOM.
- **Guess shapes.** Electron cutouts, wedges and MLC-defined fields need their geometry as input, or a template
  field that already has it.
- **Promise identical behaviour on every TPS.** It targets DICOM that a TPS should accept, and is tested
  against the TPSs we have (PG-Q5).

## Risks

| Risk | Why | Mitigation |
|---|---|---|
| Water at 0 HU isn't 1.0 everywhere | each TPS maps HU through its own CT calibration curve | report it in the build output; offer a relative-density override (`ROIPhysicalProperties`) where the TPS imports one; the QAE18 phantoms use a −1000 HU CT with a BODY override of 1.0 |
| Import refused or silently altered | TPS-specific checks (machine, energy, applicator, tolerance table) | everything machine-specific comes from the template; first-import checklist per TPS |
| Mismatched pairs in DicomStrictCompare | naming drift between builds | deterministic names from the spec; the build report lists every expected (plan, field) pair, and DicomStrictCompare can check it |
| Grid geometry errors | orientation, slice spacing, Frame of Reference | reuse the checks already proven on the QAE18 patients: reference closure, UID uniqueness, no source UID surviving (`Prep18-notes/scripts/review_prep18.py`) |

## Where it lives, and in what language (PG-Q1, PG-Q2)

There are two credible routes:

1. **C#, as `DicomStrictCompare/tools/DSCgenerate/`, writing through EvilDICOM.**
   - One runtime and one install for DicomStrictCompare users.
   - It shares the units and DICOM code in `DSClibrary`.
   - It fits the aim of making DicomStrictCompare a project others contribute to.
2. **Python, grown from the QAE18 generators** in `~Resources/Prep18-notes/scripts/`.
   - Those already write valid CT, structure set and plan files under new UIDs, and passed an independent check.
   - They're Eclipse-specific and script-shaped today.

**Recommendation:** C# for the shipped tool. Port the proven rules from the Python generators: the UID remap that
fails on an unmapped UID, the rebuilt File Meta, and dropped private tags. Keep `review_prep18.py` as an
independent, cross-language check of the output. Because it's a different implementation, a bug in one won't hide
in both.

## Open questions

- **PG-Q1:** C# in this repository, or Python beside it?
- **PG-Q2:** a command-line tool only, or also a page in the new front end?
- **PG-Q3:** UID root: `2.25.<uuid>`, or a registered root?
- **PG-Q4:** phantom density: HU 0, a density override, or both as options?
- **PG-Q5:** which TPSs must it be tested against before release? Eclipse 15.6 and 18 are available. Anyone else's
  needs a volunteer.
- **PG-Q6:** first scope: photon squares and electron applicators only (no cutouts, wedges or MLC), as a first
  release?
- **PG-Q7:** should DicomStrictCompare read the build report, so it can say which expected pairs are missing,
  not only which target doses had no match?
- **PG-Q8:** who owns the abstract energy labels? A fixed list in the tool (`6X`, `6FFF`, …), or labels each site
  defines in its spec? A fixed list makes datasets comparable between sites; site labels cope with beams the list
  never foresaw.

## A first release that would be useful

- Photon jaw-defined squares at one or more SSDs.
- Electrons on open applicators.
- One water-box phantom.
- One template per machine.

Validate it by regenerating AnnualProfileTesting, which is single fields on Journey at SSD 95 and 100. Then
compare the new system's doses against the existing ones with DicomStrictCompare. If the two systems agree, the
generator and the comparison have checked each other.

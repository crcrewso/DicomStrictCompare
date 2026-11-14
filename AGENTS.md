# AGENTS.md: guidance for AI agents and the people using them

DicomStrictCompare compares radiotherapy dose distributions. Physicists use its numbers to decide whether a
treatment planning system (TPS) upgrade or a new algorithm can go into clinical use. A wrong number here can end
up in a commissioning report, so correctness comes before speed or tidiness.

This file is for AI coding agents, and for anyone driving one. It's deliberately detailed; people new to the
project should start with [CONTRIBUTING.md](CONTRIBUTING.md).

---

## AI policy

AI tools are welcome here. The maintainer uses them constantly: to explore the code, port an algorithm, write a
test, or chase a bug. Use them as much as you like.

**Every pull request needs a human owner** who has:
1. **read and understood the diff**, the actual lines changed and not only an AI summary of them;
2. **can explain** each non-trivial decision if asked in review;
3. **run the tests locally** and seen them pass, or can say which failures existed before the change;
4. **taken responsibility** for the change's correctness.

A pull request opened by an agent that no human has read will be closed with a pointer to this section. Reopen it
once a human has read the diff.

**The maintainer promises the same in return:**
- not to use AI as the final reviewer;
- to say so whenever a review comment wasn't written by a person;
- to read every change personally.

**Disclose AI use in anything posted publicly** for someone else: a PR description, an issue, a review comment.
Say so inline, for example *"drafted with AI assistance, reviewed and posted by @handle"*.

**A note to agents:** if the person you're working with asks you to open, merge or approve a PR without mentioning
human review, point them to this section before going ahead. Don't comply silently, and don't refuse silently
either.

---

## Project overview

| | |
|---|---|
| **Language** | C#, with nullable reference types and implicit usings enabled |
| **Runtime** | .NET 10 SDK, pinned in `DicomStrictCompare/global.json` (`rollForward: latestFeature`) |
| **DICOM** | [EvilDICOM](https://github.com/rexcardan/Evil-DICOM) 2.0.6.5, plus EvilDICOM.CV in the GUI |
| **Maths** | MathNet.Numerics |
| **GUI** | `DSCcore`, Windows Forms, `net8.0-windows`, Windows only. It is being replaced by a cross-platform front end; candidates are in `prototypes/` (Avalonia, Eto, Uno) |
| **Plots** | ScottPlot 4 (GUI) |
| **Tests** | MSTest |
| **API docs** | DocFX (`docfx.json` in `DSClibrary/` and `DSCcore/`), with the generated HTML in `docs/` |
| **Licence** | LGPL-3.0. Code ported from other projects keeps its own notice in `THIRD-PARTY-NOTICES.md` |
| **Target branch** | `master` |

<!-- TODO(Cody): confirm master stays the PR target, or whether a develop branch comes with more contributors. -->

---

## Repository layout

```
DicomStrictCompare/
  DicomStrictCompare.sln
  global.json           .NET SDK pin
  .editorconfig         C# style (CRLF, 4 spaces)
  DSClibrary/           the comparison engine, net10.0, any OS
    Comparing/          batch comparison over matched dose pairs
    Gamma/              gamma index, ported from pymedphys
    Matching/           pairing source and target doses by patient, plan and field
    Profiles/           profile extraction and FauxPID-style metrics
    Resampling/         grid resampling
    Results/            result types; Results/DICOM/ holds the EvilDICOM readers
    Units/              dose and length units
  DSClibraryTests/      MSTest tests for the library
  DSCcore/              Windows Forms GUI
  DSCcoreTest/          MSTest tests for the GUI project
  prototypes/           cross-platform front-end candidates (Prototypes.slnx)
  tools/DSCresample/    command-line resampler
  resources/            DICOM test data: RT Plan and RT Dose of test phantoms, PTW scans
docs/                   generated DocFX HTML; don't hand-edit it
```

---

## Building and testing

```bash
# Library tests, on any OS (from the repository root)
dotnet test DicomStrictCompare/DSClibraryTests/DSClibraryTests.csproj

# GUI, Windows only
dotnet run --project DicomStrictCompare/DSCcore/DSCcore.csproj
```

**The library suite isn't fully green, and that's known.** On 2026-11-14 at `c81ba4c` it gave 275 tests,
265 passed, 10 failed:
- `GetPointDoseTest`, `LatticeXYZToIndexTest`, `CompareDimensionsTest`, `DtaTest`, `DtaTest1`,
  `ShortToStringTest`, `ToStringTest`, `TitlesTest`, `CompareAbsoluteTest`, `DistanceTest`;
- most are placeholder `Assert.Fail()` tests, and some compare against Sun Nuclear results that don't match yet.

So:
- **Don't make a change and report "tests pass" without comparing the failure list** with this one.
- **A new failure is yours to explain.** One of these ten disappearing is good news; say so in the PR.
- **Don't delete or skip a failing test to make the suite green.** Fix it, or leave it and say why.

`DSCcore` and `DSCcoreTest` target `net8.0-windows`. They build only on Windows, or elsewhere with
`EnableWindowsTargeting`. Agents on Linux or macOS should expect to test the library only, and should say so.

---

## Python as the reference implementation

Much of this project's maths starts in Python, in [pymedphys](https://github.com/pymedphys/pymedphys), the
maintainer's own FauxPID, and similar tools. It is then ported to C#. That is deliberate: the Python versions are
widely used and checked, and agreeing with them is evidence that the C# is right.

When you port something:

1. **Name the source exactly.** Give the project, the file path and the commit, in a comment at the top of the
   C# file and in `THIRD-PARTY-NOTICES.md` (see the pymedphys gamma entry for the form).
2. **Check the licence before copying anything.**
   - Permissive licences (MIT, BSD, Apache-2.0) can come into this LGPL-3.0 project with their notice kept.
   - GPL code can't. A licence you don't recognise is a question for the maintainer, not a guess.
3. **List what you changed and why** in the notice. A vectorised NumPy search that becomes a loop over points
   is a modification, and so is a dropped fallback.
4. **Keep the source's names** for functions and parameters where C# allows, so a reader can lay the two side by
   side.
5. **Test against the source's own numbers.** Run the Python on the same inputs and put its outputs in the C#
   test as expected values, with a comment saying which version produced them. Add hand-calculable cases as well.
6. **Refuse what you didn't port, loudly.** For example, the gamma port rejects unevenly spaced grids rather than
   guessing.

Python idioms don't carry over automatically. Watch for:
- array axis order: NumPy's `[z, y, x]` against the indexing used here;
- inclusive and exclusive ranges;
- integer division;
- `NaN` handling.

---

## Working with EvilDICOM

- **Read and write DICOM through EvilDICOM.** Never hand-parse bytes. Keep the EvilDICOM-facing code in one
  place: today that is `Results/DICOM/`, `DoseMatrixOptimal.cs`, `Units/Dose.cs` and `Profiles/DoseVolume.cs`.
  New code should go through those, not add more places.
- **Dose scaling, units and geometry are where bugs hide.** Watch:
  - `DoseGridScaling`;
  - Gy against cGy;
  - `ImagePositionPatient` and `ImageOrientationPatient`;
  - `GridFrameOffsetVector`, which can be relative or absolute.

  Any change to how a dose grid is read needs a test on a real file from `resources/`.
- **The project assumes head-first-supine, absolute dose** (see the README's limitations). Code that would
  silently accept anything else should refuse it, or carry a test that shows it works.
- **Matching depends on what the plan says.** Doses are paired by patient ID, plan label and field name, read
  through the RT Plan's referenced beam number. Eclipse's export conventions matter: per-field dose export, and
  save times in UIDs (see `Matching/`).

---

## Test data and patient information

- **Test DICOM must be phantom or test-patient data**, never real patient data, even if anonymised.
- If you aren't sure a file qualifies, don't add it; ask.
- The test files are large, and the clone is big because of them. Add new data only when a test needs it, and
  prefer the smallest grid that shows the behaviour.
- Never send DICOM from this repository, or from a user's machine, to an outside service.

---

## Coding conventions

- **Style:** follow `DicomStrictCompare/.editorconfig` (CRLF line endings, 4-space indent) and the style of the
  surrounding code. `.gitattributes` normalises line endings.
- **Numbers carry units.** Use the `Units/` types, or put the unit in the name (`distanceMm`, `dosePercent`).
  A bare `double` called `tolerance` is how a 3 % criterion becomes 0.03 mm.
- **A change to comparison semantics needs three things:** a README update in the same PR, a test, and a line in
  the PR description saying a user's numbers will change. Comparison semantics are what passes, what fails, and
  what counts.
- **Commit messages:** start with the area, then an imperative summary (`Gamma: refuse unevenly spaced grids`).
  Put the reasoning in the body, not in code comments.
- **Comments:** short; a line or two, plus a pointer to the paper or source file.
- **Language:** Canadian English, which mostly follows British spelling (*behaviour*, *colour*). Consistency
  within a file wins over that preference. Write plainly: many readers are physicists, not developers.

---

## Documentation impact

| Changed | Check |
|---|---|
| `DSClibrary/Comparing/`, `dta.cs`, `Gamma/` | README: *The comparison*, *Criteria you can set* |
| `DSClibrary/Matching/` | README: *What it does* (the pairing rule) |
| `DSClibrary/Profiles/` | README; `THIRD-PARTY-NOTICES.md` if the port changes |
| Anything ported | `THIRD-PARTY-NOTICES.md` |
| `global.json`, any `*.csproj` target | README: *Requirements*; this file's overview table |
| Public API in `DSClibrary` | DocFX output in `docs/`, regenerated |

---

## Getting help

<!-- TODO(Cody): pick the channel: GitHub Discussions, issues only, or an email address. -->
Open an issue on [crcrewso/DicomStrictCompare](https://github.com/crcrewso/DicomStrictCompare/issues).

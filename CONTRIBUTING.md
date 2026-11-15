# Contributing to DicomStrictCompare

Thank you for looking! DicomStrictCompare is a free, open-source tool for comparing folders of radiotherapy dose
distributions, written for treatment planning system commissioning and upgrades. It started as one physicist's
tool, and it's now open to anyone who does this work and wants it done well.

Contributions of every size are welcome: from a typo fix, to a test dataset, to a new comparison method. Many
medical physicists have never contributed to an open-source project. If that's you, you're exactly who this guide
is written for. Please assume everyone is acting in good faith. Extend the patience you'd want yourself,
including to people writing in a second language, whose words may read more bluntly than they mean.

By submitting a contribution, you agree it is licensed under the project's
[GNU Lesser General Public License v3.0](LICENSE).

If you use an AI coding tool, read the **AI policy** in [AGENTS.md](AGENTS.md#ai-policy). In short: AI-assisted
contributions are welcome when a person has read, understood and tested them.

## Ways to contribute

You don't need to write C# to help.

- **Report a bug.** If a comparison gives a number you don't believe, that is the most valuable report there is.
  Say:
  - what you compared;
  - the criteria;
  - what you expected, and why (a commercial tool's result, a hand calculation, a known physical property).
- **Validate it against something else.** Compare a result here with SNC Patient, PTW VeriSoft, pymedphys or
  another tool, and tell us whether they agree. Agreement and disagreement are both useful.
- **Share test data.** Phantom or test-patient RT Plan and RT Dose files from your TPS let us test against more
  than one vendor's exports. **Never real patient data, even anonymised.**
- **Port an algorithm.** Much of the maths here is ported from Python. See
  [AGENTS.md](AGENTS.md#python-as-the-reference-implementation) for how ports are done and credited.
- **Improve the documentation.** If the README left you guessing, the fix helps the next person.
- **Try a front end.** The Windows Forms GUI is being replaced by a cross-platform one; the candidates are in
  `DicomStrictCompare/prototypes/`. An opinion from someone who'll use it is worth having.

## Reporting bugs

1. Check the [open issues](https://github.com/crcrewso/DicomStrictCompare/issues) first.
2. Open an issue with:
   - the DicomStrictCompare version or commit, and your operating system;
   - the TPS and version that produced the files, and how they were exported (per field or per plan);
   - the criteria you used (tolerance, DTA, threshold, global or local, trim);
   - what happened, and what you expected;
   - the CSV and `-errors.txt` output, if there is any.
3. **Don't attach DICOM unless it is phantom data.** If a bug only shows on clinical data, say so, and we'll work
   out a way to reproduce it on a phantom.

## Suggesting features

Describe the problem you're trying to solve, not only the solution you have in mind. "I need to know which fields
fail in the penumbra" leads somewhere better than "add a penumbra checkbox".

## Contributing code

### Setting up

1. **Install the .NET SDK** named in `DicomStrictCompare/global.json` (10.0 today).
   - The Windows Forms GUI also needs Windows and the .NET 8 desktop workload.
   - Visual Studio 2022 or later, Rider and VS Code all work.
2. **Fork and clone the repository.** The clone is large because of the test DICOM.
3. **Run the library tests** from the repository root:
   ```bash
   dotnet test DicomStrictCompare/DSClibraryTests/DSClibraryTests.csproj
   ```
   **Ten tests fail today, and that's known.** [AGENTS.md](AGENTS.md#building-and-testing) lists them. Check
   that your change doesn't add to the list.
4. **On Windows, run the GUI:**
   ```bash
   dotnet run --project DicomStrictCompare/DSCcore/DSCcore.csproj
   ```

### Guidelines

Do your best with these, and don't worry if you miss one. Reviewers are here to help get your work in.

- **Test every change to the maths.** A small synthetic grid with an answer you can work out by hand is ideal;
  `ComparisonRulesTests.cs` shows the style. If you aren't sure how to test something, ask. That's a normal
  question, not a failure.
- **Say when results change.** If your change alters what passes or fails, update the README and say so plainly
  in the PR.
- **Follow the existing style.** `.editorconfig` covers most of it.
- **Keep PRs focused.** One idea per PR is easier to review and quicker to merge.
- **Write clear commit messages:** the area, a colon, then an imperative summary (`Matching: ignore Eclipse save
  time in plan UIDs`). Put the detail in the body.

### Opening a pull request

1. Push your branch to your fork.
2. Open the PR against `master`.
3. Say what the change does and why, how you tested it, and whether anyone's results will change.
4. Expect review comments. That's how a tool people rely on stays reliable, not a judgement of your work.

<!-- TODO(crcrewso): add .github/pull_request_template.md (QATrack+'s is a good model), and CI that runs the library
tests on Linux and Windows. Neither exists yet. -->

## Contributors

DicomStrictCompare is maintained by [@crcrewso](https://github.com/crcrewso). Everyone who
contributes code, data, validation or documentation will be listed here.

<!-- TODO(crcrewso): git history also shows "Icaarus" (3 commits). Is that you, or a past contributor to credit?
Decide too whether to list AI agents (crane, Copilot, Claude) here or only in commit trailers. -->

The project builds on others' work: [EvilDICOM](https://github.com/rexcardan/Evil-DICOM) by Rex Cardan, and the
gamma implementation of [pymedphys](https://github.com/pymedphys/pymedphys). See
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

## Code of conduct

<!-- TODO(crcrewso): add CODE_OF_CONDUCT.md, for example QATrack+'s, or the Contributor Covenant. -->
Be kind, assume good faith, and treat people the way you'd want to be treated. If something goes wrong, contact
the maintainer.

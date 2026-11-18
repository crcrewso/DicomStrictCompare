using DSClibrary.Comparing;
using DSClibrary.Studies;
using DSCprototypes.Shared.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSCprototypes.Shared.Tests;

/// <summary>
/// The Threads list and the machine settings file (crcrewso, 2026-11-15): steps of 1 to 4, then 2 to 12, then 4,
/// defaulting to the recommended count; machine settings kept in their own file, not in a study.
/// </summary>
[TestClass]
public class ThreadChoiceTests
{
    private string _dir = "";
    [TestInitialize] public void MakeDir() => Directory.CreateDirectory(_dir = Path.Combine(Path.GetTempPath(), "dsc-settings-" + Guid.NewGuid().ToString("N")));
    [TestCleanup] public void RemoveDir() => Directory.Delete(_dir, recursive: true);

    private static MainViewModel NewMain(string? settingsPath = null) => new((_, _) => Task.FromResult<string?>(null), settingsPath);

    [TestMethod]
    public void TheStepsAreOneToFourThenTwoToTwelveThenFour()
    {
        CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 6, 8, 10, 12, 16, 20, 24, 28, 32 }, MachineSettings.ThreadCounts(32, 16).ToArray());
        CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 6, 8, 10, 12, 16 }, MachineSettings.ThreadCounts(16, 12).ToArray());
        // An off-step recommended count and maximum are listed too.
        CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 6, 7, 8, 10, 12, 16, 18 }, MachineSettings.ThreadCounts(18, 7).ToArray());
        CollectionAssert.AreEqual(new[] { 1, 2 }, MachineSettings.ThreadCounts(2, 2).ToArray());
        // A count between steps rounds down to a listed one.
        var listed = MachineSettings.ThreadCounts(32, 16);
        Assert.AreEqual(12, MachineSettings.ListedThreads(listed, 14));
        Assert.AreEqual(32, MachineSettings.ListedThreads(listed, 1000));
        Assert.AreEqual(1, MachineSettings.ListedThreads(listed, 0));
    }

    [TestMethod]
    public void TheListDefaultsToTheRecommendedCount()
    {
        var vm = NewMain();
        CollectionAssert.AreEqual(MachineSettings.ThreadCounts(Environment.ProcessorCount, CpuTopology.PhysicalCores).ToArray(),
            vm.ThreadChoices.Select(c => c.Count).ToArray());
        Assert.AreEqual(1, vm.ThreadChoices.Count(c => c.Recommended));
        Assert.AreEqual($"{CpuTopology.PhysicalCores} (recommended)", vm.Threads.ToString());
        Assert.IsNull(vm.ComparerSettings.Threads);   // kept as "recommended", so the file follows the machine
    }

    [TestMethod]
    public void TheAgreedReadingDefaults()
    {
        Assert.IsTrue(MachineSettings.Defaults.PartialTargetReads);
        Assert.AreEqual(4, MachineSettings.Defaults.Readers);
        var run = NewMain().RunSettings;
        Assert.IsTrue(run.PartialTargetReads);
        Assert.AreEqual(4, run.Queue!.Readers);
    }

    [TestMethod]
    public void TheWindowStartsAtTheAgreedCriteriaAndCaxMax()
    {
        // crcrewso, 2026-11-18: 0.1 %/0 mm, 3 %/2 mm local, 3 %/2 mm global gamma, CAX normalisation.
        CollectionAssert.AreEqual(new[]
        {
            new DtaSpec(0.1, 0, 10, Global: true),
            new DtaSpec(3, 2, 10, Global: false),
            new DtaSpec(3, 2, 10, Global: true, Gamma: true),
        }, DtaSpec.Defaults.ToArray());
        var vm = NewMain(Path.Combine(_dir, "settings.json"));
        Assert.AreEqual(MaximumKind.CaxMax, vm.Maximum);
        CollectionAssert.AreEqual(DtaSpec.Defaults.ToArray(), vm.Criteria.Select(c => c.ToSpec()).ToArray());
    }

    [TestMethod]
    public void SettingsAreKeptInTheirFileAndNotTakenFromAStudy()
    {
        string path = Path.Combine(_dir, "settings.json");
        var vm = NewMain(path);
        Assert.IsFalse(File.Exists(path), "nothing is written until a setting changes");
        vm.Threads = vm.ThreadChoices[0];
        vm.Engine = ComparerKind.MultiThread;
        vm.Readers = 2;
        Assert.IsTrue(File.Exists(path));

        var again = NewMain(path);
        Assert.AreEqual(1, again.Threads.Count);
        Assert.AreEqual(ComparerKind.MultiThread, again.Engine);
        Assert.AreEqual(2, again.Readers);

        // A study decides Max and GPU precision only; this machine's engine and threads stay.
        again.Apply(new StudyRunSettings(MaximumKind.GlobalMax, new ComparerSettings(ComparerKind.OpenCl, 8, GpuPrecision.Float)));
        Assert.AreEqual(MaximumKind.GlobalMax, again.Maximum);
        Assert.IsTrue(again.GpuFloat);
        Assert.AreEqual(ComparerKind.MultiThread, again.Engine);
        Assert.AreEqual(1, again.Threads.Count);
    }

    [TestMethod]
    public void AnUnreadableFileGivesTheDefaultsAndSaysSo()
    {
        string path = Path.Combine(_dir, "settings.json");
        File.WriteAllText(path, "{ not json");
        var s = MachineSettings.Load(path, out var problem);
        Assert.AreEqual(MachineSettings.Defaults, s);
        StringAssert.Contains(problem, "using the defaults");
        Assert.AreEqual(MachineSettings.Defaults, MachineSettings.Load(Path.Combine(_dir, "missing.json"), out var none));
        Assert.IsNull(none);
    }
}

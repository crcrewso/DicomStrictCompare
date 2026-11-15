using DSClibrary.Comparing;
using DSClibrary.Studies;
using DSCprototypes.Shared.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSCprototypes.Shared.Tests;

/// <summary>The Threads list (crcrewso, 2026-11-15): 1 to every logical processor, defaulting to the recommended count.</summary>
[TestClass]
public class ThreadChoiceTests
{
    private static MainViewModel NewMain() => new((_, _) => Task.FromResult<string?>(null));

    [TestMethod]
    public void TheListRunsToEveryLogicalProcessorAndDefaultsToTheRecommendedCount()
    {
        var vm = NewMain();
        CollectionAssert.AreEqual(Enumerable.Range(1, Environment.ProcessorCount).ToArray(), vm.ThreadChoices.Select(c => c.Count).ToArray());
        Assert.AreEqual(1, vm.ThreadChoices.Count(c => c.Recommended));
        Assert.AreEqual(CpuTopology.PhysicalCores, vm.Threads.Count);
        Assert.AreEqual($"{CpuTopology.PhysicalCores} (recommended)", vm.Threads.ToString());
        Assert.IsNull(vm.ComparerSettings.Threads);   // saved as "recommended", so another machine uses its own
    }

    [TestMethod]
    public void AChosenCountIsSavedAndRestored()
    {
        var vm = NewMain();
        vm.Threads = vm.ThreadChoices[0];
        Assert.AreEqual(1, vm.ComparerSettings.Threads);
        Assert.AreEqual("1", vm.Threads.ToString().Replace(" (recommended)", "", StringComparison.Ordinal));

        var other = NewMain();
        other.Apply(vm.RunSettings);
        Assert.AreEqual(1, other.Threads.Count);
        // A saved count above this machine's processors falls back to the most it has.
        other.Apply(new StudyRunSettings(Comparer: new ComparerSettings(Threads: 100_000)));
        Assert.AreEqual(Environment.ProcessorCount, other.Threads.Count);
        other.Apply(StudyRunSettings.Default);
        Assert.IsTrue(other.Threads.Recommended);
    }
}

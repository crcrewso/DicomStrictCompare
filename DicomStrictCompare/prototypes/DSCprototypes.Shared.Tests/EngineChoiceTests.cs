using DSClibrary.Comparing;
using DSCprototypes.Shared.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSCprototypes.Shared.Tests;

/// <summary>The Settings tab's engine list (2026-11-17): plain names over the settings' code names.</summary>
[TestClass]
public class EngineChoiceTests
{
    [TestMethod]
    public void EveryEngineHasOnePlainNameInTheEnumsOrder()
    {
        // The Eto window maps the list's index to the engine, so the order must be the enum's.
        CollectionAssert.AreEqual(Enum.GetValues<ComparerKind>(), EngineChoice.All.Select(e => e.Kind).ToArray());
        Assert.IsTrue(EngineChoice.All.All(e => e.Label.Length > 0 && e.Description.Length > 40), "every engine is named and described");
        Assert.AreEqual(EngineChoice.All.Count, EngineChoice.All.Select(e => e.Label).Distinct().Count());
        Assert.IsTrue(EngineChoice.For(ComparerKind.Ilgpu).Label.Contains("Graphics card"));
        Assert.IsTrue(EngineChoice.For(ComparerKind.HandWritten).Label.StartsWith("Reference"));
    }

    [TestMethod]
    public void ChoosingAnEngineByNameSetsTheSettingAndItsDescription()
    {
        var vm = new MainViewModel((_, _) => Task.FromResult<string?>(null));
        Assert.AreEqual(ComparerKind.HandWritten, vm.SelectedEngine.Kind);
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        vm.SelectedEngine = EngineChoice.For(ComparerKind.OpenCl);
        Assert.AreEqual(ComparerKind.OpenCl, vm.Engine, "the saved setting keeps its code name");
        Assert.AreEqual(EngineChoice.For(ComparerKind.OpenCl).Description, vm.EngineDescription);
        CollectionAssert.IsSubsetOf(new[] { nameof(MainViewModel.SelectedEngine), nameof(MainViewModel.EngineDescription) }, changed);
    }
}

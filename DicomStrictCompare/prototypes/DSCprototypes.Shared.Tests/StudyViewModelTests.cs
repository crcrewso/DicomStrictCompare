using DSClibrary.Matching;
using DSClibrary.Studies;
using DSCprototypes.Shared;
using DSCprototypes.Shared.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DSCprototypes.Shared.Tests;

/// <summary>What a person types into the Study tab, and the depths setting on the Graphs tab (F7).</summary>
[TestClass]
public class StudyViewModelTests
{
    private static StudyViewModel NewStudy() =>
        new((_, _) => Task.FromResult<string?>(null), () => DtaSpec.Defaults);

    [TestMethod]
    public void RulesAndOverridesParseFromText()
    {
        var c = new SetComparisonViewModel
        {
            SourceSet = "AAA",
            TargetSet = "AXB",
            LabelRules = "(AAA|AXB) -> {algorithm}",
            FieldRules = "-100[AX]- -> -100{algorithm}-",
            Overrides = "SquareFieldsS = SquFldsSAXB; SquareE = SquEAXB",
        }.ToComparison();

        Assert.AreEqual(DoseMatchStrategy.Identifier, c.Rules.Strategy);
        Assert.AreEqual("STBjaw{algorithm}", c.Rules.SourceKeyLabel("STBjawAAA"));
        Assert.AreEqual("X-100{algorithm}-10.0-1", c.Rules.KeyFieldName("X-100X-10.0-1"));
        Assert.AreEqual("SquFldsSAXB", c.Rules.SourceKeyLabel("SquareFieldsS"));
        Assert.AreEqual(2, c.Rules.LabelOverrides.Count);
    }

    [TestMethod]
    public void AMalformedRuleSaysWhatFormItNeeds()
    {
        var ex = Assert.ThrowsExactly<FormatException>(() =>
            new SetComparisonViewModel { SourceSet = "A", TargetSet = "B", LabelRules = "AAA" }.ToComparison());
        StringAssert.Contains(ex.Message, "pattern -> replacement");
    }

    [TestMethod]
    public void TheAlgorithmButtonFillsInTheRule()
    {
        var c = new SetComparisonViewModel { MatchByUid = true };
        c.UseAcrossAlgorithmsCommand.Execute(null);
        Assert.IsFalse(c.MatchByUid, "label rules need identifier matching");
        Assert.AreEqual("(AAA|AXB) -> {algorithm}", c.LabelRules);
    }

    [TestMethod]
    public void AStudyThatCannotRunSaysWhy()
    {
        var vm = NewStudy();
        vm.Sets.Add(new DoseSetViewModel { Name = "A", Folder = "/no/such/folder" });
        vm.Comparisons.Add(new SetComparisonViewModel { SourceSet = "A", TargetSet = "B" });
        Assert.IsNull(vm.BuildStudy(out var problem));
        StringAssert.Contains(problem, "no dose set is named 'B'");
        StringAssert.Contains(problem, "does not exist");
    }

    [TestMethod]
    public void ARoundTripThroughTheRowsKeepsTheComparison()
    {
        var original = new SetComparison("AAA", "AXB", PairingRules.AcrossAlgorithms());
        var back = SetComparisonViewModel.From(original).ToComparison();
        Assert.AreEqual(original.Name, back.Name);
        Assert.AreEqual(original.Rules.ToString(), back.Rules.ToString());
    }

    [TestMethod]
    public void TheGraphsTabOffersTheDepthsTyped()
    {
        var graph = new GraphViewModel(ComparisonRunner.LoadWithEvilDicom);
        CollectionAssert.AreEqual(new[] { "Dmax", "5 cm", "10 cm", "20 cm", "All depths" }, graph.DepthChoices.Select(d => d.Label).ToArray());

        graph.DepthsText = "15, 50, 300";
        CollectionAssert.AreEqual(new[] { "15 mm", "5 cm", "30 cm", "All depths" }, graph.DepthChoices.Select(d => d.Label).ToArray());
        Assert.AreEqual(15.0, graph.DepthChoices[0].Mm);
    }
}

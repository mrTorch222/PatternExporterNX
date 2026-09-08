using FlatPatternExporter.Models;
using FlatPatternExporter.Services;

namespace PatternExporterNX.Tests;

public sealed class HierarchyQuantityCalculatorTests
{
    [Fact]
    public void Calculate_MergesBranchesAndAppliesMultiplierOnlyToSelectedSubassembly()
    {
        var root = Node("0", "Main");
        var multiplied = Node("0.1", "Multiplied", root);
        multiplied.Multiplier = 5;
        root.Children.Add(multiplied);
        var contributions = new[]
        {
            new PartOccurrenceContribution("P-01", root.Id, "Main", 2),
            new PartOccurrenceContribution("P-01", multiplied.Id, "Main / Multiplied", 1)
        };

        var result = HierarchyQuantityCalculator.Calculate([root], contributions, mergeDuplicateFiles: true);

        var part = Assert.Single(result);
        Assert.Equal("P-01", part.PartNumber);
        Assert.Equal(7, part.Quantity);
    }

    [Fact]
    public void Calculate_MultipliesNestedBranchMultipliers()
    {
        var root = Node("0", "Main");
        var first = Node("0.1", "First", root);
        var second = Node("0.1.1", "Second", first);
        root.Multiplier = 2;
        first.Multiplier = 3;
        second.Multiplier = 4;
        root.Children.Add(first);
        first.Children.Add(second);

        var result = HierarchyQuantityCalculator.Calculate(
            [root],
            [new PartOccurrenceContribution("P-02", second.Id, "Main / First / Second", 1)],
            mergeDuplicateFiles: true);

        Assert.Equal(24, Assert.Single(result).Quantity);
    }

    [Fact]
    public void Calculate_KeepsBranchRowsWhenDuplicateMergingIsDisabled()
    {
        var root = Node("0", "Main");
        var child = Node("0.1", "Child", root);
        root.Children.Add(child);
        var contributions = new[]
        {
            new PartOccurrenceContribution("P-03", root.Id, "Main", 1),
            new PartOccurrenceContribution("P-03", child.Id, "Main / Child", 1)
        };

        var result = HierarchyQuantityCalculator.Calculate([root], contributions, mergeDuplicateFiles: false);

        Assert.Equal(2, result.Count);
        Assert.Equal(2, result.Sum(item => item.Quantity));
        Assert.Equal(2, result.Select(item => item.Key).Distinct().Count());
    }

    private static AssemblyHierarchyNode Node(string id, string name, AssemblyHierarchyNode? parent = null) => new()
    {
        Id = id,
        Name = name,
        Parent = parent
    };
}

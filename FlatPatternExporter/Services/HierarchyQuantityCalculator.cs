using FlatPatternExporter.Models;

namespace FlatPatternExporter.Services;

public static class HierarchyQuantityCalculator
{
    public static IReadOnlyList<HierarchyPartQuantity> Calculate(
        IEnumerable<AssemblyHierarchyNode> roots,
        IEnumerable<PartOccurrenceContribution> contributions,
        bool mergeDuplicateFiles)
    {
        var nodes = Flatten(roots).ToDictionary(node => node.Id, StringComparer.OrdinalIgnoreCase);
        var weighted = contributions.Select(contribution =>
        {
            var multiplier = nodes.TryGetValue(contribution.AssemblyNodeId, out var node)
                ? node.EffectiveMultiplier
                : 1;
            return new HierarchyPartQuantity(
                $"{contribution.AssemblyNodeId}|{contribution.PartNumber}",
                contribution.PartNumber,
                contribution.StructurePath,
                (int)Math.Min(int.MaxValue, (long)contribution.BaseQuantity * multiplier));
        });

        if (!mergeDuplicateFiles)
            return weighted.OrderBy(item => item.StructurePath).ThenBy(item => item.PartNumber).ToList();

        return weighted
            .GroupBy(item => item.PartNumber, StringComparer.OrdinalIgnoreCase)
            .Select(group => new HierarchyPartQuantity(
                group.Key,
                group.Key,
                string.Join("; ", group.Select(item => item.StructurePath).Distinct(StringComparer.OrdinalIgnoreCase)),
                (int)Math.Min(int.MaxValue, group.Sum(item => (long)item.Quantity))))
            .OrderBy(item => item.PartNumber)
            .ToList();
    }

    private static IEnumerable<AssemblyHierarchyNode> Flatten(IEnumerable<AssemblyHierarchyNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in Flatten(node.Children))
                yield return child;
        }
    }
}

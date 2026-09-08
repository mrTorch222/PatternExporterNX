using Inventor;

namespace FlatPatternExporter.Core;

public static class SheetMetalBendAnalyzer
{
    public static BendMetrics Analyze(SheetMetalComponentDefinition definition)
    {
        var bendCount = definition.Bends.Count;
        if (!definition.HasFlatPattern) return new BendMetrics(bendCount, null, null, null);

        try
        {
            var results = definition.FlatPattern.FlatBendResults;
            var longestLengthMm = 0.0;
            var bendsUp = 0;
            var bendsDown = 0;

            foreach (FlatBendResult result in results)
            {
                if (result.IsOnBottomFace) continue;
                if (result.IsDirectionUp) bendsUp++;
                else bendsDown++;

                var evaluator = result.Edge.Evaluator;
                evaluator.GetParamExtents(out var minimum, out var maximum);
                evaluator.GetLengthAtParam(minimum, maximum, out var lengthCm);
                longestLengthMm = Math.Max(longestLengthMm, lengthCm * 10.0);
            }

            return new BendMetrics(bendCount, bendCount == 0 ? null : longestLengthMm, bendsUp, bendsDown);
        }
        catch
        {
            return new BendMetrics(bendCount, null, null, null);
        }
    }
}

public sealed record BendMetrics(int Count, double? LongestLengthMm, int? UpCount, int? DownCount);

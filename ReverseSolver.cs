using System.Diagnostics;

namespace NFSWPerformanceTool;

public sealed class ReverseSolver
{
    private readonly IReadOnlyList<PartDefinition> _allParts;

    public ReverseSolver(IReadOnlyList<PartDefinition> allParts) => _allParts = allParts;

    private sealed class PartGroup
    {
        public required RatingTriple Triple { get; init; }
        public required List<PartDefinition> Parts { get; init; }
        public PartDefinition Representative => Parts[0];
    }

    private sealed record HalfCombo(RatingTriple Sum, PartGroup A, PartGroup B, PartGroup C);

    public ReverseSearchResult Search(
        CarDefinition car,
        int wantedTopSpeed,
        int wantedAcceleration,
        int wantedHandling,
        bool includeCustom,
        bool allowEmptySlots,
        DisplayRoundingMode roundingMode,
        int tolerance,
        int maxResults,
        CancellationToken token,
        IProgress<string>? progress = null)
    {
        var sw = Stopwatch.StartNew();
        var result = new ReverseSearchResult();

        var bySlot = BuildPartsBySlot(includeCustom, allowEmptySlots);
        progress?.Report("1/3: searching possible H/A/T aggregate sums...");
        result.AggregateCandidates.AddRange(FindAggregateCandidates(
            car, wantedTopSpeed, wantedAcceleration, wantedHandling,
            bySlot, roundingMode, tolerance, token));

        if (result.AggregateCandidates.Count == 0)
        {
            sw.Stop();
            result.Elapsed = sw.Elapsed;
            return result;
        }

        token.ThrowIfCancellationRequested();
        progress?.Report($"2/3: {result.AggregateCandidates.Count} aggregate candidate(s); building part index...");
        var groups = BuildGroups(bySlot);

        var first = BuildFirstHalf(groups, token);
        var second = BuildSecondHalfMap(groups, token);

        progress?.Report("3/3: matching concrete Engine/Turbo/Transmission/Suspension/Brakes/Tires combinations...");
        foreach (var target in result.AggregateCandidates)
        {
            token.ThrowIfCancellationRequested();
            foreach (var left in first)
            {
                var need = target - left.Sum;
                if (!second.TryGetValue(need, out var rightList)) continue;

                foreach (var right in rightList)
                {
                    token.ThrowIfCancellationRequested();
                    var sixGroups = new[] { left.A, left.B, left.C, right.A, right.B, right.C };
                    var reps = sixGroups.Select(g => g.Representative).ToArray();
                    var check = GameMath.Calculate(car, reps, roundingMode);
                    if (!Matches(check, wantedTopSpeed, wantedAcceleration, wantedHandling, tolerance)) continue;

                    var multiplicity = ProductSaturating(sixGroups.Select(g => g.Parts.Count));
                    result.EstimatedTotalCombinations = AddSaturating(result.EstimatedTotalCombinations, multiplicity);

                    if (result.Results.Count < maxResults)
                    {
                        ExpandResults(sixGroups, target, car, roundingMode, result.Results, maxResults,
                            wantedTopSpeed, wantedAcceleration, wantedHandling, tolerance, token);
                    }
                }
            }
        }

        sw.Stop();
        result.Elapsed = sw.Elapsed;
        return result;
    }

    private Dictionary<string, List<PartDefinition>> BuildPartsBySlot(bool includeCustom, bool allowEmpty)
    {
        var dict = Slots.All.ToDictionary(s => s, _ => new List<PartDefinition>(), StringComparer.OrdinalIgnoreCase);
        foreach (var part in _allParts)
        {
            if (!dict.ContainsKey(part.Component)) continue;
            if (!includeCustom && string.Equals(part.Brand, "custom", StringComparison.OrdinalIgnoreCase)) continue;
            dict[part.Component].Add(part);
        }

        foreach (var slot in Slots.All)
        {
            if (allowEmpty) dict[slot].Insert(0, PartDefinition.Stock(slot));
            if (dict[slot].Count == 0) throw new InvalidOperationException($"No parts available for slot {slot}.");
        }
        return dict;
    }

    private static Dictionary<string, List<PartGroup>> BuildGroups(Dictionary<string, List<PartDefinition>> bySlot)
    {
        var result = new Dictionary<string, List<PartGroup>>(StringComparer.OrdinalIgnoreCase);
        foreach (var slot in Slots.All)
        {
            result[slot] = bySlot[slot]
                .GroupBy(p => p.Triple)
                .Select(g => new PartGroup { Triple = g.Key, Parts = g.ToList() })
                .ToList();
        }
        return result;
    }

    private static List<HalfCombo> BuildFirstHalf(Dictionary<string, List<PartGroup>> groups, CancellationToken token)
    {
        var output = new List<HalfCombo>();
        foreach (var a in groups[Slots.All[0]])
        foreach (var b in groups[Slots.All[1]])
        foreach (var c in groups[Slots.All[2]])
        {
            token.ThrowIfCancellationRequested();
            output.Add(new HalfCombo(a.Triple + b.Triple + c.Triple, a, b, c));
        }
        return output;
    }

    private static Dictionary<RatingTriple, List<HalfCombo>> BuildSecondHalfMap(
        Dictionary<string, List<PartGroup>> groups, CancellationToken token)
    {
        var output = new Dictionary<RatingTriple, List<HalfCombo>>();
        foreach (var a in groups[Slots.All[3]])
        foreach (var b in groups[Slots.All[4]])
        foreach (var c in groups[Slots.All[5]])
        {
            token.ThrowIfCancellationRequested();
            var combo = new HalfCombo(a.Triple + b.Triple + c.Triple, a, b, c);
            if (!output.TryGetValue(combo.Sum, out var list)) output[combo.Sum] = list = [];
            list.Add(combo);
        }
        return output;
    }

    private static IEnumerable<RatingTriple> FindAggregateCandidates(
        CarDefinition car,
        int wantedTs, int wantedAcc, int wantedHnd,
        Dictionary<string, List<PartDefinition>> bySlot,
        DisplayRoundingMode mode,
        int tolerance,
        CancellationToken token)
    {
        var minH = Slots.All.Sum(s => bySlot[s].Min(p => p.Handling));
        var maxH = Slots.All.Sum(s => bySlot[s].Max(p => p.Handling));
        var minA = Slots.All.Sum(s => bySlot[s].Min(p => p.Acceleration));
        var maxA = Slots.All.Sum(s => bySlot[s].Max(p => p.Acceleration));
        var minT = Slots.All.Sum(s => bySlot[s].Min(p => p.TopSpeed));
        var maxT = Slots.All.Sum(s => bySlot[s].Max(p => p.TopSpeed));

        var output = new List<RatingTriple>();
        for (var h = minH; h <= maxH; h++)
        {
            token.ThrowIfCancellationRequested();
            for (var a = minA; a <= maxA; a++)
            for (var t = minT; t <= maxT; t++)
            {
                var sum = new RatingTriple(h, a, t);
                var calc = GameMath.CalculateFromSums(car, sum, mode);
                // Aggregate sums are only a fast prefilter. The real game accumulates six parts
                // sequentially in float32, so a boundary case can differ by one displayed point.
                // Widen this stage by 1; concrete six-part combinations are verified exactly later.
                var prefilterTolerance = tolerance + 1;
                if (Math.Abs(calc.TopSpeed - wantedTs) <= prefilterTolerance &&
                    Math.Abs(calc.Acceleration - wantedAcc) <= prefilterTolerance &&
                    Math.Abs(calc.Handling - wantedHnd) <= prefilterTolerance)
                {
                    output.Add(sum);
                }
            }
        }
        return output;
    }

    private static bool Matches(PerformanceResult check, int ts, int acc, int hnd, int tolerance) =>
        Math.Abs(check.DisplayTopSpeed - ts) <= tolerance &&
        Math.Abs(check.DisplayAcceleration - acc) <= tolerance &&
        Math.Abs(check.DisplayHandling - hnd) <= tolerance;

    private static void ExpandResults(
        PartGroup[] groups,
        RatingTriple target,
        CarDefinition car,
        DisplayRoundingMode mode,
        List<ReverseResult> output,
        int maxResults,
        int wantedTs, int wantedAcc, int wantedHnd,
        int tolerance,
        CancellationToken token)
    {
        void Recurse(int index, PartDefinition[] chosen)
        {
            if (output.Count >= maxResults) return;
            token.ThrowIfCancellationRequested();
            if (index == groups.Length)
            {
                var check = GameMath.Calculate(car, chosen, mode);
                if (!Matches(check, wantedTs, wantedAcc, wantedHnd, tolerance)) return;
                output.Add(new ReverseResult
                {
                    Engine = chosen[0],
                    ForcedInduction = chosen[1],
                    Transmission = chosen[2],
                    Suspension = chosen[3],
                    Brakes = chosen[4],
                    Tires = chosen[5],
                    Sum = target,
                    TopSpeed = check.DisplayTopSpeed,
                    Acceleration = check.DisplayAcceleration,
                    Handling = check.DisplayHandling
                });
                return;
            }

            foreach (var p in groups[index].Parts)
            {
                chosen[index] = p;
                Recurse(index + 1, chosen);
                if (output.Count >= maxResults) return;
            }
        }

        Recurse(0, new PartDefinition[6]);
    }

    private static long ProductSaturating(IEnumerable<int> values)
    {
        long v = 1;
        foreach (var x in values)
        {
            if (x != 0 && v > long.MaxValue / x) return long.MaxValue;
            v *= x;
        }
        return v;
    }

    private static long AddSaturating(long a, long b) =>
        long.MaxValue - a < b ? long.MaxValue : a + b;
}

using System.Globalization;

namespace NFSWPerformanceTool;

public static class Slots
{
    public static readonly string[] All =
    [
        "engine",
        "forcedinduction",
        "transmission",
        "suspension",
        "brakes",
        "tires"
    ];

    public static string Friendly(string slot) => slot switch
    {
        "engine" => "Engine",
        "forcedinduction" => "Forced induction",
        "transmission" => "Transmission",
        "suspension" => "Suspension",
        "brakes" => "Brakes",
        "tires" => "Tires",
        _ => slot
    };
}

public readonly record struct RatingTriple(int Handling, int Acceleration, int TopSpeed)
{
    public static RatingTriple operator +(RatingTriple a, RatingTriple b) =>
        new(a.Handling + b.Handling, a.Acceleration + b.Acceleration, a.TopSpeed + b.TopSpeed);

    public static RatingTriple operator -(RatingTriple a, RatingTriple b) =>
        new(a.Handling - b.Handling, a.Acceleration - b.Acceleration, a.TopSpeed - b.TopSpeed);

    public override string ToString() => $"H={Handling}, A={Acceleration}, T={TopSpeed}";
}

public sealed class PartDefinition
{
    public string ProductId { get; init; } = "";
    public string Brand { get; init; } = "";
    public string Component { get; init; } = "";
    public string Stage { get; init; } = "";
    public string Rarity { get; init; } = "";
    public int Level { get; init; }
    public int TopSpeed { get; init; }
    public int Acceleration { get; init; }
    public int Handling { get; init; }
    public bool IsStock { get; init; }

    public RatingTriple Triple => new(Handling, Acceleration, TopSpeed);

    public static PartDefinition Stock(string slot) => new()
    {
        ProductId = $"(stock/no {slot})",
        Brand = "stock",
        Component = slot,
        Stage = "stock",
        Rarity = "",
        Level = 0,
        TopSpeed = 0,
        Acceleration = 0,
        Handling = 0,
        IsStock = true
    };

    public override string ToString()
    {
        if (IsStock) return "(Stock / no part)   H=0  A=0  T=0";
        return $"{ProductId}   H={Handling}  A={Acceleration}  T={TopSpeed}";
    }
}

public sealed class CarDefinition
{
    public string Name { get; init; } = "";
    public string Parent { get; init; } = "";
    public string HashHex { get; init; } = "";
    public float[] TopSpeed { get; init; } = new float[4];
    public float[] Acceleration { get; init; } = new float[4];
    public float[] Handling { get; init; } = new float[4];

    public override string ToString() => Name;

    public string DescribeArrays() =>
        $"TopSpeed [{Fmt(TopSpeed)}]    Acceleration [{Fmt(Acceleration)}]    Handling [{Fmt(Handling)}]";

    private static string Fmt(float[] values) =>
        string.Join(", ", values.Select(v => v.ToString("0.###", CultureInfo.InvariantCulture)));
}

public sealed record PerformanceResult(
    RatingTriple Sum,
    GameMath.Weights Weights,
    float RawTopSpeed,
    float RawAcceleration,
    float RawHandling,
    int DisplayTopSpeed,
    int DisplayAcceleration,
    int DisplayHandling);

public sealed class ReverseResult
{
    public required PartDefinition Engine { get; init; }
    public required PartDefinition ForcedInduction { get; init; }
    public required PartDefinition Transmission { get; init; }
    public required PartDefinition Suspension { get; init; }
    public required PartDefinition Brakes { get; init; }
    public required PartDefinition Tires { get; init; }
    public required RatingTriple Sum { get; init; }
    public required int TopSpeed { get; init; }
    public required int Acceleration { get; init; }
    public required int Handling { get; init; }
}

public sealed class ReverseSearchResult
{
    public List<RatingTriple> AggregateCandidates { get; } = [];
    public List<ReverseResult> Results { get; } = [];
    public long EstimatedTotalCombinations { get; set; }
    public TimeSpan Elapsed { get; set; }
}

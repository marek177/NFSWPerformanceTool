namespace NFSWPerformanceTool;

public enum DisplayRoundingMode
{
    NfswTruncate,
    NearestInteger
}

public static class GameMath
{
    // Constants recovered from nfsw.exe. 2/3 is stored as this float32 value.
    private const float OneHundredth = 0.01f;
    private const float TwoThirds = 0.6666666865348816f;

    public readonly record struct Weights(float Handling, float Acceleration, float TopSpeed)
    {
        public float Stock
        {
            get
            {
                var s = 1f - Handling;
                s -= Acceleration;
                s -= TopSpeed;
                return s;
            }
        }
    }

    public static Weights WeightsFromParts(IEnumerable<PartDefinition> parts)
    {
        float h = 0f, a = 0f, t = 0f;
        foreach (var p in parts)
        {
            var dh = p.Handling * OneHundredth;
            var da = p.Acceleration * OneHundredth;
            var dt = p.TopSpeed * OneHundredth;
            h += dh;
            a += da;
            t += dt;
        }
        return Normalize(h, a, t);
    }

    public static Weights WeightsFromSums(RatingTriple sum)
    {
        var h = sum.Handling * OneHundredth;
        var a = sum.Acceleration * OneHundredth;
        var t = sum.TopSpeed * OneHundredth;
        return Normalize(h, a, t);
    }

    private static Weights Normalize(float h, float a, float t)
    {
        var total = t + a;
        total += h;
        var denom = total * TwoThirds;
        denom += 1f;
        var fragment = 1f / denom;
        return new Weights(h * fragment, a * fragment, t * fragment);
    }

    public static float BlendRaw(float[] base4, Weights weights)
    {
        var stock = 1f - weights.Handling;
        stock -= weights.Acceleration;
        stock -= weights.TopSpeed;

        var value = base4[0] * stock;
        value += base4[1] * weights.Handling;
        value += base4[2] * weights.Acceleration;
        value += base4[3] * weights.TopSpeed;
        return value;
    }

    public static int ToDisplayed(float value, DisplayRoundingMode mode) => mode switch
    {
        DisplayRoundingMode.NfswTruncate => (int)value, // CVTTSS2SI: truncate toward zero
        DisplayRoundingMode.NearestInteger => (int)MathF.Round(value, MidpointRounding.AwayFromZero),
        _ => (int)value
    };

    public static PerformanceResult Calculate(CarDefinition car, IReadOnlyList<PartDefinition> parts,
        DisplayRoundingMode mode = DisplayRoundingMode.NfswTruncate)
    {
        var sum = parts.Aggregate(new RatingTriple(), (s, p) => s + p.Triple);
        var weights = WeightsFromParts(parts);
        var rawTs = BlendRaw(car.TopSpeed, weights);
        var rawAcc = BlendRaw(car.Acceleration, weights);
        var rawHnd = BlendRaw(car.Handling, weights);
        return new PerformanceResult(
            sum, weights,
            rawTs, rawAcc, rawHnd,
            ToDisplayed(rawTs, mode), ToDisplayed(rawAcc, mode), ToDisplayed(rawHnd, mode));
    }

    public static (int TopSpeed, int Acceleration, int Handling) CalculateFromSums(
        CarDefinition car, RatingTriple sum, DisplayRoundingMode mode)
    {
        var w = WeightsFromSums(sum);
        return (
            ToDisplayed(BlendRaw(car.TopSpeed, w), mode),
            ToDisplayed(BlendRaw(car.Acceleration, w), mode),
            ToDisplayed(BlendRaw(car.Handling, w), mode));
    }
}

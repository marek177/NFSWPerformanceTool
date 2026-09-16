using System.Globalization;

namespace NFSWPerformanceTool;

public sealed class DataLoader
{
    public IReadOnlyList<CarDefinition> Cars { get; private set; } = [];
    public IReadOnlyList<PartDefinition> Parts { get; private set; } = [];

    public void Load(string baseDirectory)
    {
        var dataDir = Path.Combine(baseDirectory, "Data");
        var carsPath = Path.Combine(dataDir, "cars.csv");
        var partsPath = Path.Combine(dataDir, "parts.csv");

        if (!File.Exists(carsPath)) throw new FileNotFoundException("Missing Data/cars.csv", carsPath);
        if (!File.Exists(partsPath)) throw new FileNotFoundException("Missing Data/parts.csv", partsPath);

        Cars = LoadCars(carsPath);
        Parts = LoadParts(partsPath);
    }

    private static IReadOnlyList<CarDefinition> LoadCars(string path)
    {
        var lines = File.ReadAllLines(path);
        if (lines.Length < 2) return [];
        var header = HeaderMap(lines[0]);
        var result = new List<CarDefinition>();

        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var c = Split(line);
            result.Add(new CarDefinition
            {
                Name = Get(c, header, "car"),
                Parent = Get(c, header, "parent"),
                HashHex = Get(c, header, "hash_hex"),
                TopSpeed = Four(c, header, "topspeed"),
                Acceleration = Four(c, header, "acceleration"),
                Handling = Four(c, header, "handling")
            });
        }

        return result.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IReadOnlyList<PartDefinition> LoadParts(string path)
    {
        var lines = File.ReadAllLines(path);
        if (lines.Length < 2) return [];
        var header = HeaderMap(lines[0]);
        var result = new List<PartDefinition>();

        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var c = Split(line);
            var slot = Get(c, header, "component");
            if (!Slots.All.Contains(slot, StringComparer.OrdinalIgnoreCase)) continue;
            if (!string.Equals(Get(c, header, "installable_slot"), "yes", StringComparison.OrdinalIgnoreCase)) continue;

            result.Add(new PartDefinition
            {
                ProductId = Get(c, header, "product_id"),
                Brand = Get(c, header, "brand"),
                Component = slot,
                Stage = Get(c, header, "stage"),
                Rarity = Get(c, header, "rarity_name"),
                Level = Int(Get(c, header, "level")),
                TopSpeed = Int(Get(c, header, "topspeed")),
                Acceleration = Int(Get(c, header, "acceleration")),
                Handling = Int(Get(c, header, "handling"))
            });
        }

        return result.OrderBy(x => x.Component).ThenBy(x => x.ProductId, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static float[] Four(string[] c, Dictionary<string, int> h, string prefix) =>
    [
        Float(Get(c, h, prefix + "0")),
        Float(Get(c, h, prefix + "1")),
        Float(Get(c, h, prefix + "2")),
        Float(Get(c, h, prefix + "3"))
    ];

    private static Dictionary<string, int> HeaderMap(string header) =>
        Split(header).Select((name, index) => (name, index))
            .ToDictionary(x => x.name, x => x.index, StringComparer.OrdinalIgnoreCase);

    private static string Get(string[] c, Dictionary<string, int> h, string key) =>
        h.TryGetValue(key, out var i) && i < c.Length ? c[i] : "";

    private static int Int(string s) => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
    private static float Float(string s) => float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0f;

    private static string[] Split(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"'); i++;
                }
                else quoted = !quoted;
            }
            else if (ch == ',' && !quoted)
            {
                fields.Add(current.ToString()); current.Clear();
            }
            else current.Append(ch);
        }
        fields.Add(current.ToString());
        return fields.ToArray();
    }
}

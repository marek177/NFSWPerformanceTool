using System.ComponentModel;
using System.Globalization;

namespace NFSWPerformanceTool;

public sealed class MainForm : Form
{
    private readonly DataLoader _data = new();
    private ReverseSolver? _solver;
    private CancellationTokenSource? _reverseCts;

    private readonly ComboBox _car = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 260 };
    private readonly Label _carInfo = new() { AutoSize = true, MaximumSize = new Size(1100, 0) };
    private readonly CheckBox _includeCustom = new() { Text = "Include custom/debug parts", AutoSize = true };
    private readonly ComboBox _rounding = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };

    private readonly Dictionary<string, ComboBox> _partBoxes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Label _sumLabel = new() { AutoSize = true };
    private readonly Label _weightsLabel = new() { AutoSize = true };
    private readonly Label _rawLabel = new() { AutoSize = true };
    private readonly Label _displayLabel = new() { AutoSize = true, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold) };

    private readonly NumericUpDown _revTs = Num(0, 5000);
    private readonly NumericUpDown _revAcc = Num(0, 5000);
    private readonly NumericUpDown _revHnd = Num(0, 5000);
    private readonly NumericUpDown _tolerance = Num(0, 5);
    private readonly NumericUpDown _maxResults = Num(1, 5000, 250);
    private readonly CheckBox _allowEmpty = new() { Text = "Allow stock/empty slots", Checked = true, AutoSize = true };
    private readonly Button _searchReverse = new() { Text = "Reverse search", AutoSize = true };
    private readonly Button _cancelReverse = new() { Text = "Cancel", AutoSize = true, Enabled = false };
    private readonly Button _useForward = new() { Text = "Use current forward result", AutoSize = true };
    private readonly Label _reverseStatus = new() { AutoSize = true };
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AutoGenerateColumns = false,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        MultiSelect = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells
    };

    public MainForm()
    {
        Text = "NFSW Performance Calculator + Reverse Solver";
        Width = 1420;
        Height = 850;
        MinimumSize = new Size(1050, 650);
        StartPosition = FormStartPosition.CenterScreen;

        _rounding.Items.Add(new ModeItem(DisplayRoundingMode.NfswTruncate, "NFSW exact: truncate toward zero"));
        _rounding.Items.Add(new ModeItem(DisplayRoundingMode.NearestInteger, "Math rounding: nearest integer"));
        _rounding.SelectedIndex = 0;

        Controls.Add(BuildLayout());
        Shown += (_, _) => LoadData();
    }

    private Control BuildLayout()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var top = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        top.Controls.Add(new Label { Text = "Car:", AutoSize = true, Padding = new Padding(0, 7, 0, 0) });
        top.Controls.Add(_car);
        top.Controls.Add(_includeCustom);
        top.Controls.Add(new Label { Text = "Displayed-value mode:", AutoSize = true, Padding = new Padding(15, 7, 0, 0) });
        top.Controls.Add(_rounding);
        root.Controls.Add(top, 0, 0);
        root.Controls.Add(_carInfo, 0, 1);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        tabs.TabPages.Add(BuildForwardTab());
        tabs.TabPages.Add(BuildReverseTab());
        root.Controls.Add(tabs, 0, 2);
        return root;
    }

    private TabPage BuildForwardTab()
    {
        var page = new TabPage("Forward calculator");
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(12), AutoScroll = true };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        var row = 0;

        foreach (var slot in Slots.All)
        {
            var cb = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top, MaxDropDownItems = 20 };
            cb.SelectedIndexChanged += (_, _) => RecalculateForward();
            _partBoxes[slot] = cb;
            panel.Controls.Add(new Label { Text = Slots.Friendly(slot) + ":", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, 0, row);
            panel.Controls.Add(cb, 1, row++);
        }

        var reset = new Button { Text = "Set all to stock", AutoSize = true };
        reset.Click += (_, _) =>
        {
            foreach (var cb in _partBoxes.Values) if (cb.Items.Count > 0) cb.SelectedIndex = 0;
            RecalculateForward();
        };
        panel.Controls.Add(reset, 1, row++);

        panel.Controls.Add(new Label { Text = "Part sums:", AutoSize = true }, 0, row);
        panel.Controls.Add(_sumLabel, 1, row++);
        panel.Controls.Add(new Label { Text = "Blend weights:", AutoSize = true }, 0, row);
        panel.Controls.Add(_weightsLabel, 1, row++);
        panel.Controls.Add(new Label { Text = "Raw float values:", AutoSize = true }, 0, row);
        panel.Controls.Add(_rawLabel, 1, row++);
        panel.Controls.Add(new Label { Text = "Displayed values:", AutoSize = true }, 0, row);
        panel.Controls.Add(_displayLabel, 1, row++);

        var note = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(1100, 0),
            Text = "Exact NFSW mode uses the float32 blend recovered from nfsw.exe and CVTTSS2SI semantics (truncate toward zero). " +
                   "The optional nearest-integer mode is provided only if you want to compare against externally rounded values."
        };
        panel.Controls.Add(note, 0, row);
        panel.SetColumnSpan(note, 2);
        page.Controls.Add(panel);
        return page;
    }

    private TabPage BuildReverseTab()
    {
        var page = new TabPage("Reverse solver");
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(10) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var inputs = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        AddInput(inputs, "TopSpeed", _revTs);
        AddInput(inputs, "Acceleration", _revAcc);
        AddInput(inputs, "Handling", _revHnd);
        AddInput(inputs, "Tolerance ±", _tolerance);
        AddInput(inputs, "Max rows", _maxResults);
        inputs.Controls.Add(_allowEmpty);
        inputs.Controls.Add(_useForward);
        inputs.Controls.Add(_searchReverse);
        inputs.Controls.Add(_cancelReverse);
        root.Controls.Add(inputs, 0, 0);

        var info = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        info.Controls.Add(_reverseStatus);
        root.Controls.Add(info, 0, 1);

        ConfigureGrid();
        root.Controls.Add(_grid, 0, 2);

        _useForward.Click += (_, _) => UseForwardResult();
        _searchReverse.Click += async (_, _) => await RunReverseAsync();
        _cancelReverse.Click += (_, _) => _reverseCts?.Cancel();

        page.Controls.Add(root);
        return page;
    }

    private static void AddInput(FlowLayoutPanel panel, string label, Control input)
    {
        panel.Controls.Add(new Label { Text = label + ":", AutoSize = true, Padding = new Padding(8, 7, 0, 0) });
        panel.Controls.Add(input);
    }

    private void ConfigureGrid()
    {
        AddGrid("Engine", nameof(ReverseResult.Engine), 260);
        AddGrid("Forced induction", nameof(ReverseResult.ForcedInduction), 260);
        AddGrid("Transmission", nameof(ReverseResult.Transmission), 260);
        AddGrid("Suspension", nameof(ReverseResult.Suspension), 260);
        AddGrid("Brakes", nameof(ReverseResult.Brakes), 260);
        AddGrid("Tires", nameof(ReverseResult.Tires), 260);
        AddGrid("Σ H/A/T", nameof(ReverseResult.Sum), 120);
        AddGrid("TS", nameof(ReverseResult.TopSpeed), 60);
        AddGrid("ACC", nameof(ReverseResult.Acceleration), 60);
        AddGrid("HND", nameof(ReverseResult.Handling), 60);
        _grid.CellFormatting += (_, e) =>
        {
            if (e.Value is PartDefinition p) e.Value = p.ProductId;
        };
    }

    private void AddGrid(string header, string property, int width)
    {
        _grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = header,
            DataPropertyName = property,
            Width = width,
            SortMode = DataGridViewColumnSortMode.Automatic
        });
    }

    private void LoadData()
    {
        try
        {
            _data.Load(AppContext.BaseDirectory);
            _solver = new ReverseSolver(_data.Parts);
            _car.DataSource = _data.Cars.ToList();
            _car.SelectedIndexChanged += (_, _) =>
            {
                UpdateCarInfo();
                RecalculateForward();
            };
            _includeCustom.CheckedChanged += (_, _) => PopulatePartBoxes();
            _rounding.SelectedIndexChanged += (_, _) => RecalculateForward();
            PopulatePartBoxes();
            UpdateCarInfo();
            RecalculateForward();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.ToString(), "Failed to load data", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void PopulatePartBoxes()
    {
        var includeCustom = _includeCustom.Checked;
        foreach (var slot in Slots.All)
        {
            var cb = _partBoxes[slot];
            var oldId = (cb.SelectedItem as PartDefinition)?.ProductId;
            var items = new List<PartDefinition> { PartDefinition.Stock(slot) };
            items.AddRange(_data.Parts.Where(p =>
                string.Equals(p.Component, slot, StringComparison.OrdinalIgnoreCase) &&
                (includeCustom || !string.Equals(p.Brand, "custom", StringComparison.OrdinalIgnoreCase))));

            cb.BeginUpdate();
            cb.DataSource = null;
            cb.DataSource = items;
            if (oldId is not null)
            {
                var found = items.FindIndex(p => p.ProductId == oldId);
                cb.SelectedIndex = found >= 0 ? found : 0;
            }
            else cb.SelectedIndex = 0;
            cb.EndUpdate();
        }
        RecalculateForward();
    }

    private CarDefinition? SelectedCar => _car.SelectedItem as CarDefinition;
    private DisplayRoundingMode SelectedMode => (_rounding.SelectedItem as ModeItem)?.Mode ?? DisplayRoundingMode.NfswTruncate;

    private PartDefinition[] CurrentParts() => Slots.All
        .Select(slot => _partBoxes[slot].SelectedItem as PartDefinition ?? PartDefinition.Stock(slot))
        .ToArray();

    private void UpdateCarInfo()
    {
        var car = SelectedCar;
        _carInfo.Text = car is null ? "" : $"{car.Name} ({car.Parent}, {car.HashHex})   {car.DescribeArrays()}";
    }

    private PerformanceResult? RecalculateForward()
    {
        var car = SelectedCar;
        if (car is null || _partBoxes.Count != 6 || _partBoxes.Values.Any(c => c.SelectedItem is null)) return null;
        var r = GameMath.Calculate(car, CurrentParts(), SelectedMode);
        _sumLabel.Text = $"Handling={r.Sum.Handling}, Acceleration={r.Sum.Acceleration}, TopSpeed={r.Sum.TopSpeed}";
        _weightsLabel.Text = string.Format(CultureInfo.InvariantCulture,
            "stock={0:0.######}, H={1:0.######}, A={2:0.######}, T={3:0.######}",
            r.Weights.Stock, r.Weights.Handling, r.Weights.Acceleration, r.Weights.TopSpeed);
        _rawLabel.Text = string.Format(CultureInfo.InvariantCulture,
            "TopSpeed={0:0.######}, Acceleration={1:0.######}, Handling={2:0.######}",
            r.RawTopSpeed, r.RawAcceleration, r.RawHandling);
        _displayLabel.Text = $"TopSpeed={r.DisplayTopSpeed}    Acceleration={r.DisplayAcceleration}    Handling={r.DisplayHandling}";
        return r;
    }

    private void UseForwardResult()
    {
        var r = RecalculateForward();
        if (r is null) return;
        _revTs.Value = Clamp(_revTs, r.DisplayTopSpeed);
        _revAcc.Value = Clamp(_revAcc, r.DisplayAcceleration);
        _revHnd.Value = Clamp(_revHnd, r.DisplayHandling);
    }

    private async Task RunReverseAsync()
    {
        if (_solver is null || SelectedCar is null) return;
        _reverseCts?.Dispose();
        _reverseCts = new CancellationTokenSource();
        var token = _reverseCts.Token;

        _searchReverse.Enabled = false;
        _cancelReverse.Enabled = true;
        _grid.DataSource = null;
        _reverseStatus.Text = "Starting...";

        try
        {
            var progress = new Progress<string>(s => _reverseStatus.Text = s);
            var car = SelectedCar;
            var ts = (int)_revTs.Value;
            var acc = (int)_revAcc.Value;
            var hnd = (int)_revHnd.Value;
            var includeCustom = _includeCustom.Checked;
            var allowEmpty = _allowEmpty.Checked;
            var mode = SelectedMode;
            var tolerance = (int)_tolerance.Value;
            var max = (int)_maxResults.Value;

            var answer = await Task.Run(() => _solver.Search(
                car, ts, acc, hnd, includeCustom, allowEmpty, mode, tolerance, max, token, progress), token);

            _grid.DataSource = new BindingList<ReverseResult>(answer.Results);
            var totalText = answer.EstimatedTotalCombinations == long.MaxValue
                ? ">= Int64.MaxValue"
                : answer.EstimatedTotalCombinations.ToString("N0", CultureInfo.InvariantCulture);
            _reverseStatus.Text =
                $"Done in {answer.Elapsed.TotalSeconds:0.00}s. Aggregate sums: {answer.AggregateCandidates.Count}. " +
                $"Estimated concrete combinations: {totalText}. Showing {answer.Results.Count}.";
        }
        catch (OperationCanceledException)
        {
            _reverseStatus.Text = "Cancelled.";
        }
        catch (Exception ex)
        {
            _reverseStatus.Text = "Error.";
            MessageBox.Show(this, ex.ToString(), "Reverse solver error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _searchReverse.Enabled = true;
            _cancelReverse.Enabled = false;
        }
    }

    private static decimal Clamp(NumericUpDown box, int v) => Math.Max(box.Minimum, Math.Min(box.Maximum, v));

    private static NumericUpDown Num(decimal min, decimal max, decimal value = 0) => new()
    {
        Minimum = min,
        Maximum = max,
        Value = Math.Max(min, Math.Min(max, value)),
        Width = 80
    };

    private sealed record ModeItem(DisplayRoundingMode Mode, string Text)
    {
        public override string ToString() => Text;
    }
}

# NFSW Performance Tool

C# / WinForms calculator and reverse solver for Need for Speed World performance tuning.

## Features

- Select a car from extracted `pvehicle` data.
- Select Engine, Forced Induction, Transmission, Suspension, Brakes and Tires.
- Calculate aggregate Handling / Acceleration / TopSpeed part values.
- Reproduce the NFSW float32 performance calculation.
- Default display conversion matches the game path: x86 `CVTTSS2SI` (truncate toward zero).
- Optional nearest-integer mode for comparison with externally rounded values.
- Reverse search from displayed TopSpeed / Acceleration / Handling back to possible aggregate values and concrete six-part combinations.
- Optional tolerance for rounded/uncertain observed values.

## Source

The repository contains the main project files directly. The complete generated source tree, including `MainForm.cs`, `ReverseSolver.cs`, `Data/cars.csv` and `Data/parts.csv`, is also included as:

`NFSWPerformanceTool_CSharp_Source.zip`

## Build

Requires Windows and .NET 8 SDK / Visual Studio 2022.

```text
dotnet restore
dotnet build -c Release
dotnet run
```

No third-party NuGet packages are required.

## Performance formula

For summed part values:

```text
x = Handling
y = Acceleration
z = TopSpeed
D = x + y + z + 150
stock = 225 / D - 0.5
wH = 1.5 * x / D
wA = 1.5 * y / D
wT = 1.5 * z / D
```

The implementation uses the original float32-style operation order rather than this simplified double-precision form.

For each car metric with four `pvehicle` values `[stock, H-point, A-point, T-point]`:

```text
raw = v0*stock + v1*wH + v2*wA + v3*wT
shown = truncate_toward_zero(raw)
```

Different part combinations can produce the same displayed values, so reverse results are intentionally non-unique.

# NFSW Performance Tool

C# / WinForms calculator and reverse solver for Need for Speed World performance tuning.

AI assistance disclosure
This is an AI-assisted reverse-engineering and reconstruction project. A substantial part of the analysis, research, documentation, code generation, refactoring, and interpretation of reverse-engineered material has been produced with the assistance of ChatGPT by OpenAI, under the direction and review of marek177.

Git commit authorship therefore identifies the account that committed the files and should not be interpreted as meaning that every analysis, document, or line of code was written manually and independently by the repository owner. AI-generated or AI-assisted findings may contain errors, especially where original source code or symbols are unavailable, so important reverse-engineering conclusions should be independently verified against the original executable and game data.

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

The complete source tree is stored directly in the repository, including `MainForm.cs`, `ReverseSolver.cs`, `Data/cars.csv` and `Data/parts.csv`.

## Build

Requires Windows and .NET 8 SDK / Visual Studio 2022.

```text
dotnet restore
dotnet build -c Release
dotnet run
```

No third-party NuGet packages are required.

### Windows x64 and x86 packages

Run `build-release.ps1` in PowerShell to publish both self-contained Windows packages:

```powershell
./build-release.ps1
```

The script creates:

- `artifacts/NFSWPerformanceTool-win-x64.zip`
- `artifacts/NFSWPerformanceTool-win-x86.zip`

Both packages include the .NET 8 runtime and can run without a separate .NET installation. GitHub Actions builds the same two downloadable artifacts automatically for pull requests, pushes to `main`, version tags and manual workflow runs.

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

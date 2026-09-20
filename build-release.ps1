[CmdletBinding()]
param(
    [string[]]$RuntimeIdentifiers = @("win-x64", "win-x86"),
    [string]$Configuration = "Release",
    [string]$OutputDirectory = "artifacts"
)

$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "NFSWPerformanceTool.csproj"
$outputRoot = Join-Path $PSScriptRoot $OutputDirectory

New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

foreach ($runtimeIdentifier in $RuntimeIdentifiers) {
    $publishDirectory = Join-Path $outputRoot $runtimeIdentifier
    $archivePath = Join-Path $outputRoot "NFSWPerformanceTool-$runtimeIdentifier.zip"
    $publishArguments = @(
        "publish",
        $project,
        "--configuration", $Configuration,
        "--runtime", $runtimeIdentifier,
        "--self-contained", "true",
        "--output", $publishDirectory,
        "-p:PublishSingleFile=false",
        "-p:DebugType=None",
        "-p:DebugSymbols=false"
    )

    & dotnet @publishArguments

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for $runtimeIdentifier."
    }

    if (Test-Path $archivePath) {
        Remove-Item -Force $archivePath
    }

    Compress-Archive -Path (Join-Path $publishDirectory "*") -DestinationPath $archivePath -CompressionLevel Optimal
    Write-Host "Created $archivePath"
}

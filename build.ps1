param(
    [switch]$Test,
    [switch]$Export,
    [ValidateRange(1, [int]::MaxValue)]
    [int]$TestJobs = [Math]::Max(1, [Math]::Min(16, [Environment]::ProcessorCount))
)
. "$PSScriptRoot/scripts-common.ps1"

$godot = Find-Godot
dotnet build "$PSScriptRoot/MathRacer.csproj"
Assert-ExitCode 'Sestavení C#'
& $godot --headless --path $PSScriptRoot --editor --import --quit
Assert-ExitCode 'Import'
# The project, its C# assembly and the main scene load (Get-GameTestCases, through --list-checks). It quits before
# the world is generated, so run.ps1 does not build the valley twice, and the player's save is neither read nor written.
Get-GameTestCases $godot | Out-Null
if ($Test) { Invoke-GameTests $godot $TestJobs }
if ($Export) {
    New-Item -ItemType Directory -Force "$PSScriptRoot/exports" | Out-Null
    & $godot --headless --path $PSScriptRoot --export-release 'Windows Desktop' "$PSScriptRoot/exports/MathRacer.exe"
    Assert-ExitCode 'Export (vyžaduje odpovídající .NET exportní šablony)'
}

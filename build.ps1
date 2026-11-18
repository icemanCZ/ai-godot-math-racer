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
& $godot --headless --path $PSScriptRoot --quit-after 5
Assert-ExitCode 'Načtení projektu'
if ($Test) { Invoke-GameTests $godot $TestJobs }
if ($Export) {
    New-Item -ItemType Directory -Force "$PSScriptRoot/exports" | Out-Null
    & $godot --headless --path $PSScriptRoot --export-release 'Windows Desktop' "$PSScriptRoot/exports/MathRacer.exe"
    Assert-ExitCode 'Export (vyžaduje odpovídající .NET exportní šablony)'
}

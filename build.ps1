param([switch]$Test,[switch]$Export)
. "$PSScriptRoot/scripts-common.ps1"
$godot = Find-Godot
dotnet build "$PSScriptRoot/MathRacer.csproj"
Assert-ExitCode 'Sestavení C#'
& $godot --headless --path $PSScriptRoot --editor --import --quit
Assert-ExitCode 'Import'
& $godot --headless --path $PSScriptRoot --quit-after 5
Assert-ExitCode 'Načtení projektu'
if ($Test) {
    & $godot --headless --path $PSScriptRoot --fixed-fps 60 -- --controller-checks
    Assert-ExitCode 'Testy Xbox ovladače'
    & $godot --headless --path $PSScriptRoot --fixed-fps 60 -- --collision-checks
    Assert-ExitCode 'Testy nárazů do překážek'
    & $godot --headless --path $PSScriptRoot -- --smoke-test
    Assert-ExitCode 'Herní testy'
    & $godot --headless --path $PSScriptRoot --fixed-fps 60 -- --track-checks
    Assert-ExitCode 'Testy stop při zatáčení doprava'
}
if ($Export) {
    New-Item -ItemType Directory -Force "$PSScriptRoot/exports" | Out-Null
    & $godot --headless --path $PSScriptRoot --export-release 'Windows Desktop' "$PSScriptRoot/exports/MathRacer.exe"
    Assert-ExitCode 'Export (vyžaduje odpovídající .NET exportní šablony)'
}

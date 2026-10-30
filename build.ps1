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
    & $godot --headless --path $PSScriptRoot --fixed-fps 60 -- --event-checks --seed=42
    Assert-ExitCode 'Testy jízdních eventů a oslav'
    foreach ($tradeSeed in @(1, 42, 7421)) {
        & $godot --headless --path $PSScriptRoot --fixed-fps 60 -- --trade-checks "--seed=$tradeSeed"
        Assert-ExitCode "Testy pekaře, obchodníka a cukráře (seed $tradeSeed)"
    }
    & $godot --headless --path $PSScriptRoot --fixed-fps 60 -- --collection-checks --seed=42
    Assert-ExitCode 'Testy sběratele drahokamů a hračkáře'
    & $godot --headless --path $PSScriptRoot --fixed-fps 60 -- --garden-checks --seed=42
    Assert-ExitCode 'Testy zahradníka, květin a výprav mimo silnice'
    & $godot --headless --path $PSScriptRoot --fixed-fps 60 -- --farm-checks --seed=42
    Assert-ExitCode 'Testy farmy, chytání zvířátek a ukládání výprav'
    & $godot --headless --path $PSScriptRoot --fixed-fps 60 -- --village-checks --seed=42
    Assert-ExitCode 'Testy obyvatel vesnice a vylepšení domů'
    & $godot --headless --path $PSScriptRoot --fixed-fps 60 -- --new-game-checks --seed=42
    Assert-ExitCode 'Testy nové hry a změny seedu'
    & $godot --headless --path $PSScriptRoot --fixed-fps 60 -- --save-checks --seed=42
    Assert-ExitCode 'Testy automatického ukládání a obnovení'
    & $godot --headless --path $PSScriptRoot --fixed-fps 60 -- --garage-checks --seed=42
    Assert-ExitCode 'Testy garáže a získaných odměn'
    foreach ($deliverySeed in @(1, 42, 7421)) {
        & $godot --headless --path $PSScriptRoot --fixed-fps 60 -- --delivery-checks "--seed=$deliverySeed"
        Assert-ExitCode "Obrázkové výpravy (seed $deliverySeed)"
    }
    foreach ($roadSeed in @(1, 2, 42, 44, 53, 7421)) {
        & $godot --headless --path $PSScriptRoot -- --road-audit "--seed=$roadSeed"
        Assert-ExitCode "Průjezdnost všech cest (seed $roadSeed)"
    }
    & $godot --headless --path $PSScriptRoot --fixed-fps 60 -- --controller-checks
    Assert-ExitCode 'Testy Xbox ovladače'
    & $godot --headless --path $PSScriptRoot --fixed-fps 60 -- --collision-checks
    Assert-ExitCode 'Testy nárazů do překážek'
    & $godot --headless --path $PSScriptRoot -- --smoke-test
    Assert-ExitCode 'Herní testy'
    & $godot --headless --path $PSScriptRoot --fixed-fps 60 -- --track-checks
    Assert-ExitCode 'Testy stop při zatáčení doprava'
    foreach ($drivingSeed in @(1, 16)) {
        & $godot --headless --path $PSScriptRoot --fixed-fps 60 -- --road-driving-checks "--seed=$drivingSeed"
        Assert-ExitCode "Jízda po všech cestách v obou směrech (seed $drivingSeed)"
    }
}
if ($Export) {
    New-Item -ItemType Directory -Force "$PSScriptRoot/exports" | Out-Null
    & $godot --headless --path $PSScriptRoot --export-release 'Windows Desktop' "$PSScriptRoot/exports/MathRacer.exe"
    Assert-ExitCode 'Export (vyžaduje odpovídající .NET exportní šablony)'
}

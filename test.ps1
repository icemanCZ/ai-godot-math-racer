<#
.SYNOPSIS
    Runs headless checks in parallel, without rebuilding unless asked to.

.DESCRIPTION
    The suites are the ones listed in Scripts/Diagnostics/DiagnosticSuites.cs. Without -Suite all of them run, as in
    build.ps1 -Test. Every case is a Godot process of its own, JobCount of them at a time (16 by default); each keeps
    its log and test files under artifacts/game-tests/<run>/<case>.

.EXAMPLE
    ./test.ps1 -List
    ./test.ps1 -Suite ford-checks, works-checks
    ./test.ps1 -Suite track -Seeds (1..40)
    ./test.ps1 -Build -Suite save
    ./test.ps1 -Suite plot-checks, build-checks -Seeds 1, 2, 3 -Jobs 8
    ./test.ps1 -Tool load-save -Seeds 555399881 -Argument '--save-file=C:\copy\progress.json'
    ./test.ps1 -Tool save-fixtures -Seeds 42
#>
param(
    # Names of suites; a name matches every suite that starts with it ("save" is save-checks and save-fixture-checks).
    [string[]]$Suite = @(),
    # Names of tools (not suites): they write files or look at one save, and run only when asked for by name. A name
    # matches every tool that starts with it. Give the seed with -Seeds and further game arguments with -Argument.
    [string[]]$Tool = @(),
    # Extra arguments for the game, after the flag of the suite or tool, e.g. '--save-file=PATH'. A path with a space
    # needs its own quotes inside the string.
    [string[]]$Argument = @(),
    # Seeds to run every selected suite with, instead of the seeds listed for it.
    [int[]]$Seeds = @(),
    [ValidateRange(1, 64)]
    [int]$Jobs = 16,
    # Build the C# project and import the resources first.
    [switch]$Build,
    # Only list the suites and the seeds they run with.
    [switch]$List,
    # Lines of a failed log to print; 0 prints only where the log is.
    [int]$Tail = 12
)
. "$PSScriptRoot/scripts-common.ps1"
$godot = Find-Godot
if ($Build) {
    dotnet build "$PSScriptRoot/MathRacer.csproj"
    Assert-ExitCode 'Sestavení C#'
    & $godot --headless --path $PSScriptRoot --editor --import --quit
    Assert-ExitCode 'Import'
}

$cases = @(Get-GameTestCases $godot -IncludeTools)
if ($Tool.Count -gt 0) {
    $selected = @($cases | Where-Object { $mode = $_.Mode; -not $_.InSuite -and @($Tool | Where-Object { $mode -like "$_*" }).Count -gt 0 })
    if ($selected.Count -eq 0) { throw "Žádný nástroj nepatří k: $($Tool -join ', '). Seznam: ./test.ps1 -List" }
    $cases = $selected
} else {
    $cases = @($cases | Where-Object { $_.InSuite })
}
if ($Suite.Count -gt 0) {
    $selected = @($cases | Where-Object { $label = $_.Mode; @($Suite | Where-Object { $label -like "$_*" }).Count -gt 0 })
    if ($selected.Count -eq 0) { throw "Žádná kontrola nepatří k: $($Suite -join ', '). Seznam: ./test.ps1 -List" }
    $cases = $selected
}
if ($Seeds.Count -gt 0) {
    # One case per suite and variant and seed; the seeds listed for the suite are replaced.
    $groups = $cases | Group-Object { "$($_.Mode)|$($_.Argument)|$($_.FixedFps)" }
    $cases = @(foreach ($group in $groups) {
        $first = $group.Group[0]
        $suffix = if ($first.Label -match '-(route[^-]*)$') { $Matches[1] } else { '' }
        New-GameTestCase $first.Mode $Seeds $first.Argument.Trim() $suffix $first.FixedFps
    })
}

if ($Argument.Count -gt 0) {
    $extra = ' ' + ($Argument -join ' ')
    foreach ($case in $cases) { $case.Argument += $extra }
}

if ($List) {
    $cases | Group-Object Mode | ForEach-Object { '{0,-28} {1}' -f $_.Name, (($_.Group | ForEach-Object { if ($_.Seed) { $_.Seed } else { 'výchozí' } }) -join ' ') }
    if ($Tool.Count -eq 0) {
        $tools = @(Get-GameTestCases $godot -IncludeTools | Where-Object { -not $_.InSuite } | ForEach-Object { $_.Mode } | Sort-Object -Unique)
        Write-Host ''
        Write-Host "Nástroje (-Tool): $($tools -join ', ')"
    }
    return
}

$watch = [System.Diagnostics.Stopwatch]::StartNew()
$results = @(Invoke-GameCases $godot $cases $Jobs $Tail)
$failed = @($results | Where-Object { $_.ExitCode -ne 0 })
$slowest = $results | Sort-Object Seconds -Descending | Select-Object -First 3
Write-Host ''
Write-Host ("Hotovo za {0:0} s: {1} OK, {2} selhalo ze {3}. Nejdelší: {4}" -f $watch.Elapsed.TotalSeconds, ($results.Count - $failed.Count), $failed.Count, $results.Count, (($slowest | ForEach-Object { "$($_.Label) $($_.Seconds) s" }) -join ', '))
if ($failed.Count -gt 0) {
    Write-Host "Selhalo: $(($failed | ForEach-Object { $_.Label }) -join ', ')"
    Write-Host "Logy: $($results[0].Directory)"
    exit 1
}

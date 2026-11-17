param(
    [switch]$Test,
    [switch]$Export,
    [ValidateRange(1, [int]::MaxValue)]
    [int]$TestJobs = [Math]::Max(1, [Math]::Min(16, [Environment]::ProcessorCount))
)
. "$PSScriptRoot/scripts-common.ps1"

function New-GameTestCase([string]$Mode, [int[]]$Seeds = @(0), [string]$Argument = '', [string]$Suffix = '') {
    foreach ($seed in $Seeds) {
        $label = if ($seed) { "$Mode-$seed" } else { $Mode }
        if ($Suffix) { $label = "$label-$Suffix" }
        [PSCustomObject]@{ Mode = $Mode; Seed = $seed; Label = $label; Argument = $Argument }
    }
}

# Road driving is by far the longest scenario, so each route runs as its own process.
# The "4-" part also covers any route added later.
function New-RoadDrivingCases([string[]]$Routes, [int[]]$Seeds = @(1, 16)) {
    foreach ($route in $Routes) {
        New-GameTestCase 'road-driving-checks' $Seeds " --road-driving-route=$route" "route$($route.TrimEnd('-'))"
    }
}

function Invoke-GameTests([string]$Godot, [int]$JobCount) {
    # Roughly longest first, so no long scenario starts near the end of the run.
    $cases = @(
        New-RoadDrivingCases '2', '3', '0'
        New-GameTestCase 'smoke-test'
        New-GameTestCase 'new-game-checks' 42
        New-GameTestCase 'track-checks'
        New-RoadDrivingCases '4-', '1'
        New-GameTestCase 'bandit-checks' 42
        New-GameTestCase 'collision-checks'
        New-GameTestCase 'controller-checks'
        New-GameTestCase 'village-checks' 42
        New-GameTestCase 'golem-checks' 42
        New-GameTestCase 'ford-checks' 1,42,7421
        New-GameTestCase 'works-checks' 1,42,7421
        New-GameTestCase 'build-checks' 1,42,7421
        New-GameTestCase 'weather-checks' 42
        New-GameTestCase 'postcard-checks' 42
        New-GameTestCase 'event-checks' 42
        New-GameTestCase 'trade-checks' 1,42,7421
        New-GameTestCase 'collection-checks' 42
        New-GameTestCase 'collector-pet-checks' 42
        New-GameTestCase 'garden-checks' 42
        New-GameTestCase 'farm-checks' 42
        New-GameTestCase 'save-checks' 42
        New-GameTestCase 'garage-checks' 42
        New-GameTestCase 'delivery-checks' 1,42,7421
        New-GameTestCase 'road-audit' 1,2,42,44,53,7421
    )
    $runId = '{0}-{1}' -f (Get-Date -Format 'yyyyMMdd-HHmmss'), [Guid]::NewGuid().ToString('N').Substring(0, 8)
    $runDirectory = Join-Path $PSScriptRoot "artifacts/game-tests/$runId"
    $active = [System.Collections.Generic.List[object]]::new()
    $failed = [System.Collections.Generic.List[string]]::new()
    $next = 0
    $completed = 0
    Write-Host "Testuji $($cases.Count) scénářů Godotu (souběh $JobCount). Výstupy jsou v $runDirectory"

    try {
        while ($completed -lt $cases.Count) {
            while ($next -lt $cases.Count -and $active.Count -lt $JobCount) {
                $case = $cases[$next++]
                $caseDirectory = Join-Path $runDirectory $case.Label
                New-Item -ItemType Directory -Path $caseDirectory -Force | Out-Null
                $log = Join-Path $caseDirectory 'godot.log'
                $fpsArgument = if ($case.Mode -notin 'road-audit', 'smoke-test') { ' --fixed-fps 60' } else { '' }
                $seedArgument = if ($case.Seed) { " --seed=$($case.Seed)" } else { '' }
                $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
                $startInfo.FileName = $Godot
                $startInfo.WorkingDirectory = $PSScriptRoot
                $startInfo.Arguments = '--headless --path "{0}" --log-file "{1}"{2} -- --{3}{4}{5}' -f $PSScriptRoot, $log, $fpsArgument, $case.Mode, $seedArgument, $case.Argument
                $startInfo.UseShellExecute = $false
                $startInfo.CreateNoWindow = $true
                $startInfo.EnvironmentVariables['MATHRACER_TEST_DIRECTORY'] = $caseDirectory
                $process = [System.Diagnostics.Process]::Start($startInfo)
                $active.Add([PSCustomObject]@{ Process = $process; Label = $case.Label; Log = $log })
            }

            foreach ($run in $active.ToArray()) {
                if (-not $run.Process.HasExited) { continue }
                $completed++
                $exitCode = $run.Process.ExitCode
                if ($exitCode -eq 0) {
                    Write-Host "[$completed/$($cases.Count)] OK $($run.Label)"
                } else {
                    $failed.Add($run.Label)
                    Write-Host "[$completed/$($cases.Count)] SELHAL $($run.Label) (kód $exitCode)"
                    if (Test-Path -LiteralPath $run.Log) { Get-Content -LiteralPath $run.Log -Encoding UTF8 -Tail 25 | Write-Host }
                    Write-Host "Podrobnosti v $($run.Log)"
                }
                [void]$active.Remove($run)
                $run.Process.Dispose()
            }

            if ($active.Count -gt 0) { Start-Sleep -Milliseconds 200 }
        }
    } finally {
        foreach ($run in $active) {
            Stop-Process -InputObject $run.Process -ErrorAction SilentlyContinue
            $run.Process.Dispose()
        }
    }

    if ($failed.Count -gt 0) { throw "Selhaly kontroly $($failed -join ', '). Výstupy jsou v $runDirectory" }
}

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

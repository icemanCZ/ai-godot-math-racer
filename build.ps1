param(
    [switch]$Test,
    [switch]$Export,
    [ValidateRange(1, [int]::MaxValue)]
    [int]$TestJobs = [Math]::Max(1, [Math]::Min(16, [Environment]::ProcessorCount))
)
. "$PSScriptRoot/scripts-common.ps1"

function New-GameTestCase([string]$Mode, [int[]]$Seeds = @(0), [string]$Argument = '', [string]$Suffix = '', [bool]$FixedFps = $true) {
    foreach ($seed in $Seeds) {
        $label = if ($seed) { "$Mode-$seed" } else { $Mode }
        if ($Suffix) { $label = "$label-$Suffix" }
        $extra = if ($Argument) { " $Argument" } else { '' }
        [PSCustomObject]@{ Mode = $Mode; Seed = $seed; Label = $label; Argument = $extra; FixedFps = $FixedFps }
    }
}

# The suites of the test run are listed in Scripts/Diagnostics/DiagnosticSuites.cs, in the order they should start
# (roughly longest first, so no long scenario starts near the end of the run). The game prints them as JSON.
function Get-GameTestCases([string]$Godot) {
    # Godot reports leaked resources on stderr when it quits this early, which PowerShell would treat as an error.
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $Godot
    $startInfo.WorkingDirectory = $PSScriptRoot
    $startInfo.Arguments = '--headless --path "{0}" -- --list-checks' -f $PSScriptRoot
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $process = [System.Diagnostics.Process]::Start($startInfo)
    $errorText = $process.StandardError.ReadToEndAsync()
    $output = $process.StandardOutput.ReadToEnd() -split "`r?`n"
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "Výpis kontrol selhal (kód $($process.ExitCode)): $($errorText.Result)" }
    $process.Dispose()
    $lines = @($output | Where-Object { "$_".StartsWith('DIAGNOSTIC_SUITE ') })
    if ($lines.Count -eq 0) { throw 'Hra nevypsala seznam kontrol (DIAGNOSTIC_SUITE).' }
    $suites = @($lines | ForEach-Object { "$_".Substring('DIAGNOSTIC_SUITE '.Length) | ConvertFrom-Json })
    foreach ($suite in $suites) {
        $seeds = @($suite.seeds | ForEach-Object { [int]$_ })
        if ($seeds.Count -eq 0) { $seeds = @(0) }
        $variants = @($suite.variants)
        if ($variants.Count -eq 0) { $variants = @([PSCustomObject]@{ argument = ''; suffix = '' }) }
        foreach ($variant in $variants) {
            New-GameTestCase $suite.mode $seeds $variant.argument $variant.suffix ([bool]$suite.fixedFps)
        }
    }
}

function Invoke-GameTests([string]$Godot, [int]$JobCount) {
    $cases = @(Get-GameTestCases $Godot)
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
                $fpsArgument = if ($case.FixedFps) { ' --fixed-fps 60' } else { '' }
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

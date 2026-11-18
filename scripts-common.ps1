$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
function Find-Godot {
    if ($env:GODOT_PATH -and (Test-Path -LiteralPath $env:GODOT_PATH)) { return $env:GODOT_PATH }
    $local = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot '.tools') -Filter '*_console.exe' -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($local) { return $local.FullName }
    # A git worktree has no .tools of its own: look in the main checkout, which the common git directory belongs to.
    $common = & git -C $PSScriptRoot rev-parse --git-common-dir 2>$null
    if ($LASTEXITCODE -eq 0 -and $common) {
        $gitDirectory = if ([System.IO.Path]::IsPathRooted($common)) { $common } else { Join-Path $PSScriptRoot $common }
        $main = Split-Path -Parent (Resolve-Path -LiteralPath $gitDirectory).Path
        $shared = Get-ChildItem -LiteralPath (Join-Path $main '.tools') -Filter '*_console.exe' -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($shared) { return $shared.FullName }
    }
    foreach ($name in 'godot','godot4') { $cmd = Get-Command $name -ErrorAction SilentlyContinue; if ($cmd) { return $cmd.Source } }
    throw 'Godot .NET není dostupný. Nastavte GODOT_PATH na cestu k jeho console.exe nebo rozbalte Godot .NET do .tools.'
}
function Assert-ExitCode([string]$step) { if ($LASTEXITCODE -ne 0) { throw "$step selhalo (kód $LASTEXITCODE)." } }

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

# Runs the cases in parallel, JobCount Godot processes at a time, and returns one result per case. Each case keeps
# its log and its test files in a folder of its own under artifacts/game-tests/<run>/<label>.
function Invoke-GameCases([string]$Godot, [object[]]$Cases, [int]$JobCount, [int]$TailLines = 25) {
    $runId = '{0}-{1}' -f (Get-Date -Format 'yyyyMMdd-HHmmss'), [Guid]::NewGuid().ToString('N').Substring(0, 8)
    $runDirectory = Join-Path $PSScriptRoot "artifacts/game-tests/$runId"
    $active = [System.Collections.Generic.List[object]]::new()
    $results = [System.Collections.Generic.List[object]]::new()
    $next = 0
    $completed = 0
    Write-Host "Testuji $($Cases.Count) scénářů Godotu (souběh $JobCount). Výstupy jsou v $runDirectory"

    try {
        while ($completed -lt $Cases.Count) {
            while ($next -lt $Cases.Count -and $active.Count -lt $JobCount) {
                $case = $Cases[$next++]
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
                $active.Add([PSCustomObject]@{ Process = $process; Label = $case.Label; Log = $log; Started = [DateTime]::UtcNow })
            }

            foreach ($run in $active.ToArray()) {
                if (-not $run.Process.HasExited) { continue }
                $completed++
                $exitCode = $run.Process.ExitCode
                $seconds = [Math]::Round(([DateTime]::UtcNow - $run.Started).TotalSeconds, 1)
                $results.Add([PSCustomObject]@{ Label = $run.Label; ExitCode = $exitCode; Log = $run.Log; Seconds = $seconds; Directory = $runDirectory })
                if ($exitCode -eq 0) {
                    Write-Host "[$completed/$($Cases.Count)] OK $($run.Label) ($seconds s)"
                } else {
                    Write-Host "[$completed/$($Cases.Count)] SELHAL $($run.Label) (kód $exitCode)"
                    if ($TailLines -gt 0 -and (Test-Path -LiteralPath $run.Log)) { Get-Content -LiteralPath $run.Log -Encoding UTF8 -Tail $TailLines | Write-Host }
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
    return $results.ToArray()
}

# All the suites of the test run, as build.ps1 -Test runs them.
function Invoke-GameTests([string]$Godot, [int]$JobCount) {
    $cases = @(Get-GameTestCases $Godot)
    $results = @(Invoke-GameCases $Godot $cases $JobCount)
    $failed = @($results | Where-Object { $_.ExitCode -ne 0 })
    if ($failed.Count -gt 0) { throw "Selhaly kontroly $(($failed | ForEach-Object { $_.Label }) -join ', '). Výstupy jsou v $($results[0].Directory)" }
}

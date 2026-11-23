<#
.SYNOPSIS
    Measures the frame rate and the graphics load of the game in a real window (not headless).

.DESCRIPTION
    Starts the game with --benchmark once per resolution: the car drives the roads by itself in the day, in a storm and
    at night and stands on the finished village square, with vsync off and no frame cap. Every stage prints its frame
    rate, GPU and CPU render time, draw calls and memory; the table at the end sums them up. The player's save is not
    touched (--seed). Runs one after another on purpose: two benchmarks at once would share the GPU.

.EXAMPLE
    ./benchmark.ps1
    ./benchmark.ps1 -Resolution 1280x720, 1920x1080 -Seconds 20
    ./benchmark.ps1 -Build -Resolution 1920x1080 -Scale 0.5
#>
param(
    [string[]]$Resolution = @('1280x720', '1920x1080', '2560x1440'),
    [int]$Seed = 42,
    # Seconds measured per driving stage; the village stages take 60 % of it.
    [int]$Seconds = 15,
    # 3D resolution scale (1 = full); below 1 draws the 3D view smaller and stretches it.
    [double]$Scale = 1,
    [ValidateSet('opengl3', 'opengl3_angle')]
    [string]$Driver = 'opengl3',
    # Parts of the picture to turn off, to see what they cost: shadows, msaa, glow, fog, grass, plants, details, village, wildlife,
    # forest, sea, outland (the borders and the ground beyond the old valley), outland-plants (the plants beyond it).
    [string[]]$Off = @(),
    [switch]$Build
)
. "$PSScriptRoot/scripts-common.ps1"
$godot = Find-Godot
if ($Build) {
    dotnet build "$PSScriptRoot/MathRacer.csproj"
    Assert-ExitCode 'Sestavení C#'
}
$runDirectory = Join-Path $PSScriptRoot ("artifacts/benchmark/{0}" -f (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $runDirectory -Force | Out-Null
# Godot would import summary.csv as a table of translations.
New-Item -ItemType File -Path (Join-Path $PSScriptRoot 'artifacts/benchmark/.gdignore') -Force | Out-Null
$rows = foreach ($size in $Resolution) {
    $log = Join-Path $runDirectory ("$size" + $(if ($Off.Count) { '-bez-' + ($Off -join '-') } else { '' }) + '.log')
    $scaleText = $Scale.ToString([Globalization.CultureInfo]::InvariantCulture)
    $arguments = '--path "{0}" --log-file "{1}" --rendering-driver {2} --resolution {3} -- --play --benchmark --seed={4} --benchmark-seconds={5} --benchmark-scale={6}{7}' -f $PSScriptRoot, $log, $Driver, $size, $Seed, $Seconds, $scaleText, $(if ($Off.Count) { ' --benchmark-off=' + ($Off -join ',') } else { '' })
    Write-Host "Měřím $size ($Driver, měřítko 3D $scaleText, vypnuto: $(if ($Off.Count) { $Off -join ', ' } else { 'nic' }))…"
    $process = Start-Process -FilePath $godot -ArgumentList $arguments -WorkingDirectory $PSScriptRoot -PassThru -WindowStyle Normal
    if (-not $process.WaitForExit(($Seconds * 6 + 180) * 1000)) {
        Stop-Process -InputObject $process -Force
        Write-Warning "Měření $size nedoběhlo; log je v $log"
        continue
    }
    $lines = @(Get-Content -LiteralPath $log -Encoding UTF8 | Where-Object { $_ -like 'BENCHMARK*' })
    $lines | Write-Host
    foreach ($line in $lines | Where-Object { $_ -like 'BENCHMARK stage=*' }) {
        $values = @{}
        foreach ($match in [regex]::Matches($line, '(\w+)=(\S+)')) { $values[$match.Groups[1].Value] = $match.Groups[2].Value }
        [PSCustomObject]@{
            Rozliseni = $size; Vypnuto = ($Off -join ','); Scena = $values.stage; FPS = $values.fps_avg; '1% low' = $values.fps_1pct_low
            'GPU ms' = $values.gpu_ms_avg; 'CPU ms' = $values.cpu_render_ms_avg; 'Draw calls' = $values.draws_avg
            Trojuhelniky = $values.primitives_avg; 'VRAM MB' = $values.vram_mb; 'RAM MB' = $values.process_ram_mb
        }
    }
}
$rows | Format-Table -AutoSize
$rows | Export-Csv -LiteralPath (Join-Path $runDirectory 'summary.csv') -NoTypeInformation -Encoding UTF8
Write-Host "Logy a summary.csv jsou v $runDirectory"

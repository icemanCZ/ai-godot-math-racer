$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
function Find-Godot {
    if ($env:GODOT_PATH -and (Test-Path -LiteralPath $env:GODOT_PATH)) { return $env:GODOT_PATH }
    $local = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot '.tools') -Filter '*_console.exe' -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($local) { return $local.FullName }
    foreach ($name in 'godot','godot4') { $cmd = Get-Command $name -ErrorAction SilentlyContinue; if ($cmd) { return $cmd.Source } }
    throw 'Godot .NET není dostupný. Nastavte GODOT_PATH na cestu k jeho console.exe nebo rozbalte Godot .NET do .tools.'
}
function Assert-ExitCode([string]$step) { if ($LASTEXITCODE -ne 0) { throw "$step selhalo (kód $LASTEXITCODE)." } }

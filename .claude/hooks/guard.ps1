# PreToolUse hook of Claude Code (see .claude/settings.json). It refuses, with exit code 2 and a reason on stderr,
# the tool calls that would break a rule of CLAUDE.md before any check could notice:
# - an edit of a committed save fixture or world fingerprint (Tests/Saves, Tests/Worlds), which is never edited;
# - a Godot started by hand instead of through test.ps1 or build.ps1, and the game started by run.ps1, run.cmd,
#   run.bat or god-mode.bat without -Capture, whose autosave would overwrite the player's progress.json.
# repo-checks catches a changed fixture afterwards; this hook stops the change before it is made.
$ErrorActionPreference = 'Stop'
[Console]::InputEncoding = [System.Text.Encoding]::UTF8
$payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
$toolInput = $payload.tool_input

function Deny([string]$reason) {
    [Console]::Error.WriteLine($reason)
    exit 2
}

$path = "$($toolInput.file_path)$($toolInput.notebook_path)"
if ($path) {
    if (($path -replace '\\', '/') -match '(?i)(^|/)Tests/(Saves|Worlds)/') {
        Deny ("$path is a committed save fixture or world fingerprint and is never edited or regenerated. A new save " +
            'format gets a folder of its own (./test.ps1 -Tool save-fixtures) and a migration, a new part of the world ' +
            'a fingerprint file of its own (./test.ps1 -Tool write-world-fingerprint); see Tests/README.md.')
    }
    exit 0
}

$command = "$($toolInput.command)"
if (-not $command) { exit 0 }

# Looking for, or stopping, a stray Godot is allowed: a capture sometimes stays alive after it wrote its picture.
if ($command -match '(?i)\b(Stop-Process|taskkill|Get-Process|tasklist|Get-CimInstance|Get-WmiObject)\b') { exit 0 }

# Pointing GODOT_PATH at a Godot for test.ps1 is not a launch.
$launch = $command -replace '(?i)\$env:GODOT_PATH\s*=\s*("[^"]*"|''[^'']*''|\S+)', ''
$direct = $launch -match '(?i)godot[\w.-]*\.exe|_console\.exe|Find-Godot|&\s*\$env:GODOT_PATH|\.tools[\\/][^\s''"]*godot'
if ($direct -and $command -notmatch '--no-save') {
    Deny ('Godot is not started by hand: run checks with ./test.ps1 (-Suite, -Seeds, -Build), tools with ' +
        './test.ps1 -Tool NAME -Seeds N -Argument ..., everything with ./build.ps1 -Test, pictures with ' +
        './run.ps1 -Capture -View NAME. The game itself only with --no-save, so that it keeps off the player''s save.')
}

$launcher = '(?i)(^|[;&|(]\s*|&\s*["'']?)(\.[\\/]|[\w:\\/.-]*[\\/])?(run\.(ps1|cmd|bat)|god-mode\.bat)\b'
if ($command -match $launcher -and $command -notmatch '(?i)-Capture\b') {
    Deny ('run.ps1, run.cmd, run.bat and god-mode.bat start the game on the player''s own save, and its autosave ' +
        'overwrites progress.json. Use ./run.ps1 -Capture -View NAME for a picture, or a check through ./test.ps1.')
}
exit 0

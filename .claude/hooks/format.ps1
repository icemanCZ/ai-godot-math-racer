# PostToolUse hook of Claude Code (see .claude/settings.json): formats a C# file that was just written or edited with
# the CSharpier of dotnet-tools.json, as CLAUDE.md asks before a commit. repo-checks fails on code that is not
# formatted, so a failure here only means the check will tell later; the hook never blocks.
$ErrorActionPreference = 'Stop'
[Console]::InputEncoding = [System.Text.Encoding]::UTF8
try {
    $payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
    $path = "$($payload.tool_input.file_path)"
    if (-not $path.EndsWith('.cs', [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $path)) { exit 0 }
    $root = if ($env:CLAUDE_PROJECT_DIR) { $env:CLAUDE_PROJECT_DIR } else { Split-Path -Parent (Split-Path -Parent $PSScriptRoot) }
    Set-Location -LiteralPath $root
    $output = & dotnet csharpier format $path 2>&1
    if ($LASTEXITCODE -ne 0) { [Console]::Error.WriteLine("CSharpier did not format ${path}: $output") }
} catch {
    [Console]::Error.WriteLine("The format hook failed: $_")
}
exit 0

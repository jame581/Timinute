# PostToolUse hook: whitespace-formats a just-edited C# file with dotnet format.
# Folder mode needs no MSBuild/restore, so it stays fast.
$payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
$path = $payload.tool_input.file_path
if (-not $path -or -not $path.EndsWith('.cs') -or -not (Test-Path $path)) { exit 0 }

$root = if ($env:CLAUDE_PROJECT_DIR) { $env:CLAUDE_PROJECT_DIR } else { (Get-Location).Path }
$rel = [IO.Path]::GetRelativePath($root, $path)
if ($rel.StartsWith('..')) { exit 0 }  # outside the repo — leave it alone

dotnet format whitespace $root --folder --include $rel *> $null
exit 0

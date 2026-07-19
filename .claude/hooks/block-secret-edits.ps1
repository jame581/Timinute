# PreToolUse hook: blocks Edit/Write on secret and signing-key files.
# Reads the hook JSON payload from stdin; exit 2 = block the tool call.
$payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
$path = $payload.tool_input.file_path
if (-not $path) { exit 0 }

$blocked = @(
    '(^|[\\/])\.env$',          # root .env (real secrets; .env.example stays editable)
    '[\\/]Server[\\/]keys[\\/]', # Duende IdentityServer signing keys
    'tempkey\.jwk$'              # dev signing key
)

foreach ($pattern in $blocked) {
    if ($path -match $pattern) {
        [Console]::Error.WriteLine("Blocked: '$path' is a secret/signing-key file and must never be edited by tooling.")
        exit 2
    }
}
exit 0

# For Codex's local HTTP header helper only. Do not run interactively or log output.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Security
$bytes = [System.Security.Cryptography.ProtectedData]::Unprotect(
    [IO.File]::ReadAllBytes((Join-Path $PSScriptRoot 'key.dpapi')),
    $null, [System.Security.Cryptography.DataProtectionScope]::CurrentUser)
try {
    @{ 'X-Goog-Api-Key' = [Text.Encoding]::UTF8.GetString($bytes) } | ConvertTo-Json -Compress
} finally {
    [Array]::Clear($bytes, 0, $bytes.Length)
}

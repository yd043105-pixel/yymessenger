# Exercise input handling without opening the dialog, accessing real keys, or changing Codex configuration.
$ErrorActionPreference = 'Stop'
$taskTokens = $null
$taskErrors = $null
$taskAst = [Management.Automation.Language.Parser]::ParseFile(
    (Join-Path $PSScriptRoot '../scripts/Connect-Stitch.ps1'), [ref]$taskTokens, [ref]$taskErrors)
if ($taskErrors.Count) { throw 'Setup script syntax check failed' }
$taskFunction = $taskAst.Find({ param($node)
    $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'ConvertTo-StitchApiKey'
}, $true)
if (!$taskFunction) { throw 'Key input handler not found' }
. ([scriptblock]::Create($taskFunction.Extent.Text))

$taskAccepted = @(
    'synthetic.ABC_def-123+/=',
    ('synthetic.' + ('A' * 512)),
    'synthetic-key',
    ('A' * 8192)
)
foreach ($taskSample in $taskAccepted) {
    if ((ConvertTo-StitchApiKey $taskSample) -cne $taskSample) { throw 'Accepted key was altered' }
}
$taskTrimmed = ConvertTo-StitchApiKey " `r`nsynthetic.ABC+/= `r`n"
if ($taskTrimmed -cne 'synthetic.ABC+/=') { throw 'Outer copy/paste whitespace was not removed' }

$taskRejected = @($null, '', '   ', "synthetic`r`nX-Injected: value", "synthetic`nother",
    'synthetic key', "synthetic`tkey", ("synthetic" + [char]0x200B), ('A' * 8193))
foreach ($taskSample in $taskRejected) {
    $taskDidReject = $false
    try { ConvertTo-StitchApiKey $taskSample | Out-Null } catch { $taskDidReject = $true }
    if (!$taskDidReject) { throw 'Unsafe or empty input was accepted' }
}
Write-Output 'PASS: 14 synthetic key input cases; no real credentials or configuration accessed.'

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$source = Get-Content -LiteralPath (Join-Path $projectRoot 'scripts\Configure-RemoteFirewall.ps1') -Raw
# Only mock cmdlets run here. Strip elevation directive for this isolated check.
$configure = [scriptblock]::Create(($source -replace '(?m)^#Requires[^\r\n]*', ''))
$script:applied = $null; $script:existing = $false; $script:removed = $false
function Get-NetFirewallRule { [CmdletBinding()]param($Name) if ($script:existing) { [pscustomobject]@{ Name = $Name } } }
function New-NetFirewallRule {
    param($Name,$DisplayName,$Program,$Direction,$Action,$Enabled,$Profile,$Protocol,$LocalPort,$RemoteAddress)
    $script:applied = $PSBoundParameters; $script:existing = $true
}
function Set-NetFirewallRule {
    [CmdletBinding()]param([Parameter(ValueFromPipeline)]$InputObject,$Program,$Direction,$Action,$Enabled,$Profile,$Protocol,$LocalPort,$RemoteAddress)
    process { $script:applied = $PSBoundParameters }
}
function Remove-NetFirewallRule { [CmdletBinding()]param([Parameter(ValueFromPipeline)]$InputObject) process { $script:removed = $true } }
$clientExecutable = Join-Path $projectRoot 'src\SchoolMessenger.Desktop\bin\Debug\net10.0-windows\SchoolMessenger.exe'
& $configure -ClientPath $clientExecutable -RemoteAddresses '192.168.10.0/24' -WhatIf
if ($script:applied) { throw 'WhatIf changed a rule' }
& $configure -ClientPath $clientExecutable -RemoteAddresses '192.168.10.0/24'
if ($script:applied.LocalPort -ne '49152-65535' -or $script:applied.Protocol -ne 'TCP' -or $script:applied.Direction -ne 'Inbound' -or
    ($script:applied.Profile -join ',') -ne 'Domain,Private' -or $script:applied.RemoteAddress[0] -ne '192.168.10.0/24' -or
    $script:applied.Program -ne $clientExecutable) { throw 'Firewall scope mismatch' }
& $configure -ClientPath $clientExecutable
if ($script:applied.RemoteAddress[0] -ne 'LocalSubnet') { throw 'Default scope must be local subnet' }
foreach ($invalidNetwork in @('Any','0.0.0.0/0','192.168.10.0/33','bogus')) {
    $rejected = $false
    try { & $configure -ClientPath $clientExecutable -RemoteAddresses $invalidNetwork } catch { $rejected = $true }
    if (-not $rejected) { throw "Invalid network allowed: $invalidNetwork" }
}
& $configure -ClientPath $clientExecutable -Remove
if (-not $script:removed) { throw 'Rule removal failed' }
Write-Host 'PASS firewall scope, update, WhatIf, removal and invalid-network rejection using mocks; actual firewall untouched.'

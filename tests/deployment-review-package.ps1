param([Parameter(Mandatory=$true)][string]$FixtureRoot)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$folder=Join-Path ([IO.Path]::GetFullPath($FixtureRoot)) ('review-package-'+[guid]::NewGuid().ToString())
New-Item -ItemType Directory -Path (Join-Path $folder 'config') -Force|Out-Null
$template=Join-Path $folder 'config\deployment-review.draft.json'
Copy-Item -LiteralPath (Join-Path $root 'config\deployment-review.draft.json') -Destination $template
$checker=Join-Path $root 'scripts\Test-DeploymentReviewPackage.ps1'
& $checker -PackageDirectory $folder
Write-Host 'PASS Public review draft can be packaged'
$operator=Join-Path $folder 'config\review.json'
Copy-Item -LiteralPath $template -Destination $operator
$rejected=$false;try{& $checker -PackageDirectory $folder}catch{$rejected=$true}
if(-not $rejected){throw 'School-specific configuration was accepted'}
Remove-Item -LiteralPath $operator
Write-Host 'PASS Unlisted school configuration cannot enter the installer'
$draft=Get-Content -LiteralPath $template -Raw -Encoding UTF8|ConvertFrom-Json
$draft.status='approved';$draft|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $template -Encoding UTF8
$rejected=$false;try{& $checker -PackageDirectory $folder}catch{$rejected=$true}
if(-not $rejected){throw 'Approved record was accepted as a public draft'}
Write-Host 'PASS Approved school review cannot masquerade as the public draft'
Copy-Item -LiteralPath $template -Destination (Join-Path $folder 'renamed-review.json')
Copy-Item -LiteralPath (Join-Path $root 'config\deployment-review.draft.json') -Destination $template
$rejected=$false;try{& $checker -PackageDirectory $folder}catch{$rejected=$true}
if(-not $rejected){throw 'Renamed school review was accepted'}
Write-Host 'PASS Renamed review records are blocked outside the configuration directory'

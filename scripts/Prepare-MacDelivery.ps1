param(
    [string]$BuildDirectory,
    [ValidatePattern('^[0-9a-f]{40}$')][string]$SourceHead,
    [ValidatePattern('^https://github\.com/yd043105-pixel/yymessenger/actions/runs/[0-9]+$')][string]$VerificationRun
)
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
if(-not $BuildDirectory){$BuildDirectory=Join-Path $projectRoot 'artifacts\mac'}
$version=[string]([xml](Get-Content -LiteralPath (Join-Path $projectRoot 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
$report=Get-Content -LiteralPath (Join-Path $BuildDirectory 'mac-results.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if($report.ok -ne $true -or @($report.passed).Count -lt 1){throw 'Mac native verification must pass before preparing delivery.'}
$files=@()
foreach($architecture in @('arm64','x64')){
    $name="yymessenger-$version-macos-$architecture-trial.zip"
    $source=Join-Path $BuildDirectory $name
    $expected=((Get-Content -LiteralPath ($source+'.sha256') -Raw).Trim() -split '\s+')[0]
    $hash=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
    if($expected -notmatch '^[0-9a-fA-F]{64}$' -or $hash -ne $expected.ToLowerInvariant()){throw "Mac build checksum mismatch: $name"}
    $files+=[pscustomobject]@{file=$name;architecture=$architecture;bytes=(Get-Item -LiteralPath $source).Length;sha256=$hash}
}
$destination=Join-Path $projectRoot '01_설치파일\05_Mac'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
foreach($file in $files){
    $target=Join-Path $destination $file.file
    Copy-Item -LiteralPath (Join-Path $BuildDirectory $file.file) -Destination $target -Force
    if((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $file.sha256){throw 'Mac delivery copy mismatch'}
}
$manifest=[pscustomobject]@{version=$version;preparedAt=[DateTimeOffset]::UtcNow.ToString('o');configuration='Release';signing='AdHoc';notarized=$false;releasePublished=$false;sourceHead=$SourceHead;verificationRun=$VerificationRun;nativeChecks=@($report.passed).Count;files=$files}
$manifest|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $destination 'Mac전달파일목록.json') -Encoding UTF8
@($files|ForEach-Object {"$($_.sha256)  $($_.file)"})|Set-Content -LiteralPath (Join-Path $destination 'SHA256SUMS.txt') -Encoding ASCII
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\mac-operations.md') -Destination (Join-Path $destination '00_Mac설치안내.txt') -Force
Copy-Item -LiteralPath (Join-Path $BuildDirectory 'mac-results.json') -Destination (Join-Path $destination '네이티브실행검사.json') -Force
Write-Host 'Mac trial delivery prepared. Developer ID signing and Apple notarization are not completed.'

param()
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$artifacts=Join-Path $projectRoot 'artifacts'
$version=[string]([xml](Get-Content -LiteralPath (Join-Path $projectRoot 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
$files=@{
    '01_서버PC'=@("yymessenger-$version-server-setup-win-x64.exe","yymessenger-$version-servers-win-x64.zip","yymessenger-$version-setup-win-x64.exe")
    '02_교사PC'=@("yymessenger-$version-setup-win-x64.exe")
    '03_안드로이드'=@('mobile\kr.school.yeoyang.teacher-Signed.apk','mobile\kr.school.yeoyang.family-Signed.apk')
}
# Fail before changing the delivery kit if a required current build is missing.
foreach($group in $files.Values){foreach($relative in $group){if(-not(Test-Path -LiteralPath (Join-Path $artifacts $relative) -PathType Leaf)){throw "먼저 최신 빌드를 완료하세요: $relative"}}}

$sourceStage=Join-Path $artifacts ('ios-source-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $sourceStage | Out-Null
try{
    foreach($file in @('global.json','Directory.Build.props')){Copy-Item -LiteralPath (Join-Path $projectRoot $file) -Destination $sourceStage}
    foreach($project in @('SchoolMessenger.Mobile','SchoolMessenger.Contracts','SchoolMessenger.Shared')){
        $target=Join-Path $sourceStage "src\$project"
        New-Item -ItemType Directory -Path $target -Force | Out-Null
        Get-ChildItem -LiteralPath (Join-Path $projectRoot "src\$project") -Force | Where-Object {$_.Name -notin @('bin','obj')} | Copy-Item -Destination $target -Recurse
    }
    foreach($folder in @('scripts','tests','docs')){New-Item -ItemType Directory -Path (Join-Path $sourceStage $folder) | Out-Null}
    Copy-Item -LiteralPath (Join-Path $projectRoot 'scripts\Build-iOS.sh') -Destination (Join-Path $sourceStage 'scripts')
    Copy-Item -LiteralPath (Join-Path $projectRoot 'tests\IosRuntimeChecks.cs') -Destination (Join-Path $sourceStage 'tests')
    foreach($doc in @('ios-operations.md','ios-verification.md','final-test-runbook.md','operation-order.md')){Copy-Item -LiteralPath (Join-Path $projectRoot "docs\$doc") -Destination (Join-Path $sourceStage 'docs')}
    $entries=@(Get-ChildItem -LiteralPath $sourceStage -Recurse -Force)
    if(@($entries | Where-Object {($_.Attributes -band [IO.FileAttributes]::ReparsePoint) -or $_.Name -match '(?i)(\.db($|-)|\.dpapi$|\.pfx$|\.pem$|\.key$|\.p12$|\.mobileprovision$|\.keystore$|\.jks$|^\.env|^secrets\.json$|^login\.dat$|^preferences\.json$|^appsettings\.Production\.json$)'}).Count){throw 'iOS 전달 소스에 비밀 파일 또는 링크가 있습니다.'}
    $iosZip="yymessenger-$version-ios-build-source.zip"
    Compress-Archive -LiteralPath @(Get-ChildItem -LiteralPath $sourceStage | ForEach-Object FullName) -DestinationPath (Join-Path $artifacts $iosZip) -Force
    $files['04_iOS']=@($iosZip)
}finally{
    $resolved=[IO.Path]::GetFullPath($sourceStage)
    if(-not $resolved.StartsWith($artifacts+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw '임시 소스 정리 경로 오류'}
    if(((Get-Item -LiteralPath $resolved).Attributes -band [IO.FileAttributes]::ReparsePoint) -or
       @(Get-ChildItem -LiteralPath $resolved -Recurse -Force | Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}).Count){throw '임시 소스 링크는 삭제하지 않습니다.'}
    Remove-Item -LiteralPath $resolved -Recurse -Force
}

$kit=Join-Path $projectRoot '01_설치파일'
$manifest=@()
foreach($group in ($files.Keys | Sort-Object)){
    $destination=Join-Path $kit $group
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    $checksums=@()
    foreach($relative in $files[$group]){
        $source=Join-Path $artifacts $relative
        $target=Join-Path $destination (Split-Path $source -Leaf)
        Copy-Item -LiteralPath $source -Destination $target -Force
        $hash=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant()
        if((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $hash){throw "전달 사본 검증 실패: $target"}
        $checksums+="$hash  $(Split-Path $source -Leaf)"
        $manifest+=[pscustomobject]@{device=$group;file=(Split-Path $source -Leaf);bytes=(Get-Item -LiteralPath $target).Length;sha256=$hash}
    }
    Set-Content -LiteralPath (Join-Path $destination 'SHA256SUMS.txt') -Value $checksums -Encoding ASCII
}
$mobile=Join-Path $artifacts 'mobile'
Set-Content -LiteralPath (Join-Path $mobile 'SHA256SUMS.txt') -Value @($manifest | Where-Object device -eq '03_안드로이드' | ForEach-Object {"$($_.sha256)  $($_.file)"}) -Encoding ASCII
$windows=@("yymessenger-$version-win-x64.zip","yymessenger-$version-servers-win-x64.zip","yymessenger-$version-server-setup-win-x64.exe","yymessenger-$version-setup-win-x64.exe",$iosZip)
Set-Content -LiteralPath (Join-Path $artifacts 'SHA256SUMS.txt') -Value @($windows | ForEach-Object {((Get-FileHash -LiteralPath (Join-Path $artifacts $_) -Algorithm SHA256).Hash.ToLowerInvariant())+"  $_"}) -Encoding ASCII
$delivery=[pscustomobject]@{version=$version;preparedAt=[DateTimeOffset]::UtcNow.ToString('o');releasePublished=$false;androidConfiguration='Debug';iosInstallable=$false;files=$manifest}
$delivery | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $kit '전달파일목록.json') -Encoding UTF8
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\operation-order.md') -Destination (Join-Path $kit '00_설치순서.txt') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\operation-order.md') -Destination (Join-Path $kit '01_서버PC\00_설치순서.txt') -Force
Write-Host '완료: 01_설치파일의 기기별 폴더에 실제 파일을 복사하고 SHA256을 대조했습니다. 정식 배포는 수행하지 않았습니다.'

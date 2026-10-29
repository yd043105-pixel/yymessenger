param([Parameter(Mandatory=$true)][string]$Destination)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$destinationPath=[IO.Path]::GetFullPath($Destination)
if(-not [IO.Path]::IsPathRooted($Destination) -or $destinationPath.TrimEnd('\') -eq [IO.Path]::GetPathRoot($destinationPath).TrimEnd('\')){throw 'Choose a dedicated delivery folder.'}
$stage=Join-Path $root ('artifacts\public-delivery-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage|Out-Null
$version=[string]([xml](Get-Content -LiteralPath (Join-Path $root 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
function Put([string]$Source,[string]$Relative){
    $target=Join-Path $stage $Relative
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force|Out-Null
    Copy-Item -LiteralPath (Join-Path $root $Source) -Destination $target
}
function Note([string]$Relative,[string]$Text){
    $target=Join-Path $stage $Relative
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force|Out-Null
    [IO.File]::WriteAllText($target,$Text,[Text.UTF8Encoding]::new($true))
}
Put 'output\pdf\public\00_먼저읽기.pdf' '00_먼저읽기.pdf'
Put 'docs\public\00_먼저읽기.md' '00_먼저읽기.txt'
foreach($name in @('01_관리자_설치와운영','02_교직원_사용안내','03_학생학부모_앱안내')){
    Put "output\pdf\public\$name.pdf" "01_설명서\$name.pdf"
}
Get-ChildItem -LiteralPath (Join-Path $root 'docs\public') -Filter '*.md' -File|ForEach-Object {Put ('docs\public\'+$_.Name) ('01_설명서\수정용원문\'+$_.Name)}
Put "artifacts\yymessenger-$version-server-setup-win-x64.exe" "02_설치파일\01_서버관리자_Windows\교무톡_서버설치_${version}_Windows.exe"
Put "artifacts\yymessenger-$version-setup-win-x64.exe" "02_설치파일\02_교직원_Windows\교무톡_교사용_${version}_Windows.exe"
Put "01_설치파일\05_Mac\yymessenger-$version-macos-arm64-trial.zip" "02_설치파일\03_교직원_Mac\교무톡_Mac_AppleSilicon_${version}_시험본.zip"
Put "01_설치파일\05_Mac\yymessenger-$version-macos-x64-trial.zip" "02_설치파일\03_교직원_Mac\교무톡_Mac_Intel_${version}_시험본.zip"
Put 'artifacts\mobile\kr.school.yeoyang.teacher-Signed.apk' '02_설치파일\04_Android\교무톡_교사용_Android_시험본.apk'
Put 'artifacts\mobile\kr.school.yeoyang.family-Signed.apk' '02_설치파일\04_Android\학교톡_학생학부모용_Android_시험본.apk'
Note '02_설치파일\05_iOS\준비상태.txt' @'
iPhone·iPad 설치 준비 상태

현재 이 전달본에는 설치 가능한 IPA 또는 TestFlight 초대가 없습니다.
iOS 프로젝트는 별도 Mac에서 빌드하고 Apple 서명·프로비저닝·배포를 준비해야 합니다.
소스 ZIP은 설치 파일이 아니므로 사용자용 폴더에는 넣지 않았습니다.
추후 학교가 안내하는 배포 경로를 사용하세요.
'@
Note '02_설치파일\04_Android\시험본안내.txt' @'
두 APK는 각각 교무톡(교사용) / 학교톡(학생·학부모용) 앱입니다.
현재 시험 서명본이며 정식 스토어 배포본이 아닙니다.
학교가 허용한 시험 기기에서 사용하고 실제 운영 자료를 넣기 전에 운영 검토를 완료하세요.
앱 종료 상태의 운영체제 푸시 알림은 현재 제공·검증 범위에 포함되지 않습니다.
'@
Note '02_설치파일\03_교직원_Mac\시험본안내.txt' @'
Apple Silicon 칩: AppleSilicon ZIP / Intel 프로세서: Intel ZIP을 선택하세요.
압축을 풀고 앱을 응용 프로그램 폴더로 옮깁니다.
현재 시험본이며 일반 배포용 Apple 서명·공증이 완료되지 않았습니다.
운영체제 보안 정책으로 실행이 차단되면 관리자에게 문의하세요. 보안 기능을 끄지 마세요.
지원 기능과 로그인 순서는 교직원 설명서를 확인하세요.
'@
Put 'docs\public\학교도입_내부기안_작성양식.txt' '03_관리자_준비양식\학교도입_내부기안_작성양식.txt'
Put 'docs\public\교육현장_운영고지.txt' '03_관리자_준비양식\교육현장_운영고지.txt'
foreach($name in @('registration-policy.draft.json','deployment-review.draft.json','server-operations.example.json','cloudflared.example.yml')){Put "config\$name" "03_관리자_준비양식\$name"}
Get-ChildItem -LiteralPath (Join-Path $root 'docs\policies') -File|ForEach-Object {Put ('docs\policies\'+$_.Name) ('03_관리자_준비양식\'+$_.Name)}
Note '03_관리자_준비양식\먼저읽기.txt' @'
이 폴더는 학교별 운영 준비를 위한 미확정 예시입니다.
실제 운영 설정이나 승인된 약관이 아닙니다. 관리자 설명서 3~5장을 먼저 읽으세요.
학교명·문의처·개인정보 처리 기준·보유기간·책임자·보안 검토 자료는 운영 학교에서 확정합니다.
실제 작성한 운영 설정은 보호된 별도 폴더에 보관하고 이 전달 폴더에 다시 넣지 마세요.
예시 주소 example.invalid는 접속할 수 없는 문서용 주소입니다.
인증서·터널 토큰·비밀번호·학생 명부·실제 검토 자료를 설치 파일과 함께 공유하지 마세요.
'@
Note '99_검증결과\개인정보점검결과.txt' @'
교무톡 1.0.0 전달본 점검 범위

설명서: 역할별 PDF 4권 16쪽과 원문, 설치 안내문을 점검했습니다.
설정 예시: 학교명·문의처·내부 IP·개발 PC 경로 등 특정 운영자의 정보가 남지 않게 정리했습니다.
약관 예시: 학교별 문의처 입력란으로 통일했으며 운영 승인 상태는 미확정으로 유지합니다.
서버 설치본: 기존 학교별 개발·감사 문서를 제외하고 공통 안내문과 새 설정 예시로 다시 구성했습니다.
파일 전달: 실제 파일을 복사한 뒤 원본·사본의 SHA-256을 대조합니다.

유지한 정보: 요청에 따라 앱의 MIT 저작권 표시와 학교 등록 신청 연락처는 유지합니다.
기존 설치 경로·앱 식별자·인증 용도 문자열 등 호환 정보도 유지합니다.
이 점검은 모든 앱 바이너리의 익명화나 보안 인증을 의미하지 않습니다.

제외: 실제 명부·DB·운영 비밀 설정·인증서 비밀키·학교별 승인 기록·개인 PC 바로가기.
상태: Windows 설치본 / Android 시험 서명본 / Mac 시험본 / iOS 설치본 미포함.
실제 학교망 연결, 외부 도메인·인증서, 스토어 배포와 OS 푸시는 이번 복사 작업으로 완료되지 않습니다.
'@
$files=@(Get-ChildItem -LiteralPath $stage -Recurse -File|Sort-Object FullName)
$records=@($files|ForEach-Object {[pscustomobject]@{file=[IO.Path]::GetRelativePath($stage,$_.FullName).Replace('\','/');bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}})
@{version=$version;guideDate='2026-10-29';releasePublished=$false;android='debug-trial';mac='ad-hoc-trial';iosInstallable=$false;files=$records}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $stage '99_검증결과\파일목록.json') -Encoding utf8
$records|ForEach-Object {"$($_.sha256)  $($_.file)"}|Set-Content -LiteralPath (Join-Path $stage '99_검증결과\SHA256SUMS.txt') -Encoding utf8
# Never delete existing destination files or overwrite a different delivery silently.
New-Item -ItemType Directory -Path $destinationPath -Force|Out-Null
if((Get-Item -LiteralPath $destinationPath).Attributes -band [IO.FileAttributes]::ReparsePoint -or @(Get-ChildItem -LiteralPath $destinationPath -Force -Recurse|Where-Object {$_.Attributes -band [IO.FileAttributes]::ReparsePoint}).Count){throw 'Linked destination is not allowed.'}
$all=@(Get-ChildItem -LiteralPath $stage -Recurse -File)
$previous=@{}
$previousManifest=Join-Path $destinationPath '99_검증결과\파일목록.json'
if(Test-Path -LiteralPath $previousManifest){
    foreach($entry in (Get-Content -LiteralPath $previousManifest -Raw -Encoding utf8|ConvertFrom-Json).files){$previous[$entry.file.Replace('/','\')]=$entry.sha256}
}
foreach($file in $all){
    $relative=[IO.Path]::GetRelativePath($stage,$file.FullName);$target=Join-Path $destinationPath $relative
    if((Test-Path -LiteralPath $target) -and (Get-FileHash -LiteralPath $target).Hash -ne (Get-FileHash -LiteralPath $file.FullName).Hash){
        $metadata=$relative -in @('99_검증결과\파일목록.json','99_검증결과\SHA256SUMS.txt') -and (Test-Path -LiteralPath $previousManifest)
        if(-not $metadata -and (-not $previous.ContainsKey($relative) -or (Get-FileHash -LiteralPath $target).Hash -ne $previous[$relative])){throw 'Destination contains an edited file. Preserve it and choose a new delivery folder.'}
    }
}
foreach($file in $all){
    $target=Join-Path $destinationPath ([IO.Path]::GetRelativePath($stage,$file.FullName))
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force|Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $target -Force
    if((Get-FileHash -LiteralPath $target).Hash -ne (Get-FileHash -LiteralPath $file.FullName).Hash){throw 'Delivery copy hash mismatch.'}
}
# Remove only unchanged files tracked in the previous manifest and absent from this kit.
foreach($relative in $previous.Keys){
    $target=[IO.Path]::GetFullPath((Join-Path $destinationPath $relative))
    if(-not $target.StartsWith($destinationPath.TrimEnd('\')+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Previous manifest path escapes delivery folder.'}
    if(-not(Test-Path -LiteralPath (Join-Path $stage $relative)) -and (Test-Path -LiteralPath $target) -and (Get-FileHash -LiteralPath $target).Hash -eq $previous[$relative]){Remove-Item -LiteralPath $target -Force}
}
Write-Host ("Delivered {0} verified files; version {1}. No release was published." -f $all.Count,$version)
Write-Host $destinationPath

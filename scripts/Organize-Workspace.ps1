param()
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$artifacts = Join-Path $projectRoot 'artifacts'
if (-not (Test-Path -LiteralPath (Join-Path $projectRoot 'Directory.Build.props'))) { throw '프로젝트 루트에서만 정리할 수 있습니다.' }

# Keep build/test paths intact. Move only individual historical files, never directories or live data.
$moved = @()
if (Test-Path -LiteralPath $artifacts) {
    $logDirectory = Join-Path $artifacts 'logs'
    $oldDirectory = Join-Path $artifacts 'previous-packages'
    $oldNames = @('yymessenger-0.1.0-beta.1-win-x64.zip', 'yymessenger-0.1.0-beta.2-win-x64.zip', 'yymessenger-1.0.0-server-win-x64.zip')
    $files = Get-ChildItem -LiteralPath $artifacts -File | Where-Object { $_.Extension -eq '.log' -or $_.Name -in $oldNames }
    foreach ($file in $files) {
        $destinationDirectory = if ($file.Extension -eq '.log') { $logDirectory } else { $oldDirectory }
        New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
        $destination = [IO.Path]::GetFullPath((Join-Path $destinationDirectory $file.Name))
        if (-not $destination.StartsWith($artifacts + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw '정리 대상이 artifacts 밖에 있습니다.' }
        if (Test-Path -LiteralPath $destination) { $destination = Join-Path $destinationDirectory ($file.BaseName + '-' + [guid]::NewGuid().ToString('N') + $file.Extension) }
        Move-Item -LiteralPath $file.FullName -Destination $destination
        $moved += [pscustomobject]@{ from=$file.FullName; to=$destination; sha256=(Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash }
    }
    if ($moved.Count) {
        $record = Join-Path $artifacts ('workspace-organization-' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '.json')
        ConvertTo-Json -InputObject @($moved) -Depth 3 | Set-Content -LiteralPath $record -Encoding UTF8
    }
}

[xml]$properties = Get-Content -LiteralPath (Join-Path $projectRoot 'Directory.Build.props') -Raw
$version = [string]$properties.Project.PropertyGroup.Version
$links = @{
    '01_설치파일\01_PC교사용_설치마법사.lnk' = "artifacts\yymessenger-$version-setup-win-x64.exe"
    '01_설치파일\02_안드로이드_교사용_시험앱.lnk' = 'artifacts\mobile\kr.school.yeoyang.teacher-Signed.apk'
    '01_설치파일\03_안드로이드_학생학부모용_시험앱.lnk' = 'artifacts\mobile\kr.school.yeoyang.family-Signed.apk'
    '01_설치파일\04_서버컴퓨터용_두서버_압축파일.lnk' = "artifacts\yymessenger-$version-servers-win-x64.zip"
    '01_설치파일\05_체크섬_원본폴더.lnk' = 'artifacts'
    '02_설명서\02_플랫폼별_상세안내.lnk' = 'docs\platform-deployment-guide.md'
    '02_설명서\03_전체문서_목차.lnk' = 'docs\README.md'
    '02_설명서\04_최신보안점검.lnk' = 'docs\security-audit-2026-10-20.md'
    '02_설명서\05_아이폰_제작안내.lnk' = 'docs\ios-operations.md'
    '02_설명서\06_검증로그_보관폴더.lnk' = 'artifacts\logs'
    '02_설명서\07_오류메일_설정안내.lnk' = 'docs\feedback-email.md'
    '02_설명서\08_기기별_설치와_테스트순서.lnk' = 'docs\installation-and-test-order.md'
    '02_설명서\09_선택과목_신청안내.lnk' = 'docs\elective-registration.md'
    '01_설치파일\06_Mac전달용_iOS_최신소스.lnk' = "artifacts\yymessenger-$version-ios-build-source.zip"
}
$shell = New-Object -ComObject WScript.Shell
$created = 0
$missing = @()
foreach ($entry in $links.GetEnumerator()) {
    $target = Join-Path $projectRoot $entry.Value
    if (-not (Test-Path -LiteralPath $target)) { $missing += $entry.Value; continue }
    $linkPath = Join-Path $projectRoot $entry.Key
    New-Item -ItemType Directory -Path (Split-Path $linkPath -Parent) -Force | Out-Null
    $shortcut = $shell.CreateShortcut($linkPath)
    $shortcut.TargetPath = $target
    $shortcut.WorkingDirectory = Split-Path $target -Parent
    $shortcut.Description = '현재 프로젝트 원본 바로가기. 다른 컴퓨터에 전달할 때는 대상 원본 파일을 복사하세요.'
    $shortcut.Save()
    $created++
}
$guideDirectory = Join-Path $projectRoot '02_설명서'
New-Item -ItemType Directory -Path $guideDirectory -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\beginner-setup-guide.md') -Destination (Join-Path $guideDirectory '01_처음설치부터_Cloudflare까지.txt') -Force
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\installation-and-test-order.md') -Destination (Join-Path $guideDirectory '00_기기별_설치와_테스트순서.txt') -Force
Write-Host "완료: 바로가기 $created 개, 로그/이전 패키지 이동 $($moved.Count) 개. 삭제한 파일 없음."
if ($missing.Count) { Write-Host ('아직 생성되지 않은 대상: ' + ($missing -join ', ')) }

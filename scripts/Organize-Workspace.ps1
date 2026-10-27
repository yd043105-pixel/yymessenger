param([switch]$SkipDeliveryRefresh)
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$artifacts=Join-Path $projectRoot 'artifacts'
if(-not(Test-Path -LiteralPath (Join-Path $projectRoot 'Directory.Build.props'))){throw '프로젝트 루트에서만 정리할 수 있습니다.'}
$moved=@()
function Move-WorkspaceFile([string]$From,[string]$To){
    $source=[IO.Path]::GetFullPath((Join-Path $projectRoot $From))
    $target=[IO.Path]::GetFullPath((Join-Path $projectRoot $To))
    foreach($path in @($source,$target)){
        if(-not $path.StartsWith($projectRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw '정리 경로가 프로젝트 밖에 있습니다.'}
    }
    if(-not(Test-Path -LiteralPath $source -PathType Leaf)){return}
    if((Get-Item -LiteralPath $source -Force).Attributes -band [IO.FileAttributes]::ReparsePoint){throw '링크 파일은 이동하지 않습니다.'}
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force|Out-Null
    if(Test-Path -LiteralPath $target){$target=Join-Path (Split-Path $target -Parent) (([IO.Path]::GetFileNameWithoutExtension($target))+'-'+[guid]::NewGuid().ToString('N')+[IO.Path]::GetExtension($target))}
    $hash=(Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
    Move-Item -LiteralPath $source -Destination $target
    if((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ne $hash){throw '정리 중 파일 내용이 달라졌습니다.'}
    $script:moved += [pscustomobject]@{from=$source;to=$target;sha256=$hash}
}
if(Test-Path -LiteralPath $artifacts){
    foreach($file in Get-ChildItem -LiteralPath $artifacts -File){
        $destination=$null
        if($file.Extension -eq '.log'){$destination='artifacts\logs\'+$file.Name}
        elseif($file.Name -in @('yymessenger-0.1.0-beta.1-win-x64.zip','yymessenger-0.1.0-beta.2-win-x64.zip','yymessenger-1.0.0-server-win-x64.zip')){$destination='artifacts\previous-packages\'+$file.Name}
        elseif($file.Name -like 'workspace-organization-*.json' -or $file.Name -in @('github-pr-body.txt','counseling-pr-body.md','security-final-pr-before.txt')){$destination='artifacts\workspace-history\'+$file.Name}
        if($destination){Move-WorkspaceFile ('artifacts\'+$file.Name) $destination}
    }
}
foreach($name in @('시작-교사용.cmd','시작-로컬체험.cmd')){Move-WorkspaceFile $name ("installer\launchers\$name")}
$pdfTemporary=Join-Path $projectRoot 'tmp\pdfs'
if(Test-Path -LiteralPath $pdfTemporary){
    foreach($file in Get-ChildItem -LiteralPath $pdfTemporary -File -Filter '*.png'){
        Move-WorkspaceFile ('tmp\pdfs\'+$file.Name) ('artifacts\pdf-review\'+$file.Name)
    }
}
[xml]$properties=Get-Content -LiteralPath (Join-Path $projectRoot 'Directory.Build.props') -Raw
$version=[string]$properties.Project.PropertyGroup.Version
if(-not $SkipDeliveryRefresh -and (Test-Path -LiteralPath (Join-Path $artifacts "yymessenger-$version-setup-win-x64.exe"))){
    & (Join-Path $PSScriptRoot 'Prepare-TestDelivery.ps1')
}
foreach($name in @('01_PC교사용_설치마법사.lnk','02_안드로이드_교사용_시험앱.lnk','03_안드로이드_학생학부모용_시험앱.lnk','04_서버컴퓨터용_두서버_압축파일.lnk','05_체크섬_원본폴더.lnk','06_Mac전달용_iOS_최신소스.lnk')){
    Move-WorkspaceFile ("01_설치파일\$name") ("artifacts\workspace-history\$name")
}
$links=@{
    '02_설명서\01_설치와_접속\02_플랫폼별_상세안내.lnk'='docs\platform-deployment-guide.md'
    '02_설명서\99_전체문서_목차.lnk'='docs\README.md'
    '02_설명서\04_운영과_보안\04_최신보안점검.lnk'='docs\final-review-2026-10-24.md'
    '02_설명서\01_설치와_접속\05_아이폰_제작안내.lnk'='docs\ios-operations.md'
    '02_설명서\04_운영과_보안\06_검증로그_보관폴더.lnk'='artifacts\logs'
    '02_설명서\04_운영과_보안\07_오류메일_설정안내.lnk'='docs\feedback-email.md'
    '02_설명서\01_설치와_접속\08_기기별_설치와_테스트순서.lnk'='docs\installation-and-test-order.md'
    '02_설명서\02_사용기능\09_선택과목_신청안내.lnk'='docs\elective-registration.md'
    '02_설명서\04_운영과_보안\10_보안체크리스트_점검결과.lnk'='docs\security-checklist-2026-10-24.md'
    '02_설명서\03_등록과_개인정보\11_학운위_에듀집_보안근거.lnk'='docs\committee-security-evidence-2026-10-24.md'
    '02_설명서\03_등록과_개인정보\12_에듀집등록과_학교별설정.lnk'='docs\edzip-registration-guide.md'
    '02_설명서\03_등록과_개인정보\13_개인정보_권리요청.lnk'='docs\privacy-rights-operations.md'
    '02_설명서\02_사용기능\01_교직원과_특별실_관리.lnk'='docs\school-day-and-administration.md'
    '02_설명서\02_사용기능\02_시간표_업로드.lnk'='docs\timetable-operations.md'
    '02_설명서\02_사용기능\03_가정통신문_이미지.lnk'='docs\home-letter-images.md'
}
foreach($entry in $links.GetEnumerator()){
    $old='02_설명서\'+(Split-Path $entry.Key -Leaf)
    if($old -ne $entry.Key){Move-WorkspaceFile $old $entry.Key}
}
Move-WorkspaceFile '02_설명서\03_전체문서_목차.lnk' '02_설명서\99_전체문서_목차.lnk'
Move-WorkspaceFile '02_설명서\00_기기별_설치와_테스트순서.txt' '02_설명서\01_설치와_접속\00_기기별_설치와_테스트순서.txt'
Move-WorkspaceFile '02_설명서\01_처음설치부터_Cloudflare까지.txt' '02_설명서\01_설치와_접속\01_처음설치부터_Cloudflare까지.txt'
foreach($name in @('07_Mac시뮬레이터_교사용.lnk','08_Mac시뮬레이터_학생학부모용.lnk')){
    Move-WorkspaceFile ("01_설치파일\$name") ("01_설치파일\04_iOS\$name")
}
$shell=New-Object -ComObject WScript.Shell
$created=0;$missing=@()
foreach($entry in $links.GetEnumerator()){
    $target=Join-Path $projectRoot $entry.Value
    if(-not(Test-Path -LiteralPath $target)){$missing+=$entry.Value;continue}
    $linkPath=Join-Path $projectRoot $entry.Key
    New-Item -ItemType Directory -Path (Split-Path $linkPath -Parent) -Force|Out-Null
    $shortcut=$shell.CreateShortcut($linkPath)
    if($shortcut.TargetPath -eq $target -and $shortcut.WorkingDirectory -eq (Split-Path $target -Parent)){$created++;continue}
    $shortcut.TargetPath=$target
    $shortcut.WorkingDirectory=Split-Path $target -Parent
    $shortcut.Description='현재 프로젝트 원본 바로가기. 다른 컴퓨터로 전달할 때는 원본 파일을 복사하세요.'
    $shortcut.Save();$created++
}
$copies=@{
    'docs\operation-order.md'='02_설명서\01_설치와_접속\00_기기별_설치와_테스트순서.txt'
    'docs\beginner-setup-guide.md'='02_설명서\01_설치와_접속\01_처음설치부터_Cloudflare까지.txt'
    'output\pdf\04_설치와_기능테스트_안내.pdf'='02_설명서\00_설치와_기능테스트_안내.pdf'
    'output\pdf\01_필수기준_작성전_검토표.pdf'='02_설명서\03_등록과_개인정보\01_필수기준_작성전_검토표.pdf'
    'output\pdf\02_개인정보처리방침_검토안.pdf'='02_설명서\03_등록과_개인정보\02_개인정보처리방침_검토안.pdf'
    'output\pdf\03_등록방법과_학교별설정.pdf'='02_설명서\03_등록과_개인정보\03_등록방법과_학교별설정.pdf'
    'artifacts\client-preview.png'='04_화면미리보기\01_메신저_조직도.png'
    'artifacts\mailbox-preview.png'='04_화면미리보기\02_메시지함.png'
    'artifacts\compose-preview.png'='04_화면미리보기\03_메시지_작성.png'
    'artifacts\tasks-preview.png'='04_화면미리보기\04_할일_목록.png'
    'artifacts\submissions-preview.png'='04_화면미리보기\05_파일_제출현황.png'
    'artifacts\ui-review-20261027\detail-design.png'='04_화면미리보기\06_앱과_PC_세부화면_디자인시안.png'
    'artifacts\server-manager\server-manager-running.png'='04_화면미리보기\07_서버_가동관리.png'
    'artifacts\server-manager\server-operations.png'='04_화면미리보기\08_관리자_서버운영.png'
}
foreach($entry in $copies.GetEnumerator()){
    $source=Join-Path $projectRoot $entry.Key;$target=Join-Path $projectRoot $entry.Value
    if(-not(Test-Path -LiteralPath $source -PathType Leaf)){continue}
    New-Item -ItemType Directory -Path (Split-Path $target -Parent) -Force|Out-Null
    Copy-Item -LiteralPath $source -Destination $target -Force
    if((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $target).Hash){throw '설명서 또는 화면 사본 검증 실패'}
}
if($moved.Count){
    $records=Join-Path $artifacts 'workspace-history';New-Item -ItemType Directory -Path $records -Force|Out-Null
    $record=Join-Path $records ('workspace-organization-'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff')+'.json')
    ConvertTo-Json -InputObject @($moved) -Depth 3|Set-Content -LiteralPath $record -Encoding UTF8
}
Write-Host "완료: 설명서 바로가기 $created 개, 파일 이동 $($moved.Count) 개. 설치파일은 기기별 실제 파일로 전달하세요."
if($missing.Count){Write-Host ('아직 생성되지 않은 대상: '+($missing -join ', '))}

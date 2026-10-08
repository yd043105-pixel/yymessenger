param(
    [switch]$SkipTests,
    [switch]$HeadlessTests,
    [ValidatePattern('^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$')][string]$Version
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$releaseNotes = Get-Content -LiteralPath (Join-Path $projectRoot 'src\SchoolMessenger.Shared\updates.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if (-not $Version) { $Version = $releaseNotes.version }
if ($Version -ne $releaseNotes.version) { throw 'Release version must match the bundled updates.json version.' }
$buildProperties = [xml](Get-Content -LiteralPath (Join-Path $projectRoot 'Directory.Build.props') -Raw)
if ($Version -ne $buildProperties.Project.PropertyGroup.Version) { throw 'Release version must match Directory.Build.props. Keep 1.0.0 until the first release is authorized.' }
$localDotnet = Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
if (Test-Path -LiteralPath $localDotnet) { $dotnetExecutable = $localDotnet }
else { $dotnetExecutable = (Get-Command dotnet -ErrorAction Stop).Source }
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_ROOT = Split-Path $dotnetExecutable -Parent
Push-Location $projectRoot
try {
    foreach ($project in @('Server','Desktop','AnnouncementServer')) {
        & $dotnetExecutable restore "src\SchoolMessenger.$project" --locked-mode --nologo -v quiet
        if ($LASTEXITCODE -ne 0) { throw "$project 패키지 잠금 검증 실패" }
        & $dotnetExecutable build "src\SchoolMessenger.$project" "-p:Version=$Version" --no-restore --nologo -v quiet
        if ($LASTEXITCODE -ne 0) { throw "$project 빌드 실패" }
    }
    if (-not $SkipTests) {
        node tests\update-notice.mjs
        if ($LASTEXITCODE -ne 0) { throw 'Update notice verification failed' }
        node tests\remote-channel.mjs
        if ($LASTEXITCODE -ne 0) { throw 'Remote test channel verification failed' }
        & powershell -NoProfile -ExecutionPolicy Bypass -File tests\remote-firewall.ps1
        if ($LASTEXITCODE -ne 0) { throw '원격 지원 방화벽 스크립트 검증 실패' }
        if ($HeadlessTests) { node tests\smoke.mjs --features }
        else { node tests\smoke.mjs --ui }
        if ($LASTEXITCODE -ne 0) { throw '송수신 검증 실패' }
        node tests\announcements.mjs
        if ($LASTEXITCODE -ne 0) { throw 'External announcement verification failed' }
        node tests\enrollment.mjs
        if ($LASTEXITCODE -ne 0) { throw 'Student and parent enrollment verification failed' }
        node tests\announcement-bridge.mjs
        if ($LASTEXITCODE -ne 0) { throw 'Announcement bridge verification failed' }
        node tests\data-boundaries.mjs
        if ($LASTEXITCODE -ne 0) { throw 'Server data isolation verification failed' }
        & $dotnetExecutable build tests\TimetableChecks -p:RestoreLockedMode=true --nologo -v quiet
        if ($LASTEXITCODE -ne 0) { throw 'Timetable checks build failed' }
        node tests\timetable.mjs
        if ($LASTEXITCODE -ne 0) { throw 'Timetable import and replica verification failed' }
    }
    $packageDirectory = Join-Path $projectRoot 'artifacts\여양고-교무메신저'
    New-Item -ItemType Directory -Force -Path $packageDirectory | Out-Null
    if (Test-Path -LiteralPath (Join-Path $packageDirectory 'data')) { throw '배포 폴더에 운영 데이터가 있습니다. 데이터를 별도 경로로 옮긴 뒤 빌드하세요.' }
    & $dotnetExecutable publish src\SchoolMessenger.Server -c Release -r win-x64 --self-contained true "-p:Version=$Version" -p:RestoreLockedMode=true -o (Join-Path $packageDirectory 'server') --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw '서버 배포 파일 생성 실패' }
    & $dotnetExecutable publish src\SchoolMessenger.Desktop -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true "-p:Version=$Version" -p:RestoreLockedMode=true -o (Join-Path $packageDirectory 'client') --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw '교사용 배포 파일 생성 실패' }
    & $dotnetExecutable publish src\SchoolMessenger.AnnouncementServer -c Release -r win-x64 --self-contained true "-p:Version=$Version" -p:RestoreLockedMode=true -o (Join-Path $packageDirectory 'announcement-server') --nologo -v quiet
    if ($LASTEXITCODE -ne 0) { throw 'External announcement package failed' }
    New-Item -ItemType Directory -Force -Path (Join-Path $packageDirectory 'scripts') | Out-Null
    Copy-Item -LiteralPath 'scripts\Start-Server.ps1','scripts\Start-AnnouncementServer.ps1','scripts\Configure-RemoteFirewall.ps1' -Destination (Join-Path $packageDirectory 'scripts')
    Copy-Item -LiteralPath 'README.md','시작-로컬체험.cmd','시작-교사용.cmd' -Destination $packageDirectory
    $packageDocs = Join-Path $packageDirectory 'docs'
    New-Item -ItemType Directory -Force -Path $packageDocs | Out-Null
    Copy-Item -Path 'docs\security-audit-*.md' -Destination $packageDocs
    Copy-Item -LiteralPath 'docs\web-tasks-design.md' -Destination $packageDocs
    Copy-Item -LiteralPath 'docs\mobile-announcements-design.md' -Destination $packageDocs
    Copy-Item -LiteralPath 'docs\mobile-operations.md' -Destination $packageDocs
    Copy-Item -LiteralPath 'docs\family-registration.md' -Destination $packageDocs
    Copy-Item -LiteralPath 'docs\ios-operations.md','docs\ios-verification.md' -Destination $packageDocs
    Copy-Item -LiteralPath 'docs\releases.md' -Destination $packageDocs
    Copy-Item -LiteralPath 'docs\timetable-import-design.md','docs\timetable-operations.md' -Destination $packageDocs
    Set-Content -LiteralPath (Join-Path $packageDirectory 'VERSION.txt') -Value $Version -Encoding ASCII
    $privateFiles = Get-ChildItem -LiteralPath $packageDirectory -Recurse -File | Where-Object { $_.Name -match '(?i)(\.db($|-)|\.sqlite3?($|-)|\.pfx$|\.pem$|\.key$|^login\.dat$|^\.env|^appsettings\.Production\.json$)' }
    if ($privateFiles) { throw '배포 폴더에 데이터 또는 비밀 파일이 있습니다. 압축을 중단합니다.' }
    Compress-Archive -LiteralPath $packageDirectory -DestinationPath 'artifacts\여양고-교무메신저.zip' -Force
    $releaseZip = Join-Path $projectRoot "artifacts\yymessenger-$Version-win-x64.zip"
    Copy-Item -LiteralPath 'artifacts\여양고-교무메신저.zip' -Destination $releaseZip -Force
    $hash = (Get-FileHash -LiteralPath $releaseZip -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -LiteralPath 'artifacts\SHA256SUMS.txt' -Value "$hash  yymessenger-$Version-win-x64.zip" -Encoding ASCII
    Write-Host '완료: artifacts\여양고-교무메신저.zip'
} finally { Pop-Location }

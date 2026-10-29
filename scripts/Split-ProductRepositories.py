"""Export three independent, curated product repositories; never copy Git history or user data."""
import argparse
import hashlib
import json
import shutil
import subprocess
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
parser = argparse.ArgumentParser()
parser.add_argument('destination', type=Path)
args = parser.parse_args()
destination = args.destination.resolve()
source_commit = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
tracked = subprocess.check_output(['git', 'ls-files', '-z'], cwd=ROOT).decode().split('\0')
common_root = ['Directory.Build.props', 'global.json', 'LICENSE', 'THIRD-PARTY-NOTICES.md', '.gitattributes']
common_config = ['config/school-directory.json', 'config/registration-policy.draft.json']
shared = [p for p in tracked if p.startswith('src/SchoolMessenger.Shared/') and '/Backend/' not in p]
contracts = [p for p in tracked if p.startswith('src/SchoolMessenger.Contracts/')]
desktop = [p for p in tracked if p.startswith('src/SchoolMessenger.Desktop/')]
server = [p for p in tracked if p.startswith(('src/SchoolMessenger.Server/', 'src/SchoolMessenger.AnnouncementServer/', 'src/SchoolMessenger.Shared/Backend/'))]
public = [p.relative_to(ROOT).as_posix() for p in (ROOT/'docs/public').glob('*') if p.is_file()]
policy = [p for p in tracked if p.startswith('docs/policies/')]
checks = [p for p in tracked if p.startswith(('tests/SchoolDirectoryChecks/', 'tests/DiscoveryChecks/', 'tests/IdleChecks/'))]
base_scripts = ['scripts/Build-Installer.ps1', 'scripts/Install-InnoCompiler.ps1']
server_scripts = ['Build.ps1', 'Build-ServerInstaller.ps1', 'Test-DeploymentReviewPackage.ps1', 'Start-Server.ps1', 'Start-AnnouncementServer.ps1', 'Server-Operations.ps1', 'Configure-ServerOperations.ps1', 'Configure-RemoteFirewall.ps1', 'Inspect-ServerComputer.ps1', 'Stage-ServerUpdate.ps1', 'Update-SchoolDirectory.ps1', 'Review-SchoolRegistration.ps1']
server_tests = ['registration-policy.mjs', 'deployment-review.mjs', 'deployment-review-fixture.mjs', 'backend-hardening.mjs', 'announcement-bridge.mjs', 'school-directory-servers.mjs', 'bridge-client.mjs', 'registration-fixture.mjs', 'deployment-review-operations.ps1', 'server-manager.mjs', 'server-installer.ps1', 'server-operations.mjs']
mobile_shared = [p for p in tracked if p.startswith('src/SchoolMessenger.Mobile/') and (p.count('/') == 2 and p.endswith(('.cs', 'App.xaml')) or '/Resources/' in p or p.endswith('Platforms/iOS/Resources/PrivacyInfo.xcprivacy'))]
mac = [p for p in tracked if p.startswith('src/SchoolMessenger.Mac/')]
mac_tests = ['tests/MacRuntimeChecks.cs', 'tests/mac-runtime.mjs', 'tests/mac-bundle.py', 'tests/portal-preview.mjs', 'tests/bridge-client.mjs', 'tests/registration-fixture.mjs']
checkout = 'actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1'
dotnet = 'actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68'
node = 'actions/setup-node@820762786026740c76f36085b0efc47a31fe5020'
upload = 'actions/upload-artifact@cf430e030ddbb5b0abf93d22962f4752f3646cd9'

def write(folder, relative, content):
    target = folder/relative
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(content, encoding='utf-8', newline='\n')

def workflow_windows(kind):
    header = f'''name: {kind} verification
on:
  push:
    branches: [main]
  pull_request:
    branches: [main]
  workflow_dispatch:
permissions:
  contents: read
jobs:
  verify:
    runs-on: windows-latest
    timeout-minutes: 35
    env:
      DOTNET_PROCESSOR_COUNT: '2'
    steps:
      - uses: {checkout}
      - uses: {dotnet}
        with:
          dotnet-version: 10.0.402
      - uses: {node}
        with:
          node-version: 24
'''
    if kind == 'Server':
        body = '''      - name: Build isolated server and administrator sources
        shell: pwsh
        run: |
          foreach ($project in @('Server','AnnouncementServer','Desktop')) {
            dotnet build "src/SchoolMessenger.$project" -p:RestoreLockedMode=true --nologo -v quiet
            if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
          }
          dotnet run --project tests/SchoolDirectoryChecks -p:RestoreLockedMode=true
          if ($LASTEXITCODE -ne 0) { throw 'School directory verification failed' }
      - name: Verify school boundaries, review records, accounts and bridge
        shell: pwsh
        run: |
          foreach ($test in @('registration-policy','deployment-review','backend-hardening','announcement-bridge','school-directory-servers','server-manager')) {
            node "tests/$test.mjs"
            if ($LASTEXITCODE -ne 0) { throw "Verification failed: $test" }
          }
      - name: Build server administrator installer
        shell: pwsh
        run: |
          ./scripts/Build.ps1 -SkipTests
          ./scripts/Build-ServerInstaller.ps1
          ./tests/server-installer.ps1 -ShortcutOnly
          Copy-Item artifacts/yymessenger-1.0.0-server-setup-win-x64.exe artifacts/gyomutalk-server-1.0.0-trial.exe
          ((Get-FileHash 'artifacts/gyomutalk-server-1.0.0-trial.exe' -Algorithm SHA256).Hash.ToLowerInvariant() + '  gyomutalk-server-1.0.0-trial.exe') | Set-Content artifacts/SHA256SUMS.txt -Encoding ascii
'''
        artifact = 'artifacts/gyomutalk-server-1.0.0-trial.exe\n            artifacts/SHA256SUMS.txt\n            artifacts/server-launcher-checks.json'
    else:
        body = '''      - name: Build Windows messenger and verify client safeguards
        shell: pwsh
        run: |
          dotnet build src/SchoolMessenger.Desktop -p:RestoreLockedMode=true --nologo -v quiet
          if ($LASTEXITCODE -ne 0) { throw 'Windows build failed' }
          foreach ($test in @('SchoolDirectoryChecks','DiscoveryChecks','IdleChecks')) {
            dotnet run --project "tests/$test" -p:RestoreLockedMode=true
            if ($LASTEXITCODE -ne 0) { throw "Verification failed: $test" }
          }
      - name: Build and test Windows user installer
        shell: pwsh
        run: |
          ./scripts/Build-Installer.ps1
          ./tests/installer.ps1 -ClientDirectory (Join-Path $PWD 'artifacts/installer-client')
          Copy-Item artifacts/yymessenger-1.0.0-setup-win-x64.exe artifacts/gyomutalk-windows-1.0.0-trial.exe
          ((Get-FileHash 'artifacts/gyomutalk-windows-1.0.0-trial.exe' -Algorithm SHA256).Hash.ToLowerInvariant() + '  gyomutalk-windows-1.0.0-trial.exe') | Set-Content artifacts/SHA256SUMS.txt -Encoding ascii
'''
        artifact = 'artifacts/gyomutalk-windows-1.0.0-trial.exe\n            artifacts/SHA256SUMS.txt'
    return header + body + f'''      - uses: {upload}
        with:
          name: gyomutalk-{kind.lower()}-1.0.0-trial
          path: |
            {artifact}
          retention-days: 7
          if-no-files-found: error
'''

def workflow_mac():
    return f'''name: Native Mac verification
on:
  push:
    branches: [main]
  pull_request:
    branches: [main]
  workflow_dispatch:
permissions:
  contents: read
concurrency:
  group: mac-${{{{ github.ref }}}}
  cancel-in-progress: true
jobs:
  mac:
    runs-on: macos-26
    timeout-minutes: 45
    env:
      YY_TEST_SERVICE_ROOT: ${{{{ github.workspace }}}}/.test-services
    steps:
      - uses: {checkout}
      - name: Checkout pinned server sources for synthetic tests only
        uses: {checkout}
        with:
          repository: yd043105-pixel/gyomutalk-server
          ref: SERVER_COMMIT
          path: .test-services
          persist-credentials: false
      - uses: {dotnet}
        with:
          dotnet-version: 10.0.402
      - uses: {node}
        with:
          node-version: 24
      - name: Install pinned Apple workload
        run: |
          sudo xcode-select -s /Applications/Xcode_26.6.app/Contents/Developer
          dotnet workload install maui-maccatalyst --version 10.0.401
          dotnet restore src/SchoolMessenger.Mac --locked-mode
      - name: Build native app and isolated test services
        run: |
          runtime=maccatalyst-x64
          [[ "$(uname -m)" != arm64 ]] || runtime=maccatalyst-arm64
          dotnet build src/SchoolMessenger.Mac -c Debug -r "$runtime" --no-restore -p:MacVerification=true -p:CodesignKey=- -p:CodesignRequireProvisioningProfile=false
          dotnet build .test-services/src/SchoolMessenger.Server -p:RestoreLockedMode=true
          dotnet build .test-services/src/SchoolMessenger.AnnouncementServer -p:RestoreLockedMode=true
      - name: Verify actual native application
        run: node tests/mac-runtime.mjs
      - name: Publish both Mac architectures
        run: |
          bash scripts/Build-Mac.sh maccatalyst-arm64
          bash scripts/Build-Mac.sh maccatalyst-x64
      - uses: {upload}
        if: always()
        with:
          name: gyomutalk-mac-1.0.0-trial
          path: artifacts/mac/
          retention-days: 7
          if-no-files-found: error
'''

for kind in ('server', 'windows', 'mac'):
    folder = destination/('gyomutalk-'+kind)
    if folder.exists():
        raise SystemExit('Destination already exists; preserve existing work and choose a new export parent.')
    folder.mkdir(parents=True)
    paths = set(common_root+common_config+contracts+shared+public+policy+['docs/repository-split.md'])
    if kind == 'server':
        paths.update(desktop+server+checks+base_scripts)
        paths.update('scripts/'+p for p in server_scripts)
        paths.update('tests/'+p for p in server_tests)
        paths.update(p for p in tracked if p.startswith('installer/'))
        paths.update('config/'+p for p in ['deployment-review.draft.json','server-operations.example.json','cloudflared.example.yml'])
    elif kind == 'windows':
        paths.update(desktop+checks+base_scripts+['installer/SchoolMessenger.iss','tests/installer.ps1'])
    else:
        paths.update(mac+mobile_shared+mac_tests+['scripts/Build-Mac.sh'])
    for relative in sorted(paths):
        source = ROOT/relative
        if not source.is_file() or source.is_symlink():
            raise SystemExit('Missing or linked export source: '+relative)
        target = folder/relative
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
    write(folder, '.gitignore', (ROOT/'.gitignore').read_text(encoding='utf-8-sig')+'\n# School records must not enter public source control.\nprivate/\noperations/\nschool-evidence/\n.test-services/\n')
    label = {'server':'서버 관리자', 'windows':'Windows 교직원 메신저', 'mac':'Mac 교직원 메신저'}[kind]
    command = {'server':'./scripts/Build.ps1 -SkipTests\n./scripts/Build-ServerInstaller.ps1', 'windows':'./scripts/Build-Installer.ps1', 'mac':'dotnet workload install maui-maccatalyst --version 10.0.401\ndotnet restore src/SchoolMessenger.Mac --locked-mode\nbash scripts/Build-Mac.sh maccatalyst-arm64\nbash scripts/Build-Mac.sh maccatalyst-x64'}[kind]
    write(folder, 'README.md', f'''# 교무톡 - {label}

학교가 자체 서버를 운영하는 교무톡의 {label} 제품 저장소입니다. MIT 라이선스입니다.

## 사용과 운영

교직원 PC·교사용 앱은 교내 업무 서버, 학교톡(학생·학부모용 앱)은 학교 소식 서버에 접속합니다. 승인된 학교 검색 목록에서 학교를 선택합니다. 이 저장소는 학교 계정·메시지·학생 명부를 보관하는 장소가 아닙니다.

도입·운영 내부결재 승인 문서는 각 학교에 보관하세요. 필요한 학운위 심의·보안 검토·개인정보 처리 절차를 따로 확인해야 합니다. 학교 검색 등록 승인은 그 절차나 보안 인증을 대신하지 않습니다. 결재 원본·운영 설정·학교 검토 자료는 공개 GitHub에 올리지 마세요.

- [공통 사용 설명서](docs/public/00_먼저읽기.md)
- [관리자 설치·운영](docs/public/01_관리자_설치와운영.md)
- [교직원 사용 안내](docs/public/02_교직원_사용안내.md)
- [학교 내부기안 작성양식](docs/public/학교도입_내부기안_작성양식.txt)
- [분리 저장소와 호환성 관리](docs/repository-split.md)

## 빌드

.NET SDK는 global.json에 고정합니다. Windows 제품은 64비트 Windows가 필요하며, Mac은 macOS·Xcode 26.6이 필요합니다.

```{'bash' if kind=='mac' else 'powershell'}
{command}
```

GitHub Actions의 제품별 검증이 성공하면 Artifacts에서 시험 산출물을 내려받습니다. 현재 모두 1.0.0 시험본이며 정식 Release·자동 공개 배포·자동 업데이트는 활성화하지 않았습니다. Windows 일반 배포용 코드 서명과 Mac Developer ID 서명·공증은 별도 준비가 필요합니다.

서버: [gyomutalk-server](https://github.com/yd043105-pixel/gyomutalk-server) / Windows: [gyomutalk-windows](https://github.com/yd043105-pixel/gyomutalk-windows) / Mac: [gyomutalk-mac](https://github.com/yd043105-pixel/gyomutalk-mac)

모바일 앱과 기존 이력은 [기존 저장소](https://github.com/yd043105-pixel/yymessenger)에 유지합니다. 앱용 새 저장소는 만들지 않았습니다. 설치·앱·데이터 식별자는 호환성을 위해 유지합니다.
''')
    workflow = workflow_mac() if kind == 'mac' else workflow_windows(kind.title())
    write(folder, '.github/workflows/verify.yml', workflow)
    baseline = {p:hashlib.sha256((folder/p).read_bytes()).hexdigest() for p in sorted(paths) if p.startswith(('src/SchoolMessenger.Contracts/', 'src/SchoolMessenger.Shared/'))}
    write(folder, 'source-snapshot.json', json.dumps({'schema':1,'sourceRepository':'yd043105-pixel/yymessenger','sourceCommit':source_commit,'date':'2026-10-29','sharedSourceSha256':baseline},ensure_ascii=False,indent=2)+'\n')
    write(folder, 'protocol-baseline.json', json.dumps({'schema':1,'productVersion':'1.0.0','schoolDirectorySchema':1,'schoolDirectoryProduct':'SchoolMessenger.School.1','sourceCommit':source_commit,'apiChangedBySplit':False,'note':'API/DTO/session changes require cross-product compatibility verification.'},indent=2)+'\n')
    print(kind+': '+str(len(paths))+' source files exported')

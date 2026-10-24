# 서버·Windows PC·Android·iPhone 설치 및 시운행 순서

2026-10-24: 먼저 [최신 시험 설치 순서](final-test-runbook.md)를 읽는다. `01_설치파일`의 기기별 폴더에는 최신 원본과 해시를 대조한 실제 파일이 있으며, 이 문서는 설정 항목을 자세히 확인할 때 참고한다.

2026-10-23 추가: 이름 아이디·초기 비밀번호 변경, 서버 자동검색과 관리자 화면의 서버 자동 구동은 [연결과 자동 구동 안내](server-connection-and-autostart.md)를 먼저 확인한다. 기존 직접 주소 입력도 계속 사용할 수 있다.

2026-10-20 · 현재 버전 1.0.0 · 정식 배포 전

이 문서는 실제로 작업할 순서입니다. 학교 서버 설치·외부 공개·스토어 업로드가 이미 완료됐다는 뜻은 아닙니다. 처음에는 가상의 교사·학생·보호자 계정으로 시험하세요.

## 1. 전체 우선순위

| 순서 | 어디에서 | 할 일 | 다음 단계로 넘어가는 기준 |
| --- | --- | --- | --- |
| 1 | 실제 서버 PC | 사양 확인, 기존 백신·포트 확인, 서버 역할 격리·UPS·백업 준비 | 담당자가 운영 구성을 확정함 |
| 2 | 실제 서버 PC | 파일 검사 엔진과 실행 계정 확인 | 무해한 파일의 운영용 검사 성공. 현재 개발 PC에서 실패한 상태를 그대로 가져가지 않음 |
| 3 | 서버 2개 역할 | HTTPS·약관·메일·운영 계정 설정, 학교 소식 서버와 업무 서버 실행 | 두 서버가 정상 시작하고 역할별 접근 제한이 동작함 |
| 4 | 학교 소식 서버 역할 | Cloudflare 연결, 업무 서버의 공지 연결 확인 | 공개 HTTPS 정상, 개인 업무 서버는 외부에 공개되지 않음 |
| 5 | Windows PC 2대 | 관리자와 시험 교사 계정 설정, 메시지·파일·공지·시간표 시험 | 아래 PC 시험표 통과 |
| 6 | Android 휴대폰 | 교사용 앱과 학생·학부모용 앱 설치·실기기 시험 | 교내 업무 및 학교 밖 학교 소식 시험 통과 |
| 7 | Mac 및 iPhone | 최신 iOS 소스 빌드·서명·TestFlight 시험 | 두 앱의 실제 iPhone 시험 통과 |
| 8 | 모든 기기 | 장애·백업 복원·권한 회수·장기 사용 시험 | 문제 수정 후 학교가 시험 배포 또는 정식 배포를 별도로 결정 |

**Mac 확보·Apple 개발자 계정 준비·도메인 준비는 1번부터 병행할 수 있습니다.** iPhone의 실제 서버 연동 시험은 서버가 준비된 뒤에 합니다. Android와 iPhone 시험은 서버·계정 준비 이후 병행할 수 있습니다.

현재 바로 할 수 있는 일은 **서버 ZIP 복사, 실제 서버 사양 확인, 담당자에게 파일 검사·망 분리·백업 확인 요청**입니다. 서버 PC에 모두 설치하기 전에 Windows 에디션과 메모리부터 확인하세요. 앞서 점검한 RAM 약 16GB의 사용자 PC와 사진으로 받은 서버 PC는 다른 컴퓨터입니다.

## 2. 복사할 파일의 정확한 원본 경로

아래 경로는 현재 개발 PC 기준입니다. 다른 PC로 옮기면 사용자 폴더 이름은 달라질 수 있으므로 원본 파일을 복사하세요. `.lnk` 바로가기를 복사하는 것으로 대체하지 않습니다.

| 용도 | 현재 개발 PC의 원본 파일 | 어디로 전달하나요? |
| --- | --- | --- |
| 서버 프로그램 2종 | `C:\Users\user\Documents\ChatGPT\여양고 온라인 교무실\artifacts\yymessenger-1.0.0-servers-win-x64.zip` | 실제 서버 관리자에게 전달 |
| Windows 설치 마법사 | `C:\Users\user\Documents\ChatGPT\여양고 온라인 교무실\artifacts\yymessenger-1.0.0-setup-win-x64.exe` | 시험할 교사 PC 2대 |
| 교사용 Android | `C:\Users\user\Documents\ChatGPT\여양고 온라인 교무실\artifacts\mobile\kr.school.yeoyang.teacher-Signed.apk` | 시험 교사의 Android 기기 |
| 학생·학부모용 Android | `C:\Users\user\Documents\ChatGPT\여양고 온라인 교무실\artifacts\mobile\kr.school.yeoyang.family-Signed.apk` | 시험 학생·보호자의 Android 기기 |
| Mac에서 빌드할 최신 iOS 소스 | `C:\Users\user\Documents\ChatGPT\여양고 온라인 교무실\artifacts\yymessenger-1.0.0-ios-build-source.zip` | Mac으로 복사 후 압축 해제 |
| Windows·서버 파일 해시 | `C:\Users\user\Documents\ChatGPT\여양고 온라인 교무실\artifacts\SHA256SUMS.txt` | EXE·서버 ZIP과 함께 전달 |
| Android 파일 해시 | `C:\Users\user\Documents\ChatGPT\여양고 온라인 교무실\artifacts\mobile\SHA256SUMS.txt` | APK와 함께 전달 |

`kr.school.yeoyang.messenger-Signed.apk`는 이전 통합 시험 앱입니다. 이번에는 teacher와 family 두 파일을 사용하세요. family는 내부 파일 식별자이고, 제품 이름은 **여양고 학교 소식**입니다.

Android APK는 Debug 시험용입니다. 이름에 Signed가 있어도 정식 배포용 서명과 Google Play 등록이 완료됐다는 뜻은 아닙니다. **iPhone 설치용 IPA는 현재 없습니다.** iOS 소스 ZIP이나 시뮬레이터 ZIP은 iPhone에 설치할 수 없습니다.

GitHub에는 아직 올리지 않은 최신 변경이 있으므로, 이번 Mac 작업은 위 소스 ZIP을 기준으로 하세요. 현재 GitHub 저장소만 복제해서 같은 최신 내용이 된다고 가정하지 않습니다.

## 3. 서버 PC에서 할 일

### 3-1. ZIP을 풀고 실제 사양부터 확인

1. 서버 PC에 서버 ZIP과 체크섬 파일을 복사합니다. 학교가 허용한 USB·파일 전달 수단을 사용합니다.
2. ZIP을 `C:\SchoolMessenger`에 풉니다. 다음 두 파일이 정확히 보여야 합니다.
   - `C:\SchoolMessenger\server\SchoolMessenger.Server.exe`
   - `C:\SchoolMessenger\announcement-server\SchoolMessenger.AnnouncementServer.exe`
3. `C:\SchoolMessenger\여양고-교무메신저\server`처럼 상위 폴더가 한 번 더 생겼다면, 이 문서의 기준 경로와 다릅니다. 담당자가 폴더 위치를 맞춥니다.
4. 일반 PowerShell 창에서 다음을 실행합니다. 이 명령은 사양을 읽고 결과 파일만 만듭니다.

```powershell
Set-Location 'C:\SchoolMessenger'
powershell -NoProfile -File .\scripts\Inspect-ServerComputer.ps1 -OutputPath C:\SchoolMessenger\server-computer-check.json
```

실행 정책으로 차단되면 학교의 허용 방법을 담당자에게 확인하세요. 실행 정책·백신·방화벽을 일괄 해제하지 않습니다. 결과 파일은 **서버 PC의 `C:\SchoolMessenger\server-computer-check.json`**입니다. 이 파일을 서버 설정 담당자에게 전달합니다.

### 3-2. 서버 관리자에게 먼저 확인받을 것

- 학교 회선에서 외부 앱 서버를 운영하는 승인과 실제 운영 담당자.
- Windows 에디션, RAM 규격·용량, VM 지원과 게스트 OS 사용 권한.
- 교내 업무 서버와 외부 학교 소식 서버의 운영체제·계정·저장소·접근망 격리. 같은 PC의 폴더 두 개는 침해 격리가 아닙니다. 한 물리 서버를 쓰려면 담당자가 가상화와 망 정책을 설계합니다.
- UPS와 실제 별도 암호화 백업 장치. 같은 SSD의 다른 폴더나 VM 가상 디스크를 별도 백업 장치로 판단하지 않습니다.
- 학교가 승인한 백신의 파일 검사 연동과 서버 운영 계정 권한. 현재 사용자 PC에서는 Defender가 중지되어 있었고 비관리자 계정의 무해한 파일 검사·운영 업로드가 실패했습니다. 원인을 실제 서버에서 확인해야 합니다.
- TCP 7443과 앱 서버용 포트의 점유. 현재 사용자 PC에서는 KCaseAgent가 8443을 점유합니다. 실제 서버는 별도로 확인합니다. 충돌 시 기존 보안 프로그램을 중지하지 않고 앱 서버 포트와 관련 설정을 함께 조정합니다.

Windows Home인지도 확인되지 않은 서버에 곧바로 VM 설치를 안내할 수는 없습니다. RAM 사진은 DDR4로 확인됐으므로 DDR3 메모리를 임의로 추가하지 마세요. 자세한 판단은 [처음 설치 안내](beginner-setup-guide.md)를 담당자에게 전달합니다.

### 3-3. 주소와 인증서를 정하기

| 이름 | 사용처 | 담당자가 정해 줄 값 |
| --- | --- | --- |
| 업무 서버 주소 | Windows 메신저·교사용 앱 | 인증서 이름과 일치하는 내부 HTTPS 주소, 기본 포트 7443 |
| 학교 소식 서버 주소 | 학생·학부모 앱·업무 서버 연결 | 앱 전용 공개 HTTPS 도메인 |
| 원본 학교 소식 포트 | 같은 VM의 cloudflared와 앱 서버 | 기본 8443, 충돌이면 예를 들어 18443으로 함께 조정 |
| 허용 교직원망 | 업무 서버 방화벽·앱 접근 제한 | 담당자가 확인한 실제 CIDR |

`10.85.88.101`은 기존 서버로 안내받은 내부 IP입니다. 아직 새 서버가 실행 중인 주소라고 확인되지 않았습니다. IP를 안다고 HTTPS 인증서와 일치하는 것도 아닙니다. `localhost`는 입력한 기기 자기 자신이므로 휴대폰에 서버 PC를 뜻하는 주소로 입력하면 안 됩니다.

각 서버의 HTTPS 인증서는 해당 서버의 로컬 컴퓨터 인증서 저장소에 개인키와 함께 설치하고, 운영 계정의 개인키 사용 권한·신뢰 체인·갱신 방법을 담당자가 준비합니다. Windows 설치 파일용 코드서명 인증서와는 별개입니다.

PC뿐 아니라 실제 Android·iPhone 앱이 신뢰하는 체인이어야 합니다. 브라우저에서 자체 서명 인증서 경고를 건너뛴 결과를 앱 접속 성공으로 보지 않습니다. 앱의 인증서 검증을 끄는 방식으로 해결하지 않습니다.

### 3-4. 설정 파일을 역할별로 배치

다음 경로는 **각각 분리된 서버 역할의 Windows 환경 안에서** 사용하는 기준입니다. 두 VM에서는 같은 경로 이름을 사용해도 서로 다른 디스크입니다.

| 파일·폴더 | 업무 서버 역할 | 학교 소식 서버 역할 |
| --- | --- | --- |
| 프로그램 | `C:\SchoolMessenger\server` | `C:\SchoolMessenger\announcement-server` |
| 운영 설정 | `C:\SchoolMessengerOperations\server-operations.local.json` | 동일한 경로 이름, 내용은 해당 역할용 |
| 보호된 비밀값 | `C:\SchoolMessengerOperations\server-operations.secrets.xml` | 해당 Windows 운영 계정으로 각각 생성 |
| 서비스 데이터 | `C:\SchoolMessengerData\office` | `C:\SchoolMessengerData\announcements` |
| 별도 장치 백업 예시 | `E:\YeoyangBackup\office` | `E:\YeoyangBackup\announcements` |
| 확정한 가입 정책 | `C:\SchoolMessengerOperations\registration-policy.local.json` | 학교가 확정한 같은 문서 버전을 각각 배치 |

`E:`는 별도 백업 장치를 연결한 경우의 예시입니다. 실제 장치 경로를 확인한 뒤 입력합니다. DPAPI 비밀 파일은 다른 VM이나 Windows 계정으로 복사해서 사용하는 파일이 아닙니다. 연결 키만 두 역할에 동일한 값으로 숨김 입력하고 나머지 비밀값은 각 역할에서 생성·관리합니다.

담당자는 `C:\SchoolMessenger\config\server-operations.example.json`을 각 역할의 운영 설정 경로로 복사하고 다음 값을 채웁니다.

| 설정 | 업무 서버 | 학교 소식 서버 |
| --- | --- | --- |
| `service` | `office` | `announcements` |
| `executable` | 업무 서버 EXE 전체 경로 | 학교 소식 서버 EXE 전체 경로 |
| `port` | 7443 | 실제 확정한 8443 또는 대체 포트 |
| `tunnelOnly` | false | true |
| `allowedNetworks` | 승인된 CIDR | 빈 문자열 |
| `publicHost` | 공개 학교 소식 도메인, https:// 제외 | 같은 도메인 |
| `resourceProfile` | low-memory | low-memory |
| `attachmentBackup` | true | true |
| `feedbackEmailEnabled`·`automaticBugReportsEnabled` | 오류 메일 시험 시 true | 오류 메일 시험 시 true |

인증서 지문·데이터·백업 경로·발신 메일·수신할 개인 Gmail·SMTP도 실제 값으로 입력합니다. JSON의 Windows 경로는 `C:\\SchoolMessengerData\\office`처럼 역슬래시를 두 번 적습니다. 발신 계정 앱 비밀번호는 JSON에 쓰지 않고 아래 숨김 입력으로 등록합니다.

운영 가입 정책은 학교의 문구 확정 후 등록합니다. `approvedForProduction`은 학교가 실제로 확정했을 때만 true로 변경합니다. 초안 내용은 그대로 두고 플래그만 바꾸지 않습니다. 각 역할의 프로그램 폴더에 담당자가 다음 내용을 기존 설정에 병합합니다. 기존 설정 파일이 있으면 덮어쓰지 않습니다.

업무 역할: `C:\SchoolMessenger\server\appsettings.Production.json`

학교 소식 역할: `C:\SchoolMessenger\announcement-server\appsettings.Production.json`

```json
{
  "Registration": {
    "PolicyFile": "C:\\SchoolMessengerOperations\\registration-policy.local.json"
  }
}
```

파일 검사 경로를 바꾸려면 해당 실행 파일이 현재 명령 인자와 반환값을 지원하는지 확인해야 합니다. 알약·V3 GUI 실행 파일 경로를 대신 넣는 방식은 지원되는 연동이 아닙니다. [백신 호환성 점검](security-audit-2026-10-20.md)을 먼저 확인하세요.

### 3-5. 비밀값·시험 메일·사전 점검

인증서·폴더 권한 등 관리자 작업이 끝나면, 각 역할의 **전용 표준 Windows 운영 계정**으로 로그인해 진행합니다. 메신저 안의 admin 계정과 Windows 운영 계정은 다릅니다.

```powershell
Set-Location 'C:\SchoolMessenger'
.\scripts\Configure-ServerOperations.ps1 -ConfigurationPath C:\SchoolMessengerOperations\server-operations.local.json -InitializeSecrets -Apply
.\scripts\Server-Operations.ps1 -ConfigurationPath C:\SchoolMessengerOperations\server-operations.local.json -Mode TestEmail
```

첫 명령은 관리자 초기 비밀번호·서버 연결 키·메일 앱 비밀번호를 숨김 입력받습니다. **이미 비밀 파일이 있으면 다시 초기화하지 않습니다.** 두 번째는 실제 시험 메일을 보냅니다. 수신함·스팸함에서 도착을 확인한 다음 `alerts.deliveryVerified`를 true로 기록합니다.

망 분리·UPS·백업 암호화도 실제 확인 후 해당 확인값만 true로 기록합니다. 이어 사전 점검합니다.

```powershell
.\scripts\Server-Operations.ps1 -ConfigurationPath C:\SchoolMessengerOperations\server-operations.local.json -Mode Preflight
```

`ready: true`와 빈 issues 목록을 확인합니다. **이 점검에는 파일 검사 엔진의 실제 검사 성공이나 약관 전문 검토가 포함되지 않습니다.** 두 사항은 별도로 확인해야 합니다. false 항목을 임의로 true로 바꿔 시작시키지 않습니다.

### 3-6. 서버를 켜기

학교 소식 서버 역할을 먼저 시작하고, Cloudflare가 준비되면 업무 서버 역할을 시작하는 순서가 연결 상태를 확인하기 쉽습니다. 업무 서버도 미리 켤 수 있지만 학교 소식 연결이 완료되기 전까지 공지·학급 명부 연동은 정상 판정하지 않습니다.

각 역할에서 준비 점검 통과 후 한 번만 실행합니다.

```powershell
.\scripts\Configure-ServerOperations.ps1 -ConfigurationPath C:\SchoolMessengerOperations\server-operations.local.json -RegisterTasks -Apply
.\scripts\Server-Operations.ps1 -ConfigurationPath C:\SchoolMessengerOperations\server-operations.local.json -Mode Start
```

첫 명령은 다음 Windows 부팅 때 실행할 작업과 분당 감시를 등록합니다. 기존 작업이 있으면 중복 등록하지 않습니다. 두 번째는 지금 서버를 실행하고 창을 계속 사용합니다. 이 창을 닫으면 서버도 종료될 수 있습니다. 시험 후 담당자가 재부팅해 예약 작업으로 자동 실행되는지 확인합니다. 이미 실행 중인 서버를 이중으로 실행하지 않습니다. 관리자 계정으로 바꿔 실행하거나 파일 검사를 생략해서 작업 권한 문제를 해결하지 않습니다.

### 3-7. Cloudflare는 학교 소식 서버 역할에만 연결

앱 전용 도메인을 학교 소유 계정으로 준비하고 Cloudflare DNS 활성화·계정 2단계 인증을 완료합니다. 기존 학교 홈페이지 전체의 네임서버를 임의로 바꾸는 작업이 아닙니다.

학교 소식 역할에서 공식 Windows 64비트 `cloudflared.exe`를 `C:\Cloudflared\bin\cloudflared.exe`에 배치한 뒤 진행합니다.

```powershell
Set-Location 'C:\Cloudflared\bin'
.\cloudflared.exe tunnel login
.\cloudflared.exe tunnel create yeoyang-school-news
```

브라우저 로그인은 Cloudflare를 설정하기 위한 관리자 절차입니다. 사용자는 계속 설치 앱을 사용합니다. 출력된 실제 UUID와 자격 증명 JSON 경로를 기록합니다.

- 원본 예시: `C:\SchoolMessenger\config\cloudflared.example.yml`
- 실제 설정: `C:\SchoolMessengerOperations\cloudflared.yml`
- 터널 자격 증명: `C:\SchoolMessengerOperations\실제-터널-UUID.json`

예시 설정을 복사하고 tunnel·credentials-file·hostname 두 곳·originServerName·httpHostHeader를 실제 값으로 바꿉니다. `service`는 같은 역할의 `https://127.0.0.1:실제포트`입니다. 포트가 18443이라면 여기와 서버 운영 설정의 port를 모두 18443으로 맞춥니다. `noTLSVerify: false`, 운영 감시 경로의 404 규칙, 마지막 404 규칙은 유지합니다.

실제 도메인으로 바꾼 뒤 담당자가 아래 명령을 실행합니다. 첫 줄은 실제 DNS를 변경합니다.

```powershell
.\cloudflared.exe tunnel route dns yeoyang-school-news 실제.앱도메인
.\cloudflared.exe tunnel --config C:\SchoolMessengerOperations\cloudflared.yml run yeoyang-school-news
```

터널이 정상 연결된 다음 [Cloudflare Windows 서비스 안내](https://developers.cloudflare.com/tunnel/features/locally-managed-tunnels/as-a-service/windows/)에 따라 담당자가 자동 시작을 등록합니다. 서비스 계정이 설정·자격 증명·인증서 체인을 읽을 수 있어야 합니다. 명령 창에서 연결됐다는 것만으로 Windows 서비스 등록이 끝난 것은 아닙니다. [공식 터널 생성 절차](https://developers.cloudflare.com/tunnel/features/locally-managed-tunnels/create-local-tunnel/)

교내 업무 서버의 7443은 Cloudflare 공개 경로에 넣지 않습니다. 터널이 막히면 담당자가 승인된 아웃바운드 7844 경로를 확인합니다. 원본 HTTPS 검증을 끄거나 학교 방화벽을 해제하지 않습니다.

### 3-8. 서버에서 시험할 것

| 시험 | 방법 | 정상 기준 |
| --- | --- | --- |
| HTTPS | 실제 업무·학교 소식 주소의 `/health`를 조회 | 인증서 경고 없이 status ok. 이 조회 화면은 서버 점검일 뿐 사용자용 웹앱이 아님 |
| 운영 파일 검사 | 시험 계정으로 무해한 TXT·공지 이미지를 업로드 | 정상 파일만 등록됨. 검사 불능을 위험 파일 확정으로 오해하지 않음 |
| 자동 시작 | 승인된 시험 시간에 서버 역할 재부팅 | 각 서버·cloudflared가 자동 시작하고 기기에서 다시 접속됨 |
| 메일 | TestEmail과 시험용 수동 오류 신고 | 실제 수신, 신고 중복·개인정보 과다 포함 없음 |
| 백업 | 아래 Backup 명령 후 실제 별도 장치 확인 | 새 백업 파일·첨부 백업이 생성됨 |
| 복원 | 운영 서버와 분리된 시험 환경에서 담당자가 복원 | 사용자·공지·권한·첨부가 복원되며 검토 대기와 세션 폐기가 적용됨 |
| 격리 | 학교 소식 역할에서 업무 PC·업무 서버로 임의 접근 시도 | 승인된 예외 외 연결 차단. 업무 역할에서 학교 소식 HTTPS로 나가는 연결은 허용 |

백업 명령은 역할별 운영 설정으로 실행합니다.

```powershell
C:\SchoolMessenger\scripts\Server-Operations.ps1 -ConfigurationPath C:\SchoolMessengerOperations\server-operations.local.json -Mode Backup
```

처음부터 운영 DB에 복원하거나 실제 전원을 뽑지 않습니다. 복원·UPS 종료 시험은 담당자가 별도 시험 환경과 중단 시간에 수행합니다. 학교 전체 정전은 같은 서버의 메일 기능만으로 즉시 알릴 수 없으므로 독립 전원·회선의 외부 감시도 준비합니다.

## 4. 설치할 Windows PC에서 할 일

### 4-1. 관리자 시험 PC 한 대를 먼저 설치

1. 위 원본 설치 EXE를 해당 PC의 다운로드 폴더 등으로 복사합니다. 서버 ZIP은 교사 PC에 풀지 않습니다.
2. 설치 EXE를 실행하고 설치 마법사의 다음·설치·완료를 진행합니다. 기본 실행 파일 위치는 `%LOCALAPPDATA%\Programs\YeoyangSchoolMessenger\SchoolMessenger.exe`입니다.
3. 실행이 보안 정책으로 차단되면 정확한 문구·파일 해시를 기록하고 담당자에게 확인합니다. 현재 Windows 파일은 학교 배포자 코드서명이 없으므로 전체 교직원 배포 전에 승인·서명 절차가 필요합니다.
4. 서버 관리자가 알려 준 **업무 서버 HTTPS 주소**와 해당 역할의 초기 admin 비밀번호로 로그인합니다.
5. 설정의 **교직원 · 서버 관리**에서 교직원 계정·좌석 IP·직책·권한·특별실을 등록합니다. 초기 시험 교사는 A·B·C 세 명으로 만들면 읽음·미읽음 회수까지 시험할 수 있습니다.
6. **학생·학부모 관리**를 열고 학교 소식 서버의 별도 HTTPS 주소·admin 계정으로 로그인합니다. 학급 A·B와 가상 학생·보호자, 담당 교사를 등록합니다. 교사는 교내 계정 ID와 연결하고 담당 학급·담임·발송 권한을 확인합니다.
7. 학교가 확정한 가입 정책이 적용된 상태에서 교직원 최초 활성화·개인 비밀번호 설정을 시험합니다. IP만으로 로그인되거나 공동 임시 비밀번호를 계속 사용하는 상태로 완료 처리하지 않습니다.

시험 초기에 학생 전체 명부를 넣지 않습니다. 현재 관리자 화면에서 소수 계정을 등록·초대하는 절차를 먼저 확인합니다. 학생 CSV·엑셀 일괄 등록 파일 업로드 절차는 이번에 확인한 화면에서 제공되지 않으므로, 존재하지 않는 메뉴로 안내하지 않습니다. 대량 등록은 추가 구현·양식 확정 후 진행합니다.

### 4-2. 두 번째 교사 PC 설치

같은 설치 EXE를 복사·설치하고 동일한 업무 서버 주소에 교사 B로 로그인합니다. 학교가 허용한 교직원망을 사용합니다. 기존 쿨메신저·알약·GPI·Zombie ZERO·FTP Utility는 켜 둔 상태로 시험합니다. V3와 Defender는 실제 해당 백신이 활성화된 별도 시험 PC에서 확인하며, 한 PC에 백신을 더 설치해 시험하지 않습니다.

### 4-3. PC 시험표

| 시험 | 하는 방법 | 정상 기준 |
| --- | --- | --- |
| 메시지·파일 | A에서 B에게 짧은 글과 무해한 TXT 보내기 | B 수신·파일 열람, A 읽음 상태 확인 |
| 오프라인 전달 | B 로그아웃, A 전송, B 다시 로그인 | 재접속 후 수신 |
| 회수 | A가 B·C에게 보낸 뒤 B만 읽고 C는 미읽음 유지 | A 회수 가능. 모두 읽은 새 메시지는 회수 거부 |
| 예약·나에게·임시저장 | 미래 시각 예약, 별도 자기 전송, 초안 저장·다시 열기 | 중복 없이 처리되고 예약 전에는 수신되지 않음 |
| 제출 | A가 B에게 제출 요청, B가 제출·회수·재제출 | A의 제출 현황에 최신 상태 표시 |
| 할 일 | 기한이 있는 메시지에서 후보 추출 후 확인·등록 | 등록 전 확인 가능, 기한별 D-Day 일치 |
| 채팅·설문 | A·B로 대화하고 시험 설문 응답 | 권한에 맞는 기록과 결과 확인 |
| 원격지원 | 담당자가 승인한 두 PC에서 요청·동의·보기·종료 | 동의 전 화면 없음, 종료 후 조작 불가. 차단되면 경로·정책 확인 |
| 특별실 | 같은 방·날짜·시간으로 A와 B가 예약 | 중복 거부, 특별실은 7교시까지 |
| 상담 개설 | A가 개방하고 학생·보호자가 신청 | 중복 예약 거부, 8교시·야간1·야간2까지 선택 가능 |
| 로그인 복원 | 개인 시험 PC에서 자동 실행·자동 로그인 설정 후 Windows 재로그인 | 메신저 실행·세션 복원. 공용 PC에서는 설정하지 않음 |
| 권한 회수 | 관리자에서 시험 계정 중지 | 기존 PC·앱 세션도 사용 불가 |

기본 메시지 송수신 때문에 사용자 PC의 인바운드를 모두 열 필요는 없습니다. PC 직접 원격지원은 별도 인바운드 경로가 필요하며, 담당자가 기존 `Configure-RemoteFirewall.ps1`을 실행 파일·교직원망·Domain/Private로 제한해 적용한 뒤 확인합니다.

### 4-4. 파일을 업로드하는 위치

업무 자료는 서버의 파일 폴더에 직접 복사하는 방식으로 등록하지 않습니다. 프로그램의 파일 선택·업로드 화면을 이용해야 권한·기한·검사 기록이 적용됩니다.

| 자료 | 어디에서 올리나요? | 파일 위치·제한 |
| --- | --- | --- |
| 교직원 업무 파일 | 메시지 작성 창의 첨부 | 사용자가 가진 파일 선택, 내부 첨부 한 파일 100MB까지 |
| 공지·가정통신문 | 학교 공지 작성 화면 | JPG/PNG 한 장 200,000바이트까지, 공지당 최대 5장 |
| 기초시간표 | 시간표의 학기 기초시간표 올리기 | 아래 실제 원본 XLSX, 10MB 이하 |
| 일자별 시간표 | 시간표의 일자별 시간표 올리기 | 아래 실제 원본 XLSX, 10MB 이하 |
| 제출 자료 | 받은 제출 요청의 제출 화면 | 답장 메시지 없이 제출 가능, 자신의 제출 회수·재제출 시험 |

현재 PC에 있는 시간표 원본:

- `C:\Users\user\Downloads\기초시간표 예시.xlsx`
- `C:\Users\user\Desktop\24년메신저다운방\2026. 10. 8. 목요일 시간표 - 1학년.xlsx`

다른 PC에서 선택하려면 원본 XLSX를 해당 PC로 복사합니다. 처음에는 개인정보를 제거한 사본으로 시험합니다. 수업 담당자 권한·담임·학급 명부 연결 후 업로드하고, 미리보기에서 교사·학급을 연결한 다음 게시를 확정합니다. 단순 파일 선택만으로 시간표가 바뀌는 것은 아닙니다. 변경 파일이 없는 날에는 기초시간표가 보이는지도 확인합니다.

## 5. Android 기기에서 할 일

### 5-1. 설치

1. 위 teacher APK는 교사 기기에, family APK는 학생·보호자 시험 기기에 복사합니다. 학교가 허용한 방법으로 USB·파일 전달을 사용합니다.
2. 기기의 파일 앱에서 APK를 엽니다. 보통 `Download` 폴더에서 찾을 수 있지만 실제 저장 위치는 전달 앱에 따라 달라집니다.
3. Android가 요구하면 이번 파일을 연 파일 앱에만 **출처를 알 수 없는 앱 설치** 권한을 허용합니다. 학교 관리 기기의 정책으로 차단되면 담당자에게 확인하고 우회하지 않습니다. Play Protect 전체를 끄지 않습니다. [Android의 별도 설치 배포 설명](https://developer.android.com/distribute/marketing-tools/alternative-distribution)
4. 설치 후 필요 없는 설치 허용 권한은 다시 끕니다. `여양고 교무실`과 `여양고 학교 소식`이 별도 앱으로 보이는지 확인합니다.

현재 APK를 시험하는 데 Google Play 업로드는 필요하지 않습니다. Google Play 내부 시험을 사용할 때는 별도 Release 서명 AAB·개발자 계정·등록 준비가 필요하며 현재 Debug APK로 완료된 상태가 아닙니다.

### 5-2. 접속·시험

| 사용자 | 앱 | 입력 주소·통신 환경 | 시험 |
| --- | --- | --- | --- |
| 교사 | 여양고 교무실 | 업무 서버 HTTPS, 승인된 교내 Wi-Fi 또는 VPN | PC에서 보낸 메시지·첨부, 개인 시간표, 공지 작성·상담 개설·특별실 예약 |
| 학생 | 여양고 학교 소식 | 공개 학교 소식 HTTPS | 시험 계정·초대 가입 후 소속 학급 공지·시간표·상담 |
| 학부모 | 여양고 학교 소식 | 같은 공개 학교 소식 HTTPS | 임시회원 가입, 설정에서 시험 자녀 번호 연결, 자녀별 전환 |

교사용 앱의 기본 로그인 주소는 업무 서버입니다. 학교 소식 연계는 업무 서버의 연결 설정과 교사·학급 연결을 준비해야 동작합니다. 학생·학부모 계정으로 교사용 앱의 업무 로그인에 접속하는 방식이 아닙니다.

먼저 허용된 교내 Wi-Fi에서 시험하고, 학생·학부모 앱은 Wi-Fi를 끈 뒤 LTE/5G에서도 다시 시험합니다. 외부에서 교사의 개인 업무 메시지를 확인하려면 학교가 승인한 VPN이 필요합니다. 이를 해결하려고 업무 서버를 공개하지 않습니다.

시험 중 Android 파일 선택기로 고른 첨부는 앱이 권한을 받아 읽습니다. 사용자 파일 전체 접근 권한을 임의로 열 필요는 없습니다. 로그아웃·앱 재시작·화면 회전·글자 크게 설정·네트워크 전환·네트워크 단절 후 재시도도 확인합니다.

**현재 앱을 닫은 상태의 FCM/APNs 푸시는 미구현입니다.** 시험은 앱 열기·새로고침으로 수신을 확인합니다. 앱 종료 후 알림이 없는데 정상 구현 완료라고 판정하지 않습니다.

## 6. iPhone·iPad에서 할 일

### 6-1. Mac에서 제작 준비 — 서버 작업과 병행 가능

1. 위 `yymessenger-1.0.0-ios-build-source.zip`을 Mac에 복사합니다.
2. 예를 들어 `/Users/<Mac사용자>/Projects/yymessenger`에 압축을 풉니다. `<Mac사용자>`는 실제 Mac 계정으로 바꾸며, 그 폴더 바로 안에 global.json·src·scripts가 보여야 합니다.
3. 개발 담당자가 [iOS 제작 안내](ios-operations.md)에 있는 .NET SDK·MAUI iOS 워크로드·Xcode 호환 조합을 준비합니다. Windows 서버 PC에 Xcode를 설치하는 절차가 아닙니다.
4. 두 앱의 식별자를 각각 준비합니다: 교사용 `kr.school.yeoyang.teacher`, 학생·학부모용 `kr.school.yeoyang.family`.

Mac 터미널에서 소스 폴더로 이동한 후 시뮬레이터를 각각 빌드합니다.

```bash
YY_SCHOOL_APP=Teacher bash scripts/Build-iOS.sh simulator
YY_SCHOOL_APP=Family bash scripts/Build-iOS.sh simulator
```

결과는 Mac 프로젝트의 `artifacts/ios/` 아래 역할별 simulator ZIP입니다. 이것은 Mac 시뮬레이터 전용이며 iPhone 설치 파일이 아닙니다. 현재 소스의 iOS 네이티브 빌드·기능을 새로 검증해야 하며 예전 CI 성공 기록으로 대신하지 않습니다.

### 6-2. 실제 iPhone 시험용 서명·업로드

1. 학교의 Apple Developer Program 계정과 App Store Connect를 준비합니다. 두 앱의 앱 기록·Bundle ID·서명 인증서·프로비저닝 프로파일이 각각 일치해야 합니다.
2. Mac Keychain에 Apple Distribution 인증서와 개인키를 설치하고, 교사용 App Store 프로파일에 맞춰 `YY_IOS_SIGNING_IDENTITY`, `YY_IOS_PROFILE`을 Mac에서 설정합니다. 인증서 개인키·비밀번호·프로파일을 GitHub에 올리지 않습니다.
3. 교사용을 제작한 뒤, 학교 소식 앱의 프로파일로 바꿔 각각 제작합니다.

```bash
YY_SCHOOL_APP=Teacher bash scripts/Build-iOS.sh archive
# 학교 소식 앱의 프로파일로 바꾼 후
YY_SCHOOL_APP=Family bash scripts/Build-iOS.sh archive
```

스크립트가 지정하는 결과 폴더:

- Mac 소스 폴더의 `artifacts/ios/archive/Teacher/`
- Mac 소스 폴더의 `artifacts/ios/archive/Family/`

**여기에 실제로 만들어진 해당 앱의 `.ipa`가 업로드할 파일**입니다. 현재 Windows 폴더에 이미 있다는 뜻이 아닙니다. 출력 위치가 SDK에 따라 다르면 Mac에서 아래 검색 결과와 빌드 로그를 대조합니다. IPA가 없으면 생성 완료로 보지 않습니다.

```bash
find artifacts/ios src/SchoolMessenger.Mobile/bin/Release -type f -name '*.ipa'
```

4. 실제 생성된 서명 IPA를 Apple Transporter 등 지원되는 도구로 App Store Connect의 해당 앱에 업로드합니다. 소스 ZIP·시뮬레이터 ZIP·APK를 올리는 것이 아닙니다. [Apple 빌드 업로드 안내](https://developer.apple.com/help/app-store-connect/manage-builds/upload-builds)
5. 처리 완료 후 TestFlight 시험 그룹을 준비합니다. 외부 테스터는 Apple의 베타 심사가 필요할 수 있습니다. iPhone에 TestFlight를 설치하고 초대를 수락해 각각 설치합니다. [Apple TestFlight 안내](https://developer.apple.com/help/app-store-connect/test-a-beta-version/testflight-overview/)

이 절차는 시험 배포 준비 안내이며 지금 App Store 공개를 승인하거나 실행한 것은 아닙니다. 실제 업로드·배포 시점은 학교가 별도로 정합니다.

### 6-3. iPhone 시험

Android와 같은 서버·시험 계정·권한 표를 적용합니다. 추가로 다음을 확인합니다.

- 교사용 앱의 교내망 접속 시 iOS가 로컬 네트워크 권한을 요구하면 허용 후 접속되는지.
- 앱을 완전히 종료했다 켜도 Keychain 세션 복원이 정상인지, 로그아웃 후 복원되지 않는지.
- 백그라운드·앱 전환 화면에서 민감한 내용 가림, 큰 글씨·작은 화면·뒤로 가기.
- 사진·파일 선택, 공지 이미지 표시, 기초·변경 시간표, 특별실·상담 신청과 취소.
- 학교 밖 LTE/5G에서 학교 소식은 열리고, VPN 없이 교내 개인 업무 API는 열리지 않는지.

## 7. 기기들을 연결해서 마지막으로 확인할 시나리오

1. **업무 메시지:** PC A에서 교사 B에게 전송. PC B와 B의 교사용 앱에서 확인. 학생·학부모 앱에는 보이지 않아야 합니다.
2. **대상 공지:** 교사 A가 학급 A만 대상으로 이미지 공지 게시. A 학생·연결 보호자·연결된 담임은 열람, 학급 B 계정은 열람 불가여야 합니다. 제목 목록뿐 아니라 직접 열람 요청도 차단돼야 합니다.
3. **다자녀:** 서로 다른 학급의 가상 자녀 둘을 보호자에게 연결하고, 자녀별 공지·시간표가 섞이지 않는지 확인합니다. 보호자 연결은 운영 기본 학교 확인 방식이며, 학교가 별도로 승인한 코드 즉시 연결 설정은 구별해 시험합니다.
4. **시간표:** PC에서 기초 게시 후 특정 날짜의 변경 게시. PC·교사용 Android/iPhone·학교 소식 Android/iPhone에서 같은 날짜·학급·교사를 비교합니다. 변경이 없는 날짜는 기초표여야 합니다.
5. **상담:** 교사 PC 또는 교사용 앱에서 슬롯 개설. 학생·보호자 앱 중 한쪽이 예약하면 다른 쪽의 동일 슬롯 중복이 거부돼야 합니다. 취소 후 다시 신청해 봅니다. 3학년 4교시는 12:50~13:40, 야간1은 19:00~19:50, 야간2는 20:00~20:50인지 확인합니다.
6. **첨부 경계:** 공지는 200,000바이트 이하의 JPG/PNG 5장까지 성공, 초과 크기·6장·PDF는 거부돼야 합니다. 앱 안에서 이미지를 확인하며 다운로드 메뉴는 없어야 합니다. 화면 촬영 자체를 완전히 막는다는 뜻은 아닙니다.
7. **권한 회수·졸업:** 운영 명부가 아닌 시험 계정에서만 중지·보호자 관계 해제·졸업 파기를 검증합니다. 백업에서 관계가 되살아나지 않는지 담당자가 확인합니다.
8. **신고:** 실제 개인정보 없이 “시험 신고”를 직접 접수하고 관리자 접수 목록·메일을 확인합니다. 자동 오류 수집은 인증된 서버 API의 5xx 등을 대상으로 하며 모든 화면 멈춤·오프라인 오류를 자동 감지하는 것은 아닙니다. 자동 수집을 시험하려고 운영 DB를 손상시키지 않습니다.

9. **수강신청:** 관리자가 PC 수강신청 메뉴에서 담당자를 지정하고 대상 학급·신청 기간·묶음별 선택 개수·과목과 담당 교사를 설정합니다. 공개 후 학생 두 명이 같은 과목에 모두 신청되는지(정원 없음), 변경·취소·재신청, 미신청자 표시, 마감 후 차단과 분반 확정을 확인합니다. 학부모 대리 신청과 일반 교사의 전체 명단 조회는 차단돼야 합니다. [수강신청 운영 안내](elective-registration.md)를 따릅니다.

시험 결과는 기기·앱 버전·계정 역할·네트워크·시험 시각·예상 결과·실제 결과로 기록합니다. 화면을 공유할 때 학생 이름·자녀 번호·비밀번호·연결 키를 가립니다. 자동 오류 수정·릴리즈 예약은 현재 중지 상태를 유지합니다.

## 8. 아직 완료로 처리하면 안 되는 항목

- 실제 서버의 파일 검사 성공과 외부 HTTPS·망 격리·UPS·별도 백업 복원.
- iOS 최신 소스 빌드·서명 IPA·실제 기기 시험.
- 앱 종료 상태의 FCM/APNs 푸시, 관리자 앱 계정 다중 인증, 스토어 요구에 맞는 앱 내 계정 삭제 절차.
- 학생 대량 등록 화면·입력 양식과 선택과목/분반을 개인 시간표의 실제 수업 시간대에 연결하는 자료. 선택과목 신청·확정 명부는 구현했다.
- 급식 NEIS 실연동: 이번 현재 소스 검색에서는 급식·NEIS 호출 구현을 확인하지 못했습니다. 예전 안내의 “운영 키 입력”만으로 완료된 기능이라고 판단하지 않습니다. 연동 코드·화면·학교 코드·실제 응답 확인을 개발 작업으로 남깁니다.
- Windows 코드서명, Android 정식 서명 AAB와 스토어 등록, 학교 약관·운영 정책 확정.

지금의 APK·EXE·서버 ZIP은 시험 준비물입니다. 이 문서에 적힌 미완료 항목까지 해결하고 학교가 별도로 승인한 뒤 전체 학생·학부모 대상으로 배포합니다.

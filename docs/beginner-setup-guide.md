# 처음 설치부터 Cloudflare 연결까지

2026-10-15 · 여양고 온라인 교무실 · 배포 전 **1.0.0**

2026-10-20 이후의 실제 작업 순서·기기별 시험·파일 전체 경로는 [기기별 설치·테스트 순서](installation-and-test-order.md)를 먼저 확인하세요. 현재 서버 파일 검사 실패와 iOS IPA 미생성 등 준비되지 않은 항목도 구분했습니다.

이 안내는 **학교 서버와 앱을 처음 준비하는 사람**을 위한 작업 순서입니다. 실제 학교 서버·Cloudflare·앱스토어는 아직 설치하거나 공개하지 않았습니다. 아래 주소와 경로는 예시입니다. 학교에서 확정한 값으로 바꾼 뒤 사용하세요.

## 1. 지금 무엇을 만드는 것인가요?

사용자는 세 가지 설치 프로그램을 사용합니다.

| 사용하는 사람 | 설치할 프로그램 | 어디에 설치하나요? |
| --- | --- | --- |
| 선생님 | 여양고 교무메신저 | 선생님 자리의 Windows PC |
| 선생님 | 여양고 교무실 | Android 휴대폰 또는 iPhone |
| 학생·학부모 | 여양고 학교 소식 | Android 휴대폰 또는 iPhone |

이 프로그램들이 공통으로 접속하는 곳이 **서버**입니다. 학교에서 항상 켜 놓는 컴퓨터에 두 종류의 서버 프로그램을 운영합니다.

| 서버 종류 | 하는 일 | 접속 가능한 곳 |
| --- | --- | --- |
| 교내 업무 서버 | 교직원 쪽지·채팅·설문·업무 파일 | 교내 허용망, 승인된 VPN |
| 학교 소식 서버 | 학생·학부모 계정, 대상 공지, 시간표, 상담 | Cloudflare를 통한 외부 HTTPS |

교내 업무 서버가 학교 소식 서버로 승인된 공지와 시간표 등을 보냅니다. 교직원의 개인 쪽지와 업무 첨부 전체를 외부 서버로 복사하지 않습니다. **앱에서 서버 주소를 입력하는 것은 설치 앱의 통신 설정**입니다. 사용자용 웹앱을 설치한다는 뜻이 아닙니다.

## 2. 이 폴더에서 무엇을 열면 되나요?

현재 작업 폴더는 `C:\Users\user\Documents\ChatGPT\여양고 온라인 교무실`입니다.

1. 탐색기에서 위 폴더를 여세요.
2. `00_처음보세요.txt`를 읽으세요.
3. `01_설치파일`의 기기별 폴더에서 필요한 EXE·APK·ZIP 실제 파일을 찾으세요.
4. `02_설명서/00_설치와_기능테스트_안내.pdf`부터 읽으세요. 이 안내의 메모장용 사본은 `02_설명서/01_설치와_접속`에 있습니다. 사용기능·등록과 개인정보·운영과 보안 자료도 주제별로 구분했습니다.

**다른 컴퓨터에 전달할 때는 기기별 폴더의 실제 설치파일과 SHA256SUMS.txt를 함께 복사**하세요. 설명서의 `.lnk`는 현재 PC에서 원본 문서를 찾는 바로가기이며 설치파일 대신 전달하지 않습니다.

| 실제 원본 파일 | 용도 |
| --- | --- |
| `artifacts/yymessenger-1.0.0-setup-win-x64.exe` | 교직원 PC 설치 마법사 |
| `artifacts/mobile/kr.school.yeoyang.teacher-Signed.apk` | 교사용 Android 시험 앱 |
| `artifacts/mobile/kr.school.yeoyang.family-Signed.apk` | 학생·학부모용 Android 시험 앱 |
| `artifacts/yymessenger-1.0.0-servers-win-x64.zip` | 담당자에게 전달할 두 서버 패키지 |
| `artifacts/SHA256SUMS.txt`, `artifacts/mobile/SHA256SUMS.txt` | 파일 내용이 바뀌지 않았는지 비교하는 체크섬 |

아이폰 설치용 IPA는 아직 없습니다. Android APK는 **Debug 시운행용**이고, Windows 설치 파일의 학교 배포 서명도 아직 준비 전입니다. 최신 전달 파일은 `01_설치파일`의 기기별 폴더에서 복사하세요. 예전 단일 서버 ZIP과 중간 빌드는 사용하지 않습니다.

## 3. 가장 먼저 할 일: 준비 담당자를 정하세요

처음부터 서버를 혼자 공개하려고 하지 말고, 아래 표에서 담당자를 정한 뒤 한 단계씩 완료하세요. 학교가 승인하지 않은 상태에서 회선 설정이나 기존 홈페이지 DNS를 바꾸지 않습니다.

| 순서 | 할 일 | 담당 | 완료 확인 |
| --- | --- | --- | --- |
| 1 | 학교 회선을 통한 앱 운영과 개인정보 처리 승인 | 학교 책임자·정보 담당자 | 승인한 운영 방식·담당자 기록 |
| 2 | 서버 Windows 에디션·RAM·가상화 확인 | 서버 관리자 | 사양 확인 JSON |
| 3 | 업무/학교 소식 서버의 망·계정·저장소 분리 | 정보 담당자 | 실제 허용·차단 시험 |
| 4 | UPS·별도 암호화 백업 장치 마련 | 학교·정보 담당자 | 정전 종료 및 복원 시험 |
| 5 | 앱 도메인·HTTPS 인증서 준비 | 도메인 관리자·정보 담당자 | 도메인 관리 권한·인증서 검증 |
| 6 | 두 서버 설치·설정·메일·자동 시작 | 서버 관리자 | 아래 사전 점검 통과 |
| 7 | 학교 소식 서버에 Cloudflare 연결 | 도메인 관리자·정보 담당자 | 외부 휴대폰 접속·비대상 차단 |
| 8 | PC·휴대폰을 소수 계정으로 시험 | 담당 교사·시험 사용자 | 메시지·공지·시간표·상담 확인 |
| 9 | 남은 앱 기능·서명·스토어 준비 | 개발자·학교 | 학교가 배포 결정 |

지금은 **2번 사양 확인과 5번 도메인 소유·관리 방법 확인**부터 진행할 수 있습니다. UPS·백업 장치와 망 분리는 아직 준비되지 않았습니다.

## 4. 도메인이 무엇이고, 어느 도메인을 말하나요?

도메인은 IP 대신 기억하기 쉬운 서버 이름입니다. 여기서는 **이 프로그램의 학교 소식 서버 주소**를 말합니다. 기존 학교 홈페이지 주소 전체를 옮기라는 뜻이 아닙니다.

예를 들어 학교가 앱 전용으로 소유한 도메인이 `school-app.example`이면, 앱 서버 주소를 `https://apps.school-app.example`처럼 정할 수 있습니다. **이 문서의 `.example`·`.invalid` 주소는 실제로 접속할 수 없는 설명용 이름**입니다. 그대로 입력하지 마세요.

두 가지 선택지가 있습니다.

- 기존 학교 홈페이지의 하위 주소 사용: 홈페이지/교육청의 **실제 DNS 관리자**가 해당 주소를 사용할 수 있는지, Cloudflare에 연결할 수 있는 지원 방식인지 확인해야 합니다. 주소 문자열을 아는 것만으로는 설정 권한이 생기지 않습니다.
- 앱 전용 별도 도메인 사용: 학교가 관리하는 계정으로 등록하고 그 도메인의 DNS를 Cloudflare에서 관리합니다. 기존 홈페이지·메일과 연결하지 않은 새 도메인이면 기존 서비스에 미치는 영향을 줄일 수 있습니다.

아래 절차는 **앱 전용 별도 도메인을 준비한 경우**를 기준으로 설명합니다. 아직 구매를 진행하지 않았으며 가격·갱신 주기와 학교 소유권·인수인계 방법을 확인한 뒤 정하세요. 도메인 등록비와 앱스토어 계정 비용은 별개입니다. Cloudflare 연결만으로 앱스토어에 등록되는 것은 아닙니다.

## 5. 서버 사양을 먼저 확인하세요

사진에서는 i5-10500·RAM 8GB·Windows 11·DDR4로 보입니다. 추가하려는 RAM이 DDR3라면 서로 맞지 않으므로 먼저 모듈 라벨과 메인보드 규격을 확인하세요.

학교 서버에 서버 ZIP을 전달해 압축을 푸세요. 아래에서는 압축 내부의 `scripts`·`server`·`announcement-server` 폴더가 **바로 `C:\SchoolMessenger` 안에 보이는 상태**를 예로 듭니다. ZIP 안에 상위 폴더가 있으면 그 안의 내용물을 지정 위치로 옮겨 주세요. 이 경로는 현재 개발 PC의 작업 폴더와 별개입니다.

1. 학교 서버에서 시작 버튼을 누르고 `Windows PowerShell`을 검색해 여세요.
2. 아래 명령을 한 줄씩 입력하세요. 이 사양 확인에는 관리자 창이 필요하지 않습니다.

```powershell
Set-Location 'C:\SchoolMessenger'
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Inspect-ServerComputer.ps1 -OutputPath .\server-computer-check.json
```

3. 같은 폴더에 생긴 `server-computer-check.json`을 정보 담당자에게 전달하세요. 학교 서버 설정을 바꾸는 명령은 아닙니다.
4. 담당자가 Windows 에디션·가상화 지원·메모리를 확인하고 격리 방법을 정합니다.

**가상 컴퓨터(VM)**는 한 물리 PC 안에서 별도 운영체제를 실행하는 방식입니다. 현재 검토 구성은 관리용 호스트와 업무용·학교 소식용 VM 두 대입니다. VM 두 개를 만드는 것만으로 망 분리가 완료되지는 않습니다. 학교 소식 VM에서 업무 VM·교내 PC·호스트로 접근할 수 없도록 실제 망 규칙과 차단 시험이 필요합니다. 8GB에서 이 구성이 원활하게 돌아간다고 보장할 수 없습니다. 32GB는 초기 검토 기준이며 측정된 최소 사양은 아닙니다.

서버 CPU·RAM 사용률을 무조건 특정 비율 아래로 고정하는 기능은 없습니다. 현재 저부하 설정은 동시 파일 작업과 전송 속도 등을 줄이는 기능입니다. 실제 동시 사용자·첨부·백업을 시험해 자원 사용량을 측정해야 합니다.

## 6. 정보 담당자와 두 서버를 설치하세요

여기부터는 사양·망·UPS·백업을 확인한 다음 진행합니다. 담당자에게 다음 표를 그대로 전달해 빈칸을 확정해 달라고 하세요.

| 확정할 값 | 설명 |
| --- | --- |
| 업무 서버 내부 HTTPS 주소 | 교사 PC·교사용 앱이 접속할 주소. 인증서 이름과 일치해야 함 |
| 학교 소식 공개 HTTPS 주소 | 학생·학부모 앱이 접속할 주소 |
| 업무 서버 허용 CIDR | 실제 허용 IP 범위. 기존 서버 IP 하나를 보고 임의로 추정하지 않음 |
| 각 서버 인증서 지문 | Windows가 올바른 인증서를 찾는 식별값 |
| VM별 전용 표준 운영 계정 | 서버 실행·보호된 비밀값·예약 작업에 같은 계정 사용 |
| 실제 별도 백업 장치 경로 | 같은 SSD의 다른 폴더나 VM 가상 디스크는 별도 장치가 아님 |
| 장애 알림 발신/수신 계정 | 실제 이메일 도착을 시험해야 함 |
| 학교가 확정한 약관 파일 | 현재 검토안으로 운영 가입을 열지 않음 |

### 6-1. 파일 배치

각 VM에 필요한 서버 폴더와 `scripts`·`config`·`docs`를 배치합니다. 업무 VM은 `server/SchoolMessenger.Server.exe`, 학교 소식 VM은 `announcement-server/SchoolMessenger.AnnouncementServer.exe`를 사용합니다. PC용 `SchoolMessenger.exe`와 구별하세요.

각 VM에서 별도 `C:\SchoolMessengerOperations` 폴더를 만들고 `config/server-operations.example.json`을 `server-operations.local.json`이라는 이름으로 그 안에 복사합니다. 이 폴더와 비밀 설정을 두 VM에서 공유하지 않습니다. 폴더 생성·권한·인증서 개인키 접근은 정보 담당자가 준비합니다.

### 6-2. 설정 파일 입력

메모장으로 각 VM의 `C:\SchoolMessengerOperations\server-operations.local.json`을 여세요. JSON은 항목 이름의 큰따옴표·쉼표를 유지해야 합니다. Windows 경로의 역슬래시는 JSON 안에서 `\\`로 적습니다.

| 항목 | 업무 VM | 학교 소식 VM |
| --- | --- | --- |
| `service` | `office` | `announcements` |
| `executable` | `C:\\SchoolMessenger\\server\\SchoolMessenger.Server.exe` | `C:\\SchoolMessenger\\announcement-server\\SchoolMessenger.AnnouncementServer.exe` |
| `dataDirectory` | 업무 전용 데이터 경로 | 학교 소식 전용 데이터 경로 |
| `backupDirectory` | 별도 장치의 업무 백업 경로 | 별도 장치의 학교 소식 백업 경로 |
| `certificateThumbprint` | 업무 인증서의 실제 지문 | 학교 소식 인증서의 실제 지문 |
| `publicHost` | 공개 앱 도메인, `https://`와 경로 제외 | 왼쪽과 같은 이름 |
| `allowedNetworks` | 담당자가 확정한 CIDR | 빈 문자열 |
| `port` | `7443` | `8443` |
| `tunnelOnly` | `false` | `true` |
| `resourceProfile` | `low-memory` | `low-memory` |
| `attachmentBackup` | `true` | `true` |

`networkIsolationVerified`·`upsInstalledAndTested`·`backupDeviceAndEncryptionVerified`는 처음에는 `false`입니다. 실제 시험을 통과한 뒤에만 `true`로 바꾸세요. 시작을 통과시키려고 미리 바꾸지 않습니다.

인증서는 각 VM의 로컬 컴퓨터 저장소에 개인키와 함께 설치하고 운영 계정의 사용 권한을 설정합니다. 학교 소식 인증서는 실제 앱 도메인 이름과 맞아야 합니다. 인증서 발급·신뢰 체인·갱신은 담당자가 준비해야 하는 별도 작업입니다. Cloudflare의 바깥쪽 HTTPS가 동작해도 원본 서버 인증서를 대신 설치해 주지는 않습니다.

### 6-3. Gmail 알림과 비밀값 등록

`alerts.sender`에는 발신 계정을, `alerts.recipient`에는 받을 개인 이메일을 로컬에서 입력하세요. 예시 파일이나 GitHub에는 주소와 인증 정보를 넣지 않습니다. 발신 서비스에 맞는 SMTP·포트·인증 방식을 확인합니다. Gmail 예시 값은 `smtp.gmail.com`, `587`입니다. 지원되는 계정에서는 2단계 인증과 앱 비밀번호를 준비합니다. [Google 앱 비밀번호 안내](https://support.google.com/mail/answer/185833).

발신 계정이 준비되면 `alerts.enabled`를 `true`로 하고 `deliveryVerified`는 아직 `false`로 둡니다. 두 VM 각각의 **전용 표준 운영 계정**으로 PowerShell을 열어 실행합니다.

```powershell
Set-Location 'C:\SchoolMessenger'
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Configure-ServerOperations.ps1 -ConfigurationPath C:\SchoolMessengerOperations\server-operations.local.json -InitializeSecrets -Apply
```

화면에서 최초 서버 관리자 비밀번호(12~128자), 두 서버에 동일하게 넣을 무작위 연결 키(32자 이상), 메일 발신 계정과 앱 비밀번호를 입력합니다. 현재 도구는 이 초기화 단계에 메일 인증 정보도 요구하므로 발신 계정을 먼저 준비해야 합니다. 학교가 승인한 비밀번호 관리 도구로 연결 키를 만들고 두 서버에 같은 값을 넣으세요. 비밀값을 채팅에 보내지 마세요.

보호 파일은 해당 Windows 계정에 묶입니다. 다른 계정·새 컴퓨터로 복사하는 것만으로 복구되지 않습니다. 키·인증서·설정의 복구 자료는 담당자가 별도 암호화 보관소에 관리해야 합니다. 기존 파일이 있으면 재초기화로 덮어쓰지 않습니다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Server-Operations.ps1 -ConfigurationPath C:\SchoolMessengerOperations\server-operations.local.json -Mode TestEmail
```

실제 수신함과 스팸함을 확인한 뒤에만 `alerts.deliveryVerified`를 `true`로 바꿉니다. 메일 수신 계정과 운영 Windows 계정은 서로 다른 것입니다.

### 6-4. 가입 정책·사전 점검·시작

담당자가 학교 확정 약관을 `Registration__PolicyFile` 설정으로 각 운영 계정/작업 환경에 연결해야 합니다. 약관 검토와 운영 적용 절차는 [약관 설정 안내](registration-policy-operations.md)에 있습니다. 운영 점검 통과만으로 가입 정책이 승인되는 것은 아닙니다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Server-Operations.ps1 -ConfigurationPath C:\SchoolMessengerOperations\server-operations.local.json -Mode Preflight
```

결과의 `ready`가 `true`인지 확인합니다. `false`라면 `issues`에 표시된 준비사항을 해결합니다. 이 결과는 실행 준비 점검이며, 실제 망 차단·백업 복원·전체 보안을 자동 인증하는 기능은 아닙니다.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Configure-ServerOperations.ps1 -ConfigurationPath C:\SchoolMessengerOperations\server-operations.local.json -RegisterTasks -Apply
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\Server-Operations.ps1 -ConfigurationPath C:\SchoolMessengerOperations\server-operations.local.json -Mode Start
```

이 작업은 Windows 시작 작업과 감시 작업을 등록합니다. 비밀값을 등록한 **같은 표준 Windows 계정**으로 진행하세요. 작업 등록이 거부되면 정보 담당자에게 해당 계정으로 실행되도록 지원받으세요. 초기화부터 관리자 계정으로 바꿔 실행하는 방식으로 해결하지 않습니다. `Start`는 서버가 동작하는 동안 계속 실행되므로 그 창을 닫으면 서버도 종료될 수 있습니다. 시운행 후 재부팅하여 예약 작업에 의해 실제로 시작되는지 확인하세요. 이미 실행 중인 서버를 중복 실행하지 않습니다.

## 7. Cloudflare: 앱 전용 도메인을 연결하세요

**Cloudflare는 바깥의 앱 요청을 학교 소식 서버로 전달하는 통로**입니다. DB와 첨부 원본은 학교 서버에 있습니다. 학교 내부에 외부 인바운드 포트를 새로 여는 방식은 쓰지 않지만, 공개 앱의 로그인·권한·패치·망 격리는 계속 필요합니다. 익명화 수단은 아닙니다.

### 7-1. 도메인을 Cloudflare에 등록

학교가 관리하는 Cloudflare 계정으로 로그인하고 2단계 인증과 복구 수단을 준비합니다. 홈페이지의 도메인 추가 화면에서 **새 앱 전용 도메인**을 추가하세요. DNS 관리에 사용할 요금제·기능은 화면에서 확인합니다. 도메인 구매 업체의 관리 화면에 Cloudflare가 표시한 네임서버를 입력합니다. 기존 학교 홈페이지 도메인의 네임서버를 이 절차로 바꾸지 마세요. 새 도메인이더라도 기존 메일/DNS 사용 여부를 먼저 확인합니다.

완료 기준은 Cloudflare에서 해당 도메인이 활성 상태로 표시되는 것입니다. 단순히 도메인 이름을 추가한 화면과 실제 DNS 위임 완료는 다릅니다. 공식 전제조건은 [Cloudflare Tunnel 시작 안내](https://developers.cloudflare.com/tunnel/get-started/)를 따릅니다.

### 7-2. 학교 소식 VM에만 cloudflared 설치

이 프로젝트는 경로 차단 규칙을 포함한 **로컬 설정 파일 방식**을 예제로 제공합니다. 아래 방식과 대시보드의 토큰 방식 터널을 섞지 마세요. 관리 웹사이트는 설정 도구이며 사용자 앱은 계속 설치 앱입니다.

1. 학교 소식 VM에서 [공식 다운로드](https://developers.cloudflare.com/tunnel/downloads/)의 Windows 64비트 실행 파일을 받습니다.
2. 정보 담당자가 `C:\Cloudflared\bin`에 `cloudflared.exe`라는 이름으로 배치합니다. 프로그램과 설정은 허용된 관리자만 변경할 수 있게 합니다.
3. 이 VM에서 PowerShell을 열고 아래를 실행합니다. 로그인 명령이 여는 브라우저에서 학교 계정과 앱 전용 도메인을 선택합니다.

```powershell
Set-Location 'C:\Cloudflared\bin'
.\cloudflared.exe --version
.\cloudflared.exe tunnel login
.\cloudflared.exe tunnel create yeoyang-school-news
```

4. 출력된 터널 UUID와 자격 증명 JSON 경로를 담당자의 보호된 작업 기록에 적습니다. 터널 이름과 UUID는 서로 다릅니다. 이 단계는 터널을 만들 뿐 아직 실행하지 않습니다. [공식 생성 절차](https://developers.cloudflare.com/tunnel/features/locally-managed-tunnels/create-local-tunnel/).

### 7-3. 이 프로젝트의 설정 파일 채우기

`C:\SchoolMessenger\config\cloudflared.example.yml`을 `C:\SchoolMessengerOperations\cloudflared.yml`로 복사합니다. YAML은 들여쓰기를 유지해야 합니다. JSON과 달리 예제 Windows 경로의 역슬래시는 하나입니다.

| 바꿀 부분 | 입력할 내용 |
| --- | --- |
| `tunnel` | 앞 단계에서 만든 실제 UUID |
| `credentials-file` | 이 터널의 자격 증명 JSON을 보호된 위치로 복사한 전체 경로 |
| 두 곳의 `hostname` | 실제 공개 앱 도메인 이름, `https://` 제외 |
| `originServerName`, `httpHostHeader` | 학교 소식 서버 인증서와 일치하는 실제 앱 도메인 |
| `service` | 같은 VM의 `https://127.0.0.1:8443` 유지 |
| `noTLSVerify` | `false` 유지 |

`/health/operations` 차단 규칙과 마지막 `http_status:404` 규칙을 유지합니다. 터널 자격 증명에는 업무 서버 비밀번호나 연결 키를 넣지 않습니다. 설정·JSON·로그인 인증 파일을 GitHub에 올리지 않습니다. 실행 계정과 관리자 외 읽기 권한을 제한하세요.

```powershell
.\cloudflared.exe tunnel --config C:\SchoolMessengerOperations\cloudflared.yml ingress validate
```

오류가 없으면 설정 문법 검사가 완료된 것입니다. 인증서와 앱 접속까지 성공했다는 뜻은 아닙니다. 원본 인증서는 이름과 신뢰 체인까지 맞아야 합니다. 경고를 없애려고 인증서 검증을 끄지 않습니다. [HTTPS 원본 확인 방법](https://developers.cloudflare.com/tunnel/troubleshooting/https-origins/).

### 7-4. DNS 연결 후 시험 실행

담당자는 다음 명령의 `실제앱도메인`을 학교가 확정한 이름으로 바꿉니다. 학교 소식 서버가 먼저 실행 중이어야 합니다.

```powershell
.\cloudflared.exe tunnel route dns yeoyang-school-news 실제앱도메인
.\cloudflared.exe tunnel --config C:\SchoolMessengerOperations\cloudflared.yml run yeoyang-school-news
```

첫 줄은 DNS를 실제 변경합니다. 두 번째 줄은 터널을 실행하고 창을 점유합니다. 같은 명령을 교내 업무 서버에서 실행하지 마세요. 터널이 연결되지 않으면 담당자가 학교 정책 안에서 Cloudflare로 나가는 7844 연결 등을 확인합니다. 학교 방화벽을 무단 해제하거나 서버 포트를 외부에 공개하는 방식으로 해결하지 않습니다.

### 7-5. 외부 접속·보호 범위 확인 후 자동 실행

1. 시험 휴대폰에서 학교 Wi-Fi를 끄고 LTE/5G로 전환합니다.
2. 실제 공개 주소의 `/health`가 응답하는지 확인합니다. 루트 주소가 일반 홈페이지로 보일 필요는 없습니다.
3. 학생·학부모용 앱에 공개 HTTPS 주소를 입력하고 가상 시험 계정으로 로그인합니다.
4. 대상 공지는 보이고, 대상이 아닌 다른 학급의 공지·이미지는 직접 요청해도 거부되는지 담당자가 확인합니다.
5. `/health/operations`가 외부에 노출되지 않는지 확인합니다. 개인별 API 응답을 Cloudflare 캐시 규칙으로 강제 저장하지 않습니다.
6. 업무 VM에서 학교 소식 서버로 공지·시간표가 전달되는지, 반대로 학교 소식 VM에서 업무망·교사 PC 접근이 막히는지 시험합니다.

`Healthy` 표시는 Cloudflare와 터널의 연결 상태일 뿐 앱 정상·권한·인증서 전체의 증명이 아닙니다. 자동 실행은 시험 터널을 종료한 후 정보 담당자가 [Windows 서비스 공식 절차](https://developers.cloudflare.com/tunnel/features/locally-managed-tunnels/as-a-service/windows/)로 등록합니다. **서비스가 사용할 설정 파일 경로, 실행 계정의 JSON 읽기 권한과 인증서 신뢰, 재부팅 후 동작**을 함께 확인해야 합니다. 로컬 설정 방식에서 토큰 설치 명령만 복사하면 설정 파일이 누락될 수 있습니다. 레지스트리/서비스 변경이 필요한 단계는 담당자가 수행하세요.

## 8. 선생님 PC에 메신저를 설치하세요

서버 시험을 마친 뒤 `01_설치파일`의 PC 교사용 원본 EXE를 교직원 PC에 복사합니다. 설치 마법사에서 위치·바로가기·설치·완료를 진행하세요. 서버 EXE를 교직원 PC에서 실행하는 것이 아닙니다.

메신저에 **내부 업무 HTTPS 주소**를 입력합니다. 관리자에게 받은 로그인 이름·공통 임시 비밀번호·개별 활성화 번호로 시작하고, 약관 확인과 개인 비밀번호 변경을 완료합니다. 교사의 자리 IP는 관리 정보이며 IP만으로 로그인하지 않습니다. 관리자는 허용된 관리 화면에서 교직원 이름·IP·부서·직책과 특별실을 관리합니다.

자동 실행은 PC 전원 버튼을 누른 즉시가 아니라 Windows 로그인 뒤에 동작합니다. 자동 로그인은 개인 PC에서만 선택하세요. 다른 학교나 집에서 업무 서버에 접속하려면 학교가 승인한 VPN 등이 필요하며 Cloudflare 공개 앱 주소로 업무 서버를 대신 연결하지 않습니다.

## 9. Android와 iPhone은 따로 준비하세요

**Android:** 교직원은 Teacher APK, 학생·학부모는 Family라는 내부 식별자가 붙은 학교 소식 APK를 휴대폰에 전달합니다. APK는 Windows에서 실행하지 않습니다. 학교가 허용한 시험 기기에 설치하고 설치 앱의 추가 설치 권한은 시험 후 해제합니다. 교무실 앱에는 학교가 안내한 내부 업무/학교 소식 주소를, 학교 소식 앱에는 공개 HTTPS 주소를 설정합니다. 교직원 최초 활성화는 Windows에서 완료합니다.

학생은 학교 명부·승인 절차를 사용합니다. 학부모는 본인 임시계정에서 자녀 번호를 연결합니다. 현재 운영 기본은 학교 확인입니다. 앞서 정한 즉시 연결 방식을 운영하려면 학교가 `CodeOnly` 정책을 승인하고 해당 설정을 적용해야 합니다. 학생 계정으로 학부모가 로그인하는 방식은 아닙니다.

**iPhone:** Windows에서 APK를 IPA로 바꿔 설치할 수 없습니다. Mac·호환 Xcode·학교 Apple 개발자 계정·서명 인증서·앱별 프로파일을 준비해 교사용/학교 소식 앱을 각각 빌드하고 TestFlight로 시험해야 합니다. 개발자가 따라갈 명령은 [iOS 제작 안내](ios-operations.md)에 있습니다. 아직 서명 IPA와 실기기 설치는 완료하지 않았습니다.

## 10. 시험 순서와 정식 배포 전 남은 일

실제 개인정보 대신 가상 교직원·학생·학부모로 먼저 확인합니다.

1. 두 교직원 간 메시지·첨부·읽음·미읽음이 남은 메시지 회수.
2. 파일 제출 요청·제출·회수·재제출과 담당자의 제출 현황.
3. 특정 학급 공지를 교무메신저에서 발행하고 해당 학생·학부모·담임만 확인.
4. 공지 이미지 한 장당 200,000바이트 이하·최대 5장 제한과 권한.
5. 기초/일자별 시간표, 상담 개설·동시 예약 방지·취소, 특별실 예약. 급식 NEIS 연동은 현재 구현을 확인하지 못해 개발·검증 과제로 구분합니다.
6. 로그아웃·계정 중지·기기 재실행·자녀 추가·잘못된 번호·권한 없는 조회 차단.
7. 별도 시험 데이터의 백업·복원·졸업 파기·첨부 만료 및 재부팅 자동 실행.
8. 장애 시험 메일을 실제 수신하고, UPS 제조사 절차에 따라 안전 종료 확인.

지금 **앱을 완전히 닫았을 때의 FCM/APNs 푸시, 앱 관리자 다중 인증, 앱 내 탈퇴/삭제 신청 흐름**은 남아 있습니다. 앱을 닫아도 즉시 알림이 온다고 안내하지 마세요. 급식 NEIS 호출·화면 구현은 현재 소스에서 확인하지 못했습니다. 연동 구현부터 별도 개발·검증해야 하며 키만 입력하면 완료되는 상태가 아닙니다. 운영망·스마트폰·인증서·스토어 배포도 별도 검증해야 합니다.

학교 정전이면 서버 자체 이메일 감시도 멈춥니다. 정전·회선 장애 알림까지 받으려면 학교와 전원/회선이 다른 상시 외부 감시 환경이 필요합니다. UPS만 설치한다고 그 감시가 생기지는 않습니다. 업데이트·백업·복원·별도 감시의 상세 명령은 [서버 운영 안내](server-setup-school-tunnel.md)를 따릅니다.

학교가 배포한다고 결정하기 전까지 **1.0.0**을 유지합니다. 이 안내와 폴더 정리는 GitHub 릴리즈·학교 서버 공개·스토어 등록을 실행하지 않습니다.

## 11. 막혔을 때 어디를 확인하나요?

| 현상 | 먼저 확인할 것 |
| --- | --- |
| 설치 바로가기 대상이 없다고 함 | 이 폴더를 이동했는지, 개발자가 빌드·정리했는지. 원본 파일 존재 확인 |
| 서버 EXE가 없다고 함 | 서버 ZIP을 풀었는지, `C:\SchoolMessenger` 바로 아래 역할 폴더가 있는지 |
| 사전 점검이 `false` | `issues` 내용을 확인하고 실제 조건 해결. 확인값만 바꾸지 않음 |
| 서버는 켜졌는데 가입이 안 됨 | 약관 승인·정책 파일·가입 역할·학교 승인 상태 확인 |
| Cloudflare 터널은 연결됐는데 앱 오류 | 원본 서버 가동·8443·인증서 이름/신뢰·Host 설정 확인 |
| 학교 밖에서 교무실 메시지가 안 보임 | 내부 업무 주소·승인 VPN 여부 확인 |
| 재부팅 뒤 안 켜짐 | Windows 예약 작업의 계정·로그, cloudflared 서비스 설정 경로 확인 |
| 알림 메일이 안 옴 | SMTP 인증·스팸함·발신 정책 확인. 학교 정전이면 별도 감시 필요 |

오류를 문의할 때는 발생 시각·기기·작업·오류 문구를 알려 주세요. 메시지 본문, 학생 명부, 비밀번호, 터널 JSON·토큰, 인증서 개인키는 첨부하지 마세요.

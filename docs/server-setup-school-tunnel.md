# 학교 서버 설치·운영 안내

2026-10-14 · 배포 전 버전 1.0.0. Windows 컴퓨터 한 대를 사용하고, 학생·학부모용 서버는 Cloudflare Tunnel로 연결하는 준비안이다. 학교 서버의 i5-10500·8GB·Windows 11 x64 사양은 사진으로 확인했다. 사진의 메모리 규격은 DDR4이며 사용자 설명의 DDR3와 달라 추가 모듈 라벨 확인이 필요하다. Windows 에디션·망·도메인·인증서·메일 인증과 실제 부하는 아직 검증되지 않았다. 학교 서버에 작업·터널·방화벽을 등록하지 않았다.

## 1. 먼저 확인할 것

학교 서버에서 아래 파일을 실행하면 Windows 버전, CPU, RAM 규격·용량·사용 슬롯과 가상화 지원 여부를 출력한다. 이름·IP·비밀번호는 수집하지 않으며 설정을 변경하지 않는다.

```powershell
.\scripts\Inspect-ServerComputer.ps1 -OutputPath .\server-computer-check.json
```

확인 결과를 정보 담당자와 검토한다. 물리 컴퓨터 한 대에는 관리용 Windows 호스트, 교내 업무용 Windows VM, 학생·학부모용 Windows VM을 분리하는 구성을 권장한다. RAM은 호스트와 두 VM을 함께 계산해야 하므로 32GB를 초기 검토 기준으로 삼는다. 이는 실부하 검증 결과나 최소 요구 사양이 아니다. Hyper-V 지원과 게스트 Windows 사용 권한은 학교 담당자가 확인한다. [Microsoft Hyper-V 요구사항](https://learn.microsoft.com/en-gb/windows-server/virtualization/hyper-v/host-hardware-requirements)

두 프로그램을 같은 Windows 계정에서 실행하거나 폴더만 나누는 것은 침해 격리가 아니다. Hyper-V 기본 스위치에 VM을 연결하는 것만으로도 교내망 차단이 보장되지 않는다. 두 VM이 같은 호스트에 있으므로 정전·디스크 장애·호스트 침해에 대한 공통 위험도 남는다.

정보 담당자가 학생·학부모 VM의 교내 PC·업무 VM·관리 호스트 접근을 차단하고, 승인된 DNS·Cloudflare·메일 등 필요한 외부 통신만 허용하는 격리된 가상망을 구성한다. 교내 서버가 학교 소식 서버의 HTTPS 주소로 연결을 시작한다. 학교 밖에서 교내 업무 서버로 새 연결을 시작할 수 없게 한다. 가상망·방화벽은 이 배포 도구가 자동 변경하지 않는다. 실제 허용/차단 시험 후에만 `networkIsolationVerified`를 true로 기록한다.

## 2. 백업 장치와 UPS 준비

현재 둘 다 없으므로 운영 준비 미완료다. 별도 물리 백업 장치와 UPS를 확보한 뒤 설정한다. 다른 드라이브 문자나 다른 VM 디스크가 같은 SSD에 있으면 디스크 장애 대비 백업이 아니다.

- 각 서버가 자기 백업 폴더에만 접근하도록 권한을 나눈다. 외부 서버에 교내 업무 백업 접근 권한을 주지 않는다.
- 데이터와 백업 장치를 암호화한다. BitLocker 복구 키는 보호하는 장치와 따로 보관하고 실제 해제·복구 방법을 확인한다. [Microsoft 복구 키 안내](https://learn.microsoft.com/en-gb/windows/security/operating-system-security/data-protection/bitlocker/recovery-overview)
- 공유 백업 장치를 쓰면 승인된 SMB 인증·전송 암호화와 공유별 접근 제한이 필요하다. DB 원본은 각 VM의 로컬 디스크에 둔다.
- UPS 제조사 관리 프로그램으로 배터리 부족 때 VM과 호스트가 안전하게 종료되도록 설정한다. Hyper-V의 VM 자동 종료 작업은 정상 종료로, 자동 시작은 시간차를 두고 설정한다. BIOS/UEFI의 전원 복구 후 켜기 지원도 확인한다.
- 서버만 UPS에 연결해도 공유기·스위치가 꺼지면 앱은 접속할 수 없다. 유지하려는 네트워크 장비의 소비전력도 계산한다.

UPS 모델과 실제 배터리 시간이 없으므로 임의의 배터리 기준이나 강제 종료 명령을 등록하지 않는다. 운영 데이터가 없는 상태에서 제조사 절차에 따라 시험한 뒤 `upsInstalledAndTested`를 true로 기록한다. 암호화와 별도 물리 장치 확인 후 `backupDeviceAndEncryptionVerified`를 true로 기록한다. 이 두 확인값과 망 분리 값은 학교의 확인 기록이며 자동 증명이 아니다.

## 3. 프로그램·설정 설치

교내 VM에는 ZIP의 `server`, 학교 소식 VM에는 `announcement-server`를 설치한다. `scripts`, `config`, 이 안내도 보관한다. 각 VM에 관리자 권한이 없는 전용 Windows 운영 계정을 만들고 서버·작업·비밀 설정에 같은 계정을 사용한다. 관리자 작업은 정보 담당자가 수행한다.

각 서버마다 `config/server-operations.example.json`을 **운영 계정 전용 폴더**의 `server-operations.local.json`으로 복사한다. 실행 파일·스크립트 폴더에는 다른 일반 사용자의 쓰기 권한을 주지 않는다. 설정·비밀 폴더는 두 VM이 공유하지 않는다.

| 설정 | 교내 업무 VM | 학교 소식 VM |
| --- | --- | --- |
| `service` | `office` | `announcements` |
| `executable` | `server\SchoolMessenger.Server.exe` 전체 경로 | `announcement-server\SchoolMessenger.AnnouncementServer.exe` 전체 경로 |
| `dataDirectory` | 업무 데이터 전용 경로 | 학교 소식 데이터 전용 경로 |
| `backupDirectory` | 업무 서버 전용 별도 장치 경로 | 학교 소식 서버 전용 별도 장치 경로 |
| `certificateThumbprint` | 학교 업무 서버 인증서 지문 | 학교 앱 도메인 인증서 지문 |
| `allowedNetworks` | 실제 학교 허용 대역 | 빈 문자열 |
| `publicHost` | 외부 학교 앱 도메인 | 같은 학교 앱 도메인 |
| `port` | 예: 7443 | 예: 8443 |
| `tunnelOnly` | false | true |
| `attachmentBackup` | true | true |

인증서는 각 VM의 로컬 컴퓨터 인증서 저장소에 개인키와 함께 설치하고 운영 계정의 개인키 접근을 허용한다. 이름·유효기간·신뢰 체인을 확인한다. 업무 서버 인증서는 교사 PC·앱이 신뢰해야 한다. 학교가 확정한 약관 파일은 별도 `Registration__PolicyFile` 환경변수 등 기존 정책 설정으로 연결한다. 검토용 초안으로 운영 가입을 열지 않는다.

설정값과 폴더가 준비되면 각 VM의 전용 운영 계정에서 실행한다. 최초 관리자 비밀번호, 두 서버에 동일하게 등록할 연결 키, 메일 앱 비밀번호를 직접 입력한다. 현재 학교 서버의 값은 여기서 생성·설치하지 않았다.

```powershell
.\scripts\Configure-ServerOperations.ps1 -ConfigurationPath C:\SchoolMessengerOperations\server-operations.local.json -InitializeSecrets -Apply
.\scripts\Server-Operations.ps1 -ConfigurationPath C:\SchoolMessengerOperations\server-operations.local.json -Mode Preflight
```

비밀은 현재 Windows 계정의 DPAPI로 보호한다. 다른 계정·새 컴퓨터로 암호화 파일을 복사하는 것만으로는 복구되지 않는다. 연결 키·인증서 개인키·서버 암호화 키·운영 설정의 복구 자료를 학교가 승인한 별도 암호화 보관소에 관리해야 한다. 도구가 이 복구 자료를 자동 반출하거나 메일로 보내지 않는다. 최초 로그인 후 관리자 비밀번호를 바꾸고 초기 비밀번호의 별도 보관 필요성을 검토한다.

## 4. Cloudflare Tunnel 연결

학교 승인 후 학교가 소유·관리하는 도메인과 Cloudflare 계정을 사용한다. Cloudflare 관리 계정에 2단계 인증을 켜고 터널의 권한·토큰을 제한한다. 터널은 학교 소식 VM에만 설치한다. 교내 업무 VM의 터널은 만들지 않는다.

`config/cloudflared.example.yml`은 로컬 관리 터널 예시다. UUID·인증 파일 경로·도메인을 실제 값으로 바꾸고 Cloudflare 공식 절차에 따라 Windows 서비스로 등록한다. [Windows 서비스 설정](https://developers.cloudflare.com/tunnel/features/locally-managed-tunnels/as-a-service/windows/)

학교 소식 서버는 `https://127.0.0.1:8443`에서만 수신한다. 공개 주소는 Cloudflare가 제공한다. 터널의 원본 서버 이름과 인증서 이름을 맞추고 **인증서 검증을 끄지 않는다**. 학교 CA라면 신뢰 CA 설정도 필요하다. [HTTPS 원본 설정](https://developers.cloudflare.com/tunnel/troubleshooting/https-origins/)

공개 경로 `/health/operations`는 예시 라우트에서 404로 막는다. 운영 감시는 localhost에서 별도 감시 키로 인증한다. 앱 관리 API는 기존 네이티브 관리 창이 사용하므로 일괄 차단하면 승인 기능이 깨진다. 서버의 관리자 권한 검사를 유지하고 실제 관리자 로그인 보강·접근 제한을 출시 전에 검증한다. 앱 관리자용 2단계 인증은 이번 변경에 추가하지 않았으며 Cloudflare 관리 계정의 2단계 인증과 구분한다.

서버 응답은 `Cache-Control: no-store`를 사용한다. Cloudflare에도 `/api`, `/bridge`, `/health`를 캐시하지 않는 규칙을 적용하고 이를 덮어쓰는 Cache Everything·Worker 캐시를 사용하지 않는다. 권한 확인 없이 캐시된 공지·첨부나 오래된 정상 상태를 전달하지 않는지 실제 도메인에서 확인한다.

`tunnelOnly` 모드에서는 루프백 커넥터의 `CF-Connecting-IP`만 처리해 전체 이용자를 동일한 IP로 잘못 묶지 않도록 한다. 일반 `X-Forwarded-For`를 모든 외부 요청에서 신뢰하지 않는다. 커넥터 호스트에 승인되지 않은 로컬 사용자를 두지 않는다. Cloudflare의 방문자 IP 제거·덮어쓰기 설정과 다른 프록시/Worker 연결 여부도 시범 운영에서 확인한다. [Cloudflare 요청 헤더](https://developers.cloudflare.com/fundamentals/reference/http-headers/)

터널에는 외부 방향 7844번 통신이 필요하다. 학교에서 차단돼 있다면 승인을 받아야 하며 보안 정책을 우회하지 않는다. [방화벽 조건](https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/configure-tunnels/tunnel-with-firewall/)

공개 주소의 업로드는 서비스 제공자와 서버의 요청 크기·시간 제한을 함께 받는다. 학교 소식 서버 첨부는 JPG·PNG 한 장당 200,000바이트이며 요청 본문은 업로드 포장을 포함해 240,000바이트로 먼저 제한한다. 공지당 최대 5장이고 파일은 한 장씩 올린다. 교직원 파일의 100MiB 상한을 외부 서버에도 적용된다고 가정하지 않는다. 교직원 개인 업무 파일은 교내 서버에 유지한다. 실제 도메인과 서비스 제공자 설정에서 경계 용량을 시험한다.

앱은 기존 Android·iOS 앱을 사용한다. Vercel·Supabase나 사용자용 웹앱으로 바꾸지 않는다. Cloudflare 중계와 개인정보 흐름·저장 위치는 학교 심의 자료에도 반영한다.

## 5. 개인 이메일 알림

개인 이메일 주소는 공개 저장소나 예시 파일에 넣지 않는다. 제공받은 수신 주소는 로컬 전용 설정에만 기록한다. `alerts.sender`에 발신 Gmail 계정, `alerts.recipient`에 받을 주소를 입력한다. `smtp.gmail.com`, 587, TLS를 사용한다. 발신 계정의 2단계 인증과 앱 비밀번호 지원을 확인한다. [Google 앱 비밀번호 안내](https://support.google.com/mail/answer/185833)

앱 비밀번호는 위 비밀 등록 화면에서 직접 입력한다. 채팅·명령 인수·소스에 붙여 넣지 않는다. `alerts.enabled`는 인증 준비 후 true로 바꾼다. 아래 명령으로 시험 메일을 보내 실제 수신함을 확인한 뒤에만 `alerts.deliveryVerified`를 true로 기록한다. SMTP 서버의 수락을 실제 수신함 도착으로 표현하지 않는다.

```powershell
.\scripts\Server-Operations.ps1 -ConfigurationPath C:\SchoolMessengerOperations\server-operations.local.json -Mode TestEmail
```

감시는 분마다 서버 연결·백업 실패/30시간 경과·백업 사본 존재·데이터 저장 공간·터널 서비스·외부 접속을 확인한다. 장애 변경 시 알리고 같은 장애는 기본 6시간 간격으로 다시 안내한다. 복구는 한 번 알리고 발송 실패는 다음 점검에서 재시도한다. 메일에는 서버 종류·고정된 한국어 상태 안내·한국 시각만 넣으며 학생·학부모 이름·메시지·파일명·비밀 값은 넣지 않는다.

운영 준비 점검을 통과하면 다음을 전용 표준 계정에서 실행한다. 작업 등록 권한이 막히면 정보 담당자가 **같은 실행 계정**으로 등록을 지원한다. DPAPI 비밀을 만든 계정과 작업 계정이 달라지면 안 된다.

```powershell
.\scripts\Configure-ServerOperations.ps1 -ConfigurationPath C:\SchoolMessengerOperations\server-operations.local.json -RegisterTasks -Apply
.\scripts\Server-Operations.ps1 -ConfigurationPath C:\SchoolMessengerOperations\server-operations.local.json -Mode Start
```

서버 내부 감시는 서버가 꺼지면 함께 멈춘다. 학교 정전·회선 장애까지 이메일로 받으려면 **학교와 전원·회선을 공유하지 않는 상시 감시 PC/운영 환경**이 필요하다. 그 환경에는 학교 소식 서버의 공개 주소와 SMTP 설정만 등록하고 서버 연결 키·관리자 비밀번호는 넣지 않는다.

```powershell
.\scripts\Configure-ServerOperations.ps1 -ConfigurationPath C:\ExternalMonitor\server-operations.local.json -InitializeSecrets -ExternalMonitorOnly -Apply
.\scripts\Configure-ServerOperations.ps1 -ConfigurationPath C:\ExternalMonitor\server-operations.local.json -RegisterTasks -ExternalMonitorOnly -Apply
```

별도 감시 환경은 아직 확보하지 않았으며 Cloudflare 서비스 자체를 이용한 관제도 자동 신청하지 않았다. SMTP 인증·실제 수신·감시 PC의 절전 해제를 별도 확인한다.

## 6. 백업·복구 시험

DB는 기존 SQLite 온라인 백업과 무결성 검사로 저장하고 최근 14일 기준으로 정리한다. `attachmentBackup:true`는 유효한 첨부의 관리 사본을 `backupDirectory\attachments`에 저장한다. UUID·원래 만료 시각·SHA256만 기록하며 파일별 과거 버전을 쌓지 않는다. 원본과 백업 사본은 같은 만료 시각을 사용하고 정리 주기/재시작/첨부 복원 때 만료 사본을 제거한다. 서버가 꺼져 있거나 백업 장치가 분리됐으면 물리 정리는 다음 연결·가동 때 진행되며 즉시 파기를 보장하지 않는다.

새 DB·첨부 백업은 애플리케이션 자체 암호화가 아니라 **학교에서 검증한 암호화 장치와 접근 권한**으로 보호한다. 장치가 연결되지 않으면 실패로 기록한다. 최초 백업 시간은 첨부량에 따라 길어질 수 있다.

졸업 파기는 앱이 관리하는 별도 경로의 DB 백업에도 적용한다. 이미 반출한 백업·개인 단말 사본은 학교가 별도로 파기해야 한다. 정상 파일 내용의 자유 입력 개인정보가 구조화된 졸업 계정 파기로 자동 제거된다고 보장하지 않는다.

복원 시험은 가상 계정과 별도 시험 데이터로 한다. 서버를 정지하고 기존 DB 복원 도구로 DB를 복원한다. 권한 검토 대기 상태에서 아래 도구를 실행하면 아직 만료되지 않은 백업 첨부를 SHA256 확인 후 복원한다. 기존 파일과 내용이 다르면 덮어쓰지 않는다.

```powershell
.\scripts\Server-Operations.ps1 -ConfigurationPath C:\SchoolMessengerOperations\server-operations.local.json -Mode RestoreAttachments
```

학교 명부·탈퇴·보호자 관계·권한을 대조한 뒤 기존 PC 관리 창에서 복원 검토를 완료한다. 실제 디스크 교체·키 복구·정전 시험은 아직 하지 않았다.

## 7. 업데이트 절차

운영체제·백신·터널은 정보 담당자가 업데이트 일정과 재시작 시간을 관리한다. 이 도구는 Windows Update 정책·자동 재부팅·학교 방화벽을 변경하지 않는다.

메신저 서버는 학교가 승인한 GitHub 릴리즈 ZIP을 내려받아 신뢰할 수 있는 경로에서 SHA256을 확인한다. 현재 배포 전 1.0.0이며 릴리즈·태그를 새로 발행하지 않는다. 현재 배포본에는 학교 배포 서명이 없어 체크섬 자체를 서명 검증으로 표현하지 않는다.

```powershell
.\scripts\Stage-ServerUpdate.ps1 -ConfigurationPath C:\SchoolMessengerOperations\server-operations.local.json -Package C:\Downloads\server-package.zip -ExpectedSha256 <확인한64자리SHA256> -VersionDirectory C:\SchoolMessengerVersions\new-version
```

위 실행은 검사만 한다. 실제 교체는 이용자에게 점검 시간을 안내하고 자동 시작 작업/서버를 정지한 뒤 같은 인수에 `-Apply`를 추가한다. 도구는 실행 중인 서버에서 백업을 거부하고, 오프라인 백업 성공 후 새 폴더로 추출한다. 이전 숫자 버전·경로 탈출·비밀/DB 포함 패키지를 거부하고 운영 설정의 실행 경로만 바꾼다. 데이터·비밀·키 경로는 유지하며 이전 설정을 남긴다. 새 버전으로 시작하고 로그인·공지·첨부·메일·재시작을 확인한다.

새 코드가 이미 DB를 이관했다면 이전 실행 파일만 복구하는 것으로 충분하지 않을 수 있다. 실패 시 서비스를 정지하고 검증된 DB 복원·권한 대조 절차를 따른다. DB를 자동 되돌려 탈퇴한 계정이나 폐기한 개인정보를 복구하지 않는다.

## 8. 이번 검증과 남은 준비

`tests/server-operations.mjs`와 PowerShell 시험은 가상 자료로 수행한다. 백업/복원/만료/졸업 백업 파기, 감시 키·상태, 메일 중복 방지·재시도, ZIP 검사와 준비 미완료 차단을 검사한다. 메일은 모의 발송이며 실제 개인 이메일로 보내지 않는다. 실제 학교 VM·방화벽·터널·인증서·Gmail·UPS와 외부 관제는 검증 전이다. 전체 앱 관리자 MFA와 실제 스마트폰·종료 상태 푸시도 출시 전 준비 항목으로 남는다.

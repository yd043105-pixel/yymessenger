# 처음 설치부터 실제 사용까지 — 최신 작업 순서

2026-10-28 · 정식 출시 전 **1.0.0** · 통합 관리자 프로그램

학교 운영 모드로 전환하기 전에 [학교별 보안 도입 검토](security-regulatory-controls-2026-10-28.md)를 진행한다. 운영 설정의 schoolId·securityReviewFile과 역할별 검토 기록·증빙을 준비해야 하며, 이 PC 시험 모드는 실제 운영 승인을 의미하지 않는다.

서버 PC는 설치 후 **여양고 메신저 서버 → 관리자 프로그램**을 연다. **여양메신저 관리자**의 왼쪽 서버 메뉴에서 가동하고, **교직원·권한 / 특별실 / 학급·분반 / 학생·학부모 / 약관·개인정보** 등에서 관리자 로그인 후 설정한다. 교사용 메신저를 별도로 열 필요가 없다. 학교 소식 서버는 별도 관리자 로그인이 필요하다. 설치 시 바탕화면 바로가기와 Windows 로그인 후 자동 실행을 선택한다. 아래 운영 스크립트 명령은 담당자용 기술 참고이며 이 PC 시험은 문서 마지막의 관리 화면 절차를 따른다.

현재 저장소는 하나로 유지한다. **자동업데이트 제작은 나중에 진행**한다. 지금은 설치 EXE를 직접 실행하는 방식이다. 아래 절차는 학교 서버에 설치·공개가 끝났다는 의미가 아니며, 처음에는 가상 계정으로 시험한다.

## 1. 전체 순서

| 순서 | 어디에서 | 할 일 | 완료 확인 |
| --- | --- | --- | --- |
| 1 | 학교 담당자와 서버 PC | 사양·서버 역할 분리·학교망·인증서·백업·UPS·메일 준비 | 실제 운영 구성이 정해짐 |
| 2 | 각 서버 역할의 Windows | 서버 설치 EXE 실행 | 선택한 서버 프로그램이 설치됨 |
| 3 | 각 서버 역할의 Windows | 운영 설정·비밀값·약관·파일 검사 준비, 사전 점검 | 준비 점검 통과와 실제 파일 검사 성공 |
| 4 | 학교 소식 서버 → 업무 서버 | 서버 시작, Cloudflare와 두 서버 연결 확인 | 학생·학부모 서버와 교내 서버가 각각 정상 응답 |
| 5 | 서버 관리자 PC | 관리자 로그인, 시험 계정·권한·자동검색·자동 구동 설정 | 계정과 두 서버 연결이 준비됨 |
| 6 | 교사 PC 2대 | 메신저 설치·활성화·송수신 시험 | 서로 메시지·파일을 주고받음 |
| 7 | Android / Mac·iPhone | 역할별 앱 설치·제작·연결 시험 | 각 역할의 허용 자료만 확인됨 |
| 8 | 전체 | 재부팅·장애 메일·백업 복원·권한 회수 시험 | 학교가 시험 배포 여부를 결정 |

도메인 준비와 Mac·Apple 서명 준비는 1번부터 병행할 수 있다. 휴대폰의 실제 서버 연결은 서버와 계정을 준비한 뒤 한다.

## 2. 필요한 설치파일

개발 PC 기준 폴더는 다음과 같다. 다른 기기로는 **실제 파일과 해당 폴더의 SHA256SUMS.txt**를 함께 복사한다.

`C:\Users\user\Documents\ChatGPT\여양고 온라인 교무실\01_설치파일`

| 대상 | 위 폴더 안에서 가져갈 파일 |
| --- | --- |
| 서버 PC | `01_서버PC\yymessenger-1.0.0-server-setup-win-x64.exe` |
| 일반 교사 PC | `02_교사PC\yymessenger-1.0.0-setup-win-x64.exe` |
| 교사용 Android | `03_안드로이드\kr.school.yeoyang.teacher-Signed.apk` |
| 학생·학부모 Android | `03_안드로이드\kr.school.yeoyang.family-Signed.apk` |
| Mac 제작 담당자 | `04_iOS\yymessenger-1.0.0-ios-build-source.zip` |

`server-setup`은 관리자 프로그램과 서버용, `setup`은 일반 교사 PC용이다. 서버 PC에 교사용 EXE를 또 설치할 필요는 없다. 별도 관리자 PC는 `server-setup`에서 **관리자 전용 PC**를 선택한다. 사용자 지정에서 서버 역할을 해제하고 관리자만 설치할 수도 있다. 서버 ZIP은 수동 배치용으로 남겨 두며 **EXE로 설치할 때 따로 압축 해제하지 않는다.** Windows 프로그램은 런타임을 포함하므로 개발용 .NET SDK·Node.js·소스 전체를 학교 PC에 설치하지 않는다.

## 3. 실제 서버 설치 전에 준비할 것

현재 학교 서버는 Windows 한 대이고, RAM·Windows 에디션·가상화 가능 여부를 확인해야 한다. 기존에 별도 백업 장치와 UPS가 없다고 안내받았으므로 두 가지도 준비 항목이다.

1. 학교 담당자와 외부 앱 서버 운영 및 학교 회선 사용을 확인한다.
2. 교내 업무 서버와 외부 학교 소식 서버의 역할 분리를 정한다. 한 물리 PC를 쓰더라도 Windows 가상 환경·계정·망·저장소의 분리 가능 여부를 확인한다. EXE에서 두 역할을 골라 같은 Windows에 설치하는 것만으로 침해가 격리되지는 않는다. 자세한 조건은 [서버 운영 준비](server-setup-school-tunnel.md)를 따른다.
3. 업무 서버의 교내 HTTPS 주소, 학교 소식 서버의 앱 전용 공개 HTTPS 도메인을 정한다. 후자는 기존 학교 홈페이지 주소 자체를 바꾸라는 뜻이 아니다.
4. PC와 휴대폰이 신뢰하는 HTTPS 인증서, 전용 표준 Windows 운영 계정, 실제 허용 교직원망, 승인된 악성코드 검사 연동을 준비한다.
5. 별도 암호화 백업 장치·UPS·실제 메일 발신 계정과 인증 방법을 준비한다. 수신처는 `yd043105@gmail.com`이다. 비밀번호는 채팅이나 일반 JSON에 쓰지 않는다.
6. 학교가 운영 약관과 개인정보 안내를 확정한다. 운영 가입이 차단됐다고 초안의 승인 플래그만 임의로 바꾸지 않는다.

파일 검사·서버 포트가 학교 보안 프로그램과 충돌하면 담당자가 원인을 확인한다. 기존 백신·보안 프로그램·방화벽을 일괄 해제하지 않는다. 아직 운영 준비가 없다면 마지막의 **이 PC에서만 화면 시험**부터 할 수 있다.

## 4. 서버 PC에 설치하기

이후 절차의 ‘각 서버 역할’은 담당자가 분리해 준비한 각각의 Windows 환경을 뜻한다. 두 VM이면 아래 경로 이름이 같아도 서로 다른 파일이다.

1. 서버 EXE를 실행하고 Windows 관리자 권한 요청을 확인한다. 현재 시험본의 학교 배포자 코드서명은 아직 적용 전이다.
2. 시작 → 설치 안내 → 설치 위치 → 서버 역할 선택 → 시작 메뉴 → 설치 → 완료 순서로 진행한다.
3. 업무 역할에서는 **교내 업무 서버 PC**, 외부 앱 역할에서는 **학생·학부모용 학교 소식 서버 PC**를 선택한다.
4. 기본 설치 위치는 `C:\Program Files\YeoyangMessengerServer`다. 완료 후 ‘서버 설치·설정 안내’를 연다.
5. 설치 뒤 설정과 서버 실행은 **전용 표준 Windows 운영 계정**으로 진행한다. 설치를 위한 Windows 관리자 권한과 메신저의 `admin` 계정은 다르다.

| 용도 | 기본 경로 |
| --- | --- |
| 업무 서버 EXE | `C:\Program Files\YeoyangMessengerServer\server\SchoolMessenger.Server.exe` |
| 학교 소식 서버 EXE | `C:\Program Files\YeoyangMessengerServer\announcement-server\SchoolMessenger.AnnouncementServer.exe` |
| 관리자 프로그램 | `C:\Program Files\YeoyangMessengerServer\admin\SchoolMessenger.exe` · 시작 메뉴 바로가기 사용 |
| 운영 도구 | `C:\Program Files\YeoyangMessengerServer\scripts` |
| 운영 설정 양식 | `C:\Program Files\YeoyangMessengerServer\config\server-operations.example.json` |
| 각 역할의 실제 운영 설정 | `C:\SchoolMessengerOperations\server-operations.local.json` |
| 실제 비밀 파일 | 같은 운영 설정 폴더의 `server-operations.secrets.xml` |

운영 데이터는 프로그램 폴더 밖에 둔다. 업무 데이터는 `C:\SchoolMessengerData\office`, 학교 소식 데이터는 `C:\SchoolMessengerData\announcements`를 기준으로 한다. 기존 ZIP 설치에서 전환한다면 데이터를 새로 초기화하지 않고 기존 데이터·키·첨부·백업·설정 경로를 유지한다. [기존 서버 전환·제거 안내](server-installer.md)를 먼저 확인한다.

## 5. 운영 설정과 최초 비밀값 준비

각 역할에서 설정 양식을 실제 운영 설정 경로로 복사하고 실제 값을 넣는다. JSON의 Windows 경로는 역슬래시를 두 번 적는다. 예를 들어 실행 경로는 `C:\\Program Files\\YeoyangMessengerServer\\server\\SchoolMessenger.Server.exe`다.

| 항목 | 업무 역할 | 학교 소식 역할 |
| --- | --- | --- |
| `service` | `office` | `announcements` |
| `executable` | 위 업무 EXE 경로 | 위 학교 소식 EXE 경로 |
| `dataDirectory` | `C:\SchoolMessengerData\office` | `C:\SchoolMessengerData\announcements` |
| `backupDirectory` | 별도 장치의 업무 전용 폴더 | 별도 장치의 학교 소식 전용 폴더 |
| `port` | 기본 7443 | 기본 8443, 실제 점유 확인 후 결정 |
| `tunnelOnly` | false | true |
| `allowedNetworks` | 담당자가 정한 교직원망 CIDR | 빈 문자열 |
| `publicHost` | 학교 소식 공개 도메인, https:// 제외 | 동일한 학교 소식 공개 도메인 |
| `resourceProfile` | `low-memory` | `low-memory` |

인증서 지문·메일·백업 경로도 실제 값으로 채운다. 오류 자동 접수·메일을 사용할 때는 `feedbackEmailEnabled`, `automaticBugReportsEnabled`, `alerts.enabled`를 true로 하고 실제 발신 계정·수신처·SMTP를 설정한다. `alerts.deliveryVerified`는 시험 메일 도착을 확인한 뒤 true로 기록한다. 격리·UPS·백업 확인값도 실제 확인 후에만 변경한다.

학교가 확정한 정책을 `C:\SchoolMessengerOperations\registration-policy.local.json`로 준비한다. 각 서버의 `appsettings.Production.json`에는 `Registration.PolicyFile`이 이 파일을 가리키도록 담당자가 기존 설정에 병합한다. Program Files의 설정 편집은 Windows 관리자 작업이며, 기존 파일을 통째로 덮어쓰지 않는다. 상세 문구와 운영 조건은 [약관 설정](registration-policy-operations.md)을 따른다.

전용 표준 Windows 운영 계정에서 일반 PowerShell을 열고 다음을 진행한다. 아래 최초 초기화는 **기존 비밀 파일이 없을 때 한 번만** 실행한다.

```powershell
Set-Location 'C:\Program Files\YeoyangMessengerServer'
.\scripts\Configure-ServerOperations.ps1 -ConfigurationPath 'C:\SchoolMessengerOperations\server-operations.local.json' -InitializeSecrets -Apply
.\scripts\Server-Operations.ps1 -ConfigurationPath 'C:\SchoolMessengerOperations\server-operations.local.json' -Mode TestEmail
```

첫 명령에서 관리자 비밀번호 12~128자, 두 서버에 동일한 무작위 연결 키 32자 이상, 발신 메일 인증 정보를 숨김 입력한다. 관리자 비밀번호는 `12345678`을 쓰지 않는다. 백업 폴더는 먼저 실제 장치에 준비한다. 두 번째 명령은 실제 시험 메일을 보내므로 수신함·스팸함에서 확인하고 설정의 수신 확인값을 갱신한다.

기존 설정·비밀 파일이 있는 서버는 초기화를 반복하지 않는다. 다른 Windows 계정에서 비밀 파일만 복사해 사용하는 방식도 지원하지 않는다.

## 6. 서버를 시작하고 Cloudflare 연결하기

각 역할의 전용 운영 계정에서 사전 점검한다.

```powershell
Set-Location 'C:\Program Files\YeoyangMessengerServer'
.\scripts\Server-Operations.ps1 -ConfigurationPath 'C:\SchoolMessengerOperations\server-operations.local.json' -Mode Preflight
```

`ready: true`와 빈 문제 목록을 확인한다. 이 결과가 실제 악성코드 검사 성공·약관 전문 검토·외부 접속 성공까지 대신하지는 않는다. 해당 항목은 따로 확인한다.

준비가 끝나면 **학교 소식 서버부터**, 그다음 업무 서버를 각각 시작한다.

```powershell
.\scripts\Server-Operations.ps1 -ConfigurationPath 'C:\SchoolMessengerOperations\server-operations.local.json' -Mode Start
```

서버가 실행 중인 창은 유지한다. 같은 서버를 두 번 실행하지 않는다. 처음 시작에 사용한 관리자 비밀번호와 데이터 경로를 기록한다.

학교 소식 서버의 외부 연결은 다음 순서로 준비한다.

1. 학교 소유 앱 전용 도메인과 Cloudflare 계정의 2단계 인증을 준비한다.
2. 학교 소식 역할에 `cloudflared`를 배치하고 로그인·터널 생성·앱 도메인 연결을 진행한다.
3. 설치 폴더의 `config\cloudflared.example.yml`을 기준으로 실제 설정을 `C:\SchoolMessengerOperations\cloudflared.yml`에 작성한다. UUID·자격 증명 파일·도메인·원본 포트를 실제 값으로 바꾼다.
4. 원본은 같은 역할의 `https://127.0.0.1:실제포트`로 연결한다. 인증서 이름과 호스트를 맞추고 `noTLSVerify: false` 및 운영 감시 경로의 외부 차단 규칙을 유지한다.
5. 터널을 실행해 실제 외부 HTTPS 접속을 확인하고 Windows 서비스 자동 시작을 구성한다.
6. 업무 서버에서 학교 소식 서버 연결을 확인한다. 업무 서버 설정의 `publicHost`와 두 서버 연결 키가 일치해야 한다.

명령어·Cloudflare 입력값은 [설치·테스트 상세 안내의 3-7](installation-and-test-order.md)에 있다. 그 문서의 `C:\SchoolMessenger\config` 대신 이번 EXE 설치 위치인 `C:\Program Files\YeoyangMessengerServer\config`를 사용한다. 업무 서버의 7443은 터널에 공개하지 않는다. `localhost`를 휴대폰에 입력하면 학교 서버가 아닌 휴대폰 자신을 가리킨다.

## 7. 관리자 프로그램에서 할 일

1. 시작 메뉴의 **여양고 메신저 서버 → 관리자 프로그램**을 연다. 왼쪽 서버 메뉴에서 필요한 서버를 먼저 가동한다.
2. **교직원·권한** 또는 **운영 현황**에서 담당자가 정한 업무 서버의 HTTPS 주소, 아이디 `admin`, 준비한 별도 관리자 비밀번호로 로그인한다. 같은 창에서 업무를 처리한다.
3. 가상 교사 A·B·C부터 이름·부서·직책·권한·담임 정보를 등록한다. 좌석 IP는 선택 관리 정보다. 동명이인은 서로 다른 이름 아이디를 배정한다.
4. 특별실 목록, 학급·담임·교사 연결, 수업·교육과정 업무 권한을 설정한다.
5. 학생·학부모 관리에서 학교 소식 서버의 별도 주소와 관리자 계정으로 연결한다. 가상 학급·학생·학부모를 먼저 준비하고 가입·자녀 연결을 시험한다. 대량 등록 기능이 이미 완성됐다고 가정해 전체 명부를 올리지 않는다.
6. **운영 현황 → 시작·연결 → 교내 서버 자동검색 설정**에서 정상 내부 HTTPS 주소를 저장한다. 필요한 경우 담당자가 UDP 45983을 교직원망 범위에 한정해 허용한다. 다른 VLAN이면 직접 주소 입력을 쓴다.
7. 실제 준비 점검을 통과한 뒤 **서버 PC 재시작 후 자동 구동 설정**에서 해당 역할의 운영 JSON을 선택해 등록한다. 두 역할을 각각 준비한다. 자동 구동 등록은 서버를 새로 하나 더 실행하는 기능이 아니다.

로그인 전부터 서버를 가동해야 하는 기술 담당자는 해당 운영 계정에서 다음으로 시작·감시를 등록할 수 있다. 관리자 프로그램의 자동 시작과 중복 등록하지 않고 원래 경로·계정을 확인한다. 등록에는 비밀 파일을 만든 Windows 운영 계정의 인증 정보가 필요하다. 운영 계정 비밀번호 변경 시 예약 작업 인증 정보도 갱신한다.

```powershell
Set-Location 'C:\Program Files\YeoyangMessengerServer'
.\scripts\Configure-ServerOperations.ps1 -ConfigurationPath 'C:\SchoolMessengerOperations\server-operations.local.json' -RegisterTasks -Apply
```

## 8. 일반 교사 PC에서 할 일

1. 교사용 EXE를 복사해 설치한다. 서버용 EXE·서버 ZIP은 교사 PC에 설치하지 않는다.
2. 메신저 실행 → 서버 자동검색 → 학교가 안내한 HTTPS 주소 확인 → 사용을 선택한다. 검색되지 않으면 주소를 직접 입력한다.
3. 본인 이름 아이디와 초기 비밀번호 `12345678`로 로그인한다.
4. 관리자가 개별 전달한 활성화 번호·약관 동의·새 개인 비밀번호 12~128자를 입력한다. 완료 후 이름과 개인 비밀번호로 다시 로그인한다.
5. 본인 전용 Windows 계정이면 자동 로그인과 Windows 로그인 시 메신저 자동 실행을 선택한다. 초기 비밀번호 상태에서는 업무 사용·자동 로그인을 완료할 수 없다.
6. 두 번째 PC에 다른 가상 교사로 로그인하고 쪽지·첨부·읽음·미읽음 회수를 시험한다.

교사 PC의 ‘메신저 자동 실행’과 서버 PC의 ‘서버 자동 구동’은 다른 설정이다. 자동 로그인도 세션 만료·로그아웃·관리자 초기화 때에는 다시 로그인해야 한다.

## 9. 휴대폰에서 할 일

**Android**

1. 교사에게는 teacher APK, 학생·학부모에게는 family APK를 전달한다. 내부 파일명 family는 제품명이 아니다.
2. 학교가 허용한 시험 설치 방법으로 파일 앱에서 APK를 연다. 현재는 Debug 시험용이며 Play스토어 출시본이 아니다. 보안 차단을 우회하거나 Play Protect 전체를 끄지 않는다.
3. 교사용 앱은 정상 업무 서버 HTTPS 주소로 연결한다. 업무 기능은 교내 Wi-Fi 또는 승인된 VPN에서 시험한다.
4. 학생·학부모 앱은 학교 소식 서버의 공개 HTTPS 주소로 연결한다. 학교 밖 이동통신에서도 확인한다.
5. 교사 로그인, 학생 가입·초대, 학부모 임시 가입·자녀 연결, 다른 사람의 자료 차단을 가상 계정으로 시험한다.

**iPhone**

1. iOS ZIP은 Mac 전달용 최신 소스다. 아이폰에 바로 설치하는 IPA가 아니다.
2. Mac·Xcode·Apple 서명 인증서·역할별 프로파일을 준비하고 교사용·학생학부모용을 각각 빌드한다.
3. TestFlight로 설치한 뒤 Android와 같은 서버·권한 시나리오를 확인한다. [iOS 제작 안내](ios-operations.md)를 따른다.

실제 Android·iPhone 동작은 현장 시험이 필요하다. 종료된 앱의 OS 푸시는 현재 완료된 것으로 보지 않는다.

## 10. 실제 사용 전에 통과할 시험

| 시험 | 확인할 내용 |
| --- | --- |
| 메시지 | A→B 송수신·파일, B 로그아웃 중 전송 후 재접속 수신 |
| 회수 | B·C 중 한 명만 읽으면 회수 가능, 모두 읽으면 거부 |
| 작성 | 나에게 전송·예약 전송·초안 목록·서식·파일 드롭 |
| 제출 | 답장 없이 제출, 본인 회수·재제출, 보낸 사람 현황 일치 |
| 할 일 | 기한 포함 메시지에서 후보 확인 후 선택 등록, D-Day 일치 |
| 대상별 공지 | 대상 학생·학부모·담임은 열람, 미대상 계정은 열람 거부 |
| 시간표·업무 | 기초/일자별 변경, 특별실 7교시까지, 상담 야간 포함, 수강신청 1·2학년만 |
| 계정 | 비밀번호 초기화·중지 후 기존 PC·앱 세션 차단 |
| 운영 | 재부팅 후 Windows 로그인 전 접속, 실제 메일 수신, 별도 시험 환경 백업 복원 |

자료는 서버 파일 폴더에 직접 복사하지 않고 앱의 업로드 화면에서 올린다. 시간표는 담당자가 미리보기·교사·학급 연결 후 게시하고 변경 파일이 없는 날에는 기초시간표가 보이는지 확인한다.

기존 개발 PC의 시간표 예시는 `C:\Users\user\Downloads\기초시간표 예시.xlsx`, `C:\Users\user\Desktop\24년메신저다운방\2026. 10. 8. 목요일 시간표 - 1학년.xlsx`다. 다른 PC에는 개인정보를 제거한 사본을 복사해 선택한다. 가정통신문을 포함한 학생·학부모 공지 첨부는 JPG/PNG 한 장 200,000바이트 이하, 공지당 최대 5장이다.

## 11. 설치 후 평소 사용과 업데이트

서버 PC는 계속 가동하고 준비된 자동 구동·백업·장애 알림을 확인한다. 교사는 Windows 로그인 후 메신저에서 받은 메시지·할 일·시간표를 확인하고, 권한이 있는 업무 담당자가 공지·시간표·상담·특별실·수강신청을 처리한다. 학생·학부모는 자기 대상 공지·시간표·상담 등을 앱에서 확인한다.

지금은 새 릴리즈가 생겨도 자동 설치되지 않는다. 업데이트할 때 교사 PC는 메신저를 완전히 종료한 뒤 새 교사용 EXE를 실행한다. 서버는 정상 백업 확인 → 해당 경로의 시작·감시 중지 → 서버 종료 → 새 서버 EXE로 같은 경로 업데이트 → 점검·기동 → 감시 복원 순서다. [서버 업데이트 상세](server-installer.md)를 따른다. 최초 정식 출시 지시 전에는 1.0.0을 유지한다.

## 선택: 이 PC에서만 화면부터 시험하기

운영 준비 전 가상 데이터로 화면만 확인할 때 사용한다. 다른 PC·휴대폰은 연결할 수 없고, 파일 악성코드 검사를 생략하는 개발 체험 모드다. 실제 운영용으로 사용하지 않는다.

1. 이 PC에 서버 EXE로 ‘교내 업무 서버 PC’를 설치한다.
2. 시작 메뉴의 **여양고 메신저 서버 → 관리자 프로그램** 또는 **여양메신저 관리자** 바탕화면 바로가기를 연다.
3. 교내 업무 서버에서 **이 PC 시험 · 외부 접속 없음**을 선택한다. 화면의 시험 데이터 경로를 확인하고 최초 관리자 비밀번호 12~128자를 두 번 입력한 뒤 **서버 시작**을 누른다. 비밀번호는 설정 파일에 저장하지 않는다.
4. 실행 중으로 바뀌면 **관리자 로그인**을 누르고 같은 창에서 `admin`, 방금 정한 비밀번호로 로그인한다. 시험 주소는 `http://localhost:5080`이다. 왼쪽 **교직원·권한 / 특별실 / 학급·분반** 등에서 설정한다.
5. 마치면 관리 화면에서 **서버 중지**를 누른다. X는 알림 영역으로 숨기며, 완전 종료는 알림 영역의 **서버 중지 후 프로그램 종료**를 사용한다. 시험 데이터는 화면에 표시된 경로에 남는다. 기존 `C:\SchoolMessengerTrial\office`가 있으면 재사용하며, 새 시험은 사용자 앱 데이터 아래에 저장한다.
6. 첫 가동 완료 후 **관리 프로그램을 열면 이 서버도 자동 시작**을 선택할 수 있다. 왼쪽 아래 **Windows 로그인 시 관리자 프로그램 자동 실행**은 관리 창을 여는 설정이다. 로그인 전 자동 기동은 운영 설정의 기존 예약 작업이며, 활성 감시 작업이 있는 서버는 운영 담당자가 전환한 뒤 관리한다.

두 역할을 한 PC에서 함께 시험하려면 설치의 사용자 지정에서 두 서버 역할을 선택한다. 시험 연결 키는 같은 Windows 계정의 DPAPI로 보호해 공유하며 서로 다른 데이터 폴더와 루프백 포트 5080·5081을 쓴다. 실제 외부 서비스 운영의 망·운영체제 격리를 대체하지 않는다.

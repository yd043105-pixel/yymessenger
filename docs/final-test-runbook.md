# 시험 설치 순서 — 서버 PC와 교사 PC

2026-10-24 · 정식 출시 전 **1.0.0**. 설치형 Windows 메신저, 교사용 앱, 학생·학부모용 앱을 연결한다. 웹브라우저를 켜서 사용하는 서비스가 아니다.

**서버 설치 EXE 추가:** 새 서버 PC에서는 [서버 설치 마법사 안내](server-installer.md)를 먼저 따른다. `01_서버PC\yymessenger-1.0.0-server-setup-win-x64.exe`를 실행하면 역할을 선택해 설치할 수 있다. 기본 위치는 `C:\Program Files\YeoyangMessengerServer`이고 업무 서버 선택 시 관리자 메신저가 포함된다. 아래 `C:\SchoolMessenger` 경로와 ZIP 배치는 수동 설치 예시다. EXE 설치자는 실행·scripts·config 경로를 실제 설치 위치로 바꾸고, 데이터·백업·운영 설정 경로는 유지한다. 이후 서버 기동·계정·PC·휴대폰 시험 순서는 동일하다.

## 1. 복사할 파일

현재 개발 PC의 기준 폴더는 `C:\Users\user\Documents\ChatGPT\여양고 온라인 교무실\01_설치파일`이다. 이 안의 기기별 폴더에는 **실제 설치파일**을 넣는다. 해당 폴더를 USB 등 학교가 허용한 방법으로 복사하면 된다. 예전 `.lnk` 바로가기나 날짜별 중간 빌드는 전달하지 않는다.

| 대상 | 복사할 폴더와 파일 | 용도 |
| --- | --- | --- |
| 서버 PC · 기본 설치 | `01_서버PC\yymessenger-1.0.0-server-setup-win-x64.exe` | 한국어 서버 설치 마법사, 역할 선택·업무 서버용 관리자 메신저 포함 |
| 서버 PC · 수동 배치 | `01_서버PC\yymessenger-1.0.0-servers-win-x64.zip` | 기존 ZIP 배치·버전별 준비용. EXE 설치 시 따로 풀 필요 없음 |
| 서버 관리자가 쓸 PC | `01_서버PC\yymessenger-1.0.0-setup-win-x64.exe` | 메신저에 관리자 계정으로 로그인해서 관리 화면 사용 |
| 일반 교사 PC | `02_교사PC\yymessenger-1.0.0-setup-win-x64.exe` | 설치 마법사로 메신저 설치. 서버 프로그램은 설치하지 않음 |
| 교사용 Android | `03_안드로이드\kr.school.yeoyang.teacher-Signed.apk` | 여양고 교무실 시험 앱 |
| 학생·학부모 Android | `03_안드로이드\kr.school.yeoyang.family-Signed.apk` | 여양고 학교 소식 시험 앱. family는 내부 식별자 |
| iPhone 제작 담당 Mac | `04_iOS\yymessenger-1.0.0-ios-build-source.zip` | 최신 소스. 아이폰에 직접 설치하는 파일이 아님 |

각 폴더의 `SHA256SUMS.txt`도 함께 복사한다. PowerShell의 `Get-FileHash -Algorithm SHA256 '복사한 파일의 전체 경로'` 결과를 대조한다. 개발 PC 원본은 `artifacts`에 있고, 전달 사본과 원본의 해시를 검사한다. Windows 서버·메신저는 자체 .NET 런타임을 포함한다. 학교 PC에 개발용 SDK·Node.js·소스 전체를 설치할 필요는 없다.

## 2. 먼저 이 PC에서만 화면을 시험하려면

학교망에 연결하기 전, 가상 계정으로 확인하는 선택 단계다. 운영 설치와 데이터 경로를 섞지 않는다.

1. 서버 ZIP을 `C:\SchoolMessengerTest`에 압축 해제한다. 바로 아래에 `server`, `announcement-server`, `scripts`, `config`, `docs`가 있어야 한다.
2. 일반 PowerShell에서 다음을 실행하고 관리자 비밀번호를 12~128자로 정한다.

```powershell
Set-Location 'C:\SchoolMessengerTest'
.\scripts\Start-Server.ps1 -LocalTest -DataDirectory 'C:\SchoolMessengerTestData\office'
```

3. 같은 PC에 설치 EXE로 메신저를 설치한다. 서버 주소 `http://localhost:5080`, 아이디 `admin`, 방금 정한 비밀번호로 로그인한다.
4. 관리자 화면과 가상 교사 계정을 확인한다. 종료하려면 서버 창에서 Ctrl+C를 누른다.

이 모드는 **해당 PC 안에서만** 접속하고 파일 악성코드 검사를 생략한다. 다른 PC·휴대폰을 연결하는 운영 설정으로 사용하지 않는다. 이 단계는 외부 앱 서버·공지 연결까지 완료하는 절차가 아니다.

## 3. 실제 서버 PC에서 할 순서

### ① 서버 운영 준비

학교 담당자와 Windows 에디션·메모리·학교 회선 사용·인증서·백신 검사 연동·백업 장치·UPS를 확인한다. 현재 자료는 서버 RAM 8GB 중 약 75%가 이미 사용 중인 상태였고, 별도 백업 장치와 UPS는 아직 준비되지 않았다고 안내받았다. 이 상태에서 외부 앱까지 운영 준비가 끝났다고 판단하면 안 된다.

한 물리 PC에 두 서버를 둘 경우 **업무 서버 역할과 외부 학교 소식 서버 역할의 운영체제·계정·망·저장소 격리**를 담당자가 설계한다. 폴더만 두 개 만드는 것으로 침해가 격리되지는 않는다. 가상화 가능 여부와 메모리 여유를 확인한 뒤 구성한다. 프로그램이 가상 머신을 자동 생성하지 않는다.

### ② 서버 ZIP 배치

각 서버 역할의 Windows 환경에서 ZIP을 `C:\SchoolMessenger`에 푼다. 결과는 다음과 같다.

```text
C:\SchoolMessenger\server\SchoolMessenger.Server.exe
C:\SchoolMessenger\announcement-server\SchoolMessenger.AnnouncementServer.exe
C:\SchoolMessenger\scripts\Server-Operations.ps1
C:\SchoolMessenger\scripts\Configure-ServerOperations.ps1
C:\SchoolMessenger\config\server-operations.example.json
C:\SchoolMessenger\docs\installation-and-test-order.md
```

운영 데이터는 `C:\SchoolMessengerData\office`와 `C:\SchoolMessengerData\announcements`처럼 실행 파일 밖에 보관한다. **실행 폴더의 ZIP을 업데이트할 때 운영 DB나 첨부를 덮어쓰지 않는다.** 서버 EXE를 더블클릭하는 것만으로 인증서·보안·백업 설정이 끝나는 것은 아니다.

### ③ 주소·인증서·운영 설정 작성

담당자가 다음 값을 정한다.

| 항목 | 업무 서버 | 학교 소식 서버 |
| --- | --- | --- |
| 접속 대상 | 교사 PC와 교사용 앱의 교내 업무 | 학생·학부모 앱의 외부 접속 |
| 주소 | 인증서와 일치하는 교내 HTTPS 주소 | 앱 전용 공개 HTTPS 도메인 |
| 포트 기본값 | TCP 7443 | 원본 TCP 8443. 충돌하면 관련 설정을 함께 변경 |
| 외부 공개 | 공개하지 않음 | Cloudflare Tunnel은 이 역할에만 연결 |
| 설정의 `service` | `office` | `announcements` |
| 설정의 `resourceProfile` | `low-memory` | `low-memory` |

`10.85.88.101`은 기존 쿨메신저 자료의 주소다. 새 서버 주소로 확인되지 않았다. `localhost`는 입력한 기기 자신이므로 다른 PC나 휴대폰에는 서버 주소로 쓰지 않는다. 인증서 검증을 끄거나 방화벽·백신을 통째로 해제하지 않는다.

각 역할에서 `config\server-operations.example.json`을 `C:\SchoolMessengerOperations\server-operations.local.json`로 복사하고 실제 경로·인증서 지문·허용 망·백업·메일을 채운다. 두 VM 안에서는 같은 경로 이름을 써도 서로 다른 파일이다. 메일 수신처는 `yd043105@gmail.com`이고 발신 계정과 앱 비밀번호는 별도 준비한다. 비밀번호는 JSON에 적지 않는다.

학교가 확정한 약관 파일도 각각 설정한다. 초안 승인 플래그를 임의로 켜면 안 된다. 실제 설정 항목과 입력 예시는 [상세 설치 안내의 3-3~3-5](installation-and-test-order.md)에 있다.

### ④ 보안·백업·메일 점검 후 서버 시작

각 역할의 **전용 표준 Windows 운영 계정**에서 다음 순서로 진행한다. 메신저의 `admin`과 Windows 운영 계정은 서로 다르다.

```powershell
Set-Location 'C:\SchoolMessenger'
# 최초 한 번만. 기존 비밀 파일은 덮어쓰지 않는다.
.\scripts\Configure-ServerOperations.ps1 -ConfigurationPath 'C:\SchoolMessengerOperations\server-operations.local.json' -InitializeSecrets -Apply
# 실제 시험 메일 발송. 학교에서 수신 여부를 확인한다.
.\scripts\Server-Operations.ps1 -ConfigurationPath 'C:\SchoolMessengerOperations\server-operations.local.json' -Mode TestEmail
# 실제 준비 결과를 설정에 반영한 뒤 확인한다.
.\scripts\Server-Operations.ps1 -ConfigurationPath 'C:\SchoolMessengerOperations\server-operations.local.json' -Mode Preflight
# ready:true이고 문제가 없을 때 한 번 실행한다. 창을 닫으면 종료될 수 있다.
.\scripts\Server-Operations.ps1 -ConfigurationPath 'C:\SchoolMessengerOperations\server-operations.local.json' -Mode Start
```

두 역할에 동일한 무작위 서버 연결 키를 숨김 입력하고, 관리자 비밀번호는 교사용 초기 비밀번호와 별도로 정한다. 기존 설정·비밀 파일이 있으면 초기화를 반복하지 않는다. 사전 점검에 미완료 항목이 있으면 해결하고 다시 검사한다. 파일 검사 엔진의 실제 검사 성공과 약관 전문 확정도 별도로 확인한다.

학교 소식 서버를 먼저 켜고 Cloudflare 연결을 확인한 뒤 업무 서버의 공지 연결을 확인하면 된다. Cloudflare는 [처음 설치 안내](beginner-setup-guide.md)의 도메인·Tunnel 절차를 따른다. **학교 홈페이지 전체 DNS를 임의로 바꾸지 않고, 업무 서버를 터널에 추가하지 않는다.**

### ⑤ 관리자 앱에서 계정·자동검색·자동 구동 설정

1. 서버 관리자가 사용할 PC에 같은 설치 EXE를 설치한다. 업무 서버의 HTTPS 주소와 별도 `admin` 비밀번호로 로그인한다. 관리자 시스템이 열린다.
2. 교사 이름·부서·직책·업무 권한·특별실·담임을 등록한다. 이름은 아이디, 좌석 IP는 선택 관리 정보다. 시험 계정은 가상 교사 A·B·C부터 만든다.
3. 학생·학부모 관리 화면에서 학교 소식 서버의 별도 주소·관리자 계정으로 연결하고 가상 학급·학생·학부모·교사를 연결한다. 처음부터 실명 전체 명부를 넣지 않는다.
4. **서버 운영 → 교내 서버 자동검색 설정**에서 정상 HTTPS 주소를 저장한다. 필요하면 담당자가 UDP 45983을 교직원망 범위에 한정해 허용한다. 다른 VLAN에서는 검색되지 않을 수 있고, 주소 직접 입력도 가능하다.
5. 같은 서버 역할의 운영 계정으로 관리자 앱을 열고 **서버 운영 → 서버 PC 재시작 후 자동 구동 설정**에서 위 운영 JSON을 선택해 확인 후 켠다. 두 역할의 설정을 각각 등록한다. 서버 기동 전에는 이 관리자 화면에 연결할 수 없으므로 최초 시작은 ④를 먼저 한다.
6. 예정된 시험 재부팅 후 Windows 로그인 전 다른 PC에서 서버 연결을 확인한다. 설치 EXE의 ‘메신저 자동 실행’은 서버 자동 구동과 다른 설정이다.

자세한 화면·조건은 [자동검색·초기 로그인·자동 구동 안내](server-connection-and-autostart.md)를 참고한다.

## 4. 일반 교사 PC에서 할 순서

1. `02_교사PC`의 EXE로 설치한다. 일반 사용자 계정의 설치 마법사다. 서버 ZIP은 복사하지 않는다.
2. 실행 후 **서버 자동검색 → 학교가 안내한 주소 확인 → 이 서버 주소 사용**을 선택한다. 못 찾으면 정상 HTTPS 주소를 직접 입력한다.
3. 본인 이름 아이디와 초기 비밀번호 `12345678`로 처음 로그인한다. 관리자가 개별 전달한 활성화 번호·필수 약관 동의·새 개인 비밀번호를 입력한다. 개인 비밀번호는 12~128자다.
4. 이름과 개인 비밀번호로 다시 로그인한다. 본인 전용 Windows 계정일 때 자동 로그인·Windows 로그인 시 자동 실행을 선택한다. 초기 비밀번호 상태에서는 업무 자료·자동 로그인을 사용할 수 없다.
5. 두 번째 시험 PC에도 같은 EXE를 설치하고 다른 가상 교사로 로그인한다. 기존 쿨메신저·백신·학교 보안 프로그램은 켠 상태로 시험한다.

현재 EXE는 학교 배포자 코드서명이 없는 시험본이다. Windows 경고나 학교 보안 제품의 차단이 발생하면 파일 해시와 차단 기록으로 담당자에게 확인한다. 백신 전체 해제·폴더 일괄 제외로 해결하지 않는다.

## 5. 휴대폰 설치

Android는 교사에게 teacher APK, 학생·학부모에게 family APK를 전달한다. 파일 앱에서 열어 학교 정책이 허용하는 시험 설치를 진행한다. 현재 APK는 **Debug 시험용**이며 `Signed`가 Play스토어 배포 서명을 뜻하지 않는다. 앱에서는 신뢰할 수 있는 HTTPS 서버 주소를 사용한다. 일반 Android 앱 HTTP 예외는 에뮬레이터 `10.0.2.2`에만 한정된다.

교사용 업무 서버 기능은 교내 Wi-Fi 또는 학교가 승인한 VPN에서 시험한다. 학생·학부모 앱은 학교 소식 서버 공개가 끝난 뒤 Wi-Fi를 끄고 이동통신에서도 확인한다. 교내 업무 서버를 외부에 공개해서 해결하지 않는다.

iPhone에 설치할 IPA는 이번 Windows 환경에서 만들지 않았다. `04_iOS` ZIP은 **최신 소스 전달용**이다. Mac·Xcode·Apple 서명 인증서·프로파일을 준비해 teacher/family 앱을 각각 빌드하고 TestFlight로 시험한다. [iOS 제작 안내](ios-operations.md)를 따른다. 예전 GitHub 시뮬레이터 결과가 이번 변경의 빌드 성공을 대신하지 않는다.

## 6. 실제 사용 전 시험표

| 순서 | 시험 | 통과 기준 |
| --- | --- | --- |
| 1 | A→B 메시지와 무해한 첨부, B 로그아웃 중 수신 | 재로그인 후 동일 메시지·파일 확인, 오류 없음 |
| 2 | A→B·C 전송, B만 읽고 회수 / 별도 메시지는 모두 읽고 회수 | 한 명이라도 미읽음이면 회수 가능, 모두 읽으면 거부 |
| 3 | 예약 전송·나에게 전송·초안 목록·서식·파일 드롭 | 예약 전 도착하지 않음, 재실행 후 초안 복원, 서식·파일 일치 |
| 4 | 파일 제출 요청과 제출·회수·재제출 | 답장 없이 제출 가능, 보낸 사람의 현황과 일치 |
| 5 | 기한 포함 메시지 열기 → 할 일 후보 확인·선택 등록 | 승인한 항목만 등록, D-Day 일치 |
| 6 | 공지·시간표·분반·상담·특별실·수강신청 | 지정 대상만 열람, 중복 예약 거부, 신청은 1·2학년만, 3학년 제외 |
| 7 | 가정통신문 이미지 게시 | JPG/PNG 한 장 200,000바이트 이하, 최대 5장. 미대상 계정 열람 거부 |
| 8 | 자동검색·자동 로그인·자리비움·휴대폰 상태 | 두 PC에서 주소 일치, 개인 로그인 복원, 현재 기기 상태와 일치 |
| 9 | 시험 계정 비활성화·비밀번호 초기화 | 기존 PC·휴대폰 세션도 접근 거부 |
| 10 | 재부팅·정상 백업·별도 시험 환경 복원·장애 메일 | 자동 기동, 복원 데이터 확인, 메일 실제 수신 |

자료는 서버 폴더에 직접 복사하지 않고 **프로그램의 업로드 화면**에서 선택한다. 기초시간표 원본은 `C:\Users\user\Downloads\기초시간표 예시.xlsx`, 일자별 원본은 `C:\Users\user\Desktop\24년메신저다운방\2026. 10. 8. 목요일 시간표 - 1학년.xlsx`다. 시험에는 개인정보를 제거한 사본을 쓰고, 수업 담당자가 미리보기·교사·학급 연결 후 게시한다. 일자별 파일이 없는 날에는 기초시간표로 표시되는지 확인한다.

자동 시험은 가상 계정과 임시 데이터로 수행한다. 실제 학교 망·백신·재부팅·메일·모바일 실기기·종료된 앱의 OS 푸시는 이번 검사만으로 보장되지 않는다. 특히 **종료된 앱의 APNs/FCM 푸시, NEIS 급식 연동, 대량 학생 등록은 별도 구현·운영 확인 과제**다. 전체 시험 후 시험 배포 여부를 결정한다. 이번 작업은 공개 릴리즈·스토어 등록을 실행하지 않는다.

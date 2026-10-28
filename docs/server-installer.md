# 서버 EXE 설치 마법사

2026-10-28 · 정식 출시 전 1.0.0

## 서버 PC에서 실행할 파일

`01_설치파일\01_서버PC\yymessenger-1.0.0-server-setup-win-x64.exe`

서버는 이제 위 EXE로 설치한다. 교사용 `yymessenger-1.0.0-setup-win-x64.exe`와 다른 파일이다. 기존 서버 ZIP은 수동 배치·버전별 준비용으로 유지한다. 새 서버 PC에서는 ZIP을 따로 풀 필요가 없다.

## 설치 화면 순서

1. 서버 설치 EXE를 실행하고 Windows 관리자 권한 요청을 확인한다. 설치 프로그램은 학교 배포자 코드서명이 없는 시험본이므로 파일 해시와 학교 허용 절차를 확인한다.
2. 시작 → 설치 안내 → 설치 위치 → 서버 역할 선택 → 시작 메뉴 → 바로가기·자동 실행 선택 → 설치 → 완료 순서로 진행한다.
3. **교내 업무 서버 PC**를 고르면 업무 서버와 관리자 프로그램을 설치한다. **학교 소식 서버 PC**를 고르면 학생·학부모 앱 서버와 관리자 프로그램을 설치한다. 기본은 교내 업무 서버다. 사용자 지정은 시험 환경이나 담당자의 역할 구성용이며 두 역할을 같은 Windows에 설치했다고 망 격리가 완성되지 않는다.
4. 기본 위치는 `C:\Program Files\YeoyangMessengerServer`다. 기존 설치는 같은 경로로 업데이트한다. 구형 ZIP을 `C:\SchoolMessenger`에 풀어 사용했다면 새 설치는 그 데이터를 자동 이동하거나 새 경로로 연결하지 않는다. 기존 서버를 백업·중지한 후 실행 경로만 새 위치로 전환한다.
5. 완료 화면의 **관리자 프로그램 실행**을 선택한다. 시작 메뉴에도 ‘여양고 메신저 서버 → 관리자 프로그램 / 서버 설치·설정 안내 / 서버 프로그램 폴더’가 생긴다. 관리자 프로그램은 어느 서버 역할로 설치해도 포함된다. 바탕화면 아이콘은 **여양메신저 관리자**다.

별도 관리자 PC는 3번에서 **관리자 전용 PC**를 선택한다. 두 서버는 설치하지 않고 관리자 프로그램만 설치한다. 실행하면 관리 메뉴의 로그인 화면이 열리며 학교가 안내한 서버의 HTTPS 주소를 입력한다. 서버의 시작·중지는 실제 서버 PC에서 처리한다.

프로그램은 컴퓨터 범위로 설치하지만 **운영 서버를 Windows 관리자나 SYSTEM 계정으로 실행하지 않는다.** 이후 작업은 비밀 파일을 만든 전용 표준 Windows 운영 계정에서 수행한다.

## 설치한 뒤 서버를 켜는 순서

### 이 PC 한 대에서 먼저 시험하기

시작 메뉴의 **여양고 메신저 서버 → 관리자 프로그램**을 실행한다. 왼쪽 **교내 업무 서버**를 선택하고 오른쪽 실행 모드에서 **이 PC 시험**을 선택한다. 최초 관리자 비밀번호 12~128자를 두 번 입력한 뒤 상단 **서버 시작**을 누른다. 실행 중이 되면 **관리자 로그인**을 누른다. 같은 창의 로그인 화면에서 `http://localhost:5080`, `admin`, 정한 비밀번호를 입력하면 **교직원·권한** 화면으로 이동한다. 교직원 등록·가입 승인·담당 권한, 계정의 이름·부서·직책·좌석·초기화를 이 안에서 처리한다.

왼쪽에는 **운영 현황 / 학급·분반 / 특별실 / 학사일정 / 학생·학부모 / 약관·개인정보 / 오류·의견 / 운영 변경 기록**이 있다. 학교 소식 서버는 **학교 소식 서버**에서 가동하고, **학생·학부모** 메뉴에서 루프백 `http://localhost:5081`의 별도 관리자 계정으로 로그인한다. 다른 서버에 로그인했다고 권한을 공유하지 않는다. 역할을 바꾸어도 실행 설정은 유지된다. 시험 경로는 **데이터 저장 위치**를 펼쳐 확인한다. 비밀번호·관리자 로그인 쿠키는 설정 파일에 저장하지 않는다.

업무 서버 EXE를 직접 더블클릭하는 대신 관리 화면을 사용한다. **서버 중지**는 로컬 이벤트로 정상 종료를 요청하며 강제 프로세스 종료를 사용하지 않는다. 관리 창 X는 알림 영역으로 숨기고, 완전 종료는 알림 영역의 **서버 중지 후 프로그램 종료**를 사용한다. 관리 프로그램이 예기치 않게 끝나도 관리하던 서버는 부모 프로세스 종료를 감지해 정상 종료한다. 기존 예약 작업이 관리하는 서버를 강제로 인수하거나 중지하지 않는다.

설치 선택 항목은 **바탕화면 바로가기**와 **Windows 로그인 시 프로그램 자동 실행**이다. 선택 결과는 현재 Windows 사용자의 자동 실행에 적용하며, 관리 화면에서도 바꿀 수 있다. 관리 프로그램 자체의 자동 실행과 카드의 **관리 프로그램을 열면 서버도 자동 시작**은 별개다. Windows 로그인 전부터 필요한 운영 서버는 기존 사전 점검·예약 작업 구성을 사용한다.

기술 담당자가 수동 진단할 때 사용할 수 있는 기존 명령은 다음과 같다.

```powershell
Set-Location 'C:\Program Files\YeoyangMessengerServer'
.\scripts\Start-Server.ps1 -LocalTest -DataDirectory 'C:\SchoolMessengerTrial\office'
```

이 시험 모드는 같은 PC에서만 접속하며 파일 악성코드 검사를 생략하므로 가상 자료만 사용한다. 실제 운영은 아래 준비 순서를 따른다.

### 실제 학교 서버 운영

| 용도 | 기본 설치 기준 경로 |
| --- | --- |
| 업무 서버 | `C:\Program Files\YeoyangMessengerServer\server\SchoolMessenger.Server.exe` |
| 학교 소식 서버 | `C:\Program Files\YeoyangMessengerServer\announcement-server\SchoolMessenger.AnnouncementServer.exe` |
| 관리자 메신저 | `C:\Program Files\YeoyangMessengerServer\admin\SchoolMessenger.exe` |
| 운영 스크립트 | `C:\Program Files\YeoyangMessengerServer\scripts` |
| 설정 양식 | `C:\Program Files\YeoyangMessengerServer\config\server-operations.example.json` |
| 운영 설정 | 각 역할의 `C:\SchoolMessengerOperations\server-operations.local.json` |
| 데이터 | 업무: `C:\SchoolMessengerData\office`, 학교 소식: `C:\SchoolMessengerData\announcements` |

설치 위치를 바꿨다면 위 프로그램 경로도 함께 바꾼다. 기존 `C:\SchoolMessenger` 기준 설명을 볼 때는 실행·scripts·config 경로를 실제 설치 위치로 읽는다. 운영 데이터·백업·비밀 파일 경로는 기존 값을 유지한다.

1. 서버 역할 격리·HTTPS 인증서·파일 악성코드 검사·백업 장치·UPS·메일·약관을 준비한다.
2. 운영 설정 JSON을 만들고 `executable`에 위 역할별 EXE 전체 경로를 입력한다. 새 설치의 `config`는 양식이며 실제 설정을 자동 생성하거나 승인 플래그를 켜지 않는다. 기존 운영 JSON이 있으면 실행 경로를 검토해 수정하고 비밀 파일을 다시 초기화하지 않는다.
3. 전용 표준 Windows 운영 계정에서 다음을 실행한다. 최초 비밀 파일 준비와 실제 시험 메일의 순서는 [기존 상세 설치 안내의 3-5](installation-and-test-order.md)를 따른다.

```powershell
Set-Location 'C:\Program Files\YeoyangMessengerServer'
.\scripts\Server-Operations.ps1 -ConfigurationPath 'C:\SchoolMessengerOperations\server-operations.local.json' -Mode Preflight
# ready:true이고 실제 준비 조건을 확인한 뒤 한 번 시작한다.
.\scripts\Server-Operations.ps1 -ConfigurationPath 'C:\SchoolMessengerOperations\server-operations.local.json' -Mode Start
```

4. 업무 서버가 시작되면 시작 메뉴의 **관리자 메신저**를 열고 정상 업무 서버 주소와 별도 `admin` 계정으로 로그인한다. 교직원·권한·서버 자동검색을 설정한다. 관리자 메신저 자체가 서버 EXE를 대신하는 것은 아니다.
5. 관리자 화면의 서버 자동 구동에서 운영 JSON을 선택해 등록한다. 이미 예약 작업이 구형 ZIP 경로를 사용한다면 덮어쓰지 않는다. 담당자가 기존 작업을 중지·등록 해제한 뒤 새 설치의 스크립트와 같은 운영 계정으로 다시 등록한다. 새 관리자 메신저는 설치 폴더의 `scripts`를 찾아 연결한다.
6. 학교 소식 서버의 Cloudflare와 두 서버 연결을 확인한다. 재부팅 후 Windows 로그인 전 교사 PC에서 접속되는지도 확인한다.

이 EXE는 파일 배치 도구다. 설치만으로 서버 실행·인터넷 공개·인증서 발급·백업·메일·자동 기동이 완료되는 것은 아니다. 설치는 학교 방화벽·보안 제품·Cloudflare·작업 스케줄러를 자동 변경하지 않는다.

## 업데이트와 제거

1. 별도 장치에 운영 DB·첨부·설정의 정상 백업을 확인한다.
2. 작업 스케줄러에서 **이 설치 경로를 사용하는** `YeoyangMessenger-office-Start`, `YeoyangMessenger-office-Monitor` 또는 `YeoyangMessenger-announcements-Start`, `YeoyangMessenger-announcements-Monitor`를 모두 ‘사용 안 함’으로 바꾸고 실행 중 작업도 확인한다. 시작 작업 하나만 꺼서는 감시가 서버를 다시 켤 수 있다.
3. 수동 실행 창은 Ctrl+C로 종료한다. 예약 작업으로 실행했다면 운영 담당자의 정지 절차로 종료하고 해당 경로의 서버 EXE가 남아 있지 않은지 확인한다. 설치 프로그램은 서버를 강제 종료하지 않는다.
4. 새 서버 EXE 설치 마법사로 같은 경로에 업데이트한다. 실행 중인 서버·관리자 메신저나 활성 예약 작업이 있으면 차단한다. 다른 경로의 프로세스·작업은 변경하지 않는다. 더 오래된 버전으로 덮어쓰기도 차단한다.
5. 업데이트 후 운영 계정에서 사전 점검·수동 기동·기능 확인을 하고, 기존 경로와 동일한 예약 작업만 다시 활성화한다. 역할을 바꾸는 업데이트는 새 역할을 선택한다고 이전 역할이 자동 제거되지는 않으므로 담당자가 기존 실행·작업·구성을 확인한다.

제거할 때는 위 종료 절차에 더해 **이 경로에 연결된 예약 작업을 담당자가 등록 해제**해야 한다. 비활성 작업도 등록된 채 남아 있으면 제거를 차단한다. Windows 앱 목록에서 ‘여양고 메신저 서버’를 제거한다.

기본 프로그램 폴더 밖의 운영 DB·첨부·백업·DPAPI 비밀 파일은 제거하지 않는다. 프로그램 폴더에 사용자가 추가한 파일도 일괄 삭제하지 않는다. 제거는 졸업 파기나 계정 탈퇴가 아니다. 기존 교사 PC용 메신저는 별도 설치이므로 서버 제거로 함께 지워지지 않는다.

## 개발자 제작·검사

```powershell
./scripts/Build.ps1 -HeadlessTests
./scripts/Build-ServerInstaller.ps1
./tests/server-installer.ps1
./scripts/Organize-Workspace.ps1
./tests/final-delivery.ps1
```

서버 설치 테스트는 임의의 시험 설치 ID와 임시 폴더를 사용한다. 시험본만 사용자 범위 권한으로 컴파일하며 실제 배포본의 관리자 권한 요청을 해제하지 않는다. 학교의 실제 서버·방화벽·예약 작업을 변경하거나 등록하지 않는다. 실제 관리자 권한 설치·기존 운영 예약 작업의 중지·복원은 학교 시험 항목이다. GitHub 공개 릴리즈와 학교 설치는 이번 파일 제작과 별개다.

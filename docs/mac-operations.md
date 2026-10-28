# Mac 교직원 메신저

교무톡 1.0.0의 Mac 클라이언트다. 학교의 기존 교직원 서버에 연결하는 네이티브 Mac Catalyst 앱이며 브라우저를 열어 사용하는 방식이 아니다. 학교 서버·관리자 서비스 프로그램은 Windows에 설치한다. Mac 때문에 별도 서버나 새 학교 계정을 만들 필요는 없다.

## 설치할 파일

실제 빌드가 성공하면 GitHub Actions의 **Native Mac → mac-1.0.0-trial**에서 다음 두 파일을 제공한다. 로컬 전달 폴더는 `01_설치파일/05_Mac`이다.

| Mac 종류 | 파일 |
|---|---|
| M1·M2·M3·M4 등 Apple Silicon | `yymessenger-1.0.0-macos-arm64-trial.zip` |
| Intel Mac | `yymessenger-1.0.0-macos-x64-trial.zip` |

macOS 12 이상이 필요하다. Mac의 Apple 메뉴 → ‘이 Mac에 관하여’에서 칩 또는 프로세서를 확인한다. ZIP을 풀면 `교무톡.app`이 나온다. Finder에서 응용 프로그램 폴더에 옮기면 된다. iPhone 시뮬레이터용 ZIP이나 Windows EXE는 Mac 설치 파일이 아니다.

**현재 제작 방식은 로컬 시험용 ad-hoc 서명이다.** Developer ID 배포 서명·Apple 공증을 받은 일반 배포본과 다르다. 다운로드한 시험본은 Gatekeeper가 실행을 차단할 수 있다. 보안 기능을 끄거나 우회하지 않는다. 다른 교사에게 일반 배포하기 전 아래의 배포 서명과 공증을 완료해야 한다.

## 학교 연결과 시험 순서

1. Windows 학교 서버를 먼저 설치하고 HTTPS 연결과 교직원 계정을 준비한다. 현재 학교 선택·서버 설정을 그대로 이용한다.
2. Mac을 교직원망이나 학교에서 승인한 VPN에 연결한다. Mac 지원 때문에 교직원 서버를 인터넷 전체에 공개하지 않는다.
3. 교무톡을 실행하고 ‘학교 검색’에서 학교를 선택한다. 학교 목록 미등록 시험 환경은 ‘연결 도움’에서 관리자가 안내한 HTTPS 주소를 사용한다.
4. 교사 이름과 비밀번호로 로그인한다. 첫 계정 활성화·개인 비밀번호 변경은 기존 정책과 동일하다.
5. 왼쪽 ‘새 메시지’에서 본인을 선택하거나 ‘나에게 전송’을 누르고 시험 메시지를 보낸다. 받은 메시지·보낸 메시지 양쪽에서 확인한다.
6. Windows 교사 PC와 서로 메시지를 보내고, Mac에서 파일 선택기로 작은 가상 파일을 첨부한다. 수신·첨부 열기·읽음 표시를 확인한다. 실제 학생 개인정보로 시험하지 않는다.
7. 긴 메시지를 열어 하단 ‘답장’이 본문 스크롤과 별개로 보이는지 확인한다.
8. ‘조직도’, ‘내 시간표’, ‘학교 일정’, ‘특별실 예약’, ‘상담 예약’을 확인한다. 공지 발송은 기존 교직원·외부 공지 서버 연결과 권한이 필요하다.
9. Windows 조직도에서 Mac 로그인 교사가 PC 온라인으로 보이는지 확인한다. Mac의 ‘자리비움 / 복귀’를 누르면 해당 상태가 전달되어야 한다. 휴대폰 접속 상태와 별도로 취급한다.
10. 로그아웃 후 메시지·첨부에 다시 접근할 수 없는지 확인한다. Mac 앱을 닫은 뒤 온라인 상태가 해제되는지도 확인한다.

자동 로그인은 비밀번호 대신 세션을 Apple Keychain에 저장한다. 시험용 빌드는 기본값을 끄고, 키체인 저장에 실패하면 현재 창에서만 로그인한다는 안내를 표시한다. 평문 파일로 대체 저장하지 않는다. 실제 배포 서명·프로파일을 준비한 Mac에서 재실행·로그아웃·키체인 삭제를 추가 시험해야 한다.

## 이번 Mac 클라이언트의 범위

메시지 목록·본문·읽음 처리, 일반 교직원 메시지 작성·답장·나에게 전송·파일 첨부, 조직도, 시간표, 일정, 특별실·상담 예약, 기존 교사용 공지·개인정보·오류 신고 화면을 제공한다. Mac 창은 사이드바와 본문을 나누고 답장을 하단에 고정한다.

Windows와 완전히 같은 기능 범위는 아니다. Windows 서버 서비스 제어·관리자 프로그램·원격 화면 제어, Windows 로컬 메시지 보관함, 서식 편집·드래그 첨부·예약 전송·임시저장 목록, 채팅·설문·할 일·파일 제출 업무의 Mac 전용 화면은 이번 범위에 포함하지 않는다. Mac의 메시지는 서버 API로 조회하며 Windows 설치 폴더의 보관함을 읽지 않는다. 앱 종료 중 운영체제 푸시 알림과 Mac 자동 업데이트도 별도 작업이다.

## 소스와 실제 빌드

프로젝트는 `src/SchoolMessenger.Mac`이다. 교사용 모바일 로직과 계약을 공유하고, Mac 고유 화면·접속 상태·권한 파일은 해당 프로젝트에서 관리한다. Android·iOS의 대상 프레임워크와 기존 잠금 파일을 변경하지 않는다.

Mac 개발 환경은 저장소의 고정 .NET SDK 10.0.402, Xcode 26.6 및 다음 워크로드를 사용한다. Windows에서는 이 Mac 앱을 직접 컴파일하지 않는다.

```bash
dotnet workload install maui-maccatalyst --version 10.0.401
dotnet restore src/SchoolMessenger.Mac --locked-mode
bash scripts/Build-Mac.sh maccatalyst-arm64
bash scripts/Build-Mac.sh maccatalyst-x64
```

잠금 파일은 실제 Mac 복원 결과로 생성해 저장했다. 이후 `--locked-mode`로 의존성 변경을 차단한다. 출력은 `artifacts/mac`이며 ZIP마다 SHA-256 파일을 함께 만든다. GitHub CI는 가상 학교 서버를 이용한 실제 네이티브 실행, 메시지 전송, 창 구성, 접속·로그아웃과 Release 번들의 아키텍처·HTTPS·샌드박스·서명 무결성을 검사한다. [2026-10-28 실제 실행 결과](mac-verification-2026-10-28.md)에 사용한 소스·CI·검증 범위를 기록했다. 실제 학교망·Finder 첨부 선택·Apple Keychain 배포 권한·실제 Intel 하드웨어 시험을 대신하지 않는다.

## 일반 배포 전 필요한 Apple 작업

Apple Developer 계정에서 이 Mac 앱 ID `kr.school.kyomutalk.mac`을 등록하고, Developer ID Application 인증서와 개인키·일치하는 Mac Catalyst 프로파일을 준비한다. 인증서와 개인키·프로파일을 공개 GitHub 저장소에 올리지 않는다. 배포용 권한 초안은 `Platforms/MacCatalyst/Entitlements.Distribution.plist`에 있다. Keychain 그룹은 실제 프로파일의 AppIdentifierPrefix와 일치해야 한다.

학교가 정식 배포를 지시하면 Hardened Runtime을 켠 Developer ID 서명본을 만들고 Apple 공증 결과를 확인·첨부한다. 현재 `Build-Mac.sh`는 공증이나 스토어 공개를 실행하지 않는다. 시험본을 공증 완료본처럼 이름만 바꿔 배포하지 않는다. Mac App Store 등록은 별도 절차다.

기준 문서: [Microsoft Mac Catalyst 배포](https://learn.microsoft.com/en-us/dotnet/maui/mac-catalyst/deployment/publish-outside-app-store?view=net-maui-10.0), [서명 없는 시험 빌드](https://learn.microsoft.com/en-us/dotnet/maui/mac-catalyst/deployment/publish-unsigned?view=net-maui-10.0), [보안 저장소](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/storage/secure-storage?view=net-maui-10.0).

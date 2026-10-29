# Mac 1.0.0 네이티브 시운행

2026-10-28 [GitHub Native Mac 실행](https://github.com/yd043105-pixel/yymessenger/actions/runs/40693482939)이 성공했다. 검토 브랜치 HEAD는 `d6c9882`이며 Xcode 26.6, .NET SDK 10.0.402, 고정 Mac Catalyst 워크로드 10.0.401과 패키지 잠금을 사용했다. 버전은 1.0.0을 유지하고 공개 릴리즈·스토어 등록을 하지 않았다.

## 실제 실행 결과

Apple Silicon Mac에서 iPhone 시뮬레이터를 사용하지 않고 Mac Catalyst 앱을 실제 실행했다. 실제 학교 데이터 대신 별도의 가상 교직원·학교 소식 서버와 임의 시험 비밀번호를 사용했다. 아래 11개 검사를 통과했다.

- Mac Catalyst 네이티브 프로세스에서 실행
- 교무톡 이름·1.0.0 버전
- 사이드바·본문을 분리한 네이티브 창
- 실제 네이티브 버튼의 제목 연결
- `teacher-macos` 제품 구분과 시험 세션의 메모리 보관
- 학교 서버가 Mac 접속을 PC 온라인으로 표시하고 휴대폰 접속으로 표시하지 않음
- 기본 Catalyst 컨트롤 표현에서 네이티브 메시지함 선택기 열기·닫기
- 실제 작성 화면에서 본인을 선택해 서버로 메시지 전송·수신 대상 확인
- 본문 스크롤과 분리한 하단 답장 버튼
- 로그아웃 시 실시간 연결 해제
- 외부 서버의 평문 HTTP 주소 거부

## 생성 파일 검사

Apple Silicon `arm64`와 Intel `x64` 각각 Release `.app`과 시험 ZIP을 실제 생성했다. 두 번들의 CPU 아키텍처·앱 ID·버전·개인정보 manifest·HTTPS 설정과 Debug용 localhost 예외 제외, 앱 샌드박스·외부 통신·사용자가 선택한 파일 권한, ad-hoc 서명 무결성 및 MIT 원문 포함을 검사했다. 각각의 ZIP에는 SHA-256을 함께 제공한다.

CI artifact에는 Mac 운영체제의 화면 캡처로 로그인·메시지함·작성·본문 화면과 `mac-results.json`을 저장했다. Mac 전용 기본 버튼 모양이 공통 디자인에 겹치는 현상은 MAUI의 기본 Catalyst UI 설정을 사용해 수정했다. Mac 공지 이미지 선택기의 JPG·PNG 필터도 연결했다. 비밀번호·세션 쿠키·가상 서버 DB는 artifact에 포함하지 않았다.

Windows에서 받은 CI 파일은 `artifacts/mac`에 놓고 `scripts/Prepare-MacDelivery.ps1`로 전달한다. 이 스크립트는 실제 실행 검사 성공·두 ZIP의 생성 SHA-256을 확인한 후 `01_설치파일/05_Mac`에 복사하고 다시 해시를 대조한다. 기존 Windows·Android·iOS 전달 목록과 별도로 Mac 목록을 남긴다. Windows 서버·PC 설치본과 iOS 소스 전달 ZIP도 갱신하고 기존 7개 전달 파일 검사를 통과했다.

## 남은 실제 운영 시험

Intel용은 교차 빌드와 실제 Mach-O 아키텍처 검사를 통과했으며 실제 Intel Mac에서 실행한 결과는 아니다. 실제 Mac에서 학교망·VPN·학교 인증서, Finder 파일 선택·첨부 내려받기와 취소, 일정·예약, 절전·복귀·네트워크 변경·계정 회수를 추가 시험해야 한다. 앱이 종료된 상태의 OS 푸시나 Mac 자동 업데이트, Windows 전용 기능 전체와의 동등성을 검증한 결과도 아니다.

Developer ID·프로비저닝 프로파일·Apple 공증 정보는 제공되지 않았다. 두 ZIP은 ad-hoc 시험본이며 Gatekeeper가 다운로드 앱 실행을 차단할 수 있다. 일반 배포 전 정식 서명·공증과 배포 Keychain 자동 로그인 시험을 마쳐야 한다. 인증서 확인을 끄거나 평문 세션 저장으로 우회하지 않았다. [Mac 설치·배포 준비](mac-operations.md)를 함께 읽는다.

## 같은 날 · Figma 공통 톤 적용

[Figma Mac 작업본](https://www.figma.com/design/x3w6KOP0uIi5VnkEDbXx0l?node-id=58-43)을 기준으로 메뉴 레일·분할 메시지함·작성창을 정리했다. [Mac 실행 40703259115](https://github.com/yd043105-pixel/yymessenger/actions/runs/40703259115)의 검토 HEAD는 `e02003f`이며 실제 실행 **14개 검사**와 Apple Silicon·Intel Release 번들 검사를 통과했다. 목록·본문 동시 표시, 중복 모바일 하단 메뉴 제거, 서버 읽음 응답 후 미확인 표시 갱신, 로그아웃 시 본문 제거를 확인했다. 시험 ZIP·SHA-256·실행 기록을 Mac 전달 폴더에 갱신했다. 이전 시운행 기록의 창 구성과 구분하며 [상세 변경](mac-ui-figma-2026-10-28.md)을 따른다.

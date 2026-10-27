# 서버 관리 화면 개선

2026-10-28 · 출시 전 1.0.0

학교 서버를 처음 다루는 교직원이 터미널 없이 서버를 시작하고 관리자 로그인으로 이동하는 화면이다. 기존 세로 카드 두 개를 반복한 화면을 역할 선택과 선택한 서버의 제어 영역으로 나눴다.

## 참고와 디자인 결과

- [NAVER WORKS 관리 콘솔](https://naver.worksmobile.com/whats-new/notice/works-admin-console/overview/)의 관리 영역 구분과 [Mattermost 상태 대시보드](https://docs.mattermost.com/administration-guide/manage/site-health-dashboard)의 상태 우선 정보 구조를 참고했다. 화면·아이콘·상표는 복제하지 않았다.
- [Stitch 기존 프로젝트](https://stitch.withgoogle.com/projects/16363322754853011110)에서 새 서버 관리자 화면을 생성한 뒤, 불필요한 외곽 메뉴와 가상 CPU·사용자·인증서 상태를 제거하도록 수정했다. 결과 제목은 `여양메신저 로컬 서버 관리자 (960x760 네이티브 윈도우)`다. 브라우저로 작업했고 개별 화면 ID는 확인되지 않아 임의로 기록하지 않았다.
- [Figma 정제 화면](https://www.figma.com/design/x3w6KOP0uIi5VnkEDbXx0l?node-id=37-26)은 기존 색상 변수·텍스트 스타일·버튼 인스턴스를 사용한 편집 가능한 레이어다. 비밀번호와 운영 설정이 조건부로 표시되도록 실제 동작에 맞췄다.
- 로그인 후 운영 화면도 Stitch에서 별도 생성하고 [Figma 운영 화면](https://www.figma.com/design/x3w6KOP0uIi5VnkEDbXx0l?node-id=38-30)에서 정제했다. **운영 현황 / 계정·학교 / 시작·연결** 탭으로 나눴고, 실제 서버의 CPU·RAM·저장 공간·전송 제한만 표시한다. Stitch가 제안한 가상의 백업 경로·응답 지연·로그아웃 버튼은 추가하지 않았다. 원래 연결된 관리자 계정의 접근 제어를 유지한다.
- `artifacts/server-manager/stitch-admin-workspace.png`, `figma-server-manager.png`, `server-manager-stopped.png`, `server-manager-running.png`, `server-manager-compact.png`에 작업 단계와 실제 WPF 화면을 보관한다. 화면 자료는 개인정보 없는 시험 데이터다.

## 실제 동작

왼쪽에서 **교내 업무 / 학생·학부모 소식**을 선택한다. 오른쪽 상단의 상태·주소·시작·중지·관리자 로그인은 창을 줄여도 설정 스크롤 밖에 남는다. 최초 비밀번호는 시험 서버의 첫 설정 때만 표시한다. 운영 모드에서는 운영 설정 파일을 선택한다. 역할을 바꿔도 입력 중인 설정을 보존한다.

프로그램 자동 실행과 서버 자동 시작은 구분한다. 시험 데이터 경로와 실행 기록은 접을 수 있다. 시작에 실패하면 해당 서버 화면을 선택하고 오류 기록을 펼친다. 창 X는 알림 영역으로 숨기며 완전 종료는 알림 영역 메뉴에서 한다.

기존 로컬 종료 이벤트·비밀번호 비저장·DPAPI 비밀 보호·운영 사전 점검·예약 작업 충돌 방지는 유지한다. 실제 CPU/RAM 등 운영 수치는 관리자 로그인 뒤 기존 서버 운영 화면에서만 조회한다. 이 디자인 작업으로 새 네트워크 포트나 공개 접속 권한을 추가하지 않는다.

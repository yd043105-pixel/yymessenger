# Stitch 협업 준비

현재 상태: 공식 스킬 설치, Google 인증, 실제 Stitch 생성·수정 완료. 기존 **캠퍼스 링크 메신저** 프로젝트에서 세 제품의 시안을 만들었다. 생성 시안과 실행 가능한 네이티브 프로그램은 구분한다.

## 연결

1. [Stitch 설정](https://stitch.withgoogle.com/settings)의 API Keys에서 키를 만든다.
2. `scripts/Connect-Stitch.cmd`를 실행하고 마스킹된 로컬 입력창에 키를 붙여 넣는다. 키를 채팅으로 보내지 않는다.
3. Codex를 다시 시작한다. 실제 연결·프로젝트 목록 조회에 성공한 뒤 작업을 이어간다. 저장 완료 메시지는 Google 인증 성공을 의미하지 않는다.

입력창은 키의 특정 접두사·길이·문자 패턴을 가정하지 않는다. 붙여넣기의 앞뒤 공백은 제거하고, 빈 값·8KB 초과·공백이나 줄바꿈 등 HTTP 헤더에 부적절한 입력은 저장 전에 구체적인 이유를 표시한다. 실제 키 유효성은 Google Stitch가 판정한다.

설정은 현재 Windows 사용자의 Codex 설정에 Stitch MCP 테이블을 추가한다. 기존에 다른 Stitch 설정이 있으면 덮어쓰지 않는다. 키는 Codex 설정 폴더의 `stitch/key.dpapi`에 Windows DPAPI CurrentUser로 암호화해 저장한다. 로컬 헤더 도우미는 Codex가 호출할 때만 복호화해 Google Stitch로 전달한다. 도우미를 직접 실행하거나 출력을 로그로 저장하지 않는다. 앱 배포·자동 시작·전역 훅은 추가하지 않는다.

암호화는 현재 사용자의 계정이 침해된 경우까지 방어하지 않는다. 키가 노출되면 Stitch에서 폐기하고 새 키로 설정한다. 연결을 제거하려면 Codex의 Stitch MCP 설정을 삭제하고 로컬 `stitch` 자격 증명 폴더를 삭제한다. 이 저장소나 메신저 설치 파일에는 키가 들어가지 않는다.

## 디자인 진행

- 원본: [Google 공식 stitch-skills](https://github.com/google-labs-code/stitch-skills), 검토한 커밋 `0337446dadde6f8c94210444e2aa9d546126480f`.
- 설치: `stitch-design@stitch-skills`, `stitch-utilities@stitch-skills`. React/web용 `stitch-build`는 설치하지 않았다.
- `DESIGN.md`는 현재 제품과 사용자 요구를 바탕으로 작성한 **로컬 디자인 기준**이다. 새 디자인 시스템으로 업로드하지 않고 기존 `EduLink Modern Desktop`과 `CampusTalk` 시스템을 사용했다.
- 프로젝트 ID: `16363322754853011110`. 모바일 시스템: `assets/cc4affe12bf54cfa96e2bdf48640742a`; PC 시스템: `assets/a552b345434849b0be42b9e2e50b7f5d`. 생성 요청에는 색상·폰트 토큰을 중복해서 넣지 않았다.
- 가상 데이터로 만든 현재 네이티브 캡처와 실제 Stitch 결과를 비교한다. 원격 생성물과 실제 ID만 `.stitch/designs/`와 `metadata.json`에 기록한다. 학생·교사 개인정보나 실제 업무 메시지는 업로드하지 않는다.
- 결과의 정보 밀도·가독성·접근성·권한 표현을 검토한 다음 WPF/MAUI에 반영한다. HTML 결과물은 참고 자료이며 웹앱으로 전환하지 않는다. 버전은 1.0.0을 유지한다.

참고: [Google Stitch MCP 연결 안내](https://stitch.withgoogle.com/docs/mcp/setup), [Codex MCP 설정](https://learn.chatgpt.com/docs/extend/mcp?surface=cli).

## 실제 생성·수정 결과

| 제품 | 최종 Stitch 화면 ID | 로컬 시안 |
| --- | --- | --- |
| 교사용 학교 앱 | `b83aba213a1549e2ab1d46495448e058` | `designs/teacher-home.png` |
| 학생·학부모용 학교 앱 | `45dde465fb0647ab9fd40c7df6dc7639` | `designs/student-parent-home.png` |
| PC 메신저 | `27758ad9f5ae4426aee232398e2fcf39` | `designs/desktop-organization.png` |

Stitch는 합성 데이터를 사용한 디자인 도구다. 초기 결과의 임의 버전, NEIS 동기화 상태, 재실·내선 정보, 잔여 예약 수, 학생증 도구를 제거하도록 수정 요청했다. 수정 결과에도 학생 일정·방과후 활동 등 현재 API와 연결되지 않은 일부 제안이 남아 있다. 이를 작동하는 기능으로 간주하거나 앱에 추가하지 않는다. PC 결과는 작은 창을 큰 캔버스에 배치하므로 PNG의 전체 픽셀 크기가 실제 창 크기와 같지는 않다. 네이티브 창 크기는 450×820을 유지한다.

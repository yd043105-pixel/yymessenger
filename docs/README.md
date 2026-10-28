# 설명서 목차

처음이라면 [최신 설치·사용 순서](operation-order.md)부터 읽으세요. 서버 EXE 설치 기준으로 준비·설정·교사 PC·휴대폰·시운행·평소 사용 순서를 정리했습니다. 자동업데이트는 추후 작업이며 현재는 수동 설치입니다. [최종 연결 점검](final-review-2026-10-24.md)에는 수정과 미확인 범위를 구분했습니다. 현재 버전은 배포 전 **1.0.0**이며, 예전 검증 문서는 당시 상태의 기록입니다.

## 설치·서버 운영

[학교 검색·여러 학교 도입](school-directory.md): 학교별 서버를 유지하면서 PC와 앱에서 학교명·지역으로 선택합니다. 실제 학교 등록 절차와 공개 목록의 범위를 확인하세요.

2026-10-28 보안 보완: [학교별 보안 도입 검토·운영 통제](security-regulatory-controls-2026-10-28.md), [보안성 검토 제출 자료 초안](security-review-submission.draft.md). 운영 서버는 학교가 확정한 검토 기록과 증빙을 확인하며 시험 모드를 운영 승인으로 표시하지 않습니다.

최신 보안 검토: [체크리스트 점검](security-checklist-2026-10-24.md), [학운위·에듀집 근거 자료](committee-security-evidence-2026-10-24.md), [사고 대응 준비안](security-incident-response.md).

에듀집 제출 준비: [교사 개인 등록·학교별 설정](edzip-registration-guide.md), [필수기준 9개 항목 대응표](edzip-checklist.draft.md), [개인정보처리방침 검토안](privacy-policy.draft.md), [개인정보 권리 요청](privacy-rights-operations.md). 검토용 문서를 충족 확인이 끝난 제출본으로 사용하지 않습니다.

| 찾는 내용 | 문서 |
| --- | --- |
| 서버 EXE 설치 마법사·역할 선택·업데이트·제거 | [서버 설치](server-installer.md) |
| 기기별 우선순위·복사할 파일·시운행 합격 기준 | [설치·테스트 순서](installation-and-test-order.md) |
| 준비물·담당자·작업 순서·도메인·Cloudflare 실습 | [처음 설치 안내](beginner-setup-guide.md) |
| Windows·Android·iOS별 설치 및 남은 출시 준비 | [플랫폼별 안내](platform-deployment-guide.md) |
| Windows 설치 마법사·업데이트 | [Windows 설치](windows-installer.md) |
| 백업·UPS·메일·자동 실행·서버 업데이트 | [서버 운영](server-setup-school-tunnel.md) |
| Android 앱과 서버 연결 | [모바일 운영](mobile-operations.md) |
| Mac·시뮬레이터·iPhone 서명 | [iOS 제작](ios-operations.md) |
| GitHub 릴리즈 절차 | [릴리즈](releases.md) |
| 버그 신고·매일 수정·출시 후 자동 공개 | [유지보수](daily-maintenance.md) |
| 오류 이메일 제목·발신 설정·재시도 | [오류 메일](feedback-email.md) |

## 기능·학교 정책

| 찾는 내용 | 문서 |
| --- | --- |
| 교직원 이름·IP·직책·특별실·학교 시각 | [교직원 관리](school-day-and-administration.md) |
| 관리자 자원 모니터·저부하 설정 | [관리자 시스템](server-administration.md) |
| 교사용 앱·업무 메뉴 | [교직원 운영](teacher-office-operations.md) |
| 학생·학부모 가입·자녀 고유번호 | [가입](family-registration.md) |
| 약관 승인·동의 기록 | [약관 운영](registration-policy-operations.md) |
| 졸업·마지막 자녀 졸업 시 파기 | [보유 기간](graduation-retention.md) |
| 상담 개설·신청·이전 서비스 전환 | [상담](counseling-integration.md) |
| 공지 이미지 200KB·최대 5장 | [공지 첨부](home-letter-images.md) |
| 선택과목 신청 설정·접수·분반 확정 | [수강신청 안내](elective-registration.md) |
| 기초·일자별 시간표 올리기 | [시간표](timetable-operations.md) |
| 메시지 수신자·예약·임시저장 | [메시지 작성](message-compose-and-scheduling.md) |
| 파일 제출 취소·메시지 회수 | [회수](message-recall-and-submission-withdrawal.md) |
| 메시지에서 할 일 추출·기한 | [할 일](tasks-deadlines.md) |
| 학운위에 설명할 준비 자료 | [심의 준비](committee-readiness.md) |

## 개발·검증 기록

최신 화면 개선: [Figma 시안·실제 PC와 앱 변경](frontend-figma-2026-10-27.md). 편집 가능한 Figma 화면과 실제 프로그램 캡처, 적용 범위·검증 한계를 구분했습니다.

최신 재검토와 보안 프로그램별 확인 범위는 [2026-10-20 백엔드·백신 호환성 점검](security-audit-2026-10-20.md)에 있습니다. 이전 취약점 수정 기록은 [2026-10-15 보안 점검](security-audit-2026-10-15.md), 세부 구조는 [백엔드 문서](backend/00-current-state.md)를 참고하세요.

`*design*.md`는 설계안, `*verification*.md`·날짜가 붙은 점검 문서는 시점별 기록입니다. 현재 설치는 최신 시험 설치 안내를 기준으로 합니다. 디자인 화면·시험 JSON은 `artifacts`, 실행 로그는 `artifacts/logs`에 있습니다. 구형 생성물은 영구 삭제가 차단되어 별도 보관했고, 이동 내역은 `artifacts/final-cleanup-2026-10-24.json`에 있습니다. 개발·시험 연결을 유지하기 위해 소스와 도구 경로는 그대로 둡니다.

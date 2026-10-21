# 설명서 목차

처음이라면 [기기별 설치·테스트 순서](installation-and-test-order.md)부터 읽으세요. 파일의 전체 경로와 서버·PC·Android·iPhone의 순서를 정리했습니다. 현재 버전은 배포 전 **1.0.0**이며, 예전 검증 문서는 당시 상태의 기록입니다.

## 설치·서버 운영

| 찾는 내용 | 문서 |
| --- | --- |
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

최신 재검토와 보안 프로그램별 확인 범위는 [2026-10-20 백엔드·백신 호환성 점검](security-audit-2026-10-20.md)에 있습니다. 이전 취약점 수정 기록은 [2026-10-15 보안 점검](security-audit-2026-10-15.md), 세부 구조는 [백엔드 문서](backend/00-current-state.md)를 참고하세요.

`*design*.md`는 설계안, `*verification*.md`·날짜가 붙은 점검 문서는 시점별 기록입니다. 현재 설치 순서는 위의 처음 설치 안내와 플랫폼별 안내를 기준으로 합니다. 디자인 화면·시험 JSON은 `artifacts`, 실행 로그는 `artifacts/logs`, 이전 ZIP은 `artifacts/previous-packages`에 있습니다. 원본 경로를 사용하는 빌드·시험을 유지하기 위해 이 파일들의 이름을 일괄 변경하지 않았습니다.

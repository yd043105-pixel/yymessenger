# 백엔드 현재 상태 · 2026-10-13

대상 기준: `378f3c6`, 앱 버전 1.0.0. 첨부 작업 지시서에 따른 Phase 0 기록이다. **아래 표는 코드 검사 결과이며 이번 작업의 실행 검증을 뜻하지 않는다.** 변경 전·후 실행 결과는 `11-test-report.md`에 별도로 남긴다. 실제 운영 데이터·운영망은 사용하지 않는다.

세 제품은 PC 메신저(WPF), 교사용 학교 앱(MAUI), 학생·학부모용 학교 앱(MAUI)이다. 교내 업무 서버와 외부 학교 소식 서버는 별도 ASP.NET Core 프로세스·SQLite DB·첨부 디렉터리를 사용한다. 프레임워크와 DB를 교체하지 않는다.

| 기능·목적 | 구현 상태 | 코드 위치·주요 처리 | 저장 | 인증·권한 | 위험도·수정 방향 | 기존 검증·추가 필요 |
| --- | --- | --- | --- | --- | --- | --- |
| 교직원 로그인·자동 로그인 | 부분 구현 | Server/Program.cs 인증·쿠키 검증, Desktop 및 Mobile 인증 클라이언트 | Users, 버전 쿠키, OS 보안 저장소 | 활성·세션 버전, HTTPS, CSRF, IP 속도 제한 | High·보완: 계정 전체 무효화 외 개별 기기 폐기와 점진적 재해시 조사 | security.mjs, smoke.mjs, IosRuntimeChecks.cs; 기기별·복원 후 세션 시험 |
| 개별 교직원 최초 활성화·좌석 | 완료(코드 기준) | Server/StaffAccounts.cs 등록·재발급·활성화 | StaffProfiles, Users, 감사·동의 테이블 | 개별 코드 해시·만료·실패 제한·개인 암호·버전 갱신 | Low·유지, 코드 재사용·좌석 이동 회귀 검증 | office.mjs |
| 학생 승인·임시 학부모·자녀 연결 | 부분 구현 | AnnouncementServer/PortalEnrollment.cs, PortalAdministration.cs | People, StudentApplications, Families, StudentCodes, FamilyCodeAttempts | 승인·관계·계정/IP 제한, 코드 보호 저장 | High·보완: 운영 보호자 확인 정책, 연결 변경 알림·감사·기기 제한 | enrollment.mjs, registration-policy.mjs; 운영 정책은 학교 승인 필요 |
| 약관·개인정보 안내 동의 | 완료(코드 기준), 운영 미승인 | Shared 등록 정책, 두 서버 가입·활성화 | 문서 식별값·버전·동의 기록 | 원자적 기록, 현재 문서 대조, Production 초안 가입 차단 | Low·유지; 법적 적합성은 별도 검토 | registration-policy.mjs |
| 관리자·업무별 권한 | 부분 구현 | Program.cs Admin 정책, OfficeFeatures.cs, StaffAccounts.cs | 관리자/발송 플래그, 업무 권한 | API에서 판정, 버전 재검증 | High·보완: 권한 변경 전수·감사·최고관리자 분리 조사 | security.mjs, office.mjs |
| 쪽지 영속 저장·멱등·읽음 | 부분 구현 | Server/Program.cs 메시지·읽음 API | Messages, Recipients, Attachments, SQLite 트랜잭션 | 참여자·수신자, 발신자별 요청 유일성 | High·보완: 커밋과 실시간 이벤트 사이 영속 Outbox·안정적 동기화 | smoke.mjs, features.mjs, 모바일 실제 읽음 회귀 |
| SignalR 재인증·접속 상태 | 부분 구현 | Maintenance.cs MessageHub, 서버 인증·연결 점검 | 메시지는 DB, 접속 상태는 메모리 | 사용자 그룹, 활성/세션 버전 검사 | Medium·유지·보완: 재접속 누락/권한 변경 전수 | security.mjs |
| 채팅·설문 | 부분 구현 | CollaborationFeatures.cs | 채팅·참여자·응답 테이블 | 참여자·대상자·작성자·기한 | Medium·보완: 알림 신뢰성·익명 여부 명시 조사 | features.mjs |
| 원격 지원 | 완료(코드 기준) | RemoteHub.cs, Desktop 원격 TLS 통신 | 단기 세션 메모리, 감사 조사 필요 | 명시적 동의·PC 핀·제어 리비전·세션 종료 | Medium·유지 P2P, 감사·재시작 기록 보완 | remote-channel.mjs, remote-firewall.ps1, features.mjs |
| 일반·채팅·제출 첨부 | 부분 구현 | Program.cs 업로드/다운로드, Shared 파일 정책, Maintenance.cs | UUID 파일·Attachments | 검사 후 저장, 참여자·만료·Production 검사 실패 차단 | High·보완: 업로드 시점 만료·체크섬·DB/파일 정합성·삭제 실패 추적 | security.mjs, work.mjs; 복원·손상·중단 시험 |
| 공지·대상·회수·분반 | 완료(코드 기준), 일부 확장 필요 | PortalNotices.cs, PortalSections.cs | Notices, Targets, 분반 명부·버전 | 현재 관계/소속·범위, 수신 스냅샷과 접근 회수 분리 | High·유지·검증: 경계·명부 변경 전수 | announcements.mjs, sections.mjs |
| 서버 간 공지 연계 | 부분 구현 | ExternalAnnouncements.cs, PortalSecurity.Bridge, /bridge API | ExternalOutbox·파일·대상 스냅샷 | 현재는 HTTPS와 고정 Bearer 키, 교사 검증 상태 | High·보완: 요청 서명·유효기간·재전송 방지·키 교체·추적 ID | announcement-bridge.mjs, data-boundaries.mjs |
| 엑셀·기초/변경 시간표 | 완료(코드 기준), 개인 수강 명부 미제공 | Timetables.cs, PortalTimetables.cs, Contracts/Timetables.cs | 시간표 버전·미리보기·게시 기록 | 담당자·교사 본인·허용 학급 | Medium·유지; 없는 개인 수강 자료는 생성하지 않음 | timetable.mjs, TimetableChecks, sections.mjs |
| 학사일정·특별실 예약 | 부분 구현 | OfficeFeatures.cs | 일정·예약·리비전·감사 | 업무 권한·원자적 중복 검사 | Medium·보완: 대상별 공개 범위·100건 경쟁·과거 시간·예약 규칙 조사 | office.mjs |
| 업무 제출·미제출 알림·할 일 | 완료(코드 기준), 확장 조사 | SubmissionFeatures.cs, TaskFeatures.cs | 요청·대상·파일·리마인더 유일성 | 소유자·자기 제출·현재 상태 | Medium·유지·보완: 장애·ZIP 자원 제한·배치 실행 추적 | work.mjs |
| OS 푸시 | 미구현(운영 전송) | 기존 앱 운영 문서·알림 조회 | 제공자·토큰·영속 큐 조사 필요 | 운영 APNs/FCM 키 없음 | Medium·보완 가능한 큐·등록 수명, 실제 전송 NOT RUN | 실제 기기/제공자 키·학교 승인 필요 |
| DB 무결성·마이그레이션 | 부분 구현 | Store.cs, PortalStore.cs 및 기능 초기화 SQL | SQLite WAL·FK·30초 Busy Timeout | 매개변수 SQL, 파일 권한은 운영 환경 의존 | High·보완: 버전 관리·변경 전 백업·정합성·잠금 시험 | 기존 기능 테스트; 이전 DB 업그레이드·실패 복구 필요 |
| DB 백업·복원 | 부분 구현 | Store.Backup, Maintenance, PortalMaintenance | 온라인 DB 백업, 기본 같은 루트/14일 | 첨부 내용은 백업하지 않음 | High·보완: 외부 백업 설정·복원 시험·세션/권한/만료 재검증 | smoke.mjs 백업 내용; 복원 운영 절차·시험 부족 |
| 감사·운영 상태·개인정보 관리 | 부분 구현 | StaffAccounts.cs, OfficeFeatures.cs, admin 상태 API, 운영 문서 | 일부 감사·상태, 일부 상태 메모리 | 관리자 API 제한 | High·보완: 관리 작업 전수·지속 상태·데이터 수명·복구 문서 | 실제 ACL/암호화·법적 근거·보존 기간은 미확정 |
| 급식·학생 공개 일정·개인 수강 시간표 | 미구현/부분 구현 | 현재 앱·시간표 조회·일정 API | 제공 자료·외부 승인 조사 필요 | 없는 자료를 공개하지 않음 | Medium·기능 상태 명시·제공 가능한 승인 데이터만 확장 | NEIS 운영 키/학교 식별자·명부 제공 필요 |
| 웹 관리 의존·공식 설치형 관리 | 부분 구현 | 서버 wwwroot, Desktop 관리 창, Mobile 관리 진입 | API와 정적 웹 UI | API 권한은 유지 | Medium·보완: 기존 관리 기능을 네이티브에 매핑하고 의존 제거 후 웹 UI 비공식/비활성화 | web-preview 등 기존 호환 테스트도 조사 |
| 업데이트·오류 접수·빌드 | 완료(기본 코드 기준) | Shared 업데이트, 서버 Feedback, scripts, CI | 버전 문서·오류 접수 기록 | 권한·최소 수집·플랫폼 검사 | Low·유지; 1.0.0·출시 미승인 | update-notice.mjs, Windows/Android/iOS CI |

## 우선 위험 후보와 검증 방식

1. 서버 간 Bearer 인증에는 요청별 서명·시간·Nonce가 없다. 같은 요청의 재전송 차단을 가상 서버에서 재현하고 보완한다.
2. 내부 메시지는 DB에 저장되지만 SignalR 이벤트는 커밋 뒤 직접 전달한다. 커밋 후 중단을 가정한 Outbox 재처리를 검증한다.
3. 파일 만료·검사 상태·메타데이터·물리 파일·복원 시점의 일치 여부를 첨부 종류별로 확인한다. 기존 만료 시각을 늘리지 않는다.
4. 백업은 온라인 DB 스냅샷이지만 첨부 복원과 삭제 정책, 복원 후 세션 폐기는 별도 절차가 필요하다.
5. 8자리 자녀 번호의 소지는 보호자 신원 자체를 증명하지 않는다. 기존 개발 흐름을 유지하고 운영 확인 정책은 승인 전 미확정으로 둔다.

원격·실제 학교 서비스·운영 방화벽·인증서·스토어는 변경하지 않는다. 표의 상태와 위험도는 추가 코드 조사·재현 결과에 따라 갱신한다.

이번 변경 뒤의 상태는 03~11 문서에 정리했다. Phase 0 표는 변경 전 기록으로 보존한다. 전체 지시서 완료·운영 승인으로 해석하지 않는다.

# 데이터베이스와 마이그레이션

`Store`는 school.db, `PortalStore`는 announcements.db를 관리한다. SQLite WAL, 외래키, 30초 대기 시간을 유지한다. 두 서버가 같은 폴더를 사용하는 것과 같은 폴더의 중복 서버 시작을 파일 잠금으로 차단한다. 네트워크 공유 폴더에서 WAL을 운영하지 않는다.

이번 새 백엔드 스키마는 `BackendMigrations.Version=5`로 한 번 적용한다. 개발 중 내부 단계 번호이며 제품 버전은 1.0.0이다. 이전 운영 스키마가 있으면 변경 전에 SQLite Backup API로 `migration-backups/before-backend-5-*.db`를 만든다. 추가 스키마·인덱스·버전 기록은 하나의 트랜잭션이다. 기존 열·데이터·SELECT 순서를 변경하지 않는다. 기존 기능의 역사적 CREATE/ALTER 초기화가 전부 버전 관리로 전환된 것은 아니다.

| 추가 테이블 | 목적·주요 제약 |
| --- | --- |
| BackendMigrations | 버전 PK·적용 UTC 시각 |
| DeviceSessions | 임의 세션 ID, UNIQUE 토큰 해시, 사용자·버전·만료·폐기, 사용자 인덱스 |
| BackendRecoveryState | 복원 시각·학교 검토 대기·일반 이용 재개 |
| BackendRoot | 최초 최고관리자 내부 ID |
| BridgeNonces | 서비스+난수 PK, 만료 인덱스 |
| MessageEvents | AUTOINCREMENT 순번, UNIQUE 이벤트 키, 수신자 인덱스, 미처리 재시도 인덱스 |
| BackendJobs | 작업 이름 PK, 시작·마지막 성공/실패·횟수·오류 종류 |
| BackendAudit | 순번·UTC·최소 계정 ID·작업·결과·추적 ID, 수정/삭제 거부 |
| FileVerification | 종류+파일 ID PK, 상태·SHA256·갱신 시각 |
| GuardianRequests | 보호자+학생 PK, 번호 해시·신청 시각·심사 상태·담당자 |

메시지 SenderId+ClientId, 공지 SenderId+ClientId, 제출 RequestId+UserId+ClientId, 파일 OwnerId+ClientId와 기존 예약 요청 제약을 유지한다. 쪽지/채팅 요청 공간은 기존 호환성을 위해 공유되고 Fingerprint가 내용을 구분한다. 동작별 별도 요청 공간의 전면 이관은 미완료다.

현재 Windows 서버 런타임이 보고한 SQLite 버전은 3.53.3이다. WAL 결함 수정 버전 확인은 실제 실행 결과에 기록한다. 다른 배포 아키텍처에서는 런타임 버전을 다시 확인한다.

백업은 DB 메타데이터만 포함한다. 파일·DPAPI/DataProtection 키·설정은 따로 복구 계획에 포함한다. 스키마 롤백은 파일을 덮어쓰는 방식으로 운영 중 실행하지 않는다. 정지 후 검증한 스냅샷과 해당 코드로 되돌린다. 백업 뒤 생성된 데이터의 손실 범위를 승인·기록한다.

근거: [SQLite WAL](https://www.sqlite.org/wal.html), [SQLite Backup API](https://www.sqlite.org/backup.html).

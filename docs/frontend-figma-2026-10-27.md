# Figma와 네이티브 화면 개선 · 2026-10-27

버전은 1.0.0이다. Windows 교직원 메신저, 교사용 앱, 학생·학부모용 학교 앱의 기존 기능에 공통 시각 기준을 적용했다. 웹앱으로 전환하거나 서버 구성·권한을 변경하지 않았다.

## 편집 가능한 디자인

[최신 Figma 작업본](https://www.figma.com/design/x3w6KOP0uIi5VnkEDbXx0l) · [이전 계정의 보존 파일](https://www.figma.com/design/4Sapf25JYKt31JwtMU24a4)

| 화면 | Figma 노드 |
| --- | --- |
| PC 조직도 · 450×820 | [2:69](https://www.figma.com/design/x3w6KOP0uIi5VnkEDbXx0l?node-id=2-69) |
| PC 메시지함 | [2:70](https://www.figma.com/design/x3w6KOP0uIi5VnkEDbXx0l?node-id=2-70) |
| PC 메시지 작성 | [2:71](https://www.figma.com/design/x3w6KOP0uIi5VnkEDbXx0l?node-id=2-71) |
| 교사용 앱 홈 | [2:72](https://www.figma.com/design/x3w6KOP0uIi5VnkEDbXx0l?node-id=2-72) |
| 학부모용 학교 앱 홈 | [2:73](https://www.figma.com/design/x3w6KOP0uIi5VnkEDbXx0l?node-id=2-73) |
| 학생용 학교 앱 홈 | [2:74](https://www.figma.com/design/x3w6KOP0uIi5VnkEDbXx0l?node-id=2-74) |
| 회원가입 | [2:75](https://www.figma.com/design/x3w6KOP0uIi5VnkEDbXx0l?node-id=2-75) |
| 공지 상세 | [2:76](https://www.figma.com/design/x3w6KOP0uIi5VnkEDbXx0l?node-id=2-76) |

색상 변수, 글자 스타일, 기본·보조·비활성 버튼 컴포넌트를 파일에 만들었다. 인물·수업·날짜는 가상 예시이며 실제 회원 정보와 메시지는 Figma에 올리지 않는다. 시안의 데이터·기기 상태바는 앱 기능이나 실측 화면의 증거가 아니다.

기존 코드에는 Figma Code Connect 매핑이 없었다. 기존 네이티브 버튼·입력칸·카드·탐색 메뉴를 수정해 재사용했고, 임시 Figma 이미지 URL이나 React 코드를 앱에 넣지 않았다. 아이콘은 기존 WPF Geometry와 MAUI 이미지 자산을 사용한다.

## 실제 프로그램에 반영한 부분

- PC는 작은 조직도 크기를 유지하면서 왼쪽 메뉴 폭을 78로 줄이고, 교직원 이름 옆에 직책을 표시한다. 긴 직책은 생략하고 마우스를 대면 전체를 보여준다.
- 공통 제목줄 높이를 44에서 38로 줄였다. 메시지함의 반복 안내 문장을 없애고 제목·본문에 공간을 더 줬다. 메시지 목록과 상세 제목은 한 줄 생략을 유지하며 답장은 본문 스크롤 밖에 고정한다.
- 작성창은 본문 공간을 확보하도록 높이 660을 사용한다. 수신자 두 줄 스크롤, 예약·파일 제출 요청 한 줄, 드래그 첨부, 글꼴 서식, 임시저장 목록은 기존 동작을 유지한다.
- 앱은 학교명·역할과 ‘내 정보’ 버튼을 상단에, 홈·메시지/소식·내 정보를 하단에 고정한다. 가운데 내용만 스크롤한다. 서버에서 받아 온 학교명을 표시하며 로그아웃 시 초기화한다.
- 두 앱의 공통 카드·입력칸·버튼·소식 목록에 색상과 여백을 적용했다. 바로가기의 이중 테두리와 소식 카드의 굵은 세로 장식을 줄였다. 소식 제목은 한 줄로 생략하고 누르면 전문을 확인한다.
- 모바일 터치 영역은 44 이상을 유지한다. 항목 제목 전문과 버튼의 접근 가능한 이름을 유지하고, 상단 내 정보 아이콘에 설명을 붙였다. 약관 항목과 동의 체크, 권한에 따른 메뉴 노출은 그대로다.
- PC의 다섯 색상 테마와 유리 효과 선택을 유지한다. 읽는 영역은 불투명한 흰색으로 정리하고 유리 느낌은 상단과 배경에 둔다. 실시간 흐림 처리·새 애니메이션·외부 글꼴 라이브러리는 추가하지 않았다. 고대비 모드는 Windows 시스템 색상을 따른다.

## 공통 시각 기준

| 항목 | 값 |
| --- | --- |
| 기본 강조 | #245BC6 |
| 본문 | #182C47 |
| 보조 글자 | #63748C |
| 구분선 | #DFE7F1 |
| 선택 배경 | #EAF0FC |
| 화면 배경 / 내용 | #F3F6FB / #FFFFFF |
| 컨트롤 / 카드 모서리 | 10 / 14 |
| 기준 간격 | 8·12·16·24 |

Figma는 Noto Sans KR로 편집한다. Windows는 맑은 고딕, 휴대폰은 운영체제 한글 글꼴과 접근성 배율을 사용한다. 화면 크기·글꼴에 맞춰 네이티브 레이아웃으로 옮기므로 기기마다 줄바꿈·컨트롤 높이가 달라질 수 있다. PC 메시지함 시안의 900×680은 넓은 보기 예시이며 실제 기본 창은 760×640을 유지한다.

## 검증과 파일

- WPF 빌드: 경고·오류 0개.
- `node tests/smoke.mjs --ui --skip-native-input`: 93개 통과. 실제 창을 띄워 450×820, 450×760, 380×580 메뉴 잘림과 아이콘 전환, 다섯 테마, 접속 상태, 역할별 메뉴, 작성·읽기·예약·제출·할 일 흐름을 검사했다. 실제 Windows 키보드 주입은 제외했다.
- 로컬 실제 화면은 `artifacts/client-preview.png`, `artifacts/mailbox-preview.png`, `artifacts/compose-preview.png`에 저장한다. Figma 시안 캡처와 작업 노드 기록은 `artifacts/figma-ui-20261027/`에 구분해 보관한다.
- Android 교사용·학생학부모용 Debug APK 빌드는 경고·오류 0개로 완료했다. GitHub의 양쪽 앱 Release 빌드도 통과했다.
- [코드 커밋 1fe054e의 검증 실행](https://github.com/yd043105-pixel/yymessenger/actions/runs/40451902297)에서 iPhone 시뮬레이터 실행 검사가 통과했다. Windows 설치파일 CI 검사도 통과했다. 기록 작성 시 iOS Release 빌드는 진행 중이다. 최신 결과는 해당 실행과 PR 상태를 확인한다.
- 로컬 Windows 설치파일 두 개와 서버 ZIP, 두 APK, iOS 최신 소스 ZIP을 `01_설치파일`에 갱신했다. `tests/final-delivery.ps1`의 전달 파일 7개 해시·현재 소스 일치 검사가 통과했다. iOS ZIP은 설치 가능한 IPA가 아니다.
- 실제 Android/iPhone의 화면 크기·큰 글씨·VoiceOver/TalkBack, 학교 운영 데이터, Apple 서명·스토어 업로드는 별도 현장 확인이 필요하다. 학교 서버 가동·공개 릴리즈는 실행하지 않는다.

기존 계정 무료 호출 한도 도달 후 사용자가 지정한 다른 연결 계정에 새 작업본을 만들었다. 기존 파일은 보존했다. 같은 공통 색상·글자 스타일·버튼과 8개 화면을 이어서 구성하고, 모바일 하단 메뉴 높이를 보정했다. PC 세 화면, 모바일 홈 세 화면, 가입·공지 두 화면으로 묶어 전체 캡처를 검토했다. 최신 캡처는 `artifacts/figma-ui-20261027/pc-design.png`, `mobile-homes-design.png`, `signup-notice-design.png`이며 새 파일의 변수·스타일·노드·잘림 검사 기록은 `continued-state.json`에 있다. 기존 파일에 편집 권한을 추가할 필요는 없다.

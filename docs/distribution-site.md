# 교무톡 · 학교톡 다운로드 홈페이지

배포 주소: https://gyomutalk.tinyrat.chatgpt.site

홈페이지 소스는 `sites/gyomutalk-downloads`의 독립적인 Sites 소스 저장소에서 관리합니다. 학교별 실제 서버·계정·메시지와는 연결하지 않는 소개·설명서·설치파일 안내 페이지입니다. 학교 서버의 호스팅 구성을 바꾸지 않습니다. 공개 여부는 Sites 접근 설정으로 별도 관리하며, 초기에 본인 전용 미리보기로 게시했습니다.

기능별 실제 실행 화면은 가상 계정을 사용했습니다. 학교 관리자·교직원·학생 및 학부모의 순서별 안내, 공개 전달용 PDF, 학교 도입 고지, FAQ를 제공합니다.

GitHub 저장소의 최신 정식 공개 릴리즈를 읽어 버전과 설치파일을 표시합니다. 서버관리자는 gyomutalk-server, Windows 교직원은 gyomutalk-windows를 사용합니다. Mac과 교무톡·학교톡 휴대폰 앱은 출시 예정이며 임의의 다운로드 링크를 만들지 않습니다. GitHub API 제한이나 연결 오류가 있으면 직접 릴리즈 페이지를 확인하도록 안내합니다.

릴리즈 설치파일 이름은 `gyomutalk-server-<버전>-setup-win-x64.exe`, `gyomutalk-windows-<버전>-setup-win-x64.exe`입니다. 각 저장소의 릴리즈 워크플로가 검증된 파일을 이 이름으로 올립니다. 공개 릴리즈가 없는 현재는 ‘첫 공개 릴리즈 준비 중’으로 표시됩니다.

홈페이지 검사: `node --test sites/gyomutalk-downloads/tests/releases.test.mjs`. 390px 및 데스크톱 화면에서 가로 넘침, 기능 탭, 역할별 사용법, 화면 확대, 릴리즈 상태를 확인했습니다. 실제 다음 버전 설치와 학교의 운영 백업 장치·방화벽 연결은 학교 환경에서 별도 시운행해야 합니다.

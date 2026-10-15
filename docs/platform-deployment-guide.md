# Windows·Android·iOS 설치와 운영 순서

2026-10-15 · 배포 전 버전 **1.0.0**. 일반 사용자는 Windows 설치 프로그램과 휴대폰 앱을 사용한다. 앱이 입력하는 HTTPS 주소는 통신할 서버 주소이며, 웹앱으로 전환한다는 뜻이 아니다. 학교 서버·Cloudflare·앱스토어를 실제로 공개하거나 설정한 결과가 아닌 준비 안내다.

## 제품과 연결 대상

| 제품 | 설치 대상 | 연결할 서버 | 역할 |
| --- | --- | --- | --- |
| Windows 여양고 교무메신저 | 교직원 PC | 교내 업무 서버 HTTPS | 쪽지·채팅·설문·제출·할 일·시간표·공지 작성·상담 개설·특별실 예약·권한별 관리 |
| Android/iOS 여양고 교무실 | 교직원 휴대폰 | 교내 업무 서버 HTTPS, 학교 소식 서버 HTTPS | 메시지·첨부 확인, 개인 시간표, 담당 공지, 학사일정, 특별실 예약, 상담 개설·예약 확인 |
| Android/iOS 여양고 학교 소식 | 학생·학부모 휴대폰 | 학교 소식 서버의 공개 HTTPS | 가입·자녀 연결, 대상 공지·가정통신문 이미지, 시간표·급식, 상담 신청·변경·취소 |

교직원 앱과 학생·학부모 앱은 **별도 앱**이다. 현재 식별자는 각각 `kr.school.yeoyang.teacher`, `kr.school.yeoyang.family`다. `Family`는 내부 빌드 구분일 뿐 별도의 가족용 제품은 없다. 교무실 앱의 업무 서버 기능은 교내 Wi-Fi 또는 학교가 허용한 VPN에서 사용한다. 학교 밖에서 업무 서버를 터널로 공개하지 않는다. 학교 소식 앱에는 교직원 쪽지·채팅·제출 파일을 복제하지 않는다.

## 먼저 할 일: 서버 준비

1. 학교 회선 외부 서비스 운영과 개인정보 처리·계정 확인 절차를 학교에서 승인한다. 약관·개인정보 안내는 현재 검토안이므로, 학교가 확정한 운영 정책 파일을 두 서버에 지정한다. 승인 전 Production 신규 가입·교직원 최초 활성화는 차단된다.
2. 서버에서 `scripts/Inspect-ServerComputer.ps1`을 실행하여 Windows 에디션·가상화·RAM을 확인한다. 제공한 사진은 DDR4다. 추가 RAM의 규격을 먼저 확인한다. UPS와 별도 암호화 백업 장치도 마련한다.
3. 교내 업무 서버와 외부 학교 소식 서버의 운영 계정·DB·폴더·접근망을 분리한다. 한 물리 PC에서 운영하려면 정보 담당자가 VM과 네트워크 격리를 검증해야 한다. 폴더만 분리하는 것은 침해 격리가 아니다. 격리·장비·부하를 확인하지 않고 8GB 한 PC에서 두 서버를 운영 준비 완료로 표시하지 않는다.
4. 최신 서버 패키지를 각 역할의 운영 위치에 풀고, 데이터와 백업은 프로그램 폴더 밖의 고정 경로로 지정한다. 서버 EXE는 Windows 메신저 설치 EXE와 별개다. 운영 서버에는 개발 SDK가 필수는 아니다. 배포 EXE에 런타임이 포함된다.
5. 유효한 HTTPS 인증서와 개인키 권한, 교내 허용 CIDR, Windows Defender 검사, 운영 계정 ACL·디스크 암호화·업데이트를 준비한다. `10.85.88.101`은 확인된 기존 학교 내부 IP이며 공개 주소가 아니다. 실제 사용 가능 여부와 허용 대역은 정보 담당자가 확인한다.
6. `config/server-operations.example.json`을 역할별 실제 설정 파일로 복사해 경로·인증서·도메인·백업·SMTP를 채운다. 운영 계정으로 비밀값을 숨김 입력하고 사전 점검·시험 메일·예약 작업을 준비한다. 아래 명령은 **배포 폴더에서, 실제 운영 조건을 준비한 뒤** 실행한다. DPAPI 비밀값과 작업은 같은 Windows 운영 계정을 사용한다.

```powershell
.\scripts\Configure-ServerOperations.ps1 -ConfigurationPath .\config\server-operations.local.json -InitializeSecrets -Apply
.\scripts\Server-Operations.ps1 -ConfigurationPath .\config\server-operations.local.json -Mode Preflight
.\scripts\Server-Operations.ps1 -ConfigurationPath .\config\server-operations.local.json -Mode TestEmail
.\scripts\Configure-ServerOperations.ps1 -ConfigurationPath .\config\server-operations.local.json -RegisterTasks -Apply
.\scripts\Server-Operations.ps1 -ConfigurationPath .\config\server-operations.local.json -Mode Start
```

실제로 확인한 조건만 검증 완료로 기록한다. 저부하 설정은 파일 동시 작업·속도를 제한하지만 CPU·RAM 전체 사용률의 상한을 보장하지 않는다. 별도 전원·회선의 외부 장애 감시는 학교 서버의 정전까지 감지하도록 준비한다. 상세 항목은 [학교 서버 운영 안내](server-setup-school-tunnel.md)에 있다.

## Cloudflare 연결 순서

1. 학교 소유 도메인을 Cloudflare에 연결하고 관리자 계정의 2단계 인증을 설정한다.
2. **학교 소식 서버 역할에만** `cloudflared`를 설치해 터널을 만든다. 교내 업무 서버에는 공개 터널을 만들지 않는다.
3. 공개 이름을 실제 학교 소식 도메인으로 정하고, 원본 서비스는 같은 격리 환경의 `https://127.0.0.1:8443`으로 연결한다. `Portal:TunnelOnly`와 실제 허용 호스트를 설정한다.
4. 원본 인증서의 호스트 이름·신뢰 체인을 맞춘다. `noTLSVerify`로 인증서 검증을 끄지 않는다. `config/cloudflared.example.yml`과 학교 서버 운영 안내를 따른다.
5. 내부 운영 감시 경로를 공개하지 않고 개인화 API 응답을 캐시하지 않는다. 터널 토큰·연결 서명 키는 Git에 넣지 않는다.
6. 학교 밖 휴대폰으로 공개 HTTPS·로그인·대상 공지·비대상 접근 차단을 확인한다. 학교 소식 서버에서 교내 PC·업무 서버로 접근이 차단되는지도 확인한다.

터널은 익명화 도구가 아니며, 학교 회선의 트래픽과 서비스를 학교·회선 운영자가 관찰할 수 있다. [Cloudflare 공식 설치 안내](https://developers.cloudflare.com/tunnel/get-started/).

## Windows: 교직원에게 배포하기

1. `artifacts/yymessenger-1.0.0-setup-win-x64.exe`를 전달한다. 학교 서버에는 별도의 서버 패키지를 설치한다.
2. 교직원이 한국어 설치 마법사의 설치 위치·바로가기·설치·완료를 따라 진행한다. 기본 위치는 `%LOCALAPPDATA%\Programs\YeoyangSchoolMessenger`이고 현재 Windows 사용자에게 설치한다.
3. 프로그램에서 학교가 안내한 **교내 HTTPS 서버 주소**를 입력한다. 관리자에게 받은 로그인 이름·임시 비밀번호로 로그인하고, 개별 활성화 번호·약관 동의·개인 비밀번호 설정을 완료한다. 자리 IP만으로 인증하지 않는다.
4. 개인 PC에서는 자동 실행·자동 로그인을 선택할 수 있다. 공용 PC에서는 켜지 않는다. 자동 로그인은 비밀번호를 보관하는 대신 보호된 세션을 사용한다. 자동 실행은 Windows 사용자 로그인 후 실행이다.
5. 관리자 계정으로 로그인하면 관리 창에서 교직원 이름·IP·직책, 권한, 특별실 목록 등 허용된 항목을 관리한다. 학생·학부모 명부와 담당 학급 연결도 학교 소식 관리에서 확인한다.

업데이트는 알림 영역에서 프로그램을 **종료**한 뒤 새 설치 파일을 실행한다. X 버튼은 숨김 동작이다. 설치 위치와 앱 식별자를 유지하고, 서버는 같은 데이터 경로와 비밀키를 보존해 갱신한다. 본문 사본·설정은 설치 폴더가 아닌 사용자별 보호된 저장소에 저장되며, 서버에도 메시지 DB가 존재한다. 설치 제거가 계정 탈퇴나 서버 데이터 삭제를 뜻하지 않는다. [설치·업데이트 상세](windows-installer.md).

이번 서버 보완에는 본문을 읽기 전에 검사하는 필수 서명 헤더가 추가됐다. **교내 업무 서버와 학교 소식 서버를 같은 최신 서버 패키지로 함께 갱신**한다. 한쪽만 구버전이면 서버 간 연결이 거부된다. 통신 키와 데이터 경로를 보존하고 두 서버 연결 상태·합성 공지·시간표·상담을 확인한 뒤 일반 이용을 재개한다.

## Android: 두 앱을 따로 설치하기

1. 교직원은 `artifacts/mobile/kr.school.yeoyang.teacher-Signed.apk`, 학생·학부모는 `artifacts/mobile/kr.school.yeoyang.family-Signed.apk`를 사용한다. 현재 로컬 APK는 Debug 시운행용이다. Google Play 출시용으로 사용하지 않는다.
2. 관리된 시험 휴대폰에 해당 APK를 설치한다. 출처 확인 없이 설치하지 않고, APK 설치를 위해 부여한 파일 앱의 설치 권한은 시험 후 해제한다.
3. 교무실 앱은 교내 업무 서버 및 학교 소식 서버 주소를, 학교 소식 앱은 공개 학교 소식 서버 주소를 설정한다. 실제 학교 서버는 HTTPS 인증서 검증을 통과해야 한다. Debug의 HTTP 예외는 로컬 시험 주소에만 해당한다.
4. 교직원 계정 최초 활성화는 Windows에서 완료한다. 학생 계정은 학교 명부에 맞게 등록·승인한다. 학부모는 본인 임시계정에서 자녀 고유번호를 추가한다. 운영 기본은 학교 확인이며, 학교가 번호 일치 즉시 전환 정책을 공식 승인한 경우에만 `CodeOnly`와 승인 설정을 사용한다. 재학 자녀를 여러 명 연결할 수 있다.
5. 실제 휴대폰에서 앱 종료·재실행·로그아웃·계정 중지, 공지 이미지, 상담 신청·취소, 시간표·급식, 접속 실패 안내를 확인한다.

개발 PC에서 시운행 APK를 다시 만들 때:

```powershell
# SDK/워크로드를 처음 준비할 때만 실행
dotnet workload install maui-android --skip-manifest-update
.\scripts\Build-Mobile.ps1 -SchoolApp Teacher
.\scripts\Build-Mobile.ps1 -SchoolApp Family
```

필요한 도구는 .NET SDK 10.0.402, MAUI Android 워크로드, Android SDK, JDK다. 한글 경로 문제를 피하는 별도 ASCII 빌드 작업 경로는 `-BuildDirectory`, 결과 경로는 `-OutputDirectory`로 지정할 수 있다. 소스 작업 위치와 별개인 임시 빌드 경로다.

Google Play 출시는 학교 개발자 계정, 두 앱의 확정된 식별자, 별도 보관·백업한 업로드 서명 키, 학교 정책·스토어 개인정보 신고, **Release 서명 AAB**를 준비해 내부 시험 후 심사를 진행한다. Debug 서명 키를 출시 키로 재사용하지 않는다. 현재 스크립트는 시운행 APK 생성용이며 스토어 업로드를 수행하지 않는다. [Microsoft Android 배포 안내](https://learn.microsoft.com/en-us/dotnet/maui/android/deployment/publish-cli?view=net-maui-10.0).

## iOS: Mac에서 두 앱을 만들고 TestFlight로 시험하기

Windows에서 IPA를 직접 생성할 수 없다. 실제 iPhone 서명 빌드·설치는 아직 수행하지 않았다. 2026-10-15 [Mac 검사](https://github.com/yd043105-pixel/yymessenger/actions/runs/38658164382/job/116172780205)에서는 동일 모바일 소스의 iPhone 시뮬레이터 자동 검사 38개와 두 제품의 Release 빌드 검사가 통과했다. 실제 기기·전체 기능 시험을 대신하지 않는다. [iOS 운영 안내](ios-operations.md)에 범위를 기록했다.

1. 학교 소유 Apple Developer 계정과 App Store Connect에서 두 앱의 Bundle ID를 준비한다. Mac·호환 Xcode·.NET SDK 10.0.402와 고정 iOS 워크로드를 준비한다. 정확한 Xcode/워크로드 조합은 현재 CI 설정과 Microsoft iOS SDK 지원표를 맞춘다.
2. 먼저 각각 시뮬레이터를 빌드한다. ZIP은 iPhone 설치 파일이 아니다.

```bash
dotnet workload install maui-ios --version 10.0.401
YY_SCHOOL_APP=Teacher bash scripts/Build-iOS.sh simulator
YY_SCHOOL_APP=Family bash scripts/Build-iOS.sh simulator
```

3. 실제 배포 지시 후 Mac Keychain에 학교 Apple Distribution 인증서·개인키와 각 앱의 App Store provisioning profile을 준비한다. 두 앱의 프로파일은 각각 해당 Bundle ID와 일치해야 한다. 비밀값은 Git에 넣지 않는다.

```bash
# YY_IOS_SIGNING_IDENTITY와 해당 앱 YY_IOS_PROFILE을 Mac에서 설정한 뒤
YY_SCHOOL_APP=Teacher bash scripts/Build-iOS.sh archive
# 학생·학부모 앱에 맞는 프로파일로 바꾼 뒤
YY_SCHOOL_APP=Family bash scripts/Build-iOS.sh archive
```

4. 만들어진 서명 IPA를 Apple이 지원하는 업로드 도구로 App Store Connect에 올리고, 처리 완료 후 TestFlight에서 두 앱을 각각 시험한다. 스크립트는 업로드·심사·공개를 자동 실행하지 않는다.
5. 실제 iPhone/iPad에서 HTTPS, 교무실 앱의 교내망/VPN, 로컬 네트워크 권한, Keychain 로그인 복원·재설치·로그아웃, 화면 가림, 첨부 선택, 상담·시간표를 확인한 뒤 학교가 출시를 결정한다. [Apple 업로드 안내](https://developer.apple.com/help/app-store-connect/manage-builds/upload-builds), [Microsoft iOS 배포 안내](https://learn.microsoft.com/en-us/dotnet/maui/ios/deployment/publish-cli?view=net-maui-10.0).

## 출시 전에 남아 있는 항목

1. 공개 도메인·운영 인증서·망 격리·UPS·별도 백업 복원·SMTP 실제 전달을 검증한다. 현재 공개 주소가 없어 공개 HTTPS 보안 검사를 수행하지 않았다.
2. 약관·개인정보 처리·보호자 확인·졸업 및 퇴직 처리·기기 분실 대응을 학교가 확정한다. 졸업 파기와 별개로 앱 내 탈퇴/삭제 신청 절차와 실제 삭제 처리는 아직 완성되지 않았다. 앱에서 가입을 제공하므로 Apple·Google의 삭제 요구사항에 맞는 절차가 출시 전에 필요하다. Google은 앱 밖의 삭제 신청 링크도 요구한다. 이는 통신용 앱을 웹앱으로 바꾸라는 의미가 아니다. [Apple 계정 삭제 기준](https://developer.apple.com/help/app-review/guideline-reference/5-1-1-account-deletion), [Google 계정 삭제 기준](https://support.google.com/googleplay/android-developer/answer/13327111).
3. 관리자 앱 계정의 다중 인증은 아직 미구현이다. Cloudflare 관리자 2단계 인증이 앱 관리자 인증을 대신하지 않는다. 최고권한은 학교가 지정한 계정·관리 환경으로 제한하고 출시 전 보강 범위를 정한다.
4. **앱이 닫힌 상태의 FCM/APNs 푸시는 아직 없다.** 현재는 앱에서 열기·새로고침으로 확인한다. 원래 요구한 즉시 휴대폰 알림을 제공하려면 푸시·기기 토큰 폐기·잠금 화면 정보 최소화·실기기 검증이 필요하다.
5. NEIS 운영 키·학교 코드와 실패 시 대체 안내를 확인한다. 학생별 수강 명부는 아직 제공되지 않아 완전한 학생 개인 시간표는 준비되지 않았다. 업로드한 학급 기초/일자별 시간표와 교직원 시간표 기능을 먼저 시험한다.
6. Windows 설치 파일의 학교 배포 서명, Android Release 서명/AAB, iOS IPA와 스토어 심사를 준비한다. SHA256 일치는 공급자 코드 서명이나 보안 인증이 아니다. Debug APK는 출시 완료 증거가 아니다.

개발·시운행 버전은 **1.0.0**으로 유지한다. GitHub에는 소스를 올리고 검토 PR을 유지하되, 학교가 배포한다고 지시하기 전에는 릴리즈·태그·스토어 공개를 하지 않는다. Windows/서버는 GitHub 릴리즈 배포 경로를 사용할 수 있지만 Android/iOS 업데이트는 각 스토어 배포 절차를 따로 거친다.

# iPhone·iPad 앱 준비

PC·Android와 같은 승인된 모바일 설계, 계정·권한·공지 API를 사용하는 .NET MAUI iOS 앱이다. 버전은 1.0.0이며 담당자의 실제 배포 지시 전까지 올리지 않는다. 이 문서는 빌드와 시험 방법을 설명하며 실제 iPhone 설치·스토어 출시가 완료됐다는 뜻이 아니다.

## Mac 빌드

macOS 26.2 이상과 Xcode 26.6, .NET SDK 10.0.401을 준비하고 `dotnet workload install maui-ios --version 10.0.401`로 고정된 워크로드를 설치한다. Mac에서는 iOS 대상을 기본으로 빌드하며 Android 잠금 파일과 별도의 `packages.ios.lock.json`을 사용한다. [Microsoft iOS SDK 릴리즈](https://github.com/dotnet/macios/releases/tag/dotnet-10.0.1xx-xcode26.5-10318)와 [GitHub Mac 실행 환경](https://github.com/actions/runner-images/blob/main/images/macos/macos-26-Readme.md)을 기준으로 버전을 맞춘다.

`bash scripts/Build-iOS.sh simulator`는 Mac CPU에 맞는 시뮬레이터 앱과 `artifacts/ios/yymessenger-1.0.0-ios-simulator.zip`을 생성한다. 이 ZIP은 iPhone 설치 파일이 아니며 Mac의 iOS Simulator에서만 사용한다. Windows에서 이 스크립트로 실제 iOS 앱을 빌드했다고 표시하지 않는다.

GitHub Verify의 iOS 작업은 Mac에서 네이티브 앱을 빌드하고 별도 가상 교직원·학급·보호자·공지 서버와 새 iPhone 시뮬레이터를 사용한다. 검사 전용 코드는 `IosVerification=true`인 Debug 빌드에만 포함하며 Release·일반 앱에는 포함하지 않는다. 결과 JSON과 시뮬레이터 화면을 Actions artifact에 저장한다. 비밀번호·세션·가상 서버 DB·연결 키는 artifact에 포함하지 않는다.

## 로그인·화면 보호

iOS 세션은 잠금이 풀린 해당 기기에 한정하는 Keychain 접근 등급을 사용한다. iOS Keychain 항목이 앱 제거 뒤에도 남을 수 있으므로, 최초 실행 표시가 없는 새 설치는 앱의 이전 보안 저장 항목을 지운다. 보통의 앱 재실행에서는 로그인 정보를 유지하고 로그아웃하면 서버 세션과 로컬 저장을 제거한다. [Microsoft 보안 저장소 안내](https://learn.microsoft.com/en-us/dotnet/maui/platform-integration/storage/secure-storage?view=net-maui-10.0)

비활성화 시 기본 창 전체를 가려 최근 앱 화면의 제목·본문·팝업 노출을 줄인다. 백그라운드 복귀는 기존 모바일 앱의 세션 재확인 절차를 사용한다. iOS의 사용자가 직접 촬영하는 스크린샷까지 차단한다고 보장하지 않는다.

교내 Wi-Fi·학교 VPN의 HTTPS 서버에 연결할 때 필요한 로컬 네트워크 접근 목적을 한국어로 표시한다. 카메라·사진첩 전체 접근은 요청하지 않으며 첨부 선택은 기본 문서 선택기, 저장·공유는 기본 공유 창을 사용한다. Release는 HTTPS만 허용한다. Debug의 ATS 예외는 `localhost` 시험 서버에 한정하며 인증서 검증을 끄지 않는다.

## 실제 iPhone·TestFlight 준비

학교가 관리할 Apple Developer 계정, 확정할 Bundle ID, Apple Distribution 인증서와 개인키, 그 Bundle ID의 App Store provisioning profile이 필요하다. 개인키·인증서 비밀번호·프로파일은 Git에 올리지 않는다. 인증서는 학교의 Mac Keychain에 준비한다.

학교가 배포를 지시한 후 서명 환경변수 `YY_IOS_SIGNING_IDENTITY`, `YY_IOS_PROFILE`을 설정하고 `bash scripts/Build-iOS.sh archive`로 Release archive와 IPA를 생성한다. 이 명령은 자동 업로드·심사 신청·스토어 공개를 하지 않는다. [Microsoft iOS 배포 안내](https://learn.microsoft.com/en-us/dotnet/maui/ios/deployment/publish-cli?view=net-maui-10.0)

실제 iPhone·iPad에서 학교 HTTPS 접속, 교내망 권한 거절·허용, 파일 앱 선택·공유, 로그인 복원·로그아웃·재설치, 잠금·최근 앱 화면, 가로 화면·큰 글꼴·VoiceOver를 확인한 뒤 TestFlight 시험과 심사를 진행한다. 서버 주소·학교 명부·개인정보 정책도 실제 운영 값으로 준비해야 한다. APNs 백그라운드 푸시는 아직 구현하지 않았다.

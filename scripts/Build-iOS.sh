#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
if [[ "$(uname -s)" != Darwin ]]; then
  echo 'iOS builds require macOS and Xcode. Use the GitHub iOS verification job.' >&2
  exit 1
fi
project=src/SchoolMessenger.Mobile/SchoolMessenger.Mobile.csproj
mode=${1:-simulator}
version=$(python3 -c 'import json; print(json.load(open("src/SchoolMessenger.Shared/updates.json"))["version"])')
common_version=$(python3 -c 'import xml.etree.ElementTree as e; print(e.parse("Directory.Build.props").findtext("./PropertyGroup/Version"))')
[[ "$version" == "$common_version" ]] || { echo 'Bundled and common versions differ.' >&2; exit 1; }
mkdir -p artifacts/ios
dotnet restore "$project" --locked-mode --nologo
case "$mode" in
  simulator)
    runtime=iossimulator-x64
    [[ "$(uname -m)" != arm64 ]] || runtime=iossimulator-arm64
    dotnet build "$project" -f net10.0-ios -c Debug -r "$runtime" --no-restore --nologo "-p:IosVerification=${IOS_VERIFICATION:-false}"
    bundle="src/SchoolMessenger.Mobile/bin/Debug/net10.0-ios/$runtime/SchoolMessenger.Mobile.app"
    /usr/bin/ditto -c -k --keepParent "$bundle" "artifacts/ios/yymessenger-$version-ios-simulator.zip"
    ;;
  archive)
    : "${YY_IOS_SIGNING_IDENTITY:?Set the installed Apple Distribution identity}"
    : "${YY_IOS_PROFILE:?Set the matching App Store provisioning profile}"
    dotnet publish "$project" -f net10.0-ios -c Release -r ios-arm64 --no-restore --nologo \
      -p:ArchiveOnBuild=true -p:BuildIpa=true "-p:CodesignKey=$YY_IOS_SIGNING_IDENTITY" "-p:CodesignProvision=$YY_IOS_PROFILE" \
      -p:EnableCodeSigning=true -o "$PWD/artifacts/ios/archive"
    ;;
  *) echo 'Usage: bash scripts/Build-iOS.sh [simulator|archive]' >&2; exit 1 ;;
esac
echo "Built iOS $version ($mode). No TestFlight or App Store upload was performed."

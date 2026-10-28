param([ValidateSet('Debug','Release')][string]$Configuration='Debug',[string]$AndroidSdkDirectory,[string]$JavaSdkDirectory,[ValidateSet('Combined','Teacher','Family')][string]$SchoolApp='Family',[string]$BuildDirectory,[string]$OutputDirectory)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$dotnetExecutable=Join-Path $projectRoot '.tools\dotnet\dotnet.exe'
if(-not(Test-Path -LiteralPath $dotnetExecutable)){$dotnetExecutable=(Get-Command dotnet -ErrorAction Stop).Source}
if(-not $AndroidSdkDirectory){$AndroidSdkDirectory=Join-Path $projectRoot '.tools\android'}
if(-not $JavaSdkDirectory){$JavaSdkDirectory=Join-Path $projectRoot '.tools\java'}
if(-not(Test-Path -LiteralPath $AndroidSdkDirectory)){throw 'Android SDK path is required.'}
if(-not(Test-Path -LiteralPath $JavaSdkDirectory)){throw 'Java SDK path is required.'}
# AAPT2 cannot build resources in a Unicode project path. Stage only source into a dedicated ASCII directory.
if(-not $BuildDirectory){$BuildDirectory=Join-Path $env:LOCALAPPDATA 'YyMessengerBuild'}
$stageRoot=Join-Path $BuildDirectory ([guid]::NewGuid().ToString('N'))
if($stageRoot -match '[^\x00-\x7F]'){throw 'Set LOCALAPPDATA to an ASCII build directory before building Android.'}
New-Item -ItemType Directory -Path $stageRoot | Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'global.json'),(Join-Path $projectRoot 'Directory.Build.props') -Destination $stageRoot
New-Item -ItemType Directory -Path (Join-Path $stageRoot 'config') -Force|Out-Null
Copy-Item -LiteralPath (Join-Path $projectRoot 'config\school-directory.json') -Destination (Join-Path $stageRoot 'config')
foreach($project in @('SchoolMessenger.Mobile','SchoolMessenger.Contracts','SchoolMessenger.Shared')){
    $source=Join-Path $projectRoot "src\$project"
    $target=Join-Path $stageRoot "src\$project"
    New-Item -ItemType Directory -Path $target -Force | Out-Null
    Get-ChildItem -LiteralPath $source -Force | Where-Object {$_.Name -notin @('bin','obj')} | Copy-Item -Destination $target -Recurse
}
New-Item -ItemType Junction -Path (Join-Path $stageRoot 'android-sdk') -Target (Resolve-Path -LiteralPath $AndroidSdkDirectory).Path | Out-Null
New-Item -ItemType Junction -Path (Join-Path $stageRoot 'java-sdk') -Target (Resolve-Path -LiteralPath $JavaSdkDirectory).Path | Out-Null
$env:DOTNET_ROOT=Split-Path $dotnetExecutable -Parent
$buildArguments=@('build',(Join-Path $stageRoot 'src\SchoolMessenger.Mobile'),'-f','net10.0-android','-c',$Configuration,'-p:RestoreLockedMode=true',"-p:AndroidSdkDirectory=$stageRoot\android-sdk","-p:JavaSdkDirectory=$stageRoot\java-sdk",'--nologo','-v','minimal')
$buildArguments+="-p:SchoolApp=$SchoolApp"
# MAUI SingleProject overwrites the project-level manifest property. A global property keeps the scoped debug manifest.
if($Configuration -eq 'Debug'){$buildArguments+='-p:AndroidManifest=Platforms/Android/AndroidManifest.Debug.xml'}
& $dotnetExecutable @buildArguments
if($LASTEXITCODE -ne 0){throw "Android build failed. Source staging directory: $stageRoot"}
$destination=if($OutputDirectory){[IO.Path]::GetFullPath($OutputDirectory)}else{Join-Path $projectRoot 'artifacts\mobile'}
New-Item -ItemType Directory -Force -Path $destination | Out-Null
$packages=Get-ChildItem -LiteralPath (Join-Path $stageRoot "src\SchoolMessenger.Mobile\bin\$Configuration\net10.0-android") -Filter '*-Signed.apk' -Recurse -File
if(-not $packages){throw 'No APK produced.'}
$packages | Copy-Item -Destination $destination -Force
Copy-Item -LiteralPath (Join-Path $stageRoot 'src\SchoolMessenger.Mobile\packages.lock.json') -Destination (Join-Path $projectRoot 'src\SchoolMessenger.Mobile\packages.lock.json') -Force
Write-Host "Android APK: $destination ($SchoolApp; $Configuration; local signing, not a store release)."

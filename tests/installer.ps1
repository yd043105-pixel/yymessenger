param([string]$OutputDirectory, [string]$ClientDirectory, [string]$Compiler, [switch]$RunNativeUi)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
if (-not $OutputDirectory) { $OutputDirectory = Join-Path $projectRoot 'artifacts' }
if (-not $ClientDirectory) { $ClientDirectory = Join-Path $projectRoot 'artifacts\여양고-교무메신저\client' }
if (-not $Compiler) { $Compiler = Join-Path $projectRoot '.tools\inno-6.7.3\ISCC.exe' }
$version = [string]([xml](Get-Content -LiteralPath (Join-Path $projectRoot 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
$productionSetup = Join-Path $OutputDirectory "yymessenger-$version-setup-win-x64.exe"
$expected = (Get-Content -LiteralPath (Join-Path $OutputDirectory 'SHA256SUMS.txt') | Where-Object { $_.EndsWith("  yymessenger-$version-setup-win-x64.exe") }).Split(' ')[0]
if ((Get-FileHash -LiteralPath $productionSetup -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected) { throw 'Installer hash mismatch.' }
$testGuid = [Guid]::NewGuid().ToString()
$testBase = if ($env:YY_TEST_DATA_ROOT) { $env:YY_TEST_DATA_ROOT } else { Join-Path $projectRoot '.test-data' }
$testRoot = [IO.Path]::GetFullPath((Join-Path $testBase "installer-$testGuid"))
$installDirectory = Join-Path $testRoot 'installed'
$regName = "Software\Microsoft\Windows\CurrentVersion\Uninstall\{$testGuid}_is1"
$startupEntry = "YyMessengerInstallerTest-$testGuid"
$runPath = 'Software\Microsoft\Windows\CurrentVersion\Run'
New-Item -ItemType Directory -Force -Path (Join-Path $testRoot 'temp') | Out-Null
$oldTemp=$env:TEMP; $oldTmp=$env:TMP; $oldClient=$env:YY_INSTALLED_CLIENT
$passed = [Collections.Generic.List[string]]::new()
function Check([string]$message) { $passed.Add($message); Write-Host "PASS $message" }
function Install([string]$log, [switch]$ExpectFailure,[string]$Tasks='',[switch]$CreateIcons) {
    $args = @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-',"/TASKS=$Tasks", ('/DIR="'+$installDirectory+'"'), ('/LOG="'+(Join-Path $testRoot $log)+'"'))
    if(-not $CreateIcons){$args+='/NOICONS'}
    $result = Start-Process -FilePath $testSetup -ArgumentList $args -WindowStyle Hidden -Wait -PassThru
    if ($ExpectFailure) { if ($result.ExitCode -eq 0) { throw 'Installer must reject this operation.' } }
    elseif ($result.ExitCode -ne 0) { throw "Installer failed: $($result.ExitCode) ($log)" }
}
function Uninstall {
    $result=Start-Process -FilePath (Join-Path $installDirectory 'unins000.exe') -ArgumentList @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') -WindowStyle Hidden -Wait -PassThru
    if($result.ExitCode -ne 0){throw 'Uninstall failed.'}
}
function UninstallKey { return [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($regName,$true) }
try {
    $env:TEMP=Join-Path $testRoot 'temp'; $env:TMP=$env:TEMP
    & (Join-Path $projectRoot 'scripts\Build-Installer.ps1') -OutputDirectory (Join-Path $testRoot 'setup') -ClientDirectory $ClientDirectory -Compiler $Compiler -InstallerAppId "{$testGuid}"
    if($LASTEXITCODE -ne 0){throw 'Test installer build failed.'}
    $testSetup=Join-Path $testRoot "setup\yymessenger-$version-setup-win-x64.exe"
    Check 'verified production installer hash; isolated installer uses its own app ID and startup entry'
    Install 'first-install.log'
    $installedExe=Join-Path $installDirectory 'SchoolMessenger.exe'
    foreach($file in Get-ChildItem -LiteralPath $ClientDirectory -File | Where-Object Extension -ne '.pdb'){
        if((Get-FileHash -LiteralPath (Join-Path $installDirectory $file.Name)).Hash -ne (Get-FileHash -LiteralPath $file.FullName).Hash){throw 'Installed payload differs.'}
    }
    $key=UninstallKey; if($null -eq $key){throw 'Windows uninstall entry missing.'}; $key.Dispose()
    Check 'silent per-user installation includes the self-contained executable, native libraries and Windows uninstall entry'
    $sentinel=Join-Path $installDirectory 'user-preserved.txt'; Set-Content -LiteralPath $sentinel -Value 'FAKE-USER-DATA-PRESERVE' -Encoding ASCII
    $sentinelHash=(Get-FileHash -LiteralPath $sentinel).Hash
    $key=UninstallKey; $key.SetValue('DisplayVersion','0.9.0'); $key.Dispose()
    Install 'upgrade.log' -CreateIcons -Tasks 'desktopicon,autostart'
    $desktop=Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) ($startupEntry+'.lnk')
    if(-not(Test-Path -LiteralPath $desktop)){throw 'Selected client desktop shortcut missing'}
    $run=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($runPath);try{if($run.GetValue($startupEntry) -ne ('"'+$installedExe+'" --startup')){throw 'Selected client startup option missing'}}finally{$run.Dispose()}
    Check 'Installer desktop and Windows-login startup checkboxes create the requested shortcut and own current-user startup entry'
    Install 'startup-disabled.log'
    $run=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($runPath);try{if($null -ne $run.GetValue($startupEntry)){throw 'Client startup option remained after deselection'}}finally{$run.Dispose()}
    Check 'Deselecting automatic startup removes this installation startup entry'
    if((Get-FileHash -LiteralPath $sentinel).Hash -ne $sentinelHash){throw 'Upgrade changed user data.'}
    $key=UninstallKey; if($key.GetValue('DisplayVersion') -ne $version){throw 'Upgrade version mismatch.'}; $key.Dispose()
    Check 'upgrade reuses the existing installation and preserves unrelated user files'
    $key=UninstallKey; $key.SetValue('DisplayVersion','9.0.0'); $key.Dispose()
    Install 'downgrade-rejected.log' -ExpectFailure
    $key=UninstallKey; if($key.GetValue('DisplayVersion') -ne '9.0.0'){throw 'Downgrade replaced an installed version.'}; $key.SetValue('DisplayVersion',$version); $key.Dispose()
    Check 'older installer cannot overwrite a newer installed version'
    $created=$false; $mutex=[Threading.Mutex]::new($false,'Local\YeoyangSchoolMessenger.Running',[ref]$created)
    try { if(-not $created){throw 'A real messenger is running; close it before installer testing.'}; Install 'running-app-rejected.log' -ExpectFailure } finally { $mutex.Dispose() }
    Check 'installation is blocked while the messenger running marker exists'
    if($RunNativeUi){
        $env:YY_INSTALLED_CLIENT=$installedExe
        Push-Location $projectRoot
        try{$nativeArguments=@('tests/smoke.mjs','--ui','--skip-native-input');if($env:YY_PACKAGE_ROOT){$nativeArguments+='--package'};node @nativeArguments;if($LASTEXITCODE -ne 0){throw 'Installed native UI failed.'}}finally{Pop-Location}
        Check 'installed executable passes actual native messaging UI and feature checks'
    }
    $run=[Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($runPath);$run.SetValue($startupEntry,'"C:\FakePortable\SchoolMessenger.exe" --startup');$run.Dispose()
    Uninstall
    if(Test-Path -LiteralPath $installedExe){throw 'Uninstall left the executable installed.'}
    if((Get-FileHash -LiteralPath $sentinel).Hash -ne $sentinelHash){throw 'Uninstall deleted unrelated user files.'}
    $run=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($runPath);if($null -eq $run.GetValue($startupEntry)){throw 'Uninstall removed another installation startup entry.'};$run.Dispose()
    Check 'uninstall removes the executable but preserves user files and a startup entry targeting another installation'
    Install 'reinstall.log'
    $run=[Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($runPath);$run.SetValue($startupEntry,'"'+$installedExe+'" --startup');$run.Dispose()
    Uninstall
    $run=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($runPath);if($null -ne $run.GetValue($startupEntry)){throw 'Uninstall left its own startup entry.'};$run.Dispose()
    Check 'reinstallation succeeds and uninstall removes only its own startup entry'
    $report=Join-Path $OutputDirectory 'installer-checks.json';@{version=$version;checks=@($passed);testDirectory=$testRoot;nativeUi=[bool]$RunNativeUi}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath $report -Encoding UTF8
} finally {
    $env:TEMP=$oldTemp; $env:TMP=$oldTmp; $env:YY_INSTALLED_CLIENT=$oldClient
    $run=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($runPath,$true);if($run){$run.DeleteValue($startupEntry,$false);$run.Dispose()}
}

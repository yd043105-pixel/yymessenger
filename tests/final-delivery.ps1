param()
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Split-Path $PSScriptRoot -Parent))
$artifacts=Join-Path $projectRoot 'artifacts'
$kit=Join-Path $projectRoot '01_설치파일'
$manifest=Get-Content -LiteralPath (Join-Path $kit '전달파일목록.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$version=[string]([xml](Get-Content -LiteralPath (Join-Path $projectRoot 'Directory.Build.props') -Raw)).Project.PropertyGroup.Version
$passed=@()
function Check([string]$Name){$script:passed+=$Name;Write-Host "PASS $Name"}
if($manifest.version -ne $version -or $manifest.releasePublished -ne $false -or $manifest.iosInstallable -ne $false -or $manifest.androidConfiguration -ne 'Debug'){throw 'Delivery status/version mismatch'}
foreach($file in $manifest.files){
    $target=[IO.Path]::GetFullPath((Join-Path (Join-Path $kit $file.device) $file.file))
    if(-not $target.StartsWith($kit+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Invalid delivery path'}
    if((Get-Item -LiteralPath $target).Length -ne $file.bytes -or (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash.ToLowerInvariant() -ne $file.sha256){throw "Delivery hash mismatch: $target"}
    $source=Join-Path $artifacts $(if($file.device -eq '03_안드로이드'){'mobile\'+$file.file}else{$file.file})
    if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $file.sha256){throw 'Original build changed after delivery preparation'}
}
if(@($manifest.files).Count -ne 7){throw 'Expected server installer, server ZIP, two client installer copies, two APKs and iOS source ZIP'}
Check 'Seven real delivery files match the canonical builds, byte lengths and SHA256 manifest; server/client installers and platform status are distinct'
Add-Type -AssemblyName System.IO.Compression.FileSystem
function StreamHash($Stream){$algorithm=[Security.Cryptography.SHA256]::Create();try{return [BitConverter]::ToString($algorithm.ComputeHash($Stream)).Replace('-','').ToLowerInvariant()}finally{$algorithm.Dispose()}}
$serverZip=[IO.Compression.ZipFile]::OpenRead((Join-Path $artifacts "yymessenger-$version-servers-win-x64.zip"))
try{
    $names=@($serverZip.Entries | ForEach-Object {$_.FullName.Replace('\','/')})
    foreach($name in @('server/SchoolMessenger.Server.exe','announcement-server/SchoolMessenger.AnnouncementServer.exe','scripts/Server-Operations.ps1','scripts/Configure-ServerOperations.ps1','config/server-operations.example.json','docs/final-test-runbook.md','VERSION.txt')){if($name -notin $names){throw "Missing server ZIP entry: $name"}}
    foreach($entry in $serverZip.Entries){
        $name=$entry.FullName.Replace('\','/')
        if($name -match '(^|/)\.\.?(/|$)|^client/|(^|/)([^/]*\.(db|sqlite3?|dpapi|pfx|pem|key)|login\.dat|\.env[^/]*|appsettings\.Production\.json|server-operations\.secrets\.xml)$'){throw 'Private data or invalid path in server ZIP'}
        if($entry.Name.Length -eq 0){continue}
        $stream=$entry.Open();try{$hash=StreamHash $stream}finally{$stream.Dispose()}
        $source=Join-Path (Join-Path $artifacts '여양고-교무메신저') $name
        if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $hash){throw "Stale server package entry: $name"}
    }
}finally{$serverZip.Dispose()}
Check 'Server ZIP extracts directly to the documented root, contains current published files and no client or operational secrets'
$iosZip=[IO.Compression.ZipFile]::OpenRead((Join-Path $artifacts "yymessenger-$version-ios-build-source.zip"))
try{
    $names=@()
    foreach($entry in $iosZip.Entries){
        if($entry.Name.Length -eq 0){continue}
        $name=$entry.FullName.Replace('\','/');$names+=$name
        if($name -match '(^|/)(bin|obj)(/|$)|(^|/)\.\.?(/|$)|\.(ipa|p12|pfx|mobileprovision|keystore|jks|db|dpapi)$'){throw 'Generated output, private file or invalid path in iOS source ZIP'}
        $source=[IO.Path]::GetFullPath((Join-Path $projectRoot $name))
        if(-not $source.StartsWith($projectRoot+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Invalid iOS source path'}
        $stream=$entry.Open();try{$hash=StreamHash $stream}finally{$stream.Dispose()}
        if((Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash.ToLowerInvariant() -ne $hash){throw "Stale iOS source: $name"}
    }
    foreach($project in @('SchoolMessenger.Mobile','SchoolMessenger.Contracts','SchoolMessenger.Shared')){
        $base=Join-Path $projectRoot "src\$project"
        foreach($file in Get-ChildItem -LiteralPath $base -Recurse -File | Where-Object {$_.FullName.Substring($base.Length) -notmatch '^\\(bin|obj)\\'}){
            $relative=$file.FullName.Substring($projectRoot.Length+1).Replace('\','/')
            if($relative -notin $names){throw "Missing iOS source: $relative"}
        }
    }
    foreach($required in @('global.json','Directory.Build.props','scripts/Build-iOS.sh','tests/IosRuntimeChecks.cs')){if($required -notin $names){throw "Missing iOS build input: $required"}}
}finally{$iosZip.Dispose()}
Check 'iOS ZIP includes the current complete mobile/shared/contracts source and locked build inputs, without claiming an installable IPA'
@{version=$version;verifiedAt=[DateTimeOffset]::UtcNow.ToString('o');passed=$passed}|ConvertTo-Json -Depth 4|Set-Content -LiteralPath (Join-Path $artifacts 'final-delivery-checks.json') -Encoding UTF8

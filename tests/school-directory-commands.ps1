param()
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
. (Join-Path $root 'scripts\Update-SchoolDirectory.ps1')
$folder=Join-Path $root ('.test-data\school-directory-'+[guid]::NewGuid().ToString())
New-Item -ItemType Directory -Path $folder|Out-Null
$office=Join-Path $folder 'office.json';$portal=Join-Path $folder 'portal.json';$list=Join-Path $folder 'schools.json'
function Save($value,[string]$path){$value|ConvertTo-Json -Depth 6|Set-Content -LiteralPath $path -Encoding UTF8}
$a=@{schema=1;schoolId='SCHOOL-A';schoolName='Synthetic A';service='office';address='https://office-a.example/'}
Save $a $office
if((Read-SchoolRegistration $office 'office').schoolId -cne 'SCHOOL-A'){throw 'Valid metadata rejected'}
Write-Host 'PASS Public registration metadata is read without accounts or credentials'
foreach($url in @('http://office.example/','https://localhost/','https://u:p@office.example/','https://office.example/api/','https://office.example/?secret=x')){
    $a.address=$url;Save $a $office;$rejected=$false;try{Read-SchoolRegistration $office 'office'|Out-Null}catch{$rejected=$true};if(-not $rejected){throw 'Unsafe address accepted'}
}
$a.address='https://office-a.example/';Save $a $office
$rejected=$false;try{Read-SchoolRegistration $office 'announcements'|Out-Null}catch{$rejected=$true};if(-not $rejected){throw 'Wrong role accepted'}
Write-Host 'PASS Registration refuses plaintext, loopback, credentials, paths, queries and wrong server roles'
$a['password']='not-real';Save $a $office;$rejected=$false;try{Read-SchoolRegistration $office 'office'|Out-Null}catch{$rejected=$true};if(-not $rejected){throw 'Credential-bearing metadata accepted'}
$a.Remove('password');Save $a $office
Write-Host 'PASS Registration refuses extra potentially private fields'
# This isolated command test mocks only the TLS identity probe. Production has no skip-verification flag.
$script:probes=0
function Confirm-SchoolRegistration($Request){$script:probes++}
Save @{schema=1;schools=@()} $list
Save @{schema=1;schoolId='SCHOOL-A';schoolName='Synthetic A';service='announcements';address='https://news-a.example/'} $portal
Update-SchoolDirectory $list $office $portal 'Synthetic region'
Update-SchoolDirectory $list $office '' 'Updated region'
$result=Get-Content -LiteralPath $list -Raw -Encoding UTF8|ConvertFrom-Json
if(@($result.schools).Count -ne 1 -or $result.schools[0].announcementsAddress -cne 'https://news-a.example/' -or $script:probes -ne 3){throw 'School merge or required probe failed'}
Write-Host 'PASS Adding two roles and updating one role preserves the other role and verifies each supplied server'
$a.schoolId='SCHOOL-B';$a.schoolName='Synthetic B';Save $a $office
$rejected=$false;try{Update-SchoolDirectory $list $office '' 'Other region'}catch{$rejected=$true};if(-not $rejected){throw 'Another school reused the endpoint'}
Write-Host 'PASS Another school cannot reuse an existing school endpoint'
Write-Host '5 registration command checks passed; no network or GitHub writes performed.'

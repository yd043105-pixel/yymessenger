param()
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
. (Join-Path $root 'scripts\Review-SchoolRegistration.ps1')
$folder=Join-Path $root ('.test-data\registration-review-'+[guid]::NewGuid().ToString())
New-Item -ItemType Directory -Path $folder|Out-Null
$requestFile=Join-Path $folder 'request.json';$directory=Join-Path $folder 'schools.json';$decisions=Join-Path $folder 'private-decisions'
function Save($value,[string]$file){$value|ConvertTo-Json -Depth 6|Set-Content -LiteralPath $file -Encoding UTF8}
$request=@{schema=1;status='pending';requestId=[guid]::NewGuid().ToString('N');schoolId='SCHOOL-A';schoolName='Synthetic A';region='Synthetic region';contactPhone='061-000-0000';createdAt=[DateTimeOffset]::UtcNow.ToString('O');officeAddress='https://office-a.example/';announcementsAddress='https://news-a.example/'}
Save $request $requestFile;Save @{schema=1;schools=@()} $directory
if((Read-PendingSchoolRequest $requestFile).status -cne 'pending'){throw 'Pending request unreadable'}
Write-Host 'PASS A school registration remains pending until a developer decision'
$original=(Get-FileHash -LiteralPath $directory).Hash
$rejected=$false;try{Review-SchoolRegistration $requestFile 'Approve' $directory 'Synthetic reviewer' '' $decisions $false $true}catch{$rejected=$true}
if(-not $rejected -or (Get-FileHash -LiteralPath $directory).Hash -ne $original){throw 'Unverified application was approved'}
Write-Host 'PASS Approval requires independent authority and public-address confirmation and leaves the directory unchanged on refusal'
# Synthetic local requests only. Production has no bypass flag for the TLS identity probe.
$script:probes=0;function Confirm-SchoolRegistration($Request){$script:probes++}
Review-SchoolRegistration $requestFile 'Approve' $directory 'Synthetic reviewer' '' $decisions $true $true
$public=Get-Content -LiteralPath $directory -Raw -Encoding UTF8
if($public -match 'contactPhone|061-000-0000|pending|requestId' -or $script:probes -ne 2){throw 'Private application data leaked or an endpoint was not checked'}
$record=Get-Content -LiteralPath (Join-Path $decisions ('school-registration-decision-'+$request.requestId+'.json')) -Raw -Encoding UTF8|ConvertFrom-Json
if($record.decision -cne 'approved' -or $record.published -ne $false -or $record.requestSha256 -ne (Get-FileHash -LiteralPath $requestFile).Hash){throw 'Decision evidence or publication status incorrect'}
Write-Host 'PASS Approval checks both endpoints, strips verification contact from the public list and saves a private request-hash decision without publishing'
$rejected=$false;try{Review-SchoolRegistration $requestFile 'Approve' $directory 'Synthetic reviewer' '' $decisions $true $true}catch{$rejected=$true};if(-not $rejected){throw 'Decision replay accepted'}
Write-Host 'PASS A decided request cannot be replayed as a new approval'
$approved=(Get-FileHash -LiteralPath $directory).Hash
$request.requestId=[guid]::NewGuid().ToString('N');Save $request $requestFile
Review-SchoolRegistration $requestFile 'Reject' $directory 'Synthetic reviewer' 'Synthetic rejection reason' $decisions $false $false
if((Get-FileHash -LiteralPath $directory).Hash -ne $approved){throw 'Rejection modified a public registration'}
Write-Host 'PASS Rejection records its reason privately and does not add, remove or change a public school'
$request.status='approved';Save $request $requestFile
$rejected=$false;try{Read-PendingSchoolRequest $requestFile|Out-Null}catch{$rejected=$true};if(-not $rejected){throw 'Self-approved application accepted'}
$request.status='pending';$request.contactPhone='010-0000-0000';Save $request $requestFile
$rejected=$false;try{Read-PendingSchoolRequest $requestFile|Out-Null}catch{$rejected=$true};if(-not $rejected){throw 'Personal mobile accepted'}
Write-Host 'PASS Self-approved files and personal mobile contact fields are rejected'
Write-Host '6 developer review checks passed; no email, network or GitHub writes performed.'

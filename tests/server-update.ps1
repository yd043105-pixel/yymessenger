param([Parameter(Mandatory=$true)][string]$Package,[Parameter(Mandatory=$true)][string]$PublishedRoot,[Parameter(Mandatory=$true)][string]$FixtureParent)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$fixture=Join-Path ([IO.Path]::GetFullPath($FixtureParent)) ('update-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $fixture|Out-Null
$hash=(Get-FileHash -LiteralPath $Package -Algorithm SHA256).Hash
$checks=[Collections.Generic.List[string]]::new()
function Stop-Fixture($Process){if($Process -and -not $Process.HasExited){Stop-Process -InputObject $Process;$Process.WaitForExit()}}
function Start-Fixture([string]$Exe,[string]$Folder,[int]$Port){
    $process=Start-Process -FilePath $Exe -ArgumentList @('--urls',('http://127.0.0.1:'+$Port)) -WorkingDirectory (Split-Path $Exe -Parent) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $Folder 'stdout.log') -RedirectStandardError (Join-Path $Folder 'stderr.log')
    for($i=0;$i -lt 100;$i++){
        if($process.HasExited){throw 'Synthetic update service exited'}
        try{if((Invoke-WebRequest -Uri ('http://127.0.0.1:'+$Port+'/health') -UseBasicParsing -TimeoutSec 1).StatusCode -eq 200){return $process}}catch{}
        Start-Sleep -Milliseconds 100
    }
    Stop-Fixture $process;throw 'Synthetic update service timeout'
}
foreach($service in @('office','announcements')){
    $folder=Join-Path $fixture $service;New-Item -ItemType Directory -Path $folder|Out-Null
    $settings=Join-Path $folder 'settings';New-Item -ItemType Directory -Path $settings|Out-Null
    $configPath=Join-Path $settings 'server-operations.local.json'
    $config=Get-Content -LiteralPath (Join-Path $root 'config\server-operations.example.json') -Raw|ConvertFrom-Json
    $config.service=$service;$config.tunnelOnly=$service -eq 'announcements';$config.publicHost='fixture.example.org';$config.allowedNetworks='192.0.2.0/24';$config.certificateThumbprint='0'*40
    $role=if($service -eq 'office'){'server'}else{'announcement-server'}
    $exeName=if($service -eq 'office'){'SchoolMessenger.Server.exe'}else{'SchoolMessenger.AnnouncementServer.exe'}
    $config.executable=Join-Path $PublishedRoot ($role+'\'+$exeName);$config.dataDirectory=Join-Path $folder 'data';$config.backupDirectory=Join-Path $folder 'backups'
    $config|ConvertTo-Json -Depth 8|Set-Content -LiteralPath $configPath -Encoding UTF8
    $bridgeKey='Synthetic-'+[Guid]::NewGuid().ToString()+[Guid]::NewGuid().ToString()
    [pscustomobject]@{monitorKey=(ConvertTo-SecureString ('Synthetic-'+[Guid]::NewGuid().ToString()+[Guid]::NewGuid().ToString()) -AsPlainText -Force);initialAdminPassword=$null;bridgeKey=(ConvertTo-SecureString $bridgeKey -AsPlainText -Force);smtpCredential=$null}|Export-Clixml -LiteralPath (Join-Path $settings 'server-operations.secrets.xml')
    $prefix=if($service -eq 'office'){'School'}else{'Portal'}
    $password='Synthetic-'+[Guid]::NewGuid().ToString()
    $values=@{ASPNETCORE_ENVIRONMENT='Development';Portal__BridgeKey=$bridgeKey;Announcements__Address='';ASPNETCORE_FORWARDEDHEADERS_ENABLED='false';DOTNET_FORWARDEDHEADERS_ENABLED='false'}
    $values[$prefix+'__DataDirectory']=$config.dataDirectory;$values[$prefix+'__BackupDirectory']=$config.backupDirectory;$values[$prefix+'__AdminPassword']=$password
    $old=@{};foreach($name in $values.Keys){$old[$name]=[Environment]::GetEnvironmentVariable($name,'Process');[Environment]::SetEnvironmentVariable($name,$values[$name],'Process')}
    $listener=[Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback,0);$listener.Start();$port=$listener.LocalEndpoint.Port;$listener.Stop()
    $process=$null
    try{
        $process=Start-Fixture $config.executable $folder $port;Stop-Fixture $process
        $sentinel=Join-Path $config.dataDirectory 'operator-fixture.txt';'Synthetic preserved operator file'|Set-Content -LiteralPath $sentinel -Encoding UTF8
        $before=(Get-FileHash -LiteralPath $sentinel).Hash
        $version=Join-Path $folder 'new-version'
        & (Join-Path $root 'scripts\Stage-ServerUpdate.ps1') -ConfigurationPath $configPath -Package $Package -ExpectedSha256 $hash -VersionDirectory $version -Apply
        $updated=Get-Content -LiteralPath $configPath -Raw|ConvertFrom-Json
        if($updated.executable -ne (Join-Path $version $exeName) -or $updated.dataDirectory -ne $config.dataDirectory -or $updated.backupDirectory -ne $config.backupDirectory -or (Get-FileHash -LiteralPath $sentinel).Hash -ne $before){throw 'Update changed persistent data or failed to switch executable'}
        if(-not(Test-Path -LiteralPath (Join-Path $settings 'server-operations.previous.local.json'))){throw 'Previous configuration not preserved'}
        $pattern=if($service -eq 'office'){'school-*.db'}else{'announcements-*.db'}
        if(-not(Get-ChildItem -LiteralPath $config.backupDirectory -Filter $pattern -File)){throw 'Pre-update verified backup missing'}
        $process=Start-Fixture $updated.executable $folder $port
        $session=New-Object Microsoft.PowerShell.Commands.WebRequestSession
        $first=Invoke-RestMethod -Uri ('http://127.0.0.1:'+$port+'/api/session') -WebSession $session
        Invoke-RestMethod -Uri ('http://127.0.0.1:'+$port+'/api/login') -Method Post -WebSession $session -Headers @{'X-CSRF-TOKEN'=$first.csrfToken} -ContentType 'application/json' -Body (@{username='admin';password=$password}|ConvertTo-Json)|Out-Null
        $signed=Invoke-RestMethod -Uri ('http://127.0.0.1:'+$port+'/api/session') -WebSession $session
        if(-not $signed.user){throw 'Existing administrator could not sign in after update'}
        $checks.Add($service+': real Release package validates, backs up offline, preserves settings/data and authenticates after version switch')
    }finally{Stop-Fixture $process;foreach($name in $old.Keys){[Environment]::SetEnvironmentVariable($name,$old[$name],'Process')}}
}
[pscustomobject]@{checks=$checks.ToArray();realSchoolData=$false;realEmailSent=$false;deploymentTasksChanged=$false}|ConvertTo-Json -Depth 4

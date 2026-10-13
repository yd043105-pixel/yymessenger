param([string]$OutputPath)
$ErrorActionPreference='Stop'
$os=Get-CimInstance Win32_OperatingSystem
$computer=Get-CimInstance Win32_ComputerSystem
$processor=Get-CimInstance Win32_Processor | Select-Object -First 1
$report=[pscustomobject]@{
    windows=$os.Caption
    windowsBuild=$os.BuildNumber
    cpu=$processor.Name
    physicalCores=$processor.NumberOfCores
    memoryGB=[Math]::Round($computer.TotalPhysicalMemory/1GB,1)
    virtualizationFirmwareEnabled=$processor.VirtualizationFirmwareEnabled
    secondLevelAddressTranslation=$processor.SecondLevelAddressTranslationExtensions
    hypervisorPresent=$computer.HypervisorPresent
    notes=@('이 점검은 설정을 변경하지 않습니다.','같은 물리 디스크의 다른 드라이브나 VM 디스크를 별도 백업 장치로 판단하지 마세요.','UPS·망 분리·운영 계정·게스트 OS 사용 권한은 별도 확인이 필요합니다.')
}
$report|ConvertTo-Json -Depth 4
if($OutputPath){$report|ConvertTo-Json -Depth 4|Set-Content -LiteralPath $OutputPath -Encoding UTF8}

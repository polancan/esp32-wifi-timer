#requires -RunAsAdministrator
param([string]$ServiceName = "ESP32TimerServer")
$svc = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($svc) {
    if ($svc.Status -ne 'Stopped') { Stop-Service $ServiceName -Force }
    sc.exe delete $ServiceName | Out-Null
    Write-Host "Sluzba odstranena. Data a programove soubory zustaly zachovany." -ForegroundColor Green
} else { Write-Host "Sluzba nebyla nalezena." }

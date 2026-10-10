#requires -RunAsAdministrator
param(
    [string]$ServiceName = "ESP32TimerServer",
    [string]$DisplayName = "ESP32 Wi-Fi Timer Server",
    [int]$Port = 8080
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $root "TimerServer.exe"
if (-not (Test-Path $exe)) { throw "TimerServer.exe nebyl nalezen: $exe" }

$existing = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
if ($existing) {
    if ($existing.Status -ne 'Stopped') { Stop-Service $ServiceName -Force }
    sc.exe delete $ServiceName | Out-Null
    Start-Sleep -Seconds 2
}

New-Service -Name $ServiceName -BinaryPathName ('"{0}"' -f $exe) -DisplayName $DisplayName -StartupType Automatic
sc.exe description $ServiceName "8kanalova Wi-Fi casomira - web/API/SQLite" | Out-Null
sc.exe failure $ServiceName reset= 86400 actions= restart/5000/restart/10000/restart/30000 | Out-Null

$ruleName = "ESP32 Timer Server TCP $Port"
if (-not (Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Action Allow -Protocol TCP -LocalPort $Port -Profile Private | Out-Null
}

Start-Service $ServiceName
Write-Host "Hotovo. Sluzba $ServiceName bezi. Web: http://localhost:$Port" -ForegroundColor Green

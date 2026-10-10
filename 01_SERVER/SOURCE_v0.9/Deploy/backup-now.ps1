param([string]$Url = "http://127.0.0.1:8080/api/admin/backup")
$ErrorActionPreference = "Stop"
$r = Invoke-RestMethod -Uri $Url -Method Post
Write-Host "Backup vytvoren: $($r.file)" -ForegroundColor Green

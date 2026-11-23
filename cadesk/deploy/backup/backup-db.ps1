<#
.SYNOPSIS
    Automated PostgreSQL Encrypted Backup Script for Windows / PowerShell.
.DESCRIPTION
    Creates pg_dump custom compressed backup, encrypts with OpenSSL AES-256-CBC,
    computes SHA-256 checksum, and applies retention pruning.
#>
[CmdletBinding()]
param(
    [string]$DbHost = $env:DB_HOST,
    [int]$DbPort = 5432,
    [string]$DbName = $env:DB_NAME,
    [string]$DbUser = $env:DB_USER,
    [string]$BackupDir = ".\backups",
    [string]$EncryptionKey = $env:BACKUP_ENCRYPTION_KEY
)

if (-not $DbHost) { $DbHost = "localhost" }
if (-not $DbName) { $DbName = "cadesk" }
if (-not $DbUser) { $DbUser = "cadesk_owner" }
if (-not $EncryptionKey) { $EncryptionKey = "default_secure_backup_key_change_in_production_123456" }

$timestamp = (Get-Date).ToUniversalTime().ToString("yyyyMMdd_HHmmssZ")
$dailyDir = Join-Path $BackupDir "daily"
$weeklyDir = Join-Path $BackupDir "weekly"
$monthlyDir = Join-Path $BackupDir "monthly"

New-Item -ItemType Directory -Force -Path $dailyDir, $weeklyDir, $monthlyDir | Out-Null

$rawDump = Join-Path $env:TEMP "$($DbName)_$timestamp.dump"
$encFile = Join-Path $dailyDir "$($DbName)_$timestamp.dump.enc"
$shaFile = Join-Path $dailyDir "$($DbName)_$timestamp.sha256"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Starting CaDesk PostgreSQL Backup (PowerShell): $timestamp" -ForegroundColor Cyan
Write-Host " Database: $DbName on $DbHost`:$DbPort" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. pg_dump
Write-Host "[1/4] Dumping database with pg_dump..." -ForegroundColor Yellow
& pg_dump -h $DbHost -p $DbPort -U $DbUser -d $DbName --format=custom --no-owner --no-privileges --file=$rawDump
if ($LASTEXITCODE -ne 0) {
    Write-Error "pg_dump failed with exit code $LASTEXITCODE"
    exit 1
}

# 2. Encrypt
Write-Host "[2/4] Encrypting with OpenSSL AES-256-CBC..." -ForegroundColor Yellow
& openssl enc -aes-256-cbc -salt -pbkdf2 -iter 100000 -in $rawDump -out $encFile -pass "pass:$EncryptionKey"
Remove-Item -Force $rawDump

# 3. Checksum
Write-Host "[3/4] Generating SHA-256 Checksum..." -ForegroundColor Yellow
$hash = (Get-FileHash -Algorithm SHA256 $encFile).Hash.ToLower()
"$hash  $(Split-Path $encFile -Leaf)" | Out-File -Encoding utf8 -FilePath $shaFile

# 4. GFS Rotation
$dayOfWeek = (Get-Date).DayOfWeek
$dayOfMonth = (Get-Date).Day
if ($dayOfWeek -eq [DayOfWeek]::Sunday) {
    Copy-Item $encFile -Destination $weeklyDir
    Copy-Item $shaFile -Destination $weeklyDir
}
if ($dayOfMonth -eq 1) {
    Copy-Item $encFile -Destination $monthlyDir
    Copy-Item $shaFile -Destination $monthlyDir
}

# 5. Pruning
Write-Host "[4/4] Pruning backups older than 7 days in daily..." -ForegroundColor Yellow
Get-ChildItem -Path $dailyDir -Filter "*.enc" | Where-Object { $_.CreationTimeUtc -lt (Get-Date).AddDays(-7) } | Remove-Item -Force
Get-ChildItem -Path $dailyDir -Filter "*.sha256" | Where-Object { $_.CreationTimeUtc -lt (Get-Date).AddDays(-7) } | Remove-Item -Force

Write-Host "==========================================================" -ForegroundColor Green
Write-Host " Backup Succeeded: $encFile" -ForegroundColor Green
Write-Host " Checksum: $hash" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Green

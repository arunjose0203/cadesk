<#
.SYNOPSIS
    Automated PostgreSQL Restore Script for Windows / PowerShell.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$EncryptedFile,
    [string]$TargetDb = "cadesk_restored",
    [string]$DbHost = $env:DB_HOST,
    [int]$DbPort = 5432,
    [string]$DbUser = $env:DB_USER,
    [string]$EncryptionKey = $env:BACKUP_ENCRYPTION_KEY
)

if (-not $DbHost) { $DbHost = "localhost" }
if (-not $DbUser) { $DbUser = "cadesk_owner" }
if (-not $EncryptionKey) { $EncryptionKey = "default_secure_backup_key_change_in_production_123456" }

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " Restoring CaDesk PostgreSQL Backup: $EncryptedFile" -ForegroundColor Cyan
Write-Host " Target Database: $TargetDb" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$decryptedDump = Join-Path $env:TEMP "cadesk_restore_temp.dump"

# Decrypt
Write-Host "[1/3] Decrypting archive..." -ForegroundColor Yellow
& openssl enc -d -aes-256-cbc -pbkdf2 -iter 100000 -in $EncryptedFile -out $decryptedDump -pass "pass:$EncryptionKey"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Decryption failed!"
    exit 1
}

# Create DB if needed
Write-Host "[2/3] Preparing target database $TargetDb..." -ForegroundColor Yellow
& psql -h $DbHost -p $DbPort -U $DbUser -d postgres -c "CREATE DATABASE `"$TargetDb`" OWNER `"$DbUser`";" 2>$null

# Restore
Write-Host "[3/3] Running pg_restore..." -ForegroundColor Yellow
& pg_restore -h $DbHost -p $DbPort -U $DbUser -d $TargetDb --clean --if-exists --no-owner --no-privileges $decryptedDump

Remove-Item -Force $decryptedDump
Write-Host "Restore complete." -ForegroundColor Green

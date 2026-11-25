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

# Ensure target DB exists (create if the role has CREATEDB; fail clearly if not)
Write-Host "[2/3] Preparing target database $TargetDb..." -ForegroundColor Yellow
$exists = & psql -h $DbHost -p $DbPort -U $DbUser -d postgres -tAc "SELECT 1 FROM pg_database WHERE datname = '$TargetDb';"
if ($LASTEXITCODE -ne 0) {
    Remove-Item -Force $decryptedDump -ErrorAction SilentlyContinue
    Write-Error "Could not query PostgreSQL server as user '$DbUser'."
    exit 1
}
if ("$exists".Trim() -ne "1") {
    & psql -h $DbHost -p $DbPort -U $DbUser -d postgres -c "CREATE DATABASE `"$TargetDb`" OWNER `"$DbUser`";"
    if ($LASTEXITCODE -ne 0) {
        Remove-Item -Force $decryptedDump -ErrorAction SilentlyContinue
        Write-Error "Database '$TargetDb' does not exist and '$DbUser' could not create it. Pre-create with: CREATE DATABASE `"$TargetDb`" OWNER `"$DbUser`";"
        exit 1
    }
}

# Restore
Write-Host "[3/3] Running pg_restore..." -ForegroundColor Yellow
& pg_restore -h $DbHost -p $DbPort -U $DbUser -d $TargetDb --clean --if-exists --no-owner --no-privileges $decryptedDump
$restoreExit = $LASTEXITCODE
Remove-Item -Force $decryptedDump -ErrorAction SilentlyContinue
if ($restoreExit -ne 0) {
    Write-Error "pg_restore failed with exit code $restoreExit!"
    exit $restoreExit
}
Write-Host "Restore complete." -ForegroundColor Green

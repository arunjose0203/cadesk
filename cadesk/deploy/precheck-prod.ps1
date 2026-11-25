<#
.SYNOPSIS
    Pre-launch validator and .env generator for CaDesk production/staging deployments.
.EXAMPLE
    .\deploy\precheck-prod.ps1 -InitEnv
    .\deploy\precheck-prod.ps1 -EnvFile .env -SiteUrl https://app.yourdomain.in
#>
param(
    [string]$EnvFile = ".env",
    [string]$TemplateFile = ".env.production.example",
    [switch]$InitEnv,
    [switch]$Force,
    [string]$SiteUrl = ""
)

$ErrorActionPreference = "Stop"

function New-RandomToken([int]$Bytes = 32, [switch]$Base64) {
    $buf = New-Object byte[] $Bytes
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $rng.GetBytes($buf)
    } finally {
        $rng.Dispose()
    }
    if ($Base64) { return [Convert]::ToBase64String($buf) }
    $chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789"
    -join ($buf | ForEach-Object { $chars[$_ % $chars.Length] })
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$envPath = if ([System.IO.Path]::IsPathRooted($EnvFile)) { $EnvFile } else { Join-Path $repoRoot $EnvFile }
$tplPath = if ([System.IO.Path]::IsPathRooted($TemplateFile)) { $TemplateFile } else { Join-Path $repoRoot $TemplateFile }

if ($InitEnv) {
    if ((Test-Path $envPath) -and -not $Force) {
        Write-Error "$envPath already exists. Pass -Force to overwrite."
        exit 1
    }
    if (-not (Test-Path $tplPath)) {
        Write-Error "Template file not found: $tplPath"
        exit 1
    }

    $content = Get-Content $tplPath -Raw
    $replacements = @{
        "CHANGE_ME_SUPERUSER_STRONG_PASSWORD_32_CHARS"                   = (New-RandomToken 32)
        "CHANGE_ME_APP_ROLE_PASSWORD_32_CHARS"                           = (New-RandomToken 32)
        "CHANGE_ME_OWNER_ROLE_PASSWORD_32_CHARS"                         = (New-RandomToken 32)
        "CHANGE_ME_GENERATE_WITH_OPENSSL_RAND_BASE64_64_CHARACTERS"      = (New-RandomToken 48 -Base64)
        "CHANGE_ME_GENERATE_STRONG_BACKUP_ENCRYPTION_KEY_32_CHARS"       = (New-RandomToken 32)
        "CHANGE_ME_GENERATE_RANDOM_WHATSAPP_VERIFY_TOKEN"                = (New-RandomToken 24)
    }
    foreach ($k in $replacements.Keys) {
        $content = $content.Replace($k, $replacements[$k])
    }
    [System.IO.File]::WriteAllText($envPath, $content.Replace("`r`n", "`n"), (New-Object System.Text.UTF8Encoding $false))
    Write-Host "Generated $envPath with cryptographically random DB/JWT/Backup/WhatsApp secrets." -ForegroundColor Green
    Write-Host "Next: set SITE_ADDRESS, LETSENCRYPT_EMAIL, EMAIL_*, and S3 credentials in $envPath." -ForegroundColor Yellow
}

$issues = New-Object System.Collections.Generic.List[string]
$warnings = New-Object System.Collections.Generic.List[string]

Write-Host "`n[1/3] Checking legal & help HTML bracket placeholders..." -ForegroundColor Cyan
$legalFiles = @(
    (Join-Path $repoRoot "web\public\legal\terms.html"),
    (Join-Path $repoRoot "web\public\legal\privacy.html"),
    (Join-Path $repoRoot "web\public\legal\dpa.html"),
    (Join-Path $repoRoot "web\public\help\report-a-problem.html")
)
foreach ($f in $legalFiles) {
    if (-not (Test-Path $f)) {
        $issues.Add("Missing required page: $f")
        continue
    }
    $matches = Select-String -Path $f -Pattern "\[[^\]]+\]" -AllMatches
    foreach ($m in $matches) {
        $tokens = ($m.Matches | ForEach-Object { $_.Value }) -join ", "
        $issues.Add("Unfilled placeholder in $(Split-Path -Leaf $f):$($m.LineNumber): $tokens")
    }
}

Write-Host "[2/3] Checking environment file ($envPath)..." -ForegroundColor Cyan
if (-not (Test-Path $envPath)) {
    $issues.Add("Environment file missing: $envPath (run with -InitEnv first)")
} else {
    $envMap = @{}
    foreach ($line in (Get-Content $envPath)) {
        $trimmed = $line.Trim()
        if ($trimmed -eq "" -or $trimmed.StartsWith("#")) { continue }
        $idx = $trimmed.IndexOf("=")
        if ($idx -gt 0) {
            $envMap[$trimmed.Substring(0, $idx).Trim()] = $trimmed.Substring($idx + 1).Trim()
        }
    }

    foreach ($reqKey in @("SITE_ADDRESS", "LETSENCRYPT_EMAIL", "POSTGRES_SUPERUSER_PASSWORD", "APP_DB_PASSWORD", "OWNER_DB_PASSWORD", "JWT_SIGNING_KEY", "BACKUP_ENCRYPTION_KEY")) {
        $val = $envMap[$reqKey]
        if ([string]::IsNullOrWhiteSpace($val) -or $val -like "CHANGE_ME*" -or $val -like "*YOUR_*") {
            $issues.Add("$reqKey in .env still uses a placeholder or is empty.")
        }
    }
    if ($envMap["JWT_SIGNING_KEY"] -and $envMap["JWT_SIGNING_KEY"].Length -lt 32) {
        $issues.Add("JWT_SIGNING_KEY must be at least 32 characters.")
    }
    if ($envMap["DOCUMENTS_STORAGE"] -eq "S3") {
        foreach ($s3k in @("DOCUMENTS_S3_ENDPOINT", "DOCUMENTS_S3_BUCKET", "DOCUMENTS_S3_ACCESS_KEY", "DOCUMENTS_S3_SECRET_KEY")) {
            if ([string]::IsNullOrWhiteSpace($envMap[$s3k]) -or $envMap[$s3k] -like "CHANGE_ME*") {
                $issues.Add("DOCUMENTS_STORAGE=S3 requires $s3k.")
            }
        }
    } else {
        $warnings.Add("DOCUMENTS_STORAGE is not S3; ensure the /documents volume is included in host backups.")
    }
    if ($envMap["EMAIL_MODE"] -eq "Smtp") {
        if ([string]::IsNullOrWhiteSpace($envMap["SMTP_HOST"]) -or [string]::IsNullOrWhiteSpace($envMap["EMAIL_FROM"]) -or $envMap["SMTP_PASSWORD"] -like "*YOUR_*") {
            $issues.Add("EMAIL_MODE=Smtp requires valid SMTP_HOST, EMAIL_FROM, and SMTP_PASSWORD.")
        }
    }
    if ([string]::IsNullOrWhiteSpace($envMap["BACKUP_S3_BUCKET"]) -and [string]::IsNullOrWhiteSpace($envMap["BACKUP_GCS_BUCKET"])) {
        $warnings.Add("Neither BACKUP_S3_BUCKET nor BACKUP_GCS_BUCKET is set; backups stay local to host volume only.")
    }
}

Write-Host "[3/3] Checking live endpoint health..." -ForegroundColor Cyan
if (-not [string]::IsNullOrWhiteSpace($SiteUrl)) {
    $base = $SiteUrl.TrimEnd("/")
    foreach ($ep in @("/health/live", "/health/ready", "/legal/terms.html", "/legal/privacy.html", "/legal/dpa.html", "/help/report-a-problem.html")) {
        try {
            $resp = Invoke-WebRequest -UseBasicParsing -Uri "$base$ep" -TimeoutSec 10
            if ($resp.StatusCode -lt 200 -or $resp.StatusCode -ge 300) {
                $issues.Add("GET $base$ep returned HTTP $($resp.StatusCode)")
            } else {
                Write-Host "  OK $ep -> HTTP $($resp.StatusCode)" -ForegroundColor Green
            }
        } catch {
            $issues.Add("GET $base$ep failed: $($_.Exception.Message)")
        }
    }
} else {
    $warnings.Add("Pass -SiteUrl https://your-domain to verify live /health/ready and published legal/help URLs.")
}

if ($warnings.Count -gt 0) {
    Write-Host "`nWarnings:" -ForegroundColor Yellow
    $warnings | ForEach-Object { Write-Host "  - $_" -ForegroundColor Yellow }
}
if ($issues.Count -gt 0) {
    Write-Host "`nPre-launch blockers found ($($issues.Count)):" -ForegroundColor Red
    $issues | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}

Write-Host "`nAll pre-launch checks passed!" -ForegroundColor Green

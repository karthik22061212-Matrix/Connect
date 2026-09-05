# ==============================================================================
# Development Environment Startup Script
# ==============================================================================

$ErrorActionPreference = "Stop"

$BackendDir = "$PSScriptRoot\..\backend\src\Connect.Api"
$FrontendDir = "$PSScriptRoot\..\frontend"
$DevConfig = "$BackendDir\appsettings.Development.json"
$DevExampleConfig = "$BackendDir\appsettings.Development.example.json"

Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host " Connect Development Environment" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan

# Ensure local dev config exists
if (-not (Test-Path $DevConfig)) {
    Write-Host "Local configuration not found. Initializing from template..." -ForegroundColor Yellow
    Copy-Item -Path $DevExampleConfig -Destination $DevConfig
    Write-Host "Created appsettings.Development.json. Please review it if needed." -ForegroundColor Green
} else {
    $configContent = Get-Content $DevConfig -Raw
    if ($configContent -match "5200" -or $configContent -match "3000") {
        Write-Host "ERROR: Your appsettings.Development.json contains obsolete development ports (5200 or 3000)." -ForegroundColor Red
        Write-Host "Please update it to match the approved architecture (e.g., using 8080 origins) or delete it to re-initialize from template." -ForegroundColor Yellow
        exit 1
    }
}

# ------------------------------------------------------------------------------
# Database Migrations Validation
# ------------------------------------------------------------------------------
Write-Host "`nChecking for pending EF Core migrations..." -ForegroundColor Cyan
$infrastructureDir = "$PSScriptRoot\..\backend\src\Connect.Infrastructure"
$apiDir = "$PSScriptRoot\..\backend\src\Connect.Api"

$migrations = dotnet ef migrations list --project $infrastructureDir --startup-project $apiDir 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Failed to list migrations. Ensure database is accessible." -ForegroundColor Red
    $migrations | ForEach-Object { Write-Host $_ -ForegroundColor Red }
    exit 1
}

$pendingMigrations = $migrations | Where-Object { $_ -match "\(Pending\)" }
if ($pendingMigrations) {
    Write-Host "Found pending migrations:" -ForegroundColor Yellow
    $pendingMigrations | ForEach-Object { Write-Host " - $_" -ForegroundColor Yellow }
    Write-Host "Applying migrations..." -ForegroundColor Cyan
    
    dotnet ef database update --project $infrastructureDir --startup-project $apiDir
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Database migration failed. Cannot start backend against a stale schema." -ForegroundColor Red
        exit 1
    }
    Write-Host "Migrations applied successfully." -ForegroundColor Green
} else {
    Write-Host "Database is up to date." -ForegroundColor Green
}

# Start Backend
Write-Host "`nStarting Backend on localhost:5234..." -ForegroundColor Cyan
Start-Process -NoNewWindow -FilePath "dotnet" -ArgumentList "run", "--project", $BackendDir, "--urls", "http://localhost:5234"

# Wait a moment for backend to initialize
Start-Sleep -Seconds 3

# Start Frontend
Write-Host "`nStarting Frontend on localhost:8080..." -ForegroundColor Cyan
Push-Location $FrontendDir
try {
    flutter run -d chrome --web-port=8080 --dart-define=API_BASE_URL=http://localhost:5234
} finally {
    Pop-Location
}

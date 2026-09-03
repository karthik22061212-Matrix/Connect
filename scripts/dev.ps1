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

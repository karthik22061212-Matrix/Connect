# ==============================================================================
# Production Deployment Script
# ==============================================================================

param(
    [string]$ResourceGroupName = "",
    [string]$ApiAppName = "",
    [string]$StaticWebAppName = "",
    [string]$ApiBaseUrl = "",
    [string]$EnvFile = "$PSScriptRoot\..\infra\azure-deployment-info.env"
)

$ErrorActionPreference = "Stop"

Write-Host "=====================================================" -ForegroundColor Cyan
Write-Host " Connect Production Deployment" -ForegroundColor Cyan
Write-Host "=====================================================" -ForegroundColor Cyan

# PHASE 1 — VALIDATION
Write-Host "`n[1/5] Validating prerequisites and environment..." -ForegroundColor Yellow

# Try to load defaults from env file if parameters are missing
if (Test-Path $EnvFile) {
    Write-Host "Found deployment info file at $EnvFile, loading defaults..." -ForegroundColor Gray
    $envContent = Get-Content $EnvFile
    foreach ($line in $envContent) {
        if ($line -match "^(AZURE_[^=]+)=(.*)$") {
            $key = $matches[1]
            $value = $matches[2]
            
            if ($key -eq "AZURE_RESOURCE_GROUP" -and [string]::IsNullOrEmpty($ResourceGroupName)) { $ResourceGroupName = $value }
            if ($key -eq "AZURE_APP_SERVICE_URL" -and [string]::IsNullOrEmpty($ApiBaseUrl)) { $ApiBaseUrl = $value }
            if ($key -eq "AZURE_STATIC_WEB_APP_NAME" -and [string]::IsNullOrEmpty($StaticWebAppName)) { $StaticWebAppName = $value }
            
            # The API app name can be extracted from the URL if not provided directly
            if ($key -eq "AZURE_APP_SERVICE_URL" -and [string]::IsNullOrEmpty($ApiAppName)) { 
                if ($value -match "https://([^.]+)\.azurewebsites\.net") {
                    $ApiAppName = $matches[1]
                }
            }
        }
    }
}

if ([string]::IsNullOrEmpty($ResourceGroupName) -or [string]::IsNullOrEmpty($ApiAppName) -or [string]::IsNullOrEmpty($StaticWebAppName) -or [string]::IsNullOrEmpty($ApiBaseUrl)) {
    Write-Host "ERROR: Missing required deployment parameters." -ForegroundColor Red
    Write-Host "Please provide them via parameters or ensure infra/azure-deployment-info.env exists." -ForegroundColor Red
    exit 1
}

Write-Host "Deployment Target:" -ForegroundColor Green
Write-Host " - Resource Group: $ResourceGroupName"
Write-Host " - Backend App:    $ApiAppName"
Write-Host " - Static Web App: $StaticWebAppName"
Write-Host " - API Base URL:   $ApiBaseUrl"

# Verify Azure CLI
if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: Azure CLI (az) is not found in PATH." -ForegroundColor Red
    exit 1
}

# Verify Azure login context
try {
    $null = az account show
} catch {
    Write-Host "ERROR: Not logged into Azure CLI. Please run 'az login' first." -ForegroundColor Red
    exit 1
}

# Verify npx is available
if (-not (Get-Command npx -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: npx is not found in PATH. Please install Node.js." -ForegroundColor Red
    exit 1
}

# Verify Flutter is available
if (-not (Get-Command flutter -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: flutter is not found in PATH." -ForegroundColor Red
    exit 1
}

# Verify .NET is available
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: dotnet is not found in PATH." -ForegroundColor Red
    exit 1
}

# Phase 1.5 - Validation of Azure Resources
Write-Host "Validating existence of Azure resources..." -ForegroundColor Gray
$rgExists = az group exists --name $ResourceGroupName
if ($rgExists -ne "true") {
    Write-Host "ERROR: Resource group $ResourceGroupName does not exist." -ForegroundColor Red
    exit 1
}

# Check if web app exists (this will fail if not found)
try {
    $null = az webapp show --name $ApiAppName --resource-group $ResourceGroupName -o none
} catch {
    Write-Host "ERROR: Backend App Service ($ApiAppName) does not exist in $ResourceGroupName." -ForegroundColor Red
    exit 1
}

# Check if static web app exists
try {
    $null = az staticwebapp show --name $StaticWebAppName --resource-group $ResourceGroupName -o none
} catch {
    Write-Host "ERROR: Static Web App ($StaticWebAppName) does not exist in $ResourceGroupName." -ForegroundColor Red
    exit 1
}

# PHASE 2 — BACKEND DEPLOYMENT
Write-Host "`n[2/5] Building and deploying Backend (.NET)..." -ForegroundColor Yellow

$BackendDir = "$PSScriptRoot\..\backend"
$PublishDir = "$BackendDir\publish"
$PublishZip = "$BackendDir\publish.zip"

if (Test-Path $PublishDir) { Remove-Item -Recurse -Force $PublishDir }
if (Test-Path $PublishZip) { Remove-Item -Force $PublishZip }

Write-Host "Publishing backend project..." -ForegroundColor Gray
$dotnetProc = Start-Process -FilePath "dotnet" -ArgumentList "publish", "$BackendDir\Connect.slnx", "-c", "Release", "-o", $PublishDir -NoNewWindow -Wait -PassThru
if ($dotnetProc.ExitCode -ne 0) {
    Write-Host "ERROR: Backend build failed." -ForegroundColor Red
    exit 1
}

Write-Host "Zipping backend artifacts..." -ForegroundColor Gray
Compress-Archive -Path "$PublishDir\*" -DestinationPath $PublishZip -Force

Write-Host "Deploying backend to Azure App Service ($ApiAppName)..." -ForegroundColor Gray
# We use zip deployment which is safe and doesn't overwrite secrets stored in App Settings
try {
    $null = az webapp deploy --resource-group $ResourceGroupName --name $ApiAppName --src-path $PublishZip --type zip
} catch {
    Write-Host "ERROR: Backend deployment failed." -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}

# PHASE 3 — FRONTEND BUILD
Write-Host "`n[3/5] Building Frontend (Flutter Web)..." -ForegroundColor Yellow

$FrontendDir = "$PSScriptRoot\..\frontend"
Push-Location $FrontendDir
try {
    Write-Host "Fetching Flutter dependencies..." -ForegroundColor Gray
    $flutterPubProc = Start-Process -FilePath "flutter" -ArgumentList "pub", "get" -NoNewWindow -Wait -PassThru
    if ($flutterPubProc.ExitCode -ne 0) {
        Write-Host "ERROR: Flutter pub get failed." -ForegroundColor Red
        exit 1
    }

    Write-Host "Building Flutter web with API_BASE_URL=$ApiBaseUrl..." -ForegroundColor Gray
    $flutterBuildProc = Start-Process -FilePath "flutter" -ArgumentList "build", "web", "--release", "--dart-define=API_BASE_URL=$ApiBaseUrl" -NoNewWindow -Wait -PassThru
    if ($flutterBuildProc.ExitCode -ne 0) {
        Write-Host "ERROR: Frontend build failed." -ForegroundColor Red
        exit 1
    }
} finally {
    Pop-Location
}

# PHASE 4 — STATIC WEB APP DEPLOYMENT
Write-Host "`n[4/5] Deploying Frontend to Azure Static Web Apps..." -ForegroundColor Yellow

Write-Host "Retrieving deployment token for Static Web App ($StaticWebAppName)..." -ForegroundColor Gray
# Retrieve token securely without logging it
$SwaToken = az staticwebapp secrets list --name $StaticWebAppName --resource-group $ResourceGroupName --query "properties.apiKey" -o tsv
if ([string]::IsNullOrWhiteSpace($SwaToken)) {
    Write-Host "ERROR: Failed to retrieve Static Web App deployment token." -ForegroundColor Red
    exit 1
}

Push-Location $FrontendDir
try {
    Write-Host "Deploying via SWA CLI..." -ForegroundColor Gray
    # Pass the token securely via environment variable
    $env:SWA_CLI_DEPLOYMENT_TOKEN = $SwaToken
    
    $npxProc = Start-Process -FilePath "npx.cmd" -ArgumentList "-y", "@azure/static-web-apps-cli", "deploy", "./build/web", "--env", "production" -NoNewWindow -Wait -PassThru
    if ($npxProc.ExitCode -ne 0) {
        Write-Host "ERROR: Static Web App deployment failed." -ForegroundColor Red
        exit 1
    }
} finally {
    $env:SWA_CLI_DEPLOYMENT_TOKEN = $null
    Pop-Location
}

# PHASE 5 — POST-DEPLOYMENT VALIDATION
Write-Host "`n[5/5] Post-Deployment Validation..." -ForegroundColor Yellow

Write-Host "Waiting 15 seconds for backend to start up..." -ForegroundColor Gray
Start-Sleep -Seconds 15

$HealthUrl = "$ApiBaseUrl/api/v1/health"
Write-Host "Pinging backend health endpoint ($HealthUrl)..." -ForegroundColor Gray
try {
    $HealthResponse = Invoke-RestMethod -Uri $HealthUrl -Method Get -TimeoutSec 30
    Write-Host "Backend is healthy!" -ForegroundColor Green
} catch {
    Write-Host "WARNING: Backend health check failed or timed out. Check Azure portal for logs." -ForegroundColor Yellow
    Write-Host $_.Exception.Message -ForegroundColor Yellow
}

Write-Host "Getting Frontend URL..." -ForegroundColor Gray
$FrontendUrlHostname = az staticwebapp show --name $StaticWebAppName --resource-group $ResourceGroupName --query "defaultHostname" -o tsv
if (-not [string]::IsNullOrWhiteSpace($FrontendUrlHostname)) {
    $FrontendUrl = "https://$FrontendUrlHostname"
    Write-Host "Pinging frontend URL ($FrontendUrl)..." -ForegroundColor Gray
    try {
        $null = Invoke-WebRequest -Uri $FrontendUrl -Method Get -TimeoutSec 15
        Write-Host "Frontend is reachable!" -ForegroundColor Green
    } catch {
        Write-Host "WARNING: Frontend reachability check failed." -ForegroundColor Yellow
        Write-Host $_.Exception.Message -ForegroundColor Yellow
    }
}

Write-Host "`n=====================================================" -ForegroundColor Cyan
Write-Host " Connect Production Deployment Completed Successfully!" -ForegroundColor Green
Write-Host "=====================================================" -ForegroundColor Cyan
